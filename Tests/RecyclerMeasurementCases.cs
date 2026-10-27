using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AlicizaX.UI.NavigationTests
{
    public sealed class MeasuredTestData : IMeasuredViewData
    {
        public float Height;

        public bool TryGetItemLength(out float length)
        {
            length = 0f;
            return false;
        }
    }

    public sealed class MeasuredTestItem : ViewHolder<MeasuredTestData>
    {
        protected override void OnBind(MeasuredTestData data, int index)
        {
            UnityEngine.RectTransform rect = (UnityEngine.RectTransform)transform;
            rect.SetSizeWithCurrentAnchors(UnityEngine.RectTransform.Axis.Vertical, data.Height);
        }
    }

    public sealed class CountingLinearLayoutManager : LinearLayoutManager
    {
        public int ContentSizeCalculations;

        public override Vector2 CalculateContentSize()
        {
            ContentSizeCalculations++;
            return base.CalculateContentSize();
        }
    }

    public abstract class RecyclerMeasurementCases
    {
        private static int CountLayoutCalculationsForCreation(int itemCount)
        {
            var layoutManager = new CountingLinearLayoutManager { VariableItemLength = true };
            using var fixture = new RecyclerViewFixture(new Vector2(400f, 1000f), layoutManager);
            fixture.CreateTemplate<MeasuredTestItem>(new Vector2(100f, 20f));
            var adapter = fixture.Bind(new Adapter<MeasuredTestData>(fixture.View));
            var data = new List<MeasuredTestData>();
            for (int i = 0; i < itemCount; i++)
            {
                data.Add(new MeasuredTestData { Height = 50f + i });
            }

            adapter.SetList(data);
            return layoutManager.ContentSizeCalculations;
        }

        [Test]
        public void CreatingVariableLengthHoldersDoesNotScaleLayoutPassesWithItemCount()
        {
            int twoItems = CountLayoutCalculationsForCreation(2);
            int eightItems = CountLayoutCalculationsForCreation(8);

            Assert.That(eightItems, Is.EqualTo(twoItems),
                "同一批创建应只刷新一次内容尺寸，与创建数量无关。");
            Assert.That(eightItems, Is.LessThan(8));
        }

        [Test]
        public void VariableLengthCreationUsesMeasuredLengths()
        {
            var layoutManager = new CountingLinearLayoutManager { VariableItemLength = true };
            using var fixture = new RecyclerViewFixture(new Vector2(400f, 1000f), layoutManager);
            fixture.CreateTemplate<MeasuredTestItem>(new Vector2(100f, 20f));
            var adapter = fixture.Bind(new Adapter<MeasuredTestData>(fixture.View));
            adapter.SetList(new List<MeasuredTestData> { new() { Height = 40f }, new() { Height = 50f } });

            Assert.That(layoutManager.ContentSize.y, Is.EqualTo(90f));
        }

        [Test]
        public void MeasurementBatchPublishesSingleLayoutUpdate()
        {
            var layoutManager = new CountingLinearLayoutManager { VariableItemLength = true };
            using var fixture = new RecyclerViewFixture(new Vector2(400f, 1000f), layoutManager);
            fixture.CreateTemplate<MeasuredTestItem>(new Vector2(100f, 20f));
            var adapter = fixture.Bind(new Adapter<MeasuredTestData>(fixture.View));
            adapter.SetList(new List<MeasuredTestData> { new() { Height = 50f }, new() { Height = 50f } });

            Assert.That(fixture.View.TryGetViewHolder(0, out ViewHolder first), Is.True);
            Assert.That(fixture.View.TryGetViewHolder(1, out ViewHolder second), Is.True);
            Assert.That(layoutManager.ContentSize.y, Is.EqualTo(100f));

            layoutManager.ContentSizeCalculations = 0;
            fixture.View.BeginMeasurementBatch();
            SetHolderHeight(first, 60f);
            SetHolderHeight(second, 70f);
            first.RequestResize();
            second.RequestResize();

            Assert.That(layoutManager.ContentSizeCalculations, Is.Zero);
            Assert.That(fixture.View.EndMeasurementBatch(), Is.True);
            Assert.That(layoutManager.ContentSizeCalculations, Is.EqualTo(1));
            Assert.That(layoutManager.ContentSize.y, Is.EqualTo(130f));
        }

        private static void SetHolderHeight(ViewHolder holder, float height)
        {
            holder.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }
    }
}
