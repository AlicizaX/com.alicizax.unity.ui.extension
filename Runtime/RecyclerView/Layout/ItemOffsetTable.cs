using System;
using System.Buffers;
using UnityEngine;

namespace AlicizaX.UI
{
    /// <summary>
    /// 变长项的"长度 + 间距"前缀和索引。
    ///
    /// 用 Fenwick 树保存长度前缀和：任意位置改长度是 O(log n)，不再对后缀整体平移；
    /// 位置查询是 O(log n)，区间定位是 O(log² n)（二分 + 前缀和）。定长列表不建表。
    /// 所有权：数组由 ArrayPool 租借，<see cref="Release"/> 归还；容量只增不减，避免在容量边界反复租还。
    /// </summary>
    internal sealed class ItemOffsetTable
    {
        private const int MinCapacity = 32;

        private float[] lengths = Array.Empty<float>();
        private float[] tree = Array.Empty<float>();
        private int rentedSize;
        private int count = -1;
        private float spacing;
        private Direction direction;
        private int revision = -1;
        private bool built;

        /// <summary>测试钩子：累计写入 Fenwick 树的次数，用于断言更新复杂度。</summary>
        internal long TreeWriteCount { get; private set; }

        public int Count => count;

        public float TotalLength => count > 0 ? Prefix(count) + spacing * (count - 1) : 0f;

        public bool Matches(int itemCount, float spacingValue, Direction dir, int dataRevision)
        {
            return built &&
                   count == itemCount &&
                   direction == dir &&
                   revision == dataRevision &&
                   Mathf.Approximately(spacing, spacingValue);
        }

        public void Release()
        {
            if (rentedSize > 0)
            {
                ArrayPool<float>.Shared.Return(lengths);
                ArrayPool<float>.Shared.Return(tree);
                lengths = Array.Empty<float>();
                tree = Array.Empty<float>();
                rentedSize = 0;
            }

            count = -1;
            built = false;
            revision = -1;
        }

        public void BeginWrite(int itemCount)
        {
            EnsureCapacity(Mathf.Max(itemCount, 0));
            count = itemCount;
            built = false;
        }

        public void SetLength(int index, float length)
        {
            lengths[index] = Mathf.Max(length, 0f);
        }

        public void Build(float spacingValue, Direction dir, int dataRevision)
        {
            spacing = spacingValue;
            direction = dir;
            revision = dataRevision;

            // 树的有效范围是整块租借容量，而不是当前 count：
            // 追加会让 count 逐步逼近容量边界，祖先节点必须在这些位置提前维护好，
            // 否则 Prefix(count) 会在跨过 2 的幂时读到空节点（表现为内容高度骤降、列表滚不动）。
            int capacity = rentedSize;
            if (count <= 0 || capacity <= 0)
            {
                // 空表：不触碰租借数组（此时数组可能还没分配）。
                built = true;
                return;
            }

            if (count < capacity)
            {
                Array.Clear(lengths, count, capacity - count);
            }

            RebuildTree(count, capacity);
            TreeWriteCount += count;
            built = true;
        }

        /// <summary>
        /// 用 <c>lengths[0..counted)</c> 线性重建整棵 Fenwick 树。
        /// 关键点：第二趟必须遍历<b>整块容量</b>，而不是只到 counted。
        /// 只遍历 counted 时，末端元素的祖先聚合停在 count 附近（例如 count=3 时 tree[4] 有值、tree[8] 没有），
        /// 追加到 8 条后 Prefix(8) 只统计到后 5 条，总长凭空变短（列表内容塌陷、滚不动）。
        /// </summary>
        private void RebuildTree(int counted, int capacity)
        {
            if (capacity <= 0 || tree.Length <= capacity)
            {
                return;
            }

            Array.Clear(tree, 0, capacity + 1);
            int limit = Mathf.Min(counted, capacity);
            for (int i = 1; i <= limit; i++)
            {
                tree[i] += lengths[i - 1];
            }

            for (int i = 1; i <= capacity; i++)
            {
                int parent = i + (i & -i);
                if (parent <= capacity)
                {
                    tree[parent] += tree[i];
                }
            }
        }

        /// <summary>
        /// 开始追加段。<paramref name="previousCount"/> 之后的位置由 <see cref="AppendLength"/> 逐项并入树。
        /// 调用方必须保证 [0, previousCount) 的长度没有变化（只有尾部追加时才成立）。
        /// </summary>
        public bool TryBeginAppend(int itemCount, out int previousCount)
        {
            previousCount = count;
            if (!built || count < 0 || itemCount < count)
            {
                return false;
            }

            EnsureCapacity(itemCount);
            count = itemCount;
            return true;
        }

        public void AppendLength(int index, float length)
        {
            Debug.Assert(index >= 0 && index < count, "ItemOffsetTable.AppendLength: index out of range.");
            float value = Mathf.Max(length, 0f);
            lengths[index] = value;
            Add(index + 1, value);
        }

        public void CommitAppend(float spacingValue, Direction dir, int dataRevision)
        {
            spacing = spacingValue;
            direction = dir;
            revision = dataRevision;
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
            Add(index + 1, delta);
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

            return Prefix(index) + spacing * index;
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
                int mid = low + ((high - low) >> 1);
                // end(mid) = start(mid) + length(mid) = prefix(mid + 1) + spacing * mid
                float end = Prefix(mid + 1) + spacing * mid;
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

        private float Prefix(int lengthCount)
        {
            float sum = 0f;
            for (int i = lengthCount; i > 0; i -= i & -i)
            {
                sum += tree[i];
            }

            return sum;
        }

        private void Add(int oneBasedIndex, float delta)
        {
            if (oneBasedIndex <= 0)
            {
                return;
            }

            // 上界是容量而不是 count：count 之后才用到的祖先节点也要同步累加，追加才不会丢前缀和。
            int capacity = rentedSize;
            for (int i = oneBasedIndex; i <= capacity; i += i & -i)
            {
                tree[i] += delta;
                TreeWriteCount++;
            }
        }

        private void EnsureCapacity(int itemCount)
        {
            if (itemCount <= rentedSize)
            {
                return;
            }

            int capacity = rentedSize > 0 ? rentedSize : MinCapacity;
            while (capacity < itemCount)
            {
                capacity <<= 1;
            }

            float[] newLengths = ArrayPool<float>.Shared.Rent(capacity);
            float[] newTree = ArrayPool<float>.Shared.Rent(capacity + 1);

            if (rentedSize > 0)
            {
                Array.Copy(lengths, newLengths, Mathf.Min(rentedSize, newLengths.Length));
                ArrayPool<float>.Shared.Return(lengths);
                ArrayPool<float>.Shared.Return(tree);
            }

            lengths = newLengths;
            tree = newTree;
            rentedSize = capacity;

            // 树不能直接拷贝：新容量上的祖先节点在原数组里不存在，旧元素的贡献永久丢失
            // （容量 32→64 后 tree[64] 只会收到 33..64 的增量，0..31 的和丢掉，Prefix 腰斩）。
            // 换到新数组后按整块新容量重建，保证 Prefix 与逐项长度始终一致。
            RebuildTree(count, capacity);
        }
    }
}
