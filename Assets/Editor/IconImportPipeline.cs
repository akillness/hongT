// Icon import hardening: everything under Assets/Resources/Icons/ must be a
// UI Sprite (Resources.Load<Sprite> returns null for Default textures) with
// alpha-is-transparency (else bilinear sampling shows dark matte fringes) and
// no mips (screen-space UI only).
//
// AssetPostprocessor covers fresh imports in the same batch invocation;
// ImportAll is the idempotent CLI entry for re-runs.
using UnityEditor;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    public sealed class IconImportPipeline : AssetPostprocessor
    {
        const string IconRoot = "Assets/Resources/Icons/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(IconRoot)) return;
            var importer = (TextureImporter)assetImporter;
            Apply(importer);
        }

        static void Apply(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (IsButtonPlate(importer.assetPath))
            {
                // 9-slice: corners stay crisp under Image.Type.Sliced.
                //
                // DEFECT FIX. The old (30,14,30,14) border on a 256x106 plate
                // consumed 60 u horizontally and 28 u vertically, but real
                // buttons are as small as 52x44 and 84x28 — so the stat "+"
                // button had a centre of MINUS 8 u (its borders overlapped)
                // and the stage-drop button exactly 0 u tall. 7 of 26 plated
                // buttons rendered crushed. The old comment assumed "buttons
                // are 34-48px tall"; six of them are 28.
                //
                // (12,8,12,8) consumes 24 x 16 and clears every size in use:
                //   stat +      28 x 28     tab          96 x 24
                //   stage drop  60 x 12     text button 120 x 18
                //   skill card  84 x 56
                importer.spriteBorder = new Vector4(12, 8, 12, 8);
            }
        }

        /// <summary>Every 9-sliced button plate: idle, active, disabled. The
        /// three must share a border or a state swap would shift the frame.</summary>
        static bool IsButtonPlate(string path) =>
            path.EndsWith("ui-button.png")
            || path.EndsWith("ui-button-active.png")
            || path.EndsWith("ui-button-disabled.png");

        /// <summary>Idempotent batch entry: -executeMethod ...IconImportPipeline.ImportAll</summary>
        public static void ImportAll()
        {
            var found = 0;
            var changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Icons" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                found += 1;
                // `> 0` was too weak — the crushed (30,14,30,14) border passed
                // it. Require the exact contract so a stale import is caught.
                var borderOk = !IsButtonPlate(path)
                    || importer.spriteBorder == new Vector4(12, 8, 12, 8);
                if (importer.textureType == TextureImporterType.Sprite &&
                    importer.alphaIsTransparency && !importer.mipmapEnabled && borderOk) continue;
                Apply(importer);
                importer.SaveAndReimport();
                changed += 1;
            }
            // changed == 0 is healthy on re-runs (OnPreprocessTexture already ran);
            // found == 0 means matting never delivered - fail the lane loudly.
            if (found == 0)
            {
                Debug.LogError("[IconImport] no icons under " + IconRoot);
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log($"[IconImport] found {found}, normalized {changed}");
            EditorApplication.Exit(0);
        }
    }
}
