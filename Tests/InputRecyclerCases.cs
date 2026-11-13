using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AlicizaX.Input.Tests;
using AlicizaX.UI.Runtime;
using AlicizaX.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AlicizaX.UI.NavigationTests
{
    public abstract class InputRecyclerCases : InputTestFixture
    {
        protected GameObject canvasRoot;
        protected GameObject listRoot;
        private GameObject eventRoot;
        protected EventSystem eventSystem;
        private EventSystem previousEventSystem;
        protected InputSystemUIInputModule inputModule;
        protected Keyboard keyboard;
        protected Gamepad gamepad;
        protected NavigationScope scope;
        protected RecyclerView recycler;
        protected Scroller scroller;
        protected RecyclerViewNavigationController navigation;
        protected UGList<IntegrationData> list;
        private IntegrationNavigationItem template;
        private InputActionAsset actions;
        private readonly List<InputActionReference> actionReferences = new();
        private readonly List<GameObject> popups = new();

        protected static T Read<T>(Type type, object target, string field) =>
            (T)type.GetField(field, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        protected static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        protected static void Call(Type type, object target, string name, params object[] args) =>
            type.GetMethod(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        public override void Setup()
        {
            Assert.That(UXInput.Navigation, Is.Null);
            Assert.That(UXInput.IsInitialized, Is.False);
            previousEventSystem = EventSystem.current;
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            eventRoot = new GameObject("input-integration-events");
            eventRoot.SetActive(false);
            eventSystem = eventRoot.AddComponent<EventSystem>();
            inputModule = eventRoot.AddComponent<InputSystemUIInputModule>();
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = actions.AddActionMap("UI");
            var move = map.AddAction("Navigate", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/dpad");
            move.AddBinding("<Gamepad>/leftStick");
            var submit = map.AddAction("Submit", InputActionType.Button);
            submit.AddBinding("<Keyboard>/enter");
            submit.AddBinding("<Gamepad>/buttonSouth");
            var cancel = map.AddAction("Cancel", InputActionType.Button);
            cancel.AddBinding("<Keyboard>/escape");
            cancel.AddBinding("<Gamepad>/buttonEast");
            inputModule.actionsAsset = actions;
            inputModule.move = Reference(move);
            inputModule.submit = Reference(submit);
            inputModule.cancel = Reference(cancel);
            inputModule.moveRepeatDelay = 100;
            eventRoot.SetActive(true);
            if (!Application.isPlaying)
            {
                Call(typeof(EventSystem), eventSystem, "OnEnable");
                Call(typeof(InputSystemUIInputModule), inputModule, "OnEnable");
            }
            EventSystem.current = eventSystem;
            UXInput.Initialize(actions, null);
            UXInput.Navigation.SetRequireSelection(true, true);
            CreateList();
            navigation.RestoreFocus(0);
            Assert.That(Visible().Length, Is.InRange(2, 10));
            Assert.That(scroller.MaxPosition, Is.GreaterThan(100));
        }

        private InputActionReference Reference(InputAction action)
        {
            var reference = InputActionReference.Create(action);
            actionReferences.Add(reference);
            return reference;
        }

        private void CreateList()
        {
            canvasRoot = new GameObject("input-integration-canvas", typeof(RectTransform), typeof(Canvas));
            canvasRoot.SetActive(false);
            canvasRoot.layer = UIComponent.UIShowLayer;
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            scope = canvasRoot.AddComponent<NavigationScope>();
            Set(scope, "_holder", InputTestViews.Bind(canvasRoot).GetHolder());
            listRoot = new GameObject("input-integration-list", typeof(RectTransform));
            listRoot.transform.SetParent(canvasRoot.transform, false);
            listRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 96);
            recycler = listRoot.AddComponent<RecyclerView>();
            scroller = listRoot.AddComponent<Scroller>();
            var content = new GameObject("content", typeof(RectTransform));
            content.transform.SetParent(listRoot.transform, false);
            var item = new GameObject("item-template", typeof(RectTransform));
            item.transform.SetParent(content.transform, false);
            item.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 32);
            template = item.AddComponent<IntegrationNavigationItem>();
            Set(recycler, "templates", new ViewHolder[] { template });
            Set(recycler, "content", content.GetComponent<RectTransform>());
            Set(recycler, "direction", Direction.Vertical);
            Set(recycler, "layoutManager", new LinearLayoutManager());
            Set(recycler, "scroller", scroller);
            Set(recycler, "movementType", MovementType.Clamped);
            navigation = listRoot.AddComponent<RecyclerViewNavigationController>();
            Set(navigation, "navigationScope", scope);
            Set(navigation, "smoothScroll", false);
            canvasRoot.SetActive(true);
            if (!Application.isPlaying)
            {
                Call(typeof(NavigationScope), scope, "OnEnable");
                Call(typeof(RecyclerView), recycler, "Awake");
            }
            list = new UGList<IntegrationData>(recycler);
            list.SetData(Enumerable.Range(0, 100).Select(i => new IntegrationData { Id = 1000 + i }).ToList());
            if (!Application.isPlaying) Call(typeof(RecyclerViewNavigationController), navigation, "OnEnable");
            scope.DefaultSelectable = navigation;
            scope.NotifySelectableStateChanged();
        }

        public override void TearDown()
        {
            try
            {
                UXInput.Shutdown();
                InputTestViews.UnbindAll();
                foreach (var popup in popups) Object.DestroyImmediate(popup);
                popups.Clear();
                if (canvasRoot != null)
                {
                    if (!Application.isPlaying)
                    {
                        list.SetData(new List<IntegrationData>());
                        Assert.That(PoolActiveCount(0), Is.Zero);
                        Call(typeof(RecyclerViewNavigationController), navigation, "OnDisable");
                        Call(typeof(NavigationScope), scope, "OnDisable");
                        recycler.LayoutManager.Release();
                    }
                    Object.DestroyImmediate(canvasRoot);
                }
                if (!Application.isPlaying && inputModule != null)
                {
                    Call(typeof(InputSystemUIInputModule), inputModule, "OnDisable");
                    Call(typeof(EventSystem), eventSystem, "OnDisable");
                }
                if (eventRoot != null) Object.DestroyImmediate(eventRoot);
                foreach (var reference in actionReferences) Object.DestroyImmediate(reference);
                actionReferences.Clear();
                if (actions != null) Object.DestroyImmediate(actions);
                if (previousEventSystem != null) EventSystem.current = previousEventSystem;
                Assert.That(UXInput.Navigation, Is.Null);
                Assert.That(UXInput.IsInitialized, Is.False);
            }
            finally { base.TearDown(); }
        }

        /// <summary>对象池活跃实例数。池本身是内部实现，不通过 protected 成员泄漏到子类。</summary>
        protected int PoolActiveCount(int templateId)
        {
            var provider = Read<object>(typeof(RecyclerView), recycler, "viewProvider");
            return Read<ViewHolderPool>(provider.GetType(), provider, "pool").GetActiveCount(templateId);
        }

        protected void QueueMove(int device, bool pressed, bool up = false)
        {
            if (device == 0) InputSystem.QueueStateEvent(keyboard, pressed ? new KeyboardState(up ? Key.UpArrow : Key.DownArrow) : new KeyboardState());
            else InputSystem.QueueStateEvent(gamepad, device == 1
                ? (pressed ? new GamepadState().WithButton(up ? GamepadButton.DpadUp : GamepadButton.DpadDown) : new GamepadState())
                : new GamepadState { leftStick = pressed ? (up ? Vector2.up : Vector2.down) : Vector2.zero });
        }

        protected void QueueSubmit(bool pad, bool pressed)
        {
            if (pad) InputSystem.QueueStateEvent(gamepad, pressed ? new GamepadState().WithButton(GamepadButton.South) : new GamepadState());
            else InputSystem.QueueStateEvent(keyboard, pressed ? new KeyboardState(Key.Enter) : new KeyboardState());
        }

        protected void QueueCancel(bool pad, bool pressed)
        {
            if (pad) InputSystem.QueueStateEvent(gamepad, pressed ? new GamepadState().WithButton(GamepadButton.East) : new GamepadState());
            else InputSystem.QueueStateEvent(keyboard, pressed ? new KeyboardState(Key.Escape) : new KeyboardState());
        }

        protected IntegrationNavigationItem[] Visible() => listRoot.GetComponentsInChildren<IntegrationNavigationItem>()
            .Where(item => item != template && item.BoundIndex >= 0).ToArray();
        protected IntegrationNavigationItem Focused() => Visible().Single(item => item.BoundIndex == navigation.FocusedDataIndex);
        protected IntegrationNavigationItem VisibleAt(int index) => Visible().Single(item => item.BoundIndex == index);

        /// <summary>焦点位置与 EventSystem 选中（不含光标视觉）。</summary>
        protected void CheckSelection(int index)
        {
            Assert.That(navigation.FocusedDataIndex, Is.EqualTo(index));
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(listRoot));
            Assert.That(PoolActiveCount(0), Is.EqualTo(Visible().Length));
        }

        /// <summary>焦点位置、EventSystem 选中，且光标恰好显示在该项上。</summary>
        protected void CheckFocus(int index)
        {
            CheckSelection(index);
            var visible = Visible();
            Assert.That(visible.Count(item => item.Focused), Is.EqualTo(1));
            Assert.That(visible.Single(item => item.Focused).BoundIndex, Is.EqualTo(index));
        }

        protected void CheckNoCursor() =>
            Assert.That(Visible().Count(item => item.Focused), Is.Zero, "非导航输入下不显示导航光标");

        /// <summary>
        /// 模拟一次指针点击（Down 认领 pointerPress，Up 转交控制器）。
        /// dragging：拖拽开始时输入模块先派发 pointerUp、再清 eligibleForClick，此刻两者同时为 true。
        /// </summary>
        protected void Click(IntegrationNavigationItem item,
            PointerEventData.InputButton button = PointerEventData.InputButton.Left,
            bool eligibleForClick = true,
            bool dragging = false)
        {
            var eventData = new PointerEventData(eventSystem)
            {
                button = button,
                eligibleForClick = eligibleForClick,
                dragging = dragging
            };
            item.OnPointerDown(eventData);
            item.OnPointerUp(eventData);
        }


        protected Button AddScopeButton(string name)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Button));
            buttonObject.transform.SetParent(canvasRoot.transform, false);
            var button = buttonObject.GetComponent<Button>();
            scope.RegisterSelectable(button, true);
            return button;
        }

        /// <summary>打开一个无可选控件的阻塞弹窗；不等待帧，scope 的压制事件同步到达。</summary>
        protected GameObject OpenBlockingPopup()
        {
            var popup = new GameObject("recycler-popup", typeof(RectTransform), typeof(Canvas));
            popups.Add(popup);
            popup.SetActive(false);
            popup.layer = UIComponent.UIShowLayer;
            popup.GetComponent<Canvas>().sortingOrder = 10;
            var popupScope = popup.AddComponent<NavigationScope>();
            Set(popupScope, "_holder", InputTestViews.Bind(popup).GetHolder());
            popup.SetActive(true);
            if (!Application.isPlaying) Call(typeof(NavigationScope), popupScope, "OnEnable");
            return popup;
        }

        protected static void ClosePopup(GameObject popup)
        {
            popup.SetActive(false);
            if (!Application.isPlaying) Call(typeof(NavigationScope), popup.GetComponent<NavigationScope>(), "OnDisable");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AutomaticListRecoveryPreservesScopeSelectionPolicy(bool remember)
        {
            var buttonObject = new GameObject("default-button", typeof(RectTransform), typeof(Button));
            buttonObject.transform.SetParent(canvasRoot.transform, false);
            var button = buttonObject.GetComponent<Button>();
            scope.RegisterSelectable(button, true);
            scope.DefaultSelectable = button;
            Set(scope, "_rememberLastSelection", remember);
            navigation.RestoreFocus(12);
            scope.NotifySelectableStateChanged();

            var popup = new GameObject("recycler-popup", typeof(RectTransform), typeof(Canvas));
            popup.SetActive(false);
            popup.layer = UIComponent.UIShowLayer;
            popup.GetComponent<Canvas>().sortingOrder = 10;
            var popupScope = popup.AddComponent<NavigationScope>();
            var popupHolder = InputTestViews.Bind(popup).GetHolder();
            Set(popupScope, "_holder", popupHolder);
            try
            {
                popup.SetActive(true);
                if (!Application.isPlaying) Call(typeof(NavigationScope), popupScope, "OnEnable");
                Assert.That(scope.NavigationSuppressed, Is.True);
                Assert.That(navigation.IsSuppressed, Is.True, "压制由 scope 事件同步，不等下一帧");
                Assert.That(eventSystem.currentSelectedGameObject, Is.Null);

                popup.SetActive(false);
                if (!Application.isPlaying) Call(typeof(NavigationScope), popupScope, "OnDisable");
                Assert.That(navigation.IsSuppressed, Is.False);
                Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(remember ? listRoot : buttonObject));
                Assert.That(navigation.FocusedDataIndex, Is.EqualTo(12));

                navigation.RestoreFocus(200);
                Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(listRoot),
                    "explicit restoration still selects the list");
                Assert.That(navigation.FocusedDataIndex, Is.EqualTo(99));
            }
            finally
            {
                if (!Application.isPlaying) Call(typeof(NavigationScope), popupScope, "OnDisable");
                InputTestViews.Unbind(popupHolder);
                Object.DestroyImmediate(popup);
            }
        }

        [Test]
        public void OffscreenFocusScrollsAndReusesHoldersForCurrentData()
        {
            var initial = Visible();
            navigation.SetFocus(18);
            CheckFocus(18);
            Assert.That(scroller.Position, Is.GreaterThan(0));
            Assert.That(Visible().Intersect(initial).Any(), Is.True, "Visible holders should be reused.");
            Assert.That(initial.Sum(item => item.BindCalls), Is.GreaterThan(initial.Length));
            Assert.That(Focused().BoundId, Is.EqualTo(1018));
            Assert.That(list.Adapter.ChoiceIndex, Is.EqualTo(-1));
            navigation.SetFocus(0);
            CheckFocus(0);
            Assert.That(scroller.Position, Is.Zero);
            Assert.That(Focused().BoundId, Is.EqualTo(1000));
        }

        [Test]
        public void SameHolderRebindingReappliesFocusForNewData()
        {
            var holder = Focused();
            uint version = holder.Version;
            int notifications = holder.FocusCalls;
            list.Data[0] = new IntegrationData { Id = 9000 };
            list.Adapter.NotifyItemChanged(0);
            Assert.That(Focused(), Is.SameAs(holder));
            Assert.That(holder.Version, Is.Not.EqualTo(version));
            Assert.That(holder.FocusCalls, Is.GreaterThan(notifications));
            CheckFocus(0);
            Assert.That(holder.BoundId, Is.EqualTo(9000));
        }

        [Test]
        public void ScrollFrameDoesNotRepeatNavigationFocusNotifications()
        {
            var holders = Visible();
            var focusCalls = holders.Select(item => item.FocusCalls).ToArray();
            Assert.That(holders.Count(item => item.Focused), Is.EqualTo(1));

            scroller.ScrollTo(scroller.Position + 2f);

            Assert.That(Visible(), Is.EquivalentTo(holders));
            Assert.That(holders.Select(item => item.FocusCalls).ToArray(), Is.EqualTo(focusCalls),
                "同一滚动帧内未变化的导航焦点不应重复通知。");
            Assert.That(holders.Count(item => item.Focused), Is.EqualTo(1));
        }

        [Test]
        public void FarJumpReusesHoldersInPlaceWithoutStaleFocusVisual()
        {
            var initial = Visible();
            var holder = Focused();
            navigation.SetFocus(60);
            CheckFocus(60);
            Assert.That(Visible().Intersect(initial).Any(), Is.True, "远跳走 TryReuseVisibleRange 原地换绑");
            Assert.That(holder.BoundIndex, Is.Not.Zero);
            // NotifiedFocused 只由框架回调写入（不在 OnBind/OnClear 重置），代表 InteractiveViewHolder 的 UXSelectable 这类视觉。
            Assert.That(Visible().Count(item => item.NotifiedFocused), Is.EqualTo(1), "原地换绑后旧数据的高亮必须被清掉");
            Assert.That(Visible().Single(item => item.NotifiedFocused).BoundIndex, Is.EqualTo(60));
        }

        [Test]
        public void HiddenCursorScrollDoesNotNotifyHolders()
        {
            int TotalFocusCalls() => listRoot.GetComponentsInChildren<IntegrationNavigationItem>(true).Sum(item => item.FocusCalls);
            Click(VisibleAt(1));
            CheckNoCursor();
            int calls = TotalFocusCalls();

            scroller.ScrollTo(scroller.Position + 64f);
            scroller.ScrollTo(scroller.Position + 400f);   // 一次跨过整屏：原地换绑

            Assert.That(TotalFocusCalls(), Is.EqualTo(calls), "光标不可见时滚动不遍历、不通知任何 holder");
            CheckNoCursor();
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SuppressionFollowsKeepFocusAndReleaseRestoresCursor(bool keepFocus)
        {
            Set(navigation, "keepFocusVisibleWhenSuppressed", keepFocus);
            var popup = OpenBlockingPopup();
            Assert.That(navigation.IsSuppressed, Is.True);
            Assert.That(eventSystem.currentSelectedGameObject, Is.Not.SameAs(listRoot));
            Assert.That(navigation.FocusedDataIndex, Is.Zero, "压制期间焦点位置不变");
            Assert.That(VisibleAt(0).Focused, Is.EqualTo(keepFocus), "压制期间是否高亮只由 Keep Focus 决定");

            ClosePopup(popup);
            Assert.That(navigation.IsSuppressed, Is.False);
            CheckFocus(0);
        }

        [Test]
        public void DisabledControllerNeverShowsCursor()
        {
            navigation.enabled = false;
            CheckNoCursor();
            navigation.SetFocus(2, false);
            Assert.That(navigation.FocusedDataIndex, Is.EqualTo(2), "禁用期间仍可写焦点位置");
            CheckNoCursor();

            navigation.enabled = true;
            CheckFocus(2);
        }

        [Test]
        public void ClickWithOtherButtonOrIneligibleDoesNotMoveFocus()
        {
            Click(VisibleAt(2), button: PointerEventData.InputButton.Right);
            Click(VisibleAt(2), eligibleForClick: false);
            CheckFocus(0);
        }

        private sealed class RecordingFeedbackObserver : InputFeedbackObserver
        {
            public readonly List<InputFeedbackCue> Cues = new List<InputFeedbackCue>();

            public override void Play(InputFeedbackCue cue) => Cues.Add(cue);
        }

        [Test]
        public void ListEmitsFocusCueOnlyForUserMovesThatChangeFocus()
        {
            var feedback = listRoot.AddComponent<RecordingFeedbackObserver>();
            AxisEventData Axis(MoveDirection direction) => new AxisEventData(eventSystem) { moveDir = direction };

            navigation.SetFocus(5, false);
            Click(VisibleAt(2));
            Assert.That(feedback.Cues, Is.Empty, "程序化焦点与点击转焦点不发焦点提示");

            Assert.That(navigation.HandleMove(Axis(MoveDirection.Down)), Is.True);
            Assert.That(navigation.FocusedDataIndex, Is.EqualTo(3));
            Assert.That(feedback.Cues, Is.EqualTo(new[] { InputFeedbackCue.FocusEnter }));

            Focused().ConsumeMove = true;
            Assert.That(navigation.HandleMove(Axis(MoveDirection.Down)), Is.True);
            Focused().ConsumeMove = false;
            Assert.That(navigation.HandleMove(Axis(MoveDirection.Left)), Is.False, "垂直列表的 Left 是边界");
            Assert.That(feedback.Cues, Has.Count.EqualTo(1), "被列表项消费、到达边界都不算列表内移动");
        }

        [Test]
        public void ClickMovesFocusAndSelectsListWithoutCursor()
        {
            eventSystem.SetSelectedGameObject(null);
            Click(VisibleAt(2));
            CheckSelection(2);
            CheckNoCursor();
            Assert.That(list.Adapter.ChoiceIndex, Is.EqualTo(-1), "点击转焦点不产生业务选中");
        }

        [Test]
        public void ClickOnUnfocusableItemKeepsFocusAndSelection()
        {
            navigation.SetFocusFilter(i => i != 2);     // 第 2 项视为表头
            navigation.SetFocus(1, false);
            var selectedBefore = eventSystem.currentSelectedGameObject;

            Click(VisibleAt(2));

            Assert.That(navigation.FocusedDataIndex, Is.EqualTo(1), "点不可聚焦项不得改投焦点");
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(selectedBefore));
            Assert.That(VisibleAt(1).Focused, Is.True, "点不可聚焦项不得改变光标");
        }

        [Test]
        public void ClickIgnoredInSameFrameAsSuppression()
        {
            OpenBlockingPopup();                           // 不等帧：验证同帧
            Click(VisibleAt(2));
            Assert.That(navigation.FocusedDataIndex, Is.Zero, "被压制时不改焦点");
            Assert.That(eventSystem.currentSelectedGameObject, Is.Not.SameAs(listRoot), "被压制时不抢选中");
        }

        [Test]
        public void DragStartPointerUpDoesNotMoveFocus()
        {
            Click(VisibleAt(2), dragging: true);           // eligibleForClick 仍为 true，只能靠 dragging 拦住
            CheckFocus(0);
        }

        [Test]
        public void CurrentIndexFollowsShiftAfterInsertAbove()
        {
            var holder = VisibleAt(1);
            int binds = holder.BindCalls;
            list.Adapter.Insert(0, new IntegrationData { Id = 5000 });   // 线性列表：ShiftIndexesFrom 平移，不重绑
            Assert.That(holder.BindCalls, Is.EqualTo(binds));
            Assert.That(holder.CurrentIndexForTest, Is.EqualTo(2), "CurrentIndex 必须跟随 DataIndex");
        }

        [Test]
        public void EmptyThenRefillRecyclesHoldersWithoutRetainingOldFocus()
        {
            navigation.SetFocus(12);
            var holders = Visible();
            list.SetData(new List<IntegrationData>());
            Assert.That(Visible(), Is.Empty);
            Assert.That(navigation.HasFocus, Is.False);
            Assert.That(PoolActiveCount(0), Is.Zero);
            Assert.That(holders.All(item => !item.Focused && item.ClearCalls > 0 && item.BoundIndex == -1), Is.True);
            list.SetData(Enumerable.Range(0, 6).Select(i => new IntegrationData { Id = 2000 + i }).ToList());
            navigation.RestoreFocus(0);
            CheckFocus(0);
            Assert.That(Focused().BoundId, Is.EqualTo(2000));
            Assert.That(Visible().Intersect(holders).Any(), Is.True);
            Assert.That(list.Adapter.ChoiceIndex, Is.EqualTo(-1));
        }
    }
}
