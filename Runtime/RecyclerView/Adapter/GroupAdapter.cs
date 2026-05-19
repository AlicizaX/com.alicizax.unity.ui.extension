using System;
using System.Collections.Generic;

namespace AlicizaX.UI
{
    public class GroupAdapter<TData> : Adapter<TData> where TData : class, IGroupViewData, new()
    {
        private readonly List<TData> showList = new();
        private readonly int groupTemplateId;
        private readonly Dictionary<int, TData> groupsByType = new();
        private readonly List<TData> groupOrder = new();
        private readonly Dictionary<int, int> itemCountByType = new();
        private readonly Dictionary<int, int> firstItemIndexByType = new();
        private readonly Dictionary<int, int> lastItemIndexByType = new();
        private int[] nextItemIndexes = Array.Empty<int>();

        public GroupAdapter(RecyclerView recyclerView, int groupTemplateId) : base(recyclerView)
        {
            this.groupTemplateId = groupTemplateId;
        }

        public GroupAdapter(RecyclerView recyclerView, List<TData> list) : base(recyclerView, list)
        {
        }

        public GroupAdapter(RecyclerView recyclerView) : base(recyclerView)
        {
        }

        public override int GetItemCount()
        {
            return showList.Count;
        }

        public override int GetRealCount()
        {
            return showList.Count;
        }

        public override int GetTemplateId(int index)
        {
            return index >= 0 && index < showList.Count
                ? showList[index].TemplateId
                : -1;
        }

        public override void NotifyDataChanged()
        {
            TData selectedData = GetChoiceData();
            NotifyDataChanged(selectedData);
        }

        private void NotifyDataChanged(TData selectedData)
        {
            if (groupTemplateId < 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Log.Error("GroupAdapter requires a non-negative groupTemplateId.");
#endif
                return;
            }

            if (list == null)
            {
                showList.Clear();
                groupsByType.Clear();
                groupOrder.Clear();
                itemCountByType.Clear();
                firstItemIndexByType.Clear();
                lastItemIndexByType.Clear();
                RestoreChoiceIndex(selectedData);
                base.NotifyDataChanged();
                return;
            }

            BuildTypeIndex();
            RebuildShowList();
            RestoreChoiceIndex(selectedData);
            base.NotifyDataChanged();
        }

        public override void SetList(List<TData> list)
        {
            TData selectedData = GetChoiceData();
            showList.Clear();
            groupsByType.Clear();
            groupOrder.Clear();
            itemCountByType.Clear();
            firstItemIndexByType.Clear();
            lastItemIndexByType.Clear();
            this.list = list ?? new List<TData>();
            recyclerView.Reset();
            NotifyDataChanged(selectedData);
        }

        public override void Add(TData item)
        {
            if (list == null)
            {
                list = new List<TData>();
            }

            TData selectedData = GetChoiceData();
            list.Add(item);
            NotifyDataChanged(selectedData);
        }

        internal override void AddRange(IEnumerable<TData> collection)
        {
            if (collection == null)
            {
                return;
            }

            TData selectedData = GetChoiceData();
            list.AddRange(collection);
            NotifyDataChanged(selectedData);
        }

        public override void Insert(int index, TData item)
        {
            TData selectedData = GetChoiceData();
            list.Insert(index, item);
            NotifyDataChanged(selectedData);
        }

        internal override void InsertRange(int index, IEnumerable<TData> collection)
        {
            if (collection == null)
            {
                return;
            }

            TData selectedData = GetChoiceData();
            list.InsertRange(index, collection);
            NotifyDataChanged(selectedData);
        }

        public override void Remove(TData item)
        {
            int index = list.IndexOf(item);
            RemoveAt(index);
        }

        public override void RemoveAt(int index)
        {
            if (index < 0 || index >= list.Count)
            {
                return;
            }

            TData selectedData = GetChoiceData();
            list.RemoveAt(index);
            NotifyDataChanged(selectedData);
        }

        public override void RemoveRange(int index, int count)
        {
            if (count <= 0)
            {
                return;
            }

            TData selectedData = GetChoiceData();
            list.RemoveRange(index, count);
            NotifyDataChanged(selectedData);
        }

        internal override void RemoveAll(Predicate<TData> match)
        {
            if (match == null)
            {
                return;
            }

            TData selectedData = GetChoiceData();
            list.RemoveAll(match);
            NotifyDataChanged(selectedData);
        }

        public override void Clear()
        {
            if (list == null || list.Count == 0)
            {
                return;
            }

            TData selectedData = GetChoiceData();
            list.Clear();
            NotifyDataChanged(selectedData);
        }

