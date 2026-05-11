using AlicizaX.Editor;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(InputGlyphDatabase))]
public sealed class InputGlyphDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Open Glyph Database"))
        {
            InputGlyphDatabaseWindow.OpenFromAsset((InputGlyphDatabase)target);
        }
    }

    [OnOpenAsset(0)]
    private static bool OpenAsset(int instanceID, int line)
    {
        InputGlyphDatabase database = EditorUtility.InstanceIDToObject(instanceID) as InputGlyphDatabase;
        if (database == null)
        {
            return false;
        }

        InputGlyphDatabaseWindow.OpenFromAsset(database);
        return true;
    }
}

internal sealed class InputGlyphDatabaseWindow : EditorWindow
{
    private const string MenuPath = "AlicizaX/Extension/Input/InputGlyph";
    private const string DefaultDatabaseName = "InputGlyphDatabase";
    private const string TablesPropertyName = "tables";
    private const string PlaceholderSpritePropertyName = "placeholderSprite";
    private const string DeviceNamePropertyName = "deviceName";
    private const string SpriteSheetPropertyName = "spriteSheetTexture";
    private const string EntriesPropertyName = "entries";
    private const string EntrySpritePropertyName = "Sprite";
    private const string EntryActionPropertyName = "action";
    private const float ToolbarHeight = 30f;
    private const float SidebarWidth = 236f;
    private const float SidebarRowHeight = 42f;
    private const float MainPadding = 12f;
    private const float EntryRowMinHeight = 62f;
    private const float EntryActionButtonSize = 20f;
    private const float PreviewSize = 40f;
    private const float EntryRowPadding = 8f;
    private const float EntryLeftColumnWidth = 56f;
    private const float EntryColumnGap = 10f;
    private const float EntryOverlayButtonSize = 16f;
    private const float FieldLabelWidth = 108f;
    private const float ParseButtonWidth = 72f;
    private const int SettingsIndex = -1;
    private const string SearchControlName = "InputGlyphDatabaseSearch";

    private static readonly string[] FixedTableNames =
    {
        "PlayStation",
        "Xbox",
        "Switch",
        "Keyboard",
    };

    private InputGlyphDatabase _database;
    private SerializedObject _serializedDatabase;
    private SerializedProperty _tablesProperty;
    private SerializedProperty _placeholderSpriteProperty;
    private Vector2 _tableScroll;
    private int _selectedTable;
    private string _search = string.Empty;
    private bool _focusSearch;
    private GUIStyle _panelStyle;
    private GUIStyle _entryBodyStyle;
    private GUIStyle _fieldRowStyle;
    private GUIStyle _rowLabelStyle;
    private GUIStyle _fieldLabelStyle;
    private GUIStyle _mutedMiniLabelStyle;
    private GUIStyle _emptyStateStyle;
    private GUIStyle _kindBadgeStyle;
    private GUIStyle _warningLabelStyle;
    private GUIContent _saveContent;
    private GUIContent _refreshContent;
    private GUIContent _addEntryContent;
    private GUIContent _settingsContent;
    private InputGlyphDatabase[] _availableDatabases = System.Array.Empty<InputGlyphDatabase>();
    private GUIContent[] _databaseOptions = System.Array.Empty<GUIContent>();
    private readonly List<int> _visibleEntryIndices = new List<int>(64);
    private VisualElement _root;
    private IMGUIContainer _chromeContainer;
    private IMGUIContainer _toolbarContainer;
    private IMGUIContainer _sidebarContainer;
    private IMGUIContainer _tableHeaderContainer;
    private ListView _entryListView;
    private SerializedProperty _currentEntriesProperty;
    private bool _entryListRefreshScheduled;

    [MenuItem(MenuPath, false, 80)]
    private static void OpenFromMenu()
    {
        InputGlyphDatabaseWindow window = GetWindow<InputGlyphDatabaseWindow>("Input Glyph Database", true);
        window.minSize = new Vector2(940f, 560f);
        window.EnsureDatabaseForWindow(true);
        window.Show();
    }

    internal static void OpenFromAsset(InputGlyphDatabase database)
    {
        InputGlyphDatabaseWindow window = GetWindow<InputGlyphDatabaseWindow>("Input Glyph Database", true);
        window.RefreshAvailableDatabases();
        window.SetDatabase(database);
        window.minSize = new Vector2(940f, 560f);
        window.Show();
    }

    private void OnEnable()
    {
        InitializeContents();
        EnsureStyles();
        RefreshAvailableDatabases();
    }

    private void CreateGUI()
    {
        BuildVisualTree();
    }

    private void SetDatabase(InputGlyphDatabase database)
    {
        _database = database;
        Undo.RecordObject(_database, "Initialize Input Glyph Tables");
        if (_database.EditorEnsureDefaultTables())
        {
            EditorUtility.SetDirty(_database);
        }

        _serializedDatabase = new SerializedObject(_database);
        _tablesProperty = _serializedDatabase.FindProperty(TablesPropertyName);
        _placeholderSpriteProperty = _serializedDatabase.FindProperty(PlaceholderSpritePropertyName);
        _selectedTable = Mathf.Clamp(_selectedTable, 0, FixedTableNames.Length - 1);
        titleContent = new GUIContent(database.name, EditorGUIUtility.IconContent("d_ScriptableObject Icon").image);
    }

