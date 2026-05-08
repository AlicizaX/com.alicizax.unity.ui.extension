using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SerializedClassDrawer
{
    public static void DrawSerializableProperty(SerializedProperty prop, string title = null)
    {
        if (prop == null) return;

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.EndHorizontal();

        if (!prop.hasVisibleChildren || IsEmptyClass(prop))
        {
            EditorGUILayout.PropertyField(prop, true);
        }
        else
        {
            SerializedProperty iterator = prop.Copy();
            SerializedProperty end = iterator.GetEndProperty();

            if (iterator.NextVisible(true))
            {
                while (!SerializedProperty.EqualContents(iterator, end))
                {
                    if (iterator.name == "m_Script")
                    {
                        if (!iterator.NextVisible(false)) break;
                        else continue;
                    }

                    GUIContent label = new GUIContent(ObjectNames.NicifyVariableName(iterator.displayName));

                    EditorGUILayout.PropertyField(iterator, label, true);

                    if (!iterator.NextVisible(false)) break;
                }
            }
        }
    }

    static bool IsEmptyClass(SerializedProperty prop)
    {
        SerializedProperty it = prop.Copy();
        if (!it.NextVisible(true)) return true;
        SerializedProperty end = prop.GetEndProperty();
        return SerializedProperty.EqualContents(it, end);
    }
}
