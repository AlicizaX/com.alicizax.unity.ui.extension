using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public sealed class GlyphEntry
{
    public Sprite Sprite;
    public InputAction action;
}

[Serializable]
public sealed class DeviceGlyphTable
{
    public string deviceName;
    public Texture2D spriteSheetTexture;
    public Sprite platformIcons;
    public List<GlyphEntry> entries = new List<GlyphEntry>();
}

[CreateAssetMenu(fileName = "InputGlyphDatabase", menuName = "GameplaySystem/Input/InputGlyphDatabase", order = 400)]
public sealed class InputGlyphDatabase : ScriptableObject
{
    private const string DeviceKeyboard = "Keyboard";
    private const string DeviceXbox = "Xbox";
    private const string DevicePlayStation = "PlayStation";
    private const string DeviceOther = "Other";
    private const int CategoryCount = 4;
    private const int InitialPathCapacity = 128;
    private static readonly InputDeviceWatcher.InputDeviceCategory[] KeyboardLookupOrder = { InputDeviceWatcher.InputDeviceCategory.Keyboard };
    private static readonly InputDeviceWatcher.InputDeviceCategory[] XboxLookupOrder =
    {
        InputDeviceWatcher.InputDeviceCategory.Xbox,
        InputDeviceWatcher.InputDeviceCategory.Other,
        InputDeviceWatcher.InputDeviceCategory.Keyboard,
    };
    private static readonly InputDeviceWatcher.InputDeviceCategory[] PlayStationLookupOrder =
    {
        InputDeviceWatcher.InputDeviceCategory.PlayStation,
        InputDeviceWatcher.InputDeviceCategory.Other,
        InputDeviceWatcher.InputDeviceCategory.Keyboard,
    };
    private static readonly InputDeviceWatcher.InputDeviceCategory[] OtherLookupOrder =
    {
        InputDeviceWatcher.InputDeviceCategory.Other,
        InputDeviceWatcher.InputDeviceCategory.Xbox,
        InputDeviceWatcher.InputDeviceCategory.Keyboard,
    };

    public List<DeviceGlyphTable> tables = new List<DeviceGlyphTable>();
    public Sprite placeholderSprite;

    private DeviceGlyphTable[] _tableByCategory = new DeviceGlyphTable[CategoryCount];
    private PathLookup[] _pathLookupByCategory = new PathLookup[CategoryCount];
    private bool _cacheBuilt;

    private struct PathLookup
    {
        public string[] Keys;
        public Sprite[] Sprites;
        public int Count;
    }

    private void OnEnable()
    {
        BuildCache();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        BuildCache();
    }
#endif

