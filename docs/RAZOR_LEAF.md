# Tranch'Herbe 75

The sequence lasts 2.15 seconds. Twelve detached leaves launch at `0.14 + index * 0.026` seconds, alternating the two native Bulbizarre feeler roots with a 12%-height upward offset. Their captured flights last 0.48–0.57 seconds. Each leaf has a curved, lobed, folded blade with procedural veins and spins along its captured fan trajectory; emitted leaves never retarget.

Roots, geometry and body contact evaluate after Animator in `LateUpdate(100)`. A rendered leading leaf tip must cross an actual animated victim triangle to create a body hit. Bounds only reject unnecessary work; there is no body ellipsoid or successful-hit fallback. The contact pose places that visible tip on the measured point. Display age begins on the detection frame, preserving the contact pose and cutting glint when a slow frame catches up several trajectory steps.

Body contact creates narrow cutting glints and three finite leaf fragments. Abri uses its own ellipsoid: whole leaves ricochet outside the shell, slow, fall and fade. This remains a plant-blade material response, with no water splash or shield fragments. Multiple leaf contacts can be observed, but only the first observed body event triggers the single damage reaction. The existing original leaf audio remains connected to the sequence.

A resolved miss locks the original aim while the defender dodges. Damage is disabled, but the captured leading-tip paths still query the actual moving body. `LastGrass.missSurfaceQueries` must show checks and `missSurfaceCrossings` must remain zero. Crossings are reported without hit effects or damage and prevent successful completion. These checks verify sampled leading-tip sweeps, not every face of every blade.

The action director follows the effect's real progress and exposes `LastLeafCamera`; the shot prepares the source, reads the projectile path/contact, then recovers. `LastGrass` retains actual mesh contact counts, baked frames, triangle tests, contact mesh, maximum hit-plane error, rendered tip gap and first observed body point/time. A miss after an earlier hit cannot erase that mesh evidence.

## Bounded review 2026-10-17

Build succeeded, settings restored. Six actual BattleEngine cases passed, zero errors, 120.00 s review / 124.55 s process. side 0 / direct: contacts=12, body=12, crossings=0; side 0 / Protect: contacts=12, body=0, crossings=0; side 0 / Miss: contacts=0, body=0, crossings=0; side 1 / direct: contacts=12, body=12, crossings=0; side 1 / Protect: contacts=12, body=0, crossings=0; side 1 / Miss: contacts=0, body=0, crossings=0. Five cancellations and orthographic fallback passed. Direct attacks have one damage reaction; Protect/Miss have none. Resolved misses move the defender 1.8 m and record no leading-tip body crossings.

Live-camera stills, direct surface diagnostic, Protect and miss images were inspected. The diagnostic close-up is not a cut in the played camera. Motion archive is silent; the original WAV is separate. PCM endpoints/peak are measured, with no subjective loudspeaker listening claimed. Runtime peak RAM 0.664 GiB. See local output/attacks/leaf-20261017-v2/verified-summary.json and runtime/demonstration.mp4.

Fouet Lianes22 regression passed in the same build: both sides/direct/Protect, five cancellations and orthographic fallback. Scope remains Bulbizarre1→Tortank9 in the modern Elite Four arena. This does not validate all species, all arenas, every blade face or all attacks. Croc Feu424 remains pending, including post-Animator mouth contact.
