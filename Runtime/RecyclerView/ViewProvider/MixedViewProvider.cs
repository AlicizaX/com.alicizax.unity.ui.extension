using System;
using System.Collections.Generic;
using Cysharp.Text;

namespace AlicizaX.UI
{
    public class MixedViewProvider : ViewProvider
    {
        private readonly MixedObjectPool<ViewHolder> objectPool;
        private readonly Dictionary<string, int> templateIdsByName = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ViewHolder> templatesByName = new(StringComparer.Ordinal);
        private readonly ViewHolder[] cachedTemplates;
        private readonly string[] templateNames;
        private readonly int[] warmCountsByType;

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
            int count = 0;
            for (int i = 0; i < templates.Length; i++)
            {
                if (templates[i] != null)
                {
                    count++;
                }
            }

            cachedTemplates = new ViewHolder[count];
            templateNames = new string[count];
            warmCountsByType = new int[count];

            int typeId = 0;
            for (int i = 0; i < templates.Length; i++)
            {
                ViewHolder template = templates[i];
                if (template == null)
                {
                    continue;
                }

                string templateName = template.GetType().Name;
                templateIdsByName[templateName] = typeId;
                templatesByName[templateName] = template;
                cachedTemplates[typeId] = template;
                templateNames[typeId] = templateName;
                typeId++;
            }

            UnityMixedComponentFactory<ViewHolder> factory = new(templatesByName, recyclerView.Content);
            objectPool = new MixedObjectPool<ViewHolder>(factory);
        }

        public override ViewHolder GetTemplate(string viewName)
        {
            if (!templatesByName.TryGetValue(viewName, out ViewHolder template))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Log.Error("ViewProvider template was not found.");
#endif
                return null;
            }

            return template;
        }

        public override ViewHolder Allocate(string viewName)
        {
            var viewHolder = objectPool.Allocate(viewName);
            viewHolder.gameObject.SetActive(true);
            return viewHolder;
        }

        public override void Free(string viewName, ViewHolder viewHolder)
        {
            objectPool.Free(viewName, viewHolder);
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

            int itemCount = GetItemCount();
            int start = Math.Max(0, LayoutManager.GetStartIndex());
            int end = Math.Min(itemCount - 1, start + warmCount - 1);

            Array.Clear(warmCountsByType, 0, warmCountsByType.Length);
            for (int index = start; index <= end; index++)
            {
                string viewName = Adapter.GetViewName(index);
                if (string.IsNullOrEmpty(viewName) || !templateIdsByName.TryGetValue(viewName, out int typeId))
                {
                    continue;
                }

                warmCountsByType[typeId]++;
            }

            for (int typeId = 0; typeId < warmCountsByType.Length; typeId++)
            {
                int count = warmCountsByType[typeId];
                if (count <= 0)
                {
                    continue;
                }

                string typeName = templateNames[typeId];
                int targetCount = count + Math.Max(1, LayoutManager.Unit);
                objectPool.EnsureCapacity(typeName, targetCount);
                objectPool.Warm(typeName, targetCount);
            }
        }
    }
}
