using System;
using System.Collections.Generic;
using System.Linq;
using AlicizaX.Editor;
using AlicizaX.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AlicizaX.UI.Editor
{
    [CustomEditor(typeof(RecyclerView))]
    public class RecyclerViewEditor : UnityEditor.Editor
    {
        #region Constants

        private const string NoneOptionName = "None";
        private const string VerticalScrollbarPath = "Packages/com.alicizax.unity.ui.extension/Editor/RecyclerView/Res/vertical.prefab";
        private const string HorizontalScrollbarPath = "Packages/com.alicizax.unity.ui.extension/Editor/RecyclerView/Res/horizontal.prefab";
        private const float SectionToolbarHeight = 30f;
        private const float PropertyRowHeight = 24f;
        private const float PropertyLabelWidth = 118f;
        private const float TemplateActionButtonSize = 20f;
        private const float DropAreaHeight = 54f;

        #endregion

        #region Serialized Properties - Layout Manager

        private SerializedProperty _layoutManagerTypeName;
        private SerializedProperty _layoutManager;
        private List<string> _layoutTypeNames = new List<string>();
        private string[] _layoutTypeNameOptions = Array.Empty<string>();
        private int _selectedLayoutIndex;

        #endregion

        #region Serialized Properties - Scroller

        private SerializedProperty _scroll;
        private SerializedProperty _scroller;
        private SerializedProperty _scrollerTypeName;
        private List<Type> _scrollerTypes = new List<Type>();
        private List<string> _scrollerTypeNames = new List<string>();
        private string[] _scrollerTypeNameOptions = Array.Empty<string>();
        private int _selectedScrollerIndex;

        #endregion

        #region Serialized Properties - Base Settings

        private SerializedProperty _direction;
        private SerializedProperty _alignment;
        private SerializedProperty _content;
        private SerializedProperty _spacing;
        private SerializedProperty _padding;
        private SerializedProperty _snap;
        private SerializedProperty _movementType;
        private SerializedProperty _inertia;
        private SerializedProperty _decelerationRate;
        private SerializedProperty _scrollSpeed;
        private SerializedProperty _wheelSpeed;

        #endregion

        #region Serialized Properties - Templates & Scrollbar

        private SerializedProperty _templates;
        private SerializedProperty _scrollbarVisibility;
        private SerializedProperty _scrollbar;

        #endregion

        #region Styles

        private GUIStyle _panelStyle;
        private GUIStyle _fieldRowStyle;
        private GUIStyle _sectionTitleStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _fieldLabelStyle;
        private GUIStyle _rowLabelStyle;
        private GUIStyle _mutedLabelStyle;
        private GUIStyle _warningLabelStyle;
        private GUIStyle _emptyStateStyle;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            InitializeLayoutManagerProperties();
            InitializeScrollerProperties();
            InitializeBaseProperties();
            InitializeTemplateProperties();

            serializedObject.Update();
            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #region Initialization

        private void InitializeLayoutManagerProperties()
        {
            _layoutManagerTypeName = serializedObject.FindProperty("_layoutManagerTypeName");
            _layoutManager = serializedObject.FindProperty("layoutManager");
            RefreshLayoutTypes();
        }

        private void InitializeScrollerProperties()
        {
            _scroll = serializedObject.FindProperty("scroll");
            _scroller = serializedObject.FindProperty("scroller");
            _scrollerTypeName = serializedObject.FindProperty("_scrollerTypeName");
            RefreshScrollerTypes();
            SyncExistingScroller();
        }

        private void InitializeBaseProperties()
        {
            _direction = serializedObject.FindProperty("direction");
            _alignment = serializedObject.FindProperty("alignment");
            _content = serializedObject.FindProperty("content");
            _spacing = serializedObject.FindProperty("spacing");
            _padding = serializedObject.FindProperty("padding");
            _snap = serializedObject.FindProperty("snap");
            _movementType = serializedObject.FindProperty("movementType");
            _inertia = serializedObject.FindProperty("inertia");
            _decelerationRate = serializedObject.FindProperty("decelerationRate");
            _scrollSpeed = serializedObject.FindProperty("scrollSpeed");
            _wheelSpeed = serializedObject.FindProperty("wheelSpeed");
        }

        private void InitializeTemplateProperties()
        {
            _templates = serializedObject.FindProperty("templates");
            _scrollbarVisibility = serializedObject.FindProperty("scrollbarVisibility");
            _scrollbar = serializedObject.FindProperty("scrollbar");
        }

        #endregion

        #region Inspector GUI

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EnsureStyles();
            bool isPlaying = Application.isPlaying;

            EditorGUILayout.Space(6f);
            DrawMissingReferenceRepairSection(isPlaying);
            DrawLayoutManagerSection(isPlaying);
            DrawBaseSettingsSection(isPlaying);
            DrawScrollerSection(isPlaying);
            DrawTemplatesSection();

            serializedObject.ApplyModifiedProperties();
        }

        #endregion


        private void DrawMissingReferenceRepairSection(bool isPlaying)
        {
            if (isPlaying)
            {
                return;
            }

            bool missingLayout = _layoutManager != null &&
                                 _layoutManager.managedReferenceValue == null &&
                                 _layoutManagerTypeName != null &&
                                 !string.IsNullOrEmpty(_layoutManagerTypeName.stringValue);
            bool missingScroller = _scroller != null &&
                                   _scroller.objectReferenceValue == null &&
                                   _scrollerTypeName != null &&
                                   !string.IsNullOrEmpty(_scrollerTypeName.stringValue);
            RecyclerView recyclerView = target as RecyclerView;
            bool missingScrollbarEx = recyclerView != null &&
                                      recyclerView.Scrollbar != null &&
                                      recyclerView.Scrollbar.GetComponent<ScrollbarEx>() == null;
            if (!missingLayout && !missingScroller && !missingScrollbarEx)
            {
                return;
            }

            EditorGUILayout.BeginVertical(_panelStyle);
            DrawSectionToolbar("Missing References");
            if (missingLayout && DrawActionRow("Layout Manager", "Restore Layout Manager", 168f))
            {
                RestoreLayoutManagerFromTypeNameIfMissing();
                serializedObject.ApplyModifiedProperties();
            }

            if (missingScroller && DrawActionRow("Scroller", "Restore Scroller Component", 178f))
            {
                RestoreScrollerFromTypeNameIfMissing();
                serializedObject.ApplyModifiedProperties();
            }

            if (missingScrollbarEx && DrawActionRow("Scrollbar", "Add ScrollbarEx", 128f))
            {
                Undo.AddComponent<ScrollbarEx>(recyclerView.Scrollbar.gameObject);
                EditorUtility.SetDirty(recyclerView.Scrollbar);
            }

            EditorGUILayout.EndVertical();
        }

        #region Layout Manager Section

        private void DrawLayoutManagerSection(bool isPlaying)
        {
            EditorGUILayout.BeginVertical(_panelStyle);
            {
                DrawSectionToolbar("Layout Manager");

                using (new EditorGUI.DisabledScope(isPlaying))
                {
                    int newIndex = DrawStyledPopupRow("Layout Type", _selectedLayoutIndex, _layoutTypeNameOptions);
                    if (newIndex != _selectedLayoutIndex)
                    {
                        _selectedLayoutIndex = newIndex;
                        UpdateLayoutManager(newIndex);
                        serializedObject.ApplyModifiedProperties();
                    }
                }

                if (_layoutManager.managedReferenceValue != null)
                {
                    EditorGUILayout.Space(3);
                    DrawManagedReferenceProperties(_layoutManager);
                }
                else
                {
                    DrawMessageRow("Please select a Layout Manager", true);
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void RefreshLayoutTypes()
        {
            _layoutTypeNames.Clear();
            _layoutTypeNames.Add(NoneOptionName);

            var types = AlicizaX.Utility.Assembly.GetRuntimeTypes(typeof(ILayoutManager));
            foreach (var type in types)
            {
                if (!typeof(MonoBehaviour).IsAssignableFrom(type))
                {
                    _layoutTypeNames.Add(type.FullName);
                }
            }

            _layoutTypeNameOptions = _layoutTypeNames.ToArray();
            _selectedLayoutIndex = Mathf.Clamp(
                _layoutTypeNames.IndexOf(_layoutManagerTypeName.stringValue),
                0,
                _layoutTypeNames.Count - 1
            );
        }

        private void UpdateLayoutManager(int selectedIndex)
        {
            try
            {
                if (selectedIndex == 0)
                {
                    ClearLayoutManager();
                    return;
                }

                if (!IsValidLayoutIndex(selectedIndex))
                {
                    Debug.LogError($"Invalid layout index: {selectedIndex}");
                    ClearLayoutManager();
                    return;
                }

                string typeName = _layoutTypeNames[selectedIndex];
                Type type = AlicizaX.Utility.Assembly.GetType(typeName);

                if (type != null && typeof(ILayoutManager).IsAssignableFrom(type))
                {
                    _layoutManager.managedReferenceValue = Activator.CreateInstance(type);
                    _layoutManagerTypeName.stringValue = typeName;
                    _selectedLayoutIndex = selectedIndex;
                }
                else
                {
                    Debug.LogError($"Invalid layout type: {typeName}");
                    ClearLayoutManager();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Layout Manager Error: {e.Message}");
                ClearLayoutManager();
            }
        }

        private void ClearLayoutManager()
        {
            _layoutManager.managedReferenceValue = null;
            _layoutManagerTypeName.stringValue = "";
            _selectedLayoutIndex = 0;
        }

        private bool IsValidLayoutIndex(int index)
        {
            return index >= 0 && index < _layoutTypeNames.Count;
        }

        #endregion

        #region Base Settings Section

        private void DrawBaseSettingsSection(bool isPlaying)
        {
            EditorGUILayout.BeginVertical(_panelStyle);
            {
                DrawSectionToolbar("Base Settings");

                using (new EditorGUI.DisabledScope(isPlaying))
                {
                    DrawPropertyRow(_direction);
                    DrawPropertyRow(_alignment);
                    DrawPropertyRow(_content);
                }

                DrawSubHeader("Spacing");
                DrawPropertyRow(_spacing);
                DrawPropertyRow(_padding);

                DrawSubHeader("Scrolling");

                using (new EditorGUI.DisabledScope(isPlaying))
                {
                    DrawPropertyRow(_scroll, "Scroll Mode");
                }

                DrawScrollBarSettings(isPlaying);
                DrawPropertyRow(_snap);
                DrawPropertyRow(_movementType, "Movement Type");
                DrawPropertyRow(_inertia);

                using (new EditorGUI.DisabledScope(!_inertia.boolValue))
                {
                    DrawPropertyRow(_decelerationRate, "Deceleration Rate");
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawScrollBarSettings(bool isPlaying)
        {
            using (new EditorGUI.DisabledScope(isPlaying))
            {
                ScrollbarVisibility previousVisibility = (ScrollbarVisibility)_scrollbarVisibility.enumValueIndex;
                DrawPropertyRow(_scrollbarVisibility, "Scrollbar Visibility");
                ScrollbarVisibility currentVisibility = (ScrollbarVisibility)_scrollbarVisibility.enumValueIndex;

                if (currentVisibility != previousVisibility)
                {
                    HandleScrollBarVisibilityChange(previousVisibility, currentVisibility);
                }
            }
        }

        #endregion

        #region Scroller Section

        private void DrawScrollerSection(bool isPlaying)
        {
            EditorGUILayout.BeginVertical(_panelStyle);
            {
                DrawSectionToolbar("Scroller Settings");

                using (new EditorGUI.DisabledScope(isPlaying))
                {
                    int newIndex = DrawStyledPopupRow("Scroller Type", _selectedScrollerIndex, _scrollerTypeNameOptions);
                    if (newIndex != _selectedScrollerIndex)
                    {
                        UpdateScroller(newIndex);
                        serializedObject.ApplyModifiedProperties();
                    }
                }

                DrawScrollerComponentProperties();
                DrawScrollerSpeedSettings();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawScrollerComponentProperties()
        {
            var recyclerView = target as RecyclerView;
            if (recyclerView == null) return;

            var scrollerComponent = recyclerView.GetComponent<Scroller>();
            if (scrollerComponent != null)
            {
                DrawComponentProperties(scrollerComponent, "Scroller Properties");
            }
            else
            {
                DrawMessageRow("Please select a Scroller type", true);
            }
        }

        private void DrawScrollerSpeedSettings()
        {
            EditorGUILayout.Space(3);
            DrawPropertyRow(_scrollSpeed);
            DrawPropertyRow(_wheelSpeed);
        }

        private void RefreshScrollerTypes()
        {
            _scrollerTypes = TypeCache.GetTypesDerivedFrom<IScroller>()
                .Where(t => typeof(Scroller).IsAssignableFrom(t) && !t.IsAbstract)
                .ToList();

            _scrollerTypeNames = _scrollerTypes
                .Select(t => t.FullName)
                .Prepend(NoneOptionName)
                .ToList();
            _scrollerTypeNameOptions = _scrollerTypeNames.ToArray();
        }

        private void SyncExistingScroller()
        {
            var recyclerView = target as RecyclerView;
            if (recyclerView == null) return;

            var existingScroller = recyclerView.GetComponent<Scroller>();
            if (existingScroller != null)
            {
                _scroller.objectReferenceValue = existingScroller;
                _scrollerTypeName.stringValue = existingScroller.GetType().FullName;
                _selectedScrollerIndex = _scrollerTypeNames.IndexOf(_scrollerTypeName.stringValue);
            }
            else
            {
                // 如果组件不存在，但属性里存了类型名，这里不清理 typeName（恢复逻辑会处理）
                _selectedScrollerIndex = Mathf.Clamp(_scrollerTypeNames.IndexOf(_scrollerTypeName.stringValue), 0, _scrollerTypeNames.Count - 1);
            }
        }

        private void UpdateScroller(int selectedIndex)
        {
            try
            {
                var recyclerView = target as RecyclerView;
                if (recyclerView == null) return;

                Undo.RecordObjects(new Object[] { recyclerView, this }, "Update Scroller");

                RemoveExistingScroller(recyclerView);

                if (selectedIndex == 0)
                {
                    ClearScrollerReferences();
                    return;
                }

                AddNewScroller(recyclerView, selectedIndex);
            }
            catch (Exception e)
            {
                Debug.LogError($"Scroller Error: {e}");
                ClearScrollerReferences();
            }
        }

        private void RemoveExistingScroller(RecyclerView recyclerView)
        {
            var oldScroller = recyclerView.GetComponent<Scroller>();
            if (oldScroller != null)
            {
                Undo.DestroyObjectImmediate(oldScroller);
            }
        }

        private void AddNewScroller(RecyclerView recyclerView, int selectedIndex)
        {
            Type selectedType = _scrollerTypes[selectedIndex - 1];
            var newScroller = Undo.AddComponent(recyclerView.gameObject, selectedType) as Scroller;

            _scroller.objectReferenceValue = newScroller;
            _scrollerTypeName.stringValue = selectedType.FullName;
            _selectedScrollerIndex = selectedIndex;

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(recyclerView);
        }

        private void ClearScrollerReferences()
        {
            _scroller.objectReferenceValue = null;
            _scrollerTypeName.stringValue = "";
            _selectedScrollerIndex = 0;
        }

        #endregion

        #region Scrollbar Handling

        private void HandleScrollBarVisibilityChange(ScrollbarVisibility previousVisibility, ScrollbarVisibility currentVisibility)
        {
            if (currentVisibility == ScrollbarVisibility.AlwaysHide)
            {
                ClearScrollBar();
            }

            if (previousVisibility == ScrollbarVisibility.AlwaysHide)
            {
                CreateScrollBar();
            }
        }

        private void CreateScrollBar()
        {
            var recyclerView = target as RecyclerView;
            if (recyclerView == null) return;
            if (_scrollbar.objectReferenceValue != null) return;

            Direction direction = (Direction)_direction.enumValueIndex;
            string prefabPath = GetScrollbarPrefabPath(direction);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                InstantiateScrollBar(prefabPath, recyclerView.transform);
            }
        }

        private string GetScrollbarPrefabPath(Direction direction)
        {

            return direction switch
            {
                Direction.Vertical => VerticalScrollbarPath,
                Direction.Horizontal => HorizontalScrollbarPath,
                _ => null
            };
        }

        private void InstantiateScrollBar(string path, Transform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"Scrollbar prefab not found at path: {path}");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.UserAction);

            _scrollbar.objectReferenceValue = instance.GetComponent<Scrollbar>();
            serializedObject.ApplyModifiedProperties();
        }

        private void ClearScrollBar()
        {
            _scrollbarVisibility.enumValueIndex = (int)ScrollbarVisibility.AlwaysHide;

            if (_scrollbar.objectReferenceValue != null)
            {
                Scrollbar scrollbarComponent = _scrollbar.objectReferenceValue as Scrollbar;
                _scrollbar.objectReferenceValue = null;

                if (scrollbarComponent != null)
                {
                    Object.DestroyImmediate(scrollbarComponent.gameObject);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        #endregion

        #region Templates Section

        private void DrawTemplatesSection()
        {
            EditorGUILayout.BeginVertical(_panelStyle);
            DrawSectionToolbar("Item Templates");

            DrawTemplatesList();
            DrawDragAndDropArea();
            EditorGUILayout.EndVertical();
        }

        private void DrawTemplatesList()
        {
            if (_templates == null || !_templates.isArray) return;

            for (int i = 0; i < _templates.arraySize; i++)
            {
                DrawTemplateItem(i);
            }
        }

        private void DrawTemplateItem(int index)
        {
            if (index < 0 || index >= _templates.arraySize) return;

            SerializedProperty item = _templates.GetArrayElementAtIndex(index);
            float height = Mathf.Max(PropertyRowHeight, EditorGUI.GetPropertyHeight(item, GUIContent.none, true) + 4f);
            Rect rowRect = GUILayoutUtility.GetRect(1f, height, GUILayout.ExpandWidth(true));
            bool hovered = rowRect.Contains(Event.current.mousePosition);
            AlicizaEditorGUI.DrawListItemBackground(rowRect, false, hovered);

            Rect fieldRect = new Rect(rowRect.x + 6f, rowRect.y + 2f, rowRect.width - TemplateActionButtonSize - 14f, height - 4f);
            Rect deleteRect = new Rect(rowRect.xMax - TemplateActionButtonSize - 2f, rowRect.y + 2f, TemplateActionButtonSize, TemplateActionButtonSize);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.PropertyField(fieldRect, item, GUIContent.none, true);
            }

            if (AlicizaEditorGUI.DrawSymbolButton(deleteRect, "-"))
            {
                RemoveTemplateItem(index);
            }
        }

        private void RemoveTemplateItem(int index)
        {
            if (_templates == null || index < 0 || index >= _templates.arraySize) return;

            _templates.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
            GUIUtility.ExitGUI();
        }

        private void DrawDragAndDropArea()
        {
            Rect dropArea = GUILayoutUtility.GetRect(0f, DropAreaHeight, GUILayout.ExpandWidth(true));
            bool hovered = dropArea.Contains(Event.current.mousePosition);
            EditorGUI.DrawRect(dropArea, hovered ? AlicizaEditorGUI.Colors.RowHover : AlicizaEditorGUI.Colors.Body);
            AlicizaEditorGUI.DrawOutline(dropArea);
            GUI.Label(dropArea, "Drag ViewHolder Templates Here", _emptyStateStyle);

            HandleDragAndDrop(dropArea);
        }

        private void HandleDragAndDrop(Rect dropArea)
        {
            Event currentEvent = Event.current;

            switch (currentEvent.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(currentEvent.mousePosition))
                        return;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (currentEvent.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        ProcessDraggedTemplates();
                        currentEvent.Use();
                    }
                    break;
            }
        }

        private void ProcessDraggedTemplates()
        {
            foreach (Object draggedObject in DragAndDrop.objectReferences)
            {
                if (draggedObject is GameObject gameObject)
                {
                    ProcessDraggedGameObject(gameObject);
                }
            }
        }

        private void ProcessDraggedGameObject(GameObject gameObject)
        {
            ViewHolder viewHolder = gameObject.GetComponent<ViewHolder>();

            if (viewHolder != null)
            {
                AddTemplate(gameObject);
            }
            else
            {
                Debug.LogWarning($"GameObject '{gameObject.name}' must have a ViewHolder component!");
            }
        }

        private void AddTemplate(GameObject templatePrefab)
        {
            if (_templates == null || templatePrefab == null) return;

            if (IsTemplateDuplicate(templatePrefab))
            {
                Debug.LogWarning($"Template '{templatePrefab.name}' already exists in the list!");
                return;
            }

            _templates.arraySize++;
            SerializedProperty newItem = _templates.GetArrayElementAtIndex(_templates.arraySize - 1);
            newItem.objectReferenceValue = templatePrefab;

            serializedObject.ApplyModifiedProperties();
        }

        private bool IsTemplateDuplicate(GameObject templatePrefab)
        {
            Type templateType = templatePrefab.GetComponent<ViewHolder>().GetType();

            for (int i = 0; i < _templates.arraySize; i++)
            {
                SerializedProperty existingItem = _templates.GetArrayElementAtIndex(i);
                var existingViewHolder = existingItem.objectReferenceValue as GameObject;

                if (existingViewHolder != null)
                {
                    var existingType = existingViewHolder.GetComponent<ViewHolder>().GetType();
                    if (existingType == templateType)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TemplatesNeedComponent<TComponent>() where TComponent : Component
        {
            if (_templates == null || !_templates.isArray)
            {
                return false;
            }

            for (int i = 0; i < _templates.arraySize; i++)
            {
                SerializedProperty item = _templates.GetArrayElementAtIndex(i);
                GameObject template = GetTemplateGameObject(item.objectReferenceValue);
                if (template != null && template.GetComponent<TComponent>() == null)
                {
                    return true;
                }
            }

            return false;
        }

        private void AddComponentToTemplates<TComponent>() where TComponent : Component
        {
            if (_templates == null || !_templates.isArray)
            {
                return;
            }

            for (int i = 0; i < _templates.arraySize; i++)
            {
                SerializedProperty item = _templates.GetArrayElementAtIndex(i);
                GameObject template = GetTemplateGameObject(item.objectReferenceValue);
                if (template == null || template.GetComponent<TComponent>() != null)
                {
                    continue;
                }

                Undo.AddComponent<TComponent>(template);
                EditorUtility.SetDirty(template);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static GameObject GetTemplateGameObject(Object templateReference)
        {
            if (templateReference is GameObject gameObject)
            {
                return gameObject;
            }

            if (templateReference is Component component)
            {
                return component.gameObject;
            }

            return null;
        }

        #endregion

        #region Helper Methods

        private void DrawManagedReferenceProperties(SerializedProperty property)
        {
            SerializedProperty iterator = property.Copy();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.name == "m_Script") continue;

                DrawPropertyRow(iterator);
            }
        }

        private void DrawComponentProperties(MonoBehaviour component, string header = null)
        {
            if (component == null) return;

            EditorGUILayout.Space(3);

            SerializedObject componentSerializedObject = new SerializedObject(component);
            componentSerializedObject.Update();

            SerializedProperty property = componentSerializedObject.GetIterator();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.name == "m_Script") continue;

                DrawPropertyRow(property);
            }

            componentSerializedObject.ApplyModifiedProperties();
        }

        private void EnsureStyles()
        {
            if (_panelStyle != null)
            {
                return;
            }

            _panelStyle = AlicizaEditorGUI.Styles.Panel;
            _fieldRowStyle = AlicizaEditorGUI.Styles.FieldRow;
            _sectionTitleStyle = AlicizaEditorGUI.Styles.RowLabel;
            _subHeaderStyle = AlicizaEditorGUI.Styles.MutedMiniLabel;
            _fieldLabelStyle = AlicizaEditorGUI.Styles.FieldLabel;
            _rowLabelStyle = AlicizaEditorGUI.Styles.RowLabel;
            _mutedLabelStyle = AlicizaEditorGUI.Styles.MutedMiniLabel;
            _warningLabelStyle = AlicizaEditorGUI.Styles.WarningLabel;
            _emptyStateStyle = AlicizaEditorGUI.Styles.EmptyState;
        }

        private void DrawSectionToolbar(string title)
        {
            Rect toolbarRect = GUILayoutUtility.GetRect(1f, SectionToolbarHeight, GUILayout.ExpandWidth(true));
            AlicizaEditorGUI.DrawToolbarBackground(toolbarRect);

            Rect titleRect = new Rect(toolbarRect.x + 8f, toolbarRect.y + 5f, toolbarRect.width - 16f, 20f);
            GUI.Label(titleRect, title, _sectionTitleStyle);
        }

        private void DrawSubHeader(string title)
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 18f, GUILayout.ExpandWidth(true));
            GUI.Label(new Rect(rect.x + 6f, rect.y + 1f, rect.width - 12f, 16f), title, _subHeaderStyle);
        }

        private bool DrawActionRow(string label, string actionLabel, float buttonWidth)
        {
            EditorGUILayout.BeginHorizontal(_fieldRowStyle);
            EditorGUILayout.LabelField(label, _fieldLabelStyle, GUILayout.Width(PropertyLabelWidth));
            GUILayout.FlexibleSpace();
            bool clicked = AlicizaEditorGUI.DrawInlineButton(actionLabel, buttonWidth);
            EditorGUILayout.EndHorizontal();
            return clicked;
        }

        private void DrawPropertyRow(SerializedProperty property, string label = null)
        {
            if (property == null)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal(_fieldRowStyle);
            EditorGUILayout.LabelField(string.IsNullOrEmpty(label) ? property.displayName : label, _fieldLabelStyle, GUILayout.Width(PropertyLabelWidth));
            EditorGUILayout.PropertyField(property, GUIContent.none, true);
            EditorGUILayout.EndHorizontal();
        }

        private int DrawStyledPopupRow(string label, int selectedIndex, string[] options)
        {
            EditorGUILayout.BeginHorizontal(_fieldRowStyle);
            EditorGUILayout.LabelField(label, _fieldLabelStyle, GUILayout.Width(PropertyLabelWidth));
            Rect popupRect = GUILayoutUtility.GetRect(90f, 20f, GUILayout.MinWidth(90f), GUILayout.ExpandWidth(true));
            int nextIndex = AlicizaEditorGUI.DrawStyledPopup(popupRect, selectedIndex, options);
            EditorGUILayout.EndHorizontal();
            return nextIndex;
        }

        private void DrawMessageRow(string message, bool warning)
        {
            Rect rect = GUILayoutUtility.GetRect(1f, 34f, GUILayout.ExpandWidth(true));
            AlicizaEditorGUI.DrawBodyBackground(rect);
            GUI.Label(rect, message, warning ? _warningLabelStyle : _mutedLabelStyle);
        }

        #endregion

        #region Restore Helpers (新增)

        private void RestoreLayoutManagerFromTypeNameIfMissing()
        {
            try
            {
                if (_layoutManager == null || _layoutManagerTypeName == null) return;

                // 如果 managedReferenceValue 已经存在就不必恢复
                if (_layoutManager.managedReferenceValue != null) return;

                string typeName = _layoutManagerTypeName.stringValue;
                if (string.IsNullOrEmpty(typeName)) return;

                Type type = AlicizaX.Utility.Assembly.GetType(typeName);
                if (type == null)
                {
                    Debug.LogWarning($"LayoutManager type '{typeName}' not found. Cannot restore layout manager.");
                    return;
                }

                if (!typeof(ILayoutManager).IsAssignableFrom(type))
                {
                    Debug.LogWarning($"Type '{typeName}' does not implement ILayoutManager. Cannot restore layout manager.");
                    _layoutManagerTypeName.stringValue = "";
                    return;
                }

                // 实例化并赋值
                var instance = Activator.CreateInstance(type);
                _layoutManager.managedReferenceValue = instance;
                // 尝试刷新下拉列表并更新选择索引
                RefreshLayoutTypes();
                _selectedLayoutIndex = Mathf.Clamp(_layoutTypeNames.IndexOf(typeName), 0, _layoutTypeNames.Count - 1);

                Debug.Log($"LayoutManager restored from type name '{typeName}'.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error restoring LayoutManager: {e}");
                _layoutManager.managedReferenceValue = null;
                _layoutManagerTypeName.stringValue = "";
            }
        }

        private void RestoreScrollerFromTypeNameIfMissing()
        {
            try
            {
                if (_scroller == null || _scrollerTypeName == null) return;

                // 如果 objectReferenceValue 已经存在就不必恢复
                if (_scroller.objectReferenceValue != null) return;

                string typeName = _scrollerTypeName.stringValue;
                if (string.IsNullOrEmpty(typeName)) return;

                Type type = AlicizaX.Utility.Assembly.GetType(typeName) ?? Type.GetType(typeName);
                if (type == null)
                {
                    Debug.LogWarning($"Scroller type '{typeName}' not found. Cannot restore scroller component.");
                    _scrollerTypeName.stringValue = "";
                    return;
                }

                if (!typeof(Scroller).IsAssignableFrom(type))
                {
                    Debug.LogWarning($"Type '{typeName}' is not a Scroller. Cannot restore scroller.");
                    _scrollerTypeName.stringValue = "";
                    return;
                }

                var recyclerView = target as RecyclerView;
                if (recyclerView == null) return;

                // 给目标 GameObject 添加组件（使用 Undo 以支持撤销）
                var newComp = Undo.AddComponent(recyclerView.gameObject, type) as Scroller;
                if (newComp == null)
                {
                    Debug.LogError($"Failed to add scroller component of type '{typeName}' to GameObject '{recyclerView.gameObject.name}'.");
                    _scrollerTypeName.stringValue = "";
                    return;
                }

                _scroller.objectReferenceValue = newComp;
                // 刷新 scroller 类型列表并更新索引（如果存在）
                RefreshScrollerTypes();
                _selectedScrollerIndex = Mathf.Clamp(_scrollerTypeNames.IndexOf(typeName), 0, _scrollerTypeNames.Count - 1);

                EditorUtility.SetDirty(recyclerView);
                Debug.Log($"Scroller component of type '{typeName}' restored and attached to GameObject '{recyclerView.gameObject.name}'.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error restoring Scroller: {e}");
                _scroller.objectReferenceValue = null;
                _scrollerTypeName.stringValue = "";
            }
        }

        #endregion
    }
}
