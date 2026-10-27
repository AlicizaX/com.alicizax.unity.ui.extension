using System.Reflection;
using AlicizaX.UI.UXFeedback;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AlicizaX.UI.NavigationTests
{
    public abstract class FeedbackCases
    {
        private GameObject _root;
        private UXButton _button;
        private UXUiAudioOverride _override;
        private UXUiAudioProfile _profile;
        private UXUiAudioProfile _alternateProfile;
        private AudioClip _globalClip;
        private AudioClip _localClip;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ui-feedback-test", typeof(RectTransform));
            _button = _root.AddComponent<UXButton>();
            _override = _root.AddComponent<UXUiAudioOverride>();
            _profile = ScriptableObject.CreateInstance<UXUiAudioProfile>();
            _alternateProfile = ScriptableObject.CreateInstance<UXUiAudioProfile>();
            _globalClip = AudioClip.Create("global", 256, 1, 44100, false);
            _localClip = AudioClip.Create("local", 256, 1, 44100, false);
            UXUiAudioRouter.Bind(null);
        }

        [TearDown]
        public void TearDown()
        {
            UXUiAudioRouter.Bind(null);
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_profile);
            Object.DestroyImmediate(_alternateProfile);
            Object.DestroyImmediate(_globalClip);
            Object.DestroyImmediate(_localClip);
        }

        [Test]
        public void OverrideWorksWithoutProfileAndAfterClearingTheme()
        {
            SetEntries(new UXUiAudioOverride.Entry
            {
                Cue = UXUiCue.Press,
                Devices = UXUiAudioDevice.All,
                Clip = _localClip,
            });

            Assert.That(Resolve(UXUiCue.Press), Is.SameAs(_localClip));
            SetRules(new UXUiAudioProfile.Rule { Cue = UXUiCue.Press, Devices = UXUiAudioDevice.All, Clip = _globalClip });
            UXUiAudioRouter.Bind(_profile);
            Assert.That(Resolve(UXUiCue.Press), Is.SameAs(_localClip));
            UXUiAudioRouter.Bind(null);
            Assert.That(Resolve(UXUiCue.Press), Is.SameAs(_localClip));
            Assert.That(Resolve(UXUiCue.PointerEnter), Is.Null);

            SetMode(UXUiAudioOverrideMode.Exclusive);
            Assert.That(Resolve(UXUiCue.Press), Is.SameAs(_localClip));
        }

        [Test]
        public void ThemeCanSwitchAndClear()
        {
            _override.enabled = false;
            SetRules(new UXUiAudioProfile.Rule { Cue = UXUiCue.Press, Devices = UXUiAudioDevice.All, Clip = _globalClip });
            SetField(_alternateProfile, "_rules", new[]
            {
                new UXUiAudioProfile.Rule { Cue = UXUiCue.Press, Devices = UXUiAudioDevice.All, Clip = _localClip },
            });

            UXUiAudioRouter.Bind(_profile);
            Assert.That(Resolve(UXUiCue.Press), Is.SameAs(_globalClip));
            UXUiAudioRouter.Bind(_alternateProfile);
            Assert.That(Resolve(UXUiCue.Press), Is.SameAs(_localClip));
            UXUiAudioRouter.Bind(null);
            Assert.That(Resolve(UXUiCue.Press), Is.Null);
        }

        [Test]
        public void ModesMuteDeviceAndDisabledOverrideFollowPriority()
        {
            SetRules(new UXUiAudioProfile.Rule { Cue = UXUiCue.Press, Devices = UXUiAudioDevice.All, Clip = _globalClip });
            UXUiAudioRouter.Bind(_profile);
            SetEntries(new UXUiAudioOverride.Entry
            {
                Cue = UXUiCue.Press,
                Devices = UXUiAudioDevice.Gamepad,
                Clip = _localClip,
            });

            Assert.That(Resolve(UXUiCue.Press, UXUiAudioDevice.KeyboardMouse), Is.SameAs(_globalClip));
            Assert.That(Resolve(UXUiCue.Press, UXUiAudioDevice.Gamepad), Is.SameAs(_localClip));

            SetEntries(new UXUiAudioOverride.Entry
            {
                Cue = UXUiCue.Press,
                Devices = UXUiAudioDevice.Gamepad,
                Mute = true,
                Clip = _localClip,
            });
            Assert.That(Resolve(UXUiCue.Press, UXUiAudioDevice.Gamepad), Is.Null);

            SetMode(UXUiAudioOverrideMode.Exclusive);
            Assert.That(Resolve(UXUiCue.Press, UXUiAudioDevice.KeyboardMouse), Is.Null);
            SetEntries(new UXUiAudioOverride.Entry { Cue = UXUiCue.Press, Devices = UXUiAudioDevice.Gamepad });
            Assert.That(Resolve(UXUiCue.Press, UXUiAudioDevice.Gamepad), Is.Null);
            SetMode(UXUiAudioOverrideMode.Silent);
            Assert.That(Resolve(UXUiCue.Press), Is.Null);

            _override.enabled = false;
            Assert.That(Resolve(UXUiCue.Press), Is.SameAs(_globalClip));
        }

        [Test]
        public void FocusUsesMatchedRulesPolicyAndFirstMatch()
        {
            const UXUiCue cue = UXUiCue.FocusEnter;
            SetRules(
                new UXUiAudioProfile.Rule { Cue = cue, Devices = UXUiAudioDevice.Gamepad, Clip = _globalClip },
                new UXUiAudioProfile.Rule { Cue = cue, Devices = UXUiAudioDevice.Gamepad, Clip = _localClip, AllowProgrammaticFocus = true });
            UXUiAudioRouter.Bind(_profile);

            Assert.That(Resolve(cue, UXUiAudioDevice.Gamepad), Is.SameAs(_globalClip));
            Assert.That(Resolve(cue, UXUiAudioDevice.Gamepad, true), Is.Null);

            SetRules(new UXUiAudioProfile.Rule
            {
                Cue = cue,
                Devices = UXUiAudioDevice.Gamepad,
                Clip = _globalClip,
                AllowProgrammaticFocus = true,
            });
            Assert.That(Resolve(cue, UXUiAudioDevice.Gamepad, true), Is.SameAs(_globalClip));

            SetEntries(new UXUiAudioOverride.Entry { Cue = cue, Devices = UXUiAudioDevice.Gamepad, Clip = _localClip });
            Assert.That(Resolve(cue, UXUiAudioDevice.Gamepad, true), Is.Null);

            SetEntries(new UXUiAudioOverride.Entry
            {
                Cue = cue,
                Devices = UXUiAudioDevice.Gamepad,
                Clip = _localClip,
                AllowProgrammaticFocus = true,
            });
            Assert.That(Resolve(cue, UXUiAudioDevice.Gamepad, true), Is.SameAs(_localClip));
        }

        private AudioClip Resolve(UXUiCue cue, UXUiAudioDevice device = UXUiAudioDevice.KeyboardMouse, bool programmaticFocus = false)
            => UXUiAudioRouter.ResolveClip(_button, cue, device, programmaticFocus);

        private void SetRules(params UXUiAudioProfile.Rule[] rules)
            => SetField(_profile, "_rules", rules);

        private void SetEntries(params UXUiAudioOverride.Entry[] entries)
            => SetField(_override, "_entries", entries);

        private void SetMode(UXUiAudioOverrideMode mode)
            => SetField(_override, "_mode", mode);

        private static void SetField(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
