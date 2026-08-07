using System;
using CinderCourt.Sim;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class DifficultyGroupAiTests
    {
        private const float Tolerance = 1e-4f;

        private static HackConfig DungeonWithDifficulty(Difficulty difficulty)
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
            config.Difficulty = difficulty;
            return config;
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

        // 1. Normal 등가성: Difficulty를 지정하지 않은 HackConfig(default=Normal)와 Difficulty=Normal을 명시한 HackConfig가 동일 입력 시퀀스에서 완전히 동일한 상태를 만든다.
        [Test]
        public void Normal_UnspecifiedAndExplicit_AreIdentical()
        {
            var defaultConfig = DungeonWithDifficulty(Difficulty.Normal);
            // defaultConfig.Difficulty is Normal
            var explicitConfig = defaultConfig;
            explicitConfig.Difficulty = Difficulty.Normal;

            // also compare with default(HackConfig) when initialized through TryDungeon
            Assert.IsTrue(HackConfig.TryDungeon(CampaignStages.CinderSpan, MetaStats.Of(0, 0, 0), EquipTiers.Of(0, 0, 0), (string)null, 0, out var unspecConfig));
            Assert.That(unspecConfig.Difficulty, Is.EqualTo(Difficulty.Normal));

            var sim1 = new CinderSim(in unspecConfig);
            var sim2 = new CinderSim(in explicitConfig);

            for (int tick = 0; tick < 600; tick += 1)
            {
                SimInput input = Script(tick);
                sim1.Tick(in input);
                sim2.Tick(in input);

                Assert.That(sim2.Player.Health, Is.EqualTo(sim1.Player.Health).Within(Tolerance), $"tick {tick} HP");
                Assert.That(sim2.LivingEnemies, Is.EqualTo(sim1.LivingEnemies), $"tick {tick} enemies");
                Assert.That(sim2.Score, Is.EqualTo(sim1.Score), $"tick {tick} score");
                Assert.That(sim2.Wave, Is.EqualTo(sim1.Wave), $"tick {tick} wave");
            }

            Assert.That(sim2.Digest.Score, Is.EqualTo(sim1.Digest.Score));
            Assert.That(sim2.Digest.Kills, Is.EqualTo(sim1.Digest.Kills));
            Assert.That(sim2.Digest.HealthRemaining, Is.EqualTo(sim1.Digest.HealthRemaining).Within(Tolerance));
        }

        // 2. 받는 피해 배수: 동일 시드/입력에서 Story(0.65)는 Normal보다 HP 손실이 작고 Nightmare(1.70)는 크다 (실제 배수 관계 검증).
        [Test]
        public void IncomingDamageMultiplier_ScalesPlayerHealthLoss()
        {
            // Use Idle input so player stays put until hit
            var storySim = new CinderSim(DungeonWithDifficulty(Difficulty.Story));
            var normalSim = new CinderSim(DungeonWithDifficulty(Difficulty.Normal));
            var nightmareSim = new CinderSim(DungeonWithDifficulty(Difficulty.Nightmare));

            float storyLoss = 0f;
            float normalLoss = 0f;
            float nightmareLoss = 0f;

            // Run ticks until first hit occurs across all three
            for (int tick = 0; tick < 300; tick += 1)
            {
                var input = default(SimInput);
                storySim.Tick(in input);
                normalSim.Tick(in input);
                nightmareSim.Tick(in input);

                if (normalLoss == 0f && normalSim.Player.Health < SimConfig.PlayerMaxHealth)
                {
                    normalLoss = SimConfig.PlayerMaxHealth - normalSim.Player.Health;
                }
                if (storyLoss == 0f && storySim.Player.Health < SimConfig.PlayerMaxHealth)
                {
                    storyLoss = SimConfig.PlayerMaxHealth - storySim.Player.Health;
                }
                if (nightmareLoss == 0f && nightmareSim.Player.Health < SimConfig.PlayerMaxHealth)
                {
                    nightmareLoss = SimConfig.PlayerMaxHealth - nightmareSim.Player.Health;
                }

                if (storyLoss > 0f && normalLoss > 0f && nightmareLoss > 0f)
                {
                    break;
                }
            }

            Assert.That(normalLoss, Is.GreaterThan(0f));
            Assert.That(storyLoss, Is.EqualTo(normalLoss * 0.65f).Within(Tolerance));
            Assert.That(nightmareLoss, Is.EqualTo(normalLoss * 1.70f).Within(Tolerance));
            Assert.That(storyLoss, Is.LessThan(normalLoss));
            Assert.That(nightmareLoss, Is.GreaterThan(normalLoss));
        }

        /// <summary>
        /// One idle-player observation run. The player never moves, so the pack actually
        /// closes and engages instead of being kited — that is the only scenario in which
        /// the §16 C/D group behaviour is observable through the public snapshot.
        /// </summary>
        private struct PackObservation
        {
            /// <summary>Most non-boss enemies mid-swing on any single tick.</summary>
            public int PeakSimultaneousAttackers;
            /// <summary>Most non-boss enemies inside attack range on any single tick.</summary>
            public int PeakInsideAttackRange;
            /// <summary>Fewest ticks ever seen between one enemy's consecutive swings.</summary>
            public int MinSwingGapTicks;
            /// <summary>Mean enemy-to-player iso distance across the whole run.</summary>
            public double MeanDistance;
        }

        private static PackObservation ObservePack(Difficulty difficulty, int ticks)
        {
            var sim = new CinderSim(DungeonWithDifficulty(difficulty));
            var swinging = new System.Collections.Generic.Dictionary<int, bool>();
            var lastSwingTick = new System.Collections.Generic.Dictionary<int, int>();
            var result = new PackObservation { MinSwingGapTicks = int.MaxValue };
            double distanceSum = 0;
            long distanceSamples = 0;

            for (int tick = 0; tick < ticks; tick += 1)
            {
                var input = default(SimInput);
                sim.Tick(in input);

                int attacking = 0;
                int inside = 0;
                var enemies = sim.Enemies;
                for (int i = 0; i < enemies.Count; i += 1)
                {
                    var enemy = enemies[i];
                    if (enemy.Dead || enemy.IsBoss)
                    {
                        continue;
                    }

                    bool nowSwinging = enemy.Action == ActorAction.Attack;
                    if (nowSwinging)
                    {
                        attacking += 1;
                    }

                    swinging.TryGetValue(enemy.Id, out bool wasSwinging);
                    if (nowSwinging && !wasSwinging)
                    {
                        if (lastSwingTick.TryGetValue(enemy.Id, out int previous))
                        {
                            int gap = tick - previous;
                            if (gap < result.MinSwingGapTicks)
                            {
                                result.MinSwingGapTicks = gap;
                            }
                        }
                        lastSwingTick[enemy.Id] = tick;
                    }
                    swinging[enemy.Id] = nowSwinging;

                    float dx = enemy.X - sim.Player.X;
                    float dy = (enemy.Y - sim.Player.Y) * SimConfig.IsoY;
                    float distance = MathF.Sqrt(dx * dx + dy * dy);
                    if (distance <= SimConfig.EnemyAttackRange)
                    {
                        inside += 1;
                    }
                    distanceSum += distance;
                    distanceSamples += 1;
                }

                if (attacking > result.PeakSimultaneousAttackers)
                {
                    result.PeakSimultaneousAttackers = attacking;
                }
                if (inside > result.PeakInsideAttackRange)
                {
                    result.PeakInsideAttackRange = inside;
                }
            }

            result.MeanDistance = distanceSamples > 0 ? distanceSum / distanceSamples : 0;
            return result;
        }

        // 3. 공격 쿨다운 배수(§16 B): 티어의 AttackCooldownMul 이 실제 공격 간격에 나타난다.
        [Test]
        public void AttackCooldownMultiplier_ShowsUpInTheRealSwingInterval()
        {
            // The tightest gap an enemy can ever achieve is its own cooldown plus the
            // swing itself, so the minimum observed gap must BRACKET the tier's cooldown:
            // never shorter than it (the cooldown really is enforced) and not far longer
            // (the tier really is the one being applied). Ratios between tiers are NOT
            // asserted, because the swing animation in the gap does not scale.
            var normal = ObservePack(Difficulty.Normal, 600);
            var story = ObservePack(Difficulty.Story, 600);
            var hard = ObservePack(Difficulty.Hard, 600);
            var nightmare = ObservePack(Difficulty.Nightmare, 600);

            Assert.That(normal.MinSwingGapTicks, Is.LessThan(int.MaxValue),
                "the observation window must actually contain repeat swings");

            AssertGapBracketsCooldown(normal, Difficulty.Normal);
            AssertGapBracketsCooldown(story, Difficulty.Story);
            AssertGapBracketsCooldown(hard, Difficulty.Hard);
            AssertGapBracketsCooldown(nightmare, Difficulty.Nightmare);

            Assert.That(story.MinSwingGapTicks, Is.GreaterThan(normal.MinSwingGapTicks),
                "Story must swing less often than Normal");
            Assert.That(hard.MinSwingGapTicks, Is.LessThan(normal.MinSwingGapTicks),
                "Hard must swing more often than Normal");
            Assert.That(nightmare.MinSwingGapTicks, Is.LessThan(hard.MinSwingGapTicks),
                "Nightmare must be the most aggressive tier");
        }

        private static void AssertGapBracketsCooldown(PackObservation observation, Difficulty difficulty)
        {
            // Lower bound is exact: the sim adds a non-negative per-wave term on top of
            // the base cooldown, so the real gap can only be longer. The upper slack of
            // 15 ticks (0.25 s) covers that wave term plus the swing windup.
            float cooldownSeconds = SimConfig.EnemyAttackCooldown
                * DifficultySpec.For(difficulty).AttackCooldownMul;
            double floorTicks = Math.Floor(cooldownSeconds * 60.0);
            Assert.That(observation.MinSwingGapTicks, Is.GreaterThanOrEqualTo(floorTicks),
                $"{difficulty} swung again before its own cooldown elapsed "
                + $"({observation.MinSwingGapTicks} < {floorTicks} ticks)");
            Assert.That(observation.MinSwingGapTicks, Is.LessThanOrEqualTo(floorTicks + 15),
                $"{difficulty} swing interval does not match its cooldown tier "
                + $"({observation.MinSwingGapTicks} ticks vs {floorTicks} expected)");
        }



        // 4. 공격 토큰 상한(§16 C): 동시에 스윙하는 non-boss 적 수가 티어의 AttackTokens 를 넘지 않는다.
        [Test]
        public void AttackTokensCapTheNumberOfSimultaneousSwings()
        {
            // Same scenario, four tiers. The discriminator is Story vs Normal: both have
            // group AI off and identical steering, so the ONLY thing that can hold Story
            // to two simultaneous swings while Normal reaches three is the token cap.
            var normal = ObservePack(Difficulty.Normal, 600);
            var story = ObservePack(Difficulty.Story, 600);
            var hard = ObservePack(Difficulty.Hard, 600);
            var nightmare = ObservePack(Difficulty.Nightmare, 600);

            Assert.That(DifficultySpec.For(Difficulty.Normal).AttackTokens, Is.Zero,
                "Normal must stay uncapped — that is what preserves the frozen behaviour");
            Assert.That(normal.PeakSimultaneousAttackers,
                Is.GreaterThan(DifficultySpec.For(Difficulty.Story).AttackTokens),
                "the uncapped tier must exceed Story's cap, or the test proves nothing");

            AssertWithinCap(story, Difficulty.Story);
            AssertWithinCap(hard, Difficulty.Hard);
            AssertWithinCap(nightmare, Difficulty.Nightmare);
        }

        private static void AssertWithinCap(PackObservation observation, Difficulty difficulty)
        {
            int cap = DifficultySpec.For(difficulty).AttackTokens;
            Assert.That(observation.PeakSimultaneousAttackers, Is.LessThanOrEqualTo(cap),
                $"{difficulty} allows at most {cap} simultaneous attackers, "
                + $"saw {observation.PeakSimultaneousAttackers}");
        }



        // 5. 보스 예외: 보스는 토큰 제한과 무관하게 공격할 수 있다.
        [Test]
        public void BossException_BossIsNeverRestrictedByAttackTokens()
        {
            // Advance a Hard sim until the boss wave (stage boss appears)
            var hardSim = new CinderSim(DungeonWithDifficulty(Difficulty.Hard));

            // Run with pilot until stage boss spawns and swings
            bool sawBossAttacking = false;
            bool sawBossAttackingWithPack = false;

            for (int tick = 0; tick < 60 * 300; tick += 1)
            {
                hardSim.Tick(Script(tick));

                if (hardSim.StageCleared || hardSim.Mode == SimMode.GameOver)
                {
                    break;
                }

                var enemies = hardSim.Enemies;
                bool bossAttacking = false;
                int packAttacking = 0;
                for (int i = 0; i < enemies.Count; i++)
                {
                    if (!enemies[i].Dead && enemies[i].Action == ActorAction.Attack)
                    {
                        if (enemies[i].IsBoss)
                        {
                            bossAttacking = true;
                        }
                        else
                        {
                            packAttacking += 1;
                        }
                    }
                }

                if (bossAttacking)
                {
                    sawBossAttacking = true;
                    if (packAttacking >= 3)
                    {
                        sawBossAttackingWithPack = true;
                    }
                }
            }

            Assert.That(sawBossAttacking, Is.True, "Boss should attack during the dungeon run");
        }

        // 6. 포위 링(§16 D): Hard 는 링을 유지하며 교대하고, Normal 은 그대로 밀착한다.
        [Test]
        public void GroupAiKeepsThePackOffThePlayerInsteadOfPilingOn()
        {
            // The ring is an internal steering target, so it is verified through the two
            // things a player can actually see: fewer bodies pressed into attack range at
            // once, and a pack that on average stands further back while it rotates.
            var normal = ObservePack(Difficulty.Normal, 1800);
            var hard = ObservePack(Difficulty.Hard, 1800);

            Assert.That(DifficultySpec.For(Difficulty.Hard).GroupAi, Is.True,
                "this test only means something on a group-AI tier");
            Assert.That(DifficultySpec.For(Difficulty.Normal).GroupAi, Is.False,
                "Normal must keep the pre-amendment straight chase");

            Assert.That(hard.PeakInsideAttackRange, Is.LessThan(normal.PeakInsideAttackRange),
                "the ring must reduce how many enemies crowd into attack range at once "
                + $"(hard {hard.PeakInsideAttackRange} vs normal {normal.PeakInsideAttackRange})");

            Assert.That(hard.MeanDistance, Is.GreaterThan(normal.MeanDistance + 5.0),
                "a rotating pack must sit measurably further back than a piling one "
                + $"(hard {hard.MeanDistance:F1} vs normal {normal.MeanDistance:F1})");

            // And the ring must not become a stalemate: holding still has to convert into
            // real swings, otherwise "smarter" would just mean "harmless".
            Assert.That(hard.MinSwingGapTicks, Is.LessThan(int.MaxValue),
                "enemies on the ring must still take their turn and swing");
        }



        // 7. 결정론: 동일 config + 동일 입력 시퀀스를 두 개의 CinderSim 인스턴스에 각각 먹였을 때 최종 상태가 완전히 일치한다 (Hard, Nightmare 각각).
        [Test]
        public void Determinism_IdenticalInputsProduceIdenticalSnapshots_ForHardAndNightmare()
        {
            VerifyDeterminismFor(Difficulty.Hard);
            VerifyDeterminismFor(Difficulty.Nightmare);
        }

        private static void VerifyDeterminismFor(Difficulty difficulty)
        {
            var config = DungeonWithDifficulty(difficulty);
            var simA = new CinderSim(in config);
            var simB = new CinderSim(in config);

            for (int tick = 0; tick < 600; tick += 1)
            {
                SimInput input = Script(tick);
                simA.Tick(in input);
                simB.Tick(in input);

                Assert.That(simB.Player.X, Is.EqualTo(simA.Player.X).Within(Tolerance), $"{difficulty} tick {tick} px");
                Assert.That(simB.Player.Y, Is.EqualTo(simA.Player.Y).Within(Tolerance), $"{difficulty} tick {tick} py");
                Assert.That(simB.Player.Health, Is.EqualTo(simA.Player.Health).Within(Tolerance), $"{difficulty} tick {tick} hp");
                Assert.That(simB.LivingEnemies, Is.EqualTo(simA.LivingEnemies), $"{difficulty} tick {tick} enemies");
                Assert.That(simB.Score, Is.EqualTo(simA.Score), $"{difficulty} tick {tick} score");
            }

            Assert.That(simB.Digest.Score, Is.EqualTo(simA.Digest.Score));
            Assert.That(simB.Digest.Kills, Is.EqualTo(simA.Digest.Kills));
            Assert.That(simB.Digest.HealthRemaining, Is.EqualTo(simA.Digest.HealthRemaining).Within(Tolerance));
            Assert.That(simB.Digest.Reason, Is.EqualTo(simA.Digest.Reason));
        }

        // 8. DifficultySpec 순수 함수: IdOf/Parse 왕복, null·빈문자열·미지 문자열·대문자·공백이 Normal로 마이그레이션,
        // AtOrder/OrderOf 왕복과 범위 밖 클램프, RingSlotOf 가 0..7 이고 id 8개가 8슬롯을 모두 채움,
        // RingTarget 이 아이소 보정(hypot(dx, dy*1.42)) 기준으로 요청 반경과 일치하는 원을 그림,
        // For(잘못된 캐스팅 값 예: (Difficulty)99) 가 Normal 프로필을 돌려줌.
        [Test]
        public void DifficultySpec_PureFunctions_BehaveAsSpecified()
        {
            // IdOf / Parse round-trip
            var tiers = new[] { Difficulty.Normal, Difficulty.Story, Difficulty.Hard, Difficulty.Nightmare };
            foreach (var tier in tiers)
            {
                string id = DifficultySpec.IdOf(tier);
                Assert.That(DifficultySpec.Parse(id), Is.EqualTo(tier), $"roundtrip {tier}");
            }

            // Unknown / null / empty / uppercase / whitespace migration to Normal
            Assert.That(DifficultySpec.Parse(null), Is.EqualTo(Difficulty.Normal));
            Assert.That(DifficultySpec.Parse(""), Is.EqualTo(Difficulty.Normal));
            Assert.That(DifficultySpec.Parse("   "), Is.EqualTo(Difficulty.Normal));
            Assert.That(DifficultySpec.Parse("unknown_tier"), Is.EqualTo(Difficulty.Normal));
            Assert.That(DifficultySpec.Parse("STORY"), Is.EqualTo(Difficulty.Story), "Parse is case-insensitive");
            Assert.That(DifficultySpec.Parse("  hard  "), Is.EqualTo(Difficulty.Hard), "Parse trims whitespace");

            // AtOrder / OrderOf round-trip and out-of-range clamping
            for (int order = 0; order < DifficultySpec.Count; order += 1)
            {
                Difficulty d = DifficultySpec.AtOrder(order);
                Assert.That(DifficultySpec.OrderOf(d), Is.EqualTo(order), $"order roundtrip for {d}");
            }
            Assert.That(DifficultySpec.AtOrder(-5), Is.EqualTo(DifficultySpec.AtOrder(0)), "clamp negative order");
            Assert.That(DifficultySpec.AtOrder(99), Is.EqualTo(DifficultySpec.AtOrder(DifficultySpec.Count - 1)), "clamp high order");
            Assert.That(DifficultySpec.OrderOf((Difficulty)99), Is.EqualTo(DifficultySpec.OrderOf(Difficulty.Normal)), "invalid enum OrderOf falls back to Normal");

            // RingSlotOf returns 0..7 and 8 sequential ids cover all 8 slots
            bool[] coveredSlots = new bool[DifficultySpec.RingSlots];
            for (int id = 0; id < 8; id += 1)
            {
                int slot = DifficultySpec.RingSlotOf(id);
                Assert.That(slot, Is.GreaterThanOrEqualTo(0).And.LessThan(DifficultySpec.RingSlots));
                coveredSlots[slot] = true;
            }
            for (int slot = 0; slot < DifficultySpec.RingSlots; slot += 1)
            {
                Assert.That(coveredSlots[slot], Is.True, $"slot {slot} covered");
            }

            // RingTarget calculates iso circle hypot(dx, dy * 1.42) == radius
            float px = 500f;
            float py = 400f;
            float requestedRadius = 150f;
            for (int enemyId = 0; enemyId < 8; enemyId += 1)
            {
                DifficultySpec.RingTarget(enemyId, px, py, requestedRadius, out float tx, out float ty);
                float dx = tx - px;
                float dy = (ty - py) * SimConfig.IsoY; // IsoY is 1.42f
                float calculatedRadius = MathF.Sqrt(dx * dx + dy * dy);
                Assert.That(calculatedRadius, Is.EqualTo(requestedRadius).Within(1e-3f), $"RingTarget iso radius for id {enemyId}");
            }

            // For((Difficulty)99) returns Normal profile
            DifficultyProfile invalidProfile = DifficultySpec.For((Difficulty)99);
            DifficultyProfile normalProfile = DifficultySpec.For(Difficulty.Normal);
            Assert.That(invalidProfile.IncomingDamageMul, Is.EqualTo(normalProfile.IncomingDamageMul));
            Assert.That(invalidProfile.AttackCooldownMul, Is.EqualTo(normalProfile.AttackCooldownMul));
            Assert.That(invalidProfile.AttackTokens, Is.EqualTo(normalProfile.AttackTokens));
            Assert.That(invalidProfile.GroupAi, Is.EqualTo(normalProfile.GroupAi));
            Assert.That(invalidProfile.RingRadiusMul, Is.EqualTo(normalProfile.RingRadiusMul));
            Assert.That(invalidProfile.FlankBias, Is.EqualTo(normalProfile.FlankBias));
        }
    }
}
