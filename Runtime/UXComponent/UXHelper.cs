using UnityEngine;

public static class UXComponentExtensionsHelper
{
    internal static IUXLocalizationHelper LocalizationHelper;
    internal static IUXAudioHelper AudioHelper;

    public static void SetLocalizationHelper(IUXLocalizationHelper helper)
    {
        LocalizationHelper = helper;
    }

    public static void SetAudioHelper(IUXAudioHelper helper)
    {
        AudioHelper = helper;
    }
}

public interface IUXLocalizationHelper
{
    public string GetString(string key);
}


public interface IUXAudioHelper
{
    void PlayAudio(AudioClip clip);
    void PlayAudio(string clipName);
}
