// AMENDMENT #15 (W-MV) — dungeon movement bounds.
// Numeric truth: docs/SIM_SPEC_HACKSLASH.md §19.
//
// The gate test comes first: default(DungeonBounds) resolves to the frozen
// SimConfig constants, so ClampToArena reads exactly what it read before and no
// golden digest can move. The rest pin the expanded geometry against the two
// things that actually constrain it — the painted backdrop plate and the frozen
// gimmick spans.
using CinderCourt.Sim;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class DungeonBoundsTests
    {
        private const float Tolerance = 1e-4f;

        // [OBSERVED] Assets/Editor/SceneBuilder.cs:126-127 — the painted court plate is
        // a SimWorld(1536) x SimWorld(1024) quad centred on sim (768, 512), i.e. it
        // spans sim x 0..1536 and y 0..1024.
        private const float PlateMinX = 0f;
        private const float PlateMaxX = 1536f;
        private const float PlateMinY = 0f;
        private const float PlateMaxY = 1024f;

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

        // Drives the player hard at every compass point in turn so the clamp is
        // actually exercised on all four extremes.
        private static SimInput Script(int tick)
        {
            var input = default(SimInput);
            int leg = tick / 900 % 8;
            input.MoveX = leg == 0 || leg == 1 || leg == 7 ? 1f : (leg == 3 || leg == 4 || leg == 5 ? -1f : 0f);
            input.MoveY = leg == 1 || leg == 2 || leg == 3 ? 1f : (leg == 5 || leg == 6 || leg == 7 ? -1f : 0f);
            input.AttackQueued = tick % 17 == 0;
            input.DashQueued = tick % 97 == 0;
            return input;
        }

        // W-MV-1 게이트: default(DungeonBounds) 는 동결 상수로 해석되고, 기존 생성자와
        // 완전 동치다. 이것이 골든 digest 무이동의 근거다.
        [Test]
        public void Bounds_Off_IsLockstepWithTheFrozenConstructor()
        {
            HackConfig config = Dungeon();
            var frozen = new CinderSim(in config);
            var gatedOff = new CinderSim(in config, default);

            // #13 and #14 must not drag #15 along: the movement amendment has a hard
            // View coupling (the boundary wall ring), so it is opted into separately.
            var wavesAndLootOnly = new CinderSim(in config, DungeonProgressionConfig.All);
            Assert.IsFalse(wavesAndLootOnly.ExpandedBoundsActive, "DungeonProgressionConfig.All must NOT expand bounds");
            Assert.That(wavesAndLootOnly.BoundsHalfWidth, Is.EqualTo(SimConfig.ArenaHalfWidth).Within(Tolerance));
            Assert.That(wavesAndLootOnly.BoundsHalfHeight, Is.EqualTo(SimConfig.ArenaHalfHeight).Within(Tolerance));

            Assert.IsFalse(gatedOff.ExpandedBoundsActive, "default(DungeonProgressionConfig) must be inert");
            Assert.That(gatedOff.BoundsHalfWidth, Is.EqualTo(SimConfig.ArenaHalfWidth).Within(Tolerance));
            Assert.That(gatedOff.BoundsHalfHeight, Is.EqualTo(SimConfig.ArenaHalfHeight).Within(Tolerance));

            for (int tick = 0; tick < 5400; tick += 1)
            {
                SimInput input = Script(tick);
                frozen.Tick(in input);
                gatedOff.Tick(in input);
                Assert.That(gatedOff.Player.X, Is.EqualTo(frozen.Player.X).Within(Tolerance), $"tick {tick} X");
                Assert.That(gatedOff.Player.Y, Is.EqualTo(frozen.Player.Y).Within(Tolerance), $"tick {tick} Y");
                Assert.That(gatedOff.Player.Health, Is.EqualTo(frozen.Player.Health).Within(Tolerance), $"tick {tick} HP");
                Assert.That(gatedOff.Enemies.Count, Is.EqualTo(frozen.Enemies.Count), $"tick {tick} enemy count");
                for (int index = 0; index < frozen.Enemies.Count; index += 1)
                {
                    Assert.That(gatedOff.Enemies[index].X, Is.EqualTo(frozen.Enemies[index].X).Within(Tolerance), $"tick {tick} enemy[{index}] X");
                    Assert.That(gatedOff.Enemies[index].Y, Is.EqualTo(frozen.Enemies[index].Y).Within(Tolerance), $"tick {tick} enemy[{index}] Y");
                }
            }
        }

        // W-MV-2 게이트 범위: 던전 전용. 아레나/프롤로그는 확장 bounds 를 넘겨도 무시한다.
        [Test]
        public void Bounds_ArenaAndPrologue_IgnoreTheExpandedBounds()
        {
            HackConfig arena = HackConfig.Arena();
            var arenaFrozen = new CinderSim(in arena);
            var arenaOptedIn = new CinderSim(in arena, DungeonProgressionConfig.Everything);
            Assert.IsFalse(arenaOptedIn.ExpandedBoundsActive, "arena must never expand");
            Assert.That(arenaOptedIn.BoundsHalfWidth, Is.EqualTo(SimConfig.ArenaHalfWidth).Within(Tolerance));

            HackConfig prologue = HackConfig.Prologue();
            var prologueFrozen = new CinderSim(in prologue);
            var prologueOptedIn = new CinderSim(in prologue, DungeonProgressionConfig.Everything);
            Assert.IsFalse(prologueOptedIn.ExpandedBoundsActive, "prologue must never expand");

            for (int tick = 0; tick < 2400; tick += 1)
            {
                SimInput input = Script(tick);
                arenaFrozen.Tick(in input);
                arenaOptedIn.Tick(in input);
                prologueFrozen.Tick(in input);
                prologueOptedIn.Tick(in input);
                Assert.That(arenaOptedIn.Player.X, Is.EqualTo(arenaFrozen.Player.X).Within(Tolerance), $"tick {tick} arena X");
                Assert.That(arenaOptedIn.Player.Y, Is.EqualTo(arenaFrozen.Player.Y).Within(Tolerance), $"tick {tick} arena Y");
                Assert.That(prologueOptedIn.Player.X, Is.EqualTo(prologueFrozen.Player.X).Within(Tolerance), $"tick {tick} prologue X");
                Assert.That(prologueOptedIn.Player.Y, Is.EqualTo(prologueFrozen.Player.Y).Within(Tolerance), $"tick {tick} prologue Y");
            }
        }

        // W-MV-3 해석기: 비활성/부분설정은 동결값, 축소 시도는 동결값으로 클램프.
        [Test]
        public void Resolve_FallsBackToFrozenAndNeverShrinks()
        {
            DungeonBoundsSpec.Resolve(default, out float halfW, out float halfH);
            Assert.That(halfW, Is.EqualTo(SimConfig.ArenaHalfWidth).Within(Tolerance));
            Assert.That(halfH, Is.EqualTo(SimConfig.ArenaHalfHeight).Within(Tolerance));

            // Half-set structs are inert — one axis must not expand alone.
            DungeonBoundsSpec.Resolve(DungeonBounds.Of(900f, 0f), out halfW, out halfH);
            Assert.That(halfW, Is.EqualTo(SimConfig.ArenaHalfWidth).Within(Tolerance), "half-set struct must be inert");
            DungeonBoundsSpec.Resolve(DungeonBounds.Of(0f, 900f), out halfW, out halfH);
            Assert.That(halfH, Is.EqualTo(SimConfig.ArenaHalfHeight).Within(Tolerance), "half-set struct must be inert");

            // A shrink request is clamped back up — shrinking would strand hazards
            // and spawn points outside the playfield.
            DungeonBoundsSpec.Resolve(DungeonBounds.Of(100f, 50f), out halfW, out halfH);
            Assert.That(halfW, Is.EqualTo(SimConfig.ArenaHalfWidth).Within(Tolerance), "shrink must clamp up");
            Assert.That(halfH, Is.EqualTo(SimConfig.ArenaHalfHeight).Within(Tolerance), "shrink must clamp up");

            DungeonBoundsSpec.Resolve(DungeonBoundsSpec.Expanded, out halfW, out halfH);
            Assert.That(halfW, Is.EqualTo(DungeonBoundsSpec.ExpandedHalfWidth).Within(Tolerance));
            Assert.That(halfH, Is.EqualTo(DungeonBoundsSpec.ExpandedHalfHeight).Within(Tolerance));
        }

        // W-MV-4 확장 기하: 링(적 정지선)이 그려진 바닥 플레이트 안에 들어가고,
        // 플레이어 도달 범위가 동결 기믹 span(x 248..1288)을 벗어나지 않는다.
        [Test]
        public void ExpandedBounds_StayInsideThePlateAndInsideEveryGimmickSpan()
        {
            float halfW = DungeonBoundsSpec.ExpandedHalfWidth;
            float halfH = DungeonBoundsSpec.ExpandedHalfHeight;

            // Enemy ring (the visible boundary wall line) inside the painted plate.
            float ringHalfW = halfW - SimConfig.EnemyMarginClamp;
            float ringHalfH = halfH - SimConfig.EnemyMarginClamp * 0.5f;
            Assert.That(SimConfig.ArenaX - ringHalfW, Is.GreaterThanOrEqualTo(PlateMinX), "ring runs off the plate (left)");
            Assert.That(SimConfig.ArenaX + ringHalfW, Is.LessThanOrEqualTo(PlateMaxX), "ring runs off the plate (right)");
            Assert.That(SimConfig.ArenaY - ringHalfH, Is.GreaterThanOrEqualTo(PlateMinY), "ring runs off the plate (top)");
            Assert.That(SimConfig.ArenaY + ringHalfH, Is.LessThanOrEqualTo(PlateMaxY), "ring runs off the plate (bottom)");

            // Player reach inside the ash-wall sweep AND every tide-current band,
            // both of which span x 248..1288 around the frozen centre.
            float playerHalfW = halfW - SimConfig.PlayerMarginClamp;
            Assert.That(SimConfig.ArenaX - playerHalfW, Is.GreaterThanOrEqualTo(CampaignSpec.WallEdgeX),
                "player can stand left of the ash wall's start — the gimmick becomes avoidable");
            Assert.That(SimConfig.ArenaX + playerHalfW, Is.LessThanOrEqualTo(CampaignSpec.WallEdgeRightX),
                "player can stand right of the ash wall's start — the gimmick becomes avoidable");
            Assert.That(playerHalfW, Is.LessThanOrEqualTo(CampaignSpec.CurrentHalfW),
                "player can step outside every tide-current band");

            // The expansion has to actually be an expansion on both axes.
            Assert.That(halfW, Is.GreaterThan(SimConfig.ArenaHalfWidth));
            Assert.That(halfH, Is.GreaterThan(SimConfig.ArenaHalfHeight));

            // Every frozen spawn point still sits inside the expanded ellipse, so no
            // enemy can spawn out of bounds and get snapped on its first tick.
            for (int index = 0; index < SimConfig.SpawnPoints.Length; index += 1)
            {
                float[] point = SimConfig.SpawnPoints[index];
                float unitX = (point[0] - SimConfig.ArenaX) / (halfW - SimConfig.EnemyMarginClamp);
                float unitY = (point[1] - SimConfig.ArenaY) / (halfH - SimConfig.EnemyMarginClamp * 0.5f);
                Assert.That(
                    System.MathF.Sqrt(unitX * unitX + unitY * unitY),
                    Is.LessThanOrEqualTo(1f),
                    $"spawn point {index} ({point[0]},{point[1]}) falls outside the expanded ellipse");
            }
        }

        // W-MV-5 심 클램프: 확장 ON 이면 플레이어와 적 모두 확장 타원 안에 머무르고,
        // 실제로 동결 타원 밖까지 나간다(확장이 관측된다).
        [Test]
        public void ExpandedBounds_On_ClampHoldsAndTheExtraSpaceIsReachable()
        {
            HackConfig config = Dungeon();
            var sim = new CinderSim(in config, DungeonProgressionConfig.Everything);
            Assert.IsTrue(sim.ExpandedBoundsActive, "dungeon must arm #15 when opted in");
            Assert.That(sim.BoundsHalfWidth, Is.EqualTo(DungeonBoundsSpec.ExpandedHalfWidth).Within(Tolerance));
            Assert.That(sim.BoundsHalfHeight, Is.EqualTo(DungeonBoundsSpec.ExpandedHalfHeight).Within(Tolerance));

            float playerHalfW = sim.BoundsHalfWidth - SimConfig.PlayerMarginClamp;
            float playerHalfH = sim.BoundsHalfHeight - SimConfig.PlayerMarginClamp * 0.5f;
            float enemyHalfW = sim.BoundsHalfWidth - SimConfig.EnemyMarginClamp;
            float enemyHalfH = sim.BoundsHalfHeight - SimConfig.EnemyMarginClamp * 0.5f;

            float frozenPlayerHalfW = SimConfig.ArenaHalfWidth - SimConfig.PlayerMarginClamp;
            float frozenPlayerHalfH = SimConfig.ArenaHalfHeight - SimConfig.PlayerMarginClamp * 0.5f;
            bool leftFrozenEllipse = false;

            for (int tick = 0; tick < 7200; tick += 1)
            {
                SimInput input = Script(tick);
                sim.Tick(in input);

                float unitX = (sim.Player.X - SimConfig.ArenaX) / playerHalfW;
                float unitY = (sim.Player.Y - SimConfig.ArenaY) / playerHalfH;
                // The current/knockback primitives push before the clamp re-runs, so
                // allow a hair of slack rather than an exact 1.0.
                Assert.That(
                    System.MathF.Sqrt(unitX * unitX + unitY * unitY),
                    Is.LessThanOrEqualTo(1.02f),
                    $"tick {tick}: player escaped the expanded ellipse at ({sim.Player.X}, {sim.Player.Y})");

                float frozenUnitX = (sim.Player.X - SimConfig.ArenaX) / frozenPlayerHalfW;
                float frozenUnitY = (sim.Player.Y - SimConfig.ArenaY) / frozenPlayerHalfH;
                if (System.MathF.Sqrt(frozenUnitX * frozenUnitX + frozenUnitY * frozenUnitY) > 1.001f)
                {
                    leftFrozenEllipse = true;
                }

                for (int index = 0; index < sim.Enemies.Count; index += 1)
                {
                    EnemyState enemy = sim.Enemies[index];
                    float ex = (enemy.X - SimConfig.ArenaX) / enemyHalfW;
                    float ey = (enemy.Y - SimConfig.ArenaY) / enemyHalfH;
                    Assert.That(
                        System.MathF.Sqrt(ex * ex + ey * ey),
                        Is.LessThanOrEqualTo(1.02f),
                        $"tick {tick}: enemy {enemy.Id} escaped the expanded ellipse at ({enemy.X}, {enemy.Y})");
                }
            }

            Assert.IsTrue(
                leftFrozenEllipse,
                "the player never reached the new space — the expansion is not observable, so the assertions above proved nothing");
        }

        // W-MV-6 결정론: 같은 config + 같은 입력 -> 좌표까지 완전 동일.
        [Test]
        public void ExpandedBounds_On_SameInputsProduceIdenticalRuns()
        {
            HackConfig config = Dungeon();
            var left = new CinderSim(in config, DungeonProgressionConfig.Everything);
            var right = new CinderSim(in config, DungeonProgressionConfig.Everything);

            for (int tick = 0; tick < 5400; tick += 1)
            {
                SimInput input = Script(tick);
                left.Tick(in input);
                right.Tick(in input);
                Assert.That(right.Player.X, Is.EqualTo(left.Player.X).Within(Tolerance), $"tick {tick} X");
                Assert.That(right.Player.Y, Is.EqualTo(left.Player.Y).Within(Tolerance), $"tick {tick} Y");
                Assert.That(right.Player.Health, Is.EqualTo(left.Player.Health).Within(Tolerance), $"tick {tick} HP");
                Assert.That(right.Score, Is.EqualTo(left.Score), $"tick {tick} score");
                Assert.That(right.Wave, Is.EqualTo(left.Wave), $"tick {tick} wave");
                Assert.That(right.Enemies.Count, Is.EqualTo(left.Enemies.Count), $"tick {tick} enemy count");
                for (int index = 0; index < left.Enemies.Count; index += 1)
                {
                    Assert.That(right.Enemies[index].X, Is.EqualTo(left.Enemies[index].X).Within(Tolerance), $"tick {tick} enemy[{index}] X");
                    Assert.That(right.Enemies[index].Y, Is.EqualTo(left.Enemies[index].Y).Within(Tolerance), $"tick {tick} enemy[{index}] Y");
                }
            }
        }

        // W-MV-7 뷰 계약: 심이 게시하는 stop-e 가 EnvironmentBuilder 의 링 파생식과
        // 같은 값을 낸다 — 확장 시 링이 클램프를 따라오게 하는 유일한 접점.
        [Test]
        public void StopE_MatchesTheRingDerivation()
        {
            // Frozen bounds must reproduce EnvironmentBuilder's current constants
            // exactly, otherwise turning #15 off would move the ring.
            Assert.That(
                DungeonBoundsSpec.EnemyStopE(SimConfig.ArenaHalfWidth),
                Is.EqualTo((SimConfig.ArenaHalfWidth - SimConfig.EnemyMarginClamp) / SimConfig.ArenaHalfWidth).Within(1e-6f));
            Assert.That(
                DungeonBoundsSpec.PlayerStopE(SimConfig.ArenaHalfWidth),
                Is.EqualTo((SimConfig.ArenaHalfWidth - SimConfig.PlayerMarginClamp) / SimConfig.ArenaHalfWidth).Within(1e-6f));

            // Expanded bounds push the stop line OUTWARD as a fraction, because the
            // margin is a fixed pixel standoff on a longer axis.
            Assert.That(
                DungeonBoundsSpec.EnemyStopE(DungeonBoundsSpec.ExpandedHalfWidth),
                Is.GreaterThan(DungeonBoundsSpec.EnemyStopE(SimConfig.ArenaHalfWidth)));
            Assert.That(DungeonBoundsSpec.EnemyStopE(DungeonBoundsSpec.ExpandedHalfWidth), Is.LessThan(1f));
            Assert.That(
                DungeonBoundsSpec.PlayerStopE(DungeonBoundsSpec.ExpandedHalfWidth),
                Is.LessThan(DungeonBoundsSpec.EnemyStopE(DungeonBoundsSpec.ExpandedHalfWidth)),
                "the player must always stop inside the enemy line");
        }

        // W-MV-8 런 스코프: bounds 는 런 중 변하지 않고 Restart 후에도 같다.
        [Test]
        public void Bounds_AreResolvedOnceAndSurviveRestart()
        {
            HackConfig config = Dungeon();
            var sim = new CinderSim(in config, DungeonProgressionConfig.Everything);
            float halfW = sim.BoundsHalfWidth;
            float halfH = sim.BoundsHalfHeight;

            for (int tick = 0; tick < 2400; tick += 1)
            {
                SimInput input = Script(tick);
                sim.Tick(in input);
                Assert.That(sim.BoundsHalfWidth, Is.EqualTo(halfW).Within(Tolerance), $"tick {tick}: bounds moved mid-run");
                Assert.That(sim.BoundsHalfHeight, Is.EqualTo(halfH).Within(Tolerance), $"tick {tick}: bounds moved mid-run");
            }

            sim.Restart();
            Assert.That(sim.BoundsHalfWidth, Is.EqualTo(halfW).Within(Tolerance), "restart must keep the configured bounds");
            Assert.That(sim.BoundsHalfHeight, Is.EqualTo(halfH).Within(Tolerance), "restart must keep the configured bounds");

            var fresh = new CinderSim(in config, DungeonProgressionConfig.Everything);
            for (int tick = 0; tick < 1800; tick += 1)
            {
                SimInput input = Script(tick);
                sim.Tick(in input);
                fresh.Tick(in input);
                Assert.That(sim.Player.X, Is.EqualTo(fresh.Player.X).Within(Tolerance), $"tick {tick} X");
                Assert.That(sim.Player.Y, Is.EqualTo(fresh.Player.Y).Within(Tolerance), $"tick {tick} Y");
            }
        }
    }
}
