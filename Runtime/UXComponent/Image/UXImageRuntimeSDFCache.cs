using System.Collections.Generic;

namespace UnityEngine.UI
{
    internal static class UXImageRuntimeSDFCache
    {
        public readonly struct Entry
        {
            public readonly bool IsValid;
            public readonly Texture Texture;
            public readonly Vector4 UvRect;
            public readonly Vector2 TexelSize;
            public readonly float SpreadPixels;
            public readonly int Padding;

            public Entry(Texture texture, Vector4 uvRect, Vector2 texelSize, float spreadPixels, int padding)
            {
                IsValid = texture != null;
                Texture = texture;
                UvRect = uvRect;
                TexelSize = texelSize;
                SpreadPixels = spreadPixels;
                Padding = padding;
            }
        }

        private const int AtlasSize = 2048;
        private const int MaxSpriteSize = 512;
        private const string GeneratorShaderName = "Hidden/UI/UXImageSDFJFA";
        private const string GeneratorResourcePath = "UXImageSDFJFA";

        private static readonly Dictionary<Key, Entry> s_Entries = new Dictionary<Key, Entry>();
        private static readonly List<Page> s_Pages = new List<Page>();
        private static Material s_GeneratorMaterial;
        private static RenderTexture s_SeedA;
        private static RenderTexture s_SeedB;
        private static RenderTexture s_Result;

        public static bool ShouldUse(float shadowSoftness)
        {
            return shadowSoftness >= UXImageEffectSettings.RuntimeSdfSoftnessThreshold;
        }

        public static bool TryGetOrRequest(Sprite sprite, float spreadPixels, int padding, out Entry entry)
        {
            entry = default;
            if (sprite == null || sprite.texture == null)
                return false;

            if (sprite.packed && (sprite.packingMode == SpritePackingMode.Tight || sprite.packingRotation != SpritePackingRotation.None))
                return false;

            spreadPixels = Mathf.Max(spreadPixels, 1f);
            padding = Mathf.Clamp(padding, Mathf.CeilToInt(spreadPixels) + 2, 128);

            Key key = new Key(sprite, Mathf.CeilToInt(spreadPixels), padding);
            if (s_Entries.TryGetValue(key, out entry) && entry.IsValid)
                return true;

            Rect textureRect = sprite.textureRect;
            int width = Mathf.CeilToInt(textureRect.width);
            int height = Mathf.CeilToInt(textureRect.height);
            if (width <= 0 || height <= 0 || width > MaxSpriteSize || height > MaxSpriteSize)
                return false;

            int paddedWidth = width + padding * 2;
            int paddedHeight = height + padding * 2;
            if (paddedWidth > AtlasSize || paddedHeight > AtlasSize)
                return false;

            if (!EnsureGeneratorMaterial())
                return false;

            Page page = Allocate(paddedWidth, paddedHeight, out int x, out int y);
            if (page == null)
                return false;

            if (!Generate(sprite, textureRect, width, height, paddedWidth, paddedHeight, padding, spreadPixels))
                return false;

            BlitToAtlas(s_Result, page.Texture, new Rect(x, y, paddedWidth, paddedHeight));

            float invSize = 1f / page.Size;
            entry = new Entry(
                page.Texture,
                new Vector4(
                    x * invSize,
                    y * invSize,
                    (x + paddedWidth) * invSize,
                    (y + paddedHeight) * invSize),
                new Vector2(invSize, invSize),
                spreadPixels,
                padding);
            s_Entries[key] = entry;
            return true;
        }

        private static bool EnsureGeneratorMaterial()
        {
            if (s_GeneratorMaterial != null)
                return true;

            Shader shader = Shader.Find(GeneratorShaderName);
            if (shader == null)
                shader = Resources.Load<Shader>(GeneratorResourcePath);
            if (shader == null)
                return false;

            s_GeneratorMaterial = new Material(shader)
            {
                name = "UXImage Runtime SDF JFA Generator",
                hideFlags = HideFlags.HideAndDontSave
            };
            return true;
        }

