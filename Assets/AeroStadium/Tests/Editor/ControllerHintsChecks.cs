using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

namespace AeroStadium.Tests
{
    // Synchronous tests deliberately use manual InputSystem updates: the fixture
    // isolates virtual devices from hardware, and EditMode UnityTests are unsupported.
    public sealed class ControllerHintsChecks : InputTestFixture
    {
        Type hintsType;
        Component hints;
        GameObject controlsObject;
        GameObject receiverObject;
        EventSystem eventSystem;
        InputSystemUIInputModule module;
        UiCommandReceiver receiver;
        bool manuallyEnabledUi;
        InputActionAsset ownedActions;
        readonly List<InputActionReference> ownedReferences = new List<InputActionReference>();
        static readonly MethodInfo DynamicUpdate = typeof(InputSystem).GetMethod("Update",
            BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(InputUpdateType) }, null);

        public override void Setup()
        {
            base.Setup();
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            hintsType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("AeroStadium.Presentation.ControllerHints", false))
                .FirstOrDefault(type => type != null);
            Assert.That(hintsType, Is.Not.Null, "The presentation assembly must have compiled.");

            controlsObject = new GameObject("Controller test controls");
            controlsObject.SetActive(false);
            eventSystem = controlsObject.AddComponent<EventSystem>();
            eventSystem.sendNavigationEvents = true;
            module = controlsObject.AddComponent<InputSystemUIInputModule>();
            // Use the package's exact default bindings, with fixture ownership.
            // The module's shared defaults use Destroy() during OnDisable, which
            // is invalid in EditMode; our asset is released with DestroyImmediate.
            var defaults = new DefaultInputActions();
            ownedActions = defaults.asset;
            module.actionsAsset = ownedActions;
            module.cancel = OwnReference(defaults.UI.Cancel);
            module.submit = OwnReference(defaults.UI.Submit);
            module.move = OwnReference(defaults.UI.Navigate);
            module.leftClick = OwnReference(defaults.UI.Click);
            module.rightClick = OwnReference(defaults.UI.RightClick);
            module.middleClick = OwnReference(defaults.UI.MiddleClick);
            module.point = OwnReference(defaults.UI.Point);
            module.scrollWheel = OwnReference(defaults.UI.ScrollWheel);
            module.trackedDeviceOrientation = OwnReference(defaults.UI.TrackedDeviceOrientation);
            module.trackedDevicePosition = OwnReference(defaults.UI.TrackedDevicePosition);
            hints = controlsObject.AddComponent(hintsType);
            controlsObject.SetActive(true);

            // EditMode does not run this component's player lifecycle. Invoke the
            // real lifecycle methods rather than injecting BaseInputModule fields.
            var cachedSystem = typeof(BaseInputModule).GetProperty("eventSystem",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(module);
            manuallyEnabledUi = cachedSystem == null;
            if (manuallyEnabledUi)
            {
                InvokeLifecycle(eventSystem, "OnEnable");
                InvokeLifecycle(module, "Awake");
                InvokeLifecycle(module, "OnEnable");
            }
            EventSystem.current = eventSystem;
            module.actionsAsset.Enable();
            eventSystem.UpdateModules();
            module.UpdateModule();
            Invoke("Initialize", module);

            receiverObject = new GameObject("Selected test command");
            receiver = receiverObject.AddComponent<UiCommandReceiver>();
            eventSystem.SetSelectedGameObject(receiverObject);
            AdvanceInput();
        }

        public override void TearDown()
        {
            try
            {
                if (hints != null) Invoke("OnDestroy");
                if (manuallyEnabledUi && module != null)
                {
                    InvokeLifecycle(module, "OnDisable");
                    InvokeLifecycle(eventSystem, "OnDisable");
                    manuallyEnabledUi = false;
                }
                if (receiverObject != null) UnityEngine.Object.DestroyImmediate(receiverObject);
                if (controlsObject != null) UnityEngine.Object.DestroyImmediate(controlsObject);
                foreach (var reference in ownedReferences)
                    if (reference != null) UnityEngine.Object.DestroyImmediate(reference);
                ownedReferences.Clear();
                if (ownedActions != null)
                {
                    ownedActions.Disable();
                    UnityEngine.Object.DestroyImmediate(ownedActions);
                    ownedActions = null;
                }
            }
            finally
            {
                base.TearDown();
            }
        }

        [Test]
        public void DisconnectedDefaultsToXboxFamilyAndKeyboardPrompts()
        {
            Assert.That(Read<bool>("Connected"), Is.False);
            Assert.That(Family, Is.EqualTo("Xbox"));
            Assert.That(Read<string>("DeviceName"), Is.EqualTo("Clavier / souris"));
            Assert.That(Read<string>("Accept"), Is.EqualTo("Entrée"));
            Assert.That(Read<string>("Back"), Is.EqualTo("Échap"));
            Assert.That(Identify(null), Is.EqualTo("Xbox"));
        }

