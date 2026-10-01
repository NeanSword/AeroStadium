# Controller checks

These synchronous Unity EditMode tests isolate Input System devices with
`InputTestFixture`. They create virtual Xbox/default, DualShock 4, DualSense and
Switch Pro devices and send full state events through their native layouts. The
checks cover displayed prompts, hotplug/removal, the last active controller,
Submit/Cancel delivery by `InputSystemUIInputModule`, keyboard fallback, and
single-press pause/cancel behavior.

The presentation currently lives in Unity's `Assembly-CSharp`. The test assembly
uses reflection only at that assembly boundary to locate `ControllerHints` and
call its small public interface; input actions and UI events use normal typed
Unity APIs. The private `Update` invocation simulates its normal frame refresh
after the last active gamepad changes.

Run the assembly `AeroStadium.ControllerChecks` through the Unity Test Runner in
EditMode. `InputTestFixture` does not support EditMode coroutine tests, so these
checks use NUnit `[Test]` methods with explicit input updates instead.
The fixture explicitly calls the UI module's normal Awake/OnEnable/OnDisable
lifecycle when EditMode has not initialized its EventSystem cache. It enables
the actual assigned action asset and processes the module through its public
API, so no synthetic Submit/Cancel implementation bypasses the real bindings.
The fixture owns an asset created by the package's `DefaultInputActions`, rather
than the module's shared default asset, so its EditMode cleanup can use
`DestroyImmediate`. Submit and Cancel run in separate fixtures: manual dynamic
input updates advance input state, while synchronous NUnit tests do not advance
Unity's `Time.frameCount`, which the UI module also checks.
The package's explicit `InputSystem.Update(InputUpdateType.Dynamic)` overload is
internal, so the fixture invokes that method through reflection; it does not
inject or suppress action state. Navigation checks also send D-pad and analog
stick input through the real module and verify received `IMoveHandler` events.

These tests validate software mapping and event handling. They do not validate
physical USB/Bluetooth devices, drivers, vibration, or controller glyph artwork.
