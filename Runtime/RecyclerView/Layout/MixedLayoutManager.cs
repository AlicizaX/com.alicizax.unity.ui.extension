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

        private float[] templateLengthsById = Array.Empty<float>();
        private byte[] templateLengthStates = Array.Empty<byte>();
        private Direction cachedTemplateDirection;
        private bool hasCachedTemplateDirection;

        public MixedLayoutManager() { }

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
            templateLengthsById = Array.Empty<float>();
            templateLengthStates = Array.Empty<byte>();
            hasCachedTemplateDirection = false;
            offsetTableDirty = true;
        }

        public override void InvalidateItemLengths()
        {
            offsetTableDirty = true;
        }

        public override bool ApplyMeasuredLength(int index, float length)
        {
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
            RefreshOffsetTable();

            float position = offsetTable.PositionAt(index) - ScrollPosition;
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
            RefreshOffsetTable();
            if (variableItemLength)
            {
                return VariableStartContentOffset();
            }

            if (offsetTable.Count <= 0)
            {
                return Vector2.zero;
            }

            float len = GetFitContentSize();
            if (direction == Direction.Vertical)
            {
                return new Vector2(0, (len - firstItemSize.y) / 2);
            }

            return new Vector2((len - firstItemSize.x) / 2, 0);
        }

        public override Vector2 CalculateViewportOffset()
        {
            RefreshOffsetTable();
            if (variableItemLength)
            {
                return Vector2.zero;
            }

            if (offsetTable.Count <= 0)
            {
                return Vector2.zero;
            }

            if (direction == Direction.Vertical)
            {
                return new Vector2(0, (viewportSize.y - firstItemSize.y) / 2);
            }

            return new Vector2((viewportSize.x - firstItemSize.x) / 2, 0);
        }

        public override int GetStartIndex()
        {
            RefreshOffsetTable();
            if (offsetTable.Count <= 0)
            {
                return 0;
            }

            int index = offsetTable.FindFirstEndingAfter(ScrollPosition);
            return index >= 0 ? index : 0;
        }

        public override int GetEndIndex()
        {
            RefreshOffsetTable();
            if (offsetTable.Count <= 0)
            {
                return -1;
            }

            float viewLength = direction == Direction.Vertical ? viewportSize.y : viewportSize.x;
            int index = offsetTable.FindFirstEndingAfter(ScrollPosition + viewLength);
            return index >= 0 ? Mathf.Min(index, offsetTable.Count - 1) : offsetTable.Count - 1;
        }

        public override float IndexToPosition(int index)
        {
            RefreshOffsetTable();
            if (offsetTable.Count <= 0)
            {
                return 0f;
            }

            float position = offsetTable.PositionAt(index);
            if (direction == Direction.Vertical)
            {
                return Mathf.Clamp(position, 0f, Mathf.Max(contentSize.y - viewportSize.y, 0f));
            }

            return Mathf.Clamp(position, 0f, Mathf.Max(contentSize.x - viewportSize.x, 0f));
        }

        public override int PositionToIndex(float position)
        {
            RefreshOffsetTable();
            if (offsetTable.Count <= 0)
            {
                return 0;
            }

            int index = offsetTable.FindFirstEndingAtOrAfter(position);
            return index >= 0 ? index : offsetTable.Count - 1;
        }

        public override float GetItemStartPosition(int index)
        {
            RefreshOffsetTable();
            return offsetTable.PositionAt(index);
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

            int index = offsetTable.FindFirstEndingAfter(Mathf.Max(position, 0f));
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

        private void RefreshOffsetTable()
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            float spacingValue = direction == Direction.Vertical ? spacing.y : spacing.x;
            if (!offsetTableDirty && offsetTable.Matches(itemCount, spacingValue, direction))
            {
                return;
            }

            offsetTable.BeginWrite(itemCount);
            firstItemSize = itemCount > 0 ? viewProvider.CalculateViewSize(0) : Vector2.zero;
            for (int i = 0; i < itemCount; i++)
            {
                float length;
                if (variableItemLength && adapter.TryGetDeclaredLength(i, out length) && length > 0f)
                {
                    offsetTable.SetLength(i, length);
                    continue;
                }

                offsetTable.SetLength(i, ResolveTemplateLength(adapter.GetTemplateId(i)));
            }

            offsetTable.Build(spacingValue, direction);
            offsetTableDirty = false;
        }

        private float ResolveTemplateLength(int templateId)
        {
            if (templateId < 0)
            {
                return 0f;
            }

            RefreshTemplateLengthCache();
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

        private void RefreshTemplateLengthCache()
        {
            int templateCount = viewProvider != null ? viewProvider.TemplateCount : 0;
            if (templateLengthsById.Length != templateCount)
            {
                templateLengthsById = templateCount > 0 ? new float[templateCount] : Array.Empty<float>();
                templateLengthStates = templateCount > 0 ? new byte[templateCount] : Array.Empty<byte>();
                hasCachedTemplateDirection = false;
            }

            if (!hasCachedTemplateDirection || cachedTemplateDirection != direction)
            {
                Array.Clear(templateLengthStates, 0, templateLengthStates.Length);
                cachedTemplateDirection = direction;
                hasCachedTemplateDirection = true;
            }
        }
    }
}
