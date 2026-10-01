# Generation I model set

The local game payload contains the 151 standard National Pokédex forms numbered 001–151. Shiny variants are not used. Numeric model folders above 151 are removed from `Assets/AeroStadium/Resources/LocalModels`; downloaded originals and prior source archives remain in the ignored `LocalModelSources` folder.

From the project root, prepare the species catalog and models with:

```powershell
.\Tools\prepare_generation_one.ps1
.\Tools\build.ps1 -Action Prepare
```

The first script pins and installs the Node conversion tools, downloads or reuses local source GLBs, reads French names, types, heights, stats and the original Red/Blue learnsets from PokéAPI, then converts all models to Unity-compatible GLBs. Conversion expands Draco geometry, turns WebP textures into PNG, and writes sparse vertex accessors as regular buffer views for Unity's importer. It records source and prepared SHA-256 hashes in each local manifest. The second script imports every model and creates a ground-pivoted prefab sized from PokéAPI's official species height.

The original downloads, converted GLBs, generated prefabs and PokéAPI cache stay local and are excluded from Git. The tracked project contains only the reproducible preparation tools, catalog data and manifests' schema. No model payload or texture should be committed or redistributed. Model rights remain with Nintendo, Creatures Inc. and Game Freak; the code license in the source repository does not grant rights to its Pokémon models.

Source for the 151 standard model files: [Pokemon-3D-api/assets](https://github.com/Pokemon-3D-api/assets/tree/main/models/opt/regular). The project uses the regular files named `1.glb` through `151.glb`; it does not use shiny assets. Species data is obtained from [PokéAPI](https://pokeapi.co/).
