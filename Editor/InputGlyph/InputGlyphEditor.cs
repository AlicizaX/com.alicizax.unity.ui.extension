using System;
using System.Collections.Generic;
using AlicizaX;
using AlicizaX.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

[CustomEditor(typeof(InputGlyph))]
[CanEditMultipleObjects]
public sealed class InputGlyphEditor : Editor
{
    private const float ToolbarHeight = 30f;
    private const float RowHeight = 24f;
    private const float RowLabelWidth = 132f;
    private const float RefreshButtonWidth = 142f;

    private static readonly List<InputActionAsset> CachedActionAssets = new(16);
    private static bool _actionAssetCacheDirty = true;

    private readonly List<string> _compositePartBuffer = new List<string>(8);
    private readonly List<string> _compositeOptionBuffer = new List<string>(9);
    private string[] _compositeOptionArray = Array.Empty<string>();
    private SerializedProperty _actionSourceMode;
    private SerializedProperty _actionReference;
    private SerializedProperty _hotkeyTrigger;
    private SerializedProperty _actionName;
    private SerializedProperty _compositePartName;
    private SerializedProperty _outputMode;
    private SerializedProperty _targetImage;
    private SerializedProperty _targetText;
    private SerializedProperty _categoryEvents;
    private GUIStyle _panelStyle;
    private GUIStyle _entryBodyStyle;
    private GUIStyle _fieldRowStyle;
    private GUIStyle _fieldLabelStyle;
    private GUIStyle _rowLabelStyle;
    private GUIStyle _mutedLabelStyle;
    private GUIStyle _warningLabelStyle;

    private void OnEnable()
    {
        _actionSourceMode = serializedObject.FindProperty("actionSourceMode");
        _actionReference = serializedObject.FindProperty("actionReference");
        _hotkeyTrigger = serializedObject.FindProperty("hotkeyTrigger");
        _actionName = serializedObject.FindProperty("actionName");
        _compositePartName = serializedObject.FindProperty("compositePartName");
        _outputMode = serializedObject.FindProperty("outputMode");
        _targetImage = serializedObject.FindProperty("targetImage");
        _targetText = serializedObject.FindProperty("targetText");
        _categoryEvents = serializedObject.FindProperty("categoryEvents");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EnsureStyles();

        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginVertical(_panelStyle);
        DrawToolbar("Input Glyph");
        DrawSourceSection();
        DrawOutputSection();
        DrawEventsSection();
        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
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
        _fieldLabelStyle = AlicizaEditorGUI.Styles.FieldLabel;
        _rowLabelStyle = AlicizaEditorGUI.Styles.RowLabel;
        _mutedLabelStyle = AlicizaEditorGUI.Styles.MutedLabel;
        _warningLabelStyle = AlicizaEditorGUI.Styles.WarningLabel;
    }

    private void DrawToolbar(string title)
    {
        Rect toolbarRect = GUILayoutUtility.GetRect(1f, ToolbarHeight, GUILayout.ExpandWidth(true));
        AlicizaEditorGUI.DrawToolbarBackground(toolbarRect);

        Rect labelRect = new Rect(toolbarRect.x + 8f, toolbarRect.y + 5f, toolbarRect.width - 16f, 20f);
        GUI.Label(labelRect, title, _rowLabelStyle);
    }

    private void DrawSourceSection()
    {
        InputAction resolvedAction = ResolveSelectedAction();

        DrawSectionBegin("Source");
        DrawEnumPropertyRow("Reference Mode", _actionSourceMode);
        DrawSourceFields();
        DrawResolvedActionInfo(resolvedAction);
        DrawCompositePartField(resolvedAction);
        DrawSectionEnd();
    }

    private void DrawSourceFields()
    {
        InputGlyph.ActionSourceMode mode = (InputGlyph.ActionSourceMode)_actionSourceMode.enumValueIndex;
        switch (mode)
        {
            case InputGlyph.ActionSourceMode.ActionReference:
                DrawPropertyRow("Action Reference", _actionReference);
                EditorUtils.TrHelpIconText("Use a direct InputActionReference.", MessageType.None);
                break;

            case InputGlyph.ActionSourceMode.HotkeyTrigger:
                DrawPropertyRow("Hotkey Trigger", _hotkeyTrigger);
                Component component = _hotkeyTrigger.objectReferenceValue as Component;
                if (component != null && !(component is UnityEngine.UI.IHotkeyTrigger))
                {
                    EditorUtils.TrHelpIconText("Hotkey Trigger must implement IHotkeyTrigger.", MessageType.Warning);
                }
                else
                {
                    EditorUtils.TrHelpIconText("Reads the action from an external IHotkeyTrigger component.", MessageType.None);
                }

                break;

            case InputGlyph.ActionSourceMode.ActionName:
                DrawPropertyRow("Action Name", _actionName);
                EditorGUILayout.BeginHorizontal(_fieldRowStyle);
                GUILayout.FlexibleSpace();
                if (AlicizaEditorGUI.DrawInlineButton("Refresh Action Cache", RefreshButtonWidth))
                {
                    _actionAssetCacheDirty = true;
                }

                EditorGUILayout.EndHorizontal();
                EditorUtils.TrHelpIconText("Supports ActionName or MapName/ActionName.", MessageType.None);
                break;
        }
    }

