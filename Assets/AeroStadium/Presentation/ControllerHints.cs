using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;

namespace AeroStadium.Presentation
{
    public enum ControllerFamily { Xbox, PlayStation, Nintendo }

    public sealed class ControllerHints : MonoBehaviour
    {
        public ControllerFamily Family { get; private set; } = ControllerFamily.Xbox;
        public bool Connected => Gamepad.current != null && Gamepad.current.enabled;
        public bool UsingGamepad { get; private set; }
        public event Action Changed;
        Gamepad last;
        InputSystemUIInputModule module;
        bool initialized;
        readonly HashSet<InputAction> watchedActions = new HashSet<InputAction>();
        public string Accept => !Connected ? "Entrée" : Family == ControllerFamily.PlayStation ? "Croix" : "A";
        public string Back => !Connected ? "Échap" : Family == ControllerFamily.PlayStation ? "Rond" : "B";
        public string DeviceName => !Connected ? "Clavier / souris" : Family == ControllerFamily.PlayStation ? "PlayStation" : Family == ControllerFamily.Nintendo ? "Nintendo" : "Xbox";
        public bool CancelPressed
        {
            get
            {
                InputAction cancel = module == null ? null : module.cancel?.action;
                if (cancel != null) return cancel.WasPerformedThisFrame();
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
                ButtonControl back = Gamepad.current?.TryGetChildControl<ButtonControl>("{Cancel}");
                return back != null && back.wasPressedThisFrame;
            }
        }
        public bool PausePressed => Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
        public bool StartPressed => (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame)
            || (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame));
        public string StartLabel => Connected ? "Start" : "Entrée";

        public void Initialize(InputSystemUIInputModule inputModule)
        {
            Unsubscribe();
            module = inputModule;
            initialized = true;
            if (isActiveAndEnabled) Subscribe();
            Refresh();
        }

        void OnEnable() { if (initialized) { Subscribe(); Refresh(); } }
        void OnDisable() { Unsubscribe(); }
        void OnDestroy() { Unsubscribe(); initialized = false; }

        void Subscribe()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            InputSystem.onDeviceChange += OnDeviceChange;
            if (module == null) return;
            Watch(module.move); Watch(module.submit); Watch(module.cancel);
            Watch(module.point); Watch(module.leftClick); Watch(module.rightClick);
            Watch(module.middleClick); Watch(module.scrollWheel);
        }

        void Watch(InputActionReference reference)
        {
            InputAction action = reference == null ? null : reference.action;
            if (action != null && watchedActions.Add(action)) action.performed += OnUiAction;
        }

        void Unsubscribe()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            foreach (InputAction action in watchedActions) action.performed -= OnUiAction;
            watchedActions.Clear();
        }

        void Update()
        {
            if (last != Gamepad.current) Refresh();
            Gamepad gamepad = Gamepad.current;
            if (gamepad != null && gamepad.startButton.wasPressedThisFrame) RegisterInput(gamepad);
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
                RegisterInput(keyboard);
        }
        void OnDeviceChange(InputDevice device, InputDeviceChange change) { if (device is Gamepad) Refresh(); }

        void OnUiAction(InputAction.CallbackContext context)
        {
            InputDevice device = context.control?.device;
            if (device == null) return;
            // A stick returning to rest must not reclaim the cursor after a mouse
            // input. The UI action already applies the package's stick dead zone.
            if (module != null && context.action == module.move?.action
                && context.ReadValue<Vector2>().sqrMagnitude < .01f) return;
            if (module != null && (context.action == module.leftClick?.action
                || context.action == module.rightClick?.action || context.action == module.middleClick?.action)
                && context.ReadValue<float>() < .5f) return;
            if (module != null && context.action == module.scrollWheel?.action
                && context.ReadValue<Vector2>().sqrMagnitude < .01f) return;
            RegisterInput(device);
        }

        void RegisterInput(InputDevice device)
        {
            if (device is Gamepad gamepad)
            {
                if (!gamepad.enabled) return;
                bool modeChanged = !UsingGamepad;
                UsingGamepad = true;
                if (Gamepad.current != gamepad) gamepad.MakeCurrent();
                if (last != gamepad) Refresh();
                else if (modeChanged) Changed?.Invoke();
            }
            else if (device is Keyboard || device is Mouse || device is Touchscreen || device is Pen)
            {
                if (!UsingGamepad) return;
                UsingGamepad = false;
                Changed?.Invoke();
            }
        }

        void Refresh()
        {
            // Removing the active controller can clear current even while a pad
            // of another layout remains connected. Recover it immediately.
            if (Gamepad.current == null || !Gamepad.current.enabled)
            {
                for (int i = Gamepad.all.Count - 1; i >= 0; i--)
                    if (Gamepad.all[i].enabled) { Gamepad.all[i].MakeCurrent(); break; }
            }
            last = Gamepad.current;
            Family = Identify(last);
            if (!Connected) UsingGamepad = false;
            // Default UI actions use native Submit/Cancel usages. Switch layouts map
            // those usages to A/B correctly, unlike a remap based on button position.
            Debug.Log("Controller prompts: " + DeviceName);
            Changed?.Invoke();
        }

        public static ControllerFamily Identify(Gamepad device)
        {
            if (device == null) return ControllerFamily.Xbox;
            string label = (device.layout + " " + device.description.manufacturer + " " + device.description.product).ToLowerInvariant();
            if (label.Contains("dualshock") || label.Contains("dualsense") || label.Contains("sony") || label.Contains("playstation")) return ControllerFamily.PlayStation;
            if (label.Contains("nintendo") || label.Contains("switch") || label.Contains("joy-con")) return ControllerFamily.Nintendo;
            return ControllerFamily.Xbox;
        }
    }
}