        [TestCase("Gamepad", "Xbox", "A", "B", "buttonSouth", "buttonEast", false)]
        [TestCase("Gamepad", "Xbox", "A", "B", "buttonSouth", "buttonEast", true)]
        [TestCase("DualShock4GamepadHID", "PlayStation", "Croix", "Rond", "buttonSouth", "buttonEast", false)]
        [TestCase("DualShock4GamepadHID", "PlayStation", "Croix", "Rond", "buttonSouth", "buttonEast", true)]
        [TestCase("DualSenseGamepadHID", "PlayStation", "Croix", "Rond", "buttonSouth", "buttonEast", false)]
        [TestCase("DualSenseGamepadHID", "PlayStation", "Croix", "Rond", "buttonSouth", "buttonEast", true)]
        [TestCase("SwitchProControllerHID", "Nintendo", "A", "B", "buttonEast", "buttonSouth", false)]
        [TestCase("SwitchProControllerHID", "Nintendo", "A", "B", "buttonEast", "buttonSouth", true)]
        public void NativeSubmitAndCancelMatchDisplayedPrompts(
            string layout, string expectedFamily, string accept, string back,
            string submitControlName, string cancelControlName, bool testCancel)
        {
            var device = (Gamepad)InputSystem.AddDevice(layout);
            Assert.That(Gamepad.current, Is.SameAs(device));
            Assert.That(Family, Is.EqualTo(expectedFamily));
            Assert.That(Identify(device), Is.EqualTo(expectedFamily));
            Assert.That(Read<string>("Accept"), Is.EqualTo(accept));
            Assert.That(Read<string>("Back"), Is.EqualTo(back));

            var submit = device.GetChildControl<ButtonControl>(submitControlName);
            var cancel = device.GetChildControl<ButtonControl>(cancelControlName);
            Assert.That(module.submit.action.controls, Has.Member(submit));
            Assert.That(module.submit.action.controls, Has.No.Member(cancel));
            Assert.That(module.cancel.action.controls, Has.Member(cancel));
            Assert.That(module.cancel.action.controls, Has.No.Member(submit));

            // Each command gets an independent fixture. Synchronous EditMode
            // tests advance InputSystem updates but do not advance Time.frameCount,
            // which the UI module uses for WasPerformedThisDynamicUpdate().
            WriteButton(testCancel ? cancel : submit, true);
            Assert.That(module.submit.action.WasPerformedThisFrame(), Is.EqualTo(!testCancel));
            Assert.That(module.cancel.action.WasPerformedThisFrame(), Is.EqualTo(testCancel));
            Assert.That(Read<bool>("CancelPressed"), Is.EqualTo(testCancel));
            module.Process();
            Assert.That(receiver.SubmitCount, Is.EqualTo(testCancel ? 0 : 1));
            Assert.That(receiver.CancelCount, Is.EqualTo(testCancel ? 1 : 0));

            // A held B/Circle must not repeatedly reopen and close the pause menu.
            AdvanceInput();
            Assert.That(Read<bool>("CancelPressed"), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void KeyboardEnterAndEscapeUseTheSameUiActions(bool testCancel)
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            WriteButton(testCancel ? keyboard.escapeKey : keyboard.enterKey, true);
            module.Process();
            Assert.That(receiver.SubmitCount, Is.EqualTo(testCancel ? 0 : 1));
            Assert.That(receiver.CancelCount, Is.EqualTo(testCancel ? 1 : 0));
            Assert.That(Read<bool>("CancelPressed"), Is.EqualTo(testCancel));
            AdvanceInput();
            Assert.That(Read<bool>("CancelPressed"), Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DpadAndStickDeliverNavigationToTheSelectedUiCommand(bool useStick)
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            InputControl<Vector2> navigation = useStick ? gamepad.leftStick : gamepad.dpad;
            WriteValue(navigation, Vector2.right);
            module.Process();
            Assert.That(receiver.MoveCount, Is.EqualTo(1));
            Assert.That(receiver.LastMoveDirection, Is.EqualTo(MoveDirection.Right));
            Assert.That(receiver.LastMoveVector.x, Is.GreaterThan(0f));
            Assert.That(receiver.SubmitCount, Is.EqualTo(0));
            Assert.That(receiver.CancelCount, Is.EqualTo(0));
        }

        [Test]
        public void HotplugAndRemovalRefreshHintsWithoutRecreatingTheScreen()
        {
            int changes = 0;
            Action changed = () => changes++;
            hintsType.GetEvent("Changed").AddEventHandler(hints, changed);

            var xbox = InputSystem.AddDevice<Gamepad>();
            Assert.That(changes, Is.GreaterThan(0));
            Assert.That(Read<bool>("Connected"), Is.True);
            Assert.That(Family, Is.EqualTo("Xbox"));

            int previous = changes;
            var nintendo = (Gamepad)InputSystem.AddDevice("SwitchProControllerHID");
            Assert.That(changes, Is.GreaterThan(previous));
            Assert.That(Family, Is.EqualTo("Nintendo"));

            previous = changes;
            InputSystem.RemoveDevice(nintendo);
            Assert.That(changes, Is.GreaterThan(previous));
            Assert.That(Gamepad.current, Is.SameAs(xbox));
            Assert.That(Family, Is.EqualTo("Xbox"));

            InputSystem.RemoveDevice(xbox);
            Assert.That(Read<bool>("Connected"), Is.False);
            Assert.That(Family, Is.EqualTo("Xbox"));
            Assert.That(Read<string>("Accept"), Is.EqualTo("Entrée"));
        }

        [Test]
        public void LastActiveGamepadAndUsageChangeRefreshTheFamily()
        {
            var xbox = InputSystem.AddDevice<Gamepad>();
            InputSystem.AddDevice("DualSenseGamepadHID");
            Assert.That(Family, Is.EqualTo("PlayStation"));

            WriteButton(xbox.buttonSouth, true);
            Invoke("Update");
            Assert.That(Gamepad.current, Is.SameAs(xbox));
            Assert.That(Family, Is.EqualTo("Xbox"));

            int changes = 0;
            hintsType.GetEvent("Changed").AddEventHandler(hints, (Action)(() => changes++));
            InputSystem.SetDeviceUsage(xbox, "Player1");
            Assert.That(changes, Is.GreaterThan(0));
            Assert.That(Family, Is.EqualTo("Xbox"));
        }

        [Test]
        public void StartIsAnEdgeTriggeredPauseCommand()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            WriteButton(gamepad.startButton, true);
            Assert.That(Read<bool>("PausePressed"), Is.True);
            AdvanceInput();
            Assert.That(Read<bool>("PausePressed"), Is.False);
        }

        [Test]
        public void ConnectingAControllerDoesNotClaimTheMenuCursor()
        {
            Assert.That(Read<bool>("UsingGamepad"), Is.False);
            InputSystem.AddDevice<Gamepad>();
            Assert.That(Read<bool>("Connected"), Is.True);
            Assert.That(Read<bool>("UsingGamepad"), Is.False);
            Assert.That(Read<string>("Accept"), Is.EqualTo("A"));
        }

        [TestCase("Gamepad", "buttonSouth")]
        [TestCase("DualSenseGamepadHID", "buttonSouth")]
        [TestCase("SwitchProControllerHID", "buttonEast")]
        public void NativeSubmitActivatesTheControllerCursor(string layout, string submitControlName)
        {
            var gamepad = (Gamepad)InputSystem.AddDevice(layout);
            Assert.That(Read<bool>("UsingGamepad"), Is.False);
            WriteButton(gamepad.GetChildControl<ButtonControl>(submitControlName), true);
            Assert.That(module.submit.action.WasPerformedThisFrame(), Is.True);
            Assert.That(Read<bool>("UsingGamepad"), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RealNavigationActivatesTheCursorWithoutRepeatingModeChanges(bool useStick)
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            int changes = 0;
            hintsType.GetEvent("Changed").AddEventHandler(hints, (Action)(() => changes++));
            InputControl<Vector2> navigation = useStick ? gamepad.leftStick : gamepad.dpad;
            WriteValue(navigation, Vector2.up);
            Assert.That(Read<bool>("UsingGamepad"), Is.True);
            Assert.That(changes, Is.EqualTo(1));
            WriteValue(navigation, Vector2.down);
            Assert.That(changes, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MouseMovementOrClickReturnsToPointerMode(bool click)
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            var mouse = InputSystem.AddDevice<Mouse>();
            WriteValue(gamepad.dpad, Vector2.up);
            Assert.That(Read<bool>("UsingGamepad"), Is.True);
            if (click) WriteButton(mouse.leftButton, true);
            else WriteValue(mouse.position, new Vector2(250f, 180f));
            Assert.That(Read<bool>("UsingGamepad"), Is.False);
            Assert.That(Read<bool>("Connected"), Is.True);
            Assert.That(Read<string>("Accept"), Is.EqualTo("A"));
        }

        [Test]
        public void AReleasedStickDoesNotReclaimTheCursorFromTheMouse()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            var mouse = InputSystem.AddDevice<Mouse>();
            WriteValue(gamepad.leftStick, Vector2.up);
            WriteValue(mouse.position, new Vector2(250f, 180f));
            Assert.That(Read<bool>("UsingGamepad"), Is.False);
            WriteValue(gamepad.leftStick, Vector2.zero);
            Assert.That(Read<bool>("UsingGamepad"), Is.False);
        }

        [Test]
        public void AutomaticScrollResetDoesNotStealTheControllerCursor()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            var mouse = InputSystem.AddDevice<Mouse>();
            WriteValue(mouse.scroll, new Vector2(0f, 120f));
            WriteValue(gamepad.dpad, Vector2.up);
            Assert.That(Read<bool>("UsingGamepad"), Is.True);
            AdvanceInput();
            Assert.That(mouse.scroll.ReadValue(), Is.EqualTo(Vector2.zero));
            Assert.That(Read<bool>("UsingGamepad"), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void KeyboardNavigationOrSubmitReturnsToKeyboardMode(bool submit)
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            WriteValue(gamepad.dpad, Vector2.up);
            Assert.That(Read<bool>("UsingGamepad"), Is.True);
            WriteButton(submit ? keyboard.enterKey : keyboard.rightArrowKey, true);
            Assert.That(Read<bool>("UsingGamepad"), Is.False);
        }

        [Test]
        public void StartAndSpaceSwitchInputModeWithoutDependingOnUiSubmit()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            WriteButton(gamepad.startButton, true);
            Invoke("Update");
            Assert.That(Read<bool>("UsingGamepad"), Is.True);
            WriteButton(keyboard.spaceKey, true);
            Invoke("Update");
            Assert.That(Read<bool>("UsingGamepad"), Is.False);
        }

        [Test]
        public void RemovingTheLastControllerClearsItsCursorMode()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            WriteValue(gamepad.dpad, Vector2.up);
            Assert.That(Read<bool>("UsingGamepad"), Is.True);
            InputSystem.RemoveDevice(gamepad);
            Assert.That(Read<bool>("UsingGamepad"), Is.False);
            Assert.That(Read<bool>("Connected"), Is.False);
        }

