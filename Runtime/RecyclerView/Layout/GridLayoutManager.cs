using UnityEngine;
using UnityEngine.Serialization;

namespace AlicizaX.UI
{
    [System.Serializable]
    public class GridLayoutManager : LayoutManager
    {
        private Vector2 cellSize;

        [FormerlySerializedAs("cellCounnt")]
        [SerializeField] private int cellCount = 1;

        public GridLayoutManager()
        {
        }

        public override Vector2 CalculateContentSize()
        {
            unit = Mathf.Max(1, cellCount);
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount <= 0)
            {
                cellSize = Vector2.zero;
                return direction == Direction.Vertical
                    ? new Vector2(contentSize.x, padding.y * 2)
                    : new Vector2(padding.x * 2, contentSize.y);
            }

            cellSize = viewProvider.CalculateViewSize(0);

            int row = Mathf.CeilToInt(itemCount / (float)unit);
            float len;
            if (direction == Direction.Vertical)
            {
                len = row * (cellSize.y + spacing.y) - spacing.y;
                return new Vector2(contentSize.x, len + padding.y * 2);
            }

            len = row * (cellSize.x + spacing.x) - spacing.x;
            return new Vector2(len + padding.x * 2, contentSize.y);
        }

        public override Vector2 CalculatePosition(int index)
        {
            int row = index / unit;
            int column = index % unit;
            float x, y;
            if (direction == Direction.Vertical)
            {
                x = column * (cellSize.x + spacing.x);
                y = row * (cellSize.y + spacing.y) - ScrollPosition;
            }
            else
            {
                x = row * (cellSize.x + spacing.x) - ScrollPosition;
                y = column * (cellSize.y + spacing.y);
            }

            return new Vector2(x + padding.x, y + padding.y);
        }

        public override Vector2 CalculateContentOffset()
        {
            if (cellSize == Vector2.zero)
            {
                return Vector2.zero;
            }

            float width, height;
            if (alignment == Alignment.Center)
            {
                width = Mathf.Min(contentSize.x, viewportSize.x);
                height = Mathf.Min(contentSize.y, viewportSize.y);
            }
            else
            {
                width = viewportSize.x;
                height = viewportSize.y;
            }
            return new Vector2((width - cellSize.x) / 2, (height - cellSize.y) / 2);
        }

        public override Vector2 CalculateViewportOffset()
        {
            if (cellSize == Vector2.zero)
            {
                return Vector2.zero;
            }

            float width, height;
            if (alignment == Alignment.Center)
            {
                width = Mathf.Min(contentSize.x, viewportSize.x);
                height = Mathf.Min(contentSize.y, viewportSize.y);
            }
            else
            {
                width = viewportSize.x;
                height = viewportSize.y;
            }
            return new Vector2((width - cellSize.x) / 2, (height - cellSize.y) / 2);
        }

        public override int GetStartIndex()
        {
            if (adapter == null || adapter.GetItemCount() <= 0)
            {
                return 0;
            }

            float len = direction == Direction.Vertical ? cellSize.y + spacing.y : cellSize.x + spacing.x;
            if (len <= 0f)
            {
                return 0;
            }

            int index = Mathf.FloorToInt(ScrollPosition / len) * unit;
            return Mathf.Max(0, index);
        }

        public override int GetEndIndex()
        {
            if (adapter == null || adapter.GetItemCount() <= 0)
            {
                return -1;
            }

            float viewLength = direction == Direction.Vertical ? viewportSize.y : viewportSize.x;
            float len = direction == Direction.Vertical ? cellSize.y + spacing.y : cellSize.x + spacing.x;
            if (len <= 0f)
            {
                return adapter.GetItemCount() - 1;
            }

            int index = Mathf.FloorToInt((ScrollPosition + viewLength) / len) * unit;
            return Mathf.Min(index, adapter.GetItemCount() - 1);
        }

        public override float IndexToPosition(int index)
        {
            if (adapter == null || adapter.GetItemCount() <= 0)
            {
                return 0f;
            }

            int row = index / unit;
            float len, viewLength, position;
            if (direction == Direction.Vertical)
            {
                len = row * (cellSize.y + spacing.y);
                viewLength = viewportSize.y;
                position = len + viewLength > contentSize.y ? contentSize.y - viewportSize.y : len;
            }
            else
            {
                len = row * (cellSize.x + spacing.x);
                viewLength = viewportSize.x;
                position = len + viewLength > contentSize.x ? contentSize.x - viewportSize.x : len;
            }

            return Mathf.Max(position, 0f);
        }

        public override int PositionToIndex(float position)
        {
            float len = direction == Direction.Vertical ? cellSize.y + spacing.y : cellSize.x + spacing.x;
            if (len <= 0f)
            {
                return 0;
            }

            int index = Mathf.RoundToInt(position / len);

            return index * unit;
        }

        public override float GetItemStartPosition(int index)
        {
            if (adapter == null || adapter.GetItemCount() <= 0)
            {
                return 0f;
            }

            index = Mathf.Clamp(index, 0, adapter.GetItemCount() - 1);
            int row = index / unit;
            float len = direction == Direction.Vertical ? cellSize.y + spacing.y : cellSize.x + spacing.x;
            return row * len;
        }

        public override float GetItemLength(int index)
        {
            return direction == Direction.Vertical ? Mathf.Max(cellSize.y, 0f) : Mathf.Max(cellSize.x, 0f);
        }

        public override int GetSnapIndex(float position)
        {
            if (adapter == null || adapter.GetItemCount() <= 0)
            {
                return 0;
            }

            float len = direction == Direction.Vertical ? cellSize.y + spacing.y : cellSize.x + spacing.x;
            float itemLength = GetItemLength(0);
            if (len <= 0f || itemLength <= 0f)
            {
                return 0;
            }

            int itemCount = adapter.GetItemCount();
            int row = Mathf.FloorToInt(Mathf.Max(position, 0f) / len);
            int index = Mathf.Clamp(row * unit, 0, itemCount - 1);

            float visibleLength = itemLength - (position - GetItemStartPosition(index));
            if (visibleLength < itemLength * 0.5f && index + unit < itemCount)
            {
                index += unit;
            }

            return index;
        }
    }
}
