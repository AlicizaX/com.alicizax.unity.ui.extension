using System;
using UnityEngine;

namespace AlicizaX.UI
{
    [Serializable]
    public class PageLayoutManager : LinearLayoutManager
    {
        [SerializeField] private float minScale = 0.9f;
        [SerializeField] private float scaleFalloff = 0.0006f;

        public PageLayoutManager()
        {
        }

        /// <summary>
        /// 翻页布局按固定格长计算，不订阅变长协议；否则位置由固定公式给出、culling 却走偏移表，两套几何会打架。
        /// </summary>
        public override bool SupportsVariableItemLength => false;

        public override bool VariableItemLengthEnabled => false;

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

            float position = itemCount * (lineHeight + AxisSpacing) - AxisSpacing;
            position += AxisViewportLength - lineHeight;
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

        public override Vector2 CalculateContentOffset()
        {
            return Vector2.zero;
        }

        public override int GetStartIndex()
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            float len = lineHeight + AxisSpacing;
            if (itemCount <= 0 || len <= 0f)
            {
                return 0;
            }

            return Mathf.Max(0, Mathf.FloorToInt((ScrollPosition - AxisPadding) / len));
        }

        public override int GetEndIndex()
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount <= 0)
            {
                return -1;
            }

            float len = lineHeight + AxisSpacing;
            if (len <= 0f)
            {
                return itemCount - 1;
            }

            int index = Mathf.FloorToInt((ScrollPosition + AxisViewportLength - AxisPadding) / len);
            return Mathf.Min(index, itemCount - 1);
        }

        public override float GetItemStartPosition(int index)
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount <= 0)
            {
                return 0f;
            }

            index = Mathf.Clamp(index, 0, itemCount - 1);
            return AxisPadding + index * (lineHeight + AxisSpacing);
        }

        public override float GetItemLength(int index)
        {
            return Mathf.Max(lineHeight, 0f);
        }

        public override int PositionToIndex(float position)
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            float len = lineHeight + AxisSpacing;
            if (itemCount <= 0 || len <= 0f)
            {
                return 0;
            }

            return Mathf.Clamp(Mathf.RoundToInt((position - AxisPadding) / len), 0, itemCount - 1);
        }

        public override int GetSnapIndex(float position)
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            float len = lineHeight + AxisSpacing;
            if (itemCount <= 0 || len <= 0f)
            {
                return 0;
            }

            return Mathf.Clamp(Mathf.RoundToInt((position - AxisPadding) / len), 0, itemCount - 1);
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
                float scale = 1 - Mathf.Min(Mathf.Abs(viewPos) * scaleFalloff, 1f);
                scale = Mathf.Max(scale, minScale);

                viewHolder.RectTransform.localScale = Vector3.one * scale;
            }
        }
    }
}