        [Test]
        public void ReinitializationAndDisableDetachUiCallbacks()
        {
            var gamepad = InputSystem.AddDevice<Gamepad>();
            var mouse = InputSystem.AddDevice<Mouse>();
            Invoke("Initialize", module);
            Invoke("Initialize", module);
            WriteValue(gamepad.dpad, Vector2.up);
            Assert.That(Read<bool>("UsingGamepad"), Is.True);
            Invoke("OnDisable");
            WriteValue(mouse.position, new Vector2(250f, 180f));
            Assert.That(Read<bool>("UsingGamepad"), Is.True, "Disabled hints must have no action subscription.");
            Invoke("OnEnable");
            WriteValue(mouse.position, new Vector2(310f, 210f));
            Assert.That(Read<bool>("UsingGamepad"), Is.False);
        }

        string Family => hintsType.GetProperty("Family").GetValue(hints).ToString();

        T Read<T>(string name) => (T)hintsType.GetProperty(name).GetValue(hints);

        string Identify(Gamepad device) => hintsType.GetMethod("Identify")
            .Invoke(null, new object[] { device }).ToString();

        void Invoke(string name, params object[] arguments)
        {
            hintsType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(hints, arguments);
        }

        static void InvokeLifecycle(Component component, string name)
        {
            component.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(component, null);
        }

