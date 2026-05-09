#if UNITY_EDITOR
using System.Collections.Generic;
using AlicizaX.Localization;
using AlicizaX.Localization.Runtime;
using UnityEditor;

namespace UnityEngine.UI
{
    [InitializeOnLoad]
    internal static class LocalizationRefreshHelper
    {
        private const string DefaultLanguage = "None";

        private static readonly Dictionary<string, string> PreviewLabelByKey = new();
        private static bool isCacheDirty = true;
        private static string cachedLanguage;

        static LocalizationRefreshHelper()
        {
            EditorApplication.projectChanged += InvalidateCache;
        }

        internal static string GetPreviewLabel(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return key;
            }

            RefreshCacheIfNeeded();
            return PreviewLabelByKey.TryGetValue(key, out string label) ? label : key;
        }

        private static void InvalidateCache()
        {
            isCacheDirty = true;
        }

        private static void RefreshCacheIfNeeded()
        {
            string language = EditorPrefs.GetString(LocalizationComponent.PrefsKey, DefaultLanguage);
            if (!isCacheDirty && cachedLanguage == language)
            {
                return;
            }

            RebuildPreviewLabels(language);
        }

        private static void RebuildPreviewLabels(string language)
        {
            PreviewLabelByKey.Clear();
            cachedLanguage = language;
            isCacheDirty = false;

            if (string.IsNullOrEmpty(language) || language == DefaultLanguage)
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:GameLocaizationTable");
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameLocaizationTable table = AssetDatabase.LoadAssetAtPath<GameLocaizationTable>(assetPath);
                if (table?.Languages == null)
                {
                    continue;
                }

                LocalizationLanguage localization = table.Languages.Find(t => t != null && t.LanguageName == language);
                if (localization?.Strings == null)
                {
                    continue;
                }

                for (int j = 0; j < localization.Strings.Count; j++)
                {
                    LocalizationLanguage.LocalizationString item = localization.Strings[j];
                    if (item == null || string.IsNullOrEmpty(item.Key))
                    {
                        continue;
                    }

                    PreviewLabelByKey.TryAdd(item.Key, item.Value);
                }
            }
        }
    }
}

#endif