    public DeviceGlyphTable GetTable(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName) || tables == null)
        {
            return null;
        }

        EnsureCache();
        return GetTable(ParseCategory(deviceName));
    }

    public DeviceGlyphTable GetTable(InputDeviceWatcher.InputDeviceCategory device)
    {
        EnsureCache();
        int index = CategoryIndex(device);
        DeviceGlyphTable table = _tableByCategory[index];
        if (table != null)
        {
            return table;
        }

        return device == InputDeviceWatcher.InputDeviceCategory.Other ? _tableByCategory[CategoryIndex(InputDeviceWatcher.InputDeviceCategory.Xbox)] : null;
    }

    public Sprite GetPlatformIcon(InputDeviceWatcher.InputDeviceCategory device)
    {
        DeviceGlyphTable table = GetTable(device);
        return table != null ? table.platformIcons : null;
    }

    public bool TryGetSprite(string controlPath, InputDeviceWatcher.InputDeviceCategory device, out Sprite sprite)
    {
        EnsureCache();
        string key = NormalizeControlPath(controlPath);
        if (string.IsNullOrEmpty(key))
        {
            sprite = placeholderSprite;
            return sprite != null;
        }

        InputDeviceWatcher.InputDeviceCategory[] lookupOrder = GetLookupOrder(device);
        for (int i = 0; i < lookupOrder.Length; i++)
        {
            if (TryGetSpriteInCategory(lookupOrder[i], key, out sprite))
            {
                return true;
            }
        }

        sprite = placeholderSprite;
        return sprite != null;
    }

    public Sprite GetSprite(string controlPath, InputDeviceWatcher.InputDeviceCategory device)
    {
        return TryGetSprite(controlPath, device, out Sprite sprite) ? sprite : placeholderSprite;
    }

    public GlyphEntry FindEntryByControlPath(string controlPath, InputDeviceWatcher.InputDeviceCategory device)
    {
        if (!TryGetSprite(controlPath, device, out Sprite sprite) || sprite == null)
        {
            return null;
        }

        InputDeviceWatcher.InputDeviceCategory[] lookupOrder = GetLookupOrder(device);
        for (int i = 0; i < lookupOrder.Length; i++)
        {
            DeviceGlyphTable table = GetTable(lookupOrder[i]);
            if (table == null || table.entries == null)
            {
                continue;
            }

            for (int j = 0; j < table.entries.Count; j++)
            {
                GlyphEntry entry = table.entries[j];
                if (entry != null && entry.Sprite == sprite)
                {
                    return entry;
                }
            }
        }

        return null;
    }

    private void EnsureCache()
    {
        if (!_cacheBuilt)
        {
            BuildCache();
        }
    }

    private void BuildCache()
    {
        if (_tableByCategory == null || _tableByCategory.Length != CategoryCount)
        {
            _tableByCategory = new DeviceGlyphTable[CategoryCount];
        }
        else
        {
            Array.Clear(_tableByCategory, 0, _tableByCategory.Length);
        }

        if (_pathLookupByCategory == null || _pathLookupByCategory.Length != CategoryCount)
        {
            _pathLookupByCategory = new PathLookup[CategoryCount];
        }

        for (int i = 0; i < CategoryCount; i++)
        {
            ResetLookup(ref _pathLookupByCategory[i]);
        }

        _cacheBuilt = true;
        if (tables == null)
        {
            return;
        }

        for (int i = 0; i < tables.Count; i++)
        {
            DeviceGlyphTable table = tables[i];
            if (table == null || string.IsNullOrWhiteSpace(table.deviceName))
            {
                continue;
            }

            InputDeviceWatcher.InputDeviceCategory category = ParseCategory(table.deviceName);
            int categoryIndex = CategoryIndex(category);
            _tableByCategory[categoryIndex] = table;
            RegisterEntries(table, ref _pathLookupByCategory[categoryIndex]);
        }
    }

#if UNITY_EDITOR
    public void EditorRefreshCache()
    {
        BuildCache();
    }

    public static string EditorNormalizeControlPath(string controlPath)
    {
        return NormalizeControlPath(controlPath);
    }
