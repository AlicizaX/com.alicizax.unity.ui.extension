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

        protected override void OnBind(InteractionTestData data, int index)
        {
        }

        protected override void OnItemClick(InteractionTestData data, int index)
        {
            Clicks++;
        }

        public void ClearForTest()
        {
            OnClear();
        }
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

            holder.ClearForTest();
            Assert.That(button.HasExternalState, Is.False, "回收/解绑后必须释放外部状态。");

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
