// EditMode tests for the deterministic Cinder Court simulation.
// Numeric truth: docs/SIM_SPEC.md. Contract: Assets/Scripts/Sim/SimTypes.cs (FROZEN).
using System;
using CinderCourt.Sim;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class CinderSimTests
    {
        private const float Tolerance = 1e-4f;
        private const float Step = SimConfig.FixedStep;

        // Observed scenario landmarks of the idle run (no input), see the lane report.
        private const int FirstSpawnTick = 11;          // 0.18 s spawn delay
        private const int ContactTick = 200;            // 4 enemies alive, 3 inside the nova ring
        private const int IdleFirstDamageTick = 161;    // first contact damage without ward

        private static readonly SimInput Idle = default;

        // --- construction ----------------------------------------------------

        [Test]
        public void Restart_ProducesSpecInitialState()
        {
            var sim = new CinderSim();

            Assert.That(sim.Mode, Is.EqualTo(SimMode.Running));
            Assert.That(sim.Wave, Is.EqualTo(1));
            Assert.That(sim.Player.X, Is.EqualTo(768f).Within(Tolerance));
            Assert.That(sim.Player.Y, Is.EqualTo(646f).Within(Tolerance));
            Assert.That(sim.Player.Facing, Is.EqualTo(1));
            Assert.That(sim.Player.Health, Is.EqualTo(SimConfig.PlayerMaxHealth));
            Assert.That(sim.Player.Action, Is.EqualTo(ActorAction.Idle));
            Assert.That(sim.Player.AttackId, Is.EqualTo(0));
            Assert.That(sim.Charge, Is.EqualTo(SimConfig.LanternMax));
            Assert.That(sim.Score, Is.EqualTo(0));
            Assert.That(sim.Kills, Is.EqualTo(0));
            Assert.That(sim.Relics, Is.EqualTo(0));
            Assert.That(sim.PendingSpawns, Is.EqualTo(4));
            Assert.That(sim.LivingEnemies, Is.EqualTo(0));
            Assert.That(sim.Enemies.Count, Is.EqualTo(0));
            Assert.That(sim.Pickups.Count, Is.EqualTo(0));
            Assert.That(sim.Events, Is.EqualTo(SimEvents.None));
        }

        [Test]
        public void Restart_AfterGameOver_RewindsRunToWaveOne()
        {
            var sim = new CinderSim();
            RunUntilGameOver(sim);

            Assert.That(sim.Mode, Is.EqualTo(SimMode.GameOver));
            Assert.That(sim.Digest.Reason, Is.EqualTo("overrun"));
            Assert.That(sim.Digest.HealthRemaining, Is.EqualTo(0f));

            var restart = new SimInput { RestartQueued = true };
            sim.Tick(restart);

            Assert.That(sim.Mode, Is.EqualTo(SimMode.Running));
            Assert.That(sim.Wave, Is.EqualTo(1));
            Assert.That(sim.Player.Health, Is.EqualTo(SimConfig.PlayerMaxHealth));
            Assert.That(sim.Enemies.Count, Is.EqualTo(0));
            Assert.That(sim.Digest.Reason, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Tick_AfterGameOver_FreezesTheRun()
        {
            var sim = new CinderSim();
            RunUntilGameOver(sim);

            int enemyCount = sim.Enemies.Count;
            float firstX = enemyCount > 0 ? sim.Enemies[0].X : 0f;
            int score = sim.Score;

            for (int tick = 0; tick < 120; tick += 1)
            {
                sim.Tick(Idle);
            }

            Assert.That(sim.Mode, Is.EqualTo(SimMode.GameOver));
            Assert.That(sim.Enemies.Count, Is.EqualTo(enemyCount));
            Assert.That(sim.Score, Is.EqualTo(score));
            Assert.That(sim.Events, Is.EqualTo(SimEvents.None));
            if (enemyCount > 0)
            {
                Assert.That(sim.Enemies[0].X, Is.EqualTo(firstX));
            }
        }

        // --- determinism -----------------------------------------------------

        [Test]
        public void Tick_SameInputScript_ProducesIdenticalRun()
        {
            var left = new CinderSim();
            var right = new CinderSim();

            for (int tick = 1; tick <= 600; tick += 1)
            {
                SimInput input = ScriptedInput(tick);
                left.Tick(input);
                right.Tick(input);
            }

            // The script must exercise real combat, otherwise equality is vacuous.
            Assert.That(left.Kills, Is.GreaterThan(0), "scripted run never killed anything");
            Assert.That(left.Player.AttackId, Is.GreaterThan(0));

            Assert.That(right.Digest.Score, Is.EqualTo(left.Digest.Score));
            Assert.That(right.Digest.Wave, Is.EqualTo(left.Digest.Wave));
            Assert.That(right.Digest.Kills, Is.EqualTo(left.Digest.Kills));
            Assert.That(right.Digest.Relics, Is.EqualTo(left.Digest.Relics));
            Assert.That(right.Digest.HealthRemaining, Is.EqualTo(left.Digest.HealthRemaining));
            Assert.That(right.Digest.Reason, Is.EqualTo(left.Digest.Reason));
            Assert.That(right.Mode, Is.EqualTo(left.Mode));
            Assert.That(right.Charge, Is.EqualTo(left.Charge));
            Assert.That(right.NovaCooldown, Is.EqualTo(left.NovaCooldown));
            Assert.That(right.WardCooldown, Is.EqualTo(left.WardCooldown));
            Assert.That(right.PendingSpawns, Is.EqualTo(left.PendingSpawns));
            Assert.That(right.LivingEnemies, Is.EqualTo(left.LivingEnemies));

            Assert.That(right.Player.X, Is.EqualTo(left.Player.X));
            Assert.That(right.Player.Y, Is.EqualTo(left.Player.Y));
            Assert.That(right.Player.Facing, Is.EqualTo(left.Player.Facing));
            Assert.That(right.Player.ActionTime, Is.EqualTo(left.Player.ActionTime));
            Assert.That(right.Player.AttackId, Is.EqualTo(left.Player.AttackId));

            Assert.That(right.Enemies.Count, Is.EqualTo(left.Enemies.Count));
            for (int index = 0; index < left.Enemies.Count; index += 1)
            {
                EnemyState expected = left.Enemies[index];
                EnemyState actual = right.Enemies[index];
                Assert.That(actual.Id, Is.EqualTo(expected.Id));
                Assert.That(actual.X, Is.EqualTo(expected.X));
                Assert.That(actual.Y, Is.EqualTo(expected.Y));
                Assert.That(actual.Health, Is.EqualTo(expected.Health));
                Assert.That(actual.Dead, Is.EqualTo(expected.Dead));
                Assert.That(actual.Action, Is.EqualTo(expected.Action));
                Assert.That(actual.Visual, Is.EqualTo(expected.Visual));
            }

            Assert.That(right.Pickups.Count, Is.EqualTo(left.Pickups.Count));
            for (int index = 0; index < left.Pickups.Count; index += 1)
            {
                PickupState expected = left.Pickups[index];
                PickupState actual = right.Pickups[index];
                Assert.That(actual.Id, Is.EqualTo(expected.Id));
                Assert.That(actual.Kind, Is.EqualTo(expected.Kind));
                Assert.That(actual.X, Is.EqualTo(expected.X));
                Assert.That(actual.Y, Is.EqualTo(expected.Y));
                Assert.That(actual.Life, Is.EqualTo(expected.Life));
            }
        }

        // --- wave arithmetic -------------------------------------------------

        [Test]
        public void SpawnCountForWave_FollowsSpecFormula()
        {
            // min(20, 3 + floor(wave*1.2)) plus one boss slot on every 5th wave.
            Assert.That(CinderSim.SpawnCountForWave(1), Is.EqualTo(4));
            Assert.That(CinderSim.SpawnCountForWave(2), Is.EqualTo(5));
            Assert.That(CinderSim.SpawnCountForWave(4), Is.EqualTo(7));
            Assert.That(CinderSim.SpawnCountForWave(5), Is.EqualTo(10));   // 9 + boss
            Assert.That(CinderSim.SpawnCountForWave(10), Is.EqualTo(16));  // 15 + boss
            Assert.That(CinderSim.SpawnCountForWave(14), Is.EqualTo(19));
            Assert.That(CinderSim.SpawnCountForWave(15), Is.EqualTo(SimConfig.EnemyCap));
            Assert.That(CinderSim.SpawnCountForWave(30), Is.EqualTo(SimConfig.EnemyCap));
        }

        [Test]
        public void SpawnPointIndex_FollowsWaveSeedFormula()
        {
            // waveSeed = (wave*3) % 8, index = (waveSeed + id*3) % 8.
            Assert.That(CinderSim.SpawnPointIndexFor(1, 1), Is.EqualTo(6));
            Assert.That(CinderSim.SpawnPointIndexFor(1, 2), Is.EqualTo(1));
            Assert.That(CinderSim.SpawnPointIndexFor(1, 3), Is.EqualTo(4));
            Assert.That(CinderSim.SpawnPointIndexFor(1, 4), Is.EqualTo(7));
            Assert.That(CinderSim.SpawnPointIndexFor(5, 23), Is.EqualTo(4));
        }

        [Test]
        public void WaveOne_SpawnsFourEnemiesOnTheFormulaSpawnPoints()
        {
            var sim = new CinderSim();

            for (int tick = 1; tick < FirstSpawnTick; tick += 1)
            {
                sim.Tick(Idle);
                Assert.That(sim.Enemies.Count, Is.EqualTo(0), "spawn happened before the 0.18 s delay");
            }

            sim.Tick(Idle);
            Assert.That(sim.Enemies.Count, Is.EqualTo(1));
            float[] firstPoint = SimConfig.SpawnPoints[CinderSim.SpawnPointIndexFor(1, 1)];
            Assert.That(sim.Enemies[0].X, Is.EqualTo(firstPoint[0]));
            Assert.That(sim.Enemies[0].Y, Is.EqualTo(firstPoint[1]));
            Assert.That(sim.Enemies[0].Facing, Is.EqualTo(-1), "spawn right of the arena centre faces left");
            Assert.That(sim.Enemies[0].Health, Is.EqualTo(SimConfig.EnemyBaseHealth));
            Assert.That(sim.Enemies[0].IsBoss, Is.False);

            for (int tick = 0; tick < 180; tick += 1)
            {
                sim.Tick(Idle);
            }

            Assert.That(sim.PendingSpawns, Is.EqualTo(0));
            Assert.That(sim.LivingEnemies, Is.EqualTo(4));
            for (int index = 0; index < sim.Enemies.Count; index += 1)
            {
                Assert.That(sim.Enemies[index].Id, Is.EqualTo(index + 1));
                // Visual rotation is (wave + spawnIndexInWave) % 4.
                Assert.That((int)sim.Enemies[index].Visual, Is.EqualTo((1 + index) % 4));
            }
        }

        [Test]
        public void WaveClear_KeepsPlayerSkillsAndPickupsTicking()
        {
            var sim = new CinderSim();
            RunGreedyUntil(sim, 40000, s => s.Mode == SimMode.WaveClear);
            Assert.That(sim.Mode, Is.EqualTo(SimMode.WaveClear), "greedy run never cleared a wave");

            float chargeBefore = sim.Charge;
            float xBefore = sim.Player.X;
            int waveBefore = sim.Wave;

            var moveLeft = new SimInput { MoveX = -1f };
            sim.Tick(moveLeft);

            Assert.That(sim.Mode, Is.EqualTo(SimMode.WaveClear));
            Assert.That(sim.Wave, Is.EqualTo(waveBefore));
            Assert.That(sim.Player.X, Is.LessThan(xBefore), "player must keep moving during wave-clear");
            Assert.That(sim.Player.Moving, Is.True);
            if (chargeBefore < SimConfig.LanternMax)
            {
                Assert.That(sim.Charge, Is.GreaterThan(chargeBefore), "lantern oil must keep regenerating");
            }

            // 2.15 s intermission, then the next wave starts.
            int intermissionTicks = (int)MathF.Ceiling(SimConfig.WaveIntermission / Step);
            for (int tick = 0; tick < intermissionTicks; tick += 1)
            {
                sim.Tick(Idle);
            }
            Assert.That(sim.Wave, Is.EqualTo(waveBefore + 1));
            Assert.That(sim.Mode, Is.EqualTo(SimMode.Running));
            Assert.That(sim.PendingSpawns, Is.EqualTo(CinderSim.SpawnCountForWave(waveBefore + 1)));
        }

        // --- arena clamp -----------------------------------------------------

        [Test]
        public void Clamp_KeepsPlayerInsideTheL1Diamond()
        {
            float[] directionsX = { 1f, -1f, 1f, -1f, 1f, 0f };
            float[] directionsY = { 1f, -1f, -1f, 1f, 0f, 1f };

            for (int direction = 0; direction < directionsX.Length; direction += 1)
            {
                var sim = new CinderSim();
                var push = new SimInput { MoveX = directionsX[direction], MoveY = directionsY[direction] };
                for (int tick = 0; tick < 600; tick += 1)
                {
                    sim.Tick(push);
                    Assert.That(DiamondNorm(sim.Player.X, sim.Player.Y, SimConfig.PlayerMarginClamp),
                        Is.LessThanOrEqualTo(1f + Tolerance));
                }

                // The push must actually have reached the boundary, otherwise the
                // clamp was never exercised.
                Assert.That(DiamondNorm(sim.Player.X, sim.Player.Y, SimConfig.PlayerMarginClamp),
                    Is.GreaterThan(0.99f));
            }
        }

        [Test]
        public void Clamp_IsDiamondNotAxisAlignedBox()
        {
            var sim = new CinderSim();
            var push = new SimInput { MoveX = 1f, MoveY = 1f };
            for (int tick = 0; tick < 600; tick += 1)
            {
                sim.Tick(push);
            }

            float halfWidth = SimConfig.ArenaHalfWidth - SimConfig.PlayerMarginClamp;
            float halfHeight = SimConfig.ArenaHalfHeight - SimConfig.PlayerMarginClamp * 0.5f;
            float localX = MathF.Abs(sim.Player.X - SimConfig.ArenaX);
            float localY = MathF.Abs(sim.Player.Y - SimConfig.ArenaY);

            // An AABB clamp would allow both extremes at once; the diamond cannot.
            Assert.That(localX, Is.LessThan(halfWidth - 1f));
            Assert.That(localY, Is.LessThan(halfHeight - 1f));
            Assert.That(localX / halfWidth + localY / halfHeight, Is.EqualTo(1f).Within(1e-3f));
        }

        // --- nova ------------------------------------------------------------

        [Test]
        public void Nova_OutsideRadius_SpendsOilAndLeavesEnemyUntouched()
        {
            var sim = new CinderSim();
            for (int tick = 0; tick < FirstSpawnTick; tick += 1)
            {
                sim.Tick(Idle);
            }

            EnemyState before = sim.Enemies[0];
            Assert.That(IsoDistance(sim.Player.X, sim.Player.Y, before.X, before.Y),
                Is.GreaterThan(SimConfig.NovaRadius), "scenario expects the enemy outside the ring");
            float chargeBefore = sim.Charge;

            sim.Tick(new SimInput { NovaQueued = true });

            Assert.That(sim.Events & SimEvents.NovaCast, Is.EqualTo(SimEvents.NovaCast));
            Assert.That(sim.Events & SimEvents.EnemyHit, Is.EqualTo(SimEvents.None));
            Assert.That(sim.Enemies[0].Health, Is.EqualTo(before.Health));
            Assert.That(sim.Charge,
                Is.EqualTo(chargeBefore - SimConfig.NovaCost + SimConfig.LanternRegenPerSecond * Step).Within(Tolerance));
            Assert.That(sim.NovaCooldown, Is.EqualTo(SimConfig.NovaCooldown - Step).Within(Tolerance));
            Assert.That(sim.NovaFlash, Is.GreaterThan(0f));
            Assert.That(sim.NovaX, Is.EqualTo(sim.Player.X));
            Assert.That(sim.NovaY, Is.EqualTo(sim.Player.Y));
        }

        [Test]
        public void Nova_DamagesExactlyTheEnemiesInsideTheIsoRadius()
        {
            var sim = new CinderSim();
            for (int tick = 0; tick < ContactTick; tick += 1)
            {
                sim.Tick(Idle);
            }

            int enemyCount = sim.Enemies.Count;
            var ids = new int[enemyCount];
            var healthBefore = new float[enemyCount];
            var inside = new bool[enemyCount];
            int insideCount = 0;
            for (int index = 0; index < enemyCount; index += 1)
            {
                EnemyState enemy = sim.Enemies[index];
                ids[index] = enemy.Id;
                healthBefore[index] = enemy.Health;
                inside[index] = IsoDistance(sim.Player.X, sim.Player.Y, enemy.X, enemy.Y) <= SimConfig.NovaRadius;
                if (inside[index])
                {
                    insideCount += 1;
                }
            }

            Assert.That(insideCount, Is.GreaterThan(0), "scenario expects enemies inside the ring");
            Assert.That(insideCount, Is.LessThan(enemyCount), "scenario expects an enemy outside the ring");

            int killsBefore = sim.Kills;
            int scoreBefore = sim.Score;
            int relicsBefore = sim.Relics;
            sim.Tick(new SimInput { NovaQueued = true });

            for (int index = 0; index < enemyCount; index += 1)
            {
                EnemyState after = FindEnemy(sim, ids[index]);
                float expected = inside[index]
                    ? MathF.Max(0f, healthBefore[index] - SimConfig.NovaDamage)
                    : healthBefore[index];
                Assert.That(after.Health, Is.EqualTo(expected).Within(Tolerance), $"enemy {ids[index]}");
                Assert.That(after.Dead, Is.EqualTo(inside[index]), $"enemy {ids[index]} death state");
            }

            int relicsGained = sim.Relics - relicsBefore;
            Assert.That(sim.Kills, Is.EqualTo(killsBefore + insideCount));
            Assert.That(sim.Score,
                Is.EqualTo(scoreBefore + 100 * sim.Wave * insideCount + SimConfig.RelicScore * relicsGained));
        }

        // --- ward ------------------------------------------------------------

        [Test]
        public void Ward_RefusesDamageForThreeSecondsButStillBurnsGrace()
        {
            var baseline = new CinderSim();
            int firstDamage = -1;
            for (int tick = 1; tick <= 1200 && firstDamage < 0; tick += 1)
            {
                baseline.Tick(Idle);
                if ((baseline.Events & SimEvents.PlayerDamaged) != SimEvents.None)
                {
                    firstDamage = tick;
                }
            }
            Assert.That(firstDamage, Is.EqualTo(IdleFirstDamageTick));

            var sim = new CinderSim();
            for (int tick = 1; tick < IdleFirstDamageTick - 60; tick += 1)
            {
                sim.Tick(Idle);
            }

            float chargeBefore = sim.Charge;
            sim.Tick(new SimInput { WardQueued = true });
            Assert.That(sim.Events & SimEvents.WardCast, Is.EqualTo(SimEvents.WardCast));
            Assert.That(sim.WardCooldown, Is.EqualTo(SimConfig.WardCooldown - Step).Within(Tolerance));
            Assert.That(sim.Charge,
                Is.EqualTo(chargeBefore - SimConfig.WardCost + SimConfig.LanternRegenPerSecond * Step).Within(Tolerance));
            Assert.That(sim.Player.WardTime, Is.EqualTo(SimConfig.WardDuration - Step).Within(Tolerance));

            int wardTicks = (int)MathF.Round(SimConfig.WardDuration / Step) - 1;
            int graceTicks = 0;
            for (int tick = 0; tick < wardTicks; tick += 1)
            {
                sim.Tick(Idle);
                Assert.That(sim.Player.Health, Is.EqualTo(SimConfig.PlayerMaxHealth), "ward let damage through");
                Assert.That(sim.Events & SimEvents.PlayerDamaged, Is.EqualTo(SimEvents.None));
                if (sim.Player.DamageCooldown > 0f)
                {
                    graceTicks += 1;
                }
            }

            // The blocked contacts must still have consumed the 0.38 s grace window.
            Assert.That(graceTicks, Is.GreaterThan(0), "warded contact never burned the hit grace");
            Assert.That(sim.Player.WardTime, Is.LessThan(Step));

            // Once the ward is gone the same contacts hurt again: the test is not vacuous.
            for (int tick = 0; tick < 240; tick += 1)
            {
                sim.Tick(Idle);
            }
            Assert.That(sim.Player.Health, Is.LessThan(SimConfig.PlayerMaxHealth));
        }

        // --- pickups ---------------------------------------------------------

        [Test]
        public void Pickup_KindRotatesOnEnemyIdAndMagnetCollectsInstantly()
        {
            var sim = new CinderSim();
            for (int tick = 0; tick < ContactTick; tick += 1)
            {
                sim.Tick(Idle);
            }

            int enemyCount = sim.Enemies.Count;
            var ids = new int[enemyCount];
            var dropX = new float[enemyCount];
            var dropY = new float[enemyCount];
            var killed = new bool[enemyCount];
            for (int index = 0; index < enemyCount; index += 1)
            {
                EnemyState enemy = sim.Enemies[index];
                ids[index] = enemy.Id;
                dropX[index] = enemy.X;
                dropY[index] = enemy.Y;
                killed[index] = IsoDistance(sim.Player.X, sim.Player.Y, enemy.X, enemy.Y) <= SimConfig.NovaRadius;
            }

            sim.Tick(new SimInput { NovaQueued = true });

            int expectedDrops = 0;
            int expectedCollected = 0;
            for (int index = 0; index < enemyCount; index += 1)
            {
                if (!killed[index])
                {
                    continue;
                }
                expectedDrops += 1;
                bool magnet = IsoDistance(sim.Player.X, sim.Player.Y, dropX[index], dropY[index])
                    <= SimConfig.PickupMagnetRadius;
                if (magnet)
                {
                    expectedCollected += 1;
                }
            }

            Assert.That(expectedCollected, Is.GreaterThan(0), "scenario expects one drop inside the magnet radius");
            Assert.That(sim.Events & SimEvents.PickupCollected, Is.EqualTo(SimEvents.PickupCollected));
            Assert.That(sim.Pickups.Count, Is.EqualTo(expectedDrops - expectedCollected));

            // Every surviving drop sits outside the magnet radius and carries the
            // id-derived kind at its owner's death position.
            for (int index = 0; index < sim.Pickups.Count; index += 1)
            {
                PickupState pickup = sim.Pickups[index];
                Assert.That(IsoDistance(sim.Player.X, sim.Player.Y, pickup.X, pickup.Y),
                    Is.GreaterThan(SimConfig.PickupMagnetRadius));

                int owner = -1;
                for (int enemyIndex = 0; enemyIndex < enemyCount; enemyIndex += 1)
                {
                    if (MathF.Abs(dropX[enemyIndex] - pickup.X) < Tolerance
                        && MathF.Abs(dropY[enemyIndex] - pickup.Y) < Tolerance)
                    {
                        owner = ids[enemyIndex];
                    }
                }
                Assert.That(owner, Is.GreaterThan(0), "pickup did not spawn on a dead enemy");
                Assert.That((int)pickup.Kind, Is.EqualTo(owner % 3));
                Assert.That(pickup.Life, Is.EqualTo(SimConfig.PickupLifetime - Step).Within(Tolerance));
            }
        }

        [Test]
        public void Pickup_RelicMoteAppliesScoreAndRelicWhenWalkedOver()
        {
            var sim = new CinderSim();
            for (int tick = 0; tick < ContactTick; tick += 1)
            {
                sim.Tick(Idle);
            }
            sim.Tick(new SimInput { NovaQueued = true });

            int relicId = -1;
            for (int index = 0; index < sim.Pickups.Count; index += 1)
            {
                if (sim.Pickups[index].Kind == PickupKind.RelicMote)
                {
                    relicId = sim.Pickups[index].Id;
                }
            }
            Assert.That(relicId, Is.GreaterThan(0), "scenario expects a relic mote on the ground");

            int scoreBefore = sim.Score;
            int relicsBefore = sim.Relics;

            bool collected = false;
            for (int tick = 0; tick < 240 && !collected; tick += 1)
            {
                PickupState target = default;
                bool found = false;
                for (int index = 0; index < sim.Pickups.Count; index += 1)
                {
                    if (sim.Pickups[index].Id == relicId)
                    {
                        target = sim.Pickups[index];
                        found = true;
                    }
                }
                if (!found)
                {
                    collected = true;
                    break;
                }

                float toX = target.X - sim.Player.X;
                float toY = target.Y - sim.Player.Y;
                float length = MathF.Sqrt(toX * toX + toY * toY);
                var walk = default(SimInput);
                if (length > 0.001f)
                {
                    walk.MoveX = toX / length;
                    walk.MoveY = toY / length;
                }
                sim.Tick(walk);
            }

            Assert.That(collected, Is.True, "player never reached the relic mote");
            Assert.That(sim.Relics, Is.EqualTo(relicsBefore + 1));
            Assert.That(sim.Score, Is.EqualTo(scoreBefore + SimConfig.RelicScore));
        }

        [Test]
        public void Pickup_ExpiresAfterTwelveSeconds()
        {
            var sim = new CinderSim();
            for (int tick = 0; tick < ContactTick; tick += 1)
            {
                sim.Tick(Idle);
            }
            sim.Tick(new SimInput { NovaQueued = true });

            Assert.That(sim.Pickups.Count, Is.GreaterThan(0));
            int trackedId = sim.Pickups[0].Id;
            int relicsBefore = sim.Relics;

            // Kite away so the magnet never touches the drop.
            var kite = new SimInput { MoveX = -1f };
            int elapsed = 0;
            bool present = true;
            while (present && elapsed < 900)
            {
                sim.Tick(kite);
                elapsed += 1;
                present = false;
                for (int index = 0; index < sim.Pickups.Count; index += 1)
                {
                    if (sim.Pickups[index].Id == trackedId)
                    {
                        present = true;
                    }
                }
            }

            Assert.That(present, Is.False, "pickup outlived its 12 s lifetime");
            Assert.That(elapsed, Is.EqualTo((int)MathF.Round(SimConfig.PickupLifetime / Step)));
            Assert.That(sim.Relics, Is.EqualTo(relicsBefore), "pickup was collected instead of expiring");
        }

        // --- melee judgement -------------------------------------------------

        [Test]
        public void Attack_HitsEnemyInFrontOncePerAttackId()
        {
            var sim = new CinderSim();
            for (int tick = 0; tick < ContactTick; tick += 1)
            {
                sim.Tick(Idle);
            }

            int targetId = NearestInFrontOfLeft(sim);
            EnemyState target = FindEnemy(sim, targetId);
            Assert.That(IsoDistance(sim.Player.X, sim.Player.Y, target.X, target.Y),
                Is.LessThanOrEqualTo(SimConfig.PlayerAttackRange));
            float healthBefore = target.Health;

            sim.Tick(new SimInput { MoveX = -1f });                     // face the target
            Assert.That(sim.Player.Facing, Is.EqualTo(-1));
            sim.Tick(new SimInput { AttackQueued = true });
            Assert.That(sim.Player.Action, Is.EqualTo(ActorAction.Attack));
            // Cooldowns tick down before the swing starts, so the fresh 0.48 s is intact.
            Assert.That(sim.Player.AttackCooldown, Is.EqualTo(SimConfig.PlayerAttackCooldown).Within(Tolerance));
            Assert.That(sim.Player.ActionTime, Is.EqualTo(Step).Within(Tolerance));

            float damageActionTime = -1f;
            float health = healthBefore;
            int hitTicks = 0;
            for (int tick = 0; tick < 22; tick += 1)
            {
                float previous = FindEnemy(sim, targetId).Health;
                sim.Tick(Idle);
                health = FindEnemy(sim, targetId).Health;
                if ((sim.Events & SimEvents.EnemyHit) != SimEvents.None)
                {
                    hitTicks += 1;
                }
                if (health < previous && damageActionTime < 0f)
                {
                    damageActionTime = sim.Player.ActionTime;
                }
            }

            // The whole active window is 10 ticks long; lastHitAttack must collapse
            // it into a single damage tick.
            Assert.That(hitTicks, Is.EqualTo(1), "one attackId damaged enemies on more than one tick");

            Assert.That(health, Is.EqualTo(MathF.Max(0f, healthBefore - SimConfig.PlayerDamage)).Within(Tolerance),
                "one attackId must land exactly one hit");
            Assert.That(damageActionTime, Is.GreaterThanOrEqualTo(SimConfig.AttackActiveFrom - Tolerance));
            Assert.That(damageActionTime, Is.LessThan(SimConfig.AttackActiveTo));
        }

        [Test]
        public void Attack_MissesEnemiesBehindTheFacingArc()
        {
            var sim = new CinderSim();
            for (int tick = 0; tick < ContactTick; tick += 1)
            {
                sim.Tick(Idle);
            }

            int targetId = NearestInFrontOfLeft(sim);
            float healthBefore = FindEnemy(sim, targetId).Health;

            sim.Tick(new SimInput { MoveX = 1f });                      // turn our back on it
            Assert.That(sim.Player.Facing, Is.EqualTo(1));
            sim.Tick(new SimInput { AttackQueued = true });

            bool sawActiveWindow = false;
            for (int tick = 0; tick < 22; tick += 1)
            {
                sim.Tick(Idle);
                if (sim.Player.Action != ActorAction.Attack
                    || sim.Player.ActionTime < SimConfig.AttackActiveFrom
                    || sim.Player.ActionTime >= SimConfig.AttackActiveTo)
                {
                    continue;
                }

                sawActiveWindow = true;
                EnemyState target = FindEnemy(sim, targetId);
                // In range but behind: only the facing arc can reject this hit.
                Assert.That(IsoDistance(sim.Player.X, sim.Player.Y, target.X, target.Y),
                    Is.LessThanOrEqualTo(SimConfig.PlayerAttackRange));
                Assert.That((target.X - sim.Player.X) * sim.Player.Facing,
                    Is.LessThan(SimConfig.FacingArcTolerance));
            }

            Assert.That(sawActiveWindow, Is.True, "attack never reached its active window");
            Assert.That(FindEnemy(sim, targetId).Health, Is.EqualTo(healthBefore), "rear enemy was hit");
        }

        [Test]
        public void Attack_RangeUsesIsoWeightedDistance()
        {
            var sim = new CinderSim();
            int targetId = -1;

            // Find an enemy that is inside the raw radius but outside the iso radius.
            for (int tick = 0; tick < 400 && targetId < 0; tick += 1)
            {
                sim.Tick(Idle);
                for (int index = 0; index < sim.Enemies.Count; index += 1)
                {
                    EnemyState enemy = sim.Enemies[index];
                    if (enemy.Dead)
                    {
                        continue;
                    }
                    float raw = RawDistance(sim.Player.X, sim.Player.Y, enemy.X, enemy.Y);
                    float iso = IsoDistance(sim.Player.X, sim.Player.Y, enemy.X, enemy.Y);
                    bool inFront = (enemy.X - sim.Player.X) * sim.Player.Facing >= SimConfig.FacingArcTolerance;
                    if (inFront && raw <= SimConfig.PlayerAttackRange && iso > SimConfig.PlayerAttackRange + 40f)
                    {
                        targetId = enemy.Id;
                    }
                }
            }

            Assert.That(targetId, Is.GreaterThan(0), "no iso-vs-raw boundary case appeared");
            float healthBefore = FindEnemy(sim, targetId).Health;

            sim.Tick(new SimInput { AttackQueued = true });
            bool sawActiveWindow = false;
            for (int tick = 0; tick < 22; tick += 1)
            {
                sim.Tick(Idle);
                if (sim.Player.Action != ActorAction.Attack
                    || sim.Player.ActionTime < SimConfig.AttackActiveFrom
                    || sim.Player.ActionTime >= SimConfig.AttackActiveTo)
                {
                    continue;
                }

                sawActiveWindow = true;
                EnemyState target = FindEnemy(sim, targetId);
                Assert.That(RawDistance(sim.Player.X, sim.Player.Y, target.X, target.Y),
                    Is.LessThanOrEqualTo(SimConfig.PlayerAttackRange));
                Assert.That(IsoDistance(sim.Player.X, sim.Player.Y, target.X, target.Y),
                    Is.GreaterThan(SimConfig.PlayerAttackRange));
                Assert.That((target.X - sim.Player.X) * sim.Player.Facing,
                    Is.GreaterThanOrEqualTo(SimConfig.FacingArcTolerance));
            }

            Assert.That(sawActiveWindow, Is.True, "attack never reached its active window");
            Assert.That(FindEnemy(sim, targetId).Health, Is.EqualTo(healthBefore),
                "iso y weighting did not reject the hit");
        }

        // --- bosses ----------------------------------------------------------

        [Test]
        public void Boss_SpawnsOnWaveFiveWithSixTimesHealth()
        {
            var sim = new CinderSim();
            RunGreedyUntil(sim, 40000, s => (s.Events & SimEvents.BossSpawned) != SimEvents.None);

            Assert.That(sim.Events & SimEvents.BossSpawned, Is.EqualTo(SimEvents.BossSpawned),
                "greedy run never reached a boss wave");
            Assert.That(sim.Wave, Is.EqualTo(SimConfig.BossEveryWaves));

            int bossCount = 0;
            EnemyState boss = default;
            for (int index = 0; index < sim.Enemies.Count; index += 1)
            {
                if (sim.Enemies[index].IsBoss)
                {
                    bossCount += 1;
                    boss = sim.Enemies[index];
                }
            }

            float expectedHealth = (SimConfig.EnemyBaseHealth + MathF.Min(92f, (5 - 1) * 9f)) * SimConfig.BossHealthMul;
            Assert.That(bossCount, Is.EqualTo(1));
            Assert.That(expectedHealth, Is.EqualTo(564f));
            Assert.That(boss.MaxHealth, Is.EqualTo(expectedHealth).Within(Tolerance));
            Assert.That(boss.Health, Is.EqualTo(expectedHealth).Within(Tolerance));
            Assert.That(boss.Scale, Is.EqualTo(SimConfig.BossScale));
            Assert.That(boss.Visual, Is.EqualTo(EnemyVisual.BossCommander));   // wave % 10 == 5

            // The boss leads the wave, so the queue is the spec count minus that spawn.
            Assert.That(sim.PendingSpawns, Is.EqualTo(CinderSim.SpawnCountForWave(5) - 1));
            float[] point = SimConfig.SpawnPoints[CinderSim.SpawnPointIndexFor(5, boss.Id)];
            Assert.That(boss.X, Is.EqualTo(point[0]));
            Assert.That(boss.Y, Is.EqualTo(point[1]));
        }

        // --- helpers ---------------------------------------------------------

        private static SimInput ScriptedInput(int tick)
        {
            var input = default(SimInput);
            input.MoveX = tick / 37 % 3 - 1;
            input.MoveY = tick / 53 % 3 - 1;
            input.AttackQueued = tick % 19 == 0;
            input.NovaQueued = tick % 211 == 0;
            input.WardQueued = tick % 307 == 0;
            return input;
        }

        /// <summary>
        /// Deterministic greedy pilot: close on the nearest enemy, swing constantly,
        /// and spend both skills the moment they are ready.
        /// </summary>
        private static void RunGreedyUntil(CinderSim sim, int maxTicks, Func<CinderSim, bool> stop)
        {
            for (int tick = 0; tick < maxTicks; tick += 1)
            {
                var input = new SimInput { AttackQueued = true, NovaQueued = true, WardQueued = true };
                float best = float.MaxValue;
                float toX = 0f;
                float toY = 0f;
                for (int index = 0; index < sim.Enemies.Count; index += 1)
                {
                    EnemyState enemy = sim.Enemies[index];
                    if (enemy.Dead)
                    {
                        continue;
                    }
                    float distance = IsoDistance(sim.Player.X, sim.Player.Y, enemy.X, enemy.Y);
                    if (distance < best)
                    {
                        best = distance;
                        toX = enemy.X - sim.Player.X;
                        toY = enemy.Y - sim.Player.Y;
                    }
                }

                if (best < float.MaxValue && best > 130f)
                {
                    float length = MathF.Sqrt(toX * toX + toY * toY);
                    if (length > 0.001f)
                    {
                        input.MoveX = toX / length;
                        input.MoveY = toY / length;
                    }
                }

                sim.Tick(input);
                if (stop(sim) || sim.Mode == SimMode.GameOver)
                {
                    return;
                }
            }
        }

        private static void RunUntilGameOver(CinderSim sim)
        {
            for (int tick = 0; tick < 20000 && sim.Mode != SimMode.GameOver; tick += 1)
            {
                sim.Tick(Idle);
            }
            Assert.That(sim.Mode, Is.EqualTo(SimMode.GameOver), "idle run never ended");
        }

        /// <summary>Nearest living enemy that stands clearly left of the player.</summary>
        private static int NearestInFrontOfLeft(CinderSim sim)
        {
            int id = -1;
            float best = float.MaxValue;
            for (int index = 0; index < sim.Enemies.Count; index += 1)
            {
                EnemyState enemy = sim.Enemies[index];
                if (enemy.Dead)
                {
                    continue;
                }
                float deltaX = enemy.X - sim.Player.X;
                float distance = IsoDistance(sim.Player.X, sim.Player.Y, enemy.X, enemy.Y);
                if (deltaX < 2f * SimConfig.FacingArcTolerance
                    && distance <= SimConfig.PlayerAttackRange
                    && distance < best)
                {
                    best = distance;
                    id = enemy.Id;
                }
            }
            Assert.That(id, Is.GreaterThan(0), "scenario expects an enemy left of the player and in range");
            return id;
        }

        private static EnemyState FindEnemy(CinderSim sim, int id)
        {
            for (int index = 0; index < sim.Enemies.Count; index += 1)
            {
                if (sim.Enemies[index].Id == id)
                {
                    return sim.Enemies[index];
                }
            }
            Assert.Fail($"enemy {id} left the simulation");
            return default;
        }

        private static float DiamondNorm(float x, float y, float margin)
        {
            float halfWidth = SimConfig.ArenaHalfWidth - margin;
            float halfHeight = SimConfig.ArenaHalfHeight - margin * 0.5f;
            return MathF.Abs(x - SimConfig.ArenaX) / halfWidth + MathF.Abs(y - SimConfig.ArenaY) / halfHeight;
        }

        private static float IsoDistance(float ax, float ay, float bx, float by)
        {
            float deltaX = bx - ax;
            float deltaY = (by - ay) * SimConfig.IsoY;
            return MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        private static float RawDistance(float ax, float ay, float bx, float by)
        {
            float deltaX = bx - ax;
            float deltaY = by - ay;
            return MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }
    }
}
