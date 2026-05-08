#if UNITY_EDITOR
using System.Collections.Generic;
using AlicizaX.Editor;
using UnityEditor;
using UnityEngine;

namespace UnityEngine.UI
{
    [CustomEditor(typeof(UXBinding))]
    public sealed class UXBindingEditor : UnityEditor.Editor
    {
        private const float PropertyToolbarHeight = 30f;
        private const float PropertyRowHeight = 24f;
        private const float PropertyActionButtonSize = 20f;
        private const float PropertyLabelWidth = 86f;
        private const float AddRulePopupWidth = 360f;
        private const float AddRulePopupHeight = 320f;

        private readonly struct AddRuleOption
        {
            public readonly string ControllerId;
            public readonly string ControllerName;
            public readonly UXBindingProperty Property;
            public readonly string PropertyName;

            public AddRuleOption(string controllerId, string controllerName, UXBindingProperty property, string propertyName)
            {
                ControllerId = controllerId;
                ControllerName = controllerName;
                Property = property;
                PropertyName = propertyName;
            }
        }

        private sealed class AddRulePopup : PopupWindowContent
        {
            private const float SearchHeight = 22f;
            private const float RowHeight = 32f;
            private const string SearchControlName = "UXBindingAddRuleSearch";

            private readonly UXBindingEditor _editor;
            private readonly UXBinding _binding;
            private readonly List<AddRuleOption> _options;
            private readonly bool _showControllerName;
            private readonly List<string> _categoryBuffer = new List<string>();
            private readonly GUIContent _nextContent = EditorGUIUtility.IconContent("d_forward", "Open category");
            private readonly GUIContent _backContent = EditorGUIUtility.IconContent("d_back", "Back");
            private string _search = string.Empty;
            private string _activePath = string.Empty;
            private Vector2 _scroll;
            private bool _focusSearch = true;

            public AddRulePopup(UXBindingEditor editor, UXBinding binding, List<AddRuleOption> options, bool showControllerName)
            {
                _editor = editor;
                _binding = binding;
                _options = new List<AddRuleOption>(options);
                _showControllerName = showControllerName;
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(AddRulePopupWidth, AddRulePopupHeight);
            }

            public override void OnGUI(Rect rect)
            {
                AlicizaEditorGUI.DrawPopupBackground(rect);

                Rect searchRect = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, SearchHeight);
                DrawSearchField(searchRect);

                Rect viewRect = new Rect(rect.x, searchRect.yMax, rect.width, rect.yMax - searchRect.yMax);
                bool searching = !string.IsNullOrWhiteSpace(_search);
                int rowCount = searching ? CountMatches() : CountHierarchyRows();
                Rect contentRect = new Rect(0f, 0f, Mathf.Max(1f, viewRect.width), Mathf.Max(viewRect.height, rowCount * RowHeight));
                _scroll = GUI.BeginScrollView(viewRect, _scroll, contentRect, GUIStyle.none, GUI.skin.verticalScrollbar);

                if (searching)
                {
                    DrawSearchRows(contentRect);
                }
                else
                {
                    DrawHierarchyRows(contentRect);
                }

                GUI.EndScrollView();

                if (rowCount == 0)
                {
                    GUI.Label(new Rect(viewRect.x, viewRect.y + 18f, viewRect.width, 20f), "No matching property", AlicizaEditorGUI.Styles.EmptyState);
                }
            }

            private static bool IsMatch(AddRuleOption option, string search)
            {
                if (string.IsNullOrEmpty(search))
                {
                    return true;
                }

                return option.ControllerName.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                       option.PropertyName.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0;
            }

            private void DrawSearchField(Rect rect)
            {
                _search = AlicizaEditorGUI.DrawSearchField(rect, _search, "Type property name...", SearchControlName, ref _focusSearch);
            }

            private void DrawSearchRows(Rect contentRect)
            {
                int visibleIndex = 0;
                for (int i = 0; i < _options.Count; i++)
                {
                    AddRuleOption option = _options[i];
                    if (!IsMatch(option, _search))
                    {
                        continue;
                    }

                    Rect rowRect = GetRowRect(contentRect, visibleIndex);
                    DrawOptionRow(rowRect, option, option.PropertyName, _showControllerName ? option.ControllerName : string.Empty);
                    visibleIndex++;
                }
            }

            private void DrawHierarchyRows(Rect contentRect)
            {
                int visibleIndex = 0;
                _categoryBuffer.Clear();

                if (!string.IsNullOrEmpty(_activePath))
                {
                    DrawBackRow(GetRowRect(contentRect, visibleIndex));
                    visibleIndex++;
                }

                for (int i = 0; i < _options.Count; i++)
                {
                    AddRuleOption option = _options[i];
                    if (!TryGetHierarchySegment(option.PropertyName, _activePath, out string segment, out bool leaf))
                    {
                        continue;
                    }

                    if (!leaf)
                    {
                        if (_categoryBuffer.Contains(segment))
                        {
                            continue;
                        }

                        _categoryBuffer.Add(segment);
                        DrawCategoryRow(GetRowRect(contentRect, visibleIndex), segment);
                        visibleIndex++;
                        continue;
                    }

                    DrawOptionRow(GetRowRect(contentRect, visibleIndex), option, segment, _showControllerName ? option.ControllerName : string.Empty);
                    visibleIndex++;
                }
            }

            private void DrawOptionRow(Rect rowRect, AddRuleOption option, string label, string subLabel)
            {
                bool hovered = rowRect.Contains(Event.current.mousePosition);
                AlicizaEditorGUI.DrawPopupRowBackground(rowRect, hovered);

                Rect glyphRect = new Rect(rowRect.x + 10f, rowRect.y + 4f, 28f, rowRect.height - 8f);
                GUI.Label(glyphRect, GetKindBadge(UXBindingPropertyUtility.GetMetadata(option.Property).ValueKind), AlicizaEditorGUI.Styles.KindBadge);

                float textX = glyphRect.xMax + 6f;
                Rect labelRect = new Rect(textX, rowRect.y + 5f, rowRect.width - textX - 36f, 15f);
                GUI.Label(labelRect, label, AlicizaEditorGUI.Styles.RowLabel);

                if (!string.IsNullOrEmpty(subLabel))
                {
                    Rect subLabelRect = new Rect(labelRect.x, rowRect.y + 19f, labelRect.width, 12f);
                    GUI.Label(subLabelRect, subLabel, AlicizaEditorGUI.Styles.MutedMiniLabel);
                }

                GUI.Label(new Rect(rowRect.xMax - 23f, rowRect.y + 2f, 18f, rowRect.height - 4f), "+", AlicizaEditorGUI.Styles.ButtonGlyph);

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rowRect.Contains(Event.current.mousePosition))
                {
                    _editor.AddEntry(_binding, option.ControllerId, option.Property);
                    editorWindow.Close();
                    Event.current.Use();
                }
            }

