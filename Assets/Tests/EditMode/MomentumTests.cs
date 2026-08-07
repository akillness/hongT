// FROZEN CONTRACT AMENDMENT #9 gates — the momentum gauge
// (docs/SIM_SPEC_HACKSLASH.md "Frozen Contract Amendment #9", proof map A9.7).
//
// Every bound below was MEASURED against this build with a pure-C# harness over
// Assets/Scripts/Sim/** before it was asserted. Four measured facts shape the file:
//
//   * A scripted wander barely lands a melee hit, so the gauge can only be exercised
//     by an input that actually WALKS AT the nearest living enemy (see Hunt). The
//     first probe used a fixed MoveX and saw 2 hits in 1800 ticks — nothing to gate.
//   * Grace is only measurable in a window with no gain AND no PlayerDamaged in it.
//     Inside such a window the gauge holds for exactly 98 ticks before the first
//     decrease: 1.6 s of grace is consumed over 97 ticks (repeated float subtraction
//     of 1/60 never lands exactly on 0) and the drop is visible on the 98th.
//   * A gain that is clipped by the 100 ceiling is invisible to a value comparison,
//     so "did the gauge gain this tick" must be read off the EnemyHit event, which in
//     a no-companion dungeon with no skill input IS a melee hit.
//   * Momentum only ever rises from MELEE, so a skills-only run sits at 0 and proves
//     nothing about A9.6. The skill ledger below therefore builds real momentum with
//     melee first and only then checks that pulse ticks stayed unscaled.
using System;
using System.Collections.Generic;
using CinderCourt.Sim;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class MomentumTests
    {
        private const float Tolerance = 1e-4f;
        private const float DamageTolerance = 1e-2f;
        private const int ScriptTicks = 1800;

        // The A9 contract, restated as literals ON PURPOSE. A gate that reads its bound
        // out of HackSpec moves with the constant it is meant to pin, so retuning the sim
        // would silently retune its own gate (CLAUDE.md §2 — the numbers are the gate).
        // Momentum_ContractMatchesHackSpec is the single place the two are tied together.
        private const float Max = 100f;
        private const float PerHit = 9f;
        private const float PerKill = 14f;
        private const float GraceSeconds = 1.6f;
        private const float DecayPerSecond = 12f;
        private const float HurtPenalty = 25f;
        private static readonly float[] Thresholds = { 0f, 30f, 60f, 90f };
        private static readonly float[] Multipliers = { 1f, 1.08f, 1.18f, 1.30f };

        /// <summary>Measured: inside a clean window the gauge holds this many ticks before
        /// its first decrease. 96 ticks is the exact-arithmetic floor (1.6 s x 60).</summary>
        private const int GraceTicks = 98;

        // --- helpers ---------------------------------------------------------

        private static HackConfig DungeonScalar(string companionId)
        {
            string[] slots = companionId == null ? Array.Empty<string>() : new[] { companionId };
            Assert.That(
                HackConfig.TryDungeon(
                    CampaignStages.CinderSpan,
                    MetaStats.Of(0, 0, 0),
                    EquipTiers.Of(0, 0, 0),
                    slots,
                    0,
                    out HackConfig config),
                Is.True,
                "cinder-span must resolve");
            return config;
        }

        private static float Iso(float fromX, float fromY, float toX, float toY)
        {
            float deltaX = toX - fromX;
            float deltaY = (toY - fromY) * SimConfig.IsoY;
            return MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        /// <summary>Walk at the nearest living enemy and swing on a fixed cadence. This is
        /// the ONLY scripted input that exercises the gauge — see the header note.</summary>
        private static SimInput Hunt(CinderSim sim, int tick, int cadence)
        {
            var input = default(SimInput);
            PlayerState player = sim.Player;
            float bestX = 0f, bestY = 0f, best = float.MaxValue;
            IReadOnlyList<EnemyState> enemies = sim.Enemies;
            for (int index = 0; index < enemies.Count; index += 1)
            {
                EnemyState enemy = enemies[index];
                if (enemy.Dead)
                {
                    continue;
                }
                float distance = Iso(player.X, player.Y, enemy.X, enemy.Y);
                if (distance < best)
                {
                    best = distance;
                    bestX = enemy.X;
                    bestY = enemy.Y;
                }
            }
            if (best < float.MaxValue)
            {
                float deltaX = bestX - player.X;
                float deltaY = bestY - player.Y;
                float length = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
                if (length > 1f)
                {
                    input.MoveX = deltaX / length;
                    input.MoveY = deltaY / length;
                }
            }
            input.AttackQueued = tick % cadence == 0;
            return input;
        }

        /// <summary>The full-kit script Amendments #7/#8 froze their digests against.</summary>
        private static SimInput HackScriptInput(int tick)
        {
            var input = default(SimInput);
            input.MoveX = tick / 120 % 2 == 0 ? 1f : -1f;
            input.MoveY = tick / 200 % 2 == 0 ? 0.5f : -0.5f;
            input.AttackQueued = tick % 30 == 0;
            input.NovaQueued = tick % 400 == 0;
            input.WardQueued = tick % 550 == 0;
            input.DashQueued = tick % 130 == 0;
            input.BoltQueued = tick % 210 == 0;
            input.PulseQueued = tick % 170 == 0;
            return input;
        }

        /// <summary>Player melee damage at the CURRENT level with no growth points and no
        /// extraction bonus — the §5 curve a scripted run produces.</summary>
        private static float PlayerDamageAt(in HackConfig config, int level)
        {
            return config.PlayerDamage * (1f + HackSpec.LevelDamageBonus * (level - 1));
        }

        private static Dictionary<int, float> SampleHealth(CinderSim sim)
        {
            var health = new Dictionary<int, float>();
            IReadOnlyList<EnemyState> enemies = sim.Enemies;
            for (int index = 0; index < enemies.Count; index += 1)
            {
                EnemyState enemy = enemies[index];
                if (!enemy.Dead)
                {
                    health[enemy.Id] = enemy.Health;
                }
            }
            return health;
        }

        private static Dictionary<int, EnemyVisual> SampleVisual(CinderSim sim)
        {
            var visual = new Dictionary<int, EnemyVisual>();
            IReadOnlyList<EnemyState> enemies = sim.Enemies;
            for (int index = 0; index < enemies.Count; index += 1)
            {
                EnemyState enemy = enemies[index];
                if (!enemy.Dead)
                {
                    visual[enemy.Id] = enemy.Visual;
                }
            }
            return visual;
        }

        // --- 1 & 2. the table itself -----------------------------------------

        [Test]
        public void Momentum_TierTableIsAscendingAndOpensNeutral()
        {
            Assert.That(Thresholds.Length, Is.EqualTo(Multipliers.Length),
                "every tier needs exactly one threshold and one multiplier");
            Assert.That(Thresholds.Length, Is.EqualTo(4), "A9.4 froze four tiers");

            // Tier 0 must start at 0 and multiply by exactly 1: that pair is what makes a
            // run that never builds momentum bit-identical to the pre-amendment sim.
            Assert.That(Thresholds[0], Is.EqualTo(0f), "tier 0 must start at an empty gauge");
            Assert.That(Multipliers[0], Is.EqualTo(1f), "tier 0 must be exactly neutral");

            for (int tier = 1; tier < Thresholds.Length; tier += 1)
            {
                Assert.That(Thresholds[tier], Is.GreaterThan(Thresholds[tier - 1]),
                    $"threshold {tier} must sit above tier {tier - 1}");
                Assert.That(Multipliers[tier], Is.GreaterThan(Multipliers[tier - 1]),
                    $"tier {tier} must reward more than tier {tier - 1} — otherwise the gauge is decoration");
            }

            Assert.That(Thresholds[Thresholds.Length - 1], Is.LessThanOrEqualTo(Max),
                "the top tier must be reachable inside the gauge's own ceiling");
        }

        [Test]
        public void Momentum_ContractMatchesHackSpec()
        {
            // The ONE place the literals above are tied back to the sim's constants.
            Assert.That(HackSpec.MomentumMax, Is.EqualTo(Max), "gauge ceiling");
            Assert.That(HackSpec.MomentumPerHit, Is.EqualTo(PerHit), "gain per struck enemy");
            Assert.That(HackSpec.MomentumPerKill, Is.EqualTo(PerKill), "kill bonus");
            Assert.That(HackSpec.MomentumGraceSeconds, Is.EqualTo(GraceSeconds), "grace window");
            Assert.That(HackSpec.MomentumDecayPerSecond, Is.EqualTo(DecayPerSecond), "decay rate");
            Assert.That(HackSpec.MomentumHurtPenalty, Is.EqualTo(HurtPenalty), "hurt penalty");
            Assert.That(HackSpec.MomentumTierThresholds, Is.EqualTo(Thresholds).AsCollection, "thresholds");
            Assert.That(HackSpec.MomentumTierDamageMul, Is.EqualTo(Multipliers).AsCollection, "multipliers");
            Assert.That(HackSpec.MomentumTierCount, Is.EqualTo(Thresholds.Length), "tier count");
        }

        [Test]
        public void Momentum_TierOfIsTotalAndLandsExactlyOnEveryBoundary()
        {
            // A threshold is INCLUSIVE, and the function must be total: nothing the sim can
            // hold — including values it should never produce — may fall off the table.
            Assert.That(HackSpec.MomentumTierOf(-1000f), Is.EqualTo(0), "below empty clamps to tier 0");
            Assert.That(HackSpec.MomentumTierOf(0f), Is.EqualTo(0));
            Assert.That(HackSpec.MomentumTierOf(29.999f), Is.EqualTo(0), "just short of 30 is still tier 0");
            Assert.That(HackSpec.MomentumTierOf(30f), Is.EqualTo(1), "30 is inclusive");
            Assert.That(HackSpec.MomentumTierOf(59.999f), Is.EqualTo(1));
            Assert.That(HackSpec.MomentumTierOf(60f), Is.EqualTo(2), "60 is inclusive");
            Assert.That(HackSpec.MomentumTierOf(89.999f), Is.EqualTo(2));
            Assert.That(HackSpec.MomentumTierOf(90f), Is.EqualTo(3), "90 is inclusive");
            Assert.That(HackSpec.MomentumTierOf(100f), Is.EqualTo(3));
            Assert.That(HackSpec.MomentumTierOf(1e9f), Is.EqualTo(3), "above the ceiling clamps to the top tier");

            for (int tier = 0; tier < Thresholds.Length; tier += 1)
            {
                Assert.That(HackSpec.MomentumDamageMulOf(Thresholds[tier]), Is.EqualTo(Multipliers[tier]),
                    $"the multiplier at threshold {tier} must be the tier's own");
            }
        }

        // --- 3 & 4. A9.1 scope: what must NOT move ---------------------------

        [Test]
        public void Momentum_ArenaAndPrologueNeverBuildItAndKeepTheirFrozenDigests()
        {
            // Same literals Amendments #7 and #8 froze. A9 multiplies player melee damage,
            // so if it ever leaked out of the dungeon these would be the first to move.
            AssertNoMomentum(HackConfig.Arena(), 6600, 4, 21, 4, 90f, string.Empty, "arena");
            AssertNoMomentum(HackConfig.Prologue(), 5500, 3, 18, 6, 73f, "prologue-clear", "prologue");
        }

        private static void AssertNoMomentum(
            HackConfig config,
            int score,
            int wave,
            int kills,
            int relics,
            float healthRemaining,
            string reason,
            string label)
        {
            var sim = new CinderSim(in config);
            for (int tick = 0; tick < ScriptTicks; tick += 1)
            {
                SimInput input = HackScriptInput(tick);
                sim.Tick(in input);
                Assert.That(sim.Momentum, Is.EqualTo(0f), $"{label} must never build momentum (tick {tick})");
                Assert.That(sim.Events & SimEvents.MomentumTierUp, Is.EqualTo(SimEvents.None),
                    $"{label} must never raise a tier-up cue (tick {tick})");
            }

            Assert.That(sim.MomentumTier, Is.EqualTo(0), $"{label} must stay at tier 0");
            Assert.That(sim.MomentumDamageMultiplier, Is.EqualTo(1f), $"{label} must stay neutral");

            RunDigest digest = sim.Digest;
            Assert.That(digest.Score, Is.EqualTo(score), $"A9 must not move the {label} score");
            Assert.That(digest.Wave, Is.EqualTo(wave), $"A9 must not move the {label} wave");
            Assert.That(digest.Kills, Is.EqualTo(kills), $"A9 must not move the {label} kills");
            Assert.That(digest.Relics, Is.EqualTo(relics), $"A9 must not move the {label} relics");
            Assert.That(digest.HealthRemaining, Is.EqualTo(healthRemaining).Within(Tolerance),
                $"A9 must not move the {label} health");
            Assert.That(digest.Reason, Is.EqualTo(reason), $"A9 must not move the {label} end reason");
        }

        [Test]
        public void Momentum_DungeonDigestSitsAtTheAmendedValue()
        {
            // The dungeon digest DOES move — that is the amendment. Re-pinned here so the
            // move stays a decision rather than drift: A9 buffs melee, so the same script
            // reaches the same kill/relic count but trades differently on the way there.
            var config = DungeonScalar(null);
            var sim = new CinderSim(in config);
            for (int tick = 0; tick < ScriptTicks; tick += 1)
            {
                SimInput input = HackScriptInput(tick);
                sim.Tick(in input);
            }

            RunDigest digest = sim.Digest;
            Assert.That(digest.Score, Is.EqualTo(3350), "dungeon score");
            Assert.That(digest.Wave, Is.EqualTo(3), "dungeon wave");
            Assert.That(digest.Kills, Is.EqualTo(13), "dungeon kills");
            Assert.That(digest.Relics, Is.EqualTo(3), "dungeon relics");
            Assert.That(digest.HealthRemaining, Is.EqualTo(71.5f).Within(Tolerance), "dungeon health");
            Assert.That(digest.Reason, Is.EqualTo(string.Empty), "dungeon end reason");
        }

        [Test]
        public void Momentum_OpensEmptyAndIsNotFedBySkills()
        {
            var config = DungeonScalar(null);
            var sim = new CinderSim(in config);
            Assert.That(sim.Momentum, Is.EqualTo(0f), "a run must open on an empty gauge");
            Assert.That(sim.MomentumTier, Is.EqualTo(0));
            Assert.That(sim.MomentumDamageMultiplier, Is.EqualTo(1f));

            // Skills only: bolt/pulse/nova land real damage, and none of it is momentum.
            bool sawEnemyHit = false;
            for (int tick = 0; tick < 1200; tick += 1)
            {
                SimInput input = Hunt(sim, tick, int.MaxValue);
                input.AttackQueued = false;
                input.BoltQueued = tick % 60 == 0;
                input.PulseQueued = tick % 45 == 0;
                input.NovaQueued = tick % 200 == 0;
                sim.Tick(in input);
                sawEnemyHit |= (sim.Events & SimEvents.EnemyHit) != 0;
                Assert.That(sim.Momentum, Is.EqualTo(0f),
                    $"A9.2: only MELEE feeds the gauge, but it moved on tick {tick}");
            }

            Assert.That(sawEnemyHit, Is.True,
                "scenario starved: the skills never connected, so this proves nothing");
        }

        // --- 5. A9.2 filling --------------------------------------------------

        [Test]
        public void Momentum_FillsFromMeleeHitsAndStopsAtTheCeiling()
        {
            var config = DungeonScalar(null);
            var sim = new CinderSim(in config);
            int firstGainTick = -1;
            float peak = 0f;
            int reachedCeilingAt = -1;

            for (int tick = 0; tick < 1200; tick += 1)
            {
                float before = sim.Momentum;
                SimInput input = Hunt(sim, tick, 12);
                sim.Tick(in input);
                if (firstGainTick < 0 && sim.Momentum > before)
                {
                    firstGainTick = tick;
                    // A9.2: the first gain of a run is one melee hit on a living enemy,
                    // and a hit that kills pays the finish bonus on top.
                    Assert.That(sim.Momentum,
                        Is.EqualTo(PerHit).Within(Tolerance)
                            .Or.EqualTo(PerHit + PerKill).Within(Tolerance),
                        "the first gain must be exactly one hit (plus the kill bonus if it finished)");
                }
                peak = MathF.Max(peak, sim.Momentum);
                Assert.That(sim.Momentum, Is.LessThanOrEqualTo(Max + Tolerance),
                    $"the gauge must never exceed its ceiling (tick {tick})");
                if (reachedCeilingAt < 0 && sim.Momentum >= Max)
                {
                    reachedCeilingAt = tick;
                }
            }

            Assert.That(firstGainTick, Is.GreaterThanOrEqualTo(0), "scenario starved: nothing was ever hit");
            Assert.That(reachedCeilingAt, Is.GreaterThanOrEqualTo(0),
                "a sustained melee offensive must be able to fill the gauge");
            Assert.That(peak, Is.EqualTo(Max).Within(Tolerance), "the peak is the ceiling, exactly");
            Assert.That(sim.MomentumTier, Is.EqualTo(3), "a full gauge is the top tier");
            Assert.That(sim.MomentumDamageMultiplier, Is.EqualTo(Multipliers[3]).Within(Tolerance),
                "a full gauge grants the top multiplier");
        }

        // --- 6 & 7. A9.3 decay and the hurt penalty --------------------------

        [Test]
        public void Momentum_HoldsForTheGraceWindowThenDrainsAtTheFixedRate()
        {
            var config = DungeonScalar(null);
            var sim = new CinderSim(in config);
            var windows = new List<int>();
            var slopes = new List<float>();
            int sinceGain = -1;

            for (int tick = 0; tick < 3000; tick += 1)
            {
                float before = sim.Momentum;
                SimInput input = Hunt(sim, tick, 12);
                sim.Tick(in input);

                // A gain clipped by the ceiling is invisible to a value compare, so read it
                // off the event: with no companion and no skill input, EnemyHit IS melee.
                if ((sim.Events & SimEvents.EnemyHit) != 0)
                {
                    sinceGain = 0;
                    continue;
                }
                if ((sim.Events & SimEvents.PlayerDamaged) != 0)
                {
                    sinceGain = -1;  // the penalty cancels the grace; measured separately
                    continue;
                }
                if (sinceGain < 0)
                {
                    continue;
                }

                sinceGain += 1;
                if (sim.Momentum < before)
                {
                    windows.Add(sinceGain);
                    slopes.Add((before - sim.Momentum) * 60f);
                    sinceGain = -1;
                }
            }

            Assert.That(windows, Is.Not.Empty,
                "scenario starved: no clean window (no gain, no damage) ever contained a decay");

            foreach (int window in windows)
            {
                Assert.That(window, Is.GreaterThanOrEqualTo(96),
                    "1.6 s x 60 Hz is the exact-arithmetic floor of the grace window");
                Assert.That(window, Is.EqualTo(GraceTicks),
                    "the grace window is deterministic — every clean window must be the same length");
            }

            foreach (float slope in slopes)
            {
                Assert.That(slope, Is.EqualTo(DecayPerSecond).Within(0.05f),
                    "the drain is a constant 12 per second");
            }
        }

        [Test]
        public void Momentum_TakingDamageCostsAFlatSliceAndCancelsTheGrace()
        {
            var config = DungeonScalar(null);
            var sim = new CinderSim(in config);
            for (int tick = 0; tick < 900; tick += 1)
            {
                SimInput input = Hunt(sim, tick, 12);
                sim.Tick(in input);
            }
            Assert.That(sim.Momentum, Is.GreaterThan(HurtPenalty),
                "scenario starved: the gauge must hold more than one penalty before this is meaningful");

            int hurts = 0;
            for (int tick = 0; tick < 1800 && hurts < 3; tick += 1)
            {
                float before = sim.Momentum;
                SimInput input = Hunt(sim, tick, 12);
                sim.Tick(in input);
                if ((sim.Events & SimEvents.PlayerDamaged) == 0)
                {
                    continue;
                }
                hurts += 1;

                // The hit lands before this tick's swing could pay it back, so on a hurt
                // tick with no simultaneous melee hit the delta is EXACTLY the penalty.
                if ((sim.Events & SimEvents.EnemyHit) == 0)
                {
                    Assert.That(before - sim.Momentum, Is.EqualTo(HurtPenalty).Within(Tolerance),
                        "being hit must cost exactly a quarter of the bar");

                    // ...and the grace is gone, so the drain starts on the very next tick.
                    float afterHurt = sim.Momentum;
                    SimInput idle = Hunt(sim, tick + 1, int.MaxValue);
                    sim.Tick(in idle);
                    if ((sim.Events & (SimEvents.EnemyHit | SimEvents.PlayerDamaged)) == SimEvents.None
                        && afterHurt > 0f)
                    {
                        Assert.That(sim.Momentum, Is.LessThan(afterHurt),
                            "A9.3: a hit cancels the grace, so decay resumes immediately");
                    }
                }
            }

            Assert.That(hurts, Is.GreaterThan(0), "scenario starved: the player was never hit");
        }

        // --- 8 & 9. A9.4/A9.6 the damage the gauge actually buys -------------

        [Test]
        public void Momentum_MeleeDamageMatchesTheTierMultiplierAndIsSampledOncePerSwing()
        {
            var config = DungeonScalar(null);
            var sim = new CinderSim(in config);
            var tiersObserved = new SortedSet<int>();
            int hits = 0;
            int multiHitTicks = 0;

            for (int tick = 0; tick < ScriptTicks; tick += 1)
            {
                Dictionary<int, float> before = SampleHealth(sim);
                int tier = sim.MomentumTier;
                int level = sim.Level;

                SimInput input = Hunt(sim, tick, 12);
                sim.Tick(in input);

                float playerDamage = PlayerDamageAt(in config, level);
                int hitsThisTick = 0;
                IReadOnlyList<EnemyState> enemies = sim.Enemies;
                for (int index = 0; index < enemies.Count; index += 1)
                {
                    EnemyState enemy = enemies[index];
                    if (!before.TryGetValue(enemy.Id, out float was))
                    {
                        continue;
                    }
                    float lost = was - enemy.Health;
                    if (lost <= 0f)
                    {
                        continue;
                    }

                    hits += 1;
                    hitsThisTick += 1;
                    tiersObserved.Add(tier);

                    // The ONLY damage source here is player melee, so every delta must be
                    // one of the three combo hits (or the charged heavy) scaled by the tier
                    // the gauge held BEFORE this tick — never by the tier this swing built.
                    bool matched = false;
                    for (int step = 0; step < HackSpec.ComboLength && !matched; step += 1)
                    {
                        float expected = playerDamage * HackSpec.ComboDamageScale[step] * Multipliers[tier];
                        matched = MathF.Abs(lost - MathF.Min(was, expected)) < DamageTolerance;
                    }
                    if (!matched)
                    {
                        float heavy = playerDamage * HackSpec.ChargeDamageMul * Multipliers[tier];
                        matched = MathF.Abs(lost - MathF.Min(was, heavy)) < DamageTolerance;
                    }

                    Assert.That(matched, Is.True,
                        $"tick {tick}: lost {lost:F3} of {was:F3} at tier {tier} matches no melee value "
                        + $"(base {playerDamage:F3}, multiplier {Multipliers[tier]:F2})");
                }

                if (hitsThisTick >= 2)
                {
                    multiHitTicks += 1;
                }
            }

            Assert.That(hits, Is.GreaterThan(20), "scenario starved: too few melee hits to gate anything");
            Assert.That(tiersObserved, Is.EquivalentTo(new[] { 0, 1, 2, 3 }),
                "the ledger must cover every tier, or the multiplier is untested where it matters");
            // A swing samples the multiplier once: on a tick that hit two enemies BOTH deltas
            // were checked against the same pre-tick tier above, so this only has to prove
            // such a tick occurred.
            Assert.That(multiHitTicks, Is.GreaterThan(0),
                "scenario starved: no swing ever hit two enemies, so single-sampling is untested");
        }

        [Test]
        public void Momentum_SkillDamageIgnoresTheGauge()
        {
            // A9.6: the gauge is a MELEE multiplier. Grave Pulse is the cleanest witness —
            // a fixed 26 per tick through the §2.4 matchup, with no charge or combo state.
            var config = DungeonScalar(null);
            var sim = new CinderSim(in config);
            int unscaledPulseTicks = 0;
            int highTierHits = 0;

            for (int tick = 0; tick < 2400; tick += 1)
            {
                Dictionary<int, float> before = SampleHealth(sim);
                Dictionary<int, EnemyVisual> visual = SampleVisual(sim);
                int tier = sim.MomentumTier;
                int level = sim.Level;

                SimInput input = Hunt(sim, tick, 12);
                input.PulseQueued = tick % 45 == 0;
                sim.Tick(in input);

                float playerDamage = PlayerDamageAt(in config, level);
                IReadOnlyList<EnemyState> enemies = sim.Enemies;
                for (int index = 0; index < enemies.Count; index += 1)
                {
                    EnemyState enemy = enemies[index];
                    if (!before.TryGetValue(enemy.Id, out float was))
                    {
                        continue;
                    }
                    float lost = was - enemy.Health;
                    if (lost <= 0f)
                    {
                        continue;
                    }
                    if (tier >= 2)
                    {
                        highTierHits += 1;
                    }

                    float pulse = HackSpec.PulseTickDamage
                        * HackSpec.Matchup(HackSpec.PulseElement, HackSpec.ElementOf(visual[enemy.Id]));

                    bool matched = MathF.Abs(lost - MathF.Min(was, pulse)) < DamageTolerance;
                    if (matched && tier >= 1)
                    {
                        // An UNSCALED pulse tick while the gauge is live. If A9 ever reached
                        // the skill path this delta would be pulse * multiplier instead.
                        unscaledPulseTicks += 1;
                    }
                    for (int step = 0; step < HackSpec.ComboLength && !matched; step += 1)
                    {
                        float melee = playerDamage * HackSpec.ComboDamageScale[step] * Multipliers[tier];
                        matched = MathF.Abs(lost - MathF.Min(was, melee)) < DamageTolerance
                            || MathF.Abs(lost - MathF.Min(was, melee + pulse)) < DamageTolerance;
                    }
                    if (!matched)
                    {
                        float heavy = playerDamage * HackSpec.ChargeDamageMul * Multipliers[tier];
                        matched = MathF.Abs(lost - MathF.Min(was, heavy)) < DamageTolerance
                            || MathF.Abs(lost - MathF.Min(was, heavy + pulse)) < DamageTolerance;
                    }

                    Assert.That(matched, Is.True,
                        $"tick {tick}: lost {lost:F3} of {was:F3} at tier {tier} is neither tier-scaled melee "
                        + $"nor an unscaled {pulse:F3} pulse tick");
                }
            }

            Assert.That(highTierHits, Is.GreaterThan(0),
                "scenario starved: nothing was damaged while the gauge sat at tier 2+");
            Assert.That(unscaledPulseTicks, Is.GreaterThan(0),
                "scenario starved: no pulse tick landed while the gauge was live, so A9.6 is untested");
        }

        // --- 10. A9.5 the cue -------------------------------------------------

        [Test]
        public void Momentum_TierUpFiresOncePerPromotionAndNeverOnDecay()
        {
            var config = DungeonScalar(null);
            var sim = new CinderSim(in config);
            int events = 0;
            int promotions = 0;
            int demotions = 0;
            int previous = sim.MomentumTier;

            for (int tick = 0; tick < 2400; tick += 1)
            {
                SimInput input = Hunt(sim, tick, 12);
                sim.Tick(in input);

                bool raised = (sim.Events & SimEvents.MomentumTierUp) != 0;
                int now = sim.MomentumTier;
                if (now > previous)
                {
                    promotions += 1;
                    Assert.That(raised, Is.True, $"tick {tick}: tier rose {previous}->{now} with no cue");
                }
                else
                {
                    if (now < previous)
                    {
                        demotions += 1;
                    }
                    Assert.That(raised, Is.False,
                        $"tick {tick}: cue raised without a promotion ({previous}->{now})");
                }
                if (raised)
                {
                    events += 1;
                }
                previous = now;
            }

            Assert.That(promotions, Is.GreaterThan(0), "scenario starved: the gauge never crossed a tier");
            Assert.That(demotions, Is.GreaterThan(0),
                "scenario starved: the gauge never fell a tier, so 'never on decay' is untested");
            Assert.That(events, Is.EqualTo(promotions), "exactly one cue per promotion");
        }

        // --- 11 & 12. lifecycle and determinism ------------------------------

        [Test]
        public void Momentum_RestartEmptiesTheGauge()
        {
            var config = DungeonScalar(null);
            var sim = new CinderSim(in config);
            for (int tick = 0; tick < 900; tick += 1)
            {
                SimInput input = Hunt(sim, tick, 12);
                sim.Tick(in input);
            }
            Assert.That(sim.Momentum, Is.GreaterThan(0f), "scenario starved: nothing to clear");

            sim.Restart();
            Assert.That(sim.Momentum, Is.EqualTo(0f), "A9.3: momentum is never banked across runs");
            Assert.That(sim.MomentumTier, Is.EqualTo(0));
            Assert.That(sim.MomentumDamageMultiplier, Is.EqualTo(1f));

            // The tier the sim remembers for the edge-trigger must reset too, or the first
            // promotion of the new run would be swallowed.
            int events = 0;
            int promotions = 0;
            int previous = 0;
            for (int tick = 0; tick < 900; tick += 1)
            {
                SimInput input = Hunt(sim, tick, 12);
                sim.Tick(in input);
                if ((sim.Events & SimEvents.MomentumTierUp) != 0)
                {
                    events += 1;
                }
                if (sim.MomentumTier > previous)
                {
                    promotions += 1;
                }
                previous = sim.MomentumTier;
            }
            Assert.That(promotions, Is.GreaterThan(0), "scenario starved: no promotion after the restart");
            Assert.That(events, Is.EqualTo(promotions), "the post-restart run must still cue every promotion");
        }

        [Test]
        public void Momentum_IdenticalInputsYieldIdenticalGaugeAndDigest()
        {
            var config = DungeonScalar("scout-echo");
            var gauges = new List<float>();
            var tiers = new List<int>();
            var digests = new List<RunDigest>();

            for (int run = 0; run < 2; run += 1)
            {
                var sim = new CinderSim(in config);
                float checksum = 0f;
                for (int tick = 0; tick < ScriptTicks; tick += 1)
                {
                    SimInput input = Hunt(sim, tick, 12);
                    sim.Tick(in input);
                    checksum += sim.Momentum;
                }
                gauges.Add(checksum);
                tiers.Add(sim.MomentumTier);
                digests.Add(sim.Digest);
            }

            Assert.That(gauges[0], Is.GreaterThan(0f), "scenario starved: the gauge never moved");
            Assert.That(gauges[1], Is.EqualTo(gauges[0]), "§13: the gauge is a pure function of the inputs");
            Assert.That(tiers[1], Is.EqualTo(tiers[0]), "§13: so is the tier");
            Assert.That(digests[1].Score, Is.EqualTo(digests[0].Score), "§13: so is the run");
            Assert.That(digests[1].Kills, Is.EqualTo(digests[0].Kills));
            Assert.That(digests[1].HealthRemaining, Is.EqualTo(digests[0].HealthRemaining).Within(Tolerance));
        }
    }
}
