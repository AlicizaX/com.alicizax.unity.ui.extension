using UnityEngine.UI;

namespace UnityEditor.UI
{
    [CustomEditor(typeof(UXStepper), true)]
    [CanEditMultipleObjects]
    internal class UXStepperEditor : UXSelectableEditor
    {
        private SerializedProperty m_AxisProperty;
        private SerializedProperty m_OnStepProperty;

        protected override void OnEnable()
        {
            base.OnEnable();

            m_AxisProperty = serializedObject.FindProperty("m_Axis");
            m_OnStepProperty = serializedObject.FindProperty("m_OnStep");

            _tabs.AppendToTab("Image", DrawStepperTab);
            _tabs.RegisterTab("Event", "EventTrigger Icon", DrawEventTab);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _tabs.UnregisterTab("Event");
        }

        private void DrawStepperTab()
        {
            EditorGUILayout.Space();
            serializedObject.Update();
            EditorGUILayout.PropertyField(m_AxisProperty);
            EditorGUILayout.Space();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawEventTab()
        {
            EditorGUILayout.Space();
            serializedObject.Update();
            EditorGUILayout.PropertyField(m_OnStepProperty);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