        private static bool Generate(
            Sprite sprite,
            Rect textureRect,
            int width,
            int height,
            int paddedWidth,
            int paddedHeight,
            int padding,
            float spreadPixels)
        {
            if (!TryGetSeedFormat(out var seedFormat))
                return false;

            EnsureWorkTexture(ref s_SeedA, paddedWidth, paddedHeight, seedFormat, FilterMode.Point);
            EnsureWorkTexture(ref s_SeedB, paddedWidth, paddedHeight, seedFormat, FilterMode.Point);
            EnsureWorkTexture(ref s_Result, paddedWidth, paddedHeight, RenderTextureFormat.ARGB32, FilterMode.Bilinear);

            Vector4 sourceRect = new Vector4(
                textureRect.x / sprite.texture.width,
                textureRect.y / sprite.texture.height,
                textureRect.width / sprite.texture.width,
                textureRect.height / sprite.texture.height);
            Vector4 spriteRect = new Vector4(
                padding,
                padding,
                padding + width,
                padding + height);
            Vector4 textureSize = new Vector4(paddedWidth, paddedHeight, 1f / paddedWidth, 1f / paddedHeight);

            s_GeneratorMaterial.SetTexture("_SourceTex", sprite.texture);
            s_GeneratorMaterial.SetVector("_SourceRect", sourceRect);
            s_GeneratorMaterial.SetVector("_SpriteRect", spriteRect);
            s_GeneratorMaterial.SetVector("_TextureSize", textureSize);
            s_GeneratorMaterial.SetFloat("_Spread", Mathf.Max(spreadPixels, 0.0001f));
            Graphics.Blit(Texture2D.whiteTexture, s_SeedA, s_GeneratorMaterial, 0);

            RenderTexture read = s_SeedA;
            RenderTexture write = s_SeedB;
            int step = HighestPowerOfTwo(Mathf.Max(paddedWidth, paddedHeight));
            while (step >= 1)
            {
                s_GeneratorMaterial.SetTexture("_SeedTex", read);
                s_GeneratorMaterial.SetFloat("_Step", step);
                Graphics.Blit(Texture2D.whiteTexture, write, s_GeneratorMaterial, 1);
                Swap(ref read, ref write);
                step >>= 1;
            }

            s_GeneratorMaterial.SetTexture("_SeedTex", read);
            Graphics.Blit(Texture2D.whiteTexture, s_Result, s_GeneratorMaterial, 2);
            return true;
        }

        private static bool TryGetSeedFormat(out RenderTextureFormat format)
        {
            if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
            {
                format = RenderTextureFormat.ARGBHalf;
                return true;
            }

            if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat))
            {
                format = RenderTextureFormat.ARGBFloat;
                return true;
            }

            format = default;
            return false;
        }

        private static void BlitToAtlas(RenderTexture source, RenderTexture atlas, Rect targetRect)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = atlas;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, atlas.width, 0, atlas.height);
            Graphics.DrawTexture(targetRect, source);
            GL.PopMatrix();
            RenderTexture.active = previous;
        }

        private static void EnsureWorkTexture(ref RenderTexture texture, int width, int height, RenderTextureFormat format, FilterMode filterMode)
        {
            if (texture != null && texture.width == width && texture.height == height && texture.format == format)
                return;

            if (texture != null)
                DestroyRuntimeObject(texture);

            texture = new RenderTexture(width, height, 0, format)
            {
                name = "UXImage Runtime SDF Work",
                hideFlags = HideFlags.HideAndDontSave,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = filterMode,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
        }

        private static void DestroyRuntimeObject(Object target)
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

        private static int HighestPowerOfTwo(int value)
        {
            int result = 1;
            while (result < value)
                result <<= 1;
            return result >> 1;
        }

        private static void Swap(ref RenderTexture a, ref RenderTexture b)
        {
            RenderTexture temp = a;
            a = b;
            b = temp;
        }

        private static Page Allocate(int width, int height, out int x, out int y)
        {
            for (int i = 0; i < s_Pages.Count; i++)
            {
                if (s_Pages[i].TryAllocate(width, height, out x, out y))
                    return s_Pages[i];
            }

            Page page = new Page(AtlasSize);
            s_Pages.Add(page);
            if (page.TryAllocate(width, height, out x, out y))
                return page;

            return null;
        }

        private readonly struct Key
        {
            private readonly Sprite m_Sprite;
            private readonly int m_SpreadPixels;
            private readonly int m_Padding;

            public Key(Sprite sprite, int spreadPixels, int padding)
            {
                m_Sprite = sprite;
                m_SpreadPixels = spreadPixels;
                m_Padding = padding;
            }

            public override bool Equals(object obj)
            {
                return obj is Key other &&
                       ReferenceEquals(m_Sprite, other.m_Sprite) &&
                       m_SpreadPixels == other.m_SpreadPixels &&
                       m_Padding == other.m_Padding;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = m_Sprite != null ? m_Sprite.GetInstanceID() : 0;
                    hash = (hash * 397) ^ m_SpreadPixels;
                    hash = (hash * 397) ^ m_Padding;
                    return hash;
                }
            }
        }

        private sealed class Page
        {
            public readonly int Size;
            public readonly RenderTexture Texture;
            private int m_X;
            private int m_Y;
            private int m_RowHeight;

            public Page(int size)
            {
                Size = size;
                Texture = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32)
                {
                    name = "UXImage Runtime SDF Atlas",
                    hideFlags = HideFlags.HideAndDontSave,
                    useMipMap = false,
                    autoGenerateMips = false,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                Texture.Create();
                Clear(Texture);
            }

            public bool TryAllocate(int width, int height, out int x, out int y)
            {
                if (m_X + width > Size)
                {
                    m_X = 0;
                    m_Y += m_RowHeight;
                    m_RowHeight = 0;
                }

                if (m_Y + height > Size)
                {
                    x = 0;
                    y = 0;
                    return false;
                }

                x = m_X;
                y = m_Y;
                m_X += width;
                if (height > m_RowHeight)
                    m_RowHeight = height;

                return true;
            }

            private static void Clear(RenderTexture texture)
            {
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = texture;
                GL.Clear(false, true, Color.clear);
                RenderTexture.active = previous;
            }
        }
    }
}
