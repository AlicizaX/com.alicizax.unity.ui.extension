using System;
using System.Collections.Generic;
using System.Linq;
using AlicizaX.Editor.UIToolkit;
using AlicizaX.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;
using Object = UnityEngine.Object;
using Scrollbar = UnityEngine.UI.Scrollbar;

namespace AlicizaX.UI.Editor
{

    [CustomEditor(typeof(RecyclerView))]
    public sealed class RecyclerViewEditor : AlicizaUIEditor
    {
        #region Constants

        private const string NoneOptionName = "None";
        private const string VerticalScrollbarPath = "Packages/com.alicizax.unity.ui.extension/Editor/RecyclerView/Res/vertical.prefab";
        private const string HorizontalScrollbarPath = "Packages/com.alicizax.unity.ui.extension/Editor/RecyclerView/Res/horizontal.prefab";
        private const float TemplateCaptionWidth = 64f;
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
        private SerializedProperty _centerCrossAxis;
        private SerializedProperty _content;
        private SerializedProperty _spacing;
        private SerializedProperty _padding;
        private SerializedProperty _snap;
        private SerializedProperty _snapDuration;
        private SerializedProperty _refreshMode;
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

        #region UI State

        private VisualElement _root;
        private VisualElement _diagnosticsRoot;
        private VisualElement _repairRoot;
        private VisualElement _layoutDetails;
        private VisualElement _scrollerDetails;
        private VisualElement _templatesList;
        private VisualElement _dropArea;
        private VisualElement _runtimeContent;
        private AlicizaRow _snapDurationRow;
        private AlicizaRow _decelerationRow;
        private AlicizaFoldoutCard _runtimeCard;
        private AlicizaEnumField _layoutTypeField;
        private AlicizaEnumField _scrollerTypeField;
        private AlicizaEnumField _directionField;
        private VisualElement _centerCrossAxisControl;
        private VisualElement _contentControl;
        private AlicizaEnumField _scrollModeField;
        private AlicizaEnumField _scrollbarVisibilityField;
        private Label _layoutBadge;
        private Label _scrollerBadge;
        private Label _templateBadge;
        private Label _runtimeBadge;
        private SerializedObject _scrollerSerializedObject;
        private string _repairSignature;
        private bool _hasEditModeState;
        private bool _lastEditMode;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            InitializeLayoutManagerProperties();
            InitializeScrollerProperties();
            InitializeBaseProperties();
            InitializeTemplateProperties();
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
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
            _centerCrossAxis = serializedObject.FindProperty("centerCrossAxis");
            _content = serializedObject.FindProperty("content");
            _spacing = serializedObject.FindProperty("spacing");
            _padding = serializedObject.FindProperty("padding");
            _snap = serializedObject.FindProperty("snap");
            _snapDuration = serializedObject.FindProperty("snapDuration");
            _refreshMode = serializedObject.FindProperty("refreshMode");
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

        private void EnsureProperties()
        {
            if (_layoutManager == null)
            {
                InitializeLayoutManagerProperties();
            }

            if (_scroller == null)
            {
                InitializeScrollerProperties();
            }

            if (_direction == null)
            {
                InitializeBaseProperties();
            }

            if (_templates == null)
            {
                InitializeTemplateProperties();
            }
        }

        #endregion

        #region Inspector Building

        protected override void BuildInspector(VisualElement root)
        {
            _root = root;
            EnsureProperties();

            BuildStatusPanel(root);
            BuildLayoutManagerPanel(root);
            BuildBaseSettingsPanel(root);
            BuildScrollerPanel(root);
            BuildTemplatesPanel(root);

            _lastEditMode = !Application.isPlaying;
            _hasEditModeState = true;

            RefreshRuntime();
            AlicizaEditorUI.RefreshEvery(root, RefreshRuntime, 250);
        }

        private void BuildStatusPanel(VisualElement root)
        {
            AlicizaPanel panel = AlicizaEditorUI.CreatePanel(null);
            root.Add(panel);

            _diagnosticsRoot = new VisualElement { name = "recycler-view-setup" };
            panel.Body.Add(_diagnosticsRoot);

            _runtimeCard = AlicizaEditorUI.CreateFoldoutCard("Runtime", true);
            _runtimeCard.Meta.text = string.Empty;
            _runtimeBadge = AlicizaEditorUI.CreateBadge("EDIT", AlicizaValueTone.Muted);
            _runtimeCard.Actions.Add(_runtimeBadge);
            _runtimeCard.ExpandedChanged += OnRuntimeCardExpandedChanged;
            _runtimeContent = new VisualElement { name = "recycler-view-runtime-content" };
            _runtimeCard.Body.Add(_runtimeContent);
            panel.Body.Add(_runtimeCard);

            _repairRoot = new VisualElement { name = "recycler-view-missing-references" };
            panel.Body.Add(_repairRoot);
        }

        private void BuildLayoutManagerPanel(VisualElement root)
        {
            AlicizaPanel panel = AlicizaEditorUI.CreatePanel(null);
            root.Add(panel);

            AlicizaSection section = AlicizaEditorUI.CreateStaticSection("Layout Manager");
            panel.Body.Add(section);

            _layoutBadge = AlicizaEditorUI.CreateBadge("None", AlicizaValueTone.Muted);
            section.Header.Add(_layoutBadge);

            _layoutTypeField = new AlicizaEnumField(_layoutTypeNameOptions, _selectedLayoutIndex);
            _layoutTypeField.AddToClassList("alicia-control--grow");
            _layoutTypeField.SetEnabled(!Application.isPlaying);
            _layoutTypeField.RegisterValueChangedCallback(evt => OnLayoutTypeChanged(evt.newValue));
            AlicizaEditorUI.AddRow(section.Body, "Layout Type", _layoutTypeField);

            _layoutDetails = new VisualElement { name = "recycler-view-layout-details" };
            section.Body.Add(_layoutDetails);

            RebuildLayoutDetails();
        }

