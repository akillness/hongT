// Drop-economy behavior tests (2026-08-07 audit M9, LootDrops T-1/T-2).
// Numeric truth: docs/SIM_SPEC.md §pickups (EmberShardHeal 18, OilFlaskCharge 35)
// and docs/SIM_SPEC_CAMPAIGN.md (EquipShard collect -> RaiseRank(kills % 3)).
//
// PURE SIM FILE — CinderCourt.Sim + NUnit only, no UnityEngine/View. Every
// scenario is deterministic (the sim has no RNG): landmarks below (idle tick
// counts, survivor enemy id, drop distances) were measured against this build
// the same way CinderSimTests.cs pins ContactTick/IdleFirstDamageTick.
//
// The kiter bot here intentionally duplicates CampaignSimTests.BotInput: that
// file is View-coupled (CinderCourt.View using), and this file must stay loadable
// in a sim-only harness. The "do not fork" rule on BotInput binds golden-digest
// replication (DungeonGoldenDigestTests), not this file's slot-mapping walk.
using System;
using CinderCourt.Sim;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class LootEconomyTests
    {
        private const float Tolerance = 1e-4f;
        private const float Step = SimConfig.FixedStep;

        // CinderSimTests landmark: 4 wave-1 enemies alive, 3 inside the nova ring.
        private const int ContactTick = 200;

        private static readonly SimInput Idle = default;

        // --- shared scenario steps --------------------------------------------

        /// <summary>Idle to ContactTick, then nova: kills ids 1/2/4, leaves id 3
        /// (3 % 3 == 0 -> its eventual drop is the EmberShard). One OilFlask is
        /// magnet-collected on the cast tick; a RelicMote + OilFlask stay grounded.</summary>
        private static CinderSim ArenaAfterFirstNova()
        {
            var sim = new CinderSim();
            for (int tick = 0; tick < ContactTick; tick += 1)
            {
                sim.Tick(Idle);
            }
            sim.Tick(new SimInput { NovaQueued = true });
            Assert.That(sim.Kills, Is.EqualTo(3), "scenario expects three nova kills");
            Assert.That(sim.LivingEnemies, Is.EqualTo(1), "scenario expects one survivor");
            return sim;
        }

        private static int FindPickup(CinderSim sim, PickupKind kind)
        {
            for (int index = 0; index < sim.Pickups.Count; index += 1)
            {
                if (sim.Pickups[index].Kind == kind)
                {
                    return sim.Pickups[index].Id;
                }
            }
            return -1;
        }

        private static bool TryGetPickup(CinderSim sim, int id, out PickupState state)
        {
            for (int index = 0; index < sim.Pickups.Count; index += 1)
            {
                if (sim.Pickups[index].Id == id)
                {
                    state = sim.Pickups[index];
                    return true;
                }
            }
            state = default;
            return false;
        }

        /// <summary>Walk straight at the pickup until the magnet eats it. Returns
        /// the snapshot taken immediately before the collecting tick.</summary>
        private static (float Health, float Charge) WalkToCollect(CinderSim sim, int pickupId)
        {
            for (int tick = 0; tick < 300; tick += 1)
            {
                Assert.That(TryGetPickup(sim, pickupId, out PickupState target), Is.True,
                    "pickup vanished without the player reaching it");

                float healthBefore = sim.Player.Health;
                float chargeBefore = sim.Charge;
                float toX = target.X - sim.Player.X;
                float toY = target.Y - sim.Player.Y;
                float length = MathF.Max(0.001f, MathF.Sqrt(toX * toX + toY * toY));
                sim.Tick(new SimInput { MoveX = toX / length, MoveY = toY / length });

                if (!TryGetPickup(sim, pickupId, out _))
                {
                    Assert.That(sim.Events & SimEvents.PickupCollected,
                        Is.EqualTo(SimEvents.PickupCollected));
                    return (healthBefore, chargeBefore);
                }
            }
            Assert.Fail("player never reached the pickup");
            return default;
        }

        // --- T-1: EmberShard (+18 HP) ------------------------------------------

        [Test]
        public void EmberShard_HealsExactly18_WhenBelowMax()
        {
            // Regression caught: EmberShard heal amount drifting off +18, or the
            // heal silently not applying on a magnet-collect tick.
            var sim = ArenaAfterFirstNova();

            // Survivor id 3 beats on the idle player through the 6.5 s nova
            // cooldown, so the heal has real headroom (measured: 93 -> 72 HP).
            int waited = 0;
            while (sim.NovaCooldown > 0f && waited < 800)
            {
                sim.Tick(Idle);
                waited += 1;
            }
            Assert.That(sim.NovaCooldown, Is.LessThanOrEqualTo(0f), "nova never recharged");
            Assert.That(sim.Player.Health,
                Is.LessThan(SimConfig.PlayerMaxHealth - SimConfig.EmberShardHeal),
                "scenario needs enough missing HP that the +18 cannot clamp");

            // Second nova kills id 3 in melee range; its EmberShard spawns inside
            // the 78 px magnet radius and is collected on the very same tick.
            float healthBefore = sim.Player.Health;
            sim.Tick(new SimInput { NovaQueued = true });

            Assert.That(sim.Events & SimEvents.EnemyKilled, Is.EqualTo(SimEvents.EnemyKilled));
            Assert.That(sim.Events & SimEvents.PickupCollected, Is.EqualTo(SimEvents.PickupCollected));
            Assert.That(sim.Player.Health,
                Is.EqualTo(healthBefore + SimConfig.EmberShardHeal).Within(Tolerance),
                "EmberShard must heal exactly +18");
        }

        [Test]
        public void EmberShard_ClampsAtMaxHealth()
        {
            // Regression caught: the heal overshooting PlayerMaxHealth (clamp lost).
            var sim = ArenaAfterFirstNova();

            // Melee the survivor down instead of waiting: the player keeps most
            // HP (measured 93), so 93 + 18 = 111 must clamp to 100.
            int killsBefore = sim.Kills;
            for (int tick = 0; tick < 600 && sim.Kills == killsBefore; tick += 1)
            {
                sim.Tick(new SimInput { AttackQueued = true });
            }
            Assert.That(sim.Kills, Is.EqualTo(killsBefore + 1), "survivor was never slain");
            Assert.That(sim.Player.Health,
                Is.GreaterThan(SimConfig.PlayerMaxHealth - SimConfig.EmberShardHeal),
                "scenario needs the +18 to overflow max HP, otherwise the clamp is untested");

            int shardId = FindPickup(sim, PickupKind.EmberShard);
            Assert.That(shardId, Is.GreaterThan(0), "id-3 kill must drop an EmberShard (3 % 3 == 0)");

            WalkToCollect(sim, shardId);
            Assert.That(sim.Player.Health, Is.EqualTo(SimConfig.PlayerMaxHealth),
                "overhealed EmberShard must clamp to PlayerMaxHealth exactly");
        }

        // --- T-1: OilFlask (+35 oil) --------------------------------------------

        [Test]
        public void OilFlask_Charges35_WhenBelowCap()
        {
            // Regression caught: OilFlask charge amount drifting off +35, or the
            // pickup crediting the wrong resource.
            var sim = ArenaAfterFirstNova();
            int flaskId = FindPickup(sim, PickupKind.OilFlask);
            Assert.That(flaskId, Is.GreaterThan(0), "an OilFlask must remain grounded after the nova");

            // Hold position near the flask until the nova cooldown ends. Oil
            // regenerates to full; the flask (12 s life) outlives the 6.5 s wait.
            int waited = 0;
            while (sim.NovaCooldown > 0f && waited < 800)
            {
                Assert.That(TryGetPickup(sim, flaskId, out PickupState flask), Is.True,
                    "flask expired during the cooldown wait");
                float offsetX = flask.X - sim.Player.X;
                float offsetY = flask.Y - sim.Player.Y;
                float iso = MathF.Sqrt(offsetX * offsetX
                    + offsetY * SimConfig.IsoY * (offsetY * SimConfig.IsoY));
                var park = default(SimInput);
                float length = MathF.Max(0.001f, MathF.Sqrt(offsetX * offsetX + offsetY * offsetY));
                if (iso > 130f)
                {
                    park.MoveX = offsetX / length;
                    park.MoveY = offsetY / length;
                }
                else if (iso < 95f)
                {
                    park.MoveX = -offsetX / length;
                    park.MoveY = -offsetY / length;
                }
                sim.Tick(park);
                waited += 1;
            }
            Assert.That(sim.NovaCooldown, Is.LessThanOrEqualTo(0f), "nova never recharged");

            // Burn 45 oil so the +35 has headroom (measured: 100 -> ~61.1).
            sim.Tick(new SimInput { NovaQueued = true });
            Assert.That(sim.Charge,
                Is.LessThan(SimConfig.LanternMax - SimConfig.OilFlaskCharge),
                "scenario needs enough missing oil that the +35 cannot clamp");

            (_, float chargeBefore) = WalkToCollect(sim, flaskId);
            float regenPerTick = SimConfig.LanternRegenPerSecond * Step;
            Assert.That(sim.Charge,
                Is.EqualTo(chargeBefore + regenPerTick + SimConfig.OilFlaskCharge).Within(Tolerance),
                "OilFlask must add exactly +35 on top of that tick's passive regen");
        }

        [Test]
        public void OilFlask_ClampsAtLanternMax()
        {
            // Regression caught: the flask overshooting the 100-oil cap.
            var sim = ArenaAfterFirstNova();
            int flaskId = FindPickup(sim, PickupKind.OilFlask);
            Assert.That(flaskId, Is.GreaterThan(0), "an OilFlask must remain grounded after the nova");

            // Walk straight over: 3 kills refunded +18 oil on the cast tick, so
            // the lantern is already back at (or within 35 of) the cap.
            (_, float chargeBefore) = WalkToCollect(sim, flaskId);
            Assert.That(chargeBefore + SimConfig.OilFlaskCharge,
                Is.GreaterThan(SimConfig.LanternMax),
                "scenario needs the +35 to overflow the cap, otherwise the clamp is untested");
            Assert.That(sim.Charge, Is.EqualTo(SimConfig.LanternMax),
                "overfilled OilFlask must clamp to LanternMax exactly");
        }

        // --- T-2: EquipShard collect -> kills % 3 slot ----------------------------

        [Test]
        public void EquipShard_Collect_RaisesTheKillCountSlot()
        {
            // Regression caught: the collect-time slot formula drifting off
            // RaiseRank(kills % 3) — e.g. rotating on enemy id or stage index —
            // and shard collects that raise more/less than exactly one rank.
            //
            // cinder-span at 0/0/4 with the kiter bot is fully deterministic and
            // (measured) collects shards at kills 10, 17, 24, 31 -> slots 1, 2, 0, 1,
            // covering every slot before the boss dies.
            Assert.That(CampaignStages.TryGet(CampaignStages.CinderSpan, 0, 0, 4, out var config),
                Is.True);
            var sim = new CinderSim(in config);
            var snapshot = (ICampaignSnapshot)sim;

            int previousWeapon = snapshot.WeaponRank;
            int previousLantern = snapshot.LanternRank;
            int previousCloak = snapshot.CloakRank;
            var slotSeen = new bool[CampaignSpec.EquipSlotCount];
            int shardCollects = 0;

            for (int tick = 0; tick < 60 * 300; tick += 1)
            {
                sim.Tick(Bot(sim));
                SimEvents events = sim.Events;

                if ((events & SimEvents.EquipDropped) != 0
                    && (events & SimEvents.StageCleared) == 0)
                {
                    // Boss-clear grants route through stageIndex % 3 instead and are
                    // owned by CampaignSimTests.StageClear_GrantsBossDrop_SlotByStageIndex.
                    int weaponDelta = snapshot.WeaponRank - previousWeapon;
                    int lanternDelta = snapshot.LanternRank - previousLantern;
                    int cloakDelta = snapshot.CloakRank - previousCloak;
                    Assert.That(weaponDelta + lanternDelta + cloakDelta, Is.EqualTo(1),
                        "one shard collect must raise exactly one rank");

                    int raisedSlot = weaponDelta == 1 ? 0 : lanternDelta == 1 ? 1 : 2;
                    Assert.That(raisedSlot, Is.EqualTo(sim.Kills % CampaignSpec.EquipSlotCount),
                        $"shard collect #{shardCollects} landed off the kills % 3 slot");
                    slotSeen[raisedSlot] = true;
                    shardCollects += 1;
                }

                previousWeapon = snapshot.WeaponRank;
                previousLantern = snapshot.LanternRank;
                previousCloak = snapshot.CloakRank;

                if ((events & SimEvents.StageCleared) != 0)
                {
                    break;
                }
                Assert.That(sim.Mode, Is.Not.EqualTo(SimMode.GameOver),
                    "bot died before the slot walk finished");
            }

            Assert.That(shardCollects, Is.GreaterThanOrEqualTo(3), "expected at least three shard collects");
            Assert.That(slotSeen[0], Is.True, "no shard collect ever mapped to the weapon slot");
            Assert.That(slotSeen[1], Is.True, "no shard collect ever mapped to the lantern slot");
            Assert.That(slotSeen[2], Is.True, "no shard collect ever mapped to the cloak slot");
        }

        [Test]
        public void EquipShard_AtRankFive_ClampsWithoutOverflow()
        {
            // Regression caught: a shard collected at max rank overflowing past 5.
            Assert.That(CampaignStages.TryGet(CampaignStages.CinderSpan, 5, 5, 5, out var config),
                Is.True);
            var sim = new CinderSim(in config);
            var snapshot = (ICampaignSnapshot)sim;

            bool sawShardAtCap = false;
            for (int tick = 0; tick < 60 * 300; tick += 1)
            {
                sim.Tick(Bot(sim));
                SimEvents events = sim.Events;
                if ((events & SimEvents.EquipDropped) != 0
                    && (events & SimEvents.StageCleared) == 0)
                {
                    sawShardAtCap = true;
                    Assert.That(snapshot.WeaponRank, Is.EqualTo(CampaignSpec.MaxEquipRank));
                    Assert.That(snapshot.LanternRank, Is.EqualTo(CampaignSpec.MaxEquipRank));
                    Assert.That(snapshot.CloakRank, Is.EqualTo(CampaignSpec.MaxEquipRank));
                }
                if ((events & SimEvents.StageCleared) != 0 || sim.Mode == SimMode.GameOver)
                {
                    break;
                }
            }
            Assert.That(sawShardAtCap, Is.True, "the 5/5/5 run never collected a shard");
        }

        // --- local kiter (pure-assembly twin of CampaignSimTests.BotInput) --------

        private static SimInput Bot(CinderSim sim)
        {
            float playerX = sim.Player.X, playerY = sim.Player.Y;
            float bestDistanceSq = float.MaxValue, offsetX = 0f, offsetY = 0f;
            var enemies = sim.Enemies;
            for (int index = 0; index < enemies.Count; index += 1)
            {
                EnemyState enemy = enemies[index];
                if (enemy.Dead)
                {
                    continue;
                }
                float toX = enemy.X - playerX, toY = (enemy.Y - playerY) * SimConfig.IsoY;
                float distanceSq = toX * toX + toY * toY;
                if (distanceSq < bestDistanceSq)
                {
                    bestDistanceSq = distanceSq;
                    offsetX = enemy.X - playerX;
                    offsetY = enemy.Y - playerY;
                }
            }
            var input = new SimInput { AttackQueued = true, NovaQueued = true, WardQueued = true };
            if (bestDistanceSq < float.MaxValue)
            {
                float distance = MathF.Sqrt(bestDistanceSq);
                float length = MathF.Max(0.001f, MathF.Sqrt(offsetX * offsetX + offsetY * offsetY));
                if (distance < 120f)
                {
                    input.MoveX = -offsetX / length;
                    input.MoveY = -offsetY / length;
                }
                else if (distance > 150f)
                {
                    input.MoveX = offsetX / length;
                    input.MoveY = offsetY / length;
                }
            }
            return input;
        }
    }
}
