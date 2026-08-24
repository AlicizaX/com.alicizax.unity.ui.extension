using System;
using System.Buffers;
using UnityEngine;

namespace AlicizaX.UI
{
    internal sealed class ItemOffsetTable
    {
        private float[] lengths = Array.Empty<float>();
        private float[] positions = Array.Empty<float>();
        private int rentedSize;
        private int count = -1;
        private float spacing;
        private Direction direction;
        private bool built;

        public int Count => count;

        public float TotalLength => count > 0 ? positions[count - 1] + lengths[count - 1] : 0f;

        public bool Matches(int itemCount, float spacingValue, Direction dir)
        {
            return built &&
                   count == itemCount &&
                   direction == dir &&
                   Mathf.Approximately(spacing, spacingValue);
        }

        public void Release()
        {
            if (rentedSize > 0)
            {
                ArrayPool<float>.Shared.Return(lengths);
                ArrayPool<float>.Shared.Return(positions);
                lengths = Array.Empty<float>();
                positions = Array.Empty<float>();
                rentedSize = 0;
            }

            count = -1;
            built = false;
        }

        public void BeginWrite(int itemCount)
        {
            if (itemCount < 0)
            {
                itemCount = 0;
            }

            if (itemCount > rentedSize || (rentedSize > 64 && itemCount * 4 < rentedSize))
            {
                Release();
                if (itemCount > 0)
                {
                    lengths = ArrayPool<float>.Shared.Rent(itemCount);
                    positions = ArrayPool<float>.Shared.Rent(itemCount);
                    rentedSize = lengths.Length;
                }
            }

            count = itemCount;
            built = false;
        }

        public void SetLength(int index, float length)
        {
            lengths[index] = Mathf.Max(length, 0f);
        }

        public void Build(float spacingValue, Direction dir)
        {
            spacing = spacingValue;
            direction = dir;
            float position = 0f;
            int last = count - 1;
            for (int i = 0; i < count; i++)
            {
                positions[i] = position;
                position += lengths[i];
                if (i < last)
                {
                    position += spacingValue;
                }
            }

            built = true;
        }

        public bool ReplaceLength(int index, float length)
        {
            if (!built || index < 0 || index >= count)
            {
                return false;
            }

            length = Mathf.Max(length, 0f);
            float delta = length - lengths[index];
            if (Mathf.Approximately(delta, 0f))
            {
                return false;
            }

            lengths[index] = length;
            for (int i = index + 1; i < count; i++)
            {
                positions[i] += delta;
            }

            return true;
        }

        public float LengthAt(int index)
        {
            if (!built || count <= 0)
            {
                return 0f;
            }

            return lengths[Mathf.Clamp(index, 0, count - 1)];
        }

        public float PositionAt(int index)
        {
            if (!built || count <= 0)
            {
                return 0f;
            }

            if (index <= 0)
            {
                return 0f;
            }

            if (index >= count)
            {
                return TotalLength;
            }

            return positions[index];
        }

        public int FindFirstEndingAfter(float position)
        {
            return FindFirst(position, false);
        }

        public int FindFirstEndingAtOrAfter(float position)
        {
            return FindFirst(position, true);
        }

        private int FindFirst(float position, bool inclusive)
        {
            if (!built || count <= 0)
            {
                return -1;
            }

            int low = 0;
            int high = count - 1;
            int result = -1;
            while (low <= high)
            {
                int mid = low + ((high - low) / 2);
                float end = ItemEnd(mid);
                bool match = inclusive ? end >= position : end > position;
                if (match)
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

        private float ItemEnd(int index)
        {
            float end = positions[index] + lengths[index];
            if (index < count - 1)
            {
                end += spacing;
            }

            return end;
        }
    }
}
