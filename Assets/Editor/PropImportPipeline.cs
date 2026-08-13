// Equip prop import (spec §Lane P): Assets/Art/Props/equip-*.fbx ->
// Resources/Props prefabs with URP-safe materials. Meshes come from
// tools/blender/convert_equip_props.py (retained blade/relic + authored
// cloak). Runs headless:
//   Unity -batchmode -executeMethod CinderCourt.EditorTools.PropImportPipeline.ImportAll
using System;
using System.Collections.Generic;
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
                // the dark court floor — assign explicit materials per slot/band
                // instead (serialized assets: variants survive WebGL shader
                // stripping because real material references exist).
                //
                // MEASURE FIRST, then build: the outline width is derived from
                // this mesh, and the material has to be complete BEFORE it is
                // copied onto the asset (see BandMaterial) — a later SetFloat
                // would land after the copy that is supposed to define the file.
                var renderers = instance.GetComponentsInChildren<Renderer>();
                var thinnest = float.MaxValue;
                foreach (var renderer in renderers)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    thinnest = Mathf.Min(thinnest, DrivingThickness(filter.sharedMesh, name));
                }
                var material = BandMaterial(name, thinnest);
                foreach (var renderer in renderers)
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

        /// <summary>How much of a prop's surface may be sacrificed to black so
        /// the rest can carry a house-weight outline. A lantern IS a cage of
        /// thin bars and a bow HAS a string; demanding every wire survive pins
        /// the whole prop to a hairline outline to protect geometry that reads
        /// as a line either way.</summary>
        const float SacrificeableArea = 0.20f;

        /// <summary>
        /// The thickness that should drive the outline width, in object space:
        /// the thinnest sub-part such that everything thinner than it accounts
        /// for no more than <see cref="SacrificeableArea"/> of the prop's
        /// surface.
        ///
        /// NOT the mesh AABB — these props are joined primitives, so the merged
        /// box's short axis is set by the FATTEST part on that axis while the
        /// parts that actually go solid black are far thinner. Connected
        /// components recover the original parts (the join never welded them).
        ///
        /// NOT a fixed hairline cutoff either, and that correction is the point
        /// of this shape. A first pass skipped parts under 0.008 and reported
        /// 0.0081 for the lantern and 0.0081 for the legacy weapon — both
        /// landing one ten-thousandth above the cutoff, which is the signature
        /// of a threshold choosing the answer rather than measuring it. Their
        /// part thicknesses form a continuum, so any hard line lands mid-
        /// distribution and the number it yields is an artefact of where the
        /// line was drawn. Area is the thing actually at stake, so ask the
        /// question in area: how thick must a part be before losing it costs
        /// more than a fifth of what the player sees?
        ///
        /// Welding by POSITION first is required, not tidiness: smart_project
        /// cuts UV seams, and a seam splits the vertex. Walking raw indices
        /// would shatter one part into several and under-report its size.
        /// </summary>
        static float DrivingThickness(Mesh mesh, string propName)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            // isReadable=false is set on the importer, but an AssetDatabase-
            // loaded mesh still reads editor-side. Assert rather than trust it:
            // an empty array would flow into the fallback below and hand every
            // prop the same default with no error anywhere.
            if (vertices.Length == 0 || triangles.Length == 0)
                throw new InvalidOperationException(
                    $"{propName}: mesh not readable editor-side ({vertices.Length} verts, "
                    + $"{triangles.Length} indices) — outline width cannot be measured");

            var weld = new Dictionary<Vector3Int, int>(vertices.Length);
            var canonical = new int[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                var key = new Vector3Int(
                    Mathf.RoundToInt(vertices[i].x * 100000f),
                    Mathf.RoundToInt(vertices[i].y * 100000f),
                    Mathf.RoundToInt(vertices[i].z * 100000f));
                if (!weld.TryGetValue(key, out var first)) weld[key] = first = i;
                canonical[i] = first;
            }

            var parent = new int[vertices.Length];
            for (var i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int x)
            {
                while (parent[x] != x) x = parent[x] = parent[parent[x]];
                return x;
            }
            void Union(int a, int b)
            {
                int ra = Find(a), rb = Find(b);
                if (ra != rb) parent[ra] = rb;
            }
            for (var i = 0; i < vertices.Length; i++) Union(i, canonical[i]);
            for (var t = 0; t < triangles.Length; t += 3)
            {
                Union(triangles[t], triangles[t + 1]);
                Union(triangles[t + 1], triangles[t + 2]);
            }

            var lo = new Dictionary<int, Vector3>();
            var hi = new Dictionary<int, Vector3>();
            for (var i = 0; i < vertices.Length; i++)
            {
                var root = Find(i);
                var v = vertices[i];
                if (!lo.ContainsKey(root)) { lo[root] = v; hi[root] = v; continue; }
                lo[root] = Vector3.Min(lo[root], v);
                hi[root] = Vector3.Max(hi[root], v);
            }

            // Area AND signed volume per component, in one sweep. Volume is the
            // divergence-theorem sum dot(a, cross(b,c))/6 — valid because every
            // part here is a closed primitive or a solidified shell.
            var area = new Dictionary<int, float>();
            var volume = new Dictionary<int, float>();
            var total = 0f;
            for (var t = 0; t < triangles.Length; t += 3)
            {
                var a = vertices[triangles[t]];
                var b = vertices[triangles[t + 1]];
                var c = vertices[triangles[t + 2]];
                var face = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                var root = Find(triangles[t]);
                area.TryGetValue(root, out var runningArea);
                area[root] = runningArea + face;
                volume.TryGetValue(root, out var runningVolume);
                volume[root] = runningVolume + Vector3.Dot(a, Vector3.Cross(b, c)) / 6f;
                total += face;
            }
            if (total <= 0f)
                throw new InvalidOperationException($"{propName}: zero surface area");

            var parts = lo.Keys
                .Select(root =>
                {
                    var size = hi[root] - lo[root];
                    var box = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
                    var a = area.TryGetValue(root, out var partArea) ? partArea : 0f;
                    var v = volume.TryGetValue(root, out var partVolume)
                        ? Mathf.Abs(partVolume) : 0f;
                    // Characteristic thickness 2V/A: a solid cylinder of radius
                    // r gives r, a slab of thickness t gives t, and a CLOSED
                    // SHELL of wall t gives t — which the bounding box cannot
                    // do. The cloak is exactly that shell (a solidify modifier
                    // at 0.012 over a curved grid), and its box short axis is
                    // the 0.0565 bow of the mantle, 4.7x its actual cloth. An
                    // open or degenerate part yields no usable volume, so the
                    // box is the fallback, and 2V/A never exceeds it for a
                    // closed body so the min is safe either way.
                    var characteristic = a > 0f && v > 0f ? 2f * v / a : box;
                    return new { Thickness = Mathf.Min(characteristic, box), Area = a };
                })
                .OrderBy(p => p.Thickness)
                .ToArray();

            var spent = 0f;
            var driving = parts[parts.Length - 1].Thickness;
            var sacrificed = 0;
            foreach (var part in parts)
            {
                if (spent + part.Area > total * SacrificeableArea)
                {
                    driving = part.Thickness;
                    break;
                }
                spent += part.Area;
                sacrificed++;
            }
            Debug.Log($"[PropImportPipeline] {propName}: {parts.Length} parts, "
                + $"sacrificing {sacrificed} ({spent / total:P0} area), "
                + $"driving thickness {driving:F4}");
            return driving;
        }

        /// <summary>Outline width = 30% of the part thickness that DrivingThickness
        /// returned, capped at the house weight.
        ///
        /// The hull grows a part of thickness t to t + 2*width, so the part keeps
        /// t/(t+2w) of its own silhouette. At w = 0.3t that is 62% — a body with
        /// an edge. The house 0.018 on the dagger's measured 0.0119 leaves 25%,
        /// i.e. three quarters of the blade is outline: the solid-black-stick
        /// failure this function exists to prevent.
        ///
        /// THE HOUSE CAP IS DEAD CODE TODAY, and saying otherwise would be the
        /// claim-without-measurement this repo forbids. 0.018 is the shader
        /// default and all 20 non-prop ToonLit materials carry exactly it; the 12
        /// props land 0.0011-0.0081, so the geometry term wins every single time
        /// and Mathf.Min never binds. It stays as a ceiling for a future prop fat
        /// enough to need one — not as a behaviour anything currently exercises.
        ///
        /// The consequence is that props ARE under-outlined next to the rest of
        /// the cast: 2.2x thinner at best, 16.5x at worst. That is forced, not
        /// chosen — these meshes are thin, and any width that reads at house
        /// weight swallows them.
        ///
        /// WHAT THIS RULE DOES NOT CHECK: whether the resulting line is VISIBLE.
        /// It optimises the part keeping its silhouette and is silent about the
        /// other side. Measured on the isolated renders: the cloak's boundary
        /// ring is as dark as the hammer's (0.035 vs 0.037) at the same ~3 px, so
        /// the line IS drawn — what differs is contrast against the body (cloak
        /// interior 0.096 vs hammer 0.290). _OutlineColor is never set here, so
        /// every prop carries the shader default while the interior behind it
        /// varies 3x. No width change fixes that; the lever is contrast.
        ///
        /// UNGATED: no test asserts _OutlineWidth. A mesh re-export moves all 12
        /// values with nothing to notice.</summary>
        static float OutlineWidthFor(float thinnestPart, string propName)
        {
            const float HouseWidth = 0.018f;
            var width = float.IsInfinity(thinnestPart) || thinnestPart <= 0f
                ? HouseWidth
                : Mathf.Min(HouseWidth, thinnestPart * 0.3f);
            Debug.Log($"[PropImportPipeline] {propName}: outline {width:F4} "
                + $"(house {HouseWidth:F3})");
            return width;
        }

        /// <summary>Mean luminance every prop sheet is normalised to by
        /// tools/qa/normalize_prop_sheets.py. This constant is one half of a
        /// two-file contract: the script guarantees the sheets average this, and
        /// BandMaterial divides its tints by it, so the shader's sheet x tint
        /// multiply lands back on the intended colour. Change one and the other
        /// must move — `normalize_prop_sheets.py --check` is the assertion, and
        /// gen_prop_textures.sh runs it so a fresh sheet cannot skip it.
        ///
        /// 0.50, not higher: the script lifts a sheet to target with a gamma,
        /// and a bigger lift needs a harsher gamma, which crushes the shadow end
        /// where this linework lives. At 0.75 the cloth weave came back as 5
        /// usable levels of 255 — the flat tint it was supposed to replace. The
        /// floor is headroom: the largest tint component is 0.46, and dividing
        /// by anything under ~0.5 pushes it past 1 and clips before lighting.
        /// So 0.50 is the mildest gamma the multiply can afford.</summary>
        const float SheetMeanLuminance = 0.50f;

        /// <summary>Serialized CinderToonLit material per prop asset — textured
        /// body + band-coded emission (basic: faint, fine: strong signature).
        ///
        /// TOON SINCE 2026-08-13. The earlier sweep excluded these because the
        /// fine/basic difference IS the emission colour and ToonLit had no
        /// emission term; that exclusion was closed by adding one to the shader
        /// (added AFTER the band clamp, so a rank glow is not quantised into
        /// the same two steps as the key light). Props were the last lit family
        /// still speaking PBR next to a cel-shaded cast and floor.
        ///
        /// BUILT FRESH, THEN COPIED OVER THE ASSET. The obvious shape — load the
        /// .mat, swap its shader, set the new properties — leaves the URP/Lit
        /// past serialized underneath: these twelve files were authored as Lit,
        /// and a shader swap does not remove a property block, so _Metallic 0.6
        /// and _Smoothness 0.45 stay in the file forever. Harmless at runtime
        /// (CinderToonLit never reads them) and exactly the "reads as live
        /// tuning" trap this pipeline warns about one function down. Building a
        /// clean Material and CopySerialized-ing it onto the existing asset
        /// defines the file by what the shader actually uses, while keeping the
        /// asset's GUID so every prefab reference survives.
        ///
        /// Two further things are load-bearing, both silent no-ops in this
        /// repo's history:
        ///   * the shader is assigned unconditionally (here, by construction).
        ///     All twelve .mat files already exist, so an assignment inside a
        ///     create-only branch would load them, skip, and log DONE with every
        ///     prop still on URP/Lit — the same "generated materials keep the
        ///     shader they were born with" trap the kit conversion hit.
        ///   * the tint goes through SetColor("_BaseColor"), not
        ///     `material.color`. That accessor resolves _Color or a
        ///     [MainColor]-tagged property, and CinderToonLit tags neither, so
        ///     it would silently leave every prop at white.
        /// </summary>
        static Material BandMaterial(string propName, float thinnestPart)
        {
            var built = new Material(PropShader()) { name = propName };
            // Emission is MODULATED by albedo in the shader, not added flat, so
            // these multipliers are scaled for that: what the player sees is
            // glow * albedo, and albedo here runs ~0.2-0.7.
            //
            // THESE NUMBERS ARE FIXED BY THE IN-GAME FRAME, NOT THE PROBE.
            // The original 1.6 fine clipped red at every texel of the sheet's
            // range — a fine hammer rendered as a flat salmon slab with no iron
            // in it, a pixel indistinguishable from the flat tint the texture
            // replaced. 0.55 was verified in a dungeon capture: no clipped
            // pixels, hammer reads as metal, bands visibly different.
            //
            // PropRenderProbe reports p95 luminance ~0.40 with no clipping, and
            // that headroom is NOT free to spend. Its rig is one directional
            // light at 1.4 against a magenta clear — no ambient, no StageMood
            // key, no emissive lava floor. The dungeon is a far hotter frame,
            // and it is where the clipping was actually observed. The probe is
            // valid for comparing props to EACH OTHER under identical light
            // (that is how the 4.4x warmth separation and the lantern's inverted
            // sign were found); it is not a light meter for the game.
            Color body, glow;
            var fine = propName.EndsWith("-fine");
            if (propName.Contains("-weapon-"))
            {
                body = new Color(0.42f, 0.40f, 0.46f);              // readable steel
                glow = new Color(0.95f, 0.35f, 0.17f) * (fine ? 0.55f : 0.10f);
            }
            else if (propName.Contains("-lantern-"))
            {
                // The lantern is the one prop that IS a light source, so it
                // carries the highest glow — but still short of clipping, or the
                // brass cage stops being brass. Its glow is CYAN, so its rank
                // reads as a COOL shift (B-R up), the opposite direction from
                // every other prop; the probe's warmth column goes DOWN with band
                // here and that is correct, not a regression.
                body = new Color(0.45f, 0.36f, 0.22f);              // brass cage
                glow = new Color(0.17f, 0.68f, 0.84f) * (fine ? 0.70f : 0.18f);
            }
            else
            {
                // The cloak already separates by BODY colour (charcoal -> verdict
                // crimson), so it needs the least glow of the three: measured
                // warmth 0.014 basic vs 0.215 fine, a 15x gap, the widest here.
                body = fine
                    ? new Color(0.38f, 0.10f, 0.10f)                // verdict crimson
                    : new Color(0.16f, 0.14f, 0.19f);               // charcoal mantle
                glow = new Color(0.95f, 0.35f, 0.17f) * (fine ? 0.22f : 0f);
            }
            // Both colours go in RGB-only with alpha pinned at 1. Color's
            // operator* and operator/ scale alpha along with the channels, so
            // the scaled glow was serializing a: 0.25 and the divided tint
            // a: 2 — inert, because CinderToonLit reads .rgb from both and its
            // lit pass returns a hard 1.0, but a value in the file that nothing
            // means is the "reads as live tuning" trap this pipeline already
            // warns about for _Metallic.
            //
            // The DIVIDE itself is load-bearing: the shader multiplies sheet by
            // tint (albedo = SAMPLE(_BaseMap) * _BaseColor), so an un-divided
            // tint lands darker than the colour named above — measured at 0.16x
            // for cloth and 0.17x for iron, and equip-cloak-basic's emission is
            // exactly 0, so it had nothing to add the light back and rendered as
            // a pure black silhouette. That is the same near-black failure this
            // pipeline exists to fix, arriving through the texture channel.
            //
            // PRECISION CAVEAT (audited 2026-08-13): this compensation is exact
            // in GAMMA space, and the project renders LINEAR
            // (m_ActiveColorSpace 1). Colour properties are gamma->linear
            // converted while _BaseMap is sRGB-sampled, so the realised product
            // lands 0.78x-1.20x off the named tint per channel (worst measured:
            // lantern R 0.45 -> 0.491, cloak-fine B 0.10 -> 0.085). It does NOT
            // clip — the largest compensated component is 0.92 — and it does not
            // reintroduce near-black, so this is a precision note, not a defect.
            // Do not read "lands back on the intended tint" as exact.
            built.SetColor("_BaseColor", new Color(
                body.r / SheetMeanLuminance,
                body.g / SheetMeanLuminance,
                body.b / SheetMeanLuminance,
                1f));
            built.SetColor("_EmissionColor", new Color(glow.r, glow.g, glow.b, 1f));
            // `new Material(shader)` defaults globalIlluminationFlags to
            // EmissiveIsBlack, and BandMaterial never overrode it — so all
            // twelve props declared "my emission is black" while eleven of them
            // carry a non-black one. Inert today (no baked GI in this project:
            // zero LightingData assets), and a false declaration that only
            // becomes wrong later is exactly the kind that ships. Realtime
            // rather than BakedEmissive: these props move with the character.
            // Keyed on the glow ALONE, never on the band. `fine ||` would have
            // been dead today (every fine multiplier is non-zero) and actively
            // wrong later: a future tune setting a fine multiplier to 0 would
            // then declare RealtimeEmissive over a black emission — the same
            // false declaration this line exists to remove, inverted.
            built.globalIlluminationFlags = glow.maxColorComponent > 0f
                ? MaterialGlobalIlluminationFlags.RealtimeEmissive
                : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            built.SetFloat("_OutlineWidth", OutlineWidthFor(thinnestPart, propName));
            BindPropTexture(built, propName);

            var path = $"{PrefabDir}/{propName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(built, path);
                return built;
            }
            EditorUtility.CopySerialized(built, existing);
            UnityEngine.Object.DestroyImmediate(built);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>Toon first, URP/Lit as the degrade path — same contract as
        /// ViewWorld.LitShader and the terrain/character pipelines. A stripped
        /// or broken toon shader must cost the STYLE, never the prop.</summary>
        static Shader PropShader()
            => Shader.Find(CinderCourt.View.ViewWorld.ToonLitShaderName)
               ?? Shader.Find("Universal Render Pipeline/Lit");

        /// <summary>
        /// Tileable material sheet per prop family (tools/gen_prop_textures.sh).
        /// FIVE sheets for twelve props because the props are five materials;
        /// band is tint + emission, never a different texture.
        ///
        /// ONE TILE, AND THAT IS A MEASURED CLAIM. A tiling sheet only reads if
        /// texel density is roughly uniform across the mesh; a primitive's
        /// default unwrap maps every part to the full 0..1 square, so a long
        /// blade and a thin guard take the same sheet area over very different
        /// world areas and one repeat smears on one part while dissolving on
        /// the other. tools/blender/probe_prop_uv_density.py measures exactly
        /// that ratio (p95/p05 of uv_area/world_area per triangle):
        ///
        ///   before  dagger 1.41  bow 2.00  hammer 1.08  cloak 1.33
        ///           lantern 6.66  legacy weapon 8.75      <- smear
        ///   after   every one of the twelve between 1.08 and 2.00
        ///
        /// The two smearing families were NOT re-authored from source:
        /// convert_equip_props.py needs blade.glb / relic.glb, retained
        /// Abyssal-Surge assets absent from this repo. That looked like "these
        /// props cannot be fixed" and it was a property of the TOOL, not the
        /// target (CLAUDE.md §4z) — the UVs live in the FBX, so
        /// tools/blender/unwrap_existing_props.py re-projects them in place with
        /// no source GLB. Hence no per-family tiling exception: the spread that
        /// would have justified one is gone.
        ///
        /// A missing sheet leaves _BaseMap unbound, which URP samples as white:
        /// the flat-tint look this replaced, never a black prop. wrapMode is NOT
        /// set here — assigning it on a loaded Texture2D does not write the
        /// .meta and is lost on the next reimport; PropTextureImportPipeline
        /// owns it, and _BaseMap_ST is dead without it.
        /// </summary>
        static void BindPropTexture(Material material, string propName)
        {
            string sheet;
            if (propName.Contains("-hammer-")) sheet = "prop-iron";
            else if (propName.Contains("-bow-")) sheet = "prop-wood";
            else if (propName.Contains("-weapon-")) sheet = "prop-steel";
            else if (propName.Contains("-lantern-")) sheet = "prop-brass";
            else sheet = "prop-cloth";

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"Assets/Resources/Textures/Props/{sheet}.png");
            material.SetTexture("_BaseMap", texture);
            if (texture == null)
            {
                Debug.LogWarning($"[PropImportPipeline] {propName}: no {sheet}.png — "
                    + "prop keeps the flat tint (run tools/gen_prop_textures.sh)");
                return;
            }
            material.SetVector("_BaseMap_ST", new Vector4(1f, 1f, 0f, 0f));
        }
    }
}