    private void DrawOutputSection()
    {
        DrawSectionBegin("Output");
        DrawEnumPropertyRow("Render Mode", _outputMode);
        DrawOutputFields();
        DrawSectionEnd();
    }

    private void DrawOutputFields()
    {
        InputGlyph.OutputMode mode = (InputGlyph.OutputMode)_outputMode.enumValueIndex;
        switch (mode)
        {
            case InputGlyph.OutputMode.Image:
                DrawPropertyRow("Target Image", _targetImage);
                EditorUtils.TrHelpIconText("Shows the resolved sprite on a Unity UI Image.", MessageType.None);
                break;

            case InputGlyph.OutputMode.Text:
                DrawPropertyRow("Target TMP Text", _targetText);
                EditorUtils.TrHelpIconText("Uses the current TMP text as a template and replaces {0}.", MessageType.None);
                TMP_Text text = _targetText.objectReferenceValue as TMP_Text;
                if (text == null)
                {
                    EditorUtils.TrHelpIconText("If TMP_Text is empty, the component tries GetComponent<TMP_Text>().", MessageType.None);
                }

                break;
        }
    }

    private void DrawEventsSection()
    {
        DrawSectionBegin("Platform Events");
        DrawPropertyBlock("Category Events", _categoryEvents);
        DrawSectionEnd();
    }

    private void DrawResolvedActionInfo(InputAction action)
    {
        if (action == null)
        {
            return;
        }

        string mapName = action.actionMap != null ? action.actionMap.name : "<No Map>";
        DrawReadOnlyRow("Resolved Action", Utility.Text.Format("{0}/{1}", mapName, action.name), _mutedLabelStyle);
    }

    private void DrawCompositePartField(InputAction action)
    {
        CollectCompositePartNames(action, _compositePartBuffer);
        if (_compositePartBuffer.Count == 0)
        {
            if (!string.IsNullOrEmpty(_compositePartName.stringValue))
            {
                _compositePartName.stringValue = string.Empty;
            }

            return;
        }

        _compositeOptionBuffer.Clear();
        _compositeOptionBuffer.Add("<None>");
        for (int i = 0; i < _compositePartBuffer.Count; i++)
        {
            _compositeOptionBuffer.Add(_compositePartBuffer[i]);
        }

        EnsureCompositeOptionArray();

        int selectedIndex = 0;
        for (int i = 0; i < _compositePartBuffer.Count; i++)
        {
            if (string.Equals(_compositePartBuffer[i], _compositePartName.stringValue, StringComparison.OrdinalIgnoreCase))
            {
                selectedIndex = i + 1;
                break;
            }
        }

        DrawPopupRow("Composite Part", selectedIndex, _compositeOptionArray, out int newIndex);
        _compositePartName.stringValue = newIndex <= 0 ? string.Empty : _compositePartBuffer[newIndex - 1];
        EditorUtils.TrHelpIconText("Shown only when the resolved action contains composite bindings.", MessageType.None);
    }

    private void DrawSectionBegin(string title)
    {
        EditorGUILayout.Space(2f);
        Rect headerRect = GUILayoutUtility.GetRect(1f, RowHeight, GUILayout.ExpandWidth(true));
        bool hovered = headerRect.Contains(Event.current.mousePosition);
        AlicizaEditorGUI.DrawListItemBackground(headerRect, true, hovered);

        Rect labelRect = new Rect(headerRect.x + 8f, headerRect.y + 3f, headerRect.width - 16f, 18f);
        GUI.Label(labelRect, title, _rowLabelStyle);
        EditorGUILayout.BeginVertical(_entryBodyStyle);
    }

    private static void DrawSectionEnd()
    {
        EditorGUILayout.EndVertical();
    }

