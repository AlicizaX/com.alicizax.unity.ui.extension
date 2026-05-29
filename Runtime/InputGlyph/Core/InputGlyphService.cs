#if INPUTSYSTEM_SUPPORT
using System;
using Cysharp.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 将 Input System 绑定路径解析为 UI Sprite 或 TMP Sprite Tag。
/// 运行时只使用外部注入的 Glyph 数据库
/// </summary>
public static class InputGlyphService
{
    private static readonly string[] KeyboardGroupHints = { "keyboard", "mouse", "keyboard&mouse", "keyboardmouse", "kbm" };
    private static readonly string[] XboxGroupHints = { "xbox", "xinput", "gamepad", "controller" };
    private static readonly string[] PlayStationGroupHints = { "playstation", "dualshock", "dualsense", "gamepad", "controller" };
    private static readonly string[] SwitchGroupHints = { "switch", "nintendo", "joy-con", "joycon", "gamepad", "controller" };
    private static readonly string[] OtherGamepadGroupHints = { "gamepad", "controller", "joystick" };
    private static readonly char[] TrimChars = { '{', '}', '<', '>', '\'', '"' };
    private const int InitialCacheCapacity = 64;
    private static string[] DisplayNameKeys = new string[InitialCacheCapacity];
    private static string[] DisplayNameValues = new string[InitialCacheCapacity];
    private static int DisplayNameCount;
    private static int[] SpriteTagKeys = new int[InitialCacheCapacity];
    private static string[] SpriteTagValues = new string[InitialCacheCapacity];
    private static int SpriteTagCount;

    private static InputGlyphDatabase _database;

    /// <summary>
    /// 设置 Glyph 数据库。
    /// </summary>
    public static void SetDatabase(InputGlyphDatabase database)
    {
        _database = database;
    }


    static InputGlyphDatabase Database
    {
        get
        {
            return _database;
        }
    }

    /// <summary>
    /// 获取 Action 当前设备分类下匹配绑定的有效控制路径。
    /// </summary>
    public static string GetBindingControlPath(
        InputAction action,
        string compositePartName = null,
        InputDeviceWatcher.InputDeviceCategory? deviceOverride = null)
    {
        return TryGetBindingControl(action, compositePartName, deviceOverride, out InputBinding binding)
            ? GetEffectivePath(binding)
            : string.Empty;
    }

    public static string GetBindingControlPath(
        InputActionReference actionReference,
        string compositePartName = null,
        InputDeviceWatcher.InputDeviceCategory? deviceOverride = null)
    {
        return GetBindingControlPath(actionReference != null ? actionReference.action : null, compositePartName, deviceOverride);
    }

    /// <summary>
    /// 根据 Action 绑定路径获取 TMP Sprite Tag，并返回显示名兜底文本。
    /// </summary>
    public static bool TryGetTMPTagForActionPath(
        InputAction action,
        string compositePartName,
        InputDeviceWatcher.InputDeviceCategory device,
        out string tag,
        out string displayFallback,
        InputGlyphDatabase db = null)
    {
        string controlPath = GetBindingControlPath(action, compositePartName, device);
        return TryGetTMPTagForActionPath(controlPath, device, out tag, out displayFallback, db);
    }

    public static bool TryGetTMPTagForActionPath(
        InputActionReference actionReference,
        string compositePartName,
        InputDeviceWatcher.InputDeviceCategory device,
        out string tag,
        out string displayFallback,
        InputGlyphDatabase db = null)
    {
        return TryGetTMPTagForActionPath(actionReference != null ? actionReference.action : null, compositePartName, device, out tag, out displayFallback, db);
    }

    /// <summary>
    /// 根据 Action 绑定路径获取对应设备分类的 UI Sprite。
    /// </summary>
    public static bool TryGetUISpriteForActionPath(
        InputAction action,
        string compositePartName,
        InputDeviceWatcher.InputDeviceCategory device,
        out Sprite sprite,
        InputGlyphDatabase db = null)
    {
        string controlPath = GetBindingControlPath(action, compositePartName, device);
        return TryGetUISpriteForActionPath(controlPath, device, out sprite, db);
    }

    public static bool TryGetUISpriteForActionPath(
        InputActionReference actionReference,
        string compositePartName,
        InputDeviceWatcher.InputDeviceCategory device,
        out Sprite sprite,
        InputGlyphDatabase db = null)
    {
        return TryGetUISpriteForActionPath(actionReference != null ? actionReference.action : null, compositePartName, device, out sprite, db);
    }

    /// <summary>
    /// 将原始控制路径解析为 TMP Sprite Tag，失败时回退为可读控制名。
    /// </summary>
    public static bool TryGetTMPTagForActionPath(
        string controlPath,
        InputDeviceWatcher.InputDeviceCategory device,
        out string tag,
        out string displayFallback,
        InputGlyphDatabase db = null)
    {
        displayFallback = GetDisplayNameFromControlPath(controlPath);
        tag = null;

        if (!TryGetUISpriteForActionPath(controlPath, device, out Sprite sprite, db))
        {
            return false;
        }

        tag = GetSpriteTag(sprite);
        return true;
    }

