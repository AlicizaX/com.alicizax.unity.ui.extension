using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.InputSystem;

[CustomEditor(typeof(InputGlyphDatabase))]
public sealed class InputGlyphDatabaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
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
    private const string TablesPropertyName = "tables";
    private const string PlaceholderSpritePropertyName = "placeholderSprite";
    private const string DeviceNamePropertyName = "deviceName";
    private const string SpriteSheetPropertyName = "spriteSheetTexture";
    private const string PlatformIconPropertyName = "platformIcons";
    private const string EntriesPropertyName = "entries";
    private const string EntrySpritePropertyName = "Sprite";
    private const string EntryActionPropertyName = "action";
    private const float SidebarWidth = 220f;
    private const float EntryMinHeight = 76f;
    private const float PreviewSize = 42f;
    private static readonly string[] DefaultTableNames = { "Keyboard", "Xbox", "PlayStation", "Other" };

    private InputGlyphDatabase _database;
    private SerializedObject _serializedDatabase;
    private SerializedProperty _tablesProperty;
    private SerializedProperty _placeholderSpriteProperty;
    private Vector2 _tableScroll;
    private Vector2 _entryScroll;
    private int _selectedTable;
    private string _search = string.Empty;
    private GUIStyle _sidebarStyle;
    private GUIStyle _selectedTableStyle;
    private GUIStyle _tableStyle;
    private GUIStyle _entryCardStyle;
    private GUIStyle _headerStyle;
    private GUIContent _addIcon;
    private GUIContent _removeIcon;
    private GUIContent _saveIcon;
    private GUIContent _refreshIcon;
    private GUIContent _searchIcon;
    private GUIContent _settingsIcon;

    internal static void OpenFromAsset(InputGlyphDatabase database)
    {
        InputGlyphDatabaseWindow window = GetWindow<InputGlyphDatabaseWindow>("Input Glyph Database", true);
        window.SetDatabase(database);
        window.minSize = new Vector2(940f, 560f);
        window.Show();
    }

    private void OnEnable()
    {
        BuildStyles();
    }

    private void SetDatabase(InputGlyphDatabase database)
    {
        _database = database;
        _serializedDatabase = new SerializedObject(_database);
        _tablesProperty = _serializedDatabase.FindProperty(TablesPropertyName);
        _placeholderSpriteProperty = _serializedDatabase.FindProperty(PlaceholderSpritePropertyName);
        _selectedTable = Mathf.Clamp(_selectedTable, 0, Mathf.Max(0, _tablesProperty.arraySize - 1));
        titleContent = new GUIContent(database.name, EditorGUIUtility.IconContent("d_ScriptableObject Icon").image);
    }

    private void OnGUI()
    {
        if (_database == null || _serializedDatabase == null)
        {
            EditorApplication.delayCall += CloseIfDatabaseMissing;
            return;
        }

        BuildStyles();
        _serializedDatabase.Update();
        DrawToolbar();

        Rect contentRect = new Rect(0f, EditorGUIUtility.singleLineHeight + 8f, position.width, position.height - EditorGUIUtility.singleLineHeight - 8f);
        Rect sidebarRect = new Rect(contentRect.x, contentRect.y, SidebarWidth, contentRect.height);
        Rect mainRect = new Rect(sidebarRect.xMax + 1f, contentRect.y, contentRect.width - SidebarWidth - 1f, contentRect.height);

        DrawSidebar(sidebarRect);
        DrawMainPanel(mainRect);

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

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button(_saveIcon, EditorStyles.toolbarButton, GUILayout.Width(28f)))
            {
                Save();
            }

            if (GUILayout.Button(_refreshIcon, EditorStyles.toolbarButton, GUILayout.Width(28f)))
            {
                _database.EditorRefreshCache();
                Repaint();
            }

            GUILayout.Space(8f);
            GUILayout.Label(_searchIcon, GUILayout.Width(20f));
            _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.Width(240f));
            if (!string.IsNullOrEmpty(_search) && GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(22f)))
            {
                _search = string.Empty;
                GUI.FocusControl(null);
            }

            GUILayout.FlexibleSpace();
            DrawObjectName();
        }
    }

    private void DrawSidebar(Rect rect)
    {
        GUI.Box(rect, GUIContent.none, _sidebarStyle);
        GUILayout.BeginArea(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f));

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Tables", _headerStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_addIcon, EditorStyles.iconButton, GUILayout.Width(24f), GUILayout.Height(22f)))
            {
                AddTable(NextTableName());
            }
        }

        _tableScroll = EditorGUILayout.BeginScrollView(_tableScroll);
        for (int i = 0; i < _tablesProperty.arraySize; i++)
        {
            SerializedProperty table = _tablesProperty.GetArrayElementAtIndex(i);
            DrawTableButton(i, table);
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(6f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(_settingsIcon, EditorStyles.iconButton, GUILayout.Width(28f), GUILayout.Height(24f)))
            {
                _selectedTable = -1;
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_addIcon, EditorStyles.iconButton, GUILayout.Width(28f), GUILayout.Height(24f)))
            {
                CreateMissingDefaultTables();
            }
        }

        GUILayout.EndArea();
    }

    private void DrawTableButton(int index, SerializedProperty table)
    {
        SerializedProperty nameProperty = table.FindPropertyRelative(DeviceNamePropertyName);
        SerializedProperty iconProperty = table.FindPropertyRelative(PlatformIconPropertyName);
        string tableName = string.IsNullOrWhiteSpace(nameProperty.stringValue) ? "Table " + (index + 1) : nameProperty.stringValue;
        bool selected = _selectedTable == index;
        GUIStyle style = selected ? _selectedTableStyle : _tableStyle;

        Rect rect = GUILayoutUtility.GetRect(1f, 44f, GUILayout.ExpandWidth(true));
        if (GUI.Button(rect, GUIContent.none, style))
        {
            _selectedTable = index;
            GUI.FocusControl(null);
        }

        Rect iconRect = new Rect(rect.x + 8f, rect.y + 6f, 32f, 32f);
        DrawSprite(iconRect, iconProperty.objectReferenceValue as Sprite);
        GUI.Label(new Rect(iconRect.xMax + 8f, rect.y + 6f, rect.width - 76f, 18f), tableName, EditorStyles.boldLabel);
        GUI.Label(new Rect(iconRect.xMax + 8f, rect.y + 24f, rect.width - 76f, 16f), EntryCountLabel(table), EditorStyles.miniLabel);

        Rect deleteRect = new Rect(rect.xMax - 28f, rect.y + 10f, 22f, 22f);
        if (GUI.Button(deleteRect, _removeIcon, EditorStyles.iconButton))
        {
            RemoveTable(index);
            GUIUtility.ExitGUI();
        }
    }

    private void DrawMainPanel(Rect rect)
    {
        GUILayout.BeginArea(new Rect(rect.x + 18f, rect.y + 14f, rect.width - 36f, rect.height - 22f));
        if (_selectedTable < 0 || _selectedTable >= _tablesProperty.arraySize)
        {
            DrawSettings();
        }
        else
        {
            DrawTable(_selectedTable);
        }

        GUILayout.EndArea();
    }

    private void DrawSettings()
    {
        GUILayout.Label("Database", _headerStyle);
        EditorGUILayout.Space(8f);
        EditorGUILayout.PropertyField(_placeholderSpriteProperty, GUIContent.none);
        Rect previewRect = GUILayoutUtility.GetRect(PreviewSize, PreviewSize, GUILayout.Width(PreviewSize), GUILayout.Height(PreviewSize));
        DrawSprite(previewRect, _placeholderSpriteProperty.objectReferenceValue as Sprite);
    }

    private void DrawTable(int tableIndex)
    {
        SerializedProperty table = _tablesProperty.GetArrayElementAtIndex(tableIndex);
        SerializedProperty nameProperty = table.FindPropertyRelative(DeviceNamePropertyName);
        SerializedProperty sheetProperty = table.FindPropertyRelative(SpriteSheetPropertyName);
        SerializedProperty iconProperty = table.FindPropertyRelative(PlatformIconPropertyName);
        SerializedProperty entriesProperty = table.FindPropertyRelative(EntriesPropertyName);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawSprite(GUILayoutUtility.GetRect(52f, 52f, GUILayout.Width(52f), GUILayout.Height(52f)), iconProperty.objectReferenceValue as Sprite);
            using (new EditorGUILayout.VerticalScope())
            {
                nameProperty.stringValue = EditorGUILayout.TextField(nameProperty.stringValue, _headerStyle);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(iconProperty, GUIContent.none, GUILayout.MinWidth(160f));
                    EditorGUILayout.PropertyField(sheetProperty, GUIContent.none, GUILayout.MinWidth(160f));
                    if (GUILayout.Button(_addIcon, EditorStyles.iconButton, GUILayout.Width(28f), GUILayout.Height(22f)))
                    {
                        AddEntry(entriesProperty);
                    }
                }
            }
        }

        EditorGUILayout.Space(12f);
        _entryScroll = EditorGUILayout.BeginScrollView(_entryScroll);
        for (int i = 0; i < entriesProperty.arraySize; i++)
        {
            SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(i);
            if (!EntryMatches(entry))
            {
                continue;
            }

            DrawEntry(entriesProperty, i, entry);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawEntry(SerializedProperty entriesProperty, int index, SerializedProperty entry)
    {
        SerializedProperty spriteProperty = entry.FindPropertyRelative(EntrySpritePropertyName);
        SerializedProperty actionProperty = entry.FindPropertyRelative(EntryActionPropertyName);
        float spriteHeight = EditorGUI.GetPropertyHeight(spriteProperty, GUIContent.none, true);
        float actionHeight = EditorGUI.GetPropertyHeight(actionProperty, GUIContent.none, true);
        float contentHeight = Mathf.Max(PreviewSize, spriteHeight + actionHeight + 8f);
        Rect rect = GUILayoutUtility.GetRect(1f, Mathf.Max(EntryMinHeight, contentHeight + 16f), GUILayout.ExpandWidth(true));
        GUI.Box(rect, GUIContent.none, _entryCardStyle);

        Rect previewRect = new Rect(rect.x + 8f, rect.y + 8f, PreviewSize, PreviewSize);
        DrawSprite(previewRect, spriteProperty.objectReferenceValue as Sprite);

        Rect removeRect = new Rect(rect.xMax - 32f, rect.y + 8f, 24f, 24f);
        float fieldX = previewRect.xMax + 12f;
        float fieldWidth = Mathf.Max(120f, removeRect.x - fieldX - 10f);
        Rect spriteRect = new Rect(fieldX, rect.y + 8f, fieldWidth, spriteHeight);
        Rect actionRect = new Rect(fieldX, spriteRect.yMax + 8f, fieldWidth, actionHeight);
        EditorGUI.PropertyField(spriteRect, spriteProperty, GUIContent.none, true);
        EditorGUI.PropertyField(actionRect, actionProperty, GUIContent.none, true);

        if (GUI.Button(removeRect, _removeIcon, EditorStyles.iconButton))
        {
            entriesProperty.DeleteArrayElementAtIndex(index);
            MarkDirty();
            GUIUtility.ExitGUI();
        }
    }

    private bool EntryMatches(SerializedProperty entry)
    {
        if (string.IsNullOrWhiteSpace(_search))
        {
            return true;
        }

        SerializedProperty spriteProperty = entry.FindPropertyRelative(EntrySpritePropertyName);
        SerializedProperty actionProperty = entry.FindPropertyRelative(EntryActionPropertyName);
        string search = _search.Trim();
        Sprite sprite = spriteProperty.objectReferenceValue as Sprite;
        Object actionObject = actionProperty.objectReferenceValue;
        return Contains(sprite != null ? sprite.name : string.Empty, search)
               || Contains(actionObject != null ? actionObject.name : string.Empty, search);
    }

    private static bool Contains(string value, string search)
    {
        return !string.IsNullOrEmpty(value) && value.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void AddEntry(SerializedProperty entriesProperty)
    {
        int index = entriesProperty.arraySize;
        entriesProperty.InsertArrayElementAtIndex(index);
        SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(index);
        entry.FindPropertyRelative(EntrySpritePropertyName).objectReferenceValue = null;
        entry.FindPropertyRelative(EntryActionPropertyName).objectReferenceValue = null;
        MarkDirty();
    }

    private void AddTable(string tableName)
    {
        int index = _tablesProperty.arraySize;
        _tablesProperty.InsertArrayElementAtIndex(index);
        SerializedProperty table = _tablesProperty.GetArrayElementAtIndex(index);
        table.FindPropertyRelative(DeviceNamePropertyName).stringValue = tableName;
        table.FindPropertyRelative(SpriteSheetPropertyName).objectReferenceValue = null;
        table.FindPropertyRelative(PlatformIconPropertyName).objectReferenceValue = null;
        table.FindPropertyRelative(EntriesPropertyName).ClearArray();
        _selectedTable = index;
        MarkDirty();
    }

    private void RemoveTable(int index)
    {
        _tablesProperty.DeleteArrayElementAtIndex(index);
        _selectedTable = Mathf.Clamp(_selectedTable, -1, _tablesProperty.arraySize - 1);
        MarkDirty();
    }

    private void CreateMissingDefaultTables()
    {
        for (int i = 0; i < DefaultTableNames.Length; i++)
        {
            if (!HasTable(DefaultTableNames[i]))
            {
                AddTable(DefaultTableNames[i]);
            }
        }
    }

    private bool HasTable(string tableName)
    {
        for (int i = 0; i < _tablesProperty.arraySize; i++)
        {
            SerializedProperty table = _tablesProperty.GetArrayElementAtIndex(i);
            if (string.Equals(table.FindPropertyRelative(DeviceNamePropertyName).stringValue, tableName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string NextTableName()
    {
        for (int i = 0; i < DefaultTableNames.Length; i++)
        {
            if (!HasTable(DefaultTableNames[i]))
            {
                return DefaultTableNames[i];
            }
        }

        return "Table " + (_tablesProperty.arraySize + 1);
    }

    private static string EntryCountLabel(SerializedProperty table)
    {
        SerializedProperty entries = table.FindPropertyRelative(EntriesPropertyName);
        return entries.arraySize + " glyphs";
    }

    private void DrawObjectName()
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField(_database, typeof(InputGlyphDatabase), false, GUILayout.Width(260f));
        }
    }

    private static void DrawSprite(Rect rect, Sprite sprite)
    {
        if (sprite == null)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            return;
        }

        Texture2D texture = AssetPreview.GetAssetPreview(sprite) ?? AssetPreview.GetMiniThumbnail(sprite);
        if (texture != null)
        {
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
        }
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

    private void BuildStyles()
    {
        _addIcon ??= EditorGUIUtility.IconContent("Toolbar Plus");
        _removeIcon ??= EditorGUIUtility.IconContent("TreeEditor.Trash");
        _saveIcon ??= EditorGUIUtility.IconContent("SaveActive");
        _refreshIcon ??= EditorGUIUtility.IconContent("Refresh");
        _searchIcon ??= EditorGUIUtility.IconContent("Search Icon");
        _settingsIcon ??= EditorGUIUtility.IconContent("d__Popup");

        _sidebarStyle ??= new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(8, 8, 8, 8)
        };

        _tableStyle ??= new GUIStyle(EditorStyles.toolbarButton)
        {
            alignment = TextAnchor.MiddleLeft,
            fixedHeight = 44f
        };

        _selectedTableStyle ??= new GUIStyle(_tableStyle)
        {
            normal = { background = Texture2D.grayTexture },
            fontStyle = FontStyle.Bold
        };

        _entryCardStyle ??= new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(8, 8, 8, 8)
        };

        _headerStyle ??= new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15
        };
    }
}
