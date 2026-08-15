using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace CinderCourt.Tests
{
    public sealed class WebGlTextureCapTests
    {
        private const int MaxTextureSize = 1024;

        [Test]
        public void TextureImporters_DefaultAndWebGlCapsDoNotExceed1024()
        {
            var violations = new List<string>();
            var inspected = 0;

            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                inspected++;

                var defaultSettings = importer.GetDefaultPlatformTextureSettings();
                if (defaultSettings.maxTextureSize > MaxTextureSize)
                {
                    violations.Add(
                        $"{path}: DefaultTexturePlatform maxTextureSize={defaultSettings.maxTextureSize}");
                }

                var webGlSettings = importer.GetPlatformTextureSettings("WebGL");
                if (webGlSettings.overridden && webGlSettings.maxTextureSize > MaxTextureSize)
                {
                    violations.Add(
                        $"{path}: WebGL (overridden) maxTextureSize={webGlSettings.maxTextureSize}");
                }
            }

            // An empty scan produces an empty violation list and passes green
            // having verified nothing — the same shape make_release_evidence.py
            // refuses for the shadow subset ("it would certify the contract by
            // having tested nothing") and StageShadowCatalogTests guards with a
            // count floor. The floor is deliberately loose: it exists to catch a
            // broken scan, not to pin an asset count that grows every cycle.
            Assert.That(inspected, Is.GreaterThan(100),
                $"only {inspected} texture importers inspected — the scan is broken, "
                + "so an empty violation list means nothing");

            Assert.That(
                violations,
                Is.Empty,
                $"Texture importer maxTextureSize must be <= {MaxTextureSize}. Offending settings:\n" +
                string.Join("\n", violations));
        }
    }
}
