// AMENDMENT #14 (W5) — graded loot + bad-luck protection.
// Numeric truth: docs/SIM_SPEC_HACKSLASH.md §18.
//
// §13 (RNG 금지) is NOT amended: the roll is an integer avalanche hash of run
// state and the guarantees come from two monotone counters, so both the
// distribution AND the pity floors are exact properties, not statistics.
using CinderCourt.Sim;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class LootGradeTests
    {
        private const float Tolerance = 1e-4f;

        private static HackConfig Dungeon()
        {
            Assert.IsTrue(
                HackConfig.TryDungeon(
                    CampaignStages.CinderSpan,
                    MetaStats.Of(0, 0, 0),
                    EquipTiers.Of(0, 0, 0),
                    (string)null,
                    0,
                    out var config),
                "unknown stage");
            return config;
        }

        private static SimInput Script(int tick)
        {
            var input = default(SimInput);
            input.MoveX = tick / 90 % 2 == 0 ? 1f : -1f;
            input.MoveY = tick / 150 % 2 == 0 ? 0.6f : -0.6f;
            input.AttackQueued = tick % 11 == 0;
            input.AttackHeld = tick % 11 == 0;
            input.NovaQueued = tick % 380 == 0;
            input.BoltQueued = tick % 233 == 0;
            return input;
        }

        // W5-1 게이트: 미지정(기존 생성자) == default(DungeonProgressionConfig).
        // 픽업 페이로드가 그대로여야 골든 다이제스트가 재-bless 없이 통과한다.
        [Test]
        public void GradedLoot_Off_IsLockstepWithTheFrozenConstructor()
        {
            HackConfig config = Dungeon();
            var frozen = new CinderSim(in config);
            var gatedOff = new CinderSim(in config, default);
            Assert.IsFalse(gatedOff.GradedLootActive, "default(DungeonProgressionConfig) must be inert");

            for (int tick = 0; tick < 3600; tick += 1)
            {
                SimInput input = Script(tick);
                frozen.Tick(in input);
                gatedOff.Tick(in input);

                Assert.That(gatedOff.Player.Health, Is.EqualTo(frozen.Player.Health).Within(Tolerance), $"tick {tick} HP");
                Assert.That(gatedOff.Charge, Is.EqualTo(frozen.Charge).Within(Tolerance), $"tick {tick} charge");
                Assert.That(gatedOff.Score, Is.EqualTo(frozen.Score), $"tick {tick} score");
                Assert.That(gatedOff.Relics, Is.EqualTo(frozen.Relics), $"tick {tick} relics");
                Assert.That(gatedOff.Pickups.Count, Is.EqualTo(frozen.Pickups.Count), $"tick {tick} pickup count");
                for (int index = 0; index < frozen.Pickups.Count; index += 1)
                {
                    Assert.That(gatedOff.Pickups[index].Id, Is.EqualTo(frozen.Pickups[index].Id), $"tick {tick} pickup[{index}] id");
                    Assert.That(gatedOff.Pickups[index].Kind, Is.EqualTo(frozen.Pickups[index].Kind), $"tick {tick} pickup[{index}] kind");
                }
            }
        }

        // W5-2 롤: 0..99 범위 안이고, 같은 입력이면 항상 같은 값이며, 축퇴하지 않는다.
        [Test]
        public void Roll_IsDeterministicInRangeAndNonDegenerate()
        {
            var seen = new bool[LootGradeSpec.RollModulus];
            int distinct = 0;
            for (int ordinal = 1; ordinal <= 2000; ordinal += 1)
            {
                int roll = LootGradeSpec.Roll(ordinal, 1 + ordinal / 9, ordinal);
                Assert.That(roll, Is.InRange(0, LootGradeSpec.RollModulus - 1), $"ordinal {ordinal} out of range");
                Assert.That(
                    LootGradeSpec.Roll(ordinal, 1 + ordinal / 9, ordinal),
                    Is.EqualTo(roll),
                    $"ordinal {ordinal} is not reproducible");
                if (!seen[roll])
                {
                    seen[roll] = true;
                    distinct += 1;
                }
            }
            Assert.That(distinct, Is.GreaterThan(80), "the hash collapsed onto too few buckets to be a usable roll");
            // Negative and zero arguments must not throw or escape the range.
            Assert.That(LootGradeSpec.Roll(0, 0, 0), Is.InRange(0, LootGradeSpec.RollModulus - 1));
            Assert.That(LootGradeSpec.Roll(-5, -3, -1), Is.InRange(0, LootGradeSpec.RollModulus - 1));
        }

        // W5-3 우선순위: epic pity > fine pity > 롤.
        [Test]
        public void Resolve_AppliesPityBeforeTheRoll()
        {
            // The roll alone.
            Assert.That(LootGradeSpec.Resolve(0, 0, 0), Is.EqualTo(LootGrade.Basic));
            Assert.That(LootGradeSpec.Resolve(LootGradeSpec.FineThreshold - 1, 0, 0), Is.EqualTo(LootGrade.Basic));
            Assert.That(LootGradeSpec.Resolve(LootGradeSpec.FineThreshold, 0, 0), Is.EqualTo(LootGrade.Fine));
            Assert.That(LootGradeSpec.Resolve(LootGradeSpec.EpicThreshold - 1, 0, 0), Is.EqualTo(LootGrade.Fine));
            Assert.That(LootGradeSpec.Resolve(LootGradeSpec.EpicThreshold, 0, 0), Is.EqualTo(LootGrade.Epic));

            // Fine pity forces at least Fine on the worst possible roll.
            Assert.That(LootGradeSpec.Resolve(0, LootGradeSpec.FinePityLimit - 1, 0), Is.EqualTo(LootGrade.Basic));
            Assert.That(LootGradeSpec.Resolve(0, LootGradeSpec.FinePityLimit, 0), Is.EqualTo(LootGrade.Fine));

            // Epic pity outranks fine pity AND the roll.
            Assert.That(LootGradeSpec.Resolve(0, LootGradeSpec.FinePityLimit, LootGradeSpec.EpicPityLimit), Is.EqualTo(LootGrade.Epic));
            Assert.That(LootGradeSpec.Resolve(0, 0, LootGradeSpec.EpicPityLimit), Is.EqualTo(LootGrade.Epic));
        }

        // W5-4 원장: 등급 부여가 카운터를 정확히 리셋/증가시킨다.
        [Test]
        public void Advance_ResetsAndIncrementsTheLedger()
        {
            int fine = 0;
            int epic = 0;

            LootGradeSpec.Advance(LootGrade.Basic, ref fine, ref epic);
            Assert.That(fine, Is.EqualTo(1));
            Assert.That(epic, Is.EqualTo(1));

            LootGradeSpec.Advance(LootGrade.Fine, ref fine, ref epic);
            Assert.That(fine, Is.EqualTo(0), "a Fine drop clears the fine ledger");
            Assert.That(epic, Is.EqualTo(2), "a Fine drop still advances the epic ledger");

            LootGradeSpec.Advance(LootGrade.Basic, ref fine, ref epic);
            LootGradeSpec.Advance(LootGrade.Epic, ref fine, ref epic);
            Assert.That(fine, Is.EqualTo(0), "an Epic drop clears both");
            Assert.That(epic, Is.EqualTo(0), "an Epic drop clears both");
        }

        // W5-5 bad-luck protection: 어떤 롤 시퀀스에서도 Basic 6연속 / non-Epic 19연속은
        // 구조적으로 불가능하다. 최악의 입력(항상 롤 0)으로 증명한다.
        [Test]
        public void Pity_BoundsBothStreaks_EvenOnTheWorstPossibleRolls()
        {
            int fine = 0;
            int epic = 0;
            int basicStreak = 0;
            int nonEpicStreak = 0;
            int maxBasicStreak = 0;
            int maxNonEpicStreak = 0;

            for (int drop = 0; drop < 500; drop += 1)
            {
                LootGrade grade = LootGradeSpec.Resolve(0, fine, epic);
                LootGradeSpec.Advance(grade, ref fine, ref epic);

                if (grade == LootGrade.Basic)
                {
                    basicStreak += 1;
                    if (basicStreak > maxBasicStreak)
                    {
                        maxBasicStreak = basicStreak;
                    }
                }
                else
                {
                    basicStreak = 0;
                }

                if (grade == LootGrade.Epic)
                {
                    nonEpicStreak = 0;
                }
                else
                {
                    nonEpicStreak += 1;
                    if (nonEpicStreak > maxNonEpicStreak)
                    {
                        maxNonEpicStreak = nonEpicStreak;
                    }
                }
            }

            Assert.That(maxBasicStreak, Is.LessThanOrEqualTo(LootGradeSpec.FinePityLimit), "fine pity floor breached");
            Assert.That(maxNonEpicStreak, Is.LessThanOrEqualTo(LootGradeSpec.EpicPityLimit), "epic pity floor breached");
            Assert.That(maxBasicStreak, Is.EqualTo(LootGradeSpec.FinePityLimit), "fine pity should be exactly tight on an all-zero roll");
            Assert.That(maxNonEpicStreak, Is.EqualTo(LootGradeSpec.EpicPityLimit), "epic pity should be exactly tight on an all-zero roll");
        }

        // W5-6 실제 롤 분포에서도 pity 상한이 유지되고, 세 등급 모두 나온다.
        [Test]
        public void Pity_HoldsOverTheRealRollSequence()
        {
            int fine = 0;
            int epic = 0;
            int basics = 0;
            int fines = 0;
            int epics = 0;
            int basicStreak = 0;
            int nonEpicStreak = 0;

            for (int ordinal = 1; ordinal <= 4000; ordinal += 1)
            {
                int roll = LootGradeSpec.Roll(ordinal, 1 + ordinal / 12, ordinal);
                LootGrade grade = LootGradeSpec.Resolve(roll, fine, epic);
                LootGradeSpec.Advance(grade, ref fine, ref epic);

                basicStreak = grade == LootGrade.Basic ? basicStreak + 1 : 0;
                nonEpicStreak = grade == LootGrade.Epic ? 0 : nonEpicStreak + 1;
                Assert.That(basicStreak, Is.LessThanOrEqualTo(LootGradeSpec.FinePityLimit), $"drop {ordinal} broke the fine floor");
                Assert.That(nonEpicStreak, Is.LessThanOrEqualTo(LootGradeSpec.EpicPityLimit), $"drop {ordinal} broke the epic floor");

                if (grade == LootGrade.Basic)
                {
                    basics += 1;
                }
                else if (grade == LootGrade.Fine)
                {
                    fines += 1;
                }
                else
                {
                    epics += 1;
                }
            }

            Assert.That(basics, Is.GreaterThan(0));
            Assert.That(fines, Is.GreaterThan(0));
            Assert.That(epics, Is.GreaterThan(0));
            // The pity floors pull the tail up, so the realised rates sit above the
            // raw thresholds. Both bands are pinned so a retune cannot drift silently.
            Assert.That(fines * 100 / 4000, Is.InRange(20, 32), "Fine rate drifted");
            Assert.That(epics * 100 / 4000, Is.InRange(8, 14), "Epic rate drifted");
        }

        // W5-7 등급 → 페이로드 배율표와 랭크 스텝표. 표 밖 값은 클램프.
        [Test]
        public void GradeTables_ArePinnedAndClamped()
        {
            Assert.That(LootGradeSpec.ValueMultiplier(LootGrade.Basic), Is.EqualTo(1.00f).Within(Tolerance));
            Assert.That(LootGradeSpec.ValueMultiplier(LootGrade.Fine), Is.EqualTo(1.45f).Within(Tolerance));
            Assert.That(LootGradeSpec.ValueMultiplier(LootGrade.Epic), Is.EqualTo(2.10f).Within(Tolerance));
            Assert.That(LootGradeSpec.RankSteps(LootGrade.Basic), Is.EqualTo(1));
            Assert.That(LootGradeSpec.RankSteps(LootGrade.Fine), Is.EqualTo(1));
            Assert.That(LootGradeSpec.RankSteps(LootGrade.Epic), Is.EqualTo(2));

            Assert.That(LootGradeSpec.ValueMultiplier((LootGrade)(-3)), Is.EqualTo(1.00f).Within(Tolerance));
            Assert.That(LootGradeSpec.ValueMultiplier((LootGrade)99), Is.EqualTo(2.10f).Within(Tolerance));
            Assert.That(LootGradeSpec.RankSteps((LootGrade)(-3)), Is.EqualTo(1));
            Assert.That(LootGradeSpec.RankSteps((LootGrade)99), Is.EqualTo(2));

            // Pinned realised payloads, so a retune of either table shows up here.
            Assert.That(SimConfig.EmberShardHeal * LootGradeSpec.ValueMultiplier(LootGrade.Epic), Is.EqualTo(37.8f).Within(1e-2f));
            Assert.That(SimConfig.OilFlaskCharge * LootGradeSpec.ValueMultiplier(LootGrade.Fine), Is.EqualTo(50.75f).Within(1e-2f));
            // +0.5f mirrors CinderSim.CollectPickup: under Unity's float
            // semantics 250 x 2.10f is 524.999..., so bare truncation pins 524
            // (the dotnet double path that authored this test said 525).
            Assert.That((int)(SimConfig.RelicScore * LootGradeSpec.ValueMultiplier(LootGrade.Epic) + 0.5f), Is.EqualTo(525));
        }

        // W5-8 심 연동: 등급 배열이 픽업 배열과 인덱스 정렬을 유지한다(수거·만료 후에도).
        [Test]
        public void PickupGrades_StayIndexAlignedWithPickups()
        {
            HackConfig config = Dungeon();
            var sim = new CinderSim(in config, DungeonProgressionConfig.All);
            Assert.IsTrue(sim.GradedLootActive, "dungeon must arm #14 when opted in");
            int observedPickups = 0;

            for (int tick = 0; tick < 5400; tick += 1)
            {
                SimInput input = Script(tick);
                sim.Tick(in input);

                Assert.That(
                    sim.PickupGrades.Count,
                    Is.EqualTo(sim.Pickups.Count),
                    $"tick {tick}: grade array desynced from the pickup array");
                for (int index = 0; index < sim.PickupGrades.Count; index += 1)
                {
                    Assert.That(
                        (int)sim.PickupGrades[index],
                        Is.InRange(0, LootGradeSpec.GradeCount - 1),
                        $"tick {tick} pickup[{index}] grade out of table");
                }
                observedPickups += sim.Pickups.Count;
            }
            Assert.That(observedPickups, Is.GreaterThan(0), "the run never dropped anything — the assertion proved nothing");
        }

        // W5-9 결정론: 같은 config + 같은 입력 -> 원장·등급·점수 전부 동일.
        [Test]
        public void GradedLoot_On_SameInputsProduceIdenticalLedgers()
        {
            HackConfig config = Dungeon();
            var left = new CinderSim(in config, DungeonProgressionConfig.All);
            var right = new CinderSim(in config, DungeonProgressionConfig.All);

            for (int tick = 0; tick < 5400; tick += 1)
            {
                SimInput input = Script(tick);
                left.Tick(in input);
                right.Tick(in input);
                Assert.That(right.FinePity, Is.EqualTo(left.FinePity), $"tick {tick} fine pity");
                Assert.That(right.EpicPity, Is.EqualTo(left.EpicPity), $"tick {tick} epic pity");
                Assert.That(right.LastLootGrade, Is.EqualTo(left.LastLootGrade), $"tick {tick} last grade");
                Assert.That(right.Score, Is.EqualTo(left.Score), $"tick {tick} score");
                Assert.That(right.Relics, Is.EqualTo(left.Relics), $"tick {tick} relics");
                Assert.That(right.Player.Health, Is.EqualTo(left.Player.Health).Within(Tolerance), $"tick {tick} HP");
                Assert.That(right.Charge, Is.EqualTo(left.Charge).Within(Tolerance), $"tick {tick} charge");
            }
        }

        // W5-10 리스타트: 원장은 런 스코프다 — 재시작이 pity 를 은행에 넣지 않는다.
        [Test]
        public void PityLedger_IsRunScoped()
        {
            HackConfig config = Dungeon();
            var sim = new CinderSim(in config, DungeonProgressionConfig.All);
            for (int tick = 0; tick < 2400; tick += 1)
            {
                SimInput input = Script(tick);
                sim.Tick(in input);
            }
            Assert.That(sim.FinePity + sim.EpicPity, Is.GreaterThan(0), "the run never banked any pity — the assertion proved nothing");

            sim.Restart();
            Assert.That(sim.FinePity, Is.EqualTo(0), "restart must clear the fine ledger");
            Assert.That(sim.EpicPity, Is.EqualTo(0), "restart must clear the epic ledger");
            Assert.That(sim.LastLootGrade, Is.EqualTo(LootGrade.Basic), "restart must clear the published grade");
            Assert.That(sim.DifficultyBand, Is.EqualTo(0), "restart must clear the DDA band");

            // A restarted run must be lockstep-identical to a fresh one.
            var fresh = new CinderSim(in config, DungeonProgressionConfig.All);
            for (int tick = 0; tick < 1800; tick += 1)
            {
                SimInput input = Script(tick);
                sim.Tick(in input);
                fresh.Tick(in input);
                Assert.That(sim.Score, Is.EqualTo(fresh.Score), $"tick {tick} score");
                Assert.That(sim.FinePity, Is.EqualTo(fresh.FinePity), $"tick {tick} fine pity");
                Assert.That(sim.EpicPity, Is.EqualTo(fresh.EpicPity), $"tick {tick} epic pity");
                Assert.That(sim.Player.Health, Is.EqualTo(fresh.Player.Health).Within(Tolerance), $"tick {tick} HP");
            }
        }
    }
}
