using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AlicizaX.UI.NavigationTests
{
    /// <summary>UXButton / UXToggle 的点击与提交遵守可交互状态（交互反馈已迁到 Input 包的 InputFeedbackObserver）。</summary>
    public sealed class UXControlInteractionPlayModeTests
    {
        private GameObject _eventRoot;
        private GameObject _buttonRoot;
        private GameObject _toggleRoot;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_eventRoot);
            Object.DestroyImmediate(_buttonRoot);
            Object.DestroyImmediate(_toggleRoot);
        }

        [UnityTest]
        public IEnumerator ClickAndSubmitRespectInteractableState()
        {
            _eventRoot = new GameObject("control-events", typeof(EventSystem));
            EventSystem events = _eventRoot.GetComponent<EventSystem>();
            _buttonRoot = new GameObject("control-button", typeof(RectTransform));
            UXButton button = _buttonRoot.AddComponent<UXButton>();
            _toggleRoot = new GameObject("control-toggle", typeof(RectTransform));
            UXToggle toggle = _toggleRoot.AddComponent<UXToggle>();
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);
            yield return null;

            var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left };
            var submit = new BaseEventData(events);
            button.OnPointerEnter(pointer);
            button.OnSelect(submit);
            button.OnPointerClick(pointer);
            button.OnSubmit(submit);
            Assert.That(clicks, Is.EqualTo(2));

            toggle.OnPointerClick(pointer);
            Assert.That(toggle.isOn, Is.True);
            toggle.OnSubmit(submit);
            Assert.That(toggle.isOn, Is.False);

            button.interactable = false;
            toggle.interactable = false;
            button.OnPointerClick(pointer);
            button.OnSubmit(submit);
            button.PlayClickPulse();
            toggle.OnPointerClick(pointer);
            toggle.OnSubmit(submit);
            Assert.That(clicks, Is.EqualTo(2), "不可交互时点击与提交都不生效");
            Assert.That(toggle.isOn, Is.False);
        }
    }
}
