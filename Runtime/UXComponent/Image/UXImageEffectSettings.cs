namespace UnityEngine.UI
{
    /// <summary>
    /// Shared helpers for the UXImage adaptive effect.
    /// Materials are owned by the individual <see cref="UXImage"/> that needs them: they are created lazily by
    /// <see cref="CreateEffectMaterial"/>, reused for the lifetime of that Graphic and destroyed in its OnDestroy.
    /// This class keeps no material cache, so animated outline/shadow colors can never grow static GPU memory.
    /// </summary>
    internal static class UXImageEffectSettings
    {
        public const string ShaderName = "UI/UXImageAdaptiveEffect";
        private const string ShaderResourcePath = "UXImageAdaptiveEffect";

        public const float RuntimeSdfSoftnessThreshold = 8f;

        private static Shader s_Shader;

        /// <summary>
        /// Creates a standalone effect material. The caller becomes its owner and must destroy it.
        /// </summary>
        public static Material CreateEffectMaterial()
        {
            Shader shader = ResolveShader();
            if (shader == null)
                return null;

            return new Material(shader)
            {
                name = "UXImage Adaptive Effect",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private static Shader ResolveShader()
        {
            if (s_Shader != null)
                return s_Shader;

            s_Shader = Shader.Find(ShaderName);
            if (s_Shader == null)
                s_Shader = Resources.Load<Shader>(ShaderResourcePath);

            return s_Shader;
        }

        /// <summary>
        /// Destroys a runtime object owned by the effect pipeline (Material, RenderTexture, ...).
        /// Uses DestroyImmediate outside play mode so edit-mode previews do not leak the object.
        /// </summary>
        public static void DestroyRuntimeObject(Object target)
        {
            if (target == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(target);
                return;
            }
#endif
            Object.Destroy(target);
        }

        /// <summary>
        /// Releases the GPU storage of a render texture and then destroys the texture object itself.
        /// </summary>
        public static void DestroyRenderTexture(RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            DestroyRuntimeObject(texture);
        }

        public static void EnsureCanvasChannels(Canvas canvas)
        {
            if (canvas == null)
                return;

            const AdditionalCanvasShaderChannels required =
                AdditionalCanvasShaderChannels.TexCoord1 |
                AdditionalCanvasShaderChannels.TexCoord2 |
                AdditionalCanvasShaderChannels.TexCoord3 |
                AdditionalCanvasShaderChannels.Normal |
                AdditionalCanvasShaderChannels.Tangent;

            if ((canvas.additionalShaderChannels & required) != required)
                canvas.additionalShaderChannels |= required;
        }
    }
}
