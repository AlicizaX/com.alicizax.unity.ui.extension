using System;
using UnityEngine;

namespace AlicizaX.UI
{
    [Serializable]
    public class PageLayoutManager : LinearLayoutManager
    {
        [SerializeField] private float minScale = 0.9f;

        public PageLayoutManager()
        {
        }

        public override Vector2 CalculateContentSize()
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount <= 0)
            {
                lineHeight = 0f;
                return direction == Direction.Vertical
                    ? new Vector2(contentSize.x, padding.y * 2)
                    : new Vector2(padding.x * 2, contentSize.y);
            }

            Vector2 size = viewProvider.CalculateViewSize(0);
            lineHeight = direction == Direction.Vertical ? size.y : size.x;

            float position;
            if (direction == Direction.Vertical)
            {
                position = itemCount * (lineHeight + spacing.y) - spacing.y;
                position += viewportSize.y - lineHeight;
                return new Vector2(contentSize.x, position + padding.y * 2);
            }

            position = itemCount * (lineHeight + spacing.x) - spacing.x;
            position += viewportSize.x - lineHeight;
            return new Vector2(position + padding.x * 2, contentSize.y);
        }

        public override Vector2 CalculatePosition(int index)
        {
            float position;
            if (direction == Direction.Vertical)
            {
                position = index * (lineHeight + spacing.y) - ScrollPosition;
                return new Vector2(0, position + padding.y);
            }

            position = index * (lineHeight + spacing.x) - ScrollPosition;
            return new Vector2(position + padding.x, 0);
        }

        public override Vector2 CalculateContentOffset()
        {
            return Vector2.zero;
        }

        public override Vector2 CalculateViewportOffset()
        {
            return Vector2.zero;
        }

        protected override float GetOffset()
        {
            if (lineHeight <= 0f)
            {
                return 0f;
            }

            float offset = direction == Direction.Vertical ? viewportSize.y - lineHeight : viewportSize.x - lineHeight;
            return offset / 2;
        }

        public override int PositionToIndex(float position)
        {
            if (adapter == null || adapter.GetItemCount() <= 0)
            {
                return 0;
            }

            float len = direction == Direction.Vertical ? lineHeight + spacing.y : lineHeight + spacing.x;
            if (len <= 0f)
            {
                return 0;
            }

            float pos = IndexToPosition(recyclerView.CurrentScrollDataIndex);
            int index = position > pos ? Mathf.RoundToInt(position / len + 0.25f) : Mathf.RoundToInt(position / len - 0.25f);

            return index;
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

                float viewPos = direction == Direction.Vertical ? -viewHolder.RectTransform.anchoredPosition.y : viewHolder.RectTransform.anchoredPosition.x;
                float scale = 1 - Mathf.Min(Mathf.Abs(viewPos) * 0.0006f, 1f);
                scale = Mathf.Max(scale, minScale);

                viewHolder.RectTransform.localScale = Vector3.one * scale;
            }
        }
    }
}
