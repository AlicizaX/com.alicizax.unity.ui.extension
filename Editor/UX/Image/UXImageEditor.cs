using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;
using UnityEditorInternal;
using UnityEditor.AnimatedValues;

namespace UnityEngine.UI
{
    [CustomEditor(typeof(UXImage), true)]
    [CanEditMultipleObjects]
    internal class UXImageEditor : Editor
    {
        private SerializedProperty m_Color;
        private SerializedProperty m_RaycastTarget;
        protected SerializedProperty m_RaycastPadding;
        private SerializedProperty m_MaskAble;
        private SerializedProperty m_Sprite;
        private SerializedProperty m_Type;
        private SerializedProperty m_Material;
        private SerializedProperty m_sourceImage;
        private SerializedProperty m_ColorType;
        private SerializedProperty m_Direction;
        private SerializedProperty m_EnableOutline;
        private SerializedProperty m_EnableShadow;
        private SerializedProperty m_OutlineEffectColor;
        private SerializedProperty m_OutlineEffectDistance;
        private SerializedProperty m_OutlineSoftness;
        private SerializedProperty m_ShadowEffectColor;
        private SerializedProperty m_ShadowEffectDistance;
        private SerializedProperty m_ShadowSoftness;
        private SerializedProperty m_UseGraphicAlpha;

        SerializedProperty m_UseSpriteMesh;
        SerializedProperty m_PreserveAspect;
        SerializedProperty m_FillCenter;
        SerializedProperty m_PixelsPerUnitMultiplier;
        SerializedProperty m_FillMethod;
        SerializedProperty m_FillOrigin;
        SerializedProperty m_FillAmount;
        SerializedProperty m_FillClockwise;

        SerializedProperty m_FlipMode;
        SerializedProperty m_FlipWithCopy;
        SerializedProperty m_FlipEdgeHorizontal;
        SerializedProperty m_FlipEdgeVertical;
        SerializedProperty m_FlipFillCenter;
        SerializedProperty m_GradientColor;
        GUIContent m_ClockwiseContent;
        GUIContent m_SpriteFlipContent;
        GUIContent m_FlipModeContent;
        GUIContent m_FlipEdgeContent;
        GUIContent m_FlipFillContent;


        private UXImage m_targetObject;
        private GameObject m_CloneObj;
        private string m_Origin_name;
        private bool m_InitialHide;
#if UNITY_2021_1_OR_NEWER
        private bool m_bIsDriven;
#endif
        GUIContent m_SpriteContent;
        GUIContent m_PaddingContent;
        GUIContent m_LeftContent;
        GUIContent m_RightContent;
        GUIContent m_TopContent;
        GUIContent m_BottomContent;
        private GUIContent m_CorrectButtonContent;
        GUIContent m_SpriteTypeContent;

        AnimBool m_ShowType;
        protected AnimBool m_ShowNativeSize;
        AnimBool m_ShowSlicedOrTiled;
        AnimBool m_ShowSliced;
        AnimBool m_ShowTiled;
        AnimBool m_ShowFilled;


        static private bool m_ShowPadding = false;

        private class Styles
        {
            public static GUIContent text = EditorGUIUtility.TrTextContent("Fill Origin");

            public static GUIContent[] OriginHorizontalStyle =
            {
                EditorGUIUtility.TrTextContent("Left"),
                EditorGUIUtility.TrTextContent("Right")
            };

            public static GUIContent[] OriginVerticalStyle =
            {
                EditorGUIUtility.TrTextContent("Bottom"),
                EditorGUIUtility.TrTextContent("Top")
            };

            public static GUIContent[] Origin90Style =
            {
                EditorGUIUtility.TrTextContent("BottomLeft"),
                EditorGUIUtility.TrTextContent("TopLeft"),
                EditorGUIUtility.TrTextContent("TopRight"),
                EditorGUIUtility.TrTextContent("BottomRight")
            };

            public static GUIContent[] Origin180Style =
            {
                EditorGUIUtility.TrTextContent("Bottom"),
                EditorGUIUtility.TrTextContent("Left"),
                EditorGUIUtility.TrTextContent("Top"),
                EditorGUIUtility.TrTextContent("Right")
            };

            public static GUIContent[] Origin360Style =
            {
                EditorGUIUtility.TrTextContent("Bottom"),
                EditorGUIUtility.TrTextContent("Right"),
                EditorGUIUtility.TrTextContent("Top"),
                EditorGUIUtility.TrTextContent("Left")
            };
        }

