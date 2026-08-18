using AlicizaX.UI.UXFeedback;
using UnityEditor;
using UnityEngine;

namespace UnityEditor.UI
{
    [CustomEditor(typeof(UXUiAudioOverride))]
    [CanEditMultipleObjects]
    internal sealed class UXUiAudioOverrideEditor : Editor
    {
        private SerializedProperty _mode;
        private SerializedProperty _entries;

        private void OnEnable()
        {
            _mode = serializedObject.FindProperty("_mode");
            _entries = serializedObject.FindProperty("_entries");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_mode, new GUIContent("Mode"));

            UXUiAudioOverrideMode mode = (UXUiAudioOverrideMode)_mode.enumValueIndex;
            switch (mode)
            {
                case UXUiAudioOverrideMode.Silent:
                    EditorGUILayout.HelpBox("This control plays no UI audio.", MessageType.Info);
                    break;
                case UXUiAudioOverrideMode.Exclusive:
                    EditorGUILayout.HelpBox("Only listed cues play. Unlisted cues stay silent.", MessageType.Info);
                    EditorGUILayout.PropertyField(_entries, true);
                    break;
                default:
                    EditorGUILayout.HelpBox("Listed cues replace the profile. Unlisted cues still use the profile.", MessageType.Info);
                    EditorGUILayout.PropertyField(_entries, true);
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