        InputActionReference OwnReference(InputAction action)
        {
            var reference = InputActionReference.Create(action);
            ownedReferences.Add(reference);
            return reference;
        }

        static void WriteButton(ButtonControl button, bool pressed)
        {
            WriteValue(button, pressed ? 1f : 0f);
        }

        static void WriteValue<T>(InputControl<T> control, T value) where T : struct
        {
            // DualSense HID deliberately discards delta events. Full state events
            // exercise each controller's real state format and semantic usages.
            using (StateEvent.From(control.device, out InputEventPtr state))
            {
                control.WriteValueIntoEvent(value, state);
                InputSystem.QueueEvent(state);
            }
            AdvanceInput();
        }

        static void AdvanceInput()
        {
            // The explicit-update overload is internal in package 1.20. The
            // public no-argument version chooses Editor updates in EditMode.
            Assert.That(DynamicUpdate, Is.Not.Null, "The package's explicit update API must exist.");
            DynamicUpdate.Invoke(null, new object[] { InputUpdateType.Dynamic });
        }
    }

    public sealed class UiCommandReceiver : MonoBehaviour, ISubmitHandler, ICancelHandler, IMoveHandler
    {
        public int SubmitCount { get; private set; }
        public int CancelCount { get; private set; }
        public int MoveCount { get; private set; }
        public MoveDirection LastMoveDirection { get; private set; }
        public Vector2 LastMoveVector { get; private set; }

        public void OnSubmit(BaseEventData eventData) { SubmitCount++; }
        public void OnCancel(BaseEventData eventData) { CancelCount++; }
        public void OnMove(AxisEventData eventData)
        {
            MoveCount++; LastMoveDirection = eventData.moveDir; LastMoveVector = eventData.moveVector;
        }
    }
}