        private void OnEnable()
        {
            m_Color = serializedObject.FindProperty("m_Color");
            m_Sprite = serializedObject.FindProperty("m_Sprite");
            m_Type = serializedObject.FindProperty("m_Type");
            m_Material = serializedObject.FindProperty("m_Material");
            m_RaycastTarget = serializedObject.FindProperty("m_RaycastTarget");
            m_RaycastPadding = serializedObject.FindProperty("m_RaycastPadding");
            m_sourceImage = serializedObject.FindProperty("m_Sprite");
            m_MaskAble = serializedObject.FindProperty("m_Maskable");
            //spriteContent = new GUIContent("Source Image");
            m_targetObject = serializedObject.targetObject as UXImage;
            m_Origin_name = m_Sprite.objectReferenceValue?.name;


            m_ColorType = serializedObject.FindProperty("m_ColorType");
            m_GradientColor = serializedObject.FindProperty("m_GradientColor");
            m_Direction = serializedObject.FindProperty("m_Direction");
            m_EnableOutline = serializedObject.FindProperty("m_EnableOutline");
            m_EnableShadow = serializedObject.FindProperty("m_EnableShadow");
            m_OutlineEffectColor = serializedObject.FindProperty("m_OutlineEffectColor");
            m_OutlineEffectDistance = serializedObject.FindProperty("m_OutlineEffectDistance");
            m_OutlineSoftness = serializedObject.FindProperty("m_OutlineSoftness");
            m_ShadowEffectColor = serializedObject.FindProperty("m_ShadowEffectColor");
            m_ShadowEffectDistance = serializedObject.FindProperty("m_ShadowEffectDistance");
            m_ShadowSoftness = serializedObject.FindProperty("m_ShadowSoftness");
            m_UseGraphicAlpha = serializedObject.FindProperty("m_UseGraphicAlpha");

            m_UseSpriteMesh = serializedObject.FindProperty("m_UseSpriteMesh");
            m_PreserveAspect = serializedObject.FindProperty("m_PreserveAspect");
            m_FillCenter = serializedObject.FindProperty("m_FillCenter");
            m_PixelsPerUnitMultiplier = serializedObject.FindProperty("m_PixelsPerUnitMultiplier");
            m_FillMethod = serializedObject.FindProperty("m_FillMethod");
            m_FillOrigin = serializedObject.FindProperty("m_FillOrigin");
            m_FillClockwise = serializedObject.FindProperty("m_FillClockwise");
            m_FillAmount = serializedObject.FindProperty("m_FillAmount");

            m_FlipModeContent = new GUIContent("FlipMode");
            m_FlipEdgeContent = new GUIContent("FlipEdge");
            m_FlipFillContent = new GUIContent("FlipFill");

            m_FlipMode = serializedObject.FindProperty("m_FlipMode");
            m_FlipWithCopy = serializedObject.FindProperty("m_FlipWithCopy");
            m_FlipEdgeHorizontal = serializedObject.FindProperty("m_FlipEdgeHorizontal");
            m_FlipEdgeVertical = serializedObject.FindProperty("m_FlipEdgeVertical");
            m_FlipFillCenter = serializedObject.FindProperty("m_FlipFillCenter");

#if UNITY_2021_1_OR_NEWER
            m_bIsDriven = false;
#endif
            m_PaddingContent = EditorGUIUtility.TrTextContent("Raycast Padding");
            m_LeftContent = EditorGUIUtility.TrTextContent("Left");
            m_RightContent = EditorGUIUtility.TrTextContent("Right");
            m_TopContent = EditorGUIUtility.TrTextContent("Top");
            m_BottomContent = EditorGUIUtility.TrTextContent("Bottom");
            m_CorrectButtonContent = EditorGUIUtility.TrTextContent("Set Native Size", "Sets the size to match the content.");
            m_SpriteTypeContent = EditorGUIUtility.TrTextContent("Image Type");
            m_ClockwiseContent = EditorGUIUtility.TrTextContent("Clockwise");
            m_SpriteContent = EditorGUIUtility.TrTextContent("Source Image");

            m_ShowType = new AnimBool(m_Sprite.objectReferenceValue != null);
            m_ShowType.valueChanged.AddListener(Repaint);
            m_ShowNativeSize = new AnimBool(ShouldShowNativeSize());
            m_ShowNativeSize.valueChanged.AddListener(Repaint);

            var typeEnum = (Image.Type)m_Type.enumValueIndex;
            m_ShowSlicedOrTiled = new AnimBool(!m_Type.hasMultipleDifferentValues && typeEnum == Image.Type.Sliced);
            m_ShowSliced = new AnimBool(!m_Type.hasMultipleDifferentValues && typeEnum == Image.Type.Sliced);
            m_ShowTiled = new AnimBool(!m_Type.hasMultipleDifferentValues && typeEnum == Image.Type.Tiled);
            m_ShowFilled = new AnimBool(!m_Type.hasMultipleDifferentValues && typeEnum == Image.Type.Filled);
            m_ShowSlicedOrTiled.valueChanged.AddListener(Repaint);
            m_ShowSliced.valueChanged.AddListener(Repaint);
            m_ShowTiled.valueChanged.AddListener(Repaint);
            m_ShowFilled.valueChanged.AddListener(Repaint);
        }

