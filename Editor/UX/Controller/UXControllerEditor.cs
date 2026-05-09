#if UNITY_EDITOR
using System.Collections.Generic;
using AlicizaX.Editor;
using UnityEditor;
using UnityEngine;

namespace UnityEngine.UI
{
    [CustomEditor(typeof(UXController))]
    public sealed class UXControllerEditor : UnityEditor.Editor
    {
        private const int MinControllerLength = 2;
        private const int MaxControllerLength = 100;
        private const float RowHeight = 24f;
        private const float BodyRowHeight = 22f;
        private const float RemoveButtonWidth = 22f;
        private const float HeaderButtonSize = 22f;
        private const float ApplyButtonWidth = 78f;

        private readonly Dictionary<int, bool> _foldouts = new Dictionary<int, bool>();
        private readonly Dictionary<string, int> _pendingLengths = new Dictionary<string, int>();
        private SerializedProperty _controllersProp;

        private void OnEnable()
        {
            _controllersProp = serializedObject.FindProperty("_controllers");
        }

        private void OnDisable()
        {
            _pendingLengths.Clear();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            UXController controller = (UXController)target;

            DrawControllerList(controller);

            if (serializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(controller);
            }
        }

        private void DrawControllerList(UXController controller)
        {
            if (_controllersProp.arraySize == 0)
            {
                Rect emptyRect = EditorGUILayout.GetControlRect(false, RowHeight);
                DrawEmptyRow(emptyRect);
                return;
            }

            for (int i = 0; i < _controllersProp.arraySize; i++)
            {
                DrawController(controller, i, i == 0);
            }
        }

        private void DrawEmptyRow(Rect rect)
        {
            if (Event.current.type == EventType.Repaint)
            {
                AlicizaEditorGUI.DrawListItemBackground(rect, false, false);
            }

            Rect addRect = new Rect(rect.xMax - HeaderButtonSize, rect.y, HeaderButtonSize, rect.height);
            GUI.Label(new Rect(rect.x + 8f, rect.y + 2f, addRect.x - rect.x - 12f, rect.height - 4f), "No controllers", AlicizaEditorGUI.Styles.MutedLabel);
            if (AlicizaEditorGUI.DrawSymbolButton(addRect, "+"))
            {
                AddController();
            }
        }

        private void DrawController(UXController controller, int index, bool showAddButton)
        {
            SerializedProperty entryProp = _controllersProp.GetArrayElementAtIndex(index);
            SerializedProperty idProp = entryProp.FindPropertyRelative("_id");
            SerializedProperty nameProp = entryProp.FindPropertyRelative("_name");
            SerializedProperty lengthProp = entryProp.FindPropertyRelative("_length");
            SerializedProperty defaultIndexProp = entryProp.FindPropertyRelative("_defaultIndex");

            EnsureControllerSerializedState(idProp, lengthProp, defaultIndexProp);

            bool expanded = IsExpanded(index);
            Rect rowRect = EditorGUILayout.GetControlRect(false, RowHeight);
            DrawControllerRow(controller, rowRect, index, nameProp.stringValue, expanded, showAddButton);

            if (expanded)
            {
                DrawControllerBody(controller, index, idProp, nameProp, lengthProp, defaultIndexProp);
            }
        }

        private void DrawControllerRow(UXController controller, Rect rowRect, int index, string controllerName, bool expanded, bool showAddButton)
        {
            Event currentEvent = Event.current;
            bool hovered = rowRect.Contains(currentEvent.mousePosition);
            if (currentEvent.type == EventType.Repaint)
            {
                AlicizaEditorGUI.DrawListItemBackground(rowRect, expanded, hovered);
            }

            Rect dragRect = new Rect(rowRect.x + 8f, rowRect.y + 4f, 12f, rowRect.height - 8f);
            Rect arrowRect = new Rect(dragRect.xMax + 6f, rowRect.y + 2f, 16f, rowRect.height - 4f);
            Rect addRect = showAddButton
                ? new Rect(rowRect.xMax - HeaderButtonSize, rowRect.y, HeaderButtonSize, rowRect.height)
                : Rect.zero;
            Rect removeRect = new Rect((showAddButton ? addRect.x : rowRect.xMax) - RemoveButtonWidth, rowRect.y, RemoveButtonWidth, rowRect.height);
            Rect labelRect = new Rect(arrowRect.xMax + 4f, rowRect.y + 2f, removeRect.x - arrowRect.xMax - 8f, rowRect.height - 4f);

            GUI.Label(dragRect, "=", AlicizaEditorGUI.Styles.Glyph);
            AlicizaEditorGUI.DrawFoldoutIcon(arrowRect, expanded);
            GUI.Label(labelRect, $"[{index}] {controllerName}", AlicizaEditorGUI.Styles.RowLabel);

            if (AlicizaEditorGUI.DrawSymbolButton(removeRect, "-"))
            {
                DeleteController(controller, index);
                return;
            }

            if (showAddButton && AlicizaEditorGUI.DrawSymbolButton(addRect, "+"))
            {
                AddController();
                return;
            }

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && rowRect.Contains(currentEvent.mousePosition))
            {
                _foldouts[index] = !expanded;
                GUI.FocusControl(string.Empty);
                currentEvent.Use();
            }
        }

