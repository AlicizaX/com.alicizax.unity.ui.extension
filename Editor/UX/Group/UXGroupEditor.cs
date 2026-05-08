using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEditor.UI
{
    [CustomEditor(typeof(UXGroup))]
    public class UXGroupInspector : Editor
    {
        private SerializedProperty m_Toggles;
        private SerializedProperty m_AllowSwitchOff;
        private SerializedProperty m_DefaultToggle;
        private UXGroup m_Target;
        private ReorderableList m_ReorderableList;

        private void OnEnable()
        {
            m_Target = (UXGroup)target;
            m_Toggles = serializedObject.FindProperty("m_Toggles");
            m_AllowSwitchOff = serializedObject.FindProperty("m_AllowSwitchOff");
            m_DefaultToggle = serializedObject.FindProperty("m_DefaultToggle");

            m_ReorderableList = new ReorderableList(serializedObject, m_Toggles, true, true, true, true)
            {
                drawHeaderCallback = DrawHeader,
                drawElementCallback = DrawElement,
                onAddCallback = OnAddList,
                onRemoveCallback = OnRemoveList,
                onChangedCallback = OnChanged
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_AllowSwitchOff);
            DrawDefaultToggleSelector();
            DrawTools();

            bool isPlaying = Application.isPlaying || EditorApplication.isPlaying;
            m_ReorderableList.draggable = !isPlaying;
            m_ReorderableList.displayAdd = !isPlaying;
            m_ReorderableList.displayRemove = !isPlaying;

            bool previousEnabled = GUI.enabled;
            GUI.enabled = !isPlaying;
            m_ReorderableList.DoLayoutList();
            GUI.enabled = previousEnabled;

            serializedObject.ApplyModifiedProperties();

            if (!Application.isPlaying)
                m_Target.EnsureValidState();
        }

        private void DrawDefaultToggleSelector()
        {
            int toggleCount = CountValidToggles();
            bool requireDefault = !m_AllowSwitchOff.boolValue && toggleCount > 0;
            int optionOffset = requireDefault ? 0 : 1;
            string[] options = new string[toggleCount + optionOffset];
            UXToggle[] toggles = new UXToggle[toggleCount];
            if (!requireDefault)
                options[0] = "<None>";

            int write = 0;
            for (int i = 0; i < m_Toggles.arraySize; i++)
            {
                UXToggle toggle = m_Toggles.GetArrayElementAtIndex(i).objectReferenceValue as UXToggle;
                if (toggle == null)
                    continue;

                toggles[write] = toggle;
                options[write + optionOffset] = "[" + i + "] " + toggle.name;
                write++;
            }

            UXToggle currentDefault = m_DefaultToggle.objectReferenceValue as UXToggle;
            int currentIndex = requireDefault ? -1 : 0;
            for (int i = 0; i < toggles.Length; i++)
            {
                if (toggles[i] == currentDefault)
                {
                    currentIndex = i + optionOffset;
                    break;
                }
            }

            if (requireDefault && currentIndex < 0)
            {
                currentDefault = GetSelectedToggle(toggles);
                if (currentDefault == null)
                    currentDefault = toggles[0];

                m_DefaultToggle.objectReferenceValue = currentDefault;
                for (int i = 0; i < toggles.Length; i++)
                {
                    if (toggles[i] == currentDefault)
                    {
                        currentIndex = i;
                        break;
                    }
                }
            }
            else if (!requireDefault && currentDefault != null && currentIndex == 0)
            {
                m_DefaultToggle.objectReferenceValue = null;
            }

            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("Default Toggle", currentIndex, options);
            if (EditorGUI.EndChangeCheck())
            {
                UXToggle newDefault = newIndex >= optionOffset ? toggles[newIndex - optionOffset] : null;
                m_DefaultToggle.objectReferenceValue = newDefault;
                serializedObject.ApplyModifiedProperties();

                if (newDefault != null)
                {
                    AssignGroup(newDefault, m_Target);
                }
            }
        }

        private static UXToggle GetSelectedToggle(UXToggle[] toggles)
        {
            for (int i = 0; i < toggles.Length; i++)
            {
                UXToggle toggle = toggles[i];
                if (toggle != null && toggle.isOn)
                    return toggle;
            }

            return null;
        }

        private void DrawTools()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Collect Children"))
            {
                CollectChildren();
            }

            if (GUILayout.Button("Clean Nulls"))
            {
                CleanNulls();
            }

            if (GUILayout.Button("Sort By Hierarchy"))
            {
                SortByHierarchy();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "Toggles", EditorStyles.boldLabel);
        }

        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty element = m_Toggles.GetArrayElementAtIndex(index);
            UXToggle oldToggle = element.objectReferenceValue as UXToggle;

            rect.y += 2;
            Rect fieldRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
            string label = oldToggle != null ? "[" + index + "] " + oldToggle.name : "[" + index + "] Null";

            bool duplicate = oldToggle != null && HasDuplicate(oldToggle, index);
            bool wrongGroup = oldToggle != null && oldToggle.group != null && oldToggle.group != m_Target;
            if (duplicate || wrongGroup)
                EditorGUI.DrawRect(fieldRect, new Color(1f, 0.55f, 0f, 0.2f));

            EditorGUI.BeginChangeCheck();
            UXToggle newToggle = EditorGUI.ObjectField(fieldRect, label, oldToggle, typeof(UXToggle), true) as UXToggle;
            if (EditorGUI.EndChangeCheck())
            {
                if (oldToggle != null && oldToggle != newToggle)
                    AssignGroup(oldToggle, null);

                if (newToggle != null && oldToggle != newToggle)
                    AssignGroup(newToggle, m_Target);

                element.objectReferenceValue = newToggle;
                serializedObject.ApplyModifiedProperties();
                m_Target.EnsureValidState();
            }
        }

        private void OnAddList(ReorderableList list)
        {
            m_Toggles.arraySize++;
            m_Toggles.GetArrayElementAtIndex(m_Toggles.arraySize - 1).objectReferenceValue = null;
            serializedObject.ApplyModifiedProperties();
        }

        private void OnRemoveList(ReorderableList list)
        {
            if (list.index < 0 || list.index >= m_Toggles.arraySize)
                return;

            UXToggle oldToggle = m_Toggles.GetArrayElementAtIndex(list.index).objectReferenceValue as UXToggle;
            if (oldToggle != null)
                AssignGroup(oldToggle, null);

            m_Toggles.DeleteArrayElementAtIndex(list.index);
            serializedObject.ApplyModifiedProperties();
            m_Target.EnsureValidState();
        }

        private void OnChanged(ReorderableList list)
        {
            serializedObject.ApplyModifiedProperties();
            if (!Application.isPlaying)
                m_Target.EnsureValidState();
        }

        private void CollectChildren()
        {
            UXToggle[] toggles = m_Target.GetComponentsInChildren<UXToggle>(true);
            m_Toggles.arraySize = toggles.Length;
            for (int i = 0; i < toggles.Length; i++)
            {
                m_Toggles.GetArrayElementAtIndex(i).objectReferenceValue = toggles[i];
                AssignGroup(toggles[i], m_Target);
            }

            serializedObject.ApplyModifiedProperties();
            m_Target.EnsureValidState();
            EditorUtility.SetDirty(m_Target);
        }

        private void CleanNulls()
        {
            for (int i = m_Toggles.arraySize - 1; i >= 0; i--)
            {
                if (m_Toggles.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    m_Toggles.DeleteArrayElementAtIndex(i);
            }

            serializedObject.ApplyModifiedProperties();
            m_Target.EnsureValidState();
            EditorUtility.SetDirty(m_Target);
        }

        private void SortByHierarchy()
        {
            int count = CountValidToggles();
            UXToggle[] toggles = new UXToggle[count];
            int write = 0;
            for (int i = 0; i < m_Toggles.arraySize; i++)
            {
                UXToggle toggle = m_Toggles.GetArrayElementAtIndex(i).objectReferenceValue as UXToggle;
                if (toggle != null)
                {
                    toggles[write] = toggle;
                    write++;
                }
            }

            System.Array.Sort(toggles, CompareHierarchyIndex);
            m_Toggles.arraySize = toggles.Length;
            for (int i = 0; i < toggles.Length; i++)
                m_Toggles.GetArrayElementAtIndex(i).objectReferenceValue = toggles[i];

            serializedObject.ApplyModifiedProperties();
            m_Target.EnsureValidState();
            EditorUtility.SetDirty(m_Target);
        }

        private int CountValidToggles()
        {
            int count = 0;
            for (int i = 0; i < m_Toggles.arraySize; i++)
            {
                if (m_Toggles.GetArrayElementAtIndex(i).objectReferenceValue != null)
                    count++;
            }

            return count;
        }

        private bool HasDuplicate(UXToggle toggle, int selfIndex)
        {
            for (int i = 0; i < m_Toggles.arraySize; i++)
            {
                if (i != selfIndex && m_Toggles.GetArrayElementAtIndex(i).objectReferenceValue == toggle)
                    return true;
            }

            return false;
        }

        private static int CompareHierarchyIndex(UXToggle left, UXToggle right)
        {
            int leftIndex = left.transform.GetSiblingIndex();
            int rightIndex = right.transform.GetSiblingIndex();
            if (leftIndex < rightIndex)
                return -1;

            if (leftIndex > rightIndex)
                return 1;

            return 0;
        }

        private static void AssignGroup(UXToggle toggle, UXGroup group)
        {
            if (toggle == null)
                return;

            SerializedObject serializedToggle = new SerializedObject(toggle);
            SerializedProperty groupProperty = serializedToggle.FindProperty("m_Group");
            groupProperty.objectReferenceValue = group;
            serializedToggle.ApplyModifiedProperties();
            toggle.group = group;
            EditorUtility.SetDirty(toggle);
        }
    }
}
