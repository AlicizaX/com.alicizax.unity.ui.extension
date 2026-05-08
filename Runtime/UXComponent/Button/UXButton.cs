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

            Press();
            PlayAudio(clickAudioClip);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
#if INPUTSYSTEM_SUPPORT && UX_NAVIGATION
            UXNavigationRuntime.NotifySelection(gameObject);
#endif
            if (eventData is PointerEventData)
                return;
            PlayAudio(hoverAudioClip);
        }

        public virtual void OnSubmit(BaseEventData eventData)
        {
            Press();
            PlayAudio(clickAudioClip);

            if (!IsActive() || !IsInteractable())
                return;

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
            if (clip && UXComponentExtensionsHelper.AudioHelper != null)
                UXComponentExtensionsHelper.AudioHelper.PlayAudio(clip);
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
