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
        if (!AppServices.HasWorld) return;
        GameApp.Audio.Play(AudioType.UISound, clip);
    }
}
