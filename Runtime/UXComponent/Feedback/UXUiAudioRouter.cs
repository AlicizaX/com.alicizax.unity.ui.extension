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
        }

        internal static void Dispatch(Component source, UXUiCue cue)
        {
            AudioClip clip = ResolveClip(source, cue, CurrentDevice(), IsProgrammaticFocus(cue));
            if (clip != null)
                UXComponentExtensionsHelper.PlayAudio(clip);
        }

        internal static AudioClip ResolveClip(Component source, UXUiCue cue, UXUiAudioDevice device, bool programmaticFocus)
        {
            if (source != null && source.TryGetComponent(out UXUiAudioOverride overrideAudio) && overrideAudio.isActiveAndEnabled)
            {
                UXUiAudioOverrideMode mode = overrideAudio.Mode;
                if (mode == UXUiAudioOverrideMode.Silent)
                    return null;

                if (overrideAudio.TryResolve(cue, device, out UXUiAudioOverride.Entry entry))
                    return entry.Mute || (programmaticFocus && !entry.AllowProgrammaticFocus) ? null : entry.Clip;

                if (mode == UXUiAudioOverrideMode.Exclusive)
                    return null;
            }

            if (_profile != null && _profile.TryResolve(cue, device, out UXUiAudioProfile.Rule rule))
                return programmaticFocus && !rule.AllowProgrammaticFocus ? null : rule.Clip;

            return null;
        }

        private static bool IsProgrammaticFocus(UXUiCue cue)
        {
#if UXNAVIGATION_SUPPORT
            return cue == UXUiCue.FocusEnter
                && AlicizaX.UI.UXNavigation.UXFocusChange.Current == AlicizaX.UI.UXNavigation.UXFocusChange.Cause.Programmatic;
#else
            return false;
#endif
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
