#if TEXTMESHPRO_SUPPORT
using System;
using System.Collections.Generic;
using System.Reflection;
using AlicizaX.Localization;
using UnityEditor;

namespace UnityEngine.UI
{
    internal static class UXTextMeshProLocalization
    {
        [MenuItem("AlicizaX/Localization/Update UXTextMeshPro Localization Refrencee")]
        static void UpdateUXTextMeshProLocalizationRefrence()
        {
            List<GameLocaizationTable> allTables = new();
            Dictionary<int, TableSelectionData> allTableNames = new();

            string[] tablesGuids = AssetDatabase.FindAssets("t:GameLocaizationTable");
            foreach (string guid in tablesGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                GameLocaizationTable table = AssetDatabase.LoadAssetAtPath<GameLocaizationTable>(assetPath);
                if (table != null)
                {
                    allTables.Add(table);
                }
            }

            foreach (var table in allTables)
            {
                foreach (var sheet in table.TableSheet)
                {
                    foreach (var selection in sheet.SectionSheet)
                    {
                        string combineKey = $"{table.name}/{sheet.SectionName}/{selection.Key}";
                        string combineValue = $"{sheet.SectionName}.{selection.Key}";
                        int id = selection.Id;
                        allTableNames.TryAdd(id, new TableSelectionData(id, combineKey, combineValue));
                    }
                }
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                GameObject prefabInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (prefabInstance == null) continue;


                UXTextMeshPro[] components = prefabInstance.GetComponentsInChildren<UXTextMeshPro>(true);
                bool updated = false;

                foreach (UXTextMeshPro component in components)
                {
                    if (component == null)
                        continue;

                    FieldInfo localizationId = component.GetType().GetField("m_localizationID", BindingFlags.NonPublic | BindingFlags.Instance);
                    FieldInfo localizationKey = component.GetType().GetField("m_localizationKey", BindingFlags.NonPublic | BindingFlags.Instance);

                    int localizationIDValue = (int)localizationId.GetValue(component);
                    string localizationKeyValue = (string)localizationKey.GetValue(component);
                    if (allTableNames.TryGetValue(localizationIDValue, out TableSelectionData data))
                    {
                        if (!localizationKeyValue.Equals(data.CombineValue))
                        {
                            updated = true;
                            component.GetType().GetField("m_localizationKey", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(component, data.CombineValue);
                        }
                    }
                }

                if (updated)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabInstance, assetPath);
                    Debug.Log($"Updated Refrence in '{prefab.name}' ({assetPath})");
                }

                Object.DestroyImmediate(prefabInstance);
            }

            Debug.Log("Update Localization Refrencee Compeleted");
        }
    }
}

#endif
