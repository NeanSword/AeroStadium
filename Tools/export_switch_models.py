"""Prepare local Switch-derived models for Unity, without publishing their payloads.

Run with Blender 4.5 in background mode:
  blender --background --factory-startup --python Tools/export_switch_models.py --

Only the prepared GLBs are read. Their original mesh, UVs and skin weights are
kept; all scaling/ground placement happens on the Unity prefab wrapper. Embedded
textures are extracted byte-for-byte rather than re-encoded by Blender.
"""

import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import struct
import sys

import bpy


MODELS = {
    152: {
        "name": "Germignon",
        "source": "germignon-switch-v2/germignon-switch-adapted.glb",
        "height": 0.9,
        "preview_idle": True,
        "limitations": [
            "The idle is an authored preview animation, not a source-game animation.",
            "Attack vines are absent from this prepared neutral GLB.",
            "Battle attacks and Stadium animation retargeting are not included.",
        ],
    },
    250: {
        "name": "Ho-Oh",
        "source": "ho-oh-switch-v2/ho-oh-switch-calibrated.glb",
        "height": 3.8,
        "preview_idle": False,
        "limitations": [
            "No animation clips were present in the source archive or prepared GLB.",
            "Runtime root motion can provide a hover; the skeleton is not animated.",
            "The prepared source uses a provisional wingspan calibration and rest pose.",
        ],
    },
}


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def safe_name(value):
    return re.sub(r"[^A-Za-z0-9_.-]+", "_", value).strip("._") or "texture"


def read_glb(path):
    data = path.read_bytes()
    magic, version, length = struct.unpack_from("<III", data)
    if magic != 0x46546C67 or version != 2 or length != len(data):
        raise ValueError(f"Invalid GLB: {path}")
    document = None
    binary = None
    offset = 12
    while offset < length:
        chunk_length, chunk_type = struct.unpack_from("<II", data, offset)
        payload = data[offset + 8:offset + 8 + chunk_length]
        if chunk_type == 0x4E4F534A:
            document = json.loads(payload)
        elif chunk_type == 0x004E4942:
            binary = payload
        offset += 8 + chunk_length
    if document is None or binary is None:
        raise ValueError("The prepared model must contain JSON and embedded binary.")
    return document, binary, sha256(data)


def extract_images(document, binary, output):
    image_files = {}
    images = []
    for index, definition in enumerate(document.get("images", [])):
        if "bufferView" not in definition:
            raise ValueError("Only embedded GLB textures are supported.")
        view = document["bufferViews"][definition["bufferView"]]
        if view.get("buffer", 0) != 0:
            raise ValueError("External texture buffers are not allowed.")
        start = view.get("byteOffset", 0)
        payload = binary[start:start + view["byteLength"]]
        mime = definition["mimeType"]
        extension = {"image/png": ".png", "image/jpeg": ".jpg"}.get(mime)
        if extension is None:
            raise ValueError(f"Unsupported texture format: {mime}")
        filename = safe_name(definition.get("name", f"image_{index}")) + extension
        target = output / filename
        target.write_bytes(payload)
        image_files[index] = filename
        size = [0, 0]
        if mime == "image/png":
            if payload[:8] != b"\x89PNG\r\n\x1a\n":
                raise ValueError(f"Invalid PNG: {filename}")
            size = list(struct.unpack_from(">II", payload, 16))
        images.append({"file": filename, "sha256": sha256(payload), "size": size})
    return image_files, images


def materials_manifest(document, image_files):
    entries = []
    for definition in document.get("materials", []):
        pbr = definition.get("pbrMetallicRoughness", {})
        albedo = pbr.get("baseColorTexture")
        if albedo is None:
            raise ValueError(f"No albedo texture for {definition['name']}")
        texture = document["textures"][albedo["index"]]
        if albedo.get("texCoord", 0) != 0 or "extensions" in albedo:
            raise ValueError("The prepared model must use untransformed UV0 for albedo.")
        entries.append({
            "name": definition["name"],
            "albedo": image_files[texture["source"]],
            "baseColor": pbr.get("baseColorFactor", [1.0, 1.0, 1.0, 1.0]),
            "roughness": pbr.get("roughnessFactor", 1.0),
            "doubleSided": definition.get("doubleSided", False),
            "alphaMode": definition.get("alphaMode", "OPAQUE"),
            "alphaCutoff": definition.get("alphaCutoff", 0.5),
        })
    return entries


