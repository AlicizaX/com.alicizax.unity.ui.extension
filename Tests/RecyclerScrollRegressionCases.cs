using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AlicizaX.UI.NavigationTests
{
    // ---------------------------------------------------------------------------------------------
    // 复刻 UIRecyclerViewExampleWindow.prefab 里三种"用偏移表"的列表配置：
    //   Chat  : LinearLayoutManager  variableItemLength = 1，padding(16,12)，spacing(0,12)
    //   Mixed : MixedLayoutManager   variableItemLength = 1，padding(16,12)，spacing(12,10)，3 模板
    //   Group : MixedLayoutManager   variableItemLength = 0，padding(16,12)，spacing(0,10)，2 模板
    // 这些是"追加数据后无法滚动 / 滚动异常"的现场；Linear 定高与 Grid 不走偏移表，所以当初没暴露。
    // ---------------------------------------------------------------------------------------------

    public sealed class ScrollProbeData : IMeasuredViewData, IMixedViewData
    {
        public float DeclaredLength;
        public int TemplateId { get; set; }

        public bool TryGetItemLength(out float length)
        {
            length = DeclaredLength;
            return DeclaredLength > 0f;
        }
    }

    public sealed class ScrollProbeItem : ViewHolder<ScrollProbeData>
    {
        protected override void OnBind(ScrollProbeData data, int index)
        {
            float height = data.DeclaredLength > 0f ? data.DeclaredLength : RectTransform.rect.height;
            RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }
    }

    public abstract class RecyclerScrollRegressionCases
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

        private RecyclerViewFixture CreateFixture(LayoutManager layoutManager, params float[] templateHeights)
        {
            fixture = new RecyclerViewFixture(new Vector2(ViewportWidth, ViewportHeight), layoutManager);
            for (int i = 0; i < templateHeights.Length; i++)
            {
                fixture.CreateTemplate<ScrollProbeItem>(new Vector2(ViewportWidth - PaddingX * 2f, templateHeights[i]));
            }

            return fixture;
        }

        private Scroller AttachScroller(RecyclerViewFixture target)
        {
            scroller = target.AddScroller();
            // fixture 的 root 在构造时激活过，Awake 早于 AddScroller；补一次订阅，测真实链路。
            RecyclerViewFixture.CallPrivate(target.View, "ConfigureScroller");
            return scroller;
        }

        private static List<ScrollProbeData> BuildData(int count, params float[] lengths)
        {
            var items = new List<ScrollProbeData>(count);
            for (int i = 0; i < count; i++)
            {
                items.Add(new ScrollProbeData
                {
                    TemplateId = 0,
                    DeclaredLength = lengths[i % lengths.Length]
                });
            }

            return items;
        }

        private static string Describe(LinearLayoutManager layoutManager, Scroller targetScroller, IAdapter adapter)
        {
            var field = typeof(LinearLayoutManager).GetField("offsetTable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var table = (ItemOffsetTable)field.GetValue(layoutManager);
            return "items=" + adapter.GetItemCount()
                + " content=" + layoutManager.ContentSize.y.ToString("F1")
                + " tableCount=" + table.Count
                + " tableTotal=" + table.TotalLength.ToString("F1")
                + " len0=" + table.LengthAt(0).ToString("F1")
                + " lenLast=" + table.LengthAt(table.Count - 1).ToString("F1")
                + " view=" + targetScroller.ViewSize.y.ToString("F1")
                + " scrollerContent=" + targetScroller.ContentSize.y.ToString("F1")
                + " max=" + targetScroller.MaxPosition.ToString("F1")
                + " pos=" + targetScroller.Position.ToString("F1");
        }

        private static float Sum(IReadOnlyList<float> values, int count)
        {
            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                total += values[i % values.Count];
            }

            return total;
        }

        // -----------------------------------------------------------------------------------------
        // 偏移表本身：容量增长时前缀和必须重建（32/64/128/256 边界）
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// 逐条追加穿过每一个 2 的幂容量边界，每次断言 <c>PositionAt</c> 与逐项长度求和一致。
        /// 旧实现扩容时只拷贝旧树，导致新容量上的祖先节点丢掉了老元素的贡献（内容高度腰斩、列表滚不动）。
        /// </summary>
        [Test]
        public void OffsetTableGrowthKeepsPrefixSumsAtEveryCapacityBoundary()
        {
            var table = new ItemOffsetTable();
            table.BeginWrite(3);
            table.SetLength(0, 40f);
            table.SetLength(1, 90f);
            table.SetLength(2, 30f);
            table.Build(10f, Direction.Vertical, 0);

            var lengths = new List<float> { 40f, 90f, 30f };
            Assert.That(table.TotalLength, Is.EqualTo(40f + 90f + 30f + 20f).Within(0.01f), "初始总长。");

            for (int i = 3; i < 260; i++)
            {
                float length = 20f + (i % 5) * 15f;
                Assert.That(table.TryBeginAppend(i + 1, out int previousCount), Is.True);
                Assert.That(previousCount, Is.EqualTo(i));
                table.AppendLength(i, length);
                table.CommitAppend(10f, Direction.Vertical, i);
                lengths.Add(length);

                float expected = 0f;
                for (int k = 0; k < lengths.Count; k++)
                {
                    expected += lengths[k];
                }

                expected += 10f * (lengths.Count - 1);
                if (Mathf.Abs(table.TotalLength - expected) > 0.05f)
                {
                    var dump = new System.Text.StringBuilder();
                    for (int k = 0; k <= i; k++)
                    {
                        dump.Append(table.LengthAt(k).ToString("F1")).Append('|');
                    }

                    Assert.Fail($"count={lengths.Count} total={table.TotalLength:F2} expected={expected:F2} lengths=[{dump}]");
                }
                Assert.That(table.Count, Is.EqualTo(lengths.Count));
                Assert.That(table.LengthAt(i), Is.EqualTo(length).Within(0.01f));

                // 中段抽查：PositionAt 与逐项求和在每个边界上都一致
                if (lengths.Count % 32 == 0)
                {
                    int mid = lengths.Count / 2;
                    float prefix = 0f;
                    for (int k = 0; k < mid; k++)
                    {
                        prefix += lengths[k];
                    }

                    Assert.That(table.PositionAt(mid), Is.EqualTo(prefix + 10f * mid).Within(0.05f),
                        $"{lengths.Count} 条时中段位置错误。");
                }
            }
        }

        // -----------------------------------------------------------------------------------------
        // Chat：改高首条（窗口 BtnActionC：DeclaredLength += 36 后 RefreshItem(0, true)）
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// 首条在视口内时改高：内容高度必须精确 +delta，首条及其后所有可见行的位置必须跟着位移，
        /// 相邻行间距保持 spacing，可见区间不能跳变。
        /// </summary>
        [Test]
        public void ChatResizeFirstItemKeepsFollowingRowsAligned()
        {
            var layoutManager = new LinearLayoutManager { VariableItemLength = true };
            RecyclerViewFixture target = CreateFixture(layoutManager, 56f);
            Scroller targetScroller = AttachScroller(target);
            RecyclerView view = target.View;
            view.Padding = new Vector2(PaddingX, PaddingY);
            view.Spacing = new Vector2(0f, 12f);
            var adapter = fixture.Bind(new Adapter<ScrollProbeData>(view));

            var data = new List<ScrollProbeData>();
            for (int i = 0; i < 20; i++)
            {
                data.Add(new ScrollProbeData { TemplateId = 0, DeclaredLength = 56f + (i % 3) * 24f });
            }

            adapter.SetList(data);
            view.ScrollToIndex(0, false);

            Assert.That(view.TryGetVisibleRange(out int startBefore, out int endBefore), Is.True);
            Assert.That(startBefore, Is.Zero, "测试前提：必须停在顶部。");

            float contentBefore = layoutManager.ContentSize.y;
            float maxBefore = targetScroller.MaxPosition;
            float length0Before = layoutManager.GetItemLength(0);
            float length1 = layoutManager.GetItemLength(1);
            Assert.That(view.TryGetViewHolder(1, out ViewHolder holder1Before), Is.True);
            float y1Before = holder1Before.RectTransform.anchoredPosition.y;

            const float delta = 36f;
            float measuredLength = -1f;
            int measuredIndex = -2;
            view.OnItemResized += (index, length) =>
            {
                measuredIndex = index;
                measuredLength = length;
            };

            data[0].DeclaredLength += delta;
            adapter.NotifyItemChanged(0, true);
            // 诊断信息：无论走"测量"还是"声明长度重算"，最终几何都必须正确。
            string measurement = "OnItemResized index=" + measuredIndex + " length=" + measuredLength.ToString("F1");

            Assert.That(layoutManager.GetItemLength(0), Is.EqualTo(length0Before + delta).Within(0.5f),
                "改高后偏移表首条长度未更新。");
            Assert.That(layoutManager.ContentSize.y, Is.EqualTo(contentBefore + delta).Within(0.5f),
                "改高后内容高度必须精确增加 delta。");
            Assert.That(targetScroller.MaxPosition, Is.EqualTo(maxBefore + delta).Within(0.5f),
                "改高后最大滚动位置必须同步增加 delta。");
            Assert.That(layoutManager.GetItemStartPosition(1),
                Is.EqualTo(layoutManager.GetItemStartPosition(0) + length0Before + delta + 12f).Within(0.5f),
                "改高后第 2 条的起点必须按新高度顺延。");

            Assert.That(view.TryGetVisibleRange(out int startAfter, out int endAfter), Is.True);
            Assert.That(startAfter, Is.EqualTo(startBefore), "改高后可见起点不应跳变。");
            Assert.That(endAfter, Is.EqualTo(endBefore), "改高后可见终点不应跳变。");

            // 后一行必须跟着下移 delta（anchoredPosition.y 减少 delta）
            Assert.That(view.TryGetViewHolder(1, out ViewHolder holder1After), Is.True);
            float y1After = holder1After.RectTransform.anchoredPosition.y;
            Assert.That(y1After, Is.EqualTo(y1Before - delta).Within(0.5f),
                "改高首条后，第 2 行的位置没有随新高度下移（会与首条重叠或留缝）。");

            // 首条自身高度必须是新高度
            Assert.That(view.TryGetViewHolder(0, out ViewHolder holder0), Is.True);
            Assert.That(holder0.RectTransform.rect.height, Is.EqualTo(length0Before + delta).Within(0.5f),
                "改高后首条的显示高度没有更新。");

            // 相邻间距仍然是 spacing（居中布局下 y 差 == (len0+len1)/2 + spacing）
            float expectedGap = ((length0Before + delta) + length1) * 0.5f + 12f;
            Assert.That(holder0.RectTransform.anchoredPosition.y - y1After, Is.EqualTo(expectedGap).Within(0.75f),
                "改高后首条与第 2 条的间距错误（重叠/穿透）。(" + measurement + ")");
        }

        /// <summary>
        /// 首条在视口外时改高：当前视口内容必须保持不动（锚点不跳），但内容高度与滚动范围要同步增长。
        /// </summary>
        [Test]
        public void ChatResizeOffscreenFirstItemKeepsCurrentViewportStable()
        {
            var layoutManager = new LinearLayoutManager { VariableItemLength = true };
            RecyclerViewFixture target = CreateFixture(layoutManager, 56f);
            Scroller targetScroller = AttachScroller(target);
            RecyclerView view = target.View;
            view.Padding = new Vector2(PaddingX, PaddingY);
            view.Spacing = new Vector2(0f, 12f);
            var adapter = fixture.Bind(new Adapter<ScrollProbeData>(view));

            var data = new List<ScrollProbeData>();
            for (int i = 0; i < 40; i++)
            {
                data.Add(new ScrollProbeData { TemplateId = 0, DeclaredLength = 56f });
            }

            adapter.SetList(data);
            view.ScrollToIndex(30, false);
            Assert.That(view.TryGetVisibleRange(out int startBefore, out _), Is.True);
            Assert.That(startBefore, Is.GreaterThan(0), "测试前提：首条必须在视口外。");

            float contentBefore = layoutManager.ContentSize.y;
            Assert.That(view.TryGetViewHolder(startBefore, out ViewHolder anchorHolderBefore), Is.True);
            float anchorYBefore = anchorHolderBefore.RectTransform.anchoredPosition.y;

            float positionBefore = targetScroller.Position;
            float maxBefore = targetScroller.MaxPosition;
            data[0].DeclaredLength += 40f;
            adapter.NotifyItemChanged(0, true);

            Assert.That(layoutManager.ContentSize.y, Is.EqualTo(contentBefore + 40f).Within(0.5f),
                "视口外改高后内容高度必须增加。");
            Assert.That(view.TryGetVisibleRange(out int startAfter, out int endAfter), Is.True);
            Assert.That(startAfter, Is.LessThanOrEqualTo(startBefore), "视口外改高导致可视锚点向下漂移。");
            Assert.That(startAfter, Is.InRange(startBefore - 1, startBefore), "视口外改高导致可视区间整体位移。");
            Assert.That(startBefore, Is.InRange(startAfter, endAfter), "锚点项掉出了可视区间。");
            Assert.That(view.TryGetViewHolder(startBefore, out ViewHolder anchorHolderAfter), Is.True);
            float anchorYAfter = anchorHolderAfter.RectTransform.anchoredPosition.y;
            string diag = " posBefore=" + positionBefore.ToString("F1") + " posAfter=" + targetScroller.Position.ToString("F1")
                + " maxBefore=" + maxBefore.ToString("F1") + " maxAfter=" + targetScroller.MaxPosition.ToString("F1")
                + " content=" + layoutManager.ContentSize.y.ToString("F1")
                + " yBefore=" + anchorYBefore.ToString("F1") + " yAfter=" + anchorYAfter.ToString("F1");
            Assert.That(anchorYAfter, Is.EqualTo(anchorYBefore).Within(0.5f),
                "视口外改高导致当前视口内容跳动。" + diag);
        }

        // -----------------------------------------------------------------------------------------
        // Chat：Linear + variableItemLength
        // -----------------------------------------------------------------------------------------

        [Test]
        public void VariableLengthAppendKeepsScrollRange()
        {
            var layoutManager = new LinearLayoutManager { VariableItemLength = true };
            RecyclerViewFixture target = CreateFixture(layoutManager, 56f);
            Scroller targetScroller = AttachScroller(target);
            RecyclerView view = target.View;
            view.Padding = new Vector2(PaddingX, PaddingY);
            view.Spacing = new Vector2(0f, 12f);
            var adapter = fixture.Bind(new Adapter<ScrollProbeData>(view));

            const int itemCount = 20;
            const float itemHeight = 56f;
            const float spacingY = 12f;
            adapter.SetList(BuildData(itemCount, itemHeight));

            float expected = itemCount * itemHeight + (itemCount - 1) * spacingY + PaddingY * 2f;
            Assert.That(layoutManager.ContentSize.y, Is.EqualTo(expected).Within(0.5f), "初始内容高度必须包含间距与上下 padding。");
            Assert.That(targetScroller.MaxPosition, Is.EqualTo(expected - ViewportHeight).Within(0.5f));

            adapter.Add(new ScrollProbeData { DeclaredLength = itemHeight });

            expected += itemHeight + spacingY;
            Assert.That(layoutManager.ContentSize.y, Is.EqualTo(expected).Within(0.5f), "追加一项后内容高度必须增长。");
            Assert.That(targetScroller.MaxPosition, Is.EqualTo(expected - ViewportHeight).Within(0.5f), "追加后最大滚动位置必须增长，否则列表看起来滚不动。");

            targetScroller.ScrollTo(targetScroller.MaxPosition, false);
            Assert.That(targetScroller.Position, Is.EqualTo(targetScroller.MaxPosition).Within(0.5f));

            Assert.That(view.TryGetVisibleRange(out int start, out int end), Is.True);
            Assert.That(end, Is.EqualTo(itemCount), "贴底时最后一项必须是新追加的那条。");
            Assert.That(start, Is.GreaterThan(0));
        }

        [Test]
        public void VariableLengthStickToEndSurvivesRepeatedAppends()
        {
            var layoutManager = new LinearLayoutManager { VariableItemLength = true };
            RecyclerViewFixture target = CreateFixture(layoutManager, 56f);
            Scroller targetScroller = AttachScroller(target);
            RecyclerView view = target.View;
            view.Padding = new Vector2(PaddingX, PaddingY);
            view.Spacing = new Vector2(0f, 12f);
            var adapter = fixture.Bind(new Adapter<ScrollProbeData>(view));
            adapter.SetList(BuildData(12, 56f));

            for (int i = 0; i < 6; i++)
            {
                adapter.Add(new ScrollProbeData { DeclaredLength = 56f });
                Assert.That(layoutManager.ContentSize.y, Is.GreaterThan(0f),
                    $"第 {i} 次追加后内容高度异常: content={layoutManager.ContentSize.y}");
                Assert.That(targetScroller.MaxPosition, Is.GreaterThan(0f),
                    $"第 {i} 次追加后最大滚动位置异常: " + Describe(layoutManager, targetScroller, adapter));

                view.StickToEnd();

                Assert.That(targetScroller.Position, Is.EqualTo(targetScroller.MaxPosition).Within(0.5f),
                    $"第 {i} 次贴底失败: " + Describe(layoutManager, targetScroller, adapter));
            }

            Assert.That(view.TryGetVisibleRange(out _, out int end), Is.True);
            Assert.That(end, Is.EqualTo(17));
        }

        // -----------------------------------------------------------------------------------------
        // Mixed：MixedLayoutManager + variableItemLength + 3 模板
        // -----------------------------------------------------------------------------------------

        [Test]
        public void MixedTemplateAppendKeepsScrollRange()
        {
            var layoutManager = new MixedLayoutManager { VariableItemLength = true };
            RecyclerViewFixture target = CreateFixture(layoutManager, 56f, 96f, 140f);
            Scroller targetScroller = AttachScroller(target);
            RecyclerView view = target.View;
            view.Padding = new Vector2(PaddingX, PaddingY);
            view.Spacing = new Vector2(12f, 10f);
            var adapter = fixture.Bind(new MixedAdapter<ScrollProbeData>(view));

            var data = new List<ScrollProbeData>();
            for (int i = 0; i < 30; i++)
            {
                int templateId = i % 3;
                data.Add(new ScrollProbeData { TemplateId = templateId, DeclaredLength = 0f });
            }

            adapter.SetList(data);

            // 声明长度为 0：走模板高度（56 / 96 / 140），每次循环 292。
            float expected = 10f * (56f + 96f + 140f) + 29 * 10f + PaddingY * 2f;
            Assert.That(layoutManager.ContentSize.y, Is.EqualTo(expected).Within(0.5f), "多模板内容高度必须按模板高度累加。");
            Assert.That(targetScroller.MaxPosition, Is.EqualTo(expected - ViewportHeight).Within(0.5f));

            adapter.Add(new ScrollProbeData { TemplateId = 0, DeclaredLength = 0f });

            expected += 56f + 10f;
            Assert.That(layoutManager.ContentSize.y, Is.EqualTo(expected).Within(0.5f), "追加短项后内容高度必须增长。");
            Assert.That(targetScroller.MaxPosition, Is.EqualTo(expected - ViewportHeight).Within(0.5f));

            targetScroller.ScrollTo(targetScroller.MaxPosition, false);
            Assert.That(view.TryGetVisibleRange(out _, out int end), Is.True);
            Assert.That(end, Is.EqualTo(30));
        }

        // -----------------------------------------------------------------------------------------
        // Group：MixedLayoutManager + 固定模板长度（group 头 + 子项）
        // -----------------------------------------------------------------------------------------

        [Test]
        public void MixedFixedTemplateAppendKeepsScrollRange()
        {
            var layoutManager = new MixedLayoutManager();
            RecyclerViewFixture target = CreateFixture(layoutManager, 40f, 36f);
            Scroller targetScroller = AttachScroller(target);
            RecyclerView view = target.View;
            view.Padding = new Vector2(PaddingX, PaddingY);
            view.Spacing = new Vector2(0f, 10f);
            var adapter = fixture.Bind(new MixedAdapter<ScrollProbeData>(view));

            var data = new List<ScrollProbeData>();
            for (int i = 0; i < 12; i++)
            {
                data.Add(new ScrollProbeData { TemplateId = i % 2 == 0 ? 0 : 1, DeclaredLength = 0f });
            }

            adapter.SetList(data);

            float expected = 6f * (40f + 36f) + 11 * 10f + PaddingY * 2f;
            Assert.That(layoutManager.ContentSize.y, Is.EqualTo(expected).Within(0.5f));

            adapter.Add(new ScrollProbeData { TemplateId = 1, DeclaredLength = 0f });

            expected += 36f + 10f;
            Assert.That(layoutManager.ContentSize.y, Is.EqualTo(expected).Within(0.5f), "固定模板长度追加后内容高度必须增长。");
            Assert.That(targetScroller.MaxPosition, Is.EqualTo(expected - ViewportHeight).Within(0.5f));
        }

        [Test]
        public void MixedFixedTemplateScrollsToArbitraryItem()
        {
            var layoutManager = new MixedLayoutManager();
            RecyclerViewFixture target = CreateFixture(layoutManager, 40f, 36f);
            Scroller targetScroller = AttachScroller(target);
            RecyclerView view = target.View;
            view.Padding = new Vector2(PaddingX, PaddingY);
            view.Spacing = new Vector2(0f, 10f);
            var adapter = fixture.Bind(new MixedAdapter<ScrollProbeData>(view));

            var data = new List<ScrollProbeData>();
            for (int i = 0; i < 40; i++)
            {
                data.Add(new ScrollProbeData { TemplateId = i % 2 == 0 ? 0 : 1, DeclaredLength = 0f });
            }

            adapter.SetList(data);
            targetScroller.ScrollTo(0f, false);

            // 每条 = 模板高度 (40/36) + 10 间距，第 i 条的起点必须能被精确定位。
            float expectedStart = 0f;
            for (int i = 0; i < 10; i++)
            {
                expectedStart += (i % 2 == 0 ? 40f : 36f) + 10f;
            }

            Assert.That(layoutManager.GetItemStartPosition(10), Is.EqualTo(expectedStart + PaddingY).Within(0.5f));

            targetScroller.ScrollTo(layoutManager.GetItemStartPosition(10), false);

            Assert.That(layoutManager.PositionToIndex(targetScroller.Position), Is.EqualTo(10));
        }

        // -----------------------------------------------------------------------------------------
        // Group 展开/折叠：单条追加跨过 next 链容量边界后仍然可用
        // -----------------------------------------------------------------------------------------

        [Test]
        public void GroupedAppendAcrossCapacityGrowthKeepsExpansionConsistent()
        {
            var layoutManager = new LinearLayoutManager();
            fixture = new RecyclerViewFixture(new Vector2(ViewportWidth, ViewportHeight), layoutManager);
            fixture.CreateTemplate<NavigationItem>(new Vector2(ViewportWidth - PaddingX * 2f, 40f));
            fixture.CreateTemplate<NavigationItem>(new Vector2(ViewportWidth - PaddingX * 2f, 36f));
            RecyclerView view = fixture.View;
            var adapter = fixture.Bind(new GroupAdapter<NavigationData>(view, 0));
            adapter.SetList(new List<NavigationData>());

            // 8 条同组数据：初始 next 链容量正好 8。
            for (int i = 0; i < 8; i++)
            {
                adapter.Add(new NavigationData { TemplateId = 1, Type = 0 });
            }

            adapter.Expand(0);
            Assert.That(adapter.GetItemCount(), Is.EqualTo(9), "组头 1 + 子项 8。");

            // 触发 next 链扩容（8 -> 16）：旧实现会丢掉所有链接。
            adapter.Add(new NavigationData { TemplateId = 1, Type = 0 });
            Assert.That(adapter.GetItemCount(), Is.EqualTo(10));

            adapter.Collapse(0);
            Assert.That(adapter.GetItemCount(), Is.EqualTo(1), "折叠后只剩组头。");

            adapter.Expand(0);
            Assert.That(adapter.GetItemCount(), Is.EqualTo(10), "重新展开必须回到 9 个子项，而不是链表断裂后的错误数量。");
        }

        // -----------------------------------------------------------------------------------------
        // Mixed（多模板 + 变长）：复刻 UIRecyclerViewExampleWindow 的 Mixed 页“不停点追加”
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// 30 条初始数据（模板 0/1/2 循环）+ 连点"追加短项" 50 次 + 连点"追加长项" 50 次
        /// + 交替/同帧连点 60 次 + 贴底连点 30 次；每次追加后校验内容高度、前缀和与可见项间距。
        /// 覆盖偏移表容量 32 → 64 → 128 → 256 的每一次扩容。
        /// </summary>
        [Test]
        public void MixedRepeatedAppendsKeepTablesAndGeometryConsistent()
        {
            var layoutManager = new MixedLayoutManager { VariableItemLength = true };
            RecyclerViewFixture target = CreateFixture(layoutManager, 72f, 112f, 168f);
            Scroller targetScroller = AttachScroller(target);
            RecyclerView view = target.View;
            view.Padding = new Vector2(PaddingX, PaddingY);
            view.Spacing = new Vector2(12f, 10f);
            var adapter = fixture.Bind(new MixedAdapter<ScrollProbeData>(view));

            var data = new List<ScrollProbeData>();
            for (int i = 0; i < 30; i++)
            {
                data.Add(new ScrollProbeData { TemplateId = i % 3, DeclaredLength = 0f });
            }

            adapter.SetList(data);

            const float spacingY = 10f;
            float lastContent = layoutManager.ContentSize.y;

            void Verify(string context, bool expectGrowth)
            {
                int count = adapter.GetItemCount();
                float sum = 0f;
                for (int i = 0; i < count; i++)
                {
                    sum += layoutManager.GetItemLength(i);
                }

                float expectedContent = sum + spacingY * (count - 1) + PaddingY * 2f;
                Assert.That(layoutManager.ContentSize.y, Is.EqualTo(expectedContent).Within(0.5f),
                    $"{context}: 内容高度必须等于 Σ项高 + 间距 + padding（Fenwick 前缀和与逐项长度一致）。");

                if (expectGrowth)
                {
                    Assert.That(layoutManager.ContentSize.y, Is.GreaterThan(lastContent), $"{context}: 追加后内容高度必须增长。");
                }

                lastContent = layoutManager.ContentSize.y;
                Assert.That(targetScroller.MaxPosition,
                    Is.EqualTo(layoutManager.ContentSize.y - layoutManager.ViewportSize.y).Within(0.5f),
                    $"{context}: 最大滚动位置必须跟随内容高度。");

                int mid = count / 2;
                float prefix = 0f;
                for (int i = 0; i < mid; i++)
                {
                    prefix += layoutManager.GetItemLength(i);
                }

                Assert.That(layoutManager.GetItemStartPosition(mid),
                    Is.EqualTo(PaddingY + prefix + spacingY * mid).Within(0.5f),
                    $"{context}: 中段项起点必须等于前缀和（表未漂移）。");

                // 可见项间距：居中布局下相邻 y 差 == (len_i + len_{i+1}) / 2 + spacing
                int previousIndex = int.MinValue;
                float previousY = 0f;
                float previousLength = 0f;
                for (int i = 0; i < view.ViewProvider.VisibleCount; i++)
                {
                    ViewHolder holder = view.ViewProvider.GetVisibleViewHolder(i);
                    if (holder == null)
                    {
                        continue;
                    }

                    float y = holder.RectTransform.anchoredPosition.y;
                    float length = layoutManager.GetItemLength(holder.Index);
                    if (previousIndex != int.MinValue)
                    {
                        float expectedDelta = (previousLength + length) * 0.5f + spacingY;
                        Assert.That(previousY - y, Is.EqualTo(expectedDelta).Within(0.75f),
                            $"{context}: 第 {holder.Index} 行与上一行的间距错误（重叠/穿透）。");
                    }

                    previousIndex = holder.Index;
                    previousY = y;
                    previousLength = length;
                }
            }

            // 连点“追加短项”
            for (int i = 0; i < 50; i++)
            {
                adapter.Add(new ScrollProbeData { TemplateId = 0, DeclaredLength = 0f });
                Verify("addShort#" + i, true);
            }

            // 连点“追加长项”
            for (int i = 0; i < 50; i++)
            {
                adapter.Add(new ScrollProbeData { TemplateId = 2, DeclaredLength = 0f });
                Verify("addTall#" + i, true);
            }

            // 交替 + 同帧连点
            for (int i = 0; i < 20; i++)
            {
                adapter.Add(new ScrollProbeData { TemplateId = 0, DeclaredLength = 0f });
                adapter.Add(new ScrollProbeData { TemplateId = 2, DeclaredLength = 0f });
                adapter.Add(new ScrollProbeData { TemplateId = 1, DeclaredLength = 0f });
                Verify("interleaved#" + i, true);
            }

            // 刷新第 1 条（对应 BtnActionC），不要求增长
            adapter.NotifyItemChanged(1, true);
            Verify("refresh", false);

            // 贴底后继续连点
            view.StickToEnd();
            for (int i = 0; i < 30; i++)
            {
                adapter.Add(new ScrollProbeData { TemplateId = 2, DeclaredLength = 0f });
                view.StickToEnd();
                Verify("pinned#" + i, true);
                Assert.That(view.IsAtEnd, Is.True, $"pinned#{i}: 贴底追加后必须仍在末尾。");
            }

            Assert.That(view.TryGetVisibleRange(out _, out int end), Is.True);
            Assert.That(end, Is.EqualTo(adapter.GetItemCount() - 1), "末尾最后一条必须可见。");
        }
    }
}
