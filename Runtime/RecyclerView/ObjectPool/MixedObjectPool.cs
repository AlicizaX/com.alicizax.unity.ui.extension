namespace AlicizaX.UI
{
    using System;
    using System.Collections.Generic;

    public class MixedObjectPool<T> : IMixedObjectPool<T> where T : class
    {
        private const int DEFAULT_MAX_SIZE_PER_TYPE = 10;

        private readonly Dictionary<string, Stack<T>> entries;
        private readonly Dictionary<string, int> typeSize;
        private readonly Dictionary<string, int> activeCountByType;
        private readonly Dictionary<string, int> peakActiveByType;
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

            entries = new Dictionary<string, Stack<T>>(StringComparer.Ordinal);
            typeSize = new Dictionary<string, int>(StringComparer.Ordinal);
            activeCountByType = new Dictionary<string, int>(StringComparer.Ordinal);
            peakActiveByType = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        public T Allocate(string typeName)
        {
            if (disposed)
            {
                return null;
            }

            Stack<T> stack = GetOrCreateStack(typeName);
            if (stack.Count > 0)
            {
                T obj = stack.Pop();
                hitCount++;
                TrackAllocate(typeName);
                return obj;
            }

            missCount++;
            T created = factory.Create(typeName);
            TrackAllocate(typeName);
            return created;
        }

        public void Free(string typeName, T obj)
        {
            if (obj == null) return;

            if (!factory.Validate(typeName, obj))
            {
                factory.Destroy(typeName, obj);
                destroyCount++;
                TrackFree(typeName);
                return;
            }

            if (disposed)
            {
                factory.Destroy(typeName, obj);
                destroyCount++;
                TrackFree(typeName);
                return;
            }

            int maxSize = GetMaxSize(typeName);
            Stack<T> stack = GetOrCreateStack(typeName);

            factory.Reset(typeName, obj);
            TrackFree(typeName);

            if (stack.Count >= maxSize)
            {
                factory.Destroy(typeName, obj);
                destroyCount++;
                return;
            }

            stack.Push(obj);
        }

        public int GetMaxSize(string typeName)
        {
            if (typeSize.TryGetValue(typeName, out int size))
            {
                return size;
            }

            return defaultMaxSizePerType;
        }

        public void SetMaxSize(string typeName, int value)
        {
            typeSize[typeName] = value;
        }

        public void EnsureCapacity(string typeName, int value)
        {
            if (disposed)
            {
                return;
            }

            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            int current = GetMaxSize(typeName);
            if (value > current)
            {
                typeSize[typeName] = value;
            }
        }

        public void Warm(string typeName, int count)
        {
            if (disposed)
            {
                return;
            }

            if (count <= 0)
            {
                return;
            }

            int maxSize = GetMaxSize(typeName);
            if (count > maxSize)
            {
                count = maxSize;
            }

            Stack<T> stack = GetOrCreateStack(typeName);
            while (stack.Count < count)
            {
                stack.Push(factory.Create(typeName));
            }
        }

        public int GetActiveCount(string typeName)
        {
            return activeCountByType.TryGetValue(typeName, out int count) ? count : 0;
        }

        public int GetPeakActiveCount(string typeName)
        {
            return peakActiveByType.TryGetValue(typeName, out int count) ? count : 0;
        }

        public int HitCount => hitCount;

        public int MissCount => missCount;

        public int DestroyCount => destroyCount;

        protected virtual void Clear()
        {
            foreach (var kv in entries)
            {
                string typeName = kv.Key;
                Stack<T> stack = kv.Value;

                if (stack == null || stack.Count <= 0) continue;

                while (stack.Count > 0)
                {
                    factory.Destroy(typeName, stack.Pop());
                    destroyCount++;
                }
            }

            entries.Clear();
            typeSize.Clear();
            activeCountByType.Clear();
            peakActiveByType.Clear();
        }

        public void ClearInactive()
        {
            Clear();
        }

        public void Dispose()
        {
            disposed = true;
            Clear();
            GC.SuppressFinalize(this);
        }

        private Stack<T> GetOrCreateStack(string typeName)
        {
            if (!entries.TryGetValue(typeName, out Stack<T> stack))
            {
                stack = new Stack<T>(GetMaxSize(typeName));
                entries[typeName] = stack;
            }

            return stack;
        }

        private void TrackAllocate(string typeName)
        {
            activeCountByType.TryGetValue(typeName, out int active);
            active++;
            activeCountByType[typeName] = active;

            peakActiveByType.TryGetValue(typeName, out int peak);
            if (active > peak)
            {
                peakActiveByType[typeName] = active;
            }
        }

        private void TrackFree(string typeName)
        {
            activeCountByType.TryGetValue(typeName, out int active);
            if (active > 0)
            {
                activeCountByType[typeName] = active - 1;
            }

            peakActiveByType.TryGetValue(typeName, out int peak);
            int recommendedMax = peak + 1;
            typeSize.TryGetValue(typeName, out int currentMax);
            if (currentMax <= 0)
            {
                currentMax = defaultMaxSizePerType;
            }

            if (recommendedMax > currentMax)
            {
                typeSize[typeName] = recommendedMax;
            }
        }
    }

}