        public override void Reverse(int index, int count)
        {
            TData selectedData = GetChoiceData();
            list.Reverse(index, count);
            NotifyDataChanged(selectedData);
        }

        public override void Reverse()
        {
            TData selectedData = GetChoiceData();
            list.Reverse();
            NotifyDataChanged(selectedData);
        }

        internal override void Sort(Comparison<TData> comparison)
        {
            TData selectedData = GetChoiceData();
            list.Sort(comparison);
            NotifyDataChanged(selectedData);
        }

        private void CreateGroup(int type)
        {
            if (groupsByType.ContainsKey(type))
            {
                return;
            }

            TData groupData = new TData
            {
                TemplateId = groupTemplateId,
                Type = type
            };
            groupsByType[type] = groupData;
            groupOrder.Add(groupData);
        }

        public void Expand(int index)
        {
            SetExpanded(index, true);
        }

        public void Collapse(int index)
        {
            SetExpanded(index, false);
        }

        public bool SetExpanded(int index, bool expanded)
        {
            if (!TryGetDisplayData(index, out TData data) || !IsGroupIndex(index))
            {
                return false;
            }

            data.Expanded = expanded;
            NotifyDataChanged();
            return true;
        }

        public bool IsGroupIndex(int index)
        {
            return index >= 0 &&
                   index < showList.Count &&
                   showList[index].TemplateId == groupTemplateId;
        }

        public bool TryGetDisplayData(int index, out TData data)
        {
            if (list == null || index < 0 || index >= showList.Count)
            {
                data = default;
                return false;
            }

            data = showList[index];
            return true;
        }

        private void BuildTypeIndex()
        {
            itemCountByType.Clear();
            firstItemIndexByType.Clear();
            lastItemIndexByType.Clear();

            if (list == null || list.Count <= 0)
            {
                return;
            }

            EnsureNextItemCapacity(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                nextItemIndexes[i] = -1;
                int type = list[i].Type;
                CreateGroup(type);

                itemCountByType.TryGetValue(type, out int count);
                itemCountByType[type] = count + 1;

                if (lastItemIndexByType.TryGetValue(type, out int lastIndex))
                {
                    nextItemIndexes[lastIndex] = i;
                }
                else
                {
                    firstItemIndexByType[type] = i;
                }

                lastItemIndexByType[type] = i;
            }
        }

        private void RebuildShowList()
        {
            showList.Clear();
            for (int i = 0; i < groupOrder.Count; i++)
            {
                TData group = groupOrder[i];
                if (!itemCountByType.TryGetValue(group.Type, out int count) || count <= 0)
                {
                    continue;
                }

                showList.Add(group);
                if (group.Expanded)
                {
                    AddItemsForType(group.Type);
                }
            }
        }

        private void AddItemsForType(int type)
        {
            if (!firstItemIndexByType.TryGetValue(type, out int index))
            {
                return;
            }

            while (index >= 0)
            {
                showList.Add(list[index]);
                index = nextItemIndexes[index];
            }
        }

        private void EnsureNextItemCapacity(int count)
        {
            if (nextItemIndexes.Length >= count)
            {
                return;
            }

            int capacity = nextItemIndexes.Length > 0 ? nextItemIndexes.Length : 4;
            while (capacity < count)
            {
                capacity <<= 1;
            }

            nextItemIndexes = new int[capacity];
        }

        protected override bool TryGetBindData(int index, out TData data)
        {
            if (index < 0 || index >= showList.Count)
            {
                data = default;
                return false;
            }

            data = showList[index];
            return true;
        }

        public void Activate(int index)
        {
            if (index < 0 || index >= showList.Count)
            {
                return;
            }

            TData data = showList[index];
            if (IsGroupIndex(index))
            {
                SetExpanded(index, !data.Expanded);
                return;
            }

            SetChoiceIndex(index);
        }

        private TData GetChoiceData()
        {
            return choiceIndex >= 0 && choiceIndex < showList.Count
                ? showList[choiceIndex]
                : default;
        }

        private void RestoreChoiceIndex(TData selectedData)
        {
            int index = IndexOfDisplayData(selectedData);
            if (choiceIndex == index)
            {
                return;
            }

            choiceIndex = index;
            OnChoiceIndexChanged?.Invoke(choiceIndex);
        }

        private int IndexOfDisplayData(TData data)
        {
            if (EqualityComparer<TData>.Default.Equals(data, default))
            {
                return -1;
            }

            for (int i = 0; i < showList.Count; i++)
            {
                TData item = showList[i];
                if (ReferenceEquals(item, data) || EqualityComparer<TData>.Default.Equals(item, data))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
