#if INPUTSYSTEM_SUPPORT && UX_NAVIGATION
using System.Collections.Generic;
using AlicizaX.Editor;
using AlicizaX.UI.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AlicizaX.UI.UXNavigation
{
    [CustomEditor(typeof(UXNavigationScope))]
    public sealed class UXNavigationScopeEditor : UnityEditor.Editor
    {
        private const float ToolbarHeight = 30f;
        private const float FieldRowHeight = 46f;
        private const float PolicyRowHeight = 24f;
        private const float BakedRowHeight = 22f;
        private const float BakedHeaderHeight = 22f;
        private const float BakedFooterHeight = 24f;
        private const float HeaderButtonHeight = 20f;
        private const float HeaderButtonGap = 4f;
        private const float HeaderButtonWidth = 64f;
        private const float SymbolButtonWidth = 22f;
        private const float FieldLabelWidth = 54f;

        private readonly List<Selectable> _selectableBuffer = new List<Selectable>(64);
        private int _selectedBakedIndex = -1;
        private SerializedProperty _defaultSelectable;
        private SerializedProperty _holder;
        private SerializedProperty _bakedSelectables;
        private SerializedProperty _rememberLastSelection;
        private SerializedProperty _blockLowerScopes;
        private GUIContent _refreshCurrentContent;
        private GUIContent _refreshAllContent;
        private GUIContent _validateContent;

        private void OnEnable()
        {
            _defaultSelectable = serializedObject.FindProperty("_defaultSelectable");
            _holder = serializedObject.FindProperty("_holder");
            _bakedSelectables = serializedObject.FindProperty("_bakedSelectables");
            _rememberLastSelection = serializedObject.FindProperty("_rememberLastSelection");
            _blockLowerScopes = serializedObject.FindProperty("_blockLowerScopes");
            _refreshCurrentContent = new GUIContent("Refresh", "Refresh baked data for this scope");
            _refreshAllContent = new GUIContent("All", "Refresh all UXNavigationScope components under the prefab root");
            _validateContent = new GUIContent("Validate", "Validate all UXNavigationScope components under the prefab root");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            UXNavigationScope scope = (UXNavigationScope)target;
            AutoBindHolder(scope, serializedObject, _holder);

            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginVertical(AlicizaEditorGUI.Styles.Panel);
            DrawToolbar(scope);
            DrawReferenceRow();
            DrawPolicyRows();
            DrawBakedSelectableList();
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawToolbar(UXNavigationScope scope)
        {
            Rect toolbarRect = EditorGUILayout.GetControlRect(false, ToolbarHeight);
            AlicizaEditorGUI.DrawToolbarBackground(toolbarRect);

            Rect validateRect = new Rect(toolbarRect.xMax - HeaderButtonWidth - 6f, toolbarRect.y + 5f, HeaderButtonWidth, HeaderButtonHeight);
            Rect allRect = new Rect(validateRect.x - HeaderButtonWidth - HeaderButtonGap, validateRect.y, HeaderButtonWidth, HeaderButtonHeight);
            Rect refreshRect = new Rect(allRect.x - HeaderButtonWidth - HeaderButtonGap, validateRect.y, HeaderButtonWidth, HeaderButtonHeight);
            Rect titleRect = new Rect(toolbarRect.x + 8f, toolbarRect.y + 5f, Mathf.Max(0f, refreshRect.x - toolbarRect.x - 12f), HeaderButtonHeight);

            GUI.Label(titleRect, BuildToolbarTitle(scope), AlicizaEditorGUI.Styles.RowLabel);

            if (AlicizaEditorGUI.DrawToolbarButton(refreshRect, _refreshCurrentContent))
            {
                RefreshCurrentScopeBakeData();
            }

            if (AlicizaEditorGUI.DrawToolbarButton(allRect, _refreshAllContent))
            {
                RefreshAllScopesInRoot();
            }

            if (AlicizaEditorGUI.DrawToolbarButton(validateRect, _validateContent))
            {
                ValidateAllScopesInRoot();
            }
        }

        private string BuildToolbarTitle(UXNavigationScope scope)
        {
            return $"UX Navigation Scope    baked {_bakedSelectables.arraySize}    runtime {scope.RuntimeSelectableCount}/{scope.RuntimeSelectableCapacity}";
        }

        private void DrawReferenceRow()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, FieldRowHeight);
            DrawFieldRowBackground(rect);

            Rect defaultRect = new Rect(rect.x + 6f, rect.y + 3f, rect.width - 12f, 18f);
            Rect holderRect = new Rect(defaultRect.x, defaultRect.yMax + 3f, defaultRect.width, 18f);

            DrawObjectReferenceProperty(defaultRect, "Default", _defaultSelectable, typeof(Selectable));
            DrawObjectReferenceProperty(holderRect, "Holder", _holder, typeof(UIHolderObjectBase));
        }

        private void DrawPolicyRows()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, PolicyRowHeight);
            DrawFieldRowBackground(rect);

            Rect rowRect = new Rect(rect.x + 6f, rect.y + 3f, rect.width - 12f, 18f);
            float gap = 8f;
            float toggleWidth = Mathf.Max(86f, Mathf.Floor((rowRect.width - gap) / 2f));
            DrawInlineToggle(new Rect(rowRect.x, rowRect.y, toggleWidth, rowRect.height), _rememberLastSelection, "Remember");
            DrawInlineToggle(new Rect(rowRect.x + (toggleWidth + gap), rowRect.y, toggleWidth, rowRect.height), _blockLowerScopes, "Block");
        }

        private static void DrawFieldRowBackground(Rect rect)
        {
            EditorGUI.DrawRect(rect, AlicizaEditorGUI.Colors.FieldRow);
            AlicizaEditorGUI.DrawOutline(rect);
        }

        private static void DrawObjectReferenceProperty(Rect rect, string label, SerializedProperty property, System.Type objectType)
        {
            Rect labelRect = new Rect(rect.x, rect.y, FieldLabelWidth, rect.height);
            Rect fieldRect = new Rect(labelRect.xMax + 4f, rect.y, rect.xMax - labelRect.xMax - 4f, rect.height);
            GUI.Label(labelRect, label, AlicizaEditorGUI.Styles.FieldLabel);
            property.objectReferenceValue = EditorGUI.ObjectField(fieldRect, property.objectReferenceValue, objectType, true);
        }

        private static void DrawInlineToggle(Rect rect, SerializedProperty property, string label)
        {
            property.boolValue = EditorGUI.ToggleLeft(rect, label, property.boolValue, AlicizaEditorGUI.Styles.FieldLabel);
        }

        private void DrawBakedSelectableList()
        {
            int count = _bakedSelectables.arraySize;
            if (_selectedBakedIndex >= count)
            {
                _selectedBakedIndex = count - 1;
            }

            float listHeight = BakedHeaderHeight + Mathf.Max(1, count) * BakedRowHeight + BakedFooterHeight + 8f;
            Rect listRect = EditorGUILayout.GetControlRect(false, listHeight);
            AlicizaEditorGUI.DrawBodyBackground(listRect);

            Rect contentRect = new Rect(listRect.x + 4f, listRect.y + 4f, listRect.width - 8f, listRect.height - 8f);
            Rect headerRect = new Rect(contentRect.x, contentRect.y, contentRect.width, BakedHeaderHeight);
            DrawBakedListHeader(headerRect, count);

            Rect rowsRect = new Rect(contentRect.x, headerRect.yMax, contentRect.width, Mathf.Max(1, count) * BakedRowHeight);
            if (count == 0)
            {
                Rect emptyRect = new Rect(rowsRect.x, rowsRect.y, rowsRect.width, BakedRowHeight);
                AlicizaEditorGUI.DrawListItemBackground(emptyRect, false, false);
                GUI.Label(new Rect(emptyRect.x + 8f, emptyRect.y + 2f, emptyRect.width - 16f, emptyRect.height - 4f), "No baked selectables", AlicizaEditorGUI.Styles.MutedMiniLabel);
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    Rect rowRect = new Rect(rowsRect.x, rowsRect.y + i * BakedRowHeight, rowsRect.width, BakedRowHeight);
                    DrawBakedSelectableRow(rowRect, i);
                }
            }

            DrawBakedListFooter(contentRect);
        }

        private void DrawBakedListHeader(Rect rect, int count)
        {
            AlicizaEditorGUI.DrawToolbarBackground(rect);
            Rect labelRect = new Rect(rect.x + 8f, rect.y + 2f, rect.width - 16f, rect.height - 4f);
            GUI.Label(labelRect, $"Selectables    {count}", AlicizaEditorGUI.Styles.RowLabel);
        }

        private void DrawBakedSelectableRow(Rect rowRect, int index)
        {
            Event currentEvent = Event.current;
            bool selected = _selectedBakedIndex == index;
            bool hovered = rowRect.Contains(currentEvent.mousePosition);
            AlicizaEditorGUI.DrawListItemBackground(rowRect, selected, hovered);

            SerializedProperty element = _bakedSelectables.GetArrayElementAtIndex(index);
            Rect indexRect = new Rect(rowRect.x + 6f, rowRect.y + 2f, 28f, rowRect.height - 4f);
            Rect removeRect = new Rect(rowRect.xMax - SymbolButtonWidth, rowRect.y, SymbolButtonWidth, rowRect.height);
            Rect fieldRect = new Rect(indexRect.xMax + 4f, rowRect.y + 2f, removeRect.x - indexRect.xMax - 8f, rowRect.height - 4f);
            GUI.Label(indexRect, index.ToString(), AlicizaEditorGUI.Styles.MutedMiniLabel);
            EditorGUI.PropertyField(fieldRect, element, GUIContent.none);

            if (AlicizaEditorGUI.DrawSymbolButton(removeRect, "-"))
            {
                RemoveBakedSelectableAt(index);
            }

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && rowRect.Contains(currentEvent.mousePosition))
            {
                _selectedBakedIndex = index;
                GUI.FocusControl(string.Empty);
                currentEvent.Use();
            }
        }

        private void DrawBakedListFooter(Rect contentRect)
        {
            Rect footerRect = new Rect(contentRect.x, contentRect.yMax - BakedFooterHeight, contentRect.width, BakedFooterHeight);
            AlicizaEditorGUI.DrawToolbarBackground(footerRect);
            Rect addRect = new Rect(footerRect.xMax - SymbolButtonWidth, footerRect.y + 1f, SymbolButtonWidth, footerRect.height - 2f);

            GUI.Label(new Rect(footerRect.x + 8f, footerRect.y + 2f, footerRect.width - 40f, footerRect.height - 4f), "Refresh auto-binds Holder and sorts selectables by hierarchy.", AlicizaEditorGUI.Styles.MutedMiniLabel);

            if (AlicizaEditorGUI.DrawSymbolButton(addRect, "+"))
            {
                AddBakedSelectableSlot();
            }
        }

        private void AddBakedSelectableSlot()
        {
            Undo.RecordObject(target, "Add UX Navigation Selectable Slot");
            int index = _bakedSelectables.arraySize;
            _bakedSelectables.arraySize++;
            _bakedSelectables.GetArrayElementAtIndex(index).objectReferenceValue = null;
            _selectedBakedIndex = index;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private void RemoveBakedSelectableAt(int index)
        {
            int count = _bakedSelectables.arraySize;
            if (count == 0 || index < 0 || index >= count)
            {
                return;
            }

            Undo.RecordObject(target, "Remove UX Navigation Selectable Slot");
            int oldSize = _bakedSelectables.arraySize;
            _bakedSelectables.DeleteArrayElementAtIndex(index);
            if (_bakedSelectables.arraySize == oldSize)
            {
                _bakedSelectables.DeleteArrayElementAtIndex(index);
            }

            _selectedBakedIndex = Mathf.Min(index, _bakedSelectables.arraySize - 1);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            GUIUtility.ExitGUI();
        }

        private void RefreshCurrentScopeBakeData()
        {
            UXNavigationScope scope = (UXNavigationScope)target;
            RefreshScopeBakeData(scope, serializedObject, _holder, _bakedSelectables, _selectableBuffer);
        }

        private void RefreshAllScopesInRoot()
        {
            GameObject root = GetRootGameObject(((UXNavigationScope)target).gameObject);
            UXNavigationScope[] scopes = root.GetComponentsInChildren<UXNavigationScope>(true);
            for (int i = 0; i < scopes.Length; i++)
            {
                UXNavigationScope scope = scopes[i];
                SerializedObject scopeObject = new SerializedObject(scope);
                SerializedProperty holder = scopeObject.FindProperty("_holder");
                SerializedProperty bakedSelectables = scopeObject.FindProperty("_bakedSelectables");
                RefreshScopeBakeData(scope, scopeObject, holder, bakedSelectables);
            }
        }

        private void ValidateAllScopesInRoot()
        {
            GameObject root = GetRootGameObject(((UXNavigationScope)target).gameObject);
            UXNavigationScope[] scopes = root.GetComponentsInChildren<UXNavigationScope>(true);
            int errorCount = 0;
            for (int i = 0; i < scopes.Length; i++)
            {
                if (!ValidateScope(scopes[i]))
                {
                    errorCount++;
                }
            }

            if (errorCount == 0)
            {
                Debug.Log("UXNavigationScope validation passed.", root);
            }
            else
            {
                Debug.LogErrorFormat(root, "UXNavigationScope validation failed. Error count: {0}", errorCount);
            }
        }

        private static readonly List<Selectable> StaticSelectableBuffer = new List<Selectable>(64);

        private static void RefreshScopeBakeData(UXNavigationScope scope, SerializedObject scopeObject, SerializedProperty holder, SerializedProperty bakedSelectables)
        {
            RefreshScopeBakeData(scope, scopeObject, holder, bakedSelectables, StaticSelectableBuffer);
        }

        private static void RefreshScopeBakeData(UXNavigationScope scope, SerializedObject scopeObject, SerializedProperty holder, SerializedProperty bakedSelectables, List<Selectable> scopeEditorBuffer)
        {
            AutoBindHolder(scope, scopeObject, holder);
            Selectable[] allSelectables = scope.GetComponentsInChildren<Selectable>(true);
            List<Selectable> ownedSelectables = scopeEditorBuffer;
            ownedSelectables.Clear();
            for (int i = 0; i < allSelectables.Length; i++)
            {
                Selectable selectable = allSelectables[i];
                if (selectable != null && selectable.GetComponentInParent<UXNavigationScope>(true) == scope)
                {
                    ownedSelectables.Add(selectable);
                }
            }

            ownedSelectables.Sort(CompareSiblingPath);
            Undo.RecordObject(scope, "Refresh UX Navigation Scope Bake Data");
            bakedSelectables.arraySize = ownedSelectables.Count;
            for (int i = 0; i < ownedSelectables.Count; i++)
            {
                bakedSelectables.GetArrayElementAtIndex(i).objectReferenceValue = ownedSelectables[i];
            }

            scopeObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(scope);
        }

        private static void AutoBindHolder(UXNavigationScope scope, SerializedObject scopeObject, SerializedProperty holder)
        {
            if (holder.objectReferenceValue != null)
            {
                return;
            }

            UIHolderObjectBase foundHolder = scope.GetComponent<UIHolderObjectBase>();
            if (foundHolder == null)
            {
                foundHolder = scope.GetComponentInParent<UIHolderObjectBase>(true);
            }

            if (foundHolder == null)
            {
                return;
            }

            Undo.RecordObject(scope, "Bind UX Navigation Scope Holder");
            holder.objectReferenceValue = foundHolder;
            scopeObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(scope);
        }

        private static bool ValidateScope(UXNavigationScope scope)
        {
            SerializedObject scopeObject = new SerializedObject(scope);
            SerializedProperty holderProperty = scopeObject.FindProperty("_holder");
            SerializedProperty defaultSelectableProperty = scopeObject.FindProperty("_defaultSelectable");
            SerializedProperty bakedSelectables = scopeObject.FindProperty("_bakedSelectables");
            UIHolderObjectBase holder = holderProperty.objectReferenceValue as UIHolderObjectBase;
            Selectable defaultSelectable = defaultSelectableProperty.objectReferenceValue as Selectable;
            bool valid = true;

            if (holder == null)
            {
                Debug.LogError("UXNavigationScope holder is not bound.", scope);
                valid = false;
            }
            else if (!IsSameRoot(scope.transform, holder.transform))
            {
                Debug.LogError("UXNavigationScope holder crosses root.", scope);
                valid = false;
            }

            if (defaultSelectable != null && defaultSelectable.GetComponentInParent<UXNavigationScope>(true) != scope)
            {
                Debug.LogError("UXNavigationScope default selectable crosses scope.", scope);
                valid = false;
            }

            for (int i = 0; i < bakedSelectables.arraySize; i++)
            {
                Selectable selectable = bakedSelectables.GetArrayElementAtIndex(i).objectReferenceValue as Selectable;
                if (selectable == null)
                {
                    Debug.LogWarning("UXNavigationScope baked selectables contain null entry.", scope);
                    valid = false;
                    continue;
                }

                if (selectable.GetComponentInParent<UXNavigationScope>(true) != scope)
                {
                    Debug.LogError("UXNavigationScope baked selectable crosses scope.", selectable);
                    valid = false;
                }

                for (int j = i + 1; j < bakedSelectables.arraySize; j++)
                {
                    if (bakedSelectables.GetArrayElementAtIndex(j).objectReferenceValue == selectable)
                    {
                        Debug.LogError("UXNavigationScope baked selectables contain duplicate entry.", selectable);
                        valid = false;
                    }
                }
            }

            return valid;
        }

        private static GameObject GetRootGameObject(GameObject gameObject)
        {
            Transform current = gameObject.transform;
            while (current.parent != null)
            {
                current = current.parent;
            }

            return current.gameObject;
        }

        private static bool IsSameRoot(Transform left, Transform right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return left.root == right.root;
        }

        private static int CompareSiblingPath(Selectable left, Selectable right)
        {
            if (left == right)
            {
                return 0;
            }

            Transform leftTransform = left != null ? left.transform : null;
            Transform rightTransform = right != null ? right.transform : null;
            return CompareSiblingPath(leftTransform, rightTransform);
        }

        private static int CompareSiblingPath(Transform left, Transform right)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            int leftDepth = GetDepth(left);
            int rightDepth = GetDepth(right);
            Transform leftCursor = left;
            Transform rightCursor = right;

            while (leftDepth > rightDepth)
            {
                leftCursor = leftCursor.parent;
                leftDepth--;
            }

            while (rightDepth > leftDepth)
            {
                rightCursor = rightCursor.parent;
                rightDepth--;
            }

            while (leftCursor.parent != rightCursor.parent)
            {
                leftCursor = leftCursor.parent;
                rightCursor = rightCursor.parent;
            }

            return leftCursor.GetSiblingIndex().CompareTo(rightCursor.GetSiblingIndex());
        }

        private static int GetDepth(Transform transform)
        {
            int depth = 0;
            Transform current = transform;
            while (current != null)
            {
                depth++;
                current = current.parent;
            }

            return depth;
        }

    }
}
#endif

