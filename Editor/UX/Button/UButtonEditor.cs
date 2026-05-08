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

        private SerializedProperty hoverAudioClip;
        private SerializedProperty clickAudioClip;

        protected override void OnEnable()
        {
            base.OnEnable();

            m_OnClickProperty = serializedObject.FindProperty("m_OnClick");

            hoverAudioClip = serializedObject.FindProperty("hoverAudioClip");
            clickAudioClip = serializedObject.FindProperty("clickAudioClip");

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
            EditorGUILayout.PropertyField(m_OnClickProperty);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSoundTab()
        {
            GUILayoutHelper.DrawProperty(hoverAudioClip, customSkin, "Hover Sound", "Play", () =>
            {
                if (hoverAudioClip.objectReferenceValue != null)
                {
                    PlayAudio((AudioClip)hoverAudioClip.objectReferenceValue);
                }
            });
            GUILayoutHelper.DrawProperty(clickAudioClip, customSkin, "Click Sound", "Play", () =>
            {
                if (clickAudioClip.objectReferenceValue != null)
                {
                    PlayAudio((AudioClip)clickAudioClip.objectReferenceValue);
                }
            });
        }

        private void PlayAudio(AudioClip clip)
        {
            if (clip != null)
            {
                ExtensionHelper.PreviewAudioClip(clip);
            }
        }
    }
}
