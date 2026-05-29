#if INPUTSYSTEM_SUPPORT
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

/// <summary>
/// 输入读取工具。
/// 负责运行时输入轮询、单次触发和切换态管理。
/// </summary>
public static class InputActionReader
{
    /// <summary>
    /// 用于标识一次输入读取上下文。
    /// 同一个 Action 在不同 owner、key 或 composite part 下会拥有独立的按下状态。
    /// </summary>
    private readonly struct InputReadKey : IEquatable<InputReadKey>
    {
        public readonly Guid ActionId;
        public readonly string ActionName;
        public readonly string CompositePartName;
        public readonly int OwnerId;
        public readonly string OwnerKey;

        /// <summary>
        /// 使用实例 ID 作为拥有者标识，适合 Unity 对象。
        /// </summary>
        public InputReadKey(InputAction action, string actionName, string compositePartName, int ownerId)
        {
            ActionId = action != null ? action.id : Guid.Empty;
            ActionName = GetActionKey(action, actionName);
            CompositePartName = compositePartName ?? string.Empty;
            OwnerId = ownerId;
            OwnerKey = string.Empty;
        }

        /// <summary>
        /// 使用字符串作为拥有者标识，适合外部系统或手动传入的 key。
        /// </summary>
        public InputReadKey(InputAction action, string actionName, string compositePartName, string ownerKey)
        {
            ActionId = action != null ? action.id : Guid.Empty;
            ActionName = GetActionKey(action, actionName);
            CompositePartName = compositePartName ?? string.Empty;
            OwnerId = 0;
            OwnerKey = ownerKey ?? string.Empty;
        }

        public bool Equals(InputReadKey other)
        {
            return ActionId.Equals(other.ActionId)
                   && OwnerId == other.OwnerId
                   && string.Equals(ActionName, other.ActionName, StringComparison.Ordinal)
                   && string.Equals(CompositePartName, other.CompositePartName, StringComparison.Ordinal)
                   && string.Equals(OwnerKey, other.OwnerKey, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is InputReadKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 17;
                hashCode = (hashCode * 31) + ActionId.GetHashCode();
                hashCode = (hashCode * 31) + OwnerId;
                hashCode = (hashCode * 31) + StringComparer.Ordinal.GetHashCode(ActionName);
                hashCode = (hashCode * 31) + StringComparer.Ordinal.GetHashCode(CompositePartName);
                hashCode = (hashCode * 31) + StringComparer.Ordinal.GetHashCode(OwnerKey);
                return hashCode;
            }
        }
    }

    private const int InitialKeyCapacity = 64;
    private static InputReadKey[] PressedKeys = new InputReadKey[InitialKeyCapacity];
    private static int PressedKeyCount;
    private static InputReadKey[] ToggledKeys = new InputReadKey[InitialKeyCapacity];
    private static int ToggledKeyCount;

    /// <summary>
    /// 直接读取指定 Action 的值。Composite Action 会返回 Input System 计算后的合成值。
    /// </summary>
    public static T ReadValue<T>(InputAction action) where T : struct
    {
        return action != null ? action.ReadValue<T>() : default;
    }

    /// <summary>
    /// 直接读取指定 Action 名称对应的值。
    /// </summary>
    public static T ReadValue<T>(string actionName) where T : struct
    {
        return ReadValue<T>(ResolveAction(actionName));
    }

