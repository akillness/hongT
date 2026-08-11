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

        static void ConfigureImporter(string fbxPath)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            // No materials from the FBX. The kit ships untextured — the source
            // GLB's PBR maps do not survive the FBX hop, and the environment
            // renders by VALUE contrast anyway (§E0.5 bans giving scenery the
            // hazard hue, so an authored stone tint is what the arena wants).
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
                var material = StoneMaterial();
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

        /// <summary>
        /// One shared URP Lit stone material for the whole kit.
        ///
        /// Shared, not per-part: the parts are all the same rock, and the View
        /// re-tints per placement through a MaterialPropertyBlock the way
        /// EnvironmentBuilder.SpawnLibraryPart already does. Twenty near-identical
        /// material assets would just be twenty more things to keep in sync.
        ///
        /// The value is picked against the floor rather than for its own sake:
        /// stone base 0.155 against floorBase 0.235 is the separation the
        /// environment already uses, so kit parts read as the same material
        /// family as the procedural geometry they stand next to.
        /// </summary>
        static Material StoneMaterial()
        {
            const string path = PrefabDir + "/kit-stone.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", new Color(0.155f, 0.145f, 0.165f));
            material.SetFloat("_Smoothness", 0.12f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
