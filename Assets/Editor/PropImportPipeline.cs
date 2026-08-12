// Equip prop import (spec §Lane P): Assets/Art/Props/equip-*.fbx ->
// Resources/Props prefabs with URP-safe materials. Meshes come from
// tools/blender/convert_equip_props.py (retained blade/relic + authored
// cloak). Runs headless:
//   Unity -batchmode -executeMethod CinderCourt.EditorTools.PropImportPipeline.ImportAll
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    public static class PropImportPipeline
    {
        const string SourceDir = "Assets/Art/Props";
        const string PrefabDir = "Assets/Resources/Props";

        [MenuItem("CinderCourt/Import Equip Props")]
        public static void ImportAll()
        {
            try
            {
                var sources = Directory.Exists(SourceDir)
                    ? Directory.GetFiles(SourceDir, "equip-*.fbx", SearchOption.TopDirectoryOnly)
                        .Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToArray()
                    : Array.Empty<string>();
                if (sources.Length == 0)
                    throw new InvalidOperationException($"no equip-*.fbx under {SourceDir}");

                Directory.CreateDirectory(PrefabDir);
                foreach (var fbxPath in sources)
                {
                    ConfigureImporter(fbxPath);
                    BuildPrefab(fbxPath);
                }
                AssetDatabase.SaveAssets();
                Debug.Log($"[PropImportPipeline] DONE ({sources.Length} props)");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError($"[PropImportPipeline] FAILED: {error}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        static void ConfigureImporter(string fbxPath)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbxPath);
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
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
                // FBX Principled import DROPS emission and lands near-black on
                // the dark court floor — assign explicit URP Lit materials per
                // slot/band instead (serialized assets: variants survive WebGL
                // shader stripping because real material references exist).
                var material = BandMaterial(name);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    var shared = new Material[renderer.sharedMaterials.Length];
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

        /// <summary>Serialized URP Lit material per prop asset — readable body
        /// color + band-coded emission (basic: faint, fine: strong signature).
        ///
        /// DELIBERATELY NOT CinderCourt/ToonLit (2026-08-12 toon sweep): the
        /// fine/basic band difference IS the emission color, and ToonLit has no
        /// emission term at all (no _EmissionColor property, no additive slot in
        /// its fragment). Swapping these to toon would flatten every fine-band
        /// glow to charcoal and erase the rank readout. If a future cycle wants
        /// toon props, it must first add an emission term to the shader — a
        /// shader edit, not a material swap.</summary>
        static Material BandMaterial(string propName)
        {
            var path = $"{PrefabDir}/{propName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            var fine = propName.EndsWith("-fine");
            Color body, glow;
            if (propName.Contains("-weapon-"))
            {
                body = new Color(0.42f, 0.40f, 0.46f);              // readable steel
                glow = new Color(0.95f, 0.35f, 0.17f) * (fine ? 1.6f : 0.25f);
            }
            else if (propName.Contains("-lantern-"))
            {
                body = new Color(0.45f, 0.36f, 0.22f);              // brass cage
                glow = new Color(0.17f, 0.68f, 0.84f) * (fine ? 1.8f : 0.45f);
            }
            else
            {
                body = fine
                    ? new Color(0.38f, 0.10f, 0.10f)                // verdict crimson
                    : new Color(0.16f, 0.14f, 0.19f);               // charcoal mantle
                glow = new Color(0.95f, 0.35f, 0.17f) * (fine ? 0.5f : 0f);
            }
            material.color = body;
            material.SetFloat("_Metallic", propName.Contains("-weapon-") ? 0.6f : 0.1f);
            material.SetFloat("_Smoothness", 0.45f);
            if (glow.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", glow);
            }
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
