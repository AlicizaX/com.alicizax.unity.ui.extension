using System;

namespace UnityEngine.UI
{
    public class UXImage : Image
    {
        public enum ColorType
        {
            Solid_Color,
            Gradient_Color
        }

        [SerializeField] public ColorType m_ColorType = ColorType.Solid_Color;

        [SerializeField] private Gradient m_GradientColor = new Gradient()
        {
            colorKeys = new GradientColorKey[2]
            {
                new GradientColorKey(new Color(0, 0, 0), 0),
                new GradientColorKey(new Color(1, 1, 1), 1)
            },

            alphaKeys = new GradientAlphaKey[2]
            {
                new GradientAlphaKey(1, 0),
                new GradientAlphaKey(1, 1)
            }
        };

        public Gradient gradient
        {
            get { return m_GradientColor; }
            set
            {
                if (m_GradientColor == value)
                    return;

                m_GradientColor = value;
                CacheGradientKeys();
                SetVerticesDirty();
            }
        }

        public enum GradientDirection
        {
            Vertical,
            Horizontal
        }

        [SerializeField] private GradientDirection m_Direction = GradientDirection.Vertical;

        public GradientDirection Direction
        {
            get { return m_Direction; }
            set
            {
                if (m_Direction == value)
                    return;

                m_Direction = value;
                SetVerticesDirty();
            }
        }

        [SerializeField] private bool m_EnableOutline;
        [SerializeField] private bool m_EnableShadow;
        [SerializeField] private Color m_OutlineEffectColor = Color.black;
        [SerializeField] private Vector2 m_OutlineEffectDistance = new Vector2(1.5f, 1.5f);
        [SerializeField] private float m_OutlineSoftness = 0.5f;
        [SerializeField] private Color m_ShadowEffectColor = new Color(0f, 0f, 0f, 0.5f);
        [SerializeField] private Vector2 m_ShadowEffectDistance = new Vector2(2f, -2f);
        [SerializeField] private float m_ShadowSoftness = 1.5f;
        [SerializeField] private bool m_UseGraphicAlpha = true;

        public bool enableOutline
        {
            get { return m_EnableOutline; }
            set
            {
                if (m_EnableOutline == value)
                    return;

                m_EnableOutline = value;
                EnsureEffectCanvasChannels();
                SetVerticesDirty();
                SetMaterialDirty();
            }
        }

        public bool enableShadow
        {
            get { return m_EnableShadow; }
            set
            {
                if (m_EnableShadow == value)
                    return;

                m_EnableShadow = value;
                EnsureEffectCanvasChannels();
                SetVerticesDirty();
                SetMaterialDirty();
            }
        }

        public Color outlineEffectColor
        {
            get { return m_OutlineEffectColor; }
            set
            {
                if (m_OutlineEffectColor == value)
                    return;

                m_OutlineEffectColor = value;
                SetVerticesDirty();
                SetMaterialDirty();
            }
        }

        public Vector2 outlineEffectDistance
        {
            get { return m_OutlineEffectDistance; }
            set
            {
                if (m_OutlineEffectDistance == value)
                    return;

                m_OutlineEffectDistance = value;
                ResetRuntimeEffectCache();
                SetVerticesDirty();
            }
        }

        public float outlineSoftness
        {
            get { return m_OutlineSoftness; }
            set
            {
                value = Mathf.Max(value, 0f);
                if (Mathf.Approximately(m_OutlineSoftness, value))
                    return;

                m_OutlineSoftness = value;
                ResetRuntimeEffectCache();
                SetVerticesDirty();
            }
        }

        public Color shadowEffectColor
        {
            get { return m_ShadowEffectColor; }
            set
            {
                if (m_ShadowEffectColor == value)
                    return;

                m_ShadowEffectColor = value;
                SetVerticesDirty();
                SetMaterialDirty();
            }
        }

        public Vector2 shadowEffectDistance
        {
            get { return m_ShadowEffectDistance; }
            set
            {
                if (m_ShadowEffectDistance == value)
                    return;

                m_ShadowEffectDistance = value;
                ResetRuntimeEffectCache();
                SetVerticesDirty();
            }
        }

        public float shadowSoftness
        {
            get { return m_ShadowSoftness; }
            set
            {
                value = Mathf.Max(value, 0f);
                if (Mathf.Approximately(m_ShadowSoftness, value))
                    return;

                m_ShadowSoftness = value;
                ResetRuntimeEffectCache();
                SetVerticesDirty();
            }
        }

        public bool useGraphicAlpha
        {
            get { return m_UseGraphicAlpha; }
            set
            {
                if (m_UseGraphicAlpha == value)
                    return;

                m_UseGraphicAlpha = value;
                SetVerticesDirty();
            }
        }

        private bool HasAdaptiveEffect
        {
            get { return m_EnableOutline || m_EnableShadow; }
        }

        private UXImageRuntimeAtlas.Entry m_RuntimeAtlasEntry;
        private Sprite m_RuntimeAtlasSprite;
        private int m_RuntimeAtlasPadding;
        private Texture m_RuntimeSdfTexture;
        private Texture m_PendingRuntimeSdfTexture;
        private Texture m_RuntimeMaterialSdfTexture;

        public override Texture mainTexture
        {
            get
            {
                if (HasAdaptiveEffect && TryGetRuntimeAtlasEntry(out var entry))
                    return entry.Texture;

                return base.mainTexture;
            }
        }

        public override Material defaultMaterial
        {
            get
            {
                if (!HasAdaptiveEffect)
                    return base.defaultMaterial;

                Material effectMaterial = UXImageEffectSettings.GetEffectMaterial(m_RuntimeSdfTexture, m_OutlineEffectColor, m_ShadowEffectColor);
                return effectMaterial != null ? effectMaterial : base.defaultMaterial;
            }
        }

        //这个用于标记属于哪个镜像区域
        public override Material materialForRendering
        {
            get
            {
                if (!HasAdaptiveEffect)
                    return base.materialForRendering;

                if (m_PendingRuntimeSdfTexture != m_RuntimeMaterialSdfTexture)
                {
                    m_RuntimeSdfTexture = m_PendingRuntimeSdfTexture;
                    m_RuntimeMaterialSdfTexture = m_PendingRuntimeSdfTexture;
                }

                Material effectMaterial = UXImageEffectSettings.GetEffectMaterial(m_RuntimeSdfTexture, m_OutlineEffectColor, m_ShadowEffectColor);
                return effectMaterial != null ? GetModifiedMaterial(effectMaterial) : base.materialForRendering;
            }
        }

        public enum FlipPart
        {
            Part1 = 0,
            Part2 = 1,
            Part3 = 2,
            Part4 = 3,
        }

        public enum FlipDirection
        {
            None = 0,
            Horziontal = 1,
            Vertical = 2,
            FourCorner = 3,
            HorizontalNotCopy = 4,
            VerticalNotCopy = 5,
            DiagonalNotCopy = 6,
        }

        public enum FlipMode
        {
            None = 0,
            Horziontal = 1,
            Vertical = 2,
            FourCorner = 3,
        }

        public enum FlipEdge
        {
            None = -1,
            Left = 0,
            HorzMiddle = 1,
            Right = 2,
            Up = 3,
            VertMiddle = 4,
            Down = 5
        }

        public enum FlipEdgeHorizontal
        {
            Left = 0,
            Middle = 1,
            Right = 2,
        }

        public enum FlipEdgeVertical
        {
            Up = 3,
            Middle = 4,
            Down = 5
        }

        public enum FlipFillCenter
        {
            LeftTop,
            RightTop,
            RightBottom,
            LeftBottom
        }


        public FlipMode m_OriginFlipMode = FlipMode.None;

        public FlipMode m_FlipMode = FlipMode.None;

        public FlipMode flipMode
        {
            get { return m_FlipMode; }
            set
            {
                if (m_FlipMode == value)
                    return;

                m_FlipMode = value;
                SetVerticesDirty();
            }
        }

        public bool m_FlipWithCopy = true;

        public bool flipWithCopy
        {
            get { return m_FlipWithCopy; }
            set
            {
                if (m_FlipWithCopy == value)
                    return;

                m_FlipWithCopy = value;
                SetVerticesDirty();
            }
        }

        public FlipEdge flipEdge
        {
            get
            {
                if (m_FlipMode == FlipMode.Horziontal)
                {
                    return (FlipEdge)(int)m_FlipEdgeHorizontal;
                }

                if (m_FlipMode == FlipMode.Vertical)
                {
                    return (FlipEdge)(int)m_FlipEdgeVertical;
                }

                return FlipEdge.None;
            }
            set
            {
                if (m_FlipMode == FlipMode.Horziontal)
                {
                    FlipEdgeHorizontal next = value == FlipEdge.HorzMiddle ? FlipEdgeHorizontal.Middle : (FlipEdgeHorizontal)(int)value;
                    if (m_FlipEdgeHorizontal == next)
                        return;

                    m_FlipEdgeHorizontal = next;
                    SetVerticesDirty();
                }
                else if (m_FlipMode == FlipMode.Vertical)
                {
                    FlipEdgeVertical next = value == FlipEdge.VertMiddle ? FlipEdgeVertical.Middle : (FlipEdgeVertical)(int)value;
                    if (m_FlipEdgeVertical == next)
                        return;

                    m_FlipEdgeVertical = next;
                    SetVerticesDirty();
                }
            }
        }

