using System.Collections.Generic;

namespace UnityEngine.UI
{
    /// <summary>
    /// Runtime sprite atlas used by the UXImage adaptive effect.
    ///
    /// Lifetime/ownership:
    /// - Pages are lazily created by this static cache and are capped by <see cref="MaxPages"/>
    ///   (<see cref="AtlasSize"/>^2 ARGB32 = 16 MiB per page, 64 MiB maximum). Once the cap is reached
    ///   <see cref="TryGet"/> returns false and the caller must fall back to the sprite texture; nothing is
    ///   over-allocated silently.
    /// - A sprite is allocated exactly once, always with <see cref="MaxPadding"/> texels of transparent ring, so
    ///   requests for any padding up to MaxPadding (or SDF padding changes) reuse the same entry with correct UVs.
    /// - <see cref="ReleaseAll"/> destroys every page and clears both static containers. It is invoked on play mode
    ///   start (SubsystemRegistration) and by UXImage.ReleaseRuntimeEffectCaches() for content shutdown.
    /// - The cache strongly references the sprites it drew until ReleaseAll; it is main-thread only (UGUI).
    /// </summary>
    internal static class UXImageRuntimeAtlas
    {
        public readonly struct Entry
        {
            public readonly bool IsValid;
            public readonly Texture Texture;
            public readonly Vector4 SourceUvRect;
            public readonly Vector2 TexelSize;
            public readonly int Padding;

            public Entry(Texture texture, Vector4 sourceUvRect, Vector2 texelSize, int padding)
            {
                IsValid = texture != null;
                Texture = texture;
                SourceUvRect = sourceUvRect;
                TexelSize = texelSize;
                Padding = padding;
            }
        }

        /// <summary>
        /// Padding reserved around every sprite. Entries are allocated once with this padding, therefore a request
        /// for a smaller padding still uses a valid (larger) ring. UXImage clamps its effect padding to this value
        /// so meshes never sample outside the allocated ring. This is also the largest effect distance/softness the
        /// runtime atlas can supply, and it limits a single sprite to AtlasSize - 2 * MaxPadding = 1792 pixels.
        /// </summary>
        internal const int MaxPadding = 128;

        private const int AtlasSize = 2048;
        private const int MaxPages = 4;

        private static readonly Dictionary<Sprite, Entry> s_Entries = new Dictionary<Sprite, Entry>(64);
        private static readonly List<Page> s_Pages = new List<Page>(MaxPages);

        private static int s_Version;

        /// <summary>
        /// Generation token incremented by <see cref="ReleaseAll"/>. Graphics compare it against the generation
        /// their cached entry came from so they never return a destroyed texture.
        /// </summary>
        public static int Version
        {
            get { return s_Version; }
        }

        public static bool TryGet(Sprite sprite, out Entry entry)
        {
            entry = default;
            if (sprite == null || sprite.texture == null)
                return false;

            if (sprite.packed && (sprite.packingMode == SpritePackingMode.Tight || sprite.packingRotation != SpritePackingRotation.None))
                return false;

            if (s_Entries.TryGetValue(sprite, out entry))
                return true;

            Rect textureRect = sprite.textureRect;
            int width = Mathf.CeilToInt(textureRect.width);
            int height = Mathf.CeilToInt(textureRect.height);
            int paddedWidth = width + MaxPadding * 2;
            int paddedHeight = height + MaxPadding * 2;

            if (width <= 0 || height <= 0 || paddedWidth > AtlasSize || paddedHeight > AtlasSize)
                return false;

            Page page = Allocate(paddedWidth, paddedHeight, out int x, out int y);
            if (page == null)
                return false;

            DrawSprite(sprite, textureRect, page.Texture, new Rect(x + MaxPadding, y + MaxPadding, width, height));

            float invSize = 1f / page.Size;
            entry = new Entry(
                page.Texture,
                new Vector4(
                    (x + MaxPadding) * invSize,
                    (y + MaxPadding) * invSize,
                    (x + MaxPadding + width) * invSize,
                    (y + MaxPadding + height) * invSize),
                new Vector2(invSize, invSize),
                MaxPadding);
            s_Entries[sprite] = entry;
            return true;
        }

        /// <summary>
        /// Destroys all pages (Release + Destroy) and clears the entry/page containers.
        /// Safe to call repeatedly; cached entries of live Graphics are invalidated through <see cref="Version"/>.
        /// </summary>
        public static void ReleaseAll()
        {
            for (int i = 0; i < s_Pages.Count; i++)
                s_Pages[i].Release();

            s_Pages.Clear();
            s_Entries.Clear();
            s_Version++;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayModeStart()
        {
            ReleaseAll();
        }

        private static Page Allocate(int width, int height, out int x, out int y)
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

            Page page = new Page(AtlasSize, "UXImage Runtime Atlas");
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

        private static void DrawSprite(Sprite sprite, Rect textureRect, RenderTexture atlas, Rect targetRect)
        {
            Texture source = sprite.texture;
            Rect sourceRect = new Rect(
                textureRect.x / source.width,
                textureRect.y / source.height,
                textureRect.width / source.width,
                textureRect.height / source.height);

            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = atlas;
                GL.PushMatrix();
                try
                {
                    GL.LoadPixelMatrix(0, atlas.width, 0, atlas.height);
                    Rect flippedSourceRect = new Rect(sourceRect.x, sourceRect.y + sourceRect.height, sourceRect.width, -sourceRect.height);
                    Graphics.DrawTexture(targetRect, source, flippedSourceRect, 0, 0, 0, 0);
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

        /// <summary>
        /// One bump-allocated 2048x2048 ARGB32 atlas page. Shared with <see cref="UXImageRuntimeSDFCache"/> so both
        /// caches use the same allocation and release rules. The cache that creates a page owns it and releases it
        /// through <see cref="Release"/>.
        /// </summary>
        internal sealed class Page
        {
            public readonly int Size;
            public readonly RenderTexture Texture;
            private int m_X;
            private int m_Y;
            private int m_RowHeight;

            internal Page(int size, string textureName)
            {
                Size = size;
                Texture = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32)
                {
                    name = textureName,
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

            public void Release()
            {
                UXImageEffectSettings.DestroyRenderTexture(Texture);
            }

            private static void Clear(RenderTexture texture)
            {
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = texture;
                    GL.Clear(false, true, Color.clear);
                }
                finally
                {
                    RenderTexture.active = previous;
                }
            }
        }
    }
}
