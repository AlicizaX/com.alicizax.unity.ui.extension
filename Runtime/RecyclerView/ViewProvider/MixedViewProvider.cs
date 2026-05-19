using System;
using Cysharp.Text;

namespace AlicizaX.UI
{
    internal class MixedViewProvider : ViewProvider
    {
        private readonly MixedObjectPool<ViewHolder> objectPool;
        private readonly int[] warmCountsByTemplateId;
        private bool templateErrorLogged;

        public override string PoolStats
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return ZString.Format("hits={0}, misses={1}, destroys={2}", objectPool.HitCount, objectPool.MissCount, objectPool.DestroyCount);
#else
                return string.Empty;
#endif
            }
        }

        public MixedViewProvider(RecyclerView recyclerView, ViewHolder[] templates) : base(recyclerView, templates)
        {
            warmCountsByTemplateId = new int[templates != null ? templates.Length : 0];
            UnityMixedComponentFactory<ViewHolder> factory = new(templates, recyclerView.Content);
            objectPool = new MixedObjectPool<ViewHolder>(factory);
        }

        public override ViewHolder GetTemplate(int templateId)
        {
            if (!IsValidTemplateId(templateId))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogTemplateError("ViewProvider template id is invalid.");
#endif
                return null;
            }

            return templates[templateId];
        }

        internal override ViewHolder Allocate(int templateId)
        {
            if (!IsValidTemplateId(templateId))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogTemplateError("ViewProvider template id is invalid.");
#endif
                return null;
            }

            var viewHolder = objectPool.Allocate(templateId);
            if (viewHolder == null)
            {
                return null;
            }

            viewHolder.SetPooledVisible(true);
            return viewHolder;
        }

        internal override void Free(int templateId, ViewHolder viewHolder)
        {
            objectPool.Free(templateId, viewHolder);
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

            int itemCount = GetItemCount();
            int start = LayoutManager.GetStartIndex();
            if (!LayoutManager.UsesVirtualLayoutRange)
            {
                start = Math.Max(0, start);
            }

            int end = LayoutManager.UsesVirtualLayoutRange
                ? start + warmCount - 1
                : Math.Min(itemCount - 1, start + warmCount - 1);

            Array.Clear(warmCountsByTemplateId, 0, warmCountsByTemplateId.Length);
            for (int index = start; index <= end; index++)
            {
                int dataIndex = LayoutManager.GetDataIndex(index);
                int templateId = Adapter.GetTemplateId(dataIndex);
                if (!IsValidTemplateId(templateId))
                {
                    continue;
                }

                warmCountsByTemplateId[templateId]++;
            }

            for (int templateId = 0; templateId < warmCountsByTemplateId.Length; templateId++)
            {
                int count = warmCountsByTemplateId[templateId];
                if (count <= 0)
                {
                    continue;
                }

                int targetCount = count + Math.Max(1, LayoutManager.Unit);
                objectPool.EnsureCapacity(templateId, targetCount);
                objectPool.Warm(templateId, targetCount);
            }
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

        private bool IsValidTemplateId(int templateId)
        {
            return templateId >= 0 &&
                   templates != null &&
                   templateId < templates.Length &&
                   templates[templateId] != null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void LogTemplateError(string message)
        {
            if (templateErrorLogged)
            {
                return;
            }

            templateErrorLogged = true;
            Log.Error(message);
        }
#endif
    }
}
