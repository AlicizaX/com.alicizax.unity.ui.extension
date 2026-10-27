using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace UnityEngine.UI
{
    /// <summary>
    /// Runtime signed-distance-field atlas used for the soft shadow of the UXImage adaptive effect.
    ///
    /// Lifetime/ownership:
    /// - Pages are lazily created by this static cache and are capped by <see cref="MaxPages"/>
    ///   (2048x2048 ARGB32, 16 MiB per page, 32 MiB maximum). When the cap is reached
    ///   <see cref="TryGetOrRequest"/> returns false and UXImage falls back to the non-SDF shadow sampling.
    /// - A bake uses temporary seed/result work textures that are destroyed as soon as the result has been copied
    ///   into the page, so no MiB-sized work texture stays resident between bakes. They are recreated per new
    ///   (sprite, spread bucket) bake only.
    /// - The generator material is created on the first bake and destroyed by <see cref="ReleaseAll"/>.
    /// - <see cref="ReleaseAll"/> destroys all pages, the generator material and clears both containers. It runs on
    ///   play mode start (SubsystemRegistration) and through UXImage.ReleaseRuntimeEffectCaches().
    /// - The atlas padding intentionally equals <see cref="UXImageRuntimeAtlas.MaxPadding"/>: the effect shader maps
    ///   the padded rect of the main atlas texture onto the padded rect of the SDF texture, so both rings must match.
    /// - Main-thread only (UGUI).
    /// </summary>
    internal static class UXImageRuntimeSDFCache
    {
        public readonly struct Entry
        {
            public readonly Texture Texture;
            public readonly Vector4 UvRect;

            public Entry(Texture texture, Vector4 uvRect)
            {
                Texture = texture;
                UvRect = uvRect;
            }
        }

        /// <summary>Smallest spread bucket. Matches <see cref="UXImageEffectSettings.RuntimeSdfSoftnessThreshold"/>.</summary>
        internal const int MinSpreadBucket = 8;
        internal const int MaxSpreadBucket = 64;

        private const int AtlasSize = 2048;
        private const int MaxSpriteSize = 512;
        private const int MaxPages = 2;
        private const int Padding = UXImageRuntimeAtlas.MaxPadding;
        private const string GeneratorShaderName = "Hidden/UI/UXImageSDFJFA";
        private const string GeneratorResourcePath = "UXImageSDFJFA";

        private static readonly Dictionary<Key, Entry> s_Entries = new Dictionary<Key, Entry>(16);
        private static readonly List<UXImageRuntimeAtlas.Page> s_Pages = new List<UXImageRuntimeAtlas.Page>(MaxPages);
        private static Material s_GeneratorMaterial;
        private static int s_Version;

        /// <summary>
        /// Generation token incremented by <see cref="ReleaseAll"/>. UXImage uses it to invalidate materials that
        /// still reference an SDF texture of an older generation.
        /// </summary>
        public static int Version
        {
            get { return s_Version; }
        }

        public static bool ShouldUse(float shadowSoftness)
        {
            return shadowSoftness >= UXImageEffectSettings.RuntimeSdfSoftnessThreshold;
        }

        /// <summary>
        /// Quantizes the continuous shadow softness into a fixed bucket set. The bucket is part of the cache key and
        /// is also the spread written into the SDF, so an animated softness cannot rebake the same sprite forever.
        /// Values above <see cref="MaxSpreadBucket"/> are clamped to that bucket.
        /// </summary>
        internal static int QuantizeSpread(float spreadPixels)
        {
            if (spreadPixels <= MinSpreadBucket)
                return MinSpreadBucket;
            if (spreadPixels <= 16f)
                return 16;
            if (spreadPixels <= 32f)
                return 32;
            return MaxSpreadBucket;
        }

        public static bool TryGetOrRequest(Sprite sprite, float spreadPixels, out Entry entry)
        {
            entry = default;
            if (sprite == null || sprite.texture == null)
                return false;

            if (sprite.packed && (sprite.packingMode == SpritePackingMode.Tight || sprite.packingRotation != SpritePackingRotation.None))
                return false;

            int spreadBucket = QuantizeSpread(spreadPixels);
            Key key = new Key(sprite, spreadBucket);
            if (s_Entries.TryGetValue(key, out entry))
                return true;

            Rect textureRect = sprite.textureRect;
            int width = Mathf.CeilToInt(textureRect.width);
            int height = Mathf.CeilToInt(textureRect.height);
            if (width <= 0 || height <= 0 || width > MaxSpriteSize || height > MaxSpriteSize)
                return false;

            int paddedWidth = width + Padding * 2;
            int paddedHeight = height + Padding * 2;
            if (paddedWidth > AtlasSize || paddedHeight > AtlasSize)
                return false;

            if (!EnsureGeneratorMaterial() || !TryGetSeedFormat(out RenderTextureFormat seedFormat))
                return false;

            UXImageRuntimeAtlas.Page page = Allocate(paddedWidth, paddedHeight, out int x, out int y);
            if (page == null)
                return false;

            Bake(sprite, textureRect, width, height, spreadBucket, page.Texture, new Rect(x, y, paddedWidth, paddedHeight), seedFormat);

            float invSize = 1f / page.Size;
            entry = new Entry(
                page.Texture,
                new Vector4(
                    x * invSize,
                    y * invSize,
                    (x + paddedWidth) * invSize,
                    (y + paddedHeight) * invSize));
            s_Entries[key] = entry;
            return true;
        }

        /// <summary>
        /// Destroys all pages, the generator material and clears the entry/page containers.
        /// Safe to call repeatedly; live Graphics invalidate their material through <see cref="Version"/>.
        /// </summary>
        public static void ReleaseAll()
        {
            for (int i = 0; i < s_Pages.Count; i++)
                s_Pages[i].Release();

            s_Pages.Clear();
            s_Entries.Clear();
            UXImageEffectSettings.DestroyRuntimeObject(s_GeneratorMaterial);
            s_GeneratorMaterial = null;
            s_Version++;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayModeStart()
        {
            ReleaseAll();
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

        private static void Bake(
            Sprite sprite,
            Rect textureRect,
            int width,
            int height,
            int spreadBucket,
            RenderTexture atlas,
            Rect targetRect,
            RenderTextureFormat seedFormat)
        {
            int paddedWidth = (int)targetRect.width;
            int paddedHeight = (int)targetRect.height;

            RenderTexture seedA = null;
            RenderTexture seedB = null;
            RenderTexture resolved = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                seedA = CreateWorkTexture(paddedWidth, paddedHeight, seedFormat, FilterMode.Point);
                seedB = CreateWorkTexture(paddedWidth, paddedHeight, seedFormat, FilterMode.Point);
                resolved = CreateWorkTexture(paddedWidth, paddedHeight, RenderTextureFormat.ARGB32, FilterMode.Bilinear);

                Vector4 sourceRect = new Vector4(
                    textureRect.x / sprite.texture.width,
                    textureRect.y / sprite.texture.height,
                    textureRect.width / sprite.texture.width,
                    textureRect.height / sprite.texture.height);
                Vector4 spriteRect = new Vector4(
                    Padding,
                    Padding,
                    Padding + width,
                    Padding + height);
                Vector4 textureSize = new Vector4(paddedWidth, paddedHeight, 1f / paddedWidth, 1f / paddedHeight);

                s_GeneratorMaterial.SetTexture("_SourceTex", sprite.texture);
                s_GeneratorMaterial.SetVector("_SourceRect", sourceRect);
                s_GeneratorMaterial.SetVector("_SpriteRect", spriteRect);
                s_GeneratorMaterial.SetVector("_TextureSize", textureSize);
                s_GeneratorMaterial.SetFloat("_Spread", spreadBucket);
                Graphics.Blit(Texture2D.whiteTexture, seedA, s_GeneratorMaterial, 0);

                RenderTexture read = seedA;
                RenderTexture write = seedB;
                for (int step = HighestPowerOfTwo(Mathf.Max(paddedWidth, paddedHeight)); step >= 1; step >>= 1)
                {
                    s_GeneratorMaterial.SetTexture("_SeedTex", read);
                    s_GeneratorMaterial.SetFloat("_Step", step);
                    Graphics.Blit(Texture2D.whiteTexture, write, s_GeneratorMaterial, 1);
                    RenderTexture swap = read;
                    read = write;
                    write = swap;
                }

                s_GeneratorMaterial.SetTexture("_SeedTex", read);
                Graphics.Blit(Texture2D.whiteTexture, resolved, s_GeneratorMaterial, 2);

                BlitToAtlas(resolved, atlas, targetRect);
            }
            finally
            {
                RenderTexture.active = previous;

                // The generator material outlives the bake; drop its references to the sprite and the temporary
                // seed texture so neither is retained between bakes.
                Material material = s_GeneratorMaterial;
                if (material != null)
                {
                    material.SetTexture("_SeedTex", null);
                    material.SetTexture("_SourceTex", null);
                }

                UXImageEffectSettings.DestroyRenderTexture(seedA);
                UXImageEffectSettings.DestroyRenderTexture(seedB);
                UXImageEffectSettings.DestroyRenderTexture(resolved);
            }
        }

        private static RenderTexture CreateWorkTexture(int width, int height, RenderTextureFormat format, FilterMode filterMode)
        {
            RenderTexture texture = new RenderTexture(width, height, 0, format)
            {
                name = "UXImage Runtime SDF Work",
                hideFlags = HideFlags.HideAndDontSave,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = filterMode,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
            return texture;
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
            try
            {
                RenderTexture.active = atlas;
                GL.PushMatrix();
                try
                {
                    GL.LoadPixelMatrix(0, atlas.width, 0, atlas.height);
                    Graphics.DrawTexture(targetRect, source);
                }
                finally
                {
                    GL.PopMatrix();
                }
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static int HighestPowerOfTwo(int value)
        {
            // Half of the next power of two, which is the usual JFA starting step.
            int result = 1;
            while (result < value)
                result <<= 1;
            return result >> 1;
        }

        private static UXImageRuntimeAtlas.Page Allocate(int width, int height, out int x, out int y)
        {
            for (int i = 0; i < s_Pages.Count; i++)
            {
                if (s_Pages[i].TryAllocate(width, height, out x, out y))
                    return s_Pages[i];
            }

            if (s_Pages.Count >= MaxPages)
            {
                x = 0;
                y = 0;
                return null;
            }

            UXImageRuntimeAtlas.Page page = new UXImageRuntimeAtlas.Page(AtlasSize, "UXImage Runtime SDF Atlas");
            s_Pages.Add(page);
            if (page.TryAllocate(width, height, out x, out y))
                return page;

            // Unreachable for callers that pre-check the block size against AtlasSize; never leak the page if a
            // future caller forgets that check.
            s_Pages.RemoveAt(s_Pages.Count - 1);
            page.Release();
            x = 0;
            y = 0;
            return null;
        }

        private readonly struct Key : System.IEquatable<Key>
        {
            private readonly Sprite m_Sprite;
            private readonly int m_SpreadBucket;

            public Key(Sprite sprite, int spreadBucket)
            {
                m_Sprite = sprite;
                m_SpreadBucket = spreadBucket;
            }

            public bool Equals(Key other)
            {
                return ReferenceEquals(m_Sprite, other.m_Sprite) && m_SpreadBucket == other.m_SpreadBucket;
            }

            public override bool Equals(object obj)
            {
                return obj is Key other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (RuntimeHelpers.GetHashCode(m_Sprite) * 397) ^ m_SpreadBucket;
                }
            }
        }
    }
}
