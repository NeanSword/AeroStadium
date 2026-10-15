# GrassAttackEffect staged integration

These are staged source artifacts only. No repository edits, Unity import, build, play session, shader compilation or visual/runtime validation was performed by this effects task.

Copy `GrassAttackEffect.cs` and its meta into `Assets/AeroStadium/Presentation/`; `GrassAttack.shader` and its meta into `Assets/AeroStadium/Shaders/`; `GrassAttackReference.mat` and its meta into `Assets/AeroStadium/Resources/Materials/`. The material shader GUID is already linked and its Resources reference protects the shader from stripping. No textures, paid services or external assets are needed.

## Caller contract

`GrassAttackEffect.Create(parent, primaryPlantOriginWorld, targetWorld, width, moveId)` accepts only 22 or 75. Immediately provide `SetSecondaryEmitter(secondaryPlantOriginWorld)`, `SetVictimBounds(victimWorldBounds)` and `ImpactEnabled = hitAllowed`. Update both origins using `SetEndpoints` and `SetSecondaryEmitter` as the actor poses. Use the actual Bulbizarre `trLFeelerC01` and `trRFeelerC01` vinebase bones reported by the integration owner; this effect performs no species/head guessing. Move 22 remains connected to the live roots; move 75 captures each origin, target and trajectory independently on emission. Existing leaves cannot be steered by subsequent poses.

For Protect, call `SetShieldSurface(actualEllipsoidWorldBounds, floorWorldY)` before emission and update it while the shell moves. This API enables shield collisions and clipping itself. Set `ImpactEnabled = false` to suppress victim hits when a shield/miss/immunity applies; shield collision remains physical. There is deliberately no fallback target-plane hit and no fabricated contact from a timer. Without valid victim bounds or a valid shield surface no actual contacts can be recorded. Caller owns the damage result, one victim reaction, body animation, SFX and disposal.

## Timing

Move 22: total 1.68 s; gather/coiling 0.025–0.22; extension 0.22–0.68; first contact depends on real bounds, usually 0.58–0.68 for a normal separated matchup; short anchored rebound after each hit; retract 0.86–1.43; at 1.43 all tube vertices collapse to their respective live origin. No leaf/vegetation fragments detach from these whips. The final 0.25 s permits caller pose settlement.

Move 75: total 2.15 s; 12 leaves emit at 0.14 + index×0.026 s; captured flight durations 0.48–0.57 s, with contacts naturally occurring at the swept leading edge before the target center time. Each independent green leaf has a modeled curved spine, alternating lobes, folded faces, midrib and secondary veins; it spins along a curved fan. Body contacts generate three finite shaped fragments and narrow cutting glints. Shield contacts reflect whole leaves into the front/side halfspace, damp the reflected motion, then let them fall and fade; they do not enter the shield or hit the victim. No shield fragments are emitted.

Use `ImpactReached` for the first physical contact and `ContactPoint`/`ContactNormal` for the latest physical contact; do not schedule damage from the approximate timing. `ImpactOpacity` is body contact energy only; `ContactOpacity` includes shield impacts. `ExpectedEmissionCount` is 2 or 12. `ActualContacts` includes only swept body/shield contacts and cannot exceed emitted actors; `BodyContacts`, primary/secondary emitter and shield counts, and `TwoSourceContacts` distinguish the result. One vine/leaf can count one contact maximum. `ProjectileDetached`/`DetachedBeforeImpact` apply only to move 75. `Anchored`, `MaximumAnchorError`, `RetractionComplete` apply to move 22 and measure rendered geometry. `EmissionCount`, `PeakParticles`, `EffectProgress`, `Completed` and `IsPlaying` are diagnostic readouts.

## Budgets and validation limits

Three dynamic meshes, three owned materials, 3,792 preallocated triangles; no per-frame managed array creation, particle prefabs, textures, physics allocations or dependency additions. Meshes/materials are destroyed in `OnDestroy`; final renderers disable at `Completed`. `Validate(out string)` checks resources, finite geometry, trajectory data and contact accounting. Playback also latches nonfinite geometry, visible vertices inside the actual shield ellipsoid, or a disconnected vine root, so a clean final frame cannot erase an earlier geometry failure. The shader adds interior fragment clipping for triangle chords between surface-conforming vertices.

This implementation has received bounded source inspection only. Unity compilation, appearance, live anatomy, hit/miss/immunity/Protect behavior and active-frame validation must be checked by the integration owner before approval. The effect uses an ellipsoid expanded by each actor's support radius for its continuous swept contact; the returned contact and normal lie on the actual unexpanded ellipsoid.
