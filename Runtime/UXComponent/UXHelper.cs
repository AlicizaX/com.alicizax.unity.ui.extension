using AlicizaX;
using UnityEngine;
using AudioType = AlicizaX.Audio.Runtime.AudioType;

internal static class UXComponentExtensionsHelper
{
    public static string GetString(string key)
    {
        if (!AppServices.HasWorld) return key;
        return GameApp.Localization.GetString(key);
    }

    public static void PlayAudio(AudioClip clip)
    {
#if UXNAVIGATION_SUPPORT
        // 仅拦截 Navigation 程序化 SetSelected 同步触发的 OnSelect 音效。
        if (AlicizaX.UI.UXNavigation.UXSelectionAudio.IsSuppressed)
            return;
#endif
        if (!AppServices.HasWorld) return;
        GameApp.Audio.Play(AudioType.UISound, clip);
    }
}
