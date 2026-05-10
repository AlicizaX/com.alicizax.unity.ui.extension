using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AlicizaX.UI
{
    public abstract class ViewProvider
    {
        private ViewHolder[] visibleHolders = new ViewHolder[8];
        private ViewHolder[] removeBuffer = new ViewHolder[4];
        private ViewHolderBucket[] bucketPool = new ViewHolderBucket[8];
        private int visibleHead;
        private int visibleCount;
        private int bucketPoolCount;
        private readonly Dictionary<int, ViewHolder> viewHoldersByIndex = new();
        private readonly Dictionary<int, ViewHolderBucket> viewHoldersByDataIndex = new();
        private readonly Dictionary<int, int> viewHolderPositions = new();

        public IAdapter Adapter { get; set; }

        public LayoutManager LayoutManager { get; set; }

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

        public abstract ViewHolder GetTemplate(string viewName);

        public abstract ViewHolder Allocate(string viewName);

        public abstract void Free(string viewName, ViewHolder viewHolder);

        public abstract void Reset();

        public abstract void PreparePool();

        public void CreateViewHolder(int index)
        {
            for (int i = index; i < index + LayoutManager.Unit; i++)
            {
                if (i > Adapter.GetItemCount() - 1) break;

                string viewName = Adapter.GetViewName(i);
                var viewHolder = Allocate(viewName);
                viewHolder.Name = viewName;
                viewHolder.Index = i;
                viewHolder.DataIndex = i;
                viewHolder.RecyclerView = recyclerView;
                (Adapter as IItemRenderPrewarmer)?.PrewarmItemRender(viewHolder, viewName);
                if (!AddVisibleHolder(viewHolder))
                {
                    Free(viewName, viewHolder);
                    continue;
                }

                if (!RegisterViewHolder(viewHolder))
                {
                    RemoveVisibleHolder(viewHolder);
                    Free(viewName, viewHolder);
                    continue;
                }

                LayoutManager.Layout(viewHolder, i);
                Adapter.OnBindViewHolder(viewHolder, i);
            }
        }

        public void RemoveViewHolder(int index)
        {
            int removeCount = 0;
            int end = index + LayoutManager.Unit;
            EnsureRemoveBufferCapacity(LayoutManager.Unit);
            for (int i = index; i < end; i++)
            {
                if (i > Adapter.GetItemCount() - 1)
                {
                    break;
                }

                int viewHolderIndex = GetViewHolderIndex(i);
                if (viewHolderIndex < 0 || viewHolderIndex >= visibleCount)
                {
                    continue;
                }

                removeBuffer[removeCount++] = visibleHolders[GetVisibleSlot(viewHolderIndex)];
            }

            for (int i = 0; i < removeCount; i++)
            {
                ViewHolder viewHolder = removeBuffer[i];
                removeBuffer[i] = null;
                if (viewHolder == null)
                {
                    continue;
                }

                string viewName = viewHolder.Name;
                RemoveVisibleHolder(viewHolder);
                UnregisterViewHolder(viewHolder);
                Adapter?.OnRecycleViewHolder(viewHolder);
                viewHolder.OnRecycled();
                ClearSelectedState(viewHolder);
                Free(viewName, viewHolder);
            }
        }

        public ViewHolder GetViewHolder(int index)
        {
            return viewHoldersByIndex.TryGetValue(index, out ViewHolder viewHolder)
                ? viewHolder
                : null;
        }

        public ViewHolder GetViewHolderByDataIndex(int dataIndex)
        {
            return viewHoldersByDataIndex.TryGetValue(dataIndex, out ViewHolderBucket bucket) && bucket.Count > 0
                ? bucket[0]
                : null;
        }

        public bool TryGetViewHolderBucket(int dataIndex, out ViewHolderBucket bucket)
        {
            return viewHoldersByDataIndex.TryGetValue(dataIndex, out bucket) && bucket.Count > 0;
        }

        public int GetViewHolderIndex(int index)
        {
            return viewHolderPositions.TryGetValue(index, out int viewHolderIndex)
                ? viewHolderIndex
                : -1;
        }

        public void Clear()
        {
            for (int i = 0; i < visibleCount; i++)
            {
                ViewHolder viewHolder = visibleHolders[GetVisibleSlot(i)];
                if (viewHolder == null)
                {
                    continue;
                }

                string viewName = viewHolder.Name;
                Adapter?.OnRecycleViewHolder(viewHolder);
                UnregisterViewHolder(viewHolder);
                viewHolder.OnRecycled();
                ClearSelectedState(viewHolder);
                Free(viewName, viewHolder);
            }

            System.Array.Clear(visibleHolders, 0, visibleHolders.Length);
            visibleHead = 0;
            visibleCount = 0;
            viewHoldersByIndex.Clear();
            foreach (var pair in viewHoldersByDataIndex)
            {
                ReleaseBucket(pair.Value);
            }
            viewHoldersByDataIndex.Clear();
            viewHolderPositions.Clear();
        }

        public Vector2 CalculateViewSize(int index)
        {
            Vector2 size = GetTemplate(Adapter.GetViewName(index)).SizeDelta;
            return size;
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

            int start = Mathf.Max(0, LayoutManager.GetStartIndex());
            int end = Mathf.Max(start, LayoutManager.GetEndIndex());
            int visibleCount = end - start + 1;
            int unit = Mathf.Max(1, LayoutManager.Unit);
            int bufferCount = unit * 2;
            return Mathf.Min(itemCount, visibleCount + bufferCount);
        }

        protected void PrepareVisibleStorage(int warmCount)
        {
            int capacity = Mathf.Max(Mathf.Max(1, LayoutManager != null ? LayoutManager.Unit : 1), warmCount);
            if (visibleHolders.Length < capacity)
            {
                visibleHolders = new ViewHolder[capacity];
            }

            if (removeBuffer.Length < capacity)
            {
                removeBuffer = new ViewHolder[capacity];
            }
        }

        private bool RegisterViewHolder(ViewHolder viewHolder)
        {
            if (viewHolder == null)
            {
                return false;
            }

            if (!viewHoldersByDataIndex.TryGetValue(viewHolder.DataIndex, out ViewHolderBucket bucket))
            {
                bucket = AllocateBucket();
                if (bucket == null)
                {
                    return false;
                }

                viewHoldersByDataIndex[viewHolder.DataIndex] = bucket;
            }

            viewHoldersByIndex[viewHolder.Index] = viewHolder;
            viewHolderPositions[viewHolder.Index] = GetVisibleSlot(visibleCount - 1);
            bucket.Add(viewHolder);
            return true;
        }

        private void UnregisterViewHolder(ViewHolder viewHolder)
        {
            if (viewHolder == null)
            {
                return;
            }

            viewHoldersByIndex.Remove(viewHolder.Index);
            viewHolderPositions.Remove(viewHolder.Index);

            if (!viewHoldersByDataIndex.TryGetValue(viewHolder.DataIndex, out ViewHolderBucket bucket))
            {
                return;
            }

            bucket.Remove(viewHolder);
            if (bucket.Count == 0)
            {
                viewHoldersByDataIndex.Remove(viewHolder.DataIndex);
                ReleaseBucket(bucket);
            }
        }

        private bool AddVisibleHolder(ViewHolder viewHolder)
        {
            if (visibleCount == visibleHolders.Length)
            {
                EnsureVisibleHolderCapacity(visibleCount + 1);
            }

            visibleHolders[GetVisibleSlot(visibleCount)] = viewHolder;
            visibleCount++;
            return true;
        }

        private void RemoveVisibleHolder(ViewHolder viewHolder)
        {
            if (!viewHolderPositions.TryGetValue(viewHolder.Index, out int slot))
            {
                return;
            }

            int last = GetVisibleSlot(visibleCount - 1);
            ViewHolder lastHolder = visibleHolders[last];
            visibleHolders[slot] = lastHolder;
            visibleHolders[last] = null;
            visibleCount--;
            if (visibleCount == 0)
            {
                visibleHead = 0;
            }

            if (lastHolder != null && lastHolder != viewHolder)
            {
                viewHolderPositions[lastHolder.Index] = slot;
            }
        }

        private int GetVisibleSlot(int index)
        {
            return (visibleHead + index) % visibleHolders.Length;
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
            for (int i = 0; i < visibleCount; i++)
            {
                next[i] = visibleHolders[GetVisibleSlot(i)];
            }

            visibleHolders = next;
            visibleHead = 0;
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

        private static void ClearSelectedState(ViewHolder viewHolder)
        {
            if (viewHolder == null)
            {
                return;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(viewHolder.transform))
            {
                eventSystem.SetSelectedGameObject(null);
            }
        }


        protected void PrepareBucketPool(int count)
        {
            PrepareVisibleStorage(count);
            EnsureBucketPoolCapacity(count);
            int capacity = GetBucketCapacity();
            while (bucketPoolCount < count)
            {
                bucketPool[bucketPoolCount++] = new ViewHolderBucket(capacity);
            }
        }

        private ViewHolderBucket AllocateBucket()
        {
            if (bucketPoolCount > 0)
            {
                ViewHolderBucket bucket = bucketPool[--bucketPoolCount];
                bucketPool[bucketPoolCount] = null;
                bucket.Clear();
                return bucket;
            }

            return new ViewHolderBucket(GetBucketCapacity());
        }

        private void ReleaseBucket(ViewHolderBucket bucket)
        {
            if (bucket == null)
            {
                return;
            }

            bucket.Clear();
            EnsureBucketPoolCapacity(bucketPoolCount + 1);
            bucketPool[bucketPoolCount++] = bucket;
        }

        private int GetBucketCapacity()
        {
            return Mathf.Max(4, LayoutManager != null ? LayoutManager.Unit + 1 : 4);
        }

        private void EnsureBucketPoolCapacity(int required)
        {
            if (required <= bucketPool.Length)
            {
                return;
            }

            int capacity = bucketPool.Length;
            while (capacity < required)
            {
                capacity <<= 1;
            }

            ViewHolderBucket[] next = new ViewHolderBucket[capacity];
            System.Array.Copy(bucketPool, next, bucketPoolCount);
            bucketPool = next;
        }

        public sealed class ViewHolderBucket
        {
            private ViewHolder[] holders;

            public ViewHolderBucket(int capacity)
            {
                holders = new ViewHolder[capacity];
            }

            public int Count { get; private set; }

            public ViewHolder this[int index]
            {
                get
                {
                    if (index < 0 || index >= Count)
                    {
                        return null;
                    }

                    return holders[index];
                }
            }

            public void Add(ViewHolder holder)
            {
                if (Count == holders.Length)
                {
                    EnsureCapacity(Count + 1);
                }

                holders[Count++] = holder;
            }

            public void Remove(ViewHolder holder)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (holders[i] != holder)
                    {
                        continue;
                    }

                    Count--;
                    holders[i] = holders[Count];
                    holders[Count] = null;
                    return;
                }
            }

            public void Clear()
            {
                for (int i = 0; i < Count; i++)
                {
                    holders[i] = null;
                }

                Count = 0;
            }

            private void EnsureCapacity(int required)
            {
                if (required <= holders.Length)
                {
                    return;
                }

                int capacity = holders.Length;
                while (capacity < required)
                {
                    capacity <<= 1;
                }

                ViewHolder[] next = new ViewHolder[capacity];
                System.Array.Copy(holders, next, Count);
                holders = next;
            }
        }
    }
}
