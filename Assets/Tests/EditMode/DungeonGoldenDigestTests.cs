// Golden digest regression (cycle-2, R1-R3 + D5) — numeric truth:
// _workspace/current/qa/golden-digests-cycle2.md.
//
// Goldens were recorded pre/post cycle-2 with a standalone dotnet 8 harness
// compiling these same Sim sources; the pre/post rows for the 12 existing lanes
// matched byte-for-byte UNDER DOTNET, proving AMENDMENT #5 is additive
// (_workspace/current/qa/golden-digests-cycle2.md). The literals BELOW are the
// UNITY-runtime recording (2026-08-05): dotnet and Unity digests are NOT
// bit-comparable — low-order X/Y float drift, ~4 ULP; ints matched across both
// runtimes on all 15 rows [OBSERVED]. Likely ARM64 FMA contraction [INFERENCE —
// unmeasured; per CLAUDE.md §4 do not cite as fact]. Unity is the shipping
// truth. If these drift, re-pin from the assert failure message (it prints the
// full actual row) and record the divergence in qa/gate-measurements.md.
//
// Ints assert exactly; floats are compared through their shortest round-trip
// ("R") form, which is bit-exact within the Unity runtime — the sim is a
// fixed-step deterministic core and any drift is a defect, not noise.
using System.Globalization;
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class DungeonGoldenDigestTests
    {
        const int GoldenTicks = 1800;

        // ---- row plumbing -------------------------------------------------

        static string Row(string label, CinderSim sim)
        {
            var digest = sim.Digest;
            var reason = string.IsNullOrEmpty(digest.Reason) ? "(running)" : digest.Reason;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}",
                label, digest.Score, digest.Wave, digest.Kills, digest.Relics,
                digest.HealthRemaining.ToString("R", CultureInfo.InvariantCulture),
                reason,
                sim.Player.X.ToString("R", CultureInfo.InvariantCulture),
                sim.Player.Y.ToString("R", CultureInfo.InvariantCulture));
        }

        static CinderSim RunKiter(CinderSim sim)
        {
            // Shared kiter — the single BotInput in CampaignSimTests (never fork it:
            // the goldens were recorded against this exact byte pattern).
            for (var t = 0; t < GoldenTicks; t++) sim.Tick(CampaignSimTests.BotInput(sim));
            return sim;
        }

        static void AssertRow(string expected, CinderSim sim)
        {
            var label = expected.Substring(0, expected.IndexOf('|'));
            var actual = Row(label, sim);
            // On mismatch the message carries the full actual row for cheap re-pinning.
            Assert.That(actual, Is.EqualTo(expected),
                $"golden drift on '{label}' — actual row (paste into goldens to re-pin):\n{actual}");
        }

        // ---- lanes ----------------------------------------------------------

        /// <summary>The 2/1/3 hack dungeon lane every R1/new-stage golden rides on.</summary>
        static HackConfig Dungeon213(string anchorId)
        {
            Assert.IsTrue(
                HackConfig.TryDungeon(anchorId, default, EquipTiers.Of(2, 1, 3), null, 0, out var config),
                $"unknown anchor {anchorId}");
            return config;
        }

        /// <summary>
        /// A logical catalog stage exactly as GameDirector.StartDungeon builds it:
        /// TryDungeon on the frozen SimAnchorId, then the catalog HazardOverride (when
        /// present) replaces the anchor placement table.
        /// </summary>
        static HackConfig LogicalStage(string catalogId)
        {
            Assert.IsTrue(StageCatalog.TryGet(catalogId, out var entry), $"unknown catalog id {catalogId}");
            var config = Dungeon213(entry.SimAnchorId);
            if (entry.HazardOverride != null) config.Hazards = entry.HazardOverride;
            return config;
        }

        // ---- R2/R3: mode regression ----------------------------------------

        // Gate: R2 — GameMode.Arena golden (kiter, 1800 ticks) is byte-frozen.
        [Test]
        public void Golden_ArenaHackLane_IsUnchanged()
        {
            var config = HackConfig.Arena();
            AssertRow("arena-hack|3700|4|15|2|82|(running)|1035.27368|717.8638",
                RunKiter(new CinderSim(in config)));
        }

        // Gate: R2 — the parameterless frozen-arena constructor matches the hack lane.
        [Test]
        public void Golden_FrozenArenaConstructor_IsUnchanged()
        {
            AssertRow("arena-frozen|3700|4|15|2|82|(running)|1035.27368|717.8638",
                RunKiter(new CinderSim()));
        }

        // Gate: R3 — prologue golden (kiter parks after wave-2 spawns stop at 1800).
        [Test]
        public void Golden_Prologue_IsUnchanged()
        {
            var config = HackConfig.Prologue();
            AssertRow("prologue|1650|2|9|1|36|(running)|930.125|435.398376",
                RunKiter(new CinderSim(in config)));
        }

        // ---- R1: the six pre-cycle-2 logical stages --------------------------

        // Gate: R1 — six logical catalog stages @2/1/3, HazardOverride applied like
        // GameDirector.StartDungeon; digests must stay byte-identical to pre-cycle-2.
        [Test]
        public void Golden_SixLogicalStages_AreUnchanged()
        {
            var rows = new[]
            {
                "cinder-span|4200|3|15|4|142|(running)|588.852356|763.74",
                "ember-gallery|3150|3|14|1|136|(running)|719.403564|831.701843",
                "abyss-chancel|3150|3|14|1|136|(running)|719.3025|831.649231",
                "witness-well|3400|3|14|2|136|(running)|459.748383|696.531555",
                "echo-throne|4200|3|15|4|142|(running)|588.852356|763.74",
                "ash-verdict|4200|3|15|4|142|(running)|588.852356|763.74",
            };
            foreach (var expected in rows)
            {
                var id = expected.Substring(0, expected.IndexOf('|'));
                var config = LogicalStage(id);
                AssertRow(expected, RunKiter(new CinderSim(in config)));
            }
        }

        // ---- R1: classic campaign lane ---------------------------------------

        // Gate: R1 — classic CinderSim(in CampaignConfig) lane, three anchors @2/1/3.
        [Test]
        public void Golden_ClassicCampaignAnchors_AreUnchanged()
        {
            var rows = new[]
            {
                "classic-cinder-span|3700|4|15|2|99|(running)|1035.27368|717.8638",
                "classic-abyss-chancel|3700|4|15|2|115|(running)|1035.92444|717.524963",
                "classic-echo-throne|3700|4|15|2|106|(running)|1035.27368|717.8638",
            };
            foreach (var expected in rows)
            {
                var id = expected.Substring("classic-".Length, expected.IndexOf('|') - "classic-".Length);
                Assert.IsTrue(CampaignStages.TryGet(id, 2, 1, 3, out var config), id);
                AssertRow(expected, RunKiter(new CinderSim(in config)));
            }
        }

        // ---- D5: the three cycle-2 stages --------------------------------------

        // Gate: D5 — first-recorded goldens for the three new anchors @2/1/3. The
        // ash-march row legitimately equals echo-throne's: within 30 s the kiter never
        // enters the wall band and the shared numeric fields match; wall behaviour is
        // pinned separately by CampaignSimTests.AshWall_TimetableAndTicks.
        [Test]
        public void Golden_NewDungeonStages_MatchFirstRecording()
        {
            var rows = new[]
            {
                "cinder-sluice|4200|4|15|4|142|(running)|979.537354|631.2189",
                "ember-bastion|3150|3|14|1|142|(running)|1217.31824|620.4512",
                "ash-march|4200|3|15|4|142|(running)|588.852356|763.74",
            };
            foreach (var expected in rows)
            {
                var id = expected.Substring(0, expected.IndexOf('|'));
                var config = Dungeon213(id);
                AssertRow(expected, RunKiter(new CinderSim(in config)));
            }
        }
    }
}
