using UnityEngine;

namespace AlicizaX.UI.UXFeedback
{
    [CreateAssetMenu(menuName = "UX/UI Audio Profile", fileName = "UXUiAudioProfile")]
    public sealed class UXUiAudioProfile : ScriptableObject
    {
        [System.Serializable]
        public struct Rule
        {
            public UXUiCue Cue;
            public UXUiAudioDevice Devices;
            public bool IgnoreProgrammatic;
            public AudioClip Clip;
        }

        [SerializeField] private Rule[] _rules;

        private AudioClip[] _clips;
        private bool[] _ignoreProgrammatic;
        private bool _cacheReady;

        public Rule[] Rules => _rules;

        private void OnEnable()
        {
            _cacheReady = false;
        }

        public void EnsureCache()
        {
            if (_cacheReady && _clips != null)
            {
                return;
            }

            BuildCache();
        }

        public void BuildCache()
        {
            int clipCount = UXUiCueUtil.CueCount * UXUiCueUtil.DeviceCount;
            if (_clips == null || _clips.Length != clipCount)
            {
                _clips = new AudioClip[clipCount];
            }
            else
            {
                System.Array.Clear(_clips, 0, _clips.Length);
            }

            if (_ignoreProgrammatic == null || _ignoreProgrammatic.Length != UXUiCueUtil.CueCount)
            {
                _ignoreProgrammatic = new bool[UXUiCueUtil.CueCount];
            }
            else
            {
                System.Array.Clear(_ignoreProgrammatic, 0, _ignoreProgrammatic.Length);
            }

            _ignoreProgrammatic[(int)UXUiCue.FocusEnter] = true;
            _ignoreProgrammatic[(int)UXUiCue.FocusExit] = true;

            if (_rules != null)
            {
                for (int i = 0; i < _rules.Length; i++)
                {
                    Rule rule = _rules[i];
                    int cue = (int)rule.Cue;
                    if ((uint)cue >= UXUiCueUtil.CueCount)
                    {
                        continue;
                    }

                    _ignoreProgrammatic[cue] = rule.IgnoreProgrammatic;

                    AssignClip(cue, UXUiAudioDevice.Touch, rule);
                    AssignClip(cue, UXUiAudioDevice.KeyboardMouse, rule);
                    AssignClip(cue, UXUiAudioDevice.Gamepad, rule);
                    AssignClip(cue, UXUiAudioDevice.Joystick, rule);
                }
            }

            _cacheReady = true;
        }

        public bool IgnoresProgrammatic(UXUiCue cue)
        {
            EnsureCache();
            int index = (int)cue;
            return (uint)index < _ignoreProgrammatic.Length && _ignoreProgrammatic[index];
        }

        public bool TryGetClip(UXUiCue cue, UXUiAudioDevice device, out AudioClip clip)
        {
            EnsureCache();
            int cueIndex = (int)cue;
            if ((uint)cueIndex >= UXUiCueUtil.CueCount)
            {
                clip = null;
                return false;
            }

            clip = _clips[cueIndex * UXUiCueUtil.DeviceCount + UXUiCueUtil.ToIndex(device)];
            return clip != null;
        }

        internal void SetRules(Rule[] rules)
        {
            _rules = rules;
            _cacheReady = false;
        }

        private void AssignClip(int cue, UXUiAudioDevice device, in Rule rule)
        {
            if ((rule.Devices & device) == 0 || rule.Clip == null)
            {
                return;
            }

            _clips[cue * UXUiCueUtil.DeviceCount + UXUiCueUtil.ToIndex(device)] = rule.Clip;
        }

#if UNITY_EDITOR
        private void Reset()
        {
            _rules = CreateDefaultRules(null, null);
            _cacheReady = false;
        }

        private void OnValidate()
        {
            _cacheReady = false;
        }

        internal static Rule[] CreateDefaultRules(AudioClip hover, AudioClip click)
        {
            return new[]
            {
                new Rule
                {
                    Cue = UXUiCue.PointerEnter,
                    Devices = UXUiAudioDevice.KeyboardMouse,
                    IgnoreProgrammatic = false,
                    Clip = hover,
                },
                new Rule
                {
                    Cue = UXUiCue.FocusEnter,
                    Devices = UXUiAudioDevice.Gamepad | UXUiAudioDevice.Joystick,
                    IgnoreProgrammatic = true,
                    Clip = hover,
                },
                new Rule
                {
                    Cue = UXUiCue.Press,
                    Devices = UXUiAudioDevice.All,
                    IgnoreProgrammatic = false,
                    Clip = click,
                },
                new Rule
                {
                    Cue = UXUiCue.ToggleOn,
                    Devices = UXUiAudioDevice.All,
                    IgnoreProgrammatic = false,
                    Clip = click,
                },
                new Rule
                {
                    Cue = UXUiCue.ToggleOff,
                    Devices = UXUiAudioDevice.All,
                    IgnoreProgrammatic = false,
                    Clip = click,
                },
            };
        }
#endif
    }
}
