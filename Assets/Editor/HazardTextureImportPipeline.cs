// Hazard texture import hardening: stage gimmick surfaces live under
// Assets/Resources/Textures/Hazards/ and mix opaque physical beds with thin
// alpha trims. Keep this policy separate from EnvTextureImportPipeline; env
// maps always repeat and strip alpha, while hazard roles need per-filename
// wrap and alpha behavior.
using System;
using UnityEditor;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    public sealed class HazardTextureImportPipeline : AssetPostprocessor
    {
        public const string HazardRoot = "Assets/Resources/Textures/Hazards/";
        public const int MaxSize = 512;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(HazardRoot, StringComparison.Ordinal)) return;
            Apply((TextureImporter)assetImporter);
        }

        public static void Apply(TextureImporter importer)
        {
            var role = GetRole(importer.assetPath);
            var alphaRole = IsAlphaRole(role);

            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = MaxSize;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.sRGBTexture = true;

            if (alphaRole)
            {
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
            }
            else
            {
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = true;
                importer.wrapMode = UsesRepeatWrap(role) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            }

            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "WebGL",
                overridden = true,
                maxTextureSize = MaxSize,
                format = alphaRole ? TextureImporterFormat.ETC2_RGBA8 : TextureImporterFormat.ETC2_RGB4,
                textureCompression = TextureImporterCompression.Compressed
            });
        }

        public static void ImportAll()
        {
            var found = 0;
            var normalized = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { HazardRoot.TrimEnd('/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

                found += 1;
                Apply(importer);
                importer.SaveAndReimport();
                normalized += 1;
            }

            Debug.Log($"HazardTextureImportPipeline: reimported {normalized}/{found} textures");
        }

        static string GetRole(string path)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (name.EndsWith("-front-edge", StringComparison.Ordinal)) return "front-edge";

            var lastDash = name.LastIndexOf('-');
            return lastDash >= 0 ? name.Substring(lastDash + 1) : string.Empty;
        }

        static bool IsAlphaRole(string role) =>
            role == "front-edge"
            || role == "edge"
            || role == "mask"
            || role == "rim"
            || role == "trim"
            || role == "decal";

        static bool UsesRepeatWrap(string role) =>
            role == "body"
            || role == "albedo"
            || role == "bed"
            || role == "band";
    }
}
