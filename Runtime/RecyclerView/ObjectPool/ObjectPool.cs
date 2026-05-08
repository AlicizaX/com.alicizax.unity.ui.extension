namespace AlicizaX.UI
{
    using System;
    using System.Collections.Generic;

    public class ObjectPool<T> : IObjectPool<T> where T : class
    {
        private readonly Stack<T> entries;
        private readonly HashSet<T> activeEntries;
        private readonly int initialSize;
        private int maxSize;
        protected readonly IObjectFactory<T> factory;
        private int totalCount;
        private int activeCount;
        private int hitCount;
        private int missCount;
        private int destroyCount;
        private int peakActive;
        private bool disposed;

        public ObjectPool(IObjectFactory<T> factory) : this(factory, Environment.ProcessorCount * 2)
        {
        }

        public ObjectPool(IObjectFactory<T> factory, int maxSize) : this(factory, 0, maxSize)
        {
        }

        public ObjectPool(IObjectFactory<T> factory, int initialSize, int maxSize)
        {
            this.factory = factory;
            this.initialSize = initialSize;
            this.maxSize = maxSize;

            if (maxSize < initialSize)
            {
                throw new ArgumentException("The maxSize must be greater than or equal to the initialSize.");
            }

            entries = new Stack<T>(maxSize);
            activeEntries = new HashSet<T>();
            Warm(initialSize);
        }

        public int MaxSize => maxSize;

        public int InitialSize => initialSize;

        public int InactiveCount => entries.Count;

        public int ActiveCount => activeCount;

        public int TotalCount => totalCount;

        public int PeakActive => peakActive;

        public int HitCount => hitCount;

        public int MissCount => missCount;

        public int DestroyCount => destroyCount;

        public virtual T Allocate()
        {
            if (disposed)
            {
                return null;
            }

            T value;
            if (entries.Count > 0)
            {
                value = entries.Pop();
                if (value != null)
                {
                    hitCount++;
                    activeCount++;
                    activeEntries.Add(value);
                    if (activeCount > peakActive)
                    {
                        peakActive = activeCount;
                    }
                    return value;
                }
            }

            missCount++;
            value = factory.Create();
            totalCount++;
            activeCount++;
            activeEntries.Add(value);
            if (activeCount > peakActive)
            {
                peakActive = activeCount;
            }

            return value;
        }

        public virtual void Free(T obj)
        {
            if (obj == null) return;

            if (!factory.Validate(obj))
            {
                activeEntries.Remove(obj);
                factory.Destroy(obj);
                destroyCount++;
                if (totalCount > 0)
                {
                    totalCount--;
                }

                if (activeCount > 0)
                {
                    activeCount--;
                }

                return;
            }

            if (disposed)
            {
                activeEntries.Remove(obj);
                factory.Destroy(obj);
                destroyCount++;
                if (totalCount > 0)
                {
                    totalCount--;
                }
                if (activeCount > 0)
                {
                    activeCount--;
                }
                return;
            }

            factory.Reset(obj);

            if (activeCount > 0)
            {
                activeCount--;
            }
            activeEntries.Remove(obj);

            if (entries.Count < maxSize)
            {
                entries.Push(obj);
                return;
            }

            factory.Destroy(obj);
            destroyCount++;
            if (totalCount > 0)
            {
                totalCount--;
            }
        }

        object IObjectPool.Allocate()
        {
            return Allocate();
        }

        public void Free(object obj)
        {
            Free((T)obj);
        }

        protected virtual void Clear()
        {
            while (entries.Count > 0)
            {
                var value = entries.Pop();
                if (value != null)
                {
                    factory.Destroy(value);
                    destroyCount++;
                }
            }

            activeCount = activeEntries.Count;
            totalCount = activeCount;
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

        public void EnsureCapacity(int value)
        {
            if (disposed)
            {
                return;
            }

            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (value > maxSize)
            {
                maxSize = value;
            }
        }

        public void Warm(int count)
        {
            if (disposed)
            {
                return;
            }

            if (count <= 0)
            {
                return;
            }

            if (count > maxSize)
            {
                count = maxSize;
            }

            while (totalCount < count)
            {
                entries.Push(factory.Create());
                totalCount++;
            }
        }
    }

}
