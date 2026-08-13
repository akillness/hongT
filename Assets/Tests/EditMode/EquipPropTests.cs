// Lane P contract: TWELVE socket-prop prefabs exist with renderers, respect the
// <=800-triangle budget each, keep the WORN TRIO under the §T4 25k character
// cap, and the tier->band mapping follows T0-1 none / T2-3 basic / T4-5 fine.
// Since 2026-08-13 they also carry the toon material, its texture, and a band
// emission difference a player can see.
//
// "Six" until 2026-08-13, when an audit found the sweep was Slots x Bands and
// therefore blind to the six ARCHETYPE props (dagger/bow/hammer) — the ones
// ActorView:615-620 actually prefers wherever a stage supplies an archetype.
// The untested half was the half most dungeons render.
using System.Linq;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class EquipPropTests
    {
        static readonly string[] Slots = { "weapon", "lantern", "cloak" };
        static readonly string[] Bands = { "basic", "fine" };

        /// <summary>Every prop prefab that Resources actually ships, not just the
        /// Slots x Bands six. ActorView.AttachEquipProps prefers
        /// `equip-weapon-{archetype}-{band}` whenever a stage supplies an
        /// archetype (ActorView.cs:615-620), so dagger/bow/hammer are what most
        /// dungeons render — and a Slots-only sweep leaves the majority case
        /// unchecked. Audited 2026-08-13: the material assertions below were
        /// reaching 6 of 12.</summary>
        static readonly string[] AllPropNames =
        {
            "equip-weapon-basic", "equip-weapon-fine",
            "equip-lantern-basic", "equip-lantern-fine",
            "equip-cloak-basic", "equip-cloak-fine",
            "equip-weapon-dagger-basic", "equip-weapon-dagger-fine",
            "equip-weapon-bow-basic", "equip-weapon-bow-fine",
            "equip-weapon-hammer-basic", "equip-weapon-hammer-fine",
        };

        /// <summary>The six prop families, derived from AllPropNames rather than
        /// listed a second time — two hand-kept lists drift, and the drift is
        /// silent because each one looks complete on its own.</summary>
        static readonly string[] PropFamilies = AllPropNames
            .Select(n => n.EndsWith("-fine") ? n[..^5] : n[..^6])
            .Distinct()
            .ToArray();

        const int TriBudgetPerProp = 800;

        static GameObject Load(string slot, string band)
            => Resources.Load<GameObject>($"Props/equip-{slot}-{band}");

        static GameObject LoadByName(string propName)
            => Resources.Load<GameObject>($"Props/{propName}");

        static int TriangleCount(GameObject prefab)
        {
            var total = 0;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                for (var s = 0; s < mesh.subMeshCount; s++)
                    total += (int)(mesh.GetIndexCount(s) / 3);
            }
            return total;
        }

        [Test]
        public void AllTwelvePropPrefabs_ExistWithRenderers()
        {
            foreach (var propName in AllPropNames)
            {
                var prefab = LoadByName(propName);
                Assert.That(prefab, Is.Not.Null, $"Props/{propName} missing");
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty,
                    $"{propName} has no renderer");
            }
        }

        [Test]
        public void EveryProp_RespectsTriangleBudget()
        {
            foreach (var propName in AllPropNames)
            {
                var tris = TriangleCount(LoadByName(propName));
                Assert.That(tris, Is.GreaterThan(0), $"{propName} has no triangles");
                Assert.That(tris, Is.LessThanOrEqualTo(TriBudgetPerProp),
                    $"{propName} over budget: {tris}");
            }
            // A character wears at most ONE band of three slots at a time, so the
            // §T4 cap applies to that worn trio, not to the whole catalogue.
            // Summing all twelve would grow with the asset count and stop meaning
            // anything about a character.
            var worstBandTotal = 0;
            foreach (var band in Bands)
            {
                var total = 0;
                foreach (var slot in Slots) total += TriangleCount(Load(slot, band));
                worstBandTotal = Mathf.Max(worstBandTotal, total);
            }
            Assert.That(worstBandTotal, Is.LessThan(25000),
                $"worst worn trio {worstBandTotal} tris exceeds the §T4 character cap");
        }

        [Test]
        public void EveryProp_HasCharacterScaleWorldSize()
        {
            // FBX unit traps (cm vs m) make props microscopic or gigantic, and a
            // Blender import/export round-trip can silently re-bake a 100x scale.
            // Character is ~1.8 wu tall: every prop's longest world span must land
            // in a wearable band. ALL TWELVE deliberately — the archetype six are
            // the ones gen_weapon_props.py regenerates from scratch, so they are
            // exactly the assets most exposed to this trap, and a Slots x Bands
            // sweep never saw them.
            foreach (var propName in AllPropNames)
            {
                var prefab = LoadByName(propName);
                Assert.That(prefab, Is.Not.Null, $"Props/{propName} missing");
                var instance = Object.Instantiate(prefab);
                try
                {
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    var bounds = renderers[0].bounds;
                    for (var i = 1; i < renderers.Length; i++)
                        bounds.Encapsulate(renderers[i].bounds);
                    var span = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                    Assert.That(span, Is.InRange(0.15f, 1.6f),
                        $"{propName} world span {span:F4} is not wearable");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        /// <summary>The props must be on the toon shader, exactly — not on a
        /// fallback, and not on URP/Lit.
        ///
        /// THIS REPLACES AN UNFALSIFIABLE ASSERTION (2026-08-13). The previous
        /// version accepted `CinderCourt/ToonLit` OR any `Universal Render
        /// Pipeline/` name and claimed to gate WebGL variant stripping. It could
        /// not: CinderToonLit.shader:280 declares
        /// `Fallback "Universal Render Pipeline/Lit"`, and under a ShaderLab
        /// fallback the Shader OBJECT is unchanged — only SubShader selection
        /// moves — so `material.shader.name` still returns "CinderCourt/ToonLit".
        /// Neither branch of the Or could ever fire. A test named for stripping
        /// was structurally incapable of observing stripping, and its second
        /// branch would have greenlit the very fall-through it warned about.
        ///
        /// What IS checkable from EditMode is that the material is the one the
        /// pipeline built: the exact shader, and the toon-only properties that
        /// carry the deliverable. `_EmissionColor` is the rank readout and
        /// `_BaseMap` is the texture; URP/Lit gates emission behind the
        /// `_EMISSION` keyword, which none of these materials set, so a real
        /// fall-through renders rank emission as exactly zero. Asserting the
        /// values here means that state cannot land silently.
        ///
        /// WHERE STRIPPING IS ACTUALLY OBSERVABLE: not here. EditMode always has
        /// the shader, so no EditMode test can see a build-time strip. Worse,
        /// two fallbacks work against each other — PropShader()'s explicit
        /// `Shader.Find(toon) ?? Lit` is inspectable because the material's
        /// shader name changes, while the shader-level `Fallback` is not,
        /// because it keeps the name. CinderToonLit is NOT in
        /// m_AlwaysIncludedShaders, so its survival rests on being reachable
        /// from a serialized material under Resources. The gate for that is the
        /// BROWSER capture against a release build
        /// (tools/qa/capture_equip_props.mjs): toon banding and outlines are
        /// visible there or they are not.
        ///
        /// Compared against ViewWorld.ToonLitShaderName rather than a literal so
        /// a rename cannot leave this passing on a stale string.</summary>
        [Test]
        public void PropMaterials_AreOnToonLitWithBandEmissionAndTexture()
        {
            var checkedMaterials = 0;
            foreach (var propName in AllPropNames)
            {
                var prefab = LoadByName(propName);
                Assert.That(prefab, Is.Not.Null, $"{propName}: prefab missing from Resources");
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null, $"{propName}: null material");
                    Assert.That(material.shader.name, Is.EqualTo(ViewWorld.ToonLitShaderName),
                        $"{propName}: shader is {material.shader.name}, not "
                        + $"{ViewWorld.ToonLitShaderName} — the prop lost its toon material");
                    Assert.That(material.HasProperty("_EmissionColor"), Is.True,
                        $"{propName}: no _EmissionColor — band rank cannot render");
                    Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null,
                        $"{propName}: _BaseMap unbound — prop is flat-tinted");
                    Assert.That(material.GetColor("_BaseColor"), Is.Not.EqualTo(Color.white),
                        $"{propName}: _BaseColor is white — the tint never landed");
                    checkedMaterials++;
                }
            }
            // A loop that inspects nothing asserts nothing. This repo already
            // treats that as a bug (StageShadowCatalogTests:130,163 and
            // make_release_evidence.py:89-91, which refuses an empty subset
            // because "it would certify the contract by having tested nothing").
            Assert.That(checkedMaterials, Is.GreaterThanOrEqualTo(AllPropNames.Length),
                $"only {checkedMaterials} materials inspected across "
                + $"{AllPropNames.Length} props — the sweep found nothing to check");
        }

        /// <summary>Band must be legible: fine emission has to exceed basic on
        /// every prop family, by a margin a player could actually see.
        ///
        /// This is the property the whole toon conversion was gated on — the
        /// earlier sweep skipped props precisely because ToonLit had no emission
        /// term — and until this test nothing failed if it regressed. A reverted
        /// shader term or a flattened multiplier shipped green.
        ///
        /// FAMILIES, not Slots: `Slots x Bands` reaches only the legacy six, and
        /// ActorView:615-620 prefers `equip-weapon-{archetype}-{band}` wherever a
        /// stage supplies one, so dagger/bow/hammer are what most dungeons render.
        /// Gating the band readout on the half that usually is NOT on screen would
        /// have reproduced the coverage hole this cycle was fixing.
        ///
        /// MARGIN, not a bare `GreaterThan`: an inequality passes at a 0.001 gap,
        /// which no player can see and is therefore not a readout.
        ///
        /// But a RATIO cannot carry the whole check, because one baseline is
        /// legitimately zero — the cloak is 0.22 fine against 0 basic, so
        /// `fine >= basic * 2` collapses to `fine >= 0`, which every value
        /// satisfies including a flattened 0. That is the vacuous-assertion shape
        /// this cycle removed twice already. So: an ABSOLUTE floor on fine always,
        /// and the ratio only where a non-zero baseline makes it meaningful. The
        /// tightest shipped non-zero pair is the lantern (0.70 vs 0.18, 3.9x), so
        /// 2x sits under every current value while still rejecting a collapse.
        /// </summary>
        [Test]
        public void FineBandEmission_ExceedsBasic_OnEveryFamily()
        {
            foreach (var family in PropFamilies)
            {
                var basic = BandEmission($"{family}-basic");
                var fine = BandEmission($"{family}-fine");
                Assert.That(fine, Is.GreaterThan(0.1f),
                    $"{family}: fine emission {fine:F3} is not visible at all — "
                    + "the band readout IS the emission");
                if (basic > 0f)
                {
                    Assert.That(fine, Is.GreaterThanOrEqualTo(basic * 2f),
                        $"{family}: fine {fine:F3} must clearly exceed basic {basic:F3} — "
                        + "a hairline gap is not a readout");
                }
            }
        }

        static float BandEmission(string propName)
        {
            var peak = 0f;
            foreach (var renderer in LoadByName(propName).GetComponentsInChildren<Renderer>(true))
            foreach (var material in renderer.sharedMaterials)
            {
                var e = material.GetColor("_EmissionColor");
                peak = Mathf.Max(peak, e.r + e.g + e.b);
            }
            return peak;
        }

        [Test]
        public void PlayerPrefab_IsHumanoidWithAllThreeSocketBones()
        {
            var player = Resources.Load<GameObject>("Characters/lantern-reaver");
            Assert.That(player, Is.Not.Null, "player prefab missing");
            var animator = player.GetComponentInChildren<Animator>();
            Assert.That(animator, Is.Not.Null.And.Property("isHuman").True,
                "player must be Humanoid for socket lookup");
            Assert.That(animator.GetBoneTransform(HumanBodyBones.RightHand), Is.Not.Null);
            Assert.That(animator.GetBoneTransform(HumanBodyBones.LeftHand), Is.Not.Null);
            Assert.That(animator.GetBoneTransform(HumanBodyBones.Chest), Is.Not.Null);
        }
    }
}
