// AMENDMENT #6 — 각인 (sigils): meta upgrades that bind to the DUNGEON GIMMICKS
// instead of the stat block. Numeric truth: HackSpec §13 + design/sigil-spec.md.
// Research the numbers answer to: .survey/meta-upgrade-gimmick-interaction/.
//
// Every test here isolates ONE face by running two sims in lockstep — identical
// stage, identical script, differing only in the equipped loadout — so a moved
// number can only come from the sigil. Three properties are pinned per face:
//   (a) the effect happens at all,
//   (b) it moves in the intended DIRECTION,
//   (c) it does NOT cross into immunity (the survey's hard rule).
//
// (c) is the reason several tests assert a lower bound as well as a delta: a
// current that stopped pushing, or a wall tick that stopped hurting, would pass
// a naive "it got better" assertion while deleting the gimmick.
using System;
using System.Collections.Generic;
using CinderCourt.Sim;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class SigilTests
    {
        private const int Fps = 60;

        private static HackConfig Stage(string anchor, SigilLoadout sigils,
                                        int weapon = 2, int lantern = 1, int cloak = 3)
        {
            Assert.IsTrue(
                HackConfig.TryDungeon(anchor, MetaStats.Of(2, 2, 2),
                    EquipTiers.Of(weapon, lantern, cloak), (string)null, 0, out var config),
                $"unknown anchor {anchor}");
            config.Sigils = sigils;
            return config;
        }

        private static SimInput Toward(CinderSim sim, float x, float y)
        {
            var player = sim.Player;
            var dx = x - player.X;
            var dy = y - player.Y;
            var length = MathF.Sqrt(dx * dx + dy * dy);
            if (length < 6f) return default;
            return new SimInput { MoveX = dx / length, MoveY = dy / length };
        }

        private static void Walk(CinderSim sim, float x, float y, int ticks)
        {
            for (var t = 0; t < ticks; t++) sim.Tick(Toward(sim, x, y));
        }

        /// <summary>Spends oil so a refund or a burst lands under the lantern cap
        /// (both would otherwise be invisible at a full 100).</summary>
        private static void BurnOil(CinderSim sim, float x, float y)
        {
            for (var t = 0; t < Fps * 6; t++)
            {
                var input = Toward(sim, x, y);
                input.NovaQueued = t % 30 == 0;
                input.BoltQueued = t % 17 == 0;
                input.PulseQueued = t % 23 == 0;
                sim.Tick(in input);
            }
        }

        private static float TotalPylonHp(CinderSim sim)
        {
            var hazards = ((ICampaignSnapshot)sim).Hazards;
            var total = 0f;
            for (var i = 0; i < hazards.Count; i++)
                if (hazards[i].Kind == HazardKind.EmberPylon) total += hazards[i].Hp;
            return total;
        }

        // =====================================================================
        // The contract that protects everything else: an unequipped run is the
        // pre-amendment run. The 15 golden rows are the deployment-grade proof;
        // this is the cheap one that fails first and names the cause.
        // =====================================================================
        [Test]
        public void NoSigils_IsIdenticalToAnEmptyLoadout()
        {
            foreach (var anchor in new[]
                     {
                         CampaignStages.CinderSpan, CampaignStages.CinderSluice,
                         CampaignStages.EmberBastion, CampaignStages.AshMarch,
                     })
            {
                var bare = new CinderSim(Stage(anchor, default));
                var empty = new CinderSim(Stage(anchor,
                    SigilLoadout.Of(SigilKind.None, SigilFace.A, SigilKind.None, SigilFace.A)));
                for (var t = 0; t < Fps * 20; t++)
                {
                    var input = new SimInput { AttackQueued = true, MoveX = t / 90 % 2 == 0 ? 1f : -1f };
                    bare.Tick(in input);
                    empty.Tick(in input);
                }
                Assert.That(empty.Digest.Score, Is.EqualTo(bare.Digest.Score), anchor + " score");
                Assert.That(empty.Digest.Kills, Is.EqualTo(bare.Digest.Kills), anchor + " kills");
                Assert.That(empty.Digest.HealthRemaining, Is.EqualTo(bare.Digest.HealthRemaining), anchor + " hp");
                Assert.That(empty.Player.X, Is.EqualTo(bare.Player.X), anchor + " x");
                Assert.That(empty.Player.Y, Is.EqualTo(bare.Player.Y), anchor + " y");
            }
        }

        [Test]
        public void EquippedRun_IsDeterministic()
        {
            var loadout = SigilLoadout.Of(SigilKind.Countercurrent, SigilFace.A,
                                          SigilKind.Ignition, SigilFace.B);
            var a = new CinderSim(Stage(CampaignStages.CinderSluice, loadout));
            var b = new CinderSim(Stage(CampaignStages.CinderSluice, loadout));
            for (var t = 0; t < Fps * 30; t++)
            {
                var input = new SimInput { AttackQueued = true, MoveX = t / 70 % 2 == 0 ? 1f : -1f };
                a.Tick(in input);
                b.Tick(in input);
            }
            Assert.That(b.Digest.Score, Is.EqualTo(a.Digest.Score));
            Assert.That(b.Digest.HealthRemaining, Is.EqualTo(a.Digest.HealthRemaining));
            Assert.That(b.Player.X, Is.EqualTo(a.Player.X));
            Assert.That(b.Player.Y, Is.EqualTo(a.Player.Y));
        }

        // =====================================================================
        // 역류인 — tide-current
        // =====================================================================
        [Test]
        public void CountercurrentA_HalvesThePushOnThePlayer_ButNeverCancelsIt()
        {
            float Drift(SigilLoadout loadout)
            {
                var sim = new CinderSim(Stage(CampaignStages.CinderSluice, loadout));
                Walk(sim, 600f, 470f, Fps * 4);          // park inside the north lane
                var start = sim.Player.X;
                for (var t = 0; t < Fps * 6; t++) sim.Tick(default);   // one full 6 s cycle
                return sim.Player.X - start;
            }

            var off = Drift(default);
            var on = Drift(SigilLoadout.One(SigilKind.Countercurrent, SigilFace.A));

            Assert.That(off, Is.GreaterThan(100f), "baseline: the lane must actually shove");
            Assert.That(on, Is.LessThan(off * 0.75f),
                $"역류인 A must materially cut the shove (off {off:0.#} -> on {on:0.#})");
            // The no-immunity line: halved is not cancelled.
            Assert.That(on, Is.GreaterThan(50f),
                $"역류인 A must NOT grant immunity — the current still displaces (on {on:0.#})");
        }

        [Test]
        public void CountercurrentB_PushesEnemiesHarder_WithoutTouchingThePlayer()
        {
            (float PlayerX, float EnemyMeanX) Run(SigilLoadout loadout)
            {
                var sim = new CinderSim(Stage(CampaignStages.CinderSluice, loadout));
                for (var t = 0; t < Fps * 12; t++) sim.Tick(Toward(sim, 300f, 604f));
                var sum = 0f;
                var live = 0;
                foreach (var enemy in sim.Enemies)
                {
                    if (enemy.Dead) continue;
                    sum += enemy.X;
                    live += 1;
                }
                return (sim.Player.X, live == 0 ? 0f : sum / live);
            }

            var off = Run(default);
            var on = Run(SigilLoadout.One(SigilKind.Countercurrent, SigilFace.B));

            Assert.That(on.EnemyMeanX, Is.Not.EqualTo(off.EnemyMeanX),
                "역류인 B must move where the current leaves the pack");
            Assert.That(on.PlayerX, Is.EqualTo(off.PlayerX),
                "역류인 B is the ENEMY face — the player's own drift must not change");
        }

        // =====================================================================
        // 판결인 — ember-pylon
        // =====================================================================
        [Test]
        public void VerdictA_ThinsTheShield_ButLeavesItStanding()
        {
            Assert.That(HackSpec.SigilPylonAuraRelief,
                Is.GreaterThan(CampaignSpec.PylonAuraDamageTakenMult),
                "판결인 A must RAISE the damage-taken multiplier (thinner shield)");
            Assert.That(HackSpec.SigilPylonAuraRelief, Is.LessThan(1f),
                "판결인 A must NOT remove the shield — that would delete the gimmick");

            int Kills(SigilLoadout loadout)
            {
                var sim = new CinderSim(Stage(CampaignStages.EmberBastion, loadout));
                for (var t = 0; t < Fps * 25; t++)
                    sim.Tick(new SimInput { AttackQueued = true, MoveX = t / 90 % 2 == 0 ? 1f : -1f });
                return sim.Digest.Kills;
            }

            Assert.That(Kills(SigilLoadout.One(SigilKind.Verdict, SigilFace.A)),
                Is.GreaterThan(Kills(default)),
                "a thinner shield must convert into more kills over the same script");
        }

        [Test]
        public void VerdictB_DoublesWhatASwingTakesOffAPylon()
        {
            float Removed(SigilLoadout loadout)
            {
                var sim = new CinderSim(Stage(CampaignStages.EmberBastion, loadout));
                Walk(sim, 430f, 500f, Fps * 4);          // west of pylon(560,500)
                var before = TotalPylonHp(sim);
                // Hold east so Facing stays +1 and the body sits inside the arc.
                for (var t = 0; t < Fps * 8; t++)
                    sim.Tick(new SimInput { MoveX = 1f, AttackQueued = true });
                return before - TotalPylonHp(sim);
            }

            var off = Removed(default);
            var on = Removed(SigilLoadout.One(SigilKind.Verdict, SigilFace.B));
            Assert.That(off, Is.GreaterThan(0f), "baseline: the script must actually hit a pylon");
            Assert.That(on, Is.GreaterThan(off),
                $"판결인 B must strip more pylon hp per swing (off {off:0} -> on {on:0})");
        }

        // =====================================================================
        // 집행인 — ash-wall
        // =====================================================================
        [Test]
        public void ExecutionerA_SoftensTheWallTick_ButTheWallStillHurts()
        {
            Assert.That(HackSpec.SigilWallPlayerTick, Is.LessThan(CampaignSpec.WallTickDamage));
            Assert.That(HackSpec.SigilWallPlayerTick, Is.GreaterThan(0f),
                "집행인 A must never zero the tick — the wall has to keep owning the space");

            float Lost(SigilLoadout loadout)
            {
                var sim = new CinderSim(Stage(CampaignStages.AshMarch, loadout, 5, 5, 5));
                Walk(sim, 1150f, 604f, Fps);             // deep in the right band before the hold
                var before = sim.Digest.HealthRemaining;
                Walk(sim, 1150f, 604f, Fps * 3);
                return before - sim.Digest.HealthRemaining;
            }

            var off = Lost(default);
            var on = Lost(SigilLoadout.One(SigilKind.Executioner, SigilFace.A));
            Assert.That(off, Is.GreaterThan(0f), "baseline: the band must actually tick");
            Assert.That(on, Is.LessThan(off), $"집행인 A must reduce the loss (off {off:0.#} -> on {on:0.#})");
            Assert.That(on, Is.GreaterThan(0f),
                "집행인 A must NOT make the band free to stand in");
        }

        [Test]
        public void ExecutionerB_MakesTheWallHitEnemiesHarder()
        {
            // Accumulate every per-enemy hp DROP, keyed by id, so respawns and
            // wave scaling cannot launder the measurement into a wash.
            float AccumulatedEnemyDamage(SigilLoadout loadout)
            {
                var sim = new CinderSim(Stage(CampaignStages.AshMarch, loadout, 5, 5, 5));
                var last = new Dictionary<int, float>();
                var total = 0f;
                for (var t = 0; t < Fps * 25 && sim.Mode != SimMode.GameOver; t++)
                {
                    sim.Tick(Toward(sim, 320f, 604f));   // bait west, never attack
                    foreach (var enemy in sim.Enemies)
                    {
                        if (last.TryGetValue(enemy.Id, out var previous) && enemy.Health < previous)
                            total += previous - enemy.Health;
                        last[enemy.Id] = enemy.Health;
                    }
                }
                return total;
            }

            var off = AccumulatedEnemyDamage(default);
            var on = AccumulatedEnemyDamage(SigilLoadout.One(SigilKind.Executioner, SigilFace.B));
            Assert.That(off, Is.GreaterThan(0f), "baseline: the wall must already bite the pack");
            Assert.That(on, Is.GreaterThan(off),
                $"집행인 B must raise environmental damage (off {off:0.#} -> on {on:0.#})");
        }

        // =====================================================================
        // 점화인 — ember-vent
        // =====================================================================
        [Test]
        public void IgnitionA_PaysOilForTheVentHit_WithoutSofteningIt()
        {
            // Measured on the HIT TICK only. Two earlier drafts failed here and
            // both failures were the probe, not the code:
            //   · a windowed net gain washes out — lantern regen refills to the
            //     100 cap either way;
            //   · the biggest jump in the window is an oil FLASK pickup (41 in
            //     the baseline), not the refund.
            // Ticks where health actually dropped are vent ticks, so summing the
            // oil delta across exactly those isolates the refund from both.
            (float Lost, float OilOnHitTicks, int Hits) Run(SigilLoadout loadout)
            {
                var sim = new CinderSim(Stage(CampaignStages.CinderSpan, loadout));
                BurnOil(sim, 560f, 480f);                // stand on vent(560,480), spend oil
                var hp = sim.Digest.HealthRemaining;
                var oilOnHits = 0f;
                var hits = 0;
                for (var t = 0; t < Fps * 8; t++)
                {
                    var oilBefore = sim.Charge;
                    var hpBefore = sim.Digest.HealthRemaining;
                    sim.Tick(Toward(sim, 560f, 480f));
                    if (sim.Digest.HealthRemaining >= hpBefore) continue;
                    oilOnHits += sim.Charge - oilBefore;
                    hits += 1;
                }
                return (hp - sim.Digest.HealthRemaining, oilOnHits, hits);
            }

            var off = Run(default);
            var on = Run(SigilLoadout.One(SigilKind.Ignition, SigilFace.A));

            Assert.That(off.Lost, Is.GreaterThan(0f), "baseline: the vent must actually connect");
            Assert.That(off.Hits, Is.GreaterThan(0), "baseline: at least one damaging tick");
            // The whole point of this face: the pain is UNCHANGED, only paid for.
            Assert.That(on.Lost, Is.EqualTo(off.Lost),
                "점화인 A must not reduce vent damage by one point — it buys resource, not safety");
            Assert.That(on.Hits, Is.EqualTo(off.Hits),
                "the two runs must take the same number of hits for the oil comparison to mean anything");
            // Bound at half the refund, not the whole of it: the lantern cap
            // clips whatever part of a 12-oil refund would cross 100, so the
            // banked total is legitimately short of 12 per hit. Regen over one
            // tick is ~0.12, so half a refund is still three orders away from
            // anything the baseline can produce (measured: off 0.39, on 10.55).
            Assert.That(on.OilOnHitTicks - off.OilOnHitTicks,
                Is.GreaterThanOrEqualTo(HackSpec.SigilVentOilRefund * 0.5f),
                $"점화인 A must refund oil ON the damaging tick (off {off.OilOnHitTicks:0.##} "
                + $"-> on {on.OilOnHitTicks:0.##} across {on.Hits} hit(s))");
        }

        [Test]
        public void IgnitionB_OptsVentsIntoTheSymmetricDoctrine()
        {
            // Damage accumulated, not kills counted: whether a vent tick finishes
            // an enemy is a knife-edge that differs between runtimes (the dotnet
            // probe killed 4, Unity killed 0 on the same script). The CONTRACT is
            // "vents may damage enemies at all", so that is what gets asserted.
            float VentDamageToEnemies(SigilLoadout loadout)
            {
                var sim = new CinderSim(Stage(CampaignStages.CinderSpan, loadout, 5, 5, 5));
                var last = new Dictionary<int, float>();
                var total = 0f;
                // Park ON the vent so chasers stack onto the same disc, and NEVER
                // attack: any enemy hp drop can only have come from the vent.
                for (var t = 0; t < Fps * 40 && sim.Mode != SimMode.GameOver; t++)
                {
                    sim.Tick(Toward(sim, 560f, 480f));
                    foreach (var enemy in sim.Enemies)
                    {
                        if (last.TryGetValue(enemy.Id, out var previous) && enemy.Health < previous)
                            total += previous - enemy.Health;
                        last[enemy.Id] = enemy.Health;
                    }
                }
                return total;
            }

            Assert.That(VentDamageToEnemies(default), Is.Zero,
                "default doctrine: vents are player-only risk (SIM_SPEC_CAMPAIGN) — "
                + "a non-zero here means something else is damaging the pack and the "
                + "probe is no longer isolating the vent");
            Assert.That(VentDamageToEnemies(SigilLoadout.One(SigilKind.Ignition, SigilFace.B)),
                Is.GreaterThan(0f),
                "점화인 B must let the vent bite the pack — the opt-in half of the symmetry rule");
        }

        // =====================================================================
        // 증언인 — relic-altar
        // =====================================================================
        [Test]
        public void WitnessA_ShortensTheChannel_ButKeepsAWindow()
        {
            Assert.That(HackSpec.SigilAltarHoldSeconds, Is.LessThan(CampaignSpec.AltarHoldSeconds));
            Assert.That(HackSpec.SigilAltarHoldSeconds, Is.GreaterThan(0f),
                "증언인 A must keep a real channel — an instant altar deletes the risk");

            int FirstBlessingTick(SigilLoadout loadout)
            {
                var sim = new CinderSim(Stage(CampaignStages.EchoThrone, loadout));
                BurnOil(sim, 768f, 604f);
                for (var t = 0; t < Fps * 3; t++)
                {
                    sim.Tick(Toward(sim, 768f, 604f));
                    if ((sim.Events & SimEvents.AltarBlessing) != 0) return t;
                }
                return int.MaxValue;
            }

            var off = FirstBlessingTick(default);
            var on = FirstBlessingTick(SigilLoadout.One(SigilKind.Witness, SigilFace.A));
            Assert.That(off, Is.LessThan(int.MaxValue), "baseline: the altar must bless within the window");
            Assert.That(on, Is.LessThan(off),
                $"증언인 A must bless sooner (off tick {off} -> on tick {on})");
        }

        [Test]
        public void WitnessB_PaysMoreOilForTheSameChannel()
        {
            Assert.That(HackSpec.SigilAltarOilBurst, Is.GreaterThan(CampaignSpec.AltarOilBurst));

            (float Gain, int Tick) Run(SigilLoadout loadout)
            {
                var sim = new CinderSim(Stage(CampaignStages.EchoThrone, loadout));
                BurnOil(sim, 768f, 604f);
                var oil = sim.Charge;
                var blessed = int.MaxValue;
                for (var t = 0; t < Fps * 3; t++)
                {
                    sim.Tick(Toward(sim, 768f, 604f));
                    if (blessed == int.MaxValue && (sim.Events & SimEvents.AltarBlessing) != 0) blessed = t;
                }
                return (sim.Charge - oil, blessed);
            }

            var off = Run(default);
            var on = Run(SigilLoadout.One(SigilKind.Witness, SigilFace.B));
            Assert.That(on.Tick, Is.EqualTo(off.Tick),
                "증언인 B is the PAYOUT face — the channel length must not move");
        }

        // =====================================================================
        // Loadout plumbing
        // =====================================================================
        [Test]
        public void Loadout_HasMatchesOnlyTheEquippedFace()
        {
            var loadout = SigilLoadout.Of(SigilKind.Verdict, SigilFace.B,
                                          SigilKind.Witness, SigilFace.A);
            Assert.IsTrue(loadout.Has(SigilKind.Verdict, SigilFace.B));
            Assert.IsFalse(loadout.Has(SigilKind.Verdict, SigilFace.A), "the other face is NOT equipped");
            Assert.IsTrue(loadout.Has(SigilKind.Witness, SigilFace.A));
            Assert.IsFalse(loadout.Has(SigilKind.Executioner, SigilFace.A), "unequipped kinds never match");
            Assert.IsFalse(default(SigilLoadout).Has(SigilKind.None, SigilFace.A),
                "None must never resolve as equipped, or an empty loadout would apply effects");
        }

        [Test]
        public void Sigils_AreDungeonOnly()
        {
            // Arena and prologue configs carry no loadout field usage; a run built
            // from them must keep the untouched constants even if a loadout leaks in.
            var arena = HackConfig.Arena();
            arena.Sigils = SigilLoadout.One(SigilKind.Executioner, SigilFace.A);
            var prologue = HackConfig.Prologue();
            prologue.Sigils = SigilLoadout.One(SigilKind.Executioner, SigilFace.A);

            var arenaSim = new CinderSim(in arena);
            var plainArena = new CinderSim(HackConfig.Arena());
            var prologueSim = new CinderSim(in prologue);
            var plainPrologue = new CinderSim(HackConfig.Prologue());
            for (var t = 0; t < Fps * 20; t++)
            {
                var input = new SimInput { AttackQueued = true, MoveX = t / 80 % 2 == 0 ? 1f : -1f };
                arenaSim.Tick(in input);
                plainArena.Tick(in input);
                prologueSim.Tick(in input);
                plainPrologue.Tick(in input);
            }
            Assert.That(arenaSim.Digest.HealthRemaining, Is.EqualTo(plainArena.Digest.HealthRemaining),
                "an arena run must ignore sigils entirely");
            Assert.That(prologueSim.Digest.HealthRemaining, Is.EqualTo(plainPrologue.Digest.HealthRemaining),
                "a prologue run must ignore sigils entirely");
        }
    }
}
