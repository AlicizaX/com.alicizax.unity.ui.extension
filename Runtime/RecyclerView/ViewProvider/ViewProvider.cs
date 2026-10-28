using System.Collections.Generic;
using Cysharp.Text;
using UnityEngine;

namespace AlicizaX.UI
{
    /// <summary>
    /// 可见 ViewHolder 的分配、回收与查询。
    /// 可见项数量通常很小，用稠密 List + 线性查找，避免 ring / 手写 hash。
    /// </summary>
    internal sealed class ViewProvider
    {
        private readonly List<ViewHolder> visibleHolders = new List<ViewHolder>(16);
        private ViewHolder[] removeBuffer = new ViewHolder[4];
        private int[] removeSlots = new int[4];
        private readonly ViewHolderPool pool;
        private readonly int[] warmCountsByTemplateId;
        private int lastWarmSignature;
        private bool hasWarmSignature;
        private bool templateErrorLogged;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private string cachedPoolStats;
        private int cachedPoolStatsStamp = -1;
#endif

        public ViewProvider(RecyclerView recyclerView, ViewHolder[] templates)
        {
            this.recyclerView = recyclerView;
            this.templates = templates;
            warmCountsByTemplateId = new int[templates.Length];
            pool = new ViewHolderPool(new ViewHolderFactory(templates, recyclerView.Content), templates.Length);
        }

        private readonly RecyclerView recyclerView;
        private readonly ViewHolder[] templates;

        internal IAdapter Adapter { get; set; }

        internal LayoutManager LayoutManager { get; set; }

        public int VisibleCount => visibleHolders.Count;

        public int TemplateCount => templates.Length;

        public ViewHolder GetVisibleViewHolder(int index)
        {
            return index >= 0 && index < visibleHolders.Count ? visibleHolders[index] : null;
        }

        public ViewHolder GetTemplate(int templateId)
        {
            return IsValidTemplateId(templateId) ? templates[templateId] : null;
        }

        /// <summary>
        /// 对象池统计。属性在编辑器里会被 Inspector 每帧重绘，因此缓存字符串。
        /// </summary>
        public string PoolStats
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                int stamp = pool.HitCount + pool.MissCount + pool.DestroyCount;
                if (cachedPoolStats == null || cachedPoolStatsStamp != stamp)
                {
                    cachedPoolStatsStamp = stamp;
                    cachedPoolStats = BuildPoolStats();
                }

