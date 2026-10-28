using UnityEngine;

namespace AlicizaX.UI
{
    [System.Serializable]
    public abstract class LayoutManager : ILayoutManager
    {
        protected Vector2 viewportSize;

        public Vector2 ViewportSize => viewportSize;

        protected Vector2 contentSize;

        public Vector2 ContentSize => contentSize;

        /// <summary>
        /// 内容在交叉轴/主轴上的居中偏移，仅由 <see cref="Layout"/> 使用，不对外暴露。
        /// </summary>
        protected Vector2 contentOffset;

        protected IAdapter adapter;

        public IAdapter Adapter
        {
            get => adapter;
            internal set => adapter = value;
        }

        internal ViewProvider viewProvider;

        internal virtual ViewProvider ViewProvider
        {
            get => viewProvider;
            set => viewProvider = value;
        }

        protected RectTransform cachedViewportRect;

        protected RecyclerView recyclerView;

        public virtual RecyclerView RecyclerView
        {
            get => recyclerView;
            internal set
            {
                recyclerView = value;
                cachedViewportRect = value != null ? value.GetComponent<RectTransform>() : null;
            }
        }

        protected Direction direction;

        public Direction Direction
        {
            get => direction;
            set => direction = value;
        }

        /// <summary>
        /// 内容不足一屏时是否在交叉轴上居中。
        /// </summary>
        protected bool centerCrossAxis;

        public bool CenterCrossAxis
        {
            get => centerCrossAxis;
            set => centerCrossAxis = value;
        }

        protected Vector2 spacing;

        public Vector2 Spacing
        {
            get => spacing;
            set => spacing = value;
        }

        protected Vector2 padding;

        public Vector2 Padding
        {
            get => padding;
            set => padding = value;
        }

        /// <summary>
        /// 一个布局单元包含的项数（网格的列/行数）。由具体布局派生，不再作为可变字段。
        /// </summary>
        public virtual int Unit => 1;

        public float ScrollPosition => recyclerView.GetScrollPosition();

        public virtual bool UsesVirtualLayoutRange => false;

        public virtual bool SupportsVariableItemLength => false;

        /// <summary>
        /// 当前是否真的启用了变长项。变长协议的判定只在这里定义，RecyclerView/UGList 不再各写一份。
        /// </summary>
        public virtual bool VariableItemLengthEnabled => false;

        public LayoutManager()
        {
        }

        public virtual void Release()
        {
        }

        public virtual void InvalidateItemLengths()
        {
        }

        /// <summary>
        /// 数据是"尾部追加"时调用。变长管理器可只把新增段并入索引，避免整表 O(n) 重建。
        /// </summary>
        public virtual void NotifyItemsAppended(int count)
        {
            InvalidateItemLengths();
        }

        public virtual bool ApplyMeasuredLength(int index, float length)
        {
            return false;
        }

        public void SetContentSize()
        {
            if (cachedViewportRect != null)
            {
                viewportSize = cachedViewportRect.rect.size;
            }

            contentSize = CalculateContentSize();
            contentOffset = CalculateContentOffset();
        }

        public void UpdateLayout()
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

