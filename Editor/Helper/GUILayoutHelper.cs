using System;
using UnityEditor;
using UnityEngine;

namespace UnityEditor.DrawUtils
{
    internal class GUILayoutHelper
    {
        public static void DrawProperty(SerializedProperty property, GUISkin skin, string content)
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);

            EditorGUILayout.LabelField(new GUIContent(content), skin.FindStyle("Text"), GUILayout.Width(120));
            EditorGUILayout.PropertyField(property, new GUIContent(""));

            GUILayout.EndHorizontal();
        }

        public static void DrawProperty(SerializedProperty property, GUISkin skin, string content,
            Action<object, object> changeCallBack)
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(new GUIContent(content), skin.FindStyle("Text"), GUILayout.Width(120));

            // 保存变化前的值
            object oldValue = SerializedPropertyUtility.GetPropertyValue(property);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(property, new GUIContent(""));

            if (EditorGUI.EndChangeCheck())
            {
                // 获取变化后的值
                object newValue = SerializedPropertyUtility.GetPropertyValue(property);
                changeCallBack?.Invoke(oldValue, newValue);
            }

            GUILayout.EndHorizontal();
        }

        public static void DrawProperty<T>(SerializedProperty property, GUISkin skin, string content,
            Action<T, T> changeCallBack=null, T defaultValue = default(T))
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            EditorGUILayout.LabelField(new GUIContent(content), skin.FindStyle("Text"), GUILayout.Width(120));

            // 保存变化前的值
            T oldValue = SerializedPropertyUtility.GetPropertyValue<T>(property, defaultValue);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(property, new GUIContent(""));

            if (EditorGUI.EndChangeCheck())
            {
                // 获取变化后的值
                T newValue = SerializedPropertyUtility.GetPropertyValue<T>(property, defaultValue);
                changeCallBack?.Invoke(oldValue, newValue);
            }

            GUILayout.EndHorizontal();
        }

        public static void DrawProperty(SerializedProperty property, GUISkin skin, string content, string btnName, Action callback)
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            if (GUILayout.Button(btnName))
            {
                callback?.Invoke();
            }

            EditorGUILayout.LabelField(new GUIContent(content), skin.FindStyle("Text"), GUILayout.Width(120));
            EditorGUILayout.PropertyField(property, new GUIContent(""));

            GUILayout.EndHorizontal();
        }

        public static void DrawPropertyPlain(SerializedProperty property, GUISkin skin, string content)
        {
            GUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(new GUIContent(content), skin.FindStyle("Text"), GUILayout.Width(120));
            EditorGUILayout.PropertyField(property, new GUIContent(""));

            GUILayout.EndHorizontal();
        }

        public static void DrawPropertyCW(SerializedProperty property, GUISkin skin, string content, float width)
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);

            EditorGUILayout.LabelField(new GUIContent(content), skin.FindStyle("Text"), GUILayout.Width(width));
            EditorGUILayout.PropertyField(property, new GUIContent(""));

            GUILayout.EndHorizontal();
        }

        public static void DrawPropertyPlainCW(SerializedProperty property, GUISkin skin, string content, float width)
        {
            GUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(new GUIContent(content), skin.FindStyle("Text"), GUILayout.Width(width));
            EditorGUILayout.PropertyField(property, new GUIContent(""));

            GUILayout.EndHorizontal();
        }

        public static int DrawTabs(int tabIndex, GUIContent[] tabs, GUISkin skin)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(17);

            tabIndex = GUILayout.Toolbar(tabIndex, tabs, skin.FindStyle("Tab Indicator"));

            GUILayout.EndHorizontal();
            GUILayout.Space(-40);
            GUILayout.BeginHorizontal();
            GUILayout.Space(17);

            return tabIndex;
        }

        public static void DrawComponentHeader(GUISkin skin, string content)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Box(new GUIContent(""), skin.FindStyle(content));
            GUILayout.EndHorizontal();
            GUILayout.Space(-42);
        }

        public static void DrawHeader(GUISkin skin, string content, int space)
        {
            GUILayout.Space(space);
            GUILayout.Box(new GUIContent(""), skin.FindStyle(content));
        }

        public static bool DrawToggle(bool value, GUISkin skin, string content)
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);

            value = GUILayout.Toggle(value, new GUIContent(content), skin.FindStyle("Toggle"));
            value = GUILayout.Toggle(value, new GUIContent(""), skin.FindStyle("Toggle Helper"));

            GUILayout.EndHorizontal();
            return value;
        }

        public static bool DrawTogglePlain(bool value, GUISkin skin, string content)
        {
            GUILayout.BeginHorizontal();

            value = GUILayout.Toggle(value, new GUIContent(content), skin.FindStyle("Toggle"));
            value = GUILayout.Toggle(value, new GUIContent(""), skin.FindStyle("Toggle Helper"));

            GUILayout.EndHorizontal();
            return value;
        }
    }
}
