using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor.AnimatedValues;
using UnityEditor.Animations;
using UnityEditor.DrawUtils;
using UnityEditor.Utils;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UI;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using AnimatorControllerParameterType = UnityEngine.AnimatorControllerParameterType;

namespace UnityEditor.UI
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(UXSelectable), true)]
    internal class UXSelectableEditor : SelectableEditor
    {
        private SerializedProperty m_ChildTransitions;
        private ReorderableList m_ChildTransitionList;
        public TabbedInspector _tabs;

        SerializedProperty m_ChildInteractableProperty;
        SerializedProperty m_ChildTargetGraphicProperty;
        SerializedProperty m_ChildTransitionProperty;
        SerializedProperty m_ChildColorBlockProperty;
        SerializedProperty m_ChildSpriteStateProperty;
        SerializedProperty m_ChildAnimTriggerProperty;
        SerializedProperty m_ChildNavigationProperty;

        AnimBool m_ChildShowColorTint = new AnimBool();
        AnimBool m_ChildShowSpriteTrasition = new AnimBool();
        AnimBool m_ChildShowAnimTransition = new AnimBool();

        protected GUISkin customSkin;

        GUIContent m_ChildVisualizeNavigation = EditorGUIUtility.TrTextContent("Visualize", "Show navigation flows between selectable UI elements.");
        private static bool s_ChildShowNavigation = false;
        private static string s_ChildShowNavigationKey = "SelectableEditor.ShowNavigation";

        private static Color darkZebraEven = new Color(0.22f, 0.22f, 0.22f);
        private static Color darkZebraOdd = new Color(0.27f, 0.27f, 0.27f);

        protected override void OnEnable()
        {
            base.OnEnable();

            m_ChildInteractableProperty = serializedObject.FindProperty("m_Interactable");
            m_ChildTargetGraphicProperty = serializedObject.FindProperty("m_TargetGraphic");
            m_ChildTransitionProperty = serializedObject.FindProperty("m_Transition");
            m_ChildColorBlockProperty = serializedObject.FindProperty("m_Colors");
            m_ChildSpriteStateProperty = serializedObject.FindProperty("m_SpriteState");
            m_ChildAnimTriggerProperty = serializedObject.FindProperty("m_AnimationTriggers");
            m_ChildNavigationProperty = serializedObject.FindProperty("m_Navigation");
            m_ChildTransitions = serializedObject.FindProperty("m_ChildTransitions");

            CreateChildTransitionList();

            m_ChildTransitions = serializedObject.FindProperty("m_ChildTransitions");

            var trans = GetTransition(m_ChildTransitionProperty);
            m_ChildShowColorTint.value = (trans == Selectable.Transition.ColorTint);
            m_ChildShowSpriteTrasition.value = (trans == Selectable.Transition.SpriteSwap);
            m_ChildShowAnimTransition.value = (trans == Selectable.Transition.Animation);

            m_ChildShowColorTint.valueChanged.AddListener(Repaint);
            m_ChildShowSpriteTrasition.valueChanged.AddListener(Repaint);

            _tabs = new TabbedInspector(typeof(UXSelectable).FullName + ".TabbedIndex");
            _tabs.EnsureDefaultTab("Image", "d_Texture Icon", DrawBaseButtonInspector);

            customSkin = AssetDatabase.LoadAssetAtPath<GUISkin>("Packages/com.alicizax.unity.ui.extension/Editor/Res/GUISkin/UIExtensionGUISkin.guiskin");
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            m_ChildShowColorTint.valueChanged.RemoveListener(Repaint);
            m_ChildShowSpriteTrasition.valueChanged.RemoveListener(Repaint);
            _tabs.UnregisterTab("Image");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            _tabs.DrawTabs();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawBaseButtonInspector()
        {
            serializedObject.Update();

            var interactable = GUILayoutHelper.DrawToggle(m_ChildInteractableProperty.boolValue, customSkin, "Interactable");
            if (interactable != m_ChildInteractableProperty.boolValue)
            {
                m_ChildInteractableProperty.boolValue = interactable;
            }


            EditorGUILayout.PropertyField(m_ChildNavigationProperty);

            EditorGUI.BeginChangeCheck();
            Rect toggleRect = EditorGUILayout.GetControlRect();
            toggleRect.xMin += EditorGUIUtility.labelWidth;
            s_ChildShowNavigation = GUI.Toggle(toggleRect, s_ChildShowNavigation, m_ChildVisualizeNavigation, EditorStyles.miniButton);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(s_ChildShowNavigationKey, s_ChildShowNavigation);
                SceneView.RepaintAll();
            }

            GUILayout.Space(1);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Main Transition", EditorStyles.boldLabel);
            var trans = GetTransition(m_ChildTransitionProperty);

            var graphic = m_ChildTargetGraphicProperty.objectReferenceValue as Graphic;
            if (graphic == null)
                graphic = (target as Selectable).GetComponent<Graphic>();

            var animator = (target as Selectable).GetComponent<Animator>();
            m_ChildShowColorTint.target = (!m_ChildTransitionProperty.hasMultipleDifferentValues && trans == Button.Transition.ColorTint);
            m_ChildShowSpriteTrasition.target = (!m_ChildTransitionProperty.hasMultipleDifferentValues && trans == Button.Transition.SpriteSwap);
            m_ChildShowAnimTransition.target = (!m_ChildTransitionProperty.hasMultipleDifferentValues && trans == Button.Transition.Animation);

            EditorGUILayout.PropertyField(m_ChildTransitionProperty);


            {
                if (trans == Selectable.Transition.ColorTint || trans == Selectable.Transition.SpriteSwap)
                {
                    EditorGUILayout.PropertyField(m_ChildTargetGraphicProperty);
                }

                switch (trans)
                {
                    case Selectable.Transition.ColorTint:
                        if (graphic == null)
                            EditorGUILayout.HelpBox("You must have a Graphic target in order to use a color transition.", MessageType.Warning);
                        break;

                    case Selectable.Transition.SpriteSwap:
                        if (graphic as Image == null)
                            EditorGUILayout.HelpBox("You must have a Image target in order to use a sprite swap transition.", MessageType.Warning);
                        break;
                }

                if (EditorGUILayout.BeginFadeGroup(m_ChildShowColorTint.faded))
                {
                    EditorGUILayout.PropertyField(m_ChildColorBlockProperty);
                }

                EditorGUILayout.EndFadeGroup();

                if (EditorGUILayout.BeginFadeGroup(m_ChildShowSpriteTrasition.faded))
                {
                    EditorGUILayout.PropertyField(m_ChildSpriteStateProperty);
                }

                EditorGUILayout.EndFadeGroup();

                if (EditorGUILayout.BeginFadeGroup(m_ChildShowAnimTransition.faded))
                {
                    EditorGUILayout.PropertyField(m_ChildAnimTriggerProperty);

                    if (animator == null || animator.runtimeAnimatorController == null)
                    {
                        Rect buttonRect = EditorGUILayout.GetControlRect();
                        buttonRect.xMin += EditorGUIUtility.labelWidth;
                        if (GUI.Button(buttonRect, "Auto Generate Animation", EditorStyles.miniButton))
                        {
                            var controller = GenerateSelectableAnimatorContoller((target as Selectable).animationTriggers, target as Selectable);
                            if (controller != null)
                            {
                                if (animator == null)
                                    animator = (target as Selectable).gameObject.AddComponent<Animator>();

                                Animations.AnimatorController.SetAnimatorController(animator, controller);
                            }
                        }
                    }
                }

                EditorGUILayout.EndFadeGroup();
            }


            EditorGUILayout.Space();

            GUILayout.EndVertical();
            GUILayout.Space(5);

            m_ChildTransitionList.DoLayoutList();

            GUILayout.Space(1);
            serializedObject.ApplyModifiedProperties();
        }

        #region ChildTransition

        private void CreateChildTransitionList()
        {
            m_ChildTransitionList = new ReorderableList(serializedObject, m_ChildTransitions, true, false, true, true);

            m_ChildTransitionList.drawElementBackgroundCallback = (rect, index, isActive, isFocused) =>
            {
                var background = index % 2 == 0 ? darkZebraEven : darkZebraOdd;
                EditorGUI.DrawRect(rect, background);
            };

            m_ChildTransitionList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var element = m_ChildTransitionList.serializedProperty.GetArrayElementAtIndex(index);
                rect.y += 2;

                string elementTitle = $"Null Transition";
                var targetProp = element.FindPropertyRelative("targetGraphic");
                if (targetProp.objectReferenceValue != null)
                    elementTitle = targetProp.objectReferenceValue.name;

                EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    elementTitle, EditorStyles.boldLabel);

                rect.y += EditorGUIUtility.singleLineHeight + 2;
                DrawTransitionData(new Rect(rect.x, rect.y, rect.width, 0), element);
            };

            m_ChildTransitionList.elementHeightCallback = (index) =>
            {
                return EditorGUIUtility.singleLineHeight +
                       CalculateTransitionDataHeight(m_ChildTransitionList.serializedProperty.GetArrayElementAtIndex(index)) +
                       10;
            };

            m_ChildTransitionList.onAddCallback = (list) =>
            {
                list.serializedProperty.arraySize++;
                serializedObject.ApplyModifiedProperties();
            };

            m_ChildTransitionList.onRemoveCallback = (list) => { ReorderableList.defaultBehaviours.DoRemoveButton(list); };
        }

        private void DrawTransitionData(Rect position, SerializedProperty transitionData)
        {
            SerializedProperty targetGraphic = transitionData.FindPropertyRelative("targetGraphic");
            SerializedProperty transition = transitionData.FindPropertyRelative("transition");
            SerializedProperty colorBlock = transitionData.FindPropertyRelative("colors");
            SerializedProperty spriteState = transitionData.FindPropertyRelative("spriteState");

            EditorGUI.indentLevel++;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = 2f;
            float y = position.y;

            var currentTransition = GetTransition(transition);

            if (currentTransition == Selectable.Transition.Animation)
            {
                Rect warningRect = new Rect(position.x, y, position.width, lineHeight);
                EditorGUI.HelpBox(warningRect, "Animation 过渡仅允许用于 Main Transition，子 Transition 不支持 Animation（已显示为 ColorTint）", MessageType.Warning);
                y += lineHeight + spacing;
                currentTransition = Selectable.Transition.ColorTint;
            }

            switch (currentTransition)
            {
                case Selectable.Transition.ColorTint:
                case Selectable.Transition.SpriteSwap:
                    Rect targetRect = new Rect(position.x, y, position.width, lineHeight);
                    EditorGUI.PropertyField(targetRect, targetGraphic);
                    y += lineHeight + spacing;
                    break;
            }

            Rect transitionRect = new Rect(position.x, y, position.width, lineHeight);

            string[] options = new[] { "None", "Color Tint", "Sprite Swap" };
            int[] values = new[] { (int)Selectable.Transition.None, (int)Selectable.Transition.ColorTint, (int)Selectable.Transition.SpriteSwap };

            int curVal = transition.enumValueIndex;
            int selIdx = Array.IndexOf(values, curVal);
            if (selIdx < 0) selIdx = 0;

            selIdx = EditorGUI.Popup(transitionRect, "Transition", selIdx, options);
            transition.enumValueIndex = values[selIdx];

            currentTransition = GetTransition(transition);
            y += lineHeight + spacing;

            var graphic = targetGraphic.objectReferenceValue as Graphic;

            switch (currentTransition)
            {
                case Selectable.Transition.ColorTint:
                    if (graphic == null)
                    {
                        Rect warningRect = new Rect(position.x, y, position.width, lineHeight);
                        EditorGUI.HelpBox(warningRect, "需要Graphic组件来使用颜色过渡", MessageType.Warning);
                        y += lineHeight + spacing;
                    }

                    break;

                case Selectable.Transition.SpriteSwap:
                    if (!(graphic is Image))
                    {
                        Rect warningRect = new Rect(position.x, y, position.width, lineHeight);
                        EditorGUI.HelpBox(warningRect, "需要Image组件来使用精灵切换", MessageType.Warning);
                        y += lineHeight + spacing;
                    }

                    break;
            }

            switch (currentTransition)
            {
                case Selectable.Transition.ColorTint:
                    CheckAndSetColorDefaults(colorBlock, targetGraphic);
                    Rect colorRect = new Rect(position.x, y, position.width, EditorGUI.GetPropertyHeight(colorBlock));

                    if (EditorGUILayout.BeginFadeGroup(m_ChildShowColorTint.faded))
                    {
                        EditorGUI.PropertyField(colorRect, colorBlock);
                    }

                    EditorGUILayout.EndFadeGroup();
                    break;

                case Selectable.Transition.SpriteSwap:
                    CheckAndSetColorDefaults(colorBlock, targetGraphic);
                    Rect spriteRect = new Rect(position.x, y, position.width, EditorGUI.GetPropertyHeight(spriteState));

                    if (EditorGUILayout.BeginFadeGroup(m_ChildShowSpriteTrasition.faded))
                    {
                        EditorGUI.PropertyField(spriteRect, spriteState);
                    }

                    EditorGUILayout.EndFadeGroup();
                    break;
            }

            if (graphic != null && currentTransition != (Selectable.Transition)transition.enumValueIndex &&
                ((Selectable.Transition)transition.enumValueIndex == Selectable.Transition.Animation ||
                 (Selectable.Transition)transition.enumValueIndex == Selectable.Transition.None))
            {
                graphic.canvasRenderer.SetColor(Color.white);
            }

            EditorGUI.indentLevel--;
        }

        private float CalculateTransitionDataHeight(SerializedProperty transitionData)
        {
            float height = 0;
            SerializedProperty transition = transitionData.FindPropertyRelative("transition");
            var currentTransition = GetTransition(transition);

            bool isChild = m_ChildTransitions != null &&
                           !string.IsNullOrEmpty(m_ChildTransitions.propertyPath) &&
                           transitionData.propertyPath.StartsWith(m_ChildTransitions.propertyPath);

            if (isChild && currentTransition == Selectable.Transition.Animation)
            {
                currentTransition = Selectable.Transition.ColorTint;
            }

            height += EditorGUIUtility.singleLineHeight * 1.5f;
            height += EditorGUIUtility.singleLineHeight;

            SerializedProperty targetGraphic = transitionData.FindPropertyRelative("targetGraphic");
            var graphic = targetGraphic.objectReferenceValue as Graphic;

            switch (currentTransition)
            {
                case Selectable.Transition.ColorTint:
                    if (graphic == null) height += EditorGUIUtility.singleLineHeight;
                    break;
                case Selectable.Transition.SpriteSwap:
                    if (!(graphic is Image)) height += EditorGUIUtility.singleLineHeight;
                    break;
            }

            switch (currentTransition)
            {
                case Selectable.Transition.ColorTint:
                    height += EditorGUI.GetPropertyHeight(transitionData.FindPropertyRelative("colors"));
                    break;
                case Selectable.Transition.SpriteSwap:
                    height += EditorGUI.GetPropertyHeight(transitionData.FindPropertyRelative("spriteState"));
                    break;
            }

            return height;
        }

        protected void CheckAndSetColorDefaults(SerializedProperty colorBlock, SerializedProperty targetGraphic)
        {
            if (colorBlock == null) return;

            bool isDirty = false;
            string[] colorProps = new string[] { "m_NormalColor", "m_HighlightedColor", "m_PressedColor", "m_SelectedColor", "m_DisabledColor" };
            foreach (var propName in colorProps)
            {
                SerializedProperty prop = colorBlock.FindPropertyRelative(propName);
                if (prop == null) continue;
                Color color = prop.colorValue;
                if (color.r == 0 && color.g == 0 && color.b == 0 && color.a == 0)
                {
                    isDirty = true;
                    if (prop.name == "m_PressedColor")
                    {
                        prop.colorValue = new Color(0.7843137f, 0.7843137f, 0.7843137f, 1.0f);
                    }
                    else if (prop.name == "m_DisabledColor")
                    {
                        prop.colorValue = new Color(0.7843137f, 0.7843137f, 0.7843137f, 0.5f);
                    }
                    else
                    {
                        prop.colorValue = Color.white;
                    }
                }
            }

            SerializedProperty fadeDuration = colorBlock.FindPropertyRelative("m_FadeDuration");
            SerializedProperty m_ColorMultiplier = colorBlock.FindPropertyRelative("m_ColorMultiplier");
            if (isDirty)
            {
                if (m_ColorMultiplier != null) m_ColorMultiplier.floatValue = 1f;
                if (fadeDuration != null) fadeDuration.floatValue = 0.1f;
            }

            var graphic = targetGraphic != null ? targetGraphic.objectReferenceValue as Graphic : null;
            if (graphic != null)
            {
                if (!EditorApplication.isPlaying)
                {
                    SerializedProperty normalColorProp = colorBlock.FindPropertyRelative("m_NormalColor");
                    if (normalColorProp != null)
                    {
                        Color color = normalColorProp.colorValue;
                        graphic.canvasRenderer.SetColor(color);
                    }
                }
            }
        }

        #endregion

        #region Static Method

        static Selectable.Transition GetTransition(SerializedProperty transition)
        {
            return (Selectable.Transition)transition.enumValueIndex;
        }

        private static AnimatorController GenerateSelectableAnimatorContoller(AnimationTriggers animationTriggers, Selectable target)
        {
            if (target == null)
                return null;

            // Where should we create the controller?
            var path = GetSaveControllerPath(target);
            if (string.IsNullOrEmpty(path))
                return null;

            // figure out clip names
            var normalName = string.IsNullOrEmpty(animationTriggers.normalTrigger) ? "Normal" : animationTriggers.normalTrigger;
            var highlightedName = string.IsNullOrEmpty(animationTriggers.highlightedTrigger) ? "Highlighted" : animationTriggers.highlightedTrigger;
            var pressedName = string.IsNullOrEmpty(animationTriggers.pressedTrigger) ? "Pressed" : animationTriggers.pressedTrigger;
            var selectedName = string.IsNullOrEmpty(animationTriggers.selectedTrigger) ? "Selected" : animationTriggers.selectedTrigger;
            var disabledName = string.IsNullOrEmpty(animationTriggers.disabledTrigger) ? "Disabled" : animationTriggers.disabledTrigger;

            // Create controller and hook up transitions.
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            GenerateTriggerableTransition(normalName, controller);
            GenerateTriggerableTransition(highlightedName, controller);
            GenerateTriggerableTransition(pressedName, controller);
            GenerateTriggerableTransition(selectedName, controller);
            GenerateTriggerableTransition(disabledName, controller);

            AssetDatabase.ImportAsset(path);

            return controller;
        }

        private static string GetSaveControllerPath(Selectable target)
        {
            var defaultName = target.gameObject.name;
            var message = string.Format("Create a new animator for the game object '{0}':", defaultName);
            return EditorUtility.SaveFilePanelInProject("New Animation Contoller", defaultName, "controller", message);
        }

        private static void SetUpCurves(AnimationClip highlightedClip, AnimationClip pressedClip, string animationPath)
        {
            string[] channels = { "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };

            var highlightedKeys = new[] { new Keyframe(0f, 1f), new Keyframe(0.5f, 1.1f), new Keyframe(1f, 1f) };
            var highlightedCurve = new AnimationCurve(highlightedKeys);
            foreach (var channel in channels)
                AnimationUtility.SetEditorCurve(highlightedClip, EditorCurveBinding.FloatCurve(animationPath, typeof(Transform), channel), highlightedCurve);

            var pressedKeys = new[] { new Keyframe(0f, 1.15f) };
            var pressedCurve = new AnimationCurve(pressedKeys);
            foreach (var channel in channels)
                AnimationUtility.SetEditorCurve(pressedClip, EditorCurveBinding.FloatCurve(animationPath, typeof(Transform), channel), pressedCurve);
        }

        private static string BuildAnimationPath(Selectable target)
        {
            // if no target don't hook up any curves.
            var highlight = target.targetGraphic;
            if (highlight == null)
                return string.Empty;

            var startGo = highlight.gameObject;
            var toFindGo = target.gameObject;

            var pathComponents = new Stack<string>();
            while (toFindGo != startGo)
            {
                pathComponents.Push(startGo.name);

                // didn't exist in hierarchy!
                if (startGo.transform.parent == null)
                    return string.Empty;

                startGo = startGo.transform.parent.gameObject;
            }

            // calculate path
            var animPath = new StringBuilder();
            if (pathComponents.Count > 0)
                animPath.Append(pathComponents.Pop());

            while (pathComponents.Count > 0)
                animPath.Append("/").Append(pathComponents.Pop());

            return animPath.ToString();
        }

        private static AnimationClip GenerateTriggerableTransition(string name, AnimatorController controller)
        {
            // Create the clip
            var clip = AnimatorController.AllocateAnimatorClip(name);
            AssetDatabase.AddObjectToAsset(clip, controller);

            // Create a state in the animatior controller for this clip
            var state = controller.AddMotion(clip);

            // Add a transition property
            controller.AddParameter(name, AnimatorControllerParameterType.Trigger);

            // Add an any state transition
            var stateMachine = controller.layers[0].stateMachine;
            var transition = stateMachine.AddAnyStateTransition(state);
            transition.AddCondition(AnimatorConditionMode.If, 0, name);
            return clip;
        }

        #endregion
    }
}
