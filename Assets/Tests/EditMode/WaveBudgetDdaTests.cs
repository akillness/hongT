// AMENDMENT #13 (W4) — point-budget waves + DDA.
// Numeric truth: docs/SIM_SPEC_HACKSLASH.md §17.
//
// The gate test comes first on purpose: every number this amendment introduces is
// unreachable unless the caller passes a DungeonProgressionConfig with
// AdaptiveWaves set, so the frozen goldens cannot move. The rest pin the budget
// curve, the band arithmetic and determinism.
using CinderCourt.Sim;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class WaveBudgetDdaTests
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
            input.MoveX = tick / 120 % 2 == 0 ? 1f : -1f;
            input.MoveY = tick / 200 % 2 == 0 ? 0.5f : -0.5f;
            input.AttackQueued = tick % 13 == 0;
            input.AttackHeld = tick % 13 == 0;
            input.NovaQueued = tick % 400 == 0;
            input.WardQueued = tick % 550 == 0;
            input.DashQueued = tick % 173 == 0;
            return input;
        }

        private static void AssertLockstep(CinderSim left, CinderSim right, int ticks)
        {
            for (int tick = 0; tick < ticks; tick += 1)
            {
                SimInput input = Script(tick);
                left.Tick(in input);
                right.Tick(in input);

                Assert.That(right.Player.Health, Is.EqualTo(left.Player.Health).Within(Tolerance), $"tick {tick} HP");
                Assert.That(right.Player.X, Is.EqualTo(left.Player.X).Within(Tolerance), $"tick {tick} X");
                Assert.That(right.Player.Y, Is.EqualTo(left.Player.Y).Within(Tolerance), $"tick {tick} Y");
                Assert.That(right.Wave, Is.EqualTo(left.Wave), $"tick {tick} wave");
                Assert.That(right.Score, Is.EqualTo(left.Score), $"tick {tick} score");
                Assert.That(right.Kills, Is.EqualTo(left.Kills), $"tick {tick} kills");
                Assert.That(right.Relics, Is.EqualTo(left.Relics), $"tick {tick} relics");
                Assert.That(right.Enemies.Count, Is.EqualTo(left.Enemies.Count), $"tick {tick} enemy count");
                for (int index = 0; index < left.Enemies.Count; index += 1)
                {
                    EnemyState expected = left.Enemies[index];
                    EnemyState actual = right.Enemies[index];
                    Assert.That(actual.Id, Is.EqualTo(expected.Id), $"tick {tick} enemy[{index}] id");
                    Assert.That(actual.Health, Is.EqualTo(expected.Health).Within(Tolerance), $"tick {tick} enemy[{index}] hp");
                    Assert.That(actual.MaxHealth, Is.EqualTo(expected.MaxHealth).Within(Tolerance), $"tick {tick} enemy[{index}] maxhp");
                    Assert.That(actual.X, Is.EqualTo(expected.X).Within(Tolerance), $"tick {tick} enemy[{index}] x");
                    Assert.That(actual.Action, Is.EqualTo(expected.Action), $"tick {tick} enemy[{index}] action");
                }
            }
        }

        // W4-1 게이트: 프로그레션 미지정(기존 생성자)과 default(DungeonProgressionConfig)는
        // 완전 동치다. 이것이 골든 다이제스트가 재-bless 없이 통과하는 근거다.
        [Test]
        public void AdaptiveWaves_Off_IsLockstepWithTheFrozenConstructor()
        {
            HackConfig config = Dungeon();
            var frozen = new CinderSim(in config);
            var gatedOff = new CinderSim(in config, default);

            Assert.IsFalse(gatedOff.AdaptiveWavesActive, "default(DungeonProgressionConfig) must be inert");
            AssertLockstep(frozen, gatedOff, 3600);
        }

        // W4-2 게이트 범위: 던전 전용. 아레나/프롤로그는 progression을 넘겨도 무시한다(D3).
        [Test]
        public void AdaptiveWaves_ArenaAndPrologue_IgnoreTheProgressionConfig()
        {
            HackConfig arena = HackConfig.Arena();
            var arenaFrozen = new CinderSim(in arena);
            var arenaOptedIn = new CinderSim(in arena, DungeonProgressionConfig.All);
            Assert.IsFalse(arenaOptedIn.AdaptiveWavesActive, "arena must never arm #13");
            Assert.IsFalse(arenaOptedIn.GradedLootActive, "arena must never arm #14");
            AssertLockstep(arenaFrozen, arenaOptedIn, 1800);

            HackConfig prologue = HackConfig.Prologue();
            var prologueFrozen = new CinderSim(in prologue);
            var prologueOptedIn = new CinderSim(in prologue, DungeonProgressionConfig.All);
            Assert.IsFalse(prologueOptedIn.AdaptiveWavesActive, "prologue must never arm #13");
            AssertLockstep(prologueFrozen, prologueOptedIn, 1800);
        }

        // W4-3 예산 곡선: 단조 증가하고 BudgetCap 에서 멈춘다.
        [Test]
        public void BaseBudget_IsMonotoneAndSaturatesAtTheCap()
        {
            Assert.That(WaveBudgetSpec.BaseBudget(1), Is.EqualTo(WaveBudgetSpec.BudgetBase));
            Assert.That(WaveBudgetSpec.BaseBudget(0), Is.EqualTo(WaveBudgetSpec.BudgetBase), "wave<1 clamps to the base");

            int previous = WaveBudgetSpec.BaseBudget(1);
            for (int wave = 2; wave <= 60; wave += 1)
            {
                int budget = WaveBudgetSpec.BaseBudget(wave);
                Assert.That(budget, Is.GreaterThanOrEqualTo(previous), $"wave {wave} budget went backwards");
                Assert.That(budget, Is.LessThanOrEqualTo(WaveBudgetSpec.BudgetCap), $"wave {wave} exceeded the cap");
                previous = budget;
            }
            Assert.That(WaveBudgetSpec.BaseBudget(21), Is.EqualTo(WaveBudgetSpec.BudgetCap), "cap is reached at wave 21");
            Assert.That(WaveBudgetSpec.BaseBudget(2), Is.EqualTo(126));
            Assert.That(WaveBudgetSpec.BaseBudget(10), Is.EqualTo(334));
        }

        // W4-4 예산 소비: 몸값 먼저, 남은 예산이 체력. 핀 고정 + 단조성.
        [Test]
        public void Budget_BuysBodiesFirstThenHitPoints()
        {
            Assert.That(WaveBudgetSpec.SpawnCountForBudget(100), Is.EqualTo(6));
            Assert.That(WaveBudgetSpec.SpawnCountForBudget(204), Is.EqualTo(12));
            Assert.That(WaveBudgetSpec.SpawnCountForBudget(600), Is.EqualTo(WaveBudgetSpec.MaxSpawns));
            Assert.That(WaveBudgetSpec.SpawnCountForBudget(0), Is.EqualTo(WaveBudgetSpec.MinSpawns), "floor holds");

            // Under a full roster the enemies keep the frozen 86 HP base exactly.
            Assert.That(WaveBudgetSpec.HealthMultiplierForBudget(204), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(WaveBudgetSpec.HealthMultiplierForBudget(WaveBudgetSpec.FullRosterSpend), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(WaveBudgetSpec.HealthMultiplierForBudget(334), Is.EqualTo(1.4910714f).Within(1e-3f));
            Assert.That(WaveBudgetSpec.HealthMultiplierForBudget(600), Is.EqualTo(2.6785715f).Within(1e-3f));
            Assert.That(
                WaveBudgetSpec.HealthMultiplierForBudget(int.MaxValue / 2),
                Is.EqualTo(1f + WaveBudgetSpec.HealthSurplusCap).Within(Tolerance),
                "surplus bonus is capped");

            float previous = 0f;
            int previousCount = 0;
            for (int wave = 1; wave <= 60; wave += 1)
            {
                int budget = WaveBudgetSpec.EffectiveBudget(wave, 0);
                float health = WaveBudgetSpec.HealthMultiplierForBudget(budget);
                int count = WaveBudgetSpec.SpawnCountForBudget(budget);
                Assert.That(health, Is.GreaterThanOrEqualTo(previous), $"wave {wave} enemy HP went backwards");
                Assert.That(count, Is.GreaterThanOrEqualTo(previousCount), $"wave {wave} spawn count went backwards");
                previous = health;
                previousCount = count;
            }
        }

        // W4-5 정예 배정: 예산에 비례해 늘고 상한에서 멈춘다.
        [Test]
        public void EliteAllowance_GrowsWithBudgetAndCaps()
        {
            Assert.That(WaveBudgetSpec.EliteAllowanceForBudget(100), Is.EqualTo(0));
            Assert.That(WaveBudgetSpec.EliteAllowanceForBudget(152), Is.EqualTo(1));
            Assert.That(WaveBudgetSpec.EliteAllowanceForBudget(334), Is.EqualTo(2));
            Assert.That(WaveBudgetSpec.EliteAllowanceForBudget(600), Is.EqualTo(WaveBudgetSpec.EliteAllowanceCap));
            Assert.That(
                WaveBudgetSpec.EliteAllowanceForBudget(int.MaxValue),
                Is.EqualTo(WaveBudgetSpec.EliteAllowanceCap),
                "allowance is capped");
        }

        // W4-6 밴드: 웨이브당 최대 1단 이동, ±2 클램프, 신호 3종 각각이 읽힌다.
        [Test]
        public void DifficultyBand_MovesOneStepPerWaveAndClamps()
        {
            // Every signal positive -> +3 raw, clamped to a single step.
            Assert.That(WaveBudgetSpec.PerformanceDelta(1f, 5f, 0), Is.EqualTo(3));
            Assert.That(WaveBudgetSpec.NextBand(0, 1f, 5f, 0), Is.EqualTo(1));
            // Every signal negative -> -3 raw, clamped to a single step.
            Assert.That(WaveBudgetSpec.PerformanceDelta(0.1f, 90f, 30), Is.EqualTo(-3));
            Assert.That(WaveBudgetSpec.NextBand(0, 0.1f, 90f, 30), Is.EqualTo(-1));
            // Mid-band on every axis -> no movement.
            Assert.That(WaveBudgetSpec.PerformanceDelta(0.5f, 30f, 5), Is.EqualTo(0));
            Assert.That(WaveBudgetSpec.NextBand(1, 0.5f, 30f, 5), Is.EqualTo(1));

            // Each signal in isolation.
            Assert.That(WaveBudgetSpec.PerformanceDelta(WaveBudgetSpec.HealthyFraction, 30f, 5), Is.EqualTo(1), "health signal");
            Assert.That(WaveBudgetSpec.PerformanceDelta(0.5f, WaveBudgetSpec.FastWaveSeconds, 5), Is.EqualTo(1), "clock signal");
            Assert.That(WaveBudgetSpec.PerformanceDelta(0.5f, 30f, WaveBudgetSpec.CleanHits), Is.EqualTo(1), "hits signal");
            Assert.That(WaveBudgetSpec.PerformanceDelta(0.2f, 30f, 5), Is.EqualTo(-1), "health signal (down)");
            Assert.That(WaveBudgetSpec.PerformanceDelta(0.5f, WaveBudgetSpec.SlowWaveSeconds, 5), Is.EqualTo(-1), "clock signal (down)");
            Assert.That(WaveBudgetSpec.PerformanceDelta(0.5f, 30f, WaveBudgetSpec.BatteredHits), Is.EqualTo(-1), "hits signal (down)");

            // Clamps hold at both ends no matter how long the streak runs.
            int band = 0;
            for (int wave = 0; wave < 12; wave += 1)
            {
                band = WaveBudgetSpec.NextBand(band, 1f, 5f, 0);
            }
            Assert.That(band, Is.EqualTo(WaveBudgetSpec.BandMax));
            for (int wave = 0; wave < 12; wave += 1)
            {
                band = WaveBudgetSpec.NextBand(band, 0.1f, 90f, 30);
            }
            Assert.That(band, Is.EqualTo(WaveBudgetSpec.BandMin));
        }

        // W4-7 밴드→예산: 밴드가 오르면 예산도 (약하게) 단조 증가한다.
        [Test]
        public void Band_ScalesTheBudgetMonotonically()
        {
            int previous = 0;
            for (int band = WaveBudgetSpec.BandMin; band <= WaveBudgetSpec.BandMax; band += 1)
            {
                int budget = WaveBudgetSpec.EffectiveBudget(10, band);
                Assert.That(budget, Is.GreaterThan(previous), $"band {band} did not raise the budget");
                previous = budget;
            }
            Assert.That(WaveBudgetSpec.EffectiveBudget(10, 0), Is.EqualTo(334), "band 0 is the un-scaled budget");
            Assert.That(WaveBudgetSpec.EffectiveBudget(10, WaveBudgetSpec.BandMin), Is.EqualTo(260));
            Assert.That(WaveBudgetSpec.EffectiveBudget(10, WaveBudgetSpec.BandMax), Is.EqualTo(417));
            // Out-of-range bands clamp instead of throwing.
            Assert.That(WaveBudgetSpec.EffectiveBudget(10, -99), Is.EqualTo(WaveBudgetSpec.EffectiveBudget(10, WaveBudgetSpec.BandMin)));
            Assert.That(WaveBudgetSpec.EffectiveBudget(10, 99), Is.EqualTo(WaveBudgetSpec.EffectiveBudget(10, WaveBudgetSpec.BandMax)));
        }

        // W4-8 결정론: 같은 config + 같은 입력 시퀀스 -> 밴드·예산·상태 전부 동일.
        [Test]
        public void AdaptiveWaves_On_SameInputsProduceIdenticalRuns()
        {
            HackConfig config = Dungeon();
            var left = new CinderSim(in config, DungeonProgressionConfig.All);
            var right = new CinderSim(in config, DungeonProgressionConfig.All);
            Assert.IsTrue(left.AdaptiveWavesActive, "dungeon must arm #13 when opted in");

            for (int tick = 0; tick < 5400; tick += 1)
            {
                SimInput input = Script(tick);
                left.Tick(in input);
                right.Tick(in input);
                Assert.That(right.DifficultyBand, Is.EqualTo(left.DifficultyBand), $"tick {tick} band");
                Assert.That(right.WaveBudget, Is.EqualTo(left.WaveBudget), $"tick {tick} budget");
                Assert.That(right.WaveEliteAllowance, Is.EqualTo(left.WaveEliteAllowance), $"tick {tick} elite allowance");
                Assert.That(right.WaveHitsTaken, Is.EqualTo(left.WaveHitsTaken), $"tick {tick} hits");
            }
            AssertLockstep(left, right, 600);
        }

        // W4-9 심 연동: 살아있는 웨이브의 예산·정예 배정이 순수 함수 결과와 일치하고,
        // 밴드는 항상 합법 범위 안이며, 스폰 수는 예산이 정한 수를 넘지 않는다.
        [Test]
        public void AdaptiveWaves_On_SimAgreesWithThePureBudgetArithmetic()
        {
            HackConfig config = Dungeon();
            var sim = new CinderSim(in config, DungeonProgressionConfig.All);
            int observedWaves = 0;
            int lastWave = 0;

            for (int tick = 0; tick < 7200; tick += 1)
            {
                SimInput input = Script(tick);
                sim.Tick(in input);

                Assert.That(sim.DifficultyBand, Is.InRange(WaveBudgetSpec.BandMin, WaveBudgetSpec.BandMax), $"tick {tick} band out of range");
                Assert.That(sim.WaveBudget, Is.GreaterThan(0), $"tick {tick} budget was never published");
                Assert.That(
                    sim.WaveEliteAllowance,
                    Is.LessThanOrEqualTo(WaveBudgetSpec.EliteAllowanceCap),
                    $"tick {tick} elite allowance over cap");

                if (sim.Wave != lastWave)
                {
                    lastWave = sim.Wave;
                    observedWaves += 1;
                    Assert.That(
                        sim.WaveBudget,
                        Is.EqualTo(WaveBudgetSpec.EffectiveBudget(sim.Wave, sim.DifficultyBand)),
                        $"wave {sim.Wave} budget disagrees with WaveBudgetSpec");
                }
                Assert.That(
                    sim.Enemies.Count,
                    Is.LessThanOrEqualTo(SimConfig.EnemyCap),
                    $"tick {tick} enemy cap breached");
            }
            Assert.That(observedWaves, Is.GreaterThanOrEqualTo(2), "the run never reached a second wave");
        }

        // W4-10 신호 정합성: WaveHitsTaken 은 실제로 체력이 깎인 틱에서만 오른다
        // (부록 B 의 "실제 깎인 피격" 정의와 동일).
        [Test]
        public void WaveHitsTaken_CountsOnlyDamageThatCostHealth()
        {
            HackConfig config = Dungeon();
            var sim = new CinderSim(in config, DungeonProgressionConfig.All);
            int previousHits = 0;
            float previousHealth = sim.Player.Health;
            int previousWave = sim.Wave;
            int increments = 0;

            for (int tick = 0; tick < 5400; tick += 1)
            {
                SimInput input = Script(tick);
                sim.Tick(in input);

                if (sim.Wave != previousWave)
                {
                    // A new wave resets the accumulator by contract.
                    previousWave = sim.Wave;
                    previousHits = sim.WaveHitsTaken;
                    previousHealth = sim.Player.Health;
                    continue;
                }

                if (sim.WaveHitsTaken > previousHits)
                {
                    increments += 1;
                    Assert.That(
                        sim.Player.Health,
                        Is.LessThan(previousHealth),
                        $"tick {tick}: hit counted without losing health");
                }
                previousHits = sim.WaveHitsTaken;
                previousHealth = sim.Player.Health;
            }
            Assert.That(increments, Is.GreaterThan(0), "the scripted run never took a hit — the assertion proved nothing");
        }
    }
}