        private void BuildBaseSettingsPanel(VisualElement root)
        {
            AlicizaPanel panel = AlicizaEditorUI.CreatePanel(null);
            root.Add(panel);

            AlicizaSection section = AlicizaEditorUI.CreateStaticSection("Base Settings");
            panel.Body.Add(section);
            VisualElement body = section.Body;
            bool editing = !Application.isPlaying;

            AlicizaRow directionRow = AlicizaEditorUI.AddPropertyRow(body, "Direction", _direction);
            _directionField = directionRow != null ? directionRow.Q<AlicizaEnumField>() : null;
            if (_directionField != null)
            {
                _directionField.SetEnabled(editing);
                TrackSettingsChanges(_directionField);
                _directionField.RegisterValueChangedCallback(_ => OnDirectionChanged());
            }

            AlicizaRow centerRow = AlicizaEditorUI.AddPropertyRow(body, "Center Cross Axis", _centerCrossAxis);
            _centerCrossAxisControl = centerRow != null ? centerRow.Content : null;
            SetControlEnabled(_centerCrossAxisControl, editing);
            TrackSettingsChanges(_centerCrossAxisControl);

            AlicizaRow contentRow = AlicizaEditorUI.AddPropertyRow(
                body, "Content", _content, true, typeof(RectTransform), true);
            _contentControl = contentRow != null ? contentRow.Content : null;
            SetControlEnabled(_contentControl, editing);

            AddSubHeader(body, "Spacing");
            AddVector2Row(body, "Spacing", _spacing);
            AddVector2Row(body, "Padding", _padding);

            AddSubHeader(body, "Scrolling");

            AlicizaRow scrollRow = AlicizaEditorUI.AddPropertyRow(body, "Scroll Mode", _scroll);
            _scrollModeField = scrollRow != null ? scrollRow.Q<AlicizaEnumField>() : null;
            if (_scrollModeField != null)
            {
                _scrollModeField.SetEnabled(editing);
                TrackSettingsChanges(_scrollModeField);
            }

            AlicizaRow scrollbarRow = AlicizaEditorUI.AddPropertyRow(
                body, "Scrollbar Visibility", _scrollbarVisibility);
            _scrollbarVisibilityField = scrollbarRow != null
                ? scrollbarRow.Q<AlicizaEnumField>()
                : null;
            if (_scrollbarVisibilityField != null)
            {
                _scrollbarVisibilityField.SetEnabled(editing);
                TrackSettingsChanges(_scrollbarVisibilityField);
                _scrollbarVisibilityField.RegisterValueChangedCallback(
                    evt => OnScrollbarVisibilityChanged(evt.previousValue, evt.newValue));
            }

            AlicizaRow snapRow = AlicizaEditorUI.AddPropertyRow(body, "Snap", _snap);
            AlicizaPillToggle snapToggle = snapRow != null ? snapRow.Q<AlicizaPillToggle>() : null;
            TrackSettingsChanges(snapRow != null ? snapRow.Content : null);

            _snapDurationRow = AlicizaEditorUI.AddPropertyRow(body, "Snap Duration", _snapDuration);
            TrackSettingsChanges(_snapDurationRow != null ? _snapDurationRow.Content : null);
            UpdateSnapDurationVisibility(_snap != null && _snap.boolValue);
            if (snapToggle != null)
            {
                snapToggle.RegisterValueChangedCallback(evt =>
                {
                    UpdateSnapDurationVisibility(evt.newValue);
                    OnSettingsChanged();
                });
            }

            AlicizaRow refreshRow = AlicizaEditorUI.AddPropertyRow(body, "Refresh Mode", _refreshMode);
            TrackSettingsChanges(refreshRow != null ? refreshRow.Content : null);

            AlicizaRow movementRow = AlicizaEditorUI.AddPropertyRow(body, "Movement Type", _movementType);
            TrackSettingsChanges(movementRow != null ? movementRow.Content : null);

            AlicizaRow inertiaRow = AlicizaEditorUI.AddPropertyRow(body, "Inertia", _inertia);
            AlicizaPillToggle inertiaToggle = inertiaRow != null ? inertiaRow.Q<AlicizaPillToggle>() : null;
            TrackSettingsChanges(inertiaRow != null ? inertiaRow.Content : null);

            _decelerationRow = AlicizaEditorUI.AddPropertyRow(
                body, "Deceleration Rate", _decelerationRate);
            TrackSettingsChanges(_decelerationRow != null ? _decelerationRow.Content : null);
            SetDecelerationEnabled(_inertia != null && _inertia.boolValue);
            if (inertiaToggle != null)
            {
                inertiaToggle.RegisterValueChangedCallback(evt =>
                {
                    SetDecelerationEnabled(evt.newValue);
                    OnSettingsChanged();
                });
            }
        }

        private void BuildScrollerPanel(VisualElement root)
        {
            AlicizaPanel panel = AlicizaEditorUI.CreatePanel(null);
            root.Add(panel);

            AlicizaSection section = AlicizaEditorUI.CreateStaticSection("Scroller Settings");
            panel.Body.Add(section);

            _scrollerBadge = AlicizaEditorUI.CreateBadge("None", AlicizaValueTone.Muted);
            section.Header.Add(_scrollerBadge);

            _scrollerTypeField = new AlicizaEnumField(_scrollerTypeNameOptions, _selectedScrollerIndex);
            _scrollerTypeField.AddToClassList("alicia-control--grow");
            _scrollerTypeField.SetEnabled(!Application.isPlaying);
            _scrollerTypeField.RegisterValueChangedCallback(evt => OnScrollerTypeChanged(evt.newValue));
            AlicizaEditorUI.AddRow(section.Body, "Scroller Type", _scrollerTypeField);

            _scrollerDetails = new VisualElement { name = "recycler-view-scroller-details" };
            section.Body.Add(_scrollerDetails);
            RebuildScrollerDetails();

            AddSubHeader(section.Body, "Speed");
            AlicizaRow scrollSpeedRow = AlicizaEditorUI.AddPropertyRow(section.Body, "Scroll Speed", _scrollSpeed);
            TrackSettingsChanges(scrollSpeedRow != null ? scrollSpeedRow.Content : null);
            AlicizaRow wheelSpeedRow = AlicizaEditorUI.AddPropertyRow(section.Body, "Wheel Speed", _wheelSpeed);
            TrackSettingsChanges(wheelSpeedRow != null ? wheelSpeedRow.Content : null);
        }

