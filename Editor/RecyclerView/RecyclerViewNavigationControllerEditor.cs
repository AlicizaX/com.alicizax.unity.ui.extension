#if INPUTSYSTEM_SUPPORT && UX_NAVIGATION
using AlicizaX.Editor;
using AlicizaX.UI;
using AlicizaX.UI.UXNavigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AlicizaX.UI.Editor
{
    [CustomEditor(typeof(RecyclerViewNavigationController))]
    public sealed class RecyclerViewNavigationControllerEditor : UnityEditor.Editor
    {
        private const float ReferenceRowHeight = 46f;
        private const float ScrollRowHeight = 70f;
        private const float ExitRowHeight = 70f;
        private const float FieldLabelWidth = 92f;
        private const float NavigationRowHeight = 18f;
        private const float NavigationRowGap = 3f;
        private const float NavigationPadding = 8f;

        private SerializedProperty _recyclerView;
        private SerializedProperty _navigationScope;
        private SerializedProperty _wrapNavigation;
        private SerializedProperty _smoothScroll;
        private SerializedProperty _smoothScrollDuration;
        private SerializedProperty _focusAlignment;
        private SerializedProperty _exitLeft;
        private SerializedProperty _exitRight;
        private SerializedProperty _exitUp;
        private SerializedProperty _exitDown;
        private SerializedProperty _selectableNavigation;
        private SerializedProperty _navigationMode;
        private SerializedProperty _navigationWrapAround;
        private SerializedProperty _navigationSelectOnUp;
        private SerializedProperty _navigationSelectOnDown;
        private SerializedProperty _navigationSelectOnLeft;
        private SerializedProperty _navigationSelectOnRight;

        private void OnEnable()
        {
            _recyclerView = serializedObject.FindProperty("recyclerView");
            _navigationScope = serializedObject.FindProperty("navigationScope");
            _wrapNavigation = serializedObject.FindProperty("wrapNavigation");
            _smoothScroll = serializedObject.FindProperty("smoothScroll");
            _smoothScrollDuration = serializedObject.FindProperty("smoothScrollDuration");
            _focusAlignment = serializedObject.FindProperty("focusAlignment");
            _exitLeft = serializedObject.FindProperty("exitLeft");
            _exitRight = serializedObject.FindProperty("exitRight");
            _exitUp = serializedObject.FindProperty("exitUp");
            _exitDown = serializedObject.FindProperty("exitDown");
            _selectableNavigation = serializedObject.FindProperty("m_Navigation");
            _navigationMode = _selectableNavigation.FindPropertyRelative("m_Mode");
            _navigationWrapAround = _selectableNavigation.FindPropertyRelative("m_WrapAround");
            _navigationSelectOnUp = _selectableNavigation.FindPropertyRelative("m_SelectOnUp");
            _navigationSelectOnDown = _selectableNavigation.FindPropertyRelative("m_SelectOnDown");
            _navigationSelectOnLeft = _selectableNavigation.FindPropertyRelative("m_SelectOnLeft");
            _navigationSelectOnRight = _selectableNavigation.FindPropertyRelative("m_SelectOnRight");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginVertical(AlicizaEditorGUI.Styles.Panel);
            DrawReferenceRows();
            DrawScrollRows();
            DrawExitRows();
            DrawNavigationRow();
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawReferenceRows()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, ReferenceRowHeight);
            DrawFieldRowBackground(rect);

            Rect recyclerViewRect = new Rect(rect.x + 6f, rect.y + 3f, rect.width - 12f, 18f);
            Rect scopeRect = new Rect(recyclerViewRect.x, recyclerViewRect.yMax + 3f, recyclerViewRect.width, 18f);

            DrawObjectReferenceProperty(recyclerViewRect, "RecyclerView", _recyclerView, typeof(RecyclerView), false);
            DrawObjectReferenceProperty(scopeRect, "Scope", _navigationScope, typeof(UXNavigationScope), true);
        }

        private void DrawScrollRows()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, ScrollRowHeight);
            DrawFieldRowBackground(rect);

            Rect wrapRect = new Rect(rect.x + 6f, rect.y + 3f, rect.width - 12f, 18f);
            Rect smoothRect = new Rect(wrapRect.x, wrapRect.yMax + 3f, wrapRect.width, 18f);
            Rect alignRect = new Rect(wrapRect.x, smoothRect.yMax + 3f, wrapRect.width, 18f);

            DrawToggleProperty(wrapRect, _wrapNavigation, "Wrap");
            DrawToggleWithFloatProperty(smoothRect, _smoothScroll, "Smooth", _smoothScrollDuration, "Duration");
            DrawPropertyRow(alignRect, "Alignment", _focusAlignment);
        }

        private void DrawExitRows()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, ExitRowHeight);
            DrawFieldRowBackground(rect);

            float columnGap = 8f;
            float columnWidth = (rect.width - 12f - columnGap) * 0.5f;
            Rect leftRect = new Rect(rect.x + 6f, rect.y + 3f, columnWidth, 18f);
            Rect rightRect = new Rect(leftRect.xMax + columnGap, leftRect.y, columnWidth, 18f);
            Rect upRect = new Rect(leftRect.x, leftRect.yMax + 3f, columnWidth, 18f);
            Rect downRect = new Rect(rightRect.x, upRect.y, columnWidth, 18f);
            Rect hintRect = new Rect(leftRect.x, upRect.yMax + 3f, rect.width - 12f, 18f);

            DrawObjectReferenceProperty(leftRect, "Exit Left", _exitLeft, typeof(Selectable), true);
            DrawObjectReferenceProperty(rightRect, "Exit Right", _exitRight, typeof(Selectable), true);
            DrawObjectReferenceProperty(upRect, "Exit Up", _exitUp, typeof(Selectable), true);
            DrawObjectReferenceProperty(downRect, "Exit Down", _exitDown, typeof(Selectable), true);
            GUI.Label(hintRect, "边界方向无可导航项时跳转到指定 Selectable。", AlicizaEditorGUI.Styles.MutedMiniLabel);
        }

        private void DrawNavigationRow()
        {
            bool hasNavigation = _navigationMode.enumValueIndex != (int)Navigation.Mode.None;
            bool explicitNavigation = _navigationMode.enumValueIndex == (int)Navigation.Mode.Explicit;
            int rowCount = 1 + (hasNavigation ? 1 : 0) + (explicitNavigation ? 4 : 0);
            float totalHeight = rowCount * NavigationRowHeight + (rowCount - 1) * NavigationRowGap + NavigationPadding;
            Rect rect = EditorGUILayout.GetControlRect(false, totalHeight);
            DrawFieldRowBackground(rect);

            float y = rect.y + 4f;
            Rect fieldRect = new Rect(rect.x + 6f, y, rect.width - 12f, NavigationRowHeight);
            DrawPropertyRow(fieldRect, "Navigation", _navigationMode);

            if (!hasNavigation)
            {
                return;
            }

            y += NavigationRowHeight + NavigationRowGap;
            fieldRect.y = y;
            DrawToggleProperty(fieldRect, _navigationWrapAround, "Wrap Around");

            if (!explicitNavigation)
            {
                return;
            }

            y += NavigationRowHeight + NavigationRowGap;
            fieldRect.y = y;
            DrawObjectReferenceProperty(fieldRect, "Select Up", _navigationSelectOnUp, typeof(Selectable), true);

            y += NavigationRowHeight + NavigationRowGap;
            fieldRect.y = y;
            DrawObjectReferenceProperty(fieldRect, "Select Down", _navigationSelectOnDown, typeof(Selectable), true);

            y += NavigationRowHeight + NavigationRowGap;
            fieldRect.y = y;
            DrawObjectReferenceProperty(fieldRect, "Select Left", _navigationSelectOnLeft, typeof(Selectable), true);

            y += NavigationRowHeight + NavigationRowGap;
            fieldRect.y = y;
            DrawObjectReferenceProperty(fieldRect, "Select Right", _navigationSelectOnRight, typeof(Selectable), true);
        }

        private static void DrawFieldRowBackground(Rect rect)
        {
            EditorGUI.DrawRect(rect, AlicizaEditorGUI.Colors.FieldRow);
            AlicizaEditorGUI.DrawOutline(rect);
        }

        private static void DrawObjectReferenceProperty(Rect rect, string label, SerializedProperty property, System.Type objectType, bool allowSceneObjects)
        {
            Rect labelRect = new Rect(rect.x, rect.y, FieldLabelWidth, rect.height);
            Rect fieldRect = new Rect(labelRect.xMax + 4f, rect.y, rect.xMax - labelRect.xMax - 4f, rect.height);
            GUI.Label(labelRect, label, AlicizaEditorGUI.Styles.FieldLabel);
            property.objectReferenceValue = EditorGUI.ObjectField(fieldRect, property.objectReferenceValue, objectType, allowSceneObjects);
        }

        private static void DrawPropertyRow(Rect rect, string label, SerializedProperty property, bool includeChildren = false)
        {
            Rect labelRect = new Rect(rect.x, rect.y, FieldLabelWidth, rect.height);
            Rect fieldRect = new Rect(labelRect.xMax + 4f, rect.y, rect.xMax - labelRect.xMax - 4f, rect.height);
            GUI.Label(labelRect, label, AlicizaEditorGUI.Styles.FieldLabel);
            EditorGUI.PropertyField(fieldRect, property, GUIContent.none, includeChildren);
        }

        private static void DrawToggleProperty(Rect rect, SerializedProperty property, string label)
        {
            Rect labelRect = new Rect(rect.x, rect.y, FieldLabelWidth, rect.height);
            Rect fieldRect = new Rect(labelRect.xMax + 4f, rect.y, rect.xMax - labelRect.xMax - 4f, rect.height);
            GUI.Label(labelRect, label, AlicizaEditorGUI.Styles.FieldLabel);
            property.boolValue = EditorGUI.ToggleLeft(fieldRect, GUIContent.none, property.boolValue);
        }

        private static void DrawToggleWithFloatProperty(Rect rect, SerializedProperty toggleProperty, string toggleLabel, SerializedProperty floatProperty, string floatLabel)
        {
            Rect labelRect = new Rect(rect.x, rect.y, FieldLabelWidth, rect.height);
            Rect toggleRect = new Rect(labelRect.xMax + 4f, rect.y, 20f, rect.height);
            Rect floatLabelRect = new Rect(toggleRect.xMax + 8f, rect.y, 58f, rect.height);
            Rect floatRect = new Rect(floatLabelRect.xMax + 4f, rect.y, rect.xMax - floatLabelRect.xMax - 4f, rect.height);

            GUI.Label(labelRect, toggleLabel, AlicizaEditorGUI.Styles.FieldLabel);
            toggleProperty.boolValue = EditorGUI.Toggle(toggleRect, toggleProperty.boolValue);

            using (new EditorGUI.DisabledScope(!toggleProperty.boolValue))
            {
                GUI.Label(floatLabelRect, floatLabel, AlicizaEditorGUI.Styles.FieldLabel);
                floatProperty.floatValue = Mathf.Max(0f, EditorGUI.FloatField(floatRect, floatProperty.floatValue));
            }
        }
    }
}
#endif
