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
        // Gate: R8/G7 — nine ordered logical stages; cycle-2 appends cinder-sluice/
        // ember-bastion/ash-march (own sim anchors, prereq chain through ash-verdict).
        public void Entries_AreNineOrderedUniqueLogicalStages()
        {
            var entries = StageCatalog.Entries;
            var ids = new[]
            {
                "cinder-span", "ember-gallery", "abyss-chancel",
                "witness-well", "echo-throne", "ash-verdict",
                "cinder-sluice", "ember-bastion", "ash-march",
            };
            var anchors = new[]
            {
                "cinder-span", "cinder-span", "abyss-chancel",
                "abyss-chancel", "echo-throne", "echo-throne",
                "cinder-sluice", "ember-bastion", "ash-march",
            };
            var prereqs = new[]
            {
                null, "cinder-span", "ember-gallery", "abyss-chancel", "witness-well", "echo-throne",
                "ash-verdict", "cinder-sluice", "ember-bastion",
            };
            var rewards = new[]
            {
                "ember-cohort", null, "shade-echo", null, "possessed-echo", null,
                null, null, "scout-echo",
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

            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1, 2, 2, 0, 1, 2 }, rewardIndices);
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
            // Cycle-2 stages reuse shipped prefabs: sluice=abyss-chancel,
            // bastion=cinder-span, march=echo-throne (no new terrain assets).
            var expectedTerrain = new[]
            {
                "cinder-span", "abyss-chancel", "abyss-chancel",
                "echo-throne", "echo-throne", "echo-throne",
                "abyss-chancel", "cinder-span", "echo-throne",
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
        // Gate: R4/G2 (v1.2 fun pass) — the four override tables ship the spec's
        // verbatim placements (campaign-fun-pass-spec.md §세부 배치): gallery vent
        // ring, well dual altars, throne current preview (override was NULL until
        // v1.2), verdict pylon preview. Kind/coords/phase/push/band fields all pin.
        public void CompositeHazards_MatchPlacementAndClearanceContracts()
        {
            AssertCompositeHazards("ember-gallery", new[]
            {
                HazardConfig.Vent(560f, 480f, 0f),
                HazardConfig.Vent(980f, 480f, 0.6f),
                HazardConfig.Vent(980f, 720f, 1.2f),
                HazardConfig.Vent(560f, 720f, 1.8f),
                HazardConfig.Pillar(768f, 604f),
            });
            AssertCompositeHazards("witness-well", new[]
            {
                HazardConfig.Altar(560f, 500f),
                HazardConfig.Altar(980f, 700f),
                HazardConfig.Pillar(768f, 604f),
                HazardConfig.Vent(560f, 700f, 0.3f),
                HazardConfig.Vent(980f, 500f, 1.5f),
            });
            AssertCompositeHazards("echo-throne", new[]
            {
                HazardConfig.Altar(768f, 604f),
                HazardConfig.Vent(500f, 700f, 0f),
                HazardConfig.Vent(1030f, 480f, 1.2f),
                HazardConfig.Current(768f, 604f, 120f, 0.3f),
            });
            AssertCompositeHazards("ash-verdict", new[]
            {
                HazardConfig.Altar(768f, 604f),
                HazardConfig.Pylon(960f, 540f),
                HazardConfig.Vent(560f, 480f, 0f),
                HazardConfig.Vent(980f, 720f, 1.2f),
            });
        }

        // Gate: R4/G2 — the three new catalog entries are pure anchors (no override;
        // placement lives in the frozen CampaignStages tables) and their anchor
        // tables obey the same non-overlap/pillar-clearance contract as composites
        // (with the v1.2 guarded-altar exemption — see AssertRadialClearance).
        [Test]
        public void NewStageAnchors_CarryNoOverrideAndClearRadialHazards()
        {
            foreach (var id in new[] { "cinder-sluice", "ember-bastion", "ash-march" })
            {
                Assert.That(StageCatalog.TryGet(id, out var entry), Is.True, id);
                Assert.That(entry.SimAnchorId, Is.EqualTo(id), id + " must anchor its own sim stage");
                Assert.That(entry.HazardOverride, Is.Null,
                    id + " placement is owned by the frozen CampaignStages table");

                Assert.That(CampaignStages.TryGet(id, 0, 0, 0, out var config), Is.True, id);
                AssertRadialClearance(id, config.Hazards);
            }
        }

        static bool IsBand(HazardKind kind)
            => kind == HazardKind.TideCurrent || kind == HazardKind.AshWall;

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

        // Gate: R8 — the 0x3F bug class: a six-bit clear mask silently drops bits
        // 6-8, orphaning cycle-2 first-clears. MarkCleared on catalog index 8
        // (ash-march) must survive a CampaignStore Save->Load round trip.
        [Test]
        public void MarkCleared_Index8_SurvivesSaveLoadRoundTrip()
        {
            Assert.That(StageCatalog.TryGet("ash-march", out var ashMarch), Is.True);
            Assert.That(ashMarch.CatalogIndex, Is.EqualTo(8), "ash-march owns the top catalog bit");

            var data = new CampaignData { PrologueDone = true };
            StageCatalog.MarkCleared(ref data, ashMarch, out var firstClear);
            Assert.That(firstClear, Is.True);
            Assert.That(data.ClearedMask, Is.EqualTo(1 << 8), "bit 8 must survive the valid-mask AND");

            CampaignStore.Save(in data);
            var loaded = CampaignStore.Load();
            Assert.That(loaded.ClearedMask, Is.EqualTo(1 << 8),
                "bit 8 must survive persistence (mask width regression)");
            Assert.That(StageCatalog.IsCleared(loaded, ashMarch), Is.True);

            // Garbage bits above the catalog width must still be scrubbed on load.
            var noisy = loaded;
            noisy.ClearedMask = (1 << 8) | (1 << 9) | (1 << 12);
            CampaignStore.Save(in noisy);
            var scrubbed = CampaignStore.Load();
            Assert.That(scrubbed.ClearedMask, Is.EqualTo(1 << 8),
                "bits past the 9-entry catalog never round trip");
        }

        // Gate: R8 — a legacy six-bit save (pre-cycle-2 blob) loads identically:
        // same six bits in, same six bits out, new stages simply read uncleared.
        [Test]
        public void Load_LegacySixBitMask_LoadsIdentically()
        {
            PlayerPrefs.SetString(CampaignKey,
                "{\"clearedMask\":63,\"equipment\":{\"weapon\":1,\"lantern\":1,\"cloak\":1}}");
            PlayerPrefs.Save();

            var data = CampaignStore.Load();
            Assert.That(data.ClearedMask, Is.EqualTo(0x3F), "all six legacy bits preserved");
            foreach (var id in new[] { "cinder-sluice", "ember-bastion", "ash-march" })
            {
                Assert.That(StageCatalog.TryGet(id, out var entry), Is.True);
                Assert.That(StageCatalog.IsCleared(data, entry), Is.False,
                    id + " must read uncleared from a legacy six-bit save");
            }

            // The legacy chain head for cycle-2: ash-verdict cleared -> sluice unlocks.
            data.PrologueDone = true;
            Assert.That(StageCatalog.TryGet("cinder-sluice", out var sluice), Is.True);
            Assert.That(StageCatalog.IsUnlocked(data, sluice), Is.True,
                "a fully-cleared legacy save must already unlock cinder-sluice");
        }

        private static void AssertCompositeHazards(string id, HazardConfig[] expected)
        {
            Assert.That(StageCatalog.TryGet(id, out var entry), Is.True);
            Assert.That(entry.HazardOverride, Is.Not.Null);
            Assert.That(entry.HazardOverride.Length, Is.EqualTo(expected.Length));

            // v1.2: bands and pylons carry live fields beyond kind/x/y/phase —
            // pin them ALL (the throne current's push IS the stage identity).
            for (var index = 0; index < expected.Length; index += 1)
                AssertHazardFieldsEqual(id, index, expected[index], entry.HazardOverride[index]);

            AssertRadialClearance(id, entry.HazardOverride);
        }

        /// <summary>
        /// Radial non-overlap for a placement table. Band kinds (current/wall) have
        /// no radial footprint and are exempt (the throne current is co-located with
        /// its altar BY DESIGN — the tide covers the channel). One more documented
        /// v1.2 exemption: altar↔pylon pairs may overlap — the "guarded altar" motif
        /// (verdict 960,540 vs altar r70 clears anyway; march 768,520 sits 84 px from
        /// the corridor altar: pylon bodies never block movement and altars are pure
        /// channel discs, so the overlap is mechanically inert and intended).
        /// v1.3 (pact tables only): a PACT-EXTRA vent (index ≥ <paramref name="pactExtraStart"/>)
        /// may colocate with a base altar or pillar — the "guard vent" motif
        /// (meta-fun-pass-spec M3): a periodic damage disc over a channel disc
        /// (well) or around a solid core (sluice) is mechanically well-defined
        /// and IS the pact bite. Scope is deliberately narrow: base tables and
        /// non-vent extras keep the full v1.2 rules.
        /// </summary>
        private static void AssertRadialClearance(string id, HazardConfig[] hazards, int pactExtraStart = int.MaxValue)
        {
            for (var left = 0; left < hazards.Length; left += 1)
            {
                for (var right = left + 1; right < hazards.Length; right += 1)
                {
                    var a = hazards[left];
                    var b = hazards[right];
                    if (IsBand(a.Kind) || IsBand(b.Kind)) continue;
                    if (IsGuardedAltarPair(a.Kind, b.Kind)) continue;
                    if (right >= pactExtraStart && IsPactGuardVentPair(a.Kind, b.Kind)) continue;
                    var x = a.X - b.X;
                    var y = a.Y - b.Y;
                    var distance = (float)Math.Sqrt(x * x + y * y);
                    Assert.That(distance, Is.GreaterThan(a.Radius + b.Radius),
                        id + " radial hazards must not overlap");
                    if (a.Kind == HazardKind.ObsidianPillar && b.Kind == HazardKind.ObsidianPillar)
                    {
                        Assert.That(distance, Is.GreaterThanOrEqualTo(a.Radius + b.Radius + 2f * CampaignSpec.PlayerPushRadius),
                            id + " pillars need two player push radii of separation");
                    }
                }
            }
        }

        /// <summary>v1.3 pact-extra guard-vent colocation (see AssertRadialClearance doc).</summary>
        private static bool IsPactGuardVentPair(HazardKind baseKind, HazardKind extraKind)
            => extraKind == HazardKind.EmberVent
            && (baseKind == HazardKind.RelicAltar || baseKind == HazardKind.ObsidianPillar);

        // --- v1.3 meta fun pass — Verdict Pact tables (meta-fun-pass-spec.md M3) --

        // Gate: R4/G2 (v1.3) — the pact contract agreed with MetaView (irc,
        // 2026-08-05): PactFor(id) is non-null for ALL 9 catalog ids; the pact
        // table's leading base.Length entries are field-equal to the EFFECTIVE
        // base table (entry.HazardOverride ?? frozen CampaignStages anchor) in
        // the same order; extras are strictly APPENDED; every extra reuses a
        // kind already present in that stage's base table (spec M3: 정체성 기믹
        // 강화 배치, 신규 종류 없음 — the per-stage kind row is pinned below).
        // The full pact table obeys the same radial-clearance rules as shipping
        // tables (band kinds + guarded-altar pairs exempt, pillars 2×push-radius),
        // plus the v1.3 pact-colocation exemption documented in
        // AssertRadialClearance.
        [Test]
        public void PactFor_AllNineStages_AppendIdentityExtrasOntoBaseTable()
        {
            // Final extras agreed with MetaView (irc 2026-08-05), pinned
            // content-exactly like every other shipping table. Three stages
            // reinforce with a VENT rather than their headline gimmick — all
            // deliberate: well "+1 altar-guard vent" (colocated on the NW
            // altar), sluice "+1 mid-lane vent" (annulus around the center
            // pillar), march south-strip denial vent (y796 keeps prop-010
            // dressing clearance d≈144 ≥ 140 and altar d=192 ≥ 160). Gallery
            // reinforces with PILLARS — a 5th ring vent is budget-impossible
            // (see header); 604±136 keeps pillar spacing ≥132 and edge gaps
            // 56 ≥ the 52 player diameter (squeezable, not sealed). Every
            // extra reuses a kind already present in that stage's base table.
            var expectedExtras = new System.Collections.Generic.Dictionary<string, HazardConfig[]>
            {
                ["cinder-span"]   = new[] { HazardConfig.Vent(768f, 604f, 0.6f) },
                ["ember-gallery"] = new[] { HazardConfig.Pillar(768f, 468f), HazardConfig.Pillar(768f, 740f) },
                ["abyss-chancel"] = new[] { HazardConfig.Pillar(900f, 500f) },
                ["witness-well"]  = new[] { HazardConfig.Vent(560f, 500f, 0.9f) },
                ["echo-throne"]   = new[] { HazardConfig.Current(768f, 740f, -120f, 3.3f) },
                ["ash-verdict"]   = new[] { HazardConfig.Pylon(576f, 668f) },
                ["cinder-sluice"] = new[] { HazardConfig.Vent(768f, 604f, 1.7f) },
                ["ember-bastion"] = new[] { HazardConfig.Pylon(620f, 720f) },
                ["ash-march"]     = new[] { HazardConfig.Vent(768f, 796f, 1.2f) },
            };

            Assert.That(expectedExtras.Count, Is.EqualTo(StageCatalog.Entries.Count),
                "every catalog stage needs an expected pact-extra row");

            for (var index = 0; index < StageCatalog.Entries.Count; index += 1)
            {
                var entry = StageCatalog.Entries[index];
                var pact = StageCatalog.PactFor(entry.Id);
                Assert.That(pact, Is.Not.Null, entry.Id + ": every stage must own a pact table");

                var baseTable = EffectiveBaseTable(in entry);
                var extras = expectedExtras[entry.Id];
                Assert.That(pact.Length, Is.EqualTo(baseTable.Length + extras.Length),
                    entry.Id + ": pact = base + identity extras, nothing else");
                Assert.That(pact, Is.Not.SameAs(baseTable),
                    entry.Id + ": the pact table must be its own array — the base table is FROZEN");

                for (var i = 0; i < baseTable.Length; i += 1)
                    AssertHazardFieldsEqual(entry.Id + " pact base prefix", i, baseTable[i], pact[i]);

                for (var i = 0; i < extras.Length; i += 1)
                {
                    AssertHazardFieldsEqual(entry.Id + " pact extra", baseTable.Length + i,
                        extras[i], pact[baseTable.Length + i]);
                    var kindExistsInBase = false;
                    for (var b = 0; b < baseTable.Length && !kindExistsInBase; b += 1)
                        kindExistsInBase = baseTable[b].Kind == extras[i].Kind;
                    Assert.That(kindExistsInBase, Is.True,
                        entry.Id + " pact extra " + i + " must reuse a kind already in the base table (신규 종류 없음)");
                }

                AssertRadialClearance(entry.Id + " (pact)", pact, pactExtraStart: baseTable.Length);
            }

            Assert.That(StageCatalog.PactFor("not-a-stage"), Is.Null,
                "unknown ids must not fabricate a pact table");
        }

        /// <summary>The table a NON-pact run actually rides (v1.2 shipping state).</summary>
        private static HazardConfig[] EffectiveBaseTable(in StageEntry entry)
        {
            if (entry.HazardOverride != null) return entry.HazardOverride;
            Assert.That(CampaignStages.TryGet(entry.SimAnchorId, 0, 0, 0, out var config), Is.True,
                entry.SimAnchorId + " must resolve a frozen sim anchor");
            return config.Hazards;
        }

        private static void AssertHazardFieldsEqual(string context, int index, in HazardConfig wanted, in HazardConfig actual)
        {
            Assert.That(actual.Kind, Is.EqualTo(wanted.Kind), context + " hazard " + index + " kind");
            Assert.That(actual.X, Is.EqualTo(wanted.X), context + " hazard " + index + " X");
            Assert.That(actual.Y, Is.EqualTo(wanted.Y), context + " hazard " + index + " Y");
            Assert.That(actual.Radius, Is.EqualTo(wanted.Radius), context + " hazard " + index + " radius");
            Assert.That(actual.Phase, Is.EqualTo(wanted.Phase), context + " hazard " + index + " phase");
            Assert.That(actual.PushX, Is.EqualTo(wanted.PushX), context + " hazard " + index + " pushX");
            Assert.That(actual.PushY, Is.EqualTo(wanted.PushY), context + " hazard " + index + " pushY");
            Assert.That(actual.HalfW, Is.EqualTo(wanted.HalfW), context + " hazard " + index + " halfW");
            Assert.That(actual.HalfH, Is.EqualTo(wanted.HalfH), context + " hazard " + index + " halfH");
            Assert.That(actual.Hp, Is.EqualTo(wanted.Hp), context + " hazard " + index + " hp");
        }
        private static bool IsGuardedAltarPair(HazardKind a, HazardKind b)
            => (a == HazardKind.RelicAltar && b == HazardKind.EmberPylon)
            || (a == HazardKind.EmberPylon && b == HazardKind.RelicAltar);
    }
}
