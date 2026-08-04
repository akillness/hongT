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

            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

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

            Assert.That(
                violations,
                Is.Empty,
                $"Texture importer maxTextureSize must be <= {MaxTextureSize}. Offending settings:\n" +
                string.Join("\n", violations));
        }
    }
}
