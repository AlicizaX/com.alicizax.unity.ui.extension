using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 输入读取工具。
/// 负责运行时输入轮询、单次触发和切换态管理，
/// </summary>
public static class InputActionReader
{
    /// <summary>
    /// 用于标识一次输入读取上下文。
    /// 同一个 Action 在不同 owner 或 key 下会拥有独立的按下状态。
    /// </summary>
    private readonly struct InputReadKey : IEquatable<InputReadKey>
    {
        public readonly string ActionName;
        public readonly int OwnerId;
        public readonly string OwnerKey;

        /// <summary>
        /// 使用实例 ID 作为拥有者标识，适合 Unity 对象。
        /// </summary>
        public InputReadKey(string actionName, int ownerId)
        {
            ActionName = actionName ?? string.Empty;
            OwnerId = ownerId;
            OwnerKey = string.Empty;
        }

        /// <summary>
        /// 使用字符串作为拥有者标识，适合外部系统或手动传入的 key。
        /// </summary>
        public InputReadKey(string actionName, string ownerKey)
        {
            ActionName = actionName ?? string.Empty;
            OwnerId = 0;
            OwnerKey = ownerKey ?? string.Empty;
        }

        public bool Equals(InputReadKey other)
        {
            return OwnerId == other.OwnerId
                && string.Equals(ActionName, other.ActionName, StringComparison.Ordinal)
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
                hashCode = (hashCode * 31) + OwnerId;
                hashCode = (hashCode * 31) + StringComparer.Ordinal.GetHashCode(ActionName);
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
    /// 直接读取指定 Action 的值。
    /// </summary>
    public static T ReadValue<T>(string actionName) where T : struct
    {
        InputAction inputAction = ResolveAction(actionName);
        return inputAction != null ? inputAction.ReadValue<T>() : default;
    }

    /// <summary>
    /// 仅在 Action 处于按下状态时读取值。
    /// </summary>
    public static bool TryReadValue<T>(string actionName, out T value) where T : struct
    {
        InputAction inputAction = ResolveAction(actionName);
        if (inputAction != null && inputAction.IsPressed())
        {
            value = inputAction.ReadValue<T>();
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// 只在本次按下的第一帧返回 true，并输出当前值。
    /// owner 用来隔离不同对象的读取状态。
    /// </summary>
    public static bool TryReadValueOnce<T>(UnityEngine.Object owner, string actionName, out T value) where T : struct
    {
        if (owner == null)
        {
            value = default;
            return false;
        }

        return TryReadValueOnceInternal(new InputReadKey(actionName, owner.GetInstanceID()), actionName, out value);
    }

    /// <summary>
    /// 读取按钮型 Action。
    /// 非按钮类型会直接抛出异常，避免误用。
    /// </summary>
    public static bool ReadButton(string actionName)
    {
        InputAction inputAction = ResolveAction(actionName);
        if (inputAction == null)
        {
            return false;
        }

        if (inputAction.type == InputActionType.Button)
        {
            return inputAction.IsPressed();
        }

        throw new NotSupportedException("[InputActionReader] The Input Action must be a button type.");
    }

    /// <summary>
    /// 对 Unity 对象做一次性按钮读取。
    /// </summary>
    public static bool ReadButtonOnce(UnityEngine.Object owner, string actionName)
    {
        return owner != null && ReadButtonOnce(owner.GetInstanceID(), actionName);
    }

    /// <summary>
    /// 对实例 ID 做一次性按钮读取。
    /// </summary>
    public static bool ReadButtonOnce(int instanceID, string actionName)
    {
        return ReadButtonOnceInternal(new InputReadKey(actionName, instanceID), actionName);
    }

    /// <summary>
    /// 对字符串 key 做一次性按钮读取。
    /// </summary>
    public static bool ReadButtonOnce(string key, string actionName)
    {
        return ReadButtonOnceInternal(new InputReadKey(actionName, key), actionName);
    }

    /// <summary>
    /// 对 Unity 对象读取按钮切换态。
    /// 每次新的按下沿会在开/关之间切换。
    /// </summary>
    public static bool ReadButtonToggle(UnityEngine.Object owner, string actionName)
    {
        return owner != null && ReadButtonToggle(owner.GetInstanceID(), actionName);
    }

    /// <summary>
    /// 对实例 ID 读取按钮切换态。
    /// </summary>
    public static bool ReadButtonToggle(int instanceID, string actionName)
    {
        return ReadButtonToggleInternal(new InputReadKey(actionName, instanceID), actionName);
    }

    /// <summary>
    /// 对字符串 key 读取按钮切换态。
    /// </summary>
    public static bool ReadButtonToggle(string key, string actionName)
    {
        return ReadButtonToggleInternal(new InputReadKey(actionName, key), actionName);
    }

    /// <summary>
    /// 重置指定 key 的切换态。
    /// </summary>
    public static void ResetToggledButton(string key, string actionName)
    {
        RemoveKey(ToggledKeys, ref ToggledKeyCount, new InputReadKey(actionName, key));
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
    /// 解析 Action；找不到时立即抛错，避免静默失败。
    /// </summary>
    private static InputAction ResolveAction(string actionName)
    {
        return InputBindingManager.Action(actionName);
    }

    /// <summary>
    /// 内部的单次值读取逻辑。
    /// 当按键抬起时，会清理 PressedKeys 中对应状态。
    /// </summary>
    private static bool TryReadValueOnceInternal<T>(InputReadKey readKey, string actionName, out T value) where T : struct
    {
        InputAction inputAction = ResolveAction(actionName);
        if (inputAction != null && inputAction.IsPressed())
        {
            if (AddKey(ref PressedKeys, ref PressedKeyCount, readKey))
            {
                value = inputAction.ReadValue<T>();
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
    private static bool ReadButtonOnceInternal(InputReadKey readKey, string actionName)
    {
        if (ReadButton(actionName))
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
    private static bool ReadButtonToggleInternal(InputReadKey readKey, string actionName)
    {
        if (ReadButtonOnceInternal(readKey, actionName))
        {
            if (!AddKey(ref ToggledKeys, ref ToggledKeyCount, readKey))
            {
                RemoveKey(ToggledKeys, ref ToggledKeyCount, readKey);
            }
        }

        return IndexOf(ToggledKeys, ToggledKeyCount, readKey) >= 0;
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
