#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
using AlicizaX.Editor.UIToolkit;
using AlicizaX.UI;
using AlicizaX.UI.UXNavigation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace AlicizaX.UI.Editor
{
    [CustomEditor(typeof(RecyclerViewNavigationController))]
    public sealed class RecyclerViewNavigationControllerEditor : AlicizaUIEditor
    {
        private SerializedProperty _recyclerView;
        private SerializedProperty _navigationScope;
        private SerializedProperty _wrapNavigation;
        private SerializedProperty _keepFocusVisibleWhenSuppressed;
        private SerializedProperty _smoothScroll;
        private SerializedProperty _smoothScrollDuration;
        private SerializedProperty _focusAlignment;
        private SerializedProperty _exitLeft;
        private SerializedProperty _exitRight;
        private SerializedProperty _exitUp;
        private SerializedProperty _exitDown;
        private SerializedProperty _navigationMode;
        private SerializedProperty _navigationWrapAround;
        private SerializedProperty _navigationSelectOnUp;
        private SerializedProperty _navigationSelectOnDown;
        private SerializedProperty _navigationSelectOnLeft;
        private SerializedProperty _navigationSelectOnRight;
        private VisualElement _navigationDetails;

        private void OnEnable()
        {
            CacheProperties();
        }

        protected override void BuildInspector(VisualElement root)
        {
            if (_recyclerView == null)
            {
                CacheProperties();
            }

            AlicizaPanel panel = AlicizaEditorUI.CreatePanel(null);
            root.Add(panel);
            VisualElement body = panel.Body;

            AlicizaEditorUI.AddPropertyRow(
                body, "RecyclerView", _recyclerView, true, typeof(RecyclerView), false);
            AlicizaEditorUI.AddPropertyRow(
                body, "Scope", _navigationScope, true, typeof(UXNavigationScope), true);

            AddToggleRow(body, "Wrap", _wrapNavigation);
            AddToggleRow(body, "Keep Focus", _keepFocusVisibleWhenSuppressed);
            AddSmoothScrollRow(body);
            AlicizaEditorUI.AddPropertyRow(body, "Alignment", _focusAlignment);

            AlicizaEditorUI.AddPropertyRow(
                body, "Exit Left", _exitLeft, true, typeof(Selectable), true);
            AlicizaEditorUI.AddPropertyRow(
                body, "Exit Right", _exitRight, true, typeof(Selectable), true);
            AlicizaEditorUI.AddPropertyRow(
                body, "Exit Up", _exitUp, true, typeof(Selectable), true);
            AlicizaEditorUI.AddPropertyRow(
                body, "Exit Down", _exitDown, true, typeof(Selectable), true);

            Label hint = AlicizaEditorUI.CreateHint("边界方向无可导航项时跳转到指定 Selectable。");
            body.Add(hint);

            AlicizaSection navigation = AlicizaEditorUI.CreateStaticSection("Navigation");
            body.Add(navigation);

            var modeField = new AlicizaEnumField(_navigationMode);
            modeField.AddToClassList("alicia-control--grow");
            AlicizaEditorUI.AddRow(navigation.Body, "Mode", modeField);

            _navigationDetails = new VisualElement();
            _navigationDetails.AddToClassList("alicia-navigation-details");
            navigation.Body.Add(_navigationDetails);

            modeField.RegisterValueChangedCallback(_ => RebuildNavigationDetails());
            RebuildNavigationDetails();
        }

        private void CacheProperties()
        {
            _recyclerView = serializedObject.FindProperty("recyclerView");
            _navigationScope = serializedObject.FindProperty("navigationScope");
            _wrapNavigation = serializedObject.FindProperty("wrapNavigation");
            _keepFocusVisibleWhenSuppressed = serializedObject.FindProperty("keepFocusVisibleWhenSuppressed");
            _smoothScroll = serializedObject.FindProperty("smoothScroll");
            _smoothScrollDuration = serializedObject.FindProperty("smoothScrollDuration");
            _focusAlignment = serializedObject.FindProperty("focusAlignment");
            _exitLeft = serializedObject.FindProperty("exitLeft");
            _exitRight = serializedObject.FindProperty("exitRight");
            _exitUp = serializedObject.FindProperty("exitUp");
            _exitDown = serializedObject.FindProperty("exitDown");
            SerializedProperty selectableNavigation = serializedObject.FindProperty("m_Navigation");
            if (selectableNavigation == null)
            {
                return;
            }

            _navigationMode = selectableNavigation.FindPropertyRelative("m_Mode");
            _navigationWrapAround = selectableNavigation.FindPropertyRelative("m_WrapAround");
            _navigationSelectOnUp = selectableNavigation.FindPropertyRelative("m_SelectOnUp");
            _navigationSelectOnDown = selectableNavigation.FindPropertyRelative("m_SelectOnDown");
            _navigationSelectOnLeft = selectableNavigation.FindPropertyRelative("m_SelectOnLeft");
            _navigationSelectOnRight = selectableNavigation.FindPropertyRelative("m_SelectOnRight");
        }

        private static void AddToggleRow(VisualElement parent, string caption, SerializedProperty property)
        {
            if (property == null)
            {
                return;
            }

            var pill = new AlicizaPillToggle();
            pill.BindProperty(property);
            AlicizaEditorUI.AddRow(parent, caption, pill);
        }

        private void AddSmoothScrollRow(VisualElement parent)
        {
            if (_smoothScroll == null)
            {
                return;
            }

            var row = new AlicizaRow("Smooth");
            var pill = new AlicizaPillToggle();
            pill.BindProperty(_smoothScroll);

            var duration = new FloatField { label = string.Empty };
            duration.AddToClassList("alicia-control");
            duration.style.width = 90f;
            duration.style.minWidth = 90f;
            if (_smoothScrollDuration != null)
            {
                duration.BindProperty(_smoothScrollDuration);
            }

            duration.SetEnabled(_smoothScroll.boolValue);
            pill.RegisterValueChangedCallback(evt => duration.SetEnabled(evt.newValue));

            row.Content.Add(pill);
            Label spacer = AlicizaEditorUI.CreateHint("Duration");
            spacer.style.marginLeft = 10f;
            spacer.style.marginRight = 6f;
            row.Content.Add(spacer);
            row.Content.Add(duration);
            parent.Add(row);
        }

        private void RebuildNavigationDetails()
        {
            if (_navigationDetails == null || _navigationMode == null)
            {
                return;
            }

            _navigationDetails.Clear();
            int mode = _navigationMode.enumValueIndex;
            if (mode == (int)Navigation.Mode.None)
            {
                _navigationDetails.Add(AlicizaEditorUI.CreateHint("Navigation is disabled."));
                return;
            }

            AddToggleRow(_navigationDetails, "Wrap Around", _navigationWrapAround);

            if (mode != (int)Navigation.Mode.Explicit)
            {
                return;
            }

            AlicizaEditorUI.AddPropertyRow(
                _navigationDetails, "Select Up", _navigationSelectOnUp, true, typeof(Selectable), true);
            AlicizaEditorUI.AddPropertyRow(
                _navigationDetails, "Select Down", _navigationSelectOnDown, true, typeof(Selectable), true);
            AlicizaEditorUI.AddPropertyRow(
                _navigationDetails, "Select Left", _navigationSelectOnLeft, true, typeof(Selectable), true);
            AlicizaEditorUI.AddPropertyRow(
                _navigationDetails, "Select Right", _navigationSelectOnRight, true, typeof(Selectable), true);
        }
    }
}
#endif
