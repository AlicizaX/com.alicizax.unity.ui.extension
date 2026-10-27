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
    /// </summary>
    public sealed class UXImageEffectEditModeTests
    {
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

            _texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            _sprite = Sprite.Create(_texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f));
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
        public void MaterialForRenderingDoesNotCreatePerColorEffectMaterials()
        {
            _image.enableOutline = true;

            GameObject otherObject = new GameObject("image2", typeof(RectTransform), typeof(CanvasRenderer), typeof(UXImage));
            otherObject.transform.SetParent(_root.transform, false);
            UXImage other = otherObject.GetComponent<UXImage>();
            other.sprite = _sprite;
            other.enableOutline = true;
            other.outlineEffectColor = Color.red;

            // Before the mesh/material update path runs, both Graphics fall back to the same shared UI material;
            // no per-color effect material may be created by a getter anymore.
            Assert.That(other.materialForRendering, Is.SameAs(_image.materialForRendering));
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
    }
}
