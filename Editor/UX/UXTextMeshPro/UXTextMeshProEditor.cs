#if TEXTMESHPRO_SUPPORT

using System;
using System.Collections.Generic;
using AlicizaX.Localization;
using AlicizaX.Localization.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

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

    internal static class UXTextMeshProLocalizationTableUtility
    {
        internal const string NoneSelection = "None";
        internal const string MixedSelection = "Mixed Values";

        internal static void RebuildSelectionData(
            Dictionary<int, TableSelectionData> selectionById,
            Dictionary<string, TableSelectionData> selectionByKey = null,
            List<string> selectionOptions = null,
            Dictionary<string, string> previewLabelByKey = null,
            string previewLanguage = null,
            bool includeNone = true)
        {
            selectionById?.Clear();
            selectionByKey?.Clear();
            selectionOptions?.Clear();
            previewLabelByKey?.Clear();

            if (includeNone)
            {
                AddSelection(
                    new TableSelectionData(0, NoneSelection, string.Empty),
                    selectionById,
                    selectionByKey,
                    selectionOptions);
            }

            string[] guids = AssetDatabase.FindAssets("t:GameLocaizationTable");
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameLocaizationTable table = AssetDatabase.LoadAssetAtPath<GameLocaizationTable>(assetPath);
                if (table == null)
                {
                    continue;
                }

                AddPreviewLabels(table, previewLanguage, previewLabelByKey);
                AddSelections(table, selectionById, selectionByKey, selectionOptions);
            }
        }

        private static void AddSelections(
            GameLocaizationTable table,
            Dictionary<int, TableSelectionData> selectionById,
            Dictionary<string, TableSelectionData> selectionByKey,
            List<string> selectionOptions)
        {
            if (table.TableSheet == null)
            {
                return;
            }

            for (int i = 0; i < table.TableSheet.Count; i++)
            {
                GameLocaizationTable.TableData sheet = table.TableSheet[i];
                if (string.IsNullOrEmpty(sheet.SectionName) || sheet.SectionSheet == null)
                {
                    continue;
                }

                for (int j = 0; j < sheet.SectionSheet.Count; j++)
                {
                    GameLocaizationTable.SheetItem selection = sheet.SectionSheet[j];
                    if (selection.Id <= 0 || string.IsNullOrEmpty(selection.Key))
                    {
                        continue;
                    }

                    string combineKey = $"{table.name}/{sheet.SectionName}/{selection.Key}";
                    string combineValue = $"{sheet.SectionName}.{selection.Key}";
                    AddSelection(
                        new TableSelectionData(selection.Id, combineKey, combineValue),
                        selectionById,
                        selectionByKey,
                        selectionOptions);
                }
            }
        }

        private static void AddPreviewLabels(
            GameLocaizationTable table,
            string previewLanguage,
            Dictionary<string, string> previewLabelByKey)
        {
            if (previewLabelByKey == null || string.IsNullOrEmpty(previewLanguage) || table.Languages == null)
            {
                return;
            }

            LocalizationLanguage localization = table.Languages.Find(t => t != null && t.LanguageName == previewLanguage);
            if (localization?.Strings == null)
            {
                return;
            }

            for (int i = 0; i < localization.Strings.Count; i++)
            {
                LocalizationLanguage.LocalizationString item = localization.Strings[i];
                if (item == null || string.IsNullOrEmpty(item.Key))
                {
                    continue;
                }

                previewLabelByKey.TryAdd(item.Key, item.Value);
            }
        }

        private static void AddSelection(
            TableSelectionData data,
            Dictionary<int, TableSelectionData> selectionById,
            Dictionary<string, TableSelectionData> selectionByKey,
            List<string> selectionOptions)
        {
            bool addedById = selectionById == null || selectionById.TryAdd(data.Id, data);
            if (!addedById)
            {
                return;
            }

            selectionByKey?.TryAdd(data.CombineKey, data);
            selectionOptions?.Add(data.CombineKey);
        }
    }

    [CustomEditor(typeof(UXTextMeshPro), true)]
    [CanEditMultipleObjects]
    internal class UXTextMeshProEditor : TMPro.EditorUtilities.TMP_EditorPanelUI
    {
        private SerializedProperty localizationID;
        private SerializedProperty m_localizationKey;

        private readonly Dictionary<int, TableSelectionData> selectionById = new();
        private readonly Dictionary<string, TableSelectionData> selectionByKey = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> previewLabelByKey = new(StringComparer.Ordinal);
        private readonly List<string> allSelection = new();
        private int selectedSelectionIndex;

        public override VisualElement CreateInspectorGUI()
        {
            RefreshLocalizationData();
            return base.CreateInspectorGUI();
        }

        private void RefreshLocalizationData()
        {
            UXTextMeshProLocalizationTableUtility.RebuildSelectionData(
                selectionById,
                selectionByKey,
                allSelection,
                previewLabelByKey,
                LocalizationConfiguration.Instance.GenerateScriptCodeFirstConfigName);
        }

        protected string GetPreviewLabel()
        {
            if (m_localizationKey == null || m_localizationKey.hasMultipleDifferentValues)
            {
                return UXTextMeshProLocalizationTableUtility.MixedSelection;
            }

            return previewLabelByKey.TryGetValue(m_localizationKey.stringValue, out string label)
                ? label
                : UXTextMeshProLocalizationTableUtility.NoneSelection;
        }


        protected void FindSelectSelection()
        {
            selectedSelectionIndex = 0;
            if (localizationID == null || localizationID.hasMultipleDifferentValues)
            {
                return;
            }

            if (selectionById.TryGetValue(localizationID.intValue, out TableSelectionData data))
            {
                int idx = allSelection.FindIndex(t => t == data.CombineKey);
                if (idx >= 0)
                {
                    selectedSelectionIndex = idx;
                }
            }
        }

        protected void RefreshKeyValue()
        {
            if (localizationID == null || m_localizationKey == null || localizationID.hasMultipleDifferentValues)
            {
                return;
            }

            if (selectionById.TryGetValue(localizationID.intValue, out TableSelectionData data) &&
                !string.Equals(m_localizationKey.stringValue, data.CombineValue, StringComparison.Ordinal))
            {
                m_localizationKey.stringValue = data.CombineValue;
                serializedObject.ApplyModifiedProperties();
            }
        }

        protected override void OnEnable()
        {
            localizationID = serializedObject.FindProperty("m_localizationID");
            m_localizationKey = serializedObject.FindProperty("m_localizationKey");

            RefreshLocalizationData();
            FindSelectSelection();
            RefreshKeyValue();
            base.OnEnable();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            FindSelectSelection();

            // m_localizationKey 只读
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(m_localizationKey);
                EditorGUILayout.LabelField("Text", GetPreviewLabel());
            }

            // 检查是否找不到对应 ID 的 key
            if (!localizationID.hasMultipleDifferentValues &&
                localizationID.intValue > 0 &&
                !selectionById.ContainsKey(localizationID.intValue))
            {
                m_localizationKey.stringValue = string.Empty;
                EditorGUILayout.HelpBox($"已选择的多语言 Key (ID={localizationID.intValue}) 已被删除，但仍然保留该 ID。", MessageType.Warning);
            }

            EditorGUILayout.Space();

            // 下拉按钮
            if (GUILayout.Button(GetSelectionButtonLabel(), EditorStyles.popup))
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
            serializedObject.Update();

            if (selectedKey == UXTextMeshProLocalizationTableUtility.NoneSelection)
            {
                localizationID.intValue = 0;
                m_localizationKey.stringValue = string.Empty;
                selectedSelectionIndex = 0;
                ClearTargetText();
            }
            else if (selectionByKey.TryGetValue(selectedKey, out TableSelectionData data))
            {
                localizationID.intValue = data.Id;
                m_localizationKey.stringValue = data.CombineValue;
                selectedSelectionIndex = allSelection.IndexOf(data.CombineKey);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private string GetSelectionButtonLabel()
        {
            if (localizationID != null && localizationID.hasMultipleDifferentValues)
            {
                return UXTextMeshProLocalizationTableUtility.MixedSelection;
            }

            return selectedSelectionIndex >= 0 && selectedSelectionIndex < allSelection.Count
                ? allSelection[selectedSelectionIndex]
                : UXTextMeshProLocalizationTableUtility.NoneSelection;
        }

        private void ClearTargetText()
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is not TextMeshProUGUI textComponent)
                {
                    continue;
                }

                Undo.RecordObject(textComponent, "Clear Localization Text");
                textComponent.text = string.Empty;
                EditorUtility.SetDirty(textComponent);
            }
        }
    }

    internal class LocalizationSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        private List<string> options;
        private Action<string> onSelect;

        public LocalizationSearchProvider Init(List<string> options, Action<string> onSelect)
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
            var groupPaths = new HashSet<string>(StringComparer.Ordinal);

            if (options == null)
            {
                return tree;
            }

            foreach (string option in options)
            {
                if (string.IsNullOrEmpty(option))
                {
                    continue;
                }

                if (option == UXTextMeshProLocalizationTableUtility.NoneSelection)
                {
                    tree.Add(new SearchTreeEntry(new GUIContent(UXTextMeshProLocalizationTableUtility.NoneSelection)) { level = 1, userData = option });
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
                            if (groupPaths.Add(groupName))
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