        private void BuildTemplatesPanel(VisualElement root)
        {
            AlicizaPanel panel = AlicizaEditorUI.CreatePanel(null);
            root.Add(panel);

            AlicizaSection section = AlicizaEditorUI.CreateStaticSection("Item Templates");
            panel.Body.Add(section);

            _templateBadge = AlicizaEditorUI.CreateBadge("0", AlicizaValueTone.Muted);
            section.Header.Add(_templateBadge);

            _templatesList = new VisualElement { name = "recycler-view-template-list" };
            section.Body.Add(_templatesList);

            _dropArea = BuildDropArea();
            section.Body.Add(_dropArea);

            RebuildTemplatesList();
        }

        #endregion

        #region Runtime Refresh

        private void RefreshRuntime()
        {
            if (_root == null)
            {
                return;
            }

            RefreshDiagnostics();
            RefreshRuntimeSection();
            RefreshRepairState();
            RefreshEditModeAvailability();
        }

        private void RefreshDiagnostics()
        {
            if (_diagnosticsRoot == null)
            {
                return;
            }

            _diagnosticsRoot.Clear();

            AlicizaSection section = AlicizaEditorUI.CreateStaticSection("Setup");
            _diagnosticsRoot.Add(section);

            RecyclerView recyclerView = target as RecyclerView;
            if (recyclerView == null)
            {
                AddDiagnosticRow(section.Body, "RecyclerView target is unavailable.", AlicizaValueTone.Danger);
                section.Header.Add(AlicizaEditorUI.CreateBadge("!", AlicizaValueTone.Danger));
                return;
            }

            int warningCount = 0;

            if (recyclerView.Content == null)
            {
                AddDiagnosticRow(section.Body, "Content is missing.", AlicizaValueTone.Danger);
                warningCount++;
            }

            if (recyclerView.Templates == null || recyclerView.Templates.Length == 0)
            {
                AddDiagnosticRow(section.Body, "Templates are missing.", AlicizaValueTone.Danger);
                warningCount++;
            }

            if (recyclerView.LayoutManager == null)
            {
                AddDiagnosticRow(section.Body, "Layout Manager is missing.", AlicizaValueTone.Danger);
                warningCount++;
            }

            if (recyclerView.GetComponent<Scroller>() == null)
            {
                AddDiagnosticRow(section.Body, "Scroller is missing.", AlicizaValueTone.Danger);
                warningCount++;
            }

            LayoutManager layoutManager = recyclerView.LayoutManager;
            bool variableLength = layoutManager != null && layoutManager.VariableItemLengthEnabled;
            if (variableLength && !layoutManager.SupportsVariableItemLength)
            {
                AddDiagnosticRow(
                    section.Body,
                    "Variable item length is only supported by Linear and Mixed layouts.",
                    AlicizaValueTone.Warning);
                warningCount++;
            }
            else if (variableLength)
            {
                AddDiagnosticRow(section.Body, "Current layout is variable height.", AlicizaValueTone.Accent);
            }
            else
            {
                AddDiagnosticRow(
                    section.Body,
                    "Current layout is fixed height. Chat bubbles need Variable Item Length.",
                    AlicizaValueTone.Muted);
            }

            warningCount += AddTemplateStretchWarnings(section.Body, recyclerView);

            section.Header.Add(AlicizaEditorUI.CreateBadge(
                warningCount > 0 ? warningCount.ToString() : "OK",
                warningCount > 0 ? AlicizaValueTone.Danger : AlicizaValueTone.Success));
        }

        private int AddTemplateStretchWarnings(VisualElement parent, RecyclerView recyclerView)
        {
            ViewHolder[] templates = recyclerView.Templates;
            if (templates == null)
            {
                return 0;
            }

            Direction direction = recyclerView.Direction;
            int count = 0;
            for (int i = 0; i < templates.Length; i++)
            {
                GameObject template = GetTemplateGameObject(templates[i]);
                if (template == null)
                {
                    continue;
                }

                RectTransform rect = template.GetComponent<RectTransform>();
                if (rect != null && ItemMeasure.IsStretchedOnAxis(rect, direction))
                {
                    AddDiagnosticRow(
                        parent,
                        $"Template '{template.name}' is stretched on the scroll axis.",
                        AlicizaValueTone.Warning);
                    count++;
                }
            }

            return count;
        }

        private void RefreshRuntimeSection()
        {
            if (_runtimeCard == null || _runtimeContent == null)
            {
                return;
            }

            bool playing = Application.isPlaying;
            SetBadge(
                _runtimeBadge,
                playing ? "LIVE" : "EDIT",
                playing ? AlicizaValueTone.Accent : AlicizaValueTone.Muted,
                playing ? "Play Mode runtime diagnostics." : "Runtime diagnostics are available in Play Mode.");

            if (!_runtimeCard.Expanded)
            {
                _runtimeContent.Clear();
                _runtimeCard.Meta.text = playing ? "Live" : "Play Mode";
                return;
            }

            _runtimeContent.Clear();

            if (!playing)
            {
                _runtimeCard.Meta.text = "Play Mode";
                _runtimeContent.Add(AlicizaEditorUI.CreateHint(
                    "Enter Play Mode to inspect the visible range and pool statistics."));
                return;
            }

            RecyclerView recyclerView = target as RecyclerView;
            if (recyclerView == null)
            {
                _runtimeCard.Meta.text = "Unavailable";
                _runtimeContent.Add(AlicizaEditorUI.CreateEmptyState("RecyclerView is unavailable."));
                return;
            }

            bool hasRange = recyclerView.TryGetVisibleRange(out int start, out int end);
            string range = start >= 0 ? start + "-" + end : "none";
            string motion = recyclerView.Scroller != null
                ? recyclerView.Scroller.MotionKind.ToString()
                : "None";
            string poolStats = recyclerView.PoolStats;

            _runtimeCard.Meta.text = "Visible " + range;
            AlicizaEditorUI.AddValueRow(
                _runtimeContent,
                "Visible Range",
                range,
                start >= 0 ? AlicizaValueTone.Default : AlicizaValueTone.Muted);
            AlicizaEditorUI.AddValueRow(
                _runtimeContent,
                "Scroll Data Index",
                recyclerView.ScrollDataIndex.ToString(),
                AlicizaValueTone.Default);
            AlicizaEditorUI.AddValueRow(
                _runtimeContent,
                "Motion",
                motion,
                recyclerView.Scroller != null ? AlicizaValueTone.Default : AlicizaValueTone.Muted);
            AlicizaEditorUI.AddValueRow(
                _runtimeContent,
                "Pool",
                string.IsNullOrEmpty(poolStats) ? "<None>" : poolStats,
                string.IsNullOrEmpty(poolStats) ? AlicizaValueTone.Muted : AlicizaValueTone.Default);
        }