                Layout(viewHolder, viewHolder.Index);
            }
        }

        public virtual void Layout(ViewHolder viewHolder, int index)
        {
            Vector2 pos = CalculatePosition(index);
            Vector3 position = new Vector3(pos.x - contentOffset.x, -pos.y + contentOffset.y, 0f);
            viewHolder.RectTransform.anchoredPosition3D = position;
        }

        protected void LayoutFromItemStart(ViewHolder viewHolder, int index)
        {
            Vector2 pos = CalculatePosition(index);
            float itemLength = GetItemLength(index);
            if (itemLength <= 0f)
            {
                itemLength = ItemMeasure.ReadAxisLength(viewHolder.RectTransform, direction);
            }

            float axisOffset = itemLength * 0.5f;
            Vector3 position = direction == Direction.Vertical
                ? new Vector3(pos.x - contentOffset.x, -pos.y + contentOffset.y - axisOffset, 0f)
                : new Vector3(pos.x - contentOffset.x + axisOffset, -pos.y + contentOffset.y, 0f);
            viewHolder.RectTransform.anchoredPosition3D = position;
        }

        protected Vector2 VariableStartContentOffset()
        {
            return direction == Direction.Vertical
                ? new Vector2(0f, viewportSize.y * 0.5f)
                : new Vector2(viewportSize.x * 0.5f, 0f);
        }

        /// <summary>
        /// 主轴方向上的内边距。坐标空间统一包含 padding，避免 culling 与位置计算各算一套。
        /// </summary>
        protected float AxisPadding => direction == Direction.Vertical ? padding.y : padding.x;

        protected float AxisSpacing => direction == Direction.Vertical ? spacing.y : spacing.x;

        protected float AxisViewportLength => direction == Direction.Vertical ? viewportSize.y : viewportSize.x;

        public abstract Vector2 CalculateContentSize();

        public abstract Vector2 CalculatePosition(int index);

        public abstract Vector2 CalculateContentOffset();

        public abstract int GetStartIndex();

        public abstract int GetEndIndex();

        public abstract float IndexToPosition(int index);

        public abstract int PositionToIndex(float position);

        public abstract float GetItemStartPosition(int index);

        public abstract float GetItemLength(int index);

        public virtual int GetDataIndex(int layoutIndex)
        {
            if (adapter == null)
            {
                return layoutIndex;
            }

            int itemCount = adapter.GetItemCount();
            int realCount = adapter.GetRealCount();
            if (realCount <= 0)
            {
                return layoutIndex;
            }

            return itemCount != realCount ? WrapIndex(layoutIndex, realCount) : layoutIndex;
        }

        public virtual int GetLayoutIndex(int dataIndex)
        {
            if (adapter == null)
            {
                return dataIndex;
            }

            int itemCount = adapter.GetItemCount();
            int realCount = adapter.GetRealCount();
            if (realCount <= 0)
            {
                return dataIndex;
            }

            dataIndex = WrapIndex(dataIndex, realCount);
            return itemCount != realCount
                ? GetNearestWrappedLayoutIndex(dataIndex, PositionToIndex(ScrollPosition), realCount)
                : dataIndex;
        }

        public virtual int GetSnapIndex(float position)
        {
            if (adapter == null || adapter.GetItemCount() <= 0)
            {
                return 0;
            }

            return Mathf.Clamp(PositionToIndex(position), 0, adapter.GetItemCount() - 1);
        }

        public virtual void DoItemAnimation()
        {
        }

        protected static int WrapIndex(int index, int count)
        {
            if (count <= 0)
            {
                return index;
            }

            int wrapped = index % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }

        protected static int GetNearestWrappedLayoutIndex(int dataIndex, int centerLayoutIndex, int itemCount)
        {
            if (itemCount <= 0)
            {
                return dataIndex;
            }

            int cycle = Mathf.RoundToInt((centerLayoutIndex - dataIndex) / (float)itemCount);
            int candidate = dataIndex + cycle * itemCount;
            int previous = candidate - itemCount;
            int next = candidate + itemCount;

            if (Mathf.Abs(previous - centerLayoutIndex) < Mathf.Abs(candidate - centerLayoutIndex))
            {
                candidate = previous;
            }

            if (Mathf.Abs(next - centerLayoutIndex) < Mathf.Abs(candidate - centerLayoutIndex))
            {
                candidate = next;
            }

            return candidate;
        }

        protected virtual float GetFitContentSize()
        {
            float len;
            if (direction == Direction.Vertical)
            {
                len = centerCrossAxis ? Mathf.Min(contentSize.y, viewportSize.y) : viewportSize.y;
            }
            else
            {
                len = centerCrossAxis ? Mathf.Min(contentSize.x, viewportSize.x) : viewportSize.x;
            }

            return len;
        }
    }

    public enum Direction
    {
        Vertical = 0,
        Horizontal = 1
    }

    public enum ScrollbarVisibility
    {
        AlwaysHide = 0,
        AlwaysShow = 1,
        WhenScrollable = 2
    }

    public enum ScrollMode
    {
        AlwaysDisable = 0,
        AlwaysEnable = 1,
        WhenScrollable = 2
    }
}
