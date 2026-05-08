#if TEXTMESHPRO_SUPPORT

using System.Collections.Generic;
using System.Linq;
using AlicizaX.Localization;
using AlicizaX.Localization.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;

namespace UnityEngine.UI
{
    internal readonly struct TableSelectionData
    {
        public readonly int Id;
        public readonly string CombineKey; // SectionName/Key → 菜单层级
        public readonly string CombineValue; // SectionName.Key → 存储用

        public TableSelectionData(int id, string combineKey, string combineValue)
        {
            Id = id;
            CombineKey = combineKey;
            CombineValue = combineValue;
        }
    }

    [CustomEditor(typeof(UXTextMeshPro), true)]
    [CanEditMultipleObjects]
    internal class UXTextMeshProEditor : TMPro.EditorUtilities.TMP_EditorPanelUI
    {
        private SerializedProperty localizationID;
        private SerializedProperty m_localizationKey;

        private List<GameLocaizationTable> allTables = new();
        private Dictionary<int, TableSelectionData> allTableNames = new();
        private Dictionary<string, string> previewLabelDic = new();
        private List<string> allSelection = new();
        private int selectedSelectionIndex = 0;

        public override VisualElement CreateInspectorGUI()
        {
            RefreshAllTables();
            return base.CreateInspectorGUI();
        }

        private void RefreshAllTables()
        {
            allTables.Clear();
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

            InitAllTables();
        }

        private void InitAllTables()
        {
            allTableNames.Clear();
            allSelection.Clear();
            previewLabelDic.Clear();
            allSelection.Add("None");
            allTableNames.TryAdd(0, new TableSelectionData(0, "None", string.Empty));

            string commentLanguage = LocalizationConfiguration.Instance.GenerateScriptCodeFirstConfigName;
            foreach (var table in allTables)
            {
                var localization = table.Languages.Find(t => t != null && t.LanguageName == commentLanguage);
                if (localization == null)
                {
                    continue;
                }

                foreach (var item in localization.Strings)
                {
                    previewLabelDic.TryAdd(item.Key, item.Value);
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
                        allSelection.Add(combineKey);
                    }
                }
            }
        }

        protected string GetPreviewLabel()
        {
            return previewLabelDic.TryGetValue(m_localizationKey.stringValue, out var label)
                ? label
                : "None";
        }


        protected void FindSelectSelection()
        {
            selectedSelectionIndex = 0;
            if (allTableNames.TryGetValue(localizationID.intValue, out TableSelectionData data))
            {
                int idx = allSelection.FindIndex(t => t == data.CombineKey);
                if (idx >= 0)
                    selectedSelectionIndex = idx;
            }
        }

        protected void RefreshKeyValue()
        {
            if (allTableNames.TryGetValue(localizationID.intValue, out TableSelectionData data))
            {
                m_localizationKey.stringValue = data.CombineValue;
                serializedObject.ApplyModifiedProperties();
            }
        }

        protected override void OnEnable()
        {
            localizationID = serializedObject.FindProperty("m_localizationID");
            m_localizationKey = serializedObject.FindProperty("m_localizationKey");

            RefreshAllTables();
            FindSelectSelection();
            RefreshKeyValue();
            base.OnEnable();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // m_localizationKey 只读
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(m_localizationKey);
            EditorGUILayout.LabelField("Text", GetPreviewLabel());
            EditorGUI.EndDisabledGroup();

            // 检查是否找不到对应 ID 的 key
            if (localizationID.intValue > 0 && !allTableNames.ContainsKey(localizationID.intValue))
            {
                m_localizationKey.stringValue = string.Empty;
                EditorGUILayout.HelpBox($"已选择的多语言 Key (ID={localizationID.intValue}) 已被删除，但仍然保留该 ID。", MessageType.Warning);
            }

            EditorGUILayout.Space();

            // 下拉按钮
            if (GUILayout.Button(allSelection[selectedSelectionIndex], EditorStyles.popup))
            {
                var mousePos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                SearchWindow.Open(
                    new SearchWindowContext(mousePos),
                    ScriptableObject.CreateInstance<LocalizationSearchProvider>().Init(allSelection, ApplySelection)
                );
            }

            serializedObject.ApplyModifiedProperties();
            base.OnInspectorGUI();
        }

        private void ApplySelection(string selectedKey)
        {
            if (selectedKey == "None")
            {
                localizationID.intValue = 0;
                m_localizationKey.stringValue = string.Empty;
                selectedSelectionIndex = 0;
                (target as TextMeshProUGUI).text = string.Empty;
            }
            else
            {
                foreach (var kvp in allTableNames)
                {
                    if (kvp.Value.CombineKey == selectedKey)
                    {
                        localizationID.intValue = kvp.Value.Id;
                        m_localizationKey.stringValue = kvp.Value.CombineValue;
                        selectedSelectionIndex = allSelection.IndexOf(kvp.Value.CombineKey);
                        break;
                    }
                }
            }
            serializedObject.ApplyModifiedProperties();
        }
    }

    internal class LocalizationSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private List<string> options;
        private System.Action<string> onSelect;

        public LocalizationSearchProvider Init(List<string> options, System.Action<string> onSelect)
        {
            this.options = options;
            this.onSelect = onSelect;
            return this;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Localization Keys"), 0)
            };

            foreach (var option in options)
            {
                if (option == "None")
                {
                    tree.Add(new SearchTreeEntry(new GUIContent("None")) { level = 1, userData = "None" });
                }
                else
                {
                    // `/` 自动分层
                    string[] parts = option.Split('/');
                    if (parts.Length == 1)
                    {
                        tree.Add(new SearchTreeEntry(new GUIContent(parts[0])) { level = 1, userData = option });
                    }
                    else
                    {
                        // 前面的部分作为 group
                        for (int i = 0; i < parts.Length - 1; i++)
                        {
                            // 确保 group 唯一
                            string groupName = string.Join("/", parts, 0, i + 1);
                            if (!tree.Exists(e => e.content.text == parts[i] && e.level == i + 1))
                            {
                                tree.Add(new SearchTreeGroupEntry(new GUIContent(parts[i])) { level = i + 1 });
                            }
                        }

                        // 最后部分作为 Entry
                        tree.Add(new SearchTreeEntry(new GUIContent(parts[^1])) { level = parts.Length, userData = option });
                    }
                }
            }

            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            if (searchTreeEntry.userData is string key)
            {
                onSelect?.Invoke(key);
                return true;
            }

            return false;
        }
    }
}
#endif
