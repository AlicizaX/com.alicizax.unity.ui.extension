using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace AlicizaX.UI
{
    /// <summary>
    /// RecyclerView 的核心组件，负责适配器绑定、布局刷新、滚动控制与焦点导航。
    /// </summary>
    [DisallowMultipleComponent]
    public class RecyclerView : MonoBehaviour
    {
        /// <summary>
        /// 滚动条拖拽手柄允许的最小像素长度。
        /// </summary>
        private const float MinScrollbarHandlePixels = 20f;


        #region 序列化字段 - 布局设置

        /// <summary>
        /// 列表的主滚动方向。
        /// </summary>
        [HideInInspector] [SerializeField] private Direction direction;

        /// <summary>
        /// 列表项在交叉轴上的对齐方式。
        /// </summary>
        [HideInInspector] [SerializeField] private Alignment alignment;

        /// <summary>
        /// 列表项之间的间距。
        /// </summary>
        [HideInInspector] [SerializeField] private Vector2 spacing;

        /// <summary>
        /// 列表内容区域的内边距。
        /// </summary>
        [HideInInspector] [SerializeField] private Vector2 padding;

        #endregion

        #region 序列化字段 - 滚动设置

        /// <summary>
        /// 滚动到边界时的运动类型：Elastic（弹性回弹）或 Clamped（硬性限制）。
        /// </summary>
        [HideInInspector] [SerializeField] private MovementType movementType = MovementType.Elastic;

        /// <summary>
        /// 是否启用滚动能力。
        /// </summary>
        [HideInInspector] [SerializeField] private ScrollMode scroll = ScrollMode.AlwaysEnable;

        /// <summary>
        /// 是否在停止滚动后自动吸附到最近项。
        /// </summary>
        [HideInInspector] [SerializeField] private bool snap;

        /// <summary>
        /// 是否启用惯性滑动。
        /// </summary>
        [HideInInspector] [SerializeField] private bool inertia = true;

        /// <summary>
        /// 惯性减速率，值越小减速越快。
        /// </summary>
        [HideInInspector] [SerializeField, Range(0.001f, 0.999f)]
        private float decelerationRate = 0.135f;

        /// <summary>
        /// 平滑滚动时的速度系数。
        /// </summary>
        [HideInInspector] [SerializeField, Range(0.5f, 50f)]
        private float scrollSpeed = 7f;

        /// <summary>
        /// 鼠标滚轮滚动时的速度系数。
        /// </summary>
        [HideInInspector] [SerializeField, Range(1f, 50f)]
        private float wheelSpeed = 30f;

        #endregion

        #region 序列化字段 - 组件引用

        /// <summary>
        /// 可用于创建列表项的模板集合。
        /// </summary>
        [HideInInspector] [SerializeField] private ViewHolder[] templates;

        /// <summary>
        /// 承载所有列表项的内容节点。
        /// </summary>
        [HideInInspector] [SerializeField] private RectTransform content;

        /// <summary>
        /// 是否显示滚动条。
        /// </summary>
        [FormerlySerializedAs("showScrollBar")] [HideInInspector] [SerializeField]
        private ScrollbarVisibility scrollbarVisibility;

        /// <summary>
        /// 与当前列表关联的滚动条组件。
        /// </summary>
        [HideInInspector] [SerializeField] private Scrollbar scrollbar;

        #endregion

        #region 序列化字段 - 内部字段（检视面板隐藏）

        /// <summary>
        /// 序列化保存的布局管理器类型名称。
        /// </summary>
        [HideInInspector] [SerializeField] private string _layoutManagerTypeName;

        /// <summary>
        /// 当前使用的布局管理器实例。
        /// </summary>
        [SerializeReference] private LayoutManager layoutManager;

        /// <summary>
        /// 序列化保存的滚动器类型名称。
        /// </summary>
        [HideInInspector] [SerializeField] private string _scrollerTypeName;

        /// <summary>
        /// 当前使用的滚动器实例。
        /// </summary>
        [HideInInspector] [SerializeField] private Scroller scroller;

        #endregion

        #region 私有字段

        /// <summary>
        /// 负责创建、回收与查询视图持有者的提供器。
        /// </summary>
        private ViewProvider viewProvider;

        private bool isValid = true;
        private bool validationErrorLogged;
        private bool scrollbarVisibleState;
        private bool scrollbarInteractableState;
        private ScrollbarEx scrollbarEx;

        private bool hasPendingScrollbarRatio;

        private float pendingScrollbarRatio;
        private RectTransform cachedRectTransform;
        private Vector2 lastViewportSize;
        private bool hasLastViewportSize;

        /// <summary>
        /// 当前可见区间的起始布局索引。
        /// </summary>
        private int startIndex;

        /// <summary>
        /// 当前可见区间的结束布局索引。
        /// </summary>
        private int endIndex;

        /// <summary>
        /// 当前记录的逻辑选中索引。
        /// </summary>
        private int currentScrollDataIndex;

        #endregion

        #region 公共属性 - 布局设置

        /// <summary>
        /// 获取或设置列表的主滚动方向。
        /// </summary>
        public Direction Direction
        {
            get => direction;
            set
            {
                if (direction == value)
                {
                    return;
                }

                direction = value;
                ApplyLayoutSettings();
                RequestLayout();
                Refresh();
            }
        }

        /// <summary>
        /// 获取或设置列表项在交叉轴上的对齐方式。
        /// </summary>
        public Alignment Alignment
        {
            get => alignment;
            set
            {
                if (alignment == value)
                {
                    return;
                }

                alignment = value;
                ApplyLayoutSettings();
                RequestLayout();
                Refresh();
            }
        }

        /// <summary>
        /// 获取或设置列表项之间的间距。
        /// </summary>
        public Vector2 Spacing
        {
            get => spacing;
            set
            {
                if (spacing == value)
                {
                    return;
                }

                spacing = value;
                ApplyLayoutSettings();
                RequestLayout();
                Refresh();
            }
        }

        /// <summary>
        /// 获取或设置列表内容区域的内边距。
        /// </summary>
        public Vector2 Padding
        {
            get => padding;
            set
            {
                if (padding == value)
                {
                    return;
                }

                padding = value;
                ApplyLayoutSettings();
                RequestLayout();
                Refresh();
            }
        }

        #endregion

        #region 公共属性 - 滚动设置

        /// <summary>
        /// 获取或设置滚动到边界时的运动类型。
        /// </summary>
        public MovementType MovementType
        {
            get => movementType;
            set
            {
                if (movementType == value) return;
                movementType = value;

                if (scroller != null)
                {
                    scroller.MovementType = movementType;
                }
            }
        }

        /// <summary>
        /// 获取或设置是否启用滚动能力。
        /// </summary>
        public ScrollMode Scroll
        {
            get => scroll;
            set
            {
                if (scroll == value) return;
                scroll = value;

                if (scroller != null)
                {
                    scroller.WheelSpeed = wheelSpeed;
                    scroller.Snap = snap;
                    UpdateScrollerState();
                }

                RequestLayout();
            }
        }

        /// <summary>
        /// 获取或设置是否在停止滚动后自动吸附到最近项。
        /// </summary>
        public bool Snap
        {
            get => snap;
            set
            {
                if (snap == value) return;
                snap = value;

                if (scroller != null)
                {
                    scroller.Snap = snap;
                }

                // 如需在启用吸附后立即校正位置，可在此触发最近项吸附。
            }
        }

        /// <summary>
        /// 获取或设置是否启用惯性滑动。
        /// </summary>
        public bool Inertia
        {
            get => inertia;
            set
            {
                if (inertia == value) return;
                inertia = value;

                if (scroller != null)
                {
                    scroller.Inertia = inertia;
                }
            }
        }

        /// <summary>
        /// 获取或设置惯性减速率。值越小减速越快，范围 [0.001, 0.999]。
        /// </summary>
        public float DecelerationRate
        {
            get => decelerationRate;
            set
            {
                if (Mathf.Approximately(decelerationRate, value)) return;
                decelerationRate = value;

                if (scroller != null)
                {
                    scroller.DecelerationRate = decelerationRate;
                }
            }
        }

        /// <summary>
        /// 获取或设置平滑滚动速度系数。
        /// </summary>
        public float ScrollSpeed
        {
            get => scrollSpeed;
            set
            {
                if (Mathf.Approximately(scrollSpeed, value)) return;
                scrollSpeed = value;

                if (scroller != null)
                {
                    scroller.ScrollSpeed = scrollSpeed;
                }
            }
        }

        /// <summary>
        /// 获取或设置鼠标滚轮滚动速度系数。
        /// </summary>
        public float WheelSpeed
        {
            get => wheelSpeed;
            set
            {
                if (Mathf.Approximately(wheelSpeed, value)) return;
                wheelSpeed = value;

                if (scroller != null)
                {
                    scroller.WheelSpeed = wheelSpeed;
                }
            }
        }

        /// <summary>
        /// 获取或设置是否仅在内容可滚动时显示滚动条。
        /// </summary>
        public ScrollbarVisibility ScrollbarVisibility
        {
            get => scrollbarVisibility;
            set
            {
                if (scrollbarVisibility == value) return;
                scrollbarVisibility = value;
                RequestLayout();
            }
        }

        #endregion


        #region 公共属性 - 组件引用

        /// <summary>
        /// 获取或设置用于创建列表项的模板集合。
        /// </summary>
        public ViewHolder[] Templates
        {
            get => templates;
            set => templates = value;
        }

        /// <summary>
        /// 获取内容节点；未显式指定时会尝试从首个子节点推断。
        /// </summary>
        public RectTransform Content
        {
            get
            {
                if (content == null)
                {
                    if (transform.childCount == 0)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Log.Error("RecyclerView content is missing.");
#endif
                        return null;
                    }

                    content = transform.GetChild(0).GetComponent<RectTransform>();
                    if (content == null)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Log.Error("RecyclerView content RectTransform is missing.");
#endif
                        return null;
                    }
                }

                return content;
            }
        }

        /// <summary>
        /// 获取当前绑定的滚动条组件。
        /// </summary>
        public Scrollbar Scrollbar => scrollbar;

        /// <summary>
        /// 获取当前绑定的滚动器实例。
        /// </summary>
        public Scroller Scroller => scroller;

        /// <summary>
        /// 获取视图提供器；首次访问时根据模板数量自动创建。
        /// </summary>
        internal ViewProvider ViewProvider
        {
            get
            {
                if (viewProvider == null)
                {
                    if (templates == null || templates.Length == 0)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Log.Error("RecyclerView templates are missing.");
#endif
                        return null;
                    }

                    viewProvider = templates.Length > 1
                        ? new MixedViewProvider(this, templates)
                        : new SimpleViewProvider(this, templates);
                }

                return viewProvider;
            }
        }

        /// <summary>
        /// 获取当前对象池的统计信息文本。
        /// </summary>
        public string PoolStats
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return viewProvider?.PoolStats ?? string.Empty;
#else
                return string.Empty;
