// Campaign amendment gates (docs/SIM_SPEC_CAMPAIGN.md).
// Arena regression is owned by CinderSimTests.cs (20 tests) — untouched.
using System;
using CinderCourt.Sim;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    public sealed class CampaignSimTests
    {
        static SimInput Idle => default;

        static CampaignConfig Stage(string id, int weapon = 0, int lantern = 0, int cloak = 0)
        {
            Assert.IsTrue(CampaignStages.TryGet(id, weapon, lantern, cloak, out var config),
                $"unknown stage {id}");
            return config;
        }

        static void Run(CinderSim sim, int ticks, in SimInput input)
        {
            for (var t = 0; t < ticks; t++) sim.Tick(in input);
        }

        // --- factory / config -------------------------------------------------

        [Test]
        public void StageTable_MatchesAmendment()
        {
            var stage1 = Stage(CampaignStages.CinderSpan);
            Assert.AreEqual(5, stage1.Waves);
            Assert.AreEqual(EnemyVisual.BossCommander, stage1.BossVisual);
            Assert.AreEqual(2, stage1.Hazards.Length);

            var stage2 = Stage(CampaignStages.AbyssChancel);
            Assert.AreEqual(6, stage2.Waves);
            Assert.AreEqual(4, stage2.Hazards.Length);

            var stage3 = Stage(CampaignStages.EchoThrone);
            Assert.AreEqual(7, stage3.Waves);
            Assert.AreEqual(EnemyVisual.BossMonarch, stage3.BossVisual);
            Assert.AreEqual(3, stage3.Hazards.Length);

            Assert.IsFalse(CampaignStages.TryGet("no-such-stage", 0, 0, 0, out _));
        }

        [Test]
        public void EquipmentFormulas_ApplyAtRunStart()
        {
            var config = Stage(CampaignStages.CinderSpan, weapon: 3, lantern: 2, cloak: 5);
            Assert.AreEqual(58f * 1.18f, config.PlayerDamage, 1e-3f);
            Assert.AreEqual(7f * 1.16f, config.LanternRegenPerSecond, 1e-3f);
            Assert.AreEqual(140f, config.PlayerMaxHealth, 1e-3f);

            var sim = new CinderSim(in config);
            Assert.AreEqual(140f, sim.Player.Health, 1e-3f, "cloak HP applies at spawn");

            // Ranks are clamped to 5.
            var over = Stage(CampaignStages.CinderSpan, weapon: 99, lantern: -1, cloak: 7);
            Assert.AreEqual(5, over.WeaponRank);
            Assert.AreEqual(0, over.LanternRank);
            Assert.AreEqual(5, over.CloakRank);
        }

        // --- boss wave --------------------------------------------------------

        [Test]
        public void BossWave_Composition_BossPlusEscorts()
        {
            // Immortal-enough setup: rank 5 cloak, and we only inspect spawns.
            var config = Stage(CampaignStages.CinderSpan, cloak: 5);
            var sim = new CinderSim(in config);
            var input = new SimInput { AttackQueued = true, NovaQueued = true, WardQueued = true };

            var sawBossWave = false;
            for (var t = 0; t < 60 * 300 && sim.Mode != SimMode.GameOver; t++)
            {
                sim.Tick(in input);
                if (sim.Wave == config.Waves + 1 && sim.BossAlive)
                {
                    sawBossWave = true;
                    break;
                }
            }
            Assert.IsTrue(sawBossWave, "boss wave never started");

            // Let the full boss-wave queue spawn, then census bosses/escorts.
            for (var t = 0; t < 60 * 10; t++) { sim.Tick(in input); if (sim.PendingSpawns == 0) break; }
            var bosses = 0;
            var escorts = 0;
            for (var i = 0; i < sim.Enemies.Count; i++)
            {
                var enemy = sim.Enemies[i];
                if (enemy.IsBoss) bosses++;
                else escorts++;
            }
            Assert.AreEqual(1, bosses, "exactly one stage boss");
            // Escorts: min(8, 3 + stageIndex*2) = 3 for stage index 0. Some may
            // already be dead from the bot's swings — census counts spawned-alive
            // plus fading, so allow <= but require at least 1 surviving record.
            Assert.LessOrEqual(escorts, 3);
            Assert.GreaterOrEqual(escorts, 1);
        }

        // --- full clear (kiting bot) -------------------------------------------

        [Test]
        public void CinderSpan_KitingBot_ClearsStage()
        {
            var config = Stage(CampaignStages.CinderSpan, 5, 5, 5);
            var sim = new CinderSim(in config);
            var cleared = false;
            for (var t = 0; t < 60 * 300; t++)
            {
                sim.Tick(BotInput(sim));
                if ((sim.Events & SimEvents.StageCleared) != 0) { cleared = true; break; }
                if (sim.Mode == SimMode.GameOver) break;
            }
            Assert.IsTrue(cleared, "kiting bot must clear stage 1 at rank 5/5/5");
            Assert.AreEqual("stage-clear", sim.Digest.Reason);
            Assert.IsTrue(((ICampaignSnapshot)sim).StageCleared);
        }

        // Shared kiter bot — DungeonGoldenDigestTests reuses this exact body (do not fork).
        internal static SimInput BotInput(CinderSim sim)
        {
            float px = sim.Player.X, py = sim.Player.Y;
            float bestD2 = float.MaxValue, dx = 0f, dy = 0f;
            var enemies = sim.Enemies;
            for (var i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy.Dead) continue;
                float ex = enemy.X - px, ey = (enemy.Y - py) * SimConfig.IsoY;
                var d2 = ex * ex + ey * ey;
                if (d2 < bestD2) { bestD2 = d2; dx = enemy.X - px; dy = enemy.Y - py; }
            }
            var input = new SimInput { AttackQueued = true, NovaQueued = true, WardQueued = true };
            if (bestD2 < float.MaxValue)
            {
                var d = MathF.Sqrt(bestD2);
                var len = MathF.Max(0.001f, MathF.Sqrt(dx * dx + dy * dy));
                if (d < 120f) { input.MoveX = -dx / len; input.MoveY = -dy / len; }
                else if (d > 150f) { input.MoveX = dx / len; input.MoveY = dy / len; }
            }
            return input;
        }

        // --- equipment drops ----------------------------------------------------

        [Test]
        public void EquipShard_DropsFromEnemyIdModulo()
        {
            // Wave 1 spawns ids 1..4; id 3 satisfies id % 7 == 3.
            var config = Stage(CampaignStages.CinderSpan, cloak: 5);
            var sim = new CinderSim(in config);
            var input = new SimInput { AttackQueued = true, NovaQueued = true, WardQueued = true };

            var sawEquipShard = false;
            for (var t = 0; t < 60 * 90 && sim.Wave == 1; t++)
            {
                sim.Tick(in input);
                for (var i = 0; i < sim.Pickups.Count; i++)
                    if (sim.Pickups[i].Kind == PickupKind.EquipShard) sawEquipShard = true;
                if (sawEquipShard) break;
            }
            Assert.IsTrue(sawEquipShard, "enemy id 3 must drop an equip shard in wave 1");
        }

        [Test]
        public void StageClear_GrantsBossDrop_SlotByStageIndex()
        {
            // Stage index 0 -> slot 0 (weapon). Start at 0 ranks via config but
            // rank-5 cloak so the bot survives: use cloak=5 (slot 2) so the boss
            // drop on slot 0 is unambiguous.
            var config = Stage(CampaignStages.CinderSpan, weapon: 0, lantern: 0, cloak: 5);
            var sim = new CinderSim(in config);
            var startWeapon = ((ICampaignSnapshot)sim).WeaponRank;
            for (var t = 0; t < 60 * 300; t++)
            {
                sim.Tick(BotInput(sim));
                if ((sim.Events & SimEvents.StageCleared) != 0) break;
                if (sim.Mode == SimMode.GameOver) Assert.Fail("bot died before clear");
            }
            Assert.Greater(((ICampaignSnapshot)sim).WeaponRank, startWeapon,
                "boss kill must raise the stageIndex%3 slot rank");
        }

        // --- hazards --------------------------------------------------------------

        [Test]
        public void EmberVent_DamagesInsideRadius_WardBlocks()
        {
            // Echo-throne vent at (500,700) phase 0. Teleporting isn't possible;
            // walk the player onto it (spawn 768,646 -> ~272px away, ~1.4 s).
            var config = Stage(CampaignStages.EchoThrone, cloak: 5);
            var simA = new CinderSim(in config);
            // Phase A: walk to the vent, then idle on it without ward.
            WalkOnto(simA, 500f, 700f, 60 * 4);
            var hpBefore = simA.Player.Health;
            var pulsedDamage = false;
            for (var t = 0; t < 60 * 3; t++)
            {
                simA.Tick(HoldPosition(simA, 500f, 700f));
                if ((simA.Events & SimEvents.HazardPulse) != 0 && simA.Player.Health < hpBefore)
                    pulsedDamage = true;
                hpBefore = Math.Min(hpBefore, simA.Player.Health);
            }
            Assert.IsTrue(pulsedDamage, "standing on a vent across a pulse must cost HP");
        }

        [Test]
        public void EmberVent_Pulse_WardNegatesDamageAndStartsGrace()
        {
            var config = Stage(CampaignStages.CinderSpan, cloak: 5);
            // Place the sole vent under the public campaign spawn position. Its phase
            // guarantees the second fixed step crosses exactly one pulse boundary.
            config.Hazards = new[]
            {
                HazardConfig.Vent(
                    SimConfig.ArenaX,
                    SimConfig.ArenaY + SimConfig.PlayerStartYOffset,
                    CampaignSpec.VentPeriod - SimConfig.FixedStep * 1.5f)
            };
            var sim = new CinderSim(in config);
            var healthBefore = sim.Player.Health;
            var ward = new SimInput { WardQueued = true };

            sim.Tick(in ward);
            Assert.IsTrue((sim.Events & SimEvents.WardCast) != 0, "ward must be active before the vent pulse");
            Assert.Greater(sim.Player.WardTime, 0f, "ward duration must remain active");

            sim.Tick(Idle);

            Assert.IsTrue((sim.Events & SimEvents.HazardPulse) != 0, "configured vent must still pulse");
            Assert.AreEqual(healthBefore, sim.Player.Health, 1e-3f, "active ward must negate the vent pulse");
            Assert.AreEqual(SimConfig.PlayerHitGrace, sim.Player.DamageCooldown, 1e-3f,
                "warded pulse must retain normal contact-grace semantics");
        }

        static void WalkOnto(CinderSim sim, float targetX, float targetY, int maxTicks)
        {
            for (var t = 0; t < maxTicks; t++)
            {
                var dx = targetX - sim.Player.X;
                var dy = targetY - sim.Player.Y;
                if (dx * dx + dy * dy < 20f * 20f) return;
                var len = MathF.Max(0.001f, MathF.Sqrt(dx * dx + dy * dy));
                var input = new SimInput { MoveX = dx / len, MoveY = dy / len };
                sim.Tick(in input);
            }
        }

        static SimInput HoldPosition(CinderSim sim, float targetX, float targetY)
        {
            var dx = targetX - sim.Player.X;
            var dy = targetY - sim.Player.Y;
            var input = default(SimInput);
            if (dx * dx + dy * dy > 12f * 12f)
            {
                var len = MathF.Max(0.001f, MathF.Sqrt(dx * dx + dy * dy));
                input.MoveX = dx / len;
                input.MoveY = dy / len;
            }
            return input;
        }

        [Test]
        public void ObsidianPillar_BlocksPlayer()
        {
            // Abyss-chancel pillar at arena center (768,604), radius 40 + player 26.
            var config = Stage(CampaignStages.AbyssChancel, cloak: 5);
            var sim = new CinderSim(in config);
            // Spawn (768,646) is south of the pillar; push straight into it.
            for (var t = 0; t < 60 * 3; t++)
            {
                var input = new SimInput { MoveY = -1f };  // up (screen-up = -y)
                sim.Tick(in input);
            }
            // Contract: ALL combat-space distances are iso-weighted (dy*1.42),
            // pillars included — the pushout ring is an ellipse in raw px space.
            var dx = sim.Player.X - 768f;
            var dy = (sim.Player.Y - 604f) * SimConfig.IsoY;
            var isoDistance = MathF.Sqrt(dx * dx + dy * dy);
            Assert.GreaterOrEqual(isoDistance, 66f - 0.5f,
                "player must stay outside pillar radius + player radius (iso metric)");
        }

        [Test]
        public void RelicAltar_BlessesAfterHold_WithCooldown()
        {
            var config = Stage(CampaignStages.EchoThrone, cloak: 5);
            var sim = new CinderSim(in config);
            // Altar at arena center (768,604); spawn is 42px south — instant reach.
            // Burn oil first so +18 is observable (ward costs 30).
            var wardInput = new SimInput { WardQueued = true };
            sim.Tick(in wardInput);
            Assert.Less(sim.Charge, 100f, "ward must burn oil");

            var blessings = 0;
            var lastCharge = sim.Charge;
            var jumped = false;
            for (var t = 0; t < 60 * 10; t++)
            {
                sim.Tick(HoldPosition(sim, 768f, 604f));
                if ((sim.Events & SimEvents.AltarBlessing) != 0)
                {
                    blessings++;
                    if (sim.Charge >= lastCharge + 10f) jumped = true;
                }
                lastCharge = sim.Charge;
            }
            Assert.GreaterOrEqual(blessings, 1, "standing on the altar must bless");
            Assert.IsTrue(jumped, "blessing must visibly add oil");
            Assert.LessOrEqual(blessings, 2, "6 s cooldown bounds blessings in 10 s");
        }

        // --- determinism -------------------------------------------------------------

        [Test]
        public void Campaign_Deterministic_SameConfigSameInputs()
        {
            var digestA = RunScripted();
            var digestB = RunScripted();
            Assert.AreEqual(digestA.Score, digestB.Score);
            Assert.AreEqual(digestA.Wave, digestB.Wave);
            Assert.AreEqual(digestA.Kills, digestB.Kills);
            Assert.AreEqual(digestA.Relics, digestB.Relics);
            Assert.AreEqual(digestA.HealthRemaining, digestB.HealthRemaining);
            Assert.AreEqual(digestA.Reason, digestB.Reason);
        }

        static RunDigest RunScripted()
        {
            var config = Stage(CampaignStages.AbyssChancel, 2, 1, 3);
            var sim = new CinderSim(in config);
            for (var t = 0; t < 1800; t++)
            {
                var input = default(SimInput);
                input.MoveX = ((t / 120) % 2 == 0) ? 1f : -1f;
                input.MoveY = ((t / 200) % 2 == 0) ? 0.5f : -0.5f;
                input.AttackQueued = t % 30 == 0;
                input.NovaQueued = t % 400 == 0;
                input.WardQueued = t % 550 == 0;
                sim.Tick(in input);
            }
            return sim.Digest;
        }

        // --- cycle-2 dungeon expansion (docs/SIM_SPEC_DUNGEONS.md, AMENDMENT #5,
        // REVISION v1.1 — Stage-2 retune "gimmicks must be unmissable";
        // numeric truth also: _workspace/current/design/gimmick-retune-spec.md) ---

        static HackConfig Dungeon213(string stageId)
        {
            Assert.IsTrue(
                HackConfig.TryDungeon(stageId, default, EquipTiers.Of(2, 1, 3), null, 0, out var config),
                $"unknown stage {stageId}");
            return config;
        }

        static void AssertSameDigest(RunDigest expected, RunDigest actual, string because)
        {
            Assert.AreEqual(expected.Score, actual.Score, because);
            Assert.AreEqual(expected.Wave, actual.Wave, because);
            Assert.AreEqual(expected.Kills, actual.Kills, because);
            Assert.AreEqual(expected.Relics, actual.Relics, because);
            Assert.AreEqual(expected.HealthRemaining, actual.HealthRemaining, because);
            Assert.AreEqual(expected.Reason, actual.Reason, because);
        }

        static HazardState FindHazard(CinderSim sim, HazardKind kind, float x, float y)
        {
            var hazards = ((ICampaignSnapshot)sim).Hazards;
            for (var i = 0; i < hazards.Count; i++)
                if (hazards[i].Kind == kind && hazards[i].X == x && hazards[i].Y == y) return hazards[i];
            Assert.Fail($"hazard {kind} at ({x},{y}) not published");
            return default;
        }

        static HazardState FindHazard(CinderSim sim, HazardKind kind)
        {
            var hazards = ((ICampaignSnapshot)sim).Hazards;
            for (var i = 0; i < hazards.Count; i++)
                if (hazards[i].Kind == kind) return hazards[i];
            Assert.Fail($"hazard {kind} not published");
            return default;
        }

        // Gate: R4/G2 — stage table + v1.1 placement tables per SIM_SPEC_DUNGEONS §Stages/§배치.
        [Test]
        public void StageTable_MatchesDungeonAmendment()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    CampaignStages.CinderSpan, CampaignStages.AbyssChancel, CampaignStages.EchoThrone,
                    CampaignStages.CinderSluice, CampaignStages.EmberBastion, CampaignStages.AshMarch,
                },
                (System.Collections.ICollection)CampaignStages.Ids,
                "anchor order is frozen (indices 0..5)");

            var waves = new[] { 5, 6, 7, 8, 8, 9 };
            var bosses = new[]
            {
                EnemyVisual.BossCommander, EnemyVisual.BossCommander, EnemyVisual.BossMonarch,
                EnemyVisual.BossCommander, EnemyVisual.BossCommander, EnemyVisual.BossMonarch,
            };
            for (var index = 0; index < 6; index++)
            {
                var config = CampaignStages.ForIndex(index, 0, 0, 0);
                Assert.AreEqual(waves[index], config.Waves, config.StageId);
                Assert.AreEqual(bosses[index], config.BossVisual, config.StageId);
                Assert.AreEqual(index, config.StageIndex, config.StageId);
            }

            // cinder-sluice v1.1: current(768,470,+200,0) · current(768,740,−200,3.0) ·
            // vent(500,604,0.9) · vent(1030,604,2.1) · pillar(768,604) — the two vents
            // bomb the only safe corridor (y 580..630) between the widened lanes.
            var sluice = Stage(CampaignStages.CinderSluice).Hazards;
            Assert.AreEqual(5, sluice.Length);
            Assert.AreEqual(HazardKind.TideCurrent, sluice[0].Kind);
            Assert.AreEqual(768f, sluice[0].X); Assert.AreEqual(470f, sluice[0].Y);
            Assert.AreEqual(CampaignSpec.CurrentPush, sluice[0].PushX);
            Assert.AreEqual(0f, sluice[0].PushY);
            Assert.AreEqual(0f, sluice[0].Phase);
            Assert.AreEqual(CampaignSpec.CurrentHalfW, sluice[0].HalfW);
            Assert.AreEqual(CampaignSpec.CurrentHalfH, sluice[0].HalfH);
            Assert.AreEqual(HazardKind.TideCurrent, sluice[1].Kind);
            Assert.AreEqual(768f, sluice[1].X); Assert.AreEqual(740f, sluice[1].Y);
            Assert.AreEqual(-CampaignSpec.CurrentPush, sluice[1].PushX);
            Assert.AreEqual(3f, sluice[1].Phase);
            Assert.AreEqual(HazardKind.EmberVent, sluice[2].Kind);
            Assert.AreEqual(500f, sluice[2].X); Assert.AreEqual(604f, sluice[2].Y);
            Assert.AreEqual(0.9f, sluice[2].Phase);
            Assert.AreEqual(CampaignSpec.VentRadius, sluice[2].Radius);
            Assert.AreEqual(HazardKind.EmberVent, sluice[3].Kind);
            Assert.AreEqual(1030f, sluice[3].X); Assert.AreEqual(604f, sluice[3].Y);
            Assert.AreEqual(2.1f, sluice[3].Phase);
            Assert.AreEqual(HazardKind.ObsidianPillar, sluice[4].Kind);
            Assert.AreEqual(768f, sluice[4].X); Assert.AreEqual(604f, sluice[4].Y);

            // ember-bastion v1.1: pylon(560,500) · pylon(980,700) · pylon(768,430) ·
            // pillar(640,650) · pillar(900,560) · vent(768,604,0.6) — the third pylon
            // closes the aura net over the spawn convergence point (768,604).
            var bastion = Stage(CampaignStages.EmberBastion).Hazards;
            Assert.AreEqual(6, bastion.Length);
            Assert.AreEqual(HazardKind.EmberPylon, bastion[0].Kind);
            Assert.AreEqual(560f, bastion[0].X); Assert.AreEqual(500f, bastion[0].Y);
            Assert.AreEqual(CampaignSpec.PylonHp, bastion[0].Hp);
            Assert.AreEqual(CampaignSpec.PylonBodyRadius, bastion[0].Radius);
            Assert.AreEqual(HazardKind.EmberPylon, bastion[1].Kind);
            Assert.AreEqual(980f, bastion[1].X); Assert.AreEqual(700f, bastion[1].Y);
            Assert.AreEqual(HazardKind.EmberPylon, bastion[2].Kind);
            Assert.AreEqual(768f, bastion[2].X); Assert.AreEqual(430f, bastion[2].Y);
            Assert.AreEqual(CampaignSpec.PylonHp, bastion[2].Hp);
            Assert.AreEqual(HazardKind.ObsidianPillar, bastion[3].Kind);
            Assert.AreEqual(640f, bastion[3].X); Assert.AreEqual(650f, bastion[3].Y);
            Assert.AreEqual(HazardKind.ObsidianPillar, bastion[4].Kind);
            Assert.AreEqual(900f, bastion[4].X); Assert.AreEqual(560f, bastion[4].Y);
            Assert.AreEqual(HazardKind.EmberVent, bastion[5].Kind);
            Assert.AreEqual(768f, bastion[5].X); Assert.AreEqual(604f, bastion[5].Y);
            Assert.AreEqual(0.6f, bastion[5].Phase);

            // ash-march v1.1: wall(left,0) · wall(right,11.5) · altar(768,604) ·
            // vent(560,760,0.6) · vent(980,450,1.8) — closing jaws at half-period
            // offset, altar moved to the centre as the corridor reward.
            var march = Stage(CampaignStages.AshMarch).Hazards;
            Assert.AreEqual(5, march.Length);
            Assert.AreEqual(HazardKind.AshWall, march[0].Kind);
            Assert.AreEqual(CampaignSpec.WallEdgeX, march[0].X);
            Assert.AreEqual(0f, march[0].Phase);
            Assert.AreEqual(1f, march[0].PushX, "PushX +1 = left-edge encoding (spec v1.1)");
            Assert.AreEqual(HazardKind.AshWall, march[1].Kind);
            Assert.AreEqual(CampaignSpec.WallEdgeRightX, march[1].X);
            Assert.AreEqual(11.5f, march[1].Phase, "right wall runs half a period out of phase");
            Assert.AreEqual(-1f, march[1].PushX, "PushX −1 = right-edge encoding (spec v1.1)");
            Assert.AreEqual(HazardKind.RelicAltar, march[2].Kind);
            Assert.AreEqual(768f, march[2].X); Assert.AreEqual(604f, march[2].Y);
            Assert.AreEqual(HazardKind.EmberVent, march[3].Kind);
            Assert.AreEqual(560f, march[3].X); Assert.AreEqual(760f, march[3].Y);
            Assert.AreEqual(0.6f, march[3].Phase);
            Assert.AreEqual(HazardKind.EmberVent, march[4].Kind);
            Assert.AreEqual(980f, march[4].X); Assert.AreEqual(450f, march[4].Y);
            Assert.AreEqual(1.8f, march[4].Phase);
        }

        // Gate: G2 — v1.1 tide-current: bands y 360-580/630-850 (halfH 110), push 200,
        // active window [0.8,4.0)+k·6. The parked player is pushed +x ONLY across
        // active-interior ticks, never during telegraph/idle; enemies are pushed too
        // (symmetric doctrine, SIM_SPEC_DUNGEONS §Gimmick 1).
        [Test]
        public void TideCurrent_PushesPlayerInsideActiveBand()
        {
            var config = Stage(CampaignStages.CinderSluice, cloak: 5);
            var sim = new CinderSim(in config);
            // Park inside lane A at (650,470): west of the pillar column x≈768 and
            // clear of BOTH corridor vents (500,604)/(1030,604) — v1.1 widened the
            // lanes (halfH 110) and vent-bombed the corridor, so the park spot is
            // pinned outside every blast disc below.
            WalkOnto(sim, 650f, 470f, 60 * 4);
            Assert.Less(sim.Player.X, 700f, "park must stay west of the pillar column");
            Assert.LessOrEqual(MathF.Abs(sim.Player.Y - 470f), CampaignSpec.CurrentHalfH,
                "park must sit inside lane A (y 360-580)");
            foreach (var (ventX, ventY) in new[] { (500f, 604f), (1030f, 604f) })
            {
                var ventDx = sim.Player.X - ventX;
                var ventDy = (sim.Player.Y - ventY) * SimConfig.IsoY;
                Assert.Greater(MathF.Sqrt(ventDx * ventDx + ventDy * ventDy), CampaignSpec.VentRadius,
                    $"park spot must clear the corridor vent at ({ventX},{ventY})");
            }

            // Interior ticks only (active on both edges) — the activation boundary
            // itself reads the previous tick's stage clock (1-tick latency contract).
            // Nova+ward keep the parked bot healthy against wave contact; neither
            // moves the player, so the push kinematics stay pure.
            var parkCast = new SimInput { NovaQueued = true, WardQueued = true };
            var activeIncreases = 0;
            var activeNonIncreases = 0;
            var idleDrifts = 0;
            var idleStills = 0;
            for (var t = 0; t < 60 * 12 && sim.Mode != SimMode.GameOver; t++)
            {
                var activeBefore = FindHazard(sim, HazardKind.TideCurrent, 768f, 470f).Active;
                var xBefore = sim.Player.X;
                var inBand = MathF.Abs(xBefore - 768f) <= CampaignSpec.CurrentHalfW
                    && MathF.Abs(sim.Player.Y - 470f) <= CampaignSpec.CurrentHalfH;
                sim.Tick(in parkCast);
                var activeAfter = FindHazard(sim, HazardKind.TideCurrent, 768f, 470f).Active;
                if (!inBand) continue;
                if (activeBefore && activeAfter)
                {
                    if (sim.Player.X > xBefore) activeIncreases++;
                    else activeNonIncreases++;
                }
                else if (!activeBefore && !activeAfter)
                {
                    if (sim.Player.X != xBefore) idleDrifts++;
                    else idleStills++;
                }
            }
            Assert.Greater(activeIncreases, 100, "two active windows must push the player +x");
            Assert.AreEqual(0, activeNonIncreases, "x must STRICTLY increase across active ticks");
            Assert.AreEqual(0, idleDrifts, "no drift during telegraph/idle windows");
            Assert.Greater(idleStills, 200, "idle windows must actually be sampled");
            Assert.AreNotEqual(SimMode.GameOver, sim.Mode, "parked cloak-5 bot must survive the window");

            // Enemies are pushed too: displacement of an in-band enemy > 0 along +x
            // across one active window while the player stays parked.
            var config2 = Stage(CampaignStages.CinderSluice, cloak: 5);
            var sim2 = new CinderSim(in config2);
            WalkOnto(sim2, 650f, 470f, 60 * 4);
            for (var guard = 0; guard < 60 * 7; guard++)
            {
                if (FindHazard(sim2, HazardKind.TideCurrent, 768f, 470f).Active) break;
                sim2.Tick(Idle);
            }
            Assert.IsTrue(FindHazard(sim2, HazardKind.TideCurrent, 768f, 470f).Active,
                "lane A must activate within one period");
            var startX = new System.Collections.Generic.Dictionary<int, float>();
            for (var i = 0; i < sim2.Enemies.Count; i++)
            {
                var enemy = sim2.Enemies[i];
                if (!enemy.Dead
                    && MathF.Abs(enemy.X - 768f) <= CampaignSpec.CurrentHalfW
                    && MathF.Abs(enemy.Y - 470f) <= CampaignSpec.CurrentHalfH)
                    startX[enemy.Id] = enemy.X;
            }
            Assert.Greater(startX.Count, 0, "an enemy must be inside lane A at activation");
            for (var t = 0; t < 60 * 2; t++) sim2.Tick(Idle);
            var bestDisplacement = float.MinValue;
            for (var i = 0; i < sim2.Enemies.Count; i++)
            {
                var enemy = sim2.Enemies[i];
                if (startX.TryGetValue(enemy.Id, out var x0))
                    bestDisplacement = MathF.Max(bestDisplacement, enemy.X - x0);
            }
            Assert.Greater(bestDisplacement, 0f, "an in-band enemy must be displaced along the push sign");
        }

        // Gate: G2 — active push cannot escape the arena (push → re-clamp order) and
        // lane B (phase 3.0, push −200) mirrors lane A.
        [Test]
        public void TideCurrent_SymmetricAndClamped()
        {
            // Lane A, held +x through two active windows: never beyond the L1 diamond.
            var config = Stage(CampaignStages.CinderSluice, cloak: 5);
            var sim = new CinderSim(in config);
            WalkOnto(sim, 990f, 470f, 60 * 8);
            var right = new SimInput { MoveX = 1f };
            var maxX = sim.Player.X;
            for (var t = 0; t < 60 * 14 && sim.Mode != SimMode.GameOver; t++)
            {
                sim.Tick(in right);
                maxX = MathF.Max(maxX, sim.Player.X);
            }
            var halfW = SimConfig.ArenaHalfWidth - SimConfig.PlayerMarginClamp;
            var halfH = SimConfig.ArenaHalfHeight - SimConfig.PlayerMarginClamp * 0.5f;
            var bound = SimConfig.ArenaX + halfW * (1f - MathF.Abs(sim.Player.Y - SimConfig.ArenaY) / halfH);
            Assert.LessOrEqual(maxX, bound + 0.01f, "current push must re-clamp to the arena diamond");

            // Lane B: parked at (830,740), x strictly decreases across active ticks.
            var configB = Stage(CampaignStages.CinderSluice, cloak: 5);
            var simB = new CinderSim(in configB);
            WalkOnto(simB, 830f, 740f, 60 * 4);
            var decreases = 0;
            var nonDecreases = 0;
            for (var t = 0; t < 60 * 12 && simB.Mode != SimMode.GameOver; t++)
            {
                var activeBefore = FindHazard(simB, HazardKind.TideCurrent, 768f, 740f).Active;
                var xBefore = simB.Player.X;
                var inBand = MathF.Abs(xBefore - 768f) <= CampaignSpec.CurrentHalfW
                    && MathF.Abs(simB.Player.Y - 740f) <= CampaignSpec.CurrentHalfH;
                simB.Tick(Idle);
                var activeAfter = FindHazard(simB, HazardKind.TideCurrent, 768f, 740f).Active;
                if (!inBand || !activeBefore || !activeAfter) continue;
                if (simB.Player.X < xBefore) decreases++;
                else nonDecreases++;
            }
            Assert.Greater(decreases, 100, "lane B active window must push -x");
            Assert.AreEqual(0, nonDecreases, "lane B push must be strict");
        }

        // Gate: G2/G3 — pylon combat contract (SIM_SPEC_DUNGEONS §Gimmick 2 v1.1):
        // (a) weapon-5 basic swings kill the 300 hp pylon in exactly 4 hits, hp falls
        // monotonically, PylonDown exactly once, hp pinned 0; (b) live aura scales
        // enemy damage TAKEN ×0.40 (two sims, identical inputs); (c) skills never
        // damage the pylon; (d) pylon-only combo swing still counts as "landed" for
        // ComboFinisher eligibility.
        [Test]
        public void EmberPylon_CombatContract()
        {
            // (a) walk to (450,500), face +x, spam basic attacks at pylon(560,500):
            // 75.4+75.4+75.4+73.8 = 300 — the v1.1 hp is a real commitment cost.
            var config = Stage(CampaignStages.EmberBastion, weapon: 5, cloak: 5);
            var sim = new CinderSim(in config);
            WalkOnto(sim, 450f, 500f, 60 * 4);
            var east = new SimInput { MoveX = 1f };
            for (var t = 0; t < 3; t++) sim.Tick(in east);
            Assert.AreEqual(1, sim.Player.Facing, "swing arc requires facing the pylon");
            var pylonDownEvents = 0;
            var swings = 0;
            var swingSum = 0f;
            var hp = FindHazard(sim, HazardKind.EmberPylon, 560f, 500f).Hp;
            Assert.AreEqual(CampaignSpec.PylonHp, hp, "pylon must publish full hp before the fight");
            var attack = new SimInput { AttackQueued = true };
            for (var t = 0; t < 60 * 8 && sim.Mode != SimMode.GameOver; t++)
            {
                sim.Tick(in attack);
                if ((sim.Events & SimEvents.PylonDown) != 0) pylonDownEvents++;
                var hpNow = FindHazard(sim, HazardKind.EmberPylon, 560f, 500f).Hp;
                Assert.LessOrEqual(hpNow, hp, "pylon hp must fall monotonically (no regen/respawn)");
                if (hpNow < hp) { swings++; swingSum += hp - hpNow; }
                hp = hpNow;
            }
            Assert.AreEqual(4, swings, "weapon-5 (75.4/swing) must need exactly 4 swings for 300 hp");
            Assert.AreEqual(CampaignSpec.PylonHp, swingSum, 1e-2f, "swing drops must sum to the full hp pool");
            Assert.AreEqual(1, pylonDownEvents, "PylonDown must be raised exactly once");
            Assert.AreEqual(0f, hp, "destroyed pylon hp stays 0 (no respawn within the run)");

            // (b) aura ×0.40: same scripted inputs, config with pylons vs pylons
            // stripped; compare the first enemy health delta. Pylons never move or
            // block, so both trajectories are identical until the first landed hit.
            var withConfig = Stage(CampaignStages.EmberBastion, cloak: 5);
            var withoutConfig = Stage(CampaignStages.EmberBastion, cloak: 5);
            var stripped = new System.Collections.Generic.List<HazardConfig>();
            foreach (var hazard in withoutConfig.Hazards)
                if (hazard.Kind != HazardKind.EmberPylon) stripped.Add(hazard);
            withoutConfig.Hazards = stripped.ToArray();
            var auraSim = new CinderSim(in withConfig);
            var bareSim = new CinderSim(in withoutConfig);
            var auraDelta = -1f;
            var bareDelta = -1f;
            for (var t = 0; t < 60 * 20 && (auraDelta < 0f || bareDelta < 0f); t++)
            {
                var healthsA = EnemyHealths(auraSim);
                var healthsB = EnemyHealths(bareSim);
                var inputA = AuraAnchorInput(auraSim, 620f, 520f);
                var inputB = AuraAnchorInput(bareSim, 620f, 520f);
                auraSim.Tick(in inputA);
                bareSim.Tick(in inputB);
                if (auraDelta < 0f && (auraSim.Events & SimEvents.EnemyHit) != 0)
                    auraDelta = FirstHealthDelta(healthsA, auraSim);
                if (bareDelta < 0f && (bareSim.Events & SimEvents.EnemyHit) != 0)
                    bareDelta = FirstHealthDelta(healthsB, bareSim);
            }
            Assert.Greater(bareDelta, 0f, "control sim must land a hit");
            Assert.Greater(auraDelta, 0f, "aura sim must land a hit");
            Assert.AreEqual(CampaignSpec.PylonAuraDamageTakenMult, auraDelta / bareDelta, 1e-4f,
                "live pylon aura must scale enemy damage taken by exactly 0.40");

            // (c) skills do NOT damage the pylon: cast nova adjacent, hp unchanged.
            var novaConfig = Stage(CampaignStages.EmberBastion, cloak: 5);
            var novaSim = new CinderSim(in novaConfig);
            WalkOnto(novaSim, 470f, 500f, 60 * 4);
            Assert.AreEqual(CampaignSpec.PylonHp, FindHazard(novaSim, HazardKind.EmberPylon, 560f, 500f).Hp);
            var castSeen = false;
            for (var t = 0; t < 60 * 20 && novaSim.Mode != SimMode.GameOver; t++)
            {
                var input = default(SimInput);
                if (novaSim.Charge >= SimConfig.NovaCost) input.NovaQueued = true;
                novaSim.Tick(in input);
                if ((novaSim.Events & SimEvents.NovaCast) != 0) { castSeen = true; break; }
            }
            Assert.IsTrue(castSeen, "nova must cast next to the pylon");
            Assert.AreEqual(CampaignSpec.PylonHp, FindHazard(novaSim, HazardKind.EmberPylon, 560f, 500f).Hp,
                "skills are inert against pylons (§Gimmick 2 tactical clarity)");

            // (d) pylon-only combo swing sets ComboFinisher eligibility (hack lane —
            // the combo kit only exists there; same CinderSim contract).
            Assert.IsTrue(HackConfig.TryDungeon(
                CampaignStages.EmberBastion, default, EquipTiers.Of(0, 0, 5), null, 0, out var hackConfig));
            var comboSim = new CinderSim(in hackConfig);
            WalkOnto(comboSim, 450f, 500f, 60 * 4);
            for (var t = 0; t < 3; t++) comboSim.Tick(in east);
            var pylonOnlyFinisher = false;
            for (var t = 0; t < 60 * 6 && comboSim.Mode != SimMode.GameOver; t++)
            {
                comboSim.Tick(in attack);
                if ((comboSim.Events & SimEvents.ComboFinisher) == 0) continue;
                var enemyInReach = false;
                for (var i = 0; i < comboSim.Enemies.Count; i++)
                {
                    var enemy = comboSim.Enemies[i];
                    if (enemy.Dead) continue;
                    var dx = enemy.X - comboSim.Player.X;
                    var dy = (enemy.Y - comboSim.Player.Y) * SimConfig.IsoY;
                    if (dx * dx + dy * dy <= SimConfig.PlayerAttackRange * SimConfig.PlayerAttackRange)
                        enemyInReach = true;
                }
                if (!enemyInReach) { pylonOnlyFinisher = true; break; }
            }
            Assert.IsTrue(pylonOnlyFinisher,
                "a combo finisher landing ONLY on the pylon must still raise ComboFinisher");
        }

        // Gate: G2/R2 — THE v1.1 retune pin (gimmick-retune-spec §R2): aura 280 now
        // covers the spawn convergence point (768,604) from all three pylons
        // (iso 247-256), which v1.0's 220 did NOT — centre combat is always a shield
        // fight, so ignoring pylons is a visible, measurable mistake.
        [Test]
        public void EmberPylon_AuraCoversSpawnConvergence()
        {
            // Arithmetic pin: every pylon anchor sits inside (220, 280] iso of spawn.
            var bastion = Stage(CampaignStages.EmberBastion).Hazards;
            var pylons = 0;
            foreach (var hazard in bastion)
            {
                if (hazard.Kind != HazardKind.EmberPylon) continue;
                pylons++;
                var dx = hazard.X - 768f;
                var dy = (hazard.Y - 604f) * SimConfig.IsoY;
                var isoDistance = MathF.Sqrt(dx * dx + dy * dy);
                Assert.Greater(isoDistance, 220f,
                    $"pylon ({hazard.X},{hazard.Y}): the v1.0 aura 220 must NOT have covered spawn");
                Assert.LessOrEqual(isoDistance, CampaignSpec.PylonAuraRadius,
                    $"pylon ({hazard.X},{hazard.Y}): the v1.1 aura 280 must cover spawn");
            }
            Assert.AreEqual(3, pylons, "v1.1 fields three pylons");

            // Behavioural pin: a hex ring of pylons at iso-405 around the spawn anchor
            // puts EVERY enemy the anchored player can strike (reach 160 + park drift
            // ≤24 → ≤184 from anchor) at 221..262 iso from the nearest ring pylon —
            // OUTSIDE v1.0's 220, inside v1.1's 280. Damage taken must still be ×0.40.
            var ringConfig = Stage(CampaignStages.EmberBastion, cloak: 5);
            var bareConfig = Stage(CampaignStages.EmberBastion, cloak: 5);
            var ringHazards = new System.Collections.Generic.List<HazardConfig>();
            foreach (var hazard in ringConfig.Hazards)
                if (hazard.Kind != HazardKind.EmberPylon) ringHazards.Add(hazard);
            var bareHazards = new System.Collections.Generic.List<HazardConfig>(ringHazards);
            for (var k = 0; k < 6; k++)
            {
                var angle = MathF.PI / 3f * k;
                ringHazards.Add(HazardConfig.Pylon(
                    768f + 405f * MathF.Cos(angle),
                    604f + 405f * MathF.Sin(angle) / SimConfig.IsoY));
            }
            ringConfig.Hazards = ringHazards.ToArray();
            bareConfig.Hazards = bareHazards.ToArray();
            var ringSim = new CinderSim(in ringConfig);
            var bareSim = new CinderSim(in bareConfig);
            var ringDelta = -1f;
            var bareDelta = -1f;
            var hitDistance = -1f;
            for (var t = 0; t < 60 * 20 && (ringDelta < 0f || bareDelta < 0f); t++)
            {
                var healthsA = EnemyHealths(ringSim);
                var healthsB = EnemyHealths(bareSim);
                var inputA = AuraAnchorInput(ringSim, 768f, 604f);
                var inputB = AuraAnchorInput(bareSim, 768f, 604f);
                ringSim.Tick(in inputA);
                bareSim.Tick(in inputB);
                if (ringDelta < 0f && (ringSim.Events & SimEvents.EnemyHit) != 0)
                {
                    for (var i = 0; i < healthsA.Length && i < ringSim.Enemies.Count; i++)
                    {
                        var delta = healthsA[i] - ringSim.Enemies[i].Health;
                        if (delta <= 0f) continue;
                        ringDelta = delta;
                        var best = float.MaxValue;
                        foreach (var hazard in ringConfig.Hazards)
                        {
                            if (hazard.Kind != HazardKind.EmberPylon) continue;
                            var pylonDx = ringSim.Enemies[i].X - hazard.X;
                            var pylonDy = (ringSim.Enemies[i].Y - hazard.Y) * SimConfig.IsoY;
                            best = MathF.Min(best, MathF.Sqrt(pylonDx * pylonDx + pylonDy * pylonDy));
                        }
                        hitDistance = best;
                        break;
                    }
                }
                if (bareDelta < 0f && (bareSim.Events & SimEvents.EnemyHit) != 0)
                    bareDelta = FirstHealthDelta(healthsB, bareSim);
            }
            Assert.Greater(bareDelta, 0f, "control sim must land a hit");
            Assert.Greater(ringDelta, 0f, "ring sim must land a hit");
            Assert.Greater(hitDistance, 220f, "the struck enemy must sit OUTSIDE the v1.0 aura radius");
            Assert.LessOrEqual(hitDistance, CampaignSpec.PylonAuraRadius,
                "the struck enemy must sit inside the v1.1 aura radius");
            Assert.AreEqual(CampaignSpec.PylonAuraDamageTakenMult, ringDelta / bareDelta, 1e-4f,
                "beyond-220 iso must now be shielded at exactly ×0.40 — the retune's point");
        }

        static SimInput AuraAnchorInput(CinderSim sim, float anchorX, float anchorY)
        {
            // Hold the anchor; any enemy within player reach 160 of an in-aura anchor
            // stays inside that aura (280) by the triangle inequality. Reads only
            // positions/dead flags, so paired sims stay in lockstep until a hit lands.
            var input = default(SimInput);
            float px = sim.Player.X, py = sim.Player.Y;
            float dx = anchorX - px, dy = anchorY - py;
            if (dx * dx + dy * dy > 12f * 12f)
            {
                var len = MathF.Max(0.001f, MathF.Sqrt(dx * dx + dy * dy));
                input.MoveX = dx / len;
                input.MoveY = dy / len;
                return input;
            }
            var bestD2 = float.MaxValue;
            var bestDx = 0f;
            for (var i = 0; i < sim.Enemies.Count; i++)
            {
                var enemy = sim.Enemies[i];
                if (enemy.Dead) continue;
                float ex = enemy.X - px, ey = (enemy.Y - py) * SimConfig.IsoY;
                var d2 = ex * ex + ey * ey;
                if (d2 < bestD2) { bestD2 = d2; bestDx = ex; }
            }
            if (bestD2 < float.MaxValue && bestDx * sim.Player.Facing < 0f)
                input.MoveX = bestDx > 0f ? 1f : -1f;
            else
                input.AttackQueued = true;
            return input;
        }

        static float[] EnemyHealths(CinderSim sim)
        {
            var healths = new float[sim.Enemies.Count];
            for (var i = 0; i < healths.Length; i++) healths[i] = sim.Enemies[i].Health;
            return healths;
        }

        static float FirstHealthDelta(float[] before, CinderSim sim)
        {
            for (var i = 0; i < before.Length && i < sim.Enemies.Count; i++)
            {
                var delta = before[i] - sim.Enemies[i].Health;
                if (delta > 0f) return delta;
            }
            return -1f;
        }

        // Gate: G2 — ash-wall v1.1 kinematics + tick discipline (§Gimmick 3):
        // cycle 23.0 s = rest 4.5 / telegraph 1.5 / advance 7 / hold 3 / recede 7,
        // speed 80; LEFT band grows from x 248, RIGHT wall (phase 11.5) mirrors from
        // x 1288; exact-10 drops ride the 0.6 s global grid; walls alone kill with
        // full credit.
        [Test]
        public void AshWall_TimetableAndTicks()
        {
            // (1) timetable — kiter keeps the sim alive (a parked-idle bot dies to
            // melee saturation, freezing the hazard clock with it). Both walls are
            // sampled at t = 4.5 / 7 / 13.5 / 20 s (ticks 270/420/810/1200).
            var config = Stage(CampaignStages.AshMarch, 5, 5, 5);
            var sim = new CinderSim(in config);
            var leftSamples = new System.Collections.Generic.Dictionary<int, float>();
            var rightSamples = new System.Collections.Generic.Dictionary<int, float>();
            for (var t = 1; t <= 1200; t++)
            {
                Assert.AreNotEqual(SimMode.GameOver, sim.Mode, "5/5/5 kiter must survive to t=20");
                sim.Tick(BotInput(sim));
                if (t == 270 || t == 420 || t == 810 || t == 1200)
                {
                    leftSamples[t] = FindHazard(
                        sim, HazardKind.AshWall, CampaignSpec.WallEdgeX, SimConfig.ArenaY).FrontX;
                    rightSamples[t] = FindHazard(
                        sim, HazardKind.AshWall, CampaignSpec.WallEdgeRightX, SimConfig.ArenaY).FrontX;
                }
            }
            var tolerance = CampaignSpec.WallSpeed / 60f + 0.01f;   // one fixed step of travel
            Assert.AreEqual(248f, leftSamples[270], tolerance, "t=4.5: rest ends, left depth still 0");
            Assert.AreEqual(328f, leftSamples[420], tolerance, "t=7.0: advance 248+(7-6)*80");
            Assert.AreEqual(808f, leftSamples[810], tolerance, "t=13.5: hold at 248+560 — PAST centre 768");
            Assert.AreEqual(488f, leftSamples[1200], tolerance, "t=20.0: recede 808-(20-16)*80");
            // Right wall local t = stage t + 11.5 — half a period out of phase.
            Assert.AreEqual(728f, rightSamples[270], tolerance, "t=4.5: right local 16.0, hold end at 1288-560");
            Assert.AreEqual(928f, rightSamples[420], tolerance, "t=7.0: right local 18.5, recede 728+(18.5-16)*80");
            Assert.AreEqual(1288f, rightSamples[810], tolerance, "t=13.5: right local 2.0, resting at its edge");
            Assert.AreEqual(1088f, rightSamples[1200], tolerance, "t=20.0: right local 8.5, advance 1288-(8.5-6)*80");

            // (2) exact-10 drops on the 0.6 s grid. Killer-park at (300,604) — the L1
            // clamp pins x≈282-324 — one-shots wave arrivals (weapon 5: 75.4 vs ≤80 hp
            // in-window) so melee contact-grace cannot mask wall ticks. The left front
            // crosses the park spot at t≈6.6 s; every exact-10 band drop in [7.2,11.4)
            // must ride the grid in whole 36-tick steps (float stage-clock accumulation
            // may shift the boundary by one tick, so cadence — not tick%36 — is the
            // contract).
            var gridConfig = Stage(CampaignStages.AshMarch, 5, 0, 5);
            var gridSim = new CinderSim(in gridConfig);
            var wallDropTicks = new System.Collections.Generic.List<int>();
            var hpPrevious = gridSim.Player.Health;
            for (var t = 1; t < 684; t++)
            {
                Assert.AreNotEqual(SimMode.GameOver, gridSim.Mode, "grid bot must survive to t=11.4");
                gridSim.Tick(KillerParkInput(gridSim, 300f, 604f));
                var front = FindHazard(
                    gridSim, HazardKind.AshWall, CampaignSpec.WallEdgeX, SimConfig.ArenaY).FrontX;
                var hpNow = gridSim.Player.Health;
                if (hpNow < hpPrevious && t >= 432
                    && hpPrevious - hpNow == CampaignSpec.WallTickDamage
                    && gridSim.Player.X < front)
                {
                    wallDropTicks.Add(t);
                }
                hpPrevious = hpNow;
            }
            Assert.GreaterOrEqual(wallDropTicks.Count, 3, ">=3 exact-10 band drops in [7.2,11.4)");
            var spacedExactly36 = 0;
            for (var i = 1; i < wallDropTicks.Count; i++)
            {
                var gap = wallDropTicks[i] - wallDropTicks[i - 1];
                Assert.AreEqual(0, gap % 36, "wall drops must ride the global 0.6 s grid");
                if (gap == 36) spacedExactly36++;
            }
            Assert.GreaterOrEqual(spacedExactly36, 2, "consecutive grid ticks must land while parked in-band");

            // (3) environmental kill credit: bait bot never attacks — wave 1 is lured
            // east, the sprint west drags pursuers across the advancing left front, and
            // the wall alone kills them (kill + score credit, §Gimmick 3 DamageEnemy
            // path). Hazards are overridden to a single left wall: under the full v1.1
            // table the right wall grinds the bait down during the lure leg (v1.1
            // placement itself is pinned by StageTable/right-wall tests), and the
            // isolated wall keeps the credit observation deterministic. The bait dies
            // to the wall AFTER credit is banked — the loop tolerates it.
            var baitConfig = Stage(CampaignStages.AshMarch, cloak: 5);
            baitConfig.Hazards = new[] { HazardConfig.Wall(0f) };
            var baitSim = new CinderSim(in baitConfig);
            var killsAtWindowOpen = -1;
            var scoreAtWindowOpen = -1;
            var deadInBand = 0;
            var parkTick = -1;
            var phase = 0;
            var wasAlive = new System.Collections.Generic.HashSet<int>();
            for (var t = 1; t <= 1140 && baitSim.Mode != SimMode.GameOver; t++)
            {
                for (var i = 0; i < baitSim.Enemies.Count; i++)
                    if (!baitSim.Enemies[i].Dead) wasAlive.Add(baitSim.Enemies[i].Id);
                baitSim.Tick(BaitInput(baitSim, ref phase));
                if (phase == 2 && parkTick < 0 && baitSim.Player.X < 380f) parkTick = t;
                if (t == 630)
                {
                    killsAtWindowOpen = baitSim.Digest.Kills;
                    scoreAtWindowOpen = baitSim.Digest.Score;
                }
                var baitFront = FindHazard(baitSim, HazardKind.AshWall).FrontX;
                for (var i = 0; i < baitSim.Enemies.Count; i++)
                {
                    var enemy = baitSim.Enemies[i];
                    if (enemy.Dead && wasAlive.Contains(enemy.Id))
                    {
                        wasAlive.Remove(enemy.Id);
                        if (enemy.X < baitFront) deadInBand++;
                    }
                }
            }
            Assert.IsTrue(parkTick > 0 && parkTick < 630, "bait must be parked west before t=10.5");
            Assert.AreEqual(0, killsAtWindowOpen, "the bait bot never attacks — kills at t=10.5 must be 0");
            Assert.GreaterOrEqual(deadInBand, 1, "an enemy standing in the band must eventually die to wall ticks");
            Assert.Greater(baitSim.Digest.Kills, killsAtWindowOpen, "wall kills grant kill credit");
            Assert.Greater(baitSim.Digest.Score, scoreAtWindowOpen, "wall kills grant score");
        }

        static SimInput KillerParkInput(CinderSim sim, float parkX, float parkY)
        {
            // Park at (parkX,parkY) (the L1 clamp may pin short of it) and one-shot
            // arrivals facing-aware; nova as pack backup. No ward — it would eat wall
            // ticks and mask the grid.
            var input = default(SimInput);
            float px = sim.Player.X, py = sim.Player.Y;
            float dx = parkX - px, dy = parkY - py;
            if (dx * dx + dy * dy > 24f * 24f)
            {
                var len = MathF.Max(0.001f, MathF.Sqrt(dx * dx + dy * dy));
                input.MoveX = dx / len;
                input.MoveY = dy / len;
                return input;
            }
            input.NovaQueued = true;
            var bestD2 = float.MaxValue;
            var bestDx = 0f;
            for (var i = 0; i < sim.Enemies.Count; i++)
            {
                var enemy = sim.Enemies[i];
                if (enemy.Dead) continue;
                float ex = enemy.X - px, ey = (enemy.Y - py) * SimConfig.IsoY;
                var d2 = ex * ex + ey * ey;
                if (d2 < bestD2) { bestD2 = d2; bestDx = ex; }
            }
            if (bestD2 < 200f * 200f && bestDx * sim.Player.Facing < 0f)
                input.MoveX = bestDx > 0f ? 1f : -1f;
            else if (bestD2 < 200f * 200f)
                input.AttackQueued = true;
            return input;
        }

        static SimInput CorridorMidInput(CinderSim sim)
        {
            // Stand midway between the two published wall fronts (the guaranteed-safe
            // corridor centre — the v1.1 no-escape proof keeps it >=300 px from either
            // band), one-shotting adjacent arrivals like KillerParkInput.
            var hazards = ((ICampaignSnapshot)sim).Hazards;
            var leftFront = CampaignSpec.WallEdgeX;
            var rightFront = CampaignSpec.WallEdgeRightX;
            for (var i = 0; i < hazards.Count; i++)
            {
                if (hazards[i].Kind != HazardKind.AshWall) continue;
                if (hazards[i].X == CampaignSpec.WallEdgeX) leftFront = hazards[i].FrontX;
                else rightFront = hazards[i].FrontX;
            }
            return KillerParkInput(sim, (leftFront + rightFront) * 0.5f, SimConfig.ArenaY);
        }

        static SimInput BaitInput(CinderSim sim, ref int phase)
        {
            var input = default(SimInput);
            float px = sim.Player.X, py = sim.Player.Y;
            if (phase == 0)
            {
                float dx = 1150f - px, dy = 560f - py;
                if (dx * dx + dy * dy < 20f * 20f) phase = 1;
                else
                {
                    var len = MathF.Max(0.001f, MathF.Sqrt(dx * dx + dy * dy));
                    input.MoveX = dx / len;
                    input.MoveY = dy / len;
                }
            }
            if (phase == 1)
            {
                var best = float.MaxValue;
                for (var i = 0; i < sim.Enemies.Count; i++)
                {
                    var enemy = sim.Enemies[i];
                    if (enemy.Dead) continue;
                    float ex = enemy.X - px, ey = (enemy.Y - py) * SimConfig.IsoY;
                    best = MathF.Min(best, ex * ex + ey * ey);
                }
                if (best < 170f * 170f) phase = 2;
            }
            if (phase == 2)
            {
                float dx = 300f - px, dy = 604f - py;
                var len = MathF.Max(0.001f, MathF.Sqrt(dx * dx + dy * dy));
                if (len > 6f)
                {
                    input.MoveX = dx / len;
                    input.MoveY = dy / len;
                }
            }
            return input;
        }

        // Gate: G2 — v1.1 right wall (march table Wall(11.5,right)) + the corridor
        // invariant: during the right hold (stage t ∈ [1.5,4.5), local t ∈ [13,16))
        // a player parked at x>1000 takes exact-10 grid drops while a corridor player
        // at x≈600 takes none; LEFT wall damage never reaches x>808 and RIGHT never
        // reaches x<728 (WallCovers tests strictly against FrontX, so the published
        // FrontX bounds ARE the damage-band edges); whenever both walls are live the
        // published gap stays ≥600 px (dual-encroachment depth sum 440 — the spec's
        // no-escape proof).
        [Test]
        public void AshWall_RightWallHoldAndCorridorInvariant()
        {
            // (a) runner parks deep in the right band (x≈1100) before the first
            // hold-window grid tick (t=1.9 s; walk-in ≈1.5 s), then eats exact-10
            // drops on the 36-tick cadence.
            var runnerConfig = Stage(CampaignStages.AshMarch, 5, 0, 5);
            var runnerSim = new CinderSim(in runnerConfig);
            var runnerDrops = new System.Collections.Generic.List<int>();
            var hpPrevious = runnerSim.Player.Health;
            for (var t = 1; t < 270; t++)
            {
                Assert.AreNotEqual(SimMode.GameOver, runnerSim.Mode, "runner must survive the hold window");
                runnerSim.Tick(KillerParkInput(runnerSim, 1100f, 604f));
                var front = FindHazard(
                    runnerSim, HazardKind.AshWall, CampaignSpec.WallEdgeRightX, SimConfig.ArenaY).FrontX;
                var hpNow = runnerSim.Player.Health;
                if (hpNow < hpPrevious && t >= 90
                    && hpPrevious - hpNow == CampaignSpec.WallTickDamage
                    && runnerSim.Player.X > front)
                {
                    runnerDrops.Add(t);
                }
                hpPrevious = hpNow;
            }
            Assert.GreaterOrEqual(runnerDrops.Count, 4, "right hold must land >=4 exact-10 drops at x>1000");
            for (var i = 1; i < runnerDrops.Count; i++)
                Assert.AreEqual(0, (runnerDrops[i] - runnerDrops[i - 1]) % 36,
                    "right-wall drops must ride the same global 0.6 s grid");

            // Control: identical bot parked in the corridor at (600,604) — the left
            // band (x<248+depth) reaches x=600 only when depth>352 (first at t=10.4 s,
            // after this window) and the right band never reaches x<728: ZERO
            // exact-10 drops.
            var controlConfig = Stage(CampaignStages.AshMarch, 5, 0, 5);
            var controlSim = new CinderSim(in controlConfig);
            hpPrevious = controlSim.Player.Health;
            var controlDrops = 0;
            for (var t = 1; t < 270; t++)
            {
                Assert.AreNotEqual(SimMode.GameOver, controlSim.Mode, "control must survive the window");
                controlSim.Tick(KillerParkInput(controlSim, 600f, 604f));
                var hpNow = controlSim.Player.Health;
                if (hpNow < hpPrevious && hpPrevious - hpNow == CampaignSpec.WallTickDamage) controlDrops++;
                hpPrevious = hpNow;
            }
            Assert.AreEqual(0, controlDrops, "the corridor player must take no wall-sized drops");

            // (b) corridor invariant over one full 23 s cycle: a corridor-mid bot
            // survives untouched; FrontX bounds pin "left never past 808 / right never
            // past 728"; the dual-active gap never dips below 600 px.
            var sweepConfig = Stage(CampaignStages.AshMarch, 5, 0, 5);
            var sweepSim = new CinderSim(in sweepConfig);
            var maxLeftFront = float.MinValue;
            var minRightFront = float.MaxValue;
            var minGap = float.MaxValue;
            var bothActiveTicks = 0;
            for (var t = 1; t <= 1380; t++)
            {
                Assert.AreNotEqual(SimMode.GameOver, sweepSim.Mode, "corridor bot must survive a full cycle");
                sweepSim.Tick(CorridorMidInput(sweepSim));
                var leftWall = FindHazard(
                    sweepSim, HazardKind.AshWall, CampaignSpec.WallEdgeX, SimConfig.ArenaY);
                var rightWall = FindHazard(
                    sweepSim, HazardKind.AshWall, CampaignSpec.WallEdgeRightX, SimConfig.ArenaY);
                maxLeftFront = MathF.Max(maxLeftFront, leftWall.FrontX);
                minRightFront = MathF.Min(minRightFront, rightWall.FrontX);
                if (leftWall.Active && rightWall.Active)
                {
                    bothActiveTicks++;
                    minGap = MathF.Min(minGap, rightWall.FrontX - leftWall.FrontX);
                }
            }
            Assert.AreEqual(CampaignSpec.WallEdgeX + CampaignSpec.WallDepthMax, maxLeftFront, 0.5f,
                "left hold must encroach to exactly x=808 — past centre 768");
            Assert.LessOrEqual(maxLeftFront, 808f + 0.001f, "LEFT wall damage never reaches x>808");
            Assert.AreEqual(CampaignSpec.WallEdgeRightX - CampaignSpec.WallDepthMax, minRightFront, 0.5f,
                "right hold must encroach to exactly x=728 — past centre 768");
            Assert.GreaterOrEqual(minRightFront, 728f - 0.001f, "RIGHT wall damage never reaches x<728");
            Assert.Greater(bothActiveTicks, 600, "the closing jaws must overlap for ~11 s per cycle");
            Assert.GreaterOrEqual(minGap, 600f - 0.01f,
                "safe corridor >=600 px whenever both walls are live (depth sum 440)");
        }

        // Gate: D3 — simultaneous-telegraph census (qa band 5): ≤3 concurrent, ≤2
        // same-kind. v1.1 LCM windows: sluice LCM(6,2.4)=12 s (kiter), bastion
        // vent-only 2.4 s (sampled 3 s, kiter), march LCM(23,2.4)=276 s — swept
        // analytically over the full LCM with a byte-exact mirror of the published
        // telegraph windows, after the mirror is cross-checked tick-for-tick against
        // the live sim for one whole 23 s wall period (a corridor-mid bot provably
        // survives that; no bot is guaranteed 4.6 min under the v1.1 walls).
        [Test]
        public void Telegraph_CensusUnderBudget()
        {
            AssertTelegraphCensus(CampaignStages.CinderSluice, 60 * 12, BotInput);
            AssertTelegraphCensus(CampaignStages.EmberBastion, 60 * 3, BotInput);
            AssertTelegraphCensus(CampaignStages.AshMarch, 60 * 23, CorridorMidInput);

            // march full-LCM analytic sweep (mirror validated tick-exactly above).
            var march = Stage(CampaignStages.AshMarch);
            var maxTotal = 0;
            var maxSameKind = 0;
            var stageClock = 0f;
            var perKind = new System.Collections.Generic.Dictionary<HazardKind, int>();
            for (var t = 1; t <= 60 * 276; t++)
            {
                stageClock += SimConfig.FixedStep;
                var total = 0;
                perKind.Clear();
                foreach (var hazard in march.Hazards)
                {
                    if (!MirrorTelegraph(hazard, stageClock)) continue;
                    total++;
                    perKind.TryGetValue(hazard.Kind, out var count);
                    perKind[hazard.Kind] = count + 1;
                }
                maxTotal = Math.Max(maxTotal, total);
                foreach (var pair in perKind) maxSameKind = Math.Max(maxSameKind, pair.Value);
            }
            Assert.LessOrEqual(maxTotal, 3, "march 276 s LCM: max simultaneous telegraphs");
            Assert.LessOrEqual(maxSameKind, 2, "march 276 s LCM: max same-kind telegraphs");
        }

        delegate SimInput CensusBot(CinderSim sim);

        static void AssertTelegraphCensus(string stageId, int ticks, CensusBot bot)
        {
            // Hazard clocks run on stage time (they tick through intermissions), so a
            // surviving bot samples the full window deterministically. The analytic
            // mirror must agree with every published Telegraphing flag — that is what
            // licenses the mirror-only 276 s march sweep (same float accumulation:
            // stageClock += FixedStep, bit-identical to the sim's stage clock).
            var config = Stage(stageId, 5, 5, 5);
            var sim = new CinderSim(in config);
            var maxTotal = 0;
            var maxSameKind = 0;
            var stageClock = 0f;
            var perKind = new System.Collections.Generic.Dictionary<HazardKind, int>();
            for (var t = 0; t < ticks; t++)
            {
                Assert.AreNotEqual(SimMode.GameOver, sim.Mode,
                    $"{stageId}: census bot must survive the sampling window");
                sim.Tick(bot(sim));
                stageClock += SimConfig.FixedStep;
                var hazards = ((ICampaignSnapshot)sim).Hazards;
                var total = 0;
                perKind.Clear();
                for (var i = 0; i < hazards.Count; i++)
                {
                    Assert.AreEqual(hazards[i].Telegraphing, MirrorTelegraph(config.Hazards[i], stageClock),
                        $"{stageId}: telegraph mirror must match the sim at tick {t + 1}");
                    if (!hazards[i].Telegraphing) continue;
                    total++;
                    perKind.TryGetValue(hazards[i].Kind, out var count);
                    perKind[hazards[i].Kind] = count + 1;
                }
                maxTotal = Math.Max(maxTotal, total);
                foreach (var pair in perKind) maxSameKind = Math.Max(maxSameKind, pair.Value);
            }
            Assert.LessOrEqual(maxTotal, 3, $"{stageId}: max simultaneous telegraphs");
            Assert.LessOrEqual(maxSameKind, 2, $"{stageId}: max same-kind telegraphs");
        }

        static bool MirrorTelegraph(in HazardConfig hazard, float stageTime)
        {
            switch (hazard.Kind)
            {
                case HazardKind.EmberVent:
                {
                    var cycleT = (stageTime + hazard.Phase) % CampaignSpec.VentPeriod;
                    return cycleT >= CampaignSpec.VentPeriod - CampaignSpec.VentTelegraph;
                }
                case HazardKind.TideCurrent:
                {
                    var cycleT = (stageTime + hazard.Phase) % CampaignSpec.CurrentPeriod;
                    return cycleT < CampaignSpec.CurrentTelegraph;
                }
                case HazardKind.AshWall:
                {
                    var cycleT = (stageTime + hazard.Phase) % CampaignSpec.WallPeriod;
                    return cycleT >= CampaignSpec.WallRest
                        && cycleT < CampaignSpec.WallRest + CampaignSpec.WallTelegraph;
                }
                default:
                    return false;
            }
        }

        // Gate: D1 — same config + kiter, 1800 ticks, two fresh sims -> identical
        // digests AND identical player position, per new stage (hack lane 2/1/3).
        [Test]
        public void NewStages_SameConfigSameInputs_IdenticalDigests()
        {
            foreach (var stageId in new[]
                { CampaignStages.CinderSluice, CampaignStages.EmberBastion, CampaignStages.AshMarch })
            {
                var configA = Dungeon213(stageId);
                var configB = Dungeon213(stageId);
                var simA = new CinderSim(in configA);
                var simB = new CinderSim(in configB);
                for (var t = 0; t < 1800; t++) simA.Tick(BotInput(simA));
                for (var t = 0; t < 1800; t++) simB.Tick(BotInput(simB));
                AssertSameDigest(simA.Digest, simB.Digest, $"{stageId} repeat run");
                Assert.AreEqual(simA.Player.X, simB.Player.X, $"{stageId} player X");
                Assert.AreEqual(simA.Player.Y, simB.Player.Y, $"{stageId} player Y");
            }
        }

        // Gate: D3 — mutate ONE hazard datum per new stage via a HackConfig.Hazards
        // override copy: the digest MUST change (placement is live data, not baked).
        // v1.1 recipes (re-probed — the v1.0 pylon/vent recipes went blind: aura 280
        // now covers the centre script's whole reach so a +50 pylon shift changes no
        // multiplier, and both march vents sit inside wall bands no anchored fighter
        // survives):
        //  · sluice — current phase +0.6 shifts the push windows under the script;
        //  · bastion — pillar x +50 moves a hard blocker on the wander path;
        //  · march — wall phase +0.3 (off the 0.6 s grid) moves both fronts under a
        //    corridor-mid bot whose park position IS the published front midpoint.
        [Test]
        public void NewStages_MutatedPlacement_ChangesDigest()
        {
            AssertMutationChangesOutcome(
                CampaignStages.CinderSluice,
                hazards =>
                {
                    for (var i = 0; i < hazards.Length; i++)
                        if (hazards[i].Kind == HazardKind.TideCurrent) { hazards[i].Phase += 0.6f; return; }
                    Assert.Fail("no current on cinder-sluice");
                },
                RunCentreScript);

            AssertMutationChangesOutcome(
                CampaignStages.EmberBastion,
                hazards =>
                {
                    for (var i = 0; i < hazards.Length; i++)
                        if (hazards[i].Kind == HazardKind.ObsidianPillar) { hazards[i].X += 50f; return; }
                    Assert.Fail("no pillar on ember-bastion");
                },
                RunCentreScript);

            AssertMutationChangesOutcome(
                CampaignStages.AshMarch,
                hazards =>
                {
                    for (var i = 0; i < hazards.Length; i++)
                        if (hazards[i].Kind == HazardKind.AshWall) { hazards[i].Phase += 0.3f; return; }
                    Assert.Fail("no wall on ash-march");
                },
                RunCorridorBot);
        }

        delegate void HazardMutation(HazardConfig[] hazards);
        delegate (RunDigest Digest, float X, float Y) BotRun(HackConfig config);

        static void AssertMutationChangesOutcome(string stageId, HazardMutation mutate, BotRun run)
        {
            var baseline = run(Dungeon213(stageId));
            var mutatedConfig = Dungeon213(stageId);
            var mutatedHazards = (HazardConfig[])mutatedConfig.Hazards.Clone();
            mutate(mutatedHazards);
            mutatedConfig.Hazards = mutatedHazards;
            var mutated = run(mutatedConfig);
            var differs = baseline.Digest.Score != mutated.Digest.Score
                || baseline.Digest.Wave != mutated.Digest.Wave
                || baseline.Digest.Kills != mutated.Digest.Kills
                || baseline.Digest.Relics != mutated.Digest.Relics
                || !baseline.Digest.HealthRemaining.Equals(mutated.Digest.HealthRemaining)
                || baseline.Digest.Reason != mutated.Digest.Reason
                || !baseline.X.Equals(mutated.X)
                || !baseline.Y.Equals(mutated.Y);
            Assert.IsTrue(differs, $"{stageId}: a mutated hazard placement must change the run outcome");
        }

        static (RunDigest Digest, float X, float Y) RunCentreScript(HackConfig config)
        {
            var sim = new CinderSim(in config);
            for (var t = 0; t < 1800; t++)
            {
                var input = default(SimInput);
                input.MoveX = t / 120 % 2 == 0 ? 1f : -1f;
                input.MoveY = t / 200 % 2 == 0 ? 0.5f : -0.5f;
                input.AttackQueued = t % 30 == 0;
                input.NovaQueued = t % 400 == 0;
                input.WardQueued = t % 550 == 0;
                sim.Tick(in input);
            }
            return (sim.Digest, sim.Player.X, sim.Player.Y);
        }

        static (RunDigest Digest, float X, float Y) RunCorridorBot(HackConfig config)
        {
            var sim = new CinderSim(in config);
            for (var t = 0; t < 1800 && sim.Mode != SimMode.GameOver; t++)
                sim.Tick(CorridorMidInput(sim));
            return (sim.Digest, sim.Player.X, sim.Player.Y);
        }
    }
}
