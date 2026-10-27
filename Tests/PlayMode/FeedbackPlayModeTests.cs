using System.Collections;
using AlicizaX;
using AlicizaX.Audio.Runtime;
using AlicizaX.UI.UXFeedback;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AlicizaX.UI.NavigationTests
{
    public sealed class FeedbackPlayModeTests : FeedbackCases
    {
        private GameObject _eventRoot;
        private GameObject _buttonRoot;
        private GameObject _toggleRoot;

        [TearDown]
        public void TearDownControls()
        {
            Object.DestroyImmediate(_eventRoot);
            Object.DestroyImmediate(_buttonRoot);
            Object.DestroyImmediate(_toggleRoot);
        }

        [UnityTest]
        public IEnumerator DefaultThemeLoadsAndControlsRespectInteractionState()
        {
            Assert.That(AppServices.TryGet<IAudioService>(out _), Is.False);
            UXUiAudioProfile profile = Resources.Load<UXUiAudioProfile>("UX/UXUiAudioProfile");
            Assert.That(profile, Is.Not.Null);
            UXUiAudioRouter.Bind(profile);

            _eventRoot = new GameObject("feedback-events", typeof(EventSystem));
            EventSystem events = _eventRoot.GetComponent<EventSystem>();
            _buttonRoot = new GameObject("feedback-button", typeof(RectTransform));
            UXButton button = _buttonRoot.AddComponent<UXButton>();
            _toggleRoot = new GameObject("feedback-toggle", typeof(RectTransform));
            UXToggle toggle = _toggleRoot.AddComponent<UXToggle>();
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);
            Assert.That(UXUiAudioRouter.ResolveClip(button, UXUiCue.Press, UXUiAudioDevice.KeyboardMouse, false), Is.Not.Null);
            Assert.That(UXUiAudioRouter.ResolveClip(button, UXUiCue.PointerEnter, UXUiAudioDevice.KeyboardMouse, false), Is.Not.Null);
            Assert.That(UXUiAudioRouter.ResolveClip(button, UXUiCue.FocusEnter, UXUiAudioDevice.Gamepad, false), Is.Not.Null);
            Assert.That(UXUiAudioRouter.ResolveClip(button, UXUiCue.FocusEnter, UXUiAudioDevice.Gamepad, true), Is.Null);

            yield return null;

            var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left };
            var submit = new BaseEventData(events);
            Assert.DoesNotThrow(() => button.OnPointerEnter(pointer));
            Assert.DoesNotThrow(() => button.OnSelect(submit));
            Assert.DoesNotThrow(() => button.OnPointerClick(pointer));
            Assert.DoesNotThrow(() => button.OnSubmit(submit));
            Assert.That(clicks, Is.EqualTo(2));

            Assert.DoesNotThrow(() => toggle.OnPointerClick(pointer));
            Assert.That(toggle.isOn, Is.True);
            Assert.DoesNotThrow(() => toggle.OnSubmit(submit));
            Assert.That(toggle.isOn, Is.False);

            button.interactable = false;
            toggle.interactable = false;
            button.OnPointerClick(pointer);
            button.OnSubmit(submit);
            button.PlayClickFeedback();
            toggle.OnPointerClick(pointer);
            toggle.OnSubmit(submit);
            Assert.That(clicks, Is.EqualTo(2));
            Assert.That(toggle.isOn, Is.False);

            UXUiAudioRouter.Bind(null);
        }
    }
}
