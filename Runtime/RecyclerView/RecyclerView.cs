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
        /// 内容不足一屏时是否在交叉轴上居中。
        /// </summary>
        [HideInInspector] [SerializeField] private bool centerCrossAxis;

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

        [HideInInspector] [SerializeField, Min(0.01f)]
        private float snapDuration = 0.12f;

        [HideInInspector] [SerializeField]
        private ListRefreshMode refreshMode = ListRefreshMode.Immediate;

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

        /// <summary>
        /// 待处理的刷新意图。两个 bool 标志会互相覆盖，且一次数据变更会被重复登记，
        /// 因此收敛成一个"取最重"的枚举，每帧只 flush 一次。
        /// </summary>
        private DirtyKind pendingDirty;

        private bool applyingDeferredRefresh;
        private readonly List<ViewHolder> pendingMeasurements = new List<ViewHolder>(16);
        private readonly List<MeasuredItemLength> measuredItemLengths = new List<MeasuredItemLength>(16);
        private int measurementBatchDepth;
        private bool flushingMeasurements;
        private RectTransform cachedRectTransform;
        private Vector2 lastViewportSize;
        private bool hasLastViewportSize;
#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
        private RecyclerViewNavigationController cachedNavigationController;
        private bool navigationControllerResolved;
#endif

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
                MarkDirty(DirtyKind.Layout);
            }
        }

        /// <summary>
        /// 获取或设置内容不足一屏时是否在交叉轴上居中。
        /// </summary>
        public bool CenterCrossAxis
        {
            get => centerCrossAxis;
            set
            {
                if (centerCrossAxis == value)
                {
                    return;
                }

                centerCrossAxis = value;
                MarkDirty(DirtyKind.Layout);
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
                MarkDirty(DirtyKind.Layout);
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
                MarkDirty(DirtyKind.Layout);
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
                UpdateScrollerState();
            }
        }

        /// <summary>
        /// 获取或设置是否在停止滚动后自动吸附到最近项。
        /// </summary>
        public bool Snap
        {
            get => snap;
            set => snap = value;
        }

        public float SnapDuration
        {
            get => snapDuration;
            set => snapDuration = Mathf.Max(0.01f, value);
        }

        public ListRefreshMode RefreshMode
        {
            get => refreshMode;
            set => refreshMode = value;
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
                MarkDirty(DirtyKind.Layout);
            }
        }

        #endregion


        #region 公共属性 - 组件引用

        /// <summary>
        /// 列表项模板。只允许在编辑期配置，运行期禁止更换。
        /// </summary>
        public ViewHolder[] Templates => templates;

        /// <summary>
        /// 获取内容节点。必须在 Inspector 中显式指定。
        /// </summary>
        public RectTransform Content => content;

        /// <summary>
        /// 获取当前绑定的滚动条组件。
        /// </summary>
        public Scrollbar Scrollbar => scrollbar;

        /// <summary>
        /// 获取当前绑定的滚动器实例。
        /// </summary>
        public Scroller Scroller => scroller;

        internal ViewProvider ViewProvider => viewProvider;

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

        public int ScrollDataIndex => currentScrollDataIndex;

        #endregion

        #region 事件

        /// <summary>
        /// 当滚动位置发生变化时触发。
        /// </summary>
        public event Action<float> OnScrollValueChanged;

        /// <summary>
        /// 当滚动停止时触发。
        /// </summary>
        public event Action<ScrollStopReason> OnScrollStopped;

        /// <summary>
        /// 当拖拽状态变化时触发。
        /// </summary>
        public event Action<bool> OnScrollDraggingChanged;

        public event Action<int, float> OnItemResized;
#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
        internal event Action OnNavigationDataChanged;
        internal event Action<ViewHolder> OnHolderRecycled;
