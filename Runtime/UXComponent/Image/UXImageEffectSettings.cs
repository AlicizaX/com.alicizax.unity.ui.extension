using System;
using System.Collections.Generic;

namespace UnityEngine.UI
{
    /// <summary>
    /// Shared helpers for the UXImage adaptive effect.
    ///
    /// 材质按 (Shader, SDF 贴图, 描边颜色, 阴影颜色) 共享：状态完全相同的 Graphic 复用同一个材质实例，所以 UGUI 可以合批。
    /// 每个使用者通过 <see cref="AcquireEffectMaterial"/> / <see cref="ReleaseEffectMaterial"/> 维护引用计数：
    /// - 引用计数归零的材质不会立刻销毁，而是留在有界缓存里等待同状态的 Graphic 复用；
    /// - 缓存超过 <see cref="MaxCachedMaterials"/> 时按 LRU 淘汰引用计数为 0 的项，仍被使用的材质绝不销毁；
    /// - <see cref="ReleaseAll"/> 销毁所有缓存材质并清空索引，由 UXImage.ReleaseRuntimeEffectCaches() 和
    ///   play mode 的 SubsystemRegistration 重置调用。
    /// 动画颜色因此只会替换（或在独占时原地修改）材质，永远不会无界增长 GPU 内存。
    /// </summary>
    internal static class UXImageEffectSettings
    {
        public const string ShaderName = "UI/UXImageAdaptiveEffect";
        private const string ShaderResourcePath = "UXImageAdaptiveEffect";

        public const float RuntimeSdfSoftnessThreshold = 8f;

        /// <summary>共享材质缓存上限（LRU）。超出后只淘汰引用计数为 0 的项。</summary>
        private const int MaxCachedMaterials = 32;

        private static readonly int SdfTextureProperty = Shader.PropertyToID("_SDFTex");
        private static readonly int OutlineColorProperty = Shader.PropertyToID("_OutlineColor");
        private static readonly int ShadowColorProperty = Shader.PropertyToID("_ShadowColor");

        private static readonly Dictionary<MaterialKey, CachedMaterial> s_Cache =
            new Dictionary<MaterialKey, CachedMaterial>(MaxCachedMaterials);

        private static Shader s_Shader;
        private static long s_AccessCounter;

        /// <summary>当前缓存中的材质项数量（含引用计数为 0 的待复用项），用于测试断言缓存有界。</summary>
        internal static int CachedMaterialCount
        {
            get { return s_Cache.Count; }
        }

        /// <summary>
        /// 取得一个与给定状态匹配的共享材质。命中缓存时只增加引用计数，不产生任何托管分配；
        /// 未命中时创建一个新的运行时材质并加入有界缓存。
        /// 调用方必须成对调用 <see cref="ReleaseEffectMaterial"/>。
        /// </summary>
        public static Material AcquireEffectMaterial(Texture sdfTexture, Color outline, Color shadow)
        {
            Shader shader = ResolveShader();
            if (shader == null)
                return null;

            MaterialKey key = new MaterialKey(shader, sdfTexture, outline, shadow);
            if (s_Cache.TryGetValue(key, out CachedMaterial cached))
            {
                if (cached.Material != null)
                {
                    cached.RefCount++;
                    cached.LastUse = ++s_AccessCounter;
                    return cached.Material;
                }

                // 材质已被外部销毁（例如崩溃恢复后的域重载），清掉残留项后重新创建。
                s_Cache.Remove(key);
            }

            Material material = new Material(shader)
            {
                name = "UXImage Adaptive Effect",
                hideFlags = HideFlags.HideAndDontSave
            };
            ApplyMaterialState(material, sdfTexture, outline, shadow);

            s_Cache.Add(key, new CachedMaterial(key, material));
            TrimCache();
            return material;
        }

        /// <summary>
        /// 释放一次 <see cref="AcquireEffectMaterial"/> 的引用。引用归零的项保留在缓存里供后续复用，
        /// 只有缓存超过上限时才被 LRU 淘汰。
        /// </summary>
        public static void ReleaseEffectMaterial(Material material)
        {
            CachedMaterial cached = Find(material);
            if (cached == null)
                return;

            if (cached.RefCount > 0)
                cached.RefCount--;
            cached.LastUse = ++s_AccessCounter;

            TrimCache();
        }