        private void DrawControllerBody(UXController controller, int index, SerializedProperty idProp, SerializedProperty nameProp, SerializedProperty lengthProp, SerializedProperty defaultIndexProp)
        {
            string controllerId = idProp.stringValue;
            int savedLength = Mathf.Clamp(lengthProp.intValue, MinControllerLength, MaxControllerLength);
            bool pendingApply = HasPendingLength(controllerId, savedLength);
            float bodyHeight = BodyRowHeight * (pendingApply ? 3 : 2) + 8f;
            Rect bodyRect = EditorGUILayout.GetControlRect(false, bodyHeight);
            if (Event.current.type == EventType.Repaint)
            {
                AlicizaEditorGUI.DrawBodyBackground(bodyRect);
            }

            float x = bodyRect.x + 8f;
            float y = bodyRect.y + 5f;
            float width = bodyRect.width - 16f;
            Rect nameRect = new Rect(x, y, width, BodyRowHeight);
            Rect lengthRect = new Rect(x, nameRect.yMax, width, BodyRowHeight);

            EditorGUI.BeginChangeCheck();
            DrawNameField(nameRect, nameProp);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(controller);
            }

            EditorGUI.BeginChangeCheck();
            int length = DrawLengthSlider(lengthRect, GetDisplayLength(controllerId, savedLength));
            if (EditorGUI.EndChangeCheck())
            {
                SetPendingLength(controllerId, savedLength, length);
            }

