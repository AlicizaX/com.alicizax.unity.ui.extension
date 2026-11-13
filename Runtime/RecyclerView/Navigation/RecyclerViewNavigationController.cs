#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
using System;
using AlicizaX.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlicizaX.UI
{
    /// <summary>
    /// RecyclerView 手柄/键盘导航控制器。用数据索引维护虚拟焦点，是列表在 EventSystem 中唯一的选中对象。
    /// </summary>
    [AddComponentMenu("UI/RecyclerView Navigation Controller")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RecyclerView))]
    public sealed class RecyclerViewNavigationController : Selectable, ISubmitHandler
    {
        [SerializeField] private RecyclerView recyclerView;
        [SerializeField] private NavigationScope navigationScope;

        [SerializeField] private bool wrapNavigation;
        [SerializeField] private bool keepFocusVisibleWhenSuppressed = true;
        [SerializeField] private bool smoothScroll = true;
        [SerializeField] private float smoothScrollDuration = 0.18f;
        [SerializeField] private ScrollAlignment focusAlignment = ScrollAlignment.Center;
        // 边界出口不单独配置：使用基类 Selectable.navigation（推荐 Explicit + Select On 各方向）。

        private int focusedDataIndex = -1;
        private bool suppressed;
        private bool isSelected;                         // 是否为 EventSystem 当前选中对象（基类 hasSelection 为 private）
        private bool cursorMode;                         // 光标模式：手柄，或键鼠类型下的键盘导航
        private Predicate<int> focusFilter;

        /// <summary>
        /// 当前导航焦点所在的数据索引；没有焦点时为 -1。离开列表、被压制时保留。
        /// </summary>
        public int FocusedDataIndex => focusedDataIndex;

        /// <summary>
        /// 当前 RecyclerView 是否持有有效的导航焦点。
        /// </summary>
        public bool HasFocus => focusedDataIndex >= 0;

        /// <summary>
        /// 当前导航是否被所属 NavigationScope 抑制。
        /// </summary>
        public bool IsSuppressed => suppressed;

        #region 生命周期

        protected override void Awake()
        {
            base.Awake();
            if (recyclerView == null)
            {
                recyclerView = GetComponent<RecyclerView>();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            recyclerView.OnScrollValueChanged += OnScrollValueChanged;
            recyclerView.OnScrollStopped += OnScrollStopped;
            recyclerView.OnLayoutChanged += NotifyDataSetChanged;

            // 输入系统未初始化（编辑器、EditMode 测试）是合法状态。
            InputWatch watch = UXInput.Watch;
            cursorMode = watch != null && watch.IsNavigationInput;
            if (watch != null)
            {
                watch.OnInputTypeChanged += OnInputTypeChanged;
            }

            if (navigationScope != null)
            {
                navigationScope.NavigationSuppressionChanged += SetNavigationSuppressed;
                suppressed = navigationScope.NavigationSuppressed;   // 禁用期间错过的事件在这里对齐
                // 注册会同步触发 InputNavigation.Refresh（可能回调 OnSelect / 压制事件），状态必须先就绪。
                navigationScope.RegisterSelectable(this, true);
            }

            // 与基类 Selectable.OnEnable 恢复 hasSelection 的口径一致：仍被选中时不会再收到 OnSelect。
            EventSystem eventSystem = EventSystem.current;
            isSelected = eventSystem != null && eventSystem.currentSelectedGameObject == gameObject;
            RefreshVisibleNavigationFocus();
        }

        protected override void OnDisable()
        {
            recyclerView.OnScrollValueChanged -= OnScrollValueChanged;
            recyclerView.OnScrollStopped -= OnScrollStopped;
            recyclerView.OnLayoutChanged -= NotifyDataSetChanged;

            if (navigationScope != null)
            {
                navigationScope.NavigationSuppressionChanged -= SetNavigationSuppressed;
                navigationScope.UnregisterSelectable(this);
            }

            // 输入系统重建后实例会变：对新实例 -= 为空操作，旧实例的订阅随 Shutdown 一起清空。
            InputWatch watch = UXInput.Watch;
            if (watch != null)
            {
                watch.OnInputTypeChanged -= OnInputTypeChanged;
            }

            // 禁用后收不到 OnDeselect 与 scope 事件，两者会过期：清掉，禁用期间任何焦点写入都不显示光标；OnEnable 重新取值。
            isSelected = false;
            suppressed = false;
            NotifyVisibleHolders(-1);
            base.OnDisable();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            recyclerView = GetComponent<RecyclerView>();
            navigationScope = FindNearestParentNavigationScope();
        }

        private NavigationScope FindNearestParentNavigationScope()
        {
            for (Transform current = transform.parent; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out NavigationScope scope))
                {
                    return scope;
                }
            }

            return null;
        }
