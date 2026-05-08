using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private const float MinScrollbarHandlePixels = 18f;

        /// <summary>
        /// 记录 Unity 主线程的托管线程标识。
        /// </summary>
        private static int mainThreadId = -1;

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
        /// 是否启用滚动能力。
        /// </summary>
        [HideInInspector] [SerializeField] private ScrollMode scroll = ScrollMode.AlwaysEnable;

        /// <summary>
        /// 是否在停止滚动后自动吸附到最近项。
        /// </summary>
        [HideInInspector] [SerializeField] private bool snap;

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
        [FormerlySerializedAs("showScrollBar")]
        [HideInInspector] [SerializeField] private ScrollbarVisibility scrollbarVisibility;

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
        [HideInInspector] [SerializeReference] private Scroller scroller;

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

        /// <summary>
        /// 负责处理列表内导航逻辑的控制器。
        /// </summary>
        private RecyclerNavigationController navigationController;

        private bool hasPendingFocusRecovery;
        private GameObject pendingFocusRecoveryTarget;

        /// <summary>
        /// 是否存在等待滚动结束后执行的焦点请求。
        /// </summary>
        private bool hasPendingFocusRequest;

        /// <summary>
        /// 挂起焦点请求期望采用的对齐方式。
        /// </summary>
        private ScrollAlignment pendingFocusAlignment;

        /// <summary>
        /// 挂起焦点请求对应的数据索引。
        /// </summary>
        private int pendingFocusIndex = -1;

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
        private int currentIndex;
        private int focusIndex = -1;

        #endregion

        #region 公共属性 - 布局设置

        /// <summary>
        /// 获取或设置列表的主滚动方向。
        /// </summary>
        public Direction Direction
        {
            get => direction;
            set => direction = value;
        }

        /// <summary>
        /// 获取或设置列表项在交叉轴上的对齐方式。
        /// </summary>
        public Alignment Alignment
        {
            get => alignment;
            set => alignment = value;
        }

        /// <summary>
        /// 获取或设置列表项之间的间距。
        /// </summary>
        public Vector2 Spacing
        {
            get => spacing;
            set => spacing = value;
        }

        /// <summary>
        /// 获取或设置列表内容区域的内边距。
        /// </summary>
        public Vector2 Padding
        {
            get => padding;
            set => padding = value;
        }

        #endregion

        #region 公共属性 - 滚动设置

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
        public ViewProvider ViewProvider
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

        /// <summary>
        /// 获取导航控制器；首次访问时自动创建。
        /// </summary>
        public RecyclerNavigationController NavigationController => navigationController ??= new RecyclerNavigationController(this);

        #endregion

        #region 公共属性 - 状态

        /// <summary>
        /// 获取或设置当前绑定的适配器实例。
        /// </summary>
        internal IAdapter RecyclerViewAdapter { get; private set; }

        /// <summary>
        /// 获取当前记录的内部逻辑索引。
        /// 仅供框架内部的导航与布局逻辑使用；业务层请改为通过 <see cref="OnFocusIndexChanged"/> 维护自身状态，
        /// 或使用适配器上的 <c>ChoiceIndex</c> 表示业务选中项。
        /// </summary>
        internal int CurrentIndex => currentIndex;

        internal int FocusIndex => focusIndex;

        #endregion

        #region 事件

        /// <summary>
        /// 当当前逻辑索引发生变化时触发。
        /// </summary>
        public Action<int> OnFocusIndexChanged;

        public Action<int> OnCurrentIndexChanged;

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
        /// 初始化模板、滚动器、滚动条与导航桥接组件。
        /// </summary>
        private void Awake()
        {
            if (mainThreadId < 0)
            {
                mainThreadId = Thread.CurrentThread.ManagedThreadId;
            }

            ValidateConfiguration();
            if (!isValid)
            {
                return;
            }

            InitializeTemplates();
            ConfigureScroller();
            ConfigureScrollbar();
        }

        private void LateUpdate()
        {
            ProcessPendingFocusRecovery();
        }

        private void OnDestroy()
        {
            if (scroller != null)
            {
                scroller.OnValueChanged.RemoveListener(OnScrollChanged);
                scroller.OnMoveStoped.RemoveListener(OnMoveStoped);
                scroller.OnDragging.RemoveListener(OnScrollerDraggingChanged);
            }

            if (scrollbar != null)
            {
                scrollbar.onValueChanged.RemoveListener(OnScrollbarChanged);
                ScrollbarEx scrollbarEx = scrollbar.gameObject.GetComponent<ScrollbarEx>();
                if (scrollbarEx != null && scrollbarEx.OnDragEnd == OnScrollbarDragEnd)
                {
                    scrollbarEx.OnDragEnd = null;
                }
            }

            hasPendingFocusRecovery = false;
            pendingFocusRecoveryTarget = null;
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

                if (templates.Length > 1 && string.IsNullOrEmpty(template.GetType().Name))
                {
                    LogValidationError("RecyclerView mixed template name is missing.");
                    isValid = false;
                    return;
                }

                if (templates.Length > 1 && HasDuplicateTemplateType(i, template.GetType()))
                {
                    LogValidationError("RecyclerView mixed template type duplicated.");
                    isValid = false;
                    return;
                }
            }
        }

        private bool HasDuplicateTemplateType(int currentIndex, Type templateType)
        {
            for (int i = 0; i < currentIndex; i++)
            {
                ViewHolder template = templates[i];
                if (template != null && template.GetType() == templateType)
                {
                    return true;
                }
            }

            return false;
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
        /// 确保当前对象挂载用于导航事件桥接的组件。
        /// </summary>
        /// <summary>
        /// 查找当前可见列表边缘对应的数据索引。
        /// </summary>
        /// <param name="useMax"><see langword="true"/> 表示查找最大的布局索引；否则查找最小的布局索引。</param>
        /// <returns>找到的边缘数据索引；不存在可见项时返回 <c>-1</c>。</returns>
        private int FindVisibleEdgeDataIndex(bool useMax)
        {
            if (ViewProvider.VisibleCount == 0)
            {
                return -1;
            }

            int best = useMax ? int.MinValue : int.MaxValue;
            for (int i = 0; i < ViewProvider.VisibleCount; i++)
            {
                ViewHolder holder = ViewProvider.GetVisibleViewHolder(i);
                if (holder == null || holder.Index < 0)
                {
                    continue;
                }

                if (useMax)
                {
                    if (holder.Index > best)
                    {
                        best = holder.Index;
                    }
                }
                else if (holder.Index < best)
                {
                    best = holder.Index;
                }
            }

            return best is int.MinValue or int.MaxValue ? -1 : best;
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
            scroller.OnValueChanged.AddListener(OnScrollChanged);
            scroller.OnMoveStoped.AddListener(OnMoveStoped);
            scroller.OnDragging.AddListener(OnScrollerDraggingChanged);
            UpdateScrollerState();
        }

        /// <summary>
        /// 配置滚动条监听与拖拽结束回调。
        /// </summary>
        private void ConfigureScrollbar()
        {
            if (scrollbar == null) return;

            scrollbar.onValueChanged.AddListener(OnScrollbarChanged);

            var scrollbarEx = scrollbar.gameObject.GetComponent<ScrollbarEx>();
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
            if (!EnsureMainThread(nameof(SetAdapter)))
            {
                return;
            }

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
            (RecyclerViewAdapter as IItemRenderCacheOwner)?.ReleaseAllItemRenders();
            RecyclerViewAdapter = adapter;
            ViewProvider.Adapter = adapter;
            ViewProvider.LayoutManager = layoutManager;

            layoutManager.RecyclerView = this;
            layoutManager.Adapter = adapter;
            layoutManager.ViewProvider = viewProvider;
            layoutManager.Direction = direction;
            layoutManager.Alignment = alignment;
            layoutManager.Spacing = spacing;
            layoutManager.Padding = padding;
            startIndex = 0;
            endIndex = -1;
            currentIndex = -1;
            focusIndex = -1;
            ClearPendingFocusRequest();
        }

        /// <summary>
        /// 尝试获取当前可见区域内指定索引对应的视图持有者。
        /// </summary>
        /// <param name="index">目标布局索引。</param>
        /// <param name="viewHolder">返回找到的视图持有者。</param>
        /// <returns>找到且该持有者仍处于可见范围内时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        internal bool TryGetVisibleViewHolder(int index, out ViewHolder viewHolder)
        {
            viewHolder = ViewProvider.GetViewHolder(index);
            return viewHolder != null && layoutManager != null && layoutManager.IsVisible(viewHolder.Index);
        }

        /// <summary>
        /// 尝试将焦点移动到指定索引对应的列表项。
        /// </summary>
        /// <param name="index">目标数据索引。</param>
        /// <param name="smooth">是否先以平滑滚动方式将目标项滚入可见区域。</param>
        /// <param name="alignment">目标项滚动完成后的对齐方式。</param>
        /// <returns>成功定位并应用焦点时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        public bool TryFocusIndex(int index, bool smooth = false, ScrollAlignment alignment = ScrollAlignment.Center)
        {
            if (RecyclerViewAdapter == null || RecyclerViewAdapter.GetItemCount() <= 0 || index < 0 || index >= RecyclerViewAdapter.GetItemCount())
            {
                return false;
            }

            if (smooth && (!TryGetVisibleViewHolder(index, out ViewHolder smoothHolder) || !IsFullyVisible(smoothHolder)))
            {
                QueueFocusRequest(index, alignment);
                ScrollToWithAlignment(index, alignment, 0f, true);
                return true;
            }

            if (!TryGetVisibleViewHolder(index, out ViewHolder holder) || !IsFullyVisible(holder))
            {
                ScrollToWithAlignment(index, alignment, 0f, false);
                if (!TryGetVisibleViewHolder(index, out holder))
                {
                    Refresh();
                    TryGetVisibleViewHolder(index, out holder);
                }
            }

            if (holder == null)
            {
                return false;
            }

            if (!IsFullyVisible(holder))
            {
                ScrollToWithAlignment(index, alignment, 0f, false);
                Refresh();
                TryGetVisibleViewHolder(index, out holder);
            }

            if (holder == null || !IsFullyVisible(holder) || !TryResolveFocusTarget(holder, out GameObject target))
            {
                return false;
            }

            ApplyFocus(target);
            UpdateFocusIndex(index);
            UpdateCurrentIndex(index);
            return true;
        }

        /// <summary>
        /// 按进入方向尝试将焦点移入当前列表。
        /// </summary>
        /// <param name="entryDirection">焦点进入列表时的方向。</param>
        /// <returns>成功聚焦某个列表项时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        public bool TryFocusEntry(
            MoveDirection entryDirection,
            bool smooth = false,
            ScrollAlignment alignment = ScrollAlignment.Center)
        {
            if (RecyclerViewAdapter == null)
            {
                return false;
            }

            int realCount = RecyclerViewAdapter.GetRealCount();
            if (realCount <= 0)
            {
                return false;
            }

            int targetIndex = entryDirection is MoveDirection.Up or MoveDirection.Left
                ? FindVisibleEdgeDataIndex(true)
                : FindVisibleEdgeDataIndex(false);

            if (targetIndex < 0)
            {
                targetIndex = entryDirection is MoveDirection.Up or MoveDirection.Left
                    ? realCount - 1
                    : Mathf.Clamp(CurrentIndex, 0, realCount - 1);
            }

            int step = entryDirection is MoveDirection.Up or MoveDirection.Left ? -1 : 1;
            return TryFocusIndexRange(targetIndex, step, realCount, smooth, alignment);
        }

        /// <summary>
        /// 解析指定持有者最终应被聚焦的目标对象。
        /// </summary>
        /// <param name="holder">目标视图持有者。</param>
        /// <param name="target">返回解析得到的焦点对象。</param>
        /// <returns>成功解析到可聚焦对象时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        internal bool TryResolveFocusTarget(ViewHolder holder, out GameObject target)
        {
            target = null;
            if (holder == null)
            {
                return false;
            }

            return holder.TryGetFocusTarget(out target);
        }

        /// <summary>
        /// 判断指定持有者是否已经完整处于当前视口内。
        /// </summary>
        /// <param name="holder">待检测的视图持有者。</param>
        /// <returns>完整可见时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        internal bool IsFullyVisible(ViewHolder holder)
        {
            if (holder == null)
            {
                return false;
            }

            RectTransform viewport = content != null ? content.parent as RectTransform : null;
            if (viewport == null)
            {
                viewport = transform as RectTransform;
            }

            if (viewport == null)
            {
                return true;
            }

            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, holder.RectTransform);
            Rect viewportRect = viewport.rect;
            const float epsilon = 0.01f;

            return direction switch
            {
                Direction.Vertical => bounds.min.y >= viewportRect.yMin - epsilon &&
                                      bounds.max.y <= viewportRect.yMax + epsilon,
                Direction.Horizontal => bounds.min.x >= viewportRect.xMin - epsilon &&
                                        bounds.max.x <= viewportRect.xMax + epsilon,
                _ => bounds.min.x >= viewportRect.xMin - epsilon &&
                     bounds.max.x <= viewportRect.xMax + epsilon &&
                     bounds.min.y >= viewportRect.yMin - epsilon &&
                     bounds.max.y <= viewportRect.yMax + epsilon
            };
        }

        /// <summary>
        /// 通过 EventSystem 将焦点切换到指定目标，并在下一帧做一次恢复校正。
        /// </summary>
        /// <param name="target">目标焦点对象。</param>
        internal void ApplyFocus(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            if (ReferenceEquals(eventSystem.currentSelectedGameObject, target))
            {
                ScheduleFocusRecovery(target);
                return;
            }

            if (eventSystem.alreadySelecting)
            {
                ScheduleFocusRecovery(target);
                return;
            }

            eventSystem.SetSelectedGameObject(target);
            ScheduleFocusRecovery(target);
        }

        private void ScheduleFocusRecovery(GameObject target)
        {
            pendingFocusRecoveryTarget = target;
            hasPendingFocusRecovery = target != null;
        }

        private bool TryFocusIndexRange(int startIndex, int step, int itemCount, bool smooth, ScrollAlignment alignment)
        {
            if (itemCount <= 0 || step == 0)
            {
                return false;
            }

            int index = Mathf.Clamp(startIndex, 0, itemCount - 1);
            while (index >= 0 && index < itemCount)
            {
                if (TryFocusIndex(index, smooth, alignment))
                {
                    return true;
                }

                index += step;
            }

            return false;
        }

        private static bool IsSelectableFocusable(Selectable selectable)
        {
            return selectable != null &&
                   selectable.IsActive() &&
                   selectable.IsInteractable();
        }

        private void ProcessPendingFocusRecovery()
        {
            if (!hasPendingFocusRecovery)
            {
                return;
            }

            GameObject target = pendingFocusRecoveryTarget;
            hasPendingFocusRecovery = false;
            pendingFocusRecoveryTarget = null;

            if (target == null || !target.activeInHierarchy)
            {
                return;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || ReferenceEquals(eventSystem.currentSelectedGameObject, target))
            {
                return;
            }

            eventSystem.SetSelectedGameObject(target);
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

            if (!EnsureMainThread(nameof(Reset)))
            {
                return;
            }

            viewProvider?.Reset();

            if (scroller != null)
            {
                scroller.Position = 0;
            }

            if (scrollbar != null)
            {
                scrollbar.SetValueWithoutNotify(0);
            }

            startIndex = 0;
            endIndex = -1;
            currentIndex = -1;
            focusIndex = -1;
            ClearPendingFocusRequest();
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

            if (!EnsureMainThread(nameof(Refresh)))
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

            startIndex = Mathf.Max(0, layoutManager.GetStartIndex());
            endIndex = layoutManager.GetEndIndex();
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

            if (!EnsureMainThread(nameof(RequestLayout)))
            {
                return;
            }

            if (layoutManager == null)
            {
                UpdateScrollbarVisibility();
                return;
            }

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
            scroller.Position = Mathf.Clamp(scroller.Position, 0, scroller.MaxPosition);
            viewProvider?.PreparePool();

            UpdateScrollerState();
            UpdateScrollbarVisibility();
            UpdateScrollbarValue(scroller.Position);
        }

        #endregion

        #region 公共方法 - 滚动

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
            if (scroll == ScrollMode.AlwaysDisable || scroller == null) return;

            scroller.ScrollTo(layoutManager.IndexToPosition(index), smooth);

            if (!smooth)
            {
                Refresh();
            }

            UpdateCurrentIndex(index);
        }

        /// <summary>
        /// 将列表滚动到指定索引，并按给定对齐方式定位。
        /// </summary>
        /// <param name="index">目标数据索引。</param>
        /// <param name="alignment">目标项滚动完成后的对齐方式。</param>
        /// <param name="offset">在对齐基础上的额外偏移量。</param>
        /// <param name="smooth">是否使用平滑滚动。</param>
        /// <param name="duration">平滑滚动时长，单位为秒。</param>
        public void ScrollToWithAlignment(int index, ScrollAlignment alignment, float offset = 0f, bool smooth = false, float duration = 0.3f)
        {
            if (scroll == ScrollMode.AlwaysDisable || scroller == null) return;

            float targetPosition = CalculateScrollPositionWithAlignment(index, alignment, offset);


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

            UpdateCurrentIndex(index);
        }

        /// <summary>
        /// 计算指定索引在目标对齐方式下应滚动到的位置。
        /// </summary>
        /// <param name="index">目标数据索引。</param>
        /// <param name="alignment">目标项滚动完成后的对齐方式。</param>
        /// <param name="offset">在对齐基础上的额外偏移量。</param>
        /// <returns>计算得到的滚动位置，结果会被限制在合法范围内。</returns>
        private float CalculateScrollPositionWithAlignment(int index, ScrollAlignment alignment, float offset)
        {
            if (RecyclerViewAdapter == null || index < 0 || index >= RecyclerViewAdapter.GetItemCount())
            {
                return scroller.Position;
            }

            float itemSize = layoutManager.GetItemLength(index);
            float viewportLength = direction == Direction.Vertical ? layoutManager.ViewportSize.y : layoutManager.ViewportSize.x;

            // 计算目标项的原始位置，不在此阶段做范围限制。
            float itemPosition = layoutManager.GetItemStartPosition(index);

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
            return Mathf.Clamp(targetPosition, 0, scroller.MaxPosition);
        }

        /// <summary>
        /// 计算指定索引对应项在内容区域中的原始起始位置。
        /// </summary>
        /// <param name="index">目标数据索引。</param>
        /// <returns>未做边界限制的原始滚动位置。</returns>
        private float CalculateRawItemPosition(int index)
        {
            // 根据滚动方向选择对应轴向的间距与内边距。
            Vector2 spacing = layoutManager.Spacing;
            Vector2 padding = layoutManager.Padding;
            float itemSize = GetItemSize(index);
            float spacingValue = direction == Direction.Vertical ? spacing.y : spacing.x;
            float paddingValue = direction == Direction.Vertical ? padding.y : padding.x;

            // 直接基于索引、尺寸与间距推导原始位置。
            return index * (itemSize + spacingValue) + paddingValue;
        }

        /// <summary>
        /// 获取指定索引对应项在主滚动轴上的尺寸。
        /// </summary>
        /// <param name="index">目标数据索引。</param>
        /// <returns>目标项在主滚动轴上的尺寸值。</returns>
        private float GetItemSize(int index)
        {
            Vector2 itemSize = ViewProvider.CalculateViewSize(index);
            return direction == Direction.Vertical ? itemSize.y : itemSize.x;
        }

        #endregion

        #region 私有方法 - 滚动回调

        /// <summary>
        /// 响应滚动器位置变化，更新布局、滚动条与可见区间。
        /// </summary>
        /// <param name="position">当前滚动位置。</param>
        private void OnScrollChanged(float position)
        {
            layoutManager.UpdateLayout();
            UpdateScrollbarValue(position);
            UpdateVisibleRange();
            layoutManager.DoItemAnimation();
            OnScrollValueChanged?.Invoke(position);
        }

        /// <summary>
        /// 响应滚动器停止移动事件，处理吸附与挂起焦点请求。
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

        /// <summary>
        /// 根据当前滚动位置增量更新可见区间内的视图持有者。
        /// </summary>
        private void UpdateVisibleRange()
        {
            if (layoutManager == null || RecyclerViewAdapter == null || RecyclerViewAdapter.GetItemCount() <= 0)
            {
                return;
            }

            // 处理可见区间起始端的回收与补充。
            if (layoutManager.IsFullInvisibleStart(startIndex))
            {
                viewProvider.RemoveViewHolder(startIndex);
                startIndex += layoutManager.Unit;
            }
            else if (layoutManager.IsFullVisibleStart(startIndex))
            {
                if (startIndex == 0)
                {
                    // 待补充：在滚动到列表起始端时补充刷新逻辑。
                }
                else
                {
                    startIndex -= layoutManager.Unit;
                    viewProvider.CreateViewHolder(startIndex);
                }
            }

            // 处理可见区间末端的回收与补充。
            if (layoutManager.IsFullInvisibleEnd(endIndex))
            {
                viewProvider.RemoveViewHolder(endIndex);
                endIndex -= layoutManager.Unit;
            }
            else if (layoutManager.IsFullVisibleEnd(endIndex))
            {
                if (endIndex >= viewProvider.GetItemCount() - layoutManager.Unit)
                {
                    // 待补充：在滚动到列表末端时补充加载更多逻辑。
                }
                else
                {
                    endIndex += layoutManager.Unit;
                    viewProvider.CreateViewHolder(endIndex);
                }
            }

            // 若增量更新后的区间与实际可见区不一致，则退化为全量刷新。
            if (!layoutManager.IsVisible(startIndex) || !layoutManager.IsVisible(endIndex))
            {
                Refresh();
            }
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
            index = Mathf.Clamp(index, 0, RecyclerViewAdapter.GetItemCount() - 1);
            float targetPosition = layoutManager.IndexToPosition(index);
            UpdateCurrentIndex(index);
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

            TryProcessPendingFocusRequest();
            OnScrollStopped?.Invoke();
        }

        /// <summary>
        /// 更新当前逻辑索引，并在变化时触发事件通知。
        /// </summary>
        /// <param name="index">新的候选索引。</param>
        internal void UpdateCurrentIndex(int index)
        {
            UpdateTrackedIndex(ref currentIndex, index, OnCurrentIndexChanged);
        }

        internal void UpdateFocusIndex(int index)
        {
            UpdateTrackedIndex(ref focusIndex, index, OnFocusIndexChanged);
        }

        private void UpdateTrackedIndex(ref int trackedIndex, int index, Action<int> callback)
        {
            int itemCount = GetTrackedItemCount();
            if (itemCount <= 0)
            {
                if (trackedIndex != -1)
                {
                    trackedIndex = -1;
                    callback?.Invoke(trackedIndex);
                }

                return;
            }

            index %= itemCount;
            index = index < 0 ? itemCount + index : index;

            if (trackedIndex != index)
            {
                trackedIndex = index;
                callback?.Invoke(trackedIndex);
            }
        }

        private int GetTrackedItemCount()
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
            if (!EnsureMainThread(nameof(RebindVisibleDataIndex)) ||
                RecyclerViewAdapter == null ||
                !ViewProvider.TryGetViewHolderBucket(dataIndex, out ViewProvider.ViewHolderBucket holders))
            {
                return 0;
            }

            int reboundCount = 0;
            for (int i = 0; i < holders.Count; i++)
            {
                ViewHolder holder = holders[i];
                if (holder == null)
                {
                    continue;
                }

                RecyclerViewAdapter.OnBindViewHolder(holder, holder.Index);
                reboundCount++;
            }

            return reboundCount;
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

        /// <summary>
        /// 缓存一条等待滚动结束后执行的焦点请求。
        /// </summary>
        /// <param name="index">待聚焦的数据索引。</param>
        /// <param name="alignment">目标对齐方式。</param>
        private void QueueFocusRequest(int index, ScrollAlignment alignment)
        {
            hasPendingFocusRequest = true;
            pendingFocusIndex = index;
            pendingFocusAlignment = alignment;
        }

        /// <summary>
        /// 清除当前缓存的焦点请求。
        /// </summary>
        private void ClearPendingFocusRequest()
        {
            hasPendingFocusRequest = false;
            pendingFocusIndex = -1;
            pendingFocusAlignment = ScrollAlignment.Center;
        }

        /// <summary>
        /// 尝试执行当前缓存的焦点请求。
        /// </summary>
        private void TryProcessPendingFocusRequest()
        {
            if (!hasPendingFocusRequest)
            {
                return;
            }

            int index = pendingFocusIndex;
            ScrollAlignment alignment = pendingFocusAlignment;
            ClearPendingFocusRequest();
            TryFocusIndex(index, false, alignment);
        }

        /// <summary>
        /// 判断当前调用线程是否为 Unity 主线程。
        /// </summary>
        /// <returns>当前线程为主线程时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        private static bool IsMainThread()
        {
            return mainThreadId < 0 || Thread.CurrentThread.ManagedThreadId == mainThreadId;
        }

        /// <summary>
        /// 校验当前调用是否发生在 Unity 主线程上。
        /// </summary>
        /// <param name="caller">发起校验的调用方名称。</param>
        /// <returns>位于主线程时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        private bool EnsureMainThread(string caller)
        {
            if (IsMainThread())
            {
                return true;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Log.Error("RecyclerView method must run on Unity main thread.");
#endif
            return false;
        }

        #endregion
    }
}
