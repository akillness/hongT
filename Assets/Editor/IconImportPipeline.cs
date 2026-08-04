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
            if (importer.assetPath.EndsWith("ui-button.png"))
            {
                // 9-slice: corners stay crisp under Image.Type.Sliced.
                // Buttons are 34-48px tall, so the vertical border must stay
                // small (14+14 < 34) while horizontal can keep the full 30px
                // glow. Vector4 = (left, bottom, right, top) in sprite px.
                importer.spriteBorder = new Vector4(30, 14, 30, 14);
            }
        }

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
                var borderOk = !path.EndsWith("ui-button.png") || importer.spriteBorder.x > 0f;
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
