using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AlicizaX.UI.NavigationTests
{
    /// <summary>
    /// CPU-only coverage for the UXImage adaptive effect changes. The atlas/SDF RenderTexture paths need a graphics
    /// device and are intentionally not exercised here; these tests only assert that the rendering getters stay pure
    /// (no atlas material/GPU work), that canvas channels keep being ensured and that the release API is callable.
    /// 网格相关的用例通过反射直接驱动 UXImage.OnPopulateMesh，因此不依赖 Canvas 的编辑器刷新时机。
    /// 材质用例刻意使用 Image.Type.Sliced：它不会进入运行时图集分支，从而避免在 EditMode 下创建 2048² 图集页。
    /// </summary>
    public sealed class UXImageEffectEditModeTests
    {
        private static readonly MethodInfo s_OnPopulateMeshMethod = typeof(UXImage).GetMethod(
            "OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        private static readonly MethodInfo s_CopyImageMethod = typeof(UXImage).GetMethod(
            "CopyImage", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        private static readonly FieldInfo s_MeshIndicesField = typeof(UXImage).GetField(
            "m_MeshIndices", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        private GameObject _root;
        private Canvas _canvas;
        private UXImage _image;
        private Sprite _sprite;
        private Texture2D _texture;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("uximage-effect-test", typeof(RectTransform), typeof(Canvas));
            _canvas = _root.GetComponent<Canvas>();

            GameObject imageObject = new GameObject("image", typeof(RectTransform), typeof(CanvasRenderer), typeof(UXImage));
            imageObject.transform.SetParent(_root.transform, false);
            _image = imageObject.GetComponent<UXImage>();

            _texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[32 * 32];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            _texture.SetPixels32(pixels);
            _texture.Apply();

            // 固定 FullRect 网格，保证是一个标准的 4 顶点 / 2 三角形四边形。
            _sprite = Sprite.Create(
                _texture,
                new Rect(0f, 0f, 32f, 32f),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            _image.sprite = _sprite;
        }

        [TearDown]
        public void TearDown()
        {
            UXImage.ReleaseRuntimeEffectCaches();

            if (_root != null)
                Object.DestroyImmediate(_root);
            if (_sprite != null)
                Object.DestroyImmediate(_sprite);
            if (_texture != null)
                Object.DestroyImmediate(_texture);
        }

        [Test]
        public void MainTextureGetterDoesNotCreateRuntimeAtlasTexture()
        {
            _image.enableOutline = true;

            Texture mainTexture = _image.mainTexture;

            Assert.That(mainTexture, Is.SameAs(_texture));
            Assert.That(mainTexture is RenderTexture, Is.False);
        }

        [Test]
        public void MaterialForRenderingDoesNotAllocateEffectMaterials()
        {
            _image.enableOutline = true;
            int cachedBefore = GetCachedMaterialCount();

            // 取渲染材质是纯读取：既不能创建材质，也不能改变共享缓存的条目数。
            Material first = _image.materialForRendering;
            Material second = _image.materialForRendering;

            Assert.That(ReferenceEquals(first, second), Is.True);
            Assert.That(GetCachedMaterialCount(), Is.EqualTo(cachedBefore));
        }
        [Test]
        public void EnablingEffectsEnsuresRequiredCanvasChannels()
        {
            _canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
            _image.enableOutline = true;

            const AdditionalCanvasShaderChannels required =
                AdditionalCanvasShaderChannels.TexCoord1 |
                AdditionalCanvasShaderChannels.TexCoord2 |
                AdditionalCanvasShaderChannels.TexCoord3 |
                AdditionalCanvasShaderChannels.Normal |
                AdditionalCanvasShaderChannels.Tangent;

            Assert.That(_canvas.additionalShaderChannels & required, Is.EqualTo(required));
        }

        [Test]
        public void SoftnessSettersClampAndKeepValues()
        {
            _image.enableShadow = true;

            _image.outlineSoftness = -3f;
            _image.shadowSoftness = -1f;

            Assert.That(_image.outlineSoftness, Is.EqualTo(0f));
            Assert.That(_image.shadowSoftness, Is.EqualTo(0f));

            _image.shadowSoftness = 12.5f;
            Assert.That(_image.shadowSoftness, Is.EqualTo(12.5f));
        }

        [Test]
        public void ReleaseRuntimeEffectCachesIsRepeatableAndKeepsGettersPure()
        {
            _image.enableOutline = true;

            UXImage.ReleaseRuntimeEffectCaches();
            UXImage.ReleaseRuntimeEffectCaches();

            Assert.That(_image.mainTexture, Is.SameAs(_texture));
        }

        [Test]
        public void VerticalFlipWithoutCopyMirrorsYNotX()
        {
            // pivot 放在左下 + 非正方形：这样 xMin+xMax != yMin+yMax，用错轴的重映射会立刻越界。
            _image.rectTransform.pivot = Vector2.zero;
            _image.rectTransform.sizeDelta = new Vector2(100f, 60f);
            _image.type = Image.Type.Simple;
            _image.useSpriteMesh = false;
            _image.flipWithCopy = false;

            _image.flipMode = UXImage.FlipMode.None;
            Mesh baselineMesh = BuildMesh(_image);
            Vector3[] baselinePositions = baselineMesh.vertices;
            Object.DestroyImmediate(baselineMesh);

            _image.flipMode = UXImage.FlipMode.Vertical;
            Mesh flippedMesh = BuildMesh(_image);
            Vector3[] flippedPositions = flippedMesh.vertices;
            Object.DestroyImmediate(flippedMesh);

            Assert.That(flippedPositions.Length, Is.EqualTo(baselinePositions.Length));

            Rect rect = _image.rectTransform.rect;
            for (int i = 0; i < flippedPositions.Length; i++)
            {
                Vector3 position = flippedPositions[i];
                Assert.That(IsFinite(position.x) && IsFinite(position.y), Is.True, "存在 NaN/Infinity 顶点。");
                Assert.That(position.x, Is.InRange(rect.xMin - 0.01f, rect.xMax + 0.01f), "X 超出 rect 边界。");
                Assert.That(position.y, Is.InRange(rect.yMin - 0.01f, rect.yMax + 0.01f), "Y 超出 rect 边界。");

                // 垂直翻转必须重映射 y，并且不能擅自改写 x。
                Assert.That(position.x, Is.EqualTo(baselinePositions[i].x).Within(0.01f), "垂直翻转改写了 X 轴。");
                Assert.That(position.y, Is.EqualTo(2f * rect.center.y - baselinePositions[i].y).Within(0.01f),
                    "垂直翻转没有围绕 rect 中心镜像 Y 轴。");
            }
        }

        [Test]
        public void DegenerateRectDoesNotProduceNaNVertices()
        {
            _image.rectTransform.sizeDelta = Vector2.zero;
            _image.flipMode = UXImage.FlipMode.Vertical;
            _image.flipWithCopy = false;

            Mesh mesh = BuildMesh(_image);
            Vector3[] positions = mesh.vertices;
            Object.DestroyImmediate(mesh);

            Assert.That(positions.Length, Is.GreaterThan(0));
            for (int i = 0; i < positions.Length; i++)
            {
                Assert.That(IsFinite(positions[i].x) && IsFinite(positions[i].y), Is.True,
                    "退化 rect 产生了 NaN/Infinity 顶点。");
            }
        }

        [Test]
        public void ImagesWithSameEffectColorsShareOneMaterial()
        {
            ConfigureEffectMaterialImage(_image);

            UXImage other = CreateImage("image2");
            ConfigureEffectMaterialImage(other);

            BuildAndDispose(_image);
            BuildAndDispose(other);

            Material first = _image.materialForRendering;
            Material second = other.materialForRendering;

            Assert.That(first, Is.Not.Null);
            Assert.That(ReferenceEquals(first, second), Is.True,
                "SDF 状态与特效颜色相同的 Graphic 必须共享同一个材质实例以保持合批。");
        }

        [Test]
        public void ChangingEffectColorSwapsMaterialAndKeepsCacheBounded()
        {
            UXImage a = _image;
            ConfigureEffectMaterialImage(a);

            UXImage b = CreateImage("image2");
            ConfigureEffectMaterialImage(b);

            BuildAndDispose(a);
            BuildAndDispose(b);

            Material shared = a.materialForRendering;
            Assert.That(ReferenceEquals(shared, b.materialForRendering), Is.True, "初始状态应共享材质。");

            // 共享中的材质不能就地修改：必须换一个新实例，旧材质引用计数降为 1（b 仍持有）。
            a.outlineEffectColor = Color.red;
            Material swapped = a.materialForRendering;
            Assert.That(ReferenceEquals(swapped, shared), Is.False, "共享材质被修改时必须切换到新材质实例。");
            Assert.That(GetMaterialRefCount(shared), Is.EqualTo(1));

            // b 释放后旧材质引用计数归零，但仍保留在缓存中等待复用。
            b.enableOutline = false;
            Assert.That(GetMaterialRefCount(shared), Is.EqualTo(0));

            // 恢复相同状态；材质的唯一创建入口是网格重建路径，因此需要重建一次来重新取用缓存材质。
            b.enableOutline = true;
            BuildAndDispose(b);
            Assert.That(ReferenceEquals(b.materialForRendering, shared), Is.True, "相同状态必须复用缓存中的材质实例。");

            Assert.That(GetCachedMaterialCount(), Is.LessThanOrEqualTo(32), "材质缓存必须保持在有界上限内。");
        }

        [Test]
        public void CopyImagePreservesCustomSpriteMesh()
        {
            _image.rectTransform.sizeDelta = new Vector2(100f, 100f);
            _image.useSpriteMesh = true;
            _image.flipMode = UXImage.FlipMode.Horziontal;
            _image.flipWithCopy = true;

            Mesh mesh = BuildMesh(_image);
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.GetIndices(0);
            Object.DestroyImmediate(mesh);

            ushort[] spriteTriangles = _sprite.triangles;
            int spriteVertexCount = _sprite.vertices.Length;

            Assert.That(spriteVertexCount, Is.EqualTo(4));
            Assert.That(vertices.Length, Is.EqualTo(spriteVertexCount * 2));
            Assert.That(triangles.Length, Is.EqualTo(spriteTriangles.Length * 2));

            for (int i = 0; i < spriteTriangles.Length; i++)
            {
                Assert.That(triangles[i], Is.EqualTo((int)spriteTriangles[i]));
                Assert.That(triangles[i + spriteTriangles.Length], Is.EqualTo((int)spriteTriangles[i] + spriteVertexCount),
                    "复制出的三角形必须整体偏移原始顶点数。");
            }

            // 任意三角形列表（6 顶点 / 4 三角形）：旧实现按 4 顶点步长补三角形，会漏掉一半三角形。
            int[] customTriangles =
            {
                0, 1, 2,
                2, 3, 4,
                4, 5, 0,
                1, 3, 5
            };

            using (VertexHelper helper = new VertexHelper())
            {
                for (int i = 0; i < 6; i++)
                    helper.AddVert(new Vector3(i, i * 0.5f, 0f), Color.white, new Vector2(0f, 0f));

                for (int i = 0; i < customTriangles.Length; i += 3)
                    helper.AddTriangle(customTriangles[i], customTriangles[i + 1], customTriangles[i + 2]);

                // 真实生成路径会把索引同时记录到镜像中；这里手动补齐同样的状态。
                SeedMeshIndices(_image, customTriangles);

                Assert.That(s_CopyImageMethod, Is.Not.Null, "UXImage.CopyImage not found.");
                s_CopyImageMethod.Invoke(_image, new object[] { helper });

                Mesh copied = new Mesh();
                try
                {
                    helper.FillMesh(copied);

                    Assert.That(copied.vertexCount, Is.EqualTo(12));
                    int[] copiedTriangles = copied.GetIndices(0);
                    Assert.That(copiedTriangles.Length, Is.EqualTo(customTriangles.Length * 2));

                    for (int i = 0; i < customTriangles.Length; i++)
                    {
                        Assert.That(copiedTriangles[i], Is.EqualTo(customTriangles[i]));
                        Assert.That(copiedTriangles[i + customTriangles.Length], Is.EqualTo(customTriangles[i] + 6),
                            "非四边形网格的每条三角形都必须按顶点数偏移后复制。");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(copied);
                }
            }
        }

        private void ConfigureEffectMaterialImage(UXImage image)
        {
            // Sliced 不进入运行时图集分支（图集只服务 4 顶点的 Simple/Filled），因此 EditMode 下不会创建 2048² 页。
            image.type = Image.Type.Sliced;
            image.rectTransform.sizeDelta = new Vector2(100f, 100f);
            image.enableOutline = true;
        }

        private UXImage CreateImage(string name)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(UXImage));
            imageObject.transform.SetParent(_root.transform, false);
            UXImage image = imageObject.GetComponent<UXImage>();
            image.sprite = _sprite;
            return image;
        }

        private static void BuildAndDispose(UXImage image)
        {
            Mesh mesh = BuildMesh(image);
            Object.DestroyImmediate(mesh);
        }

        private static Mesh BuildMesh(UXImage image)
        {
            Assert.That(s_OnPopulateMeshMethod, Is.Not.Null, "UXImage.OnPopulateMesh not found.");

            using (VertexHelper helper = new VertexHelper())
            {
                s_OnPopulateMeshMethod.Invoke(image, new object[] { helper });

                Mesh mesh = new Mesh();
                helper.FillMesh(mesh);
                return mesh;
            }
        }

        private static void SeedMeshIndices(UXImage image, int[] indices)
        {
            Assert.That(s_MeshIndicesField, Is.Not.Null, "UXImage.m_MeshIndices not found.");

            List<int> mirror = (List<int>)s_MeshIndicesField.GetValue(image);
            mirror.Clear();
            for (int i = 0; i < indices.Length; i++)
                mirror.Add(indices[i]);
        }

        private static int GetMaterialRefCount(Material material)
        {
            MethodInfo method = GetEffectSettingsType().GetMethod(
                "GetMaterialRefCount", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.That(method, Is.Not.Null, "UXImageEffectSettings.GetMaterialRefCount not found.");
            return (int)method.Invoke(null, new object[] { material });
        }

        private static int GetCachedMaterialCount()
        {
            PropertyInfo property = GetEffectSettingsType().GetProperty(
                "CachedMaterialCount", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.That(property, Is.Not.Null, "UXImageEffectSettings.CachedMaterialCount not found.");
            return (int)property.GetValue(null);
        }

        private static Type GetEffectSettingsType()
        {
            Type type = typeof(UXImage).Assembly.GetType("UnityEngine.UI.UXImageEffectSettings", false);
            Assert.That(type, Is.Not.Null, "UXImageEffectSettings type not found.");
            return type;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
