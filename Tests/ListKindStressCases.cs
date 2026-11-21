using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

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

        private static readonly MethodInfo TickMotion =
            typeof(Scroller).GetMethod("TickMotion", BindingFlags.Instance | BindingFlags.NonPublic);

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

        private void AttachScroller<T>(RecyclerViewFixture target) where T : Scroller
        {
            scroller = target.AddScroller<T>();
            RecyclerViewFixture.CallPrivate(target.View, "ConfigureScroller");
        }

        /// <summary>真实布局 + 滚动器上的导航列表；关掉平滑滚动，让焦点滚动在调用内完成。</summary>
        private RecyclerViewNavigationController CreateNavigationList<TScroller>(
            LayoutManager layoutManager, Func<RecyclerView, Adapter<NavigationData>> createAdapter, int count)
            where TScroller : Scroller
        {
            fixture = new RecyclerViewFixture(new Vector2(ViewportWidth, ViewportHeight), layoutManager);
            fixture.CreateTemplate<NavigationItem>(new Vector2(80f, 80f));
            AttachScroller<TScroller>(fixture);
            var data = new List<NavigationData>(count);
            for (int i = 0; i < count; i++)
            {
                data.Add(new NavigationData());
            }

            fixture.Bind(createAdapter(fixture.View)).SetList(data);
            var navigation = fixture.Root.AddComponent<RecyclerViewNavigationController>();
            RecyclerViewFixture.SetField(navigation, "smoothScroll", false);
            return navigation;
        }

        /// <summary>
        /// 执行一次滚动输入或一帧运动，返回可见位移（按 period 取最短方向）。
        /// period 是内容的重复周期：Loop 一圈数据的长度、环形的 360°；重锚或换副本不算位移。
        /// </summary>
        private float VisibleStep(Action move, float period)
        {
            float before = scroller.Position;
            move();
            return Mathf.Repeat(scroller.Position - before + period * 0.5f, period) - period * 0.5f;
        }

        /// <summary>按 60fps 推进到静止，返回累计可见位移；任一帧被拉回或跳变都会超出 maxStep。</summary>
        private float TickUntilIdle(float period, float maxStep, string context)
        {
            float total = 0f;
            for (int frame = 0; scroller.IsMoving; frame++)
            {
                Assert.That(frame, Is.LessThan(600), $"{context}: 运动没有结束。");
                float step = VisibleStep(() => TickMotion.Invoke(scroller, new object[] { 1f / 60f }), period);
                Assert.That(Mathf.Abs(step), Is.LessThanOrEqualTo(maxStep), $"{context} 帧#{frame}: 可见位移 {step}。");
                total += step;
            }

            return total;
        }

        /// <summary>快速向下滚轮 notches 格：每次输入与之后每帧都不超过一格，累计可见位移正好是 notches 格。</summary>
        private void WheelDownContinuously(int notches, float period, string context)
        {
            float notch = scroller.WheelSpeed;
            var wheel = new PointerEventData(null) { scrollDelta = new Vector2(0f, -1f) };
            float total = 0f;
            for (int i = 0; i < notches; i++)
            {
                float step = VisibleStep(() => scroller.OnScroll(wheel), period);
                Assert.That(Mathf.Abs(step), Is.LessThanOrEqualTo(notch + 0.5f), $"{context} 滚轮#{i}: 可见位移 {step}。");
                total += step;
            }

            total += TickUntilIdle(period, notch + 0.5f, context);
            Assert.That(total, Is.EqualTo(notches * notch).Within(0.5f), $"{context}: 累计可见位移必须等于滚轮距离。");
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
            AttachScroller<Scroller>(target);
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
            AttachScroller<Scroller>(target);
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
            AttachScroller<Scroller>(target);
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

        [TestCase(ListScrollPolicy.KeepAnchor)]
        [TestCase(ListScrollPolicy.ResetScroll)]
        public void LoopSetListAnchorsAwayFromTheVirtualEdge(ListScrollPolicy policy)
        {
            var layoutManager = new LinearLayoutManager();
            RecyclerViewFixture target = CreateFixture(layoutManager, new Vector2(ViewportWidth, 80f));
            AttachScroller<Scroller>(target);
            var adapter = fixture.Bind(new LoopAdapter<ScrollProbeData>(target.View));

            adapter.SetList(BuildData(12, 0f), policy);

            int layoutIndex = layoutManager.PositionToIndex(scroller.Position);
            Assert.That(LoopVirtualRange.ShouldReanchor(layoutIndex, 12, adapter.GetItemCount()), Is.False,
                "设置数据后位置仍在虚拟区间首圈，向前环绕会找不到副本。");
            Assert.That(layoutManager.GetDataIndex(layoutIndex), Is.Zero, "锚定不能改变视口首项。");
        }

        [Test]
        public void LoopWheelScrollAcrossTheReanchorKeepsMovingForward()
        {
            const float itemHeight = 80f;
            const int realCount = 12;
            var layoutManager = new LinearLayoutManager();
            RecyclerViewFixture target = CreateFixture(layoutManager, new Vector2(ViewportWidth, itemHeight));
            AttachScroller<Scroller>(target);
            target.View.Padding = Vector2.zero;
            target.View.Spacing = Vector2.zero;
            var adapter = fixture.Bind(new LoopAdapter<ScrollProbeData>(target.View));
            adapter.SetList(BuildData(realCount, 0f));

            // 视口顶停在末圈之前一项：几格滚轮就会越过末圈边界，触发重锚。
            scroller.ScrollTo(layoutManager.GetItemStartPosition(adapter.GetItemCount() - realCount - 1));

            WheelDownContinuously(6, realCount * itemHeight, "Loop");
        }

        [Test]
        public void LoopFocusHeldDownScrollsForwardWithoutPullBack()
        {
            // 与示例窗口 ScrollView@Loop 同几何：视口 682、项高 70、间距 10、内边距 8，一圈 12 项（960）。
            const int realCount = 12;
            const float period = realCount * 80f;
            fixture = new RecyclerViewFixture(new Vector2(ViewportWidth, 682f), new LinearLayoutManager());
            fixture.CreateTemplate<NavigationItem>(new Vector2(ViewportWidth, 70f));
            AttachScroller<Scroller>(fixture);
            fixture.View.Padding = new Vector2(0f, 8f);
            fixture.View.Spacing = new Vector2(0f, 10f);
            var data = new List<NavigationData>(realCount);
            for (int i = 0; i < realCount; i++)
            {
                data.Add(new NavigationData());
            }

            fixture.Bind(new LoopAdapter<NavigationData>(fixture.View)).SetList(data);
            var navigation = fixture.Root.AddComponent<RecyclerViewNavigationController>();
            navigation.SetFocus(0);
            TickUntilIdle(period, period, "Loop 初始焦点");

            // 按住下键：输入模块每 0.1s 重复一次 Move，每次都打断上一段 0.18s 的平滑滚动，视口落后焦点约两项。
            // 从虚拟区间中段走 150 项，途中越过末圈重锚。
            var down = new AxisEventData(null) { moveDir = MoveDirection.Down };
            for (int press = 0; press < 150; press++)
            {
                Assert.That(navigation.HandleMove(down), Is.True);
                for (int frame = 0; frame < 6; frame++)
                {
                    float step = VisibleStep(() => TickMotion.Invoke(scroller, new object[] { 1f / 60f }), period);
                    Assert.That(step, Is.GreaterThanOrEqualTo(-0.01f), $"第 {press} 次重复后被拉回：可见位移 {step}。");
                }
            }
        }

        [Test]
        public void LoopNavigationWrapsToTheCopyAboveTheFirstItem()
        {
            RecyclerViewNavigationController navigation = CreateNavigationList<Scroller>(
                new LinearLayoutManager(), view => new LoopAdapter<NavigationData>(view), 12);
            navigation.SetFocus(0, scroll: false);
            float firstItemPosition = scroller.Position;

            Assert.That(navigation.HandleMove(new AxisEventData(null) { moveDir = MoveDirection.Up }), Is.True);

            Assert.That(navigation.FocusedDataIndex, Is.EqualTo(11));
            Assert.That(fixture.View.TryGetViewHolder(11, out _), Is.True, "环绕到末项后，末项必须滚入视口。");
            Assert.That(scroller.Position, Is.LessThan(firstItemPosition), "应滚到首项上方相邻的副本。");
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
            AttachScroller<Scroller>(target);
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
            AttachScroller<Scroller>(target);
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

        [Test]
        public void CircleWheelScrollAcrossTheSeamKeepsRotatingForward()
        {
            RecyclerViewFixture target = CreateFixture(new CircleLayoutManager(), new Vector2(80f, 80f));
            AttachScroller<CircleScroller>(target);
            fixture.Bind(new Adapter<ScrollProbeData>(target.View)).SetList(BuildData(10, 0f));
            scroller.ScrollTo(350f);

            WheelDownContinuously(3, 360f, "Circle");
        }

        [Test]
        public void CircleFocusScrollRotatesTheShortWay()
        {
            var layoutManager = new CircleLayoutManager();
            RecyclerViewNavigationController navigation = CreateNavigationList<CircleScroller>(
                layoutManager, view => new Adapter<NavigationData>(view), 10);
            RecyclerViewFixture.SetField(navigation, "smoothScroll", true);
            navigation.SetFocus(0, scroll: false);

            Assert.That(navigation.HandleMove(new AxisEventData(null) { moveDir = MoveDirection.Down }), Is.True);

            // 相邻一项只转一个间隔（36°）；绕远路会反向转 324°。
            float rotated = TickUntilIdle(360f, 360f, "Circle focus");
            Assert.That(Mathf.Abs(rotated), Is.EqualTo(36f).Within(0.5f));
        }

        [Test]
        public void CircleNavigationRotatesTheFocusedItemToItsIndexPosition()
        {
            var layoutManager = new CircleLayoutManager();
            RecyclerViewNavigationController navigation = CreateNavigationList<CircleScroller>(
                layoutManager, view => new Adapter<NavigationData>(view), 10);

            navigation.SetFocus(3);

            Assert.That(scroller.Position, Is.EqualTo(scroller.ClampPosition(layoutManager.IndexToPosition(3))).Within(0.01f),
                "环形布局的焦点滚动必须把目标转到索引位，与视口对齐方式无关。");
        }

        [TestCase(ScrollAlignment.Start)]
        [TestCase(ScrollAlignment.Center)]
        [TestCase(ScrollAlignment.End)]
        public void CircleAlignedScrollRotatesTheTargetToItsIndexPosition(ScrollAlignment alignment)
        {
            var layoutManager = new CircleLayoutManager();
            RecyclerViewFixture target = CreateFixture(layoutManager, new Vector2(80f, 80f));
            AttachScroller<CircleScroller>(target);
            fixture.Bind(new Adapter<ScrollProbeData>(target.View)).SetList(BuildData(10, 0f));

            target.View.ScrollToAligned(3, alignment);

            Assert.That(scroller.Position, Is.EqualTo(scroller.ClampPosition(layoutManager.IndexToPosition(3))).Within(0.01f),
                "环形布局没有视口对齐，任何对齐方式都应把目标转到索引位。");
        }
    }
}
