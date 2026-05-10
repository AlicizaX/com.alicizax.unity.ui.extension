using System;
using System.Collections.Generic;

namespace AlicizaX.UI
{
    internal interface IItemRenderCacheOwner
    {
        void ReleaseAllItemRenders();
    }

    internal interface IItemRenderPrewarmer
    {
        void PrewarmItemRender(ViewHolder viewHolder, string viewName);
    }

    public class Adapter<T> : IAdapter, IItemRenderCacheOwner, IItemRenderPrewarmer where T : ISimpleViewData
    {
        protected RecyclerView recyclerView;
        protected List<T> list;

        protected int choiceIndex = -1;
        private readonly Dictionary<string, ItemRenderResolver.ItemRenderDefinition> itemRenderDefinitions = new(StringComparer.Ordinal);
        private ItemRenderResolver.ItemRenderDefinition defaultItemRenderDefinition;

        public int ChoiceIndex
        {
            get => choiceIndex;
            set => SetChoiceIndex(value);
        }

        internal Action<int> OnChoiceIndexChanged;

        public Adapter(RecyclerView recyclerView) : this(recyclerView, new List<T>())
        {
        }

        public Adapter(RecyclerView recyclerView, List<T> list)
        {
            this.recyclerView = recyclerView;
            this.list = list ?? new List<T>();
        }

        public virtual int GetItemCount()
        {
            return list == null ? 0 : list.Count;
        }

        public virtual int GetRealCount()
        {
            return GetItemCount();
        }

        public virtual string GetViewName(int index)
        {
            return "";
        }

        public virtual void OnBindViewHolder(ViewHolder viewHolder, int index)
        {
            if (viewHolder == null) return;
            if (!TryGetBindData(index, out var data)) return;

            string viewName = GetViewName(index);
            viewHolder.AdvanceBindingVersion();
            viewHolder.DataIndex = index;
            if (TryGetOrCreateItemRender(viewHolder, viewName, out var itemRender))
            {
                if (itemRender is ITypedItemRender<T> typedItemRender)
                {
                    typedItemRender.BindData(data, index);
                }

                bool selected = index == choiceIndex;
                itemRender.SyncSelection(selected);
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Log.Error("RecyclerView item render is missing.");
#endif
        }

        public virtual void OnRecycleViewHolder(ViewHolder viewHolder)
        {
            if (viewHolder == null) return;

            if (TryGetItemRender(viewHolder, out var itemRender))
            {
                itemRender.Unbind();
            }
        }

        public virtual void NotifyDataChanged()
        {
            CoerceChoiceIndex();
            recyclerView.RequestLayout();
            recyclerView.Refresh();
        }

        public virtual void SetList(List<T> list)
        {
            this.list = list ?? new List<T>();
            recyclerView.Reset();
            NotifyDataChanged();
        }

        public virtual void NotifyItemChanged(int index, bool relayout = false)
        {
            if (index < 0 || index >= GetRealCount())
            {
                return;
            }

            CoerceChoiceIndex();
            if (relayout)
            {
                recyclerView.RequestLayout();
                recyclerView.Refresh();
                return;
            }

            recyclerView.RebindVisibleDataIndex(index);
        }

        public virtual void NotifyItemRangeChanged(int index, int count, bool relayout = false)
        {
            if (count <= 0 || index < 0 || index >= GetRealCount())
            {
                return;
            }

            CoerceChoiceIndex();
            if (relayout)
            {
                recyclerView.RequestLayout();
                recyclerView.Refresh();
                return;
            }

            recyclerView.RebindVisibleDataRange(index, count);
        }

        public virtual void NotifyItemInserted()
        {
            CoerceChoiceIndex();
            recyclerView.RequestLayout();
            recyclerView.Refresh();
        }

        public virtual void NotifyItemRangeInserted(int count)
        {
            if (count <= 0)
            {
                return;
            }

            CoerceChoiceIndex();
            recyclerView.RequestLayout();
            recyclerView.Refresh();
        }

        public virtual void NotifyItemRemoved()
        {
            CoerceChoiceIndex();
            recyclerView.RequestLayout();
            recyclerView.Refresh();
        }

        public virtual void NotifyItemRangeRemoved(int count)
        {
            if (count <= 0)
            {
                return;
            }

            CoerceChoiceIndex();
            recyclerView.RequestLayout();
            recyclerView.Refresh();
        }

        public void RegisterItemRender(Type itemRenderType)
        {
            var definition = ItemRenderResolver.GetOrCreate(itemRenderType);
            if (string.IsNullOrEmpty(definition.HolderTypeName))
            {
                ReleaseCachedItemRenders(string.Empty);
                defaultItemRenderDefinition = definition;
                return;
            }

            ReleaseCachedItemRenders(definition.HolderTypeName);
            itemRenderDefinitions[definition.HolderTypeName] = definition;
        }


        public bool UnregisterItemRender(Type itemRenderType)
        {
            if (itemRenderType == null)
            {
                return false;
            }

            if (defaultItemRenderDefinition != null && defaultItemRenderDefinition.ItemRenderType == itemRenderType)
            {
                defaultItemRenderDefinition = null;
                ReleaseCachedItemRenders(string.Empty);
                return true;
            }

            string removedViewName = null;
            foreach (var pair in itemRenderDefinitions)
            {
                if (pair.Value != null && pair.Value.ItemRenderType == itemRenderType)
                {
                    removedViewName = pair.Key;
                    break;
                }
            }

            if (removedViewName != null)
            {
                itemRenderDefinitions.Remove(removedViewName);
                ReleaseCachedItemRenders(removedViewName);
                return true;
            }

            return false;
        }

        public void ClearItemRenderRegistrations()
        {
            ReleaseAllItemRenders();
            itemRenderDefinitions.Clear();
            defaultItemRenderDefinition = null;
        }

        public T GetData(int index)
        {
            if (index < 0 || index >= GetItemCount()) return default;

            return list[index];
        }

        public void Add(T item)
        {
            if (list == null)
            {
                list = new List<T>();
            }

            list.Add(item);
            NotifyItemInserted();
        }

        public void AddRange(IEnumerable<T> collection)
        {
            if (collection == null)
            {
                return;
            }

            list.AddRange(collection);
            if (collection is ICollection<T> itemCollection)
            {
                NotifyItemRangeInserted(itemCollection.Count);
                return;
            }

            NotifyDataChanged();
        }

        public void Insert(int index, T item)
        {
            list.Insert(index, item);
            NotifyItemInserted();
        }

        public void InsertRange(int index, IEnumerable<T> collection)
        {
            if (collection == null)
            {
                return;
            }

            list.InsertRange(index, collection);
            if (collection is ICollection<T> itemCollection)
            {
                NotifyItemRangeInserted(itemCollection.Count);
                return;
            }

            NotifyDataChanged();
        }

        public void Remove(T item)
        {
            int index = list.IndexOf(item);
            RemoveAt(index);
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= GetItemCount()) return;

            list.RemoveAt(index);
            NotifyItemRemoved();
        }

