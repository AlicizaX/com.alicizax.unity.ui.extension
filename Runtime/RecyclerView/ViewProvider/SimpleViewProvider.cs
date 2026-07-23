namespace AlicizaX.UI
{
    using Cysharp.Text;

    /// <summary>
    /// 单模板 ViewProvider。底层复用 MixedObjectPool（templateId 固定为 0）。
    /// </summary>
    internal sealed class SimpleViewProvider : ViewProvider
    {
        private const int DefaultTemplateId = 0;

        private readonly MixedObjectPool<ViewHolder> objectPool;

        public override string PoolStats
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return ZString.Format("hits={0}, misses={1}, destroys={2}, active={3}, peakActive={4}, capacity={5}",
                    objectPool.HitCount,
                    objectPool.MissCount,
                    objectPool.DestroyCount,
                    objectPool.GetActiveCount(DefaultTemplateId),
                    objectPool.GetPeakActiveCount(DefaultTemplateId),
                    objectPool.GetMaxSize(DefaultTemplateId));
#else
                return string.Empty;
#endif
            }
        }

        public SimpleViewProvider(RecyclerView recyclerView, ViewHolder[] templates) : base(recyclerView, templates)
        {
            UnityMixedComponentFactory<ViewHolder> factory = new(templates, recyclerView.Content);
            objectPool = new MixedObjectPool<ViewHolder>(factory, 32);
        }

        public override ViewHolder GetTemplate(int templateId)
        {
            return templates != null && templates.Length > 0 ? templates[0] : null;
        }

        internal override ViewHolder Allocate(int templateId)
        {
            var viewHolder = objectPool.Allocate(DefaultTemplateId);
            if (viewHolder == null)
            {
                return null;
            }

            viewHolder.SetPooledVisible(true);
            return viewHolder;
        }

        internal override void Free(int templateId, ViewHolder viewHolder)
        {
            objectPool.Free(DefaultTemplateId, viewHolder);
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

            PrepareVisibleStorage(warmCount);

            objectPool.EnsureCapacity(DefaultTemplateId, warmCount);
            objectPool.Warm(DefaultTemplateId, warmCount);
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
