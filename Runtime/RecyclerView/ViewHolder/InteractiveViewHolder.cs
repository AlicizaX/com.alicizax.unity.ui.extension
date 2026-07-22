using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlicizaX.UI
{
    /// <summary>
    /// 可点击的 RecyclerView 列表项基类。
    /// 统一鼠标点击与导航提交入口；可选挂载 <see cref="UXSelectable"/>/<see cref="UXButton"/>（Navigation=None）驱动 transition/音效。
    /// 额外自定义 focus 表现可覆盖 <see cref="OnNavigationFocused"/>。
    /// </summary>
    /// <typeparam name="TData">当前列表项绑定的数据类型。</typeparam>
#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
    public abstract class InteractiveViewHolder<TData> : ViewHolder<TData>,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler,
        IRecyclerViewNavigationViewHolder
#else
    public abstract class InteractiveViewHolder<TData> : ViewHolder<TData>,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler
#endif
        where TData : class, ISimpleViewData
    {
        [SerializeField] private UXSelectable interaction;

        private bool navigationFocused;
        private bool pointerInside;
        private bool pointerPressed;
        private bool interactionConfigured;

        /// <summary>
        /// 可选交互组件（UXButton/UXSelectable）。用于 ColorTint 与点击反馈；不要参与原生导航。
        /// </summary>
        protected UXSelectable Interaction => interaction;

        /// <summary>
        /// 激活项时是否同步为 Adapter 业务选中。默认 true。
        /// </summary>
        protected virtual bool SelectOnActivate => true;

        /// <summary>
        /// 导航提交/点击时是否播放 UXButton 点击反馈。默认 true。
        /// </summary>
        protected virtual bool PlayInteractionClickFeedback => true;

        /// <summary>
        /// 判断当前绑定数据是否允许点击/提交。
        /// </summary>
        protected virtual bool CanActivate(TData data, int index)
        {
            return data != null;
        }

        /// <summary>
        /// 列表项被鼠标点击或导航提交时调用。
        /// </summary>
        protected abstract void OnItemClick(TData data, int index);

        /// <summary>
        /// 导航焦点变化时的额外自定义表现（业务高亮框等）。基类已处理 interaction 的 transition。
        /// </summary>
        protected virtual void OnNavigationFocused(bool focused)
        {
        }

        /// <summary>
        /// 尝试激活当前列表项。成功处理时返回 true。
        /// </summary>
        protected bool TryActivate()
        {
            TData data = CurrentData;
            int index = CurrentIndex;
            if (data == null || index < 0 || !CanActivate(data, index))
            {
                return false;
            }

            if (SelectOnActivate)
            {
                SetSelect();
            }

            PlayClickFeedbackIfNeeded();
            OnItemClick(data, index);
            return true;
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            TryActivate();
        }

        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            RefreshInteractionState(false);
        }

        public virtual void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            pointerPressed = false;
            RefreshInteractionState(false);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            pointerPressed = true;
            RefreshInteractionState(false);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            pointerPressed = false;
            RefreshInteractionState(false);
        }

