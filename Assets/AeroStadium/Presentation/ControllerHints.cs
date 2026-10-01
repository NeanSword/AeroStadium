using System;
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
        public event Action Changed;
        Gamepad last;
        InputSystemUIInputModule module;
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
            module = inputModule;
            InputSystem.onDeviceChange -= OnDeviceChange;
            InputSystem.onDeviceChange += OnDeviceChange;
            Refresh();
        }

        void Update() { if (last != Gamepad.current) Refresh(); }
        void OnDeviceChange(InputDevice device, InputDeviceChange change) { if (device is Gamepad) Refresh(); }
        void OnDestroy() { InputSystem.onDeviceChange -= OnDeviceChange; }

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
