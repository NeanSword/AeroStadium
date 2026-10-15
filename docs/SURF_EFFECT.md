# Surf 57 original effect

Authoritative staged files (copy these paths beneath the real repository root only when integration is authorized):

- `Assets/AeroStadium/Presentation/SurfEffect.cs` and `.meta`
- `Assets/AeroStadium/Shaders/Surf.shader` and `.meta`
- `Assets/AeroStadium/Resources/Materials/SurfReference.mat` and `.meta`

No real repository, Unity project, build, running game, or synced `sources/` file was modified by this effect subtask. No paid tool, image generator, downloaded reference asset, or external asset is used.

## Integration

`SurfEffect.Create(parent, source, target, 2.6f)` creates one ground-borne breaker. Supply `SetFloor(attackerRoot.position.y)` immediately after creation, and `SetVictimBounds(victimBounds)`. The source's horizontal position defines the emission location; height is supplied by the arena floor. Emission starts 0.62 m in front of that position and never extends behind it. `SetEndpoints` follows an attacking pose only before emission at 0.18 s. Emitted motion captures its origin/direction/speed permanently.

Use `SetShieldSurface(shell.ShieldBounds, victimRoot.position.y)` while Abri is active. This call activates full ellipsoid arrest independently of `ImpactEnabled`; `SetShieldContact` is an optional compatibility no-op, because a single plane cannot correctly clip the wide wave. No `DeflectionDirection` or reflected stream is used. The two lateral halves produce curved shell drainage and outward spray; the main sheet stops at its actual column/height intersection. Flow outside the shell silhouette can pass its sides.

For a miss, set `ImpactEnabled=false`, offset the target enough for the 2.6 m wide footprint to miss visibly, and do not set a shield surface. For a hit, presentation must latch one body reaction on `ImpactReached && !protectedTarget`; the effect's flag stays true while the unblocked wave travels onward. `ActualContact`/`ContactNormal` are the first actual ellipsoid contact, gated by `HasActualContact`; shield reaction may use increasing `ShieldContacts` or the first contact. `ImpactOpacity` and `ContactOpacity` are distinct. `RunoffDrops` counts visible falling shell droplets, and does not include thin foam drainage ribbons.

## Timing and scale

- Total lifetime: 2.90 s.
- Detached emission: 0.18 s; grown by 0.38 s.
- Nominal crest origin reaches target plane at 1.12 s. Contact occurs earlier at the target/shield front; exact time depends on bounds and attack distance.
- Unblocked wave can travel another 2.4 m beyond target. Body fades from 1.58 to 2.20 s; surface wash fades from 1.85 to 2.90 s.
- Width is clamped to 2.1–3.1 m, default 2.6 m. Default height 1.56 m; range 1.3–1.85 m. Tail depth is 72% of width. Floor wash is limited to 3.6 m behind the current front.

## Geometry and protection

Four renderers/material instances; 3,054 pooled vertices and 4,192 triangles maximum. Body: 1,920 triangles; aerated crest: 400; 41 independently triggered drainage strips: 656; 128 pooled droplets: 256; floor wash: 960. Geometry/color/index arrays are allocated once during initialization. There are no per-frame managed collections, textures, particle systems, mesh instances, or material instances. Native mesh buffers are uploaded as the effect animates. All four meshes/materials are destroyed on completion and destruction; child renderers are disabled on completion.

For every incoming mesh vertex, the effect casts a horizontal ray at that vertex's own height and lateral offset against the actual shield ellipsoid. Vertices that reach its first entry are arrested at the surface plus a small skin. This avoids treating the broad breaker as one center-line hit. Drainage follows ellipsoid spherical coordinates toward each side and downward. Complete CPU droplet quads are kept above the floor and offset outside the shell.

Connecting two valid shell points can still form a chord through a convex shield. Before uploading each triangle, all three world positions are transformed into unit-sphere space and exact closest-point-to-triangle distance is evaluated. Any triangle that enters the shell is replaced by a degenerate triangle. This also rejects triangles bridging between arrested and side-passing lanes. `RejectedShieldTriangles` exposes this count; it is expected to be nonzero during protection. No shader-displaced geometry can bypass this CPU test.

## Validation performed / remaining

`SurfEffect.cs` compiled successfully without errors using the installed Unity 6000.3.25f1 C# compiler, UnityEngine.CoreModule.dll, and Unity's netstandard reference assembly. The compilation output is staged only at `validation/SurfEffect.dll` and is not an importable game asset.

The local `Validate(out error)` checks finite/above-floor vertices, scale/triangle budgets, meshes, and every accepted triangle against the shield. This check is intended to run during actual preview scenarios. Compilation confirms the C# API and syntax; it does not validate shader compilation, runtime allocation measurements, camera presentation, contact timing, or visual polish. Those require the root task's Unity staging preview and actual hit/Abri/miss scenarios. No rendered result is claimed by this subtask.
