using System;
using UnityEngine;
using UnityEngine.EventSystems;
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

        public override int Unit => Mathf.Max(1, cellCount);

        public override bool SupportsVirtualItemCount => true;

        public override Vector2 CalculateContentSize()
        {
            int unit = Unit;
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
            float len = row * (AxisCellLength + AxisSpacing) - AxisSpacing;
            return direction == Direction.Vertical
                ? new Vector2(viewportSize.x, len + padding.y * 2)
                : new Vector2(len + padding.x * 2, viewportSize.y);
        }

        public override Vector2 CalculatePosition(int index)
        {
            int unit = Unit;
            int row = index / unit;
            int column = index % unit;
            float axis = GetItemStartPosition(index) - ScrollPosition;
            float cross = column * (CrossCellLength + CrossSpacing);

            return direction == Direction.Vertical
                ? new Vector2(cross + padding.x, axis)
                : new Vector2(axis, cross + padding.y);
        }

        public override Vector2 CalculateContentOffset()
        {
            if (cellSize == Vector2.zero)
            {
                return Vector2.zero;
            }

            return new Vector2((viewportSize.x - cellSize.x) * 0.5f, (viewportSize.y - cellSize.y) * 0.5f);
        }

        public override int GetStartIndex()
        {
            if (adapter == null || adapter.GetItemCount() <= 0)
            {
                return 0;
            }

            float len = AxisCellLength + AxisSpacing;
            if (len <= 0f)
            {
                return 0;
            }

            int index = Mathf.FloorToInt((ScrollPosition - AxisPadding) / len) * Unit;
            return Mathf.Max(0, index);
        }

        public override int GetEndIndex()
        {
            if (adapter == null || adapter.GetItemCount() <= 0)
            {
                return -1;
            }

            float len = AxisCellLength + AxisSpacing;
            if (len <= 0f)
            {
                return adapter.GetItemCount() - 1;
            }

            int index = Mathf.FloorToInt((ScrollPosition + AxisViewportLength - AxisPadding) / len) * Unit;
            return Mathf.Min(index, adapter.GetItemCount() - 1);
        }

        public override float IndexToPosition(int index)
        {
            if (adapter == null || adapter.GetItemCount() <= 0)
            {
                return 0f;
            }

            float start = GetItemStartPosition(index);
            float max = direction == Direction.Vertical
                ? Mathf.Max(contentSize.y - viewportSize.y, 0f)
                : Mathf.Max(contentSize.x - viewportSize.x, 0f);
            return Mathf.Clamp(start, 0f, max);
        }

        public override int PositionToIndex(float position)
        {
            float len = AxisCellLength + AxisSpacing;
            if (len <= 0f)
            {
                return 0;
            }

            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount <= 0)
            {
                return 0;
            }

            int index = Mathf.RoundToInt((position - AxisPadding) / len) * Unit;
            return Mathf.Clamp(index, 0, itemCount - 1);
        }

        public override float GetItemStartPosition(int index)
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount <= 0)
            {
                return 0f;
            }

            index = Mathf.Clamp(index, 0, itemCount - 1);
            return AxisPadding + index / Unit * (AxisCellLength + AxisSpacing);
        }

        public override float GetItemLength(int index)
        {
            return Mathf.Max(AxisCellLength, 0f);
        }

        /// <summary>
        /// 行内移动 ±1、跨行移动 ±Unit。越出行/列是边界；wrap 时跳到同一行/列的另一端，落点不可聚焦仍算边界。
        /// </summary>
        public override NavigationMoveResult FindNavigationTarget(
            int dataIndex, MoveDirection moveDirection, int count, bool wrap, Predicate<int> isFocusable, out int target)
        {
            target = -1;

            // 行内/跨行由“移动方向 + 布局方向”决定；不能用 |delta| 判断（Unit == 1 时会把上/下误判成行内移动）。
            bool stepInsideLine = (moveDirection is MoveDirection.Left or MoveDirection.Right) == (direction == Direction.Vertical);
            int delta = stepInsideLine ? ForwardSign(moveDirection) : ForwardSign(moveDirection) * Unit;

            int startMajor = dataIndex / Unit;
            int startMinor = dataIndex % Unit;
            int candidate = dataIndex + delta;
            while (candidate >= 0 && candidate < count)
            {
                bool leftLine = stepInsideLine ? candidate / Unit != startMajor : candidate % Unit != startMinor;
                if (leftLine)
                {
                    if (!wrap)
                    {
                        return NavigationMoveResult.Boundary;
                    }

                    // 环绕：跨行时跳一整行，行内移动时跳一整列，回到同一行/列的对应位置。
                    candidate = stepInsideLine
                        ? candidate + (candidate < dataIndex ? count : -count)
                        : candidate + delta * (count - 1);
                    if (candidate < 0 || candidate >= count || !isFocusable(candidate))
                    {
                        return NavigationMoveResult.Boundary;
                    }

                    target = candidate;
                    return NavigationMoveResult.Moved;
                }

                if (isFocusable(candidate))
                {
                    target = candidate;
                    return NavigationMoveResult.Moved;
                }

                candidate += delta;
            }

            return NavigationMoveResult.Boundary;
        }

        public override int GetSnapIndex(float position)
        {
            if (adapter == null || adapter.GetItemCount() <= 0)
            {
                return 0;
            }

            float len = AxisCellLength + AxisSpacing;
            float itemLength = GetItemLength(0);
            if (len <= 0f || itemLength <= 0f)
            {
                return 0;
            }

            int itemCount = adapter.GetItemCount();
            int index = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(position, 0f) - AxisPadding) / len) * Unit, 0, itemCount - 1);

            float visibleLength = itemLength - (position - GetItemStartPosition(index));
            if (visibleLength < itemLength * 0.5f && index + Unit < itemCount)
            {
                index += Unit;
            }

            return index;
        }

        private float AxisCellLength => direction == Direction.Vertical ? cellSize.y : cellSize.x;

        private float CrossCellLength => direction == Direction.Vertical ? cellSize.x : cellSize.y;

        private float CrossSpacing => direction == Direction.Vertical ? spacing.x : spacing.y;
    }
}
