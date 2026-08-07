// Campaign amendment gates (docs/SIM_SPEC_CAMPAIGN.md).
// Arena regression is owned by CinderSimTests.cs (20 tests) — untouched.
using System;
using System.IO;
using System.Runtime.CompilerServices;
using CinderCourt.Sim;
using CinderCourt.View;
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
                HackConfig.TryDungeon(stageId, default, EquipTiers.Of(2, 1, 3), (string)null, 0, out var config),
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

        // Gate: R4/G2 — stage table + placement tables per SIM_SPEC_DUNGEONS §Stages/§배치
        // (sluice/bastion v1.1; march v1.2 — REVISION v1.2 adds the finale pylon).
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

            // ash-march v1.2: wall(left,0) · wall(right,11.5) · altar(768,604) ·
            // pylon(768,520) · vent(560,760,0.6) · vent(980,450,1.8) — closing jaws at
            // half-period offset; the finale pylon guards the corridor altar (aura 280
            // covers it at iso 119: wall rhythm + shield war + altar risk converge,
            // campaign-fun-pass-spec.md §8).
            var march = Stage(CampaignStages.AshMarch).Hazards;
            Assert.AreEqual(6, march.Length);
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
            Assert.AreEqual(HazardKind.EmberPylon, march[3].Kind, "v1.2 finale pylon");
            Assert.AreEqual(768f, march[3].X); Assert.AreEqual(520f, march[3].Y);
            Assert.AreEqual(CampaignSpec.PylonHp, march[3].Hp);
            Assert.AreEqual(CampaignSpec.PylonBodyRadius, march[3].Radius);
            Assert.AreEqual(HazardKind.EmberVent, march[4].Kind);
            Assert.AreEqual(560f, march[4].X); Assert.AreEqual(760f, march[4].Y);
            Assert.AreEqual(0.6f, march[4].Phase);
            Assert.AreEqual(HazardKind.EmberVent, march[5].Kind);
            Assert.AreEqual(980f, march[5].X); Assert.AreEqual(450f, march[5].Y);
            Assert.AreEqual(1.8f, march[5].Phase);
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
                CampaignStages.EmberBastion, default, EquipTiers.Of(0, 0, 5), (string)null, 0, out var hackConfig));
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

        // Gate: G2 — ash-wall kinematics + tick discipline (§Gimmick 3, march table
        // v1.2): cycle 23.0 s = rest 4.5 / telegraph 1.5 / advance 7 / hold 3 /
        // recede 7, speed 80; LEFT band grows from x 248, RIGHT wall (phase 11.5)
        // mirrors from x 1288; exact-10 drops ride the 0.6 s global grid; walls
        // alone kill with full credit.
        [Test]
        public void AshWall_TimetableAndTicks()
        {
            // (1) timetable — corridor-mid bot keeps the sim alive (v1.2: the finale
            // pylon shields the centre pack ×0.40, and the plain kiter now dies at
            // t≈16.2 fighting shielded enemies — probed; the corridor bot one-shots
            // arrivals mid-corridor and provably survives past t=23). Wall clocks run
            // on stage time, so the samples are bot-independent. Both walls are
            // sampled at t = 4.5 / 7 / 13.5 / 20 s (ticks 270/420/810/1200).
            var config = Stage(CampaignStages.AshMarch, 5, 5, 5);
            var sim = new CinderSim(in config);
            var leftSamples = new System.Collections.Generic.Dictionary<int, float>();
            var rightSamples = new System.Collections.Generic.Dictionary<int, float>();
            for (var t = 1; t <= 1200; t++)
            {
                Assert.AreNotEqual(SimMode.GameOver, sim.Mode, "5/5/5 corridor bot must survive to t=20");
                sim.Tick(CorridorMidInput(sim));
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
        // same-kind. Anchor windows (v1.1): sluice LCM(6,2.4)=12 s (kiter), bastion
        // vent-only 2.4 s (sampled 3 s, kiter), march LCM(23,2.4)=276 s — swept
        // analytically over the full LCM with a byte-exact mirror of the published
        // telegraph windows, after the mirror is cross-checked tick-for-tick against
        // the live sim for one whole 23 s wall period (a corridor-mid bot provably
        // survives that; no bot is guaranteed 4.6 min under the walls).
        // v1.2 fun-pass catalog overrides (campaign-fun-pass-spec.md 사전 산술):
        // gallery vent-ring LCM 2.4 s (sampled 3 s — same-kind pairs overlap 0.2 s on
        // the 0.6 s ring: max 2) · well LCM 2.4 s (altars never telegraph: max 1) ·
        // throne LCM(6,2.4)=12 s (current tel [0,0.8) meets vent tel: max 2) ·
        // verdict LCM 2.4 s (pylon never telegraphs: max 1). All ≤3 / ≤2. Probed
        // maxima: 2/1/2/1 (gallery/well/throne/verdict), kiter survives every window.
        [Test]
        public void Telegraph_CensusUnderBudget()
        {
            AssertTelegraphCensus(CampaignStages.CinderSluice, 60 * 12, BotInput);
            AssertTelegraphCensus(CampaignStages.EmberBastion, 60 * 3, BotInput);
            AssertTelegraphCensus(CampaignStages.AshMarch, 60 * 23, CorridorMidInput);

            // v1.2 logical stages — catalog override tables, hack lane (the lane that
            // ships them via GameDirector.StartDungeon).
            AssertTelegraphCensus(CatalogStage555("ember-gallery"), "ember-gallery", 60 * 3, BotInput);
            AssertTelegraphCensus(CatalogStage555("witness-well"), "witness-well", 60 * 3, BotInput);
            AssertTelegraphCensus(CatalogStage555("echo-throne"), "echo-throne", 60 * 12, BotInput);
            AssertTelegraphCensus(CatalogStage555("ash-verdict"), "ash-verdict", 60 * 3, BotInput);

            // march full-LCM analytic sweep (mirror validated tick-exactly above).
            AssertAnalyticCensus(Stage(CampaignStages.AshMarch).Hazards, "march", 60 * 276);
        }

        /// <summary>
        /// Full-LCM budget sweep through the analytic telegraph mirror alone —
        /// used where no bot survives the whole window (the mirror is licensed by
        /// the tick-exact cross-check inside AssertTelegraphCensus: same
        /// `+= FixedStep` float accumulation, bit-identical stage clock).
        /// </summary>
        static void AssertAnalyticCensus(HazardConfig[] table, string label, int ticks)
        {
            var maxTotal = 0;
            var maxSameKind = 0;
            var stageClock = 0f;
            var perKind = new System.Collections.Generic.Dictionary<HazardKind, int>();
            for (var t = 1; t <= ticks; t++)
            {
                stageClock += SimConfig.FixedStep;
                var total = 0;
                perKind.Clear();
                foreach (var hazard in table)
                {
                    if (!MirrorTelegraph(hazard, stageClock)) continue;
                    total++;
                    perKind.TryGetValue(hazard.Kind, out var count);
                    perKind[hazard.Kind] = count + 1;
                }
                maxTotal = Math.Max(maxTotal, total);
                foreach (var pair in perKind) maxSameKind = Math.Max(maxSameKind, pair.Value);
            }
            Assert.LessOrEqual(maxTotal, 3, $"{label}: max simultaneous telegraphs (analytic LCM sweep)");
            Assert.LessOrEqual(maxSameKind, 2, $"{label}: max same-kind telegraphs (analytic LCM sweep)");
        }

        delegate SimInput CensusBot(CinderSim sim);

        static void AssertTelegraphCensus(string stageId, int ticks, CensusBot bot)
        {
            var config = Stage(stageId, 5, 5, 5);
            AssertTelegraphCensus(new CinderSim(in config), config.Hazards, stageId, ticks, bot);
        }

        // v1.2 overload — logical catalog stage on the hack lane (override applied
        // exactly like GameDirector.StartDungeon; see CatalogStage555).
        static void AssertTelegraphCensus(HackConfig config, string stageId, int ticks, CensusBot bot)
        {
            AssertTelegraphCensus(new CinderSim(in config), config.Hazards, stageId, ticks, bot);
        }

        static void AssertTelegraphCensus(
            CinderSim sim, HazardConfig[] table, string stageId, int ticks, CensusBot bot)
        {
            // Hazard clocks run on stage time (they tick through intermissions), so a
            // surviving bot samples the full window deterministically. The analytic
            // mirror must agree with every published Telegraphing flag — that is what
            // licenses the mirror-only 276 s march sweep (same float accumulation:
            // stageClock += FixedStep, bit-identical to the sim's stage clock).
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
                    Assert.AreEqual(hazards[i].Telegraphing, MirrorTelegraph(table[i], stageClock),
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
        // The recipes are PROBED, not guessed: a mutation the bot never observes
        // proves nothing, so each one is re-swept whenever bot behaviour moves.
        // v1.1 re-probe: the v1.0 pylon/vent recipes went blind (aura 280 now
        // covers the centre script's whole reach, so a +50 pylon shift changes no
        // multiplier; both march vents sit inside wall bands no anchored fighter
        // survives).
        // v1.4 re-probe (origin/main merge — the input-depth hold-charge landing
        // moved the script's fight, docs/SIM_SPEC_HACKSLASH §3): "first pillar"
        // went blind. Swept all 6 bastion hazards x 6 mutations: the (640,650)
        // pillar sits off the script's oscillation, the (900,560) one is on it
        // (x+50 -> score 2450->3050, kills 10->12 as the blocker clears). Target
        // it by COORDINATE so the recipe cannot silently drift to a blind pillar
        // again if the table is reordered.
        //  · sluice — current phase +0.6 shifts the push windows under the script;
        //  · bastion — the x=900 pillar +50: a hard blocker ON the wander path;
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
                        if (hazards[i].Kind == HazardKind.ObsidianPillar && hazards[i].X == 900f)
                        { hazards[i].X += 50f; return; }
                    Assert.Fail("ember-bastion lost its x=900 pillar — re-probe the recipe "
                        + "(a mutation the bot cannot observe proves nothing)");
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

        // --- cycle-2 v1.2 campaign fun pass (docs/SIM_SPEC_DUNGEONS.md REVISION
        // v1.2 + _workspace/current/design/campaign-fun-pass-spec.md) ------------
        // Every logical stage = one dominant gimmick (preview→mastery lineage).
        // Stages 1/3/4/5 ship as StageCatalog HazardOverride tables (view data,
        // hack lane); stage 8 (ash-march) is the one SIM anchor change (finale
        // pylon 768,520). These tests pin the OVERRIDE tables exactly as
        // GameDirector.StartDungeon runs them: TryDungeon(anchor) + catalog
        // HazardOverride replacing the anchor placement.

        /// <summary>A logical catalog stage on the hack lane, like GameDirector.StartDungeon.</summary>
        static HackConfig CatalogStage(string catalogId, int weapon, int lantern, int cloak)
        {
            Assert.IsTrue(StageCatalog.TryGet(catalogId, out var entry), $"unknown catalog id {catalogId}");
            Assert.IsTrue(
                HackConfig.TryDungeon(entry.SimAnchorId, default, EquipTiers.Of(weapon, lantern, cloak), (string)null, 0, out var config),
                $"unknown anchor {entry.SimAnchorId}");
            if (entry.HazardOverride != null) config.Hazards = entry.HazardOverride;
            return config;
        }

        static HackConfig CatalogStage555(string catalogId) => CatalogStage(catalogId, 5, 5, 5);

        // Gate: G2/D3 (v1.2) — echo-throne "왕좌의 조류" current preview: the weak
        // band (push +120, phase 0.3, halfH 110) covers the central altar; an idle
        // player parked in-band is displaced ONLY across active-interior ticks
        // ([0.8,4.0)+6k local = [0.5,3.7)+6k stage time), never during
        // telegraph/idle — and the push provably interrupts the altar channel:
        // spawn (768,646) is inside altar r70 (iso 59.6), the hold starts
        // immediately, but the active push ejects the player from r70 at t≈0.82 s
        // (probed exit tick 49) BEFORE the 1.2 s hold completes → no blessing in
        // the whole first cycle. Boundary-position channel interruption is
        // flaky-by-geometry (the deadzone bot can out-walk the 120 px/s push at
        // 218 px/s), so per the fun-pass assignment this asserts displacement>0
        // during active + hold interruption instead — both deterministic.
        [Test]
        public void EchoThrone_CurrentPreview_PushesAndInterruptsChannel()
        {
            var config = CatalogStage("echo-throne", 0, 0, 5);
            var current = default(HazardConfig);
            var found = false;
            foreach (var hazard in config.Hazards)
            {
                if (hazard.Kind != HazardKind.TideCurrent) continue;
                current = hazard;
                found = true;
            }
            Assert.IsTrue(found, "echo-throne v1.2 must carry the throne tide current");
            Assert.AreEqual(768f, current.X); Assert.AreEqual(604f, current.Y);
            Assert.AreEqual(120f, current.PushX, "spec: 약류 push +120");
            Assert.AreEqual(0.3f, current.Phase);
            Assert.AreEqual(CampaignSpec.CurrentHalfH, current.HalfH,
                "halfH 110 → band y 494..714 covers the altar (design intent)");

            var sim = new CinderSim(in config);
            var pushedActive = 0;
            var pushedOutside = 0;
            var blessings = 0;
            var exitTick = -1;
            var xPrev = sim.Player.X;
            var prevStageClock = 0f;   // stage clock BEFORE this tick's UpdateHazards
            var stageClock = 0f;
            for (var t = 1; t <= 240; t++)   // one full current cycle (4 s > 0.5+3.2)
            {
                sim.Tick(Idle);
                if ((sim.Events & SimEvents.AltarBlessing) != 0) blessings++;
                // ApplyCurrents reads the PREVIOUS tick's stage clock (1-tick latency
                // is contract). Mirror the sim's float accumulation byte-for-byte
                // (stageClock += FixedStep) — integer-division arithmetic drifts off
                // the sim clock near window boundaries (census mirror precedent).
                prevStageClock = stageClock;
                stageClock += SimConfig.FixedStep;
                var local = (prevStageClock + current.Phase) % CampaignSpec.CurrentPeriod;
                var activePrev = local >= CampaignSpec.CurrentTelegraph
                    && local < CampaignSpec.CurrentTelegraph + CampaignSpec.CurrentActive;
                if (sim.Player.X > xPrev + 1e-6f)
                {
                    if (activePrev) pushedActive++;
                    else pushedOutside++;
                }
                xPrev = sim.Player.X;
                float dx = sim.Player.X - 768f, dy = (sim.Player.Y - 604f) * SimConfig.IsoY;
                if (exitTick < 0 && dx * dx + dy * dy > CampaignSpec.AltarRadius * CampaignSpec.AltarRadius)
                    exitTick = t;
            }
            Assert.GreaterOrEqual(pushedActive, 150, "active window must displace the parked player (+x)");
            Assert.AreEqual(0, pushedOutside, "zero drift outside the active push window");
            Assert.IsTrue(exitTick > 0 && exitTick < 72,
                $"push must eject the player from altar r70 before the 1.2 s hold completes (exit tick {exitTick})");
            Assert.AreEqual(0, blessings,
                "the interrupted channel must NOT bless during the first current cycle");
        }

        // Gate: G2 (v1.2) — the throne altar channel is completable inside the
        // current rest window (local [4.0,6.0), push-free through the following
        // telegraph until [6.8): 2.8 s ≥ 1.2 s hold — the spec's timing puzzle).
        // Bot parks OUT of band (y 790 > band edge 714) through the first active
        // window, walks in when the rest window opens (stage t 3.7 s), holds the
        // altar: blessing fires (probed tick 346 = local 4.07+2.0-0.3) with the
        // current provably inactive and the player inside r70.
        [Test]
        public void EchoThrone_AltarChannel_CompletesInRestWindow()
        {
            var config = CatalogStage("echo-throne", 0, 0, 5);
            var sim = new CinderSim(in config);
            var blessTick = -1;
            var activeAtBless = false;
            var insideAtBless = false;
            for (var t = 1; t <= 480 && blessTick < 0; t++)
            {
                var input = t <= 222   // stage t 3.7 s: rest window opens (0.3 phase)
                    ? HoldPosition(sim, 768f, 790f)    // south of band edge y 714
                    : HoldPosition(sim, 768f, 604f);   // walk onto the altar
                sim.Tick(in input);
                if ((sim.Events & SimEvents.AltarBlessing) == 0) continue;
                blessTick = t;
                var hazards = ((ICampaignSnapshot)sim).Hazards;
                for (var i = 0; i < hazards.Count; i++)
                    if (hazards[i].Kind == HazardKind.TideCurrent)
                        activeAtBless = hazards[i].Active;
                float dx = sim.Player.X - 768f, dy = (sim.Player.Y - 604f) * SimConfig.IsoY;
                insideAtBless = dx * dx + dy * dy <= CampaignSpec.AltarRadius * CampaignSpec.AltarRadius;
            }
            Assert.Greater(blessTick, 222, "blessing must land after the rest-window walk-in");
            Assert.LessOrEqual(blessTick, 408, "blessing must land before the next push window (6.8 s)");
            Assert.IsFalse(activeAtBless, "the completing channel must ride the push-free window");
            Assert.IsTrue(insideAtBless, "the blessing must land while holding altar r70");
        }

        // Gate: G2/D3 (v1.2) — ember-gallery "불씨 윤무" vent ring: 4 vents,
        // clockwise phase lattice 0/0.6/1.2/1.8 on the 2.4 s period around the
        // central pillar. Pulse order over one period is fixed: phase 1.8 wraps
        // first (0.6 s), then 1.2 (1.2 s), 0.6 (1.8 s), 0 (2.4 s) — i.e. table
        // indices 3→2→1→0, one pulse every 0.6 s, exactly 8 over two periods,
        // never two ring vents on the same tick. Census: ≤2 same-kind concurrent
        // telegraphs (adjacent ring phases overlap 0.2 s of the 0.8 s window).
        [Test]
        public void EmberGallery_VentRing_PulsesInPhaseOrder()
        {
            var config = CatalogStage("ember-gallery", 0, 0, 5);
            Assert.AreEqual(5, config.Hazards.Length, "gallery v1.2: 4 ring vents + centre pillar");
            var ringPhases = new[] { 0f, 0.6f, 1.2f, 1.8f };
            for (var i = 0; i < 4; i++)
            {
                Assert.AreEqual(HazardKind.EmberVent, config.Hazards[i].Kind, $"gallery[{i}]");
                Assert.AreEqual(ringPhases[i], config.Hazards[i].Phase, $"gallery[{i}] ring phase");
            }
            Assert.AreEqual(HazardKind.ObsidianPillar, config.Hazards[4].Kind, "gallery[4] centre pillar");

            // Pulse EVENT ticks are the runtime truth; snapshot fmod-wrap and the
            // event floor can disagree by one tick at a cycle boundary, and the
            // exact boundary tick differs between dotnet and Unity float
            // accumulation (~ULP). So assert the LATTICE PATTERN, not tick ids:
            // 4 pulses per 2.4 s period, spaced 36±1 ticks apart.
            var sim = new CinderSim(in config);
            var pulseTicks = new System.Collections.Generic.List<int>();
            for (var t = 1; t <= 289; t++)   // two full periods + boundary slack
            {
                sim.Tick(Idle);
                if ((sim.Events & SimEvents.HazardPulse) != 0)
                {
                    pulseTicks.Add(t);
                }
            }
            Assert.IsTrue(pulseTicks.Count >= 8, $"two periods × 4 ring vents (saw {pulseTicks.Count})");
            for (var p = 1; p < pulseTicks.Count && p < 8; p++)
            {
                var gap = pulseTicks[p] - pulseTicks[p - 1];
                Assert.IsTrue(gap >= 35 && gap <= 37,
                    $"ring pulses ride the 0.6 s lattice ±1 tick (gap {gap} at pulse {p})");
            }
            // Order pin: the snapshot wrap sequence is the phase lattice
            // 1.8→1.2→0.6→0 (vent indices 3→2→1→0). Wrap detection is exact on
            // any runtime; only its alignment with the event tick is not.
            var wrapOrder = new System.Collections.Generic.List<int>();
            var sim2 = new CinderSim(in config);
            var prevCycleT = new float[4];
            var initialized = false;
            for (var t = 1; t <= 289 && wrapOrder.Count < 8; t++)
            {
                sim2.Tick(Idle);
                var hazards = ((ICampaignSnapshot)sim2).Hazards;
                for (var i = 0; i < 4; i++)
                {
                    var cycleT = hazards[i].CycleT;
                    if (initialized && cycleT < prevCycleT[i] && wrapOrder.Count < 8)
                    {
                        wrapOrder.Add(i);
                    }
                    prevCycleT[i] = cycleT;
                }
                initialized = true;
            }
            Assert.AreEqual(8, wrapOrder.Count, "two periods × 4 ring wraps");
            for (var p = 0; p < wrapOrder.Count; p++)
            {
                Assert.AreEqual(new[] { 3, 2, 1, 0 }[p % 4], wrapOrder[p],
                    "ring pulse order is the fixed phase lattice 1.8→1.2→0.6→0");
            }
        }

        // Gate: G2 (v1.2) — ash-verdict "판결의 방벽" pylon preview: pylon
        // (960,540) aura 280 covers the central altar (iso 212.4), so an enemy
        // fought AT the altar is shielded ×0.40 until the pylon falls — then the
        // same gated strike lands raw damage. Campaign lane (basic-attack
        // arithmetic is exact there: weapon-5 swing 75.4), catalog override table
        // applied like the classic-successor route (GameDirectorCampaignRouteTests
        // pattern). Deterministic single-sim arc, no lockstep twin needed.
        [Test]
        public void AshVerdict_PylonAura_ShieldsAltarUntilPylonDown()
        {
            Assert.IsTrue(StageCatalog.TryGet("ash-verdict", out var entry));
            Assert.IsNotNull(entry.HazardOverride, "ash-verdict v1.2 ships an override table");
            var config = Stage(entry.SimAnchorId, weapon: 5, cloak: 5);
            config.Hazards = entry.HazardOverride;

            // Geometry pin first: the altar centre must sit inside the aura
            // (iso 212.4 ≤ 280). The triangle bound alone (altar-park ≤12 px +
            // strike gate 100 → enemy ≤ iso 112 of centre → ≤ 324.4 of the pylon)
            // does NOT prove in-aura, so the shield claim also asserts the
            // MEASURED pylon distance of the actually-struck enemy below.
            float pylonDx = 960f - 768f, pylonDy = (540f - 604f) * SimConfig.IsoY;
            var pylonAltarIso = MathF.Sqrt(pylonDx * pylonDx + pylonDy * pylonDy);
            Assert.Less(pylonAltarIso, CampaignSpec.PylonAuraRadius,
                "verdict pylon aura must cover the altar centre (iso 212.4 ≤ 280)");

            var sim = new CinderSim(in config);
            var shieldedDelta = -1f;
            var unshieldedDelta = -1f;
            var shieldedEnemyPylonIso = -1f;
            var pylonDowns = 0;
            var stage = 0;   // 0 gate-strike shielded → 1 kill pylon → 2 gate-strike raw
            for (var t = 1; t <= 60 * 30 && sim.Mode != SimMode.GameOver; t++)
            {
                SimInput input = default;
                var targetIdx = -1;
                if (stage == 1)
                {
                    // Walk to the pylon and swing until PylonDown (4×75.4 ≥ 300).
                    float dx = 960f - sim.Player.X, dy = 540f - sim.Player.Y;
                    if (dx * dx + dy * dy > 130f * 130f)
                    {
                        var len = MathF.Max(0.001f, MathF.Sqrt(dx * dx + dy * dy));
                        input.MoveX = dx / len; input.MoveY = dy / len;
                    }
                    else if (sim.Player.Facing != 1) input.MoveX = 1f;
                    else input.AttackQueued = true;
                }
                else
                {
                    input = AltarGateStrikeInput(sim, out targetIdx);
                }
                float[] before = null;
                var count = sim.Enemies.Count;
                if (input.AttackQueued && stage != 1)
                {
                    before = new float[count];
                    for (var i = 0; i < count; i++) before[i] = sim.Enemies[i].Health;
                }
                sim.Tick(in input);
                if ((sim.Events & SimEvents.PylonDown) != 0)
                {
                    pylonDowns++;
                    if (stage == 1) stage = 2;
                }
                if (before == null || sim.Enemies.Count != count || targetIdx < 0) continue;
                var delta = before[targetIdx] - sim.Enemies[targetIdx].Health;
                if (delta <= 20f) continue;   // wall/stray ticks are <=10; swings are ≥30.16
                if (stage == 0)
                {
                    shieldedDelta = delta;
                    var enemy = sim.Enemies[targetIdx];
                    float ex = enemy.X - 960f, ey = (enemy.Y - 540f) * SimConfig.IsoY;
                    shieldedEnemyPylonIso = MathF.Sqrt(ex * ex + ey * ey);
                    stage = 1;
                }
                else if (stage == 2)
                {
                    unshieldedDelta = delta;
                    break;
                }
            }
            Assert.AreEqual(config.PlayerDamage * CampaignSpec.PylonAuraDamageTakenMult,
                shieldedDelta, 1e-3f,
                "enemy struck at the altar must take exactly ×0.40 while the pylon lives");
            Assert.Less(shieldedEnemyPylonIso, CampaignSpec.PylonAuraRadius,
                "the shielded strike must land inside the published aura");
            Assert.AreEqual(1, pylonDowns, "the arc must destroy the verdict pylon exactly once");
            Assert.Greater(unshieldedDelta, config.PlayerDamage * CampaignSpec.PylonAuraDamageTakenMult + 1f,
                "after PylonDown the same gated strike must land unshielded damage");
            Assert.LessOrEqual(unshieldedDelta, config.PlayerDamage + 1e-3f,
                "unshielded delta is bounded by the raw swing (kill-clamped when lethal)");
        }

        // Gate: G2 (v1.2) — ash-march finale convergence: the anchor pylon
        // (768,520) aura covers the corridor altar (iso 119.3 ≤ 280), so wall
        // ticks on in-aura in-band enemies drop exactly 10×0.40=4 while raw
        // out-of-aura enemies drop 10 (the shield-war/wall-rhythm/altar-risk
        // braid the spec names). And the pylon BODY (r30) never blocks movement:
        // a player can stand on the altar and walk straight through 768,520.
        [Test]
        public void AshMarch_FinalePylon_ShieldsAltarWithoutBlockingCorridor()
        {
            // (a) shielded wall-tick gradient — idle park at the altar, never
            // attack: enemy health drops come only from wall ticks (right-wall
            // hold covers x>728 during stage t [1.5,4.5)). Wave-1 arrivals inside
            // aura 280 of the pylon drop 4/tick; farther in-band enemies drop 10.
            var config = Stage(CampaignStages.AshMarch, cloak: 5);
            var sim = new CinderSim(in config);
            var shieldedDrops = 0;
            var rawDrops = 0;
            var previousHealth = new System.Collections.Generic.Dictionary<int, float>();
            for (var t = 1; t <= 270; t++)
            {
                sim.Tick(HoldPosition(sim, 768f, 604f));
                for (var i = 0; i < sim.Enemies.Count; i++)
                {
                    var enemy = sim.Enemies[i];
                    if (previousHealth.TryGetValue(enemy.Id, out var was) && !enemy.Dead)
                    {
                        var drop = was - enemy.Health;
                        if (drop > 0.001f)
                        {
                            float dx = enemy.X - 768f, dy = (enemy.Y - 520f) * SimConfig.IsoY;
                            var inAura = dx * dx + dy * dy
                                <= CampaignSpec.PylonAuraRadius * CampaignSpec.PylonAuraRadius;
                            if (inAura)
                            {
                                Assert.AreEqual(
                                    CampaignSpec.WallTickDamage * CampaignSpec.PylonAuraDamageTakenMult,
                                    drop, 1e-3f,
                                    "wall tick on an in-aura enemy must be shielded ×0.40");
                                shieldedDrops++;
                            }
                            else
                            {
                                Assert.AreEqual(CampaignSpec.WallTickDamage, drop, 1e-3f,
                                    "wall tick outside the aura stays raw 10");
                                rawDrops++;
                            }
                        }
                    }
                    previousHealth[enemy.Id] = enemy.Health;
                }
            }
            Assert.GreaterOrEqual(shieldedDrops, 3, "the finale pylon must shield altar-side wall ticks");
            Assert.GreaterOrEqual(rawDrops, 3, "deep-band enemies outside the aura still take raw ticks");

            // (b) corridor invariant — the pylon body never blocks: stand ON the
            // altar under the pylon aura, then walk straight through (768,520).
            Assert.AreEqual(768f, sim.Player.X, 2f, "player must stand at x 768 (altar) under the pylon");
            Assert.AreEqual(604f, sim.Player.Y, 14f, "player must hold the altar y band");
            var through = new CinderSim(in config);
            WalkOnto(through, 768f, 604f, 60 * 4);
            WalkOnto(through, 768f, 470f, 60 * 4);   // path crosses pylon centre (768,520)
            Assert.AreEqual(768f, through.Player.X, 2f, "corridor x must stay walkable through the pylon");
            Assert.Less(through.Player.Y, 520f, "the player must pass THROUGH the pylon body row");
        }

        /// <summary>
        /// Park on the altar (768,604) and swing only when the nearest live enemy
        /// is inside iso 100 and in the facing arc — the struck enemy is then
        /// provably near the altar (contact ring ≈ iso 70). Returns the intended
        /// target index, -1 when this tick cannot be a clean gated strike.
        /// </summary>
        static SimInput AltarGateStrikeInput(CinderSim sim, out int targetIdx)
        {
            targetIdx = -1;
            var input = default(SimInput);
            float px = sim.Player.X, py = sim.Player.Y;
            float dx = 768f - px, dy = 604f - py;
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
                if (d2 < bestD2) { bestD2 = d2; bestDx = ex; targetIdx = i; }
            }
            if (bestD2 >= 100f * 100f)
            {
                targetIdx = -1;   // nothing near the altar yet — wait
                return input;
            }
            if (bestDx * sim.Player.Facing < 0f)
            {
                input.MoveX = bestDx > 0f ? 1f : -1f;   // turn toward the target
                targetIdx = -1;
                return input;
            }
            input.AttackQueued = true;
            return input;
        }

        // --- cycle-2 v1.3 meta fun pass (design/meta-fun-pass-spec.md) ----------
        // M1/M2 show DERIVED REAL NUMBERS in the lobby ("공격력 75.4", not "+3%").
        // Contract (spec §검증): the view never re-implements the formulas — it
        // reads HackConfig.PlayerDamage/PlayerMaxHealth/PlayerSpeed/
        // LanternRegenPerSecond (HackTypes.cs §5/§6 properties, constant-composed).
        // These two tests are the mirror guard: (1) pins the SIM side of the
        // mirror to the closed forms at every reachable meta coordinate, so the
        // properties the lobby prints CANNOT drift from SIM_SPEC_HACKSLASH §5/§6
        // without failing here; (2) pins the VIEW side structurally — LobbyView
        // must contain the property reads and must NOT re-spell the frozen
        // constants as literals.

        // Gate: M1/M2 (v1.3) — closed-form grid: attack 58×(1+0.03a)×(1+0.06w),
        // maxHP 100+8v+8c, speed 218×(1+0.02s), regen 7×(1+0.08l), all stats
        // 0..10 (cap 10) × all ranks 0..5 (cap 5), plus out-of-range clamps.
        // Spec literals appear HERE on purpose: the test is the spec's fixed
        // point — if SimConfig/HackSpec/CampaignSpec constants move, this fails.
        [Test]
        public void DerivedStats_MatchClosedFormOnFullMetaGrid()
        {
            for (var attack = 0; attack <= HackSpec.MaxStatPoints; attack++)
            for (var weapon = 0; weapon <= CampaignSpec.MaxEquipRank; weapon++)
            {
                var config = new HackConfig
                {
                    MetaStats = MetaStats.Of(attack, 0, 0),
                    EquipTiers = EquipTiers.Of(weapon, 0, 0),
                };
                Assert.AreEqual(
                    58f * (1f + 0.03f * attack) * (1f + 0.06f * weapon),
                    config.PlayerDamage, 1e-3f,
                    $"attack a={attack} w={weapon} must be 58×(1+0.03a)×(1+0.06w)");
            }

            for (var vitality = 0; vitality <= HackSpec.MaxStatPoints; vitality++)
            for (var cloak = 0; cloak <= CampaignSpec.MaxEquipRank; cloak++)
            {
                var config = new HackConfig
                {
                    MetaStats = MetaStats.Of(0, vitality, 0),
                    EquipTiers = EquipTiers.Of(0, 0, cloak),
                };
                Assert.AreEqual(
                    100f + 8f * vitality + 8f * cloak,
                    config.PlayerMaxHealth, 1e-3f,
                    $"maxHP v={vitality} c={cloak} must be 100+8v+8c");
            }

            for (var swiftness = 0; swiftness <= HackSpec.MaxStatPoints; swiftness++)
            {
                var config = new HackConfig { MetaStats = MetaStats.Of(0, 0, swiftness) };
                Assert.AreEqual(
                    218f * (1f + 0.02f * swiftness),
                    config.PlayerSpeed, 1e-3f,
                    $"speed s={swiftness} must be 218×(1+0.02s)");
            }

            for (var lantern = 0; lantern <= CampaignSpec.MaxEquipRank; lantern++)
            {
                var config = new HackConfig { EquipTiers = EquipTiers.Of(0, lantern, 0) };
                Assert.AreEqual(
                    7f * (1f + 0.08f * lantern),
                    config.LanternRegenPerSecond, 1e-3f,
                    $"regen l={lantern} must be 7×(1+0.08l)");
            }

            // Out-of-range meta is clamped (stat cap 10, rank cap 5) — the lobby
            // preview at the cap must show the capped number, never an
            // extrapolation.
            var over = new HackConfig
            {
                MetaStats = MetaStats.Of(99, -1, 12),
                EquipTiers = EquipTiers.Of(9, 8, 7),
            };
            Assert.AreEqual(58f * 1.3f * 1.3f, over.PlayerDamage, 1e-3f, "attack clamps at a=10, w=5");
            Assert.AreEqual(100f + 8f * 5, over.PlayerMaxHealth, 1e-3f, "maxHP clamps at v=0, c=5");
            Assert.AreEqual(218f * 1.2f, over.PlayerSpeed, 1e-3f, "speed clamps at s=10");
            Assert.AreEqual(7f * 1.4f, over.LanternRegenPerSecond, 1e-3f, "regen clamps at l=5");

            var under = new HackConfig { EquipTiers = EquipTiers.Of(0, -3, 0) };
            Assert.AreEqual(7f, under.LanternRegenPerSecond, 1e-3f, "regen clamps at l=0 from below");
        }

        // Gate: M1/M2 (v1.3) — the VIEW side of the mirror, reflection-free
        // source assertion: LobbyView reads the four HackConfig derived-stat
        // properties and never re-spells the distinctive frozen constants
        // (58f/218f/0.06f/0.02f/0.08f) as literals. 0.03f/100f/8f/7f are NOT
        // banned — they collide with innocent UI values (e.g. the v1.2 card
        // background alpha 0.03f); their drift is covered by the grid test
        // above plus the required property reads here.
        [Test]
        public void LobbyView_DerivedStatDisplay_ReadsSimPropertiesNotLiterals()
        {
            var source = File.ReadAllText(LobbyViewSourcePath());

            foreach (var required in new[]
                { "PlayerDamage", "PlayerMaxHealth", "PlayerSpeed", "LanternRegenPerSecond" })
            {
                Assert.IsTrue(source.Contains(required),
                    "LobbyView must read HackConfig." + required
                    + " for its derived-stat display (meta-fun-pass-spec M1/M2)");
            }

            foreach (var banned in new[] { "58f", "218f", "0.06f", "0.02f", "0.08f" })
            {
                foreach (var idx in AllIndexesOf(source, banned))
                {
                    Assert.Fail(
                        $"LobbyView re-spells frozen formula constant '{banned}' as a literal "
                        + $"(offset {idx}). Reference SimConfig/HackSpec/CampaignSpec or read the "
                        + "HackConfig derived-stat properties instead (meta-fun-pass-spec §검증).");
                }
            }
        }

        static System.Collections.Generic.IEnumerable<int> AllIndexesOf(string text, string token)
        {
            for (var idx = text.IndexOf(token, StringComparison.Ordinal);
                 idx >= 0;
                 idx = text.IndexOf(token, idx + 1, StringComparison.Ordinal))
            {
                // Skip decimal continuations like 0.58f / 12.06f — only a literal
                // that STARTS with the token spelling counts.
                var before = idx > 0 ? text[idx - 1] : ' ';
                if (char.IsDigit(before) || before == '.') continue;
                yield return idx;
            }
        }

        /// <summary>
        /// Repo-relative LobbyView path resolved from THIS source file's compile-time
        /// location (works under both Unity EditMode and the standalone harness —
        /// no environment variables, no CWD assumptions).
        /// </summary>
        static string LobbyViewSourcePath([CallerFilePath] string thisFile = "")
        {
            var editMode = Path.GetDirectoryName(thisFile);
            var assets = Path.GetDirectoryName(Path.GetDirectoryName(editMode));
            return Path.Combine(assets, "Scripts", "View", "LobbyView.cs");
        }
        /// <summary>
        /// A pact sortie exactly as GameDirector composes it (meta-fun-pass-spec
        /// M3): anchor config on the hack lane + StageCatalog.PactFor(id) as the
        /// hazard table. Pact runs are view-composed configs — catalog-pinned,
        /// never golden-pinned.
        /// </summary>
        static HackConfig PactStage(string catalogId, int weapon, int lantern, int cloak)
        {
            Assert.IsTrue(StageCatalog.TryGet(catalogId, out var entry), $"unknown catalog id {catalogId}");
            Assert.IsTrue(
                HackConfig.TryDungeon(entry.SimAnchorId, default, EquipTiers.Of(weapon, lantern, cloak), (string)null, 0, out var config),
                $"unknown anchor {entry.SimAnchorId}");
            var pact = StageCatalog.PactFor(catalogId);
            Assert.IsNotNull(pact, $"{catalogId}: pact table must exist");
            config.Hazards = pact;
            return config;
        }

        // Gate: D3 (v1.3) — ALL NINE pact tables hold the telegraph budget
        // (≤3 simultaneous, ≤2 same-kind) over one full hazard-clock LCM.
        // Window/bot derivation from the live table (not hardcoded per stage,
        // so a MetaView phase adjustment re-verifies automatically):
        //   wall in table    → 23 s sim window (CorridorMidInput — the only bot
        //                      with a no-escape-proof under closing jaws) + the
        //                      full 276 s LCM(23,6,2.4) via the analytic mirror
        //                      (no bot survives 4.6 min; mirror licensed by the
        //                      tick-exact cross-check inside the sim window);
        //   current in table → 12 s = LCM(6,2.4) kiter window;
        //   else             → 3 s ≥ vent 2.4 s (altars/pylons/pillars never
        //                      telegraph).
        [Test]
        public void Telegraph_PactCensusUnderBudget()
        {
            for (var index = 0; index < StageCatalog.Entries.Count; index += 1)
            {
                var id = StageCatalog.Entries[index].Id;
                var config = PactStage(id, 5, 5, 5);

                var hasWall = false;
                var hasCurrent = false;
                foreach (var hazard in config.Hazards)
                {
                    hasWall |= hazard.Kind == HazardKind.AshWall;
                    hasCurrent |= hazard.Kind == HazardKind.TideCurrent;
                }

                var ticks = hasWall ? 60 * 23 : hasCurrent ? 60 * 12 : 60 * 3;
                var bot = hasWall ? (CensusBot)CorridorMidInput : BotInput;
                AssertTelegraphCensus(new CinderSim(in config), config.Hazards, id + " (pact)", ticks, bot);

                if (hasWall)
                    AssertAnalyticCensus(config.Hazards, id + " (pact)", 60 * 276);
            }
        }

        // Gate: D1 (v1.3) — pact runs stay deterministic: the pact is JUST another
        // fixed placement table (no RNG, spec M3 '결정론 유지'). Same config + same
        // kiter, two fresh sims → identical digest and player position, AND the
        // run is bot-survivable at the golden rank (2/1/3) — the pact sluice
        // gains the stage's identity current, which pushes but never damages, so
        // the v1.1 kiter survival proof must carry over.
        [Test]
        public void PactSluice_SameConfigSameInputs_IdenticalDigests_AndBotSurvivable()
        {
            var configA = PactStage("cinder-sluice", 2, 1, 3);
            var configB = PactStage("cinder-sluice", 2, 1, 3);
            var simA = new CinderSim(in configA);
            var simB = new CinderSim(in configB);
            for (var t = 0; t < 1800; t++) simA.Tick(BotInput(simA));
            for (var t = 0; t < 1800; t++) simB.Tick(BotInput(simB));

            Assert.AreNotEqual(SimMode.GameOver, simA.Mode,
                "pact sluice must stay bot-survivable at 2/1/3 (M3: harder, not lethal-by-default)");
            AssertSameDigest(simA.Digest, simB.Digest, "pact sluice repeat run");
            Assert.AreEqual(simA.Player.X, simB.Player.X, "pact sluice player X");
            Assert.AreEqual(simA.Player.Y, simB.Player.Y, "pact sluice player Y");
        }
    }
}