            private void DrawCategoryRow(Rect rowRect, string category)
            {
                bool hovered = rowRect.Contains(Event.current.mousePosition);
                AlicizaEditorGUI.DrawPopupRowBackground(rowRect, hovered);

                GUI.Label(new Rect(rowRect.x + 10f, rowRect.y + 3f, 28f, rowRect.height - 6f), _nextContent, AlicizaEditorGUI.Styles.KindBadge);
                GUI.Label(new Rect(rowRect.x + 44f, rowRect.y + 3f, rowRect.width - 72f, 18f), category, AlicizaEditorGUI.Styles.RowLabel);
                GUI.Label(new Rect(rowRect.xMax - 23f, rowRect.y + 2f, 18f, rowRect.height - 4f), _nextContent, AlicizaEditorGUI.Styles.ButtonGlyph);

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rowRect.Contains(Event.current.mousePosition))
                {
                    _activePath = string.IsNullOrEmpty(_activePath) ? category : _activePath + "/" + category;
                    _scroll = Vector2.zero;
                    Event.current.Use();
                }
            }

            private void DrawBackRow(Rect rowRect)
            {
                bool hovered = rowRect.Contains(Event.current.mousePosition);
                AlicizaEditorGUI.DrawPopupHeaderRowBackground(rowRect, hovered);
                GUI.Label(new Rect(rowRect.x + 10f, rowRect.y + 3f, 28f, rowRect.height - 6f), _backContent, AlicizaEditorGUI.Styles.ButtonGlyph);
                GUI.Label(new Rect(rowRect.x + 44f, rowRect.y + 3f, rowRect.width - 58f, 18f), _activePath, AlicizaEditorGUI.Styles.RowLabel);

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && rowRect.Contains(Event.current.mousePosition))
                {
                    int slashIndex = _activePath.LastIndexOf('/');
                    _activePath = slashIndex < 0 ? string.Empty : _activePath.Substring(0, slashIndex);
                    _scroll = Vector2.zero;
                    Event.current.Use();
                }
            }

            private int CountMatches()
            {
                int count = 0;
                for (int i = 0; i < _options.Count; i++)
                {
                    if (IsMatch(_options[i], _search))
                    {
                        count++;
                    }
                }

                return count;
            }

            private int CountHierarchyRows()
            {
                int count = string.IsNullOrEmpty(_activePath) ? 0 : 1;
                _categoryBuffer.Clear();

                for (int i = 0; i < _options.Count; i++)
                {
                    AddRuleOption option = _options[i];
                    if (!TryGetHierarchySegment(option.PropertyName, _activePath, out string segment, out bool leaf))
                    {
                        continue;
                    }

                    if (leaf)
                    {
                        count++;
                        continue;
                    }

                    if (_categoryBuffer.Contains(segment))
                    {
                        continue;
                    }

                    _categoryBuffer.Add(segment);
                    count++;
                }

                return count;
            }

            private static Rect GetRowRect(Rect contentRect, int visibleIndex)
            {
                return new Rect(8f, visibleIndex * RowHeight, contentRect.width - 16f, RowHeight);
            }

            private static bool TryGetHierarchySegment(string propertyName, string activePath, out string segment, out bool leaf)
            {
                segment = string.Empty;
                leaf = false;

                string remaining = propertyName;
                if (!string.IsNullOrEmpty(activePath))
                {
                    string prefix = activePath + "/";
                    if (!propertyName.StartsWith(prefix, System.StringComparison.Ordinal))
                    {
                        return false;
                    }

                    remaining = propertyName.Substring(prefix.Length);
                }

                int slashIndex = remaining.IndexOf('/');
                if (slashIndex < 0)
                {
                    segment = remaining;
                    leaf = !string.IsNullOrEmpty(segment);
                    return leaf;
                }

                segment = remaining.Substring(0, slashIndex);
                return !string.IsNullOrEmpty(segment);
            }
        }

        private SerializedProperty _controllerProp;
        private SerializedProperty _entriesProp;
        private readonly Dictionary<int, bool> _foldouts = new Dictionary<int, bool>();
        private readonly List<UXBindingProperty> _supportedProperties = new List<UXBindingProperty>();
        private string[] _controllerNames = System.Array.Empty<string>();
        private GUIStyle _pillOn;
        private GUIStyle _pillOff;
        private GUIStyle _panelStyle;
        private GUIStyle _entryBodyStyle;
        private GUIStyle _fieldRowStyle;
        private GUIStyle _propertyLabelStyle;
        private GUIStyle _entryTitleStyle;
        private GUIStyle _entryMetaStyle;
        private GUIStyle _entryKindStyle;
        private GUIStyle _entryHandleStyle;
        private GUIStyle _emptyStateStyle;
        private GUIStyle _statusLabelStyle;
        private readonly List<AddRuleOption> _addRuleOptions = new List<AddRuleOption>();
        private GUIContent _addRuleContent;
        private GUIContent _autoBindContent;
        private GUIContent _captureContent;
        private GUIContent _resetContent;
        private GUIContent _expandAllContent;
        private GUIContent _collapseAllContent;

        private void OnEnable()
        {
            _controllerProp = serializedObject.FindProperty("_controller");
            _entriesProp = serializedObject.FindProperty("_entries");
            InitializeContents();
        }

        private void InitializeContents()
        {
            _addRuleContent = EditorGUIUtility.IconContent("Toolbar Plus", "Add binding rule");
            _autoBindContent = EditorGUIUtility.IconContent("d_Prefab Icon", "Auto bind parent UXController");
            _captureContent = EditorGUIUtility.IconContent("d_SaveAs", "Capture defaults");
            _resetContent = EditorGUIUtility.IconContent("d_Refresh", "Reset to defaults");
            _expandAllContent = EditorGUIUtility.IconContent("d_scrollup", "Expand all rules");
            _collapseAllContent = EditorGUIUtility.IconContent("d_scrolldown", "Collapse all rules");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EnsureStyles();

            var binding = (UXBinding)target;

            EditorGUILayout.Space(6f);
            DrawEntries(binding);

            serializedObject.ApplyModifiedProperties();
        }


        private void DrawEntries(UXBinding binding)
        {
            UXBindingPropertyUtility.GetSupportedProperties(binding.gameObject, _supportedProperties);

            EditorGUILayout.BeginVertical(_panelStyle);
            DrawPropertyToolbar(binding);

            if (_entriesProp.arraySize == 0)
            {
                DrawEmptyState("No properties. Click + and choose a controller/property pair.");
                EditorGUILayout.EndVertical();
                return;
            }

            for (int i = 0; i < _entriesProp.arraySize; i++)
            {
                DrawEntry(binding, i);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawEntry(UXBinding binding, int index)
        {
            SerializedProperty entryProp = _entriesProp.GetArrayElementAtIndex(index);
            SerializedProperty controllerIdProp = entryProp.FindPropertyRelative("_controllerId");
            SerializedProperty controllerIndexProp = entryProp.FindPropertyRelative("_controllerIndex");
            SerializedProperty controllerIndexMaskProp = entryProp.FindPropertyRelative("_controllerIndexMask");
            SerializedProperty propertyProp = entryProp.FindPropertyRelative("_property");
            SerializedProperty valueProp = entryProp.FindPropertyRelative("_value");
            SerializedProperty indexedValuesProp = entryProp.FindPropertyRelative("_indexedValues");
            SerializedProperty fallbackModeProp = entryProp.FindPropertyRelative("_fallbackMode");
            SerializedProperty fallbackValueProp = entryProp.FindPropertyRelative("_fallbackValue");

            UXBindingProperty property = (UXBindingProperty)propertyProp.enumValueIndex;
            UXBindingPropertyMetadata metadata = UXBindingPropertyUtility.GetMetadata(property);
            bool expanded = _foldouts.ContainsKey(index) && _foldouts[index];
            string label = metadata.DisplayName;
            bool propertySupported = _supportedProperties.Contains(property);
            bool controllerResolved = TryGetControllerName(binding.Controller, controllerIdProp.stringValue, out string controllerName);

            if (property == UXBindingProperty.GameObjectActive)
            {
                ForceGameObjectActiveValues(valueProp, fallbackModeProp, fallbackValueProp);
            }

            int selectedIndex = GetFirstSelectedIndex(controllerIndexMaskProp.intValue);
            SerializedProperty previewValueProp = FindIndexedValuePropertyOrDefault(indexedValuesProp, valueProp, selectedIndex);
            string title = BuildEntryTitle(index, label, controllerName, controllerResolved);
            string summary = BuildEntrySummary(property, previewValueProp, metadata, controllerIndexMaskProp.intValue, fallbackModeProp, fallbackValueProp);

            expanded = DrawEntryHeader(binding, index, expanded, title, summary, metadata.ValueKind, controllerResolved, propertySupported);
            _foldouts[index] = expanded;

            if (expanded)
            {
                EditorGUILayout.BeginVertical(_entryBodyStyle);

                bool indexChanged = DrawControllerSelector(binding, controllerIdProp, controllerIndexProp, controllerIndexMaskProp, property == UXBindingProperty.GameObjectActive);

                if (property == UXBindingProperty.GameObjectActive)
                {
                    DrawStatusRow($"Visible: {BuildIndexLabel(controllerIndexMaskProp.intValue)}    Hidden: others");
                    if (indexChanged)
                    {
                        serializedObject.ApplyModifiedProperties();
                        binding.ApplyEntryValue(index, GetFirstSelectedIndex(controllerIndexMaskProp.intValue));
                        EditorUtility.SetDirty(binding);
                        SceneView.RepaintAll();
                    }
                }
                else
                {
                    selectedIndex = GetFirstSelectedIndex(controllerIndexMaskProp.intValue);
                    if (indexChanged)
                    {
                        serializedObject.ApplyModifiedProperties();
                        binding.ApplyEntryValue(index, selectedIndex);
                        EditorUtility.SetDirty(binding);
                        SceneView.RepaintAll();
                    }

                    SerializedProperty selectedValueProp = GetIndexedValueProperty(indexedValuesProp, valueProp, selectedIndex);

                    bool valueChanged = DrawValueField(selectedValueProp, metadata, $"Value {selectedIndex}", "Use Current", 112f, out bool useCurrentClicked);
                    if (valueChanged)
                    {
                        serializedObject.ApplyModifiedProperties();
                        binding.ApplyEntryValue(index, selectedIndex);
                        EditorUtility.SetDirty(binding);
                        SceneView.RepaintAll();
                    }

                    if (useCurrentClicked)
                    {
                        binding.CaptureEntryValue(index, selectedIndex);
                        EditorUtility.SetDirty(binding);
                    }

                    EditorGUILayout.BeginHorizontal(_fieldRowStyle);
                    EditorGUILayout.LabelField("Fallback", _propertyLabelStyle, GUILayout.Width(PropertyLabelWidth));
                    Rect fallbackRect = GUILayoutUtility.GetRect(90f, 20f, GUILayout.MinWidth(90f), GUILayout.ExpandWidth(true));
                    fallbackModeProp.enumValueIndex = AlicizaEditorGUI.DrawStyledPopup(fallbackRect, fallbackModeProp.enumValueIndex, fallbackModeProp.enumDisplayNames);
                    EditorGUILayout.EndHorizontal();

                    UXBindingFallbackMode fallbackMode = (UXBindingFallbackMode)fallbackModeProp.enumValueIndex;
                    if (fallbackMode == UXBindingFallbackMode.UseCustomValue)
                    {
                        DrawValueField(fallbackValueProp, metadata, "Fallback Value", "Use Current As Fallback", 160f, out bool useCurrentFallbackClicked);
                        if (useCurrentFallbackClicked)
                        {
                            binding.CaptureEntryFallbackValue(index);
                            EditorUtility.SetDirty(binding);
                        }
                    }
                }

                if (!controllerResolved)
                {
                    EditorGUILayout.HelpBox("Controller reference is missing or points to a deleted controller definition.", MessageType.Error);
                }

                if (!propertySupported)
                {
                    EditorGUILayout.HelpBox("This property is not supported by the components on the current GameObject.", MessageType.Error);
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawPropertyToolbar(UXBinding binding)
        {
            Rect toolbarRect = GUILayoutUtility.GetRect(1f, PropertyToolbarHeight, GUILayout.ExpandWidth(true));
            AlicizaEditorGUI.DrawToolbarBackground(toolbarRect);

            Rect addRect = new Rect(toolbarRect.xMax - 26f, toolbarRect.y + 5f, PropertyActionButtonSize, PropertyActionButtonSize);
            Rect expandRect = new Rect(addRect.x - 24f, toolbarRect.y + 5f, PropertyActionButtonSize, PropertyActionButtonSize);
            Rect collapseRect = new Rect(expandRect.x - 24f, toolbarRect.y + 5f, PropertyActionButtonSize, PropertyActionButtonSize);
            Rect resetRect = new Rect(collapseRect.x - 24f, toolbarRect.y + 5f, PropertyActionButtonSize, PropertyActionButtonSize);
            Rect captureRect = new Rect(resetRect.x - 24f, toolbarRect.y + 5f, PropertyActionButtonSize, PropertyActionButtonSize);
            Rect autoBindRect = new Rect(captureRect.x - 24f, toolbarRect.y + 5f, PropertyActionButtonSize, PropertyActionButtonSize);
            Rect objectRect = new Rect(toolbarRect.x + 6f, toolbarRect.y + 5f, Mathf.Max(120f, autoBindRect.x - toolbarRect.x - 12f), PropertyActionButtonSize);

            EditorGUI.BeginChangeCheck();
            UXController newController = (UXController)EditorGUI.ObjectField(objectRect, binding.Controller, typeof(UXController), true);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(binding, "Change UX Binding Controller");
                binding.SetController(newController);
                _controllerProp.objectReferenceValue = newController;
                EditorUtility.SetDirty(binding);
            }

            if (AlicizaEditorGUI.DrawToolbarButton(autoBindRect, _autoBindContent))
            {
                AutoBindController(binding);
            }

            if (AlicizaEditorGUI.DrawToolbarButton(captureRect, _captureContent))
            {
                binding.CaptureDefaults();
                EditorUtility.SetDirty(binding);
            }

            if (AlicizaEditorGUI.DrawToolbarButton(resetRect, _resetContent))
            {
                binding.ResetToDefaults();
                EditorUtility.SetDirty(binding);
                SceneView.RepaintAll();
            }

            if (AlicizaEditorGUI.DrawToolbarButton(collapseRect, _collapseAllContent))
            {
                SetAllFoldouts(false);
            }

            if (AlicizaEditorGUI.DrawToolbarButton(expandRect, _expandAllContent))
            {
                SetAllFoldouts(true);
            }

            if (AlicizaEditorGUI.DrawToolbarButton(addRect, _addRuleContent))
            {
                ShowAddRuleMenu(binding, GetCenteredAddRulePopupRect());
            }
        }

        private void DrawEmptyState(string message)
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 44f, GUILayout.ExpandWidth(true));
            AlicizaEditorGUI.DrawBodyBackground(rect);
            GUI.Label(rect, message, _emptyStateStyle);
        }

        private static Rect GetCenteredAddRulePopupRect()
        {
            Rect mainWindowRect = EditorGUIUtility.GetMainWindowPosition();
            Vector2 guiCenter = GUIUtility.ScreenToGUIPoint(mainWindowRect.center);
            return new Rect(
                guiCenter.x - AddRulePopupWidth * 0.5f,
                guiCenter.y - AddRulePopupHeight * 0.5f,
                AddRulePopupWidth,
                1f);
        }

        private bool DrawEntryHeader(
            UXBinding binding,
            int index,
            bool expanded,
            string title,
            string summary,
            UXBindingValueKind valueKind,
            bool controllerResolved,
            bool propertySupported)
        {
            Rect rowRect = GUILayoutUtility.GetRect(1f, PropertyRowHeight, GUILayout.ExpandWidth(true));
            bool hovered = rowRect.Contains(Event.current.mousePosition);
            AlicizaEditorGUI.DrawListItemBackground(rowRect, expanded, hovered);

            Rect deleteRect = new Rect(rowRect.xMax - 22f, rowRect.y, 22f, rowRect.height);
            Rect clickRect = new Rect(rowRect.x, rowRect.y, deleteRect.x - rowRect.x, rowRect.height);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && clickRect.Contains(Event.current.mousePosition))
            {
                expanded = !expanded;
                Event.current.Use();
            }

            Rect handleRect = new Rect(rowRect.x + 7f, rowRect.y + 3f, 16f, 18f);
            Rect foldRect = new Rect(handleRect.xMax + 2f, rowRect.y + 3f, 16f, 18f);
            Rect kindRect = new Rect(foldRect.xMax + 4f, rowRect.y + 3f, 34f, 18f);
            Rect titleRect = new Rect(kindRect.xMax + 6f, rowRect.y + 3f, Mathf.Max(80f, rowRect.width * 0.42f), 18f);
            Rect summaryRect = new Rect(titleRect.xMax + 8f, rowRect.y + 3f, Mathf.Max(40f, deleteRect.x - titleRect.xMax - 12f), 18f);

            GUI.Label(handleRect, "=", _entryHandleStyle);
            AlicizaEditorGUI.DrawFoldoutIcon(foldRect, expanded);
            GUI.Label(kindRect, GetKindBadge(valueKind), _entryKindStyle);
            GUI.Label(titleRect, title, controllerResolved && propertySupported ? _entryTitleStyle : _statusLabelStyle);
            GUI.Label(summaryRect, summary, _entryMetaStyle);

            if (AlicizaEditorGUI.DrawSymbolButton(deleteRect, "-"))
            {
                DeleteEntry(binding, index);
            }

            return expanded;
        }

        private static string BuildEntryTitle(int index, string propertyName, string controllerName, bool controllerResolved)
        {
            if (controllerResolved)
            {
                return $"[{index}] {controllerName} / {propertyName}";
            }

            return $"[{index}] Missing / {propertyName}";
        }

        private static string BuildEntrySummary(
            UXBindingProperty property,
            SerializedProperty valueProp,
            UXBindingPropertyMetadata metadata,
            int mask,
            SerializedProperty fallbackModeProp,
            SerializedProperty fallbackValueProp)
        {
            if (property == UXBindingProperty.GameObjectActive)
            {
                return $"visible: {BuildIndexLabel(mask)}";
            }

            UXBindingFallbackMode fallbackMode = (UXBindingFallbackMode)fallbackModeProp.enumValueIndex;
            string value = BuildValuePreview(valueProp, metadata);
            if (fallbackMode != UXBindingFallbackMode.UseCustomValue)
            {
                return value;
            }

            return $"{value}    fallback: {BuildValuePreview(fallbackValueProp, metadata)}";
        }

        private static SerializedProperty FindIndexedValuePropertyOrDefault(SerializedProperty indexedValuesProp, SerializedProperty defaultValueProp, int selectedIndex)
        {
            for (int i = 0; i < indexedValuesProp.arraySize; i++)
            {
                SerializedProperty indexedValueProp = indexedValuesProp.GetArrayElementAtIndex(i);
                if (indexedValueProp.FindPropertyRelative("_index").intValue == selectedIndex)
                {
                    return indexedValueProp.FindPropertyRelative("_value");
                }
            }

            return defaultValueProp;
        }

        private static string BuildValuePreview(SerializedProperty valueProp, UXBindingPropertyMetadata metadata)
        {
            switch (metadata.ValueKind)
            {
                case UXBindingValueKind.Boolean:
                    return valueProp.FindPropertyRelative("_boolValue").boolValue ? "true" : "false";
                case UXBindingValueKind.Float:
                    return valueProp.FindPropertyRelative("_floatValue").floatValue.ToString("0.###");
                case UXBindingValueKind.String:
                    return $"\"{TrimPreview(valueProp.FindPropertyRelative("_stringValue").stringValue)}\"";
                case UXBindingValueKind.Color:
                    return "#" + ColorUtility.ToHtmlStringRGBA(valueProp.FindPropertyRelative("_colorValue").colorValue);
                case UXBindingValueKind.Vector2:
                    {
                        Vector2 value = valueProp.FindPropertyRelative("_vector2Value").vector2Value;
                        return $"({value.x:0.##}, {value.y:0.##})";
                    }
                case UXBindingValueKind.Vector3:
                    {
                        Vector3 value = valueProp.FindPropertyRelative("_vector3Value").vector3Value;
                        return $"({value.x:0.##}, {value.y:0.##}, {value.z:0.##})";
                    }
                case UXBindingValueKind.ObjectReference:
                    {
                        Object value = valueProp.FindPropertyRelative("_objectValue").objectReferenceValue;
                        return value != null ? value.name : "None";
                    }
                default:
                    return string.Empty;
            }
        }

        private static string TrimPreview(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            const int maxLength = 36;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength - 3) + "...";
        }

        private void DrawReadOnlyTextRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal(_fieldRowStyle);
            EditorGUILayout.LabelField(label, _propertyLabelStyle, GUILayout.Width(PropertyLabelWidth));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(value);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatusRow(string message)
        {
            EditorGUILayout.BeginHorizontal(_fieldRowStyle);
            EditorGUILayout.LabelField("State", _propertyLabelStyle, GUILayout.Width(PropertyLabelWidth));
            EditorGUILayout.LabelField(message, _entryMetaStyle);
            EditorGUILayout.EndHorizontal();
        }

        private bool DrawControllerSelector(UXBinding binding, SerializedProperty controllerIdProp, SerializedProperty controllerIndexProp, SerializedProperty controllerIndexMaskProp, bool multiSelect)
        {
            UXController controller = binding.Controller;
            if (controller == null || controller.Controllers.Count == 0)
            {
                EditorGUILayout.HelpBox("Create a controller definition first.", MessageType.Info);
                return false;
            }

            bool changed = false;
            EditorGUILayout.BeginHorizontal(_fieldRowStyle);
            EditorGUILayout.LabelField("Controller", _propertyLabelStyle, GUILayout.Width(PropertyLabelWidth));
            EnsureStringArray(ref _controllerNames, controller.Controllers.Count);
            int selectedController = 0;

            for (int i = 0; i < controller.Controllers.Count; i++)
            {
                UXController.ControllerDefinition definition = controller.Controllers[i];
                _controllerNames[i] = definition.Name;
                if (definition.Id == controllerIdProp.stringValue)
                {
                    selectedController = i;
                }
            }

            EditorGUI.BeginChangeCheck();
            Rect controllerPopupRect = GUILayoutUtility.GetRect(90f, 20f, GUILayout.MinWidth(90f), GUILayout.ExpandWidth(true));
            selectedController = AlicizaEditorGUI.DrawStyledPopup(controllerPopupRect, selectedController, _controllerNames);
            if (EditorGUI.EndChangeCheck())
            {
                controllerIdProp.stringValue = controller.Controllers[selectedController].Id;
                controllerIndexProp.intValue = 0;
                controllerIndexMaskProp.intValue = 1;
                changed = true;
            }

            UXController.ControllerDefinition selectedDefinition = controller.Controllers[selectedController];
            int maxIndex = Mathf.Max(1, selectedDefinition.Length);
            controllerIndexProp.intValue = Mathf.Clamp(controllerIndexProp.intValue, 0, maxIndex - 1);
            controllerIndexMaskProp.intValue = ClampMask(controllerIndexMaskProp.intValue, maxIndex, controllerIndexProp.intValue);

            changed |= DrawIndexMask(controllerIndexMaskProp, controllerIndexProp, maxIndex, multiSelect);
            EditorGUILayout.EndHorizontal();
            return changed;
        }

        private bool DrawValueField(SerializedProperty valueProp, UXBindingPropertyMetadata metadata, string label, string actionLabel, float actionWidth, out bool actionClicked)
        {
            bool changed = false;
            actionClicked = false;

            switch (metadata.ValueKind)
            {
                case UXBindingValueKind.Boolean:
                    {
                        SerializedProperty boolProp = valueProp.FindPropertyRelative("_boolValue");
                        EditorGUILayout.BeginHorizontal(_fieldRowStyle);
                        EditorGUILayout.LabelField(label, _propertyLabelStyle, GUILayout.Width(PropertyLabelWidth));
                        EditorGUI.BeginChangeCheck();
                        boolProp.boolValue = EditorGUILayout.Toggle(boolProp.boolValue, GUILayout.Width(18f));
                        changed = EditorGUI.EndChangeCheck();
                        GUILayout.FlexibleSpace();
                        actionClicked = DrawInlineActionButton(actionLabel, actionWidth);
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                case UXBindingValueKind.Float:
                    {
                        SerializedProperty floatProp = valueProp.FindPropertyRelative("_floatValue");
                        EditorGUILayout.BeginHorizontal(_fieldRowStyle);
                        EditorGUILayout.LabelField(label, _propertyLabelStyle, GUILayout.Width(PropertyLabelWidth));
                        EditorGUI.BeginChangeCheck();
                        floatProp.floatValue = EditorGUILayout.FloatField(floatProp.floatValue);
                        changed = EditorGUI.EndChangeCheck();
                        actionClicked = DrawInlineActionButton(actionLabel, actionWidth);
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                case UXBindingValueKind.String:
                    {
                        SerializedProperty stringProp = valueProp.FindPropertyRelative("_stringValue");
                        EditorGUILayout.BeginVertical(_fieldRowStyle);
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(label, _propertyLabelStyle, GUILayout.Width(PropertyLabelWidth));
                        GUILayout.FlexibleSpace();
                        actionClicked = DrawInlineActionButton(actionLabel, actionWidth);
                        EditorGUILayout.EndHorizontal();
                        EditorGUI.BeginChangeCheck();
                        stringProp.stringValue = EditorGUILayout.TextArea(stringProp.stringValue, GUILayout.MinHeight(54f));
                        changed = EditorGUI.EndChangeCheck();
                        EditorGUILayout.EndVertical();
                        break;
                    }
                case UXBindingValueKind.Color:
                    {
                        SerializedProperty colorProp = valueProp.FindPropertyRelative("_colorValue");
                        EditorGUILayout.BeginHorizontal(_fieldRowStyle);
                        EditorGUILayout.LabelField(label, _propertyLabelStyle, GUILayout.Width(PropertyLabelWidth));
                        EditorGUI.BeginChangeCheck();
                        colorProp.colorValue = EditorGUILayout.ColorField(colorProp.colorValue);
                        changed = EditorGUI.EndChangeCheck();
                        actionClicked = DrawInlineActionButton(actionLabel, actionWidth);
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                case UXBindingValueKind.Vector2:
                    {
                        SerializedProperty vector2Prop = valueProp.FindPropertyRelative("_vector2Value");
                        EditorGUILayout.BeginHorizontal(_fieldRowStyle);
                        EditorGUILayout.LabelField(label, _propertyLabelStyle, GUILayout.Width(PropertyLabelWidth));
                        EditorGUI.BeginChangeCheck();
                        vector2Prop.vector2Value = EditorGUILayout.Vector2Field(string.Empty, vector2Prop.vector2Value);
                        changed = EditorGUI.EndChangeCheck();
                        actionClicked = DrawInlineActionButton(actionLabel, actionWidth);
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                case UXBindingValueKind.Vector3:
                    {
                        SerializedProperty vector3Prop = valueProp.FindPropertyRelative("_vector3Value");
                        EditorGUILayout.BeginHorizontal(_fieldRowStyle);
                        EditorGUILayout.LabelField(label, _propertyLabelStyle, GUILayout.Width(PropertyLabelWidth));
                        EditorGUI.BeginChangeCheck();
                        vector3Prop.vector3Value = EditorGUILayout.Vector3Field(string.Empty, vector3Prop.vector3Value);
                        changed = EditorGUI.EndChangeCheck();
                        actionClicked = DrawInlineActionButton(actionLabel, actionWidth);
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
                case UXBindingValueKind.ObjectReference:
                    {
                        SerializedProperty objectProp = valueProp.FindPropertyRelative("_objectValue");
                        EditorGUILayout.BeginHorizontal(_fieldRowStyle);
                        EditorGUILayout.LabelField(label, _propertyLabelStyle, GUILayout.Width(PropertyLabelWidth));
                        EditorGUI.BeginChangeCheck();
                        objectProp.objectReferenceValue = EditorGUILayout.ObjectField(
                            objectProp.objectReferenceValue,
                            metadata.ObjectReferenceType,
                            false);
                        changed = EditorGUI.EndChangeCheck();
                        actionClicked = DrawInlineActionButton(actionLabel, actionWidth);
                        EditorGUILayout.EndHorizontal();
                        break;
                    }
            }

            return changed;
        }

        private bool DrawInlineActionButton(string actionLabel, float width)
        {
            return AlicizaEditorGUI.DrawInlineButton(actionLabel, width);
        }

        private void AddEntry(UXBinding binding, string controllerId, UXBindingProperty property)
        {
            int index = _entriesProp.arraySize;
            _entriesProp.InsertArrayElementAtIndex(index);

            SerializedProperty entryProp = _entriesProp.GetArrayElementAtIndex(index);
            SerializedProperty controllerIdProp = entryProp.FindPropertyRelative("_controllerId");
            SerializedProperty controllerIndexProp = entryProp.FindPropertyRelative("_controllerIndex");
            SerializedProperty controllerIndexMaskProp = entryProp.FindPropertyRelative("_controllerIndexMask");
            SerializedProperty propertyProp = entryProp.FindPropertyRelative("_property");
            SerializedProperty fallbackModeProp = entryProp.FindPropertyRelative("_fallbackMode");
            SerializedProperty valueProp = entryProp.FindPropertyRelative("_value");
            SerializedProperty indexedValuesProp = entryProp.FindPropertyRelative("_indexedValues");
            SerializedProperty fallbackValueProp = entryProp.FindPropertyRelative("_fallbackValue");
            SerializedProperty capturedDefaultProp = entryProp.FindPropertyRelative("_capturedDefault");
            SerializedProperty hasCapturedDefaultProp = entryProp.FindPropertyRelative("_hasCapturedDefault");
            SerializedProperty capturedPropertyProp = entryProp.FindPropertyRelative("_capturedProperty");

            controllerIdProp.stringValue = controllerId;
            controllerIndexProp.intValue = 0;
            controllerIndexMaskProp.intValue = 1;
            propertyProp.enumValueIndex = (int)property;
            fallbackModeProp.enumValueIndex = (int)UXBindingFallbackMode.RestoreCapturedDefault;
            ResetValue(valueProp);
            indexedValuesProp.ClearArray();
            ResetValue(fallbackValueProp);
            ResetValue(capturedDefaultProp);
            hasCapturedDefaultProp.boolValue = false;
            capturedPropertyProp.enumValueIndex = (int)property;
            ApplyDefaultFallbackForProperty(entryProp, property);

            _foldouts[index] = true;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(binding);
        }

        private void ShowAddRuleMenu(UXBinding binding, Rect activatorRect)
        {
            if (binding.Controller == null || binding.Controller.Controllers.Count == 0)
            {
                EditorUtility.DisplayDialog("Add UX Binding Rule", "Assign a UXController before adding rules.", "OK");
                return;
            }

            UXBindingPropertyUtility.GetSupportedProperties(binding.gameObject, _supportedProperties);
            _addRuleOptions.Clear();

            for (int controllerIndex = 0; controllerIndex < binding.Controller.Controllers.Count; controllerIndex++)
            {
                UXController.ControllerDefinition controller = binding.Controller.Controllers[controllerIndex];
                if (controller == null)
                {
                    continue;
                }

                for (int propertyIndex = 0; propertyIndex < _supportedProperties.Count; propertyIndex++)
                {
                    UXBindingProperty property = _supportedProperties[propertyIndex];
                    if (HasRule(controller.Id, property))
                    {
                        continue;
                    }

                    UXBindingPropertyMetadata metadata = UXBindingPropertyUtility.GetMetadata(property);
                    _addRuleOptions.Add(new AddRuleOption(controller.Id, controller.Name, property, metadata.DisplayName));
                }
            }

            if (_addRuleOptions.Count == 0)
            {
                EditorUtility.DisplayDialog("Add UX Binding Rule", "All supported states already exist.", "OK");
                return;
            }

            PopupWindow.Show(activatorRect, new AddRulePopup(this, binding, _addRuleOptions, binding.Controller.Controllers.Count > 1));
        }

        private static void ResetValue(SerializedProperty valueProp)
        {
            valueProp.FindPropertyRelative("_boolValue").boolValue = false;
            valueProp.FindPropertyRelative("_floatValue").floatValue = 0f;
            valueProp.FindPropertyRelative("_stringValue").stringValue = string.Empty;
            valueProp.FindPropertyRelative("_colorValue").colorValue = Color.white;
            valueProp.FindPropertyRelative("_vector2Value").vector2Value = Vector2.zero;
            valueProp.FindPropertyRelative("_vector3Value").vector3Value = Vector3.zero;
            valueProp.FindPropertyRelative("_objectValue").objectReferenceValue = null;
        }

        private void DeleteEntry(UXBinding binding, int index)
        {
            if (index < 0 || index >= _entriesProp.arraySize)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Delete UX Binding Rule",
                    $"Delete binding rule {index}? This cannot be undone outside Unity Undo.",
                    "Delete",
                    "Cancel"))
            {
                return;
            }

            Undo.RecordObject(binding, "Delete UX Binding Rule");
            _entriesProp.DeleteArrayElementAtIndex(index);
            CleanupFoldouts(index);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(binding);
            GUIUtility.ExitGUI();
        }

        private void MoveEntry(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || toIndex < 0 || fromIndex >= _entriesProp.arraySize || toIndex >= _entriesProp.arraySize)
            {
                return;
            }

            _entriesProp.MoveArrayElement(fromIndex, toIndex);
            bool fromExpanded = _foldouts.ContainsKey(fromIndex) && _foldouts[fromIndex];
            bool toExpanded = _foldouts.ContainsKey(toIndex) && _foldouts[toIndex];
            _foldouts[fromIndex] = toExpanded;
            _foldouts[toIndex] = fromExpanded;
            serializedObject.ApplyModifiedProperties();
            GUIUtility.ExitGUI();
        }

        private void CleanupFoldouts(int removedIndex)
        {
            _foldouts.Remove(removedIndex);

            var remapped = new Dictionary<int, bool>();
            foreach (var pair in _foldouts)
            {
                int nextIndex = pair.Key > removedIndex ? pair.Key - 1 : pair.Key;
                remapped[nextIndex] = pair.Value;
            }

            _foldouts.Clear();
            foreach (var pair in remapped)
            {
                _foldouts[pair.Key] = pair.Value;
            }
        }

        private static void ApplyDefaultFallbackForProperty(SerializedProperty entryProp, UXBindingProperty property)
        {
            SerializedProperty fallbackModeProp = entryProp.FindPropertyRelative("_fallbackMode");
            SerializedProperty fallbackValueProp = entryProp.FindPropertyRelative("_fallbackValue");

            if (property == UXBindingProperty.GameObjectActive)
            {
                fallbackModeProp.enumValueIndex = (int)UXBindingFallbackMode.UseCustomValue;
                fallbackValueProp.FindPropertyRelative("_boolValue").boolValue = false;
                return;
            }

            if ((UXBindingFallbackMode)fallbackModeProp.enumValueIndex == UXBindingFallbackMode.UseCustomValue)
            {
                fallbackModeProp.enumValueIndex = (int)UXBindingFallbackMode.RestoreCapturedDefault;
                ResetValue(fallbackValueProp);
            }
        }

        private void AutoBindController(UXBinding binding)
        {
            UXController controller = binding.GetComponentInParent<UXController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Auto Bind UX Controller", "No UXController found in parents.", "OK");
                return;
            }

            Undo.RecordObject(binding, "Auto Bind UX Controller");
            binding.SetController(controller);
            _controllerProp.objectReferenceValue = controller;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(binding);
        }

        private void EnsureStyles()
        {
            if (_pillOn != null)
            {
                return;
            }

            _panelStyle = AlicizaEditorGUI.Styles.Panel;
            _entryBodyStyle = AlicizaEditorGUI.Styles.EntryBody;
            _fieldRowStyle = AlicizaEditorGUI.Styles.FieldRow;
            _propertyLabelStyle = AlicizaEditorGUI.Styles.FieldLabel;
            _entryTitleStyle = AlicizaEditorGUI.Styles.RowLabel;
            _entryMetaStyle = AlicizaEditorGUI.Styles.MutedMiniLabel;
            _entryKindStyle = AlicizaEditorGUI.Styles.KindBadge;
            _entryHandleStyle = AlicizaEditorGUI.Styles.Glyph;
            _emptyStateStyle = AlicizaEditorGUI.Styles.EmptyState;
            _statusLabelStyle = AlicizaEditorGUI.Styles.WarningLabel;
            _pillOn = AlicizaEditorGUI.Styles.PillOn;
            _pillOff = AlicizaEditorGUI.Styles.PillOff;
        }

        private static string GetKindBadge(UXBindingValueKind valueKind)
        {
            switch (valueKind)
            {
                case UXBindingValueKind.Boolean:
                    return "ON";
                case UXBindingValueKind.Float:
                    return "123";
                case UXBindingValueKind.String:
                    return "ABC";
                case UXBindingValueKind.Color:
                    return "RGB";
                case UXBindingValueKind.Vector2:
                    return "V2";
                case UXBindingValueKind.Vector3:
                    return "V3";
                case UXBindingValueKind.ObjectReference:
                    return "OBJ";
                default:
                    return "--";
            }
        }

        private bool DrawIndexMask(SerializedProperty maskProp, SerializedProperty indexProp, int length, bool multiSelect)
        {
            EditorGUILayout.LabelField(multiSelect ? "Active" : "Index", _propertyLabelStyle, GUILayout.Width(42f));
            int mask = maskProp.intValue;
            int originalMask = mask;
            for (int i = 0; i < length; i++)
            {
                int bit = UXBinding.BindingEntry.IndexToMask(i);
                bool selected = (mask & bit) != 0;
                bool nextSelected = GUILayout.Toggle(selected, i.ToString(), selected ? _pillOn : _pillOff, GUILayout.Width(26f));
                if (nextSelected != selected)
                {
                    if (multiSelect)
                    {
                        if (nextSelected)
                        {
                            mask |= bit;
                        }
                        else
                        {
                            mask &= ~bit;
                        }
                    }
                    else
                    {
                        mask = bit;
                    }
                }
            }

            if (mask == 0)
            {
                mask = UXBinding.BindingEntry.IndexToMask(Mathf.Clamp(indexProp.intValue, 0, length - 1));
            }

            maskProp.intValue = ClampMask(mask, length, indexProp.intValue);
            indexProp.intValue = GetFirstSelectedIndex(maskProp.intValue);
            return maskProp.intValue != originalMask;
        }

        private static SerializedProperty GetIndexedValueProperty(SerializedProperty indexedValuesProp, SerializedProperty fallbackValueProp, int selectedIndex)
        {
            for (int i = 0; i < indexedValuesProp.arraySize; i++)
            {
                SerializedProperty indexedValueProp = indexedValuesProp.GetArrayElementAtIndex(i);
                if (indexedValueProp.FindPropertyRelative("_index").intValue == selectedIndex)
                {
                    return indexedValueProp.FindPropertyRelative("_value");
                }
            }

            int nextIndex = indexedValuesProp.arraySize;
            indexedValuesProp.InsertArrayElementAtIndex(nextIndex);
            SerializedProperty nextValueProp = indexedValuesProp.GetArrayElementAtIndex(nextIndex);
            nextValueProp.FindPropertyRelative("_index").intValue = selectedIndex;
            CopyValue(fallbackValueProp, nextValueProp.FindPropertyRelative("_value"));
            return nextValueProp.FindPropertyRelative("_value");
        }

        private static void CopyValue(SerializedProperty source, SerializedProperty destination)
        {
            destination.FindPropertyRelative("_boolValue").boolValue = source.FindPropertyRelative("_boolValue").boolValue;
            destination.FindPropertyRelative("_floatValue").floatValue = source.FindPropertyRelative("_floatValue").floatValue;
            destination.FindPropertyRelative("_stringValue").stringValue = source.FindPropertyRelative("_stringValue").stringValue;
            destination.FindPropertyRelative("_colorValue").colorValue = source.FindPropertyRelative("_colorValue").colorValue;
            destination.FindPropertyRelative("_vector2Value").vector2Value = source.FindPropertyRelative("_vector2Value").vector2Value;
            destination.FindPropertyRelative("_vector3Value").vector3Value = source.FindPropertyRelative("_vector3Value").vector3Value;
            destination.FindPropertyRelative("_objectValue").objectReferenceValue = source.FindPropertyRelative("_objectValue").objectReferenceValue;
        }

        private static void ForceGameObjectActiveValues(SerializedProperty valueProp, SerializedProperty fallbackModeProp, SerializedProperty fallbackValueProp)
        {
            valueProp.FindPropertyRelative("_boolValue").boolValue = true;
            fallbackModeProp.enumValueIndex = (int)UXBindingFallbackMode.UseCustomValue;
            fallbackValueProp.FindPropertyRelative("_boolValue").boolValue = false;
        }

        private bool HasRule(string controllerId, UXBindingProperty property)
        {
            for (int i = 0; i < _entriesProp.arraySize; i++)
            {
                SerializedProperty entry = _entriesProp.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("_controllerId").stringValue == controllerId &&
                    entry.FindPropertyRelative("_property").enumValueIndex == (int)property)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetAllFoldouts(bool expanded)
        {
            for (int i = 0; i < _entriesProp.arraySize; i++)
            {
                _foldouts[i] = expanded;
            }
        }

        private static bool TryGetControllerName(UXController controller, string controllerId, out string controllerName)
        {
            controllerName = string.Empty;
            if (controller == null || string.IsNullOrEmpty(controllerId))
            {
                return false;
            }

            for (int i = 0; i < controller.Controllers.Count; i++)
            {
                UXController.ControllerDefinition definition = controller.Controllers[i];
                if (definition != null && definition.Id == controllerId)
                {
                    controllerName = definition.Name;
                    return true;
                }
            }

            return false;
        }

        private static string BuildIndexLabel(int mask)
        {
            if (mask == 0)
            {
                return "0";
            }

            string label = string.Empty;
            for (int i = 0; i < 31; i++)
            {
                if ((mask & UXBinding.BindingEntry.IndexToMask(i)) == 0)
                {
                    continue;
                }

                label = string.IsNullOrEmpty(label) ? i.ToString() : $"{label},{i}";
            }

            return label;
        }

        private static int ClampMask(int mask, int length, int fallbackIndex)
        {
            int validMask = 0;
            int max = Mathf.Min(length, 31);
            for (int i = 0; i < max; i++)
            {
                validMask |= UXBinding.BindingEntry.IndexToMask(i);
            }

            mask &= validMask;
            if (mask == 0)
            {
                mask = UXBinding.BindingEntry.IndexToMask(Mathf.Clamp(fallbackIndex, 0, max - 1));
            }

            return mask;
        }

        private static int GetFirstSelectedIndex(int mask)
        {
            for (int i = 0; i < 31; i++)
            {
                if ((mask & UXBinding.BindingEntry.IndexToMask(i)) != 0)
                {
                    return i;
                }
            }

            return 0;
        }

        private static void EnsureStringArray(ref string[] array, int length)
        {
            if (array.Length != length)
            {
                array = new string[length];
            }
        }
    }
}
#endif