    private void OnGUI()
    {
        if (_root != null)
        {
            return;
        }

        if (_database == null || _serializedDatabase == null)
        {
            EnsureDatabaseForWindow(false);
        }

        if (_database == null || _serializedDatabase == null)
        {
            EditorApplication.delayCall += CloseIfDatabaseMissing;
            return;
        }

        InitializeContents();
        EnsureStyles();
        _serializedDatabase.Update();
        EnsureSerializedDefaultTables();

        DrawToolbar();
        DrawContent();

        if (_serializedDatabase.ApplyModifiedProperties())
        {
            MarkDirty();
        }
    }

    private void CloseIfDatabaseMissing()
    {
        if (this != null && _database == null)
        {
            Close();
        }
    }

    private void BuildVisualTree()
    {
        _root = rootVisualElement;
        _root.Clear();
        _root.style.flexDirection = FlexDirection.Column;
        _root.style.backgroundColor = AlicizaEditorGUI.Colors.Body;

        _toolbarContainer = new IMGUIContainer(DrawToolbarGui)
        {
            style =
            {
                height = ToolbarHeight,
                flexShrink = 0f,
            }
        };
        _root.Add(_toolbarContainer);

        VisualElement content = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                flexGrow = 1f,
            }
        };
        _root.Add(content);

        _sidebarContainer = new IMGUIContainer(DrawSidebarGui)
        {
            style =
            {
                width = SidebarWidth,
                flexShrink = 0f,
            }
        };
        content.Add(_sidebarContainer);

        VisualElement main = new VisualElement
        {
            style =
            {
                flexGrow = 1f,
                paddingLeft = MainPadding,
                paddingTop = MainPadding,
                paddingRight = MainPadding,
                paddingBottom = MainPadding,
            }
        };
        content.Add(main);

        _tableHeaderContainer = new IMGUIContainer(DrawTableHeaderGui)
        {
            style =
            {
                flexShrink = 0f,
            }
        };
        main.Add(_tableHeaderContainer);

        _entryListView = new ListView
        {
            itemsSource = _visibleEntryIndices,
            virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
            fixedItemHeight = EntryRowMinHeight,
            makeItem = MakeEntryListItem,
            bindItem = BindEntryListItem,
            selectionType = SelectionType.None,
            showBorder = false,
            showAlternatingRowBackgrounds = AlternatingRowBackground.None,
        };
        _entryListView.style.flexGrow = 1f;
        _entryListView.showBoundCollectionSize = false;
        _entryListView.showAddRemoveFooter = false;
        main.Add(_entryListView);
    }

    private void DrawToolbarGui()
    {
        if (!PrepareGui())
        {
            return;
        }

        Rect toolbarRect = new Rect(0f, 0f, position.width, ToolbarHeight);
        DrawToolbar(toolbarRect);
        ApplyGuiChanges();
    }

    private void DrawSidebarGui()
    {
        if (!PrepareGui())
        {
            return;
        }

        DrawSidebar(new Rect(0f, 0f, SidebarWidth, Mathf.Max(1f, position.height - ToolbarHeight)));
        ApplyGuiChanges();
    }

    private void DrawTableHeaderGui()
    {
        if (!PrepareGui())
        {
            return;
        }

        if (_selectedTable == SettingsIndex)
        {
            EditorGUILayout.BeginVertical(_panelStyle);
            DrawSettings();
            EditorGUILayout.EndVertical();
        }
        else
        {
            _selectedTable = Mathf.Clamp(_selectedTable, 0, FixedTableNames.Length - 1);
            SerializedProperty table = GetFixedTableProperty(_selectedTable);
            SerializedProperty nameProperty = table.FindPropertyRelative(DeviceNamePropertyName);
            SerializedProperty sheetProperty = table.FindPropertyRelative(SpriteSheetPropertyName);
            SerializedProperty entriesProperty = table.FindPropertyRelative(EntriesPropertyName);
            nameProperty.stringValue = FixedTableNames[_selectedTable];
            DrawPanelToolbar(FixedTableNames[_selectedTable], EntryCountLabel(table), entriesProperty, true);
            DrawTableHeader(sheetProperty, entriesProperty);
            SetCurrentEntries(entriesProperty);
        }

        ApplyGuiChanges();
    }

    private bool PrepareGui()
    {
        if (_database == null || _serializedDatabase == null)
        {
            EnsureDatabaseForWindow(false);
        }

        if (_database == null || _serializedDatabase == null)
        {
            return false;
        }

        InitializeContents();
        EnsureStyles();
        _serializedDatabase.Update();
        EnsureSerializedDefaultTables();
        return true;
    }

    private void ApplyGuiChanges()
    {
        if (_serializedDatabase != null && _serializedDatabase.ApplyModifiedProperties())
        {
            MarkDirty();
            RefreshEntryList();
        }
    }

    private VisualElement MakeEntryListItem()
    {
        return new IMGUIContainer();
    }

    private void BindEntryListItem(VisualElement element, int itemIndex)
    {
        IMGUIContainer container = (IMGUIContainer)element;
        container.onGUIHandler = () => DrawEntryListItem(itemIndex);
    }

    private void DrawEntryListItem(int itemIndex)
    {
        if (!PrepareGui() || _currentEntriesProperty == null || itemIndex < 0 || itemIndex >= _visibleEntryIndices.Count)
        {
            return;
        }

        int entryIndex = _visibleEntryIndices[itemIndex];
        if (entryIndex < 0 || entryIndex >= _currentEntriesProperty.arraySize)
        {
            return;
        }

        SerializedProperty entry = _currentEntriesProperty.GetArrayElementAtIndex(entryIndex);
        float rowHeight = GetEntryRowHeight(entry);
        Rect rowRect = GUILayoutUtility.GetRect(1f, rowHeight, GUILayout.ExpandWidth(true));
        DrawEntryRow(rowRect, _currentEntriesProperty, entryIndex, entry);
        ApplyGuiChanges();
    }

    private void SetCurrentEntries(SerializedProperty entriesProperty)
    {
        string currentPath = _currentEntriesProperty != null ? _currentEntriesProperty.propertyPath : string.Empty;
        _currentEntriesProperty = entriesProperty.Copy();
        if (!string.Equals(currentPath, _currentEntriesProperty.propertyPath, System.StringComparison.Ordinal))
        {
            RefreshEntryList();
        }
        else if (_entryListView != null && _entryListView.itemsSource == null)
        {
            RefreshEntryList();
        }
    }

    private void RefreshEntryList()
    {
        if (_entryListRefreshScheduled || _entryListView == null)
        {
            return;
        }

        _entryListRefreshScheduled = true;
        _entryListView.schedule.Execute(RefreshEntryListNow);
    }

    private void RefreshEntryListNow()
    {
        _entryListRefreshScheduled = false;
        if (_currentEntriesProperty == null || _entryListView == null)
        {
            return;
        }

        _serializedDatabase?.Update();
        _visibleEntryIndices.Clear();
        for (int i = 0; i < _currentEntriesProperty.arraySize; i++)
        {
            SerializedProperty entry = _currentEntriesProperty.GetArrayElementAtIndex(i);
            if (EntryMatches(entry))
            {
                _visibleEntryIndices.Add(i);
            }
        }

        _entryListView.itemsSource = _visibleEntryIndices;
        _entryListView.Rebuild();
    }

    private void EnsureDatabaseForWindow(bool promptCreate)
    {
        RefreshAvailableDatabases();
        if (_availableDatabases.Length > 0)
        {
            SetDatabase(_availableDatabases[0]);
            return;
        }

        if (!promptCreate)
        {
            return;
        }

        InputGlyphDatabase createdDatabase = CreateDatabaseAsset();
        if (createdDatabase == null)
        {
            return;
        }

        RefreshAvailableDatabases();
        SetDatabase(createdDatabase);
    }

    private void RefreshAvailableDatabases()
    {
        string[] guids = AssetDatabase.FindAssets("t:InputGlyphDatabase");
        string[] paths = new string[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
        }

        System.Array.Sort(paths, System.StringComparer.Ordinal);

        InputGlyphDatabase[] databases = new InputGlyphDatabase[paths.Length];
        GUIContent[] options = new GUIContent[paths.Length];
        int count = 0;
        for (int i = 0; i < paths.Length; i++)
        {
            InputGlyphDatabase database = AssetDatabase.LoadAssetAtPath<InputGlyphDatabase>(paths[i]);
            if (database == null)
            {
                continue;
            }

            databases[count] = database;
            options[count] = new GUIContent(database.name, paths[i]);
            count++;
        }

        if (count != databases.Length)
        {
            System.Array.Resize(ref databases, count);
            System.Array.Resize(ref options, count);
        }

        _availableDatabases = databases;
        _databaseOptions = options;
    }

    private static InputGlyphDatabase CreateDatabaseAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Input Glyph Database",
            DefaultDatabaseName,
            "asset",
            "Choose where to save the InputGlyphDatabase asset.");

        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        InputGlyphDatabase database = CreateInstance<InputGlyphDatabase>();
        database.EditorEnsureDefaultTables();
        AssetDatabase.CreateAsset(database, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorGUIUtility.PingObject(database);
        return database;
    }

    private void DrawDatabasePopup(Rect rect)
    {
        if (_availableDatabases.Length == 0)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.Popup(rect, 0, new[] { new GUIContent("No InputGlyphDatabase") });
            }

            return;
        }

        int selectedIndex = 0;
        for (int i = 0; i < _availableDatabases.Length; i++)
        {
            if (_availableDatabases[i] == _database)
            {
                selectedIndex = i;
                break;
            }
        }

        int nextIndex = EditorGUI.Popup(rect, selectedIndex, _databaseOptions);
        if (nextIndex != selectedIndex && nextIndex >= 0 && nextIndex < _availableDatabases.Length)
        {
            Save();
            SetDatabase(_availableDatabases[nextIndex]);
            GUI.FocusControl(string.Empty);
        }
    }

    private void DrawToolbar()
    {
        DrawToolbar(new Rect(0f, 0f, position.width, ToolbarHeight));
    }

    private void DrawToolbar(Rect toolbarRect)
    {
        AlicizaEditorGUI.DrawToolbarBackground(toolbarRect);

        Rect saveRect = new Rect(toolbarRect.x + 6f, toolbarRect.y + 5f, 22f, 20f);
        Rect refreshRect = new Rect(saveRect.xMax + 4f, saveRect.y, 22f, 20f);
        Rect databaseRect = new Rect(refreshRect.xMax + 8f, toolbarRect.y + 5f, 260f, 20f);
        Rect searchRect = new Rect(databaseRect.xMax + 8f, toolbarRect.y + 4f, 260f, 22f);

        if (AlicizaEditorGUI.DrawToolbarButton(saveRect, _saveContent))
        {
            Save();
        }

        if (AlicizaEditorGUI.DrawToolbarButton(refreshRect, _refreshContent))
        {
            RefreshAvailableDatabases();
            _database.EditorRefreshCache();
            Repaint();
        }

        DrawDatabasePopup(databaseRect);
        string nextSearch = AlicizaEditorGUI.DrawSearchField(searchRect, _search, "Search sprite or action...", SearchControlName, ref _focusSearch);
        if (!string.Equals(_search, nextSearch, System.StringComparison.Ordinal))
        {
            _search = nextSearch;
            RefreshEntryList();
        }
    }

    private void DrawContent()
    {
        Rect contentRect = new Rect(0f, ToolbarHeight, position.width, position.height - ToolbarHeight);
        Rect sidebarRect = new Rect(contentRect.x, contentRect.y, SidebarWidth, contentRect.height);
        Rect mainRect = new Rect(sidebarRect.xMax, contentRect.y, Mathf.Max(1f, contentRect.width - SidebarWidth), contentRect.height);

        DrawSidebar(sidebarRect);
        DrawMainPanel(mainRect);
    }

    private void DrawSidebar(Rect rect)
    {
        EditorGUI.DrawRect(rect, AlicizaEditorGUI.Colors.Panel);
        AlicizaEditorGUI.DrawOutline(rect);

        Rect headerRect = new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 26f);
        AlicizaEditorGUI.DrawToolbarBackground(headerRect);
        GUI.Label(new Rect(headerRect.x + 8f, headerRect.y + 4f, headerRect.width - 16f, 18f), "Device Tables", _rowLabelStyle);

        Rect listRect = new Rect(rect.x + 8f, headerRect.yMax + 6f, rect.width - 16f, Mathf.Max(1f, rect.height - 86f));
        Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, FixedTableNames.Length * (SidebarRowHeight + 2f));
        _tableScroll = GUI.BeginScrollView(listRect, _tableScroll, viewRect, false, true);
        for (int i = 0; i < FixedTableNames.Length; i++)
        {
            SerializedProperty table = GetFixedTableProperty(i);
            DrawTableRow(new Rect(0f, i * (SidebarRowHeight + 2f), viewRect.width, SidebarRowHeight), i, table);
        }

        GUI.EndScrollView();

        Rect footerRect = new Rect(rect.x + 8f, rect.yMax - 34f, rect.width - 16f, 26f);
        DrawSettingsRow(footerRect);
    }

    private void DrawTableRow(Rect rowRect, int index, SerializedProperty table)
    {
        Event currentEvent = Event.current;
        bool selected = _selectedTable == index;
        bool hovered = rowRect.Contains(currentEvent.mousePosition);
        AlicizaEditorGUI.DrawListItemBackground(rowRect, selected, hovered);

        Rect badgeRect = new Rect(rowRect.x + 7f, rowRect.y + 8f, 42f, 24f);
        GUI.Label(badgeRect, GetDeviceBadge(FixedTableNames[index]), _kindBadgeStyle);

        Rect titleRect = new Rect(badgeRect.xMax + 8f, rowRect.y + 5f, rowRect.width - badgeRect.xMax - 14f, 17f);
        Rect subtitleRect = new Rect(titleRect.x, rowRect.y + 22f, titleRect.width, 15f);
        GUI.Label(titleRect, FixedTableNames[index], _rowLabelStyle);
        GUI.Label(subtitleRect, EntryCountLabel(table), _mutedMiniLabelStyle);

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && rowRect.Contains(currentEvent.mousePosition))
        {
            _selectedTable = index;
            GUI.FocusControl(string.Empty);
            currentEvent.Use();
        }
    }

    private void DrawSettingsRow(Rect rowRect)
    {
        Event currentEvent = Event.current;
        bool selected = _selectedTable == SettingsIndex;
        bool hovered = rowRect.Contains(currentEvent.mousePosition);
        AlicizaEditorGUI.DrawListItemBackground(rowRect, selected, hovered);

        GUI.Label(new Rect(rowRect.x + 8f, rowRect.y + 3f, 22f, 20f), _settingsContent, AlicizaEditorGUI.Styles.ButtonGlyph);
        GUI.Label(new Rect(rowRect.x + 36f, rowRect.y + 4f, rowRect.width - 44f, 18f), "Database", _rowLabelStyle);

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && rowRect.Contains(currentEvent.mousePosition))
        {
            _selectedTable = SettingsIndex;
            GUI.FocusControl(string.Empty);
            currentEvent.Use();
        }
    }

    private void DrawMainPanel(Rect rect)
    {
        EditorGUI.DrawRect(rect, AlicizaEditorGUI.Colors.Body);

        GUILayout.BeginArea(new Rect(rect.x + MainPadding, rect.y + MainPadding, rect.width - MainPadding * 2f, rect.height - MainPadding * 2f));
        EditorGUILayout.BeginVertical(_panelStyle);

        if (_selectedTable == SettingsIndex)
        {
            DrawSettings();
        }
        else
        {
            _selectedTable = Mathf.Clamp(_selectedTable, 0, FixedTableNames.Length - 1);
            DrawTable(_selectedTable);
        }

        EditorGUILayout.EndVertical();
        GUILayout.EndArea();
    }

    private void DrawSettings()
    {
        DrawPanelToolbar("Database", "Shared fallback glyph data", null, false);
        EditorGUILayout.BeginVertical(_entryBodyStyle);
        DrawPropertyRow("Placeholder", _placeholderSpriteProperty);
        EditorUtils.TrHelpIconText("Used when a binding exists but no matching glyph sprite is found.", MessageType.None);

        Rect previewRect = GUILayoutUtility.GetRect(PreviewSize, PreviewSize, GUILayout.Width(PreviewSize), GUILayout.Height(PreviewSize));
        DrawSpritePreview(previewRect, _placeholderSpriteProperty.objectReferenceValue as Sprite, "None");
        EditorGUILayout.EndVertical();
    }

    private void DrawTable(int tableIndex)
    {
        SerializedProperty table = GetFixedTableProperty(tableIndex);
        SerializedProperty nameProperty = table.FindPropertyRelative(DeviceNamePropertyName);
        SerializedProperty sheetProperty = table.FindPropertyRelative(SpriteSheetPropertyName);
        SerializedProperty entriesProperty = table.FindPropertyRelative(EntriesPropertyName);

        nameProperty.stringValue = FixedTableNames[tableIndex];
        DrawPanelToolbar(FixedTableNames[tableIndex], EntryCountLabel(table), entriesProperty, true);
        DrawTableHeader(sheetProperty, entriesProperty);
        SetCurrentEntries(entriesProperty);
    }

    private void DrawPanelToolbar(string title, string subtitle, SerializedProperty entriesProperty, bool showAdd)
    {
        Rect toolbarRect = GUILayoutUtility.GetRect(1f, ToolbarHeight, GUILayout.ExpandWidth(true));
        AlicizaEditorGUI.DrawToolbarBackground(toolbarRect);

        Rect titleRect = new Rect(toolbarRect.x + 8f, toolbarRect.y + 4f, Mathf.Max(90f, toolbarRect.width - 76f), 18f);
        GUI.Label(titleRect, title, _rowLabelStyle);

        if (!string.IsNullOrEmpty(subtitle))
        {
            Rect subtitleRect = new Rect(titleRect.x + Mathf.Min(160f, titleRect.width * 0.45f), toolbarRect.y + 4f, Mathf.Max(80f, titleRect.width * 0.5f), 18f);
            GUI.Label(subtitleRect, subtitle, _mutedMiniLabelStyle);
        }

        if (!showAdd || entriesProperty == null)
        {
            return;
        }

        Rect addRect = new Rect(toolbarRect.xMax - 26f, toolbarRect.y + 5f, EntryActionButtonSize, EntryActionButtonSize);
        if (AlicizaEditorGUI.DrawToolbarButton(addRect, _addEntryContent))
        {
            AddEntry(entriesProperty);
        }
    }

    private void DrawTableHeader(SerializedProperty sheetProperty, SerializedProperty entriesProperty)
    {
        EditorGUILayout.BeginVertical(_entryBodyStyle);
        DrawSpriteSheetRow(sheetProperty, entriesProperty);
        EditorUtils.TrHelpIconText("Tables are fixed to PlayStation, Xbox, Switch, and Keyboard. Parse adds missing sprites from the sheet.", MessageType.None);
        EditorGUILayout.EndVertical();
    }

    private void DrawSpriteSheetRow(SerializedProperty sheetProperty, SerializedProperty entriesProperty)
    {
        EditorGUILayout.BeginHorizontal(_fieldRowStyle);
        EditorGUILayout.LabelField("Sprite Sheet", _fieldLabelStyle, GUILayout.Width(FieldLabelWidth));
        EditorGUILayout.PropertyField(sheetProperty, GUIContent.none);
        bool parseClicked = AlicizaEditorGUI.DrawInlineButton("Parse", ParseButtonWidth);
        EditorGUILayout.EndHorizontal();

        if (parseClicked)
        {
            ParseSpriteSheet(sheetProperty, entriesProperty);
        }
    }

    private void DrawEntryRow(Rect rowRect, SerializedProperty entriesProperty, int index, SerializedProperty entry)
    {
        Event currentEvent = Event.current;
        bool hovered = rowRect.Contains(currentEvent.mousePosition);
        AlicizaEditorGUI.DrawListItemBackground(rowRect, false, hovered);

        SerializedProperty spriteProperty = entry.FindPropertyRelative(EntrySpritePropertyName);
        SerializedProperty actionProperty = entry.FindPropertyRelative(EntryActionPropertyName);

        Rect previewRect = new Rect(rowRect.x + EntryRowPadding, rowRect.y + EntryRowPadding, PreviewSize, PreviewSize);

        float leftColumnX = rowRect.x + EntryRowPadding;
        float leftColumnWidth = EntryLeftColumnWidth;
        float actionX = leftColumnX + leftColumnWidth + EntryColumnGap;
        float actionWidth = Mathf.Max(160f, rowRect.xMax - actionX - 8f);
        float actionHeight = EditorGUI.GetPropertyHeight(actionProperty, GUIContent.none, true);
        Rect actionRect = new Rect(actionX, rowRect.y + EntryRowPadding, actionWidth, actionHeight);
        DrawSpriteObjectField(previewRect, spriteProperty);
        EditorGUI.PropertyField(actionRect, actionProperty, GUIContent.none, true);

        if (hovered)
        {
            Rect deleteRect = new Rect(previewRect.xMax - EntryOverlayButtonSize + 1f, previewRect.y - 1f, EntryOverlayButtonSize, EntryOverlayButtonSize);
            if (AlicizaEditorGUI.DrawSymbolButton(deleteRect, "-"))
            {
                DeleteEntry(entriesProperty, index);
            }
        }
    }

    private void DrawPropertyRow(string label, SerializedProperty property)
    {
        EditorGUILayout.BeginHorizontal(_fieldRowStyle);
        EditorGUILayout.LabelField(label, _fieldLabelStyle, GUILayout.Width(FieldLabelWidth));
        EditorGUILayout.PropertyField(property, GUIContent.none);
        EditorGUILayout.EndHorizontal();
    }

    private SerializedProperty GetFixedTableProperty(int fixedIndex)
    {
        string tableName = FixedTableNames[fixedIndex];
        int tableIndex = FindSerializedTableIndex(tableName);
        if (tableIndex < 0)
        {
            AddSerializedTable(tableName);
            tableIndex = FindSerializedTableIndex(tableName);
        }

        SerializedProperty table = _tablesProperty.GetArrayElementAtIndex(tableIndex);
        table.FindPropertyRelative(DeviceNamePropertyName).stringValue = tableName;
        return table;
    }

    private void EnsureSerializedDefaultTables()
    {
        for (int i = 0; i < FixedTableNames.Length; i++)
        {
            GetFixedTableProperty(i);
        }
    }

    private int FindSerializedTableIndex(string tableName)
    {
        for (int i = 0; i < _tablesProperty.arraySize; i++)
        {
            SerializedProperty table = _tablesProperty.GetArrayElementAtIndex(i);
            SerializedProperty nameProperty = table.FindPropertyRelative(DeviceNamePropertyName);
            if (string.Equals(nameProperty.stringValue, tableName, System.StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private void AddSerializedTable(string tableName)
    {
        int index = _tablesProperty.arraySize;
        _tablesProperty.InsertArrayElementAtIndex(index);
        SerializedProperty table = _tablesProperty.GetArrayElementAtIndex(index);
        table.FindPropertyRelative(DeviceNamePropertyName).stringValue = tableName;
        table.FindPropertyRelative(SpriteSheetPropertyName).objectReferenceValue = null;
        table.FindPropertyRelative(EntriesPropertyName).ClearArray();
    }

    private void ParseSpriteSheet(SerializedProperty sheetProperty, SerializedProperty entriesProperty)
    {
        Texture2D texture = sheetProperty.objectReferenceValue as Texture2D;
        if (texture == null)
        {
            EditorUtility.DisplayDialog("Parse Sprite Sheet", "Assign a Sprite Sheet texture first.", "OK");
            return;
        }

        string path = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(path))
        {
            EditorUtility.DisplayDialog("Parse Sprite Sheet", "The Sprite Sheet must be a project asset.", "OK");
            return;
        }

        Sprite[] sprites = LoadSpritesAtPath(path);
        if (sprites.Length == 0)
        {
            EditorUtility.DisplayDialog("Parse Sprite Sheet", "No Sprite sub-assets were found on this texture.", "OK");
            return;
        }

        Undo.RecordObject(_database, "Parse Input Glyph Sprite Sheet");
        int added = 0;
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null || HasEntrySprite(entriesProperty, sprite))
            {
                continue;
            }

            int index = entriesProperty.arraySize;
            entriesProperty.InsertArrayElementAtIndex(index);
            SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative(EntrySpritePropertyName).objectReferenceValue = sprite;
            ClearInputAction(entry.FindPropertyRelative(EntryActionPropertyName));
            added++;
        }

        _serializedDatabase.ApplyModifiedProperties();
        MarkDirty();
        RefreshEntryList();
        ShowNotification(new GUIContent(added == 0 ? "No missing sprites" : $"Added {added} sprites"));
    }

    private static Sprite[] LoadSpritesAtPath(string path)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        int count = 0;
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite)
            {
                count++;
            }
        }

        Sprite[] sprites = new Sprite[count];
        int spriteIndex = 0;
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
            {
                sprites[spriteIndex++] = sprite;
            }
        }

        return sprites;
    }

    private static bool HasEntrySprite(SerializedProperty entriesProperty, Sprite sprite)
    {
        for (int i = 0; i < entriesProperty.arraySize; i++)
        {
            SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(i);
            SerializedProperty spriteProperty = entry.FindPropertyRelative(EntrySpritePropertyName);
            if (spriteProperty.objectReferenceValue == sprite)
            {
                return true;
            }
        }

        return false;
    }

    private void AddEntry(SerializedProperty entriesProperty)
    {
        Undo.RecordObject(_database, "Add Input Glyph Entry");
        int index = entriesProperty.arraySize;
        entriesProperty.InsertArrayElementAtIndex(index);
        SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative(EntrySpritePropertyName).objectReferenceValue = null;
        ClearInputAction(entry.FindPropertyRelative(EntryActionPropertyName));
        MarkDirty();
        _serializedDatabase.ApplyModifiedProperties();
        RefreshEntryList();
        _entryListView?.ScrollToItem(_visibleEntryIndices.Count - 1);
    }

    private void DeleteEntry(SerializedProperty entriesProperty, int index)
    {
        if (index < 0 || index >= entriesProperty.arraySize)
        {
            return;
        }

        Undo.RecordObject(_database, "Delete Input Glyph Entry");
        entriesProperty.DeleteArrayElementAtIndex(index);
        _serializedDatabase.ApplyModifiedProperties();
        MarkDirty();
        RefreshEntryList();
        GUIUtility.ExitGUI();
    }

    private static float GetEntryRowHeight(SerializedProperty entry)
    {
        SerializedProperty spriteProperty = entry.FindPropertyRelative(EntrySpritePropertyName);
        SerializedProperty actionProperty = entry.FindPropertyRelative(EntryActionPropertyName);
        float leftColumnHeight = PreviewSize + EntryRowPadding * 2f;
        float actionHeight = EditorGUI.GetPropertyHeight(actionProperty, GUIContent.none, true)
                             + EntryRowPadding * 2f;
        float fieldsHeight = Mathf.Max(leftColumnHeight, actionHeight);
        return Mathf.Max(EntryRowMinHeight, fieldsHeight);
    }

    private bool EntryMatches(SerializedProperty entry)
    {
        if (string.IsNullOrWhiteSpace(_search))
        {
            return true;
        }

        string search = _search.Trim();
        SerializedProperty spriteProperty = entry.FindPropertyRelative(EntrySpritePropertyName);
        SerializedProperty actionProperty = entry.FindPropertyRelative(EntryActionPropertyName);
        Sprite sprite = spriteProperty.objectReferenceValue as Sprite;
        return Contains(sprite != null ? sprite.name : string.Empty, search)
               || Contains(GetActionLabel(actionProperty), search);
    }

    private static string GetActionLabel(SerializedProperty actionProperty)
    {
        if (actionProperty == null)
        {
            return string.Empty;
        }

        if (actionProperty.propertyType == SerializedPropertyType.ObjectReference)
        {
            Object actionObject = actionProperty.objectReferenceValue;
            return actionObject != null ? actionObject.name : string.Empty;
        }

        SerializedProperty nameProperty = actionProperty.FindPropertyRelative("m_Name");
        return nameProperty != null ? nameProperty.stringValue : string.Empty;
    }

    private static void ClearInputAction(SerializedProperty actionProperty)
    {
        if (actionProperty == null)
        {
            return;
        }

        if (actionProperty.propertyType == SerializedPropertyType.ObjectReference)
        {
            actionProperty.objectReferenceValue = null;
            return;
        }

        SerializedProperty nameProperty = actionProperty.FindPropertyRelative("m_Name");
        if (nameProperty != null)
        {
            nameProperty.stringValue = string.Empty;
        }

        SerializedProperty idProperty = actionProperty.FindPropertyRelative("m_Id");
        if (idProperty != null)
        {
            idProperty.stringValue = string.Empty;
        }

        SerializedProperty bindingsProperty = actionProperty.FindPropertyRelative("m_SingletonActionBindings");
        if (bindingsProperty != null && bindingsProperty.isArray)
        {
            bindingsProperty.ClearArray();
        }
    }

    private static bool Contains(string value, string search)
    {
        return !string.IsNullOrEmpty(value) && value.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string EntryCountLabel(SerializedProperty table)
    {
        SerializedProperty entries = table.FindPropertyRelative(EntriesPropertyName);
        return entries.arraySize + " glyphs";
    }

    private static string GetDeviceBadge(string tableName)
    {
        switch (tableName)
        {
            case "PlayStation":
                return "PS";
            case "Xbox":
                return "XB";
            case "Switch":
                return "SW";
            case "Keyboard":
                return "KB";
            default:
                return "--";
        }
    }

    private static void DrawSpritePreview(Rect rect, Sprite sprite, string fallback)
    {
        EditorGUI.DrawRect(rect, AlicizaEditorGUI.Colors.FieldRow);
        AlicizaEditorGUI.DrawOutline(rect);

        if (sprite == null)
        {
            GUI.Label(rect, fallback, AlicizaEditorGUI.Styles.MutedMiniLabel);
            return;
        }

        Texture2D texture = AssetPreview.GetAssetPreview(sprite) ?? AssetPreview.GetMiniThumbnail(sprite);
        if (texture != null)
        {
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
        }
    }

    private static void DrawSpriteObjectField(Rect rect, SerializedProperty spriteProperty)
    {
        Sprite sprite = spriteProperty.objectReferenceValue as Sprite;
        DrawSpritePreview(rect, sprite, "+");

        Event currentEvent = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive, rect);
        EventType eventType = currentEvent.GetTypeForControl(controlId);

        if ((eventType == EventType.DragUpdated || eventType == EventType.DragPerform) && rect.Contains(currentEvent.mousePosition))
        {
            Sprite draggedSprite = GetDraggedSprite();
            if (draggedSprite != null)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (eventType == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    spriteProperty.objectReferenceValue = draggedSprite;
                    spriteProperty.serializedObject.ApplyModifiedProperties();
                    GUI.changed = true;
                }

                currentEvent.Use();
            }
        }

        if (eventType == EventType.MouseDown && currentEvent.button == 0 && rect.Contains(currentEvent.mousePosition))
        {
            EditorGUIUtility.ShowObjectPicker<Sprite>(sprite, false, string.Empty, controlId);
            currentEvent.Use();
        }

        if (eventType == EventType.ExecuteCommand
            && currentEvent.commandName == "ObjectSelectorUpdated"
            && EditorGUIUtility.GetObjectPickerControlID() == controlId)
        {
            spriteProperty.objectReferenceValue = EditorGUIUtility.GetObjectPickerObject() as Sprite;
            spriteProperty.serializedObject.ApplyModifiedProperties();
            GUI.changed = true;
            currentEvent.Use();
        }
    }

    private static Sprite GetDraggedSprite()
    {
        Object[] draggedObjects = DragAndDrop.objectReferences;
        for (int i = 0; i < draggedObjects.Length; i++)
        {
            if (draggedObjects[i] is Sprite sprite)
            {
                return sprite;
            }
        }

        return null;
    }

    private void Save()
    {
        _serializedDatabase.ApplyModifiedProperties();
        MarkDirty();
        AssetDatabase.SaveAssetIfDirty(_database);
    }

    private void MarkDirty()
    {
        _database.EditorRefreshCache();
        EditorUtility.SetDirty(_database);
    }

    private void InitializeContents()
    {
        _saveContent ??= EditorGUIUtility.IconContent("SaveActive", "Save asset");
        _refreshContent ??= EditorGUIUtility.IconContent("Refresh", "Refresh glyph cache");
        _addEntryContent ??= EditorGUIUtility.IconContent("Toolbar Plus", "Add glyph entry");
        _settingsContent ??= EditorGUIUtility.IconContent("d__Popup", "Database settings");
    }

    private void EnsureStyles()
    {
        if (_panelStyle != null)
        {
            return;
        }

        _panelStyle = AlicizaEditorGUI.Styles.Panel;
        _entryBodyStyle = AlicizaEditorGUI.Styles.EntryBody;
        _fieldRowStyle = AlicizaEditorGUI.Styles.FieldRow;
        _rowLabelStyle = AlicizaEditorGUI.Styles.RowLabel;
        _fieldLabelStyle = AlicizaEditorGUI.Styles.FieldLabel;
        _mutedMiniLabelStyle = AlicizaEditorGUI.Styles.MutedMiniLabel;
        _emptyStateStyle = AlicizaEditorGUI.Styles.EmptyState;
        _kindBadgeStyle = AlicizaEditorGUI.Styles.KindBadge;
        _warningLabelStyle = AlicizaEditorGUI.Styles.WarningLabel;
    }
}
