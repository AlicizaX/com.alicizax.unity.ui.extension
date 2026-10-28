using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AlicizaX.UI.Runtime;
using AlicizaX.UI.UXNavigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AlicizaX.UI.NavigationTests
{
    public sealed class InputRecyclerPlayModeTests : InputRecyclerCases
    {
        private IEnumerator Ready()
        {
            yield return null;
            Assert.That(eventSystem.currentInputModule, Is.SameAs(inputModule));
            CheckFocus(0);
            Assert.That(Visible().Sum(item => item.SubmitCalls), Is.Zero);
        }

        private IEnumerator MoveOnce(int device, bool up = false)
        {
            QueueMove(device, true, up);
            yield return null;
            QueueMove(device, false, up);
            yield return null;
        }

        private IEnumerator SubmitOnce(bool pad)
        {
            QueueSubmit(pad, true);
            yield return null;
            QueueSubmit(pad, false);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerLoopKeyboardMovesScrollsRecyclesAndSubmits() => DeviceChain(0);

        [UnityTest]
        public IEnumerator PlayerLoopGamepadDpadMovesScrollsRecyclesAndSubmits() => DeviceChain(1);

        [UnityTest]
        public IEnumerator PlayerLoopGamepadStickMovesScrollsRecyclesAndSubmits() => DeviceChain(2);

        private IEnumerator DeviceChain(int device)
        {
            yield return Ready();
            var initial = Visible();
            for (int i = 0; i < 18; i++) yield return MoveOnce(device);
            CheckFocus(18);
            Assert.That(scroller.Position, Is.GreaterThan(0));
            Assert.That(Visible().Intersect(initial).Any(), Is.True);
            Assert.That(initial.Sum(item => item.BindCalls), Is.GreaterThan(initial.Length));
            Assert.That(Visible().Sum(item => item.SubmitCalls), Is.Zero);
            var focused = Focused();
            yield return SubmitOnce(device != 0);
            Assert.That(focused.LastSubmittedId, Is.EqualTo(1018));
            Assert.That(focused.SubmitCalls, Is.EqualTo(1));
            Assert.That(list.Adapter.ChoiceIndex, Is.EqualTo(18));
            Assert.That(UXInput.Watch.CurrentInputType, Is.EqualTo(device == 0
                ? UXInput.Watch.InputType.KeyboardMouse : UXInput.Watch.InputType.Gamepad));
            for (int i = 0; i < 18; i++) yield return MoveOnce(device, true);
            CheckFocus(0);
            Assert.That(scroller.Position, Is.Zero);
            Assert.That(Focused().BoundId, Is.EqualTo(1000));
        }

        [UnityTest]
        public IEnumerator FocusedHolderConsumesKeyboardMoveAndSubmit() => ConsumeInput(false);

        [UnityTest]
        public IEnumerator FocusedHolderConsumesGamepadMoveAndSubmit() => ConsumeInput(true);

        private IEnumerator ConsumeInput(bool pad)
        {
            yield return Ready();
            var holder = Focused();
            holder.ConsumeMove = true;
            holder.ConsumeSubmit = true;
            yield return MoveOnce(pad ? 1 : 0);
            CheckFocus(0);
            yield return SubmitOnce(pad);
            Assert.That(holder.SubmitCalls, Is.EqualTo(1));
            Assert.That(holder.LastSubmittedId, Is.EqualTo(1000));
            Assert.That(list.Adapter.ChoiceIndex, Is.EqualTo(-1));
            holder.ConsumeMove = false;
            yield return MoveOnce(pad ? 1 : 0);
            CheckFocus(1);
        }

        [UnityTest]
        public IEnumerator HeldKeyboardSubmitAndReleaseDoNotDoubleSubmit() => SubmitEdges(false);

        [UnityTest]
        public IEnumerator HeldGamepadSubmitAndReleaseDoNotDoubleSubmit() => SubmitEdges(true);

        private IEnumerator SubmitEdges(bool pad)
        {
            yield return Ready();
            var holder = Focused();
            QueueSubmit(pad, true);
            for (int i = 0; i < 4; i++) yield return null;
            Assert.That(holder.SubmitCalls, Is.EqualTo(1));
            QueueSubmit(pad, false);
            yield return null;
            yield return null;
            Assert.That(holder.SubmitCalls, Is.EqualTo(1));
            yield return SubmitOnce(pad);
            Assert.That(holder.SubmitCalls, Is.EqualTo(2));
            Assert.That(holder.LastSubmittedId, Is.EqualTo(1000));
            Assert.That(list.Adapter.ChoiceIndex, Is.Zero);
        }

        [UnityTest]
        public IEnumerator KeyboardCancelReachesSelectedListWithoutSubmitting() => CancelInput(false);

        [UnityTest]
        public IEnumerator GamepadCancelReachesSelectedListWithoutSubmitting() => CancelInput(true);

        private IEnumerator CancelInput(bool pad)
        {
            yield return Ready();
            int cancellations = 0;
            var trigger = listRoot.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.Cancel };
            entry.callback.AddListener(data => { Assert.That(data.used, Is.False); cancellations++; });
            trigger.triggers.Add(entry);
            QueueCancel(pad, true);
            yield return null;
            yield return null;
            QueueCancel(pad, false);
            yield return null;
            Assert.That(cancellations, Is.EqualTo(1));
            Assert.That(Focused().SubmitCalls, Is.Zero);
            Assert.That(list.Adapter.ChoiceIndex, Is.EqualTo(-1));
            CheckFocus(0);
        }

        [UnityTest]
        public IEnumerator KeyboardAnyKeyActivityChangesContextOnceAndIgnoresRelease()
        {
            yield return Ready();
            Assert.That(keyboard.anyKey.synthetic, Is.True);
            // UXInput.Watch 的初始设备探测帧字段已随输入包重构移除，不再断言该实现细节。
            int activity = 0;
            System.Action<UXInput.Watch.InputContext> listener = context =>
            {
                Assert.That(context.DeviceId, Is.EqualTo(keyboard.deviceId));
                activity++;
            };
            UXInput.Watch.OnInputActivity += listener;
            try
            {
                yield return MoveOnce(0);
                Assert.That(activity, Is.EqualTo(1));
                Assert.That(UXInput.Watch.CurrentInputType, Is.EqualTo(UXInput.Watch.InputType.KeyboardMouse));
                yield return null;
                Assert.That(activity, Is.EqualTo(1));
            }
            finally { UXInput.Watch.OnInputActivity -= listener; }
        }

        [UnityTest]
        public IEnumerator KeyboardAndGamepadSwitchWithoutLosingListFocus()
        {
            yield return Ready();
            yield return MoveOnce(1);
            CheckFocus(1);
            Assert.That(UXInput.Watch.CurrentInputType, Is.EqualTo(UXInput.Watch.InputType.Gamepad));
            yield return MoveOnce(0);
            CheckFocus(2);
            Assert.That(UXInput.Watch.CurrentInputType, Is.EqualTo(UXInput.Watch.InputType.KeyboardMouse));
            yield return SubmitOnce(true);
            CheckFocus(2);
            Assert.That(UXInput.Watch.CurrentInputType, Is.EqualTo(UXInput.Watch.InputType.Gamepad));
            Assert.That(Focused().LastSubmittedId, Is.EqualTo(1002));
            Assert.That(Focused().SubmitCalls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator BlockingScopeRoutesSubmitAndRestoresListAfterClosing()
        {
            yield return Ready();
            for (int i = 0; i < 8; i++) yield return MoveOnce(0);
            CheckFocus(8);
            var popup = new GameObject("input-integration-popup", typeof(RectTransform), typeof(Canvas));
            try
            {
                popup.SetActive(false);
                popup.layer = UIComponent.UIShowLayer;
                popup.GetComponent<Canvas>().sortingOrder = 100;
                var popupScope = popup.AddComponent<UXNavigationScope>();
                var buttonRoot = new GameObject("popup-button", typeof(RectTransform), typeof(Button));
                buttonRoot.transform.SetParent(popup.transform, false);
                var button = buttonRoot.GetComponent<Button>();
                int clicks = 0;
                button.onClick.AddListener(() => clicks++);
                popup.SetActive(true);
                Assert.That(popupScope.RegisterSelectable(button), Is.True);
                popupScope.DefaultSelectable = button;
                yield return null;
                Assert.That(scope.NavigationSuppressed, Is.True);
                Assert.That(navigation.IsSuppressed, Is.True);
                Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(buttonRoot));
                float position = scroller.Position;
                int submissions = Visible().Sum(item => item.SubmitCalls);
                yield return MoveOnce(1);
                yield return SubmitOnce(true);
                Assert.That(clicks, Is.EqualTo(1));
                Assert.That(scroller.Position, Is.EqualTo(position));
                Assert.That(Visible().Sum(item => item.SubmitCalls), Is.EqualTo(submissions));
                popup.SetActive(false);
                yield return null;
                yield return null;
                Assert.That(scope.NavigationSuppressed, Is.False);
                CheckFocus(8);
                yield return MoveOnce(0);
                CheckFocus(9);
                yield return SubmitOnce(false);
                Assert.That(Focused().LastSubmittedId, Is.EqualTo(1009));
            }
            finally { Object.DestroyImmediate(popup); }
        }

        [UnityTest]
        public IEnumerator DisableAndEnablePreserveFocusWithoutDuplicateInputListeners()
        {
            yield return Ready();
            for (int i = 0; i < 8; i++) yield return MoveOnce(1);
            CheckFocus(8);
            var holder = Focused();
            for (int cycle = 0; cycle < 3; cycle++)
            {
                canvasRoot.SetActive(false);
                yield return null;
                Assert.That(eventSystem.currentSelectedGameObject, Is.Null);
                yield return SubmitOnce(true);
                Assert.That(holder.SubmitCalls, Is.Zero);
                canvasRoot.SetActive(true);
                yield return null;
                yield return null;
                CheckFocus(8);
                Assert.That(scope.RuntimeSelectableCount, Is.EqualTo(1));
            }
            yield return SubmitOnce(false);
            Assert.That(holder.SubmitCalls, Is.EqualTo(1));
            Assert.That(holder.LastSubmittedId, Is.EqualTo(1008));
        }

        [UnityTest]
        public IEnumerator DestroyReleasesHoldersScopeAndSelectionBeforeLaterInput()
        {
            yield return Ready();
            navigation.SetFocus(25);
            CheckFocus(25);
            var holders = listRoot.GetComponentsInChildren<IntegrationNavigationItem>(true);
            Object.Destroy(canvasRoot);
            yield return null;
            yield return null;
            Assert.That(holders.All(item => item == null), Is.True);
            // 名称即契约：销毁后对象池必须把活跃实例清零（原断言在销毁前取快照，等于没验证这一步）。
            Assert.That(PoolActiveCount(0), Is.Zero);
            Assert.That(Read<int>(typeof(UXNavigationSystem), null, "_scopeCount"), Is.Zero);
            Assert.That(eventSystem.currentSelectedGameObject, Is.Null);
            yield return MoveOnce(0);
            yield return SubmitOnce(true);
            Assert.That(eventSystem.currentSelectedGameObject, Is.Null);
        }

        [UnityTest]
        public IEnumerator ReboundFocusedHolderSubmitsNewDataThroughKeyboard()
        {
            yield return Ready();
            SameHolderRebindingReappliesFocusForNewData();
            yield return SubmitOnce(false);
            Assert.That(Focused().LastSubmittedId, Is.EqualTo(9000));
            Assert.That(Focused().SubmitCalls, Is.EqualTo(1));
            Assert.That(list.Adapter.ChoiceIndex, Is.Zero);
        }

        [UnityTest]
        public IEnumerator EmptyListIgnoresSubmitAndRefillUsesRecycledHolderForGamepad()
        {
            yield return Ready();
            var holders = Visible();
            list.SetData(new List<IntegrationData>());
            yield return SubmitOnce(true);
            Assert.That(Visible(), Is.Empty);
            Assert.That(holders.Sum(item => item.SubmitCalls), Is.Zero);
            Assert.That(PoolActiveCount(0), Is.Zero);
            Assert.That(list.Adapter.ChoiceIndex, Is.EqualTo(-1));
            list.SetData(Enumerable.Range(0, 6).Select(i => new IntegrationData { Id = 2000 + i }).ToList());
            navigation.RestoreFocus(0);
            yield return SubmitOnce(true);
            CheckFocus(0);
            Assert.That(Visible().Intersect(holders).Any(), Is.True);
            Assert.That(Focused().LastSubmittedId, Is.EqualTo(2000));
            Assert.That(Focused().SubmitCalls, Is.EqualTo(1));
            Assert.That(list.Adapter.ChoiceIndex, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SmoothNavigationFinishesWithOneFocusedReusedHolder()
        {
            yield return Ready();
            Set(navigation, "smoothScroll", true);
            Set(navigation, "smoothScrollDuration", 0.15f);
            for (int i = 0; i < 12; i++) yield return MoveOnce(1);
            Assert.That(scroller.IsMoving, Is.True);
            float deadline = Time.realtimeSinceStartup + 2;
            while (scroller.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(scroller.IsMoving, Is.False);
            Assert.That(scroller.Position, Is.GreaterThan(0));
            CheckFocus(12);
            yield return SubmitOnce(true);
            Assert.That(Focused().LastSubmittedId, Is.EqualTo(1012));
            Assert.That(Focused().SubmitCalls, Is.EqualTo(1));
        }
    }
}
