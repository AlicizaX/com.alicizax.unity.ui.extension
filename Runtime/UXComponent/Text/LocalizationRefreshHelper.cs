#if UNITY_EDITOR && TEXTMESHPRO_SUPPORT
using System;
using System.Collections.Generic;
using System.IO;
using AlicizaX.Localization.Runtime;
using UnityEditor;
using UnityEngine;

namespace UnityEngine.UI
{
    [InitializeOnLoad]
    public static class LocalizationRefreshHelper
    {
        private const string DefaultLanguage = "ChineseSimplified";
        private const string NoneLanguage = "None";

        public static Func<string> ResolveLocalizationConstPath;

        private static readonly Dictionary<int, LocalizationEntry> EntryById = new();
        private static readonly Dictionary<string, LocalizationEntry> EntryByKey = new(StringComparer.Ordinal);
        private static bool isCacheDirty = true;
        private static string cachedLanguage;
        private static string cachedPath;
        private static long cachedFileTimestamp;

        static LocalizationRefreshHelper()
        {
            EditorApplication.projectChanged += InvalidateCache;
        }

        internal static IReadOnlyDictionary<int, LocalizationEntry> EntriesById
        {
            get
            {
                RefreshCacheIfNeeded();
                return EntryById;
            }
        }

        internal static IReadOnlyDictionary<string, LocalizationEntry> EntriesByKey
        {
            get
            {
                RefreshCacheIfNeeded();
                return EntryByKey;
            }
        }

        internal static string GetPreviewLabel(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return key;
            }

            RefreshCacheIfNeeded();
            return EntryByKey.TryGetValue(key, out LocalizationEntry entry) ? entry.PreviewText : key;
        }

        internal static void InvalidateCache()
        {
            isCacheDirty = true;
        }

        private static void RefreshCacheIfNeeded()
        {
            string language = EditorPrefs.GetString(LocalizationComponent.PrefsKey, NoneLanguage);
            string path = GetConfigFilePath();
            long fileTimestamp = GetConfigTimestamp(path);
            if (!isCacheDirty && cachedLanguage == language && cachedPath == path && cachedFileTimestamp == fileTimestamp)
            {
                return;
            }

            RebuildCache(language, path, fileTimestamp);
        }

        private static void RebuildCache(string language, string path, long fileTimestamp)
        {
            EntryById.Clear();
            EntryByKey.Clear();
            cachedLanguage = language;
            cachedPath = path;
            cachedFileTimestamp = fileTimestamp;
            isCacheDirty = false;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                LocalizationConfig config = JsonUtility.FromJson<LocalizationConfig>("{\"items\":" + json + "}");
                if (config?.items == null)
                {
                    Debug.LogWarning($"Failed to parse localization config '{path}'. Expected a JSON array.");
                    return;
                }

                if (config.items.Length == 0)
                {
                    return;
                }

                string previewLanguage = NormalizeLanguage(language);
                for (int i = 0; i < config.items.Length; i++)
                {
                    LocalizationJsonItem item = config.items[i];
                    if (item == null || item.id <= 0 || string.IsNullOrEmpty(item.key))
                    {
                        continue;
                    }

                    var entry = new LocalizationEntry(
                        item.id,
                        item.sheet,
                        item.key,
                        GetLocalizedText(item, previewLanguage));

                    EntryById.TryAdd(entry.Id, entry);
                    EntryByKey.TryAdd(entry.Key, entry);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to parse localization config '{path}'.\n{exception}");
            }
        }

        private static string NormalizeLanguage(string language)
        {
            return string.IsNullOrEmpty(language) || string.Equals(language, NoneLanguage, StringComparison.Ordinal)
                ? DefaultLanguage
                : language;
        }

        private static string GetLocalizedText(LocalizationJsonItem item, string language)
        {
            return language switch
            {
                nameof(LocalizationJsonItem.ChineseSimplified) => item.ChineseSimplified,
                nameof(LocalizationJsonItem.ChineseTraditional) => item.ChineseTraditional,
                nameof(LocalizationJsonItem.English) => item.English,
                nameof(LocalizationJsonItem.Japanese) => item.Japanese,
                nameof(LocalizationJsonItem.Korean) => item.Korean,
                nameof(LocalizationJsonItem.French) => item.French,
                nameof(LocalizationJsonItem.German) => item.German,
                nameof(LocalizationJsonItem.Spanish) => item.Spanish,
                nameof(LocalizationJsonItem.Portuguese) => item.Portuguese,
                nameof(LocalizationJsonItem.Russian) => item.Russian,
                nameof(LocalizationJsonItem.Italian) => item.Italian,
                nameof(LocalizationJsonItem.Dutch) => item.Dutch,
                nameof(LocalizationJsonItem.Turkish) => item.Turkish,
                nameof(LocalizationJsonItem.Vietnamese) => item.Vietnamese,
                nameof(LocalizationJsonItem.Thai) => item.Thai,
                nameof(LocalizationJsonItem.Indonesian) => item.Indonesian,
                nameof(LocalizationJsonItem.Arabic) => item.Arabic,
                nameof(LocalizationJsonItem.Hindi) => item.Hindi,
                _ => item.ChineseSimplified
            } ?? item.ChineseSimplified ?? item.English ?? item.key;
        }

        private static long GetConfigTimestamp(string path)
        {
            return !string.IsNullOrEmpty(path) && File.Exists(path)
                ? File.GetLastWriteTimeUtc(path).Ticks
                : 0;
        }

        private static string GetConfigFilePath()
        {
            string path = ResolveLocalizationConstPath?.Invoke();
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        }

        [Serializable]
        private sealed class LocalizationConfig
        {
            public LocalizationJsonItem[] items;
        }

        [Serializable]
        private sealed class LocalizationJsonItem
        {
            public string sheet;
            public int id;
            public string key;
            public string ChineseSimplified;
            public string ChineseTraditional;
            public string English;
            public string Japanese;
            public string Korean;
            public string French;
            public string German;
            public string Spanish;
            public string Portuguese;
            public string Russian;
            public string Italian;
            public string Dutch;
            public string Turkish;
            public string Vietnamese;
            public string Thai;
            public string Indonesian;
            public string Arabic;
            public string Hindi;
        }
    }

    internal readonly struct LocalizationEntry
    {
        public readonly int Id;
        public readonly string Sheet;
        public readonly string Key;
        public readonly string PreviewText;

        public LocalizationEntry(int id, string sheet, string key, string previewText)
        {
            Id = id;
            Sheet = sheet;
            Key = key;
            PreviewText = previewText;
        }
    }
}
#endif
