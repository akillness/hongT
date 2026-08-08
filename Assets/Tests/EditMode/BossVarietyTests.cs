// AMENDMENT #16 (W6) — boss archetype variety.
// Numeric truth: docs/SIM_SPEC_HACKSLASH.md §20.
//
// The gate test comes first, same as #13/#14/#15: every number this amendment
// introduces is unreachable unless the caller passes a DungeonProgressionConfig
// with BossVariety set, so no golden digest can move. After that the file splits
// in two — pure table assertions (fast, and they pin the contract even if the
// pilot's route through a stage changes), then live-sim differentiation runs
// that prove the table actually reaches the fight.
//
// Pure Sim: no UnityEngine, no CinderCourt.View. Runs under Unity EditMode and
// under the standalone dotnet harness.
using System;
using System.Collections.Generic;
using CinderCourt.Sim;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class BossVarietyTests
    {
        private const float Tolerance = 1e-4f;

        /// <summary>Gate ON, nothing else. #13/#14/#15 stay off so any divergence
        /// this file measures is attributable to #16 alone.</summary>
        private static DungeonProgressionConfig VarietyOnly =>
            new DungeonProgressionConfig { BossVariety = true };

        private static HackConfig Dungeon(
            string stageId,
            int attack = 10,
            int vitality = 10,
            int swiftness = 10,
            int weapon = 5,
            int lantern = 5,
            int cloak = 5)
        {
            Assert.IsTrue(
                HackConfig.TryDungeon(
                    stageId,
                    MetaStats.Of(attack, vitality, swiftness),
                    EquipTiers.Of(weapon, lantern, cloak),
                    (string)null,
                    0,
                    out var config),
                $"unknown stage {stageId}");
            return config;
        }

        // The max-stat auto-pilot, lifted verbatim from HackSimTests.Pilot so the
        // two fixtures measure the same player. It is the fastest kill in the game,
        // which makes every phase length this file observes a floor case.
        private static SimInput Pilot(CinderSim sim, bool useSkills)
        {
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

            if (nearestDeltaX * sim.Player.Facing < SimConfig.FacingArcTolerance)
            {
                input.MoveX = nearestDeltaX > 0f ? 1f : -1f;
                return input;
            }

            input.AttackQueued = true;
            return input;
        }

        /// <summary>
        /// A second, deliberately worse pilot: it walks into the boss's face and
        /// pokes rarely. <see cref="Pilot"/> kites at 95 px, which is outside the
        /// 76 px swing range, so a run driven by it produces a clean phase trace
        /// and almost no boss swings — fine for thresholds, useless for cadence.
        /// This one is the cadence instrument: it does not survive the fight and is
        /// not meant to, it just stands where the boss can be observed swinging.
        /// </summary>
        private static SimInput Brawl(CinderSim sim, int tick)
        {
            const int PokeEvery = 47;
            const float MeleeGap = 55f;

            var input = default(SimInput);
            var enemies = sim.Enemies;
            EnemyState boss = default;
            bool found = false;
            for (int index = 0; index < enemies.Count; index += 1)
            {
                if (enemies[index].IsBoss && !enemies[index].Dead)
                {
                    boss = enemies[index];
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                return input;
            }

            float deltaX = boss.X - sim.Player.X;
            float deltaY = boss.Y - sim.Player.Y;
            float isoY = deltaY * SimConfig.IsoY;
            if (MathF.Sqrt(deltaX * deltaX + isoY * isoY) > MeleeGap)
            {
                float length = MathF.Max(1e-3f, MathF.Sqrt(deltaX * deltaX + deltaY * deltaY));
                input.MoveX = deltaX / length;
                input.MoveY = deltaY / length;
                return input;
            }
            if (deltaX * sim.Player.Facing < SimConfig.FacingArcTolerance)
            {
                input.MoveX = deltaX > 0f ? 1f : -1f;
                return input;
            }
            input.AttackQueued = tick % PokeEvery == 0;
            return input;
        }

        /// <summary>Everything one boss fight tells us, measured off the snapshot.</summary>
        private sealed class BossTrace
        {
            public bool BossSeen;
            public bool Cleared;
            public float MaxHp;
            public int MaxPhase;
            /// <summary>Health fraction at the tick the boss entered each phase,
            /// indexed by the 1-based phase number (so [2] and [3] are the ones
            /// that carry a boundary).</summary>
            public readonly float[] EntryFraction = { -1f, -1f, -1f, -1f };
            /// <summary>Telegraph the snapshot published while in each phase.</summary>
            public readonly float[] TelegraphAtPhase = { -1f, -1f, -1f, -1f };
            /// <summary>Ticks between consecutive boss swing starts.</summary>
            public readonly List<int> SwingGaps = new List<int>();
            /// <summary>Enemies queued to spawn by the phase boundaries.</summary>
            public int BoundarySummons;
        }

        private static BossTrace RunBossFight(CinderSim sim, bool brawl = false, int maxTicks = 60 * 900)
        {
            var trace = new BossTrace();
            int lastPhase = 0;
            int lastSwingTick = -1;
            bool bossWasSwinging = false;

            for (int tick = 0; tick < maxTicks; tick += 1)
            {
                int pendingBefore = sim.PendingSpawns;
                int phaseBefore = sim.BossPhase;
                sim.Tick(brawl && trace.BossSeen ? Brawl(sim, tick) : Pilot(sim, true));

                if ((sim.Events & SimEvents.BossSpawned) != 0)
                {
                    trace.BossSeen = true;
                }
                // The boss's stats are published by UpdateBossPhase, which runs
                // BEFORE the spawn queue drains, so BossMaxHp is still 0 on the
                // BossSpawned tick. Latch the first non-zero reading instead.
                if (trace.MaxHp == 0f && sim.BossMaxHp > 0f)
                {
                    trace.MaxHp = sim.BossMaxHp;
                }

                if (sim.BossPhase != lastPhase && sim.BossPhase > 0)
                {
                    if (sim.BossPhase < trace.EntryFraction.Length && sim.BossMaxHp > 0f)
                    {
                        trace.EntryFraction[sim.BossPhase] = sim.BossHp / sim.BossMaxHp;
                    }
                    lastPhase = sim.BossPhase;
                    trace.MaxPhase = Math.Max(trace.MaxPhase, sim.BossPhase);
                }

                if (sim.BossPhase > 0 && sim.BossPhase < trace.TelegraphAtPhase.Length)
                {
                    trace.TelegraphAtPhase[sim.BossPhase] = sim.BossTelegraphSeconds;
                }

                // A phase boundary is the only thing that can ADD to the pending
                // queue on a tick where the phase number moved, so the delta is
                // the summon count.
                if (phaseBefore > 0 && sim.BossPhase != phaseBefore && sim.PendingSpawns > pendingBefore)
                {
                    trace.BoundarySummons += sim.PendingSpawns - pendingBefore;
                }

                bool swinging = false;
                var enemies = sim.Enemies;
                for (int index = 0; index < enemies.Count; index += 1)
                {
                    if (enemies[index].IsBoss && !enemies[index].Dead)
                    {
                        swinging = enemies[index].Action == ActorAction.Attack;
                        break;
                    }
                }
                if (swinging && !bossWasSwinging)
                {
                    if (lastSwingTick >= 0)
                    {
                        trace.SwingGaps.Add(tick - lastSwingTick);
                    }
                    lastSwingTick = tick;
                }
                bossWasSwinging = swinging;

                if ((sim.Events & SimEvents.StageCleared) != 0)
                {
                    trace.Cleared = true;
                    break;
                }
                if (sim.Mode == SimMode.GameOver)
                {
                    break;
                }
            }

            return trace;
        }

        private static float MeanSwingGap(BossTrace trace)
        {
            Assert.That(trace.SwingGaps.Count, Is.GreaterThan(2),
                "the boss must swing enough times for a cadence to be measurable");
            long total = 0;
            for (int index = 0; index < trace.SwingGaps.Count; index += 1)
            {
                total += trace.SwingGaps[index];
            }
            return total / (float)trace.SwingGaps.Count;
        }

        private static SimInput Script(int tick)
        {
            var input = default(SimInput);
            input.MoveX = tick / 120 % 2 == 0 ? 1f : -1f;
            input.MoveY = tick / 200 % 2 == 0 ? 0.5f : -0.5f;
            input.AttackQueued = tick % 13 == 0;
            input.AttackHeld = tick % 13 == 0;
            input.NovaQueued = tick % 400 == 0;
            input.WardQueued = tick % 550 == 0;
            input.DashQueued = tick % 173 == 0;
            return input;
        }

        private static void AssertLockstep(CinderSim left, CinderSim right, int ticks, string what)
        {
            for (int tick = 0; tick < ticks; tick += 1)
            {
                SimInput input = Script(tick);
                left.Tick(in input);
                right.Tick(in input);

                Assert.That(right.Player.Health, Is.EqualTo(left.Player.Health).Within(Tolerance), $"{what}: tick {tick} HP");
                Assert.That(right.Player.X, Is.EqualTo(left.Player.X).Within(Tolerance), $"{what}: tick {tick} X");
                Assert.That(right.Player.Y, Is.EqualTo(left.Player.Y).Within(Tolerance), $"{what}: tick {tick} Y");
                Assert.That(right.Wave, Is.EqualTo(left.Wave), $"{what}: tick {tick} wave");
                Assert.That(right.Score, Is.EqualTo(left.Score), $"{what}: tick {tick} score");
                Assert.That(right.Kills, Is.EqualTo(left.Kills), $"{what}: tick {tick} kills");
                Assert.That(right.Enemies.Count, Is.EqualTo(left.Enemies.Count), $"{what}: tick {tick} enemy count");
                Assert.That(right.PendingSpawns, Is.EqualTo(left.PendingSpawns), $"{what}: tick {tick} pending");
                Assert.That(right.BossHp, Is.EqualTo(left.BossHp).Within(Tolerance), $"{what}: tick {tick} boss HP");
                Assert.That(right.BossMaxHp, Is.EqualTo(left.BossMaxHp).Within(Tolerance), $"{what}: tick {tick} boss max HP");
                Assert.That(right.BossPhase, Is.EqualTo(left.BossPhase), $"{what}: tick {tick} boss phase");
                Assert.That((int)right.Events, Is.EqualTo((int)left.Events), $"{what}: tick {tick} events");
            }
        }

        // --- 1. the gate ------------------------------------------------------

        /// <summary>
        /// The whole amendment hangs off one bool. A dungeon run built the old way,
        /// a run built with an explicit default config, and a run built with
        /// DungeonProgressionConfig.All (#13+#14, which #16 is deliberately NOT
        /// part of) must all step in lockstep with each other on the boss fight —
        /// including boss HP and boss phase, the two things #16 moves.
        /// </summary>
        [Test]
        public void GateOff_KeepsTheFrozenBossFight_InLockstep()
        {
            var config = Dungeon(CampaignStages.CinderSpan);

            var legacy = new CinderSim(in config);
            var explicitDefault = new CinderSim(in config, default);
            AssertLockstep(legacy, explicitDefault, 60 * 120, "legacy vs default");

            // A second pair, because All must not smuggle #16 in through the back
            // door. #13/#14 do change the run, so this pair is compared against a
            // second All run rather than against the frozen one — what is being
            // pinned is that All's boss is still the frozen boss.
            var allA = new CinderSim(in config, DungeonProgressionConfig.All);
            var allB = new CinderSim(in config, DungeonProgressionConfig.All);
            AssertLockstep(allA, allB, 60 * 60, "All vs All");
            Assert.IsFalse(DungeonProgressionConfig.All.BossVariety,
                "AMENDMENT #16 must stay out of DungeonProgressionConfig.All");
            Assert.IsTrue(DungeonProgressionConfig.Everything.BossVariety,
                "Everything must carry every amendment");
            Assert.IsFalse(allA.BossVarietyActive, "an All run must fight the frozen boss");
            Assert.That(allA.BossArchetype, Is.EqualTo(BossArchetype.None));
        }

        /// <summary>
        /// #16 is dungeon-only, the same scoping decision #13/#14 took: the arena
        /// and the prologue are frozen contracts and must not gain a branch.
        /// </summary>
        [Test]
        public void GateOn_OutsideTheDungeon_StaysOnTheFrozenBoss()
        {
            var arena = new CinderSim(HackConfig.Arena(), VarietyOnly);
            Assert.IsFalse(arena.BossVarietyActive, "the arena must never take an archetype");
            Assert.That(arena.BossArchetype, Is.EqualTo(BossArchetype.None));

            var dungeon = new CinderSim(Dungeon(CampaignStages.CinderSpan), VarietyOnly);
            Assert.IsTrue(dungeon.BossVarietyActive, "a gated dungeon run must take its archetype");
            Assert.That(dungeon.BossArchetype, Is.EqualTo(BossArchetype.Warden));
        }

        // --- 2. the table -----------------------------------------------------

        /// <summary>
        /// The None profile is not a placeholder — it is the frozen §7 contract
        /// restated, and it is what every ungated code path resolves to. If it
        /// ever drifts from HackSpec the gate stops being a no-op.
        /// </summary>
        [Test]
        public void NoneProfile_RestatesTheFrozenVectors()
        {
            BossArchetypeProfile none = BossVarietySpec.For(BossArchetype.None);

            Assert.That(none.PhaseCount, Is.EqualTo(3));
            Assert.That(none.Phase2Fraction, Is.EqualTo(HackSpec.BossPhase2HealthFraction).Within(Tolerance));
            Assert.That(none.Phase3Fraction, Is.EqualTo(HackSpec.BossPhase3HealthFraction).Within(Tolerance));
            Assert.That(none.HealthMul, Is.EqualTo(1f).Within(Tolerance));

            for (int phase = 0; phase < BossVarietySpec.MaxPhases; phase += 1)
            {
                Assert.That(none.SpeedMul[phase], Is.EqualTo(HackSpec.BossSpeedMul[phase]).Within(Tolerance),
                    $"P{phase + 1} speed");
                Assert.That(none.RangeMul[phase], Is.EqualTo(HackSpec.BossRangeMul[phase]).Within(Tolerance),
                    $"P{phase + 1} range");
                Assert.That(none.CadenceMul[phase], Is.EqualTo(1f).Within(Tolerance), $"P{phase + 1} cadence");
                Assert.That(none.ContactFrame[phase], Is.EqualTo(2), $"P{phase + 1} contact frame");
                Assert.That(none.PhaseEscorts[phase], Is.EqualTo(0), $"P{phase + 1} escorts");
            }

            // The frozen damage clause is "nothing before P2, then 1.25, then 1.45".
            Assert.That(none.DamageMul[0], Is.EqualTo(1f).Within(Tolerance));
            Assert.That(none.DamageMul[1], Is.EqualTo(HackSpec.BossPhase2DamageMul).Within(Tolerance));
            Assert.That(none.DamageMul[2], Is.EqualTo(HackSpec.BossPhase3DamageMul).Within(Tolerance));

            // And the phase resolver must agree with the frozen one everywhere.
            for (int step = 0; step <= 100; step += 1)
            {
                float fraction = step / 100f;
                Assert.That(
                    BossVarietySpec.PhaseIndexFor(BossArchetype.None, fraction),
                    Is.EqualTo(HackSpec.BossPhaseIndexFor(fraction)),
                    $"phase index at fraction {fraction:F2}");
            }
        }

        /// <summary>
        /// Mapping-table integrity. Every stage the sim can actually be handed has
        /// to land on a boss, exactly one archetype is the final boss, and anything
        /// off the table falls back to the frozen fight rather than to archetype 1.
        /// </summary>
        [Test]
        public void StageTable_CoversEveryStage_AndFallsBackToNone()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int monarchs = 0;
            for (int index = 0; index < BossVarietySpec.MappedStageCount; index += 1)
            {
                string id = BossVarietySpec.MappedStageIdAt(index);
                Assert.IsTrue(seen.Add(id), $"stage id {id} is mapped twice");
                BossArchetype archetype = BossVarietySpec.ArchetypeFor(id);
                Assert.That(archetype, Is.Not.EqualTo(BossArchetype.None), $"stage {id} maps to nothing");
                if (archetype == BossArchetype.Monarch)
                {
                    monarchs += 1;
                }
            }

            // Every SIM anchor — the ids CinderSim is actually constructed with.
            IReadOnlyList<string> anchors = CampaignStages.Ids;
            for (int index = 0; index < anchors.Count; index += 1)
            {
                Assert.That(
                    BossVarietySpec.ArchetypeFor(anchors[index]),
                    Is.Not.EqualTo(BossArchetype.None),
                    $"sim anchor {anchors[index]} must have an archetype");
            }

            // Exactly one final boss, and it is the last stage in campaign order.
            Assert.That(monarchs, Is.EqualTo(1), "there is exactly one final boss");
            Assert.That(
                BossVarietySpec.ArchetypeFor(anchors[anchors.Count - 1]),
                Is.EqualTo(BossArchetype.Monarch),
                "the last campaign stage carries the final boss");

            // All three stage archetypes are in play — a table that collapsed to
            // one archetype would pass every other assertion in this file.
            var used = new HashSet<BossArchetype>();
            for (int index = 0; index < anchors.Count; index += 1)
            {
                used.Add(BossVarietySpec.ArchetypeFor(anchors[index]));
            }
            Assert.IsTrue(used.Contains(BossArchetype.Warden), "no Warden stage");
            Assert.IsTrue(used.Contains(BossArchetype.Tactician), "no Tactician stage");
            Assert.IsTrue(used.Contains(BossArchetype.Sovereign), "no Sovereign stage");

            // Fallback.
            Assert.That(BossVarietySpec.ArchetypeFor(null), Is.EqualTo(BossArchetype.None));
            Assert.That(BossVarietySpec.ArchetypeFor(string.Empty), Is.EqualTo(BossArchetype.None));
            Assert.That(BossVarietySpec.ArchetypeFor("no-such-stage"), Is.EqualTo(BossArchetype.None));
            Assert.That(BossVarietySpec.ArchetypeFor("CINDER-SPAN"), Is.EqualTo(BossArchetype.None),
                "the lookup is ordinal, not case-insensitive");
            Assert.That(BossVarietySpec.For((BossArchetype)99).Archetype, Is.EqualTo(BossArchetype.None),
                "an out-of-range archetype degrades to the frozen profile");
        }

        /// <summary>
        /// A boss must never get EASIER by losing health. This is the invariant the
        /// frozen §7 vectors had and the one a hand-written table is most likely to
        /// break, so it is checked on every archetype across its live phases.
        /// </summary>
        [Test]
        public void EveryProfile_IsMonotoneHarder_AndStructurallyLegal()
        {
            IReadOnlyList<BossArchetypeProfile> profiles = BossVarietySpec.Profiles;
            Assert.That(profiles.Count, Is.EqualTo(5), "None + 4 archetypes");

            for (int index = 0; index < profiles.Count; index += 1)
            {
                BossArchetypeProfile profile = profiles[index];
                string who = profile.Archetype.ToString();

                Assert.That((int)profile.Archetype, Is.EqualTo(index),
                    "the profile table must be indexed by the enum value");
                Assert.That(profile.PhaseCount, Is.InRange(2, BossVarietySpec.MaxPhases), $"{who} phase count");
                Assert.That(profile.CadenceMul.Length, Is.EqualTo(BossVarietySpec.MaxPhases), $"{who} cadence length");
                Assert.That(profile.SpeedMul.Length, Is.EqualTo(BossVarietySpec.MaxPhases), $"{who} speed length");
                Assert.That(profile.RangeMul.Length, Is.EqualTo(BossVarietySpec.MaxPhases), $"{who} range length");
                Assert.That(profile.DamageMul.Length, Is.EqualTo(BossVarietySpec.MaxPhases), $"{who} damage length");
                Assert.That(profile.ContactFrame.Length, Is.EqualTo(BossVarietySpec.MaxPhases), $"{who} frame length");
                Assert.That(profile.PhaseEscorts.Length, Is.EqualTo(BossVarietySpec.MaxPhases), $"{who} escort length");

                Assert.That(profile.PhaseEscorts[0], Is.EqualTo(0), $"{who} must not summon on spawn");
                Assert.That(profile.HealthMul, Is.GreaterThan(0f), $"{who} health multiplier");
                Assert.That(profile.Phase2Fraction, Is.InRange(0.01f, 0.99f), $"{who} P2 threshold");
                if (profile.PhaseCount >= 3)
                {
                    Assert.That(profile.Phase3Fraction, Is.LessThan(profile.Phase2Fraction),
                        $"{who} must reach P3 after P2, not before");
                    Assert.That(profile.Phase3Fraction, Is.GreaterThan(0f), $"{who} P3 threshold");
                }

                for (int phase = 0; phase < profile.PhaseCount; phase += 1)
                {
                    Assert.That(profile.ContactFrame[phase],
                        Is.InRange(BossVarietySpec.MinContactFrame, BossVarietySpec.MaxContactFrame),
                        $"{who} P{phase + 1} contact frame is outside the attack clip");
                    Assert.That(profile.CadenceMul[phase], Is.GreaterThan(0f), $"{who} P{phase + 1} cadence");
                    Assert.That(profile.SpeedMul[phase], Is.GreaterThan(0f), $"{who} P{phase + 1} speed");
                    Assert.That(profile.RangeMul[phase], Is.GreaterThan(0f), $"{who} P{phase + 1} range");
                    Assert.That(profile.DamageMul[phase], Is.GreaterThan(0f), $"{who} P{phase + 1} damage");

                    if (phase == 0)
                    {
                        continue;
                    }
                    Assert.That(profile.CadenceMul[phase], Is.LessThanOrEqualTo(profile.CadenceMul[phase - 1]),
                        $"{who} slowed down entering P{phase + 1}");
                    Assert.That(profile.SpeedMul[phase], Is.GreaterThanOrEqualTo(profile.SpeedMul[phase - 1]),
                        $"{who} got slower entering P{phase + 1}");
                    Assert.That(profile.RangeMul[phase], Is.GreaterThanOrEqualTo(profile.RangeMul[phase - 1]),
                        $"{who} lost reach entering P{phase + 1}");
                    Assert.That(profile.DamageMul[phase], Is.GreaterThanOrEqualTo(profile.DamageMul[phase - 1]),
                        $"{who} hit softer entering P{phase + 1}");
                    Assert.That(profile.ContactFrame[phase], Is.LessThanOrEqualTo(profile.ContactFrame[phase - 1]),
                        $"{who} telegraphed longer entering P{phase + 1}");
                }

                // A two-phase archetype's dead P3 slot must be inert, not garbage.
                if (profile.PhaseCount == 2)
                {
                    Assert.That(profile.CadenceMul[2], Is.EqualTo(profile.CadenceMul[1]).Within(Tolerance), $"{who} dead P3");
                    Assert.That(profile.SpeedMul[2], Is.EqualTo(profile.SpeedMul[1]).Within(Tolerance), $"{who} dead P3");
                    Assert.That(BossVarietySpec.PhaseIndexFor(profile.Archetype, 0.001f), Is.EqualTo(1),
                        $"{who} must never resolve past P2");
                }
            }
        }

        /// <summary>
        /// The design requirement in one assertion: any two archetypes must differ
        /// on at least three of the five design axes. Two bosses that share four of
        /// five columns are the same boss with different art.
        /// </summary>
        [Test]
        public void EveryArchetypePair_DiffersOnAtLeastThreeAxes()
        {
            BossArchetype[] fighting =
            {
                BossArchetype.Warden, BossArchetype.Tactician,
                BossArchetype.Sovereign, BossArchetype.Monarch,
            };

            for (int a = 0; a < fighting.Length; a += 1)
            {
                for (int b = a + 1; b < fighting.Length; b += 1)
                {
                    BossArchetypeProfile left = BossVarietySpec.For(fighting[a]);
                    BossArchetypeProfile right = BossVarietySpec.For(fighting[b]);

                    int axes = 0;
                    if (MathF.Abs(left.Phase2Fraction - right.Phase2Fraction) > 0.01f
                        || left.PhaseCount != right.PhaseCount)
                    {
                        axes += 1;   // phase structure
                    }
                    if (MathF.Abs(left.CadenceMul[0] - right.CadenceMul[0]) > 0.01f)
                    {
                        axes += 1;   // attack cadence
                    }
                    if (MathF.Abs(left.SpeedMul[0] - right.SpeedMul[0]) > 0.01f)
                    {
                        axes += 1;   // movement
                    }
                    if (left.ContactFrame[0] != right.ContactFrame[0]
                        || left.ContactFrame[left.PhaseCount - 1] != right.ContactFrame[right.PhaseCount - 1])
                    {
                        axes += 1;   // telegraph rhythm
                    }
                    if (MathF.Abs(left.RangeMul[0] - right.RangeMul[0]) > 0.01f
                        || MathF.Abs(left.HealthMul - right.HealthMul) > 0.01f)
                    {
                        axes += 1;   // body: reach and bulk
                    }

                    Assert.That(axes, Is.GreaterThanOrEqualTo(3),
                        $"{fighting[a]} and {fighting[b]} differ on only {axes} axes");
                }
            }
        }

        // --- 3. live-sim differentiation --------------------------------------

        /// <summary>
        /// Warden, on cinder-span. Two phases instead of three, a later boundary,
        /// a slower swing and a fatter health bar — all measured against the same
        /// stage run through the frozen path with the same pilot.
        /// </summary>
        [Test]
        public void Warden_IsATwoPhaseFight_UnlikeTheFrozenBoss()
        {
            var config = Dungeon(CampaignStages.CinderSpan);
            BossTrace frozen = RunBossFight(new CinderSim(in config));
            BossTrace warden = RunBossFight(new CinderSim(in config, VarietyOnly));

            Assert.IsTrue(frozen.BossSeen && warden.BossSeen, "both runs must reach the boss");

            // Phase structure.
            Assert.That(frozen.MaxPhase, Is.EqualTo(3), "the frozen cinder-span boss has three phases");
            Assert.That(warden.MaxPhase, Is.EqualTo(2), "a Warden is a two-phase fight");

            // Threshold.
            BossArchetypeProfile profile = BossVarietySpec.For(BossArchetype.Warden);
            Assert.That(warden.EntryFraction[2], Is.LessThanOrEqualTo(profile.Phase2Fraction + 0.02f));
            Assert.That(warden.EntryFraction[2], Is.GreaterThan(HackSpec.BossPhase2HealthFraction + 0.01f),
                $"a Warden turns at {profile.Phase2Fraction:F2}, not the frozen "
                + $"{HackSpec.BossPhase2HealthFraction:F2} (measured {warden.EntryFraction[2]:F3})");

            // Bulk.
            Assert.That(warden.MaxHp, Is.EqualTo(frozen.MaxHp * profile.HealthMul).Within(0.5f));

            // Telegraph: longer windup, published for the View.
            Assert.That(warden.TelegraphAtPhase[1],
                Is.EqualTo(3f / BossVarietySpec.AttackClipFps).Within(Tolerance));
            Assert.That(frozen.TelegraphAtPhase[1],
                Is.EqualTo(2f / BossVarietySpec.AttackClipFps).Within(Tolerance));
        }

        /// <summary>
        /// Tactician, on abyss-chancel. The opposite corner of the table: swings
        /// far more often, carries less health, and calls escorts at BOTH
        /// boundaries where the frozen commander calls none at all.
        /// </summary>
        [Test]
        public void Tactician_SwingsFasterAndSummonsAtBothBoundaries()
        {
            var config = Dungeon(CampaignStages.AbyssChancel);
            BossTrace frozen = RunBossFight(new CinderSim(in config));
            BossTrace tactician = RunBossFight(new CinderSim(in config, VarietyOnly));

            Assert.IsTrue(frozen.BossSeen && tactician.BossSeen, "both runs must reach the boss");
            BossArchetypeProfile profile = BossVarietySpec.For(BossArchetype.Tactician);

            Assert.That(tactician.MaxHp, Is.EqualTo(frozen.MaxHp * profile.HealthMul).Within(0.5f));
            Assert.That(tactician.MaxHp, Is.LessThan(frozen.MaxHp), "a Tactician is the fragile one");
            Assert.That(tactician.MaxPhase, Is.EqualTo(3), "a Tactician is a three-phase fight");

            // abyss-chancel's frozen boss is a BossCommander, so the frozen monarch
            // escort clause never fires: every summon here is the archetype's.
            Assert.That(frozen.BoundarySummons, Is.EqualTo(0),
                "the frozen commander summons nothing at its boundaries");
            Assert.That(tactician.BoundarySummons,
                Is.EqualTo(profile.PhaseEscorts[1] + profile.PhaseEscorts[2]),
                "a Tactician calls escorts at both boundaries");

            // Telegraph: one frame, the shortest in the table.
            Assert.That(tactician.TelegraphAtPhase[1],
                Is.EqualTo(1f / BossVarietySpec.AttackClipFps).Within(Tolerance));
        }

        /// <summary>
        /// Sovereign, on echo-throne. The differentiating axis here is that the
        /// telegraph MOVES — 3 frames, then 2, then 1 — so the read the player
        /// learned in P1 is wrong twice. The frozen boss holds one windup forever.
        /// </summary>
        [Test]
        public void Sovereign_ShiftsItsTelegraphAtEveryBoundary()
        {
            var config = Dungeon(CampaignStages.EchoThrone);
            BossTrace frozen = RunBossFight(new CinderSim(in config));
            BossTrace sovereign = RunBossFight(new CinderSim(in config, VarietyOnly));

            Assert.IsTrue(frozen.BossSeen && sovereign.BossSeen, "both runs must reach the boss");
            Assert.That(sovereign.MaxPhase, Is.EqualTo(3), "a Sovereign is a three-phase fight");

            for (int phase = 1; phase <= 3; phase += 1)
            {
                Assert.That(sovereign.TelegraphAtPhase[phase],
                    Is.EqualTo((4 - phase) / BossVarietySpec.AttackClipFps).Within(Tolerance),
                    $"P{phase} telegraph");
                Assert.That(frozen.TelegraphAtPhase[phase],
                    Is.EqualTo(2f / BossVarietySpec.AttackClipFps).Within(Tolerance),
                    $"the frozen boss must hold one windup (P{phase})");
            }

            // Thresholds move too: even thirds instead of the frozen 50/20.
            BossArchetypeProfile profile = BossVarietySpec.For(BossArchetype.Sovereign);
            Assert.That(sovereign.EntryFraction[2], Is.LessThanOrEqualTo(profile.Phase2Fraction + 0.02f));
            Assert.That(sovereign.EntryFraction[2], Is.GreaterThan(frozen.EntryFraction[2] + 0.05f),
                "a Sovereign turns earlier than the frozen boss");
            Assert.That(sovereign.EntryFraction[3], Is.GreaterThan(frozen.EntryFraction[3] + 0.05f),
                "and reaches its last phase earlier too");
        }

        /// <summary>
        /// Monarch, on ash-march. The final boss keeps the frozen phase structure
        /// on purpose — the fight the whole campaign taught — and is reinforced on
        /// every other axis.
        /// </summary>
        [Test]
        public void Monarch_KeepsTheFrozenThresholds_ButIsReinforced()
        {
            var config = Dungeon(CampaignStages.AshMarch);
            BossTrace frozen = RunBossFight(new CinderSim(in config));
            BossTrace monarch = RunBossFight(new CinderSim(in config, VarietyOnly));

            Assert.IsTrue(frozen.BossSeen && monarch.BossSeen, "both runs must reach the boss");
            BossArchetypeProfile profile = BossVarietySpec.For(BossArchetype.Monarch);

            Assert.That(profile.Phase2Fraction, Is.EqualTo(HackSpec.BossPhase2HealthFraction).Within(Tolerance));
            Assert.That(profile.Phase3Fraction, Is.EqualTo(HackSpec.BossPhase3HealthFraction).Within(Tolerance));
            Assert.That(monarch.MaxPhase, Is.EqualTo(3));

            // Measured boundaries land on the frozen thresholds, not near them.
            Assert.That(monarch.EntryFraction[2],
                Is.EqualTo(frozen.EntryFraction[2]).Within(0.03f), "P2 boundary");
            Assert.That(monarch.EntryFraction[3],
                Is.EqualTo(frozen.EntryFraction[3]).Within(0.03f), "P3 boundary");

            Assert.That(monarch.MaxHp, Is.EqualTo(frozen.MaxHp * profile.HealthMul).Within(0.5f));
            Assert.That(monarch.MaxHp, Is.GreaterThan(frozen.MaxHp), "the final boss is reinforced");

            // Reinforced on every non-structural axis.
            BossArchetypeProfile none = BossVarietySpec.For(BossArchetype.None);
            for (int phase = 0; phase < BossVarietySpec.MaxPhases; phase += 1)
            {
                Assert.That(profile.SpeedMul[phase], Is.GreaterThanOrEqualTo(none.SpeedMul[phase]),
                    $"P{phase + 1} speed must not fall below the frozen boss");
                Assert.That(profile.DamageMul[phase], Is.GreaterThanOrEqualTo(none.DamageMul[phase]),
                    $"P{phase + 1} damage must not fall below the frozen boss");
                Assert.That(profile.CadenceMul[phase], Is.LessThanOrEqualTo(none.CadenceMul[phase]),
                    $"P{phase + 1} cadence must not be slower than the frozen boss");
            }

            // The P2 escort call is preserved at the frozen count, and it is the
            // only boundary the final boss summons at.
            Assert.That(profile.PhaseEscorts[1], Is.EqualTo(HackSpec.MonarchPhase2Escorts));
            Assert.That(profile.PhaseEscorts[2], Is.EqualTo(0));
            Assert.That(monarch.BoundarySummons, Is.EqualTo(frozen.BoundarySummons),
                "the final boss keeps the frozen summon schedule");
        }

        /// <summary>
        /// The cadence axis, measured live. <see cref="Pilot"/> kites outside swing
        /// range, so this one uses <see cref="Brawl"/>: it stands in the boss's
        /// face and counts the ticks between swing starts. Each archetype is
        /// compared against the SAME stage on the frozen path, so the ratio is
        /// attributable to the archetype and nothing else.
        /// </summary>
        [Test]
        public void CadenceAxis_SeparatesTheThreeStageArchetypes()
        {
            float warden = SwingRatio(CampaignStages.CinderSpan);
            float tactician = SwingRatio(CampaignStages.AbyssChancel);
            float sovereign = SwingRatio(CampaignStages.EchoThrone);

            Assert.That(warden, Is.GreaterThan(1.25f),
                $"a Warden must swing far slower than the frozen boss (ratio {warden:F3})");
            Assert.That(tactician, Is.LessThan(0.85f),
                $"a Tactician must swing far faster than the frozen boss (ratio {tactician:F3})");
            Assert.That(sovereign, Is.InRange(1.02f, 1.25f),
                $"a Sovereign opens slightly slower than the frozen boss (ratio {sovereign:F3})");

            // And all three are separated from each other, not just from frozen.
            Assert.That(warden, Is.GreaterThan(sovereign * 1.15f), "Warden vs Sovereign");
            Assert.That(sovereign, Is.GreaterThan(tactician * 1.15f), "Sovereign vs Tactician");
        }

        /// <summary>Mean boss swing gap on a gated run over the same on a frozen
        /// run of the same stage. Above 1 = the archetype swings less often.</summary>
        private static float SwingRatio(string stageId)
        {
            var config = Dungeon(stageId);
            BossTrace frozen = RunBossFight(new CinderSim(in config), brawl: true);
            BossTrace gated = RunBossFight(new CinderSim(in config, VarietyOnly), brawl: true);
            Assert.IsTrue(frozen.BossSeen && gated.BossSeen, $"{stageId}: both runs must reach the boss");
            return MeanSwingGap(gated) / MeanSwingGap(frozen);
        }

        // --- 4. determinism ---------------------------------------------------

        /// <summary>
        /// §13 is not amended: a gated run is still a pure function of (config,
        /// input sequence). Checked on all three stage archetypes, because the
        /// mapping is the only new input to the run.
        /// </summary>
        [Test]
        public void GatedRuns_AreReproducible_OnEveryArchetype()
        {
            string[] stages =
            {
                CampaignStages.CinderSpan,     // Warden
                CampaignStages.AbyssChancel,   // Tactician
                CampaignStages.EchoThrone,     // Sovereign
                CampaignStages.AshMarch,       // Monarch
            };

            for (int index = 0; index < stages.Length; index += 1)
            {
                var config = Dungeon(stages[index]);
                var left = new CinderSim(in config, VarietyOnly);
                var right = new CinderSim(in config, VarietyOnly);
                AssertLockstep(left, right, 60 * 90, stages[index]);
                Assert.That(right.BossArchetype, Is.EqualTo(left.BossArchetype));
                Assert.That(right.Digest.Reason, Is.EqualTo(left.Digest.Reason));
            }
        }

        /// <summary>
        /// The archetype is resolved once and never moves, which is what makes the
        /// run reproducible from its config alone. Restart must not reroll it.
        /// </summary>
        [Test]
        public void Archetype_IsFixedForTheRun_AndSurvivesRestart()
        {
            var sim = new CinderSim(Dungeon(CampaignStages.EmberBastion), VarietyOnly);
            Assert.That(sim.BossArchetype, Is.EqualTo(BossArchetype.Warden));
            Assert.That(sim.BossPhaseCount, Is.EqualTo(2));

            for (int tick = 0; tick < 60 * 30; tick += 1)
            {
                sim.Tick(Pilot(sim, true));
                Assert.That(sim.BossArchetype, Is.EqualTo(BossArchetype.Warden), $"tick {tick}");
            }

            sim.Restart();
            Assert.That(sim.BossArchetype, Is.EqualTo(BossArchetype.Warden), "after restart");
            Assert.That(sim.BossPhaseCount, Is.EqualTo(2), "after restart");
        }
    }
}
