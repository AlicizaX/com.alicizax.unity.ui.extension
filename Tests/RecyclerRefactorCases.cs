using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AlicizaX.UI.NavigationTests
{
    // ---------------------------------------------------------------------------------------------
    // 测试用数据类型
    // ---------------------------------------------------------------------------------------------

    public sealed class CountingData : ISimpleViewData
    {
    }

    public sealed class CountingItem : ViewHolder<CountingData>
    {
        public static int BindCount;

        protected override void OnBind(CountingData data, int index)
        {
            BindCount++;
        }
    }

    public sealed class DeclaredLengthData : IMeasuredViewData
    {
        public float Length;

        public bool TryGetItemLength(out float length)
        {
            length = Length;
            return Length > 0f;
        }
    }

    public sealed class DeclaredLengthItem : ViewHolder<DeclaredLengthData>
    {
        protected override void OnBind(DeclaredLengthData data, int index)
        {
            // 让"测量值"与"声明长度"一致，否则批量测量会用模板高度覆盖声明高度。
            float length = data.Length > 0f ? data.Length : RectTransform.rect.height;
            RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, length);
        }
    }

    /// <summary>
    /// 覆盖本轮修复点的回归测试：索引空间、单次刷新、AfterSettle、偏移表复杂度、
    /// 对象池不变量、Toggle 事件值、静默失败改造、单列网格导航。
    /// </summary>
    public abstract class RecyclerRefactorCases
    {
        private readonly List<GameObject> owned = new();

        [SetUp]
        public void SetUp()
        {
            CountingItem.BindCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i] != null)
                {
                    Object.DestroyImmediate(owned[i]);
                }
            }

            owned.Clear();
        }

        private GameObject Track(GameObject gameObject)
        {
            owned.Add(gameObject);
            return gameObject;
        }

        private static NavigationData GroupItem(int type)
        {
            return new NavigationData { TemplateId = 0, Type = type };
        }

        private RecyclerViewFixture CreateGroupFixture(out List<NavigationData> source)
        {
            var fixture = new RecyclerViewFixture(new Vector2(200f, 400f), new LinearLayoutManager());
            fixture.CreateTemplate<NavigationItem>(new Vector2(200f, 32f));
            fixture.CreateTemplate<NavigationItem>(new Vector2(200f, 24f));
            source = new List<NavigationData>();
            return fixture;
        }

        private GroupAdapter<NavigationData> ExpandedGroupAdapter(RecyclerViewFixture fixture, List<NavigationData> source)
        {
            var adapter = fixture.Bind(new GroupAdapter<NavigationData>(fixture.View, 1));
            adapter.SetList(source);
            adapter.Add(GroupItem(0));
            adapter.Add(GroupItem(0));
            adapter.Add(GroupItem(0));
            adapter.Expand(0);
            return adapter;
        }

        // -----------------------------------------------------------------------------------------
        // P0-1 GroupAdapter 索引空间
        // -----------------------------------------------------------------------------------------

        [Test]
        public void ReplaceAtShowsTheReplacementOnItsOwnDisplayRow()
        {
            RecyclerViewFixture fixture = CreateGroupFixture(out List<NavigationData> source);
            GroupAdapter<NavigationData> adapter = ExpandedGroupAdapter(fixture, source);
            NavigationData replacement = GroupItem(0);

            // 显示索引 0 是组头，源索引 index 与显示索引不同，这正是之前会重绑错误行的场景。
            Assert.That(adapter.TryGetDisplayData(2, out NavigationData before), Is.True);
            adapter.ReplaceAt(1, replacement);

            Assert.That(adapter.GetItemCount(), Is.EqualTo(4));
            Assert.That(adapter.TryGetDisplayData(2, out NavigationData after), Is.True);
            Assert.That(after, Is.SameAs(replacement));
            Assert.That(before, Is.Not.SameAs(replacement));
            Assert.That(adapter.TryGetDisplayData(1, out NavigationData first), Is.True);
            Assert.That(first, Is.Not.SameAs(replacement), "不应把替换写到错误的行上。");
        }

        [Test]
        public void ReplaceAtWithSameTypeKeepsGroupState()
        {
            RecyclerViewFixture fixture = CreateGroupFixture(out List<NavigationData> source);
            GroupAdapter<NavigationData> adapter = ExpandedGroupAdapter(fixture, source);

            adapter.ReplaceAt(0, GroupItem(0));

            Assert.That(adapter.IsGroupIndex(0), Is.True);
            Assert.That(adapter.TryGetDisplayData(0, out NavigationData group), Is.True);
            Assert.That(group.Expanded, Is.True);
        }

        [Test]
        public void MoveAcrossGroupsThrowsInsteadOfCorruptingSourceOrder()
        {
            RecyclerViewFixture fixture = CreateGroupFixture(out List<NavigationData> source);
            GroupAdapter<NavigationData> adapter = ExpandedGroupAdapter(fixture, source);
            adapter.Add(GroupItem(1));

            Assert.Throws<InvalidOperationException>(() => adapter.Move(0, 3));
        }

        [Test]
        public void RemoveByReferenceRemovesThatItemFromTheDisplayList()
        {
            RecyclerViewFixture fixture = CreateGroupFixture(out List<NavigationData> source);
            GroupAdapter<NavigationData> adapter = ExpandedGroupAdapter(fixture, source);
            Assert.That(adapter.TryGetDisplayData(2, out NavigationData target), Is.True);

            adapter.Remove(target);

            Assert.That(adapter.GetItemCount(), Is.EqualTo(3));
            for (int i = 0; i < adapter.GetItemCount(); i++)
            {
                Assert.That(adapter.TryGetDisplayData(i, out NavigationData item), Is.True);
                Assert.That(item, Is.Not.SameAs(target));
            }
        }

        [Test]
        public void AddRejectsNullInsteadOfDesyncingTheDisplayList()
        {
            RecyclerViewFixture fixture = CreateGroupFixture(out List<NavigationData> source);
            GroupAdapter<NavigationData> adapter = ExpandedGroupAdapter(fixture, source);

            Assert.Throws<ArgumentNullException>(() => adapter.Add(null));
            Assert.That(adapter.GetItemCount(), Is.EqualTo(4));
        }

        // -----------------------------------------------------------------------------------------
        // P0-2 / P0-3 刷新只有一次，且 AfterSettle 真正推迟
        // -----------------------------------------------------------------------------------------

        [Test]
        public void NotifyDataChangedRebindsEachVisibleRowExactlyOnce()
        {
            var fixture = new RecyclerViewFixture(new Vector2(200f, 400f), new LinearLayoutManager());
            fixture.CreateTemplate<CountingItem>(new Vector2(200f, 32f));
            var adapter = fixture.Bind(new Adapter<CountingData>(fixture.View));
            var data = new List<CountingData>();
            for (int i = 0; i < 40; i++)
            {
                data.Add(new CountingData());
            }

            adapter.SetList(data);
            int visible = fixture.View.ViewProvider.VisibleCount;
            Assert.That(visible, Is.GreaterThan(0));

            CountingItem.BindCount = 0;
            adapter.NotifyDataChanged();

            Assert.That(CountingItem.BindCount, Is.EqualTo(visible),
                "一次数据变更只允许重绑一次可见项；之前会 RequestLayout + Refresh 各做一次全量重建。");
        }

        [Test]
        public void AfterSettleKeepsRefreshPendingWhileTheScrollerMoves()
        {
            var fixture = new RecyclerViewFixture(new Vector2(200f, 400f), new LinearLayoutManager());
            fixture.CreateTemplate<CountingItem>(new Vector2(200f, 32f));
            Scroller scroller = fixture.AddScroller();
            // fixture 的 root 在构造时就被激活过，Awake 早于 AddScroller；这里补一次事件订阅，
            // 否则 StopMovement 不会回到 RecyclerView，测的就不是真实链路。
            RecyclerViewFixture.CallPrivate(fixture.View, "ConfigureScroller");
            var adapter = fixture.Bind(new Adapter<CountingData>(fixture.View));
            var data = new List<CountingData>();
            for (int i = 0; i < 40; i++)
            {
                data.Add(new CountingData());
            }

            adapter.SetList(data);
            fixture.View.RefreshMode = ListRefreshMode.AfterSettle;
            scroller.ScrollToDuration(100f, 1f);
            Assert.That(scroller.IsMoving, Is.True, "前置条件：滚动器必须处于运动状态。");

            CountingItem.BindCount = 0;
            adapter.NotifyDataChanged();
            RecyclerViewFixture.CallPrivate(fixture.View, "LateUpdate");

            Assert.That(CountingItem.BindCount, Is.Zero,
                "AfterSettle 期间任何刷新入口（包括 LateUpdate）都不允许重建可见项。");

            scroller.StopMovement(ScrollStopReason.Completed);

            Assert.That(CountingItem.BindCount, Is.GreaterThan(0), "落定后必须补上这次刷新。");
        }

        // -----------------------------------------------------------------------------------------
        // P0-4 偏移表复杂度与失效
        // -----------------------------------------------------------------------------------------

        private static ItemOffsetTable GetOffsetTable(LinearLayoutManager layoutManager)
        {
            FieldInfo field = typeof(LinearLayoutManager).GetField("offsetTable", BindingFlags.Instance | BindingFlags.NonPublic);
            return (ItemOffsetTable)field.GetValue(layoutManager);
        }

        private static List<DeclaredLengthData> BuildLengths(int count, float length)
        {
            var data = new List<DeclaredLengthData>(count);
            for (int i = 0; i < count; i++)
            {
                data.Add(new DeclaredLengthData { Length = length });
            }

            return data;
        }

        [Test]
        public void ReplacingOneMeasuredLengthIsLogarithmic()
        {
            var layoutManager = new LinearLayoutManager { VariableItemLength = true };
            using var fixture = new RecyclerViewFixture(new Vector2(400f, 1000f), layoutManager);
            fixture.CreateTemplate<DeclaredLengthItem>(new Vector2(100f, 20f));
            var adapter = fixture.Bind(new Adapter<DeclaredLengthData>(fixture.View));
            adapter.SetList(BuildLengths(2000, 20f));

            ItemOffsetTable table = GetOffsetTable(layoutManager);
            long before = table.TreeWriteCount;
            layoutManager.ApplyMeasuredLength(1000, 25f);
            long writes = table.TreeWriteCount - before;

            Assert.That(writes, Is.LessThanOrEqualTo(24),
                "单个长度变化只能是 O(log n) 次写；之前是对后缀整体平移 O(n)。");
        }

        [Test]
        public void AppendingOneItemDoesNotRebuildTheWholeTable()
        {
            var layoutManager = new LinearLayoutManager { VariableItemLength = true };
            using var fixture = new RecyclerViewFixture(new Vector2(400f, 1000f), layoutManager);
            fixture.CreateTemplate<DeclaredLengthItem>(new Vector2(100f, 20f));
            var adapter = fixture.Bind(new Adapter<DeclaredLengthData>(fixture.View));
            adapter.SetList(BuildLengths(1000, 20f));

            ItemOffsetTable table = GetOffsetTable(layoutManager);
            long before = table.TreeWriteCount;
            adapter.Add(new DeclaredLengthData { Length = 20f });
            long writes = table.TreeWriteCount - before;

            Assert.That(writes, Is.LessThanOrEqualTo(64),
                "尾部追加只并入新增段；之前每条 append 都整表重建（O(n²)）。");
        }

        [Test]
        public void SameCountDataSetStillInvalidatesTheOffsetTable()
        {
            var layoutManager = new LinearLayoutManager { VariableItemLength = true };
            using var fixture = new RecyclerViewFixture(new Vector2(400f, 1000f), layoutManager);
            fixture.CreateTemplate<DeclaredLengthItem>(new Vector2(100f, 20f));
            var adapter = fixture.Bind(new Adapter<DeclaredLengthData>(fixture.View));

            adapter.SetList(BuildLengths(2, 100f));
            Assert.That(layoutManager.ContentSize.y, Is.EqualTo(200f));

            adapter.SetList(BuildLengths(2, 50f));

            Assert.That(layoutManager.ContentSize.y, Is.EqualTo(100f),
                "数量不变但长度全变时必须失效；旧实现只比较 count，会一直用旧几何。");
        }

        [Test]
        public void InvalidatingItemLengthsAlsoDropsTheTemplateLengthCache()
        {
            var layoutManager = new MixedLayoutManager();
            using var fixture = new RecyclerViewFixture(new Vector2(400f, 1000f), layoutManager);
            DeclaredLengthItem template = fixture.CreateTemplate<DeclaredLengthItem>(new Vector2(100f, 20f));
            var adapter = fixture.Bind(new Adapter<DeclaredLengthData>(fixture.View));
            adapter.SetList(BuildLengths(2, 0f));

            Assert.That(layoutManager.ContentSize.y, Is.EqualTo(40f));

            template.RectTransform.sizeDelta = new Vector2(100f, 30f);
            layoutManager.InvalidateItemLengths();
            layoutManager.SetContentSize();

            Assert.That(layoutManager.ContentSize.y, Is.EqualTo(60f),
                "InvalidateItemLengths 必须同时清模板长度缓存，否则会用旧模板尺寸重建表。");
        }

        // -----------------------------------------------------------------------------------------
        // P1-8 对象池不变量
        // -----------------------------------------------------------------------------------------

        private ViewHolderPool CreatePool(out ViewHolder template)
        {
            var templateObject = Track(new GameObject("pool-template", typeof(RectTransform)));
            template = templateObject.AddComponent<CountingItem>();
            templateObject.SetActive(false);
            return new ViewHolderPool(new ViewHolderFactory(new[] { template }, null), 1);
        }

        [Test]
        public void ReleasingTheSameHolderTwiceDoesNotDuplicateIt()
        {
            ViewHolderPool pool = CreatePool(out _);
            ViewHolder first = pool.Allocate(0);
            Assert.That(first, Is.Not.Null);

            pool.Free(0, first);
            Assert.Throws<InvalidOperationException>(() => pool.Free(0, first));

            ViewHolder second = pool.Allocate(0);
            ViewHolder third = pool.Allocate(0);

            Assert.That(second, Is.SameAs(first));
            Assert.That(third, Is.Not.SameAs(first), "重复入池会让两次 Allocate 拿到同一个实例。");
        }

        [Test]
        public void WarmAndFreeLeaveHoldersInTheSameState()
        {
            ViewHolderPool pool = CreatePool(out _);
            pool.EnsureCapacity(0, 2);
            pool.Warm(0, 2);
            Assert.That(pool.HitCount, Is.Zero);

            ViewHolder holder = pool.Allocate(0);

            Assert.That(holder, Is.Not.Null);
            Assert.That(holder.gameObject.activeSelf, Is.False, "预热实例必须已经 Reset（隐藏）。");
            Assert.That(pool.HitCount, Is.EqualTo(1));
        }

        [Test]
        public void ZeroMaxSizeMeansNoPooling()
        {
            ViewHolderPool pool = CreatePool(out _);
            pool.SetMaxSize(0, 0);
            ViewHolder holder = pool.Allocate(0);

            pool.Free(0, holder);

            Assert.That(pool.DestroyCount, Is.EqualTo(1), "maxSize = 0 必须表示不池化，而不是回落到默认值。");
            Assert.That(pool.GetMaxSize(0), Is.Zero);
        }

        // -----------------------------------------------------------------------------------------
        // P1-6 / P1-15 错误必须响，事件值必须真实
        // -----------------------------------------------------------------------------------------

        [Test]
        public void SecondAdapterBindingThrows()
        {
            var fixture = new RecyclerViewFixture(new Vector2(200f, 400f), new LinearLayoutManager());
            fixture.CreateTemplate<CountingItem>(new Vector2(200f, 32f));
            fixture.Bind(new Adapter<CountingData>(fixture.View));

            Assert.Throws<TargetInvocationException>(() => fixture.Bind(new Adapter<CountingData>(fixture.View)));
            Assert.That(fixture.View.RecyclerViewAdapter, Is.InstanceOf<Adapter<CountingData>>());
        }

        [Test]
        public void CreatingLoopListOnCircleLayoutThrowsInsteadOfSilentlyDoingNothing()
        {
            var fixture = new RecyclerViewFixture(new Vector2(200f, 400f), new CircleLayoutManager());
            fixture.CreateTemplate<CountingItem>(new Vector2(64f, 64f));
            fixture.Bind(new Adapter<CountingData>(fixture.View));

            Assert.Throws<InvalidOperationException>(() => new UGLoopList<CountingData>(fixture.View));
        }

        [Test]
        public void RejectedSwitchOffDoesNotRaiseValueChanged()
        {
            var groupObject = Track(new GameObject("toggle-group", typeof(RectTransform)));
            var group = groupObject.AddComponent<UXGroup>();
            var first = Track(new GameObject("toggle-1", typeof(RectTransform))).AddComponent<UXToggle>();
            var second = Track(new GameObject("toggle-2", typeof(RectTransform))).AddComponent<UXToggle>();
            first.transform.SetParent(groupObject.transform, false);
            second.transform.SetParent(groupObject.transform, false);
            first.group = group;
            second.group = group;
            group.EnsureValidState();
            Assert.That(first.isOn, Is.True, "单选组必须自动选中默认项。");

            int changes = 0;
            bool reportedValue = false;
            first.onValueChanged.AddListener(value =>
            {
                changes++;
                reportedValue = value;
            });

            first.isOn = false;

            Assert.That(first.isOn, Is.True);
            Assert.That(changes, Is.Zero, "值没有变化就不允许发变更事件。");
            Assert.That(reportedValue, Is.False);
        }

        // -----------------------------------------------------------------------------------------
        // P2 Scroller 完成帧只发一次
        // -----------------------------------------------------------------------------------------

        [Test]
        public void DurationScrollReportsTheFinalPositionOnce()
        {
            var scrollerObject = Track(new GameObject("scroller", typeof(RectTransform)));
            var scroller = scrollerObject.AddComponent<Scroller>();
            scroller.ContentSize = new Vector2(0f, 1000f);
            scroller.ViewSize = new Vector2(0f, 100f);
            int changes = 0;
            float last = -1f;
            scroller.OnValueChanged += value =>
            {
                changes++;
                last = value;
            };

            scroller.ScrollToDuration(500f, 0.5f);
            Assert.That(changes, Is.Zero, "进入运动状态时不应立即发值。");

            RecyclerViewFixture.CallPrivate(scroller, "TickDuration", 1f);

            Assert.That(changes, Is.EqualTo(1), "完成帧之前会连发两次 OnValueChanged。");
            Assert.That(last, Is.EqualTo(500f).Within(0.001f));
        }
    }
}
