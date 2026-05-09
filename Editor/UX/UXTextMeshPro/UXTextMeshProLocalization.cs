#if TEXTMESHPRO_SUPPORT
using System.Collections.Generic;
using UnityEditor;

namespace UnityEngine.UI
{
    internal static class UXTextMeshProLocalization
    {
        private const string LocalizationIdPropertyName = "m_localizationID";
        private const string LocalizationKeyPropertyName = "m_localizationKey";

        [MenuItem("AlicizaX/Localization/Update UXTextMeshPro Localization Reference")]
        private static void UpdateUXTextMeshProLocalizationReference()
        {
            UpdateLocalizationReferences();
        }

        private static void UpdateLocalizationReferences()
        {
            var selectionById = new Dictionary<int, TableSelectionData>();
            UXTextMeshProLocalizationTableUtility.RebuildSelectionData(
                selectionById,
                includeNone: false);

            int checkedPrefabs = 0;
            int updatedPrefabs = 0;
            int updatedComponents = 0;

            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < guids.Length; i++)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                    checkedPrefabs++;
                    try
                    {
                        if (UpdatePrefab(assetPath, selectionById, out int componentCount))
                        {
                            updatedPrefabs++;
                            updatedComponents += componentCount;
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

            Debug.Log($"Update UXTextMeshPro localization references completed. Checked {checkedPrefabs} prefabs, updated {updatedComponents} components in {updatedPrefabs} prefabs.");
        }

        private static bool UpdatePrefab(
            string assetPath,
            IReadOnlyDictionary<int, TableSelectionData> selectionById,
            out int updatedComponents)
        {
            updatedComponents = 0;

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

                    if (!selectionById.TryGetValue(localizationId.intValue, out TableSelectionData data) ||
                        localizationKey.stringValue == data.CombineValue)
                    {
                        continue;
                    }

                    localizationKey.stringValue = data.CombineValue;
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    updatedComponents++;
                }

                if (updatedComponents <= 0)
                {
                    return false;
                }

                PrefabUtility.SaveAsPrefabAsset(prefabContents, assetPath);
                Debug.Log($"Updated UXTextMeshPro localization references in '{prefabContents.name}' ({assetPath})");
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
    }
}

#endif
