# Stadium 2 environments (local)

30 NP3F battle-field records recovered at archive 0x01638000. ROM is unchanged.
Source decoder: Deftones565/gen1recomp-mod-stadium2-importer, commit 360da5c636fc75971ee3df97e271d98b176ea825.
16 names independently matched against public Models Resource geometry; indices14–26 and29 retain explicit index names.
LocalStadiumSources preserves raw geometry, textures, native material metadata and provenance. Resources/Stadiums contains derived Unity meshes/materials.

Modern pass: reconstructed source colors with mipmaps and anisotropic filtering, luminance-derived relief, URP lighting/shadows, original geometry and proportions. Free Battle gains rounded metal railings and perimeter lighting.
This is a first material/lighting pass; resampling does not invent missing HD detail. Native combiner/animated textures and environment-specific animations are retained as source metadata, not fully emulated.

`--stadium KEY` selects a recovered environment; default `free_battle`.
`--stadium-review` checks all30 with two animated Pokemon, then closes automatically.
Build uses `--reuse-prepared-assets --prepare-stadiums` on ProjectBuild.BuildWindows.

Assets, source ROM, raw source bundles and reports remain local and ignored by Git.
