using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AlicizaX.UI.NavigationTests
{
    public abstract class RecyclerRefreshCases
    {
        private RecyclerViewFixture fixture;

        [TearDown]
        public void TearDown()
        {
            fixture?.Dispose();
            fixture = null;
        }

        [Test]
        public void DisableAndEnableFlushDeferredRefresh()
        {
            var layoutManager = new LinearLayoutManager();
            fixture = new RecyclerViewFixture(new Vector2(200f, 96f), layoutManager);
            fixture.CreateTemplate<NavigationItem>(new Vector2(200f, 32f));
            Scroller scroller = fixture.AddScroller();
            var data = new List<NavigationData> { new(), new(), new(), new() };
            var adapter = fixture.Bind(new Adapter<NavigationData>(fixture.View, data));
            adapter.NotifyDataChanged();
            Assert.That(layoutManager.ContentSize.y, Is.EqualTo(128f));

            fixture.View.RefreshMode = ListRefreshMode.AfterSettle;
            scroller.ScrollToDuration(64f, 10f);
            Assert.That(scroller.IsMoving, Is.True);

            data.Add(new NavigationData());
            data.Add(new NavigationData());
            data.Add(new NavigationData());
            data.Add(new NavigationData());
            fixture.View.RequestLayout();
            Assert.That(fixture.View.LayoutManager.ContentSize.y, Is.EqualTo(128f));

            RecyclerViewFixture.CallPrivate(fixture.View, "OnDisable");
            RecyclerViewFixture.CallPrivate(fixture.View, "OnEnable");

            Assert.That(fixture.View.LayoutManager.ContentSize.y, Is.EqualTo(256f));
            Assert.That(fixture.View.TryGetVisibleRange(out int start, out int end), Is.True);
            Assert.That(start, Is.Zero);
            Assert.That(end, Is.EqualTo(3));
        }
    }
}