        /// <summary>查询材质的当前引用计数；未在缓存中的材质返回 0。仅用于测试与断言。</summary>
        internal static int GetMaterialRefCount(Material material)
        {
            CachedMaterial cached = Find(material);
            return cached != null ? cached.RefCount : 0;
        }

        /// <summary>
        /// 就地写入材质状态。只允许在材质引用计数为 1（当前 Graphic 独占）时调用，
        /// 这样不会影响其它 Graphic 的合批或渲染结果。
        /// </summary>
        internal static void ApplyMaterialState(Material material, Texture sdfTexture, Color outline, Color shadow)
        {
            if (material == null)
                return;

            material.SetTexture(SdfTextureProperty, sdfTexture);
            material.SetColor(OutlineColorProperty, outline);
            material.SetColor(ShadowColorProperty, shadow);
        }

        /// <summary>销毁所有缓存材质并清空索引。可重复调用。</summary>
        public static void ReleaseAll()
        {
            foreach (KeyValuePair<MaterialKey, CachedMaterial> pair in s_Cache)
                DestroyRuntimeObject(pair.Value.Material);

            s_Cache.Clear();
            s_AccessCounter = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayModeStart()
        {
            s_Shader = null;
            ReleaseAll();
        }

        private static CachedMaterial Find(Material material)
        {
            if (material == null)
                return null;

            // 缓存上限只有 32，线性扫描比维护第二份字典更简单，也不会在取值时产生分配。
            foreach (KeyValuePair<MaterialKey, CachedMaterial> pair in s_Cache)
            {
                if (ReferenceEquals(pair.Value.Material, material))
                    return pair.Value;
            }

            return null;
        }

        private static void TrimCache()
        {
            while (s_Cache.Count > MaxCachedMaterials)
            {
                MaterialKey oldestKey = default;
                CachedMaterial oldest = null;
                foreach (KeyValuePair<MaterialKey, CachedMaterial> pair in s_Cache)
                {
                    CachedMaterial candidate = pair.Value;
                    if (candidate.RefCount != 0)
                        continue;

                    if (oldest == null || candidate.LastUse < oldest.LastUse)
                    {
                        oldest = candidate;
                        oldestKey = pair.Key;
                    }
                }

                if (oldest == null)
                    return;

                s_Cache.Remove(oldestKey);
                DestroyRuntimeObject(oldest.Material);
            }
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

        private sealed class CachedMaterial
        {
            public readonly MaterialKey Key;
            public readonly Material Material;
            public int RefCount;
            public long LastUse;

            public CachedMaterial(MaterialKey key, Material material)
            {
                Key = key;
                Material = material;
                RefCount = 1;
                LastUse = ++s_AccessCounter;
            }
        }

        private readonly struct MaterialKey : IEquatable<MaterialKey>
        {
            private readonly Shader m_Shader;
            private readonly Texture m_SdfTexture;
            private readonly Color m_OutlineColor;
            private readonly Color m_ShadowColor;

            public MaterialKey(Shader shader, Texture sdfTexture, Color outlineColor, Color shadowColor)
            {
                m_Shader = shader;
                m_SdfTexture = sdfTexture;
                m_OutlineColor = outlineColor;
                m_ShadowColor = shadowColor;
            }

            public bool Equals(MaterialKey other)
            {
                return ReferenceEquals(m_Shader, other.m_Shader) &&
                       ReferenceEquals(m_SdfTexture, other.m_SdfTexture) &&
                       m_OutlineColor == other.m_OutlineColor &&
                       m_ShadowColor == other.m_ShadowColor;
            }

            public override bool Equals(object obj)
            {
                return obj is MaterialKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = m_Shader != null ? m_Shader.GetInstanceID() : 0;
                    hash = (hash * 397) ^ (m_SdfTexture != null ? m_SdfTexture.GetInstanceID() : 0);
                    hash = (hash * 397) ^ m_OutlineColor.GetHashCode();
                    hash = (hash * 397) ^ m_ShadowColor.GetHashCode();
                    return hash;
                }
            }
        }
    }
}
