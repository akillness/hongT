// Stage environment texture import hardening: everything under
// Assets/Resources/Textures/Env/ is a TILING albedo map sampled by
// EnvironmentBuilder's shared stone/floor materials through a per-piece
// _BaseMap_ST, so it MUST wrap (Repeat) — Clamp smears the edge pixel across
// every tiled wall — and MUST stay inside the WebGL 1024 texture ceiling
// (CLAUDE.md §1). Generated at ~1254 px by tools/gen_env_textures.sh, so the
// importer is the only thing enforcing the ceiling.
//
// AssetPostprocessor covers fresh imports; ImportAll is the idempotent CLI
// entry for re-runs (mirrors IconImportPipeline).
using UnityEditor;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    public sealed class EnvTextureImportPipeline : AssetPostprocessor
    {
        public const string EnvRoot = "Assets/Resources/Textures/Env/";

        /// <summary>WebGL texture ceiling from CLAUDE.md §1.</summary>
        public const int MaxSize = 1024;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(EnvRoot)) return;
            Apply((TextureImporter)assetImporter);
        }

        static void Apply(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            // Mips ON: these are floor/wall maps viewed at a 55° rake, so the
            // far end of the arena aliases badly without them.
            importer.mipmapEnabled = true;
            importer.maxTextureSize = MaxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.sRGBTexture = true;
        }

        /// <summary>Batch entry: re-applies the settings to every env texture.</summary>
        public static void ImportAll()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[]
            {
                EnvRoot.TrimEnd('/')
            });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    Apply(importer);
                    importer.SaveAndReimport();
                }
            }
            Debug.Log($"EnvTextureImportPipeline: reimported {guids.Length} textures");
        }
    }
}
