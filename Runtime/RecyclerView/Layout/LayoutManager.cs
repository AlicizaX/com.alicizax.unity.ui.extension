using UnityEngine;

namespace AlicizaX.UI
{
    [System.Serializable]
    public abstract class LayoutManager : ILayoutManager
    {
        protected Vector2 viewportSize;
        public Vector2 ViewportSize
        {
            get => viewportSize;
            private set => viewportSize = value;
        }

        protected Vector2 contentSize;
        public Vector2 ContentSize
        {
            get => contentSize;
            private set => contentSize = value;
        }

        protected Vector2 contentOffset;
        public Vector2 ContentOffset
        {
            get => contentOffset;
            private set => contentOffset = value;
        }

        protected Vector2 viewportOffset;
        public Vector2 ViewportOffset
        {
            get => viewportOffset;
            private set => viewportOffset = value;
        }

        protected IAdapter adapter;
        public IAdapter Adapter
        {
            get => adapter;
            set => adapter = value;
        }

        protected ViewProvider viewProvider;
        public ViewProvider ViewProvider
        {
            get => viewProvider;
            set => viewProvider = value;
        }

        protected RecyclerView recyclerView;
        public virtual RecyclerView RecyclerView
        {
            get => recyclerView;
            set => recyclerView = value;
        }

        protected Direction direction;
        public Direction Direction
        {
            get => direction;
            set => direction = value;
        }

        protected Alignment alignment;
        public Alignment Alignment
        {
            get => alignment;
            set => alignment = value;
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

        protected int unit = 1;
        public int Unit
        {
            get => unit;
            set => unit = value;
        }


        public float ScrollPosition => recyclerView.GetScrollPosition();

        public virtual bool UsesVirtualLayoutRange => false;

        public LayoutManager() { }

        public void SetContentSize()
        {
            viewportSize = recyclerView.GetComponent<RectTransform>().rect.size;
            contentSize = CalculateContentSize();
            contentOffset = CalculateContentOffset();
            viewportOffset = CalculateViewportOffset();
        }

        public void UpdateLayout()
        {
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
            Vector3 position = direction == Direction.Vertical ?
                                new Vector3(pos.x - contentOffset.x, -pos.y + contentOffset.y, 0) :
                                new Vector3(pos.x - contentOffset.x, -pos.y + contentOffset.y, 0);
            viewHolder.RectTransform.anchoredPosition3D = position;
        }

        public abstract Vector2 CalculateContentSize();

        public abstract Vector2 CalculatePosition(int index);

        public abstract Vector2 CalculateContentOffset();

        public abstract Vector2 CalculateViewportOffset();

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

        public virtual void DoItemAnimation() { }

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

        public virtual bool IsFullVisibleStart(int index)
        {
            Vector2 vector2 = CalculatePosition(index);
            float position = direction == Direction.Vertical ? vector2.y : vector2.x;
            return position + GetOffset() >= 0;
        }

        public virtual bool IsFullInvisibleStart(int index)
        {
            Vector2 vector2 = CalculatePosition(index + unit);
            float position = direction == Direction.Vertical ? vector2.y : vector2.x;
            return position + GetOffset() < 0;
        }

        public virtual bool IsFullVisibleEnd(int index)
        {
            Vector2 vector2 = CalculatePosition(index + unit);
            float position = direction == Direction.Vertical ? vector2.y : vector2.x;
            float viewLength = direction == Direction.Vertical ? viewportSize.y : viewportSize.x;
            return position + GetOffset() <= viewLength;
        }

        public virtual bool IsFullInvisibleEnd(int index)
        {
            Vector2 vector2 = CalculatePosition(index);
            float position = direction == Direction.Vertical ? vector2.y : vector2.x;
            float viewLength = direction == Direction.Vertical ? viewportSize.y : viewportSize.x;
            return position + GetOffset() > viewLength;
        }

        public virtual bool IsVisible(int index)
        {
            float position, viewLength;
            viewLength = direction == Direction.Vertical ? viewportSize.y : viewportSize.x;

            Vector2 vector2 = CalculatePosition(index);
            position = direction == Direction.Vertical ? vector2.y : vector2.x;
            if (position + GetOffset() > 0 && position + GetOffset() <= viewLength)
            {
                return true;
            }

            vector2 = CalculatePosition(index + unit);
            position = direction == Direction.Vertical ? vector2.y : vector2.x;
            if (position + GetOffset() > 0 && position + GetOffset() <= viewLength)
            {
                return true;
            }

            return false;
        }

        protected virtual float GetFitContentSize()
        {
            float len;
            if (direction == Direction.Vertical)
            {
                len = alignment == Alignment.Center ? Mathf.Min(contentSize.y, viewportSize.y) : viewportSize.y;
            }
            else
            {
                len = alignment == Alignment.Center ? Mathf.Min(contentSize.x, viewportSize.x) : viewportSize.x;
            }
            return len;
        }

        protected virtual float GetOffset()
        {
            return direction == Direction.Vertical ? -contentOffset.y + viewportOffset.y : -contentOffset.x + viewportOffset.x;
        }
    }

    public enum Direction
    {
        Vertical = 0,
        Horizontal = 1,
        Custom = 2
    }

    public enum Alignment
    {
        Left,
        Center,
        Top
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
