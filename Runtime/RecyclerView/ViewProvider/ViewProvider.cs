using System.Collections.Generic;
using UnityEngine;

namespace AlicizaX.UI
{
    /// <summary>
    /// 可见 ViewHolder 的分配、回收与查询。
    /// 可见项数量通常很小，用稠密 List + 线性查找，避免 ring / 手写 hash。
    /// </summary>
    internal abstract class ViewProvider
    {
        private readonly List<ViewHolder> visibleHolders = new List<ViewHolder>(16);
        private ViewHolder[] removeBuffer = new ViewHolder[4];

        internal IAdapter Adapter { get; set; }

        internal LayoutManager LayoutManager { get; set; }

        public int VisibleCount => visibleHolders.Count;

        public ViewHolder GetVisibleViewHolder(int index)
        {
            return index >= 0 && index < visibleHolders.Count ? visibleHolders[index] : null;
        }

        public abstract string PoolStats { get; }

        protected RecyclerView recyclerView;
        protected ViewHolder[] templates;

        public ViewProvider(RecyclerView recyclerView, ViewHolder[] templates)
        {
            this.recyclerView = recyclerView;
            this.templates = templates;
        }

        public int TemplateCount => templates != null ? templates.Length : 0;

        public abstract ViewHolder GetTemplate(int templateId);

        internal abstract ViewHolder Allocate(int templateId);

        internal abstract void Free(int templateId, ViewHolder viewHolder);

        internal abstract void Reset();

        internal abstract void PreparePool();

        internal abstract void TrimInactive();

        internal abstract void Dispose();

        internal void CreateViewHolder(int index)
        {
            if (Adapter == null || LayoutManager == null)
            {
                return;
            }

            int unit = Mathf.Max(1, LayoutManager.Unit);
            for (int i = index; i < index + unit; i++)
            {
                if (!LayoutManager.UsesVirtualLayoutRange && i > Adapter.GetItemCount() - 1)
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

                viewHolder.TemplateId = templateId;
                viewHolder.Index = i;
                viewHolder.DataIndex = dataIndex;
                viewHolder.RecyclerView = recyclerView;
                visibleHolders.Add(viewHolder);

                LayoutManager.Layout(viewHolder, i);
                Adapter.OnBindViewHolder(viewHolder, dataIndex);
            }

            ValidateInvariants();
        }

        internal bool TryReuseVisibleRange(int targetStart, int targetEnd)
        {
            if (Adapter == null || LayoutManager == null || visibleHolders.Count <= 0 || targetEnd < targetStart)
            {
                return false;
            }

            int unit = Mathf.Max(1, LayoutManager.Unit);
            int slot = 0;
            for (int groupIndex = targetStart; groupIndex <= targetEnd; groupIndex += unit)
            {
                for (int layoutIndex = groupIndex; layoutIndex < groupIndex + unit; layoutIndex++)
                {
                    if (!LayoutManager.UsesVirtualLayoutRange && layoutIndex > Adapter.GetItemCount() - 1)
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

                    int dataIndex = LayoutManager.GetDataIndex(layoutIndex);
                    int templateId = Adapter.GetTemplateId(dataIndex);
                    if (viewHolder.TemplateId != templateId)
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
                    if (!LayoutManager.UsesVirtualLayoutRange && layoutIndex > Adapter.GetItemCount() - 1)
                    {
                        break;
                    }

                    ViewHolder viewHolder = visibleHolders[slot];
                    int dataIndex = LayoutManager.GetDataIndex(layoutIndex);
                    Adapter.OnRecycleViewHolder(viewHolder);
                    viewHolder.OnRecycled();

                    viewHolder.TemplateId = Adapter.GetTemplateId(dataIndex);
                    viewHolder.Index = layoutIndex;
                    viewHolder.DataIndex = dataIndex;
                    viewHolder.RecyclerView = recyclerView;

                    LayoutManager.Layout(viewHolder, layoutIndex);
                    Adapter.OnBindViewHolder(viewHolder, dataIndex);
                    slot++;
                }
            }

            ValidateInvariants();
            return true;
        }

