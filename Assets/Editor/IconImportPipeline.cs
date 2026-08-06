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
            if (TryGetFrameBorder(importer.assetPath, out var border))
            {
                // 9-slice: corners stay crisp under Image.Type.Sliced. Every
                // border below is sized against its REAL on-screen usage
                // height (not the source PNG's own pixel size) so it never
                // repeats the crushed-border defect the button plates hit —
                // see the button-plate history note for that failure mode.
                importer.spriteBorder = border;
            }
        }

        /// <summary>Every 9-sliced button plate: idle, active, disabled. The
        /// three must share a border or a state swap would shift the frame.</summary>
        static bool IsButtonPlate(string path) =>
            path.EndsWith("ui-button.png")
            || path.EndsWith("ui-button-active.png")
            || path.EndsWith("ui-button-disabled.png");

        /// <summary>Every 9-sliced sprite this pipeline knows about, mapped to
        /// its Unity spriteBorder (left, bottom, right, top). Button plates:
        /// (12,8,12,8) clears every plate size in use, 52x44 down to 84x28.
        /// HUD chrome (hud-atlas-source.png, single gti generation, sliced
        /// into 16 tiles by _workspace/current/design/hud-atlas/slice.sh):
        /// each border is well under half of its real HudView.cs usage
        /// height so no corner ever overlaps.
        ///   hp/oil bar frame   used at 284x22 -> border capped ~6 v
        ///   meters panel       used at 300x74 -> border capped ~16 v
        ///   stats panel        used at 240x108 -> border capped ~20 v
        ///   skill card frame   used at 150x88 (108x88 dungeon) -> ~16 v
        ///   boss bar frame     used at 520x46 (outer plate) -> ~10 v
        /// xp-bar / extraction-ring / shield-readout frames are NOT sliced
        /// here: their real usage height (8-14 u) is too small for any
        /// readable border, so HudView.cs only applies their FILL sprite
        /// (Image.Type.Filled has no border math, so no size limit).</summary>
        static bool TryGetFrameBorder(string path, out Vector4 border)
        {
            if (IsButtonPlate(path)) { border = new Vector4(12, 8, 12, 8); return true; }
            if (path.EndsWith("hud-hp-bar-frame.png")
                || path.EndsWith("hud-oil-bar-frame.png")) { border = new Vector4(14, 6, 14, 6); return true; }
            if (path.EndsWith("hud-meters-panel-bg.png")) { border = new Vector4(16, 16, 16, 16); return true; }
            if (path.EndsWith("hud-stats-panel-bg.png")) { border = new Vector4(16, 20, 16, 20); return true; }
            if (path.EndsWith("hud-skill-card-frame.png")
                || path.EndsWith("hud-skill-card-frame-ready.png")) { border = new Vector4(16, 16, 16, 16); return true; }
            if (path.EndsWith("hud-boss-bar-frame.png")) { border = new Vector4(20, 10, 20, 10); return true; }
            border = default;
            return false;
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
                // `> 0` was too weak — the crushed (30,14,30,14) border passed
                // it. Require the exact contract so a stale import is caught.
                var borderOk = !TryGetFrameBorder(path, out var expectedBorder)
                    || importer.spriteBorder == expectedBorder;

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
