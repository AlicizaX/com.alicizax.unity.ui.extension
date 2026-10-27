using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AlicizaX.UI.NavigationTests
{
    public abstract class RecyclerAdapterCases
    {
        private RecyclerViewFixture fixture;
        private CountingLinearLayoutManager layoutManager;

        private sealed class RecordingGroupAdapter : GroupAdapter<NavigationData>
        {
            public readonly List<(int Index, int Count)> RangeNotifications = new();

            public RecordingGroupAdapter(RecyclerView recyclerView, int groupTemplateId)
                : base(recyclerView, groupTemplateId)
            {
            }

            public override void NotifyItemRangeInserted(int index, int count)
            {
                RangeNotifications.Add((index, count));
                base.NotifyItemRangeInserted(index, count);
            }

            public override void NotifyItemRangeRemoved(int index, int count)
            {
                RangeNotifications.Add((index, count));
                base.NotifyItemRangeRemoved(index, count);
            }
        }

        [TearDown]
        public void TearDown()
        {
            fixture?.Dispose();
            fixture = null;
            layoutManager = null;
        }

        private RecyclerViewFixture CreateFixture()
        {
            fixture = new RecyclerViewFixture(new Vector2(200f, 400f), new LinearLayoutManager());
            fixture.CreateTemplate<NavigationItem>(new Vector2(200f, 32f));
            return fixture;
        }

        private RecyclerViewFixture CreateGroupFixture()
        {
            layoutManager = new CountingLinearLayoutManager();
            fixture = new RecyclerViewFixture(new Vector2(200f, 400f), layoutManager);
            fixture.CreateTemplate<NavigationItem>(new Vector2(200f, 32f));
            fixture.CreateTemplate<NavigationItem>(new Vector2(200f, 24f));
            return fixture;
        }

        private static NavigationData GroupItem(int type)
        {
            return new NavigationData { TemplateId = 0, Type = type };
        }

        private RecordingGroupAdapter CreateExpandedGroupAdapter(RecyclerViewFixture groupFixture)
        {
            var adapter = groupFixture.Bind(new RecordingGroupAdapter(groupFixture.View, 1));
            adapter.SetList(new List<NavigationData>());
            adapter.Add(GroupItem(0));
            adapter.Add(GroupItem(0));
            adapter.Expand(0);
            adapter.RangeNotifications.Clear();
            layoutManager.ContentSizeCalculations = 0;
            return adapter;
        }

        [Test]
        public void AdapterWithNullInitialListStartsEmptyAndMutates()
        {
            RecyclerViewFixture adapterFixture = CreateFixture();
            var adapter = adapterFixture.Bind(new Adapter<NavigationData>(adapterFixture.View, null));

            Assert.That(adapter.Items, Is.Not.Null);
            Assert.That(adapter.GetItemCount(), Is.Zero);

            adapter.Add(new NavigationData());
            Assert.That(adapter.GetItemCount(), Is.EqualTo(1));

            adapter.SetList(null);
            Assert.That(adapter.Items, Is.Not.Null);
            Assert.That(adapter.GetItemCount(), Is.Zero);

            adapter.AddRange(new[] { new NavigationData(), new NavigationData() });
            Assert.That(adapter.GetItemCount(), Is.EqualTo(2));
        }

        [Test]
        public void AdapterRemoveRangeUsesSiblingArgumentValidation()
        {
            var data = new List<NavigationData> { new(), new(), new(), new(), new() };
            RecyclerViewFixture adapterFixture = CreateFixture();
            var adapter = adapterFixture.Bind(new Adapter<NavigationData>(adapterFixture.View, data));

            Assert.DoesNotThrow(() => adapter.RemoveRange(-1, 2));
            Assert.DoesNotThrow(() => adapter.RemoveRange(0, 0));
            Assert.DoesNotThrow(() => adapter.RemoveRange(5, 1));
            Assert.That(adapter.GetItemCount(), Is.EqualTo(5));

            adapter.RemoveRange(3, int.MaxValue);
            Assert.That(adapter.GetItemCount(), Is.EqualTo(3));

            adapter.RemoveRange(2, 1);
            Assert.That(adapter.GetItemCount(), Is.EqualTo(2));
            Assert.That(adapter.Items[0], Is.SameAs(data[0]));
            Assert.That(adapter.Items[1], Is.SameAs(data[1]));
        }

        [Test]
        public void UgListDataTracksAdapterItems()
        {
            RecyclerViewFixture listFixture = CreateFixture();
            var list = new UGList<NavigationData>(listFixture.View);
            var data = new List<NavigationData> { new(), new() };
            list.SetData(data);

            Assert.That(list.Data, Is.SameAs(data));
            Assert.That(list.Data, Is.SameAs(list.Adapter.Items));

            list.Add(new NavigationData());
            Assert.That(list.Data, Is.SameAs(list.Adapter.Items));
            Assert.That(list.DataCount, Is.EqualTo(list.Adapter.GetItemCount()));

            list.SetData(null);
            list.Add(new NavigationData());
            Assert.That(list.Data, Is.Not.Null);
            Assert.That(list.Data, Is.SameAs(list.Adapter.Items));
            Assert.That(list.DataCount, Is.EqualTo(1));
        }

        [Test]
        public void GroupAdapterAddRangeEmitsSingleInsertRange()
        {
            RecyclerViewFixture groupFixture = CreateGroupFixture();
            RecordingGroupAdapter adapter = CreateExpandedGroupAdapter(groupFixture);
            var added = new[] { GroupItem(0), GroupItem(0) };

            adapter.AddRange(added);

            Assert.That(adapter.GetItemCount(), Is.EqualTo(5));
            Assert.That(adapter.RangeNotifications.Count, Is.EqualTo(1));
            Assert.That(adapter.RangeNotifications[0].Index, Is.EqualTo(3));
            Assert.That(adapter.RangeNotifications[0].Count, Is.EqualTo(2));
            Assert.That(layoutManager.ContentSizeCalculations, Is.EqualTo(1));
            Assert.That(adapter.TryGetDisplayData(3, out NavigationData first), Is.True);
            Assert.That(first, Is.SameAs(added[0]));
            Assert.That(adapter.TryGetDisplayData(4, out NavigationData second), Is.True);
            Assert.That(second, Is.SameAs(added[1]));
        }

        [Test]
        public void GroupAdapterInsertRangeEmitsSingleInsertRange()
        {
            RecyclerViewFixture groupFixture = CreateGroupFixture();
            RecordingGroupAdapter adapter = CreateExpandedGroupAdapter(groupFixture);
            var inserted = new[] { GroupItem(0), GroupItem(0) };

            adapter.InsertRange(0, inserted);

            Assert.That(adapter.GetItemCount(), Is.EqualTo(5));
            Assert.That(adapter.RangeNotifications.Count, Is.EqualTo(1));
            Assert.That(adapter.RangeNotifications[0].Index, Is.EqualTo(1));
            Assert.That(adapter.RangeNotifications[0].Count, Is.EqualTo(2));
            Assert.That(layoutManager.ContentSizeCalculations, Is.EqualTo(1));
            Assert.That(adapter.TryGetDisplayData(1, out NavigationData first), Is.True);
            Assert.That(first, Is.SameAs(inserted[0]));
            Assert.That(adapter.TryGetDisplayData(2, out NavigationData second), Is.True);
            Assert.That(second, Is.SameAs(inserted[1]));
        }

        [Test]
        public void GroupAdapterMultiGroupAddRangeFallsBackToSingleRefresh()
        {
            RecyclerViewFixture groupFixture = CreateGroupFixture();
            var adapter = groupFixture.Bind(new RecordingGroupAdapter(groupFixture.View, 1));
            adapter.SetList(new List<NavigationData>());
            adapter.Add(GroupItem(0));
            adapter.Add(GroupItem(1));
            adapter.Expand(0);
            adapter.Expand(2);
            adapter.RangeNotifications.Clear();
            layoutManager.ContentSizeCalculations = 0;
            var added = new[] { GroupItem(0), GroupItem(1) };

            adapter.AddRange(added);

            Assert.That(adapter.GetItemCount(), Is.EqualTo(6));
            Assert.That(adapter.RangeNotifications, Is.Empty,
                "多段插入合并为同时包含移除与插入的区间时必须退回整体刷新。");
            Assert.That(layoutManager.ContentSizeCalculations, Is.EqualTo(1));
            Assert.That(adapter.TryGetDisplayData(2, out NavigationData first), Is.True);
            Assert.That(first, Is.SameAs(added[0]));
            Assert.That(adapter.TryGetDisplayData(5, out NavigationData second), Is.True);
            Assert.That(second, Is.SameAs(added[1]));
        }

        [Test]
        public void GroupAdapterRemoveRangeEmitsSingleRemoveRange()
        {
            RecyclerViewFixture groupFixture = CreateGroupFixture();
            RecordingGroupAdapter adapter = CreateExpandedGroupAdapter(groupFixture);
            adapter.AddRange(new[] { GroupItem(0), GroupItem(0) });
            adapter.RangeNotifications.Clear();
            layoutManager.ContentSizeCalculations = 0;

            adapter.RemoveRange(2, 2);

            Assert.That(adapter.GetItemCount(), Is.EqualTo(3));
            Assert.That(adapter.RangeNotifications.Count, Is.EqualTo(1));
            Assert.That(adapter.RangeNotifications[0].Index, Is.EqualTo(3));
            Assert.That(adapter.RangeNotifications[0].Count, Is.EqualTo(2));
            Assert.That(layoutManager.ContentSizeCalculations, Is.EqualTo(1));
        }

        [Test]
        public void GroupAdapterRemoveAllEmitsSingleRemoveRangeAndPrunesGroup()
        {
            RecyclerViewFixture groupFixture = CreateGroupFixture();
            RecordingGroupAdapter adapter = CreateExpandedGroupAdapter(groupFixture);

            adapter.RemoveAll(data => data.Type == 0);

            Assert.That(adapter.GetItemCount(), Is.Zero);
            Assert.That(adapter.RangeNotifications.Count, Is.EqualTo(1));
            Assert.That(adapter.RangeNotifications[0].Index, Is.Zero);
            Assert.That(adapter.RangeNotifications[0].Count, Is.EqualTo(3));
            Assert.That(layoutManager.ContentSizeCalculations, Is.EqualTo(1));
            Assert.That(adapter.IsGroupIndex(0), Is.False);
        }

        [Test]
        public void GroupAdapterKeepsChoiceByDataIdentityAcrossBatch()
        {
            RecyclerViewFixture groupFixture = CreateGroupFixture();
            RecordingGroupAdapter adapter = CreateExpandedGroupAdapter(groupFixture);
            Assert.That(adapter.TryGetDisplayData(2, out NavigationData selected), Is.True);
            adapter.SetChoiceIndex(2);

            var changedChoices = new List<int>();
            adapter.OnChoiceIndexChanged += index => changedChoices.Add(index);
            adapter.InsertRange(0, new[] { GroupItem(0) });

            Assert.That(adapter.ChoiceIndex, Is.EqualTo(3));
            Assert.That(adapter.TryGetDisplayData(3, out NavigationData current), Is.True);
            Assert.That(current, Is.SameAs(selected));
            Assert.That(changedChoices, Is.EqualTo(new[] { 3 }));
        }
    }
}
