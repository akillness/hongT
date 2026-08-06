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
// REVISION v1.1 (2026-08-05, Stage-2 gimmick retune): the 12 pre-existing rows
// are UNTOUCHED Unity recordings and must stay byte-identical (R1-R3). The 3
// new-stage rows in Golden_NewDungeonStages_MatchFirstRecording were re-pinned
// from the v1.1 sim on the DOTNET harness — see that test's comment for the
// main-lane Unity re-pin protocol.
//
// REVISION v1.2 (2026-08-05, campaign fun pass) — THE GOLDEN SPLIT:
// docs/SIM_SPEC_DUNGEONS.md REVISION v1.2 changed the placement contract on
// purpose (user-requested stage-identity pass), so the golden set now has two
// legally different halves:
//   MUST NOT MOVE (invariant safety net — movement here is a real regression):
//     arena-hack · arena-frozen · prologue · cinder-span · abyss-chancel ·
//     cinder-sluice · ember-bastion · classic 3 (cinder-span/abyss-chancel/
//     echo-throne on the CinderSim(in CampaignConfig) lane — classic anchors
//     ignore catalog overrides by construction).
//   EXPECTED TO MOVE (v1.2 products, re-pinned in Golden_FunPassStages_*):
//     ember-gallery · witness-well · echo-throne · ash-verdict (catalog
//     override tables) · ash-march (anchor pylon 768,520).
// The invariant rows below are byte-untouched from the pre-v1.2 recordings; do
// NOT re-pin them to make a red run green — investigate instead.
//
// REVISION v1.3 (2026-08-05, meta fun pass) — NO new rows, NO moved rows.
// The Verdict Pact (design/meta-fun-pass-spec.md M3) is opt-in VIEW routing:
// GameDirector composes a pact run as HackConfig + StageCatalog.PactFor(id)
// hazards, exactly like the v1.2 catalog overrides. Default (non-pact) runs
// ride the same tables as before, so every row below must stay byte-green.
// Pact runs get no goldens by design — the pact tables themselves are pinned
// content-exactly by StageCatalogTests (catalog contract), their telegraph
// budget by CampaignSimTests.Telegraph_PactCensusUnderBudget, and their
// determinism by CampaignSimTests.PactSluice_SameConfigSameInputs_IdenticalDigests_AndBotSurvivable.
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
            AssertRow("arena-hack|3700|4|15|2|82|(running)|1035.27319|717.864",
                RunKiter(new CinderSim(in config)));
        }

        // Gate: R2 — the parameterless frozen-arena constructor matches the hack lane.
        [Test]
        public void Golden_FrozenArenaConstructor_IsUnchanged()
        {
            AssertRow("arena-frozen|3700|4|15|2|82|(running)|1035.27319|717.864",
                RunKiter(new CinderSim()));
        }

        // Gate: R3 — prologue golden (kiter parks after wave-2 spawns stop at 1800).
        [Test]
        public void Golden_Prologue_IsUnchanged()
        {
            var config = HackConfig.Prologue();
            AssertRow("prologue|1650|2|9|1|36|(running)|930.1258|435.3988",
                RunKiter(new CinderSim(in config)));
        }

        // ---- R1: the pre-cycle-2 logical stages that v1.2 leaves alone --------

        // Gate: R1 — invariant logical catalog stages @2/1/3, HazardOverride applied
        // like GameDirector.StartDungeon; digests must stay byte-identical to
        // pre-cycle-2 (cinder-span/abyss-chancel are override-null anchors; the four
        // fun-pass stages moved to Golden_FunPassStages_MatchV12Recording).
        [Test]
        public void Golden_InvariantLogicalStages_AreUnchanged()
        {
            var rows = new[]
            {
                "cinder-span|3700|4|15|2|142|(running)|931.322|525.9695",
                "abyss-chancel|2000|3|11|0|136|(running)|805.781433|783.829041",
            };
            foreach (var expected in rows)
            {
                var id = expected.Substring(0, expected.IndexOf('|'));
                var config = LogicalStage(id);
                AssertRow(expected, RunKiter(new CinderSim(in config)));
            }
        }

        // ---- v1.2: the fun-pass stages (EXPECTED movers) ----------------------

        // Gate: G2/D5 (v1.2) — the five stages the fun pass legally moved, exactly
        // as GameDirector runs them (catalog override on the anchor lane; ash-march
        // is its own anchor). Rows recorded on the standalone dotnet 8 harness
        // against the v1.2 tables (2026-08-05). Ints are runtime-transferable;
        // trailing X/Y floats carry the known ~4 ULP dotnet↔Unity drift, so the
        // FIRST Unity EditMode run may fail here — MAIN LANE: re-pin the float
        // fields from the assert failure message and record the divergence in
        // qa/gate-measurements.md. Two caveats from the recording run:
        //  · ember-gallery/witness-well: BOTH rows are byte-identical to the OLD
        //    Unity ember-gallery recording — the kiter never enters any ring vent
        //    or off-centre altar, so the v1.2 trajectory equals v1.1's (verified
        //    dotnet v1.1 vs v1.2 byte-equal). The Unity floats below are therefore
        //    the PROVEN v1.1 Unity literals, expected to pass unchanged.
        //  · ash-march ends at hp 8 — the run survives by a hair. If Unity float
        //    drift kills the kiter before tick 1800, the INT fields move too:
        //    re-pin the whole row (that outcome is still the shipped v1.2 product,
        //    not a regression) and note it in qa/gate-measurements.md.
        [Test]
        public void Golden_FunPassStages_MatchV12Recording()
        {
            var rows = new[]
            {
                "ember-gallery|3100|3|13|2|136|(running)|466.405334|754.7759",
                "witness-well|3100|3|13|2|136|(running)|466.405334|754.7759",
                "echo-throne|2250|3|11|1|136|(running)|1210.65125|708.4477",
                "ash-verdict|3700|4|15|2|142|(running)|906.255249|489.832947",
                "ash-march|3700|4|15|2|52|(running)|908.362732|625.6049",
            };
            foreach (var expected in rows)
            {
                var id = expected.Substring(0, expected.IndexOf('|'));
                var config = id == "ash-march" ? Dungeon213(id) : LogicalStage(id);
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
                "classic-cinder-span|3700|4|15|2|99|(running)|1035.27319|717.864",
                "classic-abyss-chancel|3700|4|15|2|115|(running)|1035.92444|717.524963",
                "classic-echo-throne|3700|4|15|2|106|(running)|1035.27319|717.864",
            };
            foreach (var expected in rows)
            {
                var id = expected.Substring("classic-".Length, expected.IndexOf('|') - "classic-".Length);
                Assert.IsTrue(CampaignStages.TryGet(id, 2, 1, 3, out var config), id);
                AssertRow(expected, RunKiter(new CinderSim(in config)));
            }
        }

        // ---- D5: the cycle-2 anchors v1.2 leaves alone --------------------------

        // Gate: D5 — v1.1 retune goldens for the sluice/bastion anchors @2/1/3
        // (REVISION v1.1). v1.2 moved ash-march (finale pylon) OUT of this test —
        // its row now lives in Golden_FunPassStages_MatchV12Recording; these two
        // must stay byte-identical (invariant safety net, spec v1.2 잔여 불변).
        // The rows below were recorded on the standalone dotnet 8 harness against
        // the v1.1 sim sources (2026-08-05). Ints are runtime-transferable; the
        // trailing floats carry the known dotnet↔Unity low-order drift (~4 ULP), so
        // the FIRST Unity EditMode run may fail exactly here — MAIN LANE: re-pin the
        // float fields from the assert failure message (it prints the full actual
        // row) and record the divergence in qa/gate-measurements.md.
        [Test]
        public void Golden_NewDungeonStages_MatchFirstRecording()
        {
            var rows = new[]
            {
                "cinder-sluice|2600|3|13|0|136|(running)|961.427063|611.6681",
                "ember-bastion|1950|3|10|1|136|(running)|1032.24353|524.6603",
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
