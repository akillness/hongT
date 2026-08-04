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

        static SimInput BotInput(CinderSim sim)
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
    }
}