        private void OnDisable()
        {
            if (m_CloneObj != null)
            {
                DestroyImmediate(m_CloneObj);
            }


            m_ShowType.valueChanged.RemoveListener(Repaint);
            m_ShowNativeSize.valueChanged.RemoveListener(Repaint);
            m_ShowSlicedOrTiled.valueChanged.RemoveListener(Repaint);
            m_ShowSliced.valueChanged.RemoveListener(Repaint);
            m_ShowTiled.valueChanged.RemoveListener(Repaint);
            m_ShowFilled.valueChanged.RemoveListener(Repaint);
        }


        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            //base.OnInspectorGUI();
            //CustomEditorGUILayout.PropertyField(sourceImage);
            //CustomEditorGUILayout.PropertyField(color);
            //CustomEditorGUILayout.PropertyField(material);
            //CustomEditorGUILayout.PropertyField(raycastTarget);
            //CustomEditorGUILayout.PropertyField(maskAble);

            Image image = target as Image;
            RectTransform rect = image.GetComponent<RectTransform>();
#if UNITY_2021_1_OR_NEWER
            m_bIsDriven = (rect.drivenByObject as Slider)?.fillRect == rect;
#endif
            SpriteGUI();
            AppearanceControlsGUI();
            RaycastControlsGUI();
            MaskableControlsGUI();

            //m_ShowType.target = m_Sprite.objectReferenceValue != null;
            //if (EditorGUILayout.BeginFadeGroup(m_ShowType.faded))
            TypeGUI();
            //EditorGUILayout.EndFadeGroup();

            SetShowNativeSize(false);
            if (EditorGUILayout.BeginFadeGroup(m_ShowNativeSize.faded))
            {
                EditorGUI.indentLevel++;

                if ((Image.Type)m_Type.enumValueIndex == Image.Type.Simple)
                    EditorGUILayout.PropertyField(m_UseSpriteMesh);

                EditorGUILayout.PropertyField(m_PreserveAspect);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFadeGroup();
            NativeSizeButtonGUI();

            FlipGUI();
            EffectGUI();
            serializedObject.ApplyModifiedProperties();
        }

        protected void SpriteGUI()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(m_Sprite, m_SpriteContent);
            if (EditorGUI.EndChangeCheck())
            {
                var newSprite = m_Sprite.objectReferenceValue as Sprite;
                if (newSprite)
                {
                    UXImage.Type oldType = (UXImage.Type)m_Type.enumValueIndex;
                    if (newSprite.border.SqrMagnitude() > 0)
                    {
                        m_Type.enumValueIndex = (int)UXImage.Type.Sliced;
                    }
                    else if (oldType == UXImage.Type.Sliced)
                    {
                        m_Type.enumValueIndex = (int)UXImage.Type.Simple;
                    }
                }
            }
        }

        protected void MaskableControlsGUI()
        {
            EditorGUILayout.PropertyField(m_MaskAble);
        }

        protected void AppearanceControlsGUI()
        {
            EditorGUI.BeginChangeCheck();
            string[] labels = { "Solid_Color", "Gradient_Color" };
            var type = (UXImage.ColorType)EnumPopupLayoutEx("ColorType", typeof(UXImage.ColorType), m_ColorType.intValue, labels);
            // EditorGUILayout.EnumPopup(EditorLocalization.GetLocalization("UXImage", "m_ColorType"), (UXImage.ColorType)m_ColorType.intValue);
            if (EditorGUI.EndChangeCheck())
            {
                m_ColorType.intValue = (int)type;
            }

            if (type == UXImage.ColorType.Solid_Color)
            {
                EditorGUILayout.PropertyField(m_Color);
            }
            else if (type == UXImage.ColorType.Gradient_Color)
            {
                EditorGUI.BeginChangeCheck();
                string[] labels2 = { "Vertical", "Horizontal" };
                var direction = (UXImage.GradientDirection)EnumPopupLayoutEx("m_Direction", typeof(UXImage.GradientDirection), m_Direction.intValue, labels2);
                if (EditorGUI.EndChangeCheck())
                {
                    m_Direction.intValue = (int)direction;
                }

                //EditorGUILayout.ColorField(m_Color);
                EditorGUILayout.PropertyField(m_GradientColor, new GUIContent("Gradient_Color"));
            }

            EditorGUILayout.PropertyField(m_Material);
        }

