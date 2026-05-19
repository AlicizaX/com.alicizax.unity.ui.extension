using UnityEngine;

namespace AlicizaX.UI
{
    internal abstract class ViewProvider
    {
        private ViewHolder[] visibleHolders = new ViewHolder[8];
        private ViewHolder[] removeBuffer = new ViewHolder[4];
        private ViewHolder[] viewHoldersByLayoutIndex = new ViewHolder[8];
        private int[] viewHolderSlotsByLayoutIndex = CreateSlotLookup(8);
        private int[] dataBucketIndexes = new int[8];
        private int[] dataBucketHeads = CreateSlotLookup(8);
        private int[] dataBucketCounts = new int[8];
        private int[] visibleNextByDataBucket = CreateSlotLookup(8);
        private int[] dataBucketLookupKeys = new int[16];
        private int[] dataBucketLookupValues = new int[16];
        private byte[] dataBucketLookupStates = new byte[16];
        private int visibleHead;
        private int visibleCount;
        private int visibleMask = 7;
        private int layoutLookupStartIndex;
        private int layoutLookupEndIndex = -1;
        private bool hasLayoutLookup;
        private int dataBucketCount;

        internal IAdapter Adapter { get; set; }

        internal LayoutManager LayoutManager { get; set; }

        public int VisibleCount => visibleCount;

        public ViewHolder GetVisibleViewHolder(int index)
        {
            return index >= 0 && index < visibleCount ? visibleHolders[GetVisibleSlot(index)] : null;
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
            for (int i = index; i < index + LayoutManager.Unit; i++)
            {
                if (!LayoutManager.UsesVirtualLayoutRange && i > Adapter.GetItemCount() - 1) break;

                int dataIndex = LayoutManager.GetDataIndex(i);
                int templateId = Adapter.GetTemplateId(dataIndex);
                var viewHolder = Allocate(templateId);
                if (viewHolder == null)
                {
                    continue;
                }

                viewHolder.TemplateId = templateId;
                viewHolder.Index = i;
                viewHolder.DataIndex = dataIndex;
                viewHolder.RecyclerView = recyclerView;
                if (!AddVisibleHolder(viewHolder))
                {
                    Free(templateId, viewHolder);
                    continue;
                }

                if (!RegisterViewHolder(viewHolder))
                {
                    RemoveLastVisibleHolder(viewHolder);
                    Free(templateId, viewHolder);
                    continue;
                }

                LayoutManager.Layout(viewHolder, i);
                Adapter.OnBindViewHolder(viewHolder, dataIndex);
            }

            ValidateInvariants();
        }