    /// <summary>
    /// 通过 Glyph 数据库查找表将原始控制路径解析为 UI Sprite。
    /// </summary>
    public static bool TryGetUISpriteForActionPath(
        string controlPath,
        InputDeviceWatcher.InputDeviceCategory device,
        out Sprite sprite,
        InputGlyphDatabase db = null)
    {
        sprite = null;
        db ??= Database;
        return db != null && db.TryGetSprite(controlPath, device, out sprite);
    }

    /// <summary>
    /// 获取 Action 当前绑定的可读显示名。
    /// </summary>
    public static string GetDisplayNameFromInputAction(
        InputAction action,
        string compositePartName = null,
        InputDeviceWatcher.InputDeviceCategory? deviceOverride = null)
    {
        if (!TryGetBindingControl(action, compositePartName, deviceOverride, out InputBinding binding))
        {
            return string.Empty;
        }

        string display = binding.ToDisplayString();
        return string.IsNullOrEmpty(display) ? GetDisplayNameFromControlPath(GetEffectivePath(binding)) : display;
    }

    /// <summary>
    /// 将控制路径转换为可读显示名。
    /// 优先使用 InputControlPath 转换结果，失败时使用路径最后一段。
    /// </summary>
    public static string GetDisplayNameFromControlPath(string controlPath)
    {
        if (string.IsNullOrWhiteSpace(controlPath))
        {
            return string.Empty;
        }

        int cacheIndex = IndexOf(DisplayNameKeys, DisplayNameCount, controlPath);
        if (cacheIndex >= 0)
        {
            return DisplayNameValues[cacheIndex];
        }

        string humanReadable = InputControlPath.ToHumanReadableString(controlPath, InputControlPath.HumanReadableStringOptions.OmitDevice);
        if (!string.IsNullOrWhiteSpace(humanReadable))
        {
            AddDisplayNameCache(controlPath, humanReadable);
            return humanReadable;
        }

        int separatorIndex = controlPath.LastIndexOf('/');
        string last = (separatorIndex >= 0 ? controlPath.Substring(separatorIndex + 1) : controlPath).Trim(TrimChars);
        AddDisplayNameCache(controlPath, last);
        return last;
    }