            if (pendingApply)
            {
                Rect applyRect = new Rect(bodyRect.xMax - ApplyButtonWidth - 8f, lengthRect.yMax + 2f, ApplyButtonWidth, 20f);
                if (GUI.Button(applyRect, "Apply", AlicizaEditorGUI.Styles.InlineButton))
                {
                    ApplyControllerChange(controller, index);
                }
            }
        }

        private static void DrawNameField(Rect rect, SerializedProperty nameProp)
        {
            Rect labelRect = new Rect(rect.x, rect.y + 2f, 64f, rect.height - 4f);
            Rect fieldRect = new Rect(labelRect.xMax + 8f, rect.y + 2f, rect.width - labelRect.width - 8f, rect.height - 4f);
            GUI.Label(labelRect, "Name", AlicizaEditorGUI.Styles.FieldLabel);
            nameProp.stringValue = EditorGUI.TextField(fieldRect, nameProp.stringValue);
        }

        private static int DrawLengthSlider(Rect rect, int value)
        {
            Rect labelRect = new Rect(rect.x, rect.y + 2f, 64f, rect.height - 4f);
            Rect valueRect = new Rect(rect.xMax - 46f, rect.y + 2f, 46f, rect.height - 4f);
            Rect sliderRect = new Rect(labelRect.xMax + 8f, rect.y + 3f, valueRect.x - labelRect.xMax - 14f, rect.height - 6f);
            GUI.Label(labelRect, "Length", AlicizaEditorGUI.Styles.FieldLabel);
            int nextValue = Mathf.RoundToInt(GUI.HorizontalSlider(sliderRect, Mathf.Clamp(value, MinControllerLength, MaxControllerLength), MinControllerLength, MaxControllerLength));
            nextValue = Mathf.Clamp(EditorGUI.IntField(valueRect, nextValue), MinControllerLength, MaxControllerLength);
            return nextValue;
        }

        private bool IsExpanded(int index)
        {
            return _foldouts.TryGetValue(index, out bool expanded) && expanded;
        }

        private void AddController()
        {
            int index = _controllersProp.arraySize;
            _controllersProp.InsertArrayElementAtIndex(index);

            SerializedProperty entryProp = _controllersProp.GetArrayElementAtIndex(index);
            entryProp.FindPropertyRelative("_id").stringValue = System.Guid.NewGuid().ToString("N");
            entryProp.FindPropertyRelative("_name").stringValue = $"Controller {index + 1}";
            entryProp.FindPropertyRelative("_length").intValue = MinControllerLength;
            entryProp.FindPropertyRelative("_defaultIndex").intValue = 0;
            entryProp.FindPropertyRelative("_description").stringValue = string.Empty;

            _foldouts[index] = true;
        }

        private void DeleteController(UXController controller, int index)
        {
            if (index < 0 || index >= _controllersProp.arraySize)
            {
                return;
            }

            SerializedProperty entryProp = _controllersProp.GetArrayElementAtIndex(index);
            string deletedControllerId = entryProp.FindPropertyRelative("_id").stringValue;
            int referenceCount = CountBindingReferences(controller, deletedControllerId);
            if (referenceCount > 0 && !EditorUtility.DisplayDialog(
                    "Delete UX Controller",
                    $"This controller is referenced by {referenceCount} binding rule(s). Delete and clear those references?",
                    "Delete",
                    "Cancel"))
            {
                return;
            }

            Undo.RecordObject(controller, "Delete UX Controller");
            _controllersProp.DeleteArrayElementAtIndex(index);
            CleanupFoldouts(index);
            CleanupPendingLength(deletedControllerId);
            serializedObject.ApplyModifiedProperties();
            ClearBindingReferences(controller, deletedControllerId);
            EditorUtility.SetDirty(controller);
            GUIUtility.ExitGUI();
        }

        private void ApplyControllerChange(UXController controller, int index)
        {
            if ((uint)index >= (uint)_controllersProp.arraySize)
            {
                return;
            }

            SerializedProperty entryProp = _controllersProp.GetArrayElementAtIndex(index);
            SerializedProperty idProp = entryProp.FindPropertyRelative("_id");
            SerializedProperty lengthProp = entryProp.FindPropertyRelative("_length");
            SerializedProperty defaultIndexProp = entryProp.FindPropertyRelative("_defaultIndex");
            EnsureControllerSerializedState(idProp, lengthProp, defaultIndexProp);

            string controllerId = idProp.stringValue;
            int length = GetDisplayLength(controllerId, lengthProp.intValue);
            lengthProp.intValue = length;
            defaultIndexProp.intValue = Mathf.Clamp(defaultIndexProp.intValue, 0, length - 1);
            serializedObject.ApplyModifiedProperties();
            ClampBindingReferences(controller, controllerId, length);
            CleanupPendingLength(controllerId);
            EditorUtility.SetDirty(controller);
            SceneView.RepaintAll();
        }

        private static void EnsureControllerSerializedState(SerializedProperty idProp, SerializedProperty lengthProp, SerializedProperty defaultIndexProp)
        {
            if (string.IsNullOrEmpty(idProp.stringValue))
            {
                idProp.stringValue = System.Guid.NewGuid().ToString("N");
            }

            lengthProp.intValue = Mathf.Clamp(lengthProp.intValue, MinControllerLength, MaxControllerLength);
            defaultIndexProp.intValue = Mathf.Clamp(defaultIndexProp.intValue, 0, lengthProp.intValue - 1);
        }

        private void CleanupFoldouts(int removedIndex)
        {
            _foldouts.Remove(removedIndex);
            RemapBoolDictionary(_foldouts, removedIndex);
        }

        private void CleanupPendingLength(string controllerId)
        {
            if (!string.IsNullOrEmpty(controllerId))
            {
                _pendingLengths.Remove(controllerId);
            }
        }

        private int GetDisplayLength(string controllerId, int savedLength)
        {
            savedLength = Mathf.Clamp(savedLength, MinControllerLength, MaxControllerLength);
            if (string.IsNullOrEmpty(controllerId) || !_pendingLengths.TryGetValue(controllerId, out int pendingLength))
            {
                return savedLength;
            }

            pendingLength = Mathf.Clamp(pendingLength, MinControllerLength, MaxControllerLength);
            if (pendingLength == savedLength)
            {
                _pendingLengths.Remove(controllerId);
                return savedLength;
            }

            return pendingLength;
        }

        private bool HasPendingLength(string controllerId, int savedLength)
        {
            return GetDisplayLength(controllerId, savedLength) != Mathf.Clamp(savedLength, MinControllerLength, MaxControllerLength);
        }

        private void SetPendingLength(string controllerId, int savedLength, int length)
        {
            if (string.IsNullOrEmpty(controllerId))
            {
                return;
            }

            savedLength = Mathf.Clamp(savedLength, MinControllerLength, MaxControllerLength);
            length = Mathf.Clamp(length, MinControllerLength, MaxControllerLength);
            if (length == savedLength)
            {
                _pendingLengths.Remove(controllerId);
                return;
            }

            _pendingLengths[controllerId] = length;
        }

        private static void RemapBoolDictionary(Dictionary<int, bool> dictionary, int removedIndex)
        {
            Dictionary<int, bool> remapped = new Dictionary<int, bool>();
            foreach (KeyValuePair<int, bool> pair in dictionary)
            {
                int nextIndex = pair.Key > removedIndex ? pair.Key - 1 : pair.Key;
                remapped[nextIndex] = pair.Value;
            }

            dictionary.Clear();
            foreach (KeyValuePair<int, bool> pair in remapped)
            {
                dictionary[pair.Key] = pair.Value;
            }
        }

        private static void ClearBindingReferences(UXController controller, string deletedControllerId)
        {
            if (controller == null || string.IsNullOrEmpty(deletedControllerId))
            {
                return;
            }

            IReadOnlyList<UXBinding> bindings = controller.Bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                UXBinding binding = bindings[i];
                if (binding == null)
                {
                    continue;
                }

                SerializedObject bindingObject = new SerializedObject(binding);
                SerializedProperty entriesProp = bindingObject.FindProperty("_entries");
                bool changed = false;

                for (int entryIndex = 0; entryIndex < entriesProp.arraySize; entryIndex++)
                {
                    SerializedProperty bindingEntry = entriesProp.GetArrayElementAtIndex(entryIndex);
                    SerializedProperty controllerIdProp = bindingEntry.FindPropertyRelative("_controllerId");
                    if (controllerIdProp.stringValue == deletedControllerId)
                    {
                        controllerIdProp.stringValue = string.Empty;
                        changed = true;
                    }
                }

                if (changed)
                {
                    bindingObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(binding);
                }
            }
        }

        private static void ClampBindingReferences(UXController controller, string controllerId, int length)
        {
            if (controller == null || string.IsNullOrEmpty(controllerId))
            {
                return;
            }

            length = Mathf.Clamp(length, MinControllerLength, MaxControllerLength);
            IReadOnlyList<UXBinding> bindings = controller.Bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                UXBinding binding = bindings[i];
                if (binding == null)
                {
                    continue;
                }

                SerializedObject bindingObject = new SerializedObject(binding);
                SerializedProperty entriesProp = bindingObject.FindProperty("_entries");
                bool changed = false;

                for (int entryIndex = 0; entryIndex < entriesProp.arraySize; entryIndex++)
                {
                    SerializedProperty bindingEntry = entriesProp.GetArrayElementAtIndex(entryIndex);
                    SerializedProperty controllerIdProp = bindingEntry.FindPropertyRelative("_controllerId");
                    if (controllerIdProp.stringValue != controllerId)
                    {
                        continue;
                    }

                    SerializedProperty controllerIndexProp = bindingEntry.FindPropertyRelative("_controllerIndex");
                    SerializedProperty controllerIndexMaskProp = bindingEntry.FindPropertyRelative("_controllerIndexMask");
                    int clampedIndex = Mathf.Clamp(controllerIndexProp.intValue, 0, length - 1);
                    int clampedMask = ClampMask(controllerIndexMaskProp.intValue, length, clampedIndex);
                    if (controllerIndexProp.intValue != clampedIndex || controllerIndexMaskProp.intValue != clampedMask)
                    {
                        controllerIndexProp.intValue = clampedIndex;
                        controllerIndexMaskProp.intValue = clampedMask;
                        changed = true;
                    }
                }

                if (changed)
                {
                    bindingObject.ApplyModifiedProperties();
                    EditorUtility.SetDirty(binding);
                }
            }
        }

        private static int CountBindingReferences(UXController controller, string controllerId)
        {
            if (controller == null || string.IsNullOrEmpty(controllerId))
            {
                return 0;
            }

            int count = 0;
            IReadOnlyList<UXBinding> bindings = controller.Bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                UXBinding binding = bindings[i];
                if (binding == null)
                {
                    continue;
                }

                IReadOnlyList<UXBinding.BindingEntry> entries = binding.Entries;
                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    UXBinding.BindingEntry entry = entries[entryIndex];
                    if (entry != null && entry.ControllerId == controllerId)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static int ClampMask(int mask, int length, int fallbackIndex)
        {
            int max = Mathf.Min(length, 31);
            int validMask = 0;
            for (int i = 0; i < max; i++)
            {
                validMask |= 1 << i;
            }

            mask &= validMask;
            return mask != 0 ? mask : IndexToMask(fallbackIndex);
        }

        private static int IndexToMask(int index)
        {
            if (index < 0)
            {
                return 1;
            }

            if (index >= 31)
            {
                return 1 << 30;
            }

            return 1 << index;
        }

    }
}
#endif