        private void EffectGUI()
        {
            EditorGUILayout.PropertyField(m_EnableOutline, new GUIContent("Outline"));
            if (m_EnableOutline.boolValue)
            {
                ++EditorGUI.indentLevel;
                EditorGUILayout.PropertyField(m_OutlineEffectColor, new GUIContent("Effect Color"));
                EditorGUILayout.PropertyField(m_OutlineEffectDistance, new GUIContent("Effect Distance"));
                EditorGUILayout.PropertyField(m_OutlineSoftness, new GUIContent("Softness"));
                --EditorGUI.indentLevel;
            }

            EditorGUILayout.PropertyField(m_EnableShadow, new GUIContent("Shadow"));
            if (m_EnableShadow.boolValue)
            {
                ++EditorGUI.indentLevel;
                EditorGUILayout.PropertyField(m_ShadowEffectColor, new GUIContent("Effect Color"));
                EditorGUILayout.PropertyField(m_ShadowEffectDistance, new GUIContent("Effect Distance"));
                EditorGUILayout.PropertyField(m_ShadowSoftness, new GUIContent("Softness"));
                --EditorGUI.indentLevel;
            }

            if (m_EnableOutline.boolValue || m_EnableShadow.boolValue)
                EditorGUILayout.PropertyField(m_UseGraphicAlpha, new GUIContent("Use Graphic Alpha"));
        }

        protected void RaycastControlsGUI()
        {
            EditorGUILayout.PropertyField(m_RaycastTarget);

            float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            if (m_ShowPadding)
                height += (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 4;

            var rect = EditorGUILayout.GetControlRect(true, height);
            if (m_RaycastPadding != null)
            {
                EditorGUI.BeginProperty(rect, m_PaddingContent, m_RaycastPadding);
                rect.height = EditorGUIUtility.singleLineHeight;

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    m_ShowPadding = EditorGUI.Foldout(rect, m_ShowPadding, m_PaddingContent, true);
                    if (check.changed)
                    {
                        SceneView.RepaintAll();
                    }
                }

                if (m_ShowPadding)
                {
                    using (var check = new EditorGUI.ChangeCheckScope())
                    {
                        EditorGUI.indentLevel++;
                        Vector4 newPadding = m_RaycastPadding.vector4Value;

                        rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                        newPadding.x = EditorGUI.FloatField(rect, m_LeftContent, newPadding.x);

                        rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                        newPadding.y = EditorGUI.FloatField(rect, m_BottomContent, newPadding.y);

                        rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                        newPadding.z = EditorGUI.FloatField(rect, m_RightContent, newPadding.z);

                        rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                        newPadding.w = EditorGUI.FloatField(rect, m_TopContent, newPadding.w);

                        if (check.changed)
                        {
                            m_RaycastPadding.vector4Value = newPadding;
                        }

                        EditorGUI.indentLevel--;
                    }
                }

                EditorGUI.EndProperty();
            }
        }