        private void RefreshRepairState(bool force = false)
        {
            if (_repairRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                if (_repairRoot.childCount > 0)
                {
                    _repairRoot.Clear();
                }

                _repairSignature = "playing";
                return;
            }

            RecyclerView recyclerView = target as RecyclerView;
            bool missingLayout = recyclerView != null && HasMissingLayoutManager();
            bool missingScroller = recyclerView != null && HasMissingScroller();
            bool missingScrollbarEx = recyclerView != null &&
                                      recyclerView.Scrollbar != null &&
                                      recyclerView.Scrollbar.GetComponent<ScrollbarEx>() == null;
            string signature =
                (missingLayout ? "1" : "0") +
                (missingScroller ? "1" : "0") +
                (missingScrollbarEx ? "1" : "0");

            if (!force && signature == _repairSignature)
            {
                return;
            }

            _repairSignature = signature;
            _repairRoot.Clear();

            if (!missingLayout && !missingScroller && !missingScrollbarEx)
            {
                return;
            }

            AlicizaSection section = AlicizaEditorUI.CreateStaticSection("Missing References");
            section.Header.Add(AlicizaEditorUI.CreateBadge("ACTION", AlicizaValueTone.Warning));
            _repairRoot.Add(section);

            if (missingLayout)
            {
                AddRepairAction(
                    section.Body,
                    "Layout Manager",
                    "Restore Layout Manager",
                    "Recreate the layout manager from the saved layout type name.",
                    OnRestoreLayoutManagerClicked);
            }

            if (missingScroller)
            {
                AddRepairAction(
                    section.Body,
                    "Scroller",
                    "Restore Scroller Component",
                    "Add the saved scroller component back to this GameObject.",
                    OnRestoreScrollerClicked);
            }

            if (missingScrollbarEx)
            {
                AddRepairAction(
                    section.Body,
                    "Scrollbar",
                    "Add ScrollbarEx",
                    "Add the missing ScrollbarEx component to the bound scrollbar.",
                    OnAddScrollbarExClicked);
            }
        }

        private bool HasMissingLayoutManager()
        {
            return _layoutManager != null &&
                   _layoutManager.managedReferenceValue == null &&
                   _layoutManagerTypeName != null &&
                   !string.IsNullOrEmpty(_layoutManagerTypeName.stringValue);
        }

        private bool HasMissingScroller()
        {
            return _scroller != null &&
                   _scroller.objectReferenceValue == null &&
                   _scrollerTypeName != null &&
                   !string.IsNullOrEmpty(_scrollerTypeName.stringValue);
        }

        private void RefreshEditModeAvailability()
        {
            bool editing = !Application.isPlaying;
            if (_hasEditModeState && _lastEditMode == editing)
            {
                return;
            }

            bool wasKnown = _hasEditModeState;
            _hasEditModeState = true;
            _lastEditMode = editing;

            _layoutTypeField?.SetEnabled(editing);
            _scrollerTypeField?.SetEnabled(editing);
            _directionField?.SetEnabled(editing);
            SetControlEnabled(_centerCrossAxisControl, editing);
            SetControlEnabled(_contentControl, editing);
            _scrollModeField?.SetEnabled(editing);
            _scrollbarVisibilityField?.SetEnabled(editing);

            if (_dropArea != null)
            {
                _dropArea.style.display = editing ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (wasKnown)
            {
                RebuildTemplatesList();
            }
        }

        private void OnRuntimeCardExpandedChanged(bool expanded)
        {
            if (expanded)
            {
                RefreshRuntimeSection();
            }
        }

        #endregion

        #region Layout Manager Section

        private void OnLayoutTypeChanged(int selectedIndex)
        {
            if (Application.isPlaying)
            {
                return;
            }

            UpdateLayoutManager(selectedIndex);
            serializedObject.ApplyModifiedProperties();
            ScheduleStructureRefresh();
        }

        private void RebuildLayoutDetails()
        {
            if (_layoutDetails == null)
            {
                return;
            }

            _layoutDetails.Clear();

            if (_layoutManager == null)
            {
                SetBadge(_layoutBadge, "None", AlicizaValueTone.Muted, null);
                _layoutDetails.Add(AlicizaEditorUI.CreateHint("Layout manager property is unavailable."));
                return;
            }

            if (!(_layoutManager.managedReferenceValue is LayoutManager layoutManager))
            {
                SetBadge(_layoutBadge, "None", AlicizaValueTone.Muted, null);
                _layoutDetails.Add(AlicizaEditorUI.CreateEmptyState("Please select a Layout Manager"));
                return;
            }

            Type layoutType = layoutManager.GetType();
            SetBadge(_layoutBadge, layoutType.Name, AlicizaValueTone.Accent, layoutType.FullName);

            int propertyCount = 0;
            SerializedProperty iterator = _layoutManager.Copy();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.name == "m_Script")
                {
                    continue;
                }

                if (AlicizaEditorUI.AddPropertyRow(
                        _layoutDetails, iterator.displayName, iterator, true) != null)
                {
                    propertyCount++;
                }
            }

            if (propertyCount == 0)
            {
                _layoutDetails.Add(AlicizaEditorUI.CreateHint(
                    "This layout manager has no configurable properties."));
            }
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
            string currentTypeName = _layoutManagerTypeName != null
                ? _layoutManagerTypeName.stringValue
                : string.Empty;
            _selectedLayoutIndex = Mathf.Clamp(
                _layoutTypeNames.IndexOf(currentTypeName),
                0,
                _layoutTypeNames.Count - 1);
        }

        private void UpdateLayoutManager(int selectedIndex)
        {
            if (_layoutManager == null || _layoutManagerTypeName == null)
            {
                return;
            }

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
            if (_layoutManager == null || _layoutManagerTypeName == null)
            {
                return;
            }

            _layoutManager.managedReferenceValue = null;
            _layoutManagerTypeName.stringValue = string.Empty;
            _selectedLayoutIndex = 0;
        }

