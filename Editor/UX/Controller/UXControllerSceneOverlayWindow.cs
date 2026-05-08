#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityEngine.UI
{
    public sealed class UXControllerSceneOverlayWindow : EditorWindow
    {
        private const string OverlayEnabledKey = "AlicizaX.UI.UXControllerSceneOverlay.Enabled";
        private const string MenuPath = "Window/UX/Controller Scene Overlay";
        private const float OverlayWidth = 420f;
        private const float OverlayMargin = 10f;
        private const float HeaderHeight = 24f;
        private const float RowHeight = 24f;
        private const float ButtonSize = 22f;
        private const float ControllerLabelWidth = 120f;
        private const float PreviewIndexLabelWidth = 38f;
        private const float PreviewButtonWidth = 28f;
        private const float PreviewButtonHeight = 19f;
        private const float PreviewButtonSpacing = 0f;
        private const float PreviewRowSpacing = 2f;
        private const float PreviewVerticalPadding = 4f;
        private const int MaxPreviewIndexCount = 100;

        private static bool s_overlayEnabled;
        private static Vector2 s_scrollPosition;
        private static bool s_registered;
        private static readonly List<UXController> Controllers = new List<UXController>();
        private static readonly GUIContent[] IndexContents = CreateIndexContents();

        [MenuItem(MenuPath)]
        public static void ToggleOverlay()
        {
            s_overlayEnabled = !s_overlayEnabled;
            EditorPrefs.SetBool(OverlayEnabledKey, s_overlayEnabled);
            Menu.SetChecked(MenuPath, s_overlayEnabled);
            EnsureRegistered();
            SceneView.RepaintAll();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleOverlayValidate()
        {
            Menu.SetChecked(MenuPath, s_overlayEnabled);
            return true;
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            s_overlayEnabled = EditorPrefs.GetBool(OverlayEnabledKey, false);
            Menu.SetChecked(MenuPath, s_overlayEnabled);
            EnsureRegistered();
        }

        private static void EnsureRegistered()
        {
            if (s_registered)
            {
                return;
            }

            SceneView.duringSceneGui += OnSceneGui;
            s_registered = true;
        }

        private static void OnSceneGui(SceneView sceneView)
        {
            if (!s_overlayEnabled)
            {
                return;
            }

            GetSceneControllers(Controllers);

            Handles.BeginGUI();
            float height = Mathf.Min(Mathf.Max(180f, sceneView.position.height - OverlayMargin * 2f), 520f);
            Rect area = new Rect(
                sceneView.position.width - OverlayWidth - OverlayMargin,
                OverlayMargin,
                OverlayWidth,
                height);

            GUILayout.BeginArea(area);
            DrawOverlayContent(Controllers);
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private static void DrawOverlayContent(List<UXController> controllers)
        {
            EnsureStyles();

            DrawOverlayHeader(controllers.Count);

            if (controllers.Count == 0)
            {
                DrawEmptyRow();
                return;
            }

            s_scrollPosition = EditorGUILayout.BeginScrollView(s_scrollPosition);

            for (int i = 0; i < controllers.Count; i++)
            {
                DrawControllerCard(controllers[i], i);
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawOverlayHeader(int controllerCount)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, HeaderHeight);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, Colors.Header);
                DrawOutline(rect, Colors.Border);
            }

            Rect closeRect = new Rect(rect.xMax - ButtonSize - 4f, rect.y + 2f, ButtonSize, rect.height - 4f);
            Rect countRect = new Rect(closeRect.x - 36f, rect.y + 3f, 32f, rect.height - 6f);
            Rect labelRect = new Rect(rect.x + 8f, rect.y + 3f, countRect.x - rect.x - 12f, rect.height - 6f);
            GUI.Label(labelRect, "UX Controller Preview", Styles.HeaderLabel);
            GUI.Label(countRect, controllerCount.ToString(), Styles.MutedLabel);
            if (GUI.Button(closeRect, "x", EditorStyles.miniButton))
            {
                s_overlayEnabled = false;
                EditorPrefs.SetBool(OverlayEnabledKey, s_overlayEnabled);
                Menu.SetChecked(MenuPath, false);
                SceneView.RepaintAll();
            }
        }

        private static void DrawEmptyRow()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, RowHeight);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, Colors.Row);
                DrawOutline(rect, Colors.Border);
            }

            GUI.Label(new Rect(rect.x + 8f, rect.y + 3f, rect.width - 16f, rect.height - 6f), "No UXController in loaded scenes", Styles.MutedLabel);
        }

        private static void DrawControllerCard(UXController controller, int index)
        {
            if (controller == null)
            {
                return;
            }

            DrawControllerHeader(controller, index);

            for (int controllerIndex = 0; controllerIndex < controller.ControllerCount; controllerIndex++)
            {
                UXController.ControllerDefinition definition = controller.GetControllerAt(controllerIndex);
                if (definition == null)
                {
                    continue;
                }

                DrawDefinitionPreview(controller, definition);
            }
        }

        private static void DrawControllerHeader(UXController controller, int index)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, RowHeight);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, Colors.ControllerRow);
                DrawOutline(rect, Colors.Border);
            }

            Rect labelRect = new Rect(rect.x + 8f, rect.y + 3f, rect.width - 16f, rect.height - 6f);

            GUI.Label(labelRect, $"[{index}] {controller.gameObject.name}", Styles.RowLabel);
        }

        private static void DrawDefinitionPreview(UXController controller, UXController.ControllerDefinition definition)
        {
            int length = Mathf.Clamp(definition.Length, 2, MaxPreviewIndexCount);
            int currentIndex = Mathf.Clamp(definition.SelectedIndex, 0, length - 1);
            int reservedColumns = GetPreviewButtonColumns(OverlayWidth - ControllerLabelWidth - 56f - PreviewIndexLabelWidth);
            int reservedRows = GetPreviewButtonRows(length, reservedColumns);

            Rect rect = EditorGUILayout.GetControlRect(false, GetPreviewRowHeight(reservedRows));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, Colors.Row);
                DrawOutline(rect, Colors.Border);
            }

            Rect labelRect = new Rect(rect.x + 24f, rect.y + 3f, ControllerLabelWidth, RowHeight - 6f);
            GUI.Label(labelRect, definition.Name, Styles.MutedLabel);

            Rect buttonGridRect = new Rect(
                labelRect.xMax + 8f,
                rect.y + PreviewVerticalPadding,
                rect.xMax - labelRect.xMax - 16f,
                rect.height - PreviewVerticalPadding * 2f);
            DrawPreviewButtons(controller, definition, currentIndex, length, buttonGridRect);
        }

        private static void DrawPreviewButtons(UXController controller, UXController.ControllerDefinition definition, int currentIndex, int length, Rect gridRect)
        {
            Rect indexRect = new Rect(gridRect.x, gridRect.y, PreviewIndexLabelWidth, PreviewButtonHeight);
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(indexRect, Colors.IndexTrack);
                DrawOutline(indexRect, Colors.IndexBorder);
            }

            GUI.Label(indexRect, "Index", Styles.IndexLabel);

            float buttonStartX = indexRect.xMax;
            float buttonWidth = Mathf.Max(PreviewButtonWidth, gridRect.xMax - buttonStartX);
            int columns = GetPreviewButtonColumns(buttonWidth);
            for (int i = 0; i < length; i++)
            {
                int row = i / columns;
                int column = i - row * columns;
                Rect buttonRect = new Rect(
                    buttonStartX + column * (PreviewButtonWidth + PreviewButtonSpacing),
                    gridRect.y + row * (PreviewButtonHeight + PreviewRowSpacing),
                    PreviewButtonWidth,
                    PreviewButtonHeight);
                bool selected = i == currentIndex;
                DrawIndexButtonBackground(buttonRect, selected);
                if (GUI.Button(buttonRect, GetIndexContent(i), selected ? Styles.IndexOnButton : Styles.IndexOffButton) && !selected)
                {
                    controller.SetControllerIndex(definition.Id, i);
                    EditorUtility.SetDirty(controller);
                    SceneView.RepaintAll();
                }
            }
        }

        private static int GetPreviewButtonColumns(float width)
        {
            return Mathf.Max(1, Mathf.FloorToInt((width + PreviewButtonSpacing) / (PreviewButtonWidth + PreviewButtonSpacing)));
        }

        private static int GetPreviewButtonRows(int length, int columns)
        {
            return Mathf.Max(1, (length + columns - 1) / columns);
        }

        private static float GetPreviewRowHeight(int rowCount)
        {
            return Mathf.Max(RowHeight, PreviewVerticalPadding * 2f + rowCount * PreviewButtonHeight + (rowCount - 1) * PreviewRowSpacing);
        }

        private static void DrawIndexButtonBackground(Rect rect, bool selected)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            bool hovered = rect.Contains(Event.current.mousePosition);
            Color fillColor = selected ? Colors.IndexSelected : hovered ? Colors.IndexHover : Colors.IndexButton;
            EditorGUI.DrawRect(rect, fillColor);
            DrawOutline(rect, Colors.IndexBorder);
        }

        private static GUIContent GetIndexContent(int index)
        {
            return IndexContents[index];
        }

        private static GUIContent[] CreateIndexContents()
        {
            GUIContent[] contents = new GUIContent[MaxPreviewIndexCount];
            for (int i = 0; i < contents.Length; i++)
            {
                contents[i] = new GUIContent(i.ToString());
            }

            return contents;
        }

        private static void GetSceneControllers(List<UXController> results)
        {
            results.Clear();
            UXController[] allControllers = Resources.FindObjectsOfTypeAll<UXController>();

            for (int i = 0; i < allControllers.Length; i++)
            {
                UXController controller = allControllers[i];
                if (controller == null)
                {
                    continue;
                }

                if (EditorUtility.IsPersistent(controller))
                {
                    continue;
                }

                if (!controller.gameObject.scene.IsValid() || !controller.gameObject.scene.isLoaded)
                {
                    continue;
                }

                if ((controller.hideFlags & HideFlags.HideInHierarchy) != 0)
                {
                    continue;
                }

                results.Add(controller);
            }

            results.Sort(CompareControllers);
        }

        private static int CompareControllers(UXController left, UXController right)
        {
            return string.CompareOrdinal(GetHierarchyPath(left.transform), GetHierarchyPath(right.transform));
        }

        private static void EnsureStyles()
        {
            Styles.Ensure();
        }

        private static string GetHierarchyPath(Transform target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            string path = target.name;
            Transform current = target.parent;
            while (current != null)
            {
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            return path;
        }

        private static void DrawOutline(Rect rect, Color color)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        private static class Styles
        {
            public static GUIStyle HeaderLabel;
            public static GUIStyle RowLabel;
            public static GUIStyle MutedLabel;
            public static GUIStyle IndexLabel;
            public static GUIStyle IndexOnButton;
            public static GUIStyle IndexOffButton;

            public static void Ensure()
            {
                if (HeaderLabel != null)
                {
                    return;
                }

                HeaderLabel = new GUIStyle(EditorStyles.boldLabel)
                {
                    normal = { textColor = Colors.MainText }
                };

                RowLabel = new GUIStyle(EditorStyles.label)
                {
                    normal = { textColor = Colors.MainText }
                };

                MutedLabel = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = Colors.MutedText }
                };

                IndexLabel = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Colors.MutedText },
                    hover = { textColor = Colors.MutedText },
                    active = { textColor = Colors.MutedText },
                    focused = { textColor = Colors.MutedText }
                };

                IndexOnButton = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Colors.SelectedText },
                    hover = { textColor = Colors.SelectedText },
                    active = { textColor = Colors.SelectedText },
                    focused = { textColor = Colors.SelectedText }
                };

                IndexOffButton = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Colors.MainText },
                    hover = { textColor = Colors.MainText },
                    active = { textColor = Colors.MainText },
                    focused = { textColor = Colors.MainText }
                };
            }
        }

        private static class Colors
        {
            public static readonly Color Header = new Color(0.13f, 0.13f, 0.14f, 0.96f);
            public static readonly Color ControllerRow = new Color(0.25f, 0.25f, 0.26f, 0.96f);
            public static readonly Color Row = new Color(0.21f, 0.21f, 0.22f, 0.96f);
            public static readonly Color Border = new Color(0.08f, 0.08f, 0.09f, 1f);
            public static readonly Color IndexTrack = new Color(0.18f, 0.18f, 0.18f, 1f);
            public static readonly Color IndexButton = new Color(0.25f, 0.25f, 0.25f, 1f);
            public static readonly Color IndexHover = new Color(0.31f, 0.31f, 0.31f, 1f);
            public static readonly Color IndexSelected = new Color(0.19f, 0.39f, 0.62f, 1f);
            public static readonly Color IndexBorder = new Color(0.11f, 0.11f, 0.11f, 1f);
            public static readonly Color MainText = new Color(0.86f, 0.86f, 0.86f, 1f);
            public static readonly Color MutedText = new Color(0.66f, 0.66f, 0.68f, 1f);
            public static readonly Color SelectedText = new Color(0.96f, 0.98f, 1f, 1f);
        }
    }
}
#endif
