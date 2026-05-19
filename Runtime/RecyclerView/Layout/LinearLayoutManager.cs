using System;
using UnityEngine;

namespace AlicizaX.UI
{
    [Serializable]
    public class LinearLayoutManager : LayoutManager
    {
        protected float lineHeight;

        public LinearLayoutManager() { }

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
                return new Vector2(contentSize.x, position + padding.y * 2);
            }
            position = itemCount * (lineHeight + spacing.x) - spacing.x;
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
            if (lineHeight <= 0f)
            {
                return Vector2.zero;
            }

            float len = GetFitContentSize();
            if (direction == Direction.Vertical)
            {
                return new Vector2(0, (len - lineHeight) / 2);
            }
            return new Vector2((len - lineHeight) / 2, 0);
        }

        public override Vector2 CalculateViewportOffset()
        {
            if (lineHeight <= 0f)
            {
                return Vector2.zero;
            }

            if (direction == Direction.Vertical)
            {
                return new Vector2(0, (viewportSize.y - lineHeight) / 2);
            }
            return new Vector2((viewportSize.x - lineHeight) / 2, 0);
        }

        public override int GetStartIndex()
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

            int index = Mathf.FloorToInt(ScrollPosition / len);
            return Mathf.Max(0, index);
        }

        public override int GetEndIndex()
        {
            if (adapter == null || adapter.GetItemCount() <= 0)
            {
                return -1;
            }

            float viewLength = direction == Direction.Vertical ? viewportSize.y : viewportSize.x;
            float len = direction == Direction.Vertical ? lineHeight + spacing.y : lineHeight + spacing.x;
            if (len <= 0f)
            {
                return adapter.GetItemCount() - 1;
            }

            int index = Mathf.FloorToInt((ScrollPosition + viewLength) / len);
            return Mathf.Min(index, adapter.GetItemCount() - 1);
        }

        public override float IndexToPosition(int index)
        {
            if (adapter == null || adapter.GetItemCount() <= 0 || index < 0 || index >= adapter.GetItemCount()) return 0;

            float len, viewLength, position;
            if (direction == Direction.Vertical)
            {
                len = index * (lineHeight + spacing.y);
                viewLength = viewportSize.y;
                position = len + viewLength > contentSize.y ? contentSize.y - viewportSize.y : len;
            }
            else
            {
                len = index * (lineHeight + spacing.x);
                viewLength = viewportSize.x;
                position = len + viewLength > contentSize.x ? contentSize.x - viewportSize.x : len;
            }

            return Mathf.Max(position, 0f);
        }

        public override int PositionToIndex(float position)
        {
            float len = direction == Direction.Vertical ? lineHeight + spacing.y : lineHeight + spacing.x;
            if (len <= 0f)
            {
                return 0;
            }

            int index = Mathf.RoundToInt(position / len);

            return index;
        }

        public override float GetItemStartPosition(int index)
        {
            if (adapter == null || adapter.GetItemCount() <= 0)
            {
                return 0f;
            }

            index = Mathf.Clamp(index, 0, adapter.GetItemCount() - 1);
            float len = direction == Direction.Vertical ? lineHeight + spacing.y : lineHeight + spacing.x;
            return index * len;
        }

        public override float GetItemLength(int index)
        {
            return Mathf.Max(lineHeight, 0f);
        }

        public override int GetSnapIndex(float position)
        {
            if (adapter == null || adapter.GetItemCount() <= 0)
            {
                return 0;
            }

            float len = direction == Direction.Vertical ? lineHeight + spacing.y : lineHeight + spacing.x;
            float itemLength = GetItemLength(0);
            if (len <= 0f || itemLength <= 0f)
            {
                return 0;
            }

            int itemCount = adapter.GetItemCount();
            int index = Mathf.FloorToInt(Mathf.Max(position, 0f) / len);
            index = Mathf.Clamp(index, 0, itemCount - 1);

            float visibleLength = itemLength - (position - GetItemStartPosition(index));
            if (visibleLength < itemLength * 0.5f && index < itemCount - 1)
            {
                index++;
            }

            return index;
        }
    }
}
