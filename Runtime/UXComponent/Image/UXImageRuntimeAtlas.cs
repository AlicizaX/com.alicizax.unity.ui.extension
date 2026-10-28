using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

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
    /// - Entries are keyed by sprite instanceId and only reference the sprite through a weak reference. A destroyed
    ///   sprite (or an instanceId reused by another sprite) is treated as a miss and the stale entry is dropped.
    /// - Every page tracks its live entries. A page without live entries is released immediately and bumps
    ///   <see cref="Version"/> so cached Graphic state is invalidated.
    /// - <see cref="Sweep"/> drops dead entries and reclaims empty pages. It is hooked to
    ///   <see cref="SceneManager.sceneUnloaded"/> once and is also called by <see cref="ReleaseAll"/>.
    /// - <see cref="ReleaseAll"/> destroys every page and clears both static containers. It is invoked on play mode
    ///   start (SubsystemRegistration) and by UXImage.ReleaseRuntimeEffectCaches() for content shutdown.
    /// - Main-thread only (UGUI).
    /// </summary>
    internal static class UXImageRuntimeAtlas
    {
        public readonly struct Entry
        {
            public readonly Texture Texture;
            public readonly Vector4 SourceUvRect;
            public readonly Vector2 TexelSize;
            public readonly int Padding;

            public Entry(Texture texture, Vector4 sourceUvRect, Vector2 texelSize, int padding)
            {
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

        private static readonly Dictionary<int, SpriteEntry> s_Entries = new Dictionary<int, SpriteEntry>(64);
        private static readonly List<Page> s_Pages = new List<Page>(MaxPages);
        private static readonly List<int> s_DeadKeys = new List<int>(16);

        private static int s_Version;
        private static bool s_SceneUnloadHooked;

        /// <summary>
        /// Generation token incremented whenever a texture is destroyed (<see cref="ReleaseAll"/>, page reclaim).
        /// Graphics compare it against the generation their cached entry came from so they never return a destroyed
        /// texture.
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

            EnsureSceneUnloadHook();

            int instanceId = sprite.GetInstanceID();
            if (s_Entries.TryGetValue(instanceId, out SpriteEntry cached))
            {
                if (cached.Matches(sprite))
                {
                    entry = cached.Entry;
                    return true;
                }

                // Sprite 已销毁，或 instanceId 被另一个 Sprite 复用。
                RemoveEntry(cached);
            }

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

            page.AddLiveEntry();
            s_Entries.Add(instanceId, new SpriteEntry(instanceId, sprite, entry, page));
            return true;
        }

        /// <summary>
        /// Drops entries whose sprite is gone (or whose instanceId was reused) and reclaims pages without live
        /// entries. Safe to call at any time; a released page bumps <see cref="Version"/>.
        /// </summary>
        internal static void Sweep()
        {
            if (s_Entries.Count > 0)
            {
                s_DeadKeys.Clear();
                foreach (KeyValuePair<int, SpriteEntry> pair in s_Entries)
                {
                    if (!pair.Value.IsAlive())
                        s_DeadKeys.Add(pair.Key);
                }

                for (int i = 0; i < s_DeadKeys.Count; i++)
                {
                    if (s_Entries.TryGetValue(s_DeadKeys[i], out SpriteEntry dead))
                        RemoveEntry(dead);
                }

                s_DeadKeys.Clear();
            }

            ReclaimEmptyPages();
        }

        /// <summary>
        /// Destroys all pages (Release + Destroy) and clears the entry/page containers.
        /// Safe to call repeatedly; cached entries of live Graphics are invalidated through <see cref="Version"/>.
        /// </summary>
        public static void ReleaseAll()
        {
            Sweep();

            for (int i = 0; i < s_Pages.Count; i++)
                s_Pages[i].Release();

            s_Pages.Clear();
            s_Entries.Clear();
            s_DeadKeys.Clear();
            s_Version++;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayModeStart()
        {
            ReleaseAll();
        }

        private static void EnsureSceneUnloadHook()
        {
            if (s_SceneUnloadHooked)
                return;

            s_SceneUnloadHooked = true;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            Sweep();
        }

        private static void RemoveEntry(SpriteEntry entry)
        {
            s_Entries.Remove(entry.InstanceId);

            Page page = entry.Page;
            if (page == null)
                return;

            page.RemoveLiveEntry();
            ReleasePageIfEmpty(page);
        }

        private static void ReleasePageIfEmpty(Page page)
        {
            if (page.LiveEntries > 0 || !s_Pages.Remove(page))
                return;

            page.Release();
            s_Version++;
        }

        private static void ReclaimEmptyPages()
        {
            for (int i = s_Pages.Count - 1; i >= 0; i--)
            {
                Page page = s_Pages[i];
                if (page.LiveEntries > 0)
                    continue;

                s_Pages.RemoveAt(i);
                page.Release();
                s_Version++;
            }
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
            bool allocated = page.TryAllocate(width, height, out x, out y);

            // 调用方已把块尺寸限制在 AtlasSize 以内，这里失败说明不变量被破坏。
            Debug.Assert(allocated, "UXImageRuntimeAtlas: a block larger than a fresh atlas page was requested.");
            if (!allocated)
            {
                s_Pages.RemoveAt(s_Pages.Count - 1);
                page.Release();
                s_Version++;
                x = 0;
                y = 0;
                return null;
            }

            return page;
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

        private sealed class SpriteEntry
        {
            public readonly int InstanceId;
            public readonly WeakReference<Sprite> Sprite;
            public readonly Entry Entry;
            public readonly Page Page;

            public SpriteEntry(int instanceId, Sprite sprite, Entry entry, Page page)
            {
                InstanceId = instanceId;
                Sprite = new WeakReference<Sprite>(sprite);
                Entry = entry;
                Page = page;
            }

            public bool IsAlive()
            {
                return Sprite.TryGetTarget(out Sprite target) && target != null;
            }

            public bool Matches(Sprite sprite)
            {
                return Sprite.TryGetTarget(out Sprite target) && target != null && ReferenceEquals(target, sprite);
            }
        }

        /// <summary>
        /// One bump-allocated 2048x2048 ARGB32 atlas page. Shared with <see cref="UXImageRuntimeSDFCache"/> so both
        /// caches use the same allocation and release rules. The cache that creates a page owns it and releases it
        /// through <see cref="Release"/>. <see cref="LiveEntries"/> lets the owner reclaim a page as soon as the
        /// last sprite/SDF it hosts is gone.
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

            internal int LiveEntries { get; private set; }

            internal void AddLiveEntry()
            {
                LiveEntries++;
            }

            internal void RemoveLiveEntry()
            {
                if (LiveEntries > 0)
                    LiveEntries--;
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