        public FlipEdgeHorizontal m_FlipEdgeHorizontal = FlipEdgeHorizontal.Right;

        public FlipEdgeHorizontal flipEdgeHorizontal
        {
            get { return m_FlipEdgeHorizontal; }
            set
            {
                if (m_FlipEdgeHorizontal == value)
                    return;

                m_FlipEdgeHorizontal = value;
                SetVerticesDirty();
            }
        }

        public FlipEdgeVertical m_FlipEdgeVertical = FlipEdgeVertical.Down;

        public FlipEdgeVertical flipEdgeVertical
        {
            get { return m_FlipEdgeVertical; }
            set
            {
                if (m_FlipEdgeVertical == value)
                    return;

                m_FlipEdgeVertical = value;
                SetVerticesDirty();
            }
        }


        public FlipFillCenter m_FlipFillCenter = FlipFillCenter.LeftBottom;

        public FlipFillCenter flipFillCenter
        {
            get { return m_FlipFillCenter; }
            set
            {
                if (m_FlipFillCenter == value)
                    return;

                m_FlipFillCenter = value;
                SetVerticesDirty();
            }
        }


        [SerializeField] public FlipDirection m_FlipDirection = FlipDirection.FourCorner;

        public FlipDirection flipDirection
        {
            get { return m_FlipDirection; }
            set
            {
                if (m_FlipDirection == value)
                    return;

                m_FlipDirection = value;
                SetVerticesDirty();
            }
        }

        private Rect m_WorkRect;
        private UIVertex m_WorkVert;
        private static readonly Vector4 s_DefaultTangent = new Vector4(1.0f, 0.0f, 0.0f, -1.0f);
        private static readonly Vector3 s_DefaultNormal = Vector3.back;
        private const string ProfilerPopulateMesh = "UXImage.OnPopulateMesh";
        private const string ProfilerGenerateSimpleSprite = "UXImage.GenerateSimpleSprite";
        private const string ProfilerGenerateSprite = "UXImage.GenerateSprite";
        private GradientColorKey[] m_CachedColorKeys;
        private GradientAlphaKey[] m_CachedAlphaKeys;
        private readonly Vector2[] m_VertScratch = new Vector2[4];
        private readonly Vector2[] m_UvScratch = new Vector2[4];
        private readonly Vector3[] m_Xy = new Vector3[4];
        private readonly Vector3[] m_Uv = new Vector3[4];
        private readonly Vector3[] m_Uv1 = new Vector3[4];


        /// Image's dimensions used for drawing. X = left, Y = bottom, Z = right, W = top.
        private Vector4 GetDrawingDimensions(bool shouldPreserveAspect)
        {
            var padding = overrideSprite == null ? Vector4.zero : Sprites.DataUtility.GetPadding(overrideSprite);
            var size = overrideSprite == null ? new Vector2(rectTransform.rect.width, rectTransform.rect.height) : new Vector2(overrideSprite.rect.width, overrideSprite.rect.height);

            Rect r = GetPixelRectByFlipDirection(flipMode, flipWithCopy, flipEdge, flipFillCenter);

            int spriteW = Mathf.RoundToInt(size.x);
            int spriteH = Mathf.RoundToInt(size.y);

            var v = new Vector4(
                padding.x / spriteW,
                padding.y / spriteH,
                (spriteW - padding.z) / spriteW,
                (spriteH - padding.w) / spriteH);

            if (shouldPreserveAspect && size.sqrMagnitude > 0.0f)
            {
                PreserveSpriteAspectRatio(ref r, size);
            }

            v = new Vector4(
                r.x + r.width * v.x,
                r.y + r.height * v.y,
                r.x + r.width * v.z,
                r.y + r.height * v.w
            );

            return v;
        }

        private void ResizeByFlip()
        {
            if (flipMode == FlipMode.Horziontal && flipWithCopy)
            {
                RectTransform trans = transform as RectTransform;
                trans.sizeDelta = new Vector2(trans.sizeDelta.x * 2, trans.sizeDelta.y);
            }

            if (flipMode == FlipMode.Vertical && flipWithCopy)
            {
                RectTransform trans = transform as RectTransform;
                trans.sizeDelta = new Vector2(trans.sizeDelta.x, trans.sizeDelta.y * 2);
            }

            if (flipMode == FlipMode.FourCorner)
            {
                RectTransform trans = transform as RectTransform;
                trans.sizeDelta = new Vector2(trans.sizeDelta.x * 2, trans.sizeDelta.y * 2);
            }
        }

        protected void OnPopulateMesh1(VertexHelper toFill)
        {
            //ResizeByFlip();

            //if (overrideSprite == null)
            //{
            //    //support pure color fill
            //    switch (m_ColorFillType)
            //    {
            //        case ColorFillType.None:
            //            GenerateEmpytSprite(toFill);
            //            break;
            //        case ColorFillType.Filled:
            //            GenerateFilledSprite(toFill, preserveAspect);
            //            break;
            //    }
            //}
            //else
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Profiling.Profiler.BeginSample(ProfilerPopulateMesh);
#endif
                switch (type)
                {
                    case Type.Simple:
                        if (!useSpriteMesh)
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            UnityEngine.Profiling.Profiler.BeginSample(ProfilerGenerateSimpleSprite);
#endif
                            GenerateSimpleSprite(toFill, preserveAspect);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            UnityEngine.Profiling.Profiler.EndSample();
#endif
                        }
                        else
                        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            UnityEngine.Profiling.Profiler.BeginSample(ProfilerGenerateSprite);
#endif
                            GenerateSprite(toFill, preserveAspect);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            UnityEngine.Profiling.Profiler.EndSample();
#endif
                        }

                        break;
                    case Type.Sliced:
                        GenerateSlicedSprite(toFill);
                        break;
                    case Type.Tiled:
                        GenerateTiledSprite(toFill);
                        break;
                    case Type.Filled:
                        GenerateFilledSprite(toFill, preserveAspect);
                        break;
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Profiling.Profiler.EndSample();
#endif
            }

            m_WorkRect = GetDrawPixelAdjustedRect();

            if (flipMode == FlipMode.Horziontal)
            {
                if (flipWithCopy)
                {
                    CopyImage(toFill);
                    Rect src = GetPixelRectByFlipDirection(FlipMode.Horziontal, flipWithCopy, flipEdge, flipFillCenter);
                    Rect target = GetCopyRectByFlipDirection(src, FlipMode.Horziontal, flipEdge);
                    //Rect target = new Rect(src.position - new Vector2(src.width, 0), src.size);
                    RemapImage(toFill, FlipMode.Horziontal, toFill.currentVertCount / 2, toFill.currentVertCount, src.xMin, src.xMax, target.xMax, target.xMin);
                }
                else
                {
                    RemapImage(toFill, FlipMode.Horziontal, 0, toFill.currentVertCount, m_WorkRect.xMin, m_WorkRect.xMax, m_WorkRect.xMax, m_WorkRect.xMin);
                }
            }

            if (flipMode == FlipMode.Vertical)
            {
                if (flipWithCopy)
                {
                    CopyImage(toFill);
                    Rect src = GetPixelRectByFlipDirection(FlipMode.Vertical, flipWithCopy, flipEdge, flipFillCenter);
                    Rect target = GetCopyRectByFlipDirection(src, FlipMode.Vertical, flipEdge);
                    RemapImage(toFill, FlipMode.Vertical, toFill.currentVertCount / 2, toFill.currentVertCount, src.yMin, src.yMax, target.yMax, target.yMin);
                }
                else
                {
                    RemapImage(toFill, FlipMode.Vertical, 0, toFill.currentVertCount, m_WorkRect.xMin, m_WorkRect.xMax, m_WorkRect.xMax, m_WorkRect.xMin);
                }
            }

            if (flipMode == FlipMode.FourCorner)
            {
                //先水平拷�?
                CopyImage(toFill);
                Rect src = GetPixelRectByFlipDirection(FlipMode.FourCorner, true, flipEdge, flipFillCenter);
                Rect target = GetCopyRectByFlipCenter(src, FlipMode.Horziontal, flipFillCenter);
                //Rect target = new Rect(src.position + new Vector2(src.width, 0), src.size);
                RemapImage(toFill, FlipMode.Horziontal, toFill.currentVertCount / 2, toFill.currentVertCount, src.xMin, src.xMax, target.xMax, target.xMin);

                //再垂直拷�?
                CopyImage(toFill);
                Rect src1 = GetPixelRectByFlipDirection(FlipMode.FourCorner, true, flipEdge, flipFillCenter);
                Rect target1 = GetCopyRectByFlipCenter(src1, FlipMode.Vertical, flipFillCenter);
                RemapImage(toFill, FlipMode.Vertical, toFill.currentVertCount / 2, toFill.currentVertCount, src1.yMin, src1.yMax, target1.yMax, target1.yMin);
            }

