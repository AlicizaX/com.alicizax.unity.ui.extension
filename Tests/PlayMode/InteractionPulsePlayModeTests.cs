using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AlicizaX.UI.NavigationTests
{
    public sealed class InteractionPulsePlayModeTests
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

        private sealed class RecordingGraphic : Graphic
        {
            public int Fades;

            public override void CrossFadeColor(Color targetColor, float duration, bool ignoreTimeScale, bool useAlpha, bool useRGB)
            {
                Fades++;
                base.CrossFadeColor(targetColor, duration, ignoreTimeScale, useAlpha, useRGB);
            }
        }

        private sealed class RecordingSelectable : UXSelectable
        {
            public readonly List<string> AppliedStates = new();

            protected override void DoStateTransition(SelectionState state, bool instant)
            {
                AppliedStates.Add(state.ToString());
                base.DoStateTransition(state, instant);
            }

            public void PulsePressed()
            {
                PulseDerivedState(UXSelectionState.Pressed);
            }
        }

        [UnityTest]
        public IEnumerator RepeatedExternalStateSkipsUnchangedTransition()
        {
            root = new GameObject("external-state", typeof(RectTransform));
            RecordingGraphic graphic = root.AddComponent<RecordingGraphic>();
            UXSelectable selectable = root.AddComponent<UXSelectable>();
            selectable.targetGraphic = graphic;
            yield return null;

            int baseline = graphic.Fades;
            selectable.SetExternalState(UXSelectionState.Highlighted, true);
            Assert.That(graphic.Fades, Is.EqualTo(baseline + 1));

            selectable.SetExternalState(UXSelectionState.Highlighted, true);
            Assert.That(graphic.Fades, Is.EqualTo(baseline + 1),
                "重复设置相同外部状态不应再次执行 transition。");

            selectable.SetExternalState(UXSelectionState.Pressed, true);
            Assert.That(graphic.Fades, Is.EqualTo(baseline + 2));
        }

        [UnityTest]
        public IEnumerator RapidPulsesRestoreDerivedStateOnce()
        {
            root = new GameObject("pulse", typeof(RectTransform));
            RecordingGraphic graphic = root.AddComponent<RecordingGraphic>();
            RecordingSelectable selectable = root.AddComponent<RecordingSelectable>();
            selectable.targetGraphic = graphic;
            yield return null;

            selectable.AppliedStates.Clear();
            selectable.PulsePressed();
            selectable.PulsePressed();
            yield return new WaitForSecondsRealtime(0.25f);

            Assert.That(selectable.AppliedStates, Is.EqualTo(new[]
            {
                "Pressed",
                "Pressed",
                "Normal"
            }), "连续脉冲只允许一次恢复，恢复顺序必须确定。");
        }

        [UnityTest]
        public IEnumerator ExternalPulseRestoresExternalStateAndKeepsOwnership()
        {
            root = new GameObject("external-pulse", typeof(RectTransform));
            RecordingGraphic graphic = root.AddComponent<RecordingGraphic>();
            UXSelectable selectable = root.AddComponent<UXSelectable>();
            selectable.targetGraphic = graphic;
            yield return null;

            selectable.SetExternalState(UXSelectionState.Highlighted, true);
            selectable.PulseExternalState(UXSelectionState.Pressed, UXSelectionState.Highlighted, 0.05f);
            Assert.That(selectable.ExternalState, Is.EqualTo(UXSelectionState.Pressed));

            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(selectable.ExternalState, Is.EqualTo(UXSelectionState.Highlighted));
            Assert.That(selectable.HasExternalState, Is.True);

            selectable.ClearExternalState(true);
            Assert.That(selectable.HasExternalState, Is.False);
        }
    }
}
