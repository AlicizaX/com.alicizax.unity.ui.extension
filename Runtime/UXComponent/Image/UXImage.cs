using System;
using System.Collections.Generic;

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

        /// <summary>
        /// 渐变数据源。替换引用会自动重新缓存关键帧并刷新网格；如果就地修改同一个 Gradient 实例的
        /// colorKeys/alphaKeys/mode，必须显式调用 <see cref="SetGradientDirty"/>，否则缓存不会更新。
        /// </summary>
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

        /// <summary>
        /// 就地修改 <see cref="gradient"/> 后调用：重新缓存颜色/透明度关键帧与 GradientMode，并刷新顶点。
        /// </summary>
        public void SetGradientDirty()
        {
            CacheGradientKeys();
            SetVerticesDirty();
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
                SyncEffectMaterial(m_EffectMaterialSdfTexture);
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
                SyncEffectMaterial(m_EffectMaterialSdfTexture);
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
                // 颜色只存在于材质上，不参与顶点数据：独占材质时原地改，共享时才换材质，绝不重建网格。
                SyncEffectMaterial(m_EffectMaterialSdfTexture);
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
                // 同 outlineEffectColor：只同步材质，不重建网格。
                SyncEffectMaterial(m_EffectMaterialSdfTexture);
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

        /// <summary>
        /// Releases every static cache used by the adaptive effect (atlas/SDF pages, cached sprite meshes and the
        /// shared effect materials). Call it after the affected UI has been hidden or destroyed, e.g. on content
        /// shutdown or scene transitions. All live Graphics are invalidated right away; their pages and materials are
        /// recreated lazily on the next mesh rebuild, so no visible Graphic keeps stale atlas UVs or a destroyed
        /// SDF texture.
        /// </summary>
        public static void ReleaseRuntimeEffectCaches()
        {
            UXImageRuntimeSDFCache.ReleaseAll();
            UXImageRuntimeAtlas.ReleaseAll();
            UXImageSpriteMeshCache.Clear();

            // 先让所有存活实例释放各自持有的材质引用，再销毁共享材质缓存，避免留下悬空引用。
            ReleaseLiveInstances();

            UXImageEffectSettings.ReleaseAll();
        }

        // 存活 Graphic 的弱引用注册表；只在访问时压缩，绝不强引用任何实例。
        private static readonly List<WeakReference<UXImage>> s_LiveInstances = new List<WeakReference<UXImage>>(64);

        private static void ReleaseLiveInstances()
        {
            for (int i = s_LiveInstances.Count - 1; i >= 0; i--)
            {
                if (!s_LiveInstances[i].TryGetTarget(out UXImage image) || image == null)
                {
                    s_LiveInstances.RemoveAt(i);
                    continue;
                }

                image.ClearRuntimeAtlasEntry();
                image.ClearEffectMaterialKey();
                image.SetVerticesDirty();
                image.SetMaterialDirty();
            }
        }

        private void RegisterLiveInstance()
        {
            for (int i = s_LiveInstances.Count - 1; i >= 0; i--)
            {
                if (!s_LiveInstances[i].TryGetTarget(out UXImage image) || image == null)
                {
                    s_LiveInstances.RemoveAt(i);
                    continue;
                }

                if (ReferenceEquals(image, this))
                    return;
            }

            s_LiveInstances.Add(new WeakReference<UXImage>(this));
        }

        private void UnregisterLiveInstance()
        {
            for (int i = s_LiveInstances.Count - 1; i >= 0; i--)
            {
                if (!s_LiveInstances[i].TryGetTarget(out UXImage image) || image == null || ReferenceEquals(image, this))
                    s_LiveInstances.RemoveAt(i);
            }
        }

        // Adaptive effect state. 全部由 BuildMesh -> ApplyAdaptiveEffectData 以及 SyncEffectMaterial 维护；
        // mainTexture/defaultMaterial 只读这些字段，属性 getter 永远不会创建图集页或触碰 GL 状态。

        private UXImageRuntimeAtlas.Entry m_RuntimeAtlasEntry;
        private Sprite m_RuntimeAtlasSprite;
        private int m_RuntimeAtlasVersion;

        // 材质的缓存键：Material 引用 + (SDF 贴图, 描边颜色, 阴影颜色)。键一致就复用同一材质实例。
        private Material m_EffectMaterial;
        private Texture m_EffectMaterialSdfTexture;
        private Color m_EffectMaterialOutlineColor;
        private Color m_EffectMaterialShadowColor;

        public override Texture mainTexture
        {
            get
            {
                // m_RuntimeAtlasSprite != null 即“entry 已写入”的标记；页面被回收时 Version 会变化，同样回退。
                if (HasAdaptiveEffect &&
                    m_RuntimeAtlasSprite != null &&
                    m_RuntimeAtlasVersion == UXImageRuntimeAtlas.Version)
                {
                    return m_RuntimeAtlasEntry.Texture;
                }

                return base.mainTexture;
            }
        }

        public override Material defaultMaterial
        {
            get
            {
                if (!HasAdaptiveEffect)
                    return base.defaultMaterial;

                // 共享材质让状态相同的 Graphic 保持同一个材质实例，从而保持 UGUI 合批。
                return m_EffectMaterial != null ? m_EffectMaterial : base.defaultMaterial;
            }
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
        private GradientMode m_CachedGradientMode = GradientMode.Blend;
        private readonly Vector2[] m_VertScratch = new Vector2[4];
        private readonly Vector2[] m_UvScratch = new Vector2[4];
        private readonly Vector3[] m_Xy = new Vector3[4];
        private readonly Vector3[] m_Uv = new Vector3[4];
        private readonly Vector3[] m_Uv1 = new Vector3[4];

        // 与 VertexHelper 同步的索引镜像：CopyImage 复制镜像网格时依赖它读取“真实的”三角形列表。
        // 所有索引写入都经过 AddTriangle，Clear 时同步清空，因此重建过程中不会产生托管分配。
        private readonly List<int> m_MeshIndices = new List<int>(96);


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

        private void BuildMesh(VertexHelper toFill)
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

            m_WorkRect = GetDrawPixelAdjustedRect();

            if (flipMode == FlipMode.Horziontal)
            {
                if (flipWithCopy)
                {
                    CopyImage(toFill);
                    Rect src = GetPixelRectByFlipDirection(FlipMode.Horziontal, flipWithCopy, flipEdge, flipFillCenter);
                    Rect target = GetCopyRectByFlipDirection(src, FlipMode.Horziontal, flipEdge);
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
                    // 垂直方向重映射的是 y 轴：RemapVertex 在 FlipMode.Vertical 下写入 position.y。
                    RemapImage(toFill, FlipMode.Vertical, 0, toFill.currentVertCount, m_WorkRect.yMin, m_WorkRect.yMax, m_WorkRect.yMax, m_WorkRect.yMin);
                }
            }

            if (flipMode == FlipMode.FourCorner)
            {
                //先水平拷贝?
                CopyImage(toFill);
                Rect src = GetPixelRectByFlipDirection(FlipMode.FourCorner, true, flipEdge, flipFillCenter);
                Rect target = GetCopyRectByFlipCenter(src, FlipMode.Horziontal, flipFillCenter);
                //Rect target = new Rect(src.position + new Vector2(src.width, 0), src.size);
                RemapImage(toFill, FlipMode.Horziontal, toFill.currentVertCount / 2, toFill.currentVertCount, src.xMin, src.xMax, target.xMax, target.xMin);

                //再垂直拷贝?
                CopyImage(toFill);
                Rect src1 = GetPixelRectByFlipDirection(FlipMode.FourCorner, true, flipEdge, flipFillCenter);
                Rect target1 = GetCopyRectByFlipCenter(src1, FlipMode.Vertical, flipFillCenter);
                RemapImage(toFill, FlipMode.Vertical, toFill.currentVertCount / 2, toFill.currentVertCount, src1.yMin, src1.yMax, target1.yMax, target1.yMin);
            }

        }

        #region Flip

        /// <summary>
        /// 复制当前网格：顶点整体追加一份，三角形按“原始索引 + 顶点数偏移”逐条复制。
        /// 索引来自与 VertexHelper 同步的 m_MeshIndices，因此任意三角形列表（例如 useSpriteMesh 的自定义网格）
        /// 都能被完整复制，不再假设网格一定是四边形。
        /// </summary>
        private void CopyImage(VertexHelper toFill)
        {
            int count = toFill.currentVertCount;
            if (count == 0)
                return;

            Debug.Assert(m_MeshIndices.Count == toFill.currentIndexCount,
                "UXImage.CopyImage: m_MeshIndices is out of sync with the VertexHelper index buffer.");

            for (int i = 0; i < count; ++i)
            {
                toFill.PopulateUIVertex(ref m_WorkVert, i);
                toFill.AddVert(m_WorkVert);
            }

            int indexCount = m_MeshIndices.Count;
            for (int i = 0; i < indexCount; i += 3)
            {
                AddTriangle(toFill,
                    m_MeshIndices[i] + count,
                    m_MeshIndices[i + 1] + count,
                    m_MeshIndices[i + 2] + count);
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
            // 布局阶段尺寸为 0 的退化矩形会让 k 变成 Infinity/NaN；退化时不做重映射。
            if (Mathf.Approximately(Min1, Max1))
                return;

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
        /// 如果 flipMode == Horziontal 或 Vertical, 根据copy, flipEdge来修改原本的Rect
        /// 如果 flipMode == FourCorner, 根据flipFillCenter 来修改?
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
        /// 获取Copy出来的Rect。
        /// 这里Src应该是Modify过的原Rect
        /// 只处理flipMode == Horziontal 或 Vertical的情况
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
        /// 获取Copy出来的Rect，flipMode == FourCorner时专用
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
            ClearVertexHelper(vh);

            AddQuad(vh, new Vector2(v.x, v.y), new Vector2(v.z, v.w), color, new Vector2(uv.x, uv.y), new Vector2(uv.z, uv.w), Vector2.zero, Vector2.one);
        }

        #endregion

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            BuildMesh(toFill);

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
                m_CachedGradientMode = GradientMode.Blend;
                return;
            }

            m_CachedColorKeys = m_GradientColor.colorKeys;
            m_CachedAlphaKeys = m_GradientColor.alphaKeys;
            m_CachedGradientMode = m_GradientColor.mode;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RegisterLiveInstance();
            CacheGradientKeys();
            EnsureEffectCanvasChannels();
        }

        protected override void OnDestroy()
        {
            UnregisterLiveInstance();
            ClearEffectMaterialKey();
            base.OnDestroy();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            CacheGradientKeys();
            m_OutlineSoftness = Mathf.Max(m_OutlineSoftness, 0f);
            m_ShadowSoftness = Mathf.Max(m_ShadowSoftness, 0f);
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

            Rect bounds = GetVertexBounds(toFill, count);
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

        private Rect GetVertexBounds(VertexHelper toFill, int count)
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
            bool useRuntimeSdf = false;
            Vector4 sampleRect;
            Vector4 sdfRect = Vector4.zero;
            Texture sdfTexture = null;

            // 特效几何只能向四顶点四边形外扩，因此只有 Simple/Filled 且恰好 4 个顶点时才走带 padding 的
            // 运行时图集；Sliced/Tiled 等网格继续走普通贴图路径，避免 UV 被映射进图集 padding 后又被裁掉。
            // 容量契约（静默降级）：运行时图集最多 4 页、SDF 最多 2 页；达到上限时 TryGet/TryGetOrRequest
            // 返回 false，这里回退到普通贴图与非 SDF 阴影，不抛异常也不超额分配。
            bool supportsRuntimeAtlas = count == 4 && (type == Type.Simple || type == Type.Filled);

            if (supportsRuntimeAtlas && TryPrepareRuntimeAtlasEntry(sprite, out var atlasEntry))
            {
                Vector2 atlasTexelSize = atlasEntry.TexelSize;
                sampleRect = GetPaddedRuntimeAtlasRect(atlasEntry);
                RemapVerticesToRuntimeAtlas(toFill, count, atlasEntry);
                ExpandEffectGeometry(toFill, count, atlasTexelSize);

                if (m_EnableShadow && UXImageRuntimeSDFCache.ShouldUse(shadowSoftness) &&
                    UXImageRuntimeSDFCache.TryGetOrRequest(sprite, shadowSoftness, this, out var sdfEntry))
                {
                    useRuntimeSdf = true;
                    sdfRect = sdfEntry.UvRect;
                    sdfTexture = sdfEntry.Texture;
                }
            }
            else
            {
                ClearRuntimeAtlasEntry();
                sampleRect = sprite != null
                    ? Sprites.DataUtility.GetOuterUV(sprite)
                    : new Vector4(0f, 0f, 1f, 1f);
            }

            SyncEffectMaterial(sdfTexture, true);

            if (useRuntimeSdf)
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

        /// <summary>
        /// 让当前实例的材质与 (SDF 贴图, 描边颜色, 阴影颜色) 对齐，返回材质实例是否发生了变化。
        /// - 没有特效：释放引用并清空键；
        /// - 键未变化：什么都不做（不产生任何 dirty）；
        /// - 当前材质引用计数为 1：就地修改 uniform 并更新键（材质实例不变，合批不受影响）；
        /// - 否则：从共享缓存取新材质、释放旧材质，并把材质标记为 dirty。
        /// <paramref name="createIfMissing"/> 为 false 时（属性 setter 调用）不会为尚未建过网格的实例创建材质，
        /// 真正创建材质的唯一入口仍然是网格重建路径；这样渲染 getter 与属性 setter 都不会提前碰 GPU 资源。
        /// </summary>
        private bool SyncEffectMaterial(Texture sdfTexture, bool createIfMissing = false)
        {
            if (!HasAdaptiveEffect)
            {
                bool hadMaterial = m_EffectMaterial != null;
                ClearEffectMaterialKey();
                if (hadMaterial)
                    SetMaterialDirty();
                return hadMaterial;
            }

            Color outlineColor = m_OutlineEffectColor;
            Color shadowColor = m_ShadowEffectColor;
            Material current = m_EffectMaterial;

            if (current == null && !createIfMissing)
                return false;

            if (current != null &&
                ReferenceEquals(m_EffectMaterialSdfTexture, sdfTexture) &&
                m_EffectMaterialOutlineColor == outlineColor &&
                m_EffectMaterialShadowColor == shadowColor)
            {
                return false;
            }

            if (current != null && UXImageEffectSettings.GetMaterialRefCount(current) == 1)
            {
                UXImageEffectSettings.ApplyMaterialState(current, sdfTexture, outlineColor, shadowColor);
                m_EffectMaterialSdfTexture = sdfTexture;
                m_EffectMaterialOutlineColor = outlineColor;
                m_EffectMaterialShadowColor = shadowColor;
                return false;
            }

            Material next = UXImageEffectSettings.AcquireEffectMaterial(sdfTexture, outlineColor, shadowColor);
            if (current != null)
                UXImageEffectSettings.ReleaseEffectMaterial(current);

            m_EffectMaterial = next;
            m_EffectMaterialSdfTexture = sdfTexture;
            m_EffectMaterialOutlineColor = outlineColor;
            m_EffectMaterialShadowColor = shadowColor;

            if (next != null)
                SetMaterialDirty();

            return true;
        }

        /// <summary>释放共享材质引用并清空本地缓存的键（不会销毁共享材质本身）。</summary>
        private void ClearEffectMaterialKey()
        {
            if (m_EffectMaterial != null)
                UXImageEffectSettings.ReleaseEffectMaterial(m_EffectMaterial);

            m_EffectMaterial = null;
            m_EffectMaterialSdfTexture = null;
            m_EffectMaterialOutlineColor = default;
            m_EffectMaterialShadowColor = default;
        }

        private void EnsureEffectCanvasChannels()
        {
            if (HasAdaptiveEffect)
                UXImageEffectSettings.EnsureCanvasChannels(canvas);
        }

        /// <summary>
        /// Resolves (and caches per Graphic) the runtime atlas entry for the current sprite. This is the only place
        /// that may create atlas pages; it is called from the mesh update path, never from a rendering getter.
        /// 是否启用特效由调用方唯一负责，这里只处理 sprite 与缓存有效性。
        /// </summary>
        private bool TryPrepareRuntimeAtlasEntry(Sprite sprite, out UXImageRuntimeAtlas.Entry entry)
        {
            entry = default;
            if (sprite == null)
            {
                ClearRuntimeAtlasEntry();
                return false;
            }

            if (m_RuntimeAtlasVersion == UXImageRuntimeAtlas.Version && ReferenceEquals(m_RuntimeAtlasSprite, sprite))
            {
                entry = m_RuntimeAtlasEntry;
                return true;
            }

            if (!UXImageRuntimeAtlas.TryGet(sprite, out entry))
            {
                ClearRuntimeAtlasEntry();
                return false;
            }

            m_RuntimeAtlasEntry = entry;
            m_RuntimeAtlasSprite = sprite;
            m_RuntimeAtlasVersion = UXImageRuntimeAtlas.Version;
            return true;
        }

        private void ClearRuntimeAtlasEntry()
        {
            m_RuntimeAtlasEntry = default;
            m_RuntimeAtlasSprite = null;
            m_RuntimeAtlasVersion = 0;
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

            // The atlas entry is allocated once with UXImageRuntimeAtlas.MaxPadding ring; larger requests would
            // sample neighbouring sprites, so the effect padding is capped to the same value.
            return Mathf.Clamp(Mathf.CeilToInt(padding) + 2, 1, UXImageRuntimeAtlas.MaxPadding);
        }

        private void RemapVerticesToRuntimeAtlas(VertexHelper toFill, int count, UXImageRuntimeAtlas.Entry entry)
        {
            // 走到这里说明 TryPrepareRuntimeAtlasEntry 已确认 overrideSprite 非空。
            Vector4 source = Sprites.DataUtility.GetOuterUV(overrideSprite);
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

        /// <summary>
        /// 把四顶点网格向外扩张 <see cref="GetEffectPadding"/> 个像素，并把 uv0 同步推进图集的 padding 环，
        /// 这样描边/阴影不会被网格边界裁掉。调用方保证 count == 4 且类型可扩张（Simple/Filled）。
        /// </summary>
        private void ExpandEffectGeometry(VertexHelper toFill, int count, Vector2 spriteTexelSize)
        {
            float padding = GetEffectPadding();
            Rect bounds = GetVertexBounds(toFill, count);
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
                    if (m_CachedGradientMode == GradientMode.Blend || m_CachedGradientMode == GradientMode.PerceptualBlend)
#else
                    if (m_CachedGradientMode == GradientMode.Blend)
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
                    if (m_CachedGradientMode == GradientMode.Blend || m_CachedGradientMode == GradientMode.PerceptualBlend)
#else
                    if (m_CachedGradientMode == GradientMode.Blend)
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
            ClearVertexHelper(vh);

            Sprite sprite = overrideSprite;
            if (sprite == null)
                return;

            // Sprite.vertices/uv/triangles 每次访问都会复制数组；这里走缓存，命中时重建过程零托管分配。
            if (!UXImageSpriteMeshCache.TryGet(sprite, out Vector2[] vertices, out Vector2[] uvs, out ushort[] triangles, out int triangleCount))
                return;

            var spriteSize = new Vector2(sprite.rect.width, sprite.rect.height);

            // Covert sprite pivot into normalized space.
            var spritePivot = sprite.pivot / spriteSize;
            var rectPivot = rectTransform.pivot;
            Rect r = GetPixelAdjustedRect();

            if (lPreserveAspect & spriteSize.sqrMagnitude > 0.0f)
            {
                PreserveSpriteAspectRatio(ref r, spriteSize);
            }

            var drawingSize = new Vector2(r.width, r.height);
            var spriteBoundSize = sprite.bounds.size;

            // Calculate the drawing offset based on the difference between the two pivots.
            var drawOffset = (rectPivot - spritePivot) * drawingSize;

            var color32 = color;

            for (int i = 0; i < vertices.Length; ++i)
            {
                vh.AddVert(new Vector3((vertices[i].x / spriteBoundSize.x) * drawingSize.x - drawOffset.x, (vertices[i].y / spriteBoundSize.y) * drawingSize.y - drawOffset.y), color32, new Vector2(uvs[i].x, uvs[i].y));
            }

            for (int t = 0; t < triangleCount; t++)
            {
                int i = t * 3;
                AddTriangle(vh, triangles[i + 0], triangles[i + 1], triangles[i + 2]);
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

            ClearVertexHelper(toFill);

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

            ClearVertexHelper(toFill);
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

        void AddQuad(VertexHelper vertexHelper, Vector3[] quadPositions, Color32 color, Vector3[] quadUVs, Vector3[] quadUV1s)
        {
            int startIndex = vertexHelper.currentVertCount;

            for (int i = 0; i < 4; ++i)
                vertexHelper.AddVert(quadPositions[i], color, quadUVs[i], quadUV1s[i], s_DefaultNormal, s_DefaultTangent);

            AddTriangle(vertexHelper, startIndex, startIndex + 1, startIndex + 2);
            AddTriangle(vertexHelper, startIndex + 2, startIndex + 3, startIndex);
        }

        void AddQuad(VertexHelper vertexHelper, Vector2 posMin, Vector2 posMax, Color32 color, Vector2 uvMin, Vector2 uvMax, Vector2 uv1Min, Vector2 uv1Max)
        {
            int startIndex = vertexHelper.currentVertCount;

            vertexHelper.AddVert(new Vector3(posMin.x, posMin.y, 0), color, new Vector2(uvMin.x, uvMin.y), new Vector2(uv1Min.x, uv1Min.y), s_DefaultNormal, s_DefaultTangent);
            vertexHelper.AddVert(new Vector3(posMin.x, posMax.y, 0), color, new Vector2(uvMin.x, uvMax.y), new Vector2(uv1Min.x, uv1Max.y), s_DefaultNormal, s_DefaultTangent);
            vertexHelper.AddVert(new Vector3(posMax.x, posMax.y, 0), color, new Vector2(uvMax.x, uvMax.y), new Vector2(uv1Max.x, uv1Max.y), s_DefaultNormal, s_DefaultTangent);
            vertexHelper.AddVert(new Vector3(posMax.x, posMin.y, 0), color, new Vector2(uvMax.x, uvMin.y), new Vector2(uv1Max.x, uv1Min.y), s_DefaultNormal, s_DefaultTangent);

            AddTriangle(vertexHelper, startIndex, startIndex + 1, startIndex + 2);
            AddTriangle(vertexHelper, startIndex + 2, startIndex + 3, startIndex);
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
            ClearVertexHelper(toFill);

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


        void AddQuad(VertexHelper vertexHelper, Vector3[] quadPositions, Color32 color, Vector3[] quadUVs)
        {
            int startIndex = vertexHelper.currentVertCount;

            for (int i = 0; i < 4; ++i)
                vertexHelper.AddVert(quadPositions[i], color, quadUVs[i]);

            AddTriangle(vertexHelper, startIndex, startIndex + 1, startIndex + 2);
            AddTriangle(vertexHelper, startIndex + 2, startIndex + 3, startIndex);
        }

        void AddQuad(VertexHelper vertexHelper, Vector2 posMin, Vector2 posMax, Color32 color, Vector2 uvMin, Vector2 uvMax)
        {
            int startIndex = vertexHelper.currentVertCount;

            vertexHelper.AddVert(new Vector3(posMin.x, posMin.y, 0), color, new Vector2(uvMin.x, uvMin.y));
            vertexHelper.AddVert(new Vector3(posMin.x, posMax.y, 0), color, new Vector2(uvMin.x, uvMax.y));
            vertexHelper.AddVert(new Vector3(posMax.x, posMax.y, 0), color, new Vector2(uvMax.x, uvMax.y));
            vertexHelper.AddVert(new Vector3(posMax.x, posMin.y, 0), color, new Vector2(uvMax.x, uvMin.y));

            AddTriangle(vertexHelper, startIndex, startIndex + 1, startIndex + 2);
            AddTriangle(vertexHelper, startIndex + 2, startIndex + 3, startIndex);
        }

        /// <summary>
        /// 清空 VertexHelper，同时清空索引镜像，保证两者始终一一对应。
        /// </summary>
        private void ClearVertexHelper(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            m_MeshIndices.Clear();
        }

        /// <summary>
        /// 唯一的索引写入入口：写入 VertexHelper 的同时记录到 m_MeshIndices。
        /// CopyImage 依赖这份镜像复制任意三角形列表，且整个过程零托管分配。
        /// </summary>
        private void AddTriangle(VertexHelper vertexHelper, int index0, int index1, int index2)
        {
            vertexHelper.AddTriangle(index0, index1, index2);
            m_MeshIndices.Add(index0);
            m_MeshIndices.Add(index1);
            m_MeshIndices.Add(index2);
        }
    }
}
