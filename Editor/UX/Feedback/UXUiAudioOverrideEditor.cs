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
            EditorGUILayout.PropertyField(_mode, new GUIContent("模式"));

            UXUiAudioOverrideMode mode = (UXUiAudioOverrideMode)_mode.enumValueIndex;
            switch (mode)
            {
                case UXUiAudioOverrideMode.Silent:
                    EditorGUILayout.HelpBox("此控件不播放 UI 音效。", MessageType.Info);
                    break;
                case UXUiAudioOverrideMode.Exclusive:
                    EditorGUILayout.HelpBox("仅使用下列规则；未列出的事件静音。", MessageType.Info);
                    UXUiAudioRuleEditor.Draw(_entries, true);
                    break;
                default:
                    EditorGUILayout.HelpBox("命中的规则覆盖全局主题；未命中则使用全局主题。", MessageType.Info);
                    UXUiAudioRuleEditor.Draw(_entries, true);
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