    /// <summary>
    /// 按 binding group、控制路径和设备分类选择最匹配的绑定。
    /// </summary>
    public static bool TryGetBindingControl(
        InputAction action,
        string compositePartName,
        InputDeviceWatcher.InputDeviceCategory? deviceOverride,
        out InputBinding binding)
    {
        binding = default;
        if (action == null)
        {
            return false;
        }

        InputDeviceWatcher.InputDeviceCategory category = deviceOverride ?? InputDeviceWatcher.CurrentCategory;
        int bestScore = int.MinValue;
        bool requireCompositePart = !string.IsNullOrEmpty(compositePartName);

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding candidate = action.bindings[i];
            if (candidate.isComposite)
            {
                continue;
            }

            if (requireCompositePart)
            {
                if (!candidate.isPartOfComposite || !string.Equals(candidate.name, compositePartName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }
            else if (candidate.isPartOfComposite)
            {
                continue;
            }

            string path = GetEffectivePath(candidate);
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            int score = ScoreBinding(candidate, category);
            if (score > bestScore)
            {
                bestScore = score;
                binding = candidate;
            }
        }

        return bestScore > int.MinValue;
    }

    private static int ScoreBinding(InputBinding binding, InputDeviceWatcher.InputDeviceCategory category)
    {
        int score = 0;
        string path = GetEffectivePath(binding);

        if (MatchesBindingGroups(binding.groups, category))
        {
            score += 100;
        }
        else if (!string.IsNullOrWhiteSpace(binding.groups))
        {
            score -= 20;
        }

        if (MatchesControlPath(path, category))
        {
            score += 60;
        }

        if (!binding.isPartOfComposite)
        {
            score += 5;
        }

        return score;
    }

    private static bool MatchesBindingGroups(string groups, InputDeviceWatcher.InputDeviceCategory category)
    {
        if (string.IsNullOrWhiteSpace(groups))
        {
            return false;
        }

        string[] hints = GetGroupHints(category);
        int tokenStart = 0;
        for (int i = 0; i <= groups.Length; i++)
        {
            if (i < groups.Length && groups[i] != InputBinding.Separator)
            {
                continue;
            }

            int tokenLength = i - tokenStart;
            while (tokenLength > 0 && char.IsWhiteSpace(groups[tokenStart]))
            {
                tokenStart++;
                tokenLength--;
            }

            while (tokenLength > 0 && char.IsWhiteSpace(groups[tokenStart + tokenLength - 1]))
            {
                tokenLength--;
            }

            if (tokenLength > 0)
            {
                if (ContainsAny(groups, tokenStart, tokenLength, hints))
                {
                    return true;
                }
            }

            tokenStart = i + 1;
        }

        return false;
    }

    private static string GetSpriteTag(Sprite sprite)
    {
        if (sprite == null)
        {
            return null;
        }

        int instanceId = sprite.GetInstanceID();
        int cacheIndex = IndexOf(SpriteTagKeys, SpriteTagCount, instanceId);
        if (cacheIndex >= 0)
        {
            return SpriteTagValues[cacheIndex];
        }

        string cachedTag = ZString.Concat("<sprite name=\"", sprite.name, "\">");
        AddSpriteTagCache(instanceId, cachedTag);
        return cachedTag;
    }

    private static void AddDisplayNameCache(string key, string value)
    {
        if (DisplayNameCount == DisplayNameKeys.Length)
        {
            Array.Resize(ref DisplayNameKeys, DisplayNameKeys.Length << 1);
            Array.Resize(ref DisplayNameValues, DisplayNameValues.Length << 1);
        }

        DisplayNameKeys[DisplayNameCount] = key;
        DisplayNameValues[DisplayNameCount] = value;
        DisplayNameCount++;
    }

    private static void AddSpriteTagCache(int key, string value)
    {
        if (SpriteTagCount == SpriteTagKeys.Length)
        {
            Array.Resize(ref SpriteTagKeys, SpriteTagKeys.Length << 1);
            Array.Resize(ref SpriteTagValues, SpriteTagValues.Length << 1);
        }

        SpriteTagKeys[SpriteTagCount] = key;
        SpriteTagValues[SpriteTagCount] = value;
        SpriteTagCount++;
    }

    private static int IndexOf(string[] values, int count, string value)
    {
        for (int i = 0; i < count; i++)
        {
            if (string.Equals(values[i], value, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOf(int[] values, int count, int value)
    {
        for (int i = 0; i < count; i++)
        {
            if (values[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool ContainsAny(string source, int startIndex, int length, string[] hints)
    {
        if (string.IsNullOrEmpty(source) || hints == null || length <= 0)
        {
            return false;
        }

        for (int i = 0; i < hints.Length; i++)
        {
            if (IndexOfIgnoreCase(source, startIndex, length, hints[i]) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static int IndexOfIgnoreCase(string source, int startIndex, int length, string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > length)
        {
            return -1;
        }

        int end = startIndex + length - value.Length;
        for (int i = startIndex; i <= end; i++)
        {
            int valueIndex = 0;
            while (valueIndex < value.Length && char.ToUpperInvariant(source[i + valueIndex]) == char.ToUpperInvariant(value[valueIndex]))
            {
                valueIndex++;
            }

            if (valueIndex == value.Length)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool ContainsAny(string source, string[] hints)
    {
        if (string.IsNullOrWhiteSpace(source) || hints == null)
        {
            return false;
        }

        for (int i = 0; i < hints.Length; i++)
        {
            if (source.IndexOf(hints[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool StartsWithDevice(string path, string deviceTag)
    {
        return path.StartsWith(deviceTag, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] GetGroupHints(InputDeviceWatcher.InputDeviceCategory category)
    {
        switch (category)
        {
            case InputDeviceWatcher.InputDeviceCategory.Keyboard:
                return KeyboardGroupHints;
            case InputDeviceWatcher.InputDeviceCategory.Xbox:
                return XboxGroupHints;
            case InputDeviceWatcher.InputDeviceCategory.PlayStation:
                return PlayStationGroupHints;
            case InputDeviceWatcher.InputDeviceCategory.Switch:
                return SwitchGroupHints;
            default:
                return OtherGamepadGroupHints;
        }
    }

    private static string GetEffectivePath(InputBinding binding)
    {
        return string.IsNullOrWhiteSpace(binding.effectivePath) ? binding.path : binding.effectivePath;
    }

    private static bool MatchesControlPath(string path, InputDeviceWatcher.InputDeviceCategory category)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        switch (category)
        {
            case InputDeviceWatcher.InputDeviceCategory.Keyboard:
                return StartsWithDevice(path, "<Keyboard>") || StartsWithDevice(path, "<Mouse>");
            case InputDeviceWatcher.InputDeviceCategory.Xbox:
                return StartsWithDevice(path, "<Gamepad>") || StartsWithDevice(path, "<Joystick>") || ContainsAny(path, XboxGroupHints);
            case InputDeviceWatcher.InputDeviceCategory.PlayStation:
                return StartsWithDevice(path, "<Gamepad>") || StartsWithDevice(path, "<Joystick>") || ContainsAny(path, PlayStationGroupHints);
            case InputDeviceWatcher.InputDeviceCategory.Switch:
                return StartsWithDevice(path, "<Gamepad>") || StartsWithDevice(path, "<Joystick>") || ContainsAny(path, SwitchGroupHints);
            default:
                return StartsWithDevice(path, "<Gamepad>") || StartsWithDevice(path, "<Joystick>") || ContainsAny(path, OtherGamepadGroupHints);
        }
    }
}

#endif
