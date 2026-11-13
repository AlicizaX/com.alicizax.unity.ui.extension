using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AlicizaX.UI.NavigationTests
{
    public sealed class InteractionTestData : ISimpleViewData
    {
    }

    public sealed class InteractionTestItem : InteractiveViewHolder<InteractionTestData>
    {
        public int Clicks;
        public bool Allow = true;
        public int FocusNotifications;
        public bool LastFocus;

        protected override bool CanActivate(InteractionTestData data, int index) => Allow;

        protected override void OnBind(InteractionTestData data, int index)
        {
        }

        protected override void OnItemClick(InteractionTestData data, int index)
        {
            Clicks++;
        }

        protected override void OnNavigationFocused(bool focused)
        {
            FocusNotifications++;
            LastFocus = focused;
        }

        public void PushFocus(bool focused) => ((IRecyclerViewNavigationViewHolder)this).SetNavigationFocused(focused);
    }

    public abstract class InteractionCases
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }

            root = null;
        }

        private static PointerEventData LeftPointer()
        {
            return new PointerEventData(null) { button = PointerEventData.InputButton.Left };
        }

        private InteractionTestItem CreateItem(bool withButton)
        {
            root = new GameObject("interaction-item", typeof(RectTransform));
            if (withButton)
            {
                root.AddComponent<UXButton>();
            }

            InteractionTestItem holder = root.AddComponent<InteractionTestItem>();
            typeof(InteractiveViewHolder<InteractionTestData>)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(holder, null);
            return holder;
        }

        [Test]
        public void FocusNotificationsAreDedupedPerBinding()
        {
            InteractionTestItem holder = CreateItem(false);
            holder.Bind(new InteractionTestData(), 0);

            holder.PushFocus(false);
            Assert.That(holder.FocusNotifications, Is.Zero, "从未聚焦过的项推送 false 不回调");

            holder.PushFocus(true);
            holder.PushFocus(true);
            Assert.That(holder.FocusNotifications, Is.EqualTo(1), "同一绑定重复推送 true 只回调一次");

            holder.Bind(new InteractionTestData(), 0);          // 原地重绑同一索引（NotifyItemChanged）
            holder.PushFocus(true);
            Assert.That(holder.FocusNotifications, Is.EqualTo(2), "新数据必须重新收到焦点");

            holder.PushFocus(false);
            holder.PushFocus(false);
            Assert.That(holder.FocusNotifications, Is.EqualTo(3), "重复推送 false 只回调一次");
            Assert.That(holder.LastFocus, Is.False);
        }

        [Test]
        public void InPlaceRebindToOtherDataClearsStaleHighlight()
        {
            InteractionTestItem holder = CreateItem(true);
            UXButton button = root.GetComponent<UXButton>();
            holder.Bind(new InteractionTestData(), 0);
            holder.PushFocus(true);
            Assert.That(button.ExternalState, Is.EqualTo(UXSelectionState.Highlighted));

            // TryReuseVisibleRange：可见项不禁用、不回收，直接换绑到其它数据。
            holder.Bind(new InteractionTestData(), 5);
            holder.PushFocus(false);

            Assert.That(button.ExternalState, Is.EqualTo(UXSelectionState.Normal), "换绑后上一条数据的高亮必须清掉");
            Assert.That(holder.LastFocus, Is.False);
        }

        [Test]
        public void RejectedActivationShowsDisabledState()
        {
            InteractionTestItem holder = CreateItem(true);
            UXButton button = root.GetComponent<UXButton>();
            holder.Bind(new InteractionTestData(), 0);
            holder.Allow = false;
            holder.PushFocus(true);
            Assert.That(button.ExternalState, Is.EqualTo(UXSelectionState.Disabled));
        }

        [Test]
        public void SubmitIsHandledEvenWhenActivationIsRejected()
        {
            InteractionTestItem holder = CreateItem(false);
            holder.Bind(new InteractionTestData(), 0);

            holder.Allow = false;
            Assert.That(holder.HandleNavigationSubmit(), Is.True, "提交视为已处理，不回退成仅业务选中");
            Assert.That(holder.Clicks, Is.Zero);

            holder.Allow = true;
            Assert.That(holder.HandleNavigationSubmit(), Is.True);
            Assert.That(holder.Clicks, Is.EqualTo(1));
        }

        [Test]
        public void OnlyLeftClickOnBoundItemActivates()
        {
            InteractionTestItem holder = CreateItem(false);
            holder.OnPointerClick(LeftPointer());
            Assert.That(holder.Clicks, Is.Zero, "未绑定（或绑定失败）的项点击不激活");

            holder.Bind(new InteractionTestData(), 0);
            holder.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
            Assert.That(holder.Clicks, Is.Zero);

            holder.OnPointerClick(LeftPointer());
            Assert.That(holder.Clicks, Is.EqualTo(1));
        }

        [Test]
        public void HolderDrivesExternalStateOnlyWhileItOwnsInteraction()
        {
            root = new GameObject("interaction-item", typeof(RectTransform));
            UXButton button = root.AddComponent<UXButton>();
            InteractionTestItem holder = root.AddComponent<InteractionTestItem>();
            typeof(InteractiveViewHolder<InteractionTestData>)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(holder, null);

            int clicks = 0;
            button.onClick.AddListener(() => clicks++);
            button.OnPointerClick(LeftPointer());
            Assert.That(clicks, Is.EqualTo(1), "Holder 尚未接管时 UXButton 必须可点击。");
            Assert.That(button.HasExternalState, Is.False);

            holder.OnPointerDown(LeftPointer());
            Assert.That(button.HasExternalState, Is.True);

            button.OnPointerClick(LeftPointer());
            Assert.That(clicks, Is.EqualTo(1), "Holder 接管后 UXButton 原生点击应被屏蔽。");

            // 回收路径：ViewHolderFactory.Reset → SetPooledVisible(false) → UXSelectable.OnDisable → InstantClearState。
            root.SetActive(false);
            Assert.That(button.HasExternalState, Is.False, "回收后必须由 UXSelectable 自己释放外部状态。");
            root.SetActive(true);

            button.OnPointerClick(LeftPointer());
            Assert.That(clicks, Is.EqualTo(2), "Holder 释放后 UXButton 必须恢复可点击。");
        }

        [Test]
        public void ClearExternalStateRestoresSelectableOwnership()
        {
            root = new GameObject("selectable", typeof(RectTransform));
            UXSelectable selectable = root.AddComponent<UXSelectable>();

            selectable.SetExternalState(UXSelectionState.Disabled, true);
            Assert.That(selectable.HasExternalState, Is.True);
            Assert.That(selectable.ExternalState, Is.EqualTo(UXSelectionState.Disabled));

            selectable.ClearExternalState(true);
            Assert.That(selectable.HasExternalState, Is.False);

            Assert.DoesNotThrow(() => selectable.ClearExternalState(true));
        }

        [Test]
        public void ZeroDurationPulseRestoresStateImmediately()
        {
            root = new GameObject("selectable", typeof(RectTransform));
            UXSelectable selectable = root.AddComponent<UXSelectable>();

            selectable.PulseExternalState(UXSelectionState.Pressed, UXSelectionState.Highlighted, 0f);

            Assert.That(selectable.HasExternalState, Is.True);
            Assert.That(selectable.ExternalState, Is.EqualTo(UXSelectionState.Highlighted));
        }
    }
}
