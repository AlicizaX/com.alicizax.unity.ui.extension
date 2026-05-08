using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public static class InputDeviceWatcher
{
    public enum InputDeviceCategory
    {
        Keyboard,
        Xbox,
        PlayStation,
        Other
    }

    public readonly struct DeviceContext : IEquatable<DeviceContext>
    {
        public readonly InputDeviceCategory Category;
        public readonly int DeviceId;
        public readonly int VendorId;
        public readonly int ProductId;
        public readonly string DeviceName;
        public readonly string Layout;

        public DeviceContext(
            InputDeviceCategory category,
            int deviceId,
            int vendorId,
            int productId,
            string deviceName,
            string layout)
        {
            Category = category;
            DeviceId = deviceId;
            VendorId = vendorId;
            ProductId = productId;
            DeviceName = deviceName ?? string.Empty;
            Layout = layout ?? string.Empty;
        }

        public bool Equals(DeviceContext other)
        {
            return Category == other.Category
                   && DeviceId == other.DeviceId
                   && VendorId == other.VendorId
                   && ProductId == other.ProductId
                   && string.Equals(DeviceName, other.DeviceName, StringComparison.Ordinal)
                   && string.Equals(Layout, other.Layout, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is DeviceContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (int)Category;
                hashCode = (hashCode * 397) ^ DeviceId;
                hashCode = (hashCode * 397) ^ VendorId;
                hashCode = (hashCode * 397) ^ ProductId;
                hashCode = (hashCode * 397) ^ (DeviceName != null ? DeviceName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Layout != null ? Layout.GetHashCode() : 0);
                return hashCode;
            }
        }
    }

    [Serializable]
    private struct DeviceCapabilityInfo
    {
        public int vendorId;
        public int productId;
    }

    private const float SameCategoryDebounceWindow = 0.15f;
    private const float AxisActivationThreshold = 0.5f;
    private const float StickActivationThreshold = 0.25f;
    private const string DefaultKeyboardDeviceName = "Keyboard&Mouse";

    public static InputDeviceCategory CurrentCategory { get; private set; } = InputDeviceCategory.Keyboard;
    public static string CurrentDeviceName { get; private set; } = DefaultKeyboardDeviceName;
    public static int CurrentDeviceId { get; private set; } = -1;
    public static int CurrentVendorId { get; private set; }
    public static int CurrentProductId { get; private set; }
    public static DeviceContext CurrentContext { get; private set; } = CreateDefaultContext();

    private static InputAction _anyInputAction;
    private static float _lastSwitchTime = -Mathf.Infinity;
    private static DeviceContext _lastEmittedContext = CreateDefaultContext();
    private const int InitialDeviceCacheCapacity = 16;
    private static DeviceContext[] DeviceContextCache = new DeviceContext[InitialDeviceCacheCapacity];
    private static int DeviceContextCacheCount;
    private static bool _initialized;

    public static event Action<InputDeviceCategory> OnDeviceChanged;
    public static event Action<DeviceContext> OnDeviceContextChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        ApplyContext(CreateDefaultContext(), false);
        _lastEmittedContext = CurrentContext;

        _anyInputAction = new InputAction("AnyDevice", InputActionType.PassThrough);
        _anyInputAction.AddBinding("<Keyboard>/anyKey");
        //为防止误触 暂时屏蔽鼠标检测
        _anyInputAction.AddBinding("<Mouse>/leftButton");
        _anyInputAction.AddBinding("<Mouse>/rightButton");
        _anyInputAction.AddBinding("<Mouse>/middleButton");
        _anyInputAction.AddBinding("<Gamepad>/buttonSouth");
        _anyInputAction.AddBinding("<Gamepad>/buttonNorth");
        _anyInputAction.AddBinding("<Gamepad>/buttonEast");
        _anyInputAction.AddBinding("<Gamepad>/buttonWest");
        _anyInputAction.AddBinding("<Gamepad>/start");
        _anyInputAction.AddBinding("<Gamepad>/select");
        _anyInputAction.AddBinding("<Gamepad>/leftStick");
        _anyInputAction.AddBinding("<Gamepad>/rightStick");
        _anyInputAction.AddBinding("<Gamepad>/dpad");
        _anyInputAction.AddBinding("<Gamepad>/leftTrigger");
        _anyInputAction.AddBinding("<Gamepad>/rightTrigger");
        _anyInputAction.AddBinding("<Joystick>/trigger");
        _anyInputAction.AddBinding("<Joystick>/stick");
        _anyInputAction.performed += OnAnyInputPerformed;
        _anyInputAction.Enable();

        InputSystem.onDeviceChange += OnDeviceChange;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
    }

#if UNITY_EDITOR
    private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
        {
            Dispose();
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }
    }
