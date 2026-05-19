namespace AlicizaX.UI
{
    using Cysharp.Text;

    internal sealed class SimpleViewProvider : ViewProvider
    {
        private readonly ObjectPool<ViewHolder> objectPool;

        public override string PoolStats
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return ZString.Format("hits={0}, misses={1}, destroys={2}, active={3}, inactive={4}, peakActive={5}, capacity={6}",
                    objectPool.HitCount,
                    objectPool.MissCount,
                    objectPool.DestroyCount,
                    objectPool.ActiveCount,
                    objectPool.InactiveCount,
                    objectPool.PeakActive,
                    objectPool.MaxSize);
#else
                return string.Empty;
#endif
            }
        }

        public SimpleViewProvider(RecyclerView recyclerView, ViewHolder[] templates) : base(recyclerView, templates)
        {
            UnityComponentFactory<ViewHolder> factory = new(GetTemplate(0), recyclerView.Content);
            objectPool = new ObjectPool<ViewHolder>(factory, 0, 32);
        }

        public override ViewHolder GetTemplate(int templateId)
        {
            return templates != null && templates.Length > 0 ? templates[0] : null;
        }

        internal override ViewHolder Allocate(int templateId)
        {
            var viewHolder = objectPool.Allocate();
            viewHolder.SetPooledVisible(true);
            return viewHolder;
        }

        internal override void Free(int templateId, ViewHolder viewHolder)
        {
            objectPool.Free(viewHolder);
        }

        internal override void Reset()
        {
            Clear();
        }

        internal override void PreparePool()
        {
            int warmCount = GetRecommendedWarmCount();
            if (warmCount <= 0)
            {
                return;
            }

            PrepareDataBucketStorage(warmCount);

            objectPool.EnsureCapacity(warmCount);
            objectPool.Warm(warmCount);
        }

        internal override void TrimInactive()
        {
            objectPool.TrimInactive();
        }

        internal override void Dispose()
        {
            Clear();
            objectPool.Dispose();
        }
    }
}