def mesh_statistics():
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"
              and any(modifier.type == "ARMATURE" for modifier in obj.modifiers)]
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    triangles = vertices = weighted = 0
    uv_values = []
    weight_values = []
    points = []
    records = []
    for obj in meshes:
        mesh = obj.data
        mesh.calc_loop_triangles()
        triangles += len(mesh.loop_triangles)
        vertices += len(mesh.vertices)
        weighted += sum(any(group.weight > 0 for group in vertex.groups) for vertex in mesh.vertices)
        for vertex in mesh.vertices:
            weight_values.append(sorted((obj.vertex_groups[group.group].name, round(group.weight, 5))
                                        for group in vertex.groups if group.weight > 0))
        if mesh.uv_layers.active is None:
            raise ValueError(f"No UV0 layer: {obj.name}")
        for loop in mesh.uv_layers.active.data:
            if not all(math.isfinite(value) for value in loop.uv):
                raise ValueError(f"Non-finite UVs: {obj.name}")
            uv_values.append(tuple(round(value, 5) for value in loop.uv))
        for vertex in mesh.vertices:
            point = obj.matrix_world @ vertex.co
            if not all(math.isfinite(value) for value in point):
                raise ValueError(f"Non-finite geometry: {obj.name}")
            points.append(tuple(point))
        records.append({
            "name": obj.name,
            "vertices": len(mesh.vertices),
            "triangles": len(mesh.loop_triangles),
            "uvLayers": len(mesh.uv_layers),
            "materialSlots": [slot.material.name if slot.material else "" for slot in obj.material_slots],
            "weightedVertices": sum(bool(vertex.groups) for vertex in mesh.vertices),
        })
    if not points or not triangles or not armatures or weighted != vertices:
        raise ValueError("A complete, nonempty skinned model is required.")
    minimum = [min(point[axis] for point in points) for axis in range(3)]
    maximum = [max(point[axis] for point in points) for axis in range(3)]
    return {
        "meshCount": len(meshes), "vertexCount": vertices, "triangles": triangles,
        "weightedVertices": weighted,
        "boneCount": sum(len(obj.data.bones) for obj in armatures),
        "uvLoopCount": len(uv_values),
        "uvCoordinatesSha256": sha256(json.dumps(sorted(uv_values)).encode()),
        "skinWeightsSha256": sha256(json.dumps(sorted(weight_values)).encode()),
        "uvMinimum": [min(uv[axis] for uv in uv_values) for axis in range(2)],
        "uvMaximum": [max(uv[axis] for uv in uv_values) for axis in range(2)],
        "boundsMinimum": minimum, "boundsMaximum": maximum,
        "heightBlenderZ": maximum[2] - minimum[2],
        "meshes": records,
    }


