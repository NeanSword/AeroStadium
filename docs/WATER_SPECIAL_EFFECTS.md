# Original Water Pulse / Scald effect

`AeroStadium.Presentation.WaterSpecialEffect` implements move 352 (Vibraqua / Water Pulse) and 503 (Ébullition / Scald). These are new liquid geometry and shader treatments. They do not instantiate or recolour WaterGunEffect, FlamethrowerEffect or BubbleAttackEffect.

Copy the six flat files into these destinations (the nested `Assets` directory already mirrors them):

| File | Unity destination |
| --- | --- |
| WaterSpecialEffect.cs and .meta | Assets/AeroStadium/Presentation/ |
| WaterSpecial.shader and .meta | Assets/AeroStadium/Shaders/ |
| WaterSpecialReference.mat and .meta | Assets/AeroStadium/Resources/Materials/ |

The reference material under Resources preserves the shader dependency in player builds. Every effect instance owns four meshes and four cloned materials and destroys all of them in OnDestroy. Water Pulse's fourth mesh is empty and its steam renderer is disabled. No textures, external imagery, packages, paid services or generated media are required.

## Visual design and timing

| | 352 Water Pulse | 503 Scald |
| --- | --- | --- |
| Silhouette | Six detached, substantial rippling liquid globes, each with two precessing pressure bands | Twenty-four irregular elongated liquid lobes that overlap into a brief broad gush, with torn liquid spray |
| Dual launch | Three paired volleys | Twelve paired emissions |
| Emission times | .16, .32, .48 seconds, two packets per time | .14 + pairIndex * .028 seconds; final pair at .448 seconds |
| Captured nominal flight | .620–.655 seconds to aim centre | .600–.635 seconds to aim centre |
| Physical arrival | Swept volume contact with the target body or real shield surface; before aim centre | Same; no timer-only contact |
| Contact | Three expanding water resonance rings per globe, fine splash, shell rivulets | Small turbulent liquid ring, outward splash, shell rivulets and rising rolling steam |
| Duration | 2.30 seconds | 2.45 seconds |
| Triangles | 3,336 | 7,728 |
| Active renderers | 3 | 4 |

The first contact is ordinarily around .6–.75 seconds but depends on source distance and the actual body/shield extents. Audio may start at launch; collision sound and one body reaction should use the first rising `ImpactReached` edge. `ImpactReached` latches for actual shield contacts as well as body contacts; the caller must suppress body damage/reaction when protected. This component never changes health, transforms on Pokemon, animations, shields or gameplay state.

## Call contract

```csharp
var fx = WaterSpecialEffect.Create(effectParent, cannon1World, aimWorld, width, moveId);
fx.SetSecondaryEmitter(cannon2World); // Both Tortank EffWideUse01 and EffWideUse02, after native pose update.
fx.SetVictimBounds(targetWorldBounds);
fx.SetFloor(targetPokemonRootWorldY);
// Optional, when protected:
fx.SetShieldContact(primaryPoint, primaryOutwardNormal);
fx.SetSecondaryShieldContact(secondaryPoint, secondaryOutwardNormal);
fx.SetShieldSurface(shieldWorldBounds, arenaFloorWorldY);
```

Refresh `SetEndpoints(primaryMuzzleWorld, aimWorld)` and `SetSecondaryEmitter(secondaryMuzzleWorld)` after the native pose is evaluated, until the emission window closes. A packet copies its origin, aim, velocity, acceleration and flight at birth. SetEndpoints never retargets any emitted particle, moving sphere, water lobe, ring or trail. The effect parent may be transformed; geometry is stored relative to its current world matrix while emitted motion remains in world space. Supply positive finite world bounds and the exact Protect ellipsoid bounds rather than the Pokemon body's bounds for SetShieldSurface.

The optional secondary emitter must be configured before .14/.16 seconds. If absent, all packets use the primary emitter. Fixed total counts remain six and twenty-four respectively. Repeated primary/secondary origin updates are expected for animated Tortank cannons. `SetSecondaryShieldContact` is a fallback plane for callers lacking the real shell; when `SetShieldSurface` is supplied, every projectile intersects the ellipsoid independently and those fallback points do not steer any projectile.

