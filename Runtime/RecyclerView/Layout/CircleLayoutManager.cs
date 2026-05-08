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

        private float radius;
        private float initalAngle;

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
            return 0;
        }

        public override int GetEndIndex()
        {
            return adapter == null || adapter.GetItemCount() <= 0 ? -1 : adapter.GetItemCount() - 1;
        }

        public override bool IsFullVisibleStart(int index) => false;

        public override bool IsFullInvisibleStart(int index) => false;

        public override bool IsFullVisibleEnd(int index) => false;

        public override bool IsFullInvisibleEnd(int index) => false;

        public override bool IsVisible(int index) => true;

        public override float IndexToPosition(int index)
        {
            if (Mathf.Approximately(intervalAngle, 0f))
            {
                return 0f;
            }

            float position = index * intervalAngle;

            return -position;
        }

        public override int PositionToIndex(float position)
        {
            if (Mathf.Approximately(intervalAngle, 0f))
            {
                return 0;
            }

            int index = Mathf.RoundToInt(position / intervalAngle);
            return -index;
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

            return Mathf.Clamp(PositionToIndex(position), 0, adapter.GetItemCount() - 1);
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

                float angle = i * intervalAngle + initalAngle;
                angle = circleDirection == CircleDirection.Positive ? angle + ScrollPosition : angle - ScrollPosition;
                float delta = (angle - initalAngle) % 360;
                delta = delta < 0 ? delta + 360 : delta;
                delta = delta > 180 ? 360 - delta : delta;
                float scale = delta < intervalAngle ? (1.4f - delta / intervalAngle) : 1;
                scale = Mathf.Max(scale, 1);

                viewHolder.RectTransform.localScale = Vector3.one * scale;
            }
        }
    }

    public enum CircleDirection
    {
        Positive,
        Negative
    }
}
