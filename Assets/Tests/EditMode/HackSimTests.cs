// Hack & slash overhaul gates (docs/SIM_SPEC_HACKSLASH.md).
// Arena regression is owned by CinderSimTests.cs (20) and campaign regression by
// CampaignSimTests.cs (10) — both untouched. This file only adds v0.2.0 rules.
using System;
using CinderCourt.Sim;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class HackSimTests
    {
        private const float Tolerance = 1e-4f;

        private static readonly SimInput Idle = default;

        // --- helpers ---------------------------------------------------------

        private static HackConfig Dungeon(
            string stageId = CampaignStages.CinderSpan,
            int attack = 0,
            int vitality = 0,
            int swiftness = 0,
            int weapon = 0,
            int lantern = 0,
            int cloak = 0,
            string companionId = null,
            int rosterMask = 0)
        {
            Assert.IsTrue(
                HackConfig.TryDungeon(
                    stageId,
                    MetaStats.Of(attack, vitality, swiftness),
                    EquipTiers.Of(weapon, lantern, cloak),
                    companionId,
                    rosterMask,
                    out var config),
                $"unknown stage {stageId}");
            return config;
        }
        private static CinderSim ClearedCinderSpan()
        {
            var config = Dungeon(attack: 10, vitality: 10, swiftness: 10, weapon: 5, lantern: 5, cloak: 5);
            var sim = new CinderSim(in config);
            for (int tick = 0; tick < 60 * 400; tick += 1)
            {
                sim.Tick(Pilot(sim, true));
                if (sim.StageCleared)
                {
                    return sim;
                }
                if (sim.Mode == SimMode.GameOver)
                {
                    break;
                }
            }

            Assert.Fail($"the max-stat pilot must clear cinder-span (reason {sim.Digest.Reason})");
            return sim;
        }

        private static void AssertSamePreparationOffer(
            PreparationOffer expected,
            PreparationOffer actual,
            string label)
        {
            Assert.That(actual.Kind, Is.EqualTo(expected.Kind), $"{label} kind");
            Assert.That(actual.Variant, Is.EqualTo(expected.Variant), $"{label} variant");
            Assert.That(actual.Magnitude, Is.EqualTo(expected.Magnitude), $"{label} magnitude");
        }
        private static PreparationOffer Preparation(PreparationOfferKind kind, int variant, int magnitude)
        {
            return new PreparationOffer
            {
                Kind = kind,
                Variant = variant,
                Magnitude = magnitude,
            };
        }

        private static void AssertPreparationIsInert(
            HackConfig baselineConfig,
            PreparationOffer offer,
            string label)
        {
            HackConfig preparedConfig = baselineConfig;
            preparedConfig.PreparationOffer = offer;
            var baseline = new CinderSim(in baselineConfig);
            var prepared = new CinderSim(in preparedConfig);
            for (int tick = 0; tick < 900; tick += 1)
            {
                SimInput input = Script(tick);
                baseline.Tick(in input);
                prepared.Tick(in input);
                Assert.That(prepared.Events, Is.EqualTo(baseline.Events), $"{label} tick {tick} events");
            }

            AssertSameDigest(baseline.Digest, prepared.Digest, label);
            Assert.That(prepared.Player.X, Is.EqualTo(baseline.Player.X).Within(Tolerance), label);
            Assert.That(prepared.Player.Y, Is.EqualTo(baseline.Player.Y).Within(Tolerance), label);
            Assert.That(prepared.Player.Health, Is.EqualTo(baseline.Player.Health).Within(Tolerance), label);
        }

        private static SimInput Script(int tick)
        {
            var input = default(SimInput);
            input.MoveX = tick / 120 % 2 == 0 ? 1f : -1f;
            input.MoveY = tick / 200 % 2 == 0 ? 0.5f : -0.5f;
            input.AttackQueued = tick % 30 == 0;
            input.NovaQueued = tick % 400 == 0;
            input.WardQueued = tick % 550 == 0;
            return input;
        }

        private static RunDigest RunScript(CinderSim sim, int ticks)
        {
            for (int tick = 0; tick < ticks; tick += 1)
            {
                sim.Tick(Script(tick));
            }
            return sim.Digest;
        }

        private static void AssertCompanionCommandsAreInert(HackConfig config, string label)
        {
            var baseline = new CinderSim(in config);
            var commanded = new CinderSim(in config);
            for (int tick = 0; tick < 360; tick += 1)
            {
                SimInput controlInput = Script(tick);
                SimInput commandInput = controlInput;
                commandInput.CompanionHoldQueued = tick % 71 == 0;
                commandInput.CompanionRecallQueued = tick % 113 == 0;

                baseline.Tick(in controlInput);
                commanded.Tick(in commandInput);
                Assert.That(commanded.Events, Is.EqualTo(baseline.Events), $"{label} tick {tick} events");
            }

            AssertSameDigest(baseline.Digest, commanded.Digest, $"{label} digest");
            Assert.That(commanded.CompanionBehavior, Is.EqualTo(CompanionBehavior.Follow), label);
            Assert.That(commanded.CompanionX, Is.EqualTo(baseline.CompanionX).Within(Tolerance), label);
            Assert.That(commanded.CompanionY, Is.EqualTo(baseline.CompanionY).Within(Tolerance), label);
        }

        private static RunDigest RunCompanionCommandSequence(CinderSim sim)
        {
            for (int tick = 0; tick < 900; tick += 1)
            {
                SimInput input = Script(tick);
                input.CompanionHoldQueued = tick == 90 || tick == 420 || tick == 660;
                input.CompanionRecallQueued = tick == 240 || tick == 420 || tick == 720;
                sim.Tick(in input);
            }
            return sim.Digest;
        }

        private static void AssertSameDigest(RunDigest expected, RunDigest actual, string because)
        {
            Assert.That(actual.Score, Is.EqualTo(expected.Score), because);
            Assert.That(actual.Wave, Is.EqualTo(expected.Wave), because);
            Assert.That(actual.Kills, Is.EqualTo(expected.Kills), because);
            Assert.That(actual.Relics, Is.EqualTo(expected.Relics), because);
            Assert.That(actual.HealthRemaining, Is.EqualTo(expected.HealthRemaining), because);
            Assert.That(actual.Reason, Is.EqualTo(expected.Reason), because);
        }

        /// <summary>
        /// Deterministic kiting pilot driven only by snapshot data. The attack clip
        /// scales movement to 0.42, which is barely faster than a chasing enemy, so the
        /// pilot only swings inside the poke band [120, 160] with a valid facing arc and
        /// otherwise spends every tick at full speed opening the gap.
        /// </summary>
        private static SimInput Pilot(CinderSim sim, bool useSkills)
        {
            // Enemies only start a swing inside 76 px and their contact reach is 90, so
            // 95 is the tightest gap that is still safe to stand and trade from.
            const float FleeRadius = 95f;
            const float PokeRadius = 158f;

            float playerX = sim.Player.X;
            float playerY = sim.Player.Y;
            var enemies = sim.Enemies;

            float bestSquared = float.MaxValue;
            float nearestDeltaX = 0f;
            float fleeX = 0f;
            float fleeY = 0f;
            bool anyAlive = false;

            for (int index = 0; index < enemies.Count; index += 1)
            {
                EnemyState enemy = enemies[index];
                if (enemy.Dead)
                {
                    continue;
                }
                anyAlive = true;
                float deltaX = enemy.X - playerX;
                float deltaY = (enemy.Y - playerY) * SimConfig.IsoY;
                float squared = deltaX * deltaX + deltaY * deltaY;
                if (squared < bestSquared)
                {
                    bestSquared = squared;
                    nearestDeltaX = deltaX;
                }
                if (squared >= FleeRadius * FleeRadius)
                {
                    continue;
                }
                float distance = MathF.Max(1f, MathF.Sqrt(squared));
                float weight = (FleeRadius - distance) / FleeRadius;
                fleeX -= deltaX / distance * weight;
                fleeY -= (enemy.Y - playerY) / distance * weight;
            }

            var input = default(SimInput);
            if (useSkills)
            {
                input.NovaQueued = true;
                input.WardQueued = true;
                input.BoltQueued = true;
                input.PulseQueued = true;
            }

            if (!anyAlive)
            {
                return input;
            }

            // Steer back toward the arena centre before the L1 clamp pins us to the rim.
            float rim = MathF.Abs(playerX - SimConfig.ArenaX) / 486f
                + MathF.Abs(playerY - SimConfig.ArenaY) / 253f;
            if (rim > 0.7f)
            {
                float pull = (rim - 0.7f) * 8f;
                fleeX += (SimConfig.ArenaX - playerX) / 486f * pull;
                fleeY += (SimConfig.ArenaY - playerY) / 253f * pull;
            }

            float fleeLength = MathF.Sqrt(fleeX * fleeX + fleeY * fleeY);
            if (fleeLength > 0.001f)
            {
                // Survival first: no attack key, so the clip never halves our speed.
                input.MoveX = fleeX / fleeLength;
                input.MoveY = fleeY / fleeLength;
                return input;
            }

            float nearest = MathF.Sqrt(bestSquared);
            if (nearest > PokeRadius)
            {
                input.MoveX = nearestDeltaX > 0f ? 1f : -1f;
                return input;
            }

            // Inside the poke band: face the target, then swing.
            if (nearestDeltaX * sim.Player.Facing < SimConfig.FacingArcTolerance)
            {
                input.MoveX = nearestDeltaX > 0f ? 1f : -1f;
                return input;
            }

            input.AttackQueued = true;
            return input;
        }

        // --- regression smoke -------------------------------------------------

        [Test]
        public void Regression_HackArenaConfig_ReproducesTheFrozenArenaRun()
        {
            var frozen = new CinderSim();
            var config = HackConfig.Arena();
            var hack = new CinderSim(in config);

            RunDigest frozenDigest = RunScript(frozen, 1800);
            RunDigest hackDigest = RunScript(hack, 1800);

            AssertSameDigest(frozenDigest, hackDigest, "GameMode.Arena must be the frozen run");
            Assert.That(hackDigest.Kills, Is.GreaterThan(0), "the scripted run must not be empty");
            Assert.That(hack.Player.X, Is.EqualTo(frozen.Player.X).Within(Tolerance));
            Assert.That(hack.Player.Y, Is.EqualTo(frozen.Player.Y).Within(Tolerance));
            Assert.That(hack.Enemies.Count, Is.EqualTo(frozen.Enemies.Count));
            Assert.That(hack.Charge, Is.EqualTo(frozen.Charge).Within(Tolerance));
            Assert.That(hack.HackMode, Is.EqualTo(GameMode.Arena));
            Assert.That(hack.Level, Is.EqualTo(1));
        }

        [Test]
        public void Regression_CampaignConstructor_StillProducesItsOwnDigest()
        {
            Assert.IsTrue(CampaignStages.TryGet(CampaignStages.AbyssChancel, 2, 1, 3, out var campaign));
            RunDigest first = RunScript(new CinderSim(in campaign), 1800);
            RunDigest second = RunScript(new CinderSim(in campaign), 1800);
            AssertSameDigest(first, second, "campaign constructor must stay deterministic");
            Assert.That(first.Kills, Is.GreaterThan(0));
        }

        // --- §1 prologue -------------------------------------------------------

        [Test]
        public void Prologue_RunsThreeWavesOfFourSixEight_WithNoBoss()
        {
            var config = HackConfig.Prologue();
            var sim = new CinderSim(in config);

            Assert.That(sim.HackMode, Is.EqualTo(GameMode.Prologue));
            Assert.That(sim.StageId, Is.EqualTo("prologue"));
            Assert.That(sim.PendingSpawns, Is.EqualTo(4));
            Assert.That(HackSpec.PrologueSpawnCount(1), Is.EqualTo(4));
            Assert.That(HackSpec.PrologueSpawnCount(2), Is.EqualTo(6));
            Assert.That(HackSpec.PrologueSpawnCount(3), Is.EqualTo(8));
            Assert.That(sim.Hazards.Count, Is.EqualTo(0), "the prologue has no gimmicks");

            // Prologue keeps the frozen arena stat contract (no meta, no equipment).
            Assert.That(sim.Player.Health, Is.EqualTo(SimConfig.PlayerMaxHealth));
            Assert.That(sim.WeaponRank, Is.EqualTo(0));

            bool sawBoss = false;
            int observedWaveTwoSpawns = -1;
            int observedWaveThreeSpawns = -1;
            for (int tick = 0; tick < 60 * 400 && sim.Mode != SimMode.GameOver; tick += 1)
            {
                int previousWave = sim.Wave;
                sim.Tick(Pilot(sim, false));
                if ((sim.Events & SimEvents.BossSpawned) != 0)
                {
                    sawBoss = true;
                }
                if (sim.Wave != previousWave && sim.Wave == 2)
                {
                    observedWaveTwoSpawns = sim.PendingSpawns;
                }
                if (sim.Wave != previousWave && sim.Wave == 3)
                {
                    observedWaveThreeSpawns = sim.PendingSpawns;
                }
            }

            Assert.IsFalse(sawBoss, "the prologue never spawns a boss");
            Assert.That(observedWaveTwoSpawns, Is.EqualTo(6));
            Assert.That(observedWaveThreeSpawns, Is.EqualTo(8));
        }

        [Test]
        public void Prologue_IgnoresSkillAndDashInput()
        {
            var config = HackConfig.Prologue();
            var sim = new CinderSim(in config);

            var input = new SimInput
            {
                NovaQueued = true,
                WardQueued = true,
                DashQueued = true,
                BoltQueued = true,
                PulseQueued = true,
            };

            SimEvents seen = SimEvents.None;
            for (int tick = 0; tick < 60 * 20; tick += 1)
            {
                sim.Tick(in input);
                seen |= sim.Events;
            }

            Assert.That(seen & SimEvents.NovaCast, Is.EqualTo(SimEvents.None));
            Assert.That(seen & SimEvents.WardCast, Is.EqualTo(SimEvents.None));
            Assert.That(seen & SimEvents.DashUsed, Is.EqualTo(SimEvents.None));
            Assert.That(seen & SimEvents.BoltCast, Is.EqualTo(SimEvents.None));
            Assert.That(seen & SimEvents.PulseCast, Is.EqualTo(SimEvents.None));
            Assert.That(sim.Charge, Is.EqualTo(SimConfig.LanternMax).Within(Tolerance),
                "no skill may spend oil in the prologue");
            Assert.That(sim.Player.WardTime, Is.EqualTo(0f));
            Assert.That(sim.DashCooldown, Is.EqualTo(0f));
            Assert.That(sim.Player.Action, Is.Not.EqualTo(ActorAction.Avoid));
        }

        [Test]
        public void Prologue_ClearingAllThreeWaves_EndsWithPrologueClear()
        {
            var config = HackConfig.Prologue();
            var sim = new CinderSim(in config);

            bool cleared = false;
            for (int tick = 0; tick < 60 * 400; tick += 1)
            {
                sim.Tick(Pilot(sim, false));
                if ((sim.Events & SimEvents.StageCleared) != 0)
                {
                    cleared = true;
                    break;
                }
                if (sim.Mode == SimMode.GameOver)
                {
                    break;
                }
            }

            Assert.IsTrue(cleared, $"the pilot must clear the prologue (reason {sim.Digest.Reason})");
            Assert.That(sim.Wave, Is.EqualTo(3));
            Assert.That(sim.Digest.Reason, Is.EqualTo("prologue-clear"));
            Assert.That(sim.StageCleared, Is.True);
            Assert.That(sim.Mode, Is.EqualTo(SimMode.GameOver));
        }

        // --- §2.4 elements / §2.5 xp curve (pure tables) ------------------------

        [Test]
        public void ElementCycle_AdvantageIsOneStepAhead()
        {
            Assert.That(HackSpec.Matchup(Element.Ember, Element.Frost), Is.EqualTo(1.2f).Within(Tolerance));
            Assert.That(HackSpec.Matchup(Element.Frost, Element.Veil), Is.EqualTo(1.2f).Within(Tolerance));
            Assert.That(HackSpec.Matchup(Element.Veil, Element.Void), Is.EqualTo(1.2f).Within(Tolerance));
            Assert.That(HackSpec.Matchup(Element.Void, Element.Ember), Is.EqualTo(1.2f).Within(Tolerance));

            Assert.That(HackSpec.Matchup(Element.Frost, Element.Ember), Is.EqualTo(0.85f).Within(Tolerance));
            Assert.That(HackSpec.Matchup(Element.Ember, Element.Void), Is.EqualTo(0.85f).Within(Tolerance));

            // Mirror and two-steps-away are both neutral, not favourable.
            Assert.That(HackSpec.Matchup(Element.Ember, Element.Ember), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(HackSpec.Matchup(Element.Ember, Element.Veil), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(HackSpec.Matchup(Element.None, Element.Frost), Is.EqualTo(1f).Within(Tolerance));

            Assert.That(HackSpec.ElementOf(EnemyVisual.EmberCohort), Is.EqualTo(Element.Ember));
            Assert.That(HackSpec.ElementOf(EnemyVisual.Scout), Is.EqualTo(Element.Frost));
            Assert.That(HackSpec.ElementOf(EnemyVisual.Shade), Is.EqualTo(Element.Veil));
            Assert.That(HackSpec.ElementOf(EnemyVisual.Possessed), Is.EqualTo(Element.Void));
            Assert.That(HackSpec.ElementOf(EnemyVisual.BossCommander), Is.EqualTo(Element.Veil));
            Assert.That(HackSpec.ElementOf(EnemyVisual.BossMonarch), Is.EqualTo(Element.Void));
        }

        [Test]
        public void XpCurve_MatchesSpecAndCapsAtTwelve()
        {
            int[] expected = { 30, 55, 85, 120, 160, 205, 255, 310 };
            for (int level = 1; level <= expected.Length; level += 1)
            {
                Assert.That(HackSpec.XpToNextLevel(level), Is.EqualTo(expected[level - 1]), $"level {level}");
            }
            Assert.That(HackSpec.XpToNextLevel(9), Is.EqualTo(370));
            Assert.That(HackSpec.XpToNextLevel(10), Is.EqualTo(430));
            Assert.That(HackSpec.XpToNextLevel(11), Is.EqualTo(490));
            Assert.That(HackSpec.XpToNextLevel(HackSpec.LevelCap), Is.EqualTo(0), "capped at 12");
        }

        // --- §5 / §6 derived stats ----------------------------------------------

        [Test]
        public void DerivedStats_CombineMetaStatsAndEquipTiers()
        {
            var config = Dungeon(attack: 4, vitality: 3, swiftness: 5, weapon: 2, lantern: 3, cloak: 1);

            Assert.That(config.PlayerDamage, Is.EqualTo(58f * 1.12f * 1.12f).Within(1e-3f));
            Assert.That(config.PlayerMaxHealth, Is.EqualTo(100f + 24f + 8f).Within(1e-3f));
            Assert.That(config.PlayerSpeed, Is.EqualTo(218f * 1.10f).Within(1e-3f));
            Assert.That(config.LanternRegenPerSecond, Is.EqualTo(7f * 1.24f).Within(1e-3f));

            var sim = new CinderSim(in config);
            Assert.That(sim.Player.Health, Is.EqualTo(132f).Within(1e-3f), "vitality + cloak apply at spawn");
            Assert.That(sim.WeaponRank, Is.EqualTo(2));
            Assert.That(sim.LanternRank, Is.EqualTo(3));
            Assert.That(sim.CloakRank, Is.EqualTo(1));

            // Stats cap at 10 points / rank 5.
            var capped = Dungeon(attack: 99, vitality: -3, swiftness: 40, weapon: 9, lantern: -1, cloak: 5);
            Assert.That(capped.PlayerDamage, Is.EqualTo(58f * 1.30f * 1.30f).Within(1e-3f));
            Assert.That(capped.PlayerMaxHealth, Is.EqualTo(100f + 0f + 40f).Within(1e-3f));
            Assert.That(capped.PlayerSpeed, Is.EqualTo(218f * 1.20f).Within(1e-3f));

            // Meta stats never leak into the prologue or the arena (§5).
            var prologue = HackConfig.Prologue();
            prologue.MetaStats = MetaStats.Of(10, 10, 10);
            prologue.EquipTiers = EquipTiers.Of(5, 5, 5);
            var prologueSim = new CinderSim(in prologue);
            Assert.That(prologueSim.Player.Health, Is.EqualTo(SimConfig.PlayerMaxHealth));
            Assert.That(prologueSim.WeaponRank, Is.EqualTo(0));
        }

        // --- §2.1 combo ---------------------------------------------------------

        /// <summary>
        /// A reproducible dungeon snapshot: cinder-span with a rank-5 cloak, wave 2
        /// fully spawned and the four survivors parked in the poke ring around a
        /// stationary player. Every §2 test starts here.
        /// </summary>
        private static CinderSim ClusteredWaveTwo(
            int rosterMask = 0,
            PreparationOffer preparation = default)
        {
            // Vitality only widens the health pool — it never moves an enemy, so the
            // trajectory into this state is identical for every stat spread.
            var config = Dungeon(vitality: 10, cloak: 5, rosterMask: rosterMask);
            config.PreparationOffer = preparation;
            var sim = new CinderSim(in config);

            for (int tick = 0; tick < 60 * 120 && !(sim.Wave == 2 && sim.PendingSpawns == 0); tick += 1)
            {
                sim.Tick(Pilot(sim, true));
            }
            for (int tick = 0; tick < 480; tick += 1)
            {
                sim.Tick(Idle);
            }

            Assert.That(sim.Wave, Is.EqualTo(2), "setup must reach wave 2");
            Assert.That(sim.LivingEnemies, Is.EqualTo(4), "setup must leave four wave-2 enemies");
            Assert.That(sim.Level, Is.EqualTo(2), "five kills is 50 xp — level 2 with 20 banked");
            Assert.That(sim.Xp, Is.EqualTo(20));
            Assert.That(sim.XpNext, Is.EqualTo(55));
            Assert.That(sim.ElitesAlive, Is.EqualTo(1), "the 7th dungeon spawn is the wave-2 elite");
            for (int index = 0; index < sim.Enemies.Count; index += 1)
            {
                EnemyState enemy = sim.Enemies[index];
                float baseline = 86f + 11f;
                Assert.That(enemy.MaxHealth,
                    Is.EqualTo(IsElite(enemy) ? baseline * HackSpec.EliteHealthMul : baseline).Within(Tolerance),
                    "wave 2 dungeon health is 86 + 11, tripled for the elite");
            }
            return sim;
        }

        /// <summary>
        /// A level-1 in-range fixture for damage assertions whose formula must not
        /// include the level-two run bonus.
        /// </summary>
        private static CinderSim FirstWaveEnemyInRange(HackConfig config)
        {
            var sim = new CinderSim(in config);
            bool foundInRange = false;
            for (int tick = 0; tick < 60 * 10 && !foundInRange; tick += 1)
            {
                sim.Tick(Idle);
                for (int index = 0; index < sim.Enemies.Count; index += 1)
                {
                    EnemyState enemy = sim.Enemies[index];
                    if (!enemy.Dead
                        && enemy.Health == enemy.MaxHealth
                        && IsoDistance(enemy.X, enemy.Y, sim.Player.X, sim.Player.Y) <= 110f)
                    {
                        foundInRange = true;
                        break;
                    }
                }
            }

            Assert.That(sim.Wave, Is.EqualTo(1), "setup must remain in wave 1");
            Assert.That(foundInRange, Is.True, "the first-wave fixture must place an untouched enemy in range");
            Assert.That(sim.Level, Is.EqualTo(1), "setup must not add a level-damage bonus");
            return sim;
        }

        /// <summary>Elites are the non-boss records the sim scales to 1.35 (§3).</summary>
        private static bool IsElite(EnemyState enemy) => !enemy.IsBoss && enemy.Scale > 1f;

        private static float IsoDistance(float ax, float ay, float bx, float by)
        {
            float deltaX = ax - bx;
            float deltaY = (ay - by) * SimConfig.IsoY;
            return MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        /// <summary>
        /// Assert a 120 px / 0.18 s knockback on <paramref name="id"/>. The push rides on
        /// top of the enemy's own chase, so the net gain is the 120 px impulse minus the
        /// few px it walks back in — but a chasing enemy never covers more than
        /// 128/60 = 2.2 px in one step, so a 9 px step can only be knockback.
        /// </summary>
        private static void AssertKnockback(CinderSim sim, int id, float isoBefore)
        {
            float peakGain = 0f;
            float fastestStep = 0f;
            float previous = isoBefore;
            for (int tick = 0; tick < 16; tick += 1)
            {
                sim.Tick(Idle);
                EnemyState enemy = EnemyById(sim, id);
                float iso = IsoDistance(enemy.X, enemy.Y, sim.Player.X, sim.Player.Y);
                if (iso - isoBefore > peakGain)
                {
                    peakGain = iso - isoBefore;
                }
                if (iso - previous > fastestStep)
                {
                    fastestStep = iso - previous;
                }
                previous = iso;
            }

            Assert.That(fastestStep, Is.GreaterThan(9f),
                "no chasing enemy covers 9 px in one step — that is the 120/0.18 push");
            Assert.That(peakGain, Is.GreaterThan(100f), "the 120 px push must actually land");
            Assert.That(peakGain, Is.LessThanOrEqualTo(HackSpec.ComboKnockbackDistance + 1f),
                "and it must not exceed 120 px");
        }

        private static EnemyState EnemyById(CinderSim sim, int id)
        {
            for (int index = 0; index < sim.Enemies.Count; index += 1)
            {
                if (sim.Enemies[index].Id == id)
                {
                    return sim.Enemies[index];
                }
            }
            Assert.Fail($"enemy {id} is gone");
            return default;
        }

        [Test]
        public void Combo_ChainsThreeHitsWithSpecSwingTimesAndActiveWindows()
        {
            var sim = ClusteredWaveTwo();
            var faceLeft = new SimInput { MoveX = -1f };
            sim.Tick(in faceLeft);
            Assert.That(sim.Player.Facing, Is.EqualTo(-1));
            Assert.That(sim.ComboIndex, Is.EqualTo(0), "a fresh chain starts on hit 1");

            var attack = new SimInput { AttackQueued = true };
            float[] expectedSwing = { 0.30f, 0.30f, 0.42f };

            for (int hit = 0; hit < 3; hit += 1)
            {
                Assert.That(sim.ComboIndex, Is.EqualTo(hit), $"chain must be on hit {hit + 1}");
                sim.Tick(in attack);
                Assert.That(sim.Events & SimEvents.PlayerStruck, Is.EqualTo(SimEvents.PlayerStruck));
                Assert.That(sim.Player.Action, Is.EqualTo(ActorAction.Attack));

                int swingTicks = 1;
                int damageTicks = 0;
                float firstDamageAt = -1f;
                while (sim.Player.Action == ActorAction.Attack)
                {
                    if ((sim.Events & SimEvents.EnemyHit) != 0)
                    {
                        damageTicks += 1;
                        if (firstDamageAt < 0f)
                        {
                            firstDamageAt = sim.Player.ActionTime;
                        }
                    }
                    sim.Tick(Idle);
                    swingTicks += 1;
                }

                // The swing ends on the first tick at or past the spec duration.
                Assert.That(swingTicks * SimConfig.FixedStep, Is.GreaterThanOrEqualTo(expectedSwing[hit]));
                Assert.That((swingTicks - 1) * SimConfig.FixedStep, Is.LessThan(expectedSwing[hit]),
                    $"hit {hit + 1} must swing for {expectedSwing[hit]} s");

                if (damageTicks > 0)
                {
                    Assert.That(damageTicks, Is.EqualTo(1), "one swing damages a given enemy once");
                    Assert.That(firstDamageAt, Is.GreaterThanOrEqualTo(HackSpec.ComboActiveFrom[hit]));
                    Assert.That(firstDamageAt, Is.LessThan(HackSpec.ComboActiveTo[hit]));
                }

                Assert.That(sim.ComboIndex, Is.EqualTo((hit + 1) % 3), "the chain advances on swing end");
            }
        }

        [Test]
        public void Combo_LinkWindowExpiresAfterNinetenthsAndRestartsTheChain()
        {
            var sim = ClusteredWaveTwo();
            sim.Tick(new SimInput { MoveX = -1f });

            var attack = new SimInput { AttackQueued = true };
            sim.Tick(in attack);
            while (sim.Player.Action == ActorAction.Attack)
            {
                sim.Tick(Idle);
            }
            Assert.That(sim.ComboIndex, Is.EqualTo(1), "hit 2 is queued right after hit 1");

            // Still linked half a second later.
            for (int tick = 0; tick < 30; tick += 1)
            {
                sim.Tick(Idle);
            }
            Assert.That(sim.ComboIndex, Is.EqualTo(1), "the 0.9 s link window is still open");

            // 0.9 s after the swing ended the chain is forgotten.
            for (int tick = 0; tick < 30; tick += 1)
            {
                sim.Tick(Idle);
            }
            Assert.That(sim.ComboIndex, Is.EqualTo(0), "the chain resets outside the link window");

            sim.Tick(in attack);
            int swingTicks = 1;
            while (sim.Player.Action == ActorAction.Attack)
            {
                sim.Tick(Idle);
                swingTicks += 1;
            }
            Assert.That(swingTicks, Is.EqualTo(18), "the restarted swing is hit 1 (0.30 s)");
        }

        [Test]
        public void Combo_FinisherScalesDamageKnocksBackAndRaisesComboFinisher()
        {
            var sim = ClusteredWaveTwo();

            // Step out to the right so hits 1 and 2 fall outside the facing arc.
            var right = new SimInput { MoveX = 1f };
            for (int tick = 0; tick < 6; tick += 1)
            {
                sim.Tick(in right);
            }

            var attack = new SimInput { AttackQueued = true };
            for (int hit = 0; hit < 2; hit += 1)
            {
                sim.Tick(in attack);
                while (sim.Player.Action == ActorAction.Attack)
                {
                    sim.Tick(Idle);
                }
            }
            Assert.That(sim.ComboIndex, Is.EqualTo(2), "two swings put the chain on the finisher");

            sim.Tick(new SimInput { MoveX = -1f });
            float playerX = sim.Player.X;
            float playerY = sim.Player.Y;

            // Snapshot the untouched targets: they are the ones the finisher will hit.
            int targetId = -1;
            float targetIsoBefore = 0f;
            int fullHealthTargets = 0;
            for (int index = 0; index < sim.Enemies.Count; index += 1)
            {
                EnemyState enemy = sim.Enemies[index];
                if (enemy.Dead || enemy.Health < enemy.MaxHealth)
                {
                    continue;
                }
                fullHealthTargets += 1;
                if (targetId < 0)
                {
                    targetId = enemy.Id;
                    targetIsoBefore = IsoDistance(enemy.X, enemy.Y, playerX, playerY);
                }
            }
            Assert.That(fullHealthTargets, Is.GreaterThanOrEqualTo(2), "the finisher needs fresh targets");

            sim.Tick(in attack);
            bool finisher = false;
            while (sim.Player.Action == ActorAction.Attack && !finisher)
            {
                sim.Tick(Idle);
                finisher = (sim.Events & SimEvents.ComboFinisher) != 0;
            }
            Assert.IsTrue(finisher, "the third hit must raise ComboFinisher");

            // Hit 3 is 87/58 of the player's attack, i.e. 58 * 1.04 (level 2) * 1.5.
            EnemyState target = EnemyById(sim, targetId);
            Assert.That(target.MaxHealth - target.Health, Is.EqualTo(58f * 1.04f * 1.5f).Within(1e-3f));

            // 120 px of knockback over 0.18 s, measured on the iso metric. The target
            // starts walking back in as soon as the push ends, so take the peak.
            AssertKnockback(sim, targetId, targetIsoBefore);
        }

        // --- §2.2 dash ------------------------------------------------------------

        [Test]
        public void Dash_TravelsOneHundredNinetyPixelsAndSpendsOilOnCooldown()
        {
            var config = Dungeon();
            var sim = new CinderSim(in config);
            float startX = sim.Player.X;
            float startY = sim.Player.Y;
            float startCharge = sim.Charge;

            var dash = new SimInput { DashQueued = true, MoveX = 1f };
            sim.Tick(in dash);
            Assert.That(sim.Events & SimEvents.DashUsed, Is.EqualTo(SimEvents.DashUsed));
            Assert.That(sim.Player.Action, Is.EqualTo(ActorAction.Avoid));
            Assert.That(sim.DashCooldown, Is.EqualTo(1.6f - SimConfig.FixedStep).Within(1e-3f));
            Assert.That(startCharge - sim.Charge,
                Is.EqualTo(HackSpec.DashCost - _lanternRegenForOneStep).Within(1e-2f),
                "the dash costs 8 oil (net of one step of regen)");

            int dashTicks = 1;
            while (sim.Player.Action == ActorAction.Avoid)
            {
                sim.Tick(Idle);
                dashTicks += 1;
            }
            Assert.That(dashTicks, Is.EqualTo(14), "0.22 s of dash spans 14 fixed steps");
            Assert.That(sim.Player.X - startX, Is.EqualTo(HackSpec.DashDistance).Within(1e-2f),
                "the clipped final step keeps the travel at exactly 190 px");
            Assert.That(sim.Player.Y, Is.EqualTo(startY).Within(Tolerance));

            // The cooldown blocks a second dash.
            sim.Tick(in dash);
            Assert.That(sim.Events & SimEvents.DashUsed, Is.EqualTo(SimEvents.None));
        }

        private const float _lanternRegenForOneStep = 7f * SimConfig.FixedStep;

        [Test]
        public void Dash_IsInvulnerableForItsWholeDuration()
        {
            // Scout the same deterministic state to find the tick the pack connects on.
            var scout = ClusteredWaveTwo();
            int hitAt = -1;
            for (int tick = 0; tick < 60 * 5; tick += 1)
            {
                scout.Tick(Idle);
                if ((scout.Events & SimEvents.PlayerDamaged) != 0)
                {
                    hitAt = tick;
                    break;
                }
            }
            Assert.That(hitAt, Is.GreaterThan(4), "the pack must land a hit to dodge");

            var dashing = ClusteredWaveTwo();
            var control = ClusteredWaveTwo();
            int lead = hitAt - 4;
            for (int tick = 0; tick < lead; tick += 1)
            {
                dashing.Tick(Idle);
                control.Tick(Idle);
            }
            float healthBefore = dashing.Player.Health;
            Assert.That(control.Player.Health, Is.EqualTo(healthBefore), "both sims are in lockstep");

            // Dash straight into the pack; the control just stands there and eats it.
            dashing.Tick(new SimInput { DashQueued = true, MoveX = -1f });
            control.Tick(Idle);
            for (int tick = 0; tick < 13; tick += 1)
            {
                Assert.That(dashing.Player.Action, Is.EqualTo(ActorAction.Avoid), "still dashing");
                dashing.Tick(Idle);
                control.Tick(Idle);
                Assert.That(dashing.Player.Health, Is.EqualTo(healthBefore),
                    "the dash refuses contact damage");
                Assert.That(dashing.Player.DamageCooldown, Is.EqualTo(0f),
                    "i-frames do not even burn the contact grace");
            }
            Assert.That(control.Player.Health, Is.LessThan(healthBefore),
                "the control proves the same window landed a hit");
        }

        // --- §2.3 skills / §2.4 elements -----------------------------------------

        [Test]
        public void RiftBolt_HitsTheNearestTargetAndSplashesAtSixtyPercent()
        {
            var sim = ClusteredWaveTwo();
            sim.Tick(new SimInput { MoveX = -1f });

            float playerX = sim.Player.X;
            float playerY = sim.Player.Y;
            int nearestId = -1;
            float nearestIso = float.MaxValue;
            for (int index = 0; index < sim.Enemies.Count; index += 1)
            {
                EnemyState enemy = sim.Enemies[index];
                float iso = IsoDistance(enemy.X, enemy.Y, playerX, playerY);
                if (iso < nearestIso)
                {
                    nearestIso = iso;
                    nearestId = enemy.Id;
                }
            }
            EnemyState primary = EnemyById(sim, nearestId);
            float primaryX = primary.X;
            float primaryY = primary.Y;

            sim.Tick(new SimInput { BoltQueued = true });
            Assert.That(sim.Events & SimEvents.BoltCast, Is.EqualTo(SimEvents.BoltCast));

            // rift-bolt is void: 145 on the primary, 87 on everything inside 115 of it,
            // both scaled by the §2.4 matchup against the victim's element.
            for (int index = 0; index < sim.Enemies.Count; index += 1)
            {
                EnemyState enemy = sim.Enemies[index];
                float matchup = HackSpec.Matchup(Element.Void, HackSpec.ElementOf(enemy.Visual));
                float expected;
                if (enemy.Id == nearestId)
                {
                    expected = HackSpec.BoltDamage * matchup;
                }
                else if (IsoDistance(enemy.X, enemy.Y, primaryX, primaryY) <= HackSpec.BoltSplashRadius)
                {
                    expected = HackSpec.BoltDamage * HackSpec.BoltSplashScale * matchup;
                }
                else
                {
                    expected = 0f;
                }
                float dealt = enemy.MaxHealth - enemy.Health;
                Assert.That(dealt, Is.EqualTo(MathF.Min(enemy.MaxHealth, expected)).Within(1e-2f),
                    $"enemy {enemy.Id} ({enemy.Visual})");
            }
        }

        [Test]
        public void GravePulse_TicksEveryHalfSecondAndRollsTheElementCycle()
        {
            var sim = ClusteredWaveTwo();
            sim.Tick(new SimInput { MoveX = -1f });

            sim.Tick(new SimInput { PulseQueued = true });
            Assert.That(sim.Events & SimEvents.PulseCast, Is.EqualTo(SimEvents.PulseCast));

            int firstTickAt = -1;
            int lastTickAt = -1;
            int ticks = 0;
            bool checkedElements = false;
            for (int tick = 1; tick <= 200; tick += 1)
            {
                sim.Tick(Idle);
                if ((sim.Events & SimEvents.EnemyHit) == 0)
                {
                    continue;
                }
                ticks += 1;
                if (firstTickAt < 0)
                {
                    firstTickAt = tick;
                    // grave-pulse is ember: 26 base, x1.2 vs frost, x0.85 vs void.
                    for (int index = 0; index < sim.Enemies.Count; index += 1)
                    {
                        EnemyState enemy = sim.Enemies[index];
                        float matchup = HackSpec.Matchup(Element.Ember, HackSpec.ElementOf(enemy.Visual));
                        Assert.That(enemy.MaxHealth - enemy.Health,
                            Is.EqualTo(HackSpec.PulseTickDamage * matchup).Within(1e-2f),
                            $"enemy {enemy.Id} ({enemy.Visual})");
                        checkedElements = true;
                    }
                }
                else
                {
                    Assert.That(tick - lastTickAt, Is.EqualTo(30), "the field ticks every 0.5 s");
                }
                lastTickAt = tick;
            }

            Assert.IsTrue(checkedElements, "the first field tick must have landed on live enemies");
            Assert.That(firstTickAt, Is.EqualTo(29),
                "the first tick lands 30 fixed steps (0.5 s) after the cast tick");
            Assert.That(ticks, Is.GreaterThanOrEqualTo(4));
            Assert.That(ticks, Is.LessThanOrEqualTo(6), "3 s at 0.5 s is six ticks at most");
            Assert.That(lastTickAt, Is.LessThanOrEqualTo(180), "the field is gone after 3 s");
        }

        [Test]
        public void AshNova_DamagesInsideTheRadiusAndKnocksBack()
        {
            var sim = ClusteredWaveTwo();
            sim.Tick(new SimInput { MoveX = -1f });

            float playerX = sim.Player.X;
            float playerY = sim.Player.Y;
            int survivorId = -1;
            float survivorIsoBefore = 0f;
            for (int index = 0; index < sim.Enemies.Count; index += 1)
            {
                EnemyState enemy = sim.Enemies[index];
                Assert.That(IsoDistance(enemy.X, enemy.Y, playerX, playerY),
                    Is.LessThanOrEqualTo(HackSpec.AshNovaRadius), "the whole pack is inside the burst");
                // ash-nova is ember: only the void archetype survives 110 * 0.85.
                if (!IsElite(enemy) && HackSpec.ElementOf(enemy.Visual) == Element.Void)
                {
                    survivorId = enemy.Id;
                    survivorIsoBefore = IsoDistance(enemy.X, enemy.Y, playerX, playerY);
                }
            }
            Assert.That(survivorId, Is.GreaterThan(0), "wave 2 must contain the void archetype");

            sim.Tick(new SimInput { NovaQueued = true });
            Assert.That(sim.Events & SimEvents.NovaCast, Is.EqualTo(SimEvents.NovaCast));
            Assert.That(sim.NovaX, Is.EqualTo(playerX).Within(Tolerance));

            EnemyState survivor = EnemyById(sim, survivorId);
            Assert.That(survivor.MaxHealth - survivor.Health,
                Is.EqualTo(HackSpec.AshNovaDamage * HackSpec.ElementDisadvantage).Within(1e-2f),
                "ember into void is a 15% penalty");
            for (int index = 0; index < sim.Enemies.Count; index += 1)
            {
                EnemyState enemy = sim.Enemies[index];
                if (enemy.Id == survivorId || IsElite(enemy))
                {
                    continue;
                }
                Assert.That(enemy.Health, Is.EqualTo(0f), $"enemy {enemy.Id} must fall to 110 ember");
            }

            AssertKnockback(sim, survivorId, survivorIsoBefore);
        }

        [Test]
        public void VoidAegis_AbsorbsFortyDamageBeforeHealthMoves()
        {
            var sim = ClusteredWaveTwo();

            sim.Tick(new SimInput { WardQueued = true });
            Assert.That(sim.Events & SimEvents.WardCast, Is.EqualTo(SimEvents.WardCast));
            Assert.That(sim.Shield, Is.EqualTo(HackSpec.AegisShield));

            float health = sim.Player.Health;
            int drainedAt = -1;
            for (int tick = 1; tick <= 60 * 8; tick += 1)
            {
                sim.Tick(Idle);
                if (sim.Shield > 0f)
                {
                    Assert.That(sim.Player.Health, Is.EqualTo(health), "the shield eats the damage");
                    continue;
                }
                drainedAt = tick;
                break;
            }

            Assert.That(drainedAt, Is.GreaterThan(0), "40 points of contact must drain the shield");
            Assert.That(drainedAt, Is.LessThanOrEqualTo(60 * 8), "and it expires after 8 s regardless");

            bool healthMoved = false;
            for (int tick = 0; tick < 120; tick += 1)
            {
                sim.Tick(Idle);
                if (sim.Player.Health < health)
                {
                    healthMoved = true;
                    break;
                }
            }
            Assert.IsTrue(healthMoved, "once the shield is gone the pack hurts again");
        }

        [Test]
        public void Skills_CostsAndCooldownsMatchTheSkillTable()
        {
            // Cast on tick 0: no enemy exists yet, so no kill or pickup tops the oil up.
            AssertSkillCost(
                new SimInput { BoltQueued = true }, HackSpec.SkillBolt,
                HackSpec.BoltCost, HackSpec.BoltCooldown, SimEvents.BoltCast);
            AssertSkillCost(
                new SimInput { PulseQueued = true }, HackSpec.SkillPulse,
                HackSpec.PulseCost, HackSpec.PulseCooldown, SimEvents.PulseCast);
            AssertSkillCost(
                new SimInput { NovaQueued = true }, HackSpec.SkillNova,
                HackSpec.AshNovaCost, HackSpec.AshNovaCooldown, SimEvents.NovaCast);
            AssertSkillCost(
                new SimInput { WardQueued = true }, HackSpec.SkillAegis,
                HackSpec.AegisCost, HackSpec.AegisCooldown, SimEvents.WardCast);
        }

        private static void AssertSkillCost(
            SimInput input, int slot, float cost, float cooldown, SimEvents cue)
        {
            var config = Dungeon();
            var sim = new CinderSim(in config);
            float startCharge = sim.Charge;

            sim.Tick(in input);
            Assert.That(sim.Events & cue, Is.EqualTo(cue), $"skill {slot} must raise its cue");
            Assert.That(startCharge - sim.Charge,
                Is.EqualTo(cost - _lanternRegenForOneStep).Within(1e-2f), $"skill {slot} oil cost");
            Assert.That(sim.SkillCooldowns[slot],
                Is.EqualTo(cooldown - SimConfig.FixedStep).Within(1e-2f), $"skill {slot} cooldown");
            Assert.That(sim.Events & SimEvents.EnemyHit, Is.EqualTo(SimEvents.None),
                "there is nothing in range on tick 0");

            // The cooldown gates the recast even with a full lantern.
            sim.Tick(in input);
            Assert.That(sim.SkillCooldowns[slot], Is.LessThan(cooldown - SimConfig.FixedStep),
                $"skill {slot} must not refresh while on cooldown");
        }

        // --- §2.1 / §2.5 dungeon curves ------------------------------------------

        [Test]
        public void DungeonEnemyHealth_UsesTheComboCurveNotTheArenaCurve()
        {
            var config = Dungeon();
            var dungeon = new CinderSim(in config);
            var arena = new CinderSim();
            for (int tick = 0; tick < 20; tick += 1)
            {
                dungeon.Tick(Idle);
                arena.Tick(Idle);
            }

            Assert.That(dungeon.Enemies.Count, Is.GreaterThan(0));
            Assert.That(dungeon.Enemies[0].MaxHealth, Is.EqualTo(86f).Within(Tolerance),
                "86 + min(140, (wave-1)*11)");
            Assert.That(arena.Enemies[0].MaxHealth, Is.EqualTo(58f).Within(Tolerance),
                "the arena curve is untouched");

            var prologueConfig = HackConfig.Prologue();
            var prologue = new CinderSim(in prologueConfig);
            for (int tick = 0; tick < 20; tick += 1)
            {
                prologue.Tick(Idle);
            }
            Assert.That(prologue.Enemies[0].MaxHealth, Is.EqualTo(58f).Within(Tolerance),
                "the prologue keeps the arena curve");
        }

        [Test]
        public void Xp_LevellingRaisesDamageHealthAndRegen()
        {
            var config = Dungeon(cloak: 5);
            var sim = new CinderSim(in config);
            Assert.That(sim.Level, Is.EqualTo(1));
            Assert.That(sim.Xp, Is.EqualTo(0));
            Assert.That(sim.XpNext, Is.EqualTo(30));
            Assert.That(sim.Player.Health, Is.EqualTo(140f).Within(Tolerance));

            bool levelled = false;
            for (int tick = 0; tick < 60 * 120 && !levelled; tick += 1)
            {
                sim.Tick(Pilot(sim, true));
                levelled = (sim.Events & SimEvents.LevelUp) != 0;
            }

            Assert.IsTrue(levelled, "three dungeon kills is 30 xp — that is level 2");
            Assert.That(sim.Level, Is.EqualTo(2));
            Assert.That(sim.XpNext, Is.EqualTo(55), "the curve moves on to 55");

            // §2.5: +6 max HP (healed), +4% damage, +0.3/s regen.
            var clustered = ClusteredWaveTwo();
            Assert.That(clustered.Player.Health, Is.LessThanOrEqualTo(100f + 80f + 40f + 6f));
            clustered.Tick(new SimInput { MoveX = -1f });
            int probeId = clustered.Enemies[0].Id;
            float before = clustered.Enemies[0].Health;
            var attack = new SimInput { AttackQueued = true };
            clustered.Tick(in attack);
            while ((clustered.Events & SimEvents.EnemyHit) == 0)
            {
                clustered.Tick(Idle);
            }
            float dealt = before - EnemyById(clustered, probeId).Health;
            Assert.That(dealt, Is.EqualTo(SimConfig.PlayerDamage * 1.04f).Within(1e-2f),
                "level 2 hits for 58 * 1.04");
        }

        // --- §3 elites and extraction --------------------------------------------

        /// <summary>
        /// Hold the attack key from the clustered state until the wave-2 elite falls.
        /// The player never moves, so the corpse it leaves stays inside channel range.
        /// </summary>
        private static CinderSim EliteCorpse(int rosterMask, out float corpseX, out float corpseY)
        {
            var sim = ClusteredWaveTwo(rosterMask);
            sim.Tick(new SimInput { MoveX = -1f });

            int eliteId = -1;
            for (int index = 0; index < sim.Enemies.Count; index += 1)
            {
                if (IsElite(sim.Enemies[index]))
                {
                    eliteId = sim.Enemies[index].Id;
                }
            }
            Assert.That(eliteId, Is.GreaterThan(0), "wave 2 carries the elite");

            var attack = new SimInput { AttackQueued = true };
            corpseX = 0f;
            corpseY = 0f;
            bool down = false;
            for (int tick = 0; tick < 60 * 20 && !down; tick += 1)
            {
                sim.Tick(in attack);
                if ((sim.Events & SimEvents.EliteDown) == 0)
                {
                    continue;
                }
                EnemyState elite = EnemyById(sim, eliteId);
                corpseX = elite.X;
                corpseY = elite.Y;
                down = true;
            }

            Assert.IsTrue(down, "the elite must fall");
            Assert.That(sim.Mode, Is.Not.EqualTo(SimMode.GameOver), "the player must survive the trade");
            Assert.That(sim.ElitesAlive, Is.EqualTo(0));

            // The finisher knockback drags the corpse a little; walk back onto it.
            for (int tick = 0; tick < 60 * 3; tick += 1)
            {
                float deltaX = corpseX - sim.Player.X;
                float deltaY = corpseY - sim.Player.Y;
                if (IsoDistance(corpseX, corpseY, sim.Player.X, sim.Player.Y) < 40f)
                {
                    break;
                }
                float length = MathF.Max(0.001f, MathF.Sqrt(deltaX * deltaX + deltaY * deltaY));
                sim.Tick(new SimInput { MoveX = deltaX / length, MoveY = deltaY / length });
            }
            return sim;
        }

        [Test]
        public void Elite_EverySeventhDungeonSpawnIsATripleHealthElite()
        {
            var config = Dungeon(attack: 10, vitality: 10, swiftness: 10, weapon: 5, lantern: 5, cloak: 5);
            var sim = new CinderSim(in config);

            var eliteIds = new System.Collections.Generic.List<int>();
            int maxElitesAlive = 0;
            for (int tick = 0; tick < 60 * 200; tick += 1)
            {
                sim.Tick(Pilot(sim, true));
                if (sim.ElitesAlive > maxElitesAlive)
                {
                    maxElitesAlive = sim.ElitesAlive;
                }
                for (int index = 0; index < sim.Enemies.Count; index += 1)
                {
                    EnemyState enemy = sim.Enemies[index];
                    if (!IsElite(enemy) || eliteIds.Contains(enemy.Id))
                    {
                        continue;
                    }
                    eliteIds.Add(enemy.Id);
                    Assert.That(enemy.Scale, Is.EqualTo(HackSpec.EliteScale).Within(Tolerance));
                }
                if (sim.Mode == SimMode.GameOver || eliteIds.Count >= 3)
                {
                    break;
                }
            }

            Assert.That(eliteIds.Count, Is.GreaterThanOrEqualTo(3));
            // Dungeon spawn ordinals run 1,2,3,... across the whole run and enemy ids
            // follow them one for one until the boss wave, so 7/14/21 are the elites.
            Assert.That(eliteIds[0], Is.EqualTo(7));
            Assert.That(eliteIds[1], Is.EqualTo(14));
            Assert.That(eliteIds[2], Is.EqualTo(21));
            Assert.That(maxElitesAlive, Is.EqualTo(1), "a wave never fields two elites");
        }

        [Test]
        public void Elite_HasTripleHealthAndOneAndAHalfContact()
        {
            var sim = ClusteredWaveTwo();
            int eliteId = -1;
            for (int index = 0; index < sim.Enemies.Count; index += 1)
            {
                if (IsElite(sim.Enemies[index]))
                {
                    eliteId = sim.Enemies[index].Id;
                    Assert.That(sim.Enemies[index].MaxHealth, Is.EqualTo(97f * 3f).Within(Tolerance),
                        "elite health is the wave baseline tripled");
                }
            }
            Assert.That(eliteId, Is.GreaterThan(0));

            // Clear the ordinary pack so the elite is the only thing that can land a hit.
            sim.Tick(new SimInput { MoveX = -1f });
            var attack = new SimInput { AttackQueued = true };
            for (int tick = 0; tick < 60 * 5 && sim.LivingEnemies > 1; tick += 1)
            {
                sim.Tick(in attack);
            }
            Assert.That(sim.LivingEnemies, Is.EqualTo(1), "only the elite is left");

            float health = sim.Player.Health;
            float drop = 0f;
            for (int tick = 0; tick < 60 * 5 && drop == 0f; tick += 1)
            {
                sim.Tick(Idle);
                if ((sim.Events & SimEvents.PlayerDamaged) != 0)
                {
                    drop = health - sim.Player.Health;
                }
                health = sim.Player.Health;
            }

            // Wave 2 contact is min(18, 7 + floor(2*0.8)) = 8, and the elite hits at 1.5x.
            Assert.That(drop, Is.EqualTo(8f * HackSpec.EliteDamageMul).Within(1e-3f));
        }

        [Test]
        public void Extraction_NeedsTwoStationarySecondsInsideNinetyPixels()
        {
            CinderSim sim = EliteCorpse(0, out float corpseX, out float corpseY);
            Assert.That(IsoDistance(corpseX, corpseY, sim.Player.X, sim.Player.Y),
                Is.LessThan(HackSpec.ExtractionRadius), "the player is standing on the marker");

            sim.Tick(Idle);
            Assert.That(sim.ExtractionTarget, Is.EqualTo(HackSpec.ExtractionSeconds),
                "a marker in range publishes the 2 s channel target");

            for (int tick = 0; tick < 60; tick += 1)
            {
                sim.Tick(Idle);
            }
            Assert.That(sim.ExtractionProgress, Is.GreaterThan(0.9f));
            Assert.That(sim.ExtractionProgress, Is.LessThan(HackSpec.ExtractionSeconds));
            Assert.That(sim.Events & SimEvents.ExtractionComplete, Is.EqualTo(SimEvents.None));

            // Any step breaks the channel: "정지 상태 2.0 s 연속".
            sim.Tick(new SimInput { MoveX = 1f });
            Assert.That(sim.ExtractionProgress, Is.EqualTo(0f), "moving resets the channel");

            // Walking out of the 90 px ring drops the target entirely.
            for (int tick = 0; tick < 60; tick += 1)
            {
                sim.Tick(new SimInput { MoveX = 1f });
            }
            Assert.That(IsoDistance(corpseX, corpseY, sim.Player.X, sim.Player.Y),
                Is.GreaterThan(HackSpec.ExtractionRadius));
            Assert.That(sim.ExtractionTarget, Is.EqualTo(0f), "no marker in range, no channel");
        }

        [Test]
        public void Extraction_ChannelResetsWhenTheNextWaveLandsAHit()
        {
            CinderSim sim = EliteCorpse(0, out _, out _);

            // Jog whenever the channel is about to finish, so the marker survives long
            // enough for wave 3 to walk in and interrupt a channel that is already going.
            bool sawDamageReset = false;
            float previousProgress = 0f;
            for (int tick = 0; tick < 60 * 12 && !sawDamageReset; tick += 1)
            {
                bool jog = sim.ExtractionProgress > 1.5f;
                sim.Tick(jog ? new SimInput { MoveX = 1f } : Idle);
                if (!jog && (sim.Events & SimEvents.PlayerDamaged) != 0 && previousProgress > 0.05f)
                {
                    Assert.That(sim.ExtractionTarget, Is.EqualTo(HackSpec.ExtractionSeconds),
                        "the marker is still in range — only the channel was interrupted");
                    Assert.That(sim.ExtractionProgress, Is.EqualTo(0f),
                        "taking a hit mid-channel resets the extraction");
                    sawDamageReset = true;
                }
                previousProgress = sim.ExtractionProgress;
            }

            Assert.IsTrue(sawDamageReset, "wave 3 must interrupt a running channel");
        }

        [Test]
        public void Extraction_NewVisualJoinsTheRosterAndBuffsRunDamage()
        {
            CinderSim sim = EliteCorpse(0, out _, out _);
            Assert.That(sim.RosterMask, Is.EqualTo(0), "the run started with an empty roster");
            int relicsBefore = sim.Relics;

            bool extracted = false;
            for (int tick = 0; tick < 60 * 4 && !extracted; tick += 1)
            {
                sim.Tick(Idle);
                extracted = (sim.Events & SimEvents.ExtractionComplete) != 0;
            }

            Assert.IsTrue(extracted, "two stationary seconds must finish the channel");
            // The wave-2 elite is the ember-cohort visual (bit 0).
            Assert.That(sim.RosterMask, Is.EqualTo(1 << (int)EnemyVisual.EmberCohort));
            Assert.That(sim.Relics, Is.EqualTo(relicsBefore), "a new visual pays in roster, not relics");
            Assert.That(sim.ExtractionProgress, Is.EqualTo(0f));

            // §3: the new echo buffs this run's damage by 8%.
            float expected = SimConfig.PlayerDamage * (1f + HackSpec.LevelDamageBonus * (sim.Level - 1))
                * (1f + HackSpec.ExtractionDamageBonus);
            AssertNextSwingDamage(sim, expected);
        }

        [Test]
        public void Extraction_DuplicateVisualPaysThirtyRelics()
        {
            int owned = 1 << (int)EnemyVisual.EmberCohort;
            CinderSim sim = EliteCorpse(owned, out _, out _);
            Assert.That(sim.RosterMask, Is.EqualTo(owned));
            int relicsBefore = sim.Relics;

            bool extracted = false;
            for (int tick = 0; tick < 60 * 4 && !extracted; tick += 1)
            {
                sim.Tick(Idle);
                extracted = (sim.Events & SimEvents.ExtractionComplete) != 0;
            }

            Assert.IsTrue(extracted);
            Assert.That(sim.RosterMask, Is.EqualTo(owned), "the roster is unchanged");
            Assert.That(sim.Relics - relicsBefore, Is.EqualTo(HackSpec.ExtractionDuplicateRelics));

            // No damage buff on the duplicate branch.
            float expected = SimConfig.PlayerDamage * (1f + HackSpec.LevelDamageBonus * (sim.Level - 1));
            AssertNextSwingDamage(sim, expected);
        }

        /// <summary>Swing once and check the first combo hit lands for the given damage.</summary>
        private static void AssertNextSwingDamage(CinderSim sim, float expected)
        {
            var faceAndWait = new SimInput { MoveX = -1f };
            int targetId = -1;
            for (int tick = 0; tick < 60 * 20 && targetId < 0; tick += 1)
            {
                sim.Tick(in faceAndWait);
                for (int index = 0; index < sim.Enemies.Count; index += 1)
                {
                    EnemyState enemy = sim.Enemies[index];
                    if (enemy.Dead
                        || enemy.Health < enemy.MaxHealth
                        || IsoDistance(enemy.X, enemy.Y, sim.Player.X, sim.Player.Y) > 120f
                        || (enemy.X - sim.Player.X) * sim.Player.Facing < SimConfig.FacingArcTolerance)
                    {
                        continue;
                    }
                    targetId = enemy.Id;
                    break;
                }
            }
            Assert.That(targetId, Is.GreaterThan(0), "the next wave must walk into range");

            float before = EnemyById(sim, targetId).Health;
            sim.Tick(new SimInput { AttackQueued = true });
            for (int tick = 0; tick < 30 && (sim.Events & SimEvents.EnemyHit) == 0; tick += 1)
            {
                sim.Tick(Idle);
            }
            float dealt = before - EnemyById(sim, targetId).Health;
            Assert.That(dealt, Is.EqualTo(expected).Within(1e-2f));
        }

        /// <summary>Swing at the in-range first-wave fixture without waiting for a new spawn.</summary>
        private static void AssertInRangeSwingDamage(CinderSim sim, float expected)
        {
            int targetId = -1;
            float nearest = float.MaxValue;
            for (int index = 0; index < sim.Enemies.Count; index += 1)
            {
                EnemyState enemy = sim.Enemies[index];
                float distance = IsoDistance(enemy.X, enemy.Y, sim.Player.X, sim.Player.Y);
                if (!enemy.Dead && enemy.Health == enemy.MaxHealth && distance < nearest)
                {
                    targetId = enemy.Id;
                    nearest = distance;
                }
            }

            Assert.That(targetId, Is.GreaterThan(0), "the in-range fixture must retain an untouched target");
            EnemyState target = EnemyById(sim, targetId);
            sim.Tick(new SimInput { MoveX = target.X > sim.Player.X ? 1f : -1f });
            target = EnemyById(sim, targetId);
            Assert.That(target.Dead, Is.False, "the fixture target survives through the facing tick");
            Assert.That(target.Health, Is.EqualTo(target.MaxHealth), "the fixture target is untouched");
            Assert.That(IsoDistance(target.X, target.Y, sim.Player.X, sim.Player.Y), Is.LessThanOrEqualTo(120f));
            Assert.That((target.X - sim.Player.X) * sim.Player.Facing,
                Is.GreaterThanOrEqualTo(SimConfig.FacingArcTolerance));

            float before = target.Health;
            sim.Tick(new SimInput { AttackQueued = true });
            for (int tick = 0; tick < 30 && (sim.Events & SimEvents.EnemyHit) == 0; tick += 1)
            {
                sim.Tick(Idle);
            }
            float dealt = before - EnemyById(sim, targetId).Health;
            Assert.That(dealt, Is.EqualTo(expected).Within(1e-2f));
        }

        [Test]
        public void GravePulse_LastsExactlyThreeSecondsOnADurableTarget()
        {
            var sim = ClusteredWaveTwo();
            sim.Tick(new SimInput { PulseQueued = true });

            int ticks = 0;
            int lastAt = -1;
            for (int tick = 1; tick <= 240; tick += 1)
            {
                sim.Tick(Idle);
                if ((sim.Events & SimEvents.EnemyHit) == 0)
                {
                    continue;
                }
                ticks += 1;
                lastAt = tick;
            }

            // The 291 hp elite outlives the field, so every tick of it is observable.
            Assert.That(ticks, Is.EqualTo(6), "3 s at 0.5 s is exactly six ticks");
            Assert.That(lastAt, Is.EqualTo(179), "the last tick lands 3 s after the cast");
        }

        // --- §4 companion ---------------------------------------------------------

        [Test]
        public void Companion_TrailsThePlayerAndAttacksOnItsOwnCadence()
        {
            var withCompanion = Dungeon(cloak: 5, companionId: "ember-cohort");
            var sim = new CinderSim(in withCompanion);

            Assert.That(sim.CompanionX,
                Is.EqualTo(sim.Player.X - HackSpec.CompanionFollowOffset * sim.Player.Facing).Within(Tolerance),
                "the companion starts parked at its 80 px offset");
            Assert.That(sim.CompanionY, Is.EqualTo(sim.Player.Y).Within(Tolerance));

            // The offset flips with the player's facing and the companion catches up.
            for (int tick = 0; tick < 120; tick += 1)
            {
                sim.Tick(new SimInput { MoveX = -1f });
            }
            Assert.That(sim.Player.Facing, Is.EqualTo(-1));
            Assert.That(sim.CompanionX - sim.Player.X,
                Is.EqualTo(HackSpec.CompanionFollowOffset).Within(2f),
                "the companion trails on the side the player turned away from");

            var noCompanion = Dungeon(cloak: 5);
            var control = new CinderSim(in noCompanion);
            for (int tick = 0; tick < 120; tick += 1)
            {
                control.Tick(new SimInput { MoveX = -1f });
            }

            // 60% of the player's damage, once every 1.1 s.
            float expected = SimConfig.PlayerDamage * HackSpec.CompanionDamageScale;
            int firstHitAt = -1;
            int secondHitAt = -1;
            for (int tick = 0; tick < 60 * 30; tick += 1)
            {
                sim.Tick(Idle);
                control.Tick(Idle);
                if ((sim.Events & SimEvents.EnemyHit) == 0)
                {
                    continue;
                }
                Assert.IsTrue(sim.CompanionAttacking, "the snapshot flags the swing for the view");
                if (firstHitAt < 0)
                {
                    firstHitAt = tick;
                }
                else if (secondHitAt < 0)
                {
                    secondHitAt = tick;
                    break;
                }
            }

            Assert.That(firstHitAt, Is.GreaterThan(0), "the companion must engage on its own");
            Assert.That((secondHitAt - firstHitAt) * SimConfig.FixedStep,
                Is.EqualTo(HackSpec.CompanionAttackInterval).Within(SimConfig.FixedStep),
                "the companion swings once every 1.1 s");
            Assert.That(control.Events & SimEvents.EnemyHit, Is.EqualTo(SimEvents.None),
                "the companion-less control never damages anything on idle input");

            // Damage: the first companion swing is 60% of a 58 base hit.
            var fresh = new CinderSim(in withCompanion);
            var beforeIds = new System.Collections.Generic.List<int>();
            var beforeHealth = new System.Collections.Generic.List<float>();
            float dealt = -1f;
            for (int tick = 0; tick < 60 * 30 && dealt < 0f; tick += 1)
            {
                beforeIds.Clear();
                beforeHealth.Clear();
                for (int index = 0; index < fresh.Enemies.Count; index += 1)
                {
                    beforeIds.Add(fresh.Enemies[index].Id);
                    beforeHealth.Add(fresh.Enemies[index].Health);
                }

                fresh.Tick(Idle);
                if ((fresh.Events & SimEvents.EnemyHit) == 0)
                {
                    continue;
                }

                for (int index = 0; index < fresh.Enemies.Count; index += 1)
                {
                    EnemyState enemy = fresh.Enemies[index];
                    int slot = beforeIds.IndexOf(enemy.Id);
                    if (slot < 0 || beforeHealth[slot] <= enemy.Health)
                    {
                        continue;
                    }
                    dealt = beforeHealth[slot] - enemy.Health;
                }
            }
            Assert.That(dealt, Is.EqualTo(expected).Within(1e-2f),
                "the companion hits for 60% of the player's attack");
        }

        [Test]
        public void Companion_AttackingLeftTarget_PublishesLeftFacing()
        {
            var config = Dungeon(companionId: "ember-cohort");
            var sim = new CinderSim(in config);
            var beforeIds = new System.Collections.Generic.List<int>();
            var beforeHealth = new System.Collections.Generic.List<float>();
            bool sawVisibleAttackAgainstLeftTarget = false;

            for (int tick = 0; tick < 60 * 6; tick += 1)
            {
                beforeIds.Clear();
                beforeHealth.Clear();
                for (int index = 0; index < sim.Enemies.Count; index += 1)
                {
                    beforeIds.Add(sim.Enemies[index].Id);
                    beforeHealth.Add(sim.Enemies[index].Health);
                }

                sim.Tick(new SimInput { MoveX = 1f });
                if ((sim.Events & SimEvents.EnemyHit) == SimEvents.None)
                {
                    continue;
                }

                EnemyState damaged = default;
                bool foundDamaged = false;
                for (int index = 0; index < sim.Enemies.Count; index += 1)
                {
                    EnemyState enemy = sim.Enemies[index];
                    int beforeIndex = beforeIds.IndexOf(enemy.Id);
                    if (beforeIndex < 0 || enemy.Health >= beforeHealth[beforeIndex])
                    {
                        continue;
                    }
                    damaged = enemy;
                    foundDamaged = true;
                    break;
                }

                if (!foundDamaged || damaged.X >= sim.CompanionX)
                {
                    continue;
                }

                sawVisibleAttackAgainstLeftTarget = true;
                Assert.That(sim.CompanionAttacking, Is.True,
                    "the target-facing direction is published while the companion swing is visible");
                Assert.That(sim.CompanionFacing, Is.EqualTo(-1),
                    "a visible companion attack must face its damaged target to the left");
                break;
            }

            Assert.That(sawVisibleAttackAgainstLeftTarget, Is.True,
                "the deterministic rightward input must produce a visible companion attack against a target to its left");
        }

        [Test]
        public void Companion_HoldLocksCoordinatesWhileRetainingItsAttackCadence()
        {
            var config = Dungeon(cloak: 5, companionId: "ember-cohort");
            var sim = new CinderSim(in config);
            for (int tick = 0; tick < 120; tick += 1)
            {
                sim.Tick(new SimInput { MoveX = -1f });
            }

            sim.Tick(new SimInput { CompanionHoldQueued = true });
            IHackSnapshot heldSnapshot = sim;
            float heldX = sim.CompanionX;
            float heldY = sim.CompanionY;
            Assert.That(heldSnapshot.CompanionBehavior, Is.EqualTo(CompanionBehavior.Hold));

            int firstHitAt = -1;
            int secondHitAt = -1;
            for (int tick = 0; tick < 60 * 4; tick += 1)
            {
                sim.Tick(Idle);
                Assert.That(sim.CompanionX, Is.EqualTo(heldX).Within(Tolerance), "hold must retain x");
                Assert.That(sim.CompanionY, Is.EqualTo(heldY).Within(Tolerance), "hold must retain y");
                if ((sim.Events & SimEvents.EnemyHit) == SimEvents.None)
                {
                    continue;
                }

                Assert.That(sim.CompanionAttacking, Is.True,
                    "holding only stops follower movement, not companion combat");
                if (firstHitAt < 0)
                {
                    firstHitAt = tick;
                }
                else
                {
                    secondHitAt = tick;
                    break;
                }
            }

            Assert.That(firstHitAt, Is.GreaterThanOrEqualTo(0), "the held companion must still engage");
            Assert.That(secondHitAt, Is.GreaterThan(firstHitAt), "the held companion must attack more than once");
            Assert.That((secondHitAt - firstHitAt) * SimConfig.FixedStep,
                Is.EqualTo(HackSpec.CompanionAttackInterval).Within(SimConfig.FixedStep),
                "hold preserves the existing 1.1 s attack cadence");
        }

        [Test]
        public void Companion_RecallResumesFollowingWithoutTeleporting()
        {
            var config = Dungeon(cloak: 5, companionId: "ember-cohort");
            var sim = new CinderSim(in config);
            for (int tick = 0; tick < 120; tick += 1)
            {
                sim.Tick(new SimInput { MoveX = -1f });
            }
            sim.Tick(new SimInput { CompanionHoldQueued = true });
            float heldX = sim.CompanionX;
            float heldY = sim.CompanionY;

            for (int tick = 0; tick < 120; tick += 1)
            {
                sim.Tick(new SimInput { MoveX = 1f });
            }
            Assert.That(sim.CompanionX, Is.EqualTo(heldX).Within(Tolerance), "hold must survive player movement");
            Assert.That(sim.CompanionY, Is.EqualTo(heldY).Within(Tolerance), "hold must survive player movement");

            float targetX = sim.Player.X - HackSpec.CompanionFollowOffset * sim.Player.Facing;
            float targetY = sim.Player.Y;
            float distanceBeforeRecall = MathF.Sqrt(
                (targetX - sim.CompanionX) * (targetX - sim.CompanionX)
                + (targetY - sim.CompanionY) * (targetY - sim.CompanionY));
            sim.Tick(new SimInput { CompanionRecallQueued = true });
            float recallStep = MathF.Sqrt(
                (sim.CompanionX - heldX) * (sim.CompanionX - heldX)
                + (sim.CompanionY - heldY) * (sim.CompanionY - heldY));
            float distanceAfterRecall = MathF.Sqrt(
                (targetX - sim.CompanionX) * (targetX - sim.CompanionX)
                + (targetY - sim.CompanionY) * (targetY - sim.CompanionY));

            Assert.That(sim.CompanionBehavior, Is.EqualTo(CompanionBehavior.Follow));
            Assert.That(recallStep, Is.GreaterThan(0f), "recall must resume normal movement");
            Assert.That(recallStep, Is.LessThanOrEqualTo(config.PlayerSpeed * SimConfig.FixedStep + Tolerance),
                "recall must take a normal follower step instead of teleporting");
            Assert.That(distanceAfterRecall, Is.LessThan(distanceBeforeRecall), "recall must close the follow gap");

            for (int tick = 0; tick < 120; tick += 1)
            {
                sim.Tick(Idle);
            }
            Assert.That(sim.CompanionX,
                Is.EqualTo(sim.Player.X - HackSpec.CompanionFollowOffset * sim.Player.Facing).Within(Tolerance));
            Assert.That(sim.CompanionY, Is.EqualTo(sim.Player.Y).Within(Tolerance));
        }

        [Test]
        public void Companion_SimultaneousHoldAndRecallResolvesToRecall()
        {
            var config = Dungeon(companionId: "ember-cohort");
            var control = new CinderSim(in config);
            var simultaneous = new CinderSim(in config);
            for (int tick = 0; tick < 90; tick += 1)
            {
                var input = new SimInput { MoveX = -1f };
                control.Tick(in input);
                simultaneous.Tick(in input);
            }

            control.Tick(Idle);
            simultaneous.Tick(new SimInput { CompanionHoldQueued = true, CompanionRecallQueued = true });

            Assert.That(simultaneous.CompanionBehavior, Is.EqualTo(CompanionBehavior.Follow));
            Assert.That(simultaneous.CompanionX, Is.EqualTo(control.CompanionX).Within(Tolerance));
            Assert.That(simultaneous.CompanionY, Is.EqualTo(control.CompanionY).Within(Tolerance));
            Assert.That(simultaneous.Events, Is.EqualTo(control.Events),
                "hold/recall inputs must not create a new event when recall wins");
            AssertSameDigest(control.Digest, simultaneous.Digest, "simultaneous commands must match a follow control");
        }

        [Test]
        public void Companion_RedundantCommandsAreNoOpsAndRestartResetsFollow()
        {
            var config = Dungeon(companionId: "ember-cohort");
            var following = new CinderSim(in config);
            var followControl = new CinderSim(in config);
            following.Tick(new SimInput { CompanionRecallQueued = true });
            followControl.Tick(Idle);
            Assert.That(following.CompanionBehavior, Is.EqualTo(CompanionBehavior.Follow));
            Assert.That(following.CompanionX, Is.EqualTo(followControl.CompanionX).Within(Tolerance));
            Assert.That(following.CompanionY, Is.EqualTo(followControl.CompanionY).Within(Tolerance));
            Assert.That(following.Events, Is.EqualTo(followControl.Events));

            following.Tick(new SimInput { CompanionHoldQueued = true });
            followControl.Tick(new SimInput { CompanionHoldQueued = true });
            following.Tick(new SimInput { CompanionHoldQueued = true });
            followControl.Tick(Idle);
            Assert.That(following.CompanionBehavior, Is.EqualTo(CompanionBehavior.Hold));
            Assert.That(following.CompanionX, Is.EqualTo(followControl.CompanionX).Within(Tolerance));
            Assert.That(following.CompanionY, Is.EqualTo(followControl.CompanionY).Within(Tolerance));
            Assert.That(following.Events, Is.EqualTo(followControl.Events));

            following.Tick(new SimInput { RestartQueued = true });
            Assert.That(following.CompanionBehavior, Is.EqualTo(CompanionBehavior.Follow));
            Assert.That(following.CompanionX,
                Is.EqualTo(following.Player.X - HackSpec.CompanionFollowOffset * following.Player.Facing).Within(Tolerance));
            Assert.That(following.CompanionY, Is.EqualTo(following.Player.Y).Within(Tolerance));
        }

        [Test]
        public void Companion_CommandsAreInertOutsideDungeonAndWithoutAnActiveCompanion()
        {
            var arena = HackConfig.Arena();
            arena.CompanionId = "ember-cohort";
            AssertCompanionCommandsAreInert(arena, "arena");

            var prologue = HackConfig.Prologue();
            prologue.CompanionId = "ember-cohort";
            AssertCompanionCommandsAreInert(prologue, "prologue");

            AssertCompanionCommandsAreInert(Dungeon(), "dungeon without a companion");
        }

        [Test]
        public void Companion_CommandSequencesProduceIdenticalSnapshotsAndDigests()
        {
            var config = Dungeon(attack: 2, vitality: 1, swiftness: 3, weapon: 1, lantern: 2, cloak: 2,
                companionId: "ember-cohort");
            var first = new CinderSim(in config);
            var second = new CinderSim(in config);
            RunDigest firstDigest = RunCompanionCommandSequence(first);
            RunDigest secondDigest = RunCompanionCommandSequence(second);

            AssertSameDigest(firstDigest, secondDigest, "hold/recall command replay");
            Assert.That(first.CompanionBehavior, Is.EqualTo(second.CompanionBehavior));
            Assert.That(first.CompanionX, Is.EqualTo(second.CompanionX).Within(Tolerance));
            Assert.That(first.CompanionY, Is.EqualTo(second.CompanionY).Within(Tolerance));
            Assert.That(first.CompanionFacing, Is.EqualTo(second.CompanionFacing));
            Assert.That(first.CompanionAttacking, Is.EqualTo(second.CompanionAttacking));
            Assert.That(first.Player.X, Is.EqualTo(second.Player.X).Within(Tolerance));
            Assert.That(first.Player.Y, Is.EqualTo(second.Player.Y).Within(Tolerance));
        }

        [Test]
        public void EmberRest_RequiresClearedDungeon_RepeatsOffersAndPersistsSelectionAfterClose()
        {
            var unclearedConfig = Dungeon();
            var uncleared = new CinderSim(in unclearedConfig);
            Assert.That(uncleared.BeginEmberRest(2, 173), Is.False,
                "Ember Rest cannot open before a dungeon stage is cleared");

            var sim = ClearedCinderSpan();
            IRunPreparationSnapshot rest = sim;
            const int RoomIndex = 2;
            const int RewardSeed = 173;

            Assert.That(sim.BeginEmberRest(RoomIndex, RewardSeed), Is.True,
                "a cleared dungeon stage can open Ember Rest");
            PreparationOffer first0 = rest.EmberRestOffer0;
            PreparationOffer first1 = rest.EmberRestOffer1;
            PreparationOffer first2 = rest.EmberRestOffer2;
            Assert.That(first0.IsValid && first1.IsValid && first2.IsValid, Is.True,
                "an open Ember Rest publishes all three selectable offers");

            Assert.That(sim.EndEmberRest(), Is.True);
            Assert.That(rest.EmberRestOpen, Is.False,
                "ending an Ember Rest removes the active choice state");

            Assert.That(sim.BeginEmberRest(RoomIndex, RewardSeed), Is.True,
                "the same cleared stage can revisit Ember Rest after the prior choice closes");
            AssertSamePreparationOffer(first0, rest.EmberRestOffer0, "first offer");
            AssertSamePreparationOffer(first1, rest.EmberRestOffer1, "second offer");
            AssertSamePreparationOffer(first2, rest.EmberRestOffer2, "third offer");

            Assert.That(sim.TrySelectPreparation(1), Is.True);
            Assert.That(sim.EndEmberRest(), Is.True);
            Assert.That(rest.EmberRestOpen, Is.False,
                "selection closes through the public Ember Rest exit");
            AssertSamePreparationOffer(first1, rest.SelectedPreparation,
                "the selected offer persists after the rest closes");
        }

        [Test]
        public void EmberRest_LegacyRoomsPersistTheirSelectedOffer()
        {
            var sim = ClearedCinderSpan();
            IRunPreparationSnapshot rest = sim;
            for (int roomIndex = 1; roomIndex <= 3; roomIndex += 1)
            {
                int selectedIndex = roomIndex - 1;
                int rewardSeed = 173 + roomIndex;
                Assert.That(sim.BeginEmberRest(roomIndex, rewardSeed), Is.True, $"room {roomIndex} must remain available");

                PreparationOffer expected;
                switch (selectedIndex)
                {
                    case 0: expected = rest.EmberRestOffer0; break;
                    case 1: expected = rest.EmberRestOffer1; break;
                    default: expected = rest.EmberRestOffer2; break;
                }

                Assert.That(sim.TrySelectPreparation(selectedIndex), Is.True, $"room {roomIndex} selection");
                Assert.That(sim.EndEmberRest(), Is.True, $"room {roomIndex} close");
                AssertSamePreparationOffer(expected, rest.SelectedPreparation, $"room {roomIndex} selected offer");
            }
        }

        [Test]
        public void EmberRest_ExtendedRoomsAreAvailableAndRepeatTheirOffersDeterministically()
        {
            var sim = ClearedCinderSpan();
            IRunPreparationSnapshot rest = sim;
            Assert.That(sim.BeginEmberRest(6, 173), Is.False, "room six remains outside the 1..5 contract");

            for (int roomIndex = 4; roomIndex <= 5; roomIndex += 1)
            {
                int rewardSeed = 173 + roomIndex;
                Assert.That(sim.BeginEmberRest(roomIndex, rewardSeed), Is.True, $"room {roomIndex} must be available");
                Assert.That(rest.EmberRestRoomIndex, Is.EqualTo(roomIndex));
                PreparationOffer first0 = rest.EmberRestOffer0;
                PreparationOffer first1 = rest.EmberRestOffer1;
                PreparationOffer first2 = rest.EmberRestOffer2;
                Assert.That(first0.IsValid && first1.IsValid && first2.IsValid, Is.True,
                    $"room {roomIndex} must publish all selectable offers");

                Assert.That(sim.EndEmberRest(), Is.True);
                Assert.That(sim.BeginEmberRest(roomIndex, rewardSeed), Is.True,
                    $"room {roomIndex} must reopen with the same seed");
                AssertSamePreparationOffer(first0, rest.EmberRestOffer0, $"room {roomIndex} first offer");
                AssertSamePreparationOffer(first1, rest.EmberRestOffer1, $"room {roomIndex} second offer");
                AssertSamePreparationOffer(first2, rest.EmberRestOffer2, $"room {roomIndex} third offer");
                Assert.That(sim.EndEmberRest(), Is.True);
            }
        }

        [Test]
        public void EmberRestPreparation_StatVariantsApplyOnlyTheirCappedDestinationStat()
        {
            for (int variant = 1; variant <= 3; variant += 1)
            {
                for (int magnitude = 1; magnitude <= 2; magnitude += 1)
                {
                    var config = Dungeon(attack: 9, vitality: 9, swiftness: 9);
                    config.PreparationOffer = Preparation(PreparationOfferKind.Stat, variant, magnitude);
                    var sim = FirstWaveEnemyInRange(config);
                    int expectedAttack = variant == 1 ? Math.Min(10, 9 + magnitude) : 9;
                    int expectedVitality = variant == 2 ? Math.Min(10, 9 + magnitude) : 9;
                    int expectedSwiftness = variant == 3 ? Math.Min(10, 9 + magnitude) : 9;

                    Assert.That(sim.Player.Health,
                        Is.EqualTo(SimConfig.PlayerMaxHealth + HackSpec.VitalityHealthPerPoint * expectedVitality)
                            .Within(Tolerance),
                        $"stat variant {variant}, magnitude {magnitude} vitality");

                    float x = sim.Player.X;
                    sim.Tick(new SimInput { MoveX = 1f });
                    Assert.That(sim.Player.X - x,
                        Is.EqualTo(SimConfig.PlayerSpeed
                            * (1f + HackSpec.SwiftnessSpeedPerPoint * expectedSwiftness)
                            * SimConfig.FixedStep).Within(Tolerance),
                        $"stat variant {variant}, magnitude {magnitude} swiftness");

                    AssertInRangeSwingDamage(
                        sim,
                        SimConfig.PlayerDamage * (1f + HackSpec.AttackPerPoint * expectedAttack));
                }
            }
        }

        [Test]
        public void EmberRestPreparation_SkillRuneVariantsMultiplyOnlyTheirSelectedSkillDamage()
        {
            for (int magnitude = 1; magnitude <= 2; magnitude += 1)
            {
                float multiplier = 1f + 0.10f * magnitude;

                var bolt = ClusteredWaveTwo(
                    preparation: Preparation(PreparationOfferKind.SkillRune, 1, magnitude));
                bolt.Tick(new SimInput { MoveX = -1f });
                int primaryId = -1;
                float primaryDistance = float.MaxValue;
                int boltEliteId = -1;
                for (int index = 0; index < bolt.Enemies.Count; index += 1)
                {
                    EnemyState enemy = bolt.Enemies[index];
                    float distance = IsoDistance(enemy.X, enemy.Y, bolt.Player.X, bolt.Player.Y);
                    if (distance < primaryDistance)
                    {
                        primaryDistance = distance;
                        primaryId = enemy.Id;
                    }
                    if (IsElite(enemy))
                    {
                        boltEliteId = enemy.Id;
                    }
                }
                EnemyState primary = EnemyById(bolt, primaryId);
                EnemyState boltElite = EnemyById(bolt, boltEliteId);
                Assert.That(boltEliteId, Is.GreaterThan(0), "clustered wave contains a durable bolt target");
                Assert.That(boltEliteId == primaryId
                        || IsoDistance(boltElite.X, boltElite.Y, primary.X, primary.Y) <= HackSpec.BoltSplashRadius,
                    Is.True,
                    "the durable elite receives either the bolt primary or its splash");
                bolt.Tick(new SimInput { BoltQueued = true });
                float boltScale = boltEliteId == primaryId ? 1f : HackSpec.BoltSplashScale;
                float boltExpected = HackSpec.BoltDamage * multiplier * boltScale
                    * HackSpec.Matchup(Element.Void, HackSpec.ElementOf(boltElite.Visual));
                Assert.That(boltElite.MaxHealth - EnemyById(bolt, boltEliteId).Health,
                    Is.EqualTo(boltExpected).Within(1e-2f),
                    $"rift bolt magnitude {magnitude}");

                var pulse = ClusteredWaveTwo(
                    preparation: Preparation(PreparationOfferKind.SkillRune, 2, magnitude));
                pulse.Tick(new SimInput { MoveX = -1f });
                int pulseEliteId = -1;
                for (int index = 0; index < pulse.Enemies.Count; index += 1)
                {
                    if (IsElite(pulse.Enemies[index]))
                    {
                        pulseEliteId = pulse.Enemies[index].Id;
                        break;
                    }
                }
                pulse.Tick(new SimInput { PulseQueued = true });
                for (int tick = 0; tick < 30 && (pulse.Events & SimEvents.EnemyHit) == SimEvents.None; tick += 1)
                {
                    pulse.Tick(Idle);
                }
                EnemyState pulseElite = EnemyById(pulse, pulseEliteId);
                float pulseExpected = HackSpec.PulseTickDamage * multiplier
                    * HackSpec.Matchup(Element.Ember, HackSpec.ElementOf(pulseElite.Visual));
                Assert.That(pulseElite.MaxHealth - pulseElite.Health,
                    Is.EqualTo(pulseExpected).Within(1e-2f),
                    $"grave pulse tick magnitude {magnitude}");

                var nova = ClusteredWaveTwo(
                    preparation: Preparation(PreparationOfferKind.SkillRune, 3, magnitude));
                nova.Tick(new SimInput { MoveX = -1f });
                int novaEliteId = -1;
                for (int index = 0; index < nova.Enemies.Count; index += 1)
                {
                    if (IsElite(nova.Enemies[index]))
                    {
                        novaEliteId = nova.Enemies[index].Id;
                        break;
                    }
                }
                EnemyState novaEliteBefore = EnemyById(nova, novaEliteId);
                Assert.That(IsoDistance(novaEliteBefore.X, novaEliteBefore.Y, nova.Player.X, nova.Player.Y),
                    Is.LessThanOrEqualTo(HackSpec.AshNovaRadius), "the durable elite is inside the nova");
                nova.Tick(new SimInput { NovaQueued = true });
                EnemyState novaElite = EnemyById(nova, novaEliteId);
                float novaExpected = HackSpec.AshNovaDamage * multiplier
                    * HackSpec.Matchup(Element.Ember, HackSpec.ElementOf(novaElite.Visual));
                Assert.That(novaElite.MaxHealth - novaElite.Health,
                    Is.EqualTo(novaExpected).Within(1e-2f),
                    $"ash nova magnitude {magnitude}");
            }
        }

        [Test]
        public void EmberRestPreparation_GuardianResonanceUsesOnlyItsSelectedCompanionValue()
        {
            for (int magnitude = 1; magnitude <= 2; magnitude += 1)
            {
                var cadenceConfig = Dungeon(companionId: "ember-cohort");
                cadenceConfig.PreparationOffer = Preparation(PreparationOfferKind.GuardianResonance, 1, magnitude);
                var cadence = new CinderSim(in cadenceConfig);
                int firstHitAt = -1;
                int secondHitAt = -1;
                for (int tick = 0; tick < 60 * 30 && secondHitAt < 0; tick += 1)
                {
                    cadence.Tick(Idle);
                    if ((cadence.Events & SimEvents.EnemyHit) == SimEvents.None || !cadence.CompanionAttacking)
                    {
                        continue;
                    }
                    if (firstHitAt < 0)
                    {
                        firstHitAt = tick;
                    }
                    else
                    {
                        secondHitAt = tick;
                    }
                }
                Assert.That(firstHitAt, Is.GreaterThanOrEqualTo(0), $"cadence magnitude {magnitude} first hit");
                Assert.That(secondHitAt, Is.GreaterThan(firstHitAt), $"cadence magnitude {magnitude} second hit");
                float expectedCadence = MathF.Max(0.5f,
                    HackSpec.CompanionAttackInterval * (1f - 0.10f * magnitude));
                Assert.That((secondHitAt - firstHitAt) * SimConfig.FixedStep,
                    Is.EqualTo(expectedCadence).Within(SimConfig.FixedStep),
                    $"guardian cadence magnitude {magnitude}");

                var rangeConfig = Dungeon(companionId: "ember-cohort");
                rangeConfig.PreparationOffer = Preparation(PreparationOfferKind.GuardianResonance, 2, magnitude);
                var ordinaryRangeConfig = Dungeon(companionId: "ember-cohort");
                var ordinaryRange = new CinderSim(in ordinaryRangeConfig);
                var preparedRange = new CinderSim(in rangeConfig);
                float extendedRange = HackSpec.CompanionAttackRange + 20f * magnitude;
                bool hitOnlyInExtendedBand = false;
                for (int tick = 0; tick < 60 * 60 && !hitOnlyInExtendedBand; tick += 1)
                {
                    float nearest = float.MaxValue;
                    for (int index = 0; index < preparedRange.Enemies.Count; index += 1)
                    {
                        EnemyState enemy = preparedRange.Enemies[index];
                        if (!enemy.Dead)
                        {
                            nearest = MathF.Min(nearest,
                                IsoDistance(enemy.X, enemy.Y, preparedRange.CompanionX, preparedRange.CompanionY));
                        }
                    }

                    SimInput input = new SimInput
                    {
                        MoveX = tick < 180 ? 1f : -1f,
                        CompanionHoldQueued = tick == 0,
                    };
                    ordinaryRange.Tick(in input);
                    preparedRange.Tick(in input);
                    if ((preparedRange.Events & SimEvents.EnemyHit) == SimEvents.None
                        || !preparedRange.CompanionAttacking)
                    {
                        continue;
                    }

                    Assert.That(nearest, Is.LessThanOrEqualTo(extendedRange + Tolerance),
                        $"guardian range magnitude {magnitude} must not attack beyond its stated range");
                    if (nearest > HackSpec.CompanionAttackRange + Tolerance
                        && (ordinaryRange.Events & SimEvents.EnemyHit) == SimEvents.None)
                    {
                        hitOnlyInExtendedBand = true;
                    }
                }
                Assert.That(hitOnlyInExtendedBand, Is.True,
                    $"guardian range magnitude {magnitude} must reach an enemy beyond 200 px");

                var damageConfig = Dungeon(companionId: "ember-cohort");
                damageConfig.PreparationOffer = Preparation(PreparationOfferKind.GuardianResonance, 3, magnitude);
                var damage = new CinderSim(in damageConfig);
                float dealt = -1f;
                var ids = new System.Collections.Generic.List<int>();
                var health = new System.Collections.Generic.List<float>();
                for (int tick = 0; tick < 60 * 30 && dealt < 0f; tick += 1)
                {
                    ids.Clear();
                    health.Clear();
                    for (int index = 0; index < damage.Enemies.Count; index += 1)
                    {
                        ids.Add(damage.Enemies[index].Id);
                        health.Add(damage.Enemies[index].Health);
                    }

                    damage.Tick(Idle);
                    if ((damage.Events & SimEvents.EnemyHit) == SimEvents.None || !damage.CompanionAttacking)
                    {
                        continue;
                    }
                    for (int index = 0; index < damage.Enemies.Count; index += 1)
                    {
                        EnemyState enemy = damage.Enemies[index];
                        int before = ids.IndexOf(enemy.Id);
                        if (before >= 0 && health[before] > enemy.Health)
                        {
                            dealt = health[before] - enemy.Health;
                            break;
                        }
                    }
                }
                Assert.That(dealt,
                    Is.EqualTo(SimConfig.PlayerDamage * HackSpec.CompanionDamageScale
                        * (1f + 0.10f * magnitude)).Within(1e-2f),
                    $"guardian damage magnitude {magnitude}");
            }
        }

        [Test]
        public void EmberRestPreparation_NoneAndInvalidOffersAreExactDungeonNoOps()
        {
            HackConfig dungeon = Dungeon(companionId: "ember-cohort");
            AssertPreparationIsInert(dungeon, default, "default preparation");
            AssertPreparationIsInert(dungeon, Preparation(PreparationOfferKind.None, 1, 2), "none kind");
            AssertPreparationIsInert(dungeon, Preparation(PreparationOfferKind.Stat, 0, 1), "low variant");
            AssertPreparationIsInert(dungeon, Preparation(PreparationOfferKind.SkillRune, 4, 1), "high variant");
            AssertPreparationIsInert(dungeon, Preparation(PreparationOfferKind.GuardianResonance, 1, 0), "low magnitude");
            AssertPreparationIsInert(dungeon, Preparation(PreparationOfferKind.GuardianResonance, 1, 3), "high magnitude");
            AssertPreparationIsInert(dungeon, Preparation((PreparationOfferKind)99, 1, 1), "unknown kind");
        }

        [Test]
        public void EmberRestPreparation_IsInertInArenaAndPrologue()
        {
            PreparationOffer[] offers =
            {
                Preparation(PreparationOfferKind.Stat, 1, 2),
                Preparation(PreparationOfferKind.SkillRune, 2, 2),
                Preparation(PreparationOfferKind.GuardianResonance, 3, 2),
            };
            for (int index = 0; index < offers.Length; index += 1)
            {
                AssertPreparationIsInert(HackConfig.Arena(), offers[index], $"arena offer {index}");
                AssertPreparationIsInert(HackConfig.Prologue(), offers[index], $"prologue offer {index}");
            }
        }

        // --- §7 boss phases (AMENDMENT #4: three phases at 50% / 20%) --------------

        /// <summary>The boundary function is pure, so it is pinned exhaustively
        /// here rather than sampled through a sim run. Off-by-one at a
        /// threshold silently moves the whole difficulty curve.</summary>
        [TestCase(1.00f, 0)]
        [TestCase(0.51f, 0)]
        [TestCase(0.50f, 1)]   // inclusive: exactly half health IS phase 2
        [TestCase(0.21f, 1)]
        [TestCase(0.20f, 2)]   // inclusive: exactly a fifth IS phase 3
        [TestCase(0.00f, 2)]
        public void BossPhaseIndex_PinsBothBoundaries(float fraction, int expected)
        {
            Assert.That(HackSpec.BossPhaseIndexFor(fraction), Is.EqualTo(expected),
                $"health fraction {fraction} must resolve to phase index {expected}");
        }

        /// <summary>The amendment's core claim: the stored time vector shrinks
        /// every phase. If a future edit raises one of these, the phases stop
        /// meaning "stronger" and this fails loudly.</summary>
        [Test]
        public void BossPhaseTimeBudget_DecreasesMonotonically()
        {
            float previous = float.MaxValue;
            for (int phase = 0; phase < 3; phase += 1)
            {
                float budget = HackSpec.BossTelegraph[phase]
                    + HackSpec.BossAttackInterval[phase]
                    + HackSpec.BossSkillCooldown[phase];
                Assert.That(budget, Is.LessThan(previous),
                    $"phase {phase + 1} budget {budget} must be shorter than the phase before");
                previous = budget;
            }

            // Telegraph is deliberately NOT part of the shrink — it is the
            // player's reaction window and holding it is what keeps the
            // acceleration fair.
            Assert.That(HackSpec.BossTelegraph[2], Is.EqualTo(HackSpec.BossTelegraph[0]),
                "telegraph must stay constant across phases");
        }

        /// <summary>The whole point of the phases is that a player can SEE
        /// them. A phase reads only if it fits at least one full
        /// telegraph->attack cycle; below that the boss changes state and dies
        /// before anything is legible. Measured with the max-stat pilot, i.e.
        /// the fastest kill in the game — every weaker build gets longer
        /// phases, so this is the floor case.</summary>
        [Test]
        public void EveryBossPhase_LastsLongEnoughToRead()
        {
            var config = Dungeon(attack: 10, vitality: 10, swiftness: 10, weapon: 5, lantern: 5, cloak: 5);
            var sim = new CinderSim(in config);

            int bossTick = -1, p2Tick = -1, p3Tick = -1, endTick = -1, lastPhase = 0;
            for (int tick = 0; tick < 60 * 900; tick += 1)
            {
                sim.Tick(Pilot(sim, true));
                if ((sim.Events & SimEvents.BossSpawned) != 0) bossTick = tick;
                if (sim.BossPhase != lastPhase)
                {
                    if (sim.BossPhase == 2) p2Tick = tick;
                    if (sim.BossPhase == 3) p3Tick = tick;
                    lastPhase = sim.BossPhase;
                }
                if ((sim.Events & SimEvents.StageCleared) != 0) { endTick = tick; break; }
                if (sim.Mode == SimMode.GameOver) break;
            }

            Assert.That(endTick, Is.GreaterThan(0), "the max-stat pilot must clear the dungeon boss");
            Assert.That(p2Tick, Is.GreaterThan(0), "phase 2 must be reached");
            Assert.That(p3Tick, Is.GreaterThan(0), "phase 3 must be reached");

            float readFloor = HackSpec.BossTelegraph[0] + HackSpec.BossAttackInterval[0];
            float p1 = (p2Tick - bossTick) / 60f;
            float p2 = (p3Tick - p2Tick) / 60f;
            float p3 = (endTick - p3Tick) / 60f;

            Assert.That(p1, Is.GreaterThan(readFloor), $"phase 1 lasted {p1:F2}s, under the {readFloor:F2}s read floor");
            Assert.That(p2, Is.GreaterThan(readFloor), $"phase 2 lasted {p2:F2}s, under the {readFloor:F2}s read floor");
            Assert.That(p3, Is.GreaterThan(readFloor), $"phase 3 lasted {p3:F2}s, under the {readFloor:F2}s read floor");
        }

        [Test]
        public void BossPhases_FireOnceEachAtTheirThresholds()
        {
            var config = Dungeon(attack: 10, vitality: 10, swiftness: 10, weapon: 5, lantern: 5, cloak: 5);
            var sim = new CinderSim(in config);

            int triggers = 0;
            int phaseBefore = -1;
            float healthAtTrigger = -1f;
            float maxAtTrigger = -1f;
            int phase3Before = -1;
            float health3AtTrigger = -1f;
            float max3AtTrigger = -1f;
            bool bossSeen = false;
            bool cleared = false;
            for (int tick = 0; tick < 60 * 400; tick += 1)
            {
                int previousPhase = sim.BossPhase;
                sim.Tick(Pilot(sim, true));
                if ((sim.Events & SimEvents.BossSpawned) != 0)
                {
                    bossSeen = true;
                }
                if ((sim.Events & SimEvents.BossPhase2) != 0)
                {
                    // One event serves every boundary (the frozen SimEvents
                    // surface has no third flag), so the count is 2 now.
                    triggers += 1;
                    if (triggers == 1)
                    {
                        phaseBefore = previousPhase;
                        healthAtTrigger = sim.BossHp;
                        maxAtTrigger = sim.BossMaxHp;
                    }
                    else
                    {
                        phase3Before = previousPhase;
                        health3AtTrigger = sim.BossHp;
                        max3AtTrigger = sim.BossMaxHp;
                    }
                }
                if ((sim.Events & SimEvents.StageCleared) != 0)
                {
                    cleared = true;
                    break;
                }
                if (sim.Mode == SimMode.GameOver)
                {
                    break;
                }
            }

            Assert.IsTrue(bossSeen, "the run must reach the stage boss");
            Assert.IsTrue(cleared, "the max-stat pilot must clear cinder-span");
            Assert.That(triggers, Is.EqualTo(2), "one transition per boundary, no repeats");
            Assert.That(phaseBefore, Is.EqualTo(1), "the boss was in phase 1 the tick before");
            Assert.That(healthAtTrigger,
                Is.LessThanOrEqualTo(maxAtTrigger * HackSpec.BossPhase2HealthFraction));
            Assert.That(maxAtTrigger, Is.GreaterThan(0f));
            Assert.That(phase3Before, Is.EqualTo(2), "the boss was in phase 2 the tick before");
            Assert.That(health3AtTrigger,
                Is.LessThanOrEqualTo(max3AtTrigger * HackSpec.BossPhase3HealthFraction));
            Assert.That(sim.BossPhase, Is.EqualTo(3), "the stage ends on the phase the boss died in");
        }

        [Test]
        public void BossPhase2_MonarchSummonsThreeEscorts()
        {
            var config = Dungeon(CampaignStages.EchoThrone,
                attack: 10, vitality: 10, swiftness: 10, weapon: 5, lantern: 5, cloak: 5);
            var sim = new CinderSim(in config);

            int pendingBefore = -1;
            int pendingAfter = -1;
            for (int tick = 0; tick < 60 * 400; tick += 1)
            {
                int previousPending = sim.PendingSpawns;
                sim.Tick(Pilot(sim, true));
                if ((sim.Events & SimEvents.BossPhase2) != 0)
                {
                    pendingBefore = previousPending;
                    pendingAfter = sim.PendingSpawns;
                    break;
                }
                if (sim.Mode == SimMode.GameOver)
                {
                    break;
                }
            }

            Assert.That(pendingBefore, Is.GreaterThanOrEqualTo(0), "echo-throne must reach phase 2");
            Assert.That(pendingAfter - pendingBefore, Is.EqualTo(HackSpec.MonarchPhase2Escorts),
                "the monarch adds three escorts to the live spawn queue");

            // The commander does not.
            var commander = Dungeon(CampaignStages.CinderSpan,
                attack: 10, vitality: 10, swiftness: 10, weapon: 5, lantern: 5, cloak: 5);
            var other = new CinderSim(in commander);
            int commanderBefore = -1;
            int commanderAfter = -1;
            for (int tick = 0; tick < 60 * 400; tick += 1)
            {
                int previousPending = other.PendingSpawns;
                other.Tick(Pilot(other, true));
                if ((other.Events & SimEvents.BossPhase2) != 0)
                {
                    commanderBefore = previousPending;
                    commanderAfter = other.PendingSpawns;
                    break;
                }
                if (other.Mode == SimMode.GameOver)
                {
                    break;
                }
            }
            Assert.That(commanderBefore, Is.GreaterThanOrEqualTo(0), "cinder-span must reach phase 2");
            Assert.That(commanderAfter, Is.EqualTo(commanderBefore), "the commander summons nobody");
        }

        // --- §13 determinism ------------------------------------------------------

        [Test]
        public void Companion_DefaultFollowWithoutCommandsPreservesTheDungeonDigest()
        {
            var config = Dungeon(attack: 2, vitality: 1, swiftness: 3, weapon: 1, lantern: 2, cloak: 2,
                companionId: "ember-cohort");
            var firstSim = new CinderSim(in config);
            var secondSim = new CinderSim(in config);

            RunDigest first = RunHackScript(firstSim);
            RunDigest second = RunHackScript(secondSim);
            AssertSameDigest(first, second, "a dungeon run with no hold/recall commands must be reproducible");
            Assert.That(first.Kills, Is.GreaterThan(0), "the scripted run must not be empty");
            IHackSnapshot firstSnapshot = firstSim;
            IHackSnapshot secondSnapshot = secondSim;
            Assert.That(firstSnapshot.CompanionBehavior, Is.EqualTo(CompanionBehavior.Follow),
                "the public snapshot defaults omitted control fields to Follow");
            Assert.That(secondSnapshot.CompanionBehavior, Is.EqualTo(CompanionBehavior.Follow));
            Assert.That(firstSim.CompanionX, Is.EqualTo(secondSim.CompanionX).Within(Tolerance));
            Assert.That(firstSim.CompanionY, Is.EqualTo(secondSim.CompanionY).Within(Tolerance));
        }

        private static RunDigest RunHackScript(CinderSim sim)
        {
            for (int tick = 0; tick < 1800; tick += 1)
            {
                var input = Script(tick);
                input.DashQueued = tick % 130 == 0;
                input.BoltQueued = tick % 210 == 0;
                input.PulseQueued = tick % 170 == 0;
                sim.Tick(in input);
            }
            return sim.Digest;
        }
    }
}
