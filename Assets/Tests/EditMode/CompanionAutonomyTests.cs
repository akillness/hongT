// FROZEN CONTRACT AMENDMENT #7 gates — companion autonomy
// (_workspace/current/design/companion-autonomy-amendment-proposal.md §"Required new
// deterministic tests"). Amendment #6 (per-slot companions) is the substrate; this file
// only pins what A7 adds: anchor-relative target acquisition, the target lock, leashed
// pursuit and the graceful return.
//
// Every bound below was MEASURED against this build before it was asserted (a pure-C#
// harness over Assets/Scripts/Sim/**, since the sim assembly carries no UnityEngine
// reference). Two invariants are subtle and are spelled out where they are used:
//   * the anchor a slot uses at tick T is built from the player's position AFTER
//     UpdatePlayer of tick T, while the enemy positions it sees are still the ones from
//     the end of tick T-1 (UpdateEnemies runs after UpdateCompanion);
//   * slots update in index order, so an earlier slot can kill a later slot's locked
//     target inside the same tick.
using System;
using System.Collections.Generic;
using CinderCourt.Sim;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class CompanionAutonomyTests
    {
        private const float Tolerance = 1e-4f;
        private const int ScriptTicks = 1800;

        // The Amendment #7 numbers, restated here as literals ON PURPOSE. An invariant that
        // reads its own bound out of HackSpec moves with the constant it is supposed to pin,
        // so retuning the sim would silently retune its own gate. CLAUDE.md §2: the numbers
        // are the gate. CompanionAutonomy_ConstantsMatchTheFrozenContract ties these back to
        // HackSpec so the two can never drift apart unnoticed.
        private const float AcquireRadius = 300f;
        private const float LeashRadius = 320f;
        private const float PursuitSpeedScale = 1.05f;
        private const float TargetLockSeconds = 2f;
        private const float ReturnGraceSeconds = 0.35f;

        private static readonly string[] Roster = { "scout-echo", "shade-echo", "possessed-echo" };

        // --- helpers ---------------------------------------------------------

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

        private static SimInput HackScriptInput(int tick)
        {
            var input = Script(tick);
            input.DashQueued = tick % 130 == 0;
            input.BoltQueued = tick % 210 == 0;
            input.PulseQueued = tick % 170 == 0;
            return input;
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

        /// <summary>The §2.3 iso metric <c>hypot(dx, dy*1.42)</c> the sim measures with.</summary>
        private static float Iso(float fromX, float fromY, float toX, float toY)
        {
            float deltaX = toX - fromX;
            float deltaY = (toY - fromY) * SimConfig.IsoY;
            return MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        private static float AnchorX(CinderSim sim)
        {
            return sim.Player.X - HackSpec.CompanionFollowOffset * sim.Player.Facing;
        }

        private static float AnchorY(CinderSim sim, int slot)
        {
            return sim.Player.Y + HackSpec.CompanionSlotFanout[slot];
        }

        private static float ArchetypeRange(string companionId)
        {
            HackSpec.CompanionStats(
                HackSpec.CompanionArchetype(companionId),
                out _,
                out float range,
                out _);
            return range;
        }

        private static int IndexOfId(IReadOnlyList<EnemyState> enemies, int id)
        {
            for (int index = 0; index < enemies.Count; index += 1)
            {
                if (enemies[index].Id == id)
                {
                    return index;
                }
            }

            return -1;
        }

        private static int LivingCount(IReadOnlyList<EnemyState> enemies)
        {
            int living = 0;
            for (int index = 0; index < enemies.Count; index += 1)
            {
                if (!enemies[index].Dead)
                {
                    living += 1;
                }
            }

            return living;
        }

        // --- 1. digest gate --------------------------------------------------

        /// <summary>
        /// A7 must be inert for every run that carries no companion. These six numbers are
        /// the pre-amendment digests, captured by replaying the same script against the
        /// committed (HEAD) sim sources; if an autonomy edit ever leaks into the arena,
        /// prologue or a companion-less dungeon, exactly this test fails.
        /// </summary>
        [Test]
        public void CompanionAutonomy_RunsWithoutCompanionsPreserveTheirFrozenDigests()
        {
            AssertFrozenDigest(HackConfig.Arena(), 6600, 4, 21, 4, 90f, string.Empty, "arena");
            AssertFrozenDigest(HackConfig.Prologue(), 5500, 3, 18, 6, 73f, "prologue-clear", "prologue");
            AssertFrozenDigest(DungeonScalar(null), 3350, 3, 13, 3, 89.5f, string.Empty, "companion-less dungeon");
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
                sim.Tick(in input);
            }

            RunDigest digest = sim.Digest;
            Assert.That(digest.Score, Is.EqualTo(score), $"A7 must not move the {label} score");
            Assert.That(digest.Wave, Is.EqualTo(wave), $"A7 must not move the {label} wave");
            Assert.That(digest.Kills, Is.EqualTo(kills), $"A7 must not move the {label} kills");
            Assert.That(digest.Relics, Is.EqualTo(relics), $"A7 must not move the {label} relics");
            Assert.That(digest.HealthRemaining, Is.EqualTo(healthRemaining).Within(Tolerance),
                $"A7 must not move the {label} health");
            Assert.That(digest.Reason, Is.EqualTo(reason), $"A7 must not move the {label} end reason");
            Assert.That(sim.CompanionCount, Is.EqualTo(0), $"{label} must carry no companion");
        }

        // --- 2 & 9. determinism ----------------------------------------------

        [Test]
        public void CompanionAutonomy_IdenticalInputsYieldIdenticalDigestAndPerSlotState()
        {
            var config = DungeonSlots(Roster);
            var first = new CinderSim(in config);
            var second = new CinderSim(in config);

            for (int tick = 0; tick < ScriptTicks; tick += 1)
            {
                var input = HackScriptInput(tick);
                first.Tick(in input);
                second.Tick(in input);

                for (int slot = 0; slot < Roster.Length; slot += 1)
                {
                    Assert.That(second.CompanionXAt(slot), Is.EqualTo(first.CompanionXAt(slot)).Within(Tolerance),
                        $"slot {slot} x diverged at tick {tick}");
                    Assert.That(second.CompanionYAt(slot), Is.EqualTo(first.CompanionYAt(slot)).Within(Tolerance),
                        $"slot {slot} y diverged at tick {tick}");
                    Assert.That(second.CompanionEngagedAt(slot), Is.EqualTo(first.CompanionEngagedAt(slot)),
                        $"slot {slot} engage flag diverged at tick {tick}");
                    Assert.That(second.CompanionTargetIdAt(slot), Is.EqualTo(first.CompanionTargetIdAt(slot)),
                        $"slot {slot} target lock diverged at tick {tick}");
                }
            }

            RunDigest expected = first.Digest;
            RunDigest actual = second.Digest;
            Assert.That(actual.Score, Is.EqualTo(expected.Score), "autonomy must stay reproducible");
            Assert.That(actual.Wave, Is.EqualTo(expected.Wave), "autonomy must stay reproducible");
            Assert.That(actual.Kills, Is.EqualTo(expected.Kills), "autonomy must stay reproducible");
            Assert.That(actual.Relics, Is.EqualTo(expected.Relics), "autonomy must stay reproducible");
            Assert.That(actual.HealthRemaining, Is.EqualTo(expected.HealthRemaining).Within(Tolerance),
                "autonomy must stay reproducible");
            Assert.That(actual.Reason, Is.EqualTo(expected.Reason), "autonomy must stay reproducible");
        }

        [Test]
        public void CompanionAutonomy_PerSlotEngageIsIndependent()
        {
            var config = DungeonSlots(Roster);
            var sim = new CinderSim(in config);
            int divergentTicks = 0;
            var engagedTicks = new int[Roster.Length];

            for (int tick = 0; tick < ScriptTicks; tick += 1)
            {
                var input = HackScriptInput(tick);
                sim.Tick(in input);

                bool any = false;
                bool all = true;
                for (int slot = 0; slot < Roster.Length; slot += 1)
                {
                    bool engaged = sim.CompanionEngagedAt(slot);
                    if (engaged)
                    {
                        any = true;
                        engagedTicks[slot] += 1;
                    }
                    else
                    {
                        all = false;
                    }
                }

                if (any && !all)
                {
                    divergentTicks += 1;
                }
            }

            for (int slot = 0; slot < Roster.Length; slot += 1)
            {
                Assert.That(engagedTicks[slot], Is.GreaterThan(0),
                    $"A7.2: slot {slot} ({Roster[slot]}) must actually pursue during the script");
            }

            Assert.That(divergentTicks, Is.GreaterThan(0),
                "A7: engagement is per slot — the slots must be able to disagree within a tick");
        }

        // --- 3. legacy follower preserved when nothing is in range -----------

        /// <summary>
        /// With no living enemy left there is nothing to acquire, so the slot must fall back
        /// to the frozen §4 follower: no lock, no engage, and a step no faster than
        /// <c>_playerSpeed</c> — the pursuit multiplier must not leak into the return walk.
        /// The grace dwell (0.35 s) is skipped before sampling.
        /// </summary>
        [Test]
        public void CompanionAutonomy_WithNoLivingEnemyTheSlotWalksTheLegacyFollowStep()
        {
            var config = DungeonSlots(Roster);
            var sim = new CinderSim(in config);
            ICinderSim snapshot = sim;

            var previousX = new float[Roster.Length];
            var previousY = new float[Roster.Length];
            for (int slot = 0; slot < Roster.Length; slot += 1)
            {
                previousX[slot] = sim.CompanionXAt(slot);
                previousY[slot] = sim.CompanionYAt(slot);
            }

            int graceTicks = (int)MathF.Ceiling(ReturnGraceSeconds / SimConfig.FixedStep);
            float followStep = config.PlayerSpeed * SimConfig.FixedStep + Tolerance;
            int emptyStreak = 0;
            int samples = 0;

            for (int tick = 0; tick < ScriptTicks; tick += 1)
            {
                var input = HackScriptInput(tick);
                sim.Tick(in input);
                emptyStreak = LivingCount(snapshot.Enemies) == 0 ? emptyStreak + 1 : 0;

                for (int slot = 0; slot < Roster.Length; slot += 1)
                {
                    float deltaX = sim.CompanionXAt(slot) - previousX[slot];
                    float deltaY = sim.CompanionYAt(slot) - previousY[slot];
                    float step = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
                    previousX[slot] = sim.CompanionXAt(slot);
                    previousY[slot] = sim.CompanionYAt(slot);

                    if (emptyStreak <= graceTicks)
                    {
                        continue;
                    }

                    samples += 1;
                    Assert.That(sim.CompanionEngagedAt(slot), Is.False,
                        $"slot {slot} cannot engage with no living enemy (tick {tick})");
                    Assert.That(sim.CompanionTargetIdAt(slot), Is.EqualTo(0),
                        $"slot {slot} cannot hold a lock with no living enemy (tick {tick})");
                    Assert.That(step, Is.LessThanOrEqualTo(followStep),
                        $"slot {slot} must walk home at the frozen follow speed (tick {tick})");
                }
            }

            Assert.That(samples, Is.GreaterThan(0),
                "the script must actually clear the field, otherwise this gate proves nothing");
        }

        // --- 4 & 10. engage conditions ---------------------------------------

        /// <summary>
        /// A7.2/A7.4. Engagement is decided from the state the sim saw when
        /// <c>UpdateCompanionSlot</c> ran: the slot's own position from the end of the last
        /// tick, the target's position from the end of the last tick, and the anchor built
        /// from THIS tick's player position. Under that reading the two bounds are exact —
        /// the target is strictly outside this slot's own archetype attack range, and the
        /// slot never strays past the leash.
        /// </summary>
        [Test]
        public void CompanionAutonomy_EngagesOnlyOutsideArchetypeRangeAndInsideLeash()
        {
            var config = DungeonSlots(Roster);
            var sim = new CinderSim(in config);
            ICinderSim snapshot = sim;

            var range = new float[Roster.Length];
            for (int slot = 0; slot < Roster.Length; slot += 1)
            {
                range[slot] = ArchetypeRange(Roster[slot]);
            }

            Assert.That(range[0], Is.Not.EqualTo(range[2]).Within(Tolerance),
                "the roster must mix archetype ranges, otherwise the per-archetype claim is untested");

            var previousX = new float[Roster.Length];
            var previousY = new float[Roster.Length];
            for (int slot = 0; slot < Roster.Length; slot += 1)
            {
                previousX[slot] = sim.CompanionXAt(slot);
                previousY[slot] = sim.CompanionYAt(slot);
            }

            var previousEnemyId = new List<int>();
            var previousEnemyX = new List<float>();
            var previousEnemyY = new List<float>();
            int engagedSamples = 0;

            for (int tick = 0; tick < ScriptTicks; tick += 1)
            {
                var input = HackScriptInput(tick);
                sim.Tick(in input);

                for (int slot = 0; slot < Roster.Length; slot += 1)
                {
                    if (sim.CompanionEngagedAt(slot))
                    {
                        // The lock may legitimately read 0 here: pursuit runs BEFORE the swing,
                        // so a slot can close the gap and finish its own target inside the same
                        // tick, and CinderSim.cs:1479 releases the lock immediately so the
                        // snapshot never publishes a lock on a corpse.
                        int targetId = sim.CompanionTargetIdAt(slot);
                        int previousIndex = targetId == 0 ? -1 : previousEnemyId.IndexOf(targetId);
                        if (previousIndex >= 0)
                        {
                            engagedSamples += 1;
                            float gap = Iso(
                                previousX[slot],
                                previousY[slot],
                                previousEnemyX[previousIndex],
                                previousEnemyY[previousIndex]);
                            Assert.That(gap, Is.GreaterThan(range[slot]),
                                $"slot {slot} may only close on a target outside its own {range[slot]} px reach (tick {tick})");
                        }

                        float anchorDistance = Iso(
                            sim.CompanionXAt(slot),
                            sim.CompanionYAt(slot),
                            AnchorX(sim),
                            AnchorY(sim, slot));
                        Assert.That(anchorDistance, Is.LessThanOrEqualTo(LeashRadius),
                            $"slot {slot} must stay inside the leash while engaged (tick {tick})");
                    }


                    previousX[slot] = sim.CompanionXAt(slot);
                    previousY[slot] = sim.CompanionYAt(slot);
                }

                previousEnemyId.Clear();
                previousEnemyX.Clear();
                previousEnemyY.Clear();
                var enemies = snapshot.Enemies;
                for (int index = 0; index < enemies.Count; index += 1)
                {
                    previousEnemyId.Add(enemies[index].Id);
                    previousEnemyX.Add(enemies[index].X);
                    previousEnemyY.Add(enemies[index].Y);
                }
            }

            Assert.That(engagedSamples, Is.GreaterThan(0), "the script must produce real pursuits");
        }

        /// <summary>A7.4: acquisition is anchor-relative and uses the single shared 300 px
        /// radius, never the per-archetype attack range.</summary>
        [Test]
        public void CompanionAutonomy_AcquiresOnlyInsideTheSharedAnchorRadius()
        {
            var config = DungeonSlots(Roster);
            var sim = new CinderSim(in config);
            ICinderSim snapshot = sim;

            var lastId = new int[Roster.Length];
            var previousEnemyId = new List<int>();
            var previousEnemyX = new List<float>();
            var previousEnemyY = new List<float>();
            int acquisitions = 0;

            for (int tick = 0; tick < ScriptTicks; tick += 1)
            {
                var input = HackScriptInput(tick);
                sim.Tick(in input);

                for (int slot = 0; slot < Roster.Length; slot += 1)
                {
                    int current = sim.CompanionTargetIdAt(slot);
                    if (current != 0 && current != lastId[slot])
                    {
                        int previousIndex = previousEnemyId.IndexOf(current);
                        if (previousIndex >= 0)
                        {
                            acquisitions += 1;
                            float distance = Iso(
                                previousEnemyX[previousIndex],
                                previousEnemyY[previousIndex],
                                AnchorX(sim),
                                AnchorY(sim, slot));
                            Assert.That(distance, Is.LessThanOrEqualTo(AcquireRadius + Tolerance),
                                $"slot {slot} acquired a target {distance} px from its anchor (tick {tick})");
                        }
                    }

                    lastId[slot] = current;
                }

                previousEnemyId.Clear();
                previousEnemyX.Clear();
                previousEnemyY.Clear();
                var enemies = snapshot.Enemies;
                for (int index = 0; index < enemies.Count; index += 1)
                {
                    previousEnemyId.Add(enemies[index].Id);
                    previousEnemyX.Add(enemies[index].X);
                    previousEnemyY.Add(enemies[index].Y);
                }
            }

            Assert.That(acquisitions, Is.GreaterThan(0), "the script must produce real acquisitions");
        }

        // --- 5. the lock ------------------------------------------------------

        /// <summary>
        /// A7.1. A lock is only given up for one of four reasons: the target died or left the
        /// list, the 2.0 s lock expired, the target drifted outside the leash, or the slot
        /// itself was dragged outside the leash. A nearer late arrival is never a reason —
        /// that is the whole point of the lock. Death is checked at BOTH ends of the release
        /// tick, because a lower-numbered slot can kill this slot's target before this slot
        /// updates, and because a slot that closes the gap can finish its own target inside
        /// the same tick it was still pursuing it.
        /// </summary>

        [Test]
        public void CompanionAutonomy_TargetLockOnlyChangesForALegalReason()
        {
            var config = DungeonSlots(Roster);
            var sim = new CinderSim(in config);
            ICinderSim snapshot = sim;

            var lastId = new int[Roster.Length];
            var acquiredTick = new int[Roster.Length];
            var previousX = new float[Roster.Length];
            var previousY = new float[Roster.Length];
            for (int slot = 0; slot < Roster.Length; slot += 1)
            {
                previousX[slot] = sim.CompanionXAt(slot);
                previousY[slot] = sim.CompanionYAt(slot);
            }

            var previousEnemyId = new List<int>();
            var previousEnemyX = new List<float>();
            var previousEnemyY = new List<float>();
            var previousEnemyDead = new List<bool>();
            int releases = 0;


            for (int tick = 0; tick < ScriptTicks; tick += 1)
            {
                var input = HackScriptInput(tick);
                sim.Tick(in input);
                var enemies = snapshot.Enemies;

                for (int slot = 0; slot < Roster.Length; slot += 1)
                {
                    int current = sim.CompanionTargetIdAt(slot);
                    if (lastId[slot] != 0 && current != lastId[slot])
                    {
                        releases += 1;

                        int previousIndex = previousEnemyId.IndexOf(lastId[slot]);
                        int liveIndex = IndexOfId(enemies, lastId[slot]);
                        float anchorY = AnchorY(sim, slot);

                        bool gone = previousIndex < 0
                            || previousEnemyDead[previousIndex]
                            || liveIndex < 0
                            || enemies[liveIndex].Dead;
                        bool expired = (tick - acquiredTick[slot]) * SimConfig.FixedStep
                            >= TargetLockSeconds - SimConfig.FixedStep;
                        bool targetOutsideLeash = previousIndex >= 0 && Iso(
                            previousEnemyX[previousIndex],
                            previousEnemyY[previousIndex],
                            AnchorX(sim),
                            anchorY) > LeashRadius;
                        bool slotOutsideLeash = Iso(
                            previousX[slot],
                            previousY[slot],
                            AnchorX(sim),
                            anchorY) > LeashRadius;

                        Assert.That(
                            gone || expired || targetOutsideLeash || slotOutsideLeash,
                            Is.True,
                            $"slot {slot} dropped a live, unexpired, in-leash lock at tick {tick} — "
                            + "a nearer arrival must never steal the target");
                    }

                    if (current != 0 && current != lastId[slot])
                    {
                        acquiredTick[slot] = tick;
                    }

                    lastId[slot] = current;
                    previousX[slot] = sim.CompanionXAt(slot);
                    previousY[slot] = sim.CompanionYAt(slot);
                }

                previousEnemyId.Clear();
                previousEnemyX.Clear();
                previousEnemyY.Clear();
                previousEnemyDead.Clear();
                for (int index = 0; index < enemies.Count; index += 1)
                {
                    previousEnemyId.Add(enemies[index].Id);
                    previousEnemyX.Add(enemies[index].X);
                    previousEnemyY.Add(enemies[index].Y);
                    previousEnemyDead.Add(enemies[index].Dead);
                }
            }

            Assert.That(releases, Is.GreaterThan(0), "the script must actually cycle through targets");

        }

        // --- 6. no teleport ---------------------------------------------------

        /// <summary>A7.2/A7.3: pursuit is the only thing that may exceed the follow speed, and
        /// it is capped at ×1.05. Nothing — not a lock drop, not a leash break, not a return —
        /// may snap a slot across the floor.</summary>
        [Test]
        public void CompanionAutonomy_NeverTeleportsAndCapsPursuitSpeed()
        {
            var config = DungeonSlots(Roster);
            var sim = new CinderSim(in config);

            var previousX = new float[Roster.Length];
            var previousY = new float[Roster.Length];
            for (int slot = 0; slot < Roster.Length; slot += 1)
            {
                previousX[slot] = sim.CompanionXAt(slot);
                previousY[slot] = sim.CompanionYAt(slot);
            }

            float cap = config.PlayerSpeed
                * PursuitSpeedScale
                * SimConfig.FixedStep
                + Tolerance;

            for (int tick = 0; tick < ScriptTicks; tick += 1)
            {
                var input = HackScriptInput(tick);
                sim.Tick(in input);

                for (int slot = 0; slot < Roster.Length; slot += 1)
                {
                    float deltaX = sim.CompanionXAt(slot) - previousX[slot];
                    float deltaY = sim.CompanionYAt(slot) - previousY[slot];
                    float step = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
                    Assert.That(step, Is.LessThanOrEqualTo(cap),
                        $"slot {slot} moved {step} px in one tick at tick {tick} — cap is {cap}");
                    previousX[slot] = sim.CompanionXAt(slot);
                    previousY[slot] = sim.CompanionYAt(slot);
                }
            }
        }

        // --- 7 & 8. commands still win ---------------------------------------

        /// <summary>Amendment #3 hold is unchanged by A7: a held slot is pinned and never
        /// reports engagement, yet it keeps swinging on its own cadence.</summary>
        [Test]
        public void CompanionAutonomy_HoldSuppressesPursuitButKeepsSwinging()
        {
            var config = DungeonSlots(Roster);
            var sim = new CinderSim(in config);
            for (int tick = 0; tick < 300; tick += 1)
            {
                var warmup = HackScriptInput(tick);
                sim.Tick(in warmup);
            }

            sim.Tick(new SimInput { CompanionHoldQueued = true });

            var heldX = new float[Roster.Length];
            var heldY = new float[Roster.Length];
            var wasAttacking = new bool[Roster.Length];
            for (int slot = 0; slot < Roster.Length; slot += 1)
            {
                heldX[slot] = sim.CompanionXAt(slot);
                heldY[slot] = sim.CompanionYAt(slot);
            }

            int swings = 0;
            for (int tick = 300; tick < 900; tick += 1)
            {
                var input = HackScriptInput(tick);
                sim.Tick(in input);

                for (int slot = 0; slot < Roster.Length; slot += 1)
                {
                    Assert.That(sim.CompanionXAt(slot), Is.EqualTo(heldX[slot]).Within(Tolerance),
                        $"hold must pin slot {slot} x even with a target in the leash (tick {tick})");
                    Assert.That(sim.CompanionYAt(slot), Is.EqualTo(heldY[slot]).Within(Tolerance),
                        $"hold must pin slot {slot} y even with a target in the leash (tick {tick})");
                    Assert.That(sim.CompanionEngagedAt(slot), Is.False,
                        $"a held slot must never report engagement (slot {slot}, tick {tick})");

                    bool attacking = sim.CompanionAttackingAt(slot);
                    if (attacking && !wasAttacking[slot])
                    {
                        swings += 1;
                    }

                    wasAttacking[slot] = attacking;
                }
            }

            Assert.That(swings, Is.GreaterThan(0),
                "A7 must not cost a held slot its swing — hold is a locomotion command only");
        }

        [Test]
        public void CompanionAutonomy_RecallResumesFollowWithoutTeleport()
        {
            var config = DungeonSlots(Roster);
            var sim = new CinderSim(in config);
            for (int tick = 0; tick < 300; tick += 1)
            {
                var warmup = HackScriptInput(tick);
                sim.Tick(in warmup);
            }

            sim.Tick(new SimInput { CompanionHoldQueued = true });
            var heldX = new float[Roster.Length];
            var heldY = new float[Roster.Length];
            for (int slot = 0; slot < Roster.Length; slot += 1)
            {
                heldX[slot] = sim.CompanionXAt(slot);
                heldY[slot] = sim.CompanionYAt(slot);
            }

            // Recall wins a same-tick tie with hold (Amendment #3), and A7 must not turn that
            // resumed step into a snap back to the anchor.
            sim.Tick(new SimInput { CompanionHoldQueued = true, CompanionRecallQueued = true });

            float cap = config.PlayerSpeed
                * PursuitSpeedScale
                * SimConfig.FixedStep
                + Tolerance;
            for (int slot = 0; slot < Roster.Length; slot += 1)
            {
                Assert.That(sim.CompanionBehaviorAt(slot), Is.EqualTo(CompanionBehavior.Follow),
                    $"recall must win the tie for slot {slot}");
                float deltaX = sim.CompanionXAt(slot) - heldX[slot];
                float deltaY = sim.CompanionYAt(slot) - heldY[slot];
                float step = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
                Assert.That(step, Is.LessThanOrEqualTo(cap),
                    $"recall must not teleport slot {slot} (moved {step} px)");
            }
        }

        // --- 11. restart ------------------------------------------------------

        [Test]
        public void CompanionAutonomy_RestartClearsTargetAndEngage()
        {
            var config = DungeonSlots(Roster);
            var sim = new CinderSim(in config);
            for (int tick = 0; tick < 600; tick += 1)
            {
                var input = HackScriptInput(tick);
                sim.Tick(in input);
            }

            sim.Restart();

            for (int slot = 0; slot < Roster.Length; slot += 1)
            {
                Assert.That(sim.CompanionEngagedAt(slot), Is.False, $"restart must clear slot {slot} engagement");
                Assert.That(sim.CompanionTargetIdAt(slot), Is.EqualTo(0), $"restart must clear slot {slot} lock");
                Assert.That(sim.CompanionBehaviorAt(slot), Is.EqualTo(CompanionBehavior.Follow),
                    $"restart must return slot {slot} to Follow");
                Assert.That(sim.CompanionXAt(slot), Is.EqualTo(AnchorX(sim)).Within(Tolerance),
                    $"restart must park slot {slot} on its anchor");
                Assert.That(sim.CompanionYAt(slot), Is.EqualTo(AnchorY(sim, slot)).Within(Tolerance),
                    $"restart must park slot {slot} on its fan-out");
            }
        }

        // --- 12 & 13. inert elsewhere ----------------------------------------

        [Test]
        public void CompanionAutonomy_InertInArenaAndPrologue()
        {
            var arena = new CinderSim(HackConfig.Arena());
            var prologue = new CinderSim(HackConfig.Prologue());

            for (int tick = 0; tick < 600; tick += 1)
            {
                var input = HackScriptInput(tick);
                arena.Tick(in input);
                prologue.Tick(in input);

                Assert.That(arena.CompanionEngagedAt(0), Is.False, $"arena has no companion to engage (tick {tick})");
                Assert.That(arena.CompanionTargetIdAt(0), Is.EqualTo(0), $"arena must hold no lock (tick {tick})");
                Assert.That(prologue.CompanionEngagedAt(0), Is.False, $"prologue has no companion (tick {tick})");
                Assert.That(prologue.CompanionTargetIdAt(0), Is.EqualTo(0), $"prologue must hold no lock (tick {tick})");
            }
        }

        /// <summary>A7.2: the five autonomy constants are the contract. This is the one place
        /// allowed to compare them against HackSpec — every other gate in this file uses the
        /// literals, so a retune has to be argued for here before the rest will accept it.</summary>
        [Test]
        public void CompanionAutonomy_ConstantsMatchTheFrozenContract()
        {
            Assert.That(HackSpec.CompanionAcquireRadius, Is.EqualTo(AcquireRadius).Within(Tolerance),
                "A7.4 acquire radius");
            Assert.That(HackSpec.CompanionLeashRadius, Is.EqualTo(LeashRadius).Within(Tolerance),
                "A7.2 leash radius");
            Assert.That(HackSpec.CompanionPursuitSpeedScale, Is.EqualTo(PursuitSpeedScale).Within(Tolerance),
                "A7.2 pursuit speed scale");
            Assert.That(HackSpec.CompanionTargetLockSeconds, Is.EqualTo(TargetLockSeconds).Within(Tolerance),
                "A7.1 target lock duration");
            Assert.That(HackSpec.CompanionReturnGraceSeconds, Is.EqualTo(ReturnGraceSeconds).Within(Tolerance),
                "A7.3 return grace");

            Assert.That(AcquireRadius, Is.LessThan(LeashRadius),
                "a slot must always be able to reach a target it is allowed to lock");
            Assert.That(PursuitSpeedScale, Is.GreaterThan(1f),
                "pursuit must be able to close on a foe walking at player speed");
            Assert.That(TargetLockSeconds / SimConfig.FixedStep,
                Is.EqualTo(MathF.Round(TargetLockSeconds / SimConfig.FixedStep)).Within(Tolerance),
                "the lock must be a whole number of fixed steps, or it is not frame-exact");
        }

        [Test]
        public void CompanionAutonomy_FreshSnapshotsDefaultToFollowWithNoTarget()
        {
            IHackSnapshot slots = new CinderSim(DungeonSlots(Roster));
            IHackSnapshot legacy = new CinderSim(DungeonScalar("ember-cohort"));
            IHackSnapshot bare = new CinderSim(DungeonScalar(null));

            foreach (var snapshot in new[] { slots, legacy, bare })
            {
                Assert.That(snapshot.CompanionBehavior, Is.EqualTo(CompanionBehavior.Follow),
                    "a migrated snapshot defaults to Follow, never to a pursuit state");
                Assert.That(snapshot.CompanionEngagedAt(0), Is.False, "engagement is derived, so it starts false");
                Assert.That(snapshot.CompanionTargetIdAt(0), Is.EqualTo(0), "a fresh run holds no lock");
            }

            // Out-of-range slots clamp to slot 0, exactly like the Amendment #6 accessors.
            Assert.That(slots.CompanionEngagedAt(-1), Is.EqualTo(slots.CompanionEngagedAt(0)));
            Assert.That(slots.CompanionEngagedAt(9), Is.EqualTo(slots.CompanionEngagedAt(0)));
            Assert.That(slots.CompanionTargetIdAt(-1), Is.EqualTo(slots.CompanionTargetIdAt(0)));
            Assert.That(slots.CompanionTargetIdAt(9), Is.EqualTo(slots.CompanionTargetIdAt(0)));
        }
    }
}
