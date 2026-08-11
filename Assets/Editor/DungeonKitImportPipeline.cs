// AMENDMENT #17 dungeon kit import: Assets/Art/Environment/kit-*.fbx ->
// Resources/Environment prefabs with URP-safe materials. Meshes come from
// tools/blender/kit_from_glb.py (Higgsfield tripo_3d, normalised so every part
// is 1.0 long on X and sits on y=0). Runs headless:
//   Unity -batchmode -executeMethod CinderCourt.EditorTools.DungeonKitImportPipeline.ImportAll
//
// Mirrors PropImportPipeline deliberately — same importer settings, same
// "assign an explicit URP Lit material rather than trusting FBX import" rule,
// same batch-mode exit contract. A second pipeline that behaved differently
// would be a second thing to remember.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    public static class DungeonKitImportPipeline
    {
        const string SourceDir = "Assets/Art/Environment";
        const string PrefabDir = "Assets/Resources/Environment";

        [MenuItem("CinderCourt/Import Dungeon Kit")]
        public static void ImportAll()
        {
            try
            {
                var sources = Directory.Exists(SourceDir)
                    ? Directory.GetFiles(SourceDir, "kit-*.fbx", SearchOption.TopDirectoryOnly)
                        .Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToArray()
                    : Array.Empty<string>();
                if (sources.Length == 0)
                    throw new InvalidOperationException($"no kit-*.fbx under {SourceDir}");

                Directory.CreateDirectory(PrefabDir);
                ConfigureTextures();
                foreach (var fbxPath in sources)
                {
                    ConfigureImporter(fbxPath);
                    BuildPrefab(fbxPath);
                }
                AssetDatabase.SaveAssets();
                Debug.Log($"[DungeonKitImportPipeline] DONE ({sources.Length} parts)");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError($"[DungeonKitImportPipeline] FAILED: {error}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        /// <summary>
        /// Repaint every kit material as UNLIT MAGENTA so a build renders an exact
        /// per-pixel mask of the kit meshes. Not shippable — a measurement fixture.
        ///
        /// Why a whole build for a mask. The resolution comparison has to average over
        /// kit pixels only; averaging over the frame buries the change under five
        /// million pixels of HUD, floor and sky, and measured, that is exactly what
        /// happened — GATE-0 failed on a frame where the difference is plainly visible.
        /// The tempting shortcut is "kit pixels = pixels where untextured and 1024
        /// differ", but that defines the mask from the quantity being measured and
        /// would make a large D_gain arithmetically certain.
        ///
        /// Unlit matters: a lit material would shade the magenta and an exact-colour
        /// test would miss most of the surface.
        ///
        /// Restoring is not a separate undo path — ImportAll regenerates every material
        /// from the recovered maps, so running it afterwards IS the restore.
        ///   Unity -batchmode -executeMethod CinderCourt.EditorTools.DungeonKitImportPipeline.MaskAll
        /// </summary>
        [MenuItem("CinderCourt/Mask Dungeon Kit (measurement only)")]
        public static void MaskAll()
        {
            try
            {
                var unlit = Shader.Find("Universal Render Pipeline/Unlit");
                var materials = Directory.Exists(PrefabDir)
                    ? Directory.GetFiles(PrefabDir, "kit-*.mat", SearchOption.TopDirectoryOnly)
                        .Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToArray()
                    : Array.Empty<string>();
                if (materials.Length == 0)
                    throw new InvalidOperationException($"no kit materials under {PrefabDir}");

                foreach (var path in materials)
                {
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null) continue;
                    material.shader = unlit;
                    material.SetColor("_BaseColor", Color.magenta);
                    material.SetTexture("_BaseMap", null);
                    EditorUtility.SetDirty(material);
                }
                AssetDatabase.SaveAssets();
                Debug.Log($"[DungeonKitImportPipeline] MASKED ({materials.Length} materials)");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError($"[DungeonKitImportPipeline] MASK FAILED: {error}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        static void ConfigureImporter(string fbxPath)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            // Still no materials FROM THE FBX — but the reason has changed, and so has
            // the outcome. The FBX genuinely carries none (kit_from_glb.py exports with
            // embed_textures=false), yet the source GLBs carry a full set: measured,
            // 20/20 parts have UVs, baseColor and normal maps. The maps were never
            // missing, only stranded, so tools/env/extract_kit_textures.py lifts them
            // into Assets/Art/Environment/Textures and StoneMaterial binds them by name.
            //
            // The old comment concluded "the kit ships untextured … the environment
            // renders by VALUE contrast anyway". The first half was a description of a
            // pipeline gap being read as a decision; the second half is still true and
            // still governs — §E0.5 bans giving scenery the hazard hue, so the recovered
            // albedo is used for GRAIN and the stage identity stays in the tint.
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        static void BuildPrefab(string fbxPath)
        {
            var name = Path.GetFileNameWithoutExtension(fbxPath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null)
                throw new InvalidOperationException($"import produced no GameObject: {fbxPath}");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                var material = StoneMaterial(name);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    // Every env renderer in this project is shadow-free: the
                    // dungeon lights are LightShadows.None, so a caster costs
                    // draw calls and returns nothing.
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    var shared = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                    for (var i = 0; i < shared.Length; i++) shared[i] = material;
                    renderer.sharedMaterials = shared;
                }
                var prefabPath = $"{PrefabDir}/{name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath, out var ok);
                if (!ok) throw new InvalidOperationException($"prefab save failed: {prefabPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        const string TextureDir = "Assets/Art/Environment/Textures";

        /// <summary>
        /// Import settings for the recovered maps, applied BEFORE any material binds
        /// them — a texture read at its default settings is the wrong texture.
        ///
        /// Two settings are not optional:
        ///   * a normal map imported as a colour texture is sampled as RGB and lights
        ///     the surface with garbage; TextureImporterType.NormalMap is what makes it
        ///     a normal;
        ///   * maxTextureSize defaults to 2048, which is over this project's WebGL
        ///     ceiling of 1024. That default has already broken a gate here once, when
        ///     two new FX sheets imported at 2048 and
        ///     TextureImporters_DefaultAndWebGlCapsDoNotExceed1024 caught them.
        ///
        /// sRGB follows the same split: albedo is colour and must be sRGB, a normal map
        /// is a vector field and must not be.
        /// </summary>
        static void ConfigureTextures()
        {
            if (!Directory.Exists(TextureDir)) return;

            foreach (var path in Directory.GetFiles(TextureDir, "*.png", SearchOption.TopDirectoryOnly)
                         .Select(p => p.Replace('\\', '/')).OrderBy(p => p))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                var isNormal = path.EndsWith("-normal.png", StringComparison.Ordinal);
                var wantType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                var wantSrgb = !isNormal;

                var webgl = importer.GetPlatformTextureSettings("WebGL");
                if (importer.textureType == wantType
                    && importer.sRGBTexture == wantSrgb
                    && importer.maxTextureSize <= MaxTextureSize
                    && webgl.overridden && webgl.maxTextureSize <= MaxTextureSize
                    && importer.mipmapEnabled)
                {
                    continue;   // already correct — reimporting 40 textures is not free
                }

                importer.textureType = wantType;
                importer.sRGBTexture = wantSrgb;
                importer.maxTextureSize = MaxTextureSize;
                importer.mipmapEnabled = true;          // iso camera: parts render small
                importer.textureCompression = TextureImporterCompression.Compressed;

                // THE DEFAULT PLATFORM IS NOT THE BUILD PLATFORM. Setting
                // importer.maxTextureSize writes DefaultTexturePlatform only, and a
                // per-platform override outranks it — measured here: the default read
                // 1024 while the WebGL block still read 2048, which is the size that
                // would actually ship. The project's own gate
                // (TextureImporters_DefaultAndWebGlCapsDoNotExceed1024) exists because
                // this has bitten before, and it checks BOTH numbers for this reason.
                webgl.overridden = true;
                webgl.maxTextureSize = MaxTextureSize;
                webgl.format = TextureImporterFormat.Automatic;
                importer.SetPlatformTextureSettings(webgl);

                importer.SaveAndReimport();
            }
        }

        const int MaxTextureSize = 1024;

        /// <summary>
        /// The URP Lit stone material for one kit part.
        ///
        /// PER-PART NOW, not shared, and the reason is the maps. Each part carries its
        /// OWN unwrap and its own baked albedo/normal (recovered by
        /// tools/env/extract_kit_textures.py), so one shared material could only ever
        /// carry one part's maps. Sharing was right while every part was flat; it stops
        /// being right the moment the surfaces differ.
        ///
        /// What did NOT change is where stage identity lives: the View still re-tints
        /// per placement through a MaterialPropertyBlock, exactly as
        /// EnvironmentBuilder.SpawnLibraryPart does. Twenty materials is the cost of
        /// twenty unwraps; nine stages still cost zero extra materials.
        ///
        /// When a part has no maps — the eight ungenerated kit parts, or a run before
        /// extraction — the material falls back to the flat stone value. That value is
        /// picked against the floor rather than for its own sake: stone base 0.155
        /// against floorBase 0.235 is the separation the environment already uses.
        ///
        /// Smoothness stays a CONSTANT and no metallicRoughness map is bound. The kit is
        /// stone: metallic is 0 everywhere and a roughness map would spend a third of the
        /// texture budget to vary a quantity this material barely uses. Budget is the
        /// binding constraint here (build data 69.1 MB against a 120 MB ceiling), so the
        /// map that buys the least is the one that is not imported.
        /// </summary>
        static Material StoneMaterial(string partName)
        {
            var path = $"{PrefabDir}/{partName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            var albedo = LoadMap(partName, "albedo");
            var normal = LoadMap(partName, "normal");

            // A textured part shows its own albedo, so the base colour must be WHITE —
            // multiplying the map by the flat stone value would darken every recovered
            // texture by the same amount the flat look already had, and the recovery
            // would read as "slightly less flat" instead of as stone.
            material.SetColor("_BaseColor", albedo != null
                ? Color.white
                : new Color(0.155f, 0.145f, 0.165f));
            material.SetTexture("_BaseMap", albedo);
            material.SetFloat("_Smoothness", 0.12f);
            material.SetFloat("_Metallic", 0f);

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.DisableKeyword("_NORMALMAP");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// One recovered map, or null when the part has none. Null is a NORMAL state:
        /// the kit shipped 20 of 28 parts, so absence is the ungenerated remainder, not
        /// an error, and nothing here may hard-depend on a map existing.
        /// </summary>
        static Texture2D LoadMap(string partName, string suffix)
        {
            var path = $"{TextureDir}/{partName}-{suffix}.png";
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
