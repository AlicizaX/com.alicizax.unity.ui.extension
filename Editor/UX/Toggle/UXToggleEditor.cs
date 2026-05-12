using UnityEditor;
using UnityEditor.DrawUtils;
using UnityEditor.Extensions;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEditor.UI
{
    [CustomEditor(typeof(UXToggle), true)]
    [CanEditMultipleObjects]
    internal class UXToggleEditor : UXSelectableEditor
    {
        private SerializedProperty m_OnValueChangedProperty;
        private SerializedProperty m_GroupProperty;
        private SerializedProperty m_IsOnProperty;

        private SerializedProperty hoverAudioClip;
        private SerializedProperty clickAudioClip;

        protected override void OnEnable()
        {
            base.OnEnable();

            m_GroupProperty = serializedObject.FindProperty("m_Group");
            m_IsOnProperty = serializedObject.FindProperty("m_IsOn");
            m_OnValueChangedProperty = serializedObject.FindProperty("onValueChanged");

            hoverAudioClip = serializedObject.FindProperty("hoverAudioClip");
            clickAudioClip = serializedObject.FindProperty("clickAudioClip");

            _tabs.AppendToTab("Image", DrawImageTab);
            _tabs.RegisterTab("Sound", "d_AudioSource Icon", DrawSoundTab);
            _tabs.RegisterTab("Event", "EventTrigger Icon", DrawEventTab);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _tabs.UnregisterTab("Sound");
            _tabs.UnregisterTab("Event");
        }

        private void DrawEventTab()
        {
            EditorGUILayout.Space();
            serializedObject.Update();
            EditorGUILayout.PropertyField(m_OnValueChangedProperty);
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSoundTab()
        {
            GUILayoutHelper.DrawProperty(hoverAudioClip, customSkin, "Hover Sound", "Play", () =>
            {
                if (hoverAudioClip.objectReferenceValue != null)
                    PlayAudio((AudioClip)hoverAudioClip.objectReferenceValue);
            });

            GUILayoutHelper.DrawProperty(clickAudioClip, customSkin, "Click Sound", "Play", () =>
            {
                if (clickAudioClip.objectReferenceValue != null)
                    PlayAudio((AudioClip)clickAudioClip.objectReferenceValue);
            });
        }

        private void PlayAudio(AudioClip clip)
        {
            if (clip != null)
                ExtensionHelper.PreviewAudioClip(clip);
        }

        private void DrawImageTab()
        {
            EditorGUILayout.Space();
            serializedObject.Update();

            UXToggle toggle = serializedObject.targetObject as UXToggle;
            DrawIsOn(toggle);
            DrawGroup(toggle);

            EditorGUILayout.Space();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawIsOn(UXToggle toggle)
        {
            EditorGUI.BeginChangeCheck();
            GUILayoutHelper.DrawProperty(m_IsOnProperty, customSkin, "Is On");
            if (!EditorGUI.EndChangeCheck())
                return;

            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(toggle.gameObject.scene);

            UXGroup group = m_GroupProperty.objectReferenceValue as UXGroup;
            bool newIsOn = m_IsOnProperty.boolValue;
            bool oldIsOn = toggle.isOn;

            if (!Application.isPlaying && group != null && !group.allowSwitchOff && oldIsOn && !newIsOn)
            {
                Debug.LogWarning("Cannot turn off the selected toggle because its group does not allow all toggles to be off.", toggle);
                m_IsOnProperty.boolValue = true;
                serializedObject.ApplyModifiedProperties();
                return;
            }

            toggle.isOn = newIsOn;
        }

        private void DrawGroup(UXToggle toggle)
        {
            EditorGUI.BeginChangeCheck();
            GUILayoutHelper.DrawProperty<UXGroup>(m_GroupProperty, customSkin, "UXGroup", OnGroupChanged);
            if (!EditorGUI.EndChangeCheck())
                return;

            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(toggle.gameObject.scene);

            UXGroup group = m_GroupProperty.objectReferenceValue as UXGroup;
            toggle.group = group;
        }

        private void OnGroupChanged(UXGroup oldValue, UXGroup newValue)
        {
            UXToggle toggle = target as UXToggle;
            if (toggle == null)
                return;

            if (oldValue != null)
                oldValue.UnregisterToggle(toggle);

            if (newValue != null)
                newValue.RegisterToggle(toggle);
        }
    }
}
