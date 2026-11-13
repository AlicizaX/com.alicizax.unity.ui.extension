using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlicizaX.UI
{
    /// <summary>
    /// 可点击的 RecyclerView 列表项基类。
    /// 统一鼠标点击与导航提交入口；可选挂载 <see cref="UXSelectable"/>/<see cref="UXButton"/>（Navigation=None）驱动 transition。
    /// 额外自定义焦点表现可覆盖 <see cref="NavigationViewHolder{TData}.OnNavigationFocused"/>。
    /// </summary>
    /// <typeparam name="TData">当前列表项绑定的数据类型。</typeparam>
    public abstract class InteractiveViewHolder<TData> : NavigationViewHolder<TData>,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler
        where TData : class, ISimpleViewData
    {
        [SerializeField] private UXSelectable interaction;

        private bool pointerInside;
        private bool pointerPressed;

        /// <summary>
        /// 可选交互组件（UXButton/UXSelectable）。用于 ColorTint 与点击反馈；不参与原生导航。
        /// 必须是本项自身或子物体上的组件，禁用时由它自己的 OnDisable 清除外部视觉状态。
        /// </summary>
        protected UXSelectable Interaction => interaction;

        /// <summary>
        /// 激活项时是否同步为 Adapter 业务选中。默认 true。
        /// </summary>
        protected virtual bool SelectOnActivate => true;

        /// <summary>
        /// 点击/提交时是否播放按压视觉反馈。默认 true。
        /// </summary>
        protected virtual bool PlayInteractionClickFeedback => true;

        private bool IsHot => IsNavigationFocused || pointerInside;

        /// <summary>
        /// 判断当前绑定数据是否允许点击/提交。每次视觉刷新都会调用，实现必须是 O(1) 字段判断，不得查表或分配。
        /// </summary>
        protected virtual bool CanActivate(TData data, int index) => true;

        /// <summary>
        /// 列表项被鼠标点击或导航提交时调用。
        /// </summary>
        protected abstract void OnItemClick(TData data, int index);

        /// <summary>
        /// 尝试激活当前列表项。成功处理时返回 true。
        /// </summary>
        protected bool TryActivate()
        {
            // 绑定失败的可见项仍可被点击，这是唯一需要判空的入口。
            TData data = CurrentData;
            if (data == null || !CanActivate(data, CurrentIndex))
            {
                return false;
            }

            if (SelectOnActivate)
            {
                SetSelect();
            }

            PlayClickFeedback();
            OnItemClick(data, CurrentIndex);
            return true;
        }

        /// <summary>
        /// 导航提交，与鼠标点击共用 <see cref="OnItemClick"/>。
        /// 控制器只把提交转给已绑定的焦点项；即使 CanActivate 失败也视为已处理，不回退成仅业务选中。
        /// </summary>
        public override bool HandleNavigationSubmit()
        {
            TryActivate();
            return true;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            pointerPressed = true;
            RefreshInteractionState(false);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            // 先清按下标志：点击转焦点可能同步触发焦点回调刷新视觉，此时应已是松开后的状态。
            pointerPressed = false;
            base.OnPointerUp(eventData);
            RefreshInteractionState(false);
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

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                TryActivate();
            }
        }

        private protected override void OnNavigationFocusStateChanged()
        {
            RefreshInteractionState(false);
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

            UXSelectionState state =
                !interaction.IsInteractable() || (CurrentData != null && !CanActivate(CurrentData, CurrentIndex)) ? UXSelectionState.Disabled :
                pointerPressed ? UXSelectionState.Pressed :
                IsHot ? UXSelectionState.Highlighted :
                UXSelectionState.Normal;

            interaction.SetExternalState(state, instant);
        }

        private void PlayClickFeedback()
        {
            if (!PlayInteractionClickFeedback || interaction == null)
            {
                return;
            }

            if (interaction is UXButton button)
            {
                // 先把恢复态设为当前应有态，再播 pressed 脉冲
                RefreshInteractionState(true);
                button.PlayClickPulse();
                return;
            }

            interaction.PulseExternalState(UXSelectionState.Pressed,
                IsHot ? UXSelectionState.Highlighted : UXSelectionState.Normal);
        }

        /// <summary>
        /// 回收（入池 SetActive(false)）与整表禁用都会走到这里。外部视觉状态由 UXSelectable 自己的
        /// OnDisable → InstantClearState 清除，这里只清本项自有的指针标志。
        /// </summary>
        protected virtual void OnDisable()
        {
            pointerInside = false;
            pointerPressed = false;
        }

        protected virtual void Awake()
        {
            ResolveInteraction();
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            ResolveInteraction();
        }
#endif

        // OnValidate 把 None 烘进 prefab；Awake 兜住未重新保存的旧 prefab，每个实例只执行一次。
        private void ResolveInteraction()
        {
            if (interaction == null)
            {
                interaction = GetComponent<UXSelectable>();
            }

            if (interaction == null || interaction.navigation.mode == Navigation.Mode.None)
            {
                return;
            }

            Navigation navigation = interaction.navigation;
            navigation.mode = Navigation.Mode.None;
            interaction.navigation = navigation;
        }
    }
}