#endif
            }
        }

        /// <summary>
        /// 获取当前布局管理器实例。
        /// </summary>
        public LayoutManager LayoutManager => layoutManager;

        #endregion

        #region 公共属性 - 状态

        /// <summary>
        /// 获取或设置当前绑定的适配器实例。
        /// </summary>
        internal IAdapter RecyclerViewAdapter { get; private set; }

        internal int CurrentScrollDataIndex => currentScrollDataIndex;

        #endregion

        #region 事件

        /// <summary>
        /// 当滚动位置发生变化时触发。
        /// </summary>
        public Action<float> OnScrollValueChanged;

        /// <summary>
        /// 当滚动停止时触发。
        /// </summary>
        public Action OnScrollStopped;

        /// <summary>
        /// 当拖拽状态变化时触发。
        /// </summary>
        public Action<bool> OnScrollDraggingChanged;

        #endregion

        #region 引擎生命周期

        /// <summary>
        /// Initializes templates, scroller, and scrollbar.
        /// </summary>
        private void Awake()
        {
            ValidateConfiguration();
            if (!isValid)
            {
                return;
            }

            TryUpdateViewportSizeCache(out _);
            InitializeTemplates();
            ConfigureScroller();
            ConfigureScrollbar();
        }

        private void LateUpdate()
        {
            ProcessPendingScrollbarRatio();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!TryUpdateViewportSizeCache(out bool changed) || !changed)
            {
                return;
            }

            if (!Application.isPlaying || !isValid || RecyclerViewAdapter == null)
            {
                return;
            }

            RequestLayout();
            Refresh();
        }

        private void OnDestroy()
        {
            if (scroller != null)
            {
                scroller.OnValueChanged -= OnScrollChanged;
                scroller.OnMoveStoped -= OnMoveStoped;
                scroller.OnDragging -= OnScrollerDraggingChanged;
            }

            if (scrollbar != null)
            {
                scrollbar.onValueChanged.RemoveListener(OnScrollbarChanged);
                if (scrollbarEx != null && scrollbarEx.OnDragEnd == OnScrollbarDragEnd)
                {
                    scrollbarEx.OnDragEnd = null;
                }
            }

            layoutManager?.Release();
            viewProvider?.Dispose();
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化所有模板实例并将其隐藏，避免模板对象直接参与显示。
        /// </summary>
        private void InitializeTemplates()
        {
            if (templates == null) return;

            for (int i = 0; i < templates.Length; i++)
            {
                if (templates[i] != null)
                {
                    templates[i].gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 校验当前 RecyclerView 的内容节点、模板与运行配置是否有效。
        /// </summary>
        private void ValidateConfiguration()
        {
            isValid = true;

            if (content == null)
            {
                if (transform.childCount == 0)
                {
                    LogValidationError("RecyclerView content is missing.");
                    isValid = false;
                    return;
                }

                content = transform.GetChild(0).GetComponent<RectTransform>();
                if (content == null)
                {
                    LogValidationError("RecyclerView content RectTransform is missing.");
                    isValid = false;
                    return;
                }
            }

            if (templates == null || templates.Length == 0)
            {
                LogValidationError("RecyclerView templates are missing.");
                isValid = false;
                return;
            }

            for (int i = 0; i < templates.Length; i++)
            {
                ViewHolder template = templates[i];
                if (template == null)
                {
                    LogValidationError("RecyclerView template is null.");
                    isValid = false;
                    return;
                }

            }
        }

        private void LogValidationError(string message)
        {
            if (validationErrorLogged)
            {
                return;
            }

            validationErrorLogged = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Log.Error(message);
#endif
        }

        /// <summary>
        /// 配置滚动器参数并注册滚动回调。
        /// </summary>
        private void ConfigureScroller()
        {
            if (scroller == null) return;

            scroller.ScrollSpeed = scrollSpeed;
            scroller.WheelSpeed = wheelSpeed;
            scroller.Snap = snap;
            scroller.MovementType = movementType;
            scroller.Inertia = inertia;
            scroller.DecelerationRate = decelerationRate;
            scroller.OnValueChanged += OnScrollChanged;
            scroller.OnMoveStoped += OnMoveStoped;
            scroller.OnDragging += OnScrollerDraggingChanged;
            UpdateScrollerState();
        }

        /// <summary>
        /// 配置滚动条监听与拖拽结束回调。
        /// </summary>
        private void ConfigureScrollbar()
        {
            if (scrollbar == null) return;

            scrollbar.onValueChanged.AddListener(OnScrollbarChanged);

            scrollbarEx = scrollbar.gameObject.GetComponent<ScrollbarEx>();
            if (scrollbarEx == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Log.Error("ScrollbarEx is missing. Add it in prefab/editor setup.");
#endif
                return;
            }

            scrollbarEx.OnDragEnd = OnScrollbarDragEnd;
            UpdateScrollbarVisibility();
        }

        #endregion

        #region 公共方法 - 初始化与绑定

        /// <summary>
        /// 绑定新的适配器，并重置 RecyclerView 与布局管理器之间的关联关系。
        /// </summary>
        /// <param name="adapter">要绑定的适配器实例。</param>
        internal void SetAdapter(IAdapter adapter)
        {
            if (adapter == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Log.Error("Adapter cannot be null");
#endif
                return;
            }

            if (layoutManager == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Log.Error("LayoutManager cannot be null");
#endif
                return;
            }

            if (ReferenceEquals(RecyclerViewAdapter, adapter))
            {
                return;
            }

            viewProvider?.Clear();
            RecyclerViewAdapter = adapter;
            ViewProvider.Adapter = adapter;
            ViewProvider.LayoutManager = layoutManager;

            layoutManager.RecyclerView = this;
            layoutManager.Adapter = adapter;
            layoutManager.ViewProvider = viewProvider;
            ApplyLayoutSettings();
            startIndex = 0;
            endIndex = -1;
            currentScrollDataIndex = -1;
        }

        /// <summary>
        /// 重置视图池、滚动位置与当前索引状态。
        /// </summary>
        internal void Reset()
        {
            if (!isValid)
            {
                return;
            }

            viewProvider?.Reset();

            if (scroller != null)
            {
                scroller.ResetMotion(0);
            }

            if (scrollbar != null)
            {
                scrollbar.SetValueWithoutNotify(0);
            }

            startIndex = 0;
            endIndex = -1;
            currentScrollDataIndex = -1;
        }

        public void TrimInactivePool()
        {
            viewProvider?.TrimInactive();
        }

        #endregion

        #region 公共方法 - 布局

        /// <summary>
        /// 按当前滚动位置重新创建可见范围内的所有视图持有者。
        /// </summary>
        internal void Refresh()
        {
            if (!isValid)
            {
                return;
            }

            ViewProvider.Clear();
            if (layoutManager == null || RecyclerViewAdapter == null || RecyclerViewAdapter.GetItemCount() <= 0)
            {
                startIndex = 0;
                endIndex = -1;
                return;
            }

            startIndex = layoutManager.GetStartIndex();
            endIndex = layoutManager.GetEndIndex();
            if (!layoutManager.UsesVirtualLayoutRange)
            {
                startIndex = Mathf.Max(0, startIndex);
            }

            endIndex = NormalizeRangeEnd(startIndex, endIndex, Mathf.Max(1, layoutManager.Unit));
            if (endIndex < startIndex)
            {
                return;
            }

            for (int i = startIndex; i <= endIndex; i += layoutManager.Unit)
            {
                ViewProvider.CreateViewHolder(i);
            }

            layoutManager.DoItemAnimation();
        }

        /// <summary>
        /// 重新计算内容尺寸、滚动能力与对象池预热状态。
        /// </summary>
        internal void RequestLayout()
        {
            if (!isValid)
            {
                return;
            }

            if (layoutManager == null)
            {
                UpdateScrollbarVisibility();
                return;
            }

            ApplyLayoutSettings();
            layoutManager.SetContentSize();

            if (scroller == null)
            {
                viewProvider?.PreparePool();
                UpdateScrollbarVisibility();
                return;
            }

            scroller.Direction = direction;
            scroller.ViewSize = layoutManager.ViewportSize;
            scroller.ContentSize = layoutManager.ContentSize;
            scroller.Position = scroller.ClampPosition(scroller.Position);
            viewProvider?.PreparePool();

            UpdateScrollerState();
            UpdateScrollbarVisibility();
            UpdateScrollbarValue(scroller.Position);
        }

        #endregion

        private void ApplyLayoutSettings()
        {
            if (layoutManager == null)
            {
                return;
            }

            layoutManager.Direction = direction;
            layoutManager.Alignment = alignment;
            layoutManager.Spacing = spacing;
            layoutManager.Padding = padding;
        }

        #region 公共方法 - 滚动

        private bool TryUpdateViewportSizeCache(out bool changed)
        {
            changed = false;
            if (cachedRectTransform == null)
            {
                cachedRectTransform = transform as RectTransform;
            }

            if (cachedRectTransform == null)
            {
                return false;
            }

            Vector2 size = cachedRectTransform.rect.size;
            if (hasLastViewportSize && size == lastViewportSize)
            {
                return true;
            }

            changed = hasLastViewportSize;
            lastViewportSize = size;
            hasLastViewportSize = true;
            return true;
        }

        /// <summary>
        /// 获取当前滚动位置。
        /// </summary>
        /// <returns>当前滚动偏移量；未启用滚动器时返回 <c>0</c>。</returns>
        public float GetScrollPosition()
        {
            return scroller != null ? scroller.Position : 0;
        }

        /// <summary>
        /// 将列表滚动到指定索引对应的位置。
        /// </summary>
        /// <param name="index">目标数据索引。</param>
        /// <param name="smooth">是否使用平滑滚动。</param>
        public void ScrollTo(int index, bool smooth = false)
        {
            if (!TryResolveScrollTarget(index, out int layoutIndex, out int dataIndex))
            {
                return;
            }

            scroller.ScrollTo(layoutManager.IndexToPosition(layoutIndex), smooth);

            if (!smooth)
            {
                Refresh();
            }

            SetCurrentScrollDataIndex(dataIndex);
        }

        /// <summary>
        /// 将列表滚动到指定索引，并按给定对齐方式定位。
        /// </summary>
        /// <param name="index">目标数据索引。</param>
        /// <param name="alignment">目标项滚动完成后的对齐方式。</param>
        /// <param name="offset">在对齐基础上的额外偏移量。</param>
        /// <param name="smooth">是否使用平滑滚动。</param>
        /// <param name="duration">平滑滚动时长，单位为秒。</param>
        internal void ScrollToWithAlignment(int index, ScrollAlignment alignment, float offset = 0f, bool smooth = false, float duration = 0.3f)
        {
            if (!TryResolveScrollTarget(index, out int layoutIndex, out int dataIndex))
            {
                return;
            }

            float targetPosition = CalculateScrollPositionWithAlignment(layoutIndex, alignment, offset);


            if (duration > 0 && smooth)
            {
                scroller.ScrollToDuration(targetPosition, duration);
            }
            else
            {
                scroller.ScrollTo(targetPosition, smooth);
            }

            if (!smooth)
            {
                Refresh();
            }

            SetCurrentScrollDataIndex(dataIndex);
        }

        /// <summary>
        /// 计算指定索引在目标对齐方式下应滚动到的位置。
        /// </summary>
        /// <param name="layoutIndex">目标布局索引。</param>
        /// <param name="alignment">目标项滚动完成后的对齐方式。</param>
        /// <param name="offset">在对齐基础上的额外偏移量。</param>
        /// <returns>计算得到的滚动位置，结果会被限制在合法范围内。</returns>
        private float CalculateScrollPositionWithAlignment(int layoutIndex, ScrollAlignment alignment, float offset)
        {
            float itemSize = layoutManager.GetItemLength(layoutIndex);
            float viewportLength = direction == Direction.Vertical ? layoutManager.ViewportSize.y : layoutManager.ViewportSize.x;

            // 计算目标项的原始位置，不在此阶段做范围限制。
            float itemPosition = layoutManager.GetItemStartPosition(layoutIndex);

            float targetPosition = alignment switch
            {
                ScrollAlignment.Start => itemPosition,
                ScrollAlignment.Center => itemPosition - (viewportLength - itemSize) / 2f,
                ScrollAlignment.End => itemPosition - viewportLength + itemSize,
                _ => itemPosition
            };

            // 叠加调用方传入的额外偏移量。
            targetPosition += offset;

            // 将结果限制在可滚动范围内。
            return scroller.ClampPosition(targetPosition);
        }

        private bool TryResolveScrollTarget(int index, out int layoutIndex, out int dataIndex)
        {
            layoutIndex = 0;
            dataIndex = -1;

            if (!isValid || scroll == ScrollMode.AlwaysDisable || scroller == null || layoutManager == null || RecyclerViewAdapter == null)
            {
                return false;
            }

            int itemCount = RecyclerViewAdapter.GetItemCount();
            if (itemCount <= 0)
            {
                return false;
            }

            int realCount = RecyclerViewAdapter.GetRealCount();
            bool isLoop = realCount > 0 && itemCount != realCount;
            if (!isLoop && (index < 0 || index >= itemCount))
            {
                return false;
            }

            layoutIndex = layoutManager.GetLayoutIndex(index);
            if (!layoutManager.UsesVirtualLayoutRange && (layoutIndex < 0 || layoutIndex >= itemCount))
            {
                return false;
            }

            dataIndex = layoutManager.GetDataIndex(layoutIndex);
            return isLoop || (dataIndex >= 0 && dataIndex < itemCount);
        }

        #endregion

        #region 私有方法 - 滚动回调

        /// <summary>
        /// 响应滚动器位置变化，更新布局、滚动条与可见区间。
        /// </summary>
        /// <param name="position">当前滚动位置。</param>
        private void OnScrollChanged(float position)
        {
            if (!isValid || layoutManager == null || RecyclerViewAdapter == null || viewProvider == null)
            {
                return;
            }

            bool fullRefreshed = UpdateVisibleRange();
            layoutManager.UpdateLayout();
            UpdateScrollbarValue(position);
            if (!fullRefreshed)
            {
                layoutManager.DoItemAnimation();
            }

            OnScrollValueChanged?.Invoke(position);
        }

        /// <summary>
        /// Handles scroller stop events and snap behavior.
        /// </summary>
        private void OnMoveStoped()
        {
            HandleScrollSettled();
        }

        /// <summary>
        /// 响应滚动器拖拽状态变化。
        /// </summary>
        /// <param name="dragging">当前是否正在拖拽。</param>
        private void OnScrollerDraggingChanged(bool dragging)
        {
            OnScrollDraggingChanged?.Invoke(dragging);
        }

        /// <summary>
        /// 响应滚动条值变化并同步滚动器位置。
        /// </summary>
        /// <param name="ratio">滚动条归一化值。</param>
        private void OnScrollbarChanged(float ratio)
        {
            if (scrollbarEx != null && scrollbarEx.IsDragging)
            {
                pendingScrollbarRatio = ratio;
                hasPendingScrollbarRatio = true;
                return;
            }

            if (scroller != null)
            {
                scroller.ScrollToRatio(ratio);
            }
        }

        /// <summary>
        /// 响应滚动条拖拽结束事件，并在需要时触发吸附。
        /// </summary>
        private void OnScrollbarDragEnd()
        {
            if (scroller == null) return;

            ProcessPendingScrollbarRatio();
            HandleScrollSettled();
        }

        #endregion

        #region 私有方法 - 滚动辅助

        /// <summary>
        /// 根据当前滚动位置同步滚动条显示值。
        /// </summary>
        /// <param name="position">当前滚动位置。</param>
        private void UpdateScrollbarValue(float position)
        {
            if (scrollbar != null && scroller != null)
            {
                float ratio = scroller.MaxPosition > 0 ? position / scroller.MaxPosition : 0;
                scrollbar.SetValueWithoutNotify(ratio);
            }
        }

        private void ProcessPendingScrollbarRatio()
        {
            if (!hasPendingScrollbarRatio)
            {
                return;
            }

            hasPendingScrollbarRatio = false;
            if (scroller != null)
            {
                scroller.ScrollToRatio(pendingScrollbarRatio);
            }
        }

        /// <summary>
        /// 根据当前滚动位置增量更新可见区间内的视图持有者。
        /// </summary>
        /// <returns>是否触发了全量刷新。</returns>
        private bool UpdateVisibleRange()
        {
            if (layoutManager == null || RecyclerViewAdapter == null || RecyclerViewAdapter.GetItemCount() <= 0)
            {
                return false;
            }

            int itemCount = RecyclerViewAdapter.GetItemCount();
            int unit = Mathf.Max(1, layoutManager.Unit);
            bool virtualRange = layoutManager.UsesVirtualLayoutRange;
            int targetStart = layoutManager.GetStartIndex();
            int targetEnd = layoutManager.GetEndIndex();
            if (!virtualRange)
            {
                targetStart = Mathf.Clamp(targetStart, 0, itemCount - 1);
                targetEnd = Mathf.Clamp(targetEnd, -1, itemCount - 1);
            }

            targetEnd = NormalizeRangeEnd(targetStart, targetEnd, unit);

            if (targetEnd < targetStart)
            {
                if (ViewProvider.VisibleCount > 0)
                {
                    ViewProvider.Clear();
                }

                startIndex = 0;
                endIndex = -1;
                return true;
            }

            if (startIndex == targetStart && endIndex == targetEnd)
            {
                return false;
            }

            if (ShouldRebuildVisibleRange(targetStart, targetEnd, unit))
            {
                RebuildVisibleRange(targetStart, targetEnd, unit);
                return true;
            }

            while (startIndex < targetStart)
            {
                viewProvider.RemoveViewHolder(startIndex);
                startIndex += unit;
            }

            while (endIndex > targetEnd)
            {
                viewProvider.RemoveViewHolder(endIndex);
                endIndex -= unit;
            }

            while (startIndex > targetStart)
            {
                startIndex -= unit;
                viewProvider.CreateViewHolder(startIndex);
            }

            while (endIndex < targetEnd)
            {
                endIndex += unit;
                viewProvider.CreateViewHolder(endIndex);
            }

            startIndex = targetStart;
            endIndex = targetEnd;
            return false;
        }

        private bool ShouldRebuildVisibleRange(int targetStart, int targetEnd, int unit)
        {
            if (endIndex < startIndex || ViewProvider.VisibleCount <= 0)
            {
                return true;
            }

            if (targetEnd < startIndex || targetStart > endIndex)
            {
                return true;
            }

            int currentGroups = CountRangeGroups(startIndex, endIndex, unit);
            int removeGroups = CountRangeGroups(startIndex, Mathf.Min(endIndex, targetStart - unit), unit) +
                               CountRangeGroups(Mathf.Max(startIndex, targetEnd + unit), endIndex, unit);
            int createGroups = CountRangeGroups(targetStart, Mathf.Min(targetEnd, startIndex - unit), unit) +
                               CountRangeGroups(Mathf.Max(targetStart, endIndex + unit), targetEnd, unit);

            return removeGroups >= currentGroups || createGroups >= currentGroups;
        }

        private static int CountRangeGroups(int rangeStart, int rangeEnd, int unit)
        {
            if (rangeEnd < rangeStart)
            {
                return 0;
            }

            return ((rangeEnd - rangeStart) / unit) + 1;
        }

        private static int NormalizeRangeEnd(int rangeStart, int rangeEnd, int unit)
        {
            if (rangeEnd < rangeStart)
            {
                return rangeEnd;
            }

            return rangeStart + ((rangeEnd - rangeStart) / unit) * unit;
        }

        private void RebuildVisibleRange(int targetStart, int targetEnd, int unit)
        {
            if (ViewProvider.TryReuseVisibleRange(targetStart, targetEnd))
            {
                startIndex = targetStart;
                endIndex = targetEnd;
                layoutManager.DoItemAnimation();
                return;
            }

            ViewProvider.Clear();
            startIndex = targetStart;
            endIndex = targetEnd;

            for (int i = targetStart; i <= targetEnd; i += unit)
            {
                ViewProvider.CreateViewHolder(i);
            }

            layoutManager.DoItemAnimation();
        }

        /// <summary>
        /// 根据当前状态更新滚动条的显示与交互能力。
        /// </summary>
        private void UpdateScrollbarVisibility()
        {
            if (scrollbar == null)
            {
                return;
            }

            bool shouldShow = ShouldShowScrollbar();
            bool shouldInteract = shouldShow && ShouldEnableScrollbarInteraction();
            if (scrollbarVisibleState != shouldShow || scrollbar.gameObject.activeSelf != shouldShow)
            {
                scrollbarVisibleState = shouldShow;
                scrollbar.gameObject.SetActive(shouldShow);
            }

            if (scrollbarInteractableState != shouldInteract || scrollbar.interactable != shouldInteract)
            {
                scrollbarInteractableState = shouldInteract;
                scrollbar.interactable = shouldInteract;
            }

            if (shouldShow)
            {
                ConfigureScrollbarDirection();
                ConfigureScrollbarSize();
            }
        }

        /// <summary>
        /// 判断当前是否应显示滚动条。
        /// </summary>
        /// <returns>应显示滚动条时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        private bool ShouldShowScrollbar()
        {
            if (scrollbarVisibility == ScrollbarVisibility.AlwaysHide || scrollbar == null || scroller == null || layoutManager == null)
            {
                return false;
            }

            if (scrollbarVisibility == ScrollbarVisibility.AlwaysShow)
            {
                return true;
            }

            if (!SupportsOverflowCheck())
            {
                return false;
            }

            if (!HasValidScrollMetrics())
            {
                return false;
            }

            return HasScrollableContent();
        }

        /// <summary>
        /// 根据列表方向设置滚动条方向。
        /// </summary>
        private void ConfigureScrollbarDirection()
        {
            scrollbar.direction = direction == Direction.Vertical
                ? Scrollbar.Direction.TopToBottom
                : Scrollbar.Direction.LeftToRight;
        }

        /// <summary>
        /// 根据内容尺寸与视口尺寸更新滚动条手柄长度。
        /// </summary>
        private void ConfigureScrollbarSize()
        {
            float contentLength;
            float viewLength;
            float trackLength;

            if (direction == Direction.Vertical)
            {
                contentLength = scroller.ContentSize.y;
                viewLength = scroller.ViewSize.y;
                trackLength = GetScrollbarTrackLength(true);
            }
            else
            {
                contentLength = scroller.ContentSize.x;
                viewLength = scroller.ViewSize.x;
                trackLength = GetScrollbarTrackLength(false);
            }

            if (contentLength <= 0f || viewLength <= 0f)
            {
                scrollbar.size = 1f;
                return;
            }

            float normalizedSize = viewLength / contentLength;
            float minNormalizedSize = trackLength > 0f
                ? Mathf.Clamp01(MinScrollbarHandlePixels / trackLength)
                : 0f;

            scrollbar.size = Mathf.Clamp(Mathf.Max(normalizedSize, minNormalizedSize), minNormalizedSize, 1f);
        }

        /// <summary>
        /// 获取滚动条轨道的像素长度。
        /// </summary>
        /// <param name="vertical">是否按垂直滚动条计算。</param>
        /// <returns>滚动条轨道长度；无法获取时返回 <c>0</c>。</returns>
        private float GetScrollbarTrackLength(bool vertical)
        {
            if (scrollbar == null)
            {
                return 0f;
            }

            RectTransform scrollbarRect = scrollbar.transform as RectTransform;
            if (scrollbarRect == null)
            {
                return 0f;
            }

            Rect rect = scrollbarRect.rect;
            return vertical ? rect.height : rect.width;
        }

        /// <summary>
        /// 根据当前内容是否可滚动来更新滚动器启用状态。
        /// </summary>
        private void UpdateScrollerState()
        {
            if (scroller == null)
            {
                return;
            }

            scroller.InputEnabled = ShouldEnableScrollInput();
        }

        /// <summary>
        /// 判断是否需要将滚动能力限制在内容溢出场景下才启用。
        /// </summary>
        /// <returns>仅在内容溢出时才启用滚动时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        private bool ShouldEnableScrollInput()
        {
            if (scroll == ScrollMode.AlwaysDisable)
            {
                return false;
            }

            if (scroll == ScrollMode.AlwaysEnable)
            {
                return true;
            }

            return SupportsOverflowCheck() && HasScrollableContent();
        }

        private bool ShouldEnableScrollbarInteraction()
        {
            return ShouldEnableScrollInput() && HasValidScrollMetrics() && HasScrollableContent();
        }

        /// <summary>
        /// 判断当前内容尺寸是否超过视口尺寸。
        /// </summary>
        /// <returns>内容可滚动时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        private bool HasScrollableContent()
        {
            if (layoutManager == null)
            {
                return false;
            }

            if (direction == Direction.Vertical)
            {
                return layoutManager.ContentSize.y > layoutManager.ViewportSize.y;
            }

            if (direction == Direction.Horizontal)
            {
                return layoutManager.ContentSize.x > layoutManager.ViewportSize.x;
            }

            return false;
        }

        /// <summary>
        /// 判断当前方向是否支持溢出检测。
        /// </summary>
        /// <returns>支持垂直或水平溢出检测时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        private bool SupportsOverflowCheck()
        {
            return direction == Direction.Vertical || direction == Direction.Horizontal;
        }

        /// <summary>
        /// 判断当前布局是否已经具备有效的滚动尺寸信息。
        /// </summary>
        /// <returns>内容尺寸与视口尺寸均有效时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        private bool HasValidScrollMetrics()
        {
            if (direction == Direction.Vertical)
            {
                return layoutManager.ContentSize.y > 0f && layoutManager.ViewportSize.y > 0f;
            }

            if (direction == Direction.Horizontal)
            {
                return layoutManager.ContentSize.x > 0f && layoutManager.ViewportSize.x > 0f;
            }

            return false;
        }

        /// <summary>
        /// 将滚动位置吸附到最近的列表项。
        /// </summary>
        /// <returns>触发了新的吸附滚动时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        private bool SnapToNearestItem()
        {
            if (layoutManager == null || RecyclerViewAdapter == null || RecyclerViewAdapter.GetItemCount() <= 0)
            {
                return false;
            }

            float position = GetScrollPosition();
            int index = layoutManager.GetSnapIndex(position);
            float targetPosition = layoutManager.IndexToPosition(index);
            SetCurrentScrollDataIndex(layoutManager.GetDataIndex(index));
            if (Mathf.Abs(targetPosition - position) <= 0.1f)
            {
                return false;
            }

            scroller.ScrollToDuration(targetPosition, 0.12f);
            return true;
        }

        private void HandleScrollSettled()
        {
            if (snap && SnapToNearestItem())
            {
                return;
            }

            UpdateCurrentScrollDataIndexFromScrollPosition();
            OnScrollStopped?.Invoke();
        }

        private void UpdateCurrentScrollDataIndexFromScrollPosition()
        {
            if (layoutManager == null || RecyclerViewAdapter == null || RecyclerViewAdapter.GetItemCount() <= 0)
            {
                SetCurrentScrollDataIndex(-1);
                return;
            }

            int layoutIndex = layoutManager.PositionToIndex(GetScrollPosition());
            if (!layoutManager.UsesVirtualLayoutRange)
            {
                layoutIndex = Mathf.Clamp(layoutIndex, 0, RecyclerViewAdapter.GetItemCount() - 1);
            }

            SetCurrentScrollDataIndex(layoutManager.GetDataIndex(layoutIndex));
        }

        private void SetCurrentScrollDataIndex(int index)
        {
            int itemCount = GetScrollDataIndexItemCount();
            if (itemCount <= 0)
            {
                currentScrollDataIndex = -1;
                return;
            }

            if (index < 0)
            {
                currentScrollDataIndex = -1;
                return;
            }

            index %= itemCount;
            currentScrollDataIndex = index;
        }

        private int GetScrollDataIndexItemCount()
        {
            if (RecyclerViewAdapter == null)
            {
                return 0;
            }

            int itemCount = RecyclerViewAdapter.GetRealCount();
            if (itemCount > 0)
            {
                return itemCount;
            }

            return RecyclerViewAdapter.GetItemCount();
        }

        /// <summary>
        /// 重新绑定当前可见区域内指定数据索引对应的所有持有者。
        /// </summary>
        /// <param name="dataIndex">要重绑的数据索引。</param>
        /// <returns>实际完成重绑的持有者数量。</returns>
        internal int RebindVisibleDataIndex(int dataIndex)
        {
            if (RecyclerViewAdapter == null)
            {
                return 0;
            }

            return ViewProvider.RebindVisibleDataIndex(dataIndex);
        }

        /// <summary>
        /// 重新绑定当前可见区域内指定数据区间对应的所有持有者。
        /// </summary>
        /// <param name="startDataIndex">起始数据索引。</param>
        /// <param name="count">需要重绑的数据项数量。</param>
        /// <returns>实际完成重绑的持有者总数。</returns>
        internal int RebindVisibleDataRange(int startDataIndex, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            int reboundCount = 0;
            int endDataIndex = startDataIndex + count;
            for (int dataIndex = startDataIndex; dataIndex < endDataIndex; dataIndex++)
            {
                reboundCount += RebindVisibleDataIndex(dataIndex);
            }

            return reboundCount;
        }

        internal int ApplyVisibleSelection(int dataIndex, bool selected)
        {
            return ViewProvider.ApplyVisibleSelection(dataIndex, selected);
        }

        #endregion
    }
}
