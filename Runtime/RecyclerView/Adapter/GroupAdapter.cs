using System;
using System.Collections.Generic;

namespace AlicizaX.UI
{
    public class GroupAdapter<TData> : Adapter<TData> where TData : class, IGroupViewData, new()
    {
        private readonly List<TData> showList = new();
        private readonly List<TData> previousShowList = new();
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

        public GroupAdapter(RecyclerView recyclerView, List<TData> list, int groupTemplateId) : base(recyclerView, list)
        {
            this.groupTemplateId = groupTemplateId;
        }

        public override int GetItemCount()
        {
            return showList.Count;
        }

        public override int GetRealCount()
        {
            return showList.Count;
        }

        public override bool UsesMultipleTemplates => true;

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

            BuildTypeIndex();
            RebuildShowList();
            RestoreChoiceIndex(selectedData);
            base.NotifyDataChanged();
        }

        public override void SetList(List<TData> list, ListScrollPolicy scrollPolicy = ListScrollPolicy.KeepAnchor)
        {
            TData selectedData = GetChoiceData();
            showList.Clear();
            groupsByType.Clear();
            groupOrder.Clear();
            itemCountByType.Clear();
            firstItemIndexByType.Clear();
            lastItemIndexByType.Clear();
            this.list = list ?? new List<TData>();
            recyclerView.Reset(scrollPolicy);
            NotifyDataChanged(selectedData);
        }

        public override void Add(TData item)
        {
            list.Add(item);
            InsertSourceItemIntoShowList(item);
        }

        public override void AddRange(IEnumerable<TData> collection)
        {
            if (collection == null)
            {
                return;
            }

            CaptureDisplaySnapshot();
            TData selectedData = GetChoiceData();
            int previousCount = list.Count;
            list.AddRange(collection);
            if (list.Count == previousCount)
            {
                previousShowList.Clear();
                return;
            }

            ApplyDisplayChanges(selectedData);
        }

        public override void Insert(int index, TData item)
        {
            list.Insert(index, item);
            InsertSourceItemIntoShowList(item);
        }

        public override void InsertRange(int index, IEnumerable<TData> collection)
        {
            if (collection == null)
            {
                return;
            }

            CaptureDisplaySnapshot();
            TData selectedData = GetChoiceData();
            int previousCount = list.Count;
            list.InsertRange(index, collection);
            if (list.Count == previousCount)
            {
                previousShowList.Clear();
                return;
            }

            ApplyDisplayChanges(selectedData);
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

            TData item = list[index];
            int displayIndex = IndexOfDisplayData(item);
            list.RemoveAt(index);
            RemoveSourceItemFromShowList(item, displayIndex);
        }

        public override void RemoveRange(int index, int count)
        {
            if (count <= 0 || index < 0 || index >= list.Count)
            {
                return;
            }

            count = Math.Min(count, list.Count - index);
            CaptureDisplaySnapshot();
            TData selectedData = GetChoiceData();
            list.RemoveRange(index, count);
            ApplyDisplayChanges(selectedData);
        }

        public override void RemoveAll(Predicate<TData> match)
        {
            if (match == null)
            {
                return;
            }

            CaptureDisplaySnapshot();
            TData selectedData = GetChoiceData();
            int previousCount = list.Count;
            list.RemoveAll(match);
            if (list.Count == previousCount)
            {
                previousShowList.Clear();
                return;
            }

            ApplyDisplayChanges(selectedData);
        }

        public override void Clear()
        {
            if (list.Count == 0)
            {
                return;
            }

            int displayCount = showList.Count;
            list.Clear();
            showList.Clear();
            groupsByType.Clear();
            groupOrder.Clear();
            itemCountByType.Clear();
            firstItemIndexByType.Clear();
            lastItemIndexByType.Clear();
            if (displayCount > 0)
            {
                NotifyItemRangeRemoved(0, displayCount);
            }
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

        public override void Sort(Comparison<TData> comparison)
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

            if (data.Expanded == expanded)
            {
                return true;
            }

            data.Expanded = expanded;
            if (expanded)
            {
                ExpandChildren(index, data.Type);
            }
            else
            {
                CollapseChildren(index);
            }

            // 分组头 Expanded 态需要重绑；子项已由结构增量处理
            NotifyItemChanged(index);
            return true;
        }

        private void ExpandChildren(int groupIndex, int type)
        {
            if (!firstItemIndexByType.TryGetValue(type, out int itemIndex))
            {
                return;
            }

            int insertAt = groupIndex + 1;
            int inserted = 0;
            while (itemIndex >= 0)
            {
                showList.Insert(insertAt + inserted, list[itemIndex]);
                inserted++;
                itemIndex = nextItemIndexes[itemIndex];
            }

            if (inserted > 0)
            {
                NotifyItemRangeInserted(insertAt, inserted);
            }
        }

        private void CollapseChildren(int groupIndex)
        {
            int removeStart = groupIndex + 1;
            int removeCount = 0;
            while (removeStart + removeCount < showList.Count &&
                   showList[removeStart + removeCount].TemplateId != groupTemplateId)
            {
                removeCount++;
            }

            if (removeCount <= 0)
            {
                return;
            }

            showList.RemoveRange(removeStart, removeCount);
            NotifyItemRangeRemoved(removeStart, removeCount);
        }

