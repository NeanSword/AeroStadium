# Hydrocanon 56: body surface flow

Only `HydroPumpEffect.cs` is staged for integration. No actual repository, shader, material, audio, cannon placement, move data, or review scene was edited by this task. No Unity build, game runtime, or paid tool was launched.

## Behavior

- Each jet centerline ends at the first ray intersection with the ellipsoid defined by the latest victim world bounds. Its contact normal is the ellipsoid gradient, facing the incoming jet. A missed bounds intersection retains the existing target behavior.
- Both existing contact-bowl grids become curved surface fans on body impact. The fans divide around the left and right sides of the current bounds, follow the lower contour, and cascade to the arena floor. The sheet begins 0.025 world units above the surface. Jet opacity reaches the contact more closely, joining the surface fan.
- 48 of the existing 80 impact droplets follow the same surface and drain path; the other 32 preserve the outward splash. The floor clamp keeps the cascade outside small ellipsoids as well as above the floor.
- Shield contacts retain their existing bowl, normals, and 24-drop shield runoff path. Existing public APIs remain available.
- Three renderers, 3,200 triangles, 208 droplet slots, existing materials, and all lifecycle timings are unchanged. The new update calculations use value types and existing mesh arrays, with no new per-frame managed allocations.

## Integration and diagnostics

Continue updating `SetVictimBounds(bounds)` using current victim renderer bounds and `SetEndpoints(...)` as the combatants move. Call `SetFloor(arenaFloorY)` for a stable arena floor; later victim-bound updates preserve that explicit height. Without this call the fallback floor remains the bottom of the supplied bounds plus 0.02.

- `BodySurfaceFlow`: true while body contact is producing a visible sheet; reset on resource release.
- `BodyRunoffDrops`: current visible body droplets in the final gravity/drain segment; reset on release.
- `BodyContactPoint`: latest primary jet entry point, or its target if no bounds intersection exists.
- Existing `Validate(out error)`, `RunoffDrops` (shield), `PeakDroplets`, `RendererCount`, and `TriangleCount` remain available. Call `Validate` while the effect is live during the integration review.

This is an analytical bounds proxy effect, not a fluid solver or a collision test against every animated mesh triangle. Separate combatants are expected to keep the emitter outside the victim ellipsoid.

## Checks completed

- Staged C# compiled successfully against the installed Unity 6000.3.25f1 Core/Physics modules and .NET Standard 2.1 reference. This was a standalone source compile into a separate temporary check directory, without opening or building the Unity project.
- 25,250 numerical flow samples covered five body shapes, two floor heights, 101 path positions, and 25 angular lanes. Every sampled point was finite, above the floor, and outside/on the ellipsoid; minimum squared normalized radius was 1.00013086. Maximum ray-entry surface residual was 3.11e-14. Contact gap was 0.025; every tested normal faced the incoming jet.
- Independent source review confirmed the quadratic contact calculation and equivalent shield branches. It identified stale post-release diagnostics, which were fixed. The same review prompted the short-body floor projection safeguard.
- Visual behavior and `Validate` during a live move remain for the root integration review after the already-running Unity build completes.