def prepare_model(species, configuration, source_root, destination):
    source = source_root / configuration["source"]
    output = destination / str(species)
    output.mkdir(parents=True, exist_ok=True)
    document, binary, source_hash = read_glb(source)
    image_files, images = extract_images(document, binary, output)
    material_entries = materials_manifest(document, image_files)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(source), merge_vertices=False)
    if configuration["preview_idle"]:
        actions = [obj.animation_data.action for obj in bpy.context.scene.objects
                   if obj.animation_data and obj.animation_data.action]
        if not actions:
            raise ValueError("The expected authored preview idle is absent from the GLB.")
        bpy.context.scene.frame_start = math.floor(min(action.frame_range[0] for action in actions))
        bpy.context.scene.frame_end = math.ceil(max(action.frame_range[1] for action in actions))
    bpy.context.scene.frame_set(bpy.context.scene.frame_start)
    # glTF's importer creates an unskinned Icosphere used only as a bone widget.
    # It is not part of either GLB, and must never become gameplay geometry.
    for obj in list(bpy.context.scene.objects):
        if obj.type == "ARMATURE":
            for bone in obj.pose.bones:
                bone.custom_shape = None
        elif obj.type == "MESH" and not any(modifier.type == "ARMATURE" for modifier in obj.modifiers):
            bpy.data.objects.remove(obj, do_unlink=True)
            continue
        if obj.type == "MESH":
            for color_layer in obj.data.color_attributes:
                if any(abs(value - 1.0) > 0.0001 for color in color_layer.data for value in color.color):
                    raise ValueError("URP Lit albedo import requires neutral source vertex colours.")
    for definition in material_entries:
        material = bpy.data.materials.get(definition["name"])
        if material is None:
            raise ValueError(f"Material not imported: {definition['name']}")
        principled = next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
        color_socket = principled.inputs["Base Color"]
        image_node = next((node for node in material.node_tree.nodes
                           if node.type == "TEX_IMAGE" and node.image
                           and safe_name(node.image.name) == Path(definition["albedo"]).stem), None)
        if image_node is None:
            raise ValueError(f"Unexpected albedo node: {material.name}")
        image_node.image = bpy.data.images.load(str(output / definition["albedo"]), check_existing=True)
        image_node.image.colorspace_settings.name = "sRGB"
        image_node.extension = "REPEAT"
        # Neutral vertex colors add a Mix node that FBX's simple material parser
        # cannot follow. This direct link is equivalent to multiplying by white.
        material.node_tree.links.new(image_node.outputs["Color"], color_socket)
    # Give every texture node a real local path. FBX references these paths;
    # embedding or COPY mode can otherwise fail on imported packed GLB images.
    named_images = {entry.get("name", f"image_{i}"): image_files[i]
                    for i, entry in enumerate(document.get("images", []))}
    for material in bpy.data.materials:
        if material.node_tree:
            for node in material.node_tree.nodes:
                if node.type == "TEX_IMAGE" and node.image and node.image.name in named_images:
                    node.image = bpy.data.images.load(str(output / named_images[node.image.name]), check_existing=True)
    before = mesh_statistics()
    fbx = output / "Model.fbx"
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=str(fbx), use_selection=True, object_types={"MESH", "ARMATURE", "EMPTY"},
        axis_forward="-Z", axis_up="Y", global_scale=1.0, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS", use_mesh_modifiers=False,
        mesh_smooth_type="OFF", use_tspace=True, use_triangles=False,
        add_leaf_bones=False, use_armature_deform_only=False,
        bake_anim=configuration["preview_idle"], bake_anim_use_all_actions=False,
        # glTF imports an active action plus a muted backup NLA strip. Export the
        # active scene action; enabling NLA export silently discards that idle.
        bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0.0,
        path_mode="RELATIVE", embed_textures=False,
    )
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx), use_anim=True, use_custom_normals=True)
    exported_actions = [{"name": action.name, "frameRange": list(action.frame_range)}
                        for action in bpy.data.actions]
    if configuration["preview_idle"] and not exported_actions:
        raise ValueError("FBX export lost the authored preview idle.")
    after = mesh_statistics()
    invariant_fields = ["meshCount", "vertexCount", "triangles", "weightedVertices", "uvLoopCount", "uvCoordinatesSha256", "skinWeightsSha256"]
    differences = {key: [before[key], after[key]] for key in invariant_fields if before[key] != after[key]}
    if differences:
        raise ValueError(f"FBX round-trip changed geometry or UVs: {differences}")
    if abs(before["heightBlenderZ"] - after["heightBlenderZ"]) > before["heightBlenderZ"] * 0.0001:
        raise ValueError("FBX round-trip changed the source height.")
    source_manifest_path = source.parent / "manifest.json"
    provenance = json.loads(source_manifest_path.read_text(encoding="utf-8"))
    manifest = {
        "schemaVersion": 1, "species": species, "name": configuration["name"],
        "modelFile": fbx.name, "targetHeight": configuration["height"],
        "sourceGame": provenance.get("source_game", "Pokemon Scarlet/Violet (Switch)"),
        "sourcePage": provenance.get("source_page", ""),
        "preparedGlbSha256": source_hash, "fbxSha256": sha256(fbx.read_bytes()),
        "sourceAnimations": [], "previewIdle": configuration["preview_idle"],
        "animationClips": [animation.get("name", "") for animation in document.get("animations", [])],
        "fbxAnimationTakes": exported_actions,
        "materials": material_entries, "images": images,
        "geometry": before, "fbxRoundTrip": after,
        "validation": {"topologyPreserved": True, "uvCoordinatesPreserved": True,
                       "allVerticesSkinned": True, "embeddedTexturesCopiedExactly": True,
                       "unityPrefabVerified": False},
        "limitations": configuration["limitations"],
        "distribution": "Local development payload; excluded from Git. No ROM is copied.",
    }
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Prepared {species} {configuration['name']}: {before['triangles']} triangles, "
          f"{before['vertexCount']} weighted vertices, {len(images)} exact textures.", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--workspace", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--project", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--species", nargs="*", type=int, default=list(MODELS))
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    source_root = args.workspace / "graphics-workbench/local/pokemon-models"
    destination = args.project / "Assets/AeroStadium/Resources/LocalModels"
    for species in args.species:
        if species not in MODELS:
            raise ValueError(f"No prepared source configured for species {species}")
        prepare_model(species, MODELS[species], source_root, destination)


if __name__ == "__main__":
    main()