#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
        /// <summary>
        /// 导航焦点变化：驱动 interaction transition，并回调 <see cref="OnNavigationFocused"/>。
        /// </summary>
        public virtual void HandleNavigationFocused(bool focused)
        {
            navigationFocused = focused;
            RefreshInteractionState(false);
            OnNavigationFocused(focused);
        }

        /// <summary>
        /// 导航方向输入优先处理。默认不消费，交由 RecyclerView 移动焦点。
        /// </summary>
        public virtual bool HandleNavigationMove(AxisEventData eventData)
        {
            return false;
        }

        /// <summary>
        /// 导航提交，与鼠标点击共用 <see cref="OnItemClick"/>。
        /// 只要当前项已绑定数据即视为已处理，避免 CanActivate 失败时回退成仅业务选中。
        /// </summary>
        public virtual bool HandleNavigationSubmit()
        {
            if (CurrentData == null || CurrentIndex < 0)
            {
                return false;
            }

            TryActivate();
            return true;
        }

        /// <summary>
        /// 动态判断该数据索引是否可被导航聚焦。默认 true。
        /// 已绑定到目标索引时走 <see cref="IsItemNavigationFocusable"/>；离屏/模板查询时默认允许。
        /// </summary>
        public virtual bool IsNavigationFocusable(int dataIndex)
        {
            if (CurrentData != null && CurrentIndex == dataIndex)
            {
                return IsItemNavigationFocusable(CurrentData, dataIndex);
            }

            return true;
        }

        /// <summary>
        /// 在已绑定数据时判断该项是否可导航聚焦。默认 true。
        /// </summary>
        protected virtual bool IsItemNavigationFocusable(TData data, int index)
        {
            return true;
        }
#endif

        protected override void OnClear()
        {
            navigationFocused = false;
            pointerInside = false;
            pointerPressed = false;
            if (interaction != null)
            {
                // 回收后仍保持外部驱动，只复位到 Normal，避免下一次点击被 UXButton 原生逻辑抢先。
                interaction.SetExternalState(UXSelectionState.Normal, true);
            }

            base.OnClear();
        }

        /// <summary>
        /// 根据导航焦点/指针状态刷新 interaction 的外部视觉状态。
        /// </summary>
        protected void RefreshInteractionState(bool instant)
        {
            if (interaction == null)
            {
                return;
            }

            EnsureInteractionConfigured();

            bool canInteract = interaction.IsInteractable() &&
                               (CurrentData == null || CurrentIndex < 0 || CanActivate(CurrentData, CurrentIndex));

            UXSelectionState state;
            if (!canInteract)
            {
                state = UXSelectionState.Disabled;
            }
            else if (pointerPressed)
            {
                state = UXSelectionState.Pressed;
            }
            else if (navigationFocused || pointerInside)
            {
                state = UXSelectionState.Highlighted;
            }
            else
            {
                state = UXSelectionState.Normal;
            }

            interaction.SetExternalState(state, instant);
        }

        private void PlayClickFeedbackIfNeeded()
        {
            if (!PlayInteractionClickFeedback || interaction == null)
            {
                return;
            }

            EnsureInteractionConfigured();

            if (interaction is UXButton button)
            {
                // 先把恢复态设为当前应有态，再播 pressed 脉冲
                RefreshInteractionState(true);
                button.PlayClickFeedback();
                return;
            }

            UXSelectionState restore = navigationFocused || pointerInside
                ? UXSelectionState.Highlighted
                : UXSelectionState.Normal;
            interaction.PulseExternalState(UXSelectionState.Pressed, restore);
        }

        private void EnsureInteractionConfigured()
        {
            if (interaction == null)
            {
                return;
            }

            if (!interactionConfigured)
            {
                Navigation navigation = interaction.navigation;
                if (navigation.mode != Navigation.Mode.None)
                {
                    navigation.mode = Navigation.Mode.None;
                    interaction.navigation = navigation;
                }

                interactionConfigured = true;
            }

            // 列表项从进入外部驱动模式起就保持 external state，避免与 UXButton 原生 onClick 双触发。
            if (!interaction.HasExternalState)
            {
                interaction.SetExternalState(UXSelectionState.Normal, true);
            }
        }

        protected virtual void Awake()
        {
            if (interaction == null)
            {
                interaction = GetComponent<UXSelectable>();
            }

            EnsureInteractionConfigured();
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (interaction == null)
            {
                interaction = GetComponent<UXSelectable>();
            }

            if (interaction != null)
            {
                Navigation navigation = interaction.navigation;
                if (navigation.mode != Navigation.Mode.None)
                {
                    navigation.mode = Navigation.Mode.None;
                    interaction.navigation = navigation;
                }
            }
        }
#endif
    }
}
