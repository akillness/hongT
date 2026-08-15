// Equip-prop texture import hardening: everything under
// Assets/Resources/Textures/Props/ is a TILING material sheet sampled by the
// prop materials through _BaseMap_ST (PropImportPipeline.BindPropTexture), so
// it MUST wrap (Repeat) — Clamp smears the edge pixel along a blade — and MUST
// stay inside the WebGL 1024 texture ceiling (CLAUDE.md §1).
//
// TWO THINGS THIS FILE EXISTS FOR, both of which bite silently otherwise:
//
//   1. WebGlTextureCapTests walks every TextureImporter under Assets and fails
//      on a default platform size over 1024. gti writes these sheets well above
//      that, and Unity's import default is 2048 — so without a postprocessor
//      the five new sheets turn the EditMode suite red the moment they land.
//   2. Setting maxTextureSize alone leaves the WEBGL platform override at 2048,
//      and the override is what actually ships (DungeonKitImportPipeline records
//      the same trap). The explicit SetPlatformTextureSettings below is the half
//      that reaches the build.
//
// Assigning wrapMode on a loaded Texture2D at material-bind time does NOT write
// the .meta and is lost on the next reimport, which would silently kill the
// tiling. The importer is the only durable place for it.
//
// AssetPostprocessor covers fresh imports; ImportAll is the idempotent CLI entry
// for re-runs (mirrors EnvTextureImportPipeline).
using UnityEditor;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    public sealed class PropTextureImportPipeline : AssetPostprocessor
    {
        public const string PropRoot = "Assets/Resources/Textures/Props/";

        /// <summary>WebGL texture ceiling from CLAUDE.md §1.</summary>
        public const int MaxSize = 1024;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(PropRoot)) return;
            Apply((TextureImporter)assetImporter);
        }

        static void Apply(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            // Mips ON: a prop is ~40-60 px at the dungeon camera, so the sheet is
            // sampled far below its own resolution and aliases without them.
            importer.mipmapEnabled = true;
            importer.maxTextureSize = MaxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.sRGBTexture = true;
            // The half that reaches the build: the default above does not
            // constrain the WebGL override.
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "WebGL",
                overridden = true,
                maxTextureSize = MaxSize,
                format = TextureImporterFormat.Automatic,
                textureCompression = TextureImporterCompression.Compressed,
            });
        }

        /// <summary>Batch entry: re-applies the settings to every prop sheet.</summary>
        public static void ImportAll()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[]
            {
                PropRoot.TrimEnd('/')
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
            Debug.Log($"PropTextureImportPipeline: reimported {guids.Length} textures");
        }
    }
}
