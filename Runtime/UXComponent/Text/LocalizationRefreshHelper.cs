#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using AlicizaX.Localization;
using AlicizaX.Localization.Runtime;
using UnityEditor;

namespace UnityEngine.UI
{
    internal static class LocalizationRefreshHelper
    {
        private static Dictionary<string, string> previewLabelDic = new();

        internal static string GetPreviewLabel(string key)
        {
            Init();
            if (previewLabelDic.ContainsKey(key))
            {
                return previewLabelDic[key];
            }

            return key;
        }

        static void Init()
        {
            previewLabelDic.Clear();
            List<GameLocaizationTable> allTables = new();
            string[] guids = AssetDatabase.FindAssets("t:GameLocaizationTable");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                GameLocaizationTable table = AssetDatabase.LoadAssetAtPath<GameLocaizationTable>(assetPath);
                if (table != null)
                {
                    allTables.Add(table);
                }
            }

            string language = EditorPrefs.GetString(LocalizationComponent.PrefsKey, "None");
            var localizationLanguage = allTables.Select(e => e.Languages.Find(t => t.LanguageName == language)).ToList();
            foreach (var localization in localizationLanguage)
            {
                foreach (var item in localization.Strings)
                {
                    previewLabelDic.TryAdd(item.Key, item.Value);
                }
            }
        }
    }
}

#endif