        internal void RemoveViewHolder(int index)
        {
            if (Adapter == null || LayoutManager == null)
            {
                return;
            }

            int unit = Mathf.Max(1, LayoutManager.Unit);
            int removeCount = 0;
            EnsureRemoveBufferCapacity(unit);
            int end = index + unit;
            for (int i = index; i < end; i++)
            {
                if (!LayoutManager.UsesVirtualLayoutRange && i > Adapter.GetItemCount() - 1)
                {
                    break;
                }

                int slot = FindVisibleSlotByLayoutIndex(i);
                if (slot < 0)
                {
                    continue;
                }

                removeBuffer[removeCount++] = visibleHolders[slot];
            }

            for (int i = 0; i < removeCount; i++)
            {
                ViewHolder viewHolder = removeBuffer[i];
                removeBuffer[i] = null;
                if (viewHolder == null)
                {
                    continue;
                }

                int templateId = viewHolder.TemplateId;
                if (!RemoveVisibleHolder(viewHolder))
                {
                    continue;
                }

                Adapter.OnRecycleViewHolder(viewHolder);
                viewHolder.OnRecycled();
                Free(templateId, viewHolder);
            }

            ValidateInvariants();
        }

        public ViewHolder GetViewHolder(int layoutIndex)
        {
            int slot = FindVisibleSlotByLayoutIndex(layoutIndex);
            return slot >= 0 ? visibleHolders[slot] : null;
        }

        public int GetViewHolderIndex(int layoutIndex)
        {
            return FindVisibleSlotByLayoutIndex(layoutIndex);
        }

        internal int RebindVisibleDataIndex(int dataIndex)
        {
            return RebindVisibleDataRange(dataIndex, 1);
        }

        internal int RebindVisibleDataRange(int startDataIndex, int count)
        {
            if (Adapter == null || count <= 0)
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

                holder.Index += delta;
                holder.DataIndex += delta;
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
                Adapter?.OnRecycleViewHolder(holder);
                holder.OnRecycled();
                Free(templateId, holder);
                recycled++;
            }

            return recycled;
        }

        internal void Clear()
        {
            for (int i = visibleHolders.Count - 1; i >= 0; i--)
            {
                ViewHolder viewHolder = visibleHolders[i];
                visibleHolders.RemoveAt(i);
                if (viewHolder == null)
                {
                    continue;
                }

                int templateId = viewHolder.TemplateId;
                Adapter?.OnRecycleViewHolder(viewHolder);
                viewHolder.OnRecycled();
                Free(templateId, viewHolder);
            }

            ValidateInvariants();
        }

        public Vector2 CalculateViewSize(int index)
        {
            if (Adapter == null)
            {
                return Vector2.zero;
            }

            ViewHolder template = GetTemplate(Adapter.GetTemplateId(index));
            return template != null ? template.SizeDelta : Vector2.zero;
        }

        public int GetItemCount()
        {
            return Adapter == null ? 0 : Adapter.GetItemCount();
        }

        protected int GetRecommendedWarmCount()
        {
            if (Adapter == null || LayoutManager == null)
            {
                return 0;
            }

            int itemCount = Adapter.GetItemCount();
            if (itemCount <= 0)
            {
                return 0;
            }

            int start = LayoutManager.GetStartIndex();
            if (!LayoutManager.UsesVirtualLayoutRange)
            {
                start = Mathf.Max(0, start);
            }

            int end = Mathf.Max(start, LayoutManager.GetEndIndex());
            int visibleCount = end - start + 1;
            int unit = Mathf.Max(1, LayoutManager.Unit);
            int bufferCount = unit * 2;
            // Loop 虚拟 itemCount 可能很大：warm 只按可见窗口，并给硬上限
            int warm = visibleCount + bufferCount;
            if (warm > 128)
            {
                warm = 128;
            }

            return Mathf.Min(itemCount, warm);
        }

        protected void PrepareVisibleStorage(int warmCount)
        {
            int required = Mathf.Max(Mathf.Max(1, LayoutManager != null ? LayoutManager.Unit : 1), warmCount);
            if (visibleHolders.Capacity < required)
            {
                visibleHolders.Capacity = required;
            }

            if (removeBuffer.Length < required)
            {
                removeBuffer = new ViewHolder[required];
            }
        }

        private int FindVisibleSlotByLayoutIndex(int layoutIndex)
        {
            for (int i = 0; i < visibleHolders.Count; i++)
            {
                ViewHolder holder = visibleHolders[i];
                if (holder != null && holder.Index == layoutIndex)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool RemoveVisibleHolder(ViewHolder viewHolder)
        {
            int slot = visibleHolders.IndexOf(viewHolder);
            if (slot < 0)
            {
                return false;
            }

            // swap-remove 保持 O(1) 删除
            int last = visibleHolders.Count - 1;
            if (slot != last)
            {
                visibleHolders[slot] = visibleHolders[last];
            }

            visibleHolders.RemoveAt(last);
            return true;
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
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ValidateInvariants()
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
    }
}
