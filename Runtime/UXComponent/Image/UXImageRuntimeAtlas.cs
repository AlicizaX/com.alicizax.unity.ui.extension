using System.Collections.Generic;

namespace UnityEngine.UI
{
    internal static class UXImageRuntimeAtlas
    {
        public readonly struct Entry
        {
            public readonly bool IsValid;
            public readonly Texture Texture;
            public readonly Vector4 SourceUvRect;
            public readonly Vector2 TexelSize;
            public readonly int Padding;
            public readonly int Width;
            public readonly int Height;

            public Entry(Texture texture, Vector4 sourceUvRect, Vector2 texelSize, int padding, int width, int height)
            {
                IsValid = texture != null;
                Texture = texture;
                SourceUvRect = sourceUvRect;
                TexelSize = texelSize;
                Padding = padding;
                Width = width;
                Height = height;
            }
        }

        private const int AtlasSize = 2048;
        private const int MaxPadding = 128;
        private static readonly Dictionary<Sprite, Entry> s_Entries = new Dictionary<Sprite, Entry>();
        private static readonly List<Page> s_Pages = new List<Page>();

        public static bool TryGet(Sprite sprite, int padding, out Entry entry)
        {
            entry = default;
            if (sprite == null || sprite.texture == null)
                return false;

            if (sprite.packed && (sprite.packingMode == SpritePackingMode.Tight || sprite.packingRotation != SpritePackingRotation.None))
                return false;

            padding = Mathf.Clamp(padding, 1, MaxPadding);
            if (s_Entries.TryGetValue(sprite, out entry) && entry.Padding >= padding)
                return true;

            Rect textureRect = sprite.textureRect;
            int width = Mathf.CeilToInt(textureRect.width);
            int height = Mathf.CeilToInt(textureRect.height);
            int paddedWidth = width + padding * 2;
            int paddedHeight = height + padding * 2;

            if (width <= 0 || height <= 0 || paddedWidth > AtlasSize || paddedHeight > AtlasSize)
                return false;

            Page page = Allocate(paddedWidth, paddedHeight, out int x, out int y);
            if (page == null)
                return false;

            DrawSprite(sprite, textureRect, page.Texture, new Rect(x + padding, y + padding, width, height));

            float invSize = 1f / page.Size;
            entry = new Entry(
                page.Texture,
                new Vector4(
                    (x + padding) * invSize,
                    (y + padding) * invSize,
                    (x + padding + width) * invSize,
                    (y + padding + height) * invSize),
                new Vector2(invSize, invSize),
                padding,
                width,
                height);
            s_Entries[sprite] = entry;
            return true;
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

        private static void DrawSprite(Sprite sprite, Rect textureRect, RenderTexture atlas, Rect targetRect)
        {
            Texture source = sprite.texture;
            Rect sourceRect = new Rect(
                textureRect.x / source.width,
                textureRect.y / source.height,
                textureRect.width / source.width,
                textureRect.height / source.height);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = atlas;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, atlas.width, 0, atlas.height);
            Rect flippedSourceRect = new Rect(sourceRect.x, sourceRect.y + sourceRect.height, sourceRect.width, -sourceRect.height);
            Graphics.DrawTexture(targetRect, source, flippedSourceRect, 0, 0, 0, 0);
            GL.PopMatrix();
            RenderTexture.active = previous;
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
                    name = "UXImage Runtime Atlas",
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
