using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AlicizaX.UI.NavigationTests
{
    // ---------------------------------------------------------------------------------------------
    // 八种列表演示的"连续操作"压力验收（对应 UIRecyclerViewExampleWindow 的八页）：
    //   Linear / Grid / Chat / Mixed / Group / Loop / Page / Circle
    // Chat / Mixed / Group 的现场回归在 RecyclerScrollRegressionCases；这里补齐其余五种，
    // 每种都做"数百次点击级"的连续操作，并在每一次之后校验不变量（不只看最后一次）。
    // ---------------------------------------------------------------------------------------------

    public abstract class ListKindStressCases
    {
        private const float ViewportWidth = 400f;
        private const float ViewportHeight = 600f;
        private const float PaddingY = 12f;
        private const float PaddingX = 16f;

        private RecyclerViewFixture fixture;
        private Scroller scroller;

        [TearDown]
        public void TearDown()
        {
            fixture?.Dispose();
            fixture = null;
            scroller = null;
        }

        private RecyclerViewFixture CreateFixture(LayoutManager layoutManager, params Vector2[] templateSizes)
        {
            fixture = new RecyclerViewFixture(new Vector2(ViewportWidth, ViewportHeight), layoutManager);
            for (int i = 0; i < templateSizes.Length; i++)
            {
                fixture.CreateTemplate<ScrollProbeItem>(templateSizes[i]);
            }

            return fixture;
        }

        private Scroller AttachScroller(RecyclerViewFixture target)
        {
            scroller = target.AddScroller();
            RecyclerViewFixture.CallPrivate(target.View, "ConfigureScroller");
            return scroller;
        }

        private static List<ScrollProbeData> BuildData(int count, float length)
        {
            var items = new List<ScrollProbeData>(count);
            for (int i = 0; i < count; i++)
            {
                items.Add(new ScrollProbeData { TemplateId = 0, DeclaredLength = length });
            }

            return items;
        }

        /// <summary>主轴内容长度必须等于 Σ项高 + 间距 + padding，且最大滚动位跟随内容。</summary>
        private static void AssertAxisConsistent(
            LayoutManager layoutManager, Scroller targetScroller, int itemCount, float expectedContent, string context)
        {
            bool vertical = layoutManager.Direction == Direction.Vertical;
            float content = vertical ? layoutManager.ContentSize.y : layoutManager.ContentSize.x;
            float viewport = vertical ? layoutManager.ViewportSize.y : layoutManager.ViewportSize.x;

            Assert.That(content, Is.EqualTo(expectedContent).Within(0.5f), $"{context}: 主轴内容长度错误。");
            Assert.That(targetScroller.MaxPosition,
                Is.EqualTo(Mathf.Max(0f, content - viewport)).Within(0.5f),
                $"{context}: 最大滚动位置未跟随内容。");

            int startIndex = layoutManager.GetStartIndex();
            int endIndex = layoutManager.GetEndIndex();
            Assert.That(startIndex, Is.InRange(0, Mathf.Max(0, itemCount - 1)), $"{context}: 起始索引越界。");
            Assert.That(endIndex, Is.InRange(0, Mathf.Max(0, itemCount - 1)), $"{context}: 结束索引越界。");
            Assert.That(endIndex, Is.GreaterThanOrEqualTo(startIndex), $"{context}: 结束索引小于起始索引。");
        }

        private static void AssertReachesEnd(RecyclerView view, IAdapter adapter, string context)
        {
            Assert.That(view.TryGetVisibleRange(out _, out int end), Is.True, $"{context}: 没有可见区间。");
            Assert.That(end, Is.EqualTo(adapter.GetItemCount() - 1), $"{context}: 末尾不可达（end={end}）。");
        }

        // -----------------------------------------------------------------------------------------
        // Linear：定高，追加 + 头部插入（窗口 A=追加，B=Insert(0)）
        // -----------------------------------------------------------------------------------------

        [Test]
        public void LinearStressAppendAndPrependKeepGeometryExact()
        {
            const float itemHeight = 40f;
            const float spacing = 10f;
            var layoutManager = new LinearLayoutManager();
            RecyclerViewFixture target = CreateFixture(layoutManager, new Vector2(ViewportWidth - PaddingX * 2f, itemHeight));
            AttachScroller(target);
            RecyclerView view = target.View;
            view.Padding = new Vector2(PaddingX, PaddingY);
            view.Spacing = new Vector2(12f, spacing);
            var adapter = fixture.Bind(new Adapter<ScrollProbeData>(view));

            adapter.SetList(BuildData(30, 0f));
            for (int i = 0; i < 40; i++)
            {
                adapter.Add(new ScrollProbeData { TemplateId = 0, DeclaredLength = 0f });
                int count = adapter.GetItemCount();
                AssertAxisConsistent(layoutManager, scroller, count,
                    count * (itemHeight + spacing) - spacing + PaddingY * 2f, "add#" + i);
            }

            Assert.That(adapter.GetItemCount(), Is.EqualTo(70));

            // 头部插入：可视锚点必须保持在原来那条数据上（起始索引整体后移 20）。
            view.ScrollToIndex(5, false);
            Assert.That(view.TryGetVisibleRange(out int beforeStart, out _), Is.True);
            // 头插会整体平移内容坐标，锚点必须按"相对视口"比较。
            float anchorBefore = view.LayoutManager.GetItemStartPosition(beforeStart) - scroller.Position;

            for (int i = 0; i < 20; i++)
            {
                adapter.Insert(0, new ScrollProbeData { TemplateId = 0, DeclaredLength = 0f });
            }

            Assert.That(adapter.GetItemCount(), Is.EqualTo(90));
            Assert.That(view.TryGetVisibleRange(out int afterStart, out _), Is.True);
            Assert.That(afterStart, Is.EqualTo(beforeStart + 20), "头部插入后可视锚点数据错位。");
            Assert.That(view.LayoutManager.GetItemStartPosition(afterStart) - scroller.Position,
                Is.EqualTo(anchorBefore).Within(0.5f), "头部插入后可视位置跳动。");

            view.ScrollToIndex(adapter.GetItemCount() - 1, false);
            AssertReachesEnd(view, adapter, "linearEnd");
            AssertAxisConsistent(layoutManager, scroller, adapter.GetItemCount(),
                90 * (itemHeight + spacing) - spacing + PaddingY * 2f, "final");
        }

        // -----------------------------------------------------------------------------------------
        // Grid：2 列，每次点击追加 4 个，随后删尾（窗口 A/B）
        // -----------------------------------------------------------------------------------------

        [Test]
        public void GridStressAppendAndRemoveKeepRowsExact()
        {
            const float cellHeight = 100f;
            const float spacing = 10f;
            var layoutManager = new GridLayoutManager();
            RecyclerViewFixture.SetField(layoutManager, "cellCount", 2);
            RecyclerViewFixture target = CreateFixture(layoutManager,
                new Vector2((ViewportWidth - PaddingX * 2f - 90f) * 0.5f, cellHeight));
            AttachScroller(target);
            RecyclerView view = target.View;
            view.Padding = new Vector2(PaddingX, PaddingY);
            view.Spacing = new Vector2(90f, spacing);
            var adapter = fixture.Bind(new Adapter<ScrollProbeData>(view));

            adapter.SetList(BuildData(40, 0f));
            Assert.That(layoutManager.Unit, Is.EqualTo(2));

            for (int i = 0; i < 20; i++)
            {
                for (int k = 0; k < 4; k++)
                {
                    adapter.Add(new ScrollProbeData { TemplateId = 0, DeclaredLength = 0f });
                }

                int count = adapter.GetItemCount();
                int rows = (count + 1) / 2;
                AssertAxisConsistent(layoutManager, scroller, count,
                    rows * (cellHeight + spacing) - spacing + PaddingY * 2f, "add4#" + i);
            }

            Assert.That(adapter.GetItemCount(), Is.EqualTo(120));

            for (int i = 0; i < 15; i++)
            {
                adapter.RemoveAt(adapter.GetItemCount() - 1);
                int count = adapter.GetItemCount();
                int rows = (count + 1) / 2;
                AssertAxisConsistent(layoutManager, scroller, count,
                    rows * (cellHeight + spacing) - spacing + PaddingY * 2f, "removeTail#" + i);
            }

            Assert.That(adapter.GetItemCount(), Is.EqualTo(105));
            view.ScrollToIndex(adapter.GetItemCount() - 1, false);
            AssertReachesEnd(view, adapter, "gridEnd");
        }

        // -----------------------------------------------------------------------------------------
        // Loop：虚拟地址空间 + 环回滚动（窗口 A=追加，B=滚到3，C=居中）
        // -----------------------------------------------------------------------------------------

        [Test]
        public void LoopStressReanchorKeepsWindowAndGeometryValid()
        {
            const float itemHeight = 80f;
            const float spacing = 10f;
            var layoutManager = new LinearLayoutManager();
            RecyclerViewFixture target = CreateFixture(layoutManager, new Vector2(ViewportWidth - PaddingX * 2f, itemHeight));
            AttachScroller(target);
            RecyclerView view = target.View;
            view.Padding = new Vector2(PaddingX, PaddingY);
            view.Spacing = new Vector2(12f, spacing);
            var adapter = fixture.Bind(new LoopAdapter<ScrollProbeData>(view));

            adapter.SetList(BuildData(8, 0f));
            Assert.That(adapter.GetItemCount(), Is.GreaterThan(8), "虚拟计数没有扩张。");
            int realCount = 8;

            for (int i = 0; i < 30; i++)
            {
                adapter.Add(new ScrollProbeData { TemplateId = 0, DeclaredLength = 0f });
                realCount++;
                int display = adapter.GetItemCount();
                Assert.That(layoutManager.ContentSize.y, Is.GreaterThan(0f), "loop add#" + i + ": 内容高度为 0。");
                Assert.That(scroller.MaxPosition, Is.GreaterThanOrEqualTo(0f));
                Assert.That(view.TryGetVisibleRange(out int vs, out int ve), Is.True);
                Assert.That(vs, Is.InRange(0, realCount - 1), "loop add#" + i + ": 可见起点越界。");
                Assert.That(ve, Is.InRange(0, realCount - 1), "loop add#" + i + ": 可见终点越界。");
                Assert.That(display, Is.GreaterThanOrEqualTo(realCount));
            }

            for (int round = 0; round < 10; round++)
            {
                view.ScrollToIndex(3, false);
                Assert.That(view.TryGetVisibleRange(out int s3, out int e3), Is.True, "loop scroll3#" + round);
                Assert.That(e3, Is.GreaterThanOrEqualTo(s3));

                view.ScrollToAligned(0, ScrollAlignment.Center, 0f, false);
                Assert.That(view.TryGetVisibleRange(out int sc, out int ec), Is.True, "loop center#" + round);
                Assert.That(ec, Is.GreaterThanOrEqualTo(sc));

                view.ScrollToIndex(adapter.GetItemCount() - 1, false);
                Assert.That(view.TryGetVisibleRange(out int sl, out int el), Is.True, "loop last#" + round);
                Assert.That(el, Is.GreaterThanOrEqualTo(sl));

                foreach (int index in new[] { s3, sc, sl })
                {
                    Assert.That(index, Is.InRange(0, realCount - 1), "loop 可见索引越界。");
                }
            }
        }

        // -----------------------------------------------------------------------------------------
        // Page：横向翻页（窗口 A=下一项，B=上一项，C=回第 0 条）
        // -----------------------------------------------------------------------------------------

        [Test]
        public void PageStressHorizontalPagingKeepsSnapIndexInRange()
        {
            const float pageWidth = 300f;
            var layoutManager = new PageLayoutManager();
            RecyclerViewFixture target = CreateFixture(layoutManager, new Vector2(pageWidth, ViewportHeight - PaddingY * 2f));
            AttachScroller(target);
            RecyclerView view = target.View;
            view.Direction = Direction.Horizontal;
            view.Padding = Vector2.zero;
            view.Spacing = new Vector2(30f, 0f);
            var adapter = fixture.Bind(new Adapter<ScrollProbeData>(view));

            adapter.SetList(BuildData(8, 0f));
            Assert.That(view.LayoutManager.Direction, Is.EqualTo(Direction.Horizontal));
            Assert.That(layoutManager.ContentSize.x, Is.GreaterThan(0f), "Page: 内容宽度为 0。");
            Assert.That(scroller.MaxPosition, Is.GreaterThan(0f), "Page: 不可翻页。");

            int seen = view.ScrollDataIndex;
            for (int i = 0; i < 20; i++)
            {
                view.ScrollToIndex(Mathf.Min(view.ScrollDataIndex + 1, adapter.GetItemCount() - 1), false);
                Assert.That(view.ScrollDataIndex, Is.GreaterThanOrEqualTo(seen), "Page: 下一页索引回退。");
                seen = view.ScrollDataIndex;
                Assert.That(view.TryGetVisibleRange(out int vs, out int ve), Is.True, "page next#" + i);
                Assert.That(vs, Is.InRange(0, adapter.GetItemCount() - 1));
                Assert.That(ve, Is.InRange(0, adapter.GetItemCount() - 1));
            }

            Assert.That(view.ScrollDataIndex, Is.EqualTo(adapter.GetItemCount() - 1), "Page: 翻到最后一页失败。");

            for (int i = 0; i < 20; i++)
            {
                view.ScrollToIndex(Mathf.Max(view.ScrollDataIndex - 1, 0), false);
                Assert.That(view.TryGetVisibleRange(out int vs, out int ve), Is.True, "page prev#" + i);
                Assert.That(vs, Is.InRange(0, adapter.GetItemCount() - 1));
                Assert.That(ve, Is.InRange(0, adapter.GetItemCount() - 1));
            }

            Assert.That(view.ScrollDataIndex, Is.EqualTo(0), "Page: 回到第一页失败。");
        }

        // -----------------------------------------------------------------------------------------
        // Circle：环形旋转（窗口 A=追加，B=滚到3，C=停滚）
        // -----------------------------------------------------------------------------------------

        [Test]
        public void CircleStressAddAndRotateKeepGeometryFinite()
        {
            var layoutManager = new CircleLayoutManager();
            RecyclerViewFixture target = CreateFixture(layoutManager, new Vector2(120f, 120f));
            AttachScroller(target);
            RecyclerView view = target.View;
            view.Padding = new Vector2(8f, 8f);
            view.Spacing = Vector2.zero;
            var adapter = fixture.Bind(new Adapter<ScrollProbeData>(view));

            adapter.SetList(BuildData(10, 0f));
            Assert.That(layoutManager.ContentSize.y, Is.GreaterThan(0f), "Circle: 内容高度为 0。");
            Assert.That(view.ViewProvider.VisibleCount, Is.GreaterThan(0), "Circle: 没有可见项。");

            for (int i = 0; i < 30; i++)
            {
                adapter.Add(new ScrollProbeData { TemplateId = 0, DeclaredLength = 0f });
                Assert.That(layoutManager.ContentSize.y, Is.GreaterThan(0f), "circle add#" + i);
                Assert.That(view.TryGetVisibleRange(out int vs, out int ve), Is.True, "circle add#" + i);
                Assert.That(vs, Is.InRange(0, adapter.GetItemCount() - 1), "circle add#" + i + ": 起点越界。");
                Assert.That(ve, Is.InRange(0, adapter.GetItemCount() - 1), "circle add#" + i + ": 终点越界。");
            }

            Assert.That(adapter.GetItemCount(), Is.EqualTo(40));

            for (int round = 0; round < 15; round++)
            {
                view.ScrollToIndex(3, false);
                Assert.That(view.TryGetVisibleRange(out int s3, out int e3), Is.True, "circle scroll3#" + round);
                Assert.That(s3, Is.InRange(0, adapter.GetItemCount() - 1));
                Assert.That(e3, Is.InRange(0, adapter.GetItemCount() - 1));

                view.ScrollToIndex(adapter.GetItemCount() - 1, false);
                Assert.That(view.TryGetVisibleRange(out int sl, out int el), Is.True, "circle last#" + round);
                Assert.That(sl, Is.InRange(0, adapter.GetItemCount() - 1));
                Assert.That(el, Is.InRange(0, adapter.GetItemCount() - 1));
            }

            // 每个可见项的位置都必须有限（环形旋转不能产生 NaN）
            for (int i = 0; i < view.ViewProvider.VisibleCount; i++)
            {
                ViewHolder holder = view.ViewProvider.GetVisibleViewHolder(i);
                if (holder == null)
                {
                    continue;
                }

                Vector2 position = holder.RectTransform.anchoredPosition;
                Assert.That(float.IsNaN(position.x) || float.IsNaN(position.y), Is.False, "Circle: 位置出现 NaN。");
                Assert.That(float.IsInfinity(position.x) || float.IsInfinity(position.y), Is.False, "Circle: 位置出现 Infinity。");
            }
        }
    }
}
