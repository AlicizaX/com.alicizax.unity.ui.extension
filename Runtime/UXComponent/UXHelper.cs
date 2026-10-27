using AlicizaX;
using AlicizaX.Audio.Runtime;
using UnityEngine;
using AudioType = AlicizaX.Audio.Runtime.AudioType;

internal static class UXComponentExtensionsHelper
{
    public static void PlayAudio(AudioClip clip)
    {
        if (AppServices.TryGet<IAudioService>(out IAudioService audio))
            audio.Play(AudioType.UISound, clip);
    }
}
