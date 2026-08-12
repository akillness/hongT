// Stage terrain import: Assets/Art/Terrain/*.fbx (from tools/blender/
// convert_terrain.py) -> extracted textures -> CinderCourt/ToonLit materials ->
// Resources/Terrain/<stage>.prefab for runtime Resources.Load.
//
// HISTORY: these were URP/Unlit until 2026-08-12 — the terrain GLBs are painted
// isometric plates (baked lighting in the albedo), and under the PBR art
// direction a lit shader would have double-lit them. The 시안 02 toon direction
// inverted that call: an unlit photographic plate sitting beside cel-banded kit
// geometry is exactly the "이미지가 덧씌워진 느낌" the 2026-08-12 playtest
// reported, and VfxDirector's hazard bodies already made this same move for the
// same reason (an unlit override "silently undid the toon conversion" — see
// VfxDirector kit-body material comment). ToonLit's flat band multiplies the
// baked albedo by a CONSTANT per light band — posterization, not a second
// lighting gradient — so the painted detail survives while the plate joins the
// stage mood. Fallback stays Unlit: a stripped toon shader must degrade to the
// shipped look, never to magenta.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    public static class TerrainImportPipeline
    {
        const string ArtDir = "Assets/Art/Terrain";
        const string ResourceDir = "Assets/Resources/Terrain";

        public static void ImportAll()
        {
            try
            {
                var count = Run();
                Debug.Log($"[TerrainImport] imported {count} stage terrains");
                EditorApplication.Exit(count > 0 ? 0 : 1);
            }
            catch (Exception error)
            {
                Debug.LogError($"[TerrainImport] FATAL {error}");
                EditorApplication.Exit(1);
            }
        }

        static int Run()
        {
            if (!Directory.Exists(ArtDir)) throw new InvalidOperationException($"{ArtDir} missing");
            Directory.CreateDirectory(ResourceDir);
            // Toon first (시안 02), Unlit as the degrade path — mirrors
            // ViewWorld.LitShader and DungeonKitImportPipeline.StoneShader.
            var unlit = Shader.Find("CinderCourt/ToonLit")
                ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) throw new InvalidOperationException("no terrain shader available");

            var count = 0;
            foreach (var path in Directory.GetFiles(ArtDir, "*.fbx").Select(p => p.Replace('\\', '/')))
            {
                var id = Path.GetFileNameWithoutExtension(path);
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                    throw new InvalidOperationException($"no ModelImporter for {path}");

                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                var textureDir = $"{ArtDir}/{id}-textures";
                Directory.CreateDirectory(textureDir);
                importer.ExtractTextures(textureDir);
                // ExtractTextures writes during the NEXT refresh; repair must
                // run after it or it walks an empty dir and renames nothing.
                AssetDatabase.Refresh();
                RepairTextureExtensions(textureDir);
                importer.SaveAndReimport();

                RemapToUnlit(importer, path, textureDir, unlit, LoadManifest(path));

                // Runtime handle: a thin prefab that references the model root.
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) throw new InvalidOperationException($"model load failed: {path}");
                var prefabPath = $"{ResourceDir}/{id}.prefab";
                AssetDatabase.DeleteAsset(prefabPath);
                var instance = (GameObject)UnityEngine.Object.Instantiate(model);
                instance.name = id;
                UnityEditor.PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                UnityEngine.Object.DestroyImmediate(instance);
                count += 1;
                Debug.Log($"[TerrainImport] {id} -> {prefabPath}");
            }
            AssetDatabase.SaveAssets();
            return count;
        }

        /// <summary>Same magic-byte repair as CharacterImportPipeline.</summary>
        static void RepairTextureExtensions(string textureDir)
        {
            if (!Directory.Exists(textureDir)) return;
            var renamed = false;
            foreach (var file in Directory.GetFiles(textureDir))
            {
                if (file.EndsWith(".meta") || Path.HasExtension(file)) continue;
                var head = new byte[4];
                using (var stream = File.OpenRead(file)) stream.Read(head, 0, 4);
                string extension = null;
                if (head[0] == 0x89 && head[1] == 0x50) extension = ".png";
                else if (head[0] == 0xFF && head[1] == 0xD8) extension = ".jpg";
                if (extension == null) continue;
                File.Delete(file + ".meta");
                if (File.Exists(file + extension)) File.Delete(file + extension);
                File.Move(file, file + extension);
                renamed = true;
            }
            if (renamed) AssetDatabase.Refresh();
        }

        /// <summary>Sidecar from convert_terrain.py: material name -> image name.</summary>
        static Dictionary<string, string> LoadManifest(string fbxPath)
        {
            var manifestPath = fbxPath.Substring(0, fbxPath.Length - 4) + ".albedo.json";
            var map = new Dictionary<string, string>();
            if (!File.Exists(manifestPath)) return map;
            // Flat {"mat":"image"} object - parse without a JSON dependency.
            var text = File.ReadAllText(manifestPath);
            foreach (System.Text.RegularExpressions.Match pair in
                     System.Text.RegularExpressions.Regex.Matches(text, "\"([^\"]+)\"\\s*:\\s*\"([^\"]+)\""))
                map[pair.Groups[1].Value] = pair.Groups[2].Value;
            return map;
        }

        static void RemapToUnlit(ModelImporter importer, string path, string textureDir,
                                 Shader unlit, Dictionary<string, string> manifest)
        {
            var textures = Directory.Exists(textureDir)
                ? Directory.GetFiles(textureDir).Where(p => !p.EndsWith(".meta"))
                    .Select(p => p.Replace('\\', '/')).ToList()
                : new List<string>();
            // ExtractTextures materializes during SaveAndReimport, which can be
            // AFTER the earlier Refresh - force-register so LoadAssetAtPath
            // works on the first pipeline pass, not the second.
            foreach (var texture in textures)
                AssetDatabase.ImportAsset(texture, ImportAssetOptions.ForceUpdate);
            var materialDir = $"{Path.GetDirectoryName(path)!.Replace('\\', '/')}/Materials";
            Directory.CreateDirectory(materialDir);

            // Stale external remaps (targets deleted between runs) suppress the
            // embedded material sub-assets entirely - clear them first, or
            // LoadAllAssetsAtPath returns zero materials and the loop no-ops.
            var stale = importer.GetExternalObjectMap()
                .Where(p => p.Key.type == typeof(Material) && p.Value == null)
                .Select(p => p.Key).ToList();
            if (stale.Count > 0)
            {
                foreach (var identifier in stale) importer.RemoveRemap(identifier);
                importer.SaveAndReimport();
            }

            // Materials already remapped in a previous run no longer show up as
            // embedded sub-assets - iterate the UNION of embedded + remapped
            // names so re-runs repair earlier mistakes instead of skipping.
            var names = new HashSet<string>(
                AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Select(m => m.name));
            foreach (var pair in importer.GetExternalObjectMap())
                if (pair.Key.type == typeof(Material)) names.Add(pair.Key.name);

            var changed = false;
            foreach (var name in names)
            {
                var replacement = new Material(unlit) { name = name };
                // Resolution order: manifest (deterministic, written at
                // conversion) -> diffuse-name guess. Sanitized comparison on
                // both sides - material names have spaces, files have dashes.
                Texture2D albedo = null;
                if (manifest.TryGetValue(name, out var imageName))
                {
                    var hit = textures.FirstOrDefault(t =>
                        Path.GetFileNameWithoutExtension(t) == imageName);
                    if (hit != null) albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(hit);
                }
                if (albedo == null && textures.Count > 0)
                {
                    var wanted = Sanitize(name).ToLowerInvariant();
                    var candidates = textures.Where(t =>
                    {
                        var lower = t.ToLowerInvariant();
                        return lower.Contains("diffuse") || lower.Contains("basecolor")
                            || lower.Contains("albedo") || lower.Contains("shaded");
                    }).ToList();
                    var guess = candidates.FirstOrDefault(t =>
                        Sanitize(Path.GetFileNameWithoutExtension(t)).ToLowerInvariant().Contains(wanted))
                        ?? candidates.FirstOrDefault();
                    if (guess != null)
                    {
                        albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(guess);
                        Debug.LogWarning($"[TerrainImport] {name}: albedo guessed from {guess}");
                    }
                }
                if (albedo != null) replacement.SetTexture("_BaseMap", albedo);
                else Debug.LogWarning($"[TerrainImport] {name}: NO albedo resolved");
                var materialPath = $"{materialDir}/{Sanitize(path)}-{Sanitize(name)}.mat";
                AssetDatabase.DeleteAsset(materialPath);
                AssetDatabase.CreateAsset(replacement, materialPath);
                importer.AddRemap(
                    new AssetImporter.SourceAssetIdentifier(typeof(Material), name),
                    AssetDatabase.LoadAssetAtPath<Material>(materialPath));
                changed = true;
            }
            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        static string Sanitize(string name) =>
            string.Concat(name.Select(c => char.IsLetterOrDigit(c) ? c : '-'));
    }
}
