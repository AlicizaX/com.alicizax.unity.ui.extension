using UnityEngine;
using AudioType = AlicizaX.Audio.Runtime.AudioType;

internal static class UXComponentExtensionsHelper
{
    public static string GetString(string key)
    {
        return GameApp.Localization.GetString(key);
    }

    public static void PlayAudio(AudioClip clip)
    {
        GameApp.Audio.Play(AudioType.UISound, clip);
    }
}