        protected void TypeGUI()
        {
            EditorGUILayout.PropertyField(m_Type, m_SpriteTypeContent);

            ++EditorGUI.indentLevel;
            {
                Image.Type typeEnum = (Image.Type)m_Type.enumValueIndex;

                bool showSlicedOrTiled = (!m_Type.hasMultipleDifferentValues && (typeEnum == Image.Type.Sliced || typeEnum == Image.Type.Tiled));
                if (showSlicedOrTiled && targets.Length > 1)
                {
                    for (int i = 0; i < targets.Length; i++)
                    {
                        Image targetImage = targets[i] as Image;
                        if (targetImage == null || !targetImage.hasBorder)
                        {
                            showSlicedOrTiled = false;
                            break;
                        }
                    }
                }

                m_ShowSlicedOrTiled.target = showSlicedOrTiled;
                m_ShowSliced.target = (showSlicedOrTiled && !m_Type.hasMultipleDifferentValues && typeEnum == Image.Type.Sliced);
                m_ShowTiled.target = (showSlicedOrTiled && !m_Type.hasMultipleDifferentValues && typeEnum == Image.Type.Tiled);
                m_ShowFilled.target = (!m_Type.hasMultipleDifferentValues && typeEnum == Image.Type.Filled);

                Image image = target as Image;
                if (EditorGUILayout.BeginFadeGroup(m_ShowSlicedOrTiled.faded))
                {
                    if (image.hasBorder)
                        EditorGUILayout.PropertyField(m_FillCenter);
                    EditorGUILayout.PropertyField(m_PixelsPerUnitMultiplier);
                }

                EditorGUILayout.EndFadeGroup();

                if (EditorGUILayout.BeginFadeGroup(m_ShowSliced.faded))
                {
                    if (image.sprite != null && !image.hasBorder)
                        EditorGUILayout.HelpBox("This Image doesn't have a border.", MessageType.Warning);
                }

                EditorGUILayout.EndFadeGroup();

                if (EditorGUILayout.BeginFadeGroup(m_ShowTiled.faded))
                {
                    if (image.sprite != null && !image.hasBorder && (image.sprite.texture.wrapMode != TextureWrapMode.Repeat || image.sprite.packed))
                        EditorGUILayout.HelpBox("It looks like you want to tile a sprite with no border. It would be more efficient to modify the Sprite properties, clear the Packing tag and set the Wrap mode to Repeat.", MessageType.Warning);
                }

                EditorGUILayout.EndFadeGroup();

                if (EditorGUILayout.BeginFadeGroup(m_ShowFilled.faded))
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(m_FillMethod);
                    if (EditorGUI.EndChangeCheck())
                    {
                        m_FillOrigin.intValue = 0;
                    }

                    var shapeRect = EditorGUILayout.GetControlRect(true);
                    switch ((Image.FillMethod)m_FillMethod.enumValueIndex)
                    {
                        case Image.FillMethod.Horizontal:
                            m_FillOrigin.intValue = EditorGUI.Popup(shapeRect, Styles.text, m_FillOrigin.intValue, Styles.OriginHorizontalStyle);
                            break;
                        case Image.FillMethod.Vertical:
                            m_FillOrigin.intValue = EditorGUI.Popup(shapeRect, Styles.text, m_FillOrigin.intValue, Styles.OriginVerticalStyle);
                            break;
                        case Image.FillMethod.Radial90:
                            m_FillOrigin.intValue = EditorGUI.Popup(shapeRect, Styles.text, m_FillOrigin.intValue, Styles.Origin90Style);
                            break;
                        case Image.FillMethod.Radial180:
                            m_FillOrigin.intValue = EditorGUI.Popup(shapeRect, Styles.text, m_FillOrigin.intValue, Styles.Origin180Style);
                            break;
                        case Image.FillMethod.Radial360:
                            m_FillOrigin.intValue = EditorGUI.Popup(shapeRect, Styles.text, m_FillOrigin.intValue, Styles.Origin360Style);
                            break;
                    }
#if UNITY_2021_1_OR_NEWER
                    if (m_bIsDriven)
                        EditorGUILayout.HelpBox("The Fill amount property is driven by Slider.", MessageType.None);
                    using (new EditorGUI.DisabledScope(m_bIsDriven))
                    {
                        EditorGUILayout.PropertyField(m_FillAmount);
                    }
#else
                    EditorGUILayout.PropertyField(m_FillAmount);
#endif

                    if ((Image.FillMethod)m_FillMethod.enumValueIndex > Image.FillMethod.Vertical)
                    {
                        EditorGUILayout.PropertyField(m_FillClockwise, m_ClockwiseContent);
                    }
                }

                EditorGUILayout.EndFadeGroup();
            }
            --EditorGUI.indentLevel;
        }

        private void FlipGUI()
        {
            EditorGUI.BeginChangeCheck();
            //EditorGUILayout.PropertyField(m_gradientMode, m_SpriteGradientContent);
            RectTransform targetRectTransform = m_targetObject != null ? m_targetObject.rectTransform : null;
            float targetWidth = targetRectTransform != null ? targetRectTransform.rect.width : 0f;
            float targetHeight = targetRectTransform != null ? targetRectTransform.rect.height : 0f;

            UXImage.FlipMode flipmodeEnumOld = (UXImage.FlipMode)m_FlipMode.enumValueIndex;
            bool flipWithCopyOld = m_FlipWithCopy.boolValue;
#if UNITY_2021_2_OR_NEWER
            UXImage.FlipEdgeVertical edgeVerticalOld = (UXImage.FlipEdgeVertical)(m_FlipEdgeVertical.enumValueFlag);
#else
            UXImage.FlipEdgeVertical edgeVerticalOld = (UXImage.FlipEdgeVertical)(m_FlipEdgeVertical.enumValueIndex + 3);
#endif
            UXImage.FlipEdgeHorizontal edgeHorizontalOld = (UXImage.FlipEdgeHorizontal)m_FlipEdgeHorizontal.enumValueIndex;

            string[] labels =
            {
                "None", "Horizontal",
                "Vertical", "FourCorner"
            };
            m_FlipMode.intValue = EnumPopupLayoutEx(m_FlipModeContent.text, typeof(UXImage.FlipMode), m_FlipMode.intValue, labels);

            //EditorGUILayout.PropertyField(m_FlipMode, m_FlipModeContent);
            UXImage.FlipMode flipmodeEnum = (UXImage.FlipMode)m_FlipMode.enumValueIndex;
            switch (flipmodeEnum)
            {
                case UXImage.FlipMode.Horziontal:
                    ++EditorGUI.indentLevel;
                    bool iscopy1 = m_FlipWithCopy.boolValue;
                    m_FlipWithCopy.boolValue = EditorGUILayout.Toggle("Copy", m_FlipWithCopy.boolValue);
                    if (iscopy1 == true && m_FlipWithCopy.boolValue == false)
                    {
                        if (m_FlipEdgeHorizontal.intValue == 0)
                        {
                            TranslateTarget(targetRectTransform, Vector3.right, targetWidth / 4);
                        }
                        else if (m_FlipEdgeHorizontal.intValue == 2)
                        {
                            TranslateTarget(targetRectTransform, Vector3.left, targetWidth / 4);
                        }
                    }

                    if (m_FlipWithCopy.boolValue)
                    {
                        int last = m_FlipEdgeHorizontal.intValue;
                        if (iscopy1 == false)
                        {
                            if (last == 0)
                            {
                                TranslateTarget(targetRectTransform, Vector3.left, targetWidth / 2);
                            }
                            else if (last == 2)
                            {
                                TranslateTarget(targetRectTransform, Vector3.right, targetWidth / 2);
                            }
                        }

                        string[] labels2 =
                        {
                            "Left", "Middle",
                            "Right"
                        };
                        m_FlipEdgeHorizontal.intValue = EnumPopupLayoutEx(m_FlipEdgeContent.text, typeof(UXImage.FlipEdgeHorizontal), m_FlipEdgeHorizontal.intValue, labels2);
                        //EditorGUILayout.PropertyField(m_FlipEdgeHorizontal, m_FlipEdgeContent);
                        int now = m_FlipEdgeHorizontal.intValue;

                        if (last != now)
                        {
                            if ((last == 0 && now == 1))
                            {
                                TranslateTarget(targetRectTransform, Vector3.right, targetWidth / 4);
                            }
                            else if ((last == 2 && now == 1))
                            {
                                TranslateTarget(targetRectTransform, Vector3.left, targetWidth / 4);
                            }
                            else if (last == 0 && now == 2 || (last == 1 && now == 2))
                            {
                                TranslateTarget(targetRectTransform, Vector3.right, targetWidth / 2);
                            }
                            else if ((last == 1 && now == 0) || (last == 2 && now == 0))
                            {
                                TranslateTarget(targetRectTransform, Vector3.left, targetWidth / 2);
                            }
                        }
                    }

                    --EditorGUI.indentLevel;
                    break;
                case UXImage.FlipMode.Vertical:
                    ++EditorGUI.indentLevel;
                    bool iscopy = m_FlipWithCopy.boolValue;
                    m_FlipWithCopy.boolValue = EditorGUILayout.Toggle("Copy", m_FlipWithCopy.boolValue);
                    if (iscopy == true && m_FlipWithCopy.boolValue == false)
                    {
                        if (m_FlipEdgeVertical.intValue == 3)
                        {
                            TranslateTarget(targetRectTransform, Vector3.down, targetHeight / 4);
                        }
                        else if (m_FlipEdgeVertical.intValue == 5)
                        {
                            TranslateTarget(targetRectTransform, Vector3.up, targetHeight / 4);
                        }
                    }

                    if (m_FlipWithCopy.boolValue)
                    {
                        int last = m_FlipEdgeVertical.intValue;
                        if (iscopy == false)
                        {
                            if (last == 3)
                            {
                                TranslateTarget(targetRectTransform, Vector3.up, targetHeight / 2);
                            }
                            else if (last == 5)
                            {
                                TranslateTarget(targetRectTransform, Vector3.down, targetHeight / 2);
                            }
                        }

                        string[] labels2 =
                        {
                            "Up", "Middle",
                            "Down"
                        };
                        m_FlipEdgeVertical.intValue = EnumPopupLayoutEx(m_FlipEdgeContent.text, typeof(UXImage.FlipEdgeVertical), m_FlipEdgeVertical.intValue, labels2);
                        //EditorGUILayout.PropertyField(m_FlipEdgeVertical, m_FlipEdgeContent);
                        int now = m_FlipEdgeVertical.intValue;
                        if (last != now)
                        {
                            if ((last == 3 && now == 4))
                            {
                                TranslateTarget(targetRectTransform, Vector3.down, targetHeight / 4);
                            }
                            else if ((last == 5 && now == 4))
                            {
                                TranslateTarget(targetRectTransform, Vector3.up, targetHeight / 4);
                            }
                            else if (last == 3 && now == 5 || (last == 4 && now == 5))
                            {
                                TranslateTarget(targetRectTransform, Vector3.down, targetHeight / 2);
                            }
                            else if ((last == 4 && now == 3) || last == 5 && now == 3)
                            {
                                TranslateTarget(targetRectTransform, Vector3.up, targetHeight / 2);
                            }
                        }
                    }

                    --EditorGUI.indentLevel;
                    break;
                case UXImage.FlipMode.FourCorner:
                    ++EditorGUI.indentLevel;
                    int lastC = m_FlipFillCenter.intValue;
                    string[] labels3 = { "LeftTop", "RightTop", "RightBottom", "LeftBottom" };
                    m_FlipFillCenter.intValue = EnumPopupLayoutEx(m_FlipFillContent.text, typeof(UXImage.FlipFillCenter), m_FlipFillCenter.intValue, labels3);
                    //EditorGUILayout.PropertyField(m_FlipFillCenter, m_FlipFillContent);
                    int nowC = m_FlipFillCenter.intValue;
                    --EditorGUI.indentLevel;
                    break;
                default:
                    break;
            }

            if (EditorGUI.EndChangeCheck())
            {
                int OldWidthScaler = 1;
                int OldHeightScaler = 1;
                int NewWidthScaler = 1;
                int NewHeightScaler = 1;


                if (flipmodeEnumOld != flipmodeEnum)
                {
                    m_FlipWithCopy.boolValue = false;
                }

                if (flipmodeEnumOld == UXImage.FlipMode.Horziontal)
                {
                    GetSizeScaler(flipmodeEnumOld, flipWithCopyOld, (UXImage.FlipEdge)(int)edgeHorizontalOld, ref OldWidthScaler, ref OldHeightScaler);
                }

                if (flipmodeEnumOld == UXImage.FlipMode.Vertical)
                {
                    GetSizeScaler(flipmodeEnumOld, flipWithCopyOld, (UXImage.FlipEdge)(int)edgeVerticalOld, ref OldWidthScaler, ref OldHeightScaler);
                }

                if (flipmodeEnumOld == UXImage.FlipMode.FourCorner)
                {
                    GetSizeScaler(flipmodeEnumOld, flipWithCopyOld, UXImage.FlipEdge.None, ref OldWidthScaler, ref OldHeightScaler);
                }

                if (flipmodeEnum == UXImage.FlipMode.Horziontal)
                {
#if UNITY_2021_2_OR_NEWER
                    GetSizeScaler(flipmodeEnum, m_FlipWithCopy.boolValue, (UXImage.FlipEdge)(int)m_FlipEdgeHorizontal.enumValueFlag, ref NewWidthScaler, ref NewHeightScaler);
#else
                    GetSizeScaler(flipmodeEnum, m_FlipWithCopy.boolValue, (UXImage.FlipEdge)(int)m_FlipEdgeHorizontal.enumValueIndex, ref NewWidthScaler, ref NewHeightScaler);
#endif
                }

                if (flipmodeEnum == UXImage.FlipMode.Vertical)
                {
#if UNITY_2021_2_OR_NEWER
                    GetSizeScaler(flipmodeEnum, m_FlipWithCopy.boolValue, (UXImage.FlipEdge)(int)m_FlipEdgeVertical.enumValueFlag, ref NewWidthScaler, ref NewHeightScaler);
#else
                    GetSizeScaler(flipmodeEnum, m_FlipWithCopy.boolValue, (UXImage.FlipEdge)(int)(m_FlipEdgeVertical.enumValueIndex + 3), ref NewWidthScaler, ref NewHeightScaler);
#endif
                }

                if (flipmodeEnum == UXImage.FlipMode.FourCorner)
                {
                    GetSizeScaler(flipmodeEnum, m_FlipWithCopy.boolValue, UXImage.FlipEdge.None, ref NewWidthScaler, ref NewHeightScaler);
                }

                UXImage image = target as UXImage;
                float width = image.rectTransform.rect.width * ((float)NewWidthScaler / OldWidthScaler);
                float height = image.rectTransform.rect.height * ((float)NewHeightScaler / OldHeightScaler);
                image.rectTransform.sizeDelta = new Vector2(width, height);
                serializedObject.ApplyModifiedProperties();
            }
        }

        private static void TranslateTarget(RectTransform rectTransform, Vector3 direction, float distance)
        {
            if (rectTransform == null || Mathf.Approximately(distance, 0f))
                return;

            rectTransform.Translate(direction * distance);
        }

        void GetSizeScaler(UXImage.FlipMode flipMode, bool flipWithCopy, UXImage.FlipEdge flipEdge, ref int widthScaler, ref int heightScaler)
        {
            if (flipMode == UXImage.FlipMode.FourCorner)
            {
                widthScaler = 2;
                heightScaler = 2;
            }

            if (flipMode == UXImage.FlipMode.Vertical)
            {
                if (flipWithCopy && flipEdge != UXImage.FlipEdge.VertMiddle)
                {
                    widthScaler = 1;
                    heightScaler = 2;
                }
            }

            if (flipMode == UXImage.FlipMode.Horziontal)
            {
                if (flipWithCopy && flipEdge != UXImage.FlipEdge.HorzMiddle)
                {
                    widthScaler = 2;
                    heightScaler = 1;
                }
            }
        }

        void SetShowNativeSize(bool instant)
        {
            bool showNativeSize = ShouldShowNativeSize();
            if (instant)
                m_ShowNativeSize.value = showNativeSize;
            else
                m_ShowNativeSize.target = showNativeSize;
        }

        private bool ShouldShowNativeSize()
        {
            Image.Type type = (Image.Type)m_Type.enumValueIndex;
            return (type == Image.Type.Simple || type == Image.Type.Filled) && m_Sprite.objectReferenceValue != null;
        }

        protected void NativeSizeButtonGUI()
        {
            if (EditorGUILayout.BeginFadeGroup(m_ShowNativeSize.faded))
            {
                EditorGUILayout.BeginHorizontal();
                {
                    GUILayout.Space(EditorGUIUtility.labelWidth);
                    if (GUILayout.Button(m_CorrectButtonContent, EditorStyles.miniButton))
                    {
                        for (int i = 0; i < targets.Length; i++)
                        {
                            Graphic graphic = targets[i] as Graphic;
                            if (graphic == null)
                                continue;

                            Undo.RecordObject(graphic.rectTransform, "Set Native Size");
                            graphic.SetNativeSize();
                            EditorUtility.SetDirty(graphic);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndFadeGroup();
        }


        private static readonly Dictionary<Type, int[]> s_EnumValues = new Dictionary<Type, int[]>();
        private static readonly Dictionary<Type, string[]> s_EnumNames = new Dictionary<Type, string[]>();

        private static int[] GetEnumValues(Type type)
        {
            if (!s_EnumValues.TryGetValue(type, out int[] values))
            {
                Array rawValues = Enum.GetValues(type);
                values = new int[rawValues.Length];
                for (int i = 0; i < rawValues.Length; i++)
                {
                    values[i] = (int)rawValues.GetValue(i);
                }
                s_EnumValues.Add(type, values);
            }

            return values;
        }

        private static string[] GetEnumNames(Type type)
        {
            if (!s_EnumNames.TryGetValue(type, out string[] names))
            {
                names = Enum.GetNames(type);
                s_EnumNames.Add(type, names);
            }

            return names;
        }
        public int EnumPopupLayoutEx(string label, Type type, int enumValueIndex, string[] labels)
        {
            int[] ints = GetEnumValues(type);
            string[] strings = GetEnumNames(type);
            if (labels.Length != ints.Length)
            {
                return EditorGUILayout.IntPopup(label, enumValueIndex, strings, ints);
            }
            else
            {
                return EditorGUILayout.IntPopup(label, enumValueIndex, labels, ints);
            }
        }
    }
}
