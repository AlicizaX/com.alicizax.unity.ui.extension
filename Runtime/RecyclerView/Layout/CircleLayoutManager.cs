using UnityEngine;

namespace AlicizaX.UI
{
    [System.Serializable]
    public class CircleLayoutManager : LayoutManager
    {
        [SerializeField]
        private  CircleDirection circleDirection= CircleDirection.Positive;
        [SerializeField]
        private float intervalAngle=0;
        [SerializeField]
        private int maxVisibleItemCount = 32;

        private float radius;
        private float initalAngle;

        public override bool UsesVirtualLayoutRange => true;

        public CircleLayoutManager()
        {

        }

        public override Vector2 CalculateContentSize()
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount <= 0)
            {
                radius = 0f;
                intervalAngle = 0f;
                return viewportSize;
            }

            Vector2 size = viewProvider.CalculateViewSize(0);
            radius = (Mathf.Min(viewportSize.x, viewportSize.y) - Mathf.Min(size.x, size.y)) / 2f - Mathf.Max(padding.x, padding.y);
            intervalAngle = 360f / itemCount;

            return viewportSize;
        }

        public override Vector2 CalculateContentOffset()
        {
            return Vector2.zero;
        }

        public override Vector2 CalculateViewportOffset()
        {
            return Vector2.zero;
        }

        public override void Layout(ViewHolder viewHolder, int index)
        {
            viewHolder.RectTransform.anchoredPosition3D = CalculatePosition(index);
        }

        public override Vector2 CalculatePosition(int index)
        {
            float angle = index * intervalAngle;
            angle = circleDirection == CircleDirection.Positive ? angle : -angle;
            angle += initalAngle + ScrollPosition;
            float radian = angle * (Mathf.PI / 180f);
            float x = radius * Mathf.Sin(radian);
            float y = radius * Mathf.Cos(radian);

            return new Vector2(x, y);
        }

        public override int GetStartIndex()
        {
            GetVisibleWindow(out int start, out _);
            return start;
        }

        public override int GetEndIndex()
        {
            GetVisibleWindow(out _, out int end);
            return end;
        }

        public override bool IsFullVisibleStart(int index) => index > GetStartIndex();

        public override bool IsFullInvisibleStart(int index) => index < GetStartIndex();

        public override bool IsFullVisibleEnd(int index) => index < GetEndIndex();

        public override bool IsFullInvisibleEnd(int index) => index > GetEndIndex();

        public override bool IsVisible(int index)
        {
            GetVisibleWindow(out int start, out int end);
            return index >= start && index <= end;
        }

        public override float IndexToPosition(int index)
        {
            if (Mathf.Approximately(intervalAngle, 0f))
            {
                return 0f;
            }

            float position = index * intervalAngle;
            return circleDirection == CircleDirection.Positive ? -position : position;
        }

        public override int PositionToIndex(float position)
        {
            if (Mathf.Approximately(intervalAngle, 0f))
            {
                return 0;
            }

            int index = Mathf.RoundToInt(position / intervalAngle);
            return circleDirection == CircleDirection.Positive ? -index : index;
        }

        public override float GetItemStartPosition(int index)
        {
            return IndexToPosition(index);
        }

        public override float GetItemLength(int index)
        {
            return Mathf.Abs(intervalAngle);
        }

        public override int GetSnapIndex(float position)
        {
            if (adapter == null || adapter.GetItemCount() <= 0)
            {
                return 0;
            }

            return PositionToIndex(position);
        }

        public override int GetDataIndex(int layoutIndex)
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            return itemCount > 0 ? WrapIndex(layoutIndex, itemCount) : layoutIndex;
        }

        public override int GetLayoutIndex(int dataIndex)
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount <= 0)
            {
                return dataIndex;
            }

            return GetNearestWrappedLayoutIndex(WrapIndex(dataIndex, itemCount), PositionToIndex(ScrollPosition), itemCount);
        }

        public override void DoItemAnimation()
        {
            int visibleCount = viewProvider.VisibleCount;
            for (int i = 0; i < visibleCount; i++)
            {
                ViewHolder viewHolder = viewProvider.GetVisibleViewHolder(i);
                if (viewHolder == null)
                {
                    continue;
                }

                float angle = viewHolder.Index * intervalAngle + initalAngle;
                angle = circleDirection == CircleDirection.Positive ? angle + ScrollPosition : angle - ScrollPosition;
                float delta = (angle - initalAngle) % 360;
                delta = delta < 0 ? delta + 360 : delta;
                delta = delta > 180 ? 360 - delta : delta;
                float scale = delta < intervalAngle ? (1.4f - delta / intervalAngle) : 1;
                scale = Mathf.Max(scale, 1);

                viewHolder.RectTransform.localScale = Vector3.one * scale;
            }
        }

        private void GetVisibleWindow(out int start, out int end)
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount <= 0)
            {
                start = 0;
                end = -1;
                return;
            }

            int visibleCount = GetVisibleItemCount(itemCount);
            int centerIndex = PositionToIndex(ScrollPosition);
            start = centerIndex - visibleCount / 2;
            end = start + visibleCount - 1;
        }

        private int GetVisibleItemCount(int itemCount)
        {
            if (itemCount <= 0)
            {
                return 0;
            }

            return Mathf.Min(itemCount, Mathf.Max(1, maxVisibleItemCount));
        }
    }

    public enum CircleDirection
    {
        Positive,
        Negative
    }
}