        public bool IsGroupIndex(int index)
        {
            return index >= 0 &&
                   index < showList.Count &&
                   showList[index].TemplateId == groupTemplateId;
        }

        public bool TryGetDisplayData(int index, out TData data)
        {
            return TryGetBindData(index, out data);
        }

        private void BuildTypeIndex()
        {
            itemCountByType.Clear();
            firstItemIndexByType.Clear();
            lastItemIndexByType.Clear();

            if (list.Count <= 0)
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

        private void InsertSourceItemIntoShowList(TData item)
        {
            if (item == null)
            {
                return;
            }

            BuildTypeIndex();
            if (!groupsByType.TryGetValue(item.Type, out TData group))
            {
                return;
            }

            int groupIndex = IndexOfDisplayData(group);
            if (groupIndex < 0)
            {
                showList.Add(group);
                groupIndex = showList.Count - 1;
                NotifyItemInserted(groupIndex);
                if (!group.Expanded)
                {
                    return;
                }
            }

            if (!group.Expanded)
            {
                return;
            }

            int insertAt = groupIndex + 1;
            while (insertAt < showList.Count && showList[insertAt].TemplateId != groupTemplateId)
            {
                insertAt++;
            }

            showList.Insert(insertAt, item);
            NotifyItemInserted(insertAt);
        }

        private void RemoveSourceItemFromShowList(TData item, int displayIndex)
        {
            BuildTypeIndex();
            if (displayIndex >= 0 && displayIndex < showList.Count)
            {
                showList.RemoveAt(displayIndex);
                NotifyItemRemoved(displayIndex);
            }

            if (item == null || itemCountByType.TryGetValue(item.Type, out int remaining) && remaining > 0)
            {
                return;
            }

            if (!groupsByType.TryGetValue(item.Type, out TData group))
            {
                return;
            }

            int groupIndex = IndexOfDisplayData(group);
            if (groupIndex < 0)
            {
                return;
            }

            showList.RemoveAt(groupIndex);
            groupsByType.Remove(item.Type);
            groupOrder.Remove(group);
            NotifyItemRemoved(groupIndex);
        }

        private void CaptureDisplaySnapshot()
        {
            previousShowList.Clear();
            previousShowList.AddRange(showList);
        }

        /// <summary>
        /// 批量改写数据源后统一重建索引与显示列表，并尽量只发出一次结构通知。
        /// RecyclerView 的增量结构通知要求每次通知都对应数据源的中间状态；合并后的区间若同时
        /// 包含移除与插入，则无法满足该前提，此时退回一次整体刷新以保证 Holder 绑定与最终数据一致。
        /// </summary>
        private void ApplyDisplayChanges(TData selectedData)
        {
            int previousChoiceIndex = choiceIndex;
            BuildTypeIndex();
            PruneEmptyGroups();
            RebuildShowList();
            choiceIndex = -1;
            DisplayChange change = NotifyDisplayChange();
            choiceIndex = previousChoiceIndex;
            RestoreChoiceIndex(selectedData);
            if (change == DisplayChange.Range)
            {
                RefreshVisibleChoice(previousChoiceIndex);
            }
            else if (change == DisplayChange.Replace)
            {
                base.NotifyDataChanged();
            }
        }

        private DisplayChange NotifyDisplayChange()
        {
            int oldCount = previousShowList.Count;
            int newCount = showList.Count;
            int maxPrefix = Math.Min(oldCount, newCount);
            int prefix = 0;
            while (prefix < maxPrefix && ReferenceEquals(previousShowList[prefix], showList[prefix]))
            {
                prefix++;
            }

            int suffix = 0;
            int maxSuffix = maxPrefix - prefix;
            while (suffix < maxSuffix &&
                   ReferenceEquals(previousShowList[oldCount - 1 - suffix], showList[newCount - 1 - suffix]))
            {
                suffix++;
            }

            int removedCount = oldCount - prefix - suffix;
            int insertedCount = newCount - prefix - suffix;
            previousShowList.Clear();
            if (removedCount == 0 && insertedCount == 0)
            {
                return DisplayChange.None;
            }

            if (removedCount == 0)
            {
                NotifyItemRangeInserted(prefix, insertedCount);
                return DisplayChange.Range;
            }

            if (insertedCount == 0)
            {
                NotifyItemRangeRemoved(prefix, removedCount);
                return DisplayChange.Range;
            }

            return DisplayChange.Replace;
        }

        private void RefreshVisibleChoice(int previousChoiceIndex)
        {
            if (previousChoiceIndex != choiceIndex && previousChoiceIndex >= 0)
            {
                ApplySelection(previousChoiceIndex, false);
            }

            if (choiceIndex >= 0)
            {
                ApplySelection(choiceIndex, true);
            }
        }

        private void PruneEmptyGroups()
        {
            for (int i = groupOrder.Count - 1; i >= 0; i--)
            {
                TData group = groupOrder[i];
                if (itemCountByType.TryGetValue(group.Type, out int count) && count > 0)
                {
                    continue;
                }

                groupsByType.Remove(group.Type);
                groupOrder.RemoveAt(i);
            }
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

        private enum DisplayChange
        {
            None,
            Range,
            Replace
        }
    }
}
