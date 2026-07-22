using System;
using System.Collections;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    [AddComponentMenu("UI/UXButton", 30)]
    public class UXButton : UXSelectable, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField] private AudioClip hoverAudioClip;
        [SerializeField] private AudioClip clickAudioClip;


        protected UXButton()
        {
        }


        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            PlayAudio(hoverAudioClip);
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            // 外部状态驱动时，点击业务由上层处理，避免重复 onClick/音效。
            if (HasExternalState)
                return;

            Press();
            PlayAudio(clickAudioClip);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            if (eventData is PointerEventData)
                return;
            PlayAudio(hoverAudioClip);
        }

        public virtual void OnSubmit(BaseEventData eventData)
        {
            Press();
            PlayClickFeedback();
        }

        /// <summary>
        /// 播放点击音效，并短暂切换到 Pressed 视觉。
        /// 若当前存在外部状态，结束后恢复到外部状态；否则恢复 Selectable 推导状态。
        /// </summary>
        public virtual void PlayClickFeedback()
        {
            PlayAudio(clickAudioClip);

            if (!IsActive() || !IsInteractable())
                return;

            if (HasExternalState)
            {
                UXSelectionState restoreState = ExternalState == UXSelectionState.Pressed
                    ? UXSelectionState.Highlighted
                    : ExternalState;
                PulseExternalState(UXSelectionState.Pressed, restoreState);
                return;
            }

            DoStateTransition(SelectionState.Pressed, false);
            StartCoroutine(OnFinishSubmit());
        }

        private IEnumerator OnFinishSubmit()
        {
            var fadeTime = colors.fadeDuration;
            var elapsedTime = 0f;

            while (elapsedTime < fadeTime)
            {
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            DoStateTransition(currentSelectionState, false);
        }

        private void PlayAudio(AudioClip clip)
        {
            if (clip != null)
                UXComponentExtensionsHelper.PlayAudio(clip);
        }

        [SerializeField] private Button.ButtonClickedEvent m_OnClick = new Button.ButtonClickedEvent();


        public Button.ButtonClickedEvent onClick
        {
            get { return m_OnClick; }
            set { m_OnClick = value; }
        }

        private void Press()
        {
            if (!IsActive() || !IsInteractable())
                return;

            UISystemProfilerApi.AddMarker("Button.onClick", this);
            m_OnClick.Invoke();
        }
    }
}
