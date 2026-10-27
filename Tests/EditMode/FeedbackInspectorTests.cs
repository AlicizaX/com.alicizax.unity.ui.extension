using AlicizaX.UI.UXFeedback;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AlicizaX.UI.NavigationTests
{
    public sealed class FeedbackInspectorTests
    {
        private Object _target;
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root != null ? _root : _target);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConflictingDeviceMasksCanBeIdentifiedAndRepaired(bool overrideRules)
        {
            if (overrideRules)
            {
                _root = new GameObject("audio-override-test");
                _target = _root.AddComponent<UXUiAudioOverride>();
            }
            else
            {
                _target = ScriptableObject.CreateInstance<UXUiAudioProfile>();
            }

            var serialized = new SerializedObject(_target);
            SerializedProperty rules = serialized.FindProperty(overrideRules ? "_entries" : "_rules");
            rules.arraySize = 4;
            SetRule(rules, 0, UXUiCue.Press, UXUiAudioDevice.KeyboardMouse | UXUiAudioDevice.Gamepad);
            SetRule(rules, 1, UXUiCue.Press, UXUiAudioDevice.Gamepad | UXUiAudioDevice.Joystick);
            SetRule(rules, 2, UXUiCue.FocusEnter, UXUiAudioDevice.Gamepad);
            SetRule(rules, 3, UXUiCue.Press, UXUiAudioDevice.Gamepad);

            Assert.That(UXUiAudioRuleEditor.AvailableDevices(rules, 1), Is.EqualTo(UXUiAudioDevice.Touch | UXUiAudioDevice.Joystick));
            Assert.That(UXUiAudioRuleEditor.HasConflict(rules, 1), Is.True);
            Assert.That(UXUiAudioRuleEditor.HasConflict(rules, 2), Is.False);

            UXUiAudioRuleEditor.RepairOverlaps(rules);
            Assert.That(rules.arraySize, Is.EqualTo(3));
            Assert.That(rules.GetArrayElementAtIndex(0).FindPropertyRelative("Devices").intValue,
                Is.EqualTo((int)(UXUiAudioDevice.KeyboardMouse | UXUiAudioDevice.Gamepad)));
            Assert.That(rules.GetArrayElementAtIndex(1).FindPropertyRelative("Devices").intValue,
                Is.EqualTo((int)UXUiAudioDevice.Joystick));
            Assert.That(UXUiAudioRuleEditor.HasConflict(rules, 1), Is.False);
        }

        private static void SetRule(SerializedProperty rules, int index, UXUiCue cue, UXUiAudioDevice devices)
        {
            SerializedProperty rule = rules.GetArrayElementAtIndex(index);
            rule.FindPropertyRelative("Cue").enumValueIndex = (int)cue;
            rule.FindPropertyRelative("Devices").intValue = (int)devices;
        }
    }
}
