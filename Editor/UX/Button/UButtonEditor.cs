using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor.AnimatedValues;
using UnityEditor.Animations;
using UnityEditor.DrawUtils;
using UnityEditor.Extensions;
using UnityEditor.Utils;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using AnimatorControllerParameterType = UnityEngine.AnimatorControllerParameterType;

namespace UnityEditor.UI
{
    [CustomEditor(typeof(UXButton), true)]
    [CanEditMultipleObjects]
    internal class UButtonEditor : UXSelectableEditor
    {
        SerializedProperty m_OnClickProperty;

        protected override void OnEnable()
        {
            base.OnEnable();

            m_OnClickProperty = serializedObject.FindProperty("m_OnClick");

            _tabs.RegisterTab("Event", "EventTrigger Icon", DrawEventTab);
        }


        protected override void OnDisable()
        {
            base.OnDisable();
            _tabs.UnregisterTab("Event");
        }


        private void DrawEventTab()
        {
            EditorGUILayout.Space();

            serializedObject.Update();
            EditorGUILayout.PropertyField(m_OnClickProperty);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