    /// <summary>
    /// 仅在 Action 处于按下状态时读取值。Composite Action 会按整体合成结果判断是否按下。
    /// </summary>
    public static bool TryReadValue<T>(InputAction action, out T value) where T : struct
    {
        if (action != null && action.IsPressed())
        {
            value = action.ReadValue<T>();
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// 仅在指定 Action 名称对应的 Action 处于按下状态时读取值。
    /// </summary>
    public static bool TryReadValue<T>(string actionName, out T value) where T : struct
    {
        return TryReadValue(ResolveAction(actionName), out value);
    }

    /// <summary>
    /// 只在本次按下的第一帧返回 true，并输出当前值。
    /// owner 用来隔离不同对象的读取状态。
    /// </summary>
    public static bool TryReadValueOnce<T>(Object owner, InputAction action, out T value) where T : struct
    {
        if (owner == null)
        {
            value = default;
            return false;
        }

        return TryReadValueOnceInternal(new InputReadKey(action, null, null, owner.GetInstanceID()), action, out value);
    }

    /// <summary>
    /// 只在本次按下的第一帧返回 true，并输出指定 Action 名称对应的当前值。
    /// owner 用来隔离不同对象的读取状态。
    /// </summary>
    public static bool TryReadValueOnce<T>(Object owner, string actionName, out T value) where T : struct
    {
        if (owner == null)
        {
            value = default;
            return false;
        }

        InputAction action = ResolveAction(actionName);
        return TryReadValueOnceInternal(new InputReadKey(action, actionName, null, owner.GetInstanceID()), action, out value);
    }

    /// <summary>
    /// 读取按钮型 Action。
    /// 非按钮类型会直接抛出异常，避免误用。
    /// </summary>
    public static bool ReadButton(InputAction action)
    {
        if (action == null)
        {
            return false;
        }

        if (action.type == InputActionType.Button)
        {
            return action.IsPressed();
        }

        throw new NotSupportedException("[InputActionReader] The Input Action must be a button type.");
    }

    /// <summary>
    /// 读取指定 Action 名称对应的按钮型 Action。
    /// </summary>
    public static bool ReadButton(string actionName)
    {
        return ReadButton(ResolveAction(actionName));
    }

    /// <summary>
    /// 读取任意类型 Action 的按下态。适合把 Value/PassThrough 或 Composite Action 当作 actuation 判断。
    /// </summary>
    public static bool ReadPressed(InputAction action)
    {
        return action != null && action.IsPressed();
    }

    /// <summary>
    /// 读取指定 Action 名称对应的任意类型 Action 的按下态。
    /// </summary>
    public static bool ReadPressed(string actionName)
    {
        return ReadPressed(ResolveAction(actionName));
    }

    /// <summary>
    /// 对 Unity 对象做一次性按下态读取，支持 Button、Value、PassThrough 和 Composite Action。
    /// </summary>
    public static bool ReadPressedOnce(Object owner, InputAction action)
    {
        return owner != null && ReadPressedOnce(owner.GetInstanceID(), action);
    }

    /// <summary>
    /// 对 Unity 对象做一次性按下态读取，支持 Button、Value、PassThrough 和 Composite Action。
    /// </summary>
    public static bool ReadPressedOnce(Object owner, string actionName)
    {
        return owner != null && ReadPressedOnce(owner.GetInstanceID(), actionName);
    }

    /// <summary>
    /// 对实例 ID 做一次性按下态读取，支持 Button、Value、PassThrough 和 Composite Action。
    /// </summary>
    public static bool ReadPressedOnce(int instanceID, InputAction action)
    {
        return ReadButtonOnceInternal(new InputReadKey(action, null, null, instanceID), ReadPressed(action));
    }

    /// <summary>
    /// 对实例 ID 做一次性按下态读取，支持 Button、Value、PassThrough 和 Composite Action。
    /// </summary>
    public static bool ReadPressedOnce(int instanceID, string actionName)
    {
        InputAction action = ResolveAction(actionName);
        return ReadButtonOnceInternal(new InputReadKey(action, actionName, null, instanceID), ReadPressed(action));
    }

    /// <summary>
    /// 对字符串 key 做一次性按下态读取，支持 Button、Value、PassThrough 和 Composite Action。
    /// </summary>
    public static bool ReadPressedOnce(string key, InputAction action)
    {
        return ReadButtonOnceInternal(new InputReadKey(action, null, null, key), ReadPressed(action));
    }

    /// <summary>
    /// 对字符串 key 做一次性按下态读取，支持 Button、Value、PassThrough 和 Composite Action。
    /// </summary>
    public static bool ReadPressedOnce(string key, string actionName)
    {
        InputAction action = ResolveAction(actionName);
        return ReadButtonOnceInternal(new InputReadKey(action, actionName, null, key), ReadPressed(action));
    }

    /// <summary>
    /// 对 Unity 对象读取按下切换态，支持 Button、Value、PassThrough 和 Composite Action。
    /// </summary>
    public static bool ReadPressedToggle(Object owner, InputAction action)
    {
        return owner != null && ReadPressedToggle(owner.GetInstanceID(), action);
    }

    /// <summary>
    /// 对 Unity 对象读取按下切换态，支持 Button、Value、PassThrough 和 Composite Action。
    /// </summary>
    public static bool ReadPressedToggle(Object owner, string actionName)
    {
        return owner != null && ReadPressedToggle(owner.GetInstanceID(), actionName);
    }

    /// <summary>
    /// 对实例 ID 读取按下切换态，支持 Button、Value、PassThrough 和 Composite Action。
    /// </summary>
    public static bool ReadPressedToggle(int instanceID, InputAction action)
    {
        return ReadButtonToggleInternal(new InputReadKey(action, null, null, instanceID), ReadPressed(action));
    }

    /// <summary>
    /// 对实例 ID 读取按下切换态，支持 Button、Value、PassThrough 和 Composite Action。
    /// </summary>
    public static bool ReadPressedToggle(int instanceID, string actionName)
    {
        InputAction action = ResolveAction(actionName);
        return ReadButtonToggleInternal(new InputReadKey(action, actionName, null, instanceID), ReadPressed(action));
    }

    /// <summary>
    /// 对字符串 key 读取按下切换态，支持 Button、Value、PassThrough 和 Composite Action。
    /// </summary>
    public static bool ReadPressedToggle(string key, InputAction action)
    {
        return ReadButtonToggleInternal(new InputReadKey(action, null, null, key), ReadPressed(action));
    }

    /// <summary>
    /// 对字符串 key 读取按下切换态，支持 Button、Value、PassThrough 和 Composite Action。
    /// </summary>
    public static bool ReadPressedToggle(string key, string actionName)
    {
        InputAction action = ResolveAction(actionName);
        return ReadButtonToggleInternal(new InputReadKey(action, actionName, null, key), ReadPressed(action));
    }

    /// <summary>
    /// 对 Unity 对象做一次性按钮读取。
    /// </summary>
    public static bool ReadButtonOnce(Object owner, InputAction action)
    {
        return owner != null && ReadButtonOnce(owner.GetInstanceID(), action);
    }

    /// <summary>
    /// 对 Unity 对象做一次性按钮读取。
    /// </summary>
    public static bool ReadButtonOnce(Object owner, string actionName)
    {
        return owner != null && ReadButtonOnce(owner.GetInstanceID(), actionName);
    }

    /// <summary>
    /// 对实例 ID 做一次性按钮读取。
    /// </summary>
    public static bool ReadButtonOnce(int instanceID, InputAction action)
    {
        return ReadButtonOnceInternal(new InputReadKey(action, null, null, instanceID), ReadButton(action));
    }

    /// <summary>
    /// 对实例 ID 做一次性按钮读取。
    /// </summary>
    public static bool ReadButtonOnce(int instanceID, string actionName)
    {
        InputAction action = ResolveAction(actionName);
        return ReadButtonOnceInternal(new InputReadKey(action, actionName, null, instanceID), ReadButton(action));
    }

    /// <summary>
    /// 对字符串 key 做一次性按钮读取。
    /// </summary>
    public static bool ReadButtonOnce(string key, InputAction action)
    {
        return ReadButtonOnceInternal(new InputReadKey(action, null, null, key), ReadButton(action));
    }

    /// <summary>
    /// 对字符串 key 做一次性按钮读取。
    /// </summary>
    public static bool ReadButtonOnce(string key, string actionName)
    {
        InputAction action = ResolveAction(actionName);
        return ReadButtonOnceInternal(new InputReadKey(action, actionName, null, key), ReadButton(action));
    }

    /// <summary>
    /// 对 Unity 对象读取按钮切换态。
    /// 每次新的按下沿会在开/关之间切换。
    /// </summary>
    public static bool ReadButtonToggle(Object owner, InputAction action)
    {
        return owner != null && ReadButtonToggle(owner.GetInstanceID(), action);
    }

    /// <summary>
    /// 对 Unity 对象读取按钮切换态。
    /// 每次新的按下沿会在开/关之间切换。
    /// </summary>
    public static bool ReadButtonToggle(Object owner, string actionName)
    {
        return owner != null && ReadButtonToggle(owner.GetInstanceID(), actionName);
    }

    /// <summary>
    /// 对实例 ID 读取按钮切换态。
    /// </summary>
    public static bool ReadButtonToggle(int instanceID, InputAction action)
    {
        return ReadButtonToggleInternal(new InputReadKey(action, null, null, instanceID), ReadButton(action));
    }

    /// <summary>
    /// 对实例 ID 读取按钮切换态。
    /// </summary>
    public static bool ReadButtonToggle(int instanceID, string actionName)
    {
        InputAction action = ResolveAction(actionName);
        return ReadButtonToggleInternal(new InputReadKey(action, actionName, null, instanceID), ReadButton(action));
    }

    /// <summary>
    /// 对字符串 key 读取按钮切换态。
    /// </summary>
    public static bool ReadButtonToggle(string key, InputAction action)
    {
        return ReadButtonToggleInternal(new InputReadKey(action, null, null, key), ReadButton(action));
    }

    /// <summary>
    /// 对字符串 key 读取按钮切换态。
    /// </summary>
    public static bool ReadButtonToggle(string key, string actionName)
    {
        InputAction action = ResolveAction(actionName);
        return ReadButtonToggleInternal(new InputReadKey(action, actionName, null, key), ReadButton(action));
    }

    /// <summary>
    /// 读取 Composite Binding 中指定 part 的按钮态，例如 2DVector 的 up/down/left/right。
    /// </summary>
    public static bool ReadCompositePartButton(InputAction action, string compositePartName)
    {
        if (action == null || !action.enabled || string.IsNullOrWhiteSpace(compositePartName))
        {
            return false;
        }

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (!IsCompositePart(binding, compositePartName))
            {
                continue;
            }

            string path = GetEffectivePath(binding);
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var controls = action.controls;
            for (int c = 0; c < controls.Count; c++)
            {
                InputControl control = controls[c];
                if (InputControlPath.Matches(path, control) && control.IsPressed())
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 读取指定 Action 名称对应的 Composite Binding 中指定 part 的按钮态。
    /// </summary>
    public static bool ReadCompositePartButton(string actionName, string compositePartName)
    {
        return ReadCompositePartButton(ResolveAction(actionName), compositePartName);
    }

    /// <summary>
    /// 对 Unity 对象做一次性 Composite part 按钮读取。
    /// </summary>
    public static bool ReadCompositePartButtonOnce(Object owner, InputAction action, string compositePartName)
    {
        return owner != null && ReadCompositePartButtonOnce(owner.GetInstanceID(), action, compositePartName);
    }

    /// <summary>
    /// 对 Unity 对象做一次性 Composite part 按钮读取。
    /// </summary>
    public static bool ReadCompositePartButtonOnce(Object owner, string actionName, string compositePartName)
    {
        return owner != null && ReadCompositePartButtonOnce(owner.GetInstanceID(), actionName, compositePartName);
    }

    /// <summary>
    /// 对实例 ID 做一次性 Composite part 按钮读取。
    /// </summary>
    public static bool ReadCompositePartButtonOnce(int instanceID, InputAction action, string compositePartName)
    {
        return ReadButtonOnceInternal(
            new InputReadKey(action, null, compositePartName, instanceID),
            ReadCompositePartButton(action, compositePartName));
    }

    /// <summary>
    /// 对实例 ID 做一次性 Composite part 按钮读取。
    /// </summary>
    public static bool ReadCompositePartButtonOnce(int instanceID, string actionName, string compositePartName)
    {
        InputAction action = ResolveAction(actionName);
        return ReadButtonOnceInternal(
            new InputReadKey(action, actionName, compositePartName, instanceID),
            ReadCompositePartButton(action, compositePartName));
    }

    /// <summary>
    /// 对字符串 key 做一次性 Composite part 按钮读取。
    /// </summary>
    public static bool ReadCompositePartButtonOnce(string key, InputAction action, string compositePartName)
    {
        return ReadButtonOnceInternal(
            new InputReadKey(action, null, compositePartName, key),
            ReadCompositePartButton(action, compositePartName));
    }

    /// <summary>
    /// 对字符串 key 做一次性 Composite part 按钮读取。
    /// </summary>
    public static bool ReadCompositePartButtonOnce(string key, string actionName, string compositePartName)
    {
        InputAction action = ResolveAction(actionName);
        return ReadButtonOnceInternal(
            new InputReadKey(action, actionName, compositePartName, key),
            ReadCompositePartButton(action, compositePartName));
    }

    /// <summary>
    /// 对 Unity 对象读取 Composite part 按钮切换态。
    /// </summary>
    public static bool ReadCompositePartButtonToggle(Object owner, InputAction action, string compositePartName)
    {
        return owner != null && ReadCompositePartButtonToggle(owner.GetInstanceID(), action, compositePartName);
    }

    /// <summary>
    /// 对 Unity 对象读取 Composite part 按钮切换态。
    /// </summary>
    public static bool ReadCompositePartButtonToggle(Object owner, string actionName, string compositePartName)
    {
        return owner != null && ReadCompositePartButtonToggle(owner.GetInstanceID(), actionName, compositePartName);
    }

    /// <summary>
    /// 对实例 ID 读取 Composite part 按钮切换态。
    /// </summary>
    public static bool ReadCompositePartButtonToggle(int instanceID, InputAction action, string compositePartName)
    {
        return ReadButtonToggleInternal(
            new InputReadKey(action, null, compositePartName, instanceID),
            ReadCompositePartButton(action, compositePartName));
    }

    /// <summary>
    /// 对实例 ID 读取 Composite part 按钮切换态。
    /// </summary>
    public static bool ReadCompositePartButtonToggle(int instanceID, string actionName, string compositePartName)
    {
        InputAction action = ResolveAction(actionName);
        return ReadButtonToggleInternal(
            new InputReadKey(action, actionName, compositePartName, instanceID),
            ReadCompositePartButton(action, compositePartName));
    }

    /// <summary>
    /// 对字符串 key 读取 Composite part 按钮切换态。
    /// </summary>
    public static bool ReadCompositePartButtonToggle(string key, InputAction action, string compositePartName)
    {
        return ReadButtonToggleInternal(
            new InputReadKey(action, null, compositePartName, key),
            ReadCompositePartButton(action, compositePartName));
    }

    /// <summary>
    /// 对字符串 key 读取 Composite part 按钮切换态。
    /// </summary>
    public static bool ReadCompositePartButtonToggle(string key, string actionName, string compositePartName)
    {
        InputAction action = ResolveAction(actionName);
        return ReadButtonToggleInternal(
            new InputReadKey(action, actionName, compositePartName, key),
            ReadCompositePartButton(action, compositePartName));
    }

    /// <summary>
    /// 重置指定 key 的切换态。
    /// </summary>
    public static void ResetToggledButton(string key, InputAction action)
    {
        RemoveKey(ToggledKeys, ref ToggledKeyCount, new InputReadKey(action, null, null, key));
    }

    /// <summary>
    /// 重置指定 key 的切换态。
    /// </summary>
    public static void ResetToggledButton(string key, string actionName)
    {
        InputAction action = ResolveAction(actionName);
        RemoveKey(ToggledKeys, ref ToggledKeyCount, new InputReadKey(action, actionName, null, key));
    }

    /// <summary>
    /// 重置指定 key 和 composite part 的切换态。
    /// </summary>
    public static void ResetToggledCompositePartButton(string key, InputAction action, string compositePartName)
    {
        RemoveKey(ToggledKeys, ref ToggledKeyCount, new InputReadKey(action, null, compositePartName, key));
    }

    /// <summary>
    /// 重置指定 key 和 composite part 的切换态。
    /// </summary>
    public static void ResetToggledCompositePartButton(string key, string actionName, string compositePartName)
    {
        InputAction action = ResolveAction(actionName);
        RemoveKey(ToggledKeys, ref ToggledKeyCount, new InputReadKey(action, actionName, compositePartName, key));
    }

    /// <summary>
    /// 重置某个 Action 下的所有切换态。
    /// </summary>
    public static void ResetToggledButton(InputAction action)
    {
        if (action == null || ToggledKeyCount == 0)
        {
            return;
        }

        for (int i = ToggledKeyCount - 1; i >= 0; i--)
        {
            if (ToggledKeys[i].ActionId.Equals(action.id))
            {
                RemoveAt(ToggledKeys, ref ToggledKeyCount, i);
            }
        }
    }

    /// <summary>
    /// 重置某个 Action 名称下的所有切换态。
    /// </summary>
    public static void ResetToggledButton(string actionName)
    {
        if (string.IsNullOrEmpty(actionName) || ToggledKeyCount == 0)
        {
            return;
        }

        for (int i = ToggledKeyCount - 1; i >= 0; i--)
        {
            if (string.Equals(ToggledKeys[i].ActionName, actionName, StringComparison.Ordinal))
            {
                RemoveAt(ToggledKeys, ref ToggledKeyCount, i);
            }
        }
    }

    /// <summary>
    /// 清空全部切换态缓存。
    /// </summary>
    public static void ResetToggledButtons()
    {
        Array.Clear(ToggledKeys, 0, ToggledKeyCount);
        ToggledKeyCount = 0;
    }

    /// <summary>
    /// 解析 Action 名称。默认走 IInputActionProvider，旧工程可回退到 InputBindingManager。
    /// </summary>
    private static InputAction ResolveAction(string actionName)
    {
        return InputActionResolver.Action(actionName);
    }

    /// <summary>
    /// 内部的单次值读取逻辑。
    /// 当按键抬起时，会清理 PressedKeys 中对应状态。
    /// </summary>
    private static bool TryReadValueOnceInternal<T>(InputReadKey readKey, InputAction action, out T value) where T : struct
    {
        if (action != null && action.IsPressed())
        {
            if (AddKey(ref PressedKeys, ref PressedKeyCount, readKey))
            {
                value = action.ReadValue<T>();
                return true;
            }
        }
        else
        {
            RemoveKey(PressedKeys, ref PressedKeyCount, readKey);
        }

        value = default;
        return false;
    }

    /// <summary>
    /// 内部的按钮单次触发逻辑。
    /// 只有第一次按下返回 true，持续按住不会重复触发。
    /// </summary>
    private static bool ReadButtonOnceInternal(InputReadKey readKey, bool isPressed)
    {
        if (isPressed)
        {
            return AddKey(ref PressedKeys, ref PressedKeyCount, readKey);
        }

        RemoveKey(PressedKeys, ref PressedKeyCount, readKey);
        return false;
    }

    /// <summary>
    /// 内部的按钮切换逻辑。
    /// 基于 Once 触发，在每次新的按下沿时切换状态。
    /// </summary>
    private static bool ReadButtonToggleInternal(InputReadKey readKey, bool isPressed)
    {
        if (ReadButtonOnceInternal(readKey, isPressed))
        {
            if (!AddKey(ref ToggledKeys, ref ToggledKeyCount, readKey))
            {
                RemoveKey(ToggledKeys, ref ToggledKeyCount, readKey);
            }
        }

        return IndexOf(ToggledKeys, ToggledKeyCount, readKey) >= 0;
    }

    private static bool IsCompositePart(InputBinding binding, string compositePartName)
    {
        return binding.isPartOfComposite
               && string.Equals(binding.name, compositePartName, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetEffectivePath(InputBinding binding)
    {
        return string.IsNullOrWhiteSpace(binding.effectivePath) ? binding.path : binding.effectivePath;
    }

    private static string GetActionKey(InputAction action, string fallbackActionName)
    {
        if (!string.IsNullOrEmpty(fallbackActionName))
        {
            return fallbackActionName;
        }

        if (action == null)
        {
            return string.Empty;
        }

        if (action.actionMap != null && !string.IsNullOrEmpty(action.name))
        {
            return action.actionMap.name + "/" + action.name;
        }

        return !string.IsNullOrEmpty(action.name) ? action.name : action.id.ToString("N");
    }

    private static bool AddKey(ref InputReadKey[] keys, ref int count, InputReadKey key)
    {
        if (IndexOf(keys, count, key) >= 0)
        {
            return false;
        }

        if (count == keys.Length)
        {
            Array.Resize(ref keys, keys.Length << 1);
        }

        keys[count++] = key;
        return true;
    }

    private static bool RemoveKey(InputReadKey[] keys, ref int count, InputReadKey key)
    {
        int index = IndexOf(keys, count, key);
        if (index < 0)
        {
            return false;
        }

        RemoveAt(keys, ref count, index);
        return true;
    }

    private static void RemoveAt(InputReadKey[] keys, ref int count, int index)
    {
        count--;
        if (index < count)
        {
            keys[index] = keys[count];
        }

        keys[count] = default;
    }

    private static int IndexOf(InputReadKey[] keys, int count, InputReadKey key)
    {
        for (int i = 0; i < count; i++)
        {
            if (keys[i].Equals(key))
            {
                return i;
            }
        }

        return -1;
    }
}

#endif
