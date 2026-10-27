using AlicizaX.UI.UXFeedback;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    [AddComponentMenu("UI/UXButton", 30)]
    public class UXButton : UXSelectable, IPointerClickHandler, ISubmitHandler
    {
        protected UXButton()
        {
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            // 外部状态驱动时，点击业务由上层处理，避免重复 onClick/音效。
            if (HasExternalState || !IsActive() || !IsInteractable())
                return;

            UXUiFeedback.Raise(this, UXUiCue.Press);
            Press();
        }

        public virtual void OnSubmit(BaseEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
                return;

            PlayClickFeedbackCore();
            Press();
        }

        /// <summary>
        /// 播放点击音效，并短暂切换到 Pressed 视觉。
        /// 若当前存在外部状态，结束后恢复到外部状态；否则恢复 Selectable 推导状态。
        /// </summary>
        public virtual void PlayClickFeedback()
        {
            if (!IsActive() || !IsInteractable())
                return;

            PlayClickFeedbackCore();
        }

        private void PlayClickFeedbackCore()
        {
            UXUiFeedback.Raise(this, UXUiCue.Press);

            if (HasExternalState)
            {
                UXSelectionState restoreState = ExternalState == UXSelectionState.Pressed
                    ? UXSelectionState.Highlighted
                    : ExternalState;
                PulseExternalState(UXSelectionState.Pressed, restoreState);
                return;
            }

            PulseDerivedState(UXSelectionState.Pressed);
        }

        [SerializeField] private Button.ButtonClickedEvent m_OnClick = new Button.ButtonClickedEvent();


        public Button.ButtonClickedEvent onClick
        {
            get { return m_OnClick; }
            set { m_OnClick = value; }
        }

        private void Press()
        {
            UISystemProfilerApi.AddMarker("Button.onClick", this);
            m_OnClick.Invoke();
        }
    }
}