        private bool IsValidLayoutIndex(int index)
        {
            return index >= 0 && index < _layoutTypeNames.Count;
        }

        #endregion

        #region Base Settings Section

        private void OnDirectionChanged()
        {
            OnSettingsChanged();

            if (Application.isPlaying)
            {
                return;
            }

            RebuildScrollbarForDirection();
            ScheduleStructureRefresh();
        }

        private void OnScrollbarVisibilityChanged(int previousValue, int currentValue)
        {
            OnSettingsChanged();
            HandleScrollBarVisibilityChange(
                (ScrollbarVisibility)previousValue,
                (ScrollbarVisibility)currentValue);
            ScheduleStructureRefresh();
        }

        private void UpdateSnapDurationVisibility(bool visible)
        {
            if (_snapDurationRow == null)
            {
                return;
            }

            _snapDurationRow.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void SetDecelerationEnabled(bool enabled)
        {
            if (_decelerationRow != null)
            {
                _decelerationRow.Content.SetEnabled(enabled);
            }
        }

        private void ApplyRuntimeScrollerSettings()
        {
            if (!(target is RecyclerView recyclerView) || recyclerView == null)
            {
                return;
            }

            recyclerView.ApplySerializedScrollerSettings();
            if (_spacing != null)
            {
                recyclerView.Spacing = _spacing.vector2Value;
            }

            if (_padding != null)
            {
                recyclerView.Padding = _padding.vector2Value;
            }

            if (_snap != null)
            {
                recyclerView.Snap = _snap.boolValue;
            }

            if (_snapDuration != null)
            {
                recyclerView.SnapDuration = _snapDuration.floatValue;
            }

            if (_refreshMode != null)
            {
                recyclerView.RefreshMode = (ListRefreshMode)_refreshMode.enumValueIndex;
            }
        }

        private void OnSettingsChanged()
        {
            if (serializedObject == null || serializedObject.targetObject == null)
            {
                return;
            }

            serializedObject.ApplyModifiedProperties();

            if (Application.isPlaying)
            {
                ApplyRuntimeScrollerSettings();
            }
        }

        #endregion

        #region Scroller Section

        private void OnScrollerTypeChanged(int selectedIndex)
        {
            if (Application.isPlaying)
            {
                return;
            }

            UpdateScroller(selectedIndex);
            serializedObject.ApplyModifiedProperties();
            ScheduleStructureRefresh();
        }

        private void RebuildScrollerDetails()
        {
            if (_scrollerDetails == null)
            {
                return;
            }

            _scrollerDetails.Clear();

            RecyclerView recyclerView = target as RecyclerView;
            Scroller scrollerComponent = recyclerView != null
                ? recyclerView.GetComponent<Scroller>()
                : null;
            if (scrollerComponent == null)
            {
                SetBadge(_scrollerBadge, "None", AlicizaValueTone.Muted, null);
                _scrollerDetails.Add(AlicizaEditorUI.CreateEmptyState("Please select a Scroller type"));
                _scrollerSerializedObject = null;
                return;
            }

            Type scrollerType = scrollerComponent.GetType();
            SetBadge(_scrollerBadge, scrollerType.Name, AlicizaValueTone.Accent, scrollerType.FullName);

            if (_scrollerSerializedObject == null ||
                _scrollerSerializedObject.targetObject != scrollerComponent)
            {
                _scrollerSerializedObject = new SerializedObject(scrollerComponent);
            }

            _scrollerSerializedObject.Update();
            int propertyCount = 0;
            SerializedProperty iterator = _scrollerSerializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.name == "m_Script")
                {
                    continue;
                }

                if (AlicizaEditorUI.AddPropertyRow(
                        _scrollerDetails, iterator.displayName, iterator, true) != null)
                {
                    propertyCount++;
                }
            }

            if (propertyCount == 0)
            {
                _scrollerDetails.Add(AlicizaEditorUI.CreateHint(
                    "This scroller has no configurable properties."));
            }
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
            RecyclerView recyclerView = target as RecyclerView;
            if (recyclerView == null)
            {
                return;
            }

            Scroller existingScroller = recyclerView.GetComponent<Scroller>();
            if (existingScroller != null)
            {
                if (_scroller != null)
                {
                    _scroller.objectReferenceValue = existingScroller;
                }

                if (_scrollerTypeName != null)
                {
                    _scrollerTypeName.stringValue = existingScroller.GetType().FullName;
                    _selectedScrollerIndex = _scrollerTypeNames.IndexOf(_scrollerTypeName.stringValue);
                }

                return;
            }

