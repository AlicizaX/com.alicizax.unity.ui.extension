#if TEXTMESHPRO_SUPPORT
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEngine.UI
{
    internal readonly struct TableSelectionData
    {
        public readonly int Id;
        public readonly string CombineKey;
        public readonly string CombineValue;

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

            foreach (KeyValuePair<int, LocalizationEntry> pair in LocalizationRefreshHelper.EntriesById)
            {
                LocalizationEntry entry = pair.Value;
                string menuPath = GetMenuPath(entry.Key);
                string combineKey = string.IsNullOrEmpty(entry.Sheet) ? menuPath : $"{entry.Sheet}/{menuPath}";
                var selectionData = new TableSelectionData(entry.Id, combineKey, entry.Key);
                AddSelection(selectionData, selectionById, selectionByKey, selectionOptions);

                if (!string.IsNullOrEmpty(entry.Key))
                {
                    previewLabelByKey?.TryAdd(entry.Key, entry.PreviewText);
                }
            }
        }

        private static string GetMenuPath(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return key;
            }

            return key.Replace('.', '/');
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
            LocalizationRefreshHelper.InvalidateCache();
            RefreshLocalizationData();
            return base.CreateInspectorGUI();
        }

        private void RefreshLocalizationData()
        {
            UXTextMeshProLocalizationTableUtility.RebuildSelectionData(
                selectionById,
                selectionByKey,
                allSelection,
                previewLabelByKey);
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
            RefreshLocalizationData();
            FindSelectSelection();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(m_localizationKey);
                EditorGUILayout.LabelField("Text", GetPreviewLabel());
            }

            if (!localizationID.hasMultipleDifferentValues &&
                localizationID.intValue > 0 &&
                !selectionById.ContainsKey(localizationID.intValue))
            {
                m_localizationKey.stringValue = string.Empty;
                EditorGUILayout.HelpBox($"Selected localization ID ({localizationID.intValue}) was not found in LocalizationConst.json.", MessageType.Warning);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button(GetSelectionButtonLabel(), EditorStyles.popup))
            {
                var mousePos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
                SearchWindow.Open(
                    new SearchWindowContext(mousePos, 420f),
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
                RefreshTargetText(data.CombineValue);
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

        private void RefreshTargetText(string key)
        {
            string previewText = LocalizationRefreshHelper.GetPreviewLabel(key);
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is not TextMeshProUGUI textComponent)
                {
                    continue;
                }

                Undo.RecordObject(textComponent, "Refresh Localization Text");
                textComponent.text = previewText;
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
                    continue;
                }

                string[] parts = option.Split('/');
                if (parts.Length == 1)
                {
                    tree.Add(new SearchTreeEntry(new GUIContent(parts[0])) { level = 1, userData = option });
                    continue;
                }

                for (int i = 0; i < parts.Length - 1; i++)
                {
                    string groupName = string.Join("/", parts, 0, i + 1);
                    if (groupPaths.Add(groupName))
                    {
                        tree.Add(new SearchTreeGroupEntry(new GUIContent(parts[i])) { level = i + 1 });
                    }
                }

                tree.Add(new SearchTreeEntry(new GUIContent(GetSearchableLeafLabel(option))) { level = parts.Length, userData = option });
            }

            return tree;
        }

        private static string GetSearchableLeafLabel(string option)
        {
            return option.Replace('/', '.');
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
