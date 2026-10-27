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
            public bool AllowProgrammaticFocus;
            public AudioClip Clip;
        }

        [SerializeField] private Rule[] _rules;

        internal bool TryResolve(UXUiCue cue, UXUiAudioDevice device, out Rule rule)
        {
            if (_rules != null)
            {
                for (int i = 0; i < _rules.Length; i++)
                {
                    rule = _rules[i];
                    if (rule.Cue == cue && (rule.Devices & device) != 0)
                        return true;
                }
            }

            rule = default;
            return false;
        }

#if UNITY_EDITOR
        private void Reset()
        {
            _rules = new[]
            {
                new Rule { Cue = UXUiCue.PointerEnter, Devices = UXUiAudioDevice.KeyboardMouse },
                new Rule { Cue = UXUiCue.FocusEnter, Devices = UXUiAudioDevice.Gamepad | UXUiAudioDevice.Joystick },
                new Rule { Cue = UXUiCue.Press, Devices = UXUiAudioDevice.All },
            };
        }
#endif
    }
}
