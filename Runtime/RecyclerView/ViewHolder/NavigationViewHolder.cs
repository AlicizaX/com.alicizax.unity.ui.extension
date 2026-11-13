using UnityEngine.EventSystems;

namespace AlicizaX.UI
{
    /// <summary>
    /// 可参与 RecyclerView 导航的列表项基类：认领指针、点击转交控制器、焦点状态去重。
    /// 普通 <see cref="ViewHolder"/> 不感知指针与导航。
    /// </summary>
    /// <typeparam name="TData">当前列表项绑定的数据类型。</typeparam>
    public abstract class NavigationViewHolder<TData> : ViewHolder<TData>,
        IRecyclerViewNavigationViewHolder,
        IPointerDownHandler,
        IPointerUpHandler
        where TData : class, ISimpleViewData
    {
        // 焦点视觉当前状态（最后一次回调的值）。跨绑定保留：原地换绑（TryReuseVisibleRange、NotifyItemChanged）
        // 不经过禁用/回收，上一条数据的高亮要靠下一次推送 false 清掉。
        private bool notifiedFocused;

        // 回调 true 时的绑定版本。换绑/回收都会推进 BindingVersion，旧焦点自动失效，无需重置钩子。
        private uint focusedBindingVersion;

        /// <summary>
        /// 当前绑定是否持有导航焦点。OnBind 时恒为 false（版本刚推进）：在 OnBind 里重画焦点视觉时按未聚焦画。
        /// </summary>
        protected bool IsNavigationFocused => notifiedFocused && focusedBindingVersion == BindingVersion;

        void IRecyclerViewNavigationViewHolder.SetNavigationFocused(bool focused)
        {
            // true：同一绑定已聚焦才跳过（换绑后要为新数据重画）；false：视觉已是未聚焦才跳过（换绑残留的高亮要清掉）。
            if (focused ? IsNavigationFocused : !notifiedFocused)
            {
                return;
            }

            notifiedFocused = focused;
            focusedBindingVersion = BindingVersion;
            OnNavigationFocusStateChanged();
            OnNavigationFocused(focused);
        }

        /// <summary>
        /// 焦点项优先处理方向输入；返回 true 表示已消费，控制器不再移动焦点。
        /// </summary>
        public virtual bool HandleNavigationMove(AxisEventData eventData) => false;

        /// <summary>
        /// 焦点项处理提交；返回 false 时控制器回退为业务选中。
        /// </summary>
        public virtual bool HandleNavigationSubmit() => false;

        /// <summary>
        /// 导航焦点视觉出口（业务高亮框等）。只在这里写焦点视觉。
        /// </summary>
        protected virtual void OnNavigationFocused(bool focused)
        {
        }

        /// <summary>
        /// 框架内视觉钩子（<see cref="InteractiveViewHolder{TData}"/> 用），业务 override <see cref="OnNavigationFocused"/> 无需调用 base。
        /// </summary>
        private protected virtual void OnNavigationFocusStateChanged()
        {
        }

        /// <summary>
        /// 空实现：存在即认领 pointerPress，否则 pointerUp 不会派发到本项；动作在 Up 执行。
        /// </summary>
        public virtual void OnPointerDown(PointerEventData eventData)
        {
        }

        public virtual void OnPointerUp(PointerEventData eventData)
        {
#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
            // dragging 必须判断：拖拽开始时输入模块先派发 pointerUp，再把 eligibleForClick 清零。
            if (eventData.button == PointerEventData.InputButton.Left &&
                eventData.eligibleForClick &&
                !eventData.dragging &&
                RecyclerView.TryGetComponent(out RecyclerViewNavigationController controller))
            {
                controller.HandleItemPointerUp(DataIndex);
            }
#endif
        }
    }
}
