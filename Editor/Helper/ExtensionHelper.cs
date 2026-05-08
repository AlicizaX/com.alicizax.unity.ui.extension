using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityEditor.Extensions
{
    internal static class ExtensionHelper
    {
        public static void PreviewAudioClip(AudioClip clip)
        {
            if (clip == null) return;

            // 停止当前播放的所有预览音频
            Assembly audioUtilAssembly = typeof(AudioImporter).Assembly;
            Type audioUtilType = audioUtilAssembly.GetType("UnityEditor.AudioUtil");
            MethodInfo stopMethod = audioUtilType.GetMethod("StopAllPreviewClips",
                BindingFlags.Static | BindingFlags.Public);
            stopMethod.Invoke(null, null);

            // 播放音频
            MethodInfo playMethod = audioUtilType.GetMethod("PlayPreviewClip",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new System.Type[] { typeof(AudioClip), typeof(int), typeof(bool) },
                null);
            playMethod.Invoke(null, new object[] { clip, 0, false });

            // 设置播放进度（可选）
            MethodInfo setTimeMethod = audioUtilType.GetMethod("SetPreviewClipSamplePosition",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new System.Type[] { typeof(AudioClip), typeof(int) },
                null);
            setTimeMethod.Invoke(null, new object[] { clip, 0 });
        }
    }
}
