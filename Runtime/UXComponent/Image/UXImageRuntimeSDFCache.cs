using System;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UnityEngine.UI
{
    /// <summary>
    /// Runtime signed-distance-field atlas used for the soft shadow of the UXImage adaptive effect.
    ///
    /// Lifetime/ownership:
    /// - Pages are lazily created by this static cache and are capped by <see cref="MaxPages"/>
    ///   (2048x2048 ARGB32, 16 MiB per page, 32 MiB maximum). When the cap is reached
    ///   <see cref="TryGetOrRequest"/> returns false and UXImage falls back to the non-SDF shadow sampling.
    /// - <see cref="TryGetOrRequest"/> never bakes during a mesh rebuild: it only looks up the atlas and, on a miss,
    ///   enqueues a bake request. The actual JFA bake is driven by one hidden <see cref="BakeDriver"/> with a
    ///   per-frame budget (<see cref="MaxBakesPerFrame"/>), so a canvas rebuild never blocks on GPU work.
    /// - Every pending request keeps a weak waiter list of the Graphics that asked for it; after a successful bake
    ///   those Graphics are marked vertices-dirty so their next rebuild can use the SDF path.
    /// - A request is negatively cached when the environment can never satisfy it (no graphics device, missing
    ///   generator shader/format). The negative cache is weak per sprite and is cleared by <see cref="ReleaseAll"/>.
    /// - A bake uses temporary seed/result work textures that are destroyed as soon as the result has been copied
    ///   into the page, so no MiB-sized work texture stays resident between bakes.
    /// - Entries are keyed by sprite instanceId + spread bucket and reference the sprite only weakly; dead entries
    ///   are dropped by <see cref="Sweep"/> (hooked to sceneUnloaded and called by <see cref="ReleaseAll"/>), and
    ///   pages without live entries are released immediately (which bumps <see cref="Version"/>).
    /// - <see cref="ReleaseAll"/> destroys all pages, the generator material, clears the request queue/negative
    ///   cache/waiter lists and runs on play mode start (SubsystemRegistration).
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
        private const int MaxBakesPerFrame = 1;

        /// <summary>每个待烘焙请求最多记录的等待者数量，超出的 Graphic 只会在自身下一次重建时重新请求。</summary>
        private const int MaxWaitersPerRequest = 32;

        private const int Padding = UXImageRuntimeAtlas.MaxPadding;
        private const string GeneratorShaderName = "Hidden/UI/UXImageSDFJFA";
        private const string GeneratorResourcePath = "UXImageSDFJFA";

        private static readonly Dictionary<BakeKey, CachedEntry> s_Entries = new Dictionary<BakeKey, CachedEntry>(16);
        private static readonly List<UXImageRuntimeAtlas.Page> s_Pages = new List<UXImageRuntimeAtlas.Page>(MaxPages);
        private static readonly Dictionary<BakeKey, PendingBake> s_Pending = new Dictionary<BakeKey, PendingBake>(16);
        private static readonly Queue<PendingBake> s_PendingQueue = new Queue<PendingBake>(16);
        private static readonly Dictionary<BakeKey, WeakReference<Sprite>> s_Failed = new Dictionary<BakeKey, WeakReference<Sprite>>(8);
        private static readonly List<BakeKey> s_DeadKeys = new List<BakeKey>(16);

        private static Material s_GeneratorMaterial;
        private static BakeDriver s_Driver;
        private static int s_Version;
        private static bool s_SceneUnloadHooked;

        /// <summary>
        /// Generation token incremented whenever a page/SDF texture is destroyed. UXImage uses it to invalidate
        /// materials that still reference an SDF texture of an older generation.
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

        /// <summary>
        /// 查询 SDF 图集。命中时返回 true；缺失时只登记一个异步烘焙请求并返回 false（调用方先走非 SDF 阴影），
        /// 绝不在网格重建过程中同步烘焙。
        /// </summary>
        public static bool TryGetOrRequest(Sprite sprite, float spreadPixels, out Entry entry)
        {
            return TryGetOrRequest(sprite, spreadPixels, null, out entry);
        }

        /// <summary>
        /// 同 <see cref="TryGetOrRequest(Sprite, float, out Entry)"/>，额外登记 <paramref name="requester"/> 作为等待者：
        /// 烘焙成功后会对它调用 SetVerticesDirty()，让它在下一次重建时切换到 SDF 阴影。
        /// </summary>
        public static bool TryGetOrRequest(Sprite sprite, float spreadPixels, Graphic requester, out Entry entry)
        {
            entry = default;
            if (!TryResolveKey(sprite, spreadPixels, out BakeKey key, out int spreadBucket))
                return false;

            EnsureSceneUnloadHook();

            if (s_Entries.TryGetValue(key, out CachedEntry cached))
            {
                if (cached.Matches(sprite))
                {
                    entry = cached.Entry;
                    return true;
                }

                RemoveEntry(key, cached);
            }

            if (IsMarkedFailed(key, sprite))
                return false;

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                // -nographics / 无图形设备：负缓存，避免 EditMode 下每个 rebuild 反复重试。
                MarkFailed(key, sprite);
                return false;
            }

            if (!CanBake(sprite))
                return false;

            RequestBake(sprite, key, spreadBucket, requester);
            return false;
        }

        /// <summary>
        /// 处理最多 <paramref name="budget"/> 个待烘焙请求，返回本次是否处理了至少一个请求。
        /// 测试在 EditMode 下可以直接驱动它而无需进入 play mode；运行时由隐藏的 <see cref="BakeDriver"/> 调用。
        /// </summary>
        internal static bool ProcessPendingBakesForTests(int budget)
        {
            if (budget <= 0)
                budget = MaxBakesPerFrame;

            int processed = 0;
            while (processed < budget && s_PendingQueue.Count > 0)
            {
                PendingBake request = s_PendingQueue.Dequeue();
                s_Pending.Remove(request.Key);
                processed++;
                TryBake(request);
            }

            return processed > 0;
        }

        /// <summary>
        /// Drops entries whose sprite is gone (or whose instanceId was reused), purges stale negative-cache records
        /// and reclaims pages without live entries.
        /// </summary>
        internal static void Sweep()
        {
            if (s_Entries.Count > 0)
            {
                s_DeadKeys.Clear();
                foreach (KeyValuePair<BakeKey, CachedEntry> pair in s_Entries)
                {
                    if (!pair.Value.IsAlive())
                        s_DeadKeys.Add(pair.Key);
                }

                for (int i = 0; i < s_DeadKeys.Count; i++)
                {
                    if (s_Entries.TryGetValue(s_DeadKeys[i], out CachedEntry dead))
                        RemoveEntry(s_DeadKeys[i], dead);
                }

                s_DeadKeys.Clear();
            }

            if (s_Failed.Count > 0)
            {
                s_DeadKeys.Clear();
                foreach (KeyValuePair<BakeKey, WeakReference<Sprite>> pair in s_Failed)
                {
                    if (!IsAlive(pair.Value))
                        s_DeadKeys.Add(pair.Key);
                }

                for (int i = 0; i < s_DeadKeys.Count; i++)
                    s_Failed.Remove(s_DeadKeys[i]);

                s_DeadKeys.Clear();
            }

            ReclaimEmptyPages();
        }

        /// <summary>
        /// Destroys all pages, the generator material, clears the entry/page containers plus the request queue,
        /// negative cache and waiter lists. Safe to call repeatedly; live Graphics invalidate their material through
        /// <see cref="Version"/>. 队列里尚未完成的请求会静默降级为直接返回，不会在 Release 之后再烘焙。
        /// </summary>
        public static void ReleaseAll()
        {
            Sweep();

            for (int i = 0; i < s_Pages.Count; i++)
                s_Pages[i].Release();

            s_Pages.Clear();
            s_Entries.Clear();
            s_Failed.Clear();

            foreach (PendingBake request in s_PendingQueue)
                request.Waiters.Clear();

            s_PendingQueue.Clear();
            s_Pending.Clear();
            s_DeadKeys.Clear();

            UXImageEffectSettings.DestroyRuntimeObject(s_GeneratorMaterial);
            s_GeneratorMaterial = null;
            DestroyDriver();
            s_Version++;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayModeStart()
        {
            ReleaseAll();
        }

        private static bool TryResolveKey(Sprite sprite, float spreadPixels, out BakeKey key, out int spreadBucket)
        {
            key = default;
            spreadBucket = 0;

            if (sprite == null || sprite.texture == null)
                return false;

            if (sprite.packed && (sprite.packingMode == SpritePackingMode.Tight || sprite.packingRotation != SpritePackingRotation.None))
                return false;

            spreadBucket = QuantizeSpread(spreadPixels);
            key = new BakeKey(sprite.GetInstanceID(), spreadBucket);
            return true;
        }

        private static bool CanBake(Sprite sprite)
        {
            Rect textureRect = sprite.textureRect;
            int width = Mathf.CeilToInt(textureRect.width);
            int height = Mathf.CeilToInt(textureRect.height);
            if (width <= 0 || height <= 0 || width > MaxSpriteSize || height > MaxSpriteSize)
                return false;

            int paddedWidth = width + Padding * 2;
            int paddedHeight = height + Padding * 2;
            return paddedWidth <= AtlasSize && paddedHeight <= AtlasSize;
        }

        private static bool IsMarkedFailed(BakeKey key, Sprite sprite)
        {
            if (!s_Failed.TryGetValue(key, out WeakReference<Sprite> failedFor))
                return false;

            if (IsAlive(failedFor, sprite))
                return true;

            // instanceId 被复用或原 Sprite 已销毁：负缓存记录不再可信。
            s_Failed.Remove(key);
            return false;
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

        private static void MarkFailed(BakeKey key, Sprite sprite)
        {
            if (s_Failed.TryGetValue(key, out WeakReference<Sprite> failedFor) && failedFor != null)
                failedFor.SetTarget(sprite);
            else
                s_Failed[key] = new WeakReference<Sprite>(sprite);
        }

        private static bool IsAlive(WeakReference<Sprite> reference, Sprite sprite)
        {
            return reference != null &&
                   reference.TryGetTarget(out Sprite target) &&
                   target != null &&
                   ReferenceEquals(target, sprite);
        }

        private static bool IsAlive(WeakReference<Sprite> reference)
        {
            return reference != null &&
                   reference.TryGetTarget(out Sprite target) &&
                   target != null;
        }

        private static void RequestBake(Sprite sprite, BakeKey key, int spreadBucket, Graphic requester)
        {
            if (!s_Pending.TryGetValue(key, out PendingBake request))
            {
                request = new PendingBake(key, spreadBucket);
                request.Sprite.SetTarget(sprite);
                s_Pending.Add(key, request);
                s_PendingQueue.Enqueue(request);
            }

            AddWaiter(request, requester);
            EnsureDriver();
        }

        private static void AddWaiter(PendingBake request, Graphic requester)
        {
            if (requester == null)
                return;

            List<WeakReference<Graphic>> waiters = request.Waiters;
            int deadSlot = -1;
            for (int i = 0; i < waiters.Count; i++)
            {
                if (!waiters[i].TryGetTarget(out Graphic target) || target == null)
                {
                    if (deadSlot < 0)
                        deadSlot = i;
                    continue;
                }

                if (ReferenceEquals(target, requester))
                    return;
            }

            if (deadSlot >= 0)
            {
                waiters[deadSlot] = new WeakReference<Graphic>(requester);
                return;
            }

            if (waiters.Count < MaxWaitersPerRequest)
                waiters.Add(new WeakReference<Graphic>(requester));
        }

        private static void NotifyWaiters(PendingBake request)
        {
            List<WeakReference<Graphic>> waiters = request.Waiters;
            for (int i = 0; i < waiters.Count; i++)
            {
                if (waiters[i].TryGetTarget(out Graphic graphic) && graphic != null)
                    graphic.SetVerticesDirty();
            }

            waiters.Clear();
        }

        private static bool TryBake(PendingBake request)
        {
            if (!request.Sprite.TryGetTarget(out Sprite sprite) || sprite == null || sprite.texture == null)
                return false;

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                MarkFailed(request.Key, sprite);
                return false;
            }

            if (!CanBake(sprite))
                return false;

            if (!EnsureGeneratorMaterial() || !TryGetSeedFormat(out RenderTextureFormat seedFormat))
            {
                // 环境不支持，重试也不会成功，直接负缓存。
                MarkFailed(request.Key, sprite);
                return false;
            }

            Rect textureRect = sprite.textureRect;
            int width = Mathf.CeilToInt(textureRect.width);
            int height = Mathf.CeilToInt(textureRect.height);
            int paddedWidth = width + Padding * 2;
            int paddedHeight = height + Padding * 2;

            UXImageRuntimeAtlas.Page page = Allocate(paddedWidth, paddedHeight, out int x, out int y);
            if (page == null)
                return false;

            // 同一 key 已存在（例如上一次请求排队期间已经由别的路径烘焙完成）时先归还旧页，避免 live 计数泄漏。
            if (s_Entries.TryGetValue(request.Key, out CachedEntry existing))
                RemoveEntry(request.Key, existing);

            Bake(sprite, textureRect, width, height, request.SpreadBucket, page.Texture, new Rect(x, y, paddedWidth, paddedHeight), seedFormat);

            float invSize = 1f / page.Size;
            Entry entry = new Entry(
                page.Texture,
                new Vector4(
                    x * invSize,
                    y * invSize,
                    (x + paddedWidth) * invSize,
                    (y + paddedHeight) * invSize));

            page.AddLiveEntry();
            s_Entries[request.Key] = new CachedEntry(sprite, entry, page);

            NotifyWaiters(request);
            return true;
        }

        private static void EnsureDriver()
        {
            if (s_Driver != null)
                return;

            // EditMode 下不创建驱动：由测试或外部显式调用 ProcessPendingBakesForTests 驱动，避免编辑器里留下隐藏对象。
            if (!Application.isPlaying)
                return;

            GameObject driverObject = new GameObject("UXImageRuntimeSDFBakeDriver");
            driverObject.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(driverObject);
            s_Driver = driverObject.AddComponent<BakeDriver>();
        }

        private static void DestroyDriver()
        {
            if (s_Driver == null)
                return;

            UXImageEffectSettings.DestroyRuntimeObject(s_Driver.gameObject);
            s_Driver = null;
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
            bool allocated = page.TryAllocate(width, height, out x, out y);

            // 调用方已把块尺寸限制在 AtlasSize 以内，这里失败说明不变量被破坏。
            Debug.Assert(allocated, "UXImageRuntimeSDFCache: a block larger than a fresh atlas page was requested.");
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

        private static void RemoveEntry(BakeKey key, CachedEntry cached)
        {
            s_Entries.Remove(key);

            UXImageRuntimeAtlas.Page page = cached.Page;
            if (page == null)
                return;

            page.RemoveLiveEntry();
            ReleasePageIfEmpty(page);
        }

        private static void ReleasePageIfEmpty(UXImageRuntimeAtlas.Page page)
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
                UXImageRuntimeAtlas.Page page = s_Pages[i];
                if (page.LiveEntries > 0)
                    continue;

                s_Pages.RemoveAt(i);
                page.Release();
                s_Version++;
            }
        }

        /// <summary>
        /// 隐藏的逐帧驱动。只在 play mode 下创建一次，播放结束或 ReleaseAll 时销毁。
        /// </summary>
        private sealed class BakeDriver : MonoBehaviour
        {
            private void Update()
            {
                ProcessPendingBakesForTests(MaxBakesPerFrame);
            }
        }

        private sealed class CachedEntry
        {
            private readonly WeakReference<Sprite> m_Sprite;
            public readonly Entry Entry;
            public readonly UXImageRuntimeAtlas.Page Page;

            public CachedEntry(Sprite sprite, Entry entry, UXImageRuntimeAtlas.Page page)
            {
                m_Sprite = new WeakReference<Sprite>(sprite);
                Entry = entry;
                Page = page;
            }

            public bool IsAlive()
            {
                return m_Sprite.TryGetTarget(out Sprite target) && target != null;
            }

            public bool Matches(Sprite sprite)
            {
                return m_Sprite.TryGetTarget(out Sprite target) && target != null && ReferenceEquals(target, sprite);
            }
        }

        private sealed class PendingBake
        {
            public readonly BakeKey Key;
            public readonly int SpreadBucket;
            public readonly WeakReference<Sprite> Sprite = new WeakReference<Sprite>(null);
            public readonly List<WeakReference<Graphic>> Waiters = new List<WeakReference<Graphic>>(4);

            public PendingBake(BakeKey key, int spreadBucket)
            {
                Key = key;
                SpreadBucket = spreadBucket;
            }
        }

        private readonly struct BakeKey : IEquatable<BakeKey>
        {
            private readonly int m_SpriteId;
            private readonly int m_SpreadBucket;

            public BakeKey(int spriteId, int spreadBucket)
            {
                m_SpriteId = spriteId;
                m_SpreadBucket = spreadBucket;
            }

            public bool Equals(BakeKey other)
            {
                return m_SpriteId == other.m_SpriteId && m_SpreadBucket == other.m_SpreadBucket;
            }

            public override bool Equals(object obj)
            {
                return obj is BakeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (m_SpriteId * 397) ^ m_SpreadBucket;
                }
            }
        }
    }
}
