using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace CinderCourt.Tests
{
    public sealed class AudioPreloadTests
    {
        private const string AudioResourcesDirectory = "Assets/Resources/Audio";
        private const string WebGlPlatform = "WebGL";

        [Test]
        public void AudioDirectorMp3Clips_PreloadAudioDataForWebGl()
        {
            var mp3AssetPaths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets(
                         "t:AudioClip",
                         new[] { AudioResourcesDirectory }))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (assetPath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                    mp3AssetPaths.Add(assetPath);
            }
            mp3AssetPaths.Sort(StringComparer.Ordinal);

            Assert.That(
                mp3AssetPaths,
                Is.Not.Empty,
                $"No MP3 AudioClips found under {AudioResourcesDirectory}");

            var violations = new List<string>();
            foreach (var assetPath in mp3AssetPaths)
            {
                if (AssetImporter.GetAtPath(assetPath) is not AudioImporter importer)
                {
                    violations.Add($"missing MP3 AudioImporter at {assetPath}");
                    continue;
                }

                var webGlSettings = importer.GetOverrideSampleSettings(WebGlPlatform);
                if (!webGlSettings.preloadAudioData)
                    violations.Add($"preloadAudioData=false for WebGL ({assetPath})");
            }

            Assert.That(
                violations,
                Is.Empty,
                "AudioDirector MP3 clips must preload audio data on WebGL to avoid decode churn:\n" +
                string.Join("\n", violations));
        }
    }
}
