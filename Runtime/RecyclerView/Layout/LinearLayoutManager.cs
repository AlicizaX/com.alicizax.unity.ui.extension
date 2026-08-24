using System;
using UnityEngine;

namespace AlicizaX.UI
{
    [Serializable]
    public class LinearLayoutManager : LayoutManager
    {
        [SerializeField] private bool variableItemLength;

        protected float lineHeight;
        private readonly ItemOffsetTable offsetTable = new ItemOffsetTable();
        private bool offsetTableDirty = true;

        public LinearLayoutManager() { }

        public bool VariableItemLength
        {
            get => variableItemLength;
            set
            {
                if (variableItemLength == value)
                {
                    return;
                }

                variableItemLength = value;
                offsetTableDirty = true;
            }
        }

        public override bool SupportsVariableItemLength => true;

        public override void Release()
        {
            offsetTable.Release();
            offsetTableDirty = true;
        }

        public override void InvalidateItemLengths()
        {
            offsetTableDirty = true;
        }

        public override bool ApplyMeasuredLength(int index, float length)
        {
            if (!variableItemLength)
            {
                return false;
            }

            RefreshOffsetTable();
            return offsetTable.ReplaceLength(index, length);
        }

        public override Vector2 CalculateContentSize()
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount <= 0)
            {
                lineHeight = 0f;
                offsetTable.Release();
                offsetTableDirty = true;
                return direction == Direction.Vertical
                    ? new Vector2(contentSize.x, padding.y * 2)
                    : new Vector2(padding.x * 2, contentSize.y);
            }

            Vector2 size = viewProvider.CalculateViewSize(0);
            lineHeight = direction == Direction.Vertical ? size.y : size.x;
            if (variableItemLength)
            {
                RefreshOffsetTable();
                float total = offsetTable.TotalLength;
                return direction == Direction.Vertical
                    ? new Vector2(contentSize.x, total + padding.y * 2)
                    : new Vector2(total + padding.x * 2, contentSize.y);
            }

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
            float position = GetItemStartPosition(index) - ScrollPosition;
            return direction == Direction.Vertical
                ? new Vector2(0, position + padding.y)
                : new Vector2(position + padding.x, 0);
        }

        public override void Layout(ViewHolder viewHolder, int index)
        {
            if (variableItemLength)
            {
                LayoutFromItemStart(viewHolder, index);
                return;
            }

            base.Layout(viewHolder, index);
        }

        public override Vector2 CalculateContentOffset()
        {
            if (variableItemLength)
            {
                return VariableStartContentOffset();
            }

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
            if (variableItemLength)
            {
                return Vector2.zero;
            }

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
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount <= 0)
            {
                return 0;
            }

            if (variableItemLength)
            {
                RefreshOffsetTable();
                int index = offsetTable.FindFirstEndingAfter(ScrollPosition);
                return index >= 0 ? index : 0;
            }

            float len = direction == Direction.Vertical ? lineHeight + spacing.y : lineHeight + spacing.x;
            if (len <= 0f)
            {
                return 0;
            }

            return Mathf.Max(0, Mathf.FloorToInt(ScrollPosition / len));
        }

        public override int GetEndIndex()
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount <= 0)
            {
                return -1;
            }

            float viewLength = direction == Direction.Vertical ? viewportSize.y : viewportSize.x;
            if (variableItemLength)
            {
                RefreshOffsetTable();
                int index = offsetTable.FindFirstEndingAfter(ScrollPosition + viewLength);
                return index >= 0 ? Mathf.Min(index, itemCount - 1) : itemCount - 1;
            }

            float len = direction == Direction.Vertical ? lineHeight + spacing.y : lineHeight + spacing.x;
            if (len <= 0f)
            {
                return itemCount - 1;
            }

            return Mathf.Min(Mathf.FloorToInt((ScrollPosition + viewLength) / len), itemCount - 1);
        }

        public override float IndexToPosition(int index)
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount <= 0 || index < 0 || index >= itemCount)
            {
                return 0f;
            }

            float start = GetItemStartPosition(index);
            if (direction == Direction.Vertical)
            {
                return Mathf.Clamp(start, 0f, Mathf.Max(contentSize.y - viewportSize.y, 0f));
            }

            return Mathf.Clamp(start, 0f, Mathf.Max(contentSize.x - viewportSize.x, 0f));
        }

        public override int PositionToIndex(float position)
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount <= 0)
            {
                return 0;
            }

            if (variableItemLength)
            {
                RefreshOffsetTable();
                int index = offsetTable.FindFirstEndingAtOrAfter(position);
                return index >= 0 ? index : itemCount - 1;
            }

            float len = direction == Direction.Vertical ? lineHeight + spacing.y : lineHeight + spacing.x;
            if (len <= 0f)
            {
                return 0;
            }

            return Mathf.Clamp(Mathf.RoundToInt(position / len), 0, itemCount - 1);
        }

        public override float GetItemStartPosition(int index)
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount <= 0)
            {
                return 0f;
            }

            if (variableItemLength)
            {
                RefreshOffsetTable();
                return offsetTable.PositionAt(index);
            }

            index = Mathf.Clamp(index, 0, itemCount - 1);
            float len = direction == Direction.Vertical ? lineHeight + spacing.y : lineHeight + spacing.x;
            return index * len;
        }

        public override float GetItemLength(int index)
        {
            if (variableItemLength)
            {
                RefreshOffsetTable();
                return offsetTable.LengthAt(index);
            }

            return Mathf.Max(lineHeight, 0f);
        }

        public override int GetSnapIndex(float position)
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount <= 0)
            {
                return 0;
            }

            int index = PositionToIndex(Mathf.Max(position, 0f));
            float itemLength = GetItemLength(index);
            if (itemLength <= 0f)
            {
                return index;
            }

            float visibleLength = itemLength - (position - GetItemStartPosition(index));
            if (visibleLength < itemLength * 0.5f && index < itemCount - 1)
            {
                index++;
            }

            return index;
        }

        private void RefreshOffsetTable()
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            float spacingValue = direction == Direction.Vertical ? spacing.y : spacing.x;
            if (!offsetTableDirty && offsetTable.Matches(itemCount, spacingValue, direction))
            {
                return;
            }

            offsetTable.BeginWrite(itemCount);
            for (int i = 0; i < itemCount; i++)
            {
                if (!adapter.TryGetDeclaredLength(i, out float length) || length <= 0f)
                {
                    length = lineHeight;
                }

                offsetTable.SetLength(i, length);
            }

            offsetTable.Build(spacingValue, direction);
            offsetTableDirty = false;
        }
    }
}