        public void RemoveRange(int index, int count)
        {
            list.RemoveRange(index, count);
            NotifyItemRangeRemoved(count);
        }

        public void RemoveAll(Predicate<T> match)
        {
            list.RemoveAll(match);
            NotifyDataChanged();
        }

        public void Clear()
        {
            if (list == null || list.Count == 0)
            {
                return;
            }

            int count = list.Count;
            list.Clear();
            NotifyItemRangeRemoved(count);
        }

        public void Reverse(int index, int count)
        {
            list.Reverse(index, count);
            NotifyDataChanged();
        }

        public void Reverse()
        {
            list.Reverse();
            NotifyDataChanged();
        }

        public void Sort(Comparison<T> comparison)
        {
            list.Sort(comparison);
            NotifyDataChanged();
        }

        protected void SetChoiceIndex(int index)
        {
            int itemCount = GetRealCount();
            if (itemCount <= 0)
            {
                index = -1;
            }
            else if (index >= itemCount)
            {
                index = itemCount - 1;
            }
            else if (index < -1)
            {
                index = -1;
            }

            if (index == choiceIndex) return;
            int previousChoice = choiceIndex;

            if (choiceIndex != -1 && TryGetViewHolder(choiceIndex, out var oldHolder))
            {
                UpdateSelectionState(oldHolder, false);
            }

            choiceIndex = index;

            if (choiceIndex != -1 && TryGetViewHolder(choiceIndex, out var newHolder))
            {
                UpdateSelectionState(newHolder, true);
            }

            OnChoiceIndexChanged?.Invoke(choiceIndex);
        }