            m_OriginFlipMode = m_FlipMode;
            /*
            if (flipDirection == FlipDirection.HorizontalNotCopy || flipDirection == FlipDirection.DiagonalNotCopy)
            {
                RemapImage(toFill, FlipDirection.Horziontal, 0, toFill.currentVertCount, rect.xMin, rect.xMax, rect.xMax, rect.xMin);
            }

            if (flipDirection == FlipDirection.VerticalNotCopy || flipDirection == FlipDirection.DiagonalNotCopy)
            {
                RemapImage(toFill, FlipDirection.Vertical, 0, toFill.currentVertCount, rect.yMin, rect.yMax, rect.yMax, rect.yMin);
            }

            if (flipDirection == FlipDirection.Horziontal || flipDirection == FlipDirection.FourCorner)
            {
                CopyImage(toFill);
                Rect src = GetPixelRectByFlipDirection(FlipDirection.Horziontal);
                Rect target = new Rect(src.position + new Vector2(src.width, 0), src.size);
                RemapImage(toFill, FlipDirection.Horziontal, toFill.currentVertCount / 2, toFill.currentVertCount, src.xMin, src.xMax, target.xMax, target.xMin);
            }
            if (flipDirection == FlipDirection.Vertical || flipDirection == FlipDirection.FourCorner)
            {
                CopyImage(toFill);
                Rect src = GetPixelRectByFlipDirection(FlipDirection.Vertical);
                Rect target = new Rect(src.position - new Vector2(0, src.height), src.size);
                RemapImage(toFill, FlipDirection.Vertical, toFill.currentVertCount / 2, toFill.currentVertCount, src.yMin, src.yMax, target.yMax, target.yMin);
            }
            */
        }

        #region Flip

        private FlipPart GetFlipPart(int index, int vertCount)
        {
            switch (flipDirection)
            {
                case FlipDirection.None:
                    return FlipPart.Part1;
                case FlipDirection.Horziontal:
                case FlipDirection.Vertical:
                    return index < vertCount / 2 ? FlipPart.Part1 : FlipPart.Part2;
                case FlipDirection.FourCorner:
                    if (index < vertCount / 4)
                    {
                        return FlipPart.Part1;
                    }
                    else if (index < vertCount / 2)
                    {
                        return FlipPart.Part2;
                    }
                    else if (index < vertCount * 3 / 4)
                    {
                        return FlipPart.Part3;
                    }
                    else
                    {
                        return FlipPart.Part4;
                    }
            }

            return FlipPart.Part1;
        }

        //TODO: 改变加点规则
        private void CopyImage(VertexHelper toFill)
        {
            int count = toFill.currentVertCount;

            for (int i = 0; i < count; ++i)
            {
                toFill.PopulateUIVertex(ref m_WorkVert, i % count);
                toFill.AddVert(m_WorkVert);
            }

            for (int i = count; i < 2 * count - 2; i += 4)
            {
                toFill.AddTriangle(i, i + 1, i + 2);
                toFill.AddTriangle(i + 2, i + 3, i);
            }
        }

        private void RemapImage(VertexHelper toFill, FlipMode flipMode, int indexMin, int indexMax, float Min1, float Max1, float Min2, float Max2)
        {
            for (int i = indexMin; i < indexMax; i++)
            {
                toFill.PopulateUIVertex(ref m_WorkVert, i);
                RemapVertex(ref m_WorkVert, flipMode, Min1, Max1, Min2, Max2);
                toFill.SetUIVertex(m_WorkVert, i);
            }
        }

        public void RemapVertex(ref UIVertex vertex, FlipMode flipMode, float Min1, float Max1, float Min2, float Max2)
        {
            Vector2 position = vertex.position;
            float k = (Min2 - Max2) / (Min1 - Max1);
            float b = Min2 - Min1 * k;
            //水平方向，左侧图像方向不变，右侧图像翻转
            if (flipMode == FlipMode.Horziontal)
            {
                vertex.position = new Vector2(position.x * k + b, position.y);
            }

            //垂直方向，上方图像方向不变，下方图像翻转
            if (flipMode == FlipMode.Vertical)
            {
                vertex.position = new Vector2(position.x, position.y * k + b);
            }
        }

        Rect GetDrawPixelAdjustedRect()
        {
            Rect rect = GetPixelAdjustedRect();
            return rect;
        }

        private Rect GetPixelRectByFlipDirection(FlipMode flipMode, bool copy, FlipEdge flipEdge, FlipFillCenter fillCenter)
        {
            Rect rect = GetDrawPixelAdjustedRect();
            return ModifyRectByFlipDirection(rect, flipMode, copy, flipEdge, fillCenter);
        }

        private Rect GetRectByFlipDirection(FlipMode flipMode, bool copy, FlipEdge flipEdge, FlipFillCenter fillCenter)
        {
            Rect rect = rectTransform.rect;
            return ModifyRectByFlipDirection(rect, flipMode, copy, flipEdge, fillCenter);
        }

        /// <summary>
        /// 修改原来的Rect
        /// 如果 flipMode == Horziontal �?Vertical, 根据copy, flipEdge来修改原本的Rect
        /// 如果 flipMode == FourCorner, 根据flipFillCenter 来修�?
        /// </summary>
        /// <param name="rect">Image原始Rect</param>
        private Rect ModifyRectByFlipDirection(Rect rect, FlipMode flipMode, bool copy, FlipEdge flipEdge, FlipFillCenter fillCenter)
        {
            if (flipMode == FlipMode.Horziontal)
            {
                if (copy)
                {
                    if (flipEdge == FlipEdge.Left)
                    {
                        rect = new Rect(rect.center.x, rect.yMin, rect.width / 2, rect.height);
                    }

                    if (flipEdge == FlipEdge.Right)
                    {
                        rect = new Rect(rect.xMin, rect.yMin, rect.width / 2, rect.height);
                    }
                }
            }

            if (flipMode == FlipMode.Vertical)
            {
                if (copy)
                {
                    if (flipEdge == FlipEdge.Up)
                    {
                        rect = new Rect(rect.xMin, rect.yMin, rect.width, rect.height / 2);
                    }

                    if (flipEdge == FlipEdge.Down)
                    {
                        rect = new Rect(rect.xMin, rect.center.y, rect.width, rect.height / 2);
                    }
                }
            }

            if (flipMode == FlipMode.FourCorner)
            {
                if (fillCenter == FlipFillCenter.LeftBottom)
                {
                    rect = new Rect(rect.center.x, rect.center.y, rect.width / 2, rect.height / 2);
                }

                if (fillCenter == FlipFillCenter.LeftTop)
                {
                    rect = new Rect(rect.center.x, rect.yMin, rect.width / 2, rect.height / 2);
                }

                if (fillCenter == FlipFillCenter.RightTop)
                {
                    rect = new Rect(rect.xMin, rect.yMin, rect.width / 2, rect.height / 2);
                }

                if (fillCenter == FlipFillCenter.RightBottom)
                {
                    rect = new Rect(rect.xMin, rect.center.y, rect.width / 2, rect.height / 2);
                }
            }

            return rect;
        }

        /// <summary>
        /// 获取Copy出来的Rect
        /// 这里Src应该是Modify过的原Rect
        /// 只处理flipMode == Horziontal �?Vertical的情�?
        /// </summary>
        /// <param name="src"></param>
        /// <param name="flipMode"></param>
        /// <param name="flipEdge"></param>
        /// <returns></returns>
        private Rect GetCopyRectByFlipDirection(Rect src, FlipMode flipMode, FlipEdge flipEdge)
        {
            Rect target = new Rect();
            if (flipMode == FlipMode.None)
            {
                target = src;
            }

            if (flipMode == FlipMode.Horziontal)
            {
                if (flipEdge == FlipEdge.Left)
                {
                    target = new Rect(src.position - new Vector2(src.width, 0), src.size);
                }

                if (flipEdge == FlipEdge.HorzMiddle)
                {
                    target = src;
                }

                if (flipEdge == FlipEdge.Right)
                {
                    target = new Rect(src.position + new Vector2(src.width, 0), src.size);
                }
            }

            if (flipMode == FlipMode.Vertical)
            {
                if (flipEdge == FlipEdge.Up)
                {
                    target = new Rect(src.position + new Vector2(0, src.height), src.size);
                }

                if (flipEdge == FlipEdge.VertMiddle)
                {
                    target = src;
                }

                if (flipEdge == FlipEdge.Down)
                {
                    target = new Rect(src.position - new Vector2(0, src.height), src.size);
                }
            }

            return target;
        }

        /// <summary>
        /// 获取Copy出来的Rect�?flipMode == FourCorner时专�?
        /// flipMode == FourCorner时会转换成两次Copy
        /// 分别调用GetCopyRectByFlipDirection
        /// 这里Src应该是Modify过的原Rect
        /// </summary>
        /// <param name="src"></param>
        /// <param name="flipMode"></param>
        /// <param name="fillCenter"></param>
        /// <returns></returns>
        private Rect GetCopyRectByFlipCenter(Rect src, FlipMode flipMode, FlipFillCenter fillCenter)
        {
            Rect target = new Rect();
            if (flipMode == FlipMode.Horziontal)
            {
                if (fillCenter == FlipFillCenter.LeftBottom || fillCenter == FlipFillCenter.LeftTop)
                {
                    target = GetCopyRectByFlipDirection(src, FlipMode.Horziontal, FlipEdge.Left);
                }
                else
                {
                    target = GetCopyRectByFlipDirection(src, FlipMode.Horziontal, FlipEdge.Right);
                }
            }

            if (flipMode == FlipMode.Vertical)
            {
                if (fillCenter == FlipFillCenter.LeftTop || fillCenter == FlipFillCenter.RightTop)
                {
                    target = GetCopyRectByFlipDirection(src, FlipMode.Vertical, FlipEdge.Up);
                }
                else
                {
                    target = GetCopyRectByFlipDirection(src, FlipMode.Vertical, FlipEdge.Down);
                }
            }

            return target;
        }

        private void PreserveSpriteAspectRatio(ref Rect r, Vector2 size)
        {
            var spriteRatio = size.x / size.y;
            var rectRatio = r.width / r.height;

            if (spriteRatio > rectRatio)
            {
                var oldHeight = r.height;
                r.height = r.width * (1.0f / spriteRatio);
                r.y += (oldHeight - r.height) * rectTransform.pivot.y;
            }
            else
            {
                var oldWidth = r.width;
                r.width = r.height * spriteRatio;
                r.x += (oldWidth - r.width) * rectTransform.pivot.x;
            }
        }

        private void GenerateSimpleSprite(VertexHelper vh, bool lPreserveAspect)
        {
            Vector4 v = GetDrawingDimensions(lPreserveAspect);
            var uv = (overrideSprite != null) ? Sprites.DataUtility.GetOuterUV(overrideSprite) : Vector4.zero;
            vh.Clear();

            AddQuad(vh, new Vector2(v.x, v.y), new Vector2(v.z, v.w), color, new Vector2(uv.x, uv.y), new Vector2(uv.z, uv.w), Vector2.zero, Vector2.one);
        }

        #endregion

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            OnPopulateMesh1(toFill);

            if (m_ColorType == ColorType.Gradient_Color)
            {
                ApplyGradientColor(toFill);
            }

            if (HasAdaptiveEffect)
            {
                ApplyAdaptiveEffectData(toFill);
            }
        }

        private void CacheGradientKeys()
        {
            if (m_GradientColor == null)
            {
                m_CachedColorKeys = null;
                m_CachedAlphaKeys = null;
                return;
            }

            m_CachedColorKeys = m_GradientColor.colorKeys;
            m_CachedAlphaKeys = m_GradientColor.alphaKeys;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            CacheGradientKeys();
            EnsureEffectCanvasChannels();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            CacheGradientKeys();
            m_OutlineSoftness = Mathf.Max(m_OutlineSoftness, 0f);
            m_ShadowSoftness = Mathf.Max(m_ShadowSoftness, 0f);
            ResetRuntimeEffectCache(false);
            EnsureEffectCanvasChannels();
        }
