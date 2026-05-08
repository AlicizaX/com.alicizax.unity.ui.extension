using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace AlicizaX.UI
{
    public abstract class UGListBase<TData, TAdapter>
        where TAdapter : Adapter<TData>
        where TData : ISimpleViewData
    {
        protected readonly RecyclerView _recyclerView;
        protected readonly TAdapter _adapter;

        private List<TData> _datas;

        protected UGListBase(RecyclerView recyclerView, TAdapter adapter)
        {
            _recyclerView = recyclerView;
            _adapter = adapter;

            if (_recyclerView != null)
            {
                _recyclerView.SetAdapter(_adapter);
            }
        }

        public RecyclerView RecyclerView => _recyclerView;

        public TAdapter Adapter => _adapter;

        public List<TData> Data
        {
            get => _datas;
            set
            {
                _datas = value;
                _adapter.SetList(_datas);
            }
        }

        public int DataCount => _datas?.Count ?? 0;

        /// <summary>
        ///列表内的导航焦点
        /// </summary>
        public int FocusIndex => _recyclerView != null ? _recyclerView.FocusIndex : -1;

        /// <summary>
        ///内部滚动定位点/当前跟踪的索引
        ///这是针对布局、对齐和滚动跟随逻辑，而不是业务选择状态
        /// </summary>
        public int CurrentIndex => _recyclerView != null ? _recyclerView.CurrentIndex : -1;

        /// <summary>
        ///业务选择。
        ///这仅在显式提交(如单击/提交或直接赋值)时才会更改
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

        public bool HasFocus => FocusIndex >= 0;

        public bool HasCurrent => CurrentIndex >= 0;

        public bool HasChoice => ChoiceIndex >= 0;

        public float ScrollPosition => _recyclerView != null ? _recyclerView.GetScrollPosition() : 0f;

        /// <summary>
        /// 当导航焦点改变时引发
        /// </summary>
        public event Action<int> OnFocusIndexChanged
        {
            add
            {
                if (_recyclerView != null)
                {
                    _recyclerView.OnFocusIndexChanged += value;
                }
            }
            remove
            {
                if (_recyclerView != null)
                {
                    _recyclerView.OnFocusIndexChanged -= value;
                }
            }
        }

        /// <summary>
        /// 当内部跟踪的当前索引因焦点、滚动或对齐而更改时引发
        /// </summary>
        public event Action<int> OnCurrentIndexChanged
        {
            add
            {
                if (_recyclerView != null)
                {
                    _recyclerView.OnCurrentIndexChanged += value;
                }
            }
            remove
            {
                if (_recyclerView != null)
                {
                    _recyclerView.OnCurrentIndexChanged -= value;
                }
            }
        }

        /// <summary>
        /// 当业务选择更改时引发
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

        public void RegisterItemRender<TItemRender>() where TItemRender : ItemRenderBase
        {
            RegisterItemRender(typeof(TItemRender));
        }

        public void RegisterItemRender(Type itemRenderType)
        {
            _adapter.RegisterItemRender(itemRenderType);
        }

        public bool UnregisterItemRender<TItemRender>() where TItemRender : ItemRenderBase
        {
            return UnregisterItemRender(typeof(TItemRender));
        }

        public bool UnregisterItemRender(Type itemRenderType)
        {
            return _adapter.UnregisterItemRender(itemRenderType);
        }


        public void ClearItemRenderRegistrations()
        {
            _adapter.ClearItemRenderRegistrations();
        }

        public void ClearChoice()
        {
            ChoiceIndex = -1;
        }

        public bool TryFocus(int index, bool smooth = false, ScrollAlignment alignment = ScrollAlignment.Center)
        {
            return _recyclerView != null && _recyclerView.TryFocusIndex(index, smooth, alignment);
        }

        public bool TryFocusChoice(bool smooth = false, ScrollAlignment alignment = ScrollAlignment.Center)
        {
            int index = ChoiceIndex;
            return index >= 0 && TryFocus(index, smooth, alignment);
        }

        public bool CommitFocusToChoice()
        {
            int index = FocusIndex;
            if (index < 0)
            {
                return false;
            }

            ChoiceIndex = index;
            return true;
        }

        public bool TryFocusEntry(
            MoveDirection entryDirection,
            bool smooth = false,
            ScrollAlignment alignment = ScrollAlignment.Center)
        {
            return _recyclerView != null && _recyclerView.TryFocusEntry(entryDirection, smooth, alignment);
        }

        public void ScrollToIndex(int index, bool smooth = false)
        {
            _recyclerView?.ScrollTo(index, smooth);
        }

        public void ScrollTo(
            int index,
            ScrollAlignment alignment = ScrollAlignment.Start,
            float offset = 0f,
            bool smooth = false,
            float duration = 0.3f)
        {
            _recyclerView?.ScrollToWithAlignment(index, alignment, offset, smooth, duration);
        }

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

        public void ScrollToFocus(
            ScrollAlignment alignment = ScrollAlignment.Center,
            float offset = 0f,
            bool smooth = false,
            float duration = 0.3f)
        {
            int index = FocusIndex;
            if (index >= 0)
            {
                ScrollTo(index, alignment, offset, smooth, duration);
            }
        }

        public void ScrollToStart(int index, float offset = 0f, bool smooth = false, float duration = 0.3f)
        {
            ScrollTo(index, ScrollAlignment.Start, offset, smooth, duration);
        }

        public void ScrollToCenter(int index, float offset = 0f, bool smooth = false, float duration = 0.3f)
        {
            ScrollTo(index, ScrollAlignment.Center, offset, smooth, duration);
        }

        public void ScrollToEnd(int index, float offset = 0f, bool smooth = false, float duration = 0.3f)
        {
            ScrollTo(index, ScrollAlignment.End, offset, smooth, duration);
        }
    }

    public class UGList<TData> : UGListBase<TData, Adapter<TData>> where TData : ISimpleViewData
    {
        public UGList(RecyclerView recyclerView)
            : base(recyclerView, new Adapter<TData>(recyclerView))
        {
        }
    }

    public class UGGroupList<TData> : UGListBase<TData, GroupAdapter<TData>> where TData : class, IGroupViewData, new()
    {
        public UGGroupList(RecyclerView recyclerView, string groupViewName)
            : base(recyclerView, new GroupAdapter<TData>(recyclerView, groupViewName))
        {
        }

        public bool IsGroupIndex(int index)
        {
            return _adapter.IsGroupIndex(index);
        }

        public bool TryGetDisplayData(int index, out TData data)
        {
            return _adapter.TryGetDisplayData(index, out data);
        }

        public bool SetExpanded(int index, bool expanded)
        {
            return _adapter.SetExpanded(index, expanded);
        }

        public void Expand(int index)
        {
            _adapter.Expand(index);
        }

        public void Collapse(int index)
        {
            _adapter.Collapse(index);
        }

        public void Activate(int index)
        {
            _adapter.Activate(index);
        }
    }

    public class UGLoopList<TData> : UGListBase<TData, LoopAdapter<TData>> where TData : ISimpleViewData, new()
    {
        public UGLoopList(RecyclerView recyclerView)
            : base(recyclerView, new LoopAdapter<TData>(recyclerView))
        {
        }
    }

    public class UGMixedList<TData> : UGListBase<TData, MixedAdapter<TData>> where TData : IMixedViewData
    {
        public UGMixedList(RecyclerView recyclerView)
            : base(recyclerView, new MixedAdapter<TData>(recyclerView))
        {
        }
    }

    public static class UGListCreateHelper
    {
        public static UGList<TData> Create<TData>(RecyclerView recyclerView) where TData : ISimpleViewData
            => new UGList<TData>(recyclerView);

        public static UGGroupList<TData> CreateGroup<TData>(RecyclerView recyclerView, string groupViewName) where TData : class, IGroupViewData, new()
            => new UGGroupList<TData>(recyclerView, groupViewName);

        public static UGLoopList<TData> CreateLoop<TData>(RecyclerView recyclerView) where TData : ISimpleViewData, new()
            => new UGLoopList<TData>(recyclerView);

        public static UGMixedList<TData> CreateMixed<TData>(RecyclerView recyclerView) where TData : IMixedViewData
            => new UGMixedList<TData>(recyclerView);
    }
}