#endif

        #endregion

        #region EventSystem 入口

        /// <summary>
        /// 处理 Unity UI 的方向移动事件：列表内能移动则消费，否则交给基类按 navigation 离开列表。
        /// </summary>
        public override void OnMove(AxisEventData eventData)
        {
            if (HandleMove(eventData))
            {
                eventData.Use();
                return;
            }

            base.OnMove(eventData);
        }

        /// <summary>
        /// 处理 Unity UI 的提交事件，优先转发给当前焦点项，否则回退为业务选中。
        /// </summary>
        public void OnSubmit(BaseEventData eventData)
        {
            if (HandleSubmit())
            {
                eventData.Use();
            }
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            isSelected = true;
            RefreshVisibleNavigationFocus();
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            isSelected = false;
            RefreshVisibleNavigationFocus();               // 焦点位置保留，只隐藏视觉
        }

        /// <summary>
        /// 手动处理一次方向移动输入。
        /// </summary>
        /// <returns>true 表示输入已被列表消费；false 时应交给 <see cref="Selectable.OnMove"/> 离开列表。</returns>
        public bool HandleMove(AxisEventData eventData)
        {
            if (suppressed)
            {
                return false;
            }

            EnterCursorMode();
            int count = GetNavigationCount();
            if (InitializeFocusIfInvalid(count))
            {
                return true;
            }

            if (recyclerView.ViewProvider.FindByDataIndex(focusedDataIndex) is IRecyclerViewNavigationViewHolder holder &&
                holder.HandleNavigationMove(eventData))
            {
                return true;
            }

            switch (CalculateMove(eventData.moveDir, count, out int next))
            {
                case MoveResult.Moved:
                    ApplyFocus(next, true);
                    // 列表内移动没有 EventSystem 选中切换，焦点提示在这里补发（列表根挂了 InputFeedbackObserver 时）。
                    if (TryGetComponent(out InputFeedbackObserver feedback))
                    {
                        feedback.Play(InputFeedbackCue.FocusEnter);
                    }

                    return true;
                case MoveResult.Boundary:
                    return false;                          // 放行给 base.OnMove 按 navigation 离开列表
                default:
                    return true;
            }
        }

        /// <summary>
        /// 手动提交当前导航焦点。优先转发给焦点项；未处理时回退为同步 Adapter 业务选中索引。
        /// </summary>
        /// <returns>true 表示提交已被列表处理。</returns>
        public bool HandleSubmit()
        {
            if (suppressed)
            {
                return false;
            }

            EnterCursorMode();
            int count = GetNavigationCount();
            if (InitializeFocusIfInvalid(count))
            {
                return true;
            }

            if (recyclerView.ViewProvider.FindByDataIndex(focusedDataIndex) is not IRecyclerViewNavigationViewHolder holder ||
                !holder.HandleNavigationSubmit())
            {
                recyclerView.RecyclerViewAdapter.SetChoiceIndex(focusedDataIndex);
            }

            return true;
        }

        #endregion

        #region 指针入口（仅 NavigationViewHolder 调用）

        /// <summary>
        /// 鼠标/触摸点击列表项：焦点位置记为该项并选中列表，但不显示光标。
        /// </summary>
        internal void HandleItemPointerUp(int dataIndex)
        {
            // 先做无副作用判断：被压制、越界（绑定失败的项为 -1）、不可聚焦时不改投焦点、不抢选中。
            if (suppressed ||
                (uint)dataIndex >= (uint)GetNavigationCount() ||
                !IsDataIndexFocusable(dataIndex))
            {
                return;
            }

            cursorMode = false;
            ApplyFocus(dataIndex, false);
            SelectSelf();
        }

        #endregion

        #region 焦点 API

        /// <summary>
        /// 设置导航焦点；目标不可聚焦时改投最近的可聚焦项，越界时清空。
        /// </summary>
        /// <param name="dataIndex">目标数据索引。</param>
        /// <param name="scroll">是否滚动列表以显示并对齐目标项。</param>
        public void SetFocus(int dataIndex, bool scroll = true)
        {
            int count = GetNavigationCount();
            if ((uint)dataIndex >= (uint)count)
            {
                ClearFocus();
                return;
            }

            FocusNearest(dataIndex, count, scroll);
        }

        /// <summary>
        /// 恢复导航焦点（越界也取最近项），并把 EventSystem 选中切回列表。
        /// </summary>
        public void RestoreFocus(int dataIndex)
        {
            FocusNearest(dataIndex, GetNavigationCount(), true);
            if (HasFocus)
            {
                SelectSelf();
            }
        }

        /// <summary>
        /// 设置可聚焦过滤器（数据索引 → 是否可聚焦）；null 表示所有实现导航接口的模板项都可聚焦。
        /// 过滤条件依赖的数据变化走 Adapter 通知即可；仅过滤条件本身变化时需要重新调用。
        /// </summary>
        public void SetFocusFilter(Predicate<int> filter)
        {
            focusFilter = filter;
            if (HasFocus)
            {
                FocusNearest(focusedDataIndex, GetNavigationCount(), false);
            }
        }

        internal void NotifyDataSetChanged()
        {
            FocusNearest(HasFocus ? focusedDataIndex : GetInitialFocusOrigin(), GetNavigationCount(), false);
        }

        /// <summary>
        /// 由 NavigationScope 事件驱动；scope 只在状态变化时触发，这里不再去重。
        /// 解除时不滚动：ScrollToAligned 不判断可见性，会把刚点击的项重新对齐。
        /// </summary>
        internal void SetNavigationSuppressed(bool value)
        {
            suppressed = value;
            RefreshVisibleNavigationFocus();
        }

        #endregion

        #region 焦点写入（SetFocus → FocusNearest → ApplyFocus）

        private void ClearFocus()
        {
            focusedDataIndex = -1;
            NotifyVisibleHolders(-1);
        }

        /// <summary>焦点无效时初始化并返回 true：本次输入只用于建立焦点。</summary>
        private bool InitializeFocusIfInvalid(int count)
        {
            if ((uint)focusedDataIndex < (uint)count)
            {
                return false;
            }

            FocusNearest(GetInitialFocusOrigin(), count, true);
            return true;
        }

        private void FocusNearest(int origin, int count, bool scroll)
        {
            int target = FindNearestFocusable(origin, count);
            if (target < 0)
            {
                ClearFocus();
                return;
            }

            ApplyFocus(target, scroll);
        }

        /// <summary>不做校验的写入口；调用方保证 dataIndex 有效且可聚焦。</summary>
        private void ApplyFocus(int dataIndex, bool scroll)
        {
            focusedDataIndex = dataIndex;
            if (scroll)
            {
                if (focusAlignment == ScrollAlignment.Start)
                {
                    recyclerView.ScrollToIndex(dataIndex, smoothScroll);
                }
                else
                {
                    recyclerView.ScrollToAligned(dataIndex, focusAlignment, 0f, smoothScroll, smoothScrollDuration);
                }
            }

            RefreshVisibleNavigationFocus();
        }

        private int GetInitialFocusOrigin()
        {
            int scrollIndex = recyclerView.ScrollDataIndex;
            if (scrollIndex >= 0)
            {
                return scrollIndex;
            }

            // layoutManager 是序列化引用，Adapter 设置前即存在（SetAdapter 无条件解引用它），无需判空。
            LayoutManager layoutManager = recyclerView.LayoutManager;
            return layoutManager.GetDataIndex(layoutManager.GetStartIndex());
        }

        #endregion

        #region 可聚焦判断（模板门槛 + 过滤器）

        /// <summary>调用方保证 0 &lt;= dataIndex &lt; count。</summary>
        private bool IsDataIndexFocusable(int dataIndex)
        {
            IAdapter adapter = recyclerView.RecyclerViewAdapter;
            return recyclerView.ViewProvider.GetTemplate(adapter.GetTemplateId(dataIndex)) is IRecyclerViewNavigationViewHolder &&
                   (focusFilter == null || focusFilter(dataIndex));
        }

        private int FindNearestFocusable(int origin, int count)
        {
            if (count <= 0)
            {
                return -1;
            }

            origin = Mathf.Clamp(origin, 0, count - 1);
            if (IsDataIndexFocusable(origin))
            {
                return origin;
            }

            int maxOffset = Mathf.Max(origin, count - 1 - origin);
            for (int offset = 1; offset <= maxOffset; offset++)
            {
                int forward = origin + offset;
                if (forward < count && IsDataIndexFocusable(forward))
                {
                    return forward;
                }

                int backward = origin - offset;
                if (backward >= 0 && IsDataIndexFocusable(backward))
                {
                    return backward;
                }
            }

            return -1;
        }

        private int GetNavigationCount()
        {
            IAdapter adapter = recyclerView.RecyclerViewAdapter;
            if (adapter == null)
            {
                return 0;
            }

            int realCount = adapter.GetRealCount();
            return realCount > 0 ? realCount : adapter.GetItemCount();
        }

        #endregion

        #region 移动算法

        private MoveResult CalculateMove(MoveDirection direction, int count, out int next)
        {
            LayoutManager layoutManager = recyclerView.LayoutManager;
            bool circle = layoutManager is CircleLayoutManager;
            bool wrap = wrapNavigation || circle || recyclerView.IsLoopingAdapter();
            if (layoutManager is GridLayoutManager)
            {
                return CalculateGridMove(direction, count, layoutManager.Unit, wrap, out next);
            }

            // 环形布局两个轴都映射到 ±1；线性布局只响应主轴，垂直于主轴的方向交给 navigation。
            bool alongAxis = circle ||
                             (direction is MoveDirection.Up or MoveDirection.Down) == (recyclerView.Direction == Direction.Vertical);
            if (!alongAxis)
            {
                next = -1;
                return MoveResult.Boundary;
            }

            return FindNextFocusable(direction is MoveDirection.Left or MoveDirection.Up ? -1 : 1, count, wrap, out next);
        }

        private MoveResult FindNextFocusable(int delta, int count, bool wrap, out int next)
        {
            for (int step = 1; step < count; step++)
            {
                int candidate = focusedDataIndex + delta * step;
                if ((uint)candidate >= (uint)count)
                {
                    if (!wrap)
                    {
                        next = -1;
                        return MoveResult.Boundary;
                    }

                    candidate = (candidate % count + count) % count;
                }

                if (IsDataIndexFocusable(candidate))
                {
                    next = candidate;
                    return MoveResult.Moved;
                }
            }

            next = -1;
            return wrap ? MoveResult.Blocked : MoveResult.Boundary;
        }

        private MoveResult CalculateGridMove(MoveDirection direction, int count, int layoutUnit, bool wrap, out int next)
        {
            next = -1;
            int unit = Mathf.Max(1, layoutUnit);
            bool vertical = recyclerView.Direction == Direction.Vertical;

            // 行内/跨行由“移动方向 + 布局方向”决定；不能用 |delta| 判断（unit == 1 时会把上/下误判成行内移动）。
            bool stepInsideLine = (direction is MoveDirection.Left or MoveDirection.Right) == vertical;
            int sign = direction is MoveDirection.Left or MoveDirection.Up ? -1 : 1;
            int delta = stepInsideLine ? sign : sign * unit;

            int startMajor = focusedDataIndex / unit;
            int startMinor = focusedDataIndex % unit;
            int candidate = focusedDataIndex + delta;
            while (candidate >= 0 && candidate < count)
            {
                bool leftLine = stepInsideLine ? candidate / unit != startMajor : candidate % unit != startMinor;
                if (leftLine)
                {
                    if (!wrap)
                    {
                        return MoveResult.Boundary;
                    }

                    // 环绕：跨行时跳一整行，行内移动时跳一整列，回到同一行/列的对应位置。
                    candidate = stepInsideLine
                        ? candidate + (candidate < focusedDataIndex ? count : -count)
                        : candidate + delta * (count - 1);
                    if (candidate < 0 || candidate >= count || !IsDataIndexFocusable(candidate))
                    {
                        return MoveResult.Boundary;
                    }

                    next = candidate;
                    return MoveResult.Moved;
                }

                if (IsDataIndexFocusable(candidate))
                {
                    next = candidate;
                    return MoveResult.Moved;
                }

                candidate += delta;
            }

            return MoveResult.Boundary;
        }

        #endregion

        #region 焦点视觉

        /// <summary>焦点可见性的唯一规则；不可见时为 -1。</summary>
        private int VisibleFocusIndex =>
            (suppressed ? keepFocusVisibleWhenSuppressed : isSelected && cursorMode) ? focusedDataIndex : -1;

        private void RefreshVisibleNavigationFocus()
        {
            NotifyVisibleHolders(VisibleFocusIndex);
        }

        // 只在状态翻转时刷新；每次按键都刷新没有意义。
        private void EnterCursorMode()
        {
            if (cursorMode)
            {
                return;
            }

            cursorMode = true;
            RefreshVisibleNavigationFocus();
        }

        private void OnInputTypeChanged(InputWatch.InputType _)
        {
            cursorMode = UXInput.Watch.IsNavigationInput;  // 事件由 Watch 自身发出，此处必然非 null
            RefreshVisibleNavigationFocus();
        }

        /// <summary>focusIndex 为 -1 时全部取消。去重在 holder 内完成。</summary>
        private void NotifyVisibleHolders(int focusIndex)
        {
            ViewProvider provider = recyclerView.ViewProvider;
            if (provider == null)
            {
                return;                                    // Adapter 尚未设置
            }

            for (int i = 0; i < provider.VisibleCount; i++)
            {
                ViewHolder holder = provider.GetVisibleViewHolder(i);
                if (holder is IRecyclerViewNavigationViewHolder navigationHolder)
                {
                    // 必须带 focusIndex >= 0：绑定失败的项 DataIndex 也是 -1。
                    navigationHolder.SetNavigationFocused(focusIndex >= 0 && holder.DataIndex == focusIndex);
                }
            }
        }

        // 焦点不可见时所有项已是 false，新绑定项因版本号变化也读作 false，无需遍历。
        private void OnScrollValueChanged(float _)
        {
            RefreshVisibleFocusOnScroll();
        }

        // HandleScrollSettled 里的 FlushDeferredWork 可能产生绑定而不伴随滚动值变化。
        private void OnScrollStopped(ScrollStopReason _)
        {
            RefreshVisibleFocusOnScroll();
        }

        private void RefreshVisibleFocusOnScroll()
        {
            int index = VisibleFocusIndex;
            if (index >= 0)
            {
                NotifyVisibleHolders(index);
            }
        }

        private void SelectSelf()
        {
            // 控制器被禁用或不可交互时，子项点击仍可能转到这里。
            if (IsActive() && IsInteractable())
            {
                Select();
            }
        }

        #endregion

        private enum MoveResult
        {
            Moved,
            Blocked,
            Boundary
        }
    }
}
#endif
