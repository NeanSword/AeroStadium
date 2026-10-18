Face layer source checks (2026-10-18)

Verified profiles: Tortank 009, Bulbizarre 001, Arcanin 059.

Read-only repository sources inspected:
- Presentation/NativeMaterialBridge.cs: order 100; pType 2 rounds control U to .5, V to .25 after multiplication by 100; native eye scale is (2,1).
- Resources/NativeModels/{001,009,059}/native_materials.json: named eye control nodes, pType 2, native baseScale (2,1), baseUV (-.5,0).
- Resources/NativeModels/{001,009,059}/native-manifest.json: eye01, kw30_hate01, kw32_happyB01 names and durations.
- NativeClips: 001 eye 010/hate 016/happyB 018; 009 eye 011/hate 017/happyB 018; 059 eye 009/hate 015/happyB 017.
- Actual Eye_col.png atlases viewed with view_image for all three species. Each is 2 columns by 4 rows. The lower-right red invalid cell is never selected.
- Material texture GUID resolution: 001 e15b72b62eb9786418312b8abd9f0623; 009 89ab18527cffdd445a8f5392fdbbe6c3; 059 b71a3ddf430560f468888393182a5510 all resolve to the inspected species Eye_col.png.meta.
- Native U wrapping is Mirror. Control X=.005 becomes transformed offset +1, selecting the atlas's opposite column through mirroring.

Control positions exactly enumerated by native eye01: (0,0), (0,.0025), (0,.005), (0,.0075), (.005,0), (.005,.0025), (.005,.005), (.005,.0075).
Only the first six are used. Native 009/059 hate holds (.005,0), matching the narrowed top-right atlas; native happyB uses (.005,.0025), matching the closed smiling eye. Bulbizarre's native happyB uses the same relief state; its focused top-right state is verified from the eye01 enumeration and atlas image.

Jaw local Y hinge verified by native happy/hate quaternion curves. Tortank/Arcanin native quaternions vary on Y; Bulbizarre has a 180-degree rotated rest pose, but inverse(start)*sample still yields a local Y hinge. Procedural jaw motion is limited to roughly 1.4–3.8 degrees, below these native expression excursions.

Integration contract: construct DodgeFacialPose(actor) per dodge; keep Stats as DodgePoseTelemetry.facial; call Restore() in DodgePoseMotion.Update before Animator, Apply(progress) after body pose at order 90, Dispose() on End/cancellation/disable/destroy. Dispose restores control nodes and immediately applies the existing NativeMaterialBridge. The bridge applies ordinary frame updates at order 100. No material clone, material, texture, controller, clip or other native animation asset is changed.

Runtime status: not built or captured in this subtask. Atlas/source verification is not a visible in-game proof. Root still needs Tortank face closeups for alert/focus/protective blink/relief and cancellation restoration in the running build.
