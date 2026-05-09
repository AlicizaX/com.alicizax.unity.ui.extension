#if INPUTSYSTEM_SUPPORT && UX_NAVIGATION
using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace UnityEngine.UI
{
    internal sealed class UXInputModeService : MonoBehaviour
    {
        private const float StickThresholdSqr = 0.04f;
        private const float AxisThreshold = 0.2f;
        private const int InitialDeviceProbeFrames = 30;

        private const string PointerActionName = "UXPointerInput";
        private const string KeyboardActionName = "UXKeyboardInput";
        private const string GamepadActionName = "UXGamepadInput";
        private const string TouchActionName = "UXTouchInput";
        private const string MouseDeltaBinding = "<Mouse>/delta";
        private const string MouseScrollBinding = "<Mouse>/scroll";
        private const string MouseLeftButtonBinding = "<Mouse>/leftButton";
        private const string MouseRightButtonBinding = "<Mouse>/rightButton";
        private const string MouseMiddleButtonBinding = "<Mouse>/middleButton";
        private const string KeyboardAnyKeyBinding = "<Keyboard>/anyKey";
        private const string GamepadButtonSouthBinding = "<Gamepad>/buttonSouth";
        private const string GamepadButtonNorthBinding = "<Gamepad>/buttonNorth";
        private const string GamepadButtonEastBinding = "<Gamepad>/buttonEast";
        private const string GamepadButtonWestBinding = "<Gamepad>/buttonWest";
        private const string GamepadStartButtonBinding = "<Gamepad>/startButton";
        private const string GamepadSelectButtonBinding = "<Gamepad>/selectButton";
        private const string GamepadLeftShoulderBinding = "<Gamepad>/leftShoulder";
        private const string GamepadRightShoulderBinding = "<Gamepad>/rightShoulder";
        private const string GamepadDpadBinding = "<Gamepad>/dpad";
        private const string GamepadLeftStickBinding = "<Gamepad>/leftStick";
        private const string GamepadRightStickBinding = "<Gamepad>/rightStick";
        private const string TouchPressBinding = "<Touchscreen>/primaryTouch/press";
        private const string TouchDeltaBinding = "<Touchscreen>/primaryTouch/delta";

        private static UXInputModeService _instance;

        private InputAction _pointerAction;
        private InputAction _keyboardAction;
        private InputAction _gamepadAction;
        private InputAction _touchAction;
        private int _initialDeviceProbeFramesRemaining;

        public static UXInputMode CurrentMode { get; private set; } = UXInputMode.Pointer;

        public static event Action<UXInputMode> OnModeChanged;

        internal static UXInputModeService EnsureInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            GameObject go = new GameObject("[UXInputModeService]");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UXInputModeService>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            hideFlags = HideFlags.HideAndDontSave;
            CreateActions();
            SetMode(ResolveInitialMode());
        }

        private void OnEnable()
        {
            SetActionsEnabled(true);
            global::UnityEngine.InputSystem.InputSystem.onDeviceChange += OnDeviceChange;
            _initialDeviceProbeFramesRemaining = InitialDeviceProbeFrames;
            global::UnityEngine.InputSystem.InputSystem.onAfterUpdate += OnAfterInputUpdate;
        }

        private void OnDisable()
        {
            SetActionsEnabled(false);
            global::UnityEngine.InputSystem.InputSystem.onDeviceChange -= OnDeviceChange;
            global::UnityEngine.InputSystem.InputSystem.onAfterUpdate -= OnAfterInputUpdate;
        }

        private void OnDestroy()
        {
            DisposeActions();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnAfterInputUpdate()
        {
            if (_initialDeviceProbeFramesRemaining <= 0)
            {
                global::UnityEngine.InputSystem.InputSystem.onAfterUpdate -= OnAfterInputUpdate;
                return;
            }

            _initialDeviceProbeFramesRemaining--;
            UXInputMode initialMode = ResolveInitialMode();
            if (CurrentMode != UXInputMode.Gamepad && initialMode == UXInputMode.Gamepad)
            {
                SetMode(UXInputMode.Gamepad);
            }

            if (CurrentMode == UXInputMode.Gamepad || _initialDeviceProbeFramesRemaining <= 0)
            {
                _initialDeviceProbeFramesRemaining = 0;
                global::UnityEngine.InputSystem.InputSystem.onAfterUpdate -= OnAfterInputUpdate;
            }
        }

        private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device == null)
            {
                return;
            }

            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                    if (IsGamepadLike(device) && CurrentMode != UXInputMode.Gamepad)
                    {
                        SetMode(UXInputMode.Gamepad);
                    }
                    else if ((device is Keyboard || device is Mouse) && CurrentMode == UXInputMode.Pointer)
                    {
                        SetMode(UXInputMode.Keyboard);
                    }

                    break;
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                    if ((CurrentMode == UXInputMode.Gamepad && !HasGamepadLikeDevice()) ||
                        (CurrentMode == UXInputMode.Keyboard && !HasKeyboardOrMouseDevice()))
                    {
                        SetMode(ResolveInitialMode());
                    }

                    break;
            }
        }

        private void CreateActions()
        {
            if (_pointerAction != null)
            {
                return;
            }

            _pointerAction = new InputAction(PointerActionName, InputActionType.PassThrough);
            _pointerAction.AddBinding(MouseDeltaBinding);
            _pointerAction.AddBinding(MouseScrollBinding);
            _pointerAction.AddBinding(MouseLeftButtonBinding);
            _pointerAction.AddBinding(MouseRightButtonBinding);
            _pointerAction.AddBinding(MouseMiddleButtonBinding);
            _pointerAction.performed += OnPointerInput;

            _keyboardAction = new InputAction(KeyboardActionName, InputActionType.PassThrough);
            _keyboardAction.AddBinding(KeyboardAnyKeyBinding);
            _keyboardAction.performed += OnKeyboardInput;

            _gamepadAction = new InputAction(GamepadActionName, InputActionType.PassThrough);
            _gamepadAction.AddBinding(GamepadButtonSouthBinding);
            _gamepadAction.AddBinding(GamepadButtonNorthBinding);
            _gamepadAction.AddBinding(GamepadButtonEastBinding);
            _gamepadAction.AddBinding(GamepadButtonWestBinding);
            _gamepadAction.AddBinding(GamepadStartButtonBinding);
            _gamepadAction.AddBinding(GamepadSelectButtonBinding);
            _gamepadAction.AddBinding(GamepadLeftShoulderBinding);
            _gamepadAction.AddBinding(GamepadRightShoulderBinding);
            _gamepadAction.AddBinding(GamepadDpadBinding);
            _gamepadAction.AddBinding(GamepadLeftStickBinding);
            _gamepadAction.AddBinding(GamepadRightStickBinding);
            _gamepadAction.performed += OnGamepadInput;

            _touchAction = new InputAction(TouchActionName, InputActionType.PassThrough);
            _touchAction.AddBinding(TouchPressBinding);
            _touchAction.AddBinding(TouchDeltaBinding);
            _touchAction.performed += OnTouchInput;
        }

        private void SetActionsEnabled(bool enabled)
        {
            if (_pointerAction == null)
            {
                return;
            }

            if (enabled)
            {
                _pointerAction.Enable();
                _keyboardAction.Enable();
                _gamepadAction.Enable();
                _touchAction.Enable();
                return;
            }

            _pointerAction.Disable();
            _keyboardAction.Disable();
            _gamepadAction.Disable();
            _touchAction.Disable();
        }

        private void DisposeActions()
        {
            if (_pointerAction != null)
            {
                _pointerAction.performed -= OnPointerInput;
                _pointerAction.Dispose();
                _pointerAction = null;
            }

            if (_keyboardAction != null)
            {
                _keyboardAction.performed -= OnKeyboardInput;
                _keyboardAction.Dispose();
                _keyboardAction = null;
            }

            if (_gamepadAction != null)
            {
                _gamepadAction.performed -= OnGamepadInput;
                _gamepadAction.Dispose();
                _gamepadAction = null;
            }

            if (_touchAction != null)
            {
                _touchAction.performed -= OnTouchInput;
                _touchAction.Dispose();
                _touchAction = null;
            }
        }

        private static void OnPointerInput(InputAction.CallbackContext context)
        {
            if (IsInputMeaningful(context.control))
            {
                SetMode(UXInputMode.Pointer);
            }
        }

        private static void OnKeyboardInput(InputAction.CallbackContext context)
        {
            if (IsInputMeaningful(context.control))
            {
                SetMode(UXInputMode.Keyboard);
            }
        }

        private static void OnGamepadInput(InputAction.CallbackContext context)
        {
            if (IsInputMeaningful(context.control))
            {
                SetMode(UXInputMode.Gamepad);
            }
        }

        private static void OnTouchInput(InputAction.CallbackContext context)
        {
            if (IsInputMeaningful(context.control))
            {
                SetMode(UXInputMode.Touch);
            }
        }

        private static bool IsInputMeaningful(InputControl control)
        {
            if (control == null || control.device == null || control.synthetic)
            {
                return false;
            }

            switch (control)
            {
                case ButtonControl button:
                    return button.IsPressed();
                case StickControl stick:
                    return stick.ReadValue().sqrMagnitude >= StickThresholdSqr;
                case Vector2Control vector2:
                    return vector2.ReadValue().sqrMagnitude >= StickThresholdSqr;
                case AxisControl axis:
                    return Mathf.Abs(axis.ReadValue()) >= AxisThreshold;
                default:
                    return !control.noisy;
            }
        }

        private static UXInputMode ResolveInitialMode()
        {
            if (HasGamepadLikeDevice())
            {
                return UXInputMode.Gamepad;
            }

            if (HasKeyboardOrMouseDevice())
            {
                return UXInputMode.Keyboard;
            }

            return UXInputMode.Pointer;
        }

        private static bool HasGamepadLikeDevice()
        {
            for (int i = 0; i < global::UnityEngine.InputSystem.InputSystem.devices.Count; i++)
            {
                InputDevice device = global::UnityEngine.InputSystem.InputSystem.devices[i];
                if (device != null && device.added && IsGamepadLike(device))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasKeyboardOrMouseDevice()
        {
            return (Keyboard.current != null && Keyboard.current.added) ||
                   (Mouse.current != null && Mouse.current.added);
        }

        private static bool IsGamepadLike(InputDevice device)
        {
            if (device == null)
            {
                return false;
            }

            if (device is Gamepad || device is Joystick)
            {
                return true;
            }

            string layout = device.layout ?? string.Empty;
            return layout.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   layout.IndexOf("Controller", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   layout.IndexOf("Joystick", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static void SetMode(UXInputMode mode)
        {
            if (CurrentMode == mode)
            {
                if (mode == UXInputMode.Keyboard || mode == UXInputMode.Gamepad)
                {
                    UXNavigationRuntime.RequestEnsureSelection();
                }

                return;
            }

            CurrentMode = mode;
            OnModeChanged?.Invoke(mode);
        }
    }
}
#endif

