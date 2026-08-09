// FROZEN CONTRACT AMENDMENT #8 gates — companion signature skills
// (docs/SIM_SPEC_HACKSLASH.md "Frozen Contract Amendment #8", proof map A8.7).
// A8 supersedes exactly one line of Amendment #3's non-goals ("No companion skills,
// equipment, persistence, or cooldowns" -> no companion EQUIPMENT or PERSISTENCE);
// everything else in #3 stays in force and is still gated by HackSimTests.
//
// Every bound below was MEASURED against this build before it was asserted, with a
// pure-C# harness over Assets/Scripts/Sim/** (the sim assembly carries no UnityEngine
// reference). Three measured facts shape the whole file:
//
//   * A cast uses the companion position AFTER this tick's movement and the enemy
//     positions from the END OF THE PREVIOUS TICK (UpdateEnemies runs after
//     UpdateCompanion). So the nearest-first check below compares the POST-tick
//     companion position against PRE-tick enemy positions — any other pairing
//     reports false violations.
//   * In a quiet run (no player attack/skill input) the only damage sources are the
//     companion swing W and its signature skill S, so every positive health delta is
//     min(before, k) for k in {W, S, S+W}. That is what pins the damage number.
//   * A knocked-back enemy that DIED to the same cast does not move. Only LIVING
//     hits can be asserted to travel.
using System;
using System.Collections.Generic;
using CinderCourt.Sim;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class CompanionSkillTests
    {
        private const float Tolerance = 1e-4f;
        private const float DamageTolerance = 1e-2f;
        private const int ScriptTicks = 1800;

        // The A8.2 table, restated as literals ON PURPOSE. A gate that reads its bound out
        // of HackSpec moves with the constant it is supposed to pin, so retuning the sim
        // would silently retune its own gate (CLAUDE.md §2 — the numbers are the gate).
        // CompanionSkill_TableMatchesTheFrozenContract ties these back to HackSpec so the
        // two can never drift apart unnoticed.
        private readonly struct Row
        {
            public readonly string CompanionId;
            public readonly EnemyVisual Archetype;
            public readonly CompanionSkillId Skill;
            public readonly float Cooldown;
            public readonly float Radius;
            public readonly float DamageScale;
            public readonly int MaxTargets;
            public readonly int MinAutoTargets;
            public readonly float Knockback;

            public Row(
                string companionId, EnemyVisual archetype, CompanionSkillId skill,
                float cooldown, float radius, float damageScale,
                int maxTargets, int minAutoTargets, float knockback)
            {
                CompanionId = companionId;
                Archetype = archetype;
                Skill = skill;
                Cooldown = cooldown;
                Radius = radius;
                DamageScale = damageScale;
                MaxTargets = maxTargets;
                MinAutoTargets = minAutoTargets;
                Knockback = knockback;
            }
        }

        private static readonly Row[] Table =
        {
            new Row("scout-echo", EnemyVisual.Scout, CompanionSkillId.Volley, 6f, 240f, 0.55f, 3, 2, 0f),
            new Row("shade-echo", EnemyVisual.Shade, CompanionSkillId.Hex, 8f, 260f, 0.40f, 8, 2, 0f),
            new Row("possessed-echo", EnemyVisual.Possessed, CompanionSkillId.Quake, 9f, 170f, 0.70f, 6, 2, 90f),
            new Row("ember-cohort", EnemyVisual.EmberCohort, CompanionSkillId.Flare, 7f, 200f, 1.10f, 1, 1, 0f),
        };

        // --- helpers ---------------------------------------------------------

        /// <summary>Movement only. A quiet script is what makes the damage ledger readable:
        /// with no player attack or skill input the companion is the ONLY damage source.</summary>
        private static SimInput Quiet(int tick)
        {
            var input = default(SimInput);
            input.MoveX = tick / 120 % 2 == 0 ? 1f : -1f;
            input.MoveY = tick / 200 % 2 == 0 ? 0.5f : -0.5f;
            return input;
        }

        /// <summary>The full-kit script the Amendment #7 digests were frozen against.</summary>
        private static SimInput HackScriptInput(int tick)
        {
            var input = Quiet(tick);
            input.AttackQueued = tick % 30 == 0;
            input.NovaQueued = tick % 400 == 0;
            input.WardQueued = tick % 550 == 0;
            input.DashQueued = tick % 130 == 0;
            input.BoltQueued = tick % 210 == 0;
            input.PulseQueued = tick % 170 == 0;
            return input;
        }

        private static HackConfig DungeonScalar(string companionId)
        {
            Assert.IsTrue(
                HackConfig.TryDungeon(
                    CampaignStages.CinderSpan,
                    MetaStats.Of(0, 0, 0),
                    EquipTiers.Of(0, 0, 0),
                    companionId,
                    0,
                    out var config),
                "cinder-span must resolve");
            return config;
        }

        private static HackConfig DungeonSlots(string[] companionIds)
        {
            Assert.IsTrue(
                HackConfig.TryDungeon(
                    CampaignStages.CinderSpan,
                    MetaStats.Of(0, 0, 0),
                    EquipTiers.Of(0, 0, 0),
                    companionIds,
                    0,
                    out var config),
                "cinder-span must resolve");
            return config;
        }

        /// <summary>The §2.3 iso metric <c>hypot(dx, dy*1.42)</c> the sim measures with.</summary>
        private static float Iso(float fromX, float fromY, float toX, float toY)
        {
            float deltaX = toX - fromX;
            float deltaY = (toY - fromY) * SimConfig.IsoY;
            return MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        /// <summary>Player damage at the CURRENT level: the §5 curve with no growth points
        /// and no extraction bonus, which is what a quiet run produces.</summary>
        private static float PlayerDamageAt(in HackConfig config, int level)
        {
            return config.PlayerDamage * (1f + HackSpec.LevelDamageBonus * (level - 1));
        }

        private static float SwingScale(EnemyVisual archetype)
        {
            HackSpec.CompanionStats(archetype, out _, out _, out float damageScale);
            return damageScale;
        }

        private sealed class EnemySample
        {
            public int Id;
            public float X, Y, Health;
        }

        private static List<EnemySample> SampleLiving(CinderSim sim)
        {
            var living = new List<EnemySample>();
            var enemies = sim.Enemies;
            for (int index = 0; index < enemies.Count; index += 1)
            {
                EnemyState enemy = enemies[index];
                if (enemy.Dead)
                {
                    continue;
                }
                living.Add(new EnemySample { Id = enemy.Id, X = enemy.X, Y = enemy.Y, Health = enemy.Health });
            }
            return living;
        }

        private static bool TryFind(IReadOnlyList<EnemyState> enemies, int id, out EnemyState found)
        {
            for (int index = 0; index < enemies.Count; index += 1)
            {
                if (enemies[index].Id == id)
                {
                    found = enemies[index];
                    return true;
                }
            }
            found = default;
            return false;
        }

        // --- 1 & 2. the table itself -----------------------------------------

        [Test]
        public void CompanionSkill_TableIsPairwiseDistinctOnEveryAxis()
        {
            // This IS the machine-checkable form of "each companion has its OWN skill".
            // Distinctness on a single axis would be satisfiable by four re-skins of one
            // skill; requiring all four axes forces four genuinely different shapes.
            for (int a = 0; a < Table.Length; a += 1)
            {
                for (int b = a + 1; b < Table.Length; b += 1)
                {
                    string pair = $"{Table[a].Skill} vs {Table[b].Skill}";
                    Assert.That(Table[a].Skill, Is.Not.EqualTo(Table[b].Skill), $"{pair}: skill id");
                    Assert.That(Table[a].Cooldown, Is.Not.EqualTo(Table[b].Cooldown), $"{pair}: cooldown");
                    Assert.That(Table[a].Radius, Is.Not.EqualTo(Table[b].Radius), $"{pair}: radius");
                    Assert.That(Table[a].DamageScale, Is.Not.EqualTo(Table[b].DamageScale), $"{pair}: damage scale");
                    Assert.That(Table[a].MaxTargets, Is.Not.EqualTo(Table[b].MaxTargets), $"{pair}: max targets");
                }
            }
        }

        [Test]
        public void CompanionSkill_TableMatchesTheFrozenContract()
        {
            foreach (Row row in Table)
            {
                CompanionSkillSpec spec = HackSpec.CompanionSkill(row.Archetype);
                Assert.That(spec.Id, Is.EqualTo(row.Skill), $"{row.Archetype}: skill id");
                Assert.That(spec.Cooldown, Is.EqualTo(row.Cooldown).Within(Tolerance), $"{row.Archetype}: cooldown");
                Assert.That(spec.Radius, Is.EqualTo(row.Radius).Within(Tolerance), $"{row.Archetype}: radius");
                Assert.That(spec.DamageScale, Is.EqualTo(row.DamageScale).Within(Tolerance), $"{row.Archetype}: damage scale");
                Assert.That(spec.MaxTargets, Is.EqualTo(row.MaxTargets), $"{row.Archetype}: max targets");
                Assert.That(spec.MinAutoTargets, Is.EqualTo(row.MinAutoTargets), $"{row.Archetype}: min auto targets");
                Assert.That(spec.Knockback, Is.EqualTo(row.Knockback).Within(Tolerance), $"{row.Archetype}: knockback");
                Assert.That(spec.MinAutoTargets, Is.LessThanOrEqualTo(spec.MaxTargets),
                    $"{row.Archetype}: an auto threshold above the cap could never be met");
                Assert.That(spec.MaxTargets, Is.LessThanOrEqualTo(HackSpec.CompanionSkillTargetCap),
                    $"{row.Archetype}: MaxTargets must fit the sim's fixed selection buffer");
            }
        }

        [Test]
        public void CompanionSkill_EachArchetypeResolvesItsOwnSkill()
        {
            foreach (Row row in Table)
            {
                Assert.That(
                    HackSpec.CompanionSkill(HackSpec.CompanionArchetype(row.CompanionId)).Id,
                    Is.EqualTo(row.Skill),
                    $"{row.CompanionId} must resolve its own skill");

                // A8.2: skill and D6.3 stats are keyed by the SAME archetype, so a slot can
                // never carry one companion's skill with another's combat tuple.
                var config = DungeonScalar(row.CompanionId);
                var sim = new CinderSim(in config);
                Assert.That(sim.CompanionSkillIdAt(0), Is.EqualTo(row.Skill),
                    $"{row.CompanionId} slot 0 skill");
            }

            // Unknown ids fall back to the ember-cohort archetype, exactly like D6.3 stats.
            Assert.That(HackSpec.CompanionSkill(HackSpec.CompanionArchetype("no-such-echo")).Id,
                Is.EqualTo(CompanionSkillId.Flare), "unknown id must fall back to the ember-cohort skill");
        }

        // --- 3 & 4. A8.1 inertness -------------------------------------------

        [Test]
        public void CompanionSkill_RunsWithoutCompanionsPreserveTheirFrozenDigests()
        {
            // Same literals Amendment #7 froze. A8 adds a damage source, so if it ever
            // leaked outside a companion run these three numbers would be the first to move.
            AssertFrozenDigest(HackConfig.Arena(), 6600, 4, 21, 4, 90f, string.Empty, "arena");
            AssertFrozenDigest(HackConfig.Prologue(), 5500, 3, 18, 6, 73f, "prologue-clear", "prologue");
            // W1 intentionally moves the dungeon row by aiming Launcher knockback;
            // arena and prologue remain the frozen cross-mode controls above.
            AssertFrozenDigest(DungeonScalar(null), 3600, 3, 13, 4, 112f, string.Empty, "companion-less dungeon");
        }

        private static void AssertFrozenDigest(
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
                var input = HackScriptInput(tick);
                // Even under constant cast pressure a run with no companion has nothing to
                // cast with, so the command must be provably inert here.
                input.CompanionSkillQueued = true;
                sim.Tick(in input);
            }

            RunDigest digest = sim.Digest;
            Assert.That(digest.Score, Is.EqualTo(score), $"A8 must not move the {label} score");
            Assert.That(digest.Wave, Is.EqualTo(wave), $"A8 must not move the {label} wave");
            Assert.That(digest.Kills, Is.EqualTo(kills), $"A8 must not move the {label} kills");
            Assert.That(digest.Relics, Is.EqualTo(relics), $"A8 must not move the {label} relics");
            Assert.That(digest.HealthRemaining, Is.EqualTo(healthRemaining).Within(Tolerance),
                $"A8 must not move the {label} health");
            Assert.That(digest.Reason, Is.EqualTo(reason), $"A8 must not move the {label} end reason");
            Assert.That(sim.CompanionCount, Is.EqualTo(0), $"{label} must carry no companion");
        }

        [Test]
        public void CompanionSkill_RunsWithoutCompanionsReportNoSkill()
        {
            var bare = new CinderSim(DungeonScalar(null));
            var arena = new CinderSim();
            foreach (var sim in new[] { bare, arena })
            {
                Assert.That(sim.CompanionSkillIdAt(0), Is.EqualTo(CompanionSkillId.None));
                Assert.That(sim.CompanionSkillCooldownAt(0), Is.EqualTo(0f).Within(Tolerance));
                Assert.IsFalse(sim.CompanionSkillCastingAt(0));
                // Out-of-range slots must clamp exactly like every other D6.5 accessor.
                Assert.That(sim.CompanionSkillIdAt(7), Is.EqualTo(CompanionSkillId.None));
                Assert.That(sim.CompanionSkillIdAt(-3), Is.EqualTo(CompanionSkillId.None));
            }
        }

        // --- 5. A8.3 cooldown starts full ------------------------------------

        [Test]
        public void CompanionSkill_CooldownStartsFullAndNoSlotCastsBeforeIt()
        {
            foreach (Row row in Table)
            {
                var config = DungeonScalar(row.CompanionId);
                var sim = new CinderSim(in config);
                Assert.That(sim.CompanionSkillCooldownAt(0), Is.EqualTo(row.Cooldown).Within(Tolerance),
                    $"{row.CompanionId} must open a run with a FULL cooldown, not a free cast");

                // Maximum pressure: order a cast every single tick. The first legal cast is
                // therefore decided by the cooldown alone, and it is frame-exact —
                // ceil(cooldown / fixed step) ticks, observed on the tick it lands.
                int firstCast = -1;
                int expectedTick = (int)MathF.Ceiling(row.Cooldown / SimConfig.FixedStep) - 1;
                for (int tick = 0; tick < ScriptTicks; tick += 1)
                {
                    var input = Quiet(tick);
                    input.CompanionSkillQueued = true;
                    sim.Tick(in input);
                    if (sim.CompanionSkillCastingAt(0) && firstCast < 0)
                    {
                        firstCast = tick;
                    }
                }

                Assert.That(firstCast, Is.GreaterThanOrEqualTo(0),
                    $"{row.CompanionId} never cast in {ScriptTicks} ticks under constant command");
                Assert.That(firstCast, Is.EqualTo(expectedTick),
                    $"{row.CompanionId} must first cast exactly when its cooldown expires");
            }
        }

        // --- 6. A8.3 auto threshold ------------------------------------------

        [Test]
        public void CompanionSkill_AutoFiresOnlyWithEnoughTargetsInRadius()
        {
            foreach (Row row in Table)
            {
                var config = DungeonScalar(row.CompanionId);
                var sim = new CinderSim(in config);
                int casts = 0;
                bool previousFlash = false;

                for (int tick = 0; tick < ScriptTicks; tick += 1)
                {
                    List<EnemySample> before = SampleLiving(sim);
                    var input = Quiet(tick);   // NO command: this is the auto path only
                    sim.Tick(in input);

                    bool flash = sim.CompanionSkillCastingAt(0);
                    if (flash && !previousFlash)
                    {
                        casts += 1;
                        // The cast used the POST-tick companion position against these
                        // PRE-tick enemy positions. The companion can travel up to
                        // PlayerSpeed * PursuitSpeedScale * step (~3.8 px) in the tick, so
                        // count with a 4 px margin rather than pretending it stood still.
                        int inRadius = 0;
                        foreach (EnemySample sample in before)
                        {
                            if (Iso(sim.CompanionXAt(0), sim.CompanionYAt(0), sample.X, sample.Y)
                                <= row.Radius + 4f)
                            {
                                inRadius += 1;
                            }
                        }
                        Assert.That(inRadius, Is.GreaterThanOrEqualTo(row.MinAutoTargets),
                            $"{row.CompanionId} auto-fired at tick {tick} with only {inRadius} target(s) in radius");
                    }
                    previousFlash = flash;
                }

                Assert.That(casts, Is.GreaterThan(0),
                    $"{row.CompanionId} never auto-fired — the threshold gate would be vacuous");
            }
        }

        [Test]
        public void CompanionSkill_CommandBypassesTheAutoThreshold()
        {
            // The whole difference between the two trigger paths: a commanded cast needs one
            // living target, not MinAutoTargets. Proven by finding a commanded cast that the
            // auto path would have refused.
            var config = DungeonScalar("scout-echo");
            var sim = new CinderSim(in config);
            CompanionSkillSpec spec = HackSpec.CompanionSkill(EnemyVisual.Scout);
            Assert.That(spec.MinAutoTargets, Is.GreaterThan(1), "this proof needs a threshold above 1");

            bool sawSubThresholdCast = false;
            bool previousFlash = false;
            for (int tick = 0; tick < ScriptTicks; tick += 1)
            {
                List<EnemySample> before = SampleLiving(sim);
                var input = Quiet(tick);
                input.CompanionSkillQueued = true;
                sim.Tick(in input);

                bool flash = sim.CompanionSkillCastingAt(0);
                if (flash && !previousFlash)
                {
                    int inRadius = 0;
                    foreach (EnemySample sample in before)
                    {
                        if (Iso(sim.CompanionXAt(0), sim.CompanionYAt(0), sample.X, sample.Y) <= spec.Radius + 4f)
                        {
                            inRadius += 1;
                        }
                    }
                    Assert.That(inRadius, Is.GreaterThanOrEqualTo(1),
                        "even a commanded cast needs at least one living target in radius");
                    if (inRadius < spec.MinAutoTargets)
                    {
                        sawSubThresholdCast = true;
                    }
                }
                previousFlash = flash;
            }

            Assert.IsTrue(sawSubThresholdCast,
                "no commanded cast fell below the auto threshold — the bypass is unproven");
        }

        // --- 7. A8.3 commanded cast, no buffering ----------------------------

        [Test]
        public void CompanionSkill_CommandCastsEveryReadySlotAndIsNeverBuffered()
        {
            var config = DungeonSlots(new[] { "scout-echo", "shade-echo", "possessed-echo" });
            var sim = new CinderSim(in config);
            Assert.That(sim.CompanionCount, Is.EqualTo(3));

            int[] casts = new int[3];
            bool[] previousFlash = new bool[3];
            float[] previousCooldown = new float[3];
            for (int tick = 0; tick < ScriptTicks; tick += 1)
            {
                for (int slot = 0; slot < 3; slot += 1)
                {
                    previousCooldown[slot] = sim.CompanionSkillCooldownAt(slot);
                }
                var input = Quiet(tick);
                input.CompanionSkillQueued = true;
                sim.Tick(in input);

                for (int slot = 0; slot < 3; slot += 1)
                {
                    bool flash = sim.CompanionSkillCastingAt(slot);
                    if (flash && !previousFlash[slot])
                    {
                        casts[slot] += 1;
                        Assert.That(previousCooldown[slot], Is.LessThanOrEqualTo(SimConfig.FixedStep + Tolerance),
                            $"slot {slot} cast at tick {tick} while still on cooldown");
                    }
                    previousFlash[slot] = flash;
                }
            }

            for (int slot = 0; slot < 3; slot += 1)
            {
                Assert.That(casts[slot], Is.GreaterThan(0), $"slot {slot} never answered the command");
            }
        }

        [Test]
        public void CompanionSkill_ACommandOnCooldownIsNeverBuffered()
        {
            // Commands land at ticks 10/60/120 — all inside the 6 s scout cooldown, so every
            // one of them must be discarded outright. If any were buffered the two runs would
            // diverge the moment the cooldown expired.
            var config = DungeonScalar("scout-echo");
            var commanded = new CinderSim(in config);
            var quiet = new CinderSim(in config);

            for (int tick = 0; tick < ScriptTicks; tick += 1)
            {
                var withCommand = Quiet(tick);
                withCommand.CompanionSkillQueued = tick == 10 || tick == 60 || tick == 120;
                commanded.Tick(in withCommand);

                var without = Quiet(tick);
                quiet.Tick(in without);

                Assert.That(commanded.CompanionSkillCooldownAt(0),
                    Is.EqualTo(quiet.CompanionSkillCooldownAt(0)).Within(Tolerance),
                    $"a discarded command moved the cooldown at tick {tick}");
            }

            RunDigest a = commanded.Digest;
            RunDigest b = quiet.Digest;
            Assert.That(a.Score, Is.EqualTo(b.Score));
            Assert.That(a.Kills, Is.EqualTo(b.Kills));
            Assert.That(a.HealthRemaining, Is.EqualTo(b.HealthRemaining).Within(Tolerance));
        }

        // --- 8. A8.2 nearest-first, capped -----------------------------------

        [Test]
        public void CompanionSkill_StrikesTheNearestTargetsUpToTheArchetypeCap()
        {
            foreach (Row row in Table)
            {
                var config = DungeonScalar(row.CompanionId);
                var sim = new CinderSim(in config);
                float swingScale = SwingScale(row.Archetype);
                int casts = 0;
                int capExercised = 0;
                bool previousFlash = false;

                for (int tick = 0; tick < ScriptTicks; tick += 1)
                {
                    List<EnemySample> before = SampleLiving(sim);
                    var input = Quiet(tick);
                    input.CompanionSkillQueued = true;
                    sim.Tick(in input);

                    bool flash = sim.CompanionSkillCastingAt(0);
                    if (!flash || previousFlash)
                    {
                        previousFlash = flash;
                        continue;
                    }
                    previousFlash = flash;
                    casts += 1;

                    float damage = PlayerDamageAt(in config, sim.Level);
                    float skillDamage = damage * row.DamageScale;
                    float swingDamage = damage * swingScale;
                    float originX = sim.CompanionXAt(0);
                    float originY = sim.CompanionYAt(0);

                    var struck = new HashSet<int>();
                    IReadOnlyList<EnemyState> after = sim.Enemies;
                    foreach (EnemySample sample in before)
                    {
                        if (!TryFind(after, sample.Id, out EnemyState now))
                        {
                            continue;
                        }
                        float delta = sample.Health - now.Health;
                        if (delta <= 0f)
                        {
                            continue;
                        }
                        bool skillOnly = MathF.Abs(delta - MathF.Min(sample.Health, skillDamage)) < DamageTolerance;
                        bool skillAndSwing = MathF.Abs(delta - MathF.Min(sample.Health, skillDamage + swingDamage))
                            < DamageTolerance;
                        if (skillOnly || skillAndSwing)
                        {
                            struck.Add(sample.Id);
                        }
                    }

                    // The expected hit set is the N nearest inside the radius, measured from
                    // the POST-tick companion position against the PRE-tick enemy positions.
                    var inRadius = new List<EnemySample>();
                    foreach (EnemySample sample in before)
                    {
                        if (Iso(originX, originY, sample.X, sample.Y) <= row.Radius)
                        {
                            inRadius.Add(sample);
                        }
                    }
                    inRadius.Sort((left, right) =>
                        Iso(originX, originY, left.X, left.Y)
                            .CompareTo(Iso(originX, originY, right.X, right.Y)));

                    int expected = Math.Min(row.MaxTargets, inRadius.Count);
                    Assert.That(struck.Count, Is.EqualTo(expected),
                        $"{row.CompanionId} tick {tick}: struck {struck.Count}, expected the {expected} nearest "
                        + $"of {inRadius.Count} in radius (cap {row.MaxTargets})");
                    for (int index = 0; index < expected; index += 1)
                    {
                        Assert.IsTrue(struck.Contains(inRadius[index].Id),
                            $"{row.CompanionId} tick {tick}: skipped the #{index + 1} nearest target");
                    }
                    if (inRadius.Count > row.MaxTargets)
                    {
                        capExercised += 1;
                    }
                }

                Assert.That(casts, Is.GreaterThan(0), $"{row.CompanionId} never cast");
                if (row.MaxTargets == 1)
                {
                    // Flare's cap is the one the script reliably exceeds; without this the
                    // "up to the cap" claim would rest on runs that never reached it.
                    Assert.That(capExercised, Is.GreaterThan(0),
                        "no Flare cast had more targets in radius than its cap of 1");
                }
            }
        }

        // --- 9. A8.2 knockback ownership --------------------------------------

        [Test]
        public void CompanionSkill_OnlyQuakeShovesAndItShovesAwayFromTheCompanion()
        {
            foreach (Row row in Table)
            {
                var config = DungeonScalar(row.CompanionId);
                var sim = new CinderSim(in config);
                float swingScale = SwingScale(row.Archetype);
                int livingHits = 0;
                int shoved = 0;
                bool previousFlash = false;

                for (int tick = 0; tick < ScriptTicks; tick += 1)
                {
                    List<EnemySample> before = SampleLiving(sim);
                    var input = Quiet(tick);
                    input.CompanionSkillQueued = true;
                    sim.Tick(in input);

                    bool flash = sim.CompanionSkillCastingAt(0);
                    if (!flash || previousFlash)
                    {
                        previousFlash = flash;
                        continue;
                    }
                    previousFlash = flash;

                    float damage = PlayerDamageAt(in config, sim.Level);
                    float skillDamage = damage * row.DamageScale;
                    float swingDamage = damage * swingScale;
                    float originX = sim.CompanionXAt(0);
                    float originY = sim.CompanionYAt(0);
                    IReadOnlyList<EnemyState> after = sim.Enemies;

                    foreach (EnemySample sample in before)
                    {
                        if (!TryFind(after, sample.Id, out EnemyState now) || now.Dead)
                        {
                            continue;   // a corpse cannot be shoved — measured, not assumed
                        }
                        float delta = sample.Health - now.Health;
                        bool hit = delta > 0f
                            && (MathF.Abs(delta - MathF.Min(sample.Health, skillDamage)) < DamageTolerance
                                || MathF.Abs(delta - MathF.Min(sample.Health, skillDamage + swingDamage))
                                    < DamageTolerance);
                        if (!hit)
                        {
                            continue;
                        }

                        livingHits += 1;
                        float travelled = Iso(sample.X, sample.Y, now.X, now.Y);
                        if (row.Knockback > 0f)
                        {
                            // Measured: a shoved enemy clears ~6.8 px in the cast tick while an
                            // unshoved one drifts ~1.5 px on its own legs. 4 px separates them.
                            Assert.That(travelled, Is.GreaterThan(4f),
                                $"{row.CompanionId} tick {tick}: Quake hit an enemy without shoving it");
                            Assert.That(Iso(originX, originY, now.X, now.Y),
                                Is.GreaterThan(Iso(originX, originY, sample.X, sample.Y)),
                                $"{row.CompanionId} tick {tick}: the shove must push AWAY from the companion");
                            shoved += 1;
                        }
                        else
                        {
                            Assert.That(travelled, Is.LessThan(4f),
                                $"{row.CompanionId} tick {tick}: a knockback-free skill moved its target");
                        }
                    }
                }

                Assert.That(livingHits, Is.GreaterThan(0),
                    $"{row.CompanionId} never hit a surviving enemy — the shove gate would be vacuous");
                if (row.Knockback > 0f)
                {
                    Assert.That(shoved, Is.GreaterThan(0), "Quake never shoved anything");
                }
            }
        }

        // --- 10 & 11. hold and restart ---------------------------------------

        [Test]
        public void CompanionSkill_HeldSlotStillCasts()
        {
            // Amendment #3 suspends LOCOMOTION while held, never the slot's offensive
            // behavior — the swing was always allowed, so the skill is too.
            var config = DungeonScalar("scout-echo");
            var sim = new CinderSim(in config);
            int casts = 0;
            bool previousFlash = false;

            for (int tick = 0; tick < ScriptTicks; tick += 1)
            {
                var input = Quiet(tick);
                input.CompanionHoldQueued = tick == 5;
                input.CompanionSkillQueued = true;
                sim.Tick(in input);
                bool flash = sim.CompanionSkillCastingAt(0);
                if (flash && !previousFlash)
                {
                    casts += 1;
                }
                previousFlash = flash;
            }

            Assert.That(sim.CompanionBehaviorAt(0), Is.EqualTo(CompanionBehavior.Hold),
                "the slot must still be held at the end of the run");
            Assert.That(casts, Is.GreaterThan(0), "a held slot must still cast its signature skill");
        }

        [Test]
        public void CompanionSkill_RestartRefillsTheCooldown()
        {
            var config = DungeonScalar("possessed-echo");
            var sim = new CinderSim(in config);
            for (int tick = 0; tick < 700; tick += 1)
            {
                var input = Quiet(tick);
                input.CompanionSkillQueued = true;
                sim.Tick(in input);
            }

            Assert.That(sim.CompanionSkillCooldownAt(0), Is.LessThan(9f),
                "the run must have burned some cooldown before the restart is meaningful");

            sim.Restart();
            Assert.That(sim.CompanionSkillCooldownAt(0), Is.EqualTo(9f).Within(Tolerance),
                "restart must re-arm the cooldown to full, not hand out a free opening cast");
            Assert.IsFalse(sim.CompanionSkillCastingAt(0), "restart must clear the cast cue");
        }

        // --- 12. A8.5 event vs per-slot flash --------------------------------

        [Test]
        public void CompanionSkill_EventAndPerSlotFlashAgreeOnWhoCast()
        {
            var config = DungeonSlots(new[] { "scout-echo", "shade-echo", "possessed-echo" });
            var sim = new CinderSim(in config);
            bool[] previousFlash = new bool[3];
            int eventTicks = 0;

            for (int tick = 0; tick < ScriptTicks; tick += 1)
            {
                var input = Quiet(tick);
                input.CompanionSkillQueued = true;
                sim.Tick(in input);

                bool anyRisingFlash = false;
                for (int slot = 0; slot < 3; slot += 1)
                {
                    bool flash = sim.CompanionSkillCastingAt(slot);
                    if (flash && !previousFlash[slot])
                    {
                        anyRisingFlash = true;
                    }
                    previousFlash[slot] = flash;
                }

                bool raised = (sim.Events & SimEvents.CompanionSkillCast) != 0;
                Assert.That(raised, Is.EqualTo(anyRisingFlash),
                    $"tick {tick}: the run-wide event and the per-slot cues disagree about a cast");
                if (raised)
                {
                    eventTicks += 1;
                }
            }

            Assert.That(eventTicks, Is.GreaterThan(0), "no cast event was ever raised");
        }

        // --- 13. A8.6 damage model -------------------------------------------

        [Test]
        public void CompanionSkill_DamageIsNeutralAndScalesWithPlayerDamage()
        {
            // In a quiet run the companion is the only damage source, so EVERY positive
            // health delta must be min(before, k) for k in {swing, skill, skill+swing}.
            // Any elemental matchup (§2.4) or preparation scaling applied to the skill would
            // land off all three values and fail here — that is what makes "neutral" a gate.
            foreach (Row row in Table)
            {
                var config = DungeonScalar(row.CompanionId);
                var sim = new CinderSim(in config);
                float swingScale = SwingScale(row.Archetype);
                int skillDeltas = 0;

                for (int tick = 0; tick < ScriptTicks; tick += 1)
                {
                    List<EnemySample> before = SampleLiving(sim);
                    var input = Quiet(tick);
                    input.CompanionSkillQueued = true;
                    sim.Tick(in input);

                    float damage = PlayerDamageAt(in config, sim.Level);
                    float skillDamage = damage * row.DamageScale;
                    float swingDamage = damage * swingScale;
                    IReadOnlyList<EnemyState> after = sim.Enemies;

                    foreach (EnemySample sample in before)
                    {
                        if (!TryFind(after, sample.Id, out EnemyState now))
                        {
                            continue;
                        }
                        float delta = sample.Health - now.Health;
                        if (delta <= 0f)
                        {
                            continue;
                        }
                        bool isSwing = MathF.Abs(delta - MathF.Min(sample.Health, swingDamage)) < DamageTolerance;
                        bool isSkill = MathF.Abs(delta - MathF.Min(sample.Health, skillDamage)) < DamageTolerance;
                        bool isBoth = MathF.Abs(delta - MathF.Min(sample.Health, skillDamage + swingDamage))
                            < DamageTolerance;
                        Assert.IsTrue(isSwing || isSkill || isBoth,
                            $"{row.CompanionId} tick {tick}: enemy {sample.Id} lost {delta:0.000} hp, which is "
                            + $"neither the swing ({swingDamage:0.000}) nor the skill ({skillDamage:0.000}) "
                            + $"nor both, clamped at {sample.Health:0.000}");
                        if (isSkill || isBoth)
                        {
                            skillDeltas += 1;
                        }
                    }
                }

                Assert.That(skillDeltas, Is.GreaterThan(0),
                    $"{row.CompanionId} never landed a skill hit — the damage gate would be vacuous");
            }
        }

        // --- 14. §13 determinism ---------------------------------------------

        [Test]
        public void CompanionSkill_IdenticalInputsYieldIdenticalDigestAndCooldowns()
        {
            var config = DungeonSlots(new[] { "scout-echo", "shade-echo", "possessed-echo" });
            var first = new CinderSim(in config);
            var second = new CinderSim(in config);

            for (int tick = 0; tick < ScriptTicks; tick += 1)
            {
                var input = HackScriptInput(tick);
                input.CompanionSkillQueued = tick % 97 == 0;
                first.Tick(in input);
                second.Tick(in input);

                for (int slot = 0; slot < 3; slot += 1)
                {
                    Assert.That(first.CompanionSkillCooldownAt(slot),
                        Is.EqualTo(second.CompanionSkillCooldownAt(slot)).Within(Tolerance),
                        $"tick {tick} slot {slot}: cooldowns diverged");
                    Assert.That(first.CompanionSkillCastingAt(slot),
                        Is.EqualTo(second.CompanionSkillCastingAt(slot)),
                        $"tick {tick} slot {slot}: cast cues diverged");
                }
            }

            RunDigest a = first.Digest;
            RunDigest b = second.Digest;
            Assert.That(a.Score, Is.EqualTo(b.Score));
            Assert.That(a.Wave, Is.EqualTo(b.Wave));
            Assert.That(a.Kills, Is.EqualTo(b.Kills));
            Assert.That(a.Relics, Is.EqualTo(b.Relics));
            Assert.That(a.HealthRemaining, Is.EqualTo(b.HealthRemaining).Within(Tolerance));
            Assert.That(a.Reason, Is.EqualTo(b.Reason));
        }
    }
}
