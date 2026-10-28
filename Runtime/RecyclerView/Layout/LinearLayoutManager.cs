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
        private int appendedCount;

        public LinearLayoutManager()
        {
        }

        public bool VariableItemLength
        {
            get => variableItemLength;
            set
            {
                if (variableItemLength == value)
                {
                    return;
                }

                if (value && !SupportsVariableItemLength)
                {
                    throw new NotSupportedException($"{GetType().Name} does not support variable item length.");
                }

                variableItemLength = value;
                offsetTableDirty = true;
                appendedCount = 0;
            }
        }

        public override bool SupportsVariableItemLength => true;

        public override bool VariableItemLengthEnabled => variableItemLength;

        public override void Release()
        {
            offsetTable.Release();
            offsetTableDirty = true;
            appendedCount = 0;
        }

        public override void InvalidateItemLengths()
        {
            offsetTableDirty = true;
            appendedCount = 0;
        }

        public override void NotifyItemsAppended(int count)
        {
            if (!variableItemLength || count <= 0)
            {
                return;
            }

            appendedCount += count;
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
                appendedCount = 0;
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

            float position = itemCount * (lineHeight + AxisSpacing) - AxisSpacing;
            return direction == Direction.Vertical
                ? new Vector2(contentSize.x, position + padding.y * 2)
                : new Vector2(position + padding.x * 2, contentSize.y);
        }

        public override Vector2 CalculatePosition(int index)
        {
            float position = GetItemStartPosition(index) - ScrollPosition;
            return direction == Direction.Vertical
                ? new Vector2(0, position)
                : new Vector2(position, 0);
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
            return direction == Direction.Vertical
                ? new Vector2(0, (len - lineHeight) / 2)
                : new Vector2((len - lineHeight) / 2, 0);
        }

        public override int GetStartIndex()
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount <= 0)
            {
                return 0;
            }

            // 坐标空间包含 padding：首项从 padding 开始，因此 culling 必须先减掉它。
            float scrolled = ScrollPosition - AxisPadding;
            if (variableItemLength)
            {
                RefreshOffsetTable();
                int index = offsetTable.FindFirstEndingAfter(scrolled);
                return index >= 0 ? index : 0;
            }

            float len = lineHeight + AxisSpacing;
            if (len <= 0f)
            {
                return 0;
            }

            return Mathf.Max(0, Mathf.FloorToInt(scrolled / len));
        }

        public override int GetEndIndex()
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount <= 0)
            {
                return -1;
            }

            float scrolled = ScrollPosition + AxisViewportLength - AxisPadding;
            if (variableItemLength)
            {
                RefreshOffsetTable();
                int index = offsetTable.FindFirstEndingAfter(scrolled);
                return index >= 0 ? Mathf.Min(index, itemCount - 1) : itemCount - 1;
            }

            float len = lineHeight + AxisSpacing;
            if (len <= 0f)
            {
                return itemCount - 1;
            }

            return Mathf.Min(Mathf.FloorToInt(scrolled / len), itemCount - 1);
        }

        public override float IndexToPosition(int index)
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount <= 0 || index < 0 || index >= itemCount)
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
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount <= 0)
            {
                return 0;
            }

            float scrolled = position - AxisPadding;
            if (variableItemLength)
            {
                RefreshOffsetTable();
                int index = offsetTable.FindFirstEndingAtOrAfter(scrolled);
                return index >= 0 ? index : itemCount - 1;
            }

            float len = lineHeight + AxisSpacing;
            if (len <= 0f)
            {
                return 0;
            }

            return Mathf.Clamp(Mathf.RoundToInt(scrolled / len), 0, itemCount - 1);
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
                return offsetTable.PositionAt(index) + AxisPadding;
            }

            index = Mathf.Clamp(index, 0, itemCount - 1);
            return AxisPadding + index * (lineHeight + AxisSpacing);
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

        private float ResolveItemLength(int index)
        {
            if (adapter.TryGetDeclaredLength(index, out float length) && length > 0f)
            {
                return length;
            }

            return Mathf.Max(lineHeight, 0f);
        }

        private void RefreshOffsetTable()
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            float spacingValue = AxisSpacing;
            int revision = adapter != null ? adapter.DataRevision : 0;
            if (!offsetTableDirty && offsetTable.Matches(itemCount, spacingValue, direction, revision))
            {
                return;
            }

            // 尾部追加：只把新增段并入 Fenwick 树，避免每条消息都整表重建（O(n²)）。
            if (!offsetTableDirty &&
                appendedCount > 0 &&
                offsetTable.Count == itemCount - appendedCount &&
                offsetTable.TryBeginAppend(itemCount, out int previousCount))
            {
                for (int i = previousCount; i < itemCount; i++)
                {
                    offsetTable.AppendLength(i, ResolveItemLength(i));
                }

                offsetTable.CommitAppend(spacingValue, direction, revision);
                appendedCount = 0;
                return;
            }

            appendedCount = 0;
            offsetTable.BeginWrite(itemCount);
            for (int i = 0; i < itemCount; i++)
            {
                offsetTable.SetLength(i, ResolveItemLength(i));
            }

            offsetTable.Build(spacingValue, direction, revision);
            offsetTableDirty = false;
        }
    }
}
