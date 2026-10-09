# Local Stadium reference and modern presentation

48 native archive environments: 30 Stadium2, 18 Stadium1. These are records, not 48 distinct playable stadiums. Sources and recovered assets remain local and ignored.

Comparison build: Builds/WindowsReference/AeroStadium.exe --stadium-original --stadium-review. The 2026-10-09 review loaded 48 environments and two animated Pokemon, with zero technical errors and exit0. This does not establish pixel-exact or artistic fidelity.

NativeN64Sampler.hlsl implements integer clamp/mirror/mask addressing. F5/F2 metadata now covers all 534 textured Stadium2 primary bindings and 81 secondary bindings, including source-proved callback frame-zero descriptors. Nearest filtering remains intentional; N64 three-point filtering and full RDP coverage/depth are pending. Runtime texture scrolling and callback0x81000150 remain incomplete.

The shader reconstructs I4/I8 alpha from encoded intensity before combining; IA/RGBA retain independent alpha. Original PNGs are unchanged. Stadium1 applies the source selector4 combiner to 36 proven layer5 groups in 12 environments, using two cycles, source primitive colours and default LOD1. Other selectors, explicit FC overrides and inherited root render/lighting states remain to recover. The floor marking in arena19/21 is not yet visually complete.

Battle orientation uses verified rectangular floor geometry for eight records: arena_14, arena_19, arena_21, cianwood_gym, stadium1_brock, stadium1_blaine, stadium1_giovanni and stadium1_surge. Local Resources/Stadiums/battle-layouts.json stores raw centres, long axes and floor limits. Native battle anchors/camera paths remain unverified; other records retain their previous placement.

Latest backups and evidence: LocalStadiumSources/NativeMaterialFix_20261009. Stable Builds/Windows preserved. Build comparison with ProjectBuild.BuildWindows --reuse-prepared-assets --prepare-stadiums --stadium-only-validation --stadium-reference-build. --stadium KEY selects a stage. See LastProgressLogs.md for provenance, observations and the remaining fidelity work. Modern remake begins after historical-reference validation.
