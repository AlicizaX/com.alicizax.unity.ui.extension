namespace AlicizaX.UI
{
    using Cysharp.Text;

    public sealed class SimpleViewProvider : ViewProvider
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
            UnityComponentFactory<ViewHolder> factory = new(GetTemplate(), recyclerView.Content);
            objectPool = new ObjectPool<ViewHolder>(factory, 0, 1);
        }

        public override ViewHolder GetTemplate(string viewName = "")
        {
            return templates != null && templates.Length > 0 ? templates[0] : null;
        }

        public override ViewHolder Allocate(string viewName)
        {
            var viewHolder = objectPool.Allocate();
            viewHolder.gameObject.SetActive(true);
            return viewHolder;
        }

        public override void Free(string viewName, ViewHolder viewHolder)
        {
            objectPool.Free(viewHolder);
        }

        public override void Reset()
        {
            Clear();
            (Adapter as IItemRenderCacheOwner)?.ReleaseAllItemRenders();
            objectPool.ClearInactive();
        }

        public override void PreparePool()
        {
            int warmCount = GetRecommendedWarmCount();
            if (warmCount <= 0)
            {
                return;
            }

            PrepareBucketPool(warmCount);

            objectPool.EnsureCapacity(warmCount);
            objectPool.Warm(warmCount);
        }
    }
}