            string currentTypeName = _scrollerTypeName != null ? _scrollerTypeName.stringValue : string.Empty;
            _selectedScrollerIndex = Mathf.Clamp(
                _scrollerTypeNames.IndexOf(currentTypeName),
                0,
                Mathf.Max(0, _scrollerTypeNames.Count - 1));
        }

        private void UpdateScroller(int selectedIndex)
        {
            try
            {
                RecyclerView recyclerView = target as RecyclerView;
                if (recyclerView == null)
                {
                    return;
                }

                Undo.RecordObject(recyclerView, "Update Scroller");
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
            Scroller oldScroller = recyclerView.GetComponent<Scroller>();
            if (oldScroller != null)
            {
                Undo.DestroyObjectImmediate(oldScroller);
            }
        }

        private void AddNewScroller(RecyclerView recyclerView, int selectedIndex)
        {
            if (selectedIndex <= 0 || selectedIndex > _scrollerTypes.Count)
            {
                ClearScrollerReferences();
                return;
            }

            Type selectedType = _scrollerTypes[selectedIndex - 1];
            Scroller newScroller = Undo.AddComponent(recyclerView.gameObject, selectedType) as Scroller;
            if (newScroller == null)
            {
                Debug.LogError($"Failed to add scroller component of type '{selectedType.FullName}'.");
                ClearScrollerReferences();
                return;
            }

            if (_scroller != null)
            {
                _scroller.objectReferenceValue = newScroller;
            }

            if (_scrollerTypeName != null)
            {
                _scrollerTypeName.stringValue = selectedType.FullName;
            }

            _selectedScrollerIndex = selectedIndex;

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(recyclerView);
        }

        private void ClearScrollerReferences()
        {
            if (_scroller != null)
            {
                _scroller.objectReferenceValue = null;
            }

            if (_scrollerTypeName != null)
            {
                _scrollerTypeName.stringValue = string.Empty;
            }

            _selectedScrollerIndex = 0;
        }

        #endregion

        #region Scrollbar Handling

        private void HandleScrollBarVisibilityChange(
            ScrollbarVisibility previousVisibility,
            ScrollbarVisibility currentVisibility)
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
            RecyclerView recyclerView = target as RecyclerView;
            if (recyclerView == null || _scrollbar == null || _direction == null)
            {
                return;
            }

            if (_scrollbar.objectReferenceValue != null)
            {
                return;
            }

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
            Undo.RegisterCreatedObjectUndo(instance, "Create Scrollbar");
            PrefabUtility.UnpackPrefabInstance(
                instance, PrefabUnpackMode.Completely, InteractionMode.UserAction);

            if (_scrollbar != null)
            {
                _scrollbar.objectReferenceValue = instance.GetComponent<Scrollbar>();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void ClearScrollBar()
        {
            if (_scrollbarVisibility != null)
            {
                _scrollbarVisibility.enumValueIndex = (int)ScrollbarVisibility.AlwaysHide;
            }

            if (_scrollbar != null && _scrollbar.objectReferenceValue != null)
            {
                Scrollbar scrollbarComponent = _scrollbar.objectReferenceValue as Scrollbar;
                _scrollbar.objectReferenceValue = null;

                if (scrollbarComponent != null)
                {
                    Undo.DestroyObjectImmediate(scrollbarComponent.gameObject);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void RebuildScrollbarForDirection()
        {
            if (_scrollbarVisibility == null ||
                _scrollbar == null ||
                (ScrollbarVisibility)_scrollbarVisibility.enumValueIndex == ScrollbarVisibility.AlwaysHide)
            {
                return;
            }

            if (_scrollbar.objectReferenceValue is Scrollbar oldBar && oldBar != null)
            {
                _scrollbar.objectReferenceValue = null;
                Undo.DestroyObjectImmediate(oldBar.gameObject);
            }

            CreateScrollBar();
        }

        #endregion

        #region Templates Section

        private VisualElement BuildDropArea()
        {
            VisualElement dropArea = AlicizaEditorUI.CreateEmptyState("Drag ViewHolder Templates Here");
            dropArea.name = "recycler-view-template-drop-area";
            dropArea.style.minHeight = DropAreaHeight;
            dropArea.style.marginTop = 6f;
            dropArea.style.marginBottom = 2f;
            dropArea.style.display = Application.isPlaying ? DisplayStyle.None : DisplayStyle.Flex;

            dropArea.RegisterCallback<DragEnterEvent>(_ => SetDropHighlight(true));
            dropArea.RegisterCallback<DragLeaveEvent>(_ => SetDropHighlight(false));
            dropArea.RegisterCallback<DragExitedEvent>(_ => SetDropHighlight(false));
            dropArea.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            dropArea.RegisterCallback<DragPerformEvent>(OnDragPerform);
            return dropArea;
        }

        private void SetDropHighlight(bool highlighted)
        {
            if (_dropArea == null)
            {
                return;
            }

            _dropArea.EnableInClassList("alicia-chip--selected", highlighted);
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (Application.isPlaying)
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.StopPropagation();
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            if (Application.isPlaying)
            {
                return;
            }

            DragAndDrop.AcceptDrag();
            SetDropHighlight(false);
            ProcessDraggedTemplates();
            evt.StopPropagation();
        }

        private void ProcessDraggedTemplates()
        {
            bool changed = false;
            foreach (Object draggedObject in DragAndDrop.objectReferences)
            {
                if (draggedObject is GameObject gameObject)
                {
                    changed |= ProcessDraggedGameObject(gameObject);
                }
            }

            if (changed)
            {
                RebuildTemplatesList();
            }
        }

        private bool ProcessDraggedGameObject(GameObject gameObject)
        {
            ViewHolder viewHolder = gameObject.GetComponent<ViewHolder>();
            if (viewHolder != null)
            {
                return AddTemplate(viewHolder);
            }

            Debug.LogWarning($"GameObject '{gameObject.name}' must have a ViewHolder component.");
            return false;
        }

        private bool AddTemplate(ViewHolder viewHolder)
        {
            if (_templates == null || viewHolder == null)
            {
                return false;
            }

            if (IsTemplateDuplicate(viewHolder))
            {
                Debug.LogWarning($"Template '{viewHolder.name}' already exists in the list.");
                return false;
            }

            int index = _templates.arraySize;
            _templates.arraySize = index + 1;
            _templates.GetArrayElementAtIndex(index).objectReferenceValue = viewHolder;
            serializedObject.ApplyModifiedProperties();
            return true;
        }

        private bool IsTemplateDuplicate(ViewHolder viewHolder)
        {
            if (_templates == null)
            {
                return false;
            }

            for (int i = 0; i < _templates.arraySize; i++)
            {
                SerializedProperty existingItem = _templates.GetArrayElementAtIndex(i);
                if (existingItem.objectReferenceValue == viewHolder)
                {
                    return true;
                }
            }

            return false;
        }

        private void RebuildTemplatesList()
        {
            if (_templatesList == null)
            {
                return;
            }

            _templatesList.Clear();

            if (_templates == null || !_templates.isArray)
            {
                SetBadge(_templateBadge, "—", AlicizaValueTone.Muted, null);
                _templatesList.Add(AlicizaEditorUI.CreateHint(
                    "Template list property is unavailable."));
                return;
            }

            int count = _templates.arraySize;
            SetBadge(
                _templateBadge,
                count.ToString(),
                count > 0 ? AlicizaValueTone.Accent : AlicizaValueTone.Muted,
                count == 1 ? "1 template" : count + " templates");

            if (count == 0)
            {
                _templatesList.Add(AlicizaEditorUI.CreateEmptyState("No ViewHolder templates."));
                return;
            }

            for (int i = 0; i < count; i++)
            {
                AddTemplateRow(i);
            }
        }

        private void AddTemplateRow(int index)
        {
            if (_templates == null || index < 0 || index >= _templates.arraySize)
            {
                return;
            }

            SerializedProperty item = _templates.GetArrayElementAtIndex(index);
            var objectField = new ObjectField
            {
                label = string.Empty,
                objectType = typeof(ViewHolder),
                allowSceneObjects = true
            };
            objectField.AddToClassList("alicia-control");
            objectField.AddToClassList("alicia-control--grow");
            objectField.BindProperty(item);

            AlicizaRow row = AlicizaEditorUI.AddRow(
                _templatesList, "#" + index, objectField, true);
            row.Caption.style.width = TemplateCaptionWidth;
            row.Caption.style.minWidth = TemplateCaptionWidth;
            row.Caption.tooltip = "Template " + index;

            if (Application.isPlaying)
            {
                return;
            }

            var actions = new VisualElement { name = "recycler-view-template-actions" };
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.alignItems = Align.Center;
            actions.style.flexShrink = 0f;

            Button moveUp = AlicizaEditorUI.CreateIconButton("↑", "Move template up");
            moveUp.SetEnabled(index > 0);
            moveUp.clicked += () => MoveTemplateItem(index, index - 1);
            actions.Add(moveUp);

            Button moveDown = AlicizaEditorUI.CreateIconButton("↓", "Move template down");
            moveDown.SetEnabled(index < _templates.arraySize - 1);
            moveDown.clicked += () => MoveTemplateItem(index, index + 1);
            actions.Add(moveDown);

            Button remove = AlicizaEditorUI.CreateIconButton("×", "Remove template");
            remove.clicked += () => RemoveTemplateItem(index);
            actions.Add(remove);

            row.Content.Add(actions);
        }

        private void MoveTemplateItem(int fromIndex, int toIndex)
        {
            if (_templates == null ||
                fromIndex < 0 ||
                toIndex < 0 ||
                fromIndex >= _templates.arraySize ||
                toIndex >= _templates.arraySize ||
                fromIndex == toIndex)
            {
                return;
            }

            _templates.MoveArrayElement(fromIndex, toIndex);
            serializedObject.ApplyModifiedProperties();
            RebuildTemplatesList();
        }

        private void RemoveTemplateItem(int index)
        {
            if (_templates == null || index < 0 || index >= _templates.arraySize)
            {
                return;
            }

            int previousSize = _templates.arraySize;
            _templates.DeleteArrayElementAtIndex(index);
            if (_templates.arraySize == previousSize)
            {

                _templates.DeleteArrayElementAtIndex(index);
            }

            serializedObject.ApplyModifiedProperties();
            RebuildTemplatesList();
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

        #region Restore Actions

        private void OnRestoreLayoutManagerClicked()
        {
            RestoreLayoutManagerFromTypeNameIfMissing();
            serializedObject.ApplyModifiedProperties();
            ScheduleStructureRefresh();
        }

        private void OnRestoreScrollerClicked()
        {
            RestoreScrollerFromTypeNameIfMissing();
            serializedObject.ApplyModifiedProperties();
            ScheduleStructureRefresh();
        }

        private void OnAddScrollbarExClicked()
        {
            RecyclerView recyclerView = target as RecyclerView;
            if (recyclerView == null || recyclerView.Scrollbar == null)
            {
                return;
            }

            Undo.AddComponent<ScrollbarEx>(recyclerView.Scrollbar.gameObject);
            EditorUtility.SetDirty(recyclerView.Scrollbar);
            ScheduleStructureRefresh();
        }

        private void RestoreLayoutManagerFromTypeNameIfMissing()
        {
            try
            {
                if (_layoutManager == null || _layoutManagerTypeName == null)
                {
                    return;
                }

                if (_layoutManager.managedReferenceValue != null)
                {
                    return;
                }

                string typeName = _layoutManagerTypeName.stringValue;
                if (string.IsNullOrEmpty(typeName))
                {
                    return;
                }

                Type type = AlicizaX.Utility.Assembly.GetType(typeName);
                if (type == null)
                {
                    Debug.LogWarning(
                        $"LayoutManager type '{typeName}' not found. Cannot restore layout manager.");
                    return;
                }

                if (!typeof(ILayoutManager).IsAssignableFrom(type))
                {
                    Debug.LogWarning(
                        $"Type '{typeName}' does not implement ILayoutManager. Cannot restore layout manager.");
                    _layoutManagerTypeName.stringValue = string.Empty;
                    return;
                }

                _layoutManager.managedReferenceValue = Activator.CreateInstance(type);
                RefreshLayoutTypes();
                _selectedLayoutIndex = Mathf.Clamp(
                    _layoutTypeNames.IndexOf(typeName), 0, _layoutTypeNames.Count - 1);

                Debug.Log($"LayoutManager restored from type name '{typeName}'.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error restoring LayoutManager: {e}");
                if (_layoutManager != null)
                {
                    _layoutManager.managedReferenceValue = null;
                }

                if (_layoutManagerTypeName != null)
                {
                    _layoutManagerTypeName.stringValue = string.Empty;
                }
            }
        }

        private void RestoreScrollerFromTypeNameIfMissing()
        {
            try
            {
                if (_scroller == null || _scrollerTypeName == null)
                {
                    return;
                }

                if (_scroller.objectReferenceValue != null)
                {
                    return;
                }

                string typeName = _scrollerTypeName.stringValue;
                if (string.IsNullOrEmpty(typeName))
                {
                    return;
                }

                Type type = AlicizaX.Utility.Assembly.GetType(typeName) ?? Type.GetType(typeName);
                if (type == null)
                {
                    Debug.LogWarning($"Scroller type '{typeName}' not found. Cannot restore scroller component.");
                    _scrollerTypeName.stringValue = string.Empty;
                    return;
                }

                if (!typeof(Scroller).IsAssignableFrom(type))
                {
                    Debug.LogWarning($"Type '{typeName}' is not a Scroller. Cannot restore scroller.");
                    _scrollerTypeName.stringValue = string.Empty;
                    return;
                }

                RecyclerView recyclerView = target as RecyclerView;
                if (recyclerView == null)
                {
                    return;
                }

                Scroller newScroller = Undo.AddComponent(recyclerView.gameObject, type) as Scroller;
                if (newScroller == null)
                {
                    Debug.LogError(
                        $"Failed to add scroller component of type '{typeName}' to GameObject '{recyclerView.gameObject.name}'.");
                    _scrollerTypeName.stringValue = string.Empty;
                    return;
                }

                _scroller.objectReferenceValue = newScroller;
                RefreshScrollerTypes();
                _selectedScrollerIndex = Mathf.Clamp(
                    _scrollerTypeNames.IndexOf(typeName), 0, _scrollerTypeNames.Count - 1);

                EditorUtility.SetDirty(recyclerView);
                Debug.Log(
                    $"Scroller component of type '{typeName}' restored and attached to GameObject '{recyclerView.gameObject.name}'.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Error restoring Scroller: {e}");
                if (_scroller != null)
                {
                    _scroller.objectReferenceValue = null;
                }

                if (_scrollerTypeName != null)
                {
                    _scrollerTypeName.stringValue = string.Empty;
                }
            }
        }

        private void ScheduleStructureRefresh()
        {
            if (_root == null)
            {
                return;
            }

            _root.schedule.Execute(() =>
            {
                if (_root == null || serializedObject == null || serializedObject.targetObject == null)
                {
                    return;
                }

                serializedObject.Update();
                SyncTypeFields();
                RebuildLayoutDetails();
                RebuildScrollerDetails();
                RefreshRepairState(true);
                RefreshDiagnostics();
                RefreshRuntimeSection();
                RefreshEditModeAvailability();
            }).ExecuteLater(0);
        }

        private void OnUndoRedoPerformed()
        {
            if (_root == null)
            {
                return;
            }

            _root.schedule.Execute(() =>
            {
                if (_root == null || serializedObject == null || serializedObject.targetObject == null)
                {
                    return;
                }

                serializedObject.Update();
                SyncTypeFields();
                RebuildLayoutDetails();
                RebuildScrollerDetails();
                RebuildTemplatesList();
                RefreshRepairState(true);
                RefreshDiagnostics();
                RefreshRuntimeSection();
            }).ExecuteLater(0);
        }

        private void SyncTypeFields()
        {
            if (_layoutTypeField != null)
            {
                _layoutTypeField.SetValueWithoutNotify(Mathf.Clamp(
                    _selectedLayoutIndex, 0, Mathf.Max(0, _layoutTypeNameOptions.Length - 1)));
            }

            if (_scrollerTypeField != null)
            {
                _scrollerTypeField.SetValueWithoutNotify(Mathf.Clamp(
                    _selectedScrollerIndex, 0, Mathf.Max(0, _scrollerTypeNameOptions.Length - 1)));
            }
        }

        #endregion

        #region UI Helpers

        private static void AddSubHeader(VisualElement parent, string title)
        {
            var label = new Label(title);
            label.AddToClassList("alicia-section__title");
            label.style.marginTop = 10f;
            label.style.marginBottom = 2f;
            label.style.marginLeft = 2f;
            parent.Add(label);
        }

        private AlicizaRow AddVector2Row(
            VisualElement parent,
            string caption,
            SerializedProperty property)
        {
            if (property == null)
            {
                return null;
            }

            var field = new Vector2Field { label = string.Empty };
            field.AddToClassList("alicia-control");
            field.AddToClassList("alicia-control--grow");
            field.BindProperty(property);

            AlicizaRow row = AlicizaEditorUI.AddRow(parent, caption, field);
            TrackSettingsChanges(field);
            return row;
        }

        private void TrackSettingsChanges(VisualElement control)
        {
            if (control == null)
            {
                return;
            }

            control.RegisterCallback<ChangeEvent<bool>>(_ => OnSettingsChanged());
            control.RegisterCallback<ChangeEvent<int>>(_ => OnSettingsChanged());
            control.RegisterCallback<ChangeEvent<float>>(_ => OnSettingsChanged());
            control.RegisterCallback<ChangeEvent<string>>(_ => OnSettingsChanged());
            control.RegisterCallback<ChangeEvent<Vector2>>(_ => OnSettingsChanged());
            control.RegisterCallback<ChangeEvent<Object>>(_ => OnSettingsChanged());
        }

        private static void SetControlEnabled(VisualElement control, bool enabled)
        {
            if (control != null)
            {
                control.SetEnabled(enabled);
            }
        }

        private static void AddDiagnosticRow(
            VisualElement parent,
            string message,
            AlicizaValueTone tone)
        {
            var row = new VisualElement { name = "recycler-view-diagnostic" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 22f;
            row.style.paddingLeft = 2f;
            row.style.paddingRight = 2f;

            Label marker = AlicizaEditorUI.CreateBadge(
                tone == AlicizaValueTone.Danger || tone == AlicizaValueTone.Warning ? "!" : "i",
                tone);
            marker.style.marginLeft = 0f;
            row.Add(marker);

            Label label = AlicizaEditorUI.CreateValueLabel(message, tone);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginLeft = 7f;
            row.Add(label);

            parent.Add(row);
        }

        private static void AddRepairAction(
            VisualElement parent,
            string caption,
            string actionLabel,
            string tooltip,
            Action action)
        {
            AlicizaRow row = AlicizaEditorUI.AddRow(parent, caption, null);
            Button button = AlicizaEditorUI.CreateTextButton(actionLabel, tooltip);
            button.clicked += action;
            row.Content.Add(button);
        }

        private static void SetBadge(
            Label badge,
            string text,
            AlicizaValueTone tone,
            string tooltip)
        {
            if (badge == null)
            {
                return;
            }

            badge.text = text ?? string.Empty;
            badge.tooltip = tooltip ?? string.Empty;
            badge.EnableInClassList("alicia-tone--default", tone == AlicizaValueTone.Default);
            badge.EnableInClassList("alicia-tone--muted", tone == AlicizaValueTone.Muted);
            badge.EnableInClassList("alicia-tone--accent", tone == AlicizaValueTone.Accent);
            badge.EnableInClassList("alicia-tone--success", tone == AlicizaValueTone.Success);
            badge.EnableInClassList("alicia-tone--warning", tone == AlicizaValueTone.Warning);
            badge.EnableInClassList("alicia-tone--danger", tone == AlicizaValueTone.Danger);
        }

        #endregion
    }
}
