#if TEXTMESHPRO_SUPPORT
using System;
using System.Collections.Generic;
using UnityEditor;

namespace UnityEngine.UI
{
    internal static class UXTextMeshProLocalization
    {
        private const string LocalizationIdPropertyName = "m_localizationID";
        private const string LocalizationKeyPropertyName = "m_localizationKey";

        [MenuItem("AlicizaX/Localization/Update UXTextMeshPro Localization Reference", false, 200)]
        private static void UpdateUXTextMeshProLocalizationReference()
        {
            UpdateLocalizationReferences();
        }

        private static void UpdateLocalizationReferences()
        {
            LocalizationRefreshHelper.InvalidateCache();

            var selectionById = new Dictionary<int, TableSelectionData>();
            var selectionByLocalizationKey = new Dictionary<string, int>(StringComparer.Ordinal);
            UXTextMeshProLocalizationTableUtility.RebuildSelectionData(
                selectionById,
                includeNone: false);
            foreach (KeyValuePair<string, LocalizationEntry> pair in LocalizationRefreshHelper.EntriesByKey)
            {
                LocalizationEntry entry = pair.Value;
                if (!string.IsNullOrEmpty(entry.Key))
                {
                    selectionByLocalizationKey.TryAdd(entry.Key, entry.Id);
                }
            }

            var stats = new LocalizationReferenceUpdateStats();

            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < guids.Length; i++)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    stats.CheckedPrefabs++;
                    try
                    {
                        if (UpdatePrefab(assetPath, selectionById, selectionByLocalizationKey, stats))
                        {
                            stats.UpdatedPrefabs++;
                        }
                    }
                    catch (System.Exception exception)
                    {
                        Debug.LogError($"Failed to update UXTextMeshPro localization references in '{assetPath}'.\n{exception}");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            string summary = BuildSummary(stats);
            Debug.Log(summary);
            EditorUtility.DisplayDialog(GetDialogTitle(), summary, "OK");
        }

        private static bool UpdatePrefab(
            string assetPath,
            IReadOnlyDictionary<int, TableSelectionData> selectionById,
            IReadOnlyDictionary<string, int> selectionByKey,
            LocalizationReferenceUpdateStats stats)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            GameObject prefabContents = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                if (prefabContents == null)
                {
                    return false;
                }

                int updatedComponentsInPrefab = 0;
                UXTextMeshPro[] components = prefabContents.GetComponentsInChildren<UXTextMeshPro>(true);
                for (int i = 0; i < components.Length; i++)
                {
                    UXTextMeshPro component = components[i];
                    if (component == null)
                    {
                        continue;
                    }

                    var serializedObject = new SerializedObject(component);
                    SerializedProperty localizationId = serializedObject.FindProperty(LocalizationIdPropertyName);
                    SerializedProperty localizationKey = serializedObject.FindProperty(LocalizationKeyPropertyName);
                    if (localizationId == null || localizationKey == null)
                    {
                        continue;
                    }

                    if (UpdateComponentReference(
                            component,
                            assetPath,
                            serializedObject,
                            localizationId,
                            localizationKey,
                            selectionById,
                            selectionByKey,
                            stats))
                    {
                        stats.UpdatedComponents++;
                        updatedComponentsInPrefab++;
                    }
                }

                if (updatedComponentsInPrefab <= 0)
                {
                    return false;
                }

                PrefabUtility.SaveAsPrefabAsset(prefabContents, assetPath);
                Debug.Log($"Updated {updatedComponentsInPrefab} UXTextMeshPro localization references in '{prefabContents.name}' ({assetPath})");
                return true;
            }
            finally
            {
                if (prefabContents != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabContents);
                }
            }
        }

        private static bool UpdateComponentReference(
            UXTextMeshPro component,
            string assetPath,
            SerializedObject serializedObject,
            SerializedProperty localizationId,
            SerializedProperty localizationKey,
            IReadOnlyDictionary<int, TableSelectionData> selectionById,
            IReadOnlyDictionary<string, int> selectionByKey,
            LocalizationReferenceUpdateStats stats)
        {
            int oldId = localizationId.intValue;
            string oldKey = localizationKey.stringValue;
            int idByKey = 0;
            bool hasIdMatch = selectionById.TryGetValue(oldId, out TableSelectionData dataById);
            bool hasKeyMatch = !string.IsNullOrEmpty(oldKey) && selectionByKey.TryGetValue(oldKey, out idByKey);

            if (hasIdMatch && !string.Equals(oldKey, dataById.CombineValue, StringComparison.Ordinal))
            {
                localizationKey.stringValue = dataById.CombineValue;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                stats.UpdatedKeys++;
                Debug.Log($"Updated UXTextMeshPro localization key. Prefab: '{assetPath}', Component: '{GetComponentPath(component)}', ID: {oldId}, Key: '{oldKey}' -> '{dataById.CombineValue}'");
                return true;
            }

            if (hasKeyMatch && oldId != idByKey)
            {
                localizationId.intValue = idByKey;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                stats.UpdatedIds++;
                Debug.Log($"Updated UXTextMeshPro localization ID. Prefab: '{assetPath}', Component: '{GetComponentPath(component)}', Key: '{oldKey}', ID: {oldId} -> {idByKey}");
                return true;
            }

            if (!hasIdMatch && !hasKeyMatch && HasAssignedLocalization(oldId, oldKey))
            {
                stats.MissingComponents++;
                Debug.LogWarning($"Missing UXTextMeshPro localization reference. Prefab: '{assetPath}', Component: '{GetComponentPath(component)}', ID: {oldId}, Key: '{oldKey}'");
            }

            return false;
        }

        private static bool HasAssignedLocalization(int localizationId, string localizationKey)
        {
            return localizationId > 0 &&
                   !string.IsNullOrWhiteSpace(localizationKey) &&
                   !string.Equals(localizationKey, UXTextMeshProLocalizationTableUtility.NoneSelection, StringComparison.Ordinal);
        }

        private static string GetComponentPath(UXTextMeshPro component)
        {
            if (component == null)
            {
                return "<null>";
            }

            var names = new List<string>();
            Transform current = component.transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names);
        }

        private static string BuildSummary(LocalizationReferenceUpdateStats stats)
        {
            return $"{GetDialogTitle()} completed.\n" +
                   $"Checked {stats.CheckedPrefabs} prefabs.\n" +
                   $"Updated {stats.UpdatedComponents} components in {stats.UpdatedPrefabs} prefabs.\n" +
                   $"Updated {stats.UpdatedKeys} keys.\n" +
                   $"Updated {stats.UpdatedIds} IDs.\n" +
                   $"Missing {stats.MissingComponents} components.";
        }

        private static string GetDialogTitle()
        {
            return "Update UXTextMeshPro Localization References";
        }

        private sealed class LocalizationReferenceUpdateStats
        {
            public int CheckedPrefabs;
            public int UpdatedPrefabs;
            public int UpdatedComponents;
            public int UpdatedKeys;
            public int UpdatedIds;
            public int MissingComponents;
        }
    }
}
#endif
