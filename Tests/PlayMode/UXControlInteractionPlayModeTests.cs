using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AlicizaX.UI.NavigationTests
{
    /// <summary>UXButton / UXToggle / UXStepper 的点击、提交与步进遵守可交互状态与导航边界（交互反馈已迁到 Input 包的 InputFeedbackObserver）。</summary>
    public sealed class UXControlInteractionPlayModeTests
    {
        private GameObject _eventRoot;
        private GameObject _buttonRoot;
        private GameObject _toggleRoot;
        private GameObject _stepperRoot;
        private GameObject _neighborRoot;
        private EventSystem _previousEventSystem;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_neighborRoot);
            Object.DestroyImmediate(_stepperRoot);
            Object.DestroyImmediate(_toggleRoot);
            Object.DestroyImmediate(_buttonRoot);
            Object.DestroyImmediate(_eventRoot);
            if (_previousEventSystem != null) EventSystem.current = _previousEventSystem;
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

        [UnityTest]
        public IEnumerator StepperConsumesStepAxisNavigatesPerpendicularAndRespectsInteractable()
        {
            _previousEventSystem = EventSystem.current;
            _eventRoot = new GameObject("stepper-events", typeof(EventSystem));
            EventSystem events = _eventRoot.GetComponent<EventSystem>();
            EventSystem.current = events;

            _stepperRoot = new GameObject("stepper", typeof(RectTransform));
            UXStepper stepper = _stepperRoot.AddComponent<UXStepper>();
            _neighborRoot = new GameObject("neighbors", typeof(RectTransform));
            var rightNeighbor = new GameObject("right", typeof(RectTransform)).AddComponent<UXButton>();
            rightNeighbor.transform.SetParent(_neighborRoot.transform, false);
            var downNeighbor = new GameObject("down", typeof(RectTransform)).AddComponent<UXButton>();
            downNeighbor.transform.SetParent(_neighborRoot.transform, false);

            // 刻意同时配置 selectOnRight 与 selectOnDown：横向步进必须消费事件且不逃逸焦点，纵向照常换行。
            stepper.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnRight = rightNeighbor,
                selectOnDown = downNeighbor,
            };

            var deltas = new List<int>();
            stepper.onStep.AddListener(deltas.Add);
            events.SetSelectedGameObject(_stepperRoot);
            yield return null;

            var left = new AxisEventData(events) { moveDir = MoveDirection.Left };
            stepper.OnMove(left);
            Assert.That(left.used, Is.True);
            Assert.That(events.currentSelectedGameObject, Is.SameAs(_stepperRoot));

            var right = new AxisEventData(events) { moveDir = MoveDirection.Right };
            stepper.OnMove(right);
            Assert.That(right.used, Is.True);
            Assert.That(events.currentSelectedGameObject, Is.SameAs(_stepperRoot), "步进轴向不得逃逸到相邻控件");
            Assert.That(deltas, Is.EqualTo(new[] { -1, 1 }));

            // 垂直轴向交回基类，正常切到下方的邻接控件。
            var down = new AxisEventData(events) { moveDir = MoveDirection.Down };
            stepper.OnMove(down);
            Assert.That(events.currentSelectedGameObject, Is.SameAs(downNeighbor.gameObject));

            // 切到 Vertical 步进轴：Up=+1、Down=-1 且不跳到 downNeighbor；Right 交回基类切到 rightNeighbor。
            events.SetSelectedGameObject(_stepperRoot);
            stepper.axis = UXStepperAxis.Vertical;
            deltas.Clear();

            var up = new AxisEventData(events) { moveDir = MoveDirection.Up };
            stepper.OnMove(up);
            var downStep = new AxisEventData(events) { moveDir = MoveDirection.Down };
            stepper.OnMove(downStep);
            Assert.That(up.used, Is.True);
            Assert.That(downStep.used, Is.True);
            Assert.That(deltas, Is.EqualTo(new[] { 1, -1 }));
            Assert.That(events.currentSelectedGameObject, Is.SameAs(_stepperRoot));

            var rightNav = new AxisEventData(events) { moveDir = MoveDirection.Right };
            stepper.OnMove(rightNav);
            Assert.That(events.currentSelectedGameObject, Is.SameAs(rightNeighbor.gameObject));

            // 不可交互时不触发步进，并放行方向移动让焦点可以离开。
            events.SetSelectedGameObject(_stepperRoot);
            stepper.axis = UXStepperAxis.Horizontal;
            stepper.interactable = false;
            deltas.Clear();

            var disabledRight = new AxisEventData(events) { moveDir = MoveDirection.Right };
            stepper.OnMove(disabledRight);
            stepper.Step(1);
            Assert.That(deltas, Is.Empty, "不可交互时不得触发步进回调");
            Assert.That(events.currentSelectedGameObject, Is.SameAs(rightNeighbor.gameObject));
        }
    }
}