    private void DrawEnumPropertyRow(string label, SerializedProperty property)
    {
        EditorGUILayout.BeginHorizontal(_fieldRowStyle);
        EditorGUILayout.LabelField(label, _fieldLabelStyle, GUILayout.Width(RowLabelWidth));
        Rect popupRect = GUILayoutUtility.GetRect(90f, 20f, GUILayout.MinWidth(90f), GUILayout.ExpandWidth(true));
        property.enumValueIndex = AlicizaEditorGUI.DrawStyledPopup(popupRect, property.enumValueIndex, property.enumDisplayNames);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawPopupRow(string label, int selectedIndex, string[] options, out int newIndex)
    {
        EditorGUILayout.BeginHorizontal(_fieldRowStyle);
        EditorGUILayout.LabelField(label, _fieldLabelStyle, GUILayout.Width(RowLabelWidth));
        Rect popupRect = GUILayoutUtility.GetRect(90f, 20f, GUILayout.MinWidth(90f), GUILayout.ExpandWidth(true));
        newIndex = AlicizaEditorGUI.DrawStyledPopup(popupRect, selectedIndex, options);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawPropertyRow(string label, SerializedProperty property, bool includeChildren = false)
    {
        EditorGUILayout.BeginHorizontal(_fieldRowStyle);
        EditorGUILayout.LabelField(label, _fieldLabelStyle, GUILayout.Width(RowLabelWidth));
        EditorGUILayout.PropertyField(property, GUIContent.none, includeChildren);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawPropertyBlock(string label, SerializedProperty property)
    {
        EditorGUILayout.BeginVertical(_fieldRowStyle);
        EditorGUILayout.LabelField(label, _fieldLabelStyle);
        EditorGUILayout.PropertyField(property, GUIContent.none, true);
        EditorGUILayout.EndVertical();
    }

    private void DrawReadOnlyRow(string label, string value, GUIStyle valueStyle)
    {
        EditorGUILayout.BeginHorizontal(_fieldRowStyle);
        EditorGUILayout.LabelField(label, _fieldLabelStyle, GUILayout.Width(RowLabelWidth));
        EditorGUILayout.LabelField(value, valueStyle);
        EditorGUILayout.EndHorizontal();
    }

    private InputAction ResolveSelectedAction()
    {
        InputGlyph.ActionSourceMode mode = (InputGlyph.ActionSourceMode)_actionSourceMode.enumValueIndex;
        switch (mode)
        {
            case InputGlyph.ActionSourceMode.ActionReference:
                InputActionReference actionReference = _actionReference.objectReferenceValue as InputActionReference;
                return actionReference != null ? actionReference.action : null;

            case InputGlyph.ActionSourceMode.HotkeyTrigger:
                Component component = _hotkeyTrigger.objectReferenceValue as Component;
                if (component is UnityEngine.UI.IHotkeyTrigger trigger && trigger.HotkeyAction != null)
                {
                    return trigger.HotkeyAction.action;
                }

                return null;

            case InputGlyph.ActionSourceMode.ActionName:
                return ResolveActionByName(_actionName.stringValue);

            default:
                return null;
        }
    }

    private InputAction ResolveActionByName(string actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName))
        {
            return null;
        }

        foreach (InputActionAsset asset in EnumerateInputActionAssets())
        {
            if (asset == null)
            {
                continue;
            }

            InputAction action = asset.FindAction(actionName, false);
            if (action != null)
            {
                return action;
            }
        }

        return null;
    }

    private IEnumerable<InputActionAsset> EnumerateInputActionAssets()
    {
        EnsureActionAssetCache();
        for (int i = 0; i < CachedActionAssets.Count; i++)
        {
            yield return CachedActionAssets[i];
        }
    }

    private static void EnsureActionAssetCache()
    {
        if (!_actionAssetCacheDirty)
        {
            return;
        }

        _actionAssetCacheDirty = false;
        CachedActionAssets.Clear();
        InputBindingManager[] managers = Resources.FindObjectsOfTypeAll<InputBindingManager>();
        for (int i = 0; i < managers.Length; i++)
        {
            InputActionAsset asset = managers[i] != null ? managers[i].actions : null;
            if (asset != null && !CachedActionAssets.Contains(asset))
            {
                CachedActionAssets.Add(asset);
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:InputActionAsset");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (asset != null && !CachedActionAssets.Contains(asset))
            {
                CachedActionAssets.Add(asset);
            }
        }
    }

    private static void CollectCompositePartNames(InputAction action, List<string> parts)
    {
        parts.Clear();
        if (action == null)
        {
            return;
        }

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (!binding.isPartOfComposite || string.IsNullOrWhiteSpace(binding.name))
            {
                continue;
            }

            if (!ContainsCompositePart(parts, binding.name))
            {
                parts.Add(binding.name);
            }
        }
    }

    private void EnsureCompositeOptionArray()
    {
        if (_compositeOptionArray.Length != _compositeOptionBuffer.Count)
        {
            _compositeOptionArray = new string[_compositeOptionBuffer.Count];
        }

        for (int i = 0; i < _compositeOptionBuffer.Count; i++)
        {
            _compositeOptionArray[i] = _compositeOptionBuffer[i];
        }
    }

    private static bool ContainsCompositePart(List<string> parts, string value)
    {
        for (int i = 0; i < parts.Count; i++)
        {
            if (string.Equals(parts[i], value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
