#!/usr/bin/env python3
"""Prepare the local, non-redistributed Generation I model set for AeroStadium."""

from __future__ import annotations

import concurrent.futures
import hashlib
import json
import shutil
import struct
import subprocess
import time
import urllib.error
import urllib.request
from pathlib import Path

MODEL_BASE = "https://raw.githubusercontent.com/Pokemon-3D-api/assets/main/models/opt/regular"
POKEAPI_BASE = "https://pokeapi.co/api/v2"
MODEL_SOURCE = "https://github.com/Pokemon-3D-api/assets/tree/main/models/opt/regular"
USER_AGENT = "AeroStadium-local-gen1-import/1.0"


def request_bytes(url: str) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    last_error = None
    for attempt in range(6):
        try:
            with urllib.request.urlopen(request, timeout=45) as response:
                return response.read()
        except (urllib.error.URLError, TimeoutError) as error:
            last_error = error
            time.sleep(min(2 ** attempt, 20))
    raise RuntimeError(f"Unable to fetch {url}: {last_error}")


def request_json(url: str, cache_path: Path) -> dict:
    if cache_path.is_file():
        try:
            return json.loads(cache_path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            cache_path.unlink(missing_ok=True)
    payload = json.loads(request_bytes(url))
    cache_path.parent.mkdir(parents=True, exist_ok=True)
    cache_path.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")
    return payload


def valid_glb(payload: bytes) -> bool:
    return len(payload) >= 20 and struct.unpack_from("<III", payload) == (
        0x46546C67, 2, len(payload)
    )


def glb_asset_metadata(path: Path) -> dict:
    payload = path.read_bytes()
    offset = 12
    while offset + 8 <= len(payload):
        chunk_length, chunk_type = struct.unpack_from("<II", payload, offset)
        offset += 8
        chunk = payload[offset:offset + chunk_length]
        if chunk_type == 0x4E4F534A:
            gltf = json.loads(chunk.decode("utf-8").rstrip("\x00 \t\r\n"))
            extras = gltf.get("asset", {}).get("extras", {})
            return extras if isinstance(extras, dict) else {}
        offset += chunk_length
    return {}


def fetch_model(species_id: int, source_root: Path) -> tuple[int, Path, int, str]:
    target = source_root / f"{species_id}.glb"
    payload = target.read_bytes() if target.is_file() else b""
    if not valid_glb(payload):
        payload = request_bytes(f"{MODEL_BASE}/{species_id}.glb")
        if not valid_glb(payload):
            raise ValueError(f"Invalid GLB received for #{species_id:03d}")
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(payload)
    return species_id, target, len(payload), hashlib.sha256(payload).hexdigest()


def load_pokemon(species_id: int, cache_root: Path) -> tuple[int, dict, dict]:
    pokemon = request_json(
        f"{POKEAPI_BASE}/pokemon/{species_id}",
        cache_root / "pokemon" / f"{species_id}.json",
    )
    species = request_json(
        f"{POKEAPI_BASE}/pokemon-species/{species_id}",
        cache_root / "species" / f"{species_id}.json",
    )
    return species_id, pokemon, species


def local_name(records: list[dict], language: str, fallback: str) -> str:
    return next((entry["name"] for entry in records
                 if entry.get("language", {}).get("name") == language), fallback)


def move_id_from_url(url: str) -> int:
    return int(url.rstrip("/").split("/")[-1])


def type_title(name: str) -> str:
    return "-".join(part.capitalize() for part in name.split("-"))


def main() -> None:
    project = Path(__file__).resolve().parents[1]
    source_root = project / "LocalModelSources" / "Pokemon3D" / "gen1"
    cache_root = project / "LocalModelSources" / "PokedexData"
    resources_root = project / "Assets" / "AeroStadium" / "Resources"
    models_root = resources_root / "LocalModels"
    catalog_path = resources_root / "Data" / "catalog.json"

    ids = list(range(1, 152))
    print("Checking and downloading the 151 standard GLB models...", flush=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
        model_results = list(pool.map(lambda value: fetch_model(value, source_root), ids))
    model_metadata = {
        species_id: glb_asset_metadata(path)
        for species_id, path, _, _ in model_results
    }

    print("Loading species, height and Red/Blue learnset data from PokéAPI...", flush=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        pokemon_results = list(pool.map(lambda value: load_pokemon(value, cache_root), ids))
    pokemon_by_id = {value: pokemon for value, pokemon, _ in pokemon_results}
    species_by_id = {value: species for value, _, species in pokemon_results}

    candidates_by_species: dict[int, dict[int, dict]] = {}
    move_urls: dict[int, str] = {}
    for species_id, pokemon in pokemon_by_id.items():
        candidates: dict[int, dict] = {}
        for move in pokemon.get("moves", []):
            move_id = move_id_from_url(move["move"]["url"])
            for detail in move.get("version_group_details", []):
                if detail.get("version_group", {}).get("name") != "red-blue":
                    continue
                method = detail.get("move_learn_method", {}).get("name")
                if method not in {"level-up", "machine"}:
                    continue
                move_urls[move_id] = move["move"]["url"]
                candidate = candidates.setdefault(
                    move_id, {"level": 999, "machine": False}
                )
                if method == "level-up":
                    candidate["level"] = min(
                        candidate["level"],
                        detail.get("level_learned_at", 999),
                    )
                else:
                    candidate["machine"] = True
        candidates_by_species[species_id] = candidates

    # Struggle is the safe fallback if an unusual learnset has no damaging move.
    move_urls.setdefault(165, f"{POKEAPI_BASE}/move/165/")
    print(f"Reading {len(move_urls)} distinct Generation I move records...", flush=True)
    def load_move(entry: tuple[int, str]) -> tuple[int, dict]:
        move_id, url = entry
        return move_id, request_json(
            url, cache_root / "moves" / f"{move_id}.json"
        )

    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        move_records = dict(pool.map(load_move, move_urls.items()))

    definitions: dict[int, dict] = {}
    damaging: dict[int, dict] = {}
    for move_id, move in move_records.items():
        power = move.get("power")
        category = move.get("damage_class", {}).get("name", "status")
        if power is None or power <= 0 or category == "status":
            continue
        definition = {
            "id": move_id,
            "name": local_name(move.get("names", []), "fr", move["name"]),
            "type": type_title(move["type"]["name"]),
            "category": type_title(category),
            "power": int(power),
            "accuracy": int(move.get("accuracy") or 100),
            "pp": int(move.get("pp") or 1),
            "priority": int(move.get("priority") or 0),
            "effect": "Damage",
        }
        damaging[move_id] = definition

    species_definitions = []
    assigned_moves: set[int] = set()
    for species_id in ids:
        pokemon = pokemon_by_id[species_id]
        species = species_by_id[species_id]
        choices = candidates_by_species[species_id]
        # Prefer damaging moves available by level 50; fill from Red/Blue TMs.
        level_moves = sorted(
            ((info["level"], move_id) for move_id, info in choices.items()
             if 1 <= info["level"] <= 50),
            reverse=True,
        )
        machine_moves = [
            move_id for move_id, info in choices.items() if info["machine"]
        ]
        candidates = []
        for _, move_id in level_moves:
            if move_id in damaging and move_id not in candidates:
                candidates.append(move_id)
        machine_moves.sort(
            key=lambda move_id: (
                damaging.get(move_id, {}).get("power", 0),
                damaging.get(move_id, {}).get("accuracy", 0),
            ),
            reverse=True,
        )
        for move_id in machine_moves:
            if move_id in damaging and move_id not in candidates:
                candidates.append(move_id)
        if not candidates:
            candidates = [165]
        selected_moves = candidates[:4]
        assigned_moves.update(selected_moves)

        stats = {entry["stat"]["name"]: entry["base_stat"]
                 for entry in pokemon["stats"]}
        french_name = local_name(species.get("names", []), "fr", pokemon["name"])
        species_definitions.append({
            "id": species_id,
            "name": french_name,
            "types": [type_title(entry["type"]["name"])
                      for entry in sorted(pokemon["types"], key=lambda item: item["slot"])],
            "stats": {
                "hp": stats["hp"],
                "attack": stats["attack"],
                "defense": stats["defense"],
                "specialAttack": stats["special-attack"],
                "specialDefense": stats["special-defense"],
                "speed": stats["speed"],
            },
            "moves": selected_moves,
            "color": species.get("color", {}).get("name", ""),
            "height": pokemon["height"] / 10.0,
        })

    old_catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    catalog = {
        "species": species_definitions,
        "moves": [damaging[move_id] for move_id in sorted(assigned_moves)],
        "items": old_catalog.get("items", []),
    }
    catalog_path.write_text(
        json.dumps(catalog, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    attribution_lines = [
        "# Attributions des modèles Pokémon de génération I",
        "",
        "Les modèles et textures sont téléchargés et utilisés localement ; aucun payload 3D n'est inclus dans Git. Les noms, designs et droits Pokémon restent ceux de leurs ayants droit.",
        "Les crédits ci-dessous sont repris des métadonnées `asset.extras` présentes dans les GLB sources. Les fichiers sans métadonnées individuelles restent attribués au dépôt [Pokemon-3D-api/assets](https://github.com/Pokemon-3D-api/assets/tree/main/models/opt/regular).",
        "",
        "| # | Pokémon | Auteur indiqué | Licence indiquée | Modèle source |",
        "| ---: | --- | --- | --- | --- |",
    ]
    attribution_count = 0
    for species_id in ids:
        metadata = model_metadata[species_id]
        if not any(metadata.get(key) for key in ("author", "license", "source")):
            continue
        attribution_count += 1
        name = species_definitions[species_id - 1]["name"].replace("|", "\\|")
        author = str(metadata.get("author", "")).replace("|", "\\|")
        license_name = str(metadata.get("license", "")).replace("|", "\\|")
        source_url = str(metadata.get("source", ""))
        source_link = f"[page source]({source_url})" if source_url else ""
        attribution_lines.append(
            f"| {species_id:03d} | {name} | {author} | {license_name} | {source_link} |"
        )
    attribution_lines.extend([
        "",
        "Certaines métadonnées déclarent CC BY-NC 4.0 ou CC BY-SA 4.0. Le dépôt ne fournit pas les fichiers 3D ; garde les modèles et leurs textures dans ton installation locale et respecte les conditions affichées pour chaque fichier avant toute redistribution ou usage commercial.",
        "",
    ])
    (project / "docs" / "GEN1_MODEL_ATTRIBUTIONS.md").write_text(
        "\n".join(attribution_lines), encoding="utf-8"
    )

    print("Preparing local Resources payloads...", flush=True)
    hashes = {species_id: (size, digest)
              for species_id, _, size, digest in model_results}
    for species_id in ids:
        output = models_root / str(species_id)
        if output.exists():
            for child in output.iterdir():
                if child.is_dir():
                    shutil.rmtree(child)
                else:
                    child.unlink()
        else:
            output.mkdir(parents=True)
        source = source_root / f"{species_id}.glb"
        shutil.copy2(source, output / "Pokemon.glb")
        pokemon = pokemon_by_id[species_id]
        species = species_by_id[species_id]
        french_name = local_name(species.get("names", []), "fr", pokemon["name"])
        manifest = {
            "schemaVersion": 1,
            "species": species_id,
            "generation": 1,
            "name": french_name,
            "modelFile": "Pokemon.glb",
            "targetHeight": pokemon["height"] / 10.0,
            "sourceGame": "Pokémon 3D API optimized regular model",
            "sourcePage": f"{MODEL_SOURCE}/{species_id}.glb",
            "sourceAssetSha256": hashes[species_id][1],
            "assetAttribution": model_metadata[species_id],
            "previewIdle": False,
            "materials": [],
            "distribution": "Local development payload; excluded from Git. Model rights remain with their owners.",
        }
        (output / "manifest.json").write_text(
            json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )

    for child in models_root.iterdir():
        if child.is_dir() and child.name.isdigit():
            species_id = int(child.name)
            if species_id not in ids:
                shutil.rmtree(child)
                meta = child.with_suffix(child.suffix + ".meta")
                meta.unlink(missing_ok=True)

    node = shutil.which("node")
    if node is None:
        raise RuntimeError("Node.js is required to normalize the GLB models. Install Node.js and the Tools/glb-compat dependencies.")
    converter = project / "Tools" / "glb-compat" / "convert.mjs"
    print("Normalizing models for Unity (Draco/WebP and sparse-accessor conversion)...", flush=True)
    subprocess.run(
        [node, str(converter), "--batch", str(source_root), str(models_root)],
        cwd=project,
        check=True,
    )
    for species_id in ids:
        output = models_root / str(species_id)
        model_path = output / "Pokemon.glb"
        manifest_path = output / "manifest.json"
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        prepared = model_path.read_bytes()
        manifest["preparedAssetSha256"] = hashlib.sha256(prepared).hexdigest()
        manifest["preparedBytes"] = len(prepared)
        manifest_path.write_text(
            json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )

    print(
        f"Prepared and Unity-normalized {len(ids)} models, "
        f"{len(species_definitions)} species, {len(assigned_moves)} moves and "
        f"{attribution_count} source attributions."
    )
    print(f"Assigned move sets: {min(len(row['moves']) for row in species_definitions)}–"
          f"{max(len(row['moves']) for row in species_definitions)} moves per species.")
    print("Non-Generation-I numeric model folders were removed from Resources; source archives remain untouched.")


if __name__ == "__main__":
    main()
