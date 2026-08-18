using System;

namespace AlicizaX.UI.UXFeedback
{
    public enum UXUiCue : byte
    {
        PointerEnter = 0,
        PointerExit = 1,
        FocusEnter = 2,
        FocusExit = 3,
        Press = 4,
        ToggleOn = 5,
        ToggleOff = 6,
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
        public const int CueCount = 7;
        public const int DeviceCount = 4;

        public static int ToIndex(UXUiAudioDevice device)
        {
            switch (device)
            {
                case UXUiAudioDevice.Touch:
                    return 0;
                case UXUiAudioDevice.KeyboardMouse:
                    return 1;
                case UXUiAudioDevice.Gamepad:
                    return 2;
                case UXUiAudioDevice.Joystick:
                    return 3;
                default:
                    return 1;
            }
        }

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
