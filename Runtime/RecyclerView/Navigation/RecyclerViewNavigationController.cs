#if INPUTSYSTEM_SUPPORT && UX_NAVIGATION
using AlicizaX.UI.UXNavigation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlicizaX.UI
{
    /// <summary>
    /// RecyclerView 手柄/键盘导航控制器。使用数据索引维护虚拟焦点，不绑定可回收的 ViewHolder。
    /// </summary>
    [AddComponentMenu("UI/RecyclerView Navigation Controller")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RecyclerView))]
    public sealed class RecyclerViewNavigationController : Selectable, ISubmitHandler, ICancelHandler
    {
        [SerializeField] private RecyclerView recyclerView;
        [SerializeField] private UXNavigationScope navigationScope;

        [SerializeField] private bool wrapNavigation;
        [SerializeField] private bool smoothScroll = true;
        [SerializeField] private float smoothScrollDuration = 0.18f;
        [SerializeField] private ScrollAlignment focusAlignment = ScrollAlignment.Center;

        [SerializeField] private Selectable exitLeft;
        [SerializeField] private Selectable exitRight;
        [SerializeField] private Selectable exitUp;
        [SerializeField] private Selectable exitDown;

        private int focusedDataIndex = -1;
        private int rememberedDataIndex = -1;
        private bool hasFocus;
        private bool suppressed;
        private bool lastScopeSuppressed;
        private bool registeredInScope;

        public int FocusedDataIndex => hasFocus ? focusedDataIndex : -1;
        public bool HasFocus => hasFocus;
        public bool IsSuppressed => suppressed;

        protected override void Awake()
        {
            base.Awake();
            ResolveRequiredReferences();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ResolveRequiredReferences();
            RegisterToNavigationScope();

            if (recyclerView != null)
            {
                recyclerView.OnScrollValueChanged += OnScrollValueChanged;
                recyclerView.OnScrollStopped += OnScrollStopped;
                recyclerView.OnNavigationDataChanged += NotifyDataSetChanged;
            }

            SyncSuppressionState(true);
            RefreshVisibleNavigationFocus();
        }

        private void LateUpdate()
        {
            SyncSuppressionState(false);
        }

        protected override void OnDisable()
        {
            if (recyclerView != null)
            {
                recyclerView.OnScrollValueChanged -= OnScrollValueChanged;
                recyclerView.OnScrollStopped -= OnScrollStopped;
                recyclerView.OnNavigationDataChanged -= NotifyDataSetChanged;
            }

            UnregisterFromNavigationScope();
            ApplyVisibleNavigationFocus(false);
            base.OnDisable();
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (HandleMove(eventData))
            {
                eventData.Use();
                return;
            }

            base.OnMove(eventData);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (HandleSubmit())
            {
                eventData.Use();
            }
        }

        public void OnCancel(BaseEventData eventData)
        {
            if (HandleCancel())
            {
                eventData.Use();
            }
        }

        public bool HandleMove(AxisEventData eventData)
        {
            if (suppressed || recyclerView == null || eventData.moveDir == MoveDirection.None)
            {
                return false;
            }

            int count = GetNavigationCount();
            if (count <= 0)
            {
                ClearFocus();
                return true;
            }

            if (!hasFocus || !IsValidDataIndex(focusedDataIndex))
            {
                InitializeFocus();
                return true;
            }

            if (TryCurrentHolderConsumeMove(eventData))
            {
                return true;
            }

            MoveResult result = TryCalculateNextDataIndex(eventData.moveDir, count, out int nextDataIndex);
            if (result == MoveResult.Moved)
            {
                SetFocus(nextDataIndex, true);
                return true;
            }

            if (result == MoveResult.Boundary)
            {
                TrySelectExit(eventData.moveDir);
            }

            return true;
        }

        public bool HandleSubmit()
        {
            if (suppressed || recyclerView == null)
            {
                return false;
            }

            if (!hasFocus || !IsValidDataIndex(focusedDataIndex))
            {
                InitializeFocus();
                return true;
            }

            recyclerView.RecyclerViewAdapter?.SetChoiceIndex(focusedDataIndex);
            return true;
        }

        public bool HandleCancel()
        {
            return false;
        }

        public void SetFocus(int dataIndex)
        {
            SetFocus(dataIndex, true);
        }

        public void RestoreFocus(int dataIndex)
        {
            if (!IsValidDataIndex(dataIndex))
            {
                dataIndex = FindNearestFocusable(Mathf.Max(0, dataIndex));
            }

            if (dataIndex < 0)
            {
                ClearFocus();
                return;
            }

            SetFocus(dataIndex, true);
            SelectSelf();
        }

        public void ClearFocus()
        {
            focusedDataIndex = -1;
            hasFocus = false;
            ApplyVisibleNavigationFocus(false);
        }

        internal void NotifyDataSetChanged()
        {
            int count = GetNavigationCount();
            if (count <= 0)
            {
                ClearFocus();
                return;
            }

            int target = hasFocus ? Mathf.Clamp(focusedDataIndex, 0, count - 1) : GetInitialFocusOrigin();
            if (!IsDataIndexFocusable(target))
            {
                target = FindNearestFocusable(target);
            }

            if (target < 0)
            {
                ClearFocus();
                return;
            }

            SetFocus(target, false);
        }

        private void ResolveRequiredReferences()
        {
            if (recyclerView == null)
            {
                recyclerView = GetComponent<RecyclerView>();
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            recyclerView = GetComponent<RecyclerView>();
            navigationScope = FindNearestParentNavigationScope();
        }

        private UXNavigationScope FindNearestParentNavigationScope()
        {
            Transform current = transform.parent;
            while (current != null)
            {
                if (current.TryGetComponent(out UXNavigationScope scope))
                {
                    return scope;
                }

                current = current.parent;
            }

            return null;
        }
#endif

        private void RegisterToNavigationScope()
        {
            if (registeredInScope || navigationScope == null)
            {
                return;
            }

            registeredInScope = navigationScope.RegisterSelectable(this, true);
            lastScopeSuppressed = navigationScope.NavigationSuppressed;
            suppressed = lastScopeSuppressed;
        }

        private void UnregisterFromNavigationScope()
        {
            if (!registeredInScope || navigationScope == null)
            {
                registeredInScope = false;
                return;
            }

            navigationScope.UnregisterSelectable(this);
            registeredInScope = false;
        }

        private void SyncSuppressionState(bool force)
        {
            bool currentSuppressed = navigationScope != null && navigationScope.NavigationSuppressed;
            if (!force && currentSuppressed == lastScopeSuppressed)
            {
                return;
            }

            lastScopeSuppressed = currentSuppressed;
            SetNavigationSuppressed(currentSuppressed);
        }

        public void SetNavigationSuppressed(bool value)
        {
            if (suppressed == value)
            {
                return;
            }

            suppressed = value;
            if (suppressed)
            {
                rememberedDataIndex = hasFocus ? focusedDataIndex : -1;
                ApplyVisibleNavigationFocus(false);
                return;
            }

            if (rememberedDataIndex >= 0)
            {
                RestoreFocus(rememberedDataIndex);
            }
            else
            {
                RefreshVisibleNavigationFocus();
            }
        }

        private void InitializeFocus()
        {
            int target = FindNearestFocusable(GetInitialFocusOrigin());
            if (target < 0)
            {
                ClearFocus();
                return;
            }

            SetFocus(target, true);
        }

        private int GetInitialFocusOrigin()
        {
            if (recyclerView == null || recyclerView.LayoutManager == null)
            {
                return 0;
            }

            int current = recyclerView.CurrentScrollDataIndex;
            if (current >= 0)
            {
                return current;
            }

            int layoutIndex = recyclerView.LayoutManager.GetStartIndex();
            return recyclerView.LayoutManager.GetDataIndex(layoutIndex);
        }

        private void SetFocus(int dataIndex, bool scroll)
        {
            if (!IsValidDataIndex(dataIndex))
            {
                ClearFocus();
                return;
            }

            if (!IsDataIndexFocusable(dataIndex))
            {
                int nearest = FindNearestFocusable(dataIndex);
                if (nearest < 0)
                {
                    ClearFocus();
                    return;
                }

                dataIndex = nearest;
            }

            focusedDataIndex = dataIndex;
            hasFocus = true;

            if (scroll)
            {
                ScrollToFocusedDataIndex();
            }

            RefreshVisibleNavigationFocus();
        }

        private void ScrollToFocusedDataIndex()
        {
            if (recyclerView == null || !hasFocus)
            {
                return;
            }

            if (focusAlignment == ScrollAlignment.Start)
            {
                recyclerView.ScrollTo(focusedDataIndex, smoothScroll);
                return;
            }

            recyclerView.ScrollToWithAlignment(focusedDataIndex, focusAlignment, 0f, smoothScroll, smoothScrollDuration);
        }

        private MoveResult TryCalculateNextDataIndex(MoveDirection direction, int count, out int nextDataIndex)
        {
            nextDataIndex = -1;
            LayoutManager layoutManager = recyclerView.LayoutManager;
            if (layoutManager == null)
            {
                return MoveResult.Blocked;
            }

            if (layoutManager is GridLayoutManager)
            {
                return TryCalculateGridMove(direction, count, out nextDataIndex);
            }

            if (layoutManager is CircleLayoutManager)
            {
                return TryCalculateRingMove(direction, count, out nextDataIndex);
            }

            return TryCalculateLinearMove(direction, count, out nextDataIndex);
        }

        private MoveResult TryCalculateLinearMove(MoveDirection direction, int count, out int nextDataIndex)
        {
            nextDataIndex = -1;
            int delta = GetLinearDelta(direction);
            if (delta == 0)
            {
                return MoveResult.Boundary;
            }

            bool allowWrap = ShouldWrapNavigation();
            return TryFindNextFocusable(focusedDataIndex, delta, count, allowWrap, out nextDataIndex, out bool hitBoundary)
                ? MoveResult.Moved
                : hitBoundary
                    ? MoveResult.Boundary
                    : MoveResult.Blocked;
        }

        private MoveResult TryCalculateRingMove(MoveDirection direction, int count, out int nextDataIndex)
        {
            nextDataIndex = -1;
            int delta = direction == MoveDirection.Left || direction == MoveDirection.Up
                ? -1
                : direction == MoveDirection.Right || direction == MoveDirection.Down
                    ? 1
                    : 0;

            if (delta == 0)
            {
                return MoveResult.Blocked;
            }

            return TryFindNextFocusable(focusedDataIndex, delta, count, true, out nextDataIndex, out _)
                ? MoveResult.Moved
                : MoveResult.Blocked;
        }

        private MoveResult TryCalculateGridMove(MoveDirection direction, int count, out int nextDataIndex)
        {
            nextDataIndex = -1;
            int unit = Mathf.Max(1, recyclerView.LayoutManager.Unit);
            int delta;

            if (recyclerView.Direction == Direction.Vertical)
            {
                switch (direction)
                {
                    case MoveDirection.Left:
                        delta = -1;
                        break;
                    case MoveDirection.Right:
                        delta = 1;
                        break;
                    case MoveDirection.Up:
                        delta = -unit;
                        break;
                    case MoveDirection.Down:
                        delta = unit;
                        break;
                    default:
                        return MoveResult.Blocked;
                }
            }
            else
            {
                switch (direction)
                {
                    case MoveDirection.Up:
                        delta = -1;
                        break;
                    case MoveDirection.Down:
                        delta = 1;
                        break;
                    case MoveDirection.Left:
                        delta = -unit;
                        break;
                    case MoveDirection.Right:
                        delta = unit;
                        break;
                    default:
                        return MoveResult.Blocked;
                }
            }

            return TryFindNextGridFocusable(delta, count, unit, out nextDataIndex);
        }

        private MoveResult TryFindNextGridFocusable(int delta, int count, int unit, out int dataIndex)
        {
            dataIndex = -1;
            if (count <= 1 || delta == 0)
            {
                return MoveResult.Blocked;
            }

            bool stepInsideLine = delta == 1 || delta == -1;
            int startMajor = focusedDataIndex / unit;
            int startMinor = focusedDataIndex % unit;
            int candidate = focusedDataIndex + delta;
            while (candidate >= 0 && candidate < count)
            {
                if (stepInsideLine)
                {
                    if (candidate / unit != startMajor)
                    {
                        return MoveResult.Boundary;
                    }
                }
                else if (candidate % unit != startMinor)
                {
                    return MoveResult.Boundary;
                }

                if (IsDataIndexFocusable(candidate, count))
                {
                    dataIndex = candidate;
                    return MoveResult.Moved;
                }

                candidate += delta;
            }

            return MoveResult.Boundary;
        }

        private int GetLinearDelta(MoveDirection direction)
        {
            if (recyclerView.Direction == Direction.Vertical)
            {
                return direction == MoveDirection.Up ? -1 : direction == MoveDirection.Down ? 1 : 0;
            }

            if (recyclerView.Direction == Direction.Horizontal)
            {
                return direction == MoveDirection.Left ? -1 : direction == MoveDirection.Right ? 1 : 0;
            }

            return 0;
        }

        private bool TryFindNextFocusable(int start, int delta, int count, bool allowWrap, out int dataIndex, out bool hitBoundary)
        {
            dataIndex = -1;
            hitBoundary = false;
            if (count <= 1 || delta == 0)
            {
                return false;
            }

            int candidate = start + delta;
            int attempts = count - 1;
            for (int i = 0; i < attempts; i++)
            {
                if (candidate < 0 || candidate >= count)
                {
                    if (!allowWrap)
                    {
                        hitBoundary = true;
                        return false;
                    }

                    candidate = candidate < 0 ? count - 1 : 0;
                }

                if (candidate != start && IsDataIndexFocusable(candidate, count))
                {
                    dataIndex = candidate;
                    return true;
                }

                candidate += delta;
            }

            return false;
        }

        private int FindNearestFocusable(int origin)
        {
            int count = GetNavigationCount();
            if (count <= 0)
            {
                return -1;
            }

            origin = Mathf.Clamp(origin, 0, count - 1);
            if (IsDataIndexFocusable(origin, count))
            {
                return origin;
            }

            for (int offset = 1; offset < count; offset++)
            {
                int forward = origin + offset;
                if (forward < count && IsDataIndexFocusable(forward, count))
                {
                    return forward;
                }

                int backward = origin - offset;
                if (backward >= 0 && IsDataIndexFocusable(backward, count))
                {
                    return backward;
                }
            }

            return -1;
        }

        private bool TryCurrentHolderConsumeMove(AxisEventData eventData)
        {
            ViewHolder holder = GetFocusedViewHolder();
            return holder is IRecyclerViewNavigationViewHolder navigationHolder &&
                   navigationHolder.HandleNavigationMove(eventData);
        }

        private ViewHolder GetFocusedViewHolder()
        {
            if (recyclerView == null || recyclerView.RecyclerViewAdapter == null || recyclerView.LayoutManager == null || !hasFocus)
            {
                return null;
            }

            int layoutIndex = recyclerView.LayoutManager.GetLayoutIndex(focusedDataIndex);
            ViewHolder holder = recyclerView.ViewProvider.GetViewHolder(layoutIndex);
            if (holder != null && holder.DataIndex == focusedDataIndex)
            {
                return holder;
            }

            int visibleCount = recyclerView.ViewProvider.VisibleCount;
            for (int i = 0; i < visibleCount; i++)
            {
                holder = recyclerView.ViewProvider.GetVisibleViewHolder(i);
                if (holder != null && holder.DataIndex == focusedDataIndex)
                {
                    return holder;
                }
            }

            return null;
        }

        private void RefreshVisibleNavigationFocus()
        {
            if (recyclerView != null && recyclerView.RecyclerViewAdapter != null)
            {
                int visibleCount = recyclerView.ViewProvider.VisibleCount;
                for (int i = 0; i < visibleCount; i++)
                {
                    ViewHolder holder = recyclerView.ViewProvider.GetVisibleViewHolder(i);
                    if (holder == null)
                    {
                        continue;
                    }

                    bool focused = hasFocus && !suppressed && holder.DataIndex == focusedDataIndex;
                    if (holder is IRecyclerViewNavigationViewHolder navigationHolder)
                    {
                        navigationHolder.HandleNavigationFocused(focused);
                    }
                }
            }
        }

        private void ApplyVisibleNavigationFocus(bool focused)
        {
            if (recyclerView == null || recyclerView.RecyclerViewAdapter == null)
            {
                return;
            }

            int visibleCount = recyclerView.ViewProvider.VisibleCount;
            for (int i = 0; i < visibleCount; i++)
            {
                ViewHolder holder = recyclerView.ViewProvider.GetVisibleViewHolder(i);
                if (holder is IRecyclerViewNavigationViewHolder navigationHolder)
                {
                    navigationHolder.HandleNavigationFocused(focused && holder.DataIndex == focusedDataIndex);
                }
            }
        }

        private void OnScrollValueChanged(float _)
        {
            if (hasFocus)
            {
                RefreshVisibleNavigationFocus();
            }
        }

        private void OnScrollStopped()
        {
            if (hasFocus)
            {
                RefreshVisibleNavigationFocus();
            }
        }

        private bool IsValidDataIndex(int dataIndex)
        {
            return dataIndex >= 0 && dataIndex < GetNavigationCount();
        }

        private bool IsDataIndexFocusable(int dataIndex)
        {
            IAdapter adapter = recyclerView != null ? recyclerView.RecyclerViewAdapter : null;
            return adapter != null && IsDataIndexFocusable(dataIndex, GetNavigationCount(), adapter);
        }

        private bool IsDataIndexFocusable(int dataIndex, int count)
        {
            IAdapter adapter = recyclerView != null ? recyclerView.RecyclerViewAdapter : null;
            return adapter != null && IsDataIndexFocusable(dataIndex, count, adapter);
        }

        private bool IsDataIndexFocusable(int dataIndex, int count, IAdapter adapter)
        {
            if (adapter == null || dataIndex < 0 || dataIndex >= count)
            {
                return false;
            }

            int templateId = adapter.GetTemplateId(dataIndex);
            ViewHolder template = recyclerView.ViewProvider.GetTemplate(templateId);
            return template is IRecyclerViewNavigationViewHolder;
        }

        private int GetNavigationCount()
        {
            IAdapter adapter = recyclerView != null ? recyclerView.RecyclerViewAdapter : null;
            if (adapter == null)
            {
                return 0;
            }

            int realCount = adapter.GetRealCount();
            if (realCount > 0)
            {
                return realCount;
            }

            int itemCount = adapter.GetItemCount();
            return itemCount > 0 ? itemCount : 0;
        }

        private bool ShouldWrapNavigation()
        {
            if (wrapNavigation)
            {
                return true;
            }

            IAdapter adapter = recyclerView != null ? recyclerView.RecyclerViewAdapter : null;
            if (adapter == null)
            {
                return false;
            }

            int realCount = adapter.GetRealCount();
            int itemCount = adapter.GetItemCount();
            return realCount > 0 && itemCount != realCount;
        }

        private bool TrySelectExit(MoveDirection direction)
        {
            Selectable selectable = direction switch
            {
                MoveDirection.Left => exitLeft,
                MoveDirection.Right => exitRight,
                MoveDirection.Up => exitUp,
                MoveDirection.Down => exitDown,
                _ => null
            };

            if (selectable == null || !selectable.IsActive() || !selectable.IsInteractable())
            {
                return false;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            eventSystem.SetSelectedGameObject(selectable.gameObject);
            return true;
        }

        private void SelectSelf()
        {
            if (!IsActive() || !IsInteractable())
            {
                return;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(gameObject);
            }
        }

        private enum MoveResult
        {
            Moved,
            Blocked,
            Boundary
        }
    }
}
#endif
