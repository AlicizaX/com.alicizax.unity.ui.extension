using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlicizaX.UI
{
    /// <summary>
    /// 分组列表适配器：把"源数据列表"投影为"组头 + 组内项"的显示列表。
    ///
    /// 索引空间约定（与 <see cref="Adapter{T}"/> 一致，均为源列表索引）：
    /// <see cref="RemoveAt"/> / <see cref="Remove"/> / <see cref="ReplaceAt"/> / <see cref="Move"/> /
    /// <see cref="Insert"/> / <see cref="RemoveRange"/> 全部作用于源列表；显示列表由源顺序 + 分组规则派生。
    /// 因此跨组的 <see cref="Move"/> 不可表达，会直接抛出而不是悄悄改坏源数据。
    /// </summary>
    public class GroupAdapter<TData> : Adapter<TData> where TData : class, IGroupViewData, new()
    {
        private readonly List<TData> showList = new();
        private readonly List<TData> previousShowList = new();
        private readonly Dictionary<int, TData> groupsByType = new();
        private readonly List<TData> groupOrder = new();
        private readonly Dictionary<int, TypeStats> statsByType = new();
        private int[] nextItemIndexes = Array.Empty<int>();

        private readonly int groupTemplateId;

        public GroupAdapter(RecyclerView recyclerView, int groupTemplateId) : base(recyclerView)
        {
            this.groupTemplateId = ValidateGroupTemplateId(groupTemplateId);
        }

        public GroupAdapter(RecyclerView recyclerView, List<TData> list, int groupTemplateId) : base(recyclerView, list)
        {
            this.groupTemplateId = ValidateGroupTemplateId(groupTemplateId);
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
            NotifyDataChanged(base.GetData(choiceIndex));
        }

        public override void SetList(List<TData> list, ListScrollPolicy scrollPolicy = ListScrollPolicy.KeepAnchor)
        {
            TData selectedData = base.GetData(choiceIndex);
            showList.Clear();
            groupsByType.Clear();
            groupOrder.Clear();
            statsByType.Clear();
            SetSourceList(list);
            Revise();
            recyclerView.Reset(scrollPolicy);
            NotifyDataChanged(selectedData);
        }

        public override void Add(TData item)
        {
            RequireItem(item);
            int sourceIndex = list.Count;
            list.Add(item);
            Revise();
            AppendSourceItemToDisplay(item, sourceIndex);
        }

        public override void AddRange(IEnumerable<TData> collection)
        {
            if (collection == null)
            {
                return;
            }

            CaptureDisplaySnapshot();
            TData selectedData = base.GetData(choiceIndex);
            int previousCount = list.Count;
            list.AddRange(collection);
            if (list.Count == previousCount)
            {
                previousShowList.Clear();
                return;
            }

            Revise();
            ApplyDisplayChanges(selectedData);
        }

        public override void Insert(int index, TData item)
        {
            RequireItem(item);
            TData selectedData = base.GetData(choiceIndex);
            list.Insert(index, item);
            Revise();
            NotifyDataChanged(selectedData);
        }

        public override void InsertRange(int index, IEnumerable<TData> collection)
        {
            if (collection == null)
            {
                return;
            }

            CaptureDisplaySnapshot();
            TData selectedData = base.GetData(choiceIndex);
            int previousCount = list.Count;
            list.InsertRange(index, collection);
            if (list.Count == previousCount)
            {
                previousShowList.Clear();
                return;
            }

            Revise();
            ApplyDisplayChanges(selectedData);
        }

        public override void Remove(TData item)
        {
            RequireItem(item);
            int sourceIndex = list.IndexOf(item);
            if (sourceIndex < 0)
            {
                return;
            }

            RemoveAt(sourceIndex);
        }

        public override void RemoveAt(int index)
        {
            if ((uint)index >= (uint)list.Count)
            {
                return;
            }

            TData item = list[index];
            int displayIndex = IndexOfDisplayData(item);
            list.RemoveAt(index);
            Revise();
            RemoveSourceItemFromShowList(item, displayIndex);
        }

        public override void RemoveRange(int index, int count)
        {
            if (count <= 0 || (uint)index >= (uint)list.Count)
            {
                return;
            }

            count = Math.Min(count, list.Count - index);
            CaptureDisplaySnapshot();
            TData selectedData = base.GetData(choiceIndex);
            list.RemoveRange(index, count);
            Revise();
            ApplyDisplayChanges(selectedData);
        }

        public override void RemoveAll(Predicate<TData> match)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            CaptureDisplaySnapshot();
            TData selectedData = base.GetData(choiceIndex);
            int previousCount = list.Count;
            list.RemoveAll(match);
            if (list.Count == previousCount)
            {
                previousShowList.Clear();
                return;
            }

            Revise();
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
            statsByType.Clear();
            Revise();
            SetChoiceInternal(-1);
            if (displayCount > 0)
            {
                NotifyItemRangeRemoved(0, displayCount);
            }
        }

        public override void Reverse(int index, int count)
        {
            TData selectedData = base.GetData(choiceIndex);
            list.Reverse(index, count);
            Revise();
            NotifyDataChanged(selectedData);
        }

        public override void Reverse()
        {
            TData selectedData = base.GetData(choiceIndex);
            list.Reverse();
            Revise();
            NotifyDataChanged(selectedData);
        }

        public override void Sort(Comparison<TData> comparison)
        {
            TData selectedData = base.GetData(choiceIndex);
            list.Sort(comparison);
            Revise();
            NotifyDataChanged(selectedData);
        }

        /// <summary>
        /// 替换源列表中指定索引的数据。类型变化会改变显示顺序，此时整体刷新；否则只重绑该对象所在的显示行。
        /// 注意：通知必须使用显示索引，源索引在分组列表里不指向同一行。
        /// </summary>
        public override void ReplaceAt(int index, TData item, bool relayout = false)
        {
            RequireItem(item);
            if ((uint)index >= (uint)list.Count)
            {
                return;
            }

            TData previous = list[index];
            if (ReferenceEquals(previous, item))
            {
                return;
            }

            bool typeChanged = previous == null || previous.Type != item.Type;
            TData selectedData = choiceIndex == index ? item : base.GetData(choiceIndex);
            int displayIndex = IndexOfDisplayData(previous);
            list[index] = item;
            Revise();

            if (typeChanged)
            {
                NotifyDataChanged(selectedData);
                return;
            }

            if (displayIndex < 0)
            {
                // 该源项当前不在显示列表中（所在分组被折叠），没有行需要重绑。
                return;
            }

            // 显示列表持有的是同一批对象引用：必须同步替换，否则通知的行上还是旧数据。
            showList[displayIndex] = item;
            RestoreChoiceIndex(selectedData);
            NotifyItemChanged(displayIndex, relayout);
        }

        /// <summary>
        /// 在源列表内重排。显示顺序由分组决定，因此只允许在组内重排。
        /// </summary>
        public override void Move(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex ||
                (uint)fromIndex >= (uint)list.Count ||
                (uint)toIndex >= (uint)list.Count)
            {
                return;
            }

            TData moved = list[fromIndex];
            TData target = list[toIndex];
            if (moved.Type != target.Type)
            {
                throw new InvalidOperationException(
                    "GroupAdapter.Move only supports reordering inside one group; the display position is derived from the group order.");
            }

            list.RemoveAt(fromIndex);
            list.Insert(toIndex, moved);
            Revise();
            NotifyDataChanged(base.GetData(choiceIndex));
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
            if (!statsByType.TryGetValue(type, out TypeStats stats) || stats.First < 0)
            {
                return;
            }

            int insertAt = groupIndex + 1;
            int inserted = 0;
            int itemIndex = stats.First;
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

        public void Activate(int index)
        {
            if (!TryGetDisplayData(index, out TData data))
            {
                return;
            }

            if (data.TemplateId == groupTemplateId)
            {
                SetExpanded(index, !data.Expanded);
                return;
            }

            SetChoiceIndex(index);
        }

        protected override bool TryGetBindData(int index, out TData data)
        {
            if ((uint)index >= (uint)showList.Count)
            {
                data = default;
                return false;
            }

            data = showList[index];
            return true;
        }

        private void NotifyDataChanged(TData selectedData)
        {
            BuildTypeIndex();
            RebuildShowList();
            RestoreChoiceIndex(selectedData);
            base.NotifyDataChanged();
        }

        /// <summary>
        /// 全量重建类型索引。只在批量/结构性操作里使用；单条追加走 <see cref="AppendSourceItemToDisplay"/>。
        /// </summary>
        private void BuildTypeIndex()
        {
            statsByType.Clear();
            int count = list.Count;
            if (count <= 0)
            {
                return;
            }

            EnsureNextItemCapacity(count);
            for (int i = 0; i < count; i++)
            {
                nextItemIndexes[i] = -1;
                TData item = list[i];
                if (item == null)
                {
                    // 源列表里出现 null 是调用方错误，交给公开入口的校验拦，这里不再静默跳过。
                    Debug.Assert(false, "GroupAdapter: the source list contains a null item.");
                    continue;
                }

                int type = item.Type;
                statsByType.TryGetValue(type, out TypeStats stats);
                if (stats.Count == 0)
                {
                    stats.First = i;
                }
                else
                {
                    nextItemIndexes[stats.Last] = i;
                }

                stats.Last = i;
                stats.Count++;
                statsByType[type] = stats;
                EnsureGroup(type);
            }
        }

        private void RebuildShowList()
        {
            showList.Clear();
            for (int i = 0; i < groupOrder.Count; i++)
            {
                TData group = groupOrder[i];
                if (!statsByType.TryGetValue(group.Type, out TypeStats stats) || stats.Count <= 0)
                {
                    continue;
                }

                showList.Add(group);
                if (group.Expanded)
                {
                    AddItemsForType(stats);
                }
            }
        }

        private void AddItemsForType(TypeStats stats)
        {
            int index = stats.First;
            while (index >= 0)
            {
                showList.Add(list[index]);
                index = nextItemIndexes[index];
            }
        }

        /// <summary>
        /// 单条追加的 O(1) 结构更新：只改该类型的计数/首尾/链尾，再在组块末尾插入一次。
        /// </summary>
        private void AppendSourceItemToDisplay(TData item, int sourceIndex)
        {
            EnsureNextItemCapacity(sourceIndex + 1);
            nextItemIndexes[sourceIndex] = -1;

            statsByType.TryGetValue(item.Type, out TypeStats stats);
            if (stats.Count == 0)
            {
                stats.First = sourceIndex;
            }
            else
            {
                nextItemIndexes[stats.Last] = sourceIndex;
            }

            stats.Last = sourceIndex;
            stats.Count++;
            statsByType[item.Type] = stats;
            EnsureGroup(item.Type);

            TData group = groupsByType[item.Type];
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
            else if (!group.Expanded)
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

        private void EnsureGroup(int type)
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

            // 必须保留已有链表：单条 Add 是增量更新，扩容时丢链接会让展开/折叠走进死循环。
            Array.Resize(ref nextItemIndexes, capacity);
        }

        private void RemoveSourceItemFromShowList(TData item, int displayIndex)
        {
            if (displayIndex >= 0 && displayIndex < showList.Count)
            {
                showList.RemoveAt(displayIndex);
                NotifyItemRemoved(displayIndex);
            }

            if (item == null)
            {
                return;
            }

            BuildTypeIndex();
            if (statsByType.TryGetValue(item.Type, out TypeStats stats) && stats.Count > 0)
            {
                return;
            }

            // 该类型已无数据项：只把组头从显示列表摘掉，组对象保留，下次重新出现时展开态不丢。
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
            RebuildShowList();
            // 先静默清空，避免批量操作期间抛出中间的 -1 选中事件；结构通知后再按数据身份恢复。
            choiceIndex = -1;
            DisplayChange change = NotifyDisplayChange();
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

        private void RestoreChoiceIndex(TData selectedData)
        {
            int index = IndexOfDisplayData(selectedData);
            if (index == choiceIndex)
            {
                return;
            }

            SetChoiceInternal(index);
        }

        private int IndexOfDisplayData(TData data)
        {
            if (data == null)
            {
                return -1;
            }

            for (int i = 0; i < showList.Count; i++)
            {
                TData item = showList[i];
                if (ReferenceEquals(item, data))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int ValidateGroupTemplateId(int groupTemplateId)
        {
            if (groupTemplateId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(groupTemplateId), "GroupAdapter requires a non-negative group template id.");
            }

            return groupTemplateId;
        }

        private static void RequireItem(TData item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }
        }

        private struct TypeStats
        {
            public int Count;
            public int First;
            public int Last;
        }

        private enum DisplayChange
        {
            None,
            Range,
            Replace
        }
    }
}
