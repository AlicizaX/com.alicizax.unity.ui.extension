using System;
using System.Buffers;
using UnityEngine;

namespace AlicizaX.UI
{
    [Serializable]
    public class MixedLayoutManager : LayoutManager
    {
        private float[] itemLengths = Array.Empty<float>();
        private float[] itemPositions = Array.Empty<float>();
        private int rentedArraySize = 0;
        private Vector2 firstItemSize = Vector2.zero;
        private int cachedItemCount = -1;
        private bool positionCacheDirty = true;

        public MixedLayoutManager() { }

        ~MixedLayoutManager()
        {
            ReturnRentedArrays();
        }

        private void ReturnRentedArrays()
        {
            if (rentedArraySize > 0)
            {
                ArrayPool<float>.Shared.Return(itemLengths);
                ArrayPool<float>.Shared.Return(itemPositions);
                itemLengths = Array.Empty<float>();
                itemPositions = Array.Empty<float>();
                rentedArraySize = 0;
            }
        }

        public override Vector2 CalculateContentSize()
        {
            positionCacheDirty = true;
            EnsurePositionCache();

            float totalLength = cachedItemCount > 0
                ? itemPositions[cachedItemCount - 1] + itemLengths[cachedItemCount - 1]
                : 0f;

            float paddingLength = direction == Direction.Vertical ? padding.y * 2 : padding.x * 2;
            return direction == Direction.Vertical
                ? new Vector2(contentSize.x, cachedItemCount > 0 ? totalLength + paddingLength : paddingLength)
                : new Vector2(cachedItemCount > 0 ? totalLength + paddingLength : paddingLength, contentSize.y);
        }

        public override Vector2 CalculatePosition(int index)
        {
            EnsurePositionCache();

            float position = GetItemPosition(index) - ScrollPosition;
            return direction == Direction.Vertical
                ? new Vector2(0, position + padding.y)
                : new Vector2(position + padding.x, 0);
        }

        public override Vector2 CalculateContentOffset()
        {
            EnsurePositionCache();
            if (cachedItemCount <= 0)
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
            EnsurePositionCache();
            if (cachedItemCount <= 0)
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
            EnsurePositionCache();
            if (cachedItemCount <= 0)
            {
                return 0;
            }

            int index = FindFirstItemEndingAfter(ScrollPosition);
            return index >= 0 ? index : 0;
        }

        public override int GetEndIndex()
        {
            EnsurePositionCache();
            if (cachedItemCount <= 0)
            {
                return -1;
            }

            float viewLength = direction == Direction.Vertical ? viewportSize.y : viewportSize.x;
            int index = FindFirstItemEndingAfter(ScrollPosition + viewLength);
            return index >= 0 ? Mathf.Min(index, cachedItemCount - 1) : cachedItemCount - 1;
        }

        public override float IndexToPosition(int index)
        {
            EnsurePositionCache();
            if (cachedItemCount <= 0)
            {
                return 0f;
            }

            float position = GetItemPosition(index);
            if (direction == Direction.Vertical)
            {
                return Mathf.Clamp(position, 0f, Mathf.Max(contentSize.y - viewportSize.y, 0f));
            }

            return Mathf.Clamp(position, 0f, Mathf.Max(contentSize.x - viewportSize.x, 0f));
        }

        public override int PositionToIndex(float position)
        {
            EnsurePositionCache();
            if (cachedItemCount <= 0)
            {
                return 0;
            }

            int index = FindFirstItemEndingAtOrAfter(position);
            return index >= 0 ? index : cachedItemCount - 1;
        }

        public override float GetItemStartPosition(int index)
        {
            EnsurePositionCache();
            if (cachedItemCount <= 0)
            {
                return 0f;
            }

            index = Mathf.Clamp(index, 0, cachedItemCount - 1);
            return itemPositions[index];
        }

