using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class HazardTextureImportTests
    {
        const string HazardRoot = "Assets/Resources/Textures/Hazards/";
        const string FixtureRoot = HazardRoot + "__ImportTest";
        const int ExpectedTextureSize = 512;

        [SetUp]
        public void SetUp()
        {
            Directory.CreateDirectory(FixtureRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(FixtureRoot))
            {
                FileUtil.DeleteFileOrDirectory(FixtureRoot);
                FileUtil.DeleteFileOrDirectory(FixtureRoot + ".meta");
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void OpaqueUnderlay_ImportsAsUnreadableClampedCompressedSrgbWithMipsAndWebGlCap()
        {
            var importer = ImportFixture("cinder-span-ember-vent-underlay.png");

            ApplyHazardPolicy(importer);

            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(importer.maxTextureSize, Is.EqualTo(ExpectedTextureSize),
                "runtime import resolution must match the accepted 512px provenance contract.");
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.None));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Compressed));
            AssertWebGlCap(importer);
        }

        [Test]
        public void TiledStoneWallBody_ImportsWithRepeatWrapForWorldLengthMapping()
        {
            var importer = ImportFixture("cinder-sluice-stone-wall-body.png");

            ApplyHazardPolicy(importer);

            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat),
                "StoneWall body textures map along a variable-length segment and must tile instead of smearing edge pixels.");
            Assert.That(importer.mipmapEnabled, Is.True);
            AssertWebGlCap(importer);
        }

        [TestCase("echo-throne-tide-current-bed.png")]
        [TestCase("ash-march-ash-wall-band.png")]
        public void DynamicBandSurface_ImportsWithRepeatWrapForFixedTexelDensity(string fileName)
        {
            var importer = ImportFixture(fileName);

            ApplyHazardPolicy(importer);

            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat),
                "Current and AshWall surfaces crop/reveal at fixed texel density; "
                + "Repeat prevents stretching or exposed edge pixels.");
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.None));
            Assert.That(importer.mipmapEnabled, Is.True);
            AssertWebGlCap(importer);
        }

        [Test]
        public void AlphaEdgeRole_PreservesAlphaWithoutMipsAndKeepsClampWrap()
        {
            var importer = ImportFixture("ash-march-ash-wall-front-edge.png");

            ApplyHazardPolicy(importer);

            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.mipmapEnabled, Is.False,
                "thin state/edge trims are sampled at authored screen density; mips blur the cue and can leak background.");
            Assert.That(importer.isReadable, Is.False);
            AssertWebGlCap(importer);
        }

        [Test]
        public void ImportPipeline_ClaimsOnlyHazardTextureRootAndLeavesEnvPipelineSeparate()
        {
            var type = RequireType("CinderCourt.EditorTools.HazardTextureImportPipeline");
            var root = ReadStaticString(type, "HazardRoot", "Root");

            Assert.That(root, Is.EqualTo(HazardRoot));
            Assert.That(root, Is.Not.EqualTo("Assets/Resources/Textures/Env/"),
                "Hazard alpha/opaque policy must not be folded into EnvTextureImportPipeline, which strips alpha and repeats every map.");
        }

        static TextureImporter ImportFixture(string fileName)
        {
            var path = FixtureRoot + "/" + fileName;
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            try
            {
                var pixels = Enumerable.Repeat(new Color32(32, 48, 56, 255), 64).ToArray();
                pixels[0] = new Color32(255, 255, 255, 64);
                texture.SetPixels32(pixels);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            return importer;
        }

        static void ApplyHazardPolicy(TextureImporter importer)
        {
            var type = RequireType("CinderCourt.EditorTools.HazardTextureImportPipeline");
            var method = type.GetMethod(
                "Apply",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(TextureImporter) },
                null);

            Assert.That(method, Is.Not.Null,
                "HazardTextureImportPipeline.Apply(TextureImporter) must be callable by ImportAll and EditMode policy tests.");
            method.Invoke(null, new object[] { importer });
        }

        static void AssertWebGlCap(TextureImporter importer)
        {
            var webGl = importer.GetPlatformTextureSettings("WebGL");
            Assert.That(webGl.overridden, Is.True,
                importer.assetPath + ": hazard textures must carry an explicit WebGL override for deterministic build size.");
            Assert.That(webGl.maxTextureSize, Is.EqualTo(ExpectedTextureSize),
                importer.assetPath + ": WebGL must preserve the accepted 512px hazard detail.");
            Assert.That(webGl.format, Is.Not.EqualTo(TextureImporterFormat.Automatic),
                importer.assetPath + ": WebGL override must choose a concrete compressed format.");
        }

        static Type RequireType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            throw new AssertionException(fullName + " is required by the stage hazard import contract.");
        }

        static string ReadStaticString(Type type, params string[] names)
        {
            foreach (var name in names)
            {
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (field != null) return (string)field.GetValue(null);

                var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (property != null) return (string)property.GetValue(null);
            }

            throw new AssertionException(type.FullName + " must expose a hazard texture root constant.");
        }
    }
}
