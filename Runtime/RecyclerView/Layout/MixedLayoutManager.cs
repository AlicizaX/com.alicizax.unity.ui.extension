using System;
using UnityEngine;

namespace AlicizaX.UI
{
    [Serializable]
    public class MixedLayoutManager : LayoutManager
    {
        [SerializeField] private bool variableItemLength;

        private readonly ItemOffsetTable offsetTable = new ItemOffsetTable();
        private Vector2 firstItemSize = Vector2.zero;
        private bool offsetTableDirty = true;
        private int appendedCount;

        private float[] templateLengthsById = Array.Empty<float>();
        private byte[] templateLengthStates = Array.Empty<byte>();
        private Direction cachedTemplateDirection;
        private bool hasCachedTemplateDirection;

        public MixedLayoutManager()
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

                variableItemLength = value;
                offsetTableDirty = true;
                appendedCount = 0;
            }
        }

        public override bool SupportsVariableItemLength => true;

        public override bool VariableItemLengthEnabled => variableItemLength;

        internal override ViewProvider ViewProvider
        {
            get => base.ViewProvider;
            set
            {
                base.ViewProvider = value;
                int templateCount = value != null ? value.TemplateCount : 0;
                templateLengthsById = templateCount > 0 ? new float[templateCount] : Array.Empty<float>();
                templateLengthStates = templateCount > 0 ? new byte[templateCount] : Array.Empty<byte>();
                hasCachedTemplateDirection = false;
            }
        }

        public override void Release()
        {
            offsetTable.Release();
            templateLengthsById = Array.Empty<float>();
            templateLengthStates = Array.Empty<byte>();
            hasCachedTemplateDirection = false;
            offsetTableDirty = true;
            appendedCount = 0;
        }

        public override void InvalidateItemLengths()
        {
            offsetTableDirty = true;
            appendedCount = 0;
            // 模板尺寸缓存必须跟偏移表一起失效：只重建表会继续读旧模板长度。
            Array.Clear(templateLengthStates, 0, templateLengthStates.Length);
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
            RefreshOffsetTable();

            float totalLength = offsetTable.TotalLength;
            float paddingLength = direction == Direction.Vertical ? padding.y * 2 : padding.x * 2;
            return direction == Direction.Vertical
                ? new Vector2(contentSize.x, offsetTable.Count > 0 ? totalLength + paddingLength : paddingLength)
                : new Vector2(offsetTable.Count > 0 ? totalLength + paddingLength : paddingLength, contentSize.y);
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

            if (offsetTable.Count <= 0)
            {
                return Vector2.zero;
            }

            float len = GetFitContentSize();
            return direction == Direction.Vertical
                ? new Vector2(0, (len - firstItemSize.y) / 2)
                : new Vector2((len - firstItemSize.x) / 2, 0);
        }

        public override int GetStartIndex()
        {
            RefreshOffsetTable();
            if (offsetTable.Count <= 0)
            {
                return 0;
            }

            int index = offsetTable.FindFirstEndingAfter(ScrollPosition - AxisPadding);
            return index >= 0 ? index : 0;
        }

        public override int GetEndIndex()
        {
            RefreshOffsetTable();
            if (offsetTable.Count <= 0)
            {
                return -1;
            }

            int index = offsetTable.FindFirstEndingAfter(ScrollPosition + AxisViewportLength - AxisPadding);
            return index >= 0 ? Mathf.Min(index, offsetTable.Count - 1) : offsetTable.Count - 1;
        }

        public override float IndexToPosition(int index)
        {
            RefreshOffsetTable();
            if (offsetTable.Count <= 0)
            {
                return 0f;
            }

            float position = GetItemStartPosition(index);
            float max = direction == Direction.Vertical
                ? Mathf.Max(contentSize.y - viewportSize.y, 0f)
                : Mathf.Max(contentSize.x - viewportSize.x, 0f);
            return Mathf.Clamp(position, 0f, max);
        }

        public override int PositionToIndex(float position)
        {
            RefreshOffsetTable();
            if (offsetTable.Count <= 0)
            {
                return 0;
            }

            int index = offsetTable.FindFirstEndingAtOrAfter(position - AxisPadding);
            return index >= 0 ? index : offsetTable.Count - 1;
        }

        public override float GetItemStartPosition(int index)
        {
            RefreshOffsetTable();
            return offsetTable.PositionAt(index) + AxisPadding;
        }

        public override float GetItemLength(int index)
        {
            RefreshOffsetTable();
            return offsetTable.LengthAt(index);
        }

        public override int GetSnapIndex(float position)
        {
            RefreshOffsetTable();
            if (offsetTable.Count <= 0)
            {
                return 0;
            }

            int index = offsetTable.FindFirstEndingAfter(Mathf.Max(position, 0f) - AxisPadding);
            if (index < 0)
            {
                return offsetTable.Count - 1;
            }

            float itemLength = GetItemLength(index);
            if (itemLength <= 0f)
            {
                return index;
            }

            float visibleLength = itemLength - (position - GetItemStartPosition(index));
            if (visibleLength < itemLength * 0.5f && index < offsetTable.Count - 1)
            {
                index++;
            }

            return index;
        }

        private float ResolveItemLength(int index)
        {
            if (variableItemLength && adapter.TryGetDeclaredLength(index, out float declared) && declared > 0f)
            {
                return declared;
            }

            return ResolveTemplateLength(adapter.GetTemplateId(index));
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
            firstItemSize = itemCount > 0 ? viewProvider.CalculateViewSize(0) : Vector2.zero;
            for (int i = 0; i < itemCount; i++)
            {
                offsetTable.SetLength(i, ResolveItemLength(i));
            }

            offsetTable.Build(spacingValue, direction, revision);
            offsetTableDirty = false;
        }

        private float ResolveTemplateLength(int templateId)
        {
            if (templateId < 0)
            {
                return 0f;
            }

            // 模板长度是轴相关的：主轴变化后必须重新取样。
            if (!hasCachedTemplateDirection || cachedTemplateDirection != direction)
            {
                Array.Clear(templateLengthStates, 0, templateLengthStates.Length);
                cachedTemplateDirection = direction;
                hasCachedTemplateDirection = true;
            }

            if (templateId >= templateLengthsById.Length)
            {
                return 0f;
            }

            if (templateLengthStates[templateId] != 0)
            {
                return templateLengthsById[templateId];
            }

            float length = viewProvider != null ? viewProvider.GetTemplateLength(templateId) : 0f;
            templateLengthsById[templateId] = length;
            templateLengthStates[templateId] = 1;
            return length;
        }
    }
}
