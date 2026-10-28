using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace UnityEngine.UI
{
    /// <summary>
    /// Cache for the managed array copies returned by <see cref="Sprite.vertices"/>, <see cref="Sprite.uv"/> and
    /// <see cref="Sprite.triangles"/>.
    ///
    /// 这三个 getter 每次访问都会复制一份托管数组；UXImage 的自适应网格在每次重建时都会用到它们，因此这里按
    /// sprite instanceId 缓存数组，命中时零托管分配。缓存项只弱引用 Sprite：Sprite 被销毁或 instanceId 被复用时
    /// 视为未命中并移除旧项。缓存上限 <see cref="MaxEntries"/>，超出后按 LRU 淘汰；
    /// <see cref="Clear"/> 由 UXImage.ReleaseRuntimeEffectCaches() 调用，<see cref="Sweep"/> 由场景卸载触发。
    /// 主线程专用（UGUI）。
    /// </summary>
    internal static class UXImageSpriteMeshCache
    {
        private const int MaxEntries = 32;

        private static readonly Dictionary<int, CachedMesh> s_Entries = new Dictionary<int, CachedMesh>(MaxEntries);
        private static readonly List<int> s_DeadKeys = new List<int>(8);

        private static long s_AccessCounter;
        private static bool s_SceneUnloadHooked;

        /// <summary>当前缓存项数量，仅用于测试与调试断言。</summary>
        internal static int CachedMeshCount
        {
            get { return s_Entries.Count; }
        }

        /// <summary>
        /// 取得 sprite 的顶点/UV/三角形数组副本。命中缓存时不产生任何托管分配。
        /// </summary>
        internal static bool TryGet(
            Sprite sprite,
            out Vector2[] vertices,
            out Vector2[] uvs,
            out ushort[] triangles,
            out int triangleCount)
        {
            vertices = null;
            uvs = null;
            triangles = null;
            triangleCount = 0;

            if (sprite == null || sprite.texture == null)
                return false;

            EnsureSceneUnloadHook();

            int instanceId = sprite.GetInstanceID();
            if (s_Entries.TryGetValue(instanceId, out CachedMesh cached))
            {
                if (cached.Matches(sprite))
                {
                    cached.LastUse = ++s_AccessCounter;
                    vertices = cached.Vertices;
                    uvs = cached.Uvs;
                    triangles = cached.Triangles;
                    triangleCount = cached.TriangleCount;
                    return true;
                }

                s_Entries.Remove(instanceId);
            }

            ushort[] sourceTriangles = sprite.triangles;
            Vector2[] sourceVertices = sprite.vertices;
            Vector2[] sourceUvs = sprite.uv;

            if (sourceTriangles == null || sourceUvs == null || sourceVertices == null ||
                sourceVertices.Length == 0 || sourceVertices.Length != sourceUvs.Length)
                return false;

            cached = new CachedMesh(sprite, sourceVertices, sourceUvs, sourceTriangles);
            s_Entries.Add(instanceId, cached);
            EvictIfNeeded();

            vertices = cached.Vertices;
            uvs = cached.Uvs;
            triangles = cached.Triangles;
            triangleCount = cached.TriangleCount;
            return true;
        }

        /// <summary>Drops entries whose sprite is gone or whose instanceId was reused.</summary>
        internal static void Sweep()
        {
            if (s_Entries.Count == 0)
                return;

            s_DeadKeys.Clear();
            foreach (KeyValuePair<int, CachedMesh> pair in s_Entries)
            {
                if (!pair.Value.IsAlive())
                    s_DeadKeys.Add(pair.Key);
            }

            for (int i = 0; i < s_DeadKeys.Count; i++)
                s_Entries.Remove(s_DeadKeys[i]);

            s_DeadKeys.Clear();
        }

        /// <summary>Drops every cached array. Safe to call repeatedly.</summary>
        internal static void Clear()
        {
            s_Entries.Clear();
            s_DeadKeys.Clear();
        }

        private static void EvictIfNeeded()
        {
            while (s_Entries.Count > MaxEntries)
            {
                int oldestKey = 0;
                long oldestUse = long.MaxValue;
                bool found = false;
                foreach (KeyValuePair<int, CachedMesh> pair in s_Entries)
                {
                    if (pair.Value.LastUse >= oldestUse)
                        continue;

                    oldestUse = pair.Value.LastUse;
                    oldestKey = pair.Key;
                    found = true;
                }

                if (!found)
                    return;

                s_Entries.Remove(oldestKey);
            }
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

        private sealed class CachedMesh
        {
            private readonly WeakReference<Sprite> m_Sprite;
            public readonly Vector2[] Vertices;
            public readonly Vector2[] Uvs;
            public readonly ushort[] Triangles;
            public readonly int TriangleCount;
            public long LastUse;

            public CachedMesh(Sprite sprite, Vector2[] vertices, Vector2[] uvs, ushort[] triangles)
            {
                m_Sprite = new WeakReference<Sprite>(sprite);
                Vertices = vertices;
                Uvs = uvs;
                Triangles = triangles;
                TriangleCount = triangles.Length / 3;
                LastUse = ++s_AccessCounter;
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
    }
}
