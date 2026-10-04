# Local smoke textures

`generate_smoke_atlas.py` deterministically generates the presentation textures
used by Smogo (#109) and Smogogo (#110). It uses NumPy and Pillow locally; it
does not access the network or any image-generation service.

From the project root:

```powershell
python Tools/Smoke/generate_smoke_atlas.py Assets/AeroStadium/Resources/NativeModels/SharedSmoke
```

The PNGs stay in the ignored native-model directory. The importer expects
`NativeSmokeAtlas.png` (1024 px, Clamp) and `NativeSmokeFlow.png` (256 px, Repeat).
Both are linear, bilinear, uncompressed and mipmapped. Atlas RGBA stores density,
projected normal XY, and self-shadow; its alpha channel is data rather than
transparency. The shader derives transparency from density.

The 16 connected billows share zero-density tile rims. Bounded continuous UV
advection produces evolving folds without a flipbook frame transition. Native
seed bones, visibility and encoded sizes remain active. The renderer is an
AeroStadium adaptation, rather than a reproduction of the source game's shader.

NativePersistentSmoke emits from every audited body chimney: 22 on #109,
21 on #110 (14 on its large head, 7 on its small head). NativeSmokeVentLayout
stores distal cap coordinates from the native tip triangle fans/inverse bind
matrices, mirrored once for glTFast. Each point follows its own wart bone;
facial jaw appendages are excluded. Validation independently compares vent
positions with skinned body vertices to detect incorrect coordinate conversion.

Each chimney emits 0.8 small clouds/second. A bounded pool of 96 holds about
76-80 live clouds: a 3-second density plateau then a smooth 1.5-second fade.
Material opacity is 0.72. Clouds first drift outward, then rise and expand;
their birth radius is 3.6-4.4% of model height. Lifetime alpha uses UV5 and a
stable cloud identity uses UV4.A; native seed cards without UV5 retain their
former shader behavior.

The authored pool emits while the body is visible, adapting the source's
intermittent smoke flags. Original SmokeGeom cards use forceRenderingOff while
the pool runs; prior flags are restored on disable. Body animations and visibility
are preserved. A single dynamic mesh and material are reused outside ModelRoot,
so native body size/grounding measurements exclude these presentation clouds.
Reprepare both prefabs after changing emitter setup.

Fantominus uses the separate NativeGasSurface shader; its opacity is 0.85 and
its source purple RGB mixing remains unchanged.