        public override float GetItemLength(int index)
        {
            EnsurePositionCache();
            if (cachedItemCount <= 0)
            {
                return 0f;
            }

            index = Mathf.Clamp(index, 0, cachedItemCount - 1);
            float spacingLength = index < cachedItemCount - 1
                ? (direction == Direction.Vertical ? spacing.y : spacing.x)
                : 0f;
            return Mathf.Max(itemLengths[index] - spacingLength, 0f);
        }

        public override int GetSnapIndex(float position)
        {
            EnsurePositionCache();
            if (cachedItemCount <= 0)
            {
                return 0;
            }

            int index = FindFirstItemEndingAfter(Mathf.Max(position, 0f));
            if (index < 0)
            {
                return cachedItemCount - 1;
            }

            float itemLength = GetItemLength(index);
            if (itemLength <= 0f)
            {
                return index;
            }

            float visibleLength = itemLength - (position - GetItemStartPosition(index));
            if (visibleLength < itemLength * 0.5f && index < cachedItemCount - 1)
            {
                index++;
            }

            return index;
        }

        private void EnsurePositionCache()
        {
            int itemCount = adapter != null ? adapter.GetItemCount() : 0;
            if (itemCount < 0)
            {
                itemCount = 0;
            }

            if (!positionCacheDirty && cachedItemCount == itemCount)
            {
                return;
            }

            RebuildPositionCache(itemCount);
        }

        private void RebuildPositionCache(int itemCount)
        {
            // ArrayPool.Rent 返回的数组长度 >= 请求长度，rentedArraySize 记录实际容量。
            // 仅当 itemCount 超出当前容量时才归还并重新租用，缩容时复用原数组。
            if (itemCount > rentedArraySize)
            {
                ReturnRentedArrays();

                if (itemCount > 0)
                {
                    itemLengths = ArrayPool<float>.Shared.Rent(itemCount);
                    itemPositions = ArrayPool<float>.Shared.Rent(itemCount);
                    rentedArraySize = itemLengths.Length;
                }
            }

            firstItemSize = itemCount > 0 ? viewProvider.CalculateViewSize(0) : Vector2.zero;
            float position = 0f;
            for (int i = 0; i < itemCount; i++)
            {
                itemPositions[i] = position;
                itemLengths[i] = GetLength(i, itemCount);
                position += itemLengths[i];
            }

            cachedItemCount = itemCount;
            positionCacheDirty = false;
        }

        private float GetItemPosition(int index)
        {
            if (index <= 0 || cachedItemCount <= 0)
            {
                return 0f;
            }

            if (index >= cachedItemCount)
            {
                return itemPositions[cachedItemCount - 1] + itemLengths[cachedItemCount - 1];
            }

            return itemPositions[index];
        }

        private int FindFirstItemEndingAfter(float position)
        {
            int low = 0;
            int high = cachedItemCount - 1;
            int result = -1;

            while (low <= high)
            {
                int mid = low + ((high - low) / 2);
                if (GetItemEndPosition(mid) > position)
                {
                    result = mid;
                    high = mid - 1;
                }
                else
                {
                    low = mid + 1;
                }
            }

            return result;
        }

        private int FindFirstItemEndingAtOrAfter(float position)
        {
            int low = 0;
            int high = cachedItemCount - 1;
            int result = -1;

            while (low <= high)
            {
                int mid = low + ((high - low) / 2);
                if (GetItemEndPosition(mid) >= position)
                {
                    result = mid;
                    high = mid - 1;
                }
                else
                {
                    low = mid + 1;
                }
            }

            return result;
        }

        private float GetItemEndPosition(int index)
        {
            return itemPositions[index] + itemLengths[index];
        }

        private float GetLength(int index, int itemCount)
        {
            Vector2 size = viewProvider.CalculateViewSize(index);
            if (index < itemCount - 1)
            {
                size += spacing;
            }

            return direction == Direction.Vertical ? size.y : size.x;
        }
    }
}
