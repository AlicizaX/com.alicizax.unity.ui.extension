using System;
using Cysharp.Text;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AlicizaX.UI
{
    /// <summary>
    /// RecyclerView 列表项视图持有者基类，负责承载单个可复用列表项的视图逻辑。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
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

        internal float AxisLength(Direction measureDirection)
        {
            return ItemMeasure.ReadAxisLength(RectTransform, measureDirection);
        }

        /// <summary>
        /// 一次性写入布局绑定信息，避免池/布局各写一半导致中间态。
        /// </summary>
        internal void SetTemplate(int templateId, int layoutIndex, int dataIndex, RecyclerView owner)
        {
            TemplateId = templateId;
            Index = layoutIndex;
            DataIndex = dataIndex;
            RecyclerView = owner;
        }

        internal void ShiftIndexes(int delta)
        {
            Index += delta;
            DataIndex += delta;
        }

        public void RequestResize()
        {
            RecyclerView?.MeasureBoundHolder(this);
        }

        internal uint AdvanceBindingVersion()
        {
            BindingVersion = BindingVersion == uint.MaxValue ? 1u : BindingVersion + 1u;
            return BindingVersion;
        }

        internal void Bind(ISimpleViewData data, int index)
        {
            AdvanceBindingVersion();
            DataIndex = index;
            if (data == null || !OnBind(data, index))
            {
                Clear();
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

        internal void ResetPooledTransform()
        {
            RectTransform.anchoredPosition3D = Vector3.zero;
            RectTransform.localRotation = Quaternion.identity;
            RectTransform.localScale = Vector3.one;
        }

        internal void Clear()
        {
            ReleaseBinding();
            DataIndex = -1;
        }

        internal void OnRecycled()
        {
#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
            RecyclerView?.NotifyHolderRecycled(this);
#endif
            ReleaseBinding();
            AdvanceBindingVersion();
            TemplateId = -1;
            Index = -1;
            DataIndex = -1;
            RecyclerView = null;
        }

        private void ReleaseBinding()
        {
            ApplySelection(false);
            if (isBound)
            {
                OnClear();
            }

            isBound = false;
            ResetBindingState();
        }

        private protected abstract bool OnBind(ISimpleViewData data, int index);

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
            // 导航模块的双击保护：按下由本类吞掉，抬起时统一转发。
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

            RecyclerViewNavigationController navigationController = RecyclerView.NavigationController;
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
                ReportTypeMismatch(data);
#endif
                return false;
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
            return CurrentBindingVersion == bindingVersion && BindingVersion == bindingVersion;
        }

        private protected override void ResetBindingState()
        {
            CurrentData = default;
            CurrentIndex = -1;
            CurrentBindingVersion = 0;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void ReportTypeMismatch(ISimpleViewData data)
        {
            // 一次绑定失败只报一次：类型不匹配是配置错误，不应在热路径里反复抛异常分配字符串。
            if (typeMismatchLogged)
            {
                return;
            }

            typeMismatchLogged = true;
            Log.Error(ZString.Format("RecyclerView view holder '{0}' expects data '{1}', but got '{2}'.",
                GetType().FullName,
                typeof(TData).FullName,
                data.GetType().FullName));
        }

        private bool typeMismatchLogged;
#endif
    }
}