#endif

    private static void ResetLookup(ref PathLookup lookup)
    {
        if (lookup.Keys == null || lookup.Keys.Length == 0)
        {
            lookup.Keys = new string[InitialPathCapacity];
            lookup.Sprites = new Sprite[InitialPathCapacity];
        }
        else
        {
            Array.Clear(lookup.Keys, 0, lookup.Count);
            Array.Clear(lookup.Sprites, 0, lookup.Count);
        }

        lookup.Count = 0;
    }

    private void RegisterEntries(DeviceGlyphTable table, ref PathLookup lookup)
    {
        if (table.entries == null)
        {
            return;
        }

        for (int i = 0; i < table.entries.Count; i++)
        {
            GlyphEntry entry = table.entries[i];
            if (entry == null || entry.Sprite == null || entry.action == null)
            {
                continue;
            }

            for (int j = 0; j < entry.action.bindings.Count; j++)
            {
                RegisterBinding(ref lookup, entry.action.bindings[j].path, entry.Sprite);
                RegisterBinding(ref lookup, entry.action.bindings[j].effectivePath, entry.Sprite);
            }
        }
    }

    private static void RegisterBinding(ref PathLookup lookup, string controlPath, Sprite sprite)
    {
        string key = NormalizeControlPath(controlPath);
        if (string.IsNullOrEmpty(key) || IndexOf(lookup.Keys, lookup.Count, key) >= 0)
        {
            return;
        }

        if (lookup.Count == lookup.Keys.Length)
        {
            Array.Resize(ref lookup.Keys, lookup.Keys.Length << 1);
            Array.Resize(ref lookup.Sprites, lookup.Sprites.Length << 1);
        }

        lookup.Keys[lookup.Count] = key;
        lookup.Sprites[lookup.Count] = sprite;
        lookup.Count++;
    }

    private bool TryGetSpriteInCategory(InputDeviceWatcher.InputDeviceCategory category, string key, out Sprite sprite)
    {
        PathLookup lookup = _pathLookupByCategory[CategoryIndex(category)];
        int index = IndexOf(lookup.Keys, lookup.Count, key);
        if (index >= 0)
        {
            sprite = lookup.Sprites[index];
            return sprite != null;
        }

        sprite = null;
        return false;
    }

    private static int IndexOf(string[] keys, int count, string key)
    {
        for (int i = 0; i < count; i++)
        {
            if (string.Equals(keys[i], key, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static string NormalizeControlPath(string controlPath)
    {
        if (string.IsNullOrWhiteSpace(controlPath))
        {
            return string.Empty;
        }

        return CanonicalizeDeviceLayout(controlPath.Trim().ToLowerInvariant());
    }

    private static string CanonicalizeDeviceLayout(string controlPath)
    {
        int start = controlPath.IndexOf('<');
        int end = controlPath.IndexOf('>');
        if (start < 0 || end <= start + 1)
        {
            return controlPath;
        }

        string layout = controlPath.Substring(start + 1, end - start - 1);
        string canonicalLayout = GetCanonicalLayout(layout);
        if (string.Equals(layout, canonicalLayout, StringComparison.Ordinal))
        {
            return controlPath;
        }

        return controlPath.Substring(0, start + 1) + canonicalLayout + controlPath.Substring(end);
    }

    private static string GetCanonicalLayout(string layout)
    {
        if (string.IsNullOrEmpty(layout))
        {
            return string.Empty;
        }

        if (layout.IndexOf("keyboard", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "keyboard";
        }

        if (layout.IndexOf("mouse", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "mouse";
        }

        if (layout.IndexOf("joystick", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "joystick";
        }

        if (layout.IndexOf("gamepad", StringComparison.OrdinalIgnoreCase) >= 0
            || layout.IndexOf("controller", StringComparison.OrdinalIgnoreCase) >= 0
            || layout.IndexOf("xinput", StringComparison.OrdinalIgnoreCase) >= 0
            || layout.IndexOf("dualshock", StringComparison.OrdinalIgnoreCase) >= 0
            || layout.IndexOf("dualsense", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "gamepad";
        }

        return layout;
    }

    private static InputDeviceWatcher.InputDeviceCategory ParseCategory(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return InputDeviceWatcher.InputDeviceCategory.Other;
        }

        if (deviceName.Equals(DeviceKeyboard, StringComparison.OrdinalIgnoreCase))
        {
            return InputDeviceWatcher.InputDeviceCategory.Keyboard;
        }

        if (deviceName.Equals(DeviceXbox, StringComparison.OrdinalIgnoreCase))
        {
            return InputDeviceWatcher.InputDeviceCategory.Xbox;
        }

        if (deviceName.Equals(DevicePlayStation, StringComparison.OrdinalIgnoreCase))
        {
            return InputDeviceWatcher.InputDeviceCategory.PlayStation;
        }

        return InputDeviceWatcher.InputDeviceCategory.Other;
    }

    private static int CategoryIndex(InputDeviceWatcher.InputDeviceCategory category)
    {
        switch (category)
        {
            case InputDeviceWatcher.InputDeviceCategory.Keyboard:
                return 0;
            case InputDeviceWatcher.InputDeviceCategory.Xbox:
                return 1;
            case InputDeviceWatcher.InputDeviceCategory.PlayStation:
                return 2;
            default:
                return 3;
        }
    }

    private static InputDeviceWatcher.InputDeviceCategory[] GetLookupOrder(InputDeviceWatcher.InputDeviceCategory device)
    {
        switch (device)
        {
            case InputDeviceWatcher.InputDeviceCategory.Keyboard:
                return KeyboardLookupOrder;
            case InputDeviceWatcher.InputDeviceCategory.Xbox:
                return XboxLookupOrder;
            case InputDeviceWatcher.InputDeviceCategory.PlayStation:
                return PlayStationLookupOrder;
            default:
                return OtherLookupOrder;
        }
    }
}