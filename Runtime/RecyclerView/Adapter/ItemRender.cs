using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Cysharp.Text;
using UnityEngine.EventSystems;

namespace AlicizaX.UI
{
    /// <summary>
    /// 定义 ItemRender 的基础绑定与解绑协议。
    /// </summary>
    internal interface IItemRender
    {
        /// <summary>
        /// 更新当前渲染实例的选中状态。
        /// </summary>
        /// <param name="selected">是否处于选中状态。</param>
        void UpdateSelection(bool selected);

        void SyncSelection(bool selected);

        /// <summary>
        /// 清理当前渲染实例上的绑定状态。
        /// </summary>
        void Unbind();
    }

    /// <summary>
    /// 定义带强类型数据绑定能力的 ItemRender 协议。
    /// </summary>
    /// <typeparam name="TData">列表数据类型。</typeparam>
    internal interface ITypedItemRender<in TData> : IItemRender
    {
        /// <summary>
        /// 使用强类型数据执行绑定。
        /// </summary>
        /// <param name="data">待绑定的数据对象。</param>
        /// <param name="index">当前数据索引。</param>
        void BindData(TData data, int index);
    }

    /// <summary>
    /// 提供 ItemRender 的公共基类，封装框架内部的绑定生命周期入口。
    /// </summary>
    public abstract class ItemRenderBase : IItemRender
    {
        /// <summary>
        /// 将渲染实例附加到指定的视图持有者。
        /// </summary>
        /// <param name="viewHolder">目标视图持有者。</param>
        /// <param name="recyclerView">所属的 RecyclerView。</param>
        /// <param name="adapter">当前使用的适配器。</param>
        /// <param name="selectionHandler">选中项变更回调。</param>
        internal abstract void Attach(ViewHolder viewHolder, RecyclerView recyclerView, IAdapter adapter, Action<int> selectionHandler);

        /// <summary>
        /// 将渲染实例从当前视图持有者上分离。
        /// </summary>
        internal abstract void Detach();

        /// <summary>
        /// 更新内部记录的选中状态。
        /// </summary>
        /// <param name="selected">是否处于选中状态。</param>
        internal abstract void UpdateSelectionInternal(bool selected);

        internal abstract void SyncSelectionInternal(bool selected);

        /// <summary>
        /// 清理当前绑定产生的临时状态。
        /// </summary>
        internal abstract void UnbindInternal();

        /// <summary>
        /// 由框架内部调用，更新当前渲染实例的选中状态。
        /// </summary>
        /// <param name="selected">是否处于选中状态。</param>
        void IItemRender.UpdateSelection(bool selected)
        {
            UpdateSelectionInternal(selected);
        }

        void IItemRender.SyncSelection(bool selected)
        {
            SyncSelectionInternal(selected);
        }

        /// <summary>
        /// 由框架内部调用，清理当前渲染实例的绑定状态。
        /// </summary>
        void IItemRender.Unbind()
        {
            UnbindInternal();
        }
    }

    /// <summary>
    /// 提供带强类型数据与视图持有者的列表项渲染基类。
    /// </summary>
    /// <typeparam name="TData">列表数据类型。</typeparam>
    /// <typeparam name="THolder">视图持有者类型。</typeparam>
    public abstract class ItemRender<TData, THolder> : ItemRenderBase, IItemInteractionHost, ITypedItemRender<TData>
        where THolder : ViewHolder
    {
        /// <summary>
        /// 当前持有者上的交互代理组件。
        /// </summary>
        /// <summary>
        /// 当前项被选中时的回调委托。
        /// </summary>
        private Action<int> selectionHandler;

        /// <summary>
        /// 上一次绑定到交互代理的交互标记。
        /// </summary>
        private ItemInteractionFlags cachedInteractionFlags;

        /// <summary>
        /// 标记交互代理是否已经完成当前配置绑定。
        /// </summary>
        private bool interactionBindingActive;

        /// <summary>
        /// 获取当前附加的强类型视图持有者。
        /// </summary>
        private THolder Holder { get; set; }

        protected THolder baseui
        {
            get => Holder;
        }

        /// <summary>
        /// 获取当前所属的 RecyclerView。
        /// </summary>
        protected RecyclerView RecyclerView { get; private set; }

        /// <summary>
        /// 获取当前所属的适配器。
        /// </summary>
        protected IAdapter Adapter { get; private set; }

        /// <summary>
        /// 获取当前绑定的数据对象。
        /// </summary>
        protected TData CurrentData { get; private set; }

        /// <summary>
        /// 获取当前绑定的数据索引。
        /// </summary>
        protected int CurrentIndex { get; private set; } = -1;

        /// <summary>
        /// 获取当前绑定的布局索引。
        /// </summary>
        protected int CurrentLayoutIndex { get; private set; } = -1;

        /// <summary>
        /// 获取当前项是否处于选中状态。
        /// </summary>
        protected bool IsSelected { get; private set; }

        /// <summary>
        /// 获取当前绑定版本号，用于校验异步回调是否仍然有效。
        /// </summary>
        protected uint CurrentBindingVersion { get; private set; }

        /// <summary>
        /// 获取当前渲染项支持的交互能力。
        /// </summary>
        public virtual ItemInteractionFlags InteractionFlags => Holder != null ? Holder.ItemInteractionFlags : ItemInteractionFlags.None;

        /// <summary>
        /// 由框架交互代理读取当前渲染项的交互能力。
        /// </summary>
        ItemInteractionFlags IItemInteractionHost.InteractionFlags => InteractionFlags;

        /// <summary>
        /// 获取键盘或手柄导航时采用的移动选项。
        /// </summary>
        protected virtual RecyclerNavigationOptions NavigationOptions => RecyclerNavigationOptions.Circular;


        /// <summary>
        /// 由框架内部调用，使用强类型数据执行绑定。
        /// </summary>
        /// <param name="data">待绑定的数据对象。</param>
        /// <param name="index">当前数据索引。</param>
        void ITypedItemRender<TData>.BindData(TData data, int index)
        {
            BindCore(data, index);
        }

        /// <summary>
        /// 更新内部选中状态并触发选中状态回调。
        /// </summary>
        /// <param name="selected">是否处于选中状态。</param>
        internal override void UpdateSelectionInternal(bool selected)
        {
            SetSelectionState(selected, true);
        }

        internal override void SyncSelectionInternal(bool selected)
        {
            SetSelectionState(selected, true);
        }

        /// <summary>
        /// 清理当前绑定数据关联的状态，并重置内部缓存。
        /// </summary>
        internal override void UnbindInternal()
        {
            if (Holder != null)
            {
                ClearSelectionState();
                OnClear();
                Holder.ClearInteractionHost();
                interactionBindingActive = false;
                cachedInteractionFlags = ItemInteractionFlags.None;

                Holder.DataIndex = -1;
            }

            CurrentData = default;
            CurrentIndex = -1;
            CurrentLayoutIndex = -1;
            CurrentBindingVersion = 0;
        }

        /// <summary>
        /// 判断指定绑定版本是否仍与当前持有者保持一致。
        /// </summary>
        /// <param name="bindingVersion">待校验的绑定版本号。</param>
        /// <returns>版本号仍然有效时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        protected bool IsBindingCurrent(uint bindingVersion)
        {
            return Holder != null &&
                   CurrentBindingVersion != 0 &&
                   CurrentBindingVersion == bindingVersion &&
                   Holder.BindingVersion == bindingVersion;
        }

        /// <summary>
        /// 执行一次完整的数据绑定流程。
        /// </summary>
        /// <param name="itemData">待绑定的强类型数据。</param>
        /// <param name="index">当前数据索引。</param>
        private void BindCore(TData itemData, int index)
        {
            EnsureHolder();
            CurrentData = itemData;
            CurrentIndex = index;
            CurrentLayoutIndex = Holder.Index;
            Holder.DataIndex = index;
            CurrentBindingVersion = Holder.BindingVersion;
            BindInteractionHostIfNeeded();
            OnBind(itemData, index);
        }

        private void SetSelectionState(bool selected, bool notify)
        {
            EnsureHolder();
            bool previousSelected = IsSelected;
            if (previousSelected == selected)
            {
                return;
            }

            IsSelected = selected;
            if (notify)
            {
                OnSelectionChanged(IsSelected);
            }
        }

        private void ClearSelectionState()
        {
            if (!IsSelected)
            {
                return;
            }

            IsSelected = false;
            OnSelectionChanged(false);
        }

        /// <summary>
        /// 每次当前持有者绑定到新的数据项时调用。
        /// 仅用于执行数据驱动的界面刷新，例如文本、图片与状态更新。
        /// 不要在此注册持有者级别的事件监听。
        /// </summary>
        /// <param name="data">当前绑定的数据对象。</param>
        /// <param name="index">当前数据索引。</param>
        protected abstract void OnBind(TData data, int index);

        /// <summary>
        /// 当当前渲染实例附加到持有者实例时调用。
        /// 这是持有者级生命周期，通常对同一组 render 与 holder 仅触发一次。
        /// 适合执行一次性的持有者初始化，例如注册按钮监听或挂接可复用交互组件。
        /// </summary>
        protected virtual void OnHolderAttached()
        {

        }

        /// <summary>
        /// 当当前渲染实例即将从持有者实例分离时调用。
        /// 这是持有者级清理生命周期，通常对同一组 render 与 holder 仅触发一次。
        /// 适合执行一次性的持有者清理，例如注销按钮监听或释放附加阶段缓存的引用。
        /// </summary>
        protected virtual void OnHolderDetached()
        {
        }

        /// <summary>
        /// 当当前项的选中状态发生变化时调用。
        /// 仅应在此更新选中态相关的界面表现。
        /// </summary>
        /// <param name="selected">当前是否处于选中状态。</param>
        protected virtual void OnSelectionChanged(bool selected)
        {
        }

        /// <summary>
        /// 每次当前数据绑定被清理时调用。
        /// 这是绑定级清理生命周期，在复用过程中可能被多次触发。
        /// 适合在此重置由当前绑定数据产生的临时界面状态。
        /// </summary>
        protected virtual void OnClear()
        {
        }

        /// <summary>
        /// 通知外部选中当前数据项。
        /// </summary>
        private void SelectCurrentItem()
        {
            if (CurrentIndex >= 0)
            {
                selectionHandler?.Invoke(CurrentIndex);
            }
        }

        /// <summary>
        /// 将当前渲染实例附加到指定持有者，并初始化上下文引用。
        /// </summary>
        /// <param name="viewHolder">目标视图持有者。</param>
        /// <param name="recyclerView">所属的 RecyclerView。</param>
        /// <param name="adapter">当前使用的适配器。</param>
        /// <param name="selectionHandler">选中项变更回调。</param>
        internal override void Attach(ViewHolder viewHolder, RecyclerView recyclerView, IAdapter adapter, Action<int> selectionHandler)
        {
            if (viewHolder == null)
            {
                throw new ArgumentNullException(nameof(viewHolder));
            }

            if (viewHolder is not THolder holder)
            {
                throw new InvalidOperationException(
                    ZString.Format("RecyclerView item render '{0}' expects holder '{1}', but got '{2}'.", GetType().FullName, typeof(THolder).FullName, viewHolder.GetType().FullName));
            }

            Holder = holder;
            RecyclerView = recyclerView;
            Adapter = adapter;
            this.selectionHandler = selectionHandler;
            interactionBindingActive = false;
            cachedInteractionFlags = ItemInteractionFlags.None;
            OnHolderAttached();
        }

        /// <summary>
        /// 将当前渲染实例从持有者上分离，并释放上下文引用。
        /// </summary>
        internal override void Detach()
        {
            if (Holder == null)
            {
                return;
            }

            OnHolderDetached();
            Holder.ClearInteractionHost();
            selectionHandler = null;
            Holder = null;
            RecyclerView = null;
            Adapter = null;
            CurrentData = default;
            CurrentIndex = -1;
            CurrentLayoutIndex = -1;
            CurrentBindingVersion = 0;
            IsSelected = false;
            interactionBindingActive = false;
            cachedInteractionFlags = ItemInteractionFlags.None;
        }

        /// <summary>
        /// 确保当前渲染实例已经绑定有效的视图持有者。
        /// </summary>
        private void EnsureHolder()
        {
            if (Holder == null)
            {
                throw new InvalidOperationException(
                    ZString.Format("RecyclerView item render '{0}' has not been initialized with a holder.", GetType().FullName));
            }
        }

        /// <summary>
        /// 按指定方向尝试移动焦点。
        /// </summary>
        /// <param name="direction">焦点移动方向。</param>
        /// <returns>成功移动焦点时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        private bool MoveFocus(MoveDirection direction)
        {
#if UX_NAVIGATION
            return RecyclerView != null && RecyclerView.NavigationController.TryMove(Holder, direction, NavigationOptions);
#else
            return false;
#endif
        }

        /// <summary>
        /// 在需要时将当前渲染实例绑定到交互代理。
        /// </summary>
        private void BindInteractionHostIfNeeded()
        {
            ItemInteractionFlags interactionFlags = InteractionFlags;
            if (interactionBindingActive && cachedInteractionFlags == interactionFlags)
            {
                return;
            }

            Holder.BindInteractionHost(this);
            cachedInteractionFlags = interactionFlags;
            interactionBindingActive = true;
        }

        /// <summary>
        /// 由交互代理转发点击事件。
        /// </summary>
        /// <param name="eventData">点击事件数据。</param>
        void IItemInteractionHost.HandlePointerClick(PointerEventData eventData)
        {
            SelectCurrentItem();
            OnPointerClick(eventData);
        }

        /// <summary>
        /// 由交互代理转发指针进入事件。
        /// </summary>
        /// <param name="eventData">指针事件数据。</param>
        void IItemInteractionHost.HandlePointerEnter(PointerEventData eventData)
        {
            OnPointerEnter(eventData);
        }

        /// <summary>
        /// 由交互代理转发指针离开事件。
        /// </summary>
        /// <param name="eventData">指针事件数据。</param>
        void IItemInteractionHost.HandlePointerExit(PointerEventData eventData)
        {
            OnPointerExit(eventData);
        }

        /// <summary>
        /// 由交互代理转发选中事件。
        /// </summary>
        /// <param name="eventData">选中事件数据。</param>
        void IItemInteractionHost.HandleSelect(BaseEventData eventData)
        {
            if (RecyclerView != null && CurrentIndex >= 0)
            {
                RecyclerView.UpdateFocusIndex(CurrentIndex);
                RecyclerView.UpdateCurrentIndex(CurrentIndex);
            }

            OnItemSelected(eventData);
        }

        /// <summary>
        /// 由交互代理转发取消选中事件。
        /// </summary>
        /// <param name="eventData">取消选中事件数据。</param>
        void IItemInteractionHost.HandleDeselect(BaseEventData eventData)
        {
            OnItemDeselected(eventData);
        }

        /// <summary>
        /// 由交互代理转发导航移动事件。
        /// </summary>
        /// <param name="eventData">导航事件数据。</param>
        void IItemInteractionHost.HandleMove(AxisEventData eventData)
        {
            if (!OnMove(eventData))
            {
                MoveFocus(eventData.moveDir);
            }
        }


        /// <summary>
        /// 由交互代理转发提交事件。
        /// </summary>
        /// <param name="eventData">提交事件数据。</param>
        void IItemInteractionHost.HandleSubmit(BaseEventData eventData)
        {
            SelectCurrentItem();
            OnSubmit(eventData);
        }

        /// <summary>
        /// 由交互代理转发取消事件。
        /// </summary>
        /// <param name="eventData">取消事件数据。</param>
        void IItemInteractionHost.HandleCancel(BaseEventData eventData)
        {
            OnCancel(eventData);
        }

        /// <summary>
        /// 当当前项收到点击事件时调用。
        /// </summary>
        /// <param name="eventData">点击事件数据。</param>
        protected virtual void OnPointerClick(PointerEventData eventData)
        {
        }

        /// <summary>
        /// 当指针进入当前项时调用。
        /// </summary>
        /// <param name="eventData">指针事件数据。</param>
        protected virtual void OnPointerEnter(PointerEventData eventData)
        {
        }

        /// <summary>
        /// 当指针离开当前项时调用。
        /// </summary>
        /// <param name="eventData">指针事件数据。</param>
        protected virtual void OnPointerExit(PointerEventData eventData)
        {
        }

        /// <summary>
        /// 当当前项被 EventSystem 选中时调用。
        /// </summary>
        /// <param name="eventData">选中事件数据。</param>
        protected virtual void OnItemSelected(BaseEventData eventData)
        {
        }

        /// <summary>
        /// 当当前项被 EventSystem 取消选中时调用。
        /// </summary>
        /// <param name="eventData">取消选中事件数据。</param>
        protected virtual void OnItemDeselected(BaseEventData eventData)
        {
        }

        /// <summary>
        /// 当当前项收到导航移动事件时调用。
        /// </summary>
        /// <param name="eventData">导航事件数据。</param>
        /// <returns>已自行处理导航事件时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        protected virtual bool OnMove(AxisEventData eventData)
        {
            return false;
        }


        /// <summary>
        /// 当当前项收到提交操作时调用。
        /// </summary>
        /// <param name="eventData">提交事件数据。</param>
        protected virtual void OnSubmit(BaseEventData eventData)
        {
        }

        /// <summary>
        /// 当当前项收到取消操作时调用。
        /// </summary>
        /// <param name="eventData">取消事件数据。</param>
        protected virtual void OnCancel(BaseEventData eventData)
        {
        }
    }

    /// <summary>
    /// 负责解析、缓存并创建 ItemRender 定义。
    /// </summary>
    internal static class ItemRenderResolver
    {
        /// <summary>
        /// 描述单个 ItemRender 的类型信息与创建方式。
        /// </summary>
        internal sealed class ItemRenderDefinition
        {
            /// <summary>
            /// 初始化一份 ItemRender 定义。
            /// </summary>
            /// <param name="itemRenderType">渲染器运行时类型。</param>
            /// <param name="holderType">对应的持有者类型。</param>
            /// <param name="createInstance">渲染器实例创建委托。</param>
            public ItemRenderDefinition(Type itemRenderType, Type holderType, Func<IItemRender> createInstance)
            {
                ItemRenderType = itemRenderType;
                HolderType = holderType;
                HolderTypeName = holderType.Name;
                this.createInstance = createInstance;
            }

            public string HolderTypeName { get; private set; }
            /// <summary>
            /// 获取渲染器运行时类型。
            /// </summary>
            public Type ItemRenderType { get; }

            /// <summary>
            /// 获取渲染器要求的持有者类型。
            /// </summary>
            public Type HolderType { get; }

            /// <summary>
            /// 用于创建渲染器实例的缓存委托。
            /// </summary>
            private readonly Func<IItemRender> createInstance;

