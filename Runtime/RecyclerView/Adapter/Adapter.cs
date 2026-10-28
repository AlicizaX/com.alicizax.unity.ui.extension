using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlicizaX.UI
{
    /// <summary>
    /// RecyclerView 普通列表适配器，负责维护数据源并驱动列表项绑定、刷新和选择状态。
    /// </summary>
    /// <typeparam name="T">列表数据类型。</typeparam>
    public class Adapter<T> : IAdapter where T : class, ISimpleViewData
    {
        /// <summary>
        /// 当前适配器绑定的 RecyclerView 组件。
        /// </summary>
        protected RecyclerView recyclerView;

        /// <summary>
        /// 当前适配器维护的业务数据列表。
        /// </summary>
        protected List<T> list;

        /// <summary>
        /// 当前业务选中索引，-1 表示没有选中项。
        /// </summary>
        protected int choiceIndex = -1;

        internal Action<int> OnChoiceIndexChanged;

        private readonly List<T> emptyList = new List<T>();
        private int dataRevision;

        /// <summary>
        /// 当前业务选中索引，赋值时会同步刷新可见项选中状态。
        /// </summary>
        public int ChoiceIndex
        {
            get => choiceIndex;
            set => SetChoiceIndex(value);
        }

        public int DataRevision => dataRevision;

        /// <summary>
        /// 创建普通列表适配器。
        /// </summary>
        /// <param name="recyclerView">要绑定的 RecyclerView 组件。</param>
        public Adapter(RecyclerView recyclerView) : this(recyclerView, null)
        {
        }

        /// <summary>
        /// 使用指定数据源创建普通列表适配器。
        /// </summary>
        /// <param name="recyclerView">要绑定的 RecyclerView 组件。</param>
        /// <param name="list">初始业务数据列表。</param>
        public Adapter(RecyclerView recyclerView, List<T> list)
        {
            this.recyclerView = recyclerView;
            SetSourceList(list);
        }

        /// <summary>
        /// 获取当前适配器对外展示的列表项数量。
        /// </summary>
        /// <returns>列表项数量。</returns>
        public virtual int GetItemCount()
        {
            return list.Count;
        }

        /// <summary>
        /// 获取真实业务数据数量，普通列表中与展示数量一致。
        /// </summary>
        /// <returns>真实业务数据数量。</returns>
        public virtual int GetRealCount()
        {
            return GetItemCount();
        }

        /// <summary>
        /// 获取指定索引使用的模板 ID。
        /// </summary>
        /// <param name="index">数据索引。</param>
        /// <returns>模板 ID。</returns>

        public virtual int GetTemplateId(int index)
        {
            return 0;
        }

        public virtual bool TryGetDeclaredLength(int index, out float length)
        {
            if (TryGetBindData(index, out T data) &&
                data is IMeasuredViewData measured &&
                measured.TryGetItemLength(out length))
            {
                return length > 0f;
            }

            length = 0f;
            return false;
        }

        /// <summary>
        /// 将指定索引的数据绑定到列表项视图。
        /// </summary>
        /// <param name="viewHolder">要绑定的列表项视图持有者。</param>
        /// <param name="index">数据索引。</param>
        public virtual void OnBindViewHolder(ViewHolder viewHolder, int index)
        {
            if (!TryGetBindData(index, out T data))
            {
                viewHolder.Bind(null, index);
                return;
            }

            viewHolder.Bind(data, index);
            viewHolder.ApplySelection(index == choiceIndex);
        }

        /// <summary>
        /// 通知列表数据整体发生变化，并重新布局刷新。
        /// </summary>
        public virtual void NotifyDataChanged()
        {
            Revise();
            SetChoiceIndex(choiceIndex);
            recyclerView.OnDataSetChanged();
            NotifyNavigationDataChanged();
        }

        /// <summary>
        /// 替换当前业务数据列表，并重置 RecyclerView 后刷新。
        /// </summary>
        public virtual void SetList(List<T> list, ListScrollPolicy scrollPolicy = ListScrollPolicy.KeepAnchor)
        {
            SetSourceList(list);
            Revise();
            recyclerView.Reset(scrollPolicy);
            NotifyDataChanged();
        }

        /// <summary>
        /// 绑定源列表。<c>null</c> 复用同一份空表，避免"清空"路径每次分配一个 List。
        /// </summary>
        protected void SetSourceList(List<T> source)
        {
            this.list = source ?? emptyList;
            if (ReferenceEquals(this.list, emptyList))
            {
                emptyList.Clear();
            }
        }

        /// <summary>
        /// 通知指定索引的数据发生变化。
        /// </summary>
        /// <param name="index">发生变化的数据索引。</param>
        /// <param name="relayout">是否需要重新布局；当尺寸或位置可能变化时传入 true。</param>
        public virtual void NotifyItemChanged(int index, bool relayout = false)
        {
            if ((uint)index >= (uint)GetRealCount())
            {
                return;
            }

            Revise();
            SetChoiceIndex(choiceIndex);
            if (relayout)
            {
                recyclerView.RelayoutDataIndex(index);
            }
            else
            {
                recyclerView.RebindVisibleDataIndex(index);
            }

            NotifyNavigationDataChanged();
        }

        /// <summary>
        /// 通知指定范围内的数据发生变化。
        /// </summary>
        /// <param name="index">起始数据索引。</param>
        /// <param name="count">变化的数据数量。</param>
        /// <param name="relayout">是否需要重新布局；当尺寸或位置可能变化时传入 true。</param>
        public virtual void NotifyItemRangeChanged(int index, int count, bool relayout = false)
        {
            if (count <= 0 || (uint)index >= (uint)GetRealCount())
            {
                return;
            }

            Revise();
            SetChoiceIndex(choiceIndex);
            if (relayout)
            {
                recyclerView.RelayoutDataRange(index, count);
            }
            else
            {
                recyclerView.RebindVisibleDataRange(index, count);
            }

            NotifyNavigationDataChanged();
        }

        /// <summary>
        /// 通知在指定索引插入了一项。调用前数据源必须已插入完成。
        /// </summary>
        public virtual void NotifyItemInserted(int index)
        {
            NotifyItemRangeInserted(index, 1);
        }

        public virtual void NotifyItemRangeInserted(int index, int count)
        {
            if (count <= 0 || index < 0)
            {
                return;
            }

            Revise();
            if (choiceIndex >= index)
            {
                SetChoiceInternal(choiceIndex + count);
            }

            recyclerView.ApplyItemsInserted(index, count);
            NotifyNavigationDataChanged();
        }

        public virtual void NotifyItemRemoved(int index)
        {
            NotifyItemRangeRemoved(index, 1);
        }

        public virtual void NotifyItemMoved(int fromIndex, int toIndex)
        {
            int itemCount = GetRealCount();
            if (fromIndex < 0 || toIndex < 0 || fromIndex >= itemCount || toIndex >= itemCount || fromIndex == toIndex)
            {
                return;
            }

            Revise();
            if (choiceIndex == fromIndex)
            {
                SetChoiceInternal(toIndex);
            }
            else if (fromIndex < toIndex && choiceIndex > fromIndex && choiceIndex <= toIndex)
            {
                SetChoiceInternal(choiceIndex - 1);
            }
            else if (toIndex < fromIndex && choiceIndex >= toIndex && choiceIndex < fromIndex)
            {
                SetChoiceInternal(choiceIndex + 1);
            }

            recyclerView.ApplyItemsMoved(fromIndex, toIndex);
            NotifyNavigationDataChanged();
        }

        /// <summary>
        /// 通知从 index 起移除了 count 项。调用前数据源必须已删除完成。
        /// </summary>
        public virtual void NotifyItemRangeRemoved(int index, int count)
        {
            if (count <= 0 || index < 0)
            {
                return;
            }

            Revise();
            if (choiceIndex >= index && choiceIndex < index + count)
            {
                SetChoiceInternal(-1);
            }
            else if (choiceIndex >= index + count)
            {
                SetChoiceInternal(choiceIndex - count);
            }

            recyclerView.ApplyItemsRemoved(index, count);
            NotifyNavigationDataChanged();
        }

        /// <summary>
        /// 当前业务数据的只读视图。
        /// </summary>
        public IReadOnlyList<T> Items => list;

        public T GetData(int index)
        {
            return TryGetBindData(index, out T data) ? data : default;
        }

        public virtual void ReplaceAt(int index, T item, bool relayout = false)
        {
            if ((uint)index >= (uint)list.Count)
            {
                return;
            }

            list[index] = item;
            Revise();
            NotifyItemChanged(index, relayout);
        }

        /// <summary>
        /// 向列表末尾添加一个业务数据项，并刷新列表。
        /// </summary>
        public virtual void Add(T item)
        {
            int index = list.Count;
            list.Add(item);
            Revise();
            NotifyItemInserted(index);
        }

        public virtual void AddRange(IEnumerable<T> collection)
        {
            if (collection == null)
            {
                return;
            }

            int index = list.Count;
            list.AddRange(collection);
            int added = list.Count - index;
            if (added > 0)
            {
                Revise();
                NotifyItemRangeInserted(index, added);
            }
        }

        /// <summary>
        /// 在指定索引插入一个业务数据项，并刷新列表。
        /// </summary>
        /// <param name="index">插入位置索引。</param>
        /// <param name="item">要插入的业务数据。</param>
        public virtual void Insert(int index, T item)
        {
            list.Insert(index, item);
            Revise();
            NotifyItemInserted(index);
        }

        public virtual void InsertRange(int index, IEnumerable<T> collection)
        {
            if (collection == null)
            {
                return;
            }

            int previousCount = list.Count;
            list.InsertRange(index, collection);
            int added = list.Count - previousCount;
            if (added > 0)
            {
                Revise();
                NotifyItemRangeInserted(index, added);
            }
        }

        /// <summary>
        /// 移除指定业务数据项，并刷新列表。
        /// </summary>
        /// <param name="item">要移除的业务数据。</param>
        public virtual void Remove(T item)
        {
            int index = list.IndexOf(item);
            if (index < 0)
            {
                return;
            }

            list.RemoveAt(index);
            Revise();
            NotifyItemRemoved(index);
        }

        public virtual void RemoveAt(int index)
        {
            if ((uint)index >= (uint)list.Count)
            {
                return;
            }

            list.RemoveAt(index);
            Revise();
            NotifyItemRemoved(index);
        }

        public virtual void RemoveRange(int index, int count)
        {
            if (count <= 0 || (uint)index >= (uint)list.Count)
            {
                return;
            }

            count = Mathf.Min(count, list.Count - index);
            list.RemoveRange(index, count);
            Revise();
            NotifyItemRangeRemoved(index, count);
        }

        /// <summary>
        /// 单趟压缩删除，避免逐项 <see cref="RemoveAt"/> 造成的 O(n²) 搬移与逐次重布局。
        /// 删除是散点，唯一正确的通知是一次整体刷新。
        /// </summary>
        public virtual void RemoveAll(Predicate<T> match)
        {
            if (match == null)
            {
                throw new ArgumentNullException(nameof(match));
            }

            T selected = choiceIndex >= 0 && choiceIndex < list.Count ? list[choiceIndex] : null;
            int write = 0;
            for (int read = 0; read < list.Count; read++)
            {
                T item = list[read];
                if (match(item))
                {
                    continue;
                }

                if (write != read)
                {
                    list[write] = item;
                }

                write++;
            }

            if (write == list.Count)
            {
                return;
            }

            list.RemoveRange(write, list.Count - write);
            Revise();
            choiceIndex = selected != null ? list.IndexOf(selected) : -1;
            NotifyDataChanged();
        }

        /// <summary>
        /// 清空所有业务数据，并刷新列表。
        /// </summary>
        public virtual void Clear()
        {
            if (list.Count == 0)
            {
                return;
            }

            int count = list.Count;
            list.Clear();
            Revise();
            SetChoiceInternal(-1);
            NotifyItemRangeRemoved(0, count);
        }

        /// <summary>
        /// 反转指定范围内的业务数据顺序，并刷新列表。
        /// </summary>
        /// <param name="index">起始数据索引。</param>
        /// <param name="count">要反转的数据数量。</param>
        public virtual void Reverse(int index, int count)
        {
            list.Reverse(index, count);
            Revise();
            NotifyDataChanged();
        }

        /// <summary>
        /// 反转全部业务数据顺序，并刷新列表。
        /// </summary>
        public virtual void Reverse()
        {
            list.Reverse();
            Revise();
            NotifyDataChanged();
        }

        public virtual void Sort(Comparison<T> comparison)
        {
            T selected = choiceIndex >= 0 && choiceIndex < list.Count ? list[choiceIndex] : null;
            list.Sort(comparison);
            Revise();
            choiceIndex = selected != null ? list.IndexOf(selected) : -1;
            NotifyDataChanged();
        }

        public virtual void Move(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex ||
                (uint)fromIndex >= (uint)list.Count ||
                (uint)toIndex >= (uint)list.Count)
            {
                return;
            }

            T item = list[fromIndex];
            list.RemoveAt(fromIndex);
            list.Insert(toIndex, item);
            Revise();
            NotifyItemMoved(fromIndex, toIndex);
        }

        /// <summary>
        /// 设置业务选中索引，并同步刷新旧选中项和新选中项的可见选中状态。
        /// </summary>
        /// <param name="index">目标选中索引；传入 -1 表示清除选择。</param>
        public void SetChoiceIndex(int index)
        {
            int itemCount = GetRealCount();
            int clamped = itemCount <= 0 ? -1 : Mathf.Clamp(index, -1, itemCount - 1);
            SetChoiceInternal(clamped);
        }

        /// <summary>
        /// 刷新指定索引可见列表项的选中状态。
        /// </summary>
        /// <param name="index">数据索引。</param>
        /// <param name="selected">是否选中。</param>
        protected void ApplySelection(int index, bool selected)
        {
            if (index < 0)
            {
                return;
            }

            recyclerView.ApplyVisibleSelection(index, selected);
        }

        /// <summary>
        /// 尝试获取指定索引用于绑定视图的数据。
        /// </summary>
        /// <param name="index">数据索引。</param>
        /// <param name="data">获取到的绑定数据。</param>
        /// <returns>如果成功获取绑定数据，则返回 true；否则返回 false。</returns>
        protected virtual bool TryGetBindData(int index, out T data)
        {
            if ((uint)index >= (uint)list.Count)
            {
                data = default;
                return false;
            }

            data = list[index];
            return true;
        }

        protected void SetChoiceInternal(int index)
        {
            if (index == choiceIndex)
            {
                return;
            }

            int previous = choiceIndex;
            choiceIndex = index;
            ApplySelection(previous, false);
            ApplySelection(choiceIndex, true);
            OnChoiceIndexChanged?.Invoke(choiceIndex);
        }

        protected void Revise()
        {
            unchecked
            {
                dataRevision = dataRevision == int.MaxValue ? 1 : dataRevision + 1;
            }
        }

#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
        private void NotifyNavigationDataChanged()
        {
            recyclerView?.NotifyNavigationDataChanged();
        }
#else
        private void NotifyNavigationDataChanged()
        {
        }
#endif
    }
}
