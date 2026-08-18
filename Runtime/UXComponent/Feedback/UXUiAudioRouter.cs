using UnityEngine;

namespace AlicizaX.UI.UXFeedback
{
    public static class UXUiAudioRouter
    {
        private static UXUiAudioProfile _profile;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _profile = null;
        }

        public static void Bind(UXUiAudioProfile profile)
        {
            _profile = profile;
            if (_profile != null)
            {
                _profile.BuildCache();
            }
        }

        internal static void Dispatch(Component source, UXUiCue cue)
        {
            if (_profile == null)
            {
                return;
            }

#if UXNAVIGATION_SUPPORT
            if (AlicizaX.UI.UXNavigation.UXFocusChange.Current == AlicizaX.UI.UXNavigation.UXFocusChange.Cause.Programmatic
                && _profile.IgnoresProgrammatic(cue))
            {
                return;
            }
#endif

            UXUiAudioDevice device = CurrentDevice();
            if (source != null && source.TryGetComponent(out UXUiAudioOverride overrideAudio))
            {
                UXUiAudioOverrideMode mode = overrideAudio.Mode;
                if (mode == UXUiAudioOverrideMode.Silent)
                {
                    return;
                }

                if (overrideAudio.TryResolve(cue, device, out AudioClip overrideClip))
                {
                    if (overrideClip != null)
                    {
                        UXComponentExtensionsHelper.PlayAudio(overrideClip);
                    }

                    return;
                }

                if (mode == UXUiAudioOverrideMode.Exclusive)
                {
                    return;
                }
            }

            if (_profile.TryGetClip(cue, device, out AudioClip clip))
            {
                UXComponentExtensionsHelper.PlayAudio(clip);
            }
        }

        private static UXUiAudioDevice CurrentDevice()
        {
#if INPUTSYSTEM_SUPPORT
            return UXUiCueUtil.FromInputType(UXInput.Watch.CurrentInputType);
#else
            return UXUiAudioDevice.KeyboardMouse;
#endif
        }
    }
}