#endif

    public static void Dispose()
    {
        if (!_initialized)
        {
            return;
        }

        if (_anyInputAction != null)
        {
            _anyInputAction.performed -= OnAnyInputPerformed;
            _anyInputAction.Disable();
            _anyInputAction.Dispose();
            _anyInputAction = null;
        }

        InputSystem.onDeviceChange -= OnDeviceChange;
        Array.Clear(DeviceContextCache, 0, DeviceContextCacheCount);
        DeviceContextCacheCount = 0;

        ApplyContext(CreateDefaultContext(), false);
        _lastEmittedContext = CurrentContext;
        _lastSwitchTime = -Mathf.Infinity;
        OnDeviceChanged = null;
        OnDeviceContextChanged = null;
        _initialized = false;
    }

    private static void OnAnyInputPerformed(InputAction.CallbackContext context)
    {
        InputControl control = context.control;
        if (!IsRelevantControl(control))
        {
            return;
        }

        InputDevice device = control.device;
        if (device == null || device.deviceId == CurrentDeviceId)
        {
            return;
        }

        DeviceContext deviceContext = BuildContext(device);
        if (deviceContext.DeviceId == CurrentDeviceId)
        {
            return;
        }

        float now = Time.realtimeSinceStartup;
        if (deviceContext.Category == CurrentCategory && now - _lastSwitchTime < SameCategoryDebounceWindow)
        {
            return;
        }

        _lastSwitchTime = now;
        SetCurrentContext(deviceContext);
    }

    private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device == null)
        {
            return;
        }

        switch (change)
        {
            case InputDeviceChange.Removed:
            case InputDeviceChange.Disconnected:
                RemoveCachedContext(device.deviceId);
                if (device.deviceId == CurrentDeviceId)
                {
                    PromoteFallbackDevice(device.deviceId);
                }

                break;
            case InputDeviceChange.Reconnected:
            case InputDeviceChange.Added:
                RemoveCachedContext(device.deviceId);
                if (CurrentDeviceId < 0 && IsRelevantDevice(device))
                {
                    SetCurrentContext(BuildContext(device));
                }

                break;
        }
    }

    private static void PromoteFallbackDevice(int removedDeviceId)
    {
        for (int i = InputSystem.devices.Count - 1; i >= 0; i--)
        {
            InputDevice device = InputSystem.devices[i];
            if (device == null || device.deviceId == removedDeviceId || !device.added || !IsRelevantDevice(device))
            {
                continue;
            }

            SetCurrentContext(BuildContext(device));
            return;
        }

        SetCurrentContext(CreateDefaultContext());
    }

    private static void SetCurrentContext(DeviceContext context)
    {
        bool categoryChanged = CurrentCategory != context.Category;
        ApplyContext(context, true);

        if (!_lastEmittedContext.Equals(context))
        {
            OnDeviceContextChanged?.Invoke(context);
            if (categoryChanged)
            {
                OnDeviceChanged?.Invoke(context.Category);
            }

            _lastEmittedContext = context;
        }
    }

    private static void ApplyContext(DeviceContext context, bool log)
    {
        CurrentContext = context;
        CurrentCategory = context.Category;
        CurrentDeviceId = context.DeviceId;
        CurrentVendorId = context.VendorId;
        CurrentProductId = context.ProductId;
        CurrentDeviceName = context.DeviceName;

#if UNITY_EDITOR
        if (log)
        {
            AlicizaX.Log.Info($"Input device -> {CurrentCategory} name={CurrentDeviceName} vid=0x{CurrentVendorId:X} pid=0x{CurrentProductId:X} id={CurrentDeviceId}");
        }
#endif
    }

    private static DeviceContext BuildContext(InputDevice device)
    {
        if (device == null)
        {
            return CreateDefaultContext();
        }

        if (TryGetCachedContext(device.deviceId, out DeviceContext cachedContext))
        {
            return cachedContext;
        }

        TryParseVendorProductIds(device.description.capabilities, out int vendorId, out int productId);
        string deviceName = string.IsNullOrWhiteSpace(device.displayName) ? device.name : device.displayName;
        DeviceContext context = new DeviceContext(
            DetermineCategoryFromDevice(device, vendorId),
            device.deviceId,
            vendorId,
            productId,
            deviceName,
            device.layout);
        AddCachedContext(context);
        return context;
    }

    private static bool TryGetCachedContext(int deviceId, out DeviceContext context)
    {
        for (int i = 0; i < DeviceContextCacheCount; i++)
        {
            if (DeviceContextCache[i].DeviceId == deviceId)
            {
                context = DeviceContextCache[i];
                return true;
            }
        }

        context = default;
        return false;
    }

    private static void AddCachedContext(DeviceContext context)
    {
        if (DeviceContextCacheCount == DeviceContextCache.Length)
        {
            Array.Resize(ref DeviceContextCache, DeviceContextCache.Length << 1);
        }

        DeviceContextCache[DeviceContextCacheCount++] = context;
    }

    private static void RemoveCachedContext(int deviceId)
    {
        for (int i = 0; i < DeviceContextCacheCount; i++)
        {
            if (DeviceContextCache[i].DeviceId != deviceId)
            {
                continue;
            }

            DeviceContextCacheCount--;
            if (i < DeviceContextCacheCount)
            {
                DeviceContextCache[i] = DeviceContextCache[DeviceContextCacheCount];
            }

            DeviceContextCache[DeviceContextCacheCount] = default;
            return;
        }
    }

    private static DeviceContext CreateDefaultContext()
    {
        return new DeviceContext(InputDeviceCategory.Keyboard, -1, 0, 0, DefaultKeyboardDeviceName, Keyboard.current != null ? Keyboard.current.layout : string.Empty);
    }

    private static InputDeviceCategory DetermineCategoryFromDevice(InputDevice device, int vendorId = 0)
    {
        if (device == null)
        {
            return InputDeviceCategory.Keyboard;
        }

        if (device is Keyboard || device is Mouse)
        {
            return InputDeviceCategory.Keyboard;
        }

        if (IsGamepadLike(device))
        {
            return GetGamepadCategory(device, vendorId);
        }

        if (DescriptionContains(device, "xbox") || DescriptionContains(device, "xinput"))
        {
            return InputDeviceCategory.Xbox;
        }

        if (DescriptionContains(device, "dualshock")
            || DescriptionContains(device, "dualsense")
            || DescriptionContains(device, "playstation"))
        {
            return InputDeviceCategory.PlayStation;
        }

        return InputDeviceCategory.Other;
    }

    private static bool IsRelevantDevice(InputDevice device)
    {
        return device is Keyboard || device is Mouse || IsGamepadLike(device);
    }

    private static bool IsRelevantControl(InputControl control)
    {
        if (control == null || control.device == null || !IsRelevantDevice(control.device) || control.synthetic)
        {
            return false;
        }

        switch (control)
        {
            case ButtonControl button:
                return button.IsPressed();
            case StickControl stick:
                return stick.ReadValue().sqrMagnitude >= StickActivationThreshold * StickActivationThreshold;
            case Vector2Control vector2:
                return vector2.ReadValue().sqrMagnitude >= StickActivationThreshold * StickActivationThreshold;
            case AxisControl axis:
                return Mathf.Abs(axis.ReadValue()) >= AxisActivationThreshold;
            default:
                return !control.noisy;
        }
    }

    private static bool IsGamepadLike(InputDevice device)
    {
        if (device is Gamepad || device is Joystick)
        {
            return true;
        }

        string layout = device.layout ?? string.Empty;
        if (ContainsIgnoreCase(layout, "Mouse")
            || ContainsIgnoreCase(layout, "Touch")
            || ContainsIgnoreCase(layout, "Pen"))
        {
            return false;
        }

        return ContainsIgnoreCase(layout, "Gamepad")
               || ContainsIgnoreCase(layout, "Controller")
               || ContainsIgnoreCase(layout, "Joystick");
    }

    private static InputDeviceCategory GetGamepadCategory(InputDevice device, int vendorId = 0)
    {
        if (device == null)
        {
            return InputDeviceCategory.Other;
        }

        string interfaceName = device.description.interfaceName ?? string.Empty;
        if (ContainsIgnoreCase(interfaceName, "xinput"))
        {
            return InputDeviceCategory.Xbox;
        }

        if (vendorId == 0 && TryParseVendorProductIds(device.description.capabilities, out int parsedVendorId, out _))
        {
            vendorId = parsedVendorId;
        }

        if (vendorId == 0x045E || vendorId == 1118)
        {
            return InputDeviceCategory.Xbox;
        }

        if (vendorId == 0x054C || vendorId == 1356)
        {
            return InputDeviceCategory.PlayStation;
        }

        if (DescriptionContains(device, "xbox"))
        {
            return InputDeviceCategory.Xbox;
        }

        if (DescriptionContains(device, "dualshock")
            || DescriptionContains(device, "dualsense")
            || DescriptionContains(device, "playstation"))
        {
            return InputDeviceCategory.PlayStation;
        }

        return InputDeviceCategory.Other;
    }

    private static bool DescriptionContains(InputDevice device, string value)
    {
        if (device == null)
        {
            return false;
        }

        var description = device.description;
        return ContainsIgnoreCase(description.interfaceName, value)
               || ContainsIgnoreCase(device.layout, value)
               || ContainsIgnoreCase(description.product, value)
               || ContainsIgnoreCase(description.manufacturer, value)
               || ContainsIgnoreCase(device.displayName, value)
               || ContainsIgnoreCase(device.name, value);
    }

    private static bool TryParseVendorProductIds(string capabilities, out int vendorId, out int productId)
    {
        vendorId = 0;
        productId = 0;
        if (string.IsNullOrWhiteSpace(capabilities))
        {
            return false;
        }

        try
        {
            DeviceCapabilityInfo info = JsonUtility.FromJson<DeviceCapabilityInfo>(capabilities);
            vendorId = info.vendorId;
            productId = info.productId;
            return vendorId != 0 || productId != 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool ContainsIgnoreCase(string source, string value)
    {
        return !string.IsNullOrEmpty(source) && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
