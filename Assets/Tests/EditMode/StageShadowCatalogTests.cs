// Shipping shadow-caster catalogue.
//
// This intentionally follows the Resources mappings that GameBootstrap,
// GameView, StageCatalog, and ActorView actually use. It does not scan every
// asset in the Characters folder: doing so would turn import-only source rigs
// or currently unmapped archetypes into accidental release requirements. The
// inverse matters too — every mapped entry is explicit or derived from the
// live catalog, so a missing prefab cannot quietly become ActorView's capsule,
// an archetype boss cannot quietly use the shared boss, and a weapon family
// cannot quietly use ActorView's legacy equipment fallback.
using System;
using System.Collections.Generic;
using System.IO;
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class StageShadowCatalogTests
    {
        const string PlayerPrimaryPath = "Characters/human-command-boss";
        const string PlayerFallbackPath = "Characters/lantern-reaver";
        const string ReceiverMaterialPath =
            "Assets/Resources/Materials/StageShadowReceiver.mat";
        const string ReceiverShaderPath = "Assets/Shaders/StageShadowReceiver.shader";

        static readonly Dictionary<EnemyVisual, string> EnemyPaths =
            new Dictionary<EnemyVisual, string>
            {
                { EnemyVisual.EmberCohort, "Characters/ember-cohort" },
                { EnemyVisual.Scout, "Characters/scout" },
                { EnemyVisual.Shade, "Characters/shade" },
                { EnemyVisual.Possessed, "Characters/possessed" },
                { EnemyVisual.BossCommander, "Characters/shadow-commander-boss" },
                { EnemyVisual.BossMonarch, "Characters/broken-court-monarch-boss" },
            };

        // GameBootstrap deliberately has no Monarch-specific prefab. Monarch
        // is the final frozen boss and resolves through EnemyVisual.BossMonarch
        // above; inventing an s4 resource here would test an unavailable
        // archetype rather than the shipping GameView path.
        static readonly Dictionary<BossArchetype, string> ArchetypeBossPaths =
            new Dictionary<BossArchetype, string>
            {
                { BossArchetype.Warden, "Characters/s1-cinder-warden" },
                { BossArchetype.Tactician, "Characters/s2-veil-tactician" },
                { BossArchetype.Sovereign, "Characters/s3-gate-sovereign" },
            };

        static readonly string[] EquipmentBands = { "basic", "fine" };
        static readonly string[] LegacyEquipmentSlots = { "weapon", "lantern", "cloak" };

        [Test]
        public void RuntimeCharacterMappings_AllResolveWithoutActorOrBossFallbacks()
        {
            PinBootstrapSourceMappings();

            var paths = new HashSet<string>
            {
                PlayerPrimaryPath,
                // This remains a committed runtime path even though the
                // primary is now required. Its own materials must not regress.
                PlayerFallbackPath,
            };

            var enemyValues = (EnemyVisual[])Enum.GetValues(typeof(EnemyVisual));
            Assert.That(EnemyPaths.Count, Is.EqualTo(enemyValues.Length),
                "every EnemyVisual handled by GameView.Rent must have an explicit Resources mapping");
            foreach (var visual in enemyValues)
            {
                Assert.That(EnemyPaths.ContainsKey(visual), Is.True,
                    visual + " would otherwise reach ActorView.Create with a null prefab");
                paths.Add(EnemyPaths[visual]);
            }

            // The stage catalog owns the shared-boss and companion resource
            // IDs that GameView can present. Derive them here so adding a stage
            // cannot leave this catalogue frozen at today's nine rows.
            foreach (var stage in StageCatalog.Entries)
            {
                Assert.That(EnemyPaths.ContainsKey(stage.Boss.Visual), Is.True,
                    stage.Id + " boss visual has no GameBootstrap mapping");
                var stageBossPath = "Characters/" + stage.Boss.ResourceId;
                Assert.That(stageBossPath, Is.EqualTo(EnemyPaths[stage.Boss.Visual]),
                    stage.Id + " StageCatalog boss resource disagrees with EnemyVisualFor");
                paths.Add(stageBossPath);

                if (!string.IsNullOrEmpty(stage.CompanionReward))
                {
                    var companionBaseId = BaseCompanionId(stage.CompanionReward);
                    var companionPath = "Characters/" + companionBaseId;
                    var companionVisual = HackSpec.CompanionArchetype(stage.CompanionReward);
                    Assert.That(EnemyPaths.ContainsKey(companionVisual), Is.True,
                        stage.CompanionReward + " has no enemy/companion visual mapping");
                    Assert.That(companionPath, Is.EqualTo(EnemyPaths[companionVisual]),
                        stage.CompanionReward + " would load a different prefab than its sim archetype");
                    paths.Add(companionPath);
                }
            }

            // GameView asks GameBootstrap for an archetype prefab for every
            // mapped dungeon boss. Warden/Tactician/Sovereign must not fall
            // back to the shared visual; Monarch intentionally uses its
            // committed EnemyVisual.BossMonarch mapping.
            for (var index = 0; index < BossVarietySpec.MappedStageCount; index++)
            {
                var stageId = BossVarietySpec.MappedStageIdAt(index);
                var archetype = BossVarietySpec.ArchetypeFor(stageId);
                Assert.That(archetype, Is.Not.EqualTo(BossArchetype.None),
                    stageId + " progression row has no boss archetype");
                if (archetype == BossArchetype.Monarch)
                {
                    paths.Add(EnemyPaths[EnemyVisual.BossMonarch]);
                    continue;
                }

                Assert.That(ArchetypeBossPaths.ContainsKey(archetype), Is.True,
                    stageId + " archetype would fall back through GameView.RentBoss");
                paths.Add(ArchetypeBossPaths[archetype]);
            }

            var materials = new HashSet<Material>();
            foreach (var path in paths)
                AssertPrefabAndShadowCasterMaterials(path, materials, "character");

            Assert.That(materials.Count, Is.GreaterThan(0),
                "the shipping character mappings yielded no serialized materials");
        }

        [Test]
        public void RuntimeEquipmentMappings_AllResolveBeforeTheTintOrLegacyFallback()
        {
            var paths = new HashSet<string>();
            foreach (var slot in LegacyEquipmentSlots)
            foreach (var band in EquipmentBands)
                paths.Add("Props/equip-" + slot + "-" + band);

            // Do not hand-list dagger/bow/hammer. The production resolver is
            // the source of which families the current StageCatalog can put on
            // the player, and a new family must ship both bands before passing.
            foreach (var stage in StageCatalog.Entries)
            {
                var family = GameView.WeaponArchetypeFor(stage.Id);
                Assert.That(family, Is.Not.Null.And.Not.Empty,
                    stage.Id + " did not resolve a dungeon weapon family");
                foreach (var band in EquipmentBands)
                    paths.Add("Props/equip-weapon-" + family + "-" + band);
            }

            var materials = new HashSet<Material>();
            foreach (var path in paths)
            {
                AssertPrefabAndShadowCasterMaterials(
                    path,
                    materials,
                    path.Contains("equip-weapon-") ? "weapon/equipment" : "equipment");
            }

            Assert.That(materials.Count, Is.GreaterThan(0),
                "the shipping equipment mappings yielded no serialized materials");
        }

        [Test]
        public void ReceiverAndBuildSeedRetention_RemainSerializedAndOrderedBeforeBuildPlayer()
        {
            var receiver = Resources.Load<Material>("Materials/StageShadowReceiver");
            Assert.That(receiver, Is.Not.Null, ReceiverMaterialPath + " missing");
            Assert.That(AssetDatabase.GetAssetPath(receiver), Is.EqualTo(ReceiverMaterialPath));
            Assert.That(receiver.shader, Is.Not.Null);
            Assert.That(receiver.shader.name, Is.EqualTo("CinderCourt/StageShadowReceiver"));
            Assert.That(AssetDatabase.GetAssetPath(receiver.shader), Is.EqualTo(ReceiverShaderPath));

            var seeds = File.ReadAllText("Assets/Editor/RuntimeMaterialSeeds.cs");
            Assert.That(seeds, Does.Contain(
                "StageShadowReceiverAssetPath = Dir + \"/StageShadowReceiver.mat\""));
            Assert.That(seeds, Does.Contain(
                "StageShadowReceiverShaderPath = \"Assets/Shaders/StageShadowReceiver.shader\""));
            Assert.That(seeds, Does.Contain("SeedStageShadowReceiver()"));

            var build = File.ReadAllText("Assets/Editor/BuildScript.cs");
            var seedOffset = build.IndexOf(
                "if (!RuntimeMaterialSeeds.Seed())", StringComparison.Ordinal);
            var buildPlayerOffset = build.IndexOf("BuildPipeline.BuildPlayer", StringComparison.Ordinal);
            Assert.That(seedOffset, Is.GreaterThanOrEqualTo(0),
                "BuildWebGL no longer validates the committed Resources seeds");
            Assert.That(buildPlayerOffset, Is.GreaterThan(seedOffset),
                "receiver retention must be validated before BuildPlayer can strip shader variants");
            Assert.That(build, Does.Contain(
                "BuildWebGLDevelopment()\n            => BuildWebGLTo(\"build-development\", BuildOptions.Development)"),
                "Development QA must be a separately marked output, never the deployable Release build");

            var batch = File.ReadAllText("tools/unity_batch.sh");
            Assert.That(batch, Does.Contain("build-development)"));
            Assert.That(batch, Does.Contain(
                "CinderCourt.EditorTools.BuildScript.BuildWebGLDevelopment"));
        }

        static void AssertPrefabAndShadowCasterMaterials(
            string resourcePath,
            ISet<Material> inspectedMaterials,
            string catalogueKind)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            Assert.That(prefab, Is.Not.Null,
                resourcePath + " missing: the " + catalogueKind
                + " mapping would resolve through a runtime fallback");

            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var eligibleCount = 0;
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                        continue;

                    eligibleCount++;
                    Assert.That(renderer.sharedMaterials, Is.Not.Empty,
                        resourcePath + "/" + renderer.name + " has no material");
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null,
                            resourcePath + "/" + renderer.name + " has a null material slot");
                        if (!inspectedMaterials.Add(material)) continue;

                        var materialPath = AssetDatabase.GetAssetPath(material);
                        Assert.That(materialPath, Is.Not.Null.And.Not.Empty,
                            resourcePath + " uses runtime-only material " + material.name);
                        Assert.That(AssetDatabase.Contains(material), Is.True,
                            materialPath + " must be a serialized build-retention reference");
                        Assert.That(material.shader, Is.Not.Null,
                            materialPath + " has an unresolved shader");
                        Assert.That(ShaderSourceDeclaresShadowCaster(material.shader), Is.True,
                            materialPath + " / " + material.shader.name
                            + " has no serialized ShadowCaster pass");
                        Assert.That(material.GetShaderPassEnabled("ShadowCaster"), Is.True,
                            materialPath + " explicitly disables its ShadowCaster pass");
                    }
                }

                Assert.That(eligibleCount, Is.GreaterThan(0),
                    resourcePath + " has no MeshRenderer or SkinnedMeshRenderer caster");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            if (catalogueKind == "character")
            {
                var view = ActorView.Create(prefab, Color.white, 1f);
                try
                {
                    Assert.That(view.UsesFallbackForShadowDiagnostics, Is.False,
                        resourcePath + " unexpectedly entered ActorView fallback");
                    Assert.That(view.ShadowCasterSetsMatch(), Is.True,
                        resourcePath + " production ActorView caster census is incomplete");
                    Assert.That(StageShadowPolicy.FallbackActorCount, Is.Zero);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(view.gameObject);
                }
            }
        }

        static string BaseCompanionId(string companionId)
        {
            const string EchoSuffix = "-echo";
            return companionId.EndsWith(EchoSuffix, StringComparison.Ordinal)
                ? companionId.Substring(0, companionId.Length - EchoSuffix.Length)
                : companionId;
        }

        static bool ShaderSourceDeclaresShadowCaster(Shader shader)
        {
            var path = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            var source = File.ReadAllText(path);
            return source.Contains("Name \"ShadowCaster\"")
                || source.Contains("/ShadowCaster\"");
        }

        static void PinBootstrapSourceMappings()
        {
            var source = File.ReadAllText("Assets/Scripts/View/GameBootstrap.cs");
            Assert.That(source, Does.Contain(
                "PlayerPrefab = Resources.Load<GameObject>(\"" + PlayerPrimaryPath + "\")"));
            Assert.That(source, Does.Contain(
                "PlayerPrefab = Resources.Load<GameObject>(\"" + PlayerFallbackPath + "\")"));
            foreach (var pair in EnemyPaths)
            {
                var expected = "LoadEnemy(EnemyVisual." + pair.Key + ", \"" + pair.Value + "\");";
                Assert.That(source, Does.Contain(expected),
                    pair.Key + " test mapping drifted from GameBootstrap");
            }
            foreach (var pair in ArchetypeBossPaths)
            {
                var resourceId = pair.Value.Substring("Characters/".Length);
                var expected = "BossArchetype." + pair.Key + " => \"" + resourceId + "\"";
                Assert.That(source, Does.Contain(expected),
                    pair.Key + " test mapping drifted from BossArchetypePrefab");
            }
        }
    }
}
