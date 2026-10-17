# Grass attack presentation contract

`GrassAttackEffect` presents Fouet Lianes (22) and Tranch'Herbe (75). The caller owns battle resolution, native body animation, original sound, a single victim reaction and cleanup. See [VINE_WHIP.md](VINE_WHIP.md) for the current 2.50-second whip sequence and [RAZOR_LEAF.md](RAZOR_LEAF.md) for the 2.15-second leaf sequence.

## Integration

Create the effect with world-space source/aim and width; set its secondary emitter, victim bounds and floor. Bind the victim with `BindWhipVictim` or `BindLeafVictim`, then bind live native roots with the matching roots method. Bulbizarre uses `trLFeelerC01` and `trRFeelerC01`; leaves retain an upward offset of 12% of actor height above those roots. The body-side fallback for other anatomy is not an audited species attachment.

Both attacks advance in `LateUpdate` at execution order 100, after Animator evaluates. Roots and body contact therefore use the current animated pose. The effect publishes its progress to the action director after geometry advances. Move 22 stays rooted; move 75 captures each leaf's origin, aim and curved trajectory at emission. Updating endpoints cannot steer an emitted leaf.

Body contact uses `AnimatedSurfaceContact`: actual animated triangles from reusable baked skinned meshes, plus readable visible static meshes. Renderer bounds are broad-phase rejection only. Neither bounds nor an ellipsoid can create a successful body contact; a failed triangle query remains a miss. The helper owns reusable snapshots and buffers; disposing the effect releases them.

`SetShieldSurface` supplies Abri's dedicated ellipsoid and floor. Its expanded sweep and clipping keep the grass outside the shell. Body and shield responses differ: a leaf cuts into finite shaped fragments at a body contact, while the shield reflects the whole leaf into damped falling motion. Shield contact does not authorize a damage pose.

## Contacts and diagnostics

Each vine or leaf can register one contact. `ActualContacts` includes body and shield hits; source and shield counters distinguish them. Several leaves may physically hit, while the adapter plays one body reaction. `ContactPoint`/`ContactNormal` are the latest observed contact; `FirstBodyContactPoint`, `FirstBodyContactNormal` and `FirstBodyContactSeconds` retain the first observed body event. Those fields do not claim an exhaustive earliest collision across every leaf surface.

Contact display age starts on the frame that detects it, so a slow frame cannot consume the entire visible contact before rendering. `ImpactOpacity` describes body cutting glints; `ContactOpacity` includes the shield response. `Anchored`, `RetractionComplete` and whip motion measurements apply to 22; `DetachedBeforeImpact` applies to 75.

For resolved misses on 75, keep the original aim locked and set `ImpactEnabled=false`, `VerifyAvoidance=true`. Leading-tip trajectories still query the moving victim's actual triangles. `MissSurfaceQueries` records attempted checks and `MissSurfaceCrossings` records any detected crossing without creating a hit or reaction. Completion fails if a resolved miss crosses the body. Immunity does not enable this dodge-specific check.

`LastGrass` reports emissions, actual contacts, reactions, shield blocks, mesh contacts, baked frames, triangle tests, a successful contact mesh, maximum successful hit-plane error, rendered contact gap and miss queries/crossings. No-hit queries cannot erase successful mesh evidence. The battle result alone cannot manufacture these counts.

## Budget and validation limits

Three dynamic effect meshes and three owned materials contain 4,224 preallocated triangles: 1,728 vine triangles, 1,920 leaf/fragment triangles and 576 cutting-glint triangles. Effect updates create no managed arrays or actor clones. Animated-surface snapshots add one reusable mesh per skinned victim renderer; they do not create colliders. `OnDestroy` releases effect meshes, materials and surface snapshots; completed renderers disable.

`Validate` checks finite geometry, resources, accounting, vine anchoring and visible shield penetration. Geometry failures remain latched. The shader also clips shield interiors between vertices.

The 2026-10-17 build and bounded runtime/visual review passed for75; see [RAZOR_LEAF.md](RAZOR_LEAF.md). The same build passed the22 regression. Evidence covers native Bulbizarre1 against Tortank9 in the modern Elite Four arena, not all-species anatomy or whole-blade continuous collision detection. Original PCM is measured; no subjective loudspeaker listening is claimed.