#endif

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            EnsureEffectCanvasChannels();
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            EnsureEffectCanvasChannels();
        }

        private void ApplyGradientColor(VertexHelper toFill)
        {
            int count = toFill.currentVertCount;
            if (count == 0)
                return;

            if (m_CachedColorKeys == null || m_CachedAlphaKeys == null)
                CacheGradientKeys();

            Rect bounds = GetGradientBounds(toFill, count);
            float inverseSize = m_Direction == GradientDirection.Horizontal ? bounds.width : bounds.height;
            if (Mathf.Approximately(inverseSize, 0f))
                inverseSize = 1f;
            else
                inverseSize = 1f / inverseSize;

            for (int i = 0; i < count; i++)
            {
                toFill.PopulateUIVertex(ref m_WorkVert, i);
                float time = m_Direction == GradientDirection.Horizontal
                    ? (m_WorkVert.position.x - bounds.xMin) * inverseSize
                    : (m_WorkVert.position.y - bounds.yMin) * inverseSize;
                m_WorkVert.color = EvaluateCachedGradient(Mathf.Clamp01(time));
                toFill.SetUIVertex(m_WorkVert, i);
            }
        }

        private Rect GetGradientBounds(VertexHelper toFill, int count)
        {
            toFill.PopulateUIVertex(ref m_WorkVert, 0);
            float minX = m_WorkVert.position.x;
            float maxX = minX;
            float minY = m_WorkVert.position.y;
            float maxY = minY;

            for (int i = 1; i < count; i++)
            {
                toFill.PopulateUIVertex(ref m_WorkVert, i);
                Vector3 position = m_WorkVert.position;
                if (position.x < minX) minX = position.x;
                if (position.x > maxX) maxX = position.x;
                if (position.y < minY) minY = position.y;
                if (position.y > maxY) maxY = position.y;
            }

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        private Color32 EvaluateCachedGradient(float time)
        {
            Color result = EvaluateColor(time);
            result.a = EvaluateAlpha(time);
            return result;
        }

        private void ApplyAdaptiveEffectData(VertexHelper toFill)
        {
            int count = toFill.currentVertCount;
            if (count == 0)
                return;

            Sprite sprite = overrideSprite;
            float outlineEnabled = m_EnableOutline ? 1f : 0f;
            float shadowEnabled = m_EnableShadow ? 1f : 0f;
            Vector2 outlineDistance = m_OutlineEffectDistance;
            float outlineSoftness = Mathf.Max(m_OutlineSoftness, 0.001f);
            float shadowSoftness = Mathf.Max(m_ShadowSoftness, 0.001f);
            Vector2 shadowOffset = m_ShadowEffectDistance;
            float useRuntimeSdf = 0f;
            Vector2 spriteTexelSize;
            Vector4 sampleRect;
            Vector4 sdfRect = Vector4.zero;
            m_PendingRuntimeSdfTexture = null;
            bool usingRuntimeAtlas = false;

            if (TryGetRuntimeAtlasEntry(out var atlasEntry))
            {
                usingRuntimeAtlas = true;
                spriteTexelSize = atlasEntry.TexelSize;
                sampleRect = GetPaddedRuntimeAtlasRect(atlasEntry);
                RemapVerticesToRuntimeAtlas(toFill, count, atlasEntry);
            }
            else
            {
                Texture texture = sprite != null ? sprite.texture : null;
                Vector2 textureSize = texture != null
                    ? new Vector2(texture.width, texture.height)
                    : Vector2.one;

                sampleRect = sprite != null
                    ? Sprites.DataUtility.GetOuterUV(sprite)
                    : new Vector4(0f, 0f, 1f, 1f);

                float uvWidth = Mathf.Abs(sampleRect.z - sampleRect.x);
                float uvHeight = Mathf.Abs(sampleRect.w - sampleRect.y);
                spriteTexelSize = new Vector2(
                    uvWidth > 0f ? 1f / (uvWidth * textureSize.x) : 0f,
                    uvHeight > 0f ? 1f / (uvHeight * textureSize.y) : 0f);
            }

            if (usingRuntimeAtlas)
                ExpandSimpleEffectGeometry(toFill, count, spriteTexelSize);

            if (usingRuntimeAtlas && m_EnableShadow && UXImageRuntimeSDFCache.ShouldUse(shadowSoftness) &&
                UXImageRuntimeSDFCache.TryGetOrRequest(sprite, shadowSoftness, atlasEntry.Padding, out var sdfEntry) && sdfEntry.IsValid)
            {
                useRuntimeSdf = 1f;
                sdfRect = sdfEntry.UvRect;
                m_PendingRuntimeSdfTexture = sdfEntry.Texture;
            }

            if (useRuntimeSdf > 0f)
                shadowEnabled = 2f;

            Vector4 uv2 = new Vector4(outlineEnabled, shadowEnabled, m_UseGraphicAlpha ? 1f : 0f, 0f);
            Vector4 uv3 = new Vector4(outlineDistance.x, outlineDistance.y, shadowOffset.x, shadowOffset.y);
            Vector4 tangent = new Vector4(
                shadowSoftness,
                sdfRect.w,
                0f,
                outlineSoftness);

            for (int i = 0; i < count; i++)
            {
                toFill.PopulateUIVertex(ref m_WorkVert, i);
                m_WorkVert.uv1 = sampleRect;
                m_WorkVert.uv2 = uv2;
                m_WorkVert.uv3 = uv3;
                m_WorkVert.tangent = tangent;
                m_WorkVert.normal = new Vector3(sdfRect.x, sdfRect.y, sdfRect.z);
                toFill.SetUIVertex(m_WorkVert, i);
            }
        }

        private void EnsureEffectCanvasChannels()
        {
            if (HasAdaptiveEffect)
                UXImageEffectSettings.EnsureCanvasChannels(canvas);
        }

        private void ResetRuntimeEffectCache(bool setMaterialDirty = true)
        {
            m_RuntimeAtlasEntry = default;
            m_RuntimeAtlasSprite = null;
            m_RuntimeAtlasPadding = 0;
            m_PendingRuntimeSdfTexture = null;
            m_RuntimeSdfTexture = null;
            m_RuntimeMaterialSdfTexture = null;
            if (setMaterialDirty)
                SetMaterialDirty();
        }

        private bool TryGetRuntimeAtlasEntry(out UXImageRuntimeAtlas.Entry entry)
        {
            entry = default;
            if (!HasAdaptiveEffect || type == Type.Tiled)
                return false;

            Sprite sprite = overrideSprite;
            int padding = GetEffectPadding();
            if (m_RuntimeAtlasSprite == sprite && m_RuntimeAtlasPadding >= padding && m_RuntimeAtlasEntry.IsValid)
            {
                entry = m_RuntimeAtlasEntry;
                return true;
            }

            if (!UXImageRuntimeAtlas.TryGet(sprite, padding, out entry))
                return false;

            m_RuntimeAtlasSprite = sprite;
            m_RuntimeAtlasPadding = padding;
            m_RuntimeAtlasEntry = entry;

            return true;
        }

        private static Vector4 GetPaddedRuntimeAtlasRect(UXImageRuntimeAtlas.Entry entry)
        {
            Vector2 padding = entry.TexelSize * entry.Padding;
            Vector4 rect = entry.SourceUvRect;
            rect.x -= padding.x;
            rect.y -= padding.y;
            rect.z += padding.x;
            rect.w += padding.y;
            return rect;
        }

        private int GetEffectPadding()
        {
            float padding = 0f;
            if (m_EnableOutline)
            {
                padding = Mathf.Max(padding, Mathf.Abs(m_OutlineEffectDistance.x) + m_OutlineSoftness);
                padding = Mathf.Max(padding, Mathf.Abs(m_OutlineEffectDistance.y) + m_OutlineSoftness);
            }
            if (m_EnableShadow)
            {
                padding = Mathf.Max(padding, Mathf.Abs(m_ShadowEffectDistance.x) + m_ShadowSoftness);
                padding = Mathf.Max(padding, Mathf.Abs(m_ShadowEffectDistance.y) + m_ShadowSoftness);
            }

            return Mathf.CeilToInt(padding) + 2;
        }

        private void RemapVerticesToRuntimeAtlas(VertexHelper toFill, int count, UXImageRuntimeAtlas.Entry entry)
        {
            Vector4 source = overrideSprite != null
                ? Sprites.DataUtility.GetOuterUV(overrideSprite)
                : new Vector4(0f, 0f, 1f, 1f);
            Vector4 target = entry.SourceUvRect;
            float sourceWidth = source.z - source.x;
            float sourceHeight = source.w - source.y;

            if (Mathf.Approximately(sourceWidth, 0f) || Mathf.Approximately(sourceHeight, 0f))
                return;

            for (int i = 0; i < count; i++)
            {
                toFill.PopulateUIVertex(ref m_WorkVert, i);
                Vector2 uv = m_WorkVert.uv0;
                float u = Mathf.InverseLerp(source.x, source.z, uv.x);
                float v = Mathf.InverseLerp(source.y, source.w, uv.y);
                m_WorkVert.uv0 = new Vector2(
                    Mathf.Lerp(target.x, target.z, u),
                    Mathf.Lerp(target.y, target.w, v));
                toFill.SetUIVertex(m_WorkVert, i);
            }
        }

        private void ExpandSimpleEffectGeometry(VertexHelper toFill, int count, Vector2 spriteTexelSize)
        {
            if (type != Type.Simple || count != 4)
                return;

            float padding = GetEffectPadding();
            if (padding <= 0f)
                return;

            Rect bounds = GetGradientBounds(toFill, count);
            if (Mathf.Approximately(bounds.width, 0f) || Mathf.Approximately(bounds.height, 0f))
                return;

            for (int i = 0; i < count; i++)
            {
                toFill.PopulateUIVertex(ref m_WorkVert, i);
                Vector3 position = m_WorkVert.position;
                float xSign = position.x < bounds.center.x ? -1f : 1f;
                float ySign = position.y < bounds.center.y ? -1f : 1f;
                position.x += xSign * padding;
                position.y += ySign * padding;
                m_WorkVert.position = position;
                m_WorkVert.uv0 += new Vector4(
                    xSign * padding * spriteTexelSize.x,
                    ySign * padding * spriteTexelSize.y,
                    0f,
                    0f);
                toFill.SetUIVertex(m_WorkVert, i);
            }
        }

        private Color EvaluateColor(float time)
        {
            GradientColorKey[] keys = m_CachedColorKeys;
            if (keys == null || keys.Length == 0)
                return color;

            GradientColorKey first = keys[0];
            if (time <= first.time)
                return first.color;

            for (int i = 1; i < keys.Length; i++)
            {
                GradientColorKey next = keys[i];
                if (time <= next.time)
                {
#if UNITY_2022_2_OR_NEWER
                    if (m_GradientColor.mode == GradientMode.Blend || m_GradientColor.mode == GradientMode.PerceptualBlend)
#else
                    if (m_GradientColor.mode == GradientMode.Blend)
#endif
                    {
                        GradientColorKey previous = keys[i - 1];
                        float span = next.time - previous.time;
                        if (Mathf.Approximately(span, 0f))
                            return previous.color;

                        return Color.Lerp(previous.color, next.color, (time - previous.time) / span);
                    }

                    return next.color;
                }
            }

            return keys[keys.Length - 1].color;
        }

        private float EvaluateAlpha(float time)
        {
            GradientAlphaKey[] keys = m_CachedAlphaKeys;
            if (keys == null || keys.Length == 0)
                return color.a;

            GradientAlphaKey first = keys[0];
            if (time <= first.time)
                return first.alpha;

            for (int i = 1; i < keys.Length; i++)
            {
                GradientAlphaKey next = keys[i];
                if (time <= next.time)
                {
#if UNITY_2022_2_OR_NEWER
                    if (m_GradientColor.mode == GradientMode.Blend || m_GradientColor.mode == GradientMode.PerceptualBlend)
#else
                    if (m_GradientColor.mode == GradientMode.Blend)
#endif
                    {
                        GradientAlphaKey previous = keys[i - 1];
                        float span = next.time - previous.time;
                        if (Mathf.Approximately(span, 0f))
                            return previous.alpha;

                        return Mathf.Lerp(previous.alpha, next.alpha, (time - previous.time) / span);
                    }

                    return next.alpha;
                }
            }

            return keys[keys.Length - 1].alpha;
        }

        private void GenerateSprite(VertexHelper vh, bool lPreserveAspect)
        {
            var spriteSize = new Vector2(overrideSprite.rect.width, overrideSprite.rect.height);

            // Covert sprite pivot into normalized space.
            var spritePivot = overrideSprite.pivot / spriteSize;
            var rectPivot = rectTransform.pivot;
            Rect r = GetPixelAdjustedRect();

            if (lPreserveAspect & spriteSize.sqrMagnitude > 0.0f)
            {
                PreserveSpriteAspectRatio(ref r, spriteSize);
            }

            var drawingSize = new Vector2(r.width, r.height);
            var spriteBoundSize = overrideSprite.bounds.size;

            // Calculate the drawing offset based on the difference between the two pivots.
            var drawOffset = (rectPivot - spritePivot) * drawingSize;

            var color32 = color;
            vh.Clear();

            Vector2[] vertices = overrideSprite.vertices;
            Vector2[] uvs = overrideSprite.uv;
            for (int i = 0; i < vertices.Length; ++i)
            {
                vh.AddVert(new Vector3((vertices[i].x / spriteBoundSize.x) * drawingSize.x - drawOffset.x, (vertices[i].y / spriteBoundSize.y) * drawingSize.y - drawOffset.y), color32, new Vector2(uvs[i].x, uvs[i].y));
            }

            UInt16[] triangles = overrideSprite.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                vh.AddTriangle(triangles[i + 0], triangles[i + 1], triangles[i + 2]);
            }
        }

        private void GenerateSlicedSprite(VertexHelper toFill)
        {
            if (!hasBorder)
            {
                //GenerateSimpleSprite(toFill, vlist, false);
                GenerateSimpleSprite(toFill, false);
                return;
            }

            Vector4 outer, inner, padding, border;

            if (overrideSprite != null)
            {
                outer = Sprites.DataUtility.GetOuterUV(overrideSprite);
                inner = Sprites.DataUtility.GetInnerUV(overrideSprite);
                padding = Sprites.DataUtility.GetPadding(overrideSprite);
                border = overrideSprite.border;
            }
            else
            {
                outer = Vector4.zero;
                inner = Vector4.zero;
                padding = Vector4.zero;
                border = Vector4.zero;
            }

            Rect rect = GetPixelRectByFlipDirection(flipMode, flipWithCopy, flipEdge, flipFillCenter);

            border = GetAdjustedBorders(border / pixelsPerUnit, rect);
            padding = padding / pixelsPerUnit;

            m_VertScratch[0] = new Vector2(padding.x, padding.y);
            m_VertScratch[3] = new Vector2(rect.width - padding.z, rect.height - padding.w);


            m_VertScratch[1].x = border.x;
            m_VertScratch[1].y = border.y;
            m_VertScratch[2].x = rect.width - border.z;
            m_VertScratch[2].y = rect.height - border.w;

            float vertWidth = m_VertScratch[3].x - m_VertScratch[0].x;
            float vertHeight = m_VertScratch[3].y - m_VertScratch[0].y;


            for (int i = 0; i < 4; ++i)
            {
                m_VertScratch[i].x += rect.x;
                m_VertScratch[i].y += rect.y;
            }

            m_UvScratch[0] = new Vector2(outer.x, outer.y);
            m_UvScratch[1] = new Vector2(inner.x, inner.y);
            m_UvScratch[2] = new Vector2(inner.z, inner.w);
            m_UvScratch[3] = new Vector2(outer.z, outer.w);

            toFill.Clear();

            for (int x = 0; x < 3; ++x)
            {
                int x2 = x + 1;

                for (int y = 0; y < 3; ++y)
                {
                    if (!fillCenter && x == 1 && y == 1)
                        continue;

                    int y2 = y + 1;

                    Vector2 uv1Min = new Vector2((m_VertScratch[x].x - rect.x) / vertWidth, (m_VertScratch[y].y - rect.y) / vertHeight);
                    Vector2 uv1Max = new Vector2((m_VertScratch[x2].x - rect.x) / vertWidth, (m_VertScratch[y2].y - rect.y) / vertHeight);
                    AddQuad(toFill,
                        new Vector2(m_VertScratch[x].x, m_VertScratch[y].y),
                        new Vector2(m_VertScratch[x2].x, m_VertScratch[y2].y),
                        color,
                        new Vector2(m_UvScratch[x].x, m_UvScratch[y].y),
                        new Vector2(m_UvScratch[x2].x, m_UvScratch[y2].y),
                        uv1Min, uv1Max);
                }
            }
        }

        private void GenerateTiledSprite(VertexHelper toFill)
        {
            Vector4 outer, inner, border;
            Vector2 spriteSize;

            if (overrideSprite != null)
            {
                outer = Sprites.DataUtility.GetOuterUV(overrideSprite);
                inner = Sprites.DataUtility.GetInnerUV(overrideSprite);
                border = overrideSprite.border;
                spriteSize = overrideSprite.rect.size;
            }
            else
            {
                outer = Vector4.zero;
                inner = Vector4.zero;
                border = Vector4.zero;
                spriteSize = Vector2.one * 100;
            }

            Rect rect = GetPixelRectByFlipDirection(flipMode, flipWithCopy, flipEdge, flipFillCenter);

            float tileWidth = (spriteSize.x - border.x - border.z) / pixelsPerUnit;
            float tileHeight = (spriteSize.y - border.y - border.w) / pixelsPerUnit;
            border = GetAdjustedBorders(border / pixelsPerUnit, rect);


            var uvMin = new Vector2(inner.x, inner.y);
            var uvMax = new Vector2(inner.z, inner.w);

            // Min to max max range for tiled region in coordinates relative to lower left corner.
            float xMin = border.x;
            float xMax = rect.width - border.z;
            float yMin = border.y;
            float yMax = rect.height - border.w;

            toFill.Clear();
            var clipped = uvMax;

            // if either width is zero we cant tile so just assume it was the full width.
            if (tileWidth <= 0)
                tileWidth = xMax - xMin;

            if (tileHeight <= 0)
                tileHeight = yMax - yMin;

            if (overrideSprite != null && (hasBorder || overrideSprite.packed || overrideSprite.texture.wrapMode != TextureWrapMode.Repeat))
            {
                // Sprite has border, or is not in repeat mode, or cannot be repeated because of packing.
                // We cannot use texture tiling so we will generate a mesh of quads to tile the texture.

                // Evaluate how many vertices we will generate. Limit this number to something sane,
                // especially since meshes can not have more than 65000 vertices.

                int nTilesW = 0;
                int nTilesH = 0;
                if (fillCenter)
                {
                    nTilesW = (int)Math.Ceiling((xMax - xMin) / tileWidth);
                    nTilesH = (int)Math.Ceiling((yMax - yMin) / tileHeight);

                    int nVertices = 0;
                    if (hasBorder)
                    {
                        nVertices = (nTilesW + 2) * (nTilesH + 2) * 4; // 4 vertices per tile
                    }
                    else
                    {
                        nVertices = nTilesW * nTilesH * 4; // 4 vertices per tile
                    }

                    if (nVertices > 65000)
                    {
                        double maxTiles = 65000.0 / 4.0; // Max number of vertices is 65000; 4 vertices per tile.
                        double imageRatio;
                        if (hasBorder)
                        {
                            imageRatio = (nTilesW + 2.0) / (nTilesH + 2.0);
                        }
                        else
                        {
                            imageRatio = (double)nTilesW / nTilesH;
                        }

                        double targetTilesW = Math.Sqrt(maxTiles / imageRatio);
                        double targetTilesH = targetTilesW * imageRatio;
                        if (hasBorder)
                        {
                            targetTilesW -= 2;
                            targetTilesH -= 2;
                        }

                        nTilesW = (int)Math.Floor(targetTilesW);
                        nTilesH = (int)Math.Floor(targetTilesH);

                        tileWidth = (xMax - xMin) / nTilesW;
                        tileHeight = (yMax - yMin) / nTilesH;
                    }
                }
                else
                {
                    if (hasBorder)
                    {
                        // Texture on the border is repeated only in one direction.
                        nTilesW = (int)Math.Ceiling((xMax - xMin) / tileWidth);
                        nTilesH = (int)Math.Ceiling((yMax - yMin) / tileHeight);
                        int nVertices = (nTilesH + nTilesW + 2 /*corners*/) * 2 /*sides*/ * 4 /*vertices per tile*/;
                        if (nVertices > 65000)
                        {
                            double maxTiles = 65000.0 / 4.0; // Max number of vertices is 65000; 4 vertices per tile.
                            double imageRatio = (double)nTilesW / nTilesH;
                            double targetTilesW = (maxTiles - 4 /*corners*/) / (2 * (1.0 + imageRatio));
                            double targetTilesH = targetTilesW * imageRatio;

                            nTilesW = (int)Math.Floor(targetTilesW);
                            nTilesH = (int)Math.Floor(targetTilesH);
                            tileWidth = (xMax - xMin) / nTilesW;
                            tileHeight = (yMax - yMin) / nTilesH;
                        }
                    }
                    else
                    {
                        nTilesH = nTilesW = 0;
                    }
                }

                if (fillCenter)
                {
                    // TODO: we could share vertices between quads. If vertex sharing is implemented. update the computation for the number of vertices accordingly.
                    float width = nTilesW * tileWidth;
                    float height = nTilesH * tileHeight;
                    for (int j = 0; j < nTilesH; j++)
                    {
                        float y1 = yMin + j * tileHeight;
                        float y2 = yMin + (j + 1) * tileHeight;
                        if (y2 > yMax)
                        {
                            clipped.y = uvMin.y + (uvMax.y - uvMin.y) * (yMax - y1) / (y2 - y1);
                            y2 = yMax;
                        }

                        clipped.x = uvMax.x;
                        for (int i = 0; i < nTilesW; i++)
                        {
                            float x1 = xMin + i * tileWidth;
                            float x2 = xMin + (i + 1) * tileWidth;
                            if (x2 > xMax)
                            {
                                clipped.x = uvMin.x + (uvMax.x - uvMin.x) * (xMax - x1) / (x2 - x1);
                                x2 = xMax;
                            }

                            Vector2 posMin = new Vector2(x1, y1) + rect.position;
                            Vector2 posMax = new Vector2(x2, y2) + rect.position;
                            Vector2 localPosMin = new Vector2(x1, y1);
                            Vector2 localPosMax = new Vector2(x2, y2);
                            AddQuad(toFill, posMin, posMax, color, uvMin, clipped, new Vector2(localPosMin.x / width, localPosMin.y / height), new Vector2(localPosMax.x / width, localPosMax.y / height));
                        }
                    }
                }

                if (hasBorder)
                {
                    clipped = uvMax;
                    float width = nTilesW * tileWidth;
                    float height = nTilesH * tileHeight;
                    for (int j = 0; j < nTilesH; j++)
                    {
                        float y1 = yMin + j * tileHeight;
                        float y2 = yMin + (j + 1) * tileHeight;
                        if (y2 > yMax)
                        {
                            clipped.y = uvMin.y + (uvMax.y - uvMin.y) * (yMax - y1) / (y2 - y1);
                            y2 = yMax;
                        }

                        AddQuad(toFill,
                            new Vector2(0, y1) + rect.position,
                            new Vector2(xMin, y2) + rect.position,
                            color,
                            new Vector2(outer.x, uvMin.y),
                            new Vector2(uvMin.x, clipped.y),
                            new Vector2(0, y1 / height),
                            new Vector2(xMin / width, y2 / height));
                        AddQuad(toFill,
                            new Vector2(xMax, y1) + rect.position,
                            new Vector2(rect.width, y2) + rect.position,
                            color,
                            new Vector2(uvMax.x, uvMin.y),
                            new Vector2(outer.z, clipped.y),
                            new Vector2(xMax / width, y1 / height),
                            new Vector2(rect.width / width, y2 / height));
                    }

                    // Bottom and top tiled border
                    clipped = uvMax;
                    for (int i = 0; i < nTilesW; i++)
                    {
                        float x1 = xMin + i * tileWidth;
                        float x2 = xMin + (i + 1) * tileWidth;
                        if (x2 > xMax)
                        {
                            clipped.x = uvMin.x + (uvMax.x - uvMin.x) * (xMax - x1) / (x2 - x1);
                            x2 = xMax;
                        }

                        AddQuad(toFill,
                            new Vector2(x1, 0) + rect.position,
                            new Vector2(x2, yMin) + rect.position,
                            color,
                            new Vector2(uvMin.x, outer.y),
                            new Vector2(clipped.x, uvMin.y),
                            new Vector2(x1 / width, 0),
                            new Vector2(x2 / width, yMin / height));
                        AddQuad(toFill,
                            new Vector2(x1, yMax) + rect.position,
                            new Vector2(x2, rect.height) + rect.position,
                            color,
                            new Vector2(uvMin.x, uvMax.y),
                            new Vector2(clipped.x, outer.w),
                            new Vector2(x1 / width, yMax / height),
                            new Vector2(x2 / width, rect.height / height));
                    }

                    // Corners
                    AddQuad(toFill,
                        new Vector2(0, 0) + rect.position,
                        new Vector2(xMin, yMin) + rect.position,
                        color,
                        new Vector2(outer.x, outer.y),
                        new Vector2(uvMin.x, uvMin.y),
                        new Vector2(0, 0),
                        new Vector2(xMin / width, yMin / height));
                    AddQuad(toFill,
                        new Vector2(xMax, 0) + rect.position,
                        new Vector2(rect.width, yMin) + rect.position,
                        color,
                        new Vector2(uvMax.x, outer.y),
                        new Vector2(outer.z, uvMin.y),
                        new Vector2(xMax / width, 0),
                        new Vector2(rect.width / width, yMin / height));
                    AddQuad(toFill,
                        new Vector2(0, yMax) + rect.position,
                        new Vector2(xMin, rect.height) + rect.position,
                        color,
                        new Vector2(outer.x, uvMax.y),
                        new Vector2(uvMin.x, outer.w),
                        new Vector2(0, yMax / height),
                        new Vector2(xMin / width, rect.height / height));
                    AddQuad(toFill,
                        new Vector2(xMax, yMax) + rect.position,
                        new Vector2(rect.width, rect.height) + rect.position,
                        color,
                        new Vector2(uvMax.x, uvMax.y),
                        new Vector2(outer.z, outer.w),
                        new Vector2(xMax / width, yMax / height),
                        new Vector2(rect.width / width, rect.height / height));
                }
            }
            else
            {
                // Texture has no border, is in repeat mode and not packed. Use texture tiling.
                Vector2 uvScale = new Vector2((xMax - xMin) / tileWidth, (yMax - yMin) / tileHeight);

                if (fillCenter)
                {
                    AddQuad(toFill, new Vector2(xMin, yMin) + rect.position, new Vector2(xMax, yMax) + rect.position, color, Vector2.Scale(uvMin, uvScale), Vector2.Scale(uvMax, uvScale), Vector2.zero, Vector2.one);
                }
            }
        }

        static void AddQuad(VertexHelper vertexHelper, Vector3[] quadPositions, Color32 color, Vector3[] quadUVs, Vector3[] quadUV1s)
        {
            int startIndex = vertexHelper.currentVertCount;

            for (int i = 0; i < 4; ++i)
                vertexHelper.AddVert(quadPositions[i], color, quadUVs[i], quadUV1s[i], s_DefaultNormal, s_DefaultTangent);

            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
        }

        static void AddQuad(VertexHelper vertexHelper, Vector2 posMin, Vector2 posMax, Color32 color, Vector2 uvMin, Vector2 uvMax, Vector2 uv1Min, Vector2 uv1Max)
        {
            int startIndex = vertexHelper.currentVertCount;

            vertexHelper.AddVert(new Vector3(posMin.x, posMin.y, 0), color, new Vector2(uvMin.x, uvMin.y), new Vector2(uv1Min.x, uv1Min.y), s_DefaultNormal, s_DefaultTangent);
            vertexHelper.AddVert(new Vector3(posMin.x, posMax.y, 0), color, new Vector2(uvMin.x, uvMax.y), new Vector2(uv1Min.x, uv1Max.y), s_DefaultNormal, s_DefaultTangent);
            vertexHelper.AddVert(new Vector3(posMax.x, posMax.y, 0), color, new Vector2(uvMax.x, uvMax.y), new Vector2(uv1Max.x, uv1Max.y), s_DefaultNormal, s_DefaultTangent);
            vertexHelper.AddVert(new Vector3(posMax.x, posMin.y, 0), color, new Vector2(uvMax.x, uvMin.y), new Vector2(uv1Max.x, uv1Min.y), s_DefaultNormal, s_DefaultTangent);

            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
        }

        Vector4 GetAdjustedBorders(Vector4 border, Rect adjustedRect)
        {
            Rect originalRect = GetRectByFlipDirection(flipMode, flipWithCopy, flipEdge, flipFillCenter);

            for (int axis = 0; axis <= 1; axis++)
            {
                float borderScaleRatio;

                // The adjusted rect (adjusted for pixel correctness)
                // may be slightly larger than the original rect.
                // Adjust the border to match the adjustedRect to avoid
                // small gaps between borders (case 833201).
                if (originalRect.size[axis] != 0)
                {
                    borderScaleRatio = adjustedRect.size[axis] / originalRect.size[axis];
                    border[axis] *= borderScaleRatio;
                    border[axis + 2] *= borderScaleRatio;
                }

                // If the rect is smaller than the combined borders, then there's not room for the borders at their normal size.
                // In order to avoid artefacts with overlapping borders, we scale the borders down to fit.
                float combinedBorders = border[axis] + border[axis + 2];
                if (adjustedRect.size[axis] < combinedBorders && combinedBorders != 0)
                {
                    borderScaleRatio = adjustedRect.size[axis] / combinedBorders;
                    border[axis] *= borderScaleRatio;
                    border[axis + 2] *= borderScaleRatio;
                }
            }

            return border;
        }


        void GenerateFilledSprite(VertexHelper toFill, bool preserveAspect)
        {
            toFill.Clear();

            if (fillAmount < 0.001f)
                return;

            Vector4 v = GetDrawingDimensions(preserveAspect);
            Vector4 outer = overrideSprite != null ? Sprites.DataUtility.GetOuterUV(overrideSprite) : Vector4.zero;
            UIVertex uiv = UIVertex.simpleVert;
            uiv.color = color;

            float tx0 = outer.x;
            float ty0 = outer.y;
            float tx1 = outer.z;
            float ty1 = outer.w;

            // Horizontal and vertical filled sprites are simple -- just end the Image prematurely
            if (fillMethod == FillMethod.Horizontal || fillMethod == FillMethod.Vertical)
            {
                if (fillMethod == FillMethod.Horizontal)
                {
                    float fill = (tx1 - tx0) * fillAmount;

                    if (fillOrigin == 1)
                    {
                        v.x = v.z - (v.z - v.x) * fillAmount;
                        tx0 = tx1 - fill;
                    }
                    else
                    {
                        v.z = v.x + (v.z - v.x) * fillAmount;
                        tx1 = tx0 + fill;
                    }
                }
                else if (fillMethod == FillMethod.Vertical)
                {
                    float fill = (ty1 - ty0) * fillAmount;

                    if (fillOrigin == 1)
                    {
                        v.y = v.w - (v.w - v.y) * fillAmount;
                        ty0 = ty1 - fill;
                    }
                    else
                    {
                        v.w = v.y + (v.w - v.y) * fillAmount;
                        ty1 = ty0 + fill;
                    }
                }
            }

            m_Xy[0] = new Vector2(v.x, v.y);
            m_Xy[1] = new Vector2(v.x, v.w);
            m_Xy[2] = new Vector2(v.z, v.w);
            m_Xy[3] = new Vector2(v.z, v.y);

            m_Uv[0] = new Vector2(tx0, ty0);
            m_Uv[1] = new Vector2(tx0, ty1);
            m_Uv[2] = new Vector2(tx1, ty1);
            m_Uv[3] = new Vector2(tx1, ty0);

            m_Uv1[0] = new Vector2(0, 0);
            m_Uv1[1] = new Vector2(0, 1);
            m_Uv1[2] = new Vector2(1, 1);
            m_Uv1[3] = new Vector2(1, 0);

            {
                if (fillAmount < 1f && fillMethod != FillMethod.Horizontal && fillMethod != FillMethod.Vertical)
                {
                    if (fillMethod == FillMethod.Radial90)
                    {
                        if (RadialCut(m_Xy, m_Uv, fillAmount, fillClockwise, fillOrigin))
                            AddQuad(toFill, m_Xy, color, m_Uv, m_Uv1);
                    }
                    else if (fillMethod == FillMethod.Radial180)
                    {
                        for (int side = 0; side < 2; ++side)
                        {
                            float fx0, fx1, fy0, fy1;
                            int even = fillOrigin > 1 ? 1 : 0;

                            if (fillOrigin == 0 || fillOrigin == 2)
                            {
                                fy0 = 0f;
                                fy1 = 1f;
                                if (side == even)
                                {
                                    fx0 = 0f;
                                    fx1 = 0.5f;
                                }
                                else
                                {
                                    fx0 = 0.5f;
                                    fx1 = 1f;
                                }
                            }
                            else
                            {
                                fx0 = 0f;
                                fx1 = 1f;
                                if (side == even)
                                {
                                    fy0 = 0.5f;
                                    fy1 = 1f;
                                }
                                else
                                {
                                    fy0 = 0f;
                                    fy1 = 0.5f;
                                }
                            }

                            m_Xy[0].x = Mathf.Lerp(v.x, v.z, fx0);
                            m_Xy[1].x = m_Xy[0].x;
                            m_Xy[2].x = Mathf.Lerp(v.x, v.z, fx1);
                            m_Xy[3].x = m_Xy[2].x;

                            m_Xy[0].y = Mathf.Lerp(v.y, v.w, fy0);
                            m_Xy[1].y = Mathf.Lerp(v.y, v.w, fy1);
                            m_Xy[2].y = m_Xy[1].y;
                            m_Xy[3].y = m_Xy[0].y;

                            m_Uv[0].x = Mathf.Lerp(tx0, tx1, fx0);
                            m_Uv[1].x = m_Uv[0].x;
                            m_Uv[2].x = Mathf.Lerp(tx0, tx1, fx1);
                            m_Uv[3].x = m_Uv[2].x;

                            m_Uv[0].y = Mathf.Lerp(ty0, ty1, fy0);
                            m_Uv[1].y = Mathf.Lerp(ty0, ty1, fy1);
                            m_Uv[2].y = m_Uv[1].y;
                            m_Uv[3].y = m_Uv[0].y;

                            float val = fillClockwise ? fillAmount * 2f - side : fillAmount * 2f - (1 - side);

                            if (RadialCut(m_Xy, m_Uv, Mathf.Clamp01(val), fillClockwise, ((side + fillOrigin + 3) % 4)))
                            {
                                AddQuad(toFill, m_Xy, color, m_Uv, m_Uv1);
                            }
                        }
                    }
                    else if (fillMethod == FillMethod.Radial360)
                    {
                        for (int corner = 0; corner < 4; ++corner)
                        {
                            float fx0, fx1, fy0, fy1;

                            if (corner < 2)
                            {
                                fx0 = 0f;
                                fx1 = 0.5f;
                            }
                            else
                            {
                                fx0 = 0.5f;
                                fx1 = 1f;
                            }

                            if (corner == 0 || corner == 3)
                            {
                                fy0 = 0f;
                                fy1 = 0.5f;
                            }
                            else
                            {
                                fy0 = 0.5f;
                                fy1 = 1f;
                            }

                            m_Xy[0].x = Mathf.Lerp(v.x, v.z, fx0);
                            m_Xy[1].x = m_Xy[0].x;
                            m_Xy[2].x = Mathf.Lerp(v.x, v.z, fx1);
                            m_Xy[3].x = m_Xy[2].x;

                            m_Xy[0].y = Mathf.Lerp(v.y, v.w, fy0);
                            m_Xy[1].y = Mathf.Lerp(v.y, v.w, fy1);
                            m_Xy[2].y = m_Xy[1].y;
                            m_Xy[3].y = m_Xy[0].y;

                            m_Uv[0].x = Mathf.Lerp(tx0, tx1, fx0);
                            m_Uv[1].x = m_Uv[0].x;
                            m_Uv[2].x = Mathf.Lerp(tx0, tx1, fx1);
                            m_Uv[3].x = m_Uv[2].x;

                            m_Uv[0].y = Mathf.Lerp(ty0, ty1, fy0);
                            m_Uv[1].y = Mathf.Lerp(ty0, ty1, fy1);
                            m_Uv[2].y = m_Uv[1].y;
                            m_Uv[3].y = m_Uv[0].y;

                            float val = fillClockwise ? fillAmount * 4f - ((corner + fillOrigin) % 4) : fillAmount * 4f - (3 - ((corner + fillOrigin) % 4));

                            if (RadialCut(m_Xy, m_Uv, Mathf.Clamp01(val), fillClockwise, ((corner + 2) % 4)))
                                AddQuad(toFill, m_Xy, color, m_Uv, m_Uv1);
                        }
                    }
                }
                else
                {
                    AddQuad(toFill, m_Xy, color, m_Uv, m_Uv1);
                }
            }
        }

        /// <summary>
        /// Adjust the specified quad, making it be radially filled instead.
        /// </summary>
        static bool RadialCut(Vector3[] xy, Vector3[] uv, float fill, bool invert, int corner)
        {
            // Nothing to fill
            if (fill < 0.001f) return false;

            // Even corners invert the fill direction
            if ((corner & 1) == 1) invert = !invert;

            // Nothing to adjust
            if (!invert && fill > 0.999f) return true;

            // Convert 0-1 value into 0 to 90 degrees angle in radians
            float angle = Mathf.Clamp01(fill);
            if (invert) angle = 1f - angle;
            angle *= 90f * Mathf.Deg2Rad;

            // Calculate the effective X and Y factors
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            RadialCut(xy, cos, sin, invert, corner);
            RadialCut(uv, cos, sin, invert, corner);
            return true;
        }

        /// <summary>
        /// Adjust the specified quad, making it be radially filled instead.
        /// </summary>
        static void RadialCut(Vector3[] xy, float cos, float sin, bool invert, int corner)
        {
            int i0 = corner;
            int i1 = ((corner + 1) % 4);
            int i2 = ((corner + 2) % 4);
            int i3 = ((corner + 3) % 4);

            if ((corner & 1) == 1)
            {
                if (sin > cos)
                {
                    cos /= sin;
                    sin = 1f;

                    if (invert)
                    {
                        xy[i1].x = Mathf.Lerp(xy[i0].x, xy[i2].x, cos);
                        xy[i2].x = xy[i1].x;
                    }
                }
                else if (cos > sin)
                {
                    sin /= cos;
                    cos = 1f;

                    if (!invert)
                    {
                        xy[i2].y = Mathf.Lerp(xy[i0].y, xy[i2].y, sin);
                        xy[i3].y = xy[i2].y;
                    }
                }
                else
                {
                    cos = 1f;
                    sin = 1f;
                }

                if (!invert) xy[i3].x = Mathf.Lerp(xy[i0].x, xy[i2].x, cos);
                else xy[i1].y = Mathf.Lerp(xy[i0].y, xy[i2].y, sin);
            }
            else
            {
                if (cos > sin)
                {
                    sin /= cos;
                    cos = 1f;

                    if (!invert)
                    {
                        xy[i1].y = Mathf.Lerp(xy[i0].y, xy[i2].y, sin);
                        xy[i2].y = xy[i1].y;
                    }
                }
                else if (sin > cos)
                {
                    cos /= sin;
                    sin = 1f;

                    if (invert)
                    {
                        xy[i2].x = Mathf.Lerp(xy[i0].x, xy[i2].x, cos);
                        xy[i3].x = xy[i2].x;
                    }
                }
                else
                {
                    cos = 1f;
                    sin = 1f;
                }

                if (invert) xy[i3].y = Mathf.Lerp(xy[i0].y, xy[i2].y, sin);
                else xy[i1].x = Mathf.Lerp(xy[i0].x, xy[i2].x, cos);
            }
        }


        static void AddQuad(VertexHelper vertexHelper, Vector3[] quadPositions, Color32 color, Vector3[] quadUVs)
        {
            int startIndex = vertexHelper.currentVertCount;

            for (int i = 0; i < 4; ++i)
                vertexHelper.AddVert(quadPositions[i], color, quadUVs[i]);

            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
        }

        static void AddQuad(VertexHelper vertexHelper, Vector2 posMin, Vector2 posMax, Color32 color, Vector2 uvMin, Vector2 uvMax)
        {
            int startIndex = vertexHelper.currentVertCount;

            vertexHelper.AddVert(new Vector3(posMin.x, posMin.y, 0), color, new Vector2(uvMin.x, uvMin.y));
            vertexHelper.AddVert(new Vector3(posMin.x, posMax.y, 0), color, new Vector2(uvMin.x, uvMax.y));
            vertexHelper.AddVert(new Vector3(posMax.x, posMax.y, 0), color, new Vector2(uvMax.x, uvMax.y));
            vertexHelper.AddVert(new Vector3(posMax.x, posMin.y, 0), color, new Vector2(uvMax.x, uvMin.y));

            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
        }
    }
}