        protected virtual bool TryGetBindData(int index, out T data)
        {
            if (list == null || index < 0 || index >= list.Count)
            {
                data = default;
                return false;
            }

            data = list[index];
            return true;
        }

        private bool TryGetViewHolder(int index, out ViewHolder viewHolder)
        {
            viewHolder = recyclerView.ViewProvider.GetViewHolderByDataIndex(index);
            return viewHolder != null;
        }

        private static bool TryGetItemRender(ViewHolder viewHolder, out IItemRender itemRender)
        {
            itemRender = viewHolder != null ? viewHolder.CachedItemRender : null;
            return itemRender != null;
        }

        private static void ReleaseItemRender(ViewHolder viewHolder)
        {
            IItemRender itemRender = viewHolder != null ? viewHolder.CachedItemRender : null;
            if (itemRender == null)
            {
                return;
            }

            itemRender.Unbind();
            if (itemRender is ItemRenderBase itemRenderBase)
            {
                itemRenderBase.Detach();
            }

            viewHolder.CachedItemRender = null;
            viewHolder.CachedItemRenderViewName = null;
        }

        private void UpdateSelectionState(ViewHolder viewHolder, bool selected)
        {
            if (TryGetItemRender(viewHolder, out IItemRender itemRender))
            {
                itemRender.UpdateSelection(selected);
            }
        }

        private bool TryGetItemRenderDefinition(string viewName, out ItemRenderResolver.ItemRenderDefinition definition)
        {
            if (!string.IsNullOrEmpty(viewName) && itemRenderDefinitions.TryGetValue(viewName, out definition))
            {
                return definition != null;
            }

            if (string.IsNullOrEmpty(viewName) && defaultItemRenderDefinition == null && itemRenderDefinitions.Count == 1)
            {
                foreach (var pair in itemRenderDefinitions)
                {
                    definition = pair.Value;
                    return definition != null;
                }
            }

            definition = defaultItemRenderDefinition;
            return definition != null;
        }

        private bool TryGetOrCreateItemRender(ViewHolder viewHolder, string viewName, out IItemRender itemRender)
        {
            if (viewHolder == null)
            {
                itemRender = null;
                return false;
            }

            if (viewHolder.CachedItemRender != null)
            {
                if (string.Equals(viewHolder.CachedItemRenderViewName, viewName, StringComparison.Ordinal))
                {
                    itemRender = viewHolder.CachedItemRender;
                    return true;
                }

                ReleaseItemRender(viewHolder);
            }

            if (!TryGetItemRenderDefinition(viewName, out var definition))
            {
                itemRender = null;
                return false;
            }

            itemRender = definition.Create(viewHolder, recyclerView, this, SetChoiceIndex);
            if (itemRender == null)
            {
                return false;
            }

            viewHolder.CachedItemRender = itemRender;
            viewHolder.CachedItemRenderViewName = viewName;
            return true;
        }

        void IItemRenderCacheOwner.ReleaseAllItemRenders()
        {
            ReleaseAllItemRenders();
        }

        void IItemRenderPrewarmer.PrewarmItemRender(ViewHolder viewHolder, string viewName)
        {
            TryGetOrCreateItemRender(viewHolder, viewName, out _);
        }

        private void ReleaseAllItemRenders()
        {
            if (recyclerView?.ViewProvider == null)
            {
                return;
            }

            for (int i = 0; i < recyclerView.ViewProvider.VisibleCount; i++)
            {
                ReleaseItemRender(recyclerView.ViewProvider.GetVisibleViewHolder(i));
            }
        }

        private void ReleaseCachedItemRenders(string viewName)
        {
            if (recyclerView?.ViewProvider == null)
            {
                return;
            }

            for (int i = 0; i < recyclerView.ViewProvider.VisibleCount; i++)
            {
                ViewHolder viewHolder = recyclerView.ViewProvider.GetVisibleViewHolder(i);
                if (viewHolder == null || !string.Equals(viewHolder.CachedItemRenderViewName, viewName, StringComparison.Ordinal))
                {
                    continue;
                }

                ReleaseItemRender(viewHolder);
            }
        }

        private void CoerceChoiceIndex()
        {
            int itemCount = GetRealCount();
            if (itemCount <= 0)
            {
                SetChoiceIndex(-1);
                return;
            }

            if (choiceIndex >= itemCount)
            {
                SetChoiceIndex(itemCount - 1);
            }
        }
    }
}
