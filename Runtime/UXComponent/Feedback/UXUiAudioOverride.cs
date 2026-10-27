using UnityEngine;

namespace AlicizaX.UI.UXFeedback
{
    public enum UXUiAudioOverrideMode : byte
    {
        Overlay = 0,
        Silent = 1,
        Exclusive = 2,
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("UI/UX Audio Override")]
    public sealed class UXUiAudioOverride : MonoBehaviour
    {
        [System.Serializable]
        public struct Entry
        {
            public UXUiCue Cue;
            public UXUiAudioDevice Devices;
            public bool Mute;
            public bool AllowProgrammaticFocus;
            public AudioClip Clip;
        }

        [SerializeField] private UXUiAudioOverrideMode _mode = UXUiAudioOverrideMode.Overlay;
        [SerializeField] private Entry[] _entries;

        public UXUiAudioOverrideMode Mode => _mode;

        internal bool TryResolve(UXUiCue cue, UXUiAudioDevice device, out Entry entry)
        {
            if (_entries != null)
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    entry = _entries[i];
                    if (entry.Cue == cue && (entry.Devices & device) != 0)
                        return true;
                }
            }

            entry = default;
            return false;
        }
    }
}