            /// <summary>
            /// 创建并初始化一个可用的 ItemRender 实例。
            /// </summary>
            /// <param name="viewHolder">目标视图持有者。</param>
            /// <param name="recyclerView">所属的 RecyclerView。</param>
            /// <param name="adapter">当前使用的适配器。</param>
            /// <param name="selectionHandler">选中项变更回调。</param>
            /// <returns>已初始化完成的渲染器实例。</returns>
            public IItemRender Create(ViewHolder viewHolder, RecyclerView recyclerView, IAdapter adapter, Action<int> selectionHandler)
            {
                if (viewHolder == null)
                {
                    return null;
                }

                if (!HolderType.IsInstanceOfType(viewHolder))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Log.Error(ZString.Format("RecyclerView item render '{0}' expects holder '{1}', but got '{2}'.", ItemRenderType.FullName, HolderType.FullName, viewHolder.GetType().FullName));
#endif
                    return null;
                }

                if (createInstance() is not ItemRenderBase itemRender)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Log.Error(ZString.Format("RecyclerView item render '{0}' could not be created.", ItemRenderType.FullName));
#endif
                    return null;
                }

                itemRender.Attach(viewHolder, recyclerView, adapter, selectionHandler);
                return itemRender;
            }
        }

        /// <summary>
        /// ItemRender 定义缓存表，键为渲染器类型。
        /// </summary>
        private static readonly Dictionary<Type, ItemRenderDefinition> Definitions = new();

        /// <summary>
        /// 获取指定渲染器类型对应的定义，不存在时自动创建并缓存。
        /// </summary>
        /// <param name="itemRenderType">渲染器运行时类型。</param>
        /// <returns>与该类型对应的渲染器定义。</returns>
        public static ItemRenderDefinition GetOrCreate(Type itemRenderType)
        {
            if (itemRenderType == null)
            {
                throw new ArgumentNullException(nameof(itemRenderType));
            }

            if (Definitions.TryGetValue(itemRenderType, out ItemRenderDefinition definition))
            {
                return definition;
            }

            definition = CreateDefinition(itemRenderType);
            Definitions[itemRenderType] = definition;
            return definition;
        }

        /// <summary>
        /// 为指定渲染器类型构建定义信息。
        /// </summary>
        /// <param name="itemRenderType">渲染器运行时类型。</param>
        /// <returns>创建完成的渲染器定义。</returns>
        private static ItemRenderDefinition CreateDefinition(Type itemRenderType)
        {
            if (itemRenderType.IsAbstract ||
                itemRenderType.IsInterface ||
                itemRenderType.ContainsGenericParameters ||
                !typeof(IItemRender).IsAssignableFrom(itemRenderType))
            {
                throw new InvalidOperationException(
                    ZString.Format("RecyclerView item render type '{0}' is invalid.", itemRenderType.FullName));
            }

            if (!TryGetHolderType(itemRenderType, out Type holderType))
            {
                throw new InvalidOperationException(
                    ZString.Format("RecyclerView item render '{0}' must inherit from ItemRender<TData, THolder>.", itemRenderType.FullName));
            }

            ConstructorInfo constructor = itemRenderType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

            if (constructor == null)
            {
                throw new InvalidOperationException(
                    ZString.Format("RecyclerView item render '{0}' must have a parameterless constructor.", itemRenderType.FullName));
            }

            return new ItemRenderDefinition(itemRenderType, holderType, CreateFactory(constructor));
        }

        /// <summary>
        /// 尝试从渲染器继承链中解析对应的持有者类型。
        /// </summary>
        /// <param name="itemRenderType">渲染器运行时类型。</param>
        /// <param name="holderType">解析得到的持有者类型。</param>
        /// <returns>解析成功时返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
        private static bool TryGetHolderType(Type itemRenderType, out Type holderType)
        {
            for (Type current = itemRenderType; current != null && current != typeof(object); current = current.BaseType)
            {
                if (current.IsGenericType &&
                    current.GetGenericTypeDefinition() == typeof(ItemRender<,>))
                {
                    Type[] arguments = current.GetGenericArguments();
                    holderType = arguments[1];
                    return true;
                }
            }

            holderType = null;
            return false;
        }

        /// <summary>
        /// 基于无参构造函数创建渲染器实例工厂。
        /// </summary>
        /// <param name="constructor">渲染器的无参构造函数。</param>
        /// <returns>用于创建渲染器实例的委托。</returns>
        private static Func<IItemRender> CreateFactory(ConstructorInfo constructor)
        {
            NewExpression newExpression = Expression.New(constructor);
            UnaryExpression convertExpression = Expression.Convert(newExpression, typeof(IItemRender));
            return Expression.Lambda<Func<IItemRender>>(convertExpression).Compile();
        }
    }
}
