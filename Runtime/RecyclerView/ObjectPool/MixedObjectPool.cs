namespace AlicizaX.UI
{
    using System;
    using System.Collections.Generic;

    public class MixedObjectPool<T> : IMixedObjectPool<T> where T : class
    {
        private const int DEFAULT_MAX_SIZE_PER_TYPE = 10;

        private Stack<T>[] entries = Array.Empty<Stack<T>>();
        private int[] typeSize = Array.Empty<int>();
        private int[] activeCountByType = Array.Empty<int>();
        private int[] peakActiveByType = Array.Empty<int>();
        private readonly IMixedObjectFactory<T> factory;

        private readonly int defaultMaxSizePerType;
        private int hitCount;
        private int missCount;
        private int destroyCount;
        private bool disposed;

        public MixedObjectPool(IMixedObjectFactory<T> factory) : this(factory, DEFAULT_MAX_SIZE_PER_TYPE)
        {
        }

        public MixedObjectPool(IMixedObjectFactory<T> factory, int defaultMaxSizePerType)
        {
            this.factory = factory;
            this.defaultMaxSizePerType = defaultMaxSizePerType;

            if (defaultMaxSizePerType <= 0)
            {
                throw new ArgumentException("The maxSize must be greater than 0.");
            }
        }

        public T Allocate(int templateId)
        {
            if (disposed || templateId < 0)
            {
                return null;
            }

            if (templateId < entries.Length)
            {
                Stack<T> stack = entries[templateId];
                if (stack != null && stack.Count > 0)
                {
                    T obj = stack.Pop();
                    hitCount++;
                    TrackAllocate(templateId);
                    return obj;
                }
            }

            missCount++;
            T created = factory.Create(templateId);
            if (created == null)
            {
                return null;
            }

            TrackAllocate(templateId);
            return created;
        }

        public void Free(int templateId, T obj)
        {
            if (obj == null) return;

            if (templateId < 0)
            {
                factory.Destroy(templateId, obj);
                destroyCount++;
                return;
            }

            if (!factory.Validate(templateId, obj))
            {
                factory.Destroy(templateId, obj);
                destroyCount++;
                TrackFree(templateId);
                return;
            }

            if (disposed)
            {
                factory.Destroy(templateId, obj);
                destroyCount++;
                TrackFree(templateId);
                return;
            }

            int maxSize = GetMaxSize(templateId);
            Stack<T> stack = GetOrCreateStack(templateId);

            factory.Reset(templateId, obj);
            TrackFree(templateId);

            if (stack.Count >= maxSize)
            {
                factory.Destroy(templateId, obj);
                destroyCount++;
                return;
            }

            stack.Push(obj);
        }

        public int GetMaxSize(int templateId)
        {
            if (templateId < 0 || templateId >= typeSize.Length)
            {
                return defaultMaxSizePerType;
            }

            int size = typeSize[templateId];
            return size > 0 ? size : defaultMaxSizePerType;
        }

        public void SetMaxSize(int templateId, int value)
        {
            if (templateId < 0)
            {
                return;
            }

            EnsureTemplateCapacity(templateId + 1);
            typeSize[templateId] = value;
        }

        public void EnsureCapacity(int templateId, int value)
        {
            if (disposed || templateId < 0)
            {
                return;
            }

            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            EnsureTemplateCapacity(templateId + 1);
            int current = GetMaxSize(templateId);
            if (value > current)
            {
                typeSize[templateId] = value;
            }
        }

        public void Warm(int templateId, int count)
        {
            if (disposed || templateId < 0)
            {
                return;
            }

            if (count <= 0)
            {
                return;
            }

            int maxSize = GetMaxSize(templateId);
            if (count > maxSize)
            {
                count = maxSize;
            }

            Stack<T> stack = GetOrCreateStack(templateId);
            while (stack.Count < count)
            {
                T created = factory.Create(templateId);
                if (created == null)
                {
                    break;
                }

                stack.Push(created);
            }
        }

        public int GetActiveCount(int templateId)
        {
            return templateId >= 0 && templateId < activeCountByType.Length ? activeCountByType[templateId] : 0;
        }

        public int GetPeakActiveCount(int templateId)
        {
            return templateId >= 0 && templateId < peakActiveByType.Length ? peakActiveByType[templateId] : 0;
        }

        public int HitCount => hitCount;

        public int MissCount => missCount;

        public int DestroyCount => destroyCount;

        protected virtual void Clear()
        {
            ClearInactiveStacks();
        }

        private void ClearInactiveStacks()
        {
            for (int templateId = 0; templateId < entries.Length; templateId++)
            {
                Stack<T> stack = entries[templateId];
                if (stack == null || stack.Count <= 0) continue;

                while (stack.Count > 0)
                {
                    factory.Destroy(templateId, stack.Pop());
                    destroyCount++;
                }
            }
        }

        private void ClearAllState()
        {
            ClearInactiveStacks();
            Array.Clear(entries, 0, entries.Length);
            Array.Clear(typeSize, 0, typeSize.Length);
            Array.Clear(activeCountByType, 0, activeCountByType.Length);
            Array.Clear(peakActiveByType, 0, peakActiveByType.Length);
        }

        public void ClearInactive()
        {
            TrimInactive();
        }

        public void TrimInactive()
        {
            Clear();
        }

        public void Dispose()
        {
            disposed = true;
            ClearAllState();
            GC.SuppressFinalize(this);
        }

        private Stack<T> GetOrCreateStack(int templateId)
        {
            EnsureTemplateCapacity(templateId + 1);
            Stack<T> stack = entries[templateId];
            if (stack == null)
            {
                stack = new Stack<T>(GetMaxSize(templateId));
                entries[templateId] = stack;
            }

            return stack;
        }

        private void TrackAllocate(int templateId)
        {
            EnsureTemplateCapacity(templateId + 1);
            int active = activeCountByType[templateId] + 1;
            activeCountByType[templateId] = active;

            if (active > peakActiveByType[templateId])
            {
                peakActiveByType[templateId] = active;
            }
        }

        private void TrackFree(int templateId)
        {
            if (templateId < 0 || templateId >= activeCountByType.Length)
            {
                return;
            }

            int active = activeCountByType[templateId];
            if (active > 0)
            {
                activeCountByType[templateId] = active - 1;
            }
        }

        private void EnsureTemplateCapacity(int required)
        {
            if (required <= entries.Length)
            {
                return;
            }

            int capacity = entries.Length > 0 ? entries.Length : 4;
            while (capacity < required)
            {
                capacity <<= 1;
            }

            Array.Resize(ref entries, capacity);
            Array.Resize(ref typeSize, capacity);
            Array.Resize(ref activeCountByType, capacity);
            Array.Resize(ref peakActiveByType, capacity);
        }
    }
}
