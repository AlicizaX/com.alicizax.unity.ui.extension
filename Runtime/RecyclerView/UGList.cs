using System;
using System.Collections.Generic;

namespace AlicizaX.UI
{
    /// <summary>
    /// RecyclerView 的业务层列表封装基类，负责管理数据、适配器、选择和滚动操作。
    /// </summary>
    /// <typeparam name="TData">列表数据类型。</typeparam>
    /// <typeparam name="TAdapter">列表适配器类型。</typeparam>
    public abstract class UGListBase<TData, TAdapter>
        where TAdapter : Adapter<TData>
        where TData : class, ISimpleViewData
    {
        /// <summary>
        /// 当前列表绑定的 RecyclerView 组件。
        /// </summary>
        protected readonly RecyclerView _recyclerView;

        /// <summary>
        /// 当前列表使用的适配器实例。
        /// </summary>
        protected readonly TAdapter _adapter;

        private List<TData> _datas;
        private readonly bool _adapterBound;

        /// <summary>
        /// 使用指定 RecyclerView 和适配器创建业务层列表封装。
        /// </summary>
        /// <param name="recyclerView">要绑定的 RecyclerView 组件。</param>
        /// <param name="adapter">列表使用的适配器实例。</param>
        protected UGListBase(RecyclerView recyclerView, TAdapter adapter)
        {
            _recyclerView = recyclerView;
            _adapter = adapter;

            if (_recyclerView != null &&
                ValidateAdapterLayoutCompatibility(_adapter, _recyclerView.LayoutManager))
            {
                _recyclerView.SetAdapter(_adapter);
                _adapterBound = ReferenceEquals(_recyclerView.RecyclerViewAdapter, _adapter);
            }
        }

        /// <summary>
        /// 当前列表绑定的 RecyclerView 组件。
        /// </summary>
        public RecyclerView RecyclerView => _recyclerView;

        /// <summary>
        /// 当前列表使用的适配器实例，可用于调用更底层的适配器能力。
        /// </summary>
        public TAdapter Adapter => _adapter;

        /// <summary>
        /// 当前业务数据列表；赋值后会同步到适配器并刷新列表。
        /// </summary>
        public List<TData> Data
        {
            get => _datas;
            set
            {
                _datas = value;
                if (_adapterBound)
                {
                    _adapter.SetList(_datas);
                }
            }
        }

        /// <summary>
        /// 当前业务数据数量。
        /// </summary>
        public int DataCount => _datas?.Count ?? 0;


        /// <summary>
        /// 当前业务选中索引；仅在显式提交、点击或直接赋值时更改。
        /// </summary>
        public int ChoiceIndex
        {
            get => _adapter != null ? _adapter.ChoiceIndex : -1;
            set
            {
                if (_adapter != null)
                {
                    _adapter.ChoiceIndex = value;
                }
            }
        }

        /// <summary>
        /// 当前是否存在有效的业务选中项。
        /// </summary>
        public bool HasChoice => ChoiceIndex >= 0;

        /// <summary>
        /// 当前滚动位置。
        /// </summary>
        public float ScrollPosition => _recyclerView != null ? _recyclerView.GetScrollPosition() : 0f;


        /// <summary>
        /// 当业务选中索引发生变化时触发。
        /// </summary>
        public event Action<int> OnChoiceIndexChanged
        {
            add
            {
                if (_adapter != null)
                {
                    _adapter.OnChoiceIndexChanged += value;
                }
            }
            remove
            {
                if (_adapter != null)
                {
                    _adapter.OnChoiceIndexChanged -= value;
                }
            }
        }

        /// <summary>
        /// 当滚动值发生变化时触发。
        /// </summary>
        public event Action<float> ScrollValueChanged
        {
            add
            {
                if (_recyclerView != null)
                {
                    _recyclerView.OnScrollValueChanged += value;
                }
            }
            remove
            {
                if (_recyclerView != null)
                {
                    _recyclerView.OnScrollValueChanged -= value;
                }
            }
        }

        /// <summary>
        /// 当滚动停止时触发。
        /// </summary>
        public event Action ScrollStopped
        {
            add
            {
                if (_recyclerView != null)
                {
                    _recyclerView.OnScrollStopped += value;
                }
            }
            remove
            {
                if (_recyclerView != null)
                {
                    _recyclerView.OnScrollStopped -= value;
                }
            }
        }

        /// <summary>
        /// 当用户拖拽滚动状态发生变化时触发。
        /// </summary>
        public event Action<bool> ScrollDraggingChanged
        {
            add
            {
                if (_recyclerView != null)
                {
                    _recyclerView.OnScrollDraggingChanged += value;
                }
            }
            remove
            {
                if (_recyclerView != null)
                {
                    _recyclerView.OnScrollDraggingChanged -= value;
                }
            }
        }

        /// <summary>
        /// 清除当前业务选中项。
        /// </summary>
        public void ClearChoice()
        {
            ChoiceIndex = -1;
        }

        /// <summary>
        /// 滚动到指定数据索引。
        /// </summary>
        /// <param name="index">目标数据索引。</param>
        /// <param name="smooth">是否使用平滑滚动。</param>
        public void ScrollToIndex(int index, bool smooth = false)
        {
            _recyclerView?.ScrollTo(index, smooth);
        }

        /// <summary>
        /// 按指定对齐方式滚动到目标数据索引。
        /// </summary>
        /// <param name="index">目标数据索引。</param>
        /// <param name="alignment">目标项在视口中的对齐方式。</param>
        /// <param name="offset">对齐后的附加偏移。</param>
        /// <param name="smooth">是否使用平滑滚动。</param>
        /// <param name="duration">平滑滚动时长，单位为秒。</param>
        public void ScrollTo(
            int index,
            ScrollAlignment alignment = ScrollAlignment.Start,
            float offset = 0f,
            bool smooth = false,
            float duration = 0.3f)
        {
            if (!CanScrollToWithAlignment())
            {
                return;
            }

            _recyclerView?.ScrollToWithAlignment(index, alignment, offset, smooth, duration);
        }

        /// <summary>
        /// 滚动到当前业务选中项。
        /// </summary>
        /// <param name="alignment">目标项在视口中的对齐方式。</param>
        /// <param name="offset">对齐后的附加偏移。</param>
        /// <param name="smooth">是否使用平滑滚动。</param>
        /// <param name="duration">平滑滚动时长，单位为秒。</param>
        public void ScrollToChoice(
            ScrollAlignment alignment = ScrollAlignment.Center,
            float offset = 0f,
            bool smooth = false,
            float duration = 0.3f)
        {
            int index = ChoiceIndex;
            if (index >= 0)
            {
                ScrollTo(index, alignment, offset, smooth, duration);
            }
        }

        /// <summary>
        /// 滚动到指定索引，并将目标项对齐到视口起始位置。
        /// </summary>
        /// <param name="index">目标数据索引。</param>
        /// <param name="offset">对齐后的附加偏移。</param>
        /// <param name="smooth">是否使用平滑滚动。</param>
        /// <param name="duration">平滑滚动时长，单位为秒。</param>
        public void ScrollToStart(int index, float offset = 0f, bool smooth = false, float duration = 0.3f)
        {
            ScrollTo(index, ScrollAlignment.Start, offset, smooth, duration);
        }

        /// <summary>
        /// 滚动到指定索引，并将目标项对齐到视口中心位置。
        /// </summary>
        /// <param name="index">目标数据索引。</param>
        /// <param name="offset">对齐后的附加偏移。</param>
        /// <param name="smooth">是否使用平滑滚动。</param>
        /// <param name="duration">平滑滚动时长，单位为秒。</param>
        public void ScrollToCenter(int index, float offset = 0f, bool smooth = false, float duration = 0.3f)
        {
            ScrollTo(index, ScrollAlignment.Center, offset, smooth, duration);
        }

        /// <summary>
        /// 滚动到指定索引，并将目标项对齐到视口结束位置。
        /// </summary>
        /// <param name="index">目标数据索引。</param>
        /// <param name="offset">对齐后的附加偏移。</param>
        /// <param name="smooth">是否使用平滑滚动。</param>
        /// <param name="duration">平滑滚动时长，单位为秒。</param>
        public void ScrollToEnd(int index, float offset = 0f, bool smooth = false, float duration = 0.3f)
        {
            ScrollTo(index, ScrollAlignment.End, offset, smooth, duration);
        }

        private static bool ValidateAdapterLayoutCompatibility(IAdapter adapter, LayoutManager layoutManager)
        {
            if (adapter == null || layoutManager == null)
            {
                return true;
            }

            Type adapterType = adapter.GetType();
            if (IsAdapterType(adapterType, typeof(LoopAdapter<>)))
            {
                if (layoutManager is MixedLayoutManager)
                {
                    LogIncompatibleAdapterLayout(adapterType, layoutManager,
                        "LoopAdapter cannot be used with MixedLayoutManager because it exposes a virtual item count.");
                    return false;
                }

                if (layoutManager is CircleLayoutManager)
                {
                    LogIncompatibleAdapterLayout(adapterType, layoutManager,
                        "LoopAdapter cannot be used with CircleLayoutManager until circle layout uses the real item count.");
                    return false;
                }
            }

            if (IsAdapterType(adapterType, typeof(MixedAdapter<>)) && layoutManager is not MixedLayoutManager)
            {
                LogIncompatibleAdapterLayout(adapterType, layoutManager,
                    "MixedAdapter requires MixedLayoutManager so template sizes and item positions stay consistent.");
                return false;
            }

            return true;
        }

        private static bool IsAdapterType(Type type, Type openGenericAdapterType)
        {
            while (type != null)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == openGenericAdapterType)
                {
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        private static void LogIncompatibleAdapterLayout(Type adapterType, LayoutManager layoutManager, string reason)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Log.Error(
                $"RecyclerView adapter/layout combination is invalid. Adapter={adapterType.Name}, Layout={layoutManager.GetType().Name}. {reason}");
#endif
        }

        private bool CanScrollToWithAlignment()
        {
            if (_recyclerView == null)
            {
                return false;
            }

            if (_recyclerView.LayoutManager is CircleLayoutManager)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Log.Warning("UGList alignment scrolling is not supported for CircleLayoutManager.");
#endif
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// 普通 RecyclerView 列表业务封装。
    /// </summary>
    /// <typeparam name="TData">列表数据类型。</typeparam>
    public class UGList<TData> : UGListBase<TData, Adapter<TData>> where TData : class, ISimpleViewData
    {
        /// <summary>
        /// 创建普通 RecyclerView 列表业务封装。
        /// </summary>
        /// <param name="recyclerView">要绑定的 RecyclerView 组件。</param>
        public UGList(RecyclerView recyclerView)
            : base(recyclerView, new Adapter<TData>(recyclerView))
        {
        }
    }

    /// <summary>
    /// 分组 RecyclerView 列表业务封装。
    /// </summary>
    /// <typeparam name="TData">分组列表数据类型。</typeparam>
    public class UGGroupList<TData> : UGListBase<TData, GroupAdapter<TData>> where TData : class, IGroupViewData, new()
    {
        /// <summary>
        /// 创建分组 RecyclerView 列表业务封装。
        /// </summary>
        /// <param name="recyclerView">要绑定的 RecyclerView 组件。</param>
        /// <param name="groupTemplateId">分组标题或分组节点使用的模板 ID。</param>
        public UGGroupList(RecyclerView recyclerView, int groupTemplateId)
            : base(recyclerView, new GroupAdapter<TData>(recyclerView, groupTemplateId))
        {
        }

        /// <summary>
        /// 判断指定显示索引是否为分组节点。
        /// </summary>
        /// <param name="index">要判断的显示索引。</param>
        /// <returns>如果该索引对应分组节点，则返回 true；否则返回 false。</returns>
        public bool IsGroupIndex(int index)
        {
            return _adapter.IsGroupIndex(index);
        }

        /// <summary>
        /// 尝试获取指定显示索引对应的数据。
        /// </summary>
        /// <param name="index">显示索引。</param>
        /// <param name="data">获取到的数据。</param>
        /// <returns>如果成功获取数据，则返回 true；否则返回 false。</returns>
        public bool TryGetDisplayData(int index, out TData data)
        {
            return _adapter.TryGetDisplayData(index, out data);
        }

        /// <summary>
        /// 设置指定分组的展开状态。
        /// </summary>
        /// <param name="index">分组显示索引。</param>
        /// <param name="expanded">是否展开。</param>
        /// <returns>如果展开状态成功变更，则返回 true；否则返回 false。</returns>
        public bool SetExpanded(int index, bool expanded)
        {
            return _adapter.SetExpanded(index, expanded);
        }

        /// <summary>
        /// 展开指定分组。
        /// </summary>
        /// <param name="index">分组显示索引。</param>
        public void Expand(int index)
        {
            _adapter.Expand(index);
        }

        /// <summary>
        /// 折叠指定分组。
        /// </summary>
        /// <param name="index">分组显示索引。</param>
        public void Collapse(int index)
        {
            _adapter.Collapse(index);
        }

        /// <summary>
        /// 激活指定索引对应的分组项或普通项。
        /// </summary>
        /// <param name="index">要激活的显示索引。</param>
        public void Activate(int index)
        {
            _adapter.Activate(index);
        }
    }

    /// <summary>
    /// 循环 RecyclerView 列表业务封装。
    /// </summary>
    /// <typeparam name="TData">列表数据类型。</typeparam>
    public class UGLoopList<TData> : UGListBase<TData, LoopAdapter<TData>> where TData : class, ISimpleViewData
    {
        /// <summary>
        /// 创建循环 RecyclerView 列表业务封装。
        /// </summary>
        /// <param name="recyclerView">要绑定的 RecyclerView 组件。</param>
        public UGLoopList(RecyclerView recyclerView)
            : base(recyclerView, new LoopAdapter<TData>(recyclerView))
        {
        }
    }

    /// <summary>
    /// 多模板 RecyclerView 列表业务封装。
    /// </summary>
    /// <typeparam name="TData">多模板列表数据类型。</typeparam>
    public class UGMixedList<TData> : UGListBase<TData, MixedAdapter<TData>> where TData : class, IMixedViewData
    {
        /// <summary>
        /// 创建多模板 RecyclerView 列表业务封装。
        /// </summary>
        /// <param name="recyclerView">要绑定的 RecyclerView 组件。</param>
        public UGMixedList(RecyclerView recyclerView)
            : base(recyclerView, new MixedAdapter<TData>(recyclerView))
        {
        }
    }

    /// <summary>
    /// UGList 业务封装创建辅助类。
    /// </summary>
    public static class UGListCreateHelper
    {
        /// <summary>
        /// 创建普通 RecyclerView 列表业务封装。
        /// </summary>
        /// <typeparam name="TData">列表数据类型。</typeparam>
        /// <param name="recyclerView">要绑定的 RecyclerView 组件。</param>
        /// <returns>普通列表业务封装实例。</returns>
        public static UGList<TData> Create<TData>(RecyclerView recyclerView) where TData : class, ISimpleViewData
            => new UGList<TData>(recyclerView);

        /// <summary>
        /// 创建分组 RecyclerView 列表业务封装。
        /// </summary>
        /// <typeparam name="TData">分组列表数据类型。</typeparam>
        /// <param name="recyclerView">要绑定的 RecyclerView 组件。</param>
        /// <param name="groupTemplateId">分组标题或分组节点使用的模板 ID。</param>
        /// <returns>分组列表业务封装实例。</returns>
        public static UGGroupList<TData> CreateGroup<TData>(RecyclerView recyclerView, int groupTemplateId) where TData : class, IGroupViewData, new()
            => new UGGroupList<TData>(recyclerView, groupTemplateId);

        /// <summary>
        /// 创建循环 RecyclerView 列表业务封装。
        /// </summary>
        /// <typeparam name="TData">列表数据类型。</typeparam>
        /// <param name="recyclerView">要绑定的 RecyclerView 组件。</param>
        /// <returns>循环列表业务封装实例。</returns>
        public static UGLoopList<TData> CreateLoop<TData>(RecyclerView recyclerView) where TData : class, ISimpleViewData
            => new UGLoopList<TData>(recyclerView);

        /// <summary>
        /// 创建多模板 RecyclerView 列表业务封装。
        /// </summary>
        /// <typeparam name="TData">多模板列表数据类型。</typeparam>
        /// <param name="recyclerView">要绑定的 RecyclerView 组件。</param>
        /// <returns>多模板列表业务封装实例。</returns>
        public static UGMixedList<TData> CreateMixed<TData>(RecyclerView recyclerView) where TData : class, IMixedViewData
            => new UGMixedList<TData>(recyclerView);
    }
}
