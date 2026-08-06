using System;
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class StageCatalogTests
    {
        private const string CampaignKey = "abyssal-lantern:unity:campaign";
        private bool _hadCampaign;
        private string _campaignRaw;

        [SetUp]
        public void SetUp()
        {
            _hadCampaign = PlayerPrefs.HasKey(CampaignKey);
            _campaignRaw = PlayerPrefs.GetString(CampaignKey);
            PlayerPrefs.DeleteKey(CampaignKey);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadCampaign)
                PlayerPrefs.SetString(CampaignKey, _campaignRaw);
            else
                PlayerPrefs.DeleteKey(CampaignKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void Entries_AreSixOrderedUniqueLogicalStages()
        {
            var entries = StageCatalog.Entries;
            var ids = new[]
            {
                "cinder-span", "ember-gallery", "abyss-chancel",
                "witness-well", "echo-throne", "ash-verdict",
            };
            var anchors = new[]
            {
                "cinder-span", "cinder-span", "abyss-chancel",
                "abyss-chancel", "echo-throne", "echo-throne",
            };
            var prereqs = new[]
            {
                null, "cinder-span", "ember-gallery", "abyss-chancel", "witness-well", "echo-throne",
            };
            var rewards = new[]
            {
                "ember-cohort", null, "shade-echo", null, "possessed-echo", null,
            };


            Assert.That(entries.Count, Is.EqualTo(ids.Length));
            for (var index = 0; index < entries.Count; index += 1)
            {
                var entry = entries[index];
                Assert.That(entry.CatalogIndex, Is.EqualTo(index));
                Assert.That(entry.Id, Is.EqualTo(ids[index]));
                Assert.That(entry.SimAnchorId, Is.EqualTo(anchors[index]));
                Assert.That(entry.PrereqId, Is.EqualTo(prereqs[index]));
                Assert.That(entry.CompanionReward, Is.EqualTo(rewards[index]),
                    "composite stages must not duplicate frozen anchor companion rewards");
                Assert.That(StageCatalog.TryGet(entry.Id, out var found), Is.True);
                Assert.That(found.CatalogIndex, Is.EqualTo(index));
                Assert.That(found.Id, Is.EqualTo(entry.Id));

                for (var earlier = 0; earlier < index; earlier += 1)
                    Assert.That(entry.Id, Is.Not.EqualTo(entries[earlier].Id), "stage IDs must be unique");
            }

            Assert.That(StageCatalog.TryGet("not-a-stage", out _), Is.False);
        }

        [Test]
        public void Entries_ResolveFrozenAnchorWithoutChangingAnchorStageId()
        {
            var rewardIndices = new int[StageCatalog.Entries.Count];
            for (var index = 0; index < StageCatalog.Entries.Count; index += 1)
            {
                var entry = StageCatalog.Entries[index];
                Assert.That(HackConfig.TryDungeon(entry.SimAnchorId, default, default, (string)null, 0, out var config), Is.True);
                Assert.That(config.StageId, Is.EqualTo(entry.SimAnchorId));

                if (entry.HazardOverride == null)
                {
                    Assert.That(entry.Id, Is.EqualTo(entry.SimAnchorId), "only anchor entries may omit an override");
                }
                else
                {
                    config.Hazards = entry.HazardOverride;
                    var resolved = config.ToCampaignConfig();
                    Assert.That(resolved.StageId, Is.EqualTo(entry.SimAnchorId), "logical catalog IDs must never replace frozen config IDs");
                    Assert.That(resolved.Hazards, Is.SameAs(entry.HazardOverride));
                }

                rewardIndices[index] = CampaignStages.IndexOf(entry.SimAnchorId) % CampaignSpec.EquipSlotCount;
            }

            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1, 2, 2 }, rewardIndices);
        }

        [Test]
        public void Entries_FormStrictPrerequisiteChain()
        {
            var data = new CampaignData { PrologueDone = true };
            for (var index = 1; index < StageCatalog.Entries.Count; index += 1)
                Assert.That(StageCatalog.IsUnlocked(data, StageCatalog.Entries[index]), Is.False,
                    StageCatalog.Entries[index].Id + " must remain locked before its direct predecessor clears");

            for (var index = 0; index < StageCatalog.Entries.Count; index += 1)
            {
                var entry = StageCatalog.Entries[index];
                Assert.That(StageCatalog.IsUnlocked(data, entry), Is.True, entry.Id + " should unlock only after its direct predecessor");
                Assert.That(StageCatalog.IsCleared(data, entry), Is.False);

                StageCatalog.MarkCleared(ref data, entry, out var firstClear);
                Assert.That(firstClear, Is.True);
                Assert.That(StageCatalog.IsCleared(data, entry), Is.True);
            }
        }

        [Test]
        public void Entries_ReferenceOnlyExistingSharedTerrainResources()
        {
            var expectedTerrain = new[]
            {
                "cinder-span", "abyss-chancel", "abyss-chancel",
                "echo-throne", "echo-throne", "echo-throne",
            };

            for (var index = 0; index < StageCatalog.Entries.Count; index += 1)
            {
                var entry = StageCatalog.Entries[index];
                Assert.That(entry.TerrainId, Is.EqualTo(expectedTerrain[index]));
                Assert.That(Resources.Load<GameObject>("Terrain/terrain-" + entry.TerrainId), Is.Not.Null,
                    entry.Id + " must reuse a shipped terrain prefab");
            }
        }

        [Test]
        public void CompositeHazards_MatchPlacementAndClearanceContracts()
        {
            AssertCompositeHazards("ember-gallery", new[]
            {
                HazardConfig.Vent(560f, 480f, 0f),
                HazardConfig.Vent(980f, 720f, 1.2f),
                HazardConfig.Vent(1100f, 450f, 0.6f),
                HazardConfig.Pillar(768f, 604f),
            });
            AssertCompositeHazards("witness-well", new[]
            {
                HazardConfig.Altar(768f, 604f),
                HazardConfig.Pillar(640f, 500f),
                HazardConfig.Pillar(900f, 700f),
                HazardConfig.Vent(1030f, 480f, 1.2f),
            });
            AssertCompositeHazards("ash-verdict", new[]
            {
                HazardConfig.Altar(768f, 604f),
                HazardConfig.Vent(560f, 480f, 0f),
                HazardConfig.Vent(980f, 720f, 1.2f),
                HazardConfig.Vent(1030f, 480f, 0.6f),
            });
        }

        [Test]
        public void Load_LegacyClearedIdsMapToAnchorBits()
        {
            PlayerPrefs.SetString(CampaignKey,
                "{\"cleared\":[\"cinder-span\",\"abyss-chancel\",\"echo-throne\"],\"equipment\":{\"weapon\":2,\"lantern\":3,\"cloak\":4}}");
            PlayerPrefs.Save();

            var data = CampaignStore.Load();

            Assert.That(data.ClearedMask, Is.EqualTo((1 << 0) | (1 << 2) | (1 << 4)));
            Assert.That(data.Weapon, Is.EqualTo(2));
            Assert.That(data.Lantern, Is.EqualTo(3));
            Assert.That(data.Cloak, Is.EqualTo(4));
        }

        [Test]
        public void Save_RoundTripsSixBitMaskAndPreservesMetaWithoutLegacyShape()
        {
            var saved = new CampaignData
            {
                ClearedMask = 0x2B,
                Weapon = 2,
                Lantern = 3,
                Cloak = 4,
                Attack = 5,
                Vitality = 6,
                Swiftness = 7,
                Points = 8,
                Relics = 9,
                Roster = new[] { "ember-cohort-echo", "shade-echo" },
                Active = "shade-echo",
                PrologueDone = true,
            };

            CampaignStore.Save(in saved);
            var raw = PlayerPrefs.GetString(CampaignKey);
            var loaded = CampaignStore.Load();

            Assert.That(raw, Does.Contain("\"clearedMask\":43"));
            Assert.That(raw, Does.Not.Contain("\"cleared\":"));
            Assert.That(loaded.ClearedMask, Is.EqualTo(saved.ClearedMask));
            Assert.That(loaded.Weapon, Is.EqualTo(saved.Weapon));
            Assert.That(loaded.Lantern, Is.EqualTo(saved.Lantern));
            Assert.That(loaded.Cloak, Is.EqualTo(saved.Cloak));
            Assert.That(loaded.Attack, Is.EqualTo(saved.Attack));
            Assert.That(loaded.Vitality, Is.EqualTo(saved.Vitality));
            Assert.That(loaded.Swiftness, Is.EqualTo(saved.Swiftness));
            Assert.That(loaded.Points, Is.EqualTo(saved.Points));
            Assert.That(loaded.Relics, Is.EqualTo(saved.Relics));
            CollectionAssert.AreEqual(saved.Roster, loaded.Roster);
            Assert.That(loaded.Active, Is.EqualTo(saved.Active));
            Assert.That(loaded.PrologueDone, Is.EqualTo(saved.PrologueDone));
        }
        [Test]

        public void IsUnlocked_DirectlyClearedLegacyAnchorRemainsEnterable()
        {
            PlayerPrefs.SetString(CampaignKey, "{\"cleared\":[\"abyss-chancel\"]}");
            PlayerPrefs.Save();
            var legacyData = CampaignStore.Load();

            Assert.That(StageCatalog.TryGet("abyss-chancel", out var stageThree), Is.True);
            Assert.That(legacyData.ClearedMask, Is.EqualTo(1 << stageThree.CatalogIndex));
            Assert.That(StageCatalog.IsCleared(legacyData, stageThree), Is.True);

            legacyData.PrologueDone = true;
            Assert.That(StageCatalog.IsUnlocked(legacyData, stageThree), Is.True,
                "a directly cleared legacy anchor must remain re-enterable without a newly inserted prerequisite bit");
        }

        private static void AssertCompositeHazards(string id, HazardConfig[] expected)
        {
            Assert.That(StageCatalog.TryGet(id, out var entry), Is.True);
            Assert.That(entry.HazardOverride, Is.Not.Null);
            Assert.That(entry.HazardOverride.Length, Is.EqualTo(expected.Length));

            for (var index = 0; index < expected.Length; index += 1)
            {
                var actual = entry.HazardOverride[index];
                var wanted = expected[index];
                Assert.That(actual.Kind, Is.EqualTo(wanted.Kind), id + " hazard " + index + " kind");
                Assert.That(actual.X, Is.EqualTo(wanted.X), id + " hazard " + index + " X");
                Assert.That(actual.Y, Is.EqualTo(wanted.Y), id + " hazard " + index + " Y");
                Assert.That(actual.Radius, Is.EqualTo(wanted.Radius), id + " hazard " + index + " radius");
                Assert.That(actual.Phase, Is.EqualTo(wanted.Phase), id + " hazard " + index + " phase");
            }

            for (var left = 0; left < entry.HazardOverride.Length; left += 1)
            {
                for (var right = left + 1; right < entry.HazardOverride.Length; right += 1)
                {
                    var a = entry.HazardOverride[left];
                    var b = entry.HazardOverride[right];
                    var x = a.X - b.X;
                    var y = a.Y - b.Y;
                    var distance = (float)Math.Sqrt(x * x + y * y);
                    Assert.That(distance, Is.GreaterThan(a.Radius + b.Radius), id + " hazards must not overlap");
                    if (a.Kind == HazardKind.ObsidianPillar && b.Kind == HazardKind.ObsidianPillar)
                    {
                        Assert.That(distance, Is.GreaterThanOrEqualTo(a.Radius + b.Radius + 2f * CampaignSpec.PlayerPushRadius),
                            id + " pillars need two player push radii of separation");
                    }
                }
            }
        }
    }
}