#endif

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

            UpdateViewportSizeCache();
            InitializeTemplates();
            ConfigureScroller();
            ConfigureScrollbar();
        }

        private void OnEnable()
        {
            if (IsReady)
            {
                // 重新启用是生命周期边界：被 AfterSettle 推迟的刷新必须在这里补上。
                FlushDeferredWork(true);
            }
        }

        private void LateUpdate()
        {
            ProcessPendingScrollbarRatio();
            FlushDeferredWork();
        }

        private void OnDisable()
        {
            // 保留 pendingDirty，OnEnable 或下次 LateUpdate 继续处理，避免隐藏期间丢失刷新。
            applyingDeferredRefresh = false;
            ResetItemPresentation();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            UpdateViewportSizeCache();
        }

        private void OnDestroy()
        {
            if (scroller != null)
            {
                scroller.OnValueChanged -= OnScrollChanged;
                scroller.OnMoveStopped -= OnMoveStopped;
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
                LogValidationError("RecyclerView content is missing.");
                isValid = false;
                return;
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

                if (ItemMeasure.IsStretchedOnAxis(template.RectTransform, direction))
                {
                    LogValidationError("RecyclerView template is stretched on the scroll axis.");
                    isValid = false;
                    return;
                }
            }

            if (layoutManager.VariableItemLengthEnabled && !layoutManager.SupportsVariableItemLength)
            {
                LogValidationError("Current layout manager does not support variable item length.");
                isValid = false;
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

        internal void ApplySerializedScrollerSettings()
        {
            if (scroller == null)
            {
                return;
            }

            scroller.ScrollSpeed = scrollSpeed;
            scroller.WheelSpeed = wheelSpeed;
            scroller.MovementType = movementType;
            scroller.Inertia = inertia;
            scroller.DecelerationRate = decelerationRate;
        }

        /// <summary>
        /// 配置滚动器参数并注册滚动回调。
        /// </summary>
        private void ConfigureScroller()
        {
            if (scroller == null) return;

            ApplySerializedScrollerSettings();
            // 幂等：Awake 与外部手动配置可能各调用一次，重复 += 会让每次滚动都走两遍布局。
            scroller.OnValueChanged -= OnScrollChanged;
            scroller.OnMoveStopped -= OnMoveStopped;
            scroller.OnDragging -= OnScrollerDraggingChanged;
            scroller.OnValueChanged += OnScrollChanged;
            scroller.OnMoveStopped += OnMoveStopped;
            scroller.OnDragging += OnScrollerDraggingChanged;
            UpdateScrollerState();
        }

        /// <summary>
        /// 配置滚动条监听与拖拽结束回调。
        /// </summary>
        private void ConfigureScrollbar()
        {
            if (scrollbar == null) return;

            scrollbar.onValueChanged.RemoveListener(OnScrollbarChanged);
            scrollbar.onValueChanged.AddListener(OnScrollbarChanged);

            scrollbarEx = scrollbar.gameObject.GetComponent<ScrollbarEx>();
            if (scrollbarEx == null)
            {
                // 配置错误必须在 player 里也能看到，否则滚动条会静默失效。
                Log.Error("ScrollbarEx is missing. Add it in prefab/editor setup.");
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
                throw new ArgumentNullException(nameof(adapter));
            }

            if (layoutManager == null)
            {
                throw new InvalidOperationException("RecyclerView requires a layout manager before an adapter can be bound.");
            }

            if (ReferenceEquals(RecyclerViewAdapter, adapter))
            {
                return;
            }

            if (RecyclerViewAdapter != null)
            {
                throw new InvalidOperationException(
                    $"RecyclerView already has adapter '{RecyclerViewAdapter.GetType().Name}'; binding '{adapter.GetType().Name}' at runtime is not supported.");
            }

            RecyclerViewAdapter = adapter;
            BindViewProvider();
            layoutManager.RecyclerView = this;
            layoutManager.Adapter = adapter;
            layoutManager.ViewProvider = viewProvider;
            ApplyLayoutSettings();
            startIndex = 0;
            endIndex = -1;
            currentScrollDataIndex = -1;
        }

        /// <summary>
        /// 重置视图池。默认保留当前滚动像素；ResetScroll 才回到顶部。
        /// </summary>
        internal void Reset(ListScrollPolicy scrollPolicy = ListScrollPolicy.KeepAnchor)
        {
            if (!IsReady)
            {
                return;
            }

            pendingDirty = DirtyKind.None;
            viewProvider.Clear();
            startIndex = 0;
            endIndex = -1;

            if (scrollPolicy != ListScrollPolicy.ResetScroll)
            {
                return;
            }

            if (scroller != null)
            {
                scroller.ResetMotion(0);
            }

            if (scrollbar != null)
            {
                scrollbar.SetValueWithoutNotify(0);
            }

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
            MarkDirty(DirtyKind.Rebuild);
        }

        /// <summary>
        /// 重新计算内容尺寸、滚动能力与对象池预热状态。
        /// </summary>
        internal void RequestLayout()
        {
            MarkDirty(DirtyKind.Layout);
        }

        /// <summary>
        /// 数据整体变化（数量/类型都可能变）：一次全量重建即可，不能再叠加一次 Refresh。
        /// </summary>
        internal void OnDataSetChanged()
        {
            layoutManager?.InvalidateItemLengths();
            MarkDirty(DirtyKind.Rebuild);
        }

        #endregion

        private bool IsReady => isValid && layoutManager != null && RecyclerViewAdapter != null && viewProvider != null;

        private void BindViewProvider()
        {
            if (viewProvider != null || templates == null || templates.Length == 0)
            {
                return;
            }

            viewProvider = new ViewProvider(this, templates)
            {
                Adapter = RecyclerViewAdapter,
                LayoutManager = layoutManager
            };
        }

        private enum DirtyKind
        {
            None = 0,
            Layout = 1,
            Rebuild = 2
        }

        private bool ShouldDeferRefresh =>
            refreshMode == ListRefreshMode.AfterSettle &&
            scroller != null &&
            (scroller.IsDragging || scroller.IsMoving);

        /// <summary>
        /// 登记一次刷新意图。刷新只在 <see cref="FlushDeferredWork"/> 里发生一次；
        /// 拖拽/惯性期间一律推迟，这正是 <see cref="ListRefreshMode.AfterSettle"/> 的语义。
        /// </summary>
        private void MarkDirty(DirtyKind kind)
        {
            if (kind > pendingDirty)
            {
                pendingDirty = kind;
            }

            FlushDeferredWork();
        }

        private void FlushDeferredWork(bool force = false)
        {
            if (pendingDirty == DirtyKind.None || !IsReady || applyingDeferredRefresh)
            {
                return;
            }

            if (!force && ShouldDeferRefresh)
            {
                return;
            }

            DirtyKind dirty = pendingDirty;
            pendingDirty = DirtyKind.None;
            applyingDeferredRefresh = true;
            try
            {
                ApplyLayoutNow();
                if (dirty == DirtyKind.Rebuild)
                {
                    RebuildVisibleHolders();
                    return;
                }

                UpdateVisibleRange();
                layoutManager.DoItemAnimation();
                layoutManager.UpdateLayout();
            }
            finally
            {
                applyingDeferredRefresh = false;
                // 绑定/测量里抛出的异常不能把批次计数闩死，否则后续刷新全部失效。
                measurementBatchDepth = 0;
            }
        }

        private void ApplyLayoutNow()
        {
            Debug.Assert(IsReady, "ApplyLayoutNow requires a bound adapter and layout manager.");

            ApplyLayoutSettings();
            layoutManager.SetContentSize();

            if (scroller != null)
            {
                scroller.Direction = direction;
                scroller.ViewSize = layoutManager.ViewportSize;
                scroller.ContentSize = layoutManager.ContentSize;
                ClampScrollPositionIfNeeded();
            }

            viewProvider.PreparePool();
            UpdateScrollerState();
            UpdateScrollbarVisibility();
            UpdateScrollbarValue(scroller != null ? scroller.Position : 0f);
        }

        private void ClampScrollPositionIfNeeded()
        {
            scroller.Position = ClampScrollTarget(scroller.Position);
        }

        /// <summary>
        /// 越界回弹允许保持过冲，其余情况一律夹到合法区间。这条规则只在这里定义一次。
        /// </summary>
        private float ClampScrollTarget(float target)
        {
            return scroller.IsPositionInBounds || movementType == MovementType.Clamped
                ? scroller.ClampPosition(target)
                : target;
        }

        /// <summary>
        /// 唯一允许回收全部可见项并重新创建的地方（适配器/模板变更、循环重锚）。
        /// 增量数据变化走 <see cref="UpdateVisibleRange"/>，不要走这里。
        /// </summary>
        private void RebuildVisibleHolders()
        {
            if (!IsReady)
            {
                startIndex = 0;
                endIndex = -1;
                return;
            }

            viewProvider.Clear();
            if (RecyclerViewAdapter.GetItemCount() <= 0)
            {
                startIndex = 0;
                endIndex = -1;
                viewProvider.ValidateInvariants();
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
                viewProvider.ValidateInvariants();
                return;
            }

            BeginMeasurementBatch();
            for (int i = startIndex; i <= endIndex; i += layoutManager.Unit)
            {
                viewProvider.CreateViewHolder(i);
            }

            bool measured = EndMeasurementBatch();
            if (layoutManager.VariableItemLengthEnabled && !measured)
            {
                UpdateVisibleRange();
                layoutManager.UpdateLayout();
            }

            layoutManager.DoItemAnimation();
            viewProvider.ValidateInvariants();
        }

        private void ResetItemPresentation()
        {
            if (viewProvider == null)
            {
                return;
            }

            int visibleCount = viewProvider.VisibleCount;
            for (int i = 0; i < visibleCount; i++)
            {
                ViewHolder viewHolder = viewProvider.GetVisibleViewHolder(i);
                if (viewHolder == null)
                {
                    continue;
                }

                viewHolder.RectTransform.localScale = Vector3.one;
            }
        }

        private void ApplyLayoutSettings()
        {
            Debug.Assert(layoutManager != null, "ApplyLayoutSettings requires a layout manager.");
            layoutManager.Direction = direction;
            layoutManager.CenterCrossAxis = centerCrossAxis;
            layoutManager.Spacing = spacing;
            layoutManager.Padding = padding;
        }

        #region 公共方法 - 滚动

        /// <summary>
        /// 视口尺寸缓存；尺寸真的变化时登记一次布局刷新。
        /// </summary>
        private void UpdateViewportSizeCache()
        {
            if (cachedRectTransform == null)
            {
                cachedRectTransform = transform as RectTransform;
            }

            if (cachedRectTransform == null)
            {
                return;
            }

            Vector2 size = cachedRectTransform.rect.size;
            if (hasLastViewportSize && size == lastViewportSize)
            {
                return;
            }

            bool changed = hasLastViewportSize;
            lastViewportSize = size;
            hasLastViewportSize = true;
            if (changed)
            {
                MarkDirty(DirtyKind.Layout);
            }
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
        public void ScrollToIndex(int index, bool smooth = false)
        {
            if (!TryResolveScrollTarget(index, out int layoutIndex, out int dataIndex))
            {
                return;
            }

            scroller.ScrollTo(layoutManager.IndexToPosition(layoutIndex), smooth);

            if (!smooth)
            {
                RebuildVisibleHolders();
            }

            SetCurrentScrollDataIndex(dataIndex);
        }

        public bool IsAtEnd => scroller != null && scroller.Position >= scroller.MaxPosition - 0.5f;

        public bool IsScrolling => scroller != null && (scroller.IsDragging || scroller.IsMoving);

        public void StickToEnd(bool smooth = false)
        {
            if (scroller == null)
            {
                return;
            }

            scroller.ScrollTo(scroller.MaxPosition, smooth);
            if (!smooth)
            {
                RebuildVisibleHolders();
            }
        }

        public void StopScroll()
        {
            scroller?.StopMovement(ScrollStopReason.Interrupted);
        }

        public bool TryGetVisibleRange(out int startDataIndex, out int endDataIndex)
        {
            startDataIndex = -1;
            endDataIndex = -1;
            return IsReady && viewProvider.TryGetVisibleDataRange(out startDataIndex, out endDataIndex);
        }

        public bool TryGetViewHolder(int dataIndex, out ViewHolder viewHolder)
        {
            viewHolder = null;
            if (!IsReady || dataIndex < 0)
            {
                return false;
            }

            viewHolder = viewProvider.FindByDataIndex(dataIndex);
            return viewHolder != null;
        }

        /// <summary>
        /// 将列表滚动到指定索引，并按给定对齐方式定位。
        /// </summary>
        /// <param name="index">目标数据索引。</param>
        /// <param name="alignment">目标项滚动完成后的对齐方式。</param>
        /// <param name="offset">在对齐基础上的额外偏移量。</param>
        /// <param name="smooth">是否使用平滑滚动。</param>
        /// <param name="duration">平滑滚动时长，单位为秒。</param>
        public void ScrollToAligned(int index, ScrollAlignment alignment, float offset = 0f, bool smooth = false, float duration = 0.3f)
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
                RebuildVisibleHolders();
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
            bool isLoop = IsLoopingAdapter();
            if (!isLoop && (index < 0 || index >= itemCount))
            {
                return false;
            }

            layoutIndex = isLoop && index >= realCount ? index : layoutManager.GetLayoutIndex(index);
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
            if (!IsReady)
            {
                return;
            }

            TryReanchorLoopWindow(ref position);

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
        /// Loop 列表滚到虚拟地址空间边缘时，静默跳到中段等价位置，保持 data 连续。
        /// 调用点已经用 <see cref="IsReady"/> 保证引用非空，这里不再重复判空。
        /// </summary>
        private bool TryReanchorLoopWindow(ref float position)
        {
            if (!IsLoopingAdapter())
            {
                return false;
            }

            int realCount = RecyclerViewAdapter.GetRealCount();
            int virtualCount = RecyclerViewAdapter.GetItemCount();
            int layoutIndex = layoutManager.PositionToIndex(position);
            if (!LoopVirtualRange.ShouldReanchor(layoutIndex, realCount, virtualCount))
            {
                return false;
            }

            int dataIndex = layoutManager.GetDataIndex(layoutIndex);
            int targetLayoutIndex = LoopVirtualRange.GetMiddleAnchorLayoutIndex(dataIndex, realCount, virtualCount);
            if (targetLayoutIndex == layoutIndex)
            {
                return false;
            }

            float itemStart = layoutManager.GetItemStartPosition(layoutIndex);
            float offsetInItem = position - itemStart;
            float targetStart = layoutManager.GetItemStartPosition(targetLayoutIndex);
            float targetPosition = scroller.ClampPosition(targetStart + offsetInItem);
            if (Mathf.Approximately(targetPosition, position))
            {
                return false;
            }

            scroller.Position = targetPosition;
            position = targetPosition;
            PublishVisibleLayout();
            return true;
        }

        /// <summary>
        /// Handles scroller stop events and snap behavior.
        /// </summary>
        private void OnMoveStopped(ScrollStopReason reason)
        {
            HandleScrollSettled(reason);
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
            HandleScrollSettled(ScrollStopReason.Completed);
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
            int itemCount = RecyclerViewAdapter != null ? RecyclerViewAdapter.GetItemCount() : 0;
            if (layoutManager == null || itemCount <= 0)
            {
                return false;
            }

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
                if (viewProvider.VisibleCount > 0)
                {
                    viewProvider.Clear();
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

            BeginMeasurementBatch();
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
            EndMeasurementBatch();
            return false;
        }

        private bool ShouldRebuildVisibleRange(int targetStart, int targetEnd, int unit)
        {
            if (endIndex < startIndex || viewProvider.VisibleCount <= 0)
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
            BeginMeasurementBatch();
            if (!viewProvider.TryReuseVisibleRange(targetStart, targetEnd))
            {
                viewProvider.Clear();
                for (int i = targetStart; i <= targetEnd; i += unit)
                {
                    viewProvider.CreateViewHolder(i);
                }
            }

            startIndex = targetStart;
            endIndex = targetEnd;
            EndMeasurementBatch();
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

            ScrollMetrics metrics = GetScrollMetrics();
            bool shouldShow = ShouldShowScrollbar(metrics);
            bool shouldInteract = shouldShow && ShouldEnableScrollbarInteraction(metrics);
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
        /// 一次布局内滚动相关的尺寸只算一次，供滚动条/滚动器共用。
        /// </summary>
        private ScrollMetrics GetScrollMetrics()
        {
            if (layoutManager == null)
            {
                return default;
            }

            float contentLength = direction == Direction.Vertical
                ? layoutManager.ContentSize.y
                : layoutManager.ContentSize.x;
            float viewportLength = direction == Direction.Vertical
                ? layoutManager.ViewportSize.y
                : layoutManager.ViewportSize.x;
            return new ScrollMetrics(
                contentLength > 0f && viewportLength > 0f,
                contentLength > viewportLength);
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
            float contentLength = direction == Direction.Vertical ? scroller.ContentSize.y : scroller.ContentSize.x;
            float viewLength = direction == Direction.Vertical ? scroller.ViewSize.y : scroller.ViewSize.x;
            float trackLength = GetScrollbarTrackLength();

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
        private float GetScrollbarTrackLength()
        {
            if (scrollbar.transform is not RectTransform scrollbarRect)
            {
                return 0f;
            }

            Rect rect = scrollbarRect.rect;
            return direction == Direction.Vertical ? rect.height : rect.width;
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

            scroller.InputEnabled = ShouldEnableScrollInput(GetScrollMetrics());
        }

        /// <summary>
        /// 内容溢出时才启用滚动。
        /// </summary>
        private bool ShouldEnableScrollInput(ScrollMetrics metrics)
        {
            switch (scroll)
            {
                case ScrollMode.AlwaysDisable:
                    return false;
                case ScrollMode.AlwaysEnable:
                    return true;
                default:
                    return metrics.Scrollable;
            }
        }

        private bool ShouldEnableScrollbarInteraction(ScrollMetrics metrics)
        {
            return ShouldEnableScrollInput(metrics) && metrics.Scrollable;
        }

        /// <summary>
        /// 判断当前是否应显示滚动条。
        /// </summary>
        private bool ShouldShowScrollbar(ScrollMetrics metrics)
        {
            switch (scrollbarVisibility)
            {
                case ScrollbarVisibility.AlwaysHide:
                    return false;
                case ScrollbarVisibility.AlwaysShow:
                    return true;
                default:
                    return metrics.Valid && metrics.Scrollable;
            }
        }

        private readonly struct ScrollMetrics
        {
            public readonly bool Valid;
            public readonly bool Scrollable;

            public ScrollMetrics(bool valid, bool scrollable)
            {
                Valid = valid;
                Scrollable = scrollable;
            }
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

            scroller.ScrollToDuration(targetPosition, snapDuration);
            return true;
        }

        private void HandleScrollSettled(ScrollStopReason reason)
        {
            if (reason == ScrollStopReason.Completed && snap && SnapToNearestItem())
            {
                return;
            }

            FlushDeferredWork();
            UpdateCurrentScrollDataIndexFromScrollPosition();
            OnScrollStopped?.Invoke(reason);
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
        internal int RelayoutDataIndex(int dataIndex)
        {
            return RelayoutDataRange(dataIndex, 1);
        }

        internal int RelayoutDataRange(int startDataIndex, int count)
        {
            if (!IsReady || count <= 0)
            {
                return 0;
            }

            // 改高可能发生在视口内，也可能发生在视口上方（滚到下面再改首条）。
            // 这里不能用 GetItemStartPosition 取锚点：调用方（Adapter.NotifyItemRangeChanged）已经先 Revise()，
            // 读一次就会按新长度把偏移表重算，算出来的偏移正好把补偿抵消掉。
            // 因此只记录"首条可见项"的渲染坐标（顶边），改完把它顶回原位。
            bool hasAnchor = TryCaptureAxisAnchor(out int anchorDataIndex, out float anchorTop);
            bool stickToEnd = IsPinnedToEnd();

            BeginMeasurementBatch();
            int rebound = viewProvider.RebindVisibleDataRange(startDataIndex, count);
            EndMeasurementBatch();

            if (layoutManager.VariableItemLengthEnabled)
            {
                // 命中可见项时，测量里的 RefreshOffsetTable 会先按"声明长度"把表修好，
                // ReplaceLength 因此返回 false（认为没变化），测量批就不会发布布局 ——
                // 内容高度与可见项位置会停留在旧值（改高首条"没反应"就是这个）。
                // 声明长度是权威值，这里统一失效并重新发布一次布局。
                bool declaresLength = RecyclerViewAdapter != null &&
                                      RecyclerViewAdapter.TryGetDeclaredLength(startDataIndex, out float declared) &&
                                      declared > 0f;
                if (rebound <= 0 || declaresLength)
                {
                    layoutManager.InvalidateItemLengths();
                }

                ApplyLayoutNow();
                layoutManager.UpdateLayout();

                if (stickToEnd)
                {
                    scroller.Position = scroller.MaxPosition;
                }
                else if (hasAnchor && TryGetAxisAnchorTop(anchorDataIndex, out float currentTop))
                {
                    float delta = anchorTop - currentTop;
                    if (!Mathf.Approximately(delta, 0f))
                    {
                        scroller.Position = ClampScrollTarget(scroller.Position + delta);
                    }
                }

                UpdateVisibleRange();
                layoutManager.UpdateLayout();
            }

            return rebound;
        }

        /// <summary>记录当前首条可见项的数据索引与主轴"顶边"渲染坐标（不读偏移表）。</summary>
        private bool TryCaptureAxisAnchor(out int dataIndex, out float top)
        {
            dataIndex = -1;
            top = 0f;
            if (!IsReady || scroller == null || viewProvider == null)
            {
                return false;
            }

            ViewHolder holder = viewProvider.FindFirstVisible();
            if (holder == null || holder.DataIndex < 0)
            {
                return false;
            }

            dataIndex = holder.DataIndex;
            top = AxisTop(holder);
            return true;
        }

        /// <summary>按数据索引取当前主轴顶边坐标；找不到（已回收）时返回 false。</summary>
        private bool TryGetAxisAnchorTop(int dataIndex, out float top)
        {
            top = 0f;
            ViewHolder holder = viewProvider != null ? viewProvider.FindByDataIndex(dataIndex) : null;
            if (holder == null)
            {
                return false;
            }

            top = AxisTop(holder);
            return true;
        }

        private float AxisTop(ViewHolder holder)
        {
            RectTransform rect = holder.RectTransform;
            Vector2 position = rect.anchoredPosition;
            if (direction == Direction.Vertical)
            {
                return position.y + rect.rect.height * 0.5f;
            }

            return position.x - rect.rect.width * 0.5f;
        }

        internal void MeasureBoundHolder(ViewHolder viewHolder)
        {
            if (!IsReady || viewHolder == null || !layoutManager.VariableItemLengthEnabled)
            {
                return;
            }

            if (measurementBatchDepth > 0 || flushingMeasurements)
            {
                QueueMeasurement(viewHolder);
                return;
            }

            float length = MeasureViewHolder(viewHolder);
            ApplyMeasuredLength(viewHolder.Index, length);
        }

        /// <summary>
        /// 开始一次测量批处理：批内新建/重绑的 Holder 只记录待测量项，
        /// 由 <see cref="EndMeasurementBatch"/> 统一测量并只刷新一次内容尺寸、滚动条与锚点。
        /// </summary>
        internal void BeginMeasurementBatch()
        {
            measurementBatchDepth++;
        }

        /// <summary>
        /// 结束一次测量批处理；最外层结束时执行挂起的测量与唯一一次布局刷新。
        /// </summary>
        /// <returns>本次结束是否真正执行了测量后的布局刷新。</returns>
        internal bool EndMeasurementBatch()
        {
            if (measurementBatchDepth == 0)
            {
                return false;
            }

            if (--measurementBatchDepth > 0)
            {
                return false;
            }

            return !flushingMeasurements && FlushPendingMeasurements();
        }

        private void QueueMeasurement(ViewHolder viewHolder)
        {
            for (int i = 0; i < pendingMeasurements.Count; i++)
            {
                if (ReferenceEquals(pendingMeasurements[i], viewHolder))
                {
                    return;
                }
            }

            pendingMeasurements.Add(viewHolder);
        }

        private float MeasureViewHolder(ViewHolder viewHolder)
        {
            float length = ItemMeasure.MeasureBound(viewHolder.RectTransform, direction);
            if (length > 0f)
            {
                viewHolder.RectTransform.SetSizeWithCurrentAnchors(
                    direction == Direction.Horizontal ? RectTransform.Axis.Horizontal : RectTransform.Axis.Vertical,
                    length);
            }

            return length;
        }

        private bool FlushPendingMeasurements()
        {
            // 上一次若被异常中断，残留的测量记录不能重复上报。
            measuredItemLengths.Clear();
            if (!IsReady || !layoutManager.VariableItemLengthEnabled || pendingMeasurements.Count == 0)
            {
                pendingMeasurements.Clear();
                return false;
            }

            bool applied = false;
            flushingMeasurements = true;
            try
            {
                VisualAnchor anchor = CaptureVisualAnchor();
                while (pendingMeasurements.Count > 0)
                {
                    bool changed = false;
                    for (int i = 0; i < pendingMeasurements.Count; i++)
                    {
                        ViewHolder viewHolder = pendingMeasurements[i];
                        if (viewHolder == null || viewHolder.RecyclerView != this || viewHolder.DataIndex < 0)
                        {
                            continue;
                        }

                        float length = MeasureViewHolder(viewHolder);
                        int layoutIndex = viewHolder.Index;
                        if (layoutManager.ApplyMeasuredLength(layoutIndex, length))
                        {
                            changed = true;
                            measuredItemLengths.Add(new MeasuredItemLength
                            {
                                LayoutIndex = layoutIndex,
                                Length = length
                            });
                        }
                    }

                    pendingMeasurements.Clear();
                    if (!changed)
                    {
                        break;
                    }

                    PublishMeasuredLayout(anchor);
                    applied = true;
                }
            }
            finally
            {
                // 用户 OnBind / OnItemResized 抛异常时不能把这个闩锁留在 true。
                flushingMeasurements = false;
            }

            for (int i = 0; i < measuredItemLengths.Count; i++)
            {
                MeasuredItemLength item = measuredItemLengths[i];
                OnItemResized?.Invoke(layoutManager.GetDataIndex(item.LayoutIndex), item.Length);
            }

            measuredItemLengths.Clear();
#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
            if (applied)
            {
                NotifyNavigationDataChanged();
            }
#endif
            return applied;
        }

        private void PublishMeasuredLayout(VisualAnchor anchor)
        {
            ApplyLayoutSettings();
            layoutManager.SetContentSize();
            if (scroller != null)
            {
                scroller.ContentSize = layoutManager.ContentSize;
            }

            RestoreVisualAnchor(anchor, false);
            UpdateVisibleRange();
            layoutManager.UpdateLayout();
            if (scroller != null)
            {
                UpdateScrollbarVisibility();
                UpdateScrollbarValue(scroller.Position);
            }
        }

        private void ApplyMeasuredLength(int layoutIndex, float length)
        {
            if (layoutIndex < 0 || length < 0f)
            {
                return;
            }

            VisualAnchor anchor = CaptureVisualAnchor();
            if (!layoutManager.ApplyMeasuredLength(layoutIndex, length))
            {
                return;
            }

            ApplyLayoutSettings();
            layoutManager.SetContentSize();
            if (scroller != null)
            {
                scroller.ContentSize = layoutManager.ContentSize;
            }

            RestoreVisualAnchor(anchor, true);
            if (scroller != null)
            {
                UpdateScrollbarVisibility();
                UpdateScrollbarValue(scroller.Position);
            }
            OnItemResized?.Invoke(layoutManager.GetDataIndex(layoutIndex), length);
#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
            NotifyNavigationDataChanged();
#endif
        }

        internal int RebindVisibleDataIndex(int dataIndex)
        {
            if (RecyclerViewAdapter == null || viewProvider == null)
            {
                return 0;
            }

            return viewProvider.RebindVisibleDataIndex(dataIndex);
        }

        /// <summary>
        /// 重新绑定当前可见区域内指定数据区间对应的所有持有者。
        /// </summary>
        internal int RebindVisibleDataRange(int startDataIndex, int count)
        {
            if (count <= 0 || viewProvider == null)
            {
                return 0;
            }

            BeginMeasurementBatch();
            int rebound = viewProvider.RebindVisibleDataRange(startDataIndex, count);
            EndMeasurementBatch();
            return rebound;
        }

        internal int ApplyVisibleSelection(int dataIndex, bool selected)
        {
            return viewProvider != null ? viewProvider.ApplyVisibleSelection(dataIndex, selected) : 0;
        }

        internal void NotifyHolderRecycled(ViewHolder viewHolder)
        {
#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
            OnHolderRecycled?.Invoke(viewHolder);
#endif
        }

        internal void ApplyItemsInserted(int index, int count)
        {
            ApplyStructureChange(index, count, true);
        }

        internal void ApplyItemsRemoved(int index, int count)
        {
            ApplyStructureChange(index, count, false);
        }

        internal void ApplyItemsMoved(int fromIndex, int toIndex)
        {
            if (!IsReady || fromIndex == toIndex || fromIndex < 0 || toIndex < 0)
            {
                return;
            }

            // 顺序变化无法用索引平移表达，交给统一的重建刷新。
            layoutManager.InvalidateItemLengths();
            MarkDirty(DirtyKind.Rebuild);
        }

        private void ApplyStructureChange(int index, int count, bool inserted)
        {
            if (!IsReady || count <= 0)
            {
                return;
            }

            // 尾部追加是唯一的可增量路径：变长索引表只并入新增段。
            if (inserted && index + count == RecyclerViewAdapter.GetItemCount())
            {
                layoutManager.NotifyItemsAppended(count);
            }
            else
            {
                layoutManager.InvalidateItemLengths();
            }

            if (ShouldDeferRefresh)
            {
                // AfterSettle：拖拽/惯性期间不做重建，落定后由唯一 flush 处理。
                MarkDirty(DirtyKind.Rebuild);
                return;
            }

            VisualAnchor anchor = CaptureVisualAnchor();
            if (inserted && index >= RecyclerViewAdapter.GetItemCount() - count && IsPinnedToEnd())
            {
                anchor.StickToEnd = true;
            }

            AdjustAnchorForStructureChange(ref anchor, index, count, inserted);
            if (IsLoopingAdapter())
            {
                ApplyLayoutNow();
                RebuildVisibleHolders();
                RestoreVisualAnchor(anchor, true);
                return;
            }

            if (inserted)
            {
                ApplyInsertedHolders(index, count);
            }
            else
            {
                ApplyRemovedHolders(index, count);
            }

            RestoreVisualAnchor(anchor, true);
        }

        private void ApplyInsertedHolders(int index, int count)
        {
            ApplyLayoutNow();
            if (viewProvider.VisibleCount <= 0 || endIndex < startIndex || layoutManager.Unit > 1)
            {
                RebuildVisibleHolders();
                return;
            }

            if (index > endIndex)
            {
                PublishVisibleLayout();
                return;
            }

            viewProvider.ShiftIndexesFrom(index, count);
            if (index <= startIndex)
            {
                startIndex += count;
                endIndex += count;
                PublishVisibleLayout();
                return;
            }

            endIndex += count;
            BeginMeasurementBatch();
            for (int i = 0; i < count; i++)
            {
                viewProvider.CreateViewHolder(index + i);
            }

            if (!EndMeasurementBatch())
            {
                PublishVisibleLayout();
            }
        }

        private void ApplyRemovedHolders(int index, int count)
        {
            int visibleCount = viewProvider.VisibleCount;
            if (visibleCount > 0 && endIndex >= startIndex)
            {
                viewProvider.RecycleLayoutRange(index, index + count);
                viewProvider.ShiftIndexesFrom(index, -count);

                // 可见区间与 [index, index + count) 求差，三种位置关系分别平移
                int removedEnd = index + count - 1;
                bool visibleStartsAfterRemoval = startIndex > removedEnd;
                bool visibleStartsInsideRemoval = !visibleStartsAfterRemoval && startIndex >= index;
                startIndex = visibleStartsAfterRemoval ? startIndex - count
                    : visibleStartsInsideRemoval ? index
                    : startIndex;
                endIndex = endIndex > removedEnd ? endIndex - count
                    : endIndex >= index ? index - 1
                    : endIndex;
                startIndex = Mathf.Max(0, startIndex);
            }

            ApplyLayoutNow();
            if (RecyclerViewAdapter.GetItemCount() <= 0)
            {
                viewProvider.Clear();
                startIndex = 0;
                endIndex = -1;
                return;
            }

            PublishVisibleLayout();
        }

        /// <summary>
        /// 增量更新可见区间并发布布局（结构增量用；重建路径自己已经动画过）。
        /// </summary>
        private void PublishVisibleLayout()
        {
            UpdateVisibleRange();
            layoutManager.UpdateLayout();
        }

        private struct MeasuredItemLength
        {
            public int LayoutIndex;
            public float Length;
        }

        private struct VisualAnchor
        {
            public bool Valid;
            public bool StickToEnd;
            public int DataIndex;
            public float ItemOffset;
        }

        private VisualAnchor CaptureVisualAnchor()
        {
            VisualAnchor anchor = default;
            if (!IsReady || RecyclerViewAdapter.GetItemCount() <= 0 || scroller == null)
            {
                return anchor;
            }

            float position = scroller.Position;
            ViewHolder holder = FirstVisibleHolder();
            if (holder == null)
            {
                return anchor;
            }

            anchor.Valid = true;
            anchor.StickToEnd = IsPinnedToEnd();
            anchor.DataIndex = holder.DataIndex;
            anchor.ItemOffset = position - layoutManager.GetItemStartPosition(holder.Index);
            return anchor;
        }

        private ViewHolder FirstVisibleHolder()
        {
            return IsReady ? viewProvider.FindFirstVisible() : null;
        }

        private bool IsPinnedToEnd()
        {
            return scroller != null && scroller.Position >= scroller.MaxPosition - 0.5f;
        }

        private static void AdjustAnchorForStructureChange(ref VisualAnchor anchor, int index, int count, bool inserted)
        {
            if (!anchor.Valid || anchor.StickToEnd || count <= 0)
            {
                return;
            }

            if (inserted)
            {
                if (index <= anchor.DataIndex)
                {
                    anchor.DataIndex += count;
                }

                return;
            }

            if (anchor.DataIndex >= index && anchor.DataIndex < index + count)
            {
                anchor.DataIndex = index;
                anchor.ItemOffset = 0f;
                return;
            }

            if (anchor.DataIndex >= index + count)
            {
                anchor.DataIndex -= count;
            }
        }

        private void RestoreVisualAnchor(VisualAnchor anchor, bool updateVisibleRange)
        {
            if (!anchor.Valid || scroller == null || !IsReady || RecyclerViewAdapter.GetItemCount() <= 0)
            {
                return;
            }

            float target;
            if (anchor.StickToEnd)
            {
                target = scroller.MaxPosition;
            }
            else
            {
                int layoutIndex = layoutManager.GetLayoutIndex(anchor.DataIndex);
                target = ClampScrollTarget(layoutManager.GetItemStartPosition(layoutIndex) + anchor.ItemOffset);
            }

            if (!Mathf.Approximately(scroller.Position, target))
            {
                scroller.Position = target;
            }

            if (updateVisibleRange)
            {
                UpdateVisibleRange();
                layoutManager.UpdateLayout();
            }
        }

        private bool IsLoopingAdapter()
        {
            if (RecyclerViewAdapter == null)
            {
                return false;
            }

            int realCount = RecyclerViewAdapter.GetRealCount();
            int itemCount = RecyclerViewAdapter.GetItemCount();
            return realCount > 0 && itemCount != realCount;
        }

#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
        internal void NotifyNavigationDataChanged()
        {
            OnNavigationDataChanged?.Invoke();
        }

        internal RecyclerViewNavigationController NavigationController
        {
            get
            {
                if (!navigationControllerResolved)
                {
                    cachedNavigationController = GetComponent<RecyclerViewNavigationController>();
                    navigationControllerResolved = true;
                }

                return cachedNavigationController;
            }
        }

#endif

        #endregion
    }
}
