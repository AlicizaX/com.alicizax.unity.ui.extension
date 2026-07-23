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

        /// <summary>
        /// 当前业务选中索引，赋值时会同步刷新可见项选中状态。
        /// </summary>
        public int ChoiceIndex
        {
            get => choiceIndex;
            set => SetChoiceIndex(value);
        }

        internal Action<int> OnChoiceIndexChanged;

        /// <summary>
        /// 创建普通列表适配器。
        /// </summary>
        /// <param name="recyclerView">要绑定的 RecyclerView 组件。</param>
        public Adapter(RecyclerView recyclerView) : this(recyclerView, new List<T>())
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
            this.list = list ?? new List<T>();
        }

        /// <summary>
        /// 获取当前适配器对外展示的列表项数量。
        /// </summary>
        /// <returns>列表项数量。</returns>
        public virtual int GetItemCount()
        {
            return list == null ? 0 : list.Count;
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

        /// <summary>
        /// 将指定索引的数据绑定到列表项视图。
        /// </summary>
        /// <param name="viewHolder">要绑定的列表项视图持有者。</param>
        /// <param name="index">数据索引。</param>
        public virtual void OnBindViewHolder(ViewHolder viewHolder, int index)
        {
            if (viewHolder == null) return;
            if (!TryGetBindData(index, out var data) || data == null)
            {
                viewHolder.Bind(null, index);
                return;
            }

            viewHolder.Bind(data, index);
            viewHolder.ApplySelection(index == choiceIndex, true);
        }

        /// <summary>
        /// 当列表项视图被回收时调用。默认空实现；状态清理由 <see cref="ViewHolder.OnRecycled"/> 统一处理，避免双重 Clear。
        /// </summary>
        /// <param name="viewHolder">被回收的列表项视图持有者。</param>
        public virtual void OnRecycleViewHolder(ViewHolder viewHolder)
        {
        }

        /// <summary>
        /// 通知列表数据整体发生变化，并重新布局刷新。
        /// </summary>
        public virtual void NotifyDataChanged()
        {
            CoerceChoiceIndex();
            recyclerView.RequestLayout();
            recyclerView.Refresh();
#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
            NotifyNavigationDataChanged();
#endif
        }

        /// <summary>
        /// 替换当前业务数据列表，并重置 RecyclerView 后刷新。
        /// </summary>
        /// <param name="list">新的业务数据列表。</param>
        public virtual void SetList(List<T> list)
        {
            this.list = list ?? new List<T>();
            recyclerView.Reset();
            NotifyDataChanged();
        }

        /// <summary>
        /// 通知指定索引的数据发生变化。
        /// </summary>
        /// <param name="index">发生变化的数据索引。</param>
        /// <param name="relayout">是否需要重新布局；当尺寸或位置可能变化时传入 true。</param>
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
#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
                NotifyNavigationDataChanged();
#endif
                return;
            }

            recyclerView.RebindVisibleDataIndex(index);
#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
            NotifyNavigationDataChanged();
#endif
        }

        /// <summary>
        /// 通知指定范围内的数据发生变化。
        /// </summary>
        /// <param name="index">起始数据索引。</param>
        /// <param name="count">变化的数据数量。</param>
        /// <param name="relayout">是否需要重新布局；当尺寸或位置可能变化时传入 true。</param>
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
#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
                NotifyNavigationDataChanged();
#endif
                return;
            }

            recyclerView.RebindVisibleDataRange(index, count);
#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
            NotifyNavigationDataChanged();
