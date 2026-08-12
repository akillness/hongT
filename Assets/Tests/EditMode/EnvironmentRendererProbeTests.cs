// Ask the runtime what the dungeon's environment renderers actually are.
//
// WHY THIS EXISTS AS A TEST AND NOT AS A ONE-OFF SCRIPT. Four hypotheses about the
// arena boundary ring rendering as blank pale slabs have been refuted by measurement,
// and every one of them was formed by READING SOURCE:
//
//   1. the kit materials have no albedo bound            -> refuted, .mat carries _BaseMap
//   2. the toon stage textures are near-uniform sheets    -> refuted, all 18 replaced,
//                                                            ring frame unchanged
//   3. SRP Batcher swallows the MaterialPropertyBlock     -> refuted, m_UseSRPBatcher 0
//                                                            rebuild, ring unchanged
//   4. the terrain material conversion would fix it       -> refuted, 19 .mat moved to
//                                                            toon, ring locStd 3.15 -> 3.15
//
// Four wrong answers from the same method is a fact about the method. Source says what
// the code intends; only the built object graph says what exists. So this asks the
// object graph, and it is a test rather than a scratch script so the answer survives
// and the next person does not re-derive it (the repo's own pattern for "measured
// once, keep the table" -- LobbyLayoutTests' touch-floor debt table).
//
// This test ASSERTS almost nothing on purpose. Its output is the artifact. The one
// thing it does assert is that it measured something at all, because a probe that
// silently finds zero renderers would print an empty table and read as "nothing
// wrong" -- the shape of failure this repo has hit four times (§4m, §4n, §4s).
using System.Collections.Generic;
using System.Text;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class EnvironmentRendererProbeTests
    {
        GameObject _environment;

        [TearDown]
        public void TearDown()
        {
            if (_environment != null) Object.DestroyImmediate(_environment);
        }

        [Test]
        public void CinderSpan_EnvironmentRenderers_ReportWhatTheyActuallyAre()
        {
            _environment = EnvironmentBuilder.Build("cinder-span");
            Assert.That(_environment, Is.Not.Null, "EnvironmentBuilder returned nothing");

            var renderers = _environment.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers.Length, Is.GreaterThan(0),
                "zero renderers — this probe would print an empty table and read as "
                + "'nothing wrong', which is exactly the failure it exists to avoid");

            // Group by the tuple that decides how a surface looks. If the ring is flat
            // because of a material/shader/texture fact, it shows up as a group whose
            // texture is null or whose ST is (1,1,0,0) while others differ.
            var groups = new Dictionary<string, int>();
            var samples = new Dictionary<string, string>();
            var block = new MaterialPropertyBlock();
            var noTexture = 0;
            var noBlock = 0;

            foreach (var renderer in renderers)
            {
                var material = renderer.sharedMaterial;
                var shader = material != null && material.shader != null
                    ? material.shader.name : "(no material)";
                var texture = material != null && material.HasProperty("_BaseMap")
                    ? material.GetTexture("_BaseMap") : null;
                if (texture == null) noTexture += 1;

                block.Clear();
                renderer.GetPropertyBlock(block);
                var hasBlock = !block.isEmpty;
                if (!hasBlock) noBlock += 1;
                // GetVector on an unset key returns zero, which is indistinguishable
                // from a deliberately-zero ST. Report the emptiness separately rather
                // than letting one value mean two things.
                var st = hasBlock ? block.GetVector("_BaseMap_ST") : Vector4.zero;
                var tint = hasBlock ? block.GetVector("_BaseColor") : Vector4.zero;

                var key = $"{material?.name ?? "(null)"} | {shader} | "
                        + $"tex={(texture != null ? texture.name : "NULL")} | mpb={hasBlock}";
                groups.TryGetValue(key, out var count);
                groups[key] = count + 1;
                if (!samples.ContainsKey(key))
                {
                    var b = renderer.bounds;
                    samples[key] = $"ST=({st.x:F2},{st.y:F2})  tint=({tint.x:F2},{tint.y:F2},"
                        + $"{tint.z:F2})  size=({b.size.x:F1},{b.size.y:F1},{b.size.z:F1})  "
                        + $"first='{renderer.name}'";
                }
            }

            var report = new StringBuilder();
            report.AppendLine($"[cinder-span environment: {renderers.Length} MeshRenderers, "
                + $"{groups.Count} distinct material/shader/texture groups]");
            report.AppendLine($"  renderers with NO _BaseMap texture : {noTexture}");
            report.AppendLine($"  renderers with an EMPTY MPB        : {noBlock}");
            report.AppendLine();
            foreach (var pair in groups)
            {
                report.AppendLine($"  x{pair.Value,-4} {pair.Key}");
                report.AppendLine($"          {samples[pair.Key]}");
            }
            TestContext.WriteLine(report.ToString());
        }

        /// <summary>
        /// WHICH renderers form the pale ring — by POSITION, which is the question five
        /// refuted hypotheses all skipped.
        ///
        /// Every one of them assumed the ring was env-stone and then argued about why
        /// env-stone looked wrong. Nobody checked that the ring IS env-stone. The arena
        /// is 1536x1024 centred (768, 604) with radii 520x270 (CLAUDE.md §2), so a
        /// piece belongs to the boundary if its centre sits near that ellipse. Grouping
        /// the boundary pieces separately from the interior ones says, without any
        /// inference, what the ring is made of.
        /// </summary>
        [Test]
        public void CinderSpan_BoundaryRing_IsMadeOfWhat()
        {
            _environment = EnvironmentBuilder.Build("cinder-span");
            var renderers = _environment.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers.Length, Is.GreaterThan(0));

            // Arena contract (§2). Reading the ellipse rather than eyeballing a radius:
            // a hand-picked band would decide the answer before measuring it.
            const float cx = 768f, cz = 604f, rx = 520f, rz = 270f;
            var onRing = new Dictionary<string, int>();
            var inside = new Dictionary<string, int>();
            var ringTint = new Dictionary<string, string>();
            var block = new MaterialPropertyBlock();
            var minR = float.MaxValue;
            var maxR = float.MinValue;

            foreach (var renderer in renderers)
            {
                var p = renderer.bounds.center;
                // Normalised elliptical radius: 1.0 is exactly on the boundary.
                var r = Mathf.Sqrt(((p.x - cx) * (p.x - cx)) / (rx * rx)
                                 + ((p.z - cz) * (p.z - cz)) / (rz * rz));
                minR = Mathf.Min(minR, r);
                maxR = Mathf.Max(maxR, r);

                var name = renderer.sharedMaterial != null
                    ? renderer.sharedMaterial.name : "(null)";
                var bucket = r >= 0.85f ? onRing : inside;
                bucket.TryGetValue(name, out var n);
                bucket[name] = n + 1;

                if (r >= 0.85f && !ringTint.ContainsKey(name))
                {
                    block.Clear();
                    renderer.GetPropertyBlock(block);
                    var tint = block.GetVector("_BaseColor");
                    var st = block.GetVector("_BaseMap_ST");
                    var b = renderer.bounds;
                    ringTint[name] = $"r={r:F2} tint=({tint.x:F3},{tint.y:F3},{tint.z:F3}) "
                        + $"ST=({st.x:F2},{st.y:F2}) size=({b.size.x:F1},{b.size.y:F1},{b.size.z:F1})";
                }
            }

            var report = new StringBuilder();
            report.AppendLine($"[normalised elliptical radius spans {minR:F2}..{maxR:F2} "
                + "— 1.00 is the arena boundary]");
            report.AppendLine("  ON THE RING (r >= 0.85):");
            foreach (var pair in onRing)
                report.AppendLine($"    x{pair.Value,-4} {pair.Key,-14} {ringTint[pair.Key]}");
            report.AppendLine("  INSIDE (r < 0.85):");
            foreach (var pair in inside)
                report.AppendLine($"    x{pair.Value,-4} {pair.Key}");
            TestContext.WriteLine(report.ToString());

            Assert.That(maxR, Is.GreaterThan(0.5f),
                "every piece sits near the centre, so this probe never looked at a "
                + "boundary and its buckets mean nothing");
        }

        /// <summary>
        /// Every renderer inside the stage TERRAIN prefab, by material.
        ///
        /// The paint probes eliminated env-stone, env-floor, env-ember, CourtBackdrop,
        /// VoidFloor and all six cinder-span terrain materials, and the pale ring
        /// survived every one of them. Something is drawing it, and the terrain prefab
        /// is the only spawned object left that this suite can open. If a renderer in
        /// here carries a material NOT in Assets/Art/Terrain/Materials/*cinder-span*,
        /// that is why painting those six missed it.
        /// </summary>
        [Test]
        public void CinderSpanTerrainPrefab_ListsEveryRendererMaterial()
        {
            var prefab = Resources.Load<GameObject>("Terrain/terrain-cinder-span");
            Assert.That(prefab, Is.Not.Null,
                "Resources/Terrain/terrain-cinder-span missing — GameDirector loads "
                + "exactly this path, so its absence would mean the dungeon has no terrain");

            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers.Length, Is.GreaterThan(0), "prefab has no renderers");

            var byMaterial = new Dictionary<string, int>();
            var detail = new Dictionary<string, string>();
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    var name = material != null ? material.name : "(NULL MATERIAL)";
                    byMaterial.TryGetValue(name, out var n);
                    byMaterial[name] = n + 1;
                    if (detail.ContainsKey(name)) continue;
                    var shader = material != null && material.shader != null
                        ? material.shader.name : "-";
                    var tex = material != null && material.HasProperty("_BaseMap")
                        ? material.GetTexture("_BaseMap") : null;
                    var col = material != null && material.HasProperty("_BaseColor")
                        ? material.GetColor("_BaseColor") : Color.clear;
                    detail[name] = $"shader={shader}  tex={(tex != null ? tex.name : "NULL")}  "
                        + $"_BaseColor=({col.r:F3},{col.g:F3},{col.b:F3})  eg='{renderer.name}'";
                }
            }

            var report = new StringBuilder(
                $"[terrain-cinder-span prefab: {renderers.Length} MeshRenderers, "
                + $"{byMaterial.Count} materials]\n");
            foreach (var pair in byMaterial)
            {
                report.AppendLine($"  x{pair.Value,-4} {pair.Key}");
                report.AppendLine($"          {detail[pair.Key]}");
            }
            TestContext.WriteLine(report.ToString());
        }

        /// <summary>
        /// Every renderer BAKED INTO THE SCENE, by material.
        ///
        /// Last candidate standing. Paint probes have eliminated every runtime-spawned
        /// system — env-stone / env-floor / env-ember (MPB tint), the terrain prefab
        /// (its six materials), CourtBackdrop and VoidFloor — and the pale ring
        /// survived all of them. StageCatalog.DressingLibraryTerrainId is "cinder-span",
        /// so dressing clones carry those same six and are eliminated with them.
        ///
        /// What is left is geometry that was never spawned: objects saved in
        /// CinderCourt.unity by SceneBuilder. This enumerates them so the ring has a
        /// name instead of a sixth hypothesis.
        /// </summary>
        [Test]
        public void GameScene_ListsEveryBakedRendererMaterial()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Scenes/CinderCourt.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Additive);
            Assert.That(scene.IsValid(), Is.True, "CinderCourt.unity failed to open");

            var byMaterial = new Dictionary<string, int>();
            var detail = new Dictionary<string, string>();
            var total = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    total += 1;
                    foreach (var material in renderer.sharedMaterials)
                    {
                        var name = material != null ? material.name : "(NULL MATERIAL)";
                        byMaterial.TryGetValue(name, out var n);
                        byMaterial[name] = n + 1;
                        if (detail.ContainsKey(name)) continue;
                        var shader = material != null && material.shader != null
                            ? material.shader.name : "-";
                        var tex = material != null && material.HasProperty("_BaseMap")
                            ? material.GetTexture("_BaseMap") : null;
                        var col = material != null && material.HasProperty("_BaseColor")
                            ? material.GetColor("_BaseColor") : Color.clear;
                        var b = renderer.bounds;
                        detail[name] = $"shader={shader}  tex={(tex != null ? tex.name : "NULL")}  "
                            + $"_BaseColor=({col.r:F3},{col.g:F3},{col.b:F3})  "
                            + $"size=({b.size.x:F1},{b.size.y:F1},{b.size.z:F1})  eg='{renderer.name}'";
                    }
                }
            }

            var report = new StringBuilder(
                $"[CinderCourt.unity: {total} baked MeshRenderers, {byMaterial.Count} materials]\n");
            foreach (var pair in byMaterial)
            {
                report.AppendLine($"  x{pair.Value,-4} {pair.Key}");
                report.AppendLine($"          {detail[pair.Key]}");
            }
            TestContext.WriteLine(report.ToString());
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
        }

        /// <summary>
        /// The same question one level out: what does the stage's TERRAIN carry? The
        /// boundary ring might not be an EnvironmentBuilder piece at all — that is the
        /// assumption every refuted hypothesis shared, and it has never been checked.
        /// </summary>
        [Test]
        public void CinderSpan_SharedEnvironmentMaterials_CarryTheStageAlbedo()
        {
            _environment = EnvironmentBuilder.Build("cinder-span");
            var renderers = _environment.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers.Length, Is.GreaterThan(0));

            var seen = new HashSet<Material>();
            var report = new StringBuilder("[shared materials after ApplyStageTextures]\n");
            foreach (var renderer in renderers)
            {
                var material = renderer.sharedMaterial;
                if (material == null || !seen.Add(material)) continue;
                var texture = material.HasProperty("_BaseMap")
                    ? material.GetTexture("_BaseMap") : null;
                var st = material.HasProperty("_BaseMap")
                    ? material.GetVector("_BaseMap_ST") : Vector4.zero;
                var color = material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor") : Color.clear;
                report.AppendLine($"  {material.name,-14} shader={material.shader.name}");
                report.AppendLine($"      _BaseMap={(texture != null ? texture.name + " " + ((Texture2D)texture).width + "px" : "NULL")}"
                    + $"  matST=({st.x:F2},{st.y:F2})  _BaseColor=({color.r:F2},{color.g:F2},{color.b:F2})");
            }
            TestContext.WriteLine(report.ToString());
            Assert.That(seen.Count, Is.GreaterThan(0), "no shared materials found");
        }
    }
}