                return cachedPoolStats;
#else
                return string.Empty;
#endif
            }
        }

        internal void CreateViewHolder(int index)
        {
            int unit = Mathf.Max(1, LayoutManager.Unit);
            int itemCount = Adapter.GetItemCount();
            bool virtualRange = LayoutManager.UsesVirtualLayoutRange;

            for (int i = index; i < index + unit; i++)
            {
                // 最后一个 group 可能越过 itemCount，这一处边界就是唯一需要判断的地方。
                if (!virtualRange && i >= itemCount)
                {
                    break;
                }

                int dataIndex = LayoutManager.GetDataIndex(i);
                int templateId = Adapter.GetTemplateId(dataIndex);
                ViewHolder viewHolder = Allocate(templateId);
                if (viewHolder == null)
                {
                    continue;
                }

                viewHolder.SetTemplate(templateId, i, dataIndex, recyclerView);
                visibleHolders.Add(viewHolder);

                LayoutManager.Layout(viewHolder, i);
                Adapter.OnBindViewHolder(viewHolder, dataIndex);
                recyclerView.MeasureBoundHolder(viewHolder);
            }
        }

        internal bool TryReuseVisibleRange(int targetStart, int targetEnd)
        {
            if (visibleHolders.Count <= 0 || targetEnd < targetStart)
            {
                return false;
            }

            int unit = Mathf.Max(1, LayoutManager.Unit);
            int slot = 0;
            for (int groupIndex = targetStart; groupIndex <= targetEnd; groupIndex += unit)
            {
                for (int layoutIndex = groupIndex; layoutIndex < groupIndex + unit; layoutIndex++)
                {
                    if (!LayoutManager.UsesVirtualLayoutRange && layoutIndex >= Adapter.GetItemCount())
                    {
                        break;
                    }

                    if (slot >= visibleHolders.Count)
                    {
                        return false;
                    }

                    ViewHolder viewHolder = visibleHolders[slot];
                    if (viewHolder == null)
                    {
                        return false;
                    }

                    if (viewHolder.TemplateId != Adapter.GetTemplateId(LayoutManager.GetDataIndex(layoutIndex)))
                    {
                        return false;
                    }

                    slot++;
                }
            }

            if (slot != visibleHolders.Count)
            {
                return false;
            }

            slot = 0;
            for (int groupIndex = targetStart; groupIndex <= targetEnd; groupIndex += unit)
            {
                for (int layoutIndex = groupIndex; layoutIndex < groupIndex + unit; layoutIndex++)
                {
                    if (!LayoutManager.UsesVirtualLayoutRange && layoutIndex >= Adapter.GetItemCount())
                    {
                        break;
                    }

                    ViewHolder viewHolder = visibleHolders[slot];
                    int dataIndex = LayoutManager.GetDataIndex(layoutIndex);
                    viewHolder.SetTemplate(viewHolder.TemplateId, layoutIndex, dataIndex, recyclerView);
                    LayoutManager.Layout(viewHolder, layoutIndex);
                    Adapter.OnBindViewHolder(viewHolder, dataIndex);
                    recyclerView.MeasureBoundHolder(viewHolder);
                    slot++;
                }
            }

            return true;
        }

        /// <summary>
        /// 回收 layoutIndex 落在 [index, index + unit) 的可见项。单趟定位 + 由高到低 swap-remove，
        /// 不再对同一项做第二次线性查找。
        /// </summary>
        internal void RemoveViewHolder(int index)
        {
            int unit = Mathf.Max(1, LayoutManager.Unit);
            EnsureRemoveBufferCapacity(unit);

            int removeCount = 0;
            for (int slot = 0; slot < visibleHolders.Count; slot++)
            {
                ViewHolder holder = visibleHolders[slot];
                if (holder == null)
                {
                    continue;
                }

                int holderIndex = holder.Index;
                if (holderIndex < index || holderIndex >= index + unit)
                {
                    continue;
                }

                removeBuffer[removeCount] = holder;
                removeSlots[removeCount] = slot;
                removeCount++;
            }

            for (int i = removeCount - 1; i >= 0; i--)
            {
                ViewHolder holder = removeBuffer[i];
                removeBuffer[i] = null;
                int templateId = holder.TemplateId;
                RemoveVisibleSlot(removeSlots[i]);
                holder.OnRecycled();
                pool.Free(templateId, holder);
            }
        }

        public ViewHolder GetViewHolder(int layoutIndex)
        {
            for (int i = 0; i < visibleHolders.Count; i++)
            {
                ViewHolder holder = visibleHolders[i];
                if (holder != null && holder.Index == layoutIndex)
                {
                    return holder;
                }
            }

            return null;
        }

        /// <summary>
        /// 当前可见项的最小/最大数据索引。可见项顺序不保证有序，所以这里只看一次。
        /// </summary>
        internal bool TryGetVisibleDataRange(out int startDataIndex, out int endDataIndex)
        {
            startDataIndex = -1;
            endDataIndex = -1;
            for (int i = 0; i < visibleHolders.Count; i++)
            {
                ViewHolder holder = visibleHolders[i];
                if (holder == null || holder.DataIndex < 0)
                {
                    continue;
                }

                if (startDataIndex < 0 || holder.DataIndex < startDataIndex)
                {
                    startDataIndex = holder.DataIndex;
                }

                if (holder.DataIndex > endDataIndex)
                {
                    endDataIndex = holder.DataIndex;
                }
            }

            return startDataIndex >= 0;
        }

        internal ViewHolder FindByDataIndex(int dataIndex)
        {
            for (int i = 0; i < visibleHolders.Count; i++)
            {
                ViewHolder holder = visibleHolders[i];
                if (holder != null && holder.DataIndex == dataIndex)
                {
                    return holder;
                }
            }

            return null;
        }

        /// <summary>
        /// 布局索引最小的可见项（锚点计算用）。
        /// </summary>
        internal ViewHolder FindFirstVisible()
        {
            ViewHolder first = null;
            for (int i = 0; i < visibleHolders.Count; i++)
            {
                ViewHolder holder = visibleHolders[i];
                if (holder == null || holder.DataIndex < 0)
                {
                    continue;
                }

                if (first == null || holder.Index < first.Index)
                {
                    first = holder;
                }
            }

            return first;
        }

        internal int RebindVisibleDataIndex(int dataIndex)
        {
            return RebindVisibleDataRange(dataIndex, 1);
        }

        internal int RebindVisibleDataRange(int startDataIndex, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            int endDataIndex = startDataIndex + count;
            int rebound = 0;
            for (int i = 0; i < visibleHolders.Count; i++)
            {
                ViewHolder holder = visibleHolders[i];
                if (holder == null || holder.DataIndex < startDataIndex || holder.DataIndex >= endDataIndex)
                {
                    continue;
                }

                Adapter.OnBindViewHolder(holder, holder.DataIndex);
                recyclerView.MeasureBoundHolder(holder);
                rebound++;
            }

            return rebound;
        }

        internal int ApplyVisibleSelection(int dataIndex, bool selected)
        {
            int applied = 0;
            for (int i = 0; i < visibleHolders.Count; i++)
            {
                ViewHolder holder = visibleHolders[i];
                if (holder == null || holder.DataIndex != dataIndex)
                {
                    continue;
                }

                holder.ApplySelection(selected);
                applied++;
            }

            return applied;
        }

        /// <summary>
        /// 将 layoutIndex/dataIndex &gt;= fromLayoutIndex 的可见项整体平移 delta。
        /// 仅用于非 loop、layoutIndex 与 dataIndex 一致的结构增量。
        /// </summary>
        internal void ShiftIndexesFrom(int fromLayoutIndex, int delta)
        {
            if (delta == 0)
            {
                return;
            }

            for (int i = 0; i < visibleHolders.Count; i++)
            {
                ViewHolder holder = visibleHolders[i];
                if (holder == null || holder.Index < fromLayoutIndex)
                {
                    continue;
                }

                holder.ShiftIndexes(delta);
            }
        }

        /// <summary>
        /// 回收 layoutIndex 落在 [fromInclusive, toExclusive) 的可见项。
        /// </summary>
        internal int RecycleLayoutRange(int fromInclusive, int toExclusive)
        {
            if (toExclusive <= fromInclusive || visibleHolders.Count == 0)
            {
                return 0;
            }

            int recycled = 0;
            for (int i = visibleHolders.Count - 1; i >= 0; i--)
            {
                ViewHolder holder = visibleHolders[i];
                if (holder == null || holder.Index < fromInclusive || holder.Index >= toExclusive)
                {
                    continue;
                }

                int templateId = holder.TemplateId;
                visibleHolders.RemoveAt(i);
                holder.OnRecycled();
                pool.Free(templateId, holder);
                recycled++;
            }

            return recycled;
        }

        internal void Clear()
        {
            for (int i = visibleHolders.Count - 1; i >= 0; i--)
            {
                ViewHolder viewHolder = visibleHolders[i];
                if (viewHolder == null)
                {
                    continue;
                }

                int templateId = viewHolder.TemplateId;
                visibleHolders.RemoveAt(i);
                viewHolder.OnRecycled();
                pool.Free(templateId, viewHolder);
            }

            visibleHolders.Clear();
        }

        public Vector2 CalculateViewSize(int index)
        {
            ViewHolder template = GetTemplate(Adapter != null ? Adapter.GetTemplateId(index) : 0);
            if (template == null)
            {
                return Vector2.zero;
            }

            Vector2 size = template.RectTransform.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                Vector2 sizeDelta = template.RectTransform.sizeDelta;
                if (size.x <= 0f) size.x = sizeDelta.x;
                if (size.y <= 0f) size.y = sizeDelta.y;
            }

            return size;
        }

        public float GetTemplateLength(int templateId)
        {
            ViewHolder template = GetTemplate(templateId);
            return template != null ? template.AxisLength(LayoutManager.Direction) : 0f;
        }

        public int GetItemCount()
        {
            return Adapter == null ? 0 : Adapter.GetItemCount();
        }

        /// <summary>
        /// 按可见窗口预热对象池。签名不变时直接返回：布局刷新不能反复重算预热分布。
        /// </summary>
        internal void PreparePool()
        {
            if (Adapter == null || LayoutManager == null)
            {
                return;
            }

            int start = LayoutManager.GetStartIndex();
            int end = Mathf.Max(start, LayoutManager.GetEndIndex());
            int warmCount = GetRecommendedWarmCount(start, end);
            if (warmCount <= 0)
            {
                return;
            }

            PrepareVisibleStorage(warmCount);

            int signature = ComputeWarmSignature(start, end, warmCount);
            if (hasWarmSignature && signature == lastWarmSignature)
            {
                return;
            }

            lastWarmSignature = signature;
            hasWarmSignature = true;

            int itemCount = Adapter.GetItemCount();
            int lastIndex = LayoutManager.UsesVirtualLayoutRange
                ? start + warmCount - 1
                : Mathf.Min(itemCount - 1, start + warmCount - 1);

            System.Array.Clear(warmCountsByTemplateId, 0, warmCountsByTemplateId.Length);
            for (int index = start; index <= lastIndex; index++)
            {
                int templateId = Adapter.GetTemplateId(LayoutManager.GetDataIndex(index));
                if (IsValidTemplateId(templateId))
                {
                    warmCountsByTemplateId[templateId]++;
                }
            }

            int buffer = Mathf.Max(1, LayoutManager.Unit);
            for (int templateId = 0; templateId < warmCountsByTemplateId.Length; templateId++)
            {
                int count = warmCountsByTemplateId[templateId];
                if (count <= 0)
                {
                    continue;
                }

                int targetCount = count + buffer;
                pool.EnsureCapacity(templateId, targetCount);
                pool.Warm(templateId, targetCount);
            }
        }

        internal void TrimInactive()
        {
            pool.TrimInactive();
        }

        internal void Dispose()
        {
            Clear();
            pool.Dispose();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        internal void ValidateInvariants()
        {
            for (int i = 0; i < visibleHolders.Count; i++)
            {
                if (visibleHolders[i] == null)
                {
                    Log.Error("ViewProvider invariant failed: visible holder is null.");
                    return;
                }
            }
        }

        private ViewHolder Allocate(int templateId)
        {
            if (!IsValidTemplateId(templateId))
            {
                if (!templateErrorLogged)
                {
                    templateErrorLogged = true;
                    Log.Error("ViewProvider: adapter returned an invalid template id.");
                }

                return null;
            }

            ViewHolder viewHolder = pool.Allocate(templateId);
            viewHolder?.SetPooledVisible(true);
            return viewHolder;
        }

        private int GetRecommendedWarmCount(int start, int end)
        {
            int itemCount = Adapter.GetItemCount();
            if (itemCount <= 0)
            {
                return 0;
            }

            if (!LayoutManager.UsesVirtualLayoutRange)
            {
                start = Mathf.Max(0, start);
            }

            int visibleCount = end - start + 1;
            int unit = Mathf.Max(1, LayoutManager.Unit);
            // Loop 虚拟 itemCount 可能很大：warm 只按可见窗口，并给硬上限
            int warm = visibleCount + unit * 2;
            if (warm > 128)
            {
                warm = 128;
            }

            return Mathf.Min(itemCount, warm);
        }

        private int ComputeWarmSignature(int start, int end, int warmCount)
        {
            unchecked
            {
                int hash = start;
                hash = hash * 397 ^ end;
                hash = hash * 397 ^ warmCount;
                hash = hash * 397 ^ LayoutManager.Unit;
                hash = hash * 397 ^ Adapter.GetItemCount();
                hash = hash * 397 ^ Adapter.DataRevision;
                return hash;
            }
        }

        private string BuildPoolStats()
        {
            int templateCount = Mathf.Min(templates.Length, pool.TemplateCapacity);
            int active = 0;
            int peak = 0;
            int capacity = 0;
            for (int templateId = 0; templateId < templateCount; templateId++)
            {
                active += pool.GetActiveCount(templateId);
                peak += pool.GetPeakActiveCount(templateId);
                capacity += pool.GetMaxSize(templateId);
            }

            return ZString.Format(
                "hits={0}, misses={1}, destroys={2}, active={3}, peakActive={4}, capacity={5}",
                pool.HitCount,
                pool.MissCount,
                pool.DestroyCount,
                active,
                peak,
                capacity);
        }

        private void PrepareVisibleStorage(int warmCount)
        {
            int required = Mathf.Max(Mathf.Max(1, LayoutManager != null ? LayoutManager.Unit : 1), warmCount);
            if (visibleHolders.Capacity < required)
            {
                visibleHolders.Capacity = required;
            }

            if (removeBuffer.Length < required)
            {
                removeBuffer = new ViewHolder[required];
                removeSlots = new int[required];
            }
        }

        private void RemoveVisibleSlot(int slot)
        {
            int last = visibleHolders.Count - 1;
            if (slot != last)
            {
                visibleHolders[slot] = visibleHolders[last];
            }

            visibleHolders.RemoveAt(last);
        }

        private void EnsureRemoveBufferCapacity(int required)
        {
            if (required <= removeBuffer.Length)
            {
                return;
            }

            int capacity = removeBuffer.Length;
            while (capacity < required)
            {
                capacity <<= 1;
            }

            removeBuffer = new ViewHolder[capacity];
            removeSlots = new int[capacity];
        }

        private bool IsValidTemplateId(int templateId)
        {
            return templateId >= 0 && templateId < templates.Length && templates[templateId] != null;
        }
    }
}