`SetShieldContact` marks the effect shielded and disables body impacts. `SetVictimBounds` alone does not mark it shielded. `ImpactEnabled = false` without a shield makes the attack a visual miss with no contact trigger; packets leave their captured source and expire. `SetFloor(float)` is available for unprotected direct hits and misses; otherwise its default is only an approximate floor. Both SetShieldContact overloads accepting `(point, normal, Bounds, float floor)` are provided, as are the analogous secondary overloads.

## API and diagnostic semantics

| API | Meaning |
| --- | --- |
| Duration / EffectProgress | Finite duration in seconds / clamped 0–1 progress |
| IsPlaying / Completed | Playing until Duration; completed instances hide all renderers |
| ImpactReached | Latched by the first actual swept body or shell contact |
| ImpactOpacity / ContactOpacity | Current visual body splash / protected liquid contact strength |
| EmissionCount | Number of captured liquid packets emitted; final total 6 / 24 |
| ActualContacts, TotalContacts | Number of physically contacted packets; timeout/miss expiry does not count |
| PrimaryEmitterContacts / SecondaryEmitterContacts | Actual contacts assigned to the anatomical emitter used at launch |
| EmitterContacts | Sum of the two emitter contact counts; equal to ActualContacts |
| PrimaryShieldContacts / SecondaryShieldContacts / ShieldContacts | Actual protected contacts by emitter / total |
| ContactPoint / ContactNormal | Last physically computed contact point and outward surface normal |
| ShieldContactPoint / ShieldContactNormal | Aliases of the above, for the protected presentation integration |
| DetachedBeforeImpact | A packet physically travelled clear of its own captured launch origin before the first impact |
| ProjectileDetached / SourceConnectionCleared | Aliases of DetachedBeforeImpact |
| RunoffDrops | Currently visible discrete shell runoff drops; reset every render update |
| PeakParticles / PeakSteamPuffs | Peak concurrently visible liquid volumes, discrete spray and steam / steam only |
| PrimaryTrajectoryOrigin / SecondaryTrajectoryOrigin | First captured world launch origin for each emitter |
| GetEmissionOrigin(index) / IsSecondaryEmission(index) | Read the captured packet pool without exposing it or allocating |
| RendererCount / TriangleCount | Fixed runtime geometry/rendering budget |
| Validate(out string error) | Checks initialization, all owned resources, finite geometry/trajectories, the triangle budget and contact counters |

## Shield contact and runoff

Every moving water volume uses a swept segment against a conservatively expanded ellipsoid. Its actual surface point and gradient normal are then calculated on the supplied real shell. A moving shell can catch an in-flight water volume. Water Pulse dissolves into local expanding rings; Scald changes into splash and steam. There is no reflected flight direction and no source-to-contact hose.

Contact rings project every vertex onto the shell plus a small outward offset. Each protected packet captures its shell latitude/longitude at impact. Three thin liquid rivulets and discrete drops descend along that ellipsoid to its intersection with the supplied floor, then fall the remaining distance if the shield ends above ground. Nearby Scald contacts merge visually into fewer coherent runoff strips. All visible liquid vertices and billboard centres are kept outside the shell; a shader world-space ellipsoid clip removes any small interpolated triangle chords and billboard fragments inside it. Steam rises from the physical contact location, develops into soft rolling puffs, and fades in under a second.

## Verification performed in this staging task

Source inspection confirmed API compatibility with the root integration, bounded pooled arrays, deterministic math, one-time allocations, no per-frame component lookup, no shared-material mutation, no reflection/retargeting path, per-emitter accounting and owned-resource cleanup. A second agent independently inspected C# / shader source and found no critical issue. This task did not edit the repository, invoke Unity, run a player/build or change runtime state. Unity import, shader compile and presentation capture are the root integration's remaining verification steps.
