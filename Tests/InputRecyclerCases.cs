using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AlicizaX.UI.Runtime;
using AlicizaX.UI.UXNavigation;
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
        protected UXNavigationScope scope;
        protected RecyclerView recycler;
        protected Scroller scroller;
        protected RecyclerViewNavigationController navigation;
        protected UGList<IntegrationData> list;
        private IntegrationNavigationItem template;
        private InputActionAsset actions;
        private readonly List<InputActionReference> actionReferences = new();
        private bool oldKeyboardPolicy;
        private bool oldGamepadPolicy;
        private object oldInputContext;

        protected static T Read<T>(Type type, object target, string field) =>
            (T)type.GetField(field, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        protected static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        protected static void Call(Type type, object target, string name, params object[] args) =>
            type.GetMethod(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

        public override void Setup()
        {
            Assert.That(Read<bool>(typeof(UXNavigationSystem), null, "_initialized"), Is.False);
            Assert.That(Read<int>(typeof(UXNavigationSystem), null, "_scopeCount"), Is.Zero);
            Assert.That(Read<bool>(typeof(UXInput.Watch), null, "_initialized"), Is.False);
            oldInputContext = typeof(UXInput.Watch).GetProperty("Current").GetValue(null);
            oldKeyboardPolicy = UXNavigationSystem.KeyboardRequireSelection;
            oldGamepadPolicy = UXNavigationSystem.GamepadRequireSelection;
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
            UXNavigationSystem.SetRequireSelection(true, true);
            Call(typeof(UXInput.Watch), null, "Initialize");
            Call(typeof(UXNavigationSystem), null, "Initialize");
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
            scope = canvasRoot.AddComponent<UXNavigationScope>();
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
                Call(typeof(UXNavigationScope), scope, "Awake");
                Call(typeof(UXNavigationScope), scope, "OnEnable");
                Call(typeof(RecyclerView), recycler, "Awake");
            }
            list = new UGList<IntegrationData>(recycler);
            list.SetData(Enumerable.Range(0, 100).Select(i => new IntegrationData { Id = 1000 + i }).ToList());
            if (!Application.isPlaying) Call(typeof(RecyclerViewNavigationController), navigation, "OnEnable");
            scope.DefaultSelectable = navigation;
            scope.NotifySelectableStateChanged();
            Call(typeof(UXNavigationSystem), null, "RequestRefresh", true);
        }

        public override void TearDown()
        {
            try
            {
                Call(typeof(UXNavigationSystem), null, "Shutdown");
                Call(typeof(UXInput.Watch), null, "Shutdown", false);
                if (canvasRoot != null)
                {
                    if (!Application.isPlaying)
                    {
                        list.SetData(new List<IntegrationData>());
                        Assert.That(Pool.GetActiveCount(0), Is.Zero);
                        Call(typeof(RecyclerViewNavigationController), navigation, "OnDisable");
                        Call(typeof(UXNavigationScope), scope, "OnDestroy");
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
                UXNavigationSystem.SetRequireSelection(oldGamepadPolicy, oldKeyboardPolicy);
                typeof(UXInput.Watch).GetProperty("Current").SetValue(null, oldInputContext);
                if (previousEventSystem != null) EventSystem.current = previousEventSystem;
                Assert.That(Read<int>(typeof(UXNavigationSystem), null, "_scopeCount"), Is.Zero);
                Assert.That(Read<bool>(typeof(UXInput.Watch), null, "_initialized"), Is.False);
            }
            finally { base.TearDown(); }
        }

        protected MixedObjectPool<ViewHolder> Pool
        {
            get
            {
                var provider = Read<object>(typeof(RecyclerView), recycler, "viewProvider");
                return Read<MixedObjectPool<ViewHolder>>(provider.GetType(), provider, "objectPool");
            }
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
        protected void CheckFocus(int index)
        {
            Assert.That(navigation.FocusedDataIndex, Is.EqualTo(index));
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(listRoot));
            var visible = Visible();
            Assert.That(Pool.GetActiveCount(0), Is.EqualTo(visible.Length));
            Assert.That(visible.Count(item => item.Focused), Is.EqualTo(1));
            Assert.That(visible.Single(item => item.Focused).BoundIndex, Is.EqualTo(index));
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
        public void EmptyThenRefillRecyclesHoldersWithoutRetainingOldFocus()
        {
            navigation.SetFocus(12);
            var holders = Visible();
            list.SetData(new List<IntegrationData>());
            Assert.That(Visible(), Is.Empty);
            Assert.That(navigation.HasFocus, Is.False);
            Assert.That(Pool.GetActiveCount(0), Is.Zero);
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
