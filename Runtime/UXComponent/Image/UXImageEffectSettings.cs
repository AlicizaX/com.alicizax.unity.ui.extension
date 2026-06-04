using System.Collections.Generic;

namespace UnityEngine.UI
{
    internal static class UXImageEffectSettings
    {
        public const string ShaderName = "UI/UXImageAdaptiveEffect";
        private const string ShaderResourcePath = "UXImageAdaptiveEffect";

        public const float RuntimeSdfSoftnessThreshold = 8f;

        private static readonly Dictionary<MaterialKey, Material> s_Materials = new Dictionary<MaterialKey, Material>();

        public static Material EffectMaterial
        {
            get
            {
                return GetEffectMaterial(null, Color.black, new Color(0f, 0f, 0f, 0.5f));
            }
        }

        public static Material GetEffectMaterial(Texture sdfTexture, Color outlineColor, Color shadowColor)
        {
            MaterialKey key = new MaterialKey(sdfTexture, outlineColor, shadowColor);
            if (s_Materials.TryGetValue(key, out var material))
                return material;

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
                shader = Resources.Load<Shader>(ShaderResourcePath);
            if (shader == null)
                return null;

            Material next = new Material(shader)
            {
                name = sdfTexture == null ? "UXImage Adaptive Effect" : "UXImage Adaptive Effect SDF",
                hideFlags = HideFlags.HideAndDontSave
            };

            if (sdfTexture != null)
                next.SetTexture("_SDFTex", sdfTexture);

            next.SetColor("_OutlineColor", outlineColor);
            next.SetColor("_ShadowColor", shadowColor);
            s_Materials[key] = next;
            return next;
        }

        private readonly struct MaterialKey
        {
            private readonly Texture m_SdfTexture;
            private readonly Color32 m_OutlineColor;
            private readonly Color32 m_ShadowColor;

            public MaterialKey(Texture sdfTexture, Color outlineColor, Color shadowColor)
            {
                m_SdfTexture = sdfTexture;
                m_OutlineColor = outlineColor;
                m_ShadowColor = shadowColor;
            }

            public override bool Equals(object obj)
            {
                return obj is MaterialKey other &&
                       ReferenceEquals(m_SdfTexture, other.m_SdfTexture) &&
                       m_OutlineColor.Equals(other.m_OutlineColor) &&
                       m_ShadowColor.Equals(other.m_ShadowColor);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = m_SdfTexture != null ? m_SdfTexture.GetInstanceID() : 0;
                    hash = (hash * 397) ^ m_OutlineColor.GetHashCode();
                    hash = (hash * 397) ^ m_ShadowColor.GetHashCode();
                    return hash;
                }
            }
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
