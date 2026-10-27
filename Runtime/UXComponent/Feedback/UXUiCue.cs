using System;

namespace AlicizaX.UI.UXFeedback
{
    public enum UXUiCue : byte
    {
        PointerEnter = 0,
        Press = 1,
        FocusEnter = 2,
    }

    [Flags]
    public enum UXUiAudioDevice : byte
    {
        None = 0,
        Touch = 1 << 0,
        KeyboardMouse = 1 << 1,
        Gamepad = 1 << 2,
        Joystick = 1 << 3,
        All = Touch | KeyboardMouse | Gamepad | Joystick,
    }

    internal static class UXUiCueUtil
    {
#if INPUTSYSTEM_SUPPORT
        public static UXUiAudioDevice FromInputType(UXInput.Watch.InputType inputType)
        {
            switch (inputType)
            {
                case UXInput.Watch.InputType.Touch:
                    return UXUiAudioDevice.Touch;
                case UXInput.Watch.InputType.KeyboardMouse:
                    return UXUiAudioDevice.KeyboardMouse;
                case UXInput.Watch.InputType.Gamepad:
                    return UXUiAudioDevice.Gamepad;
                case UXInput.Watch.InputType.Joystick:
                    return UXUiAudioDevice.Joystick;
                default:
                    return UXUiAudioDevice.KeyboardMouse;
            }
        }
#endif
    }
}