#endif
        }

        /// <summary>
        /// 通知在列表末尾插入了一项（兼容旧调用）。
        /// </summary>
        public virtual void NotifyItemInserted()
        {
            int index = Mathf.Max(0, GetRealCount() - 1);
            NotifyItemRangeInserted(index, 1);
        }

        /// <summary>
        /// 通知在指定索引插入了一项。调用前数据源必须已插入完成。
        /// </summary>
        public virtual void NotifyItemInserted(int index)
        {
            NotifyItemRangeInserted(index, 1);
        }

        /// <summary>
        /// 通知从列表尾部向前插入了 count 项（兼容旧调用）。
        /// </summary>
        public virtual void NotifyItemRangeInserted(int count)
        {
            if (count <= 0)
            {
                return;
            }

            int index = Mathf.Max(0, GetRealCount() - count);
            NotifyItemRangeInserted(index, count);
        }

        /// <summary>
        /// 通知在 index 处插入了 count 项。调用前数据源必须已插入完成。
        /// </summary>
        public virtual void NotifyItemRangeInserted(int index, int count)
        {
            if (count <= 0 || index < 0)
            {
                return;
            }

            if (choiceIndex >= index)
            {
                choiceIndex += count;
                OnChoiceIndexChanged?.Invoke(choiceIndex);
            }

            CoerceChoiceIndex();
            recyclerView.ApplyItemsInserted(index, count);
#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
            NotifyNavigationDataChanged();
#endif
        }

        /// <summary>
        /// 通知移除了一项（兼容旧调用，等价全量结构刷新）。
        /// </summary>
        public virtual void NotifyItemRemoved()
        {
            NotifyDataChanged();
        }

        /// <summary>
        /// 通知移除了 index 处一项。调用前数据源必须已删除完成。
        /// </summary>
        public virtual void NotifyItemRemoved(int index)
        {
            NotifyItemRangeRemoved(index, 1);
        }

        /// <summary>
        /// 通知移除了一段（兼容旧调用，无 index 时只能全量刷新）。
        /// </summary>
        public virtual void NotifyItemRangeRemoved(int count)
        {
            if (count <= 0)
            {
                return;
            }

            NotifyDataChanged();
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

            if (choiceIndex >= index && choiceIndex < index + count)
            {
                choiceIndex = -1;
                OnChoiceIndexChanged?.Invoke(choiceIndex);
            }
            else if (choiceIndex >= index + count)
            {
                choiceIndex -= count;
                OnChoiceIndexChanged?.Invoke(choiceIndex);
            }

            CoerceChoiceIndex();
            recyclerView.ApplyItemsRemoved(index, count);
#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
            NotifyNavigationDataChanged();
#endif
        }

        /// <summary>
        /// 获取指定索引对应的绑定数据（与 OnBind / GetItemCount 同一语义）。
        /// </summary>
        /// <param name="index">显示/绑定索引。</param>
        /// <returns>如果索引有效则返回数据；否则返回 null。</returns>
        public T GetData(int index)
        {
            return TryGetBindData(index, out T data) ? data : default;
        }

        /// <summary>
        /// 向列表末尾添加一个业务数据项，并刷新列表。
        /// </summary>
        /// <param name="item">要添加的业务数据。</param>
        public virtual void Add(T item)
        {
            if (list == null)
            {
                list = new List<T>();
            }

            int index = list.Count;
            list.Add(item);
            NotifyItemInserted(index);
        }

        internal virtual void AddRange(IEnumerable<T> collection)
        {
            if (collection == null)
            {
                return;
            }

            if (list == null)
            {
                list = new List<T>();
            }

            int index = list.Count;
            if (collection is ICollection<T> itemCollection)
            {
                list.AddRange(itemCollection);
                NotifyItemRangeInserted(index, itemCollection.Count);
                return;
            }

            list.AddRange(collection);
            NotifyDataChanged();
        }

        /// <summary>
        /// 在指定索引插入一个业务数据项，并刷新列表。
        /// </summary>
        /// <param name="index">插入位置索引。</param>
        /// <param name="item">要插入的业务数据。</param>
        public virtual void Insert(int index, T item)
        {
            list.Insert(index, item);
            NotifyItemInserted(index);
        }

        internal virtual void InsertRange(int index, IEnumerable<T> collection)
        {
            if (collection == null)
            {
                return;
            }

            if (collection is ICollection<T> itemCollection)
            {
                list.InsertRange(index, itemCollection);
                NotifyItemRangeInserted(index, itemCollection.Count);
                return;
            }

            list.InsertRange(index, collection);
            NotifyDataChanged();
        }

        /// <summary>
        /// 移除指定业务数据项，并刷新列表。
        /// </summary>
        /// <param name="item">要移除的业务数据。</param>
        public virtual void Remove(T item)
        {
            int index = list.IndexOf(item);
            RemoveAt(index);
        }

        /// <summary>
        /// 移除指定索引处的业务数据项，并刷新列表。
        /// </summary>
        /// <param name="index">要移除的数据索引。</param>
        public virtual void RemoveAt(int index)
        {
            if (index < 0 || index >= GetRealCount()) return;

            list.RemoveAt(index);
            NotifyItemRemoved(index);
        }

        /// <summary>
        /// 移除指定范围内的业务数据项，并刷新列表。
        /// </summary>
        /// <param name="index">起始数据索引。</param>
        /// <param name="count">要移除的数据数量。</param>
        public virtual void RemoveRange(int index, int count)
        {
            list.RemoveRange(index, count);
            NotifyItemRangeRemoved(index, count);
        }

        internal virtual void RemoveAll(Predicate<T> match)
        {
            list.RemoveAll(match);
            NotifyDataChanged();
        }

        /// <summary>
        /// 清空所有业务数据，并刷新列表。
        /// </summary>
        public virtual void Clear()
        {
            if (list == null || list.Count == 0)
            {
                return;
            }

            int count = list.Count;
            list.Clear();
            NotifyItemRangeRemoved(count);
        }

        /// <summary>
        /// 反转指定范围内的业务数据顺序，并刷新列表。
        /// </summary>
        /// <param name="index">起始数据索引。</param>
        /// <param name="count">要反转的数据数量。</param>
        public virtual void Reverse(int index, int count)
        {
            list.Reverse(index, count);
            NotifyDataChanged();
        }

        /// <summary>
        /// 反转全部业务数据顺序，并刷新列表。
        /// </summary>
        public virtual void Reverse()
        {
            list.Reverse();
            NotifyDataChanged();
        }

        internal virtual void Sort(Comparison<T> comparison)
        {
            list.Sort(comparison);
            NotifyDataChanged();
        }

        /// <summary>
        /// 设置业务选中索引，并同步刷新旧选中项和新选中项的可见选中状态。
        /// </summary>
        /// <param name="index">目标选中索引；传入 -1 表示清除选择。</param>
        public void SetChoiceIndex(int index)
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

            int previousIndex = choiceIndex;
            choiceIndex = index;

            ApplySelection(previousIndex, false);
            ApplySelection(choiceIndex, true);
            OnChoiceIndexChanged?.Invoke(choiceIndex);
        }

        /// <summary>
        /// 刷新指定索引可见列表项的选中状态。
        /// </summary>
        /// <param name="index">数据索引。</param>
        /// <param name="selected">是否选中。</param>
        protected void ApplySelection(int index, bool selected)
        {
            if (recyclerView == null || index < 0)
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
            if (list == null || index < 0 || index >= list.Count)
            {
                data = default;
                return false;
            }

            data = list[index];
            return true;
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

#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
        private void NotifyNavigationDataChanged()
        {
            recyclerView?.NotifyNavigationDataChanged();
        }
#endif
    }
}