        internal bool TryReuseVisibleRange(int targetStart, int targetEnd)
        {
            if (Adapter == null || LayoutManager == null || visibleCount <= 0 || targetEnd < targetStart)
            {
                return false;
            }

            int slot = 0;
            for (int groupIndex = targetStart; groupIndex <= targetEnd; groupIndex += LayoutManager.Unit)
            {
                for (int layoutIndex = groupIndex; layoutIndex < groupIndex + LayoutManager.Unit; layoutIndex++)
                {
                    if (!LayoutManager.UsesVirtualLayoutRange && layoutIndex > Adapter.GetItemCount() - 1)
                    {
                        break;
                    }

                    if (slot >= visibleCount)
                    {
                        return false;
                    }

                    ViewHolder viewHolder = visibleHolders[GetVisibleSlot(slot)];
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

            if (slot != visibleCount)
            {
                return false;
            }

            ResetViewHolderLookups();

            slot = 0;
            for (int groupIndex = targetStart; groupIndex <= targetEnd; groupIndex += LayoutManager.Unit)
            {
                for (int layoutIndex = groupIndex; layoutIndex < groupIndex + LayoutManager.Unit; layoutIndex++)
                {
                    if (!LayoutManager.UsesVirtualLayoutRange && layoutIndex > Adapter.GetItemCount() - 1)
                    {
                        break;
                    }

                    ViewHolder viewHolder = visibleHolders[GetVisibleSlot(slot)];
                    int dataIndex = LayoutManager.GetDataIndex(layoutIndex);
                    Adapter.OnRecycleViewHolder(viewHolder);
                    viewHolder.OnRecycled();

                    viewHolder.TemplateId = Adapter.GetTemplateId(dataIndex);
                    viewHolder.Index = layoutIndex;
                    viewHolder.DataIndex = dataIndex;
                    viewHolder.RecyclerView = recyclerView;

                    RegisterViewHolder(viewHolder, GetVisibleSlot(slot));

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
            int removeCount = 0;
            int end = index + LayoutManager.Unit;
            EnsureRemoveBufferCapacity(LayoutManager.Unit);
            for (int i = index; i < end; i++)
            {
                if (!LayoutManager.UsesVirtualLayoutRange && i > Adapter.GetItemCount() - 1)
                {
                    break;
                }

                int viewHolderSlot = GetViewHolderIndex(i);
                if (viewHolderSlot < 0 || viewHolderSlot >= visibleHolders.Length)
                {
                    continue;
                }

                removeBuffer[removeCount++] = visibleHolders[viewHolderSlot];
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
                if (!RemoveRegisteredViewHolder(viewHolder))
                {
                    continue;
                }

                Adapter?.OnRecycleViewHolder(viewHolder);
                viewHolder.OnRecycled();
                Free(templateId, viewHolder);
            }

            ValidateInvariants();
        }

        public ViewHolder GetViewHolder(int index)
        {
            int offset = GetLayoutLookupOffset(index);
            return offset >= 0 ? viewHoldersByLayoutIndex[offset] : null;
        }

        public int GetViewHolderIndex(int index)
        {
            int offset = GetLayoutLookupOffset(index);
            return offset >= 0 ? viewHolderSlotsByLayoutIndex[offset] : -1;
        }

        internal int RebindVisibleDataIndex(int dataIndex)
        {
            if (Adapter == null)
            {
                return 0;
            }

            int bucketIndex = GetDataBucketIndex(dataIndex);
            if (bucketIndex < 0 || dataBucketCounts[bucketIndex] <= 0)
            {
                return 0;
            }

            int visitedCount = 0;
            int slot = dataBucketHeads[bucketIndex];
            while (slot >= 0)
            {
                int nextSlot = visibleNextByDataBucket[slot];
                ViewHolder holder = visibleHolders[slot];
                if (holder != null && holder.DataIndex == dataIndex)
                {
                    visitedCount++;
                    Adapter.OnBindViewHolder(holder, holder.DataIndex);
                }

                slot = nextSlot;
            }

            return visitedCount;
        }

        internal int ApplyVisibleSelection(int dataIndex, bool selected)
        {
            int bucketIndex = GetDataBucketIndex(dataIndex);
            if (bucketIndex < 0 || dataBucketCounts[bucketIndex] <= 0)
            {
                return 0;
            }

            int appliedCount = 0;
            int slot = dataBucketHeads[bucketIndex];
            while (slot >= 0)
            {
                int nextSlot = visibleNextByDataBucket[slot];
                ViewHolder holder = visibleHolders[slot];
                if (holder != null && holder.DataIndex == dataIndex)
                {
                    holder.ApplySelection(selected);
                    appliedCount++;
                }

                slot = nextSlot;
            }

            return appliedCount;
        }

        internal void Clear()
        {
            while (visibleCount > 0)
            {
                int slot = GetVisibleSlot(visibleCount - 1);
                ViewHolder viewHolder = visibleHolders[slot];
                if (viewHolder == null)
                {
                    visibleNextByDataBucket[slot] = -1;
                    visibleCount--;
                    continue;
                }

                int templateId = viewHolder.TemplateId;
                if (!RemoveRegisteredViewHolder(viewHolder))
                {
                    RemoveLastVisibleHolder(viewHolder);
                }

                Adapter?.OnRecycleViewHolder(viewHolder);
                viewHolder.OnRecycled();
                Free(templateId, viewHolder);
            }

            System.Array.Clear(visibleHolders, 0, visibleHolders.Length);
            visibleHead = 0;
            visibleCount = 0;
            ClearViewHolderLookups();
            ValidateInvariants();
        }

        private void ResetViewHolderLookups()
        {
            ClearViewHolderLookups();
        }

        public Vector2 CalculateViewSize(int index)
        {
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
            return Mathf.Min(itemCount, visibleCount + bufferCount);
        }

        protected void PrepareVisibleStorage(int warmCount)
        {
            int required = Mathf.Max(Mathf.Max(1, LayoutManager != null ? LayoutManager.Unit : 1), warmCount);
            if (visibleHolders.Length < required)
            {
                EnsureVisibleHolderCapacity(required);
            }

            EnsureLayoutLookupCapacity(required);
            EnsureDataBucketCapacity(required);
            EnsureDataBucketLookupCapacity(required);
            EnsureDataBucketSlotCapacity(required);

            if (removeBuffer.Length < required)
            {
                removeBuffer = new ViewHolder[required];
            }
        }

        private bool RegisterViewHolder(ViewHolder viewHolder)
        {
            return RegisterViewHolder(viewHolder, GetVisibleSlot(visibleCount - 1));
        }

        private bool RegisterViewHolder(ViewHolder viewHolder, int visibleSlot)
        {
            if (viewHolder == null)
            {
                return false;
            }

            int bucketIndex = GetOrCreateDataBucketIndex(viewHolder.DataIndex);
            if (bucketIndex < 0)
            {
                return false;
            }

            EnsureLayoutLookupRange(viewHolder.Index);
            int offset = viewHolder.Index - layoutLookupStartIndex;
            viewHoldersByLayoutIndex[offset] = viewHolder;
            viewHolderSlotsByLayoutIndex[offset] = visibleSlot;
            AddViewHolderToDataBucket(bucketIndex, visibleSlot);
            return true;
        }

        private bool RemoveRegisteredViewHolder(ViewHolder viewHolder)
        {
            if (viewHolder == null)
            {
                return false;
            }

            int slot = GetViewHolderIndex(viewHolder.Index);
            if (slot < 0)
            {
                return false;
            }

            UnregisterViewHolder(viewHolder, slot);
            RemoveVisibleHolderAtSlot(viewHolder, slot);
            return true;
        }

        private void UnregisterViewHolder(ViewHolder viewHolder, int visibleSlot)
        {
            int bucketIndex = GetDataBucketIndex(viewHolder.DataIndex);
            if (bucketIndex >= 0 &&
                RemoveViewHolderFromDataBucket(bucketIndex, visibleSlot) &&
                dataBucketCounts[bucketIndex] == 0)
            {
                RemoveDataBucketAt(bucketIndex);
            }

            RemoveLayoutLookup(viewHolder.Index);
        }

        private bool AddVisibleHolder(ViewHolder viewHolder)
        {
            if (visibleCount == visibleHolders.Length)
            {
                EnsureVisibleHolderCapacity(visibleCount + 1);
            }

            int slot = GetVisibleSlot(visibleCount);
            visibleHolders[slot] = viewHolder;
            visibleNextByDataBucket[slot] = -1;
            visibleCount++;
            return true;
        }

        private void RemoveLastVisibleHolder(ViewHolder viewHolder)
        {
            if (visibleCount <= 0)
            {
                return;
            }

            int last = GetVisibleSlot(visibleCount - 1);
            if (visibleHolders[last] != viewHolder)
            {
                return;
            }

            visibleHolders[last] = null;
            visibleNextByDataBucket[last] = -1;
            visibleCount--;
            if (visibleCount == 0)
            {
                visibleHead = 0;
            }
        }

        private void RemoveVisibleHolderAtSlot(ViewHolder viewHolder, int slot)
        {
            if (slot < 0 || slot >= visibleHolders.Length || visibleCount <= 0)
            {
                return;
            }

            int last = GetVisibleSlot(visibleCount - 1);
            ViewHolder lastHolder = visibleHolders[last];
            visibleHolders[slot] = lastHolder;
            visibleHolders[last] = null;
            if (lastHolder != null && lastHolder != viewHolder)
            {
                SetLayoutLookupSlot(lastHolder.Index, slot);
                MoveDataBucketSlot(lastHolder.DataIndex, last, slot);
            }
            else
            {
                visibleNextByDataBucket[last] = -1;
            }

            visibleCount--;
            if (visibleCount == 0)
            {
                visibleHead = 0;
            }
        }

        private static int[] CreateSlotLookup(int capacity)
        {
            int[] slots = new int[capacity];
            FillSlotRange(slots, 0, slots.Length);
            return slots;
        }

        private static void FillSlotRange(int[] slots, int start, int count)
        {
            int end = start + count;
            for (int i = start; i < end; i++)
            {
                slots[i] = -1;
            }
        }

        private int GetLayoutLookupOffset(int layoutIndex)
        {
            if (!hasLayoutLookup || layoutIndex < layoutLookupStartIndex || layoutIndex > layoutLookupEndIndex)
            {
                return -1;
            }

            int offset = layoutIndex - layoutLookupStartIndex;
            return viewHoldersByLayoutIndex[offset] != null ? offset : -1;
        }

        private void EnsureLayoutLookupRange(int layoutIndex)
        {
            if (!hasLayoutLookup)
            {
                EnsureLayoutLookupCapacity(1);
                viewHoldersByLayoutIndex[0] = null;
                viewHolderSlotsByLayoutIndex[0] = -1;
                layoutLookupStartIndex = layoutIndex;
                layoutLookupEndIndex = layoutIndex;
                hasLayoutLookup = true;
                return;
            }

            if (layoutIndex >= layoutLookupStartIndex && layoutIndex <= layoutLookupEndIndex)
            {
                return;
            }

            int oldStart = layoutLookupStartIndex;
            int oldEnd = layoutLookupEndIndex;
            int oldCount = oldEnd - oldStart + 1;
            int newStart = Mathf.Min(oldStart, layoutIndex);
            int newEnd = Mathf.Max(oldEnd, layoutIndex);
            int required = newEnd - newStart + 1;
            int copyOffset = oldStart - newStart;

            if (required > viewHoldersByLayoutIndex.Length)
            {
                int capacity = viewHoldersByLayoutIndex.Length;
                while (capacity < required)
                {
                    capacity <<= 1;
                }

                ViewHolder[] nextHolders = new ViewHolder[capacity];
                int[] nextSlots = CreateSlotLookup(capacity);
                System.Array.Copy(viewHoldersByLayoutIndex, 0, nextHolders, copyOffset, oldCount);
                System.Array.Copy(viewHolderSlotsByLayoutIndex, 0, nextSlots, copyOffset, oldCount);
                viewHoldersByLayoutIndex = nextHolders;
                viewHolderSlotsByLayoutIndex = nextSlots;
            }
            else
            {
                if (copyOffset > 0)
                {
                    System.Array.Copy(viewHoldersByLayoutIndex, 0, viewHoldersByLayoutIndex, copyOffset, oldCount);
                    System.Array.Copy(viewHolderSlotsByLayoutIndex, 0, viewHolderSlotsByLayoutIndex, copyOffset, oldCount);
                }

                ClearLayoutLookupRange(0, copyOffset);
                int clearStart = copyOffset + oldCount;
                ClearLayoutLookupRange(clearStart, required - clearStart);
            }

            layoutLookupStartIndex = newStart;
            layoutLookupEndIndex = newEnd;
        }

        private void EnsureLayoutLookupCapacity(int required)
        {
            if (required <= viewHoldersByLayoutIndex.Length)
            {
                return;
            }

            int count = hasLayoutLookup ? layoutLookupEndIndex - layoutLookupStartIndex + 1 : 0;
            int capacity = viewHoldersByLayoutIndex.Length;
            while (capacity < required)
            {
                capacity <<= 1;
            }

            ViewHolder[] nextHolders = new ViewHolder[capacity];
            int[] nextSlots = CreateSlotLookup(capacity);
            if (count > 0)
            {
                System.Array.Copy(viewHoldersByLayoutIndex, nextHolders, count);
                System.Array.Copy(viewHolderSlotsByLayoutIndex, nextSlots, count);
            }

            viewHoldersByLayoutIndex = nextHolders;
            viewHolderSlotsByLayoutIndex = nextSlots;
        }

        private void SetLayoutLookupSlot(int layoutIndex, int slot)
        {
            int offset = GetLayoutLookupOffset(layoutIndex);
            if (offset >= 0)
            {
                viewHolderSlotsByLayoutIndex[offset] = slot;
            }
        }

        private void RemoveLayoutLookup(int layoutIndex)
        {
            int offset = GetLayoutLookupOffset(layoutIndex);
            if (offset < 0)
            {
                return;
            }

            viewHoldersByLayoutIndex[offset] = null;
            viewHolderSlotsByLayoutIndex[offset] = -1;
            TrimLayoutLookupRange();
        }

        private void TrimLayoutLookupRange()
        {
            if (!hasLayoutLookup)
            {
                return;
            }

            int count = layoutLookupEndIndex - layoutLookupStartIndex + 1;
            int first = -1;
            int last = -1;
            for (int i = 0; i < count; i++)
            {
                if (viewHoldersByLayoutIndex[i] == null)
                {
                    continue;
                }

                if (first < 0)
                {
                    first = i;
                }

                last = i;
            }

            if (first < 0)
            {
                ClearLayoutLookupRange(0, count);
                hasLayoutLookup = false;
                layoutLookupStartIndex = 0;
                layoutLookupEndIndex = -1;
                return;
            }

            int newCount = last - first + 1;
            if (first > 0)
            {
                System.Array.Copy(viewHoldersByLayoutIndex, first, viewHoldersByLayoutIndex, 0, newCount);
                System.Array.Copy(viewHolderSlotsByLayoutIndex, first, viewHolderSlotsByLayoutIndex, 0, newCount);
            }

            ClearLayoutLookupRange(newCount, count - newCount);
            layoutLookupStartIndex += first;
            layoutLookupEndIndex = layoutLookupStartIndex + newCount - 1;
        }

        private void ClearLayoutLookupRange(int start, int count)
        {
            if (count <= 0)
            {
                return;
            }

            System.Array.Clear(viewHoldersByLayoutIndex, start, count);
            FillSlotRange(viewHolderSlotsByLayoutIndex, start, count);
        }

        private void ClearViewHolderLookups()
        {
            ClearLayoutLookup();
            ClearDataBuckets();
        }

        private void ClearLayoutLookup()
        {
            if (!hasLayoutLookup)
            {
                return;
            }

            int count = layoutLookupEndIndex - layoutLookupStartIndex + 1;
            ClearLayoutLookupRange(0, count);
            hasLayoutLookup = false;
            layoutLookupStartIndex = 0;
            layoutLookupEndIndex = -1;
        }

        private int GetOrCreateDataBucketIndex(int dataIndex)
        {
            int bucketIndex = GetDataBucketIndex(dataIndex);
            if (bucketIndex >= 0)
            {
                return bucketIndex;
            }

            EnsureDataBucketCapacity(dataBucketCount + 1);
            EnsureDataBucketLookupCapacity(dataBucketCount + 1);

            dataBucketIndexes[dataBucketCount] = dataIndex;
            dataBucketHeads[dataBucketCount] = -1;
            dataBucketCounts[dataBucketCount] = 0;
            AddDataBucketLookup(dataIndex, dataBucketCount);
            dataBucketCount++;
            return dataBucketCount - 1;
        }

        private void AddViewHolderToDataBucket(int bucketIndex, int visibleSlot)
        {
            visibleNextByDataBucket[visibleSlot] = dataBucketHeads[bucketIndex];
            dataBucketHeads[bucketIndex] = visibleSlot;
            dataBucketCounts[bucketIndex]++;
        }

        private bool RemoveViewHolderFromDataBucket(int bucketIndex, int visibleSlot)
        {
            int previousSlot = -1;
            int slot = dataBucketHeads[bucketIndex];
            while (slot >= 0)
            {
                int nextSlot = visibleNextByDataBucket[slot];
                if (slot == visibleSlot)
                {
                    if (previousSlot < 0)
                    {
                        dataBucketHeads[bucketIndex] = nextSlot;
                    }
                    else
                    {
                        visibleNextByDataBucket[previousSlot] = nextSlot;
                    }

                    visibleNextByDataBucket[slot] = -1;
                    dataBucketCounts[bucketIndex]--;
                    return true;
                }

                previousSlot = slot;
                slot = nextSlot;
            }

            return false;
        }

        private void MoveDataBucketSlot(int dataIndex, int oldSlot, int newSlot)
        {
            int bucketIndex = GetDataBucketIndex(dataIndex);
            if (bucketIndex < 0)
            {
                return;
            }

            if (dataBucketHeads[bucketIndex] == oldSlot)
            {
                dataBucketHeads[bucketIndex] = newSlot;
                visibleNextByDataBucket[newSlot] = visibleNextByDataBucket[oldSlot];
                visibleNextByDataBucket[oldSlot] = -1;
                return;
            }

            int slot = dataBucketHeads[bucketIndex];
            while (slot >= 0)
            {
                int nextSlot = visibleNextByDataBucket[slot];
                if (nextSlot == oldSlot)
                {
                    visibleNextByDataBucket[slot] = newSlot;
                    visibleNextByDataBucket[newSlot] = visibleNextByDataBucket[oldSlot];
                    visibleNextByDataBucket[oldSlot] = -1;
                    return;
                }

                slot = nextSlot;
            }
        }

        private bool DataBucketContainsSlot(int bucketIndex, int visibleSlot)
        {
            int slot = dataBucketHeads[bucketIndex];
            while (slot >= 0)
            {
                if (slot == visibleSlot)
                {
                    return true;
                }

                slot = visibleNextByDataBucket[slot];
            }

            return false;
        }

        private void RebuildDataBucketSlots()
        {
            for (int i = 0; i < dataBucketCount; i++)
            {
                dataBucketHeads[i] = -1;
                dataBucketCounts[i] = 0;
            }

            FillSlotRange(visibleNextByDataBucket, 0, visibleNextByDataBucket.Length);
            for (int slot = 0; slot < visibleCount; slot++)
            {
                ViewHolder holder = visibleHolders[slot];
                if (holder == null)
                {
                    continue;
                }

                int bucketIndex = GetDataBucketIndex(holder.DataIndex);
                if (bucketIndex < 0)
                {
                    continue;
                }

                AddViewHolderToDataBucket(bucketIndex, slot);
                SetLayoutLookupSlot(holder.Index, slot);
            }
        }

        private int GetDataBucketIndex(int dataIndex)
        {
            int slot = FindDataBucketLookupSlot(dataIndex);
            return slot >= 0 ? dataBucketLookupValues[slot] - 1 : -1;
        }

        private void RemoveDataBucketAt(int bucketIndex)
        {
            int removedDataIndex = dataBucketIndexes[bucketIndex];
            RemoveDataBucketLookup(removedDataIndex);
            int last = dataBucketCount - 1;
            if (bucketIndex != last)
            {
                dataBucketIndexes[bucketIndex] = dataBucketIndexes[last];
                dataBucketHeads[bucketIndex] = dataBucketHeads[last];
                dataBucketCounts[bucketIndex] = dataBucketCounts[last];
                SetDataBucketLookupValue(dataBucketIndexes[bucketIndex], bucketIndex);
            }

            dataBucketIndexes[last] = 0;
            dataBucketHeads[last] = -1;
            dataBucketCounts[last] = 0;
            dataBucketCount = last;
        }

        private void ClearDataBuckets()
        {
            for (int i = 0; i < dataBucketCount; i++)
            {
                dataBucketIndexes[i] = 0;
                dataBucketHeads[i] = -1;
                dataBucketCounts[i] = 0;
            }

            dataBucketCount = 0;
            System.Array.Clear(dataBucketLookupStates, 0, dataBucketLookupStates.Length);
            FillSlotRange(visibleNextByDataBucket, 0, visibleNextByDataBucket.Length);
        }

        private void EnsureDataBucketCapacity(int required)
        {
            if (required <= dataBucketIndexes.Length)
            {
                return;
            }

            int capacity = dataBucketIndexes.Length;
            while (capacity < required)
            {
                capacity <<= 1;
            }

            int[] nextIndexes = new int[capacity];
            int[] nextHeads = CreateSlotLookup(capacity);
            int[] nextCounts = new int[capacity];
            System.Array.Copy(dataBucketIndexes, nextIndexes, dataBucketCount);
            System.Array.Copy(dataBucketHeads, nextHeads, dataBucketCount);
            System.Array.Copy(dataBucketCounts, nextCounts, dataBucketCount);
            dataBucketIndexes = nextIndexes;
            dataBucketHeads = nextHeads;
            dataBucketCounts = nextCounts;
        }

        private void EnsureDataBucketSlotCapacity(int required)
        {
            if (required <= visibleNextByDataBucket.Length)
            {
                return;
            }

            int capacity = visibleNextByDataBucket.Length;
            while (capacity < required)
            {
                capacity <<= 1;
            }

            int[] next = CreateSlotLookup(capacity);
            System.Array.Copy(visibleNextByDataBucket, next, visibleNextByDataBucket.Length);
            visibleNextByDataBucket = next;
        }

        private void EnsureDataBucketLookupCapacity(int required)
        {
            if (required * 2 <= dataBucketLookupKeys.Length)
            {
                return;
            }

            int capacity = dataBucketLookupKeys.Length;
            while (required * 2 > capacity)
            {
                capacity <<= 1;
            }

            RebuildDataBucketLookup(capacity);
        }

        private void RebuildDataBucketLookup(int capacity)
        {
            dataBucketLookupKeys = new int[capacity];
            dataBucketLookupValues = new int[capacity];
            dataBucketLookupStates = new byte[capacity];
            for (int i = 0; i < dataBucketCount; i++)
            {
                AddDataBucketLookup(dataBucketIndexes[i], i);
            }
        }

        private void AddDataBucketLookup(int dataIndex, int bucketIndex)
        {
            int slot = FindDataBucketInsertSlot(dataIndex);
            dataBucketLookupKeys[slot] = dataIndex;
            dataBucketLookupValues[slot] = bucketIndex + 1;
            dataBucketLookupStates[slot] = 1;
        }

        private void SetDataBucketLookupValue(int dataIndex, int bucketIndex)
        {
            int slot = FindDataBucketLookupSlot(dataIndex);
            if (slot >= 0)
            {
                dataBucketLookupValues[slot] = bucketIndex + 1;
            }
        }

        private int FindDataBucketLookupSlot(int dataIndex)
        {
            int mask = dataBucketLookupKeys.Length - 1;
            int slot = HashDataIndex(dataIndex) & mask;
            while (dataBucketLookupStates[slot] != 0)
            {
                if (dataBucketLookupKeys[slot] == dataIndex)
                {
                    return slot;
                }

                slot = (slot + 1) & mask;
            }

            return -1;
        }

        private int FindDataBucketInsertSlot(int dataIndex)
        {
            int mask = dataBucketLookupKeys.Length - 1;
            int slot = HashDataIndex(dataIndex) & mask;
            while (dataBucketLookupStates[slot] != 0)
            {
                slot = (slot + 1) & mask;
            }

            return slot;
        }

        private void RemoveDataBucketLookup(int dataIndex)
        {
            int slot = FindDataBucketLookupSlot(dataIndex);
            if (slot < 0)
            {
                return;
            }

            int mask = dataBucketLookupKeys.Length - 1;
            int next = (slot + 1) & mask;
            while (dataBucketLookupStates[next] != 0)
            {
                int ideal = HashDataIndex(dataBucketLookupKeys[next]) & mask;
                int currentDistance = (next - ideal) & mask;
                int emptyDistance = (slot - ideal) & mask;
                if (emptyDistance < currentDistance)
                {
                    dataBucketLookupKeys[slot] = dataBucketLookupKeys[next];
                    dataBucketLookupValues[slot] = dataBucketLookupValues[next];
                    dataBucketLookupStates[slot] = 1;
                    slot = next;
                }

                next = (next + 1) & mask;
            }

            dataBucketLookupKeys[slot] = 0;
            dataBucketLookupValues[slot] = 0;
            dataBucketLookupStates[slot] = 0;
        }

        private static int HashDataIndex(int dataIndex)
        {
            unchecked
            {
                uint hash = (uint)dataIndex;
                hash ^= hash >> 16;
                hash *= 0x7feb352d;
                hash ^= hash >> 15;
                hash *= 0x846ca68b;
                hash ^= hash >> 16;
                return (int)hash;
            }
        }

        private int GetVisibleSlot(int index)
        {
            return (visibleHead + index) & visibleMask;
        }

        private void EnsureVisibleHolderCapacity(int required)
        {
            if (required <= visibleHolders.Length)
            {
                return;
            }

            int capacity = visibleHolders.Length;
            while (capacity < required)
            {
                capacity <<= 1;
            }

            ViewHolder[] next = new ViewHolder[capacity];
            int[] nextDataSlots = CreateSlotLookup(capacity);
            for (int i = 0; i < visibleCount; i++)
            {
                int oldSlot = GetVisibleSlot(i);
                next[i] = visibleHolders[oldSlot];
                nextDataSlots[i] = visibleNextByDataBucket[oldSlot];
            }

            visibleHolders = next;
            visibleNextByDataBucket = nextDataSlots;
            visibleMask = capacity - 1;
            visibleHead = 0;
            RebuildDataBucketSlots();
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

        protected void PrepareDataBucketStorage(int count)
        {
            PrepareVisibleStorage(count);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ValidateInvariants()
        {
            if (visibleCount < 0 || visibleCount > visibleHolders.Length)
            {
                Log.Error("ViewProvider invariant failed: visible count is out of range.");
                return;
            }

            int countedVisible = 0;
            for (int i = 0; i < visibleCount; i++)
            {
                ViewHolder holder = visibleHolders[GetVisibleSlot(i)];
                if (holder == null)
                {
                    Log.Error("ViewProvider invariant failed: visible holder is null.");
                    return;
                }

                countedVisible++;
                if (GetViewHolder(holder.Index) != holder)
                {
                    Log.Error("ViewProvider invariant failed: layout lookup does not point to the visible holder.");
                    return;
                }

                int slot = GetViewHolderIndex(holder.Index);
                if (slot < 0 || slot >= visibleHolders.Length || visibleHolders[slot] != holder)
                {
                    Log.Error("ViewProvider invariant failed: layout slot lookup is inconsistent.");
                    return;
                }

                int bucketIndex = GetDataBucketIndex(holder.DataIndex);
                if (bucketIndex < 0 || !DataBucketContainsSlot(bucketIndex, slot))
                {
                    Log.Error("ViewProvider invariant failed: data bucket does not contain the visible holder.");
                    return;
                }
            }

            if (countedVisible != visibleCount)
            {
                Log.Error("ViewProvider invariant failed: visible holder count mismatch.");
                return;
            }

            int countedBucketHolders = 0;
            for (int i = 0; i < dataBucketCount; i++)
            {
                int countedBucketCount = 0;
                int slot = dataBucketHeads[i];
                while (slot >= 0)
                {
                    ViewHolder holder = slot < visibleHolders.Length ? visibleHolders[slot] : null;
                    if (holder == null || holder.DataIndex != dataBucketIndexes[i])
                    {
                        Log.Error("ViewProvider invariant failed: data bucket entry is inconsistent.");
                        return;
                    }

                    countedBucketCount++;
                    countedBucketHolders++;
                    slot = visibleNextByDataBucket[slot];
                }

                if (countedBucketCount != dataBucketCounts[i])
                {
                    Log.Error("ViewProvider invariant failed: data bucket count mismatch.");
                    return;
                }
            }

            if (countedBucketHolders != visibleCount)
            {
                Log.Error("ViewProvider invariant failed: data bucket holder count mismatch.");
            }
        }
    }
}
