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
            public AudioClip Clip;
        }

        [SerializeField] private UXUiAudioOverrideMode _mode = UXUiAudioOverrideMode.Overlay;
        [SerializeField] private Entry[] _entries;

        public UXUiAudioOverrideMode Mode => _mode;

        public bool TryResolve(UXUiCue cue, UXUiAudioDevice device, out AudioClip clip)
        {
            clip = null;
            if (_entries == null)
            {
                return false;
            }

            for (int i = 0; i < _entries.Length; i++)
            {
                Entry entry = _entries[i];
                if (entry.Cue != cue || (entry.Devices & device) == 0)
                {
                    continue;
                }

                clip = entry.Mute ? null : entry.Clip;
                return true;
            }

            return false;
        }
    }
}
