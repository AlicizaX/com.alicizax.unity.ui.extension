using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AlicizaX.UI.NavigationTests
{
    public abstract class RecyclerNavigationCases
    {
        private GameObject root;
        private GameObject eventRoot;
        private EventSystem previousEventSystem;
        private EventSystem eventSystem;
        private RecyclerView list;
        private RecyclerViewNavigationController navigation;
        private NavigationItem template;
        private TestAdapter adapter;

        private sealed class TestAdapter : IAdapter
        {
            public int Count = 3;
            public int Choice = -1;
            public int GetItemCount() => Count;
            public int GetRealCount() => Count;
            public int DataRevision => 0;
            public int GetTemplateId(int index) => 0;
            public bool TryGetDeclaredLength(int index, out float length) { length = 32; return true; }
            public void OnBindViewHolder(ViewHolder holder, int index) { }
            public void SetChoiceIndex(int index) => Choice = index;
            public void NotifyDataChanged() { }
        }

        [SetUp]
        public void SetUp()
        {
            previousEventSystem = EventSystem.current;
            eventRoot = new GameObject("recycler-navigation-events");
            eventSystem = eventRoot.AddComponent<EventSystem>();
            if (!Application.isPlaying)
                typeof(EventSystem).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(eventSystem, null);
            EventSystem.current = eventSystem;
            root = new GameObject("recycler-navigation", typeof(RectTransform));
            root.SetActive(false);
            list = root.AddComponent<RecyclerView>();
            var content = new GameObject("content", typeof(RectTransform));
            content.transform.SetParent(root.transform, false);
            var item = new GameObject("template", typeof(RectTransform));
            item.transform.SetParent(content.transform, false);
            template = item.AddComponent<NavigationItem>();
            Set(list, "templates", new ViewHolder[] { template });
            Set(list, "content", content.GetComponent<RectTransform>());
            Set(list, "direction", Direction.Vertical);
            Set(list, "layoutManager", new LinearLayoutManager());
            navigation = root.AddComponent<RecyclerViewNavigationController>();
            root.SetActive(true);
            adapter = new TestAdapter();
            typeof(RecyclerView).GetMethod("SetAdapter", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(list, new object[] { adapter });
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (!Application.isPlaying && eventSystem != null)
                typeof(EventSystem).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(eventSystem, null);
            if (eventRoot != null) Object.DestroyImmediate(eventRoot);
            if (previousEventSystem != null) EventSystem.current = previousEventSystem;
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private void DataChanged() => typeof(RecyclerViewNavigationController)
            .GetMethod("NotifyDataSetChanged", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(navigation, null);
        private bool Move(MoveDirection direction) => navigation.HandleMove(new AxisEventData(eventSystem) { moveDir = direction });

        [TestCase(-1)] [TestCase(99)]
        public void InvalidIndexClearsFocus(int index)
        {
            navigation.SetFocus(1, false);
            navigation.SetFocus(index, false);
            Assert.That(navigation.HasFocus, Is.False);
            Assert.That(navigation.FocusedDataIndex, Is.EqualTo(-1));
        }

        [Test]
        public void UnavailableIndexSelectsNearestFocusableData()
        {
            template.ExcludedIndex = 1;
            navigation.SetFocus(1, false);
            Assert.That(navigation.FocusedDataIndex, Is.EqualTo(2));
        }

        [Test]
        public void MoveSkipsUnavailableData()
        {
            template.ExcludedIndex = 1;
            navigation.SetFocus(0, false);
            Assert.That(Move(MoveDirection.Down), Is.True);
            Assert.That(navigation.FocusedDataIndex, Is.EqualTo(2));
        }

        [TestCase(1, false)] [TestCase(3, false)]
        [TestCase(1, true)] [TestCase(3, true)]
        public void NonWrappingBoundarySelectsConfiguredExit(int count, bool grid)
        {
            adapter.Count = count;
            if (grid) Set(list, "layoutManager", new GridLayoutManager());
            var exit = new GameObject("exit", typeof(RectTransform), typeof(Button));
            exit.transform.SetParent(eventRoot.transform, false);
            Set(navigation, "exitDown", exit.GetComponent<Button>());
            navigation.SetFocus(count - 1, false);
            eventSystem.SetSelectedGameObject(root);
            Assert.That(Move(MoveDirection.Down), Is.True);
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(exit));
            Assert.That(navigation.FocusedDataIndex, Is.EqualTo(count - 1));
        }

        [Test]
        public void NonWrappingUnavailableTailSelectsConfiguredExit()
        {
            adapter.Count = 2;
            template.ExcludedIndex = 1;
            var exit = new GameObject("exit", typeof(RectTransform), typeof(Button));
            exit.transform.SetParent(eventRoot.transform, false);
            Set(navigation, "exitDown", exit.GetComponent<Button>());
            navigation.SetFocus(0, false);
            eventSystem.SetSelectedGameObject(root);
            Assert.That(Move(MoveDirection.Down), Is.True);
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(exit));
            Assert.That(navigation.FocusedDataIndex, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void SingleItemWrappingListKeepsFocus(bool circle)
        {
            adapter.Count = 1;
            if (circle) Set(list, "layoutManager", new CircleLayoutManager());
            Set(navigation, "wrapNavigation", true);
            var exit = new GameObject("exit", typeof(RectTransform), typeof(Button));
            exit.transform.SetParent(eventRoot.transform, false);
            Set(navigation, "exitDown", exit.GetComponent<Button>());
            navigation.SetFocus(0, false);
            eventSystem.SetSelectedGameObject(root);
            Assert.That(Move(MoveDirection.Down), Is.True);
            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(root));
            Assert.That(navigation.FocusedDataIndex, Is.Zero);
        }

        [TestCase(false)] [TestCase(true)]
        public void WrappingBoundaryReturnsToFirstData(bool circle)
        {
            if (circle) Set(list, "layoutManager", new CircleLayoutManager());
            Set(navigation, "wrapNavigation", true);
            navigation.SetFocus(2, false);
            Assert.That(Move(MoveDirection.Down), Is.True);
            Assert.That(navigation.FocusedDataIndex, Is.Zero);
        }

        [Test]
        public void SuppressionBlocksMoveAndSubmitUntilRestored()
        {
            navigation.SetFocus(1, false);
            navigation.SetNavigationSuppressed(true);
            Assert.That(Move(MoveDirection.Down), Is.False);
            Assert.That(navigation.HandleSubmit(), Is.False);
            Assert.That(adapter.Choice, Is.EqualTo(-1));
            navigation.SetNavigationSuppressed(false);
            Assert.That(navigation.FocusedDataIndex, Is.EqualTo(1));
            Assert.That(navigation.HandleSubmit(), Is.True);
            Assert.That(adapter.Choice, Is.EqualTo(1));
        }

        [Test]
        public void DataRemovalRepairsFocusAndEmptyDataClearsIt()
        {
            navigation.SetFocus(2, false);
            adapter.Count = 1;
            DataChanged();
            Assert.That(navigation.FocusedDataIndex, Is.Zero);
            adapter.Count = 0;
            DataChanged();
            Assert.That(navigation.HasFocus, Is.False);
            Assert.That(navigation.FocusedDataIndex, Is.EqualTo(-1));
        }

        [Test]
        public void NoFocusableDataCannotBeSubmitted()
        {
            template.ExcludeAll = true;
            navigation.SetFocus(1, false);
            navigation.HandleSubmit();
            Assert.That(navigation.HasFocus, Is.False);
            Assert.That(adapter.Choice, Is.EqualTo(-1));
        }

        [Test]
        public void SingleColumnGridStillMovesVertically()
        {
            // cellCount = 1 时行内/跨行必须由移动方向判定，否则上/下会被当成行内移动而直接判边界。
            adapter.Count = 3;
            Set(list, "layoutManager", new GridLayoutManager());
            navigation.SetFocus(0, false);

            Assert.That(Move(MoveDirection.Down), Is.True);
            Assert.That(navigation.FocusedDataIndex, Is.EqualTo(1));
        }

        [Test]
        public void BoundaryWithoutConfiguredExitDoesNotConsumeTheInput()
        {
            adapter.Count = 2;
            navigation.SetFocus(1, false);

            Assert.That(Move(MoveDirection.Down), Is.False,
                "没有把焦点交给外部控件时不能吃掉输入，否则 EventSystem 永远离不开列表。");
            Assert.That(navigation.FocusedDataIndex, Is.EqualTo(1));
        }
    }
}
