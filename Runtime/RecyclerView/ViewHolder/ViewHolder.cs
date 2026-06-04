using System;
using Cysharp.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AlicizaX.UI
{
    /// <summary>
    /// RecyclerView 列表项视图持有者基类，负责承载单个可复用列表项的视图逻辑。
    /// </summary>
    public abstract class ViewHolder : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform rectTransform;
        private bool isBound;
        private bool isSelected;

        internal RectTransform RectTransform
        {
            get
            {
                if (rectTransform == null)
                {
                    rectTransform = GetComponent<RectTransform>();
                }

                return rectTransform;
            }
        }

        internal int TemplateId { get; set; } = -1;

        internal int Index { get; set; }

        internal int DataIndex { get; set; } = -1;

        internal RecyclerView RecyclerView { get; set; }

        internal uint BindingVersion { get; private set; }

        /// <summary>
        /// 当前列表项是否处于业务选中状态。
        /// </summary>
        protected bool IsSelected => isSelected;

        internal Vector2 SizeDelta => RectTransform.sizeDelta;

        internal uint AdvanceBindingVersion()
        {
            BindingVersion = BindingVersion == uint.MaxValue ? 1u : BindingVersion + 1u;
            return BindingVersion;
        }

        internal void Bind(ISimpleViewData data, int index)
        {
            if (data == null)
            {
                AdvanceBindingVersion();
                ClearFailedBinding();
                return;
            }

            AdvanceBindingVersion();
            DataIndex = index;
            if (!OnBind(data, index))
            {
                ClearFailedBinding();
                return;
            }

            isBound = true;
        }

        internal void ApplySelection(bool selected, bool force = false)
        {
            if (!force && isSelected == selected)
            {
                return;
            }

            isSelected = selected;
            OnSelectionChange(selected);
        }

        internal void SetPooledVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        internal void Clear()
        {
            ApplySelection(false);
            if (isBound)
            {
                OnClear();
            }

            isBound = false;
            ResetBindingState();
        }

        internal void OnRecycled()
        {
            Clear();
            AdvanceBindingVersion();
            TemplateId = -1;
            Index = -1;
            DataIndex = -1;
            RecyclerView = null;
        }

        private protected abstract bool OnBind(ISimpleViewData data, int index);

        private protected void ClearFailedBinding()
        {
            ApplySelection(false);
            if (isBound)
            {
                OnClear();
            }

            isBound = false;
            DataIndex = -1;
            ResetBindingState();
        }

        /// <summary>
        /// 将当前绑定的数据项设置为业务选中项，通常在点击或确认操作中主动调用。
        /// </summary>
        protected void SetSelect()
        {
            if (RecyclerView?.RecyclerViewAdapter == null || DataIndex < 0)
            {
                return;
            }

            RecyclerView.RecyclerViewAdapter.SetChoiceIndex(DataIndex);
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            //帮导航模块兼容做拦截事件....后续在考虑单独增加脚本绑定 或者自己fork下来改
        }

        public virtual void OnPointerUp(PointerEventData eventData)
        {
#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
            if (eventData.button != PointerEventData.InputButton.Left ||
                eventData.dragging ||
                !eventData.eligibleForClick ||
                !(this is IRecyclerViewNavigationViewHolder) ||
                RecyclerView == null ||
                DataIndex < 0)
            {
                return;
            }

            RecyclerViewNavigationController navigationController =
                RecyclerView.GetComponent<RecyclerViewNavigationController>();
            if (navigationController == null)
            {
                return;
            }

            navigationController.SetFocus(DataIndex, false);
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(navigationController.gameObject);
            }
#endif
        }

        /// <summary>
        /// 当当前列表项的业务选中状态发生变化时调用，可在子类中刷新选中态表现。
        /// </summary>
        /// <param name="select">是否选中。</param>
        protected virtual void OnSelectionChange(bool select)
        {
        }

        /// <summary>
        /// 当列表项被清理或回收前调用，可在子类中释放绑定数据、取消监听或重置视图状态。
        /// </summary>
        protected virtual void OnClear()
        {
        }

        private protected virtual void ResetBindingState()
        {
        }
    }

    /// <summary>
    /// 带强类型数据的 RecyclerView 列表项视图持有者基类。
    /// </summary>
    /// <typeparam name="TData">当前列表项绑定的数据类型。</typeparam>
    public abstract class ViewHolder<TData> : ViewHolder where TData : class, ISimpleViewData
    {
        /// <summary>
        /// 当前绑定到该列表项的数据对象。
        /// </summary>
        protected TData CurrentData { get; private set; }

        /// <summary>
        /// 当前绑定数据在业务数据列表中的索引。
        /// </summary>
        protected int CurrentIndex { get; private set; } = -1;

        /// <summary>
        /// 当前绑定版本号，可用于判断异步回调是否仍对应当前绑定数据。
        /// </summary>
        protected uint CurrentBindingVersion { get; private set; }

        private protected sealed override bool OnBind(ISimpleViewData data, int index)
        {
            if (data is not TData typedData)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                ClearFailedBinding();
                throw new InvalidOperationException(
                    ZString.Format("RecyclerView view holder '{0}' expects data '{1}', but got '{2}'.",
                        GetType().FullName,
                        typeof(TData).FullName,
                        data.GetType().FullName));
#else
                return false;
#endif
            }

            CurrentData = typedData;
            CurrentIndex = index;
            CurrentBindingVersion = BindingVersion;
            OnBind(typedData, index);
            return true;
        }

        /// <summary>
        /// 绑定业务数据到当前列表项视图。
        /// </summary>
        /// <param name="data">要绑定的业务数据。</param>
        /// <param name="index">数据在业务数据列表中的索引。</param>
        protected abstract void OnBind(TData data, int index);

        /// <summary>
        /// 判断传入的绑定版本号是否仍然对应当前列表项，常用于异步加载完成后的有效性校验。
        /// </summary>
        /// <param name="bindingVersion">需要校验的绑定版本号。</param>
        /// <returns>如果版本号仍对应当前绑定数据，则返回 true；否则返回 false。</returns>
        protected bool IsBindingCurrent(uint bindingVersion)
        {
            return CurrentBindingVersion != 0 &&
                   CurrentBindingVersion == bindingVersion &&
                   BindingVersion == bindingVersion;
        }

        private protected override void ResetBindingState()
        {
            CurrentData = default;
            CurrentIndex = -1;
            CurrentBindingVersion = 0;
        }
    }
}
