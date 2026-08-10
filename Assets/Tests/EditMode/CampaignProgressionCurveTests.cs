// The campaign difficulty curve: does a later stage actually field a harder
// wave, and does the arena stay exactly where it was?
//
// This exists because the sag it guards was invisible to every other test.
// AMENDMENT #13's wave budget grows with the WAVE and with the DDA band, and
// both of those reset at the start of every run — so nothing in the budget
// ever knew which stage it was in. Player power does not reset: meta stats
// climb to 10 and equipment to rank 5 across a campaign.
//
// MEASURED 2026-08-10, fully-built player against the stage-blind budget:
//     player  damage 1.69x, health 2.20x  -> 3.72x
//     enemies s0 -> s8                    -> 2.49x
//     relative difficulty at the last stage -> 0.67x of the first
// The campaign got EASIER as it went, monotonically. The DDA band cannot fix
// that: at its +2 ceiling it multiplies EVERY stage by the same 1.25, moving
// the curve without changing its slope (0.67 -> 0.80, still sagging).
//
// The two questions below are the ones a spec-level unit test cannot answer:
// a constant can be right while the call site ignores it.
using CinderCourt.Sim;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class CampaignProgressionCurveTests
    {
        static CinderSim DungeonSim(string stageId)
        {
            Assert.That(HackConfig.TryDungeon(stageId, default, default, (string)null, 0, out var config),
                Is.True, $"unknown dungeon stage {stageId}");
            // Everything: the shipping arming (GameView.cs:91). With None the
            // budget path is skipped entirely and this fixture measures nothing.
            return new CinderSim(in config, DungeonProgressionConfig.Everything);
        }

        /// <summary>Runs to the first budgeted wave and reports what it bought.</summary>
        static int FirstWaveBudget(CinderSim sim)
        {
            for (var tick = 0; tick < 600; tick++)
            {
                sim.Tick(default);
                if (sim.WaveBudget > 0) return sim.WaveBudget;
            }
            return 0;
        }

        [Test]
        public void LaterCampaignAnchorsFieldABiggerBudgetThanTheFirst()
        {
            var first = FirstWaveBudget(DungeonSim("cinder-span"));      // anchor 0
            var last = FirstWaveBudget(DungeonSim("ash-march"));         // anchor 5

            Assert.That(first, Is.GreaterThan(0),
                "the first stage published no budget — DungeonProgressionConfig is "
                + "not armed and this fixture is measuring nothing");
            Assert.That(last, Is.GreaterThan(first),
                $"ash-march (anchor 5) bought a wave worth {last} against "
                + $"cinder-span's {first}. The stage term is not reaching the sim: "
                + "a later stage must cost the player more, or the campaign gets "
                + "easier as the player gets stronger");

            // 90 permille per anchor over 5 anchors = 1.45x, and integer
            // division can only lose a point or two of that.
            var ratio = last / (float)first;
            Assert.That(ratio, Is.EqualTo(1.45f).Within(0.03f),
                $"stage ramp measured {ratio:F3}x across five anchors; the spec's "
                + $"{WaveBudgetSpec.StageProgressionPermille} permille per "
                + "anchor says 1.45x");
        }

        [Test]
        public void EveryAnchorStepIsAnIncrease()
        {
            // One stage per anchor, in anchor order. Pairs share an anchor, so
            // this is the full set of DISTINCT steps the campaign can make.
            var byAnchor = new[]
            {
                "cinder-span", "abyss-chancel", "echo-throne",
                "cinder-sluice", "ember-bastion", "ash-march",
            };
            var previous = 0;
            var report = new System.Text.StringBuilder();
            foreach (var stage in byAnchor)
            {
                var budget = FirstWaveBudget(DungeonSim(stage));
                report.Append($"  {stage,-16}{budget,6}\n");
                Assert.That(budget, Is.GreaterThan(previous),
                    $"{stage} bought {budget} after {previous} — the campaign "
                    + "curve went flat or backwards here\n" + report);
                previous = budget;
            }
            TestContext.WriteLine("[campaign wave-1 budget by anchor]\n" + report);
        }

        /// <summary>The arena is not a campaign and must not have moved. This is
        /// the other half of the change: a stage term that leaked into the arena
        /// would be a balance change to a frozen mode.</summary>
        [Test]
        public void ArenaBudgetIsUnchangedByTheCampaignTerm()
        {
            for (var wave = 1; wave <= 12; wave++)
            {
                var plain = WaveBudgetSpec.EffectiveBudget(wave, 0);
                var stageZero = WaveBudgetSpec.EffectiveBudget(wave, 0, 0);
                Assert.That(stageZero, Is.EqualTo(plain),
                    $"wave {wave}: stage 0 must be byte-identical to the "
                    + "pre-amendment budget, or the arena moved");
            }
        }
    }
}
