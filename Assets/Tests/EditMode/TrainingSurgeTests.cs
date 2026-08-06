// AMENDMENT #7 — 훈련장 (training ground) + 돌발 (surge) + 각인 서지 조항.
// Numeric truth: HackSpec §14/§15 + design/training-and-surge-spec.md.
// Research the numbers answer to: .survey/roguelike-training-and-surge/.
//
// THE CENTRAL INVARIANT, and the reason most of this file exists: a surge
// window is SIM STATE ONLY. It opens, it publishes, it closes — and by itself
// it changes no number. Every mechanical consequence is owned by an equipped
// sigil clause. The spec records this as the second of two corrections a probe
// forced (§3.3): the draft slowed the hazard clock as a BASE effect of peril,
// which fired on plain unequipped runs and would have moved all 15 golden
// digests. So the first tests here are negative ones — they assert that
// nothing happened.
//
// Isolating a clause needs care, because most sigils also carry a permanent
// effect that shifts the whole trajectory. Two constructions do the work:
//
//   · SLOT-ORDER SWAP. Peril clauses do not stack — only the sigil in the
//     LOWER slot fires (SigilLoadout.PerilPriority). So the SAME two sigils in
//     the opposite order give two runs with an identical permanent kit that
//     differ ONLY in which peril clause is live. Both runs then open peril on
//     the same tick, and any divergence after it is the clause.
//   · WITHIN-RUN REGIME SPLIT. For surge clauses the equipped sigil changes
//     the run's trajectory, so two sims cannot be compared tick-for-tick.
//     Instead one sim supplies both regimes: the same hazard's damage before
//     the window and inside it.
//
// Where a number is measured rather than derived, it was confirmed against the
// standalone dotnet harness before being written down, and the assertion still
// spells it out of a HackSpec/CampaignSpec constant — a literal here is drift.
using System;
using System.Collections.Generic;
using CinderCourt.Sim;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class TrainingSurgeTests
    {
        private const int Fps = 60;

        // Wave 1 contact damage is 7 and the wall tick is 10, so a drop of
        // exactly WallTickDamage in wave 1 can only be the wall. Every census
        // below leans on that separation instead of a tolerance.
        private static HackConfig Dungeon(string anchor, SigilLoadout sigils,
                                          int attack = 2, int vitality = 2, int swiftness = 2,
                                          int weapon = 2, int lantern = 1, int cloak = 3)
        {
            Assert.IsTrue(
                HackConfig.TryDungeon(anchor, MetaStats.Of(attack, vitality, swiftness),
                    EquipTiers.Of(weapon, lantern, cloak), null, 0, out var config),
                $"unknown anchor {anchor}");
            config.Sigils = sigils;
            return config;
        }

        private static HackConfig Trial(string trialId, int tier)
        {
            Assert.IsTrue(
                HackConfig.TryTraining(trialId, tier, MetaStats.Of(2, 2, 2), EquipTiers.Of(2, 1, 3), out var config),
                $"unknown trial {trialId} tier {tier}");
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

        /// <summary>The house script from SigilTests: swings and shuffles. It is
        /// the only script here that reliably banks 12 kills, so every surge test
        /// is built on it.</summary>
        private static SimInput Shuffle(int tick)
            => new SimInput { AttackQueued = true, MoveX = tick / 70 % 2 == 0 ? 1f : -1f };

        /// <summary>Max health at this instant. Not on the snapshot, so it is
        /// rebuilt from the config and the level — both public.</summary>
        private static float MaxHealth(in HackConfig config, CinderSim sim)
            => config.PlayerMaxHealth + HackSpec.LevelHealthBonus * (sim.Level - 1);

        /// <summary>Walks to the midpoint between the two live wall fronts. The
        /// wall trial kills a parked player in 16 s, and a trial you cannot
        /// survive teaches nothing — this is the pilot the trial is designed for.</summary>
        private static SimInput WallDodge(CinderSim sim)
        {
            var leftFront = CampaignSpec.WallEdgeX;
            var rightFront = CampaignSpec.WallEdgeRightX;
            var hazards = ((ICampaignSnapshot)sim).Hazards;
            for (var i = 0; i < hazards.Count; i++)
            {
                if (hazards[i].Kind != HazardKind.AshWall) continue;
                if (hazards[i].X == CampaignSpec.WallEdgeX)
                    leftFront = MathF.Max(leftFront, hazards[i].FrontX);
                else
                    rightFront = MathF.Min(rightFront, hazards[i].FrontX);
            }
            var target = (leftFront + rightFront) * 0.5f;
            var dx = target - sim.Player.X;
            var input = default(SimInput);
            if (MathF.Abs(dx) > 4f) input.MoveX = dx > 0f ? 1f : -1f;
            return input;
        }

        /// <summary>Hp drops on LIVE enemies that survived the whole tick. A
        /// corpse keeps reporting its death drop every frame, and a killing blow
        /// reports a partial — both would smear the histogram this file reads.</summary>
        private static void CollectSurvivorDrops(
            CinderSim sim, Dictionary<int, float> previous, List<float> into,
            float auraX = float.NaN, float auraY = float.NaN)
        {
            var aura = !float.IsNaN(auraX);
            foreach (var enemy in sim.Enemies)
            {
                if (enemy.Dead || enemy.Health <= 0f) continue;
                if (!previous.TryGetValue(enemy.Id, out var before) || enemy.Health >= before) continue;
                if (aura && !IsoWithin(auraX, auraY, enemy.X, enemy.Y, CampaignSpec.PylonAuraRadius)) continue;
                into.Add(before - enemy.Health);
            }
        }

        private static void Snapshot(CinderSim sim, Dictionary<int, float> into)
        {
            into.Clear();
            foreach (var enemy in sim.Enemies)
                if (!enemy.Dead) into[enemy.Id] = enemy.Health;
        }

        private static bool IsoWithin(float centerX, float centerY, float x, float y, float radius)
        {
            var deltaX = x - centerX;
            var deltaY = (y - centerY) * SimConfig.IsoY;
            return deltaX * deltaX + deltaY * deltaY <= radius * radius;
        }

        private static bool Contains(List<float> values, float wanted)
        {
            for (var i = 0; i < values.Count; i++)
                if (MathF.Abs(values[i] - wanted) < 1e-3f) return true;
            return false;
        }

        private static string Describe(List<float> values)
        {
            var counts = new SortedDictionary<string, int>();
            for (var i = 0; i < values.Count; i++)
            {
                var key = values[i].ToString("0.###");
                counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;
            }
            var parts = new List<string>();
            foreach (var pair in counts) parts.Add($"{pair.Key}x{pair.Value}");
            return parts.Count == 0 ? "(none)" : string.Join(" ", parts);
        }

        /// <summary>Runs the house script until a surge window opens, then takes
        /// HANDS OFF for the rest of the window. With no input, the only thing
        /// that can damage an enemy is a hazard — which is the whole point.</summary>
        private static void SurgeWindowCensus(
            in HackConfig config, out List<float> insideWindow, out List<float> afterWindow, out int openTick)
        {
            var sim = new CinderSim(in config);
            var previous = new Dictionary<int, float>();
            insideWindow = new List<float>();
            afterWindow = new List<float>();
            openTick = -1;
            var closeTick = -1;
            var windowTicks = (int)MathF.Ceiling(HackSpec.SurgeSeconds * Fps);
            for (var t = 0; t < Fps * 300 && sim.Mode != SimMode.GameOver; t++)
            {
                Snapshot(sim, previous);
                var wasOpen = sim.SurgeRemaining > 0f;
                sim.Tick(openTick < 0 ? Shuffle(t) : default);
                if (openTick < 0)
                {
                    if ((sim.Events & SimEvents.SurgeOpened) != 0) openTick = t;
                    continue;
                }
                if (closeTick < 0 && sim.SurgeRemaining <= 0f) closeTick = t;
                if (wasOpen) CollectSurvivorDrops(sim, previous, insideWindow);
                else if (closeTick > 0 && t <= closeTick + windowTicks)
                    CollectSurvivorDrops(sim, previous, afterWindow);
                if (closeTick > 0 && t > closeTick + windowTicks) break;
            }
        }

        // =====================================================================
        // The central invariant: a window on its own is inert.
        // =====================================================================

        /// <summary>
        /// An unequipped run OPENS a peril window and the wall goes on hitting for
        /// the full tick through it. This is the cheap proof of the §3.3
        /// correction — the version that shipped first slowed the clock here, and
        /// every golden row would have moved.
        /// </summary>
        [Test]
        public void PlainRun_PerilWindowChangesNothing_TheWallStillTicksInFull()
        {
            var config = Dungeon(CampaignStages.AshMarch, default);
            config.Hazards = new[] { HazardConfig.Wall(0f) };   // one wall: nothing else can tick
            var sim = new CinderSim(in config);
            var dropsInWindow = new List<float>();
            var openTick = -1;
            var waveAtFullTick = 0;

            for (var t = 0; t < Fps * 120 && sim.Mode != SimMode.GameOver; t++)
            {
                var healthBefore = sim.Player.Health;
                var wasOpen = sim.PerilRemaining > 0f;
                sim.Tick(Toward(sim, 280f, 604f));              // park deep in the left band
                if (openTick < 0 && (sim.Events & SimEvents.PerilOpened) != 0) openTick = t;
                if (!wasOpen && sim.PerilRemaining <= 0f) continue;
                if (sim.Player.Health >= healthBefore) continue;
                var drop = healthBefore - sim.Player.Health;
                dropsInWindow.Add(drop);
                if (MathF.Abs(drop - CampaignSpec.WallTickDamage) < 1e-3f) waveAtFullTick = sim.Wave;
            }

            Assert.That(openTick, Is.GreaterThan(0),
                "baseline: the parked script must actually drive health under the peril line");
            Assert.That(sim.PerilUsed, Is.EqualTo(1), "exactly one window is under test here");
            Assert.That(Contains(dropsInWindow, CampaignSpec.WallTickDamage), Is.True,
                "돌발은 심 상태일 뿐 — an unequipped peril window must leave the wall tick at its "
                + $"full {CampaignSpec.WallTickDamage}. Drops seen inside the window: {Describe(dropsInWindow)}");
            Assert.That(waveAtFullTick, Is.EqualTo(1),
                "the full tick must be observed in wave 1, where contact damage (7) cannot be "
                + $"confused with the wall tick ({CampaignSpec.WallTickDamage})");
            Assert.That(Contains(dropsInWindow, CampaignSpec.WallTickDamage * 0.5f), Is.False,
                "a halved tick without 집행인 equipped would mean the window is paying out on its own");
        }

        /// <summary>
        /// Enemy-facing hazard damage is the other half of the same promise: a
        /// surge window with nothing equipped must not multiply it.
        /// </summary>
        [Test]
        public void PlainRun_SurgeWindowDoesNotRaiseHazardDamageAgainstEnemies()
        {
            var config = Dungeon(CampaignStages.AshMarch, default, 10, 10, 10, 5, 5, 5);
            config.Hazards = new[]
            {
                HazardConfig.Wall(0f),
                HazardConfig.Wall(CampaignSpec.WallPeriod * 0.5f, fromRight: true),
            };
            SurgeWindowCensus(in config, out var inside, out var after, out var openTick);

            Assert.That(openTick, Is.GreaterThan(0), "baseline: the script must open a surge window");
            Assert.That(inside, Is.Not.Empty, "baseline: the wall must tick an enemy inside the window");
            Assert.That(Contains(inside, CampaignSpec.WallTickDamage), Is.True,
                "무장착이면 서지는 상태 표시만 — the enemy-side wall tick must stay at "
                + $"{CampaignSpec.WallTickDamage} inside the window. Seen: {Describe(inside)}");
            Assert.That(Contains(inside, CampaignSpec.WallTickDamage * HackSpec.SurgeEnemyHazardMult), Is.False,
                $"the ×{HackSpec.SurgeEnemyHazardMult} surge multiplier is NOT a base effect — it belongs "
                + "to 점화인, and a plain run applying it moves all 15 golden digests");
            Assert.That(Contains(inside, CampaignSpec.WallTickDamage * HackSpec.SigilSurgeEnemyHazardMult), Is.False,
                $"nor the ×{HackSpec.SigilSurgeEnemyHazardMult} sigil multiplier. Seen: {Describe(inside)}");
        }

        /// <summary>
        /// Surge is dungeon-only. A trial is where the gimmick is learned unaided.
        ///
        /// A passive trial never loses health, so "no window opened" would be true
        /// of a sim with the subsystem fully enabled — vacuous. This script parks
        /// ON the trial's own vent instead, which walks health from full to zero
        /// straight through the 35% line: the exact input that MUST open a peril
        /// window in a dungeon, and must open nothing here.
        /// </summary>
        [Test]
        public void Trials_NeverOpenASurgeOrPerilWindow_EvenWhileHealthCrossesTheLine()
        {
            var crossedSomewhere = false;
            foreach (var trialId in TrainingTrials.Ids)
            {
                for (var tier = 0; tier < HackSpec.TrainingTiers; tier++)
                {
                    var config = Trial(trialId, tier);
                    var sim = new CinderSim(in config);
                    var maxHealth = config.PlayerMaxHealth;

                    // Stand on the trial's first vent if it has one; the wall trial
                    // is dodged (a parked player dies before it can teach anything)
                    // and the rest have no damaging gimmick at the spawn point.
                    var targetX = SimConfig.ArenaX;
                    var targetY = SimConfig.ArenaY;
                    var hasVent = false;
                    foreach (var hazard in config.Hazards)
                    {
                        if (hazard.Kind != HazardKind.EmberVent) continue;
                        targetX = hazard.X;
                        targetY = hazard.Y;
                        hasVent = true;
                        break;
                    }

                    var perilOpens = 0;
                    var surgeOpens = 0;
                    var lowestFraction = 1f;
                    for (var t = 0; t < Fps * 90 && sim.Mode != SimMode.GameOver; t++)
                    {
                        sim.Tick(trialId == TrainingTrials.Wall ? WallDodge(sim) : Toward(sim, targetX, targetY));
                        if ((sim.Events & SimEvents.PerilOpened) != 0) perilOpens += 1;
                        if ((sim.Events & SimEvents.SurgeOpened) != 0) surgeOpens += 1;
                        var fraction = sim.Player.Health / maxHealth;
                        if (fraction < lowestFraction) lowestFraction = fraction;
                    }

                    if (lowestFraction < HackSpec.PerilHealthFraction) crossedSomewhere = true;
                    Assert.That(surgeOpens, Is.Zero,
                        $"{trialId} tier {tier}: 돌발은 던전 전용 — a trial must never surge");
                    Assert.That(perilOpens, Is.Zero,
                        $"{trialId} tier {tier}: a trial must never open a peril window, and this run "
                        + $"took health down to {lowestFraction:0.###} of max — under the "
                        + $"{HackSpec.PerilHealthFraction} line that opens one in a dungeon");
                    Assert.That(sim.SurgeRemaining, Is.Zero, $"{trialId} tier {tier}: surge timer must stay closed");
                    Assert.That(sim.PerilRemaining, Is.Zero, $"{trialId} tier {tier}: peril timer must stay closed");
                    Assert.That(sim.PerilUsed, Is.Zero,
                        $"{trialId} tier {tier}: a trial must not spend a run's peril budget");
                    if (hasVent)
                        Assert.That(sim.TrainingHits, Is.GreaterThan(0),
                            $"{trialId} tier {tier}: setup — parking on the vent must actually register hits, "
                            + "or this run proves nothing about a trial under pressure");
                }
            }
            Assert.That(crossedSomewhere, Is.True,
                $"setup: at least one trial script must drive health under {HackSpec.PerilHealthFraction}, "
                + "or every assertion above passes for a sim that simply never took damage");
        }

        /// <summary>
        /// The other half of "dungeon-only": the arena, the prologue and the
        /// classic campaign constructor all bank kills and lose health, so they
        /// would trip BOTH doors if the gate ever widened. The arena run below
        /// reaches 69 kills — five crossings of the surge interval — and bottoms
        /// out at zero health, and must still publish nothing.
        /// </summary>
        [Test]
        public void NonDungeonModes_NeverOpenAWindow_ThoughTheyCrossBothTriggers()
        {
            void Check(string label, CinderSim sim, bool expectKillCrossing)
            {
                var perilOpens = 0;
                var surgeOpens = 0;
                var lowestFraction = 1f;
                for (var t = 0; t < Fps * 400 && sim.Mode != SimMode.GameOver; t++)
                {
                    sim.Tick(Shuffle(t));
                    if ((sim.Events & SimEvents.PerilOpened) != 0) perilOpens += 1;
                    if ((sim.Events & SimEvents.SurgeOpened) != 0) surgeOpens += 1;
                    var fraction = sim.Player.Health / SimConfig.PlayerMaxHealth;
                    if (fraction < lowestFraction) lowestFraction = fraction;
                }

                if (expectKillCrossing)
                {
                    Assert.That(sim.Digest.Kills, Is.GreaterThanOrEqualTo(HackSpec.SurgeKillInterval),
                        $"{label}: setup — the run must cross the {HackSpec.SurgeKillInterval}-kill door "
                        + "for its silence to mean anything");
                    Assert.That(lowestFraction, Is.LessThan(HackSpec.PerilHealthFraction),
                        $"{label}: setup — the run must also cross the {HackSpec.PerilHealthFraction} "
                        + $"health door (bottomed at {lowestFraction:0.###})");
                }
                Assert.That(surgeOpens, Is.Zero,
                    $"{label}: surge is dungeon-only — {sim.Digest.Kills} kills must open no window here");
                Assert.That(perilOpens, Is.Zero,
                    $"{label}: peril is dungeon-only — bottoming at {lowestFraction:0.###} of max health "
                    + "must open no window here");
                Assert.That(sim.PerilUsed, Is.Zero, $"{label}: nor spend any budget");
            }

            Check("arena", new CinderSim(), true);
            Check("prologue", new CinderSim(HackConfig.Prologue()), false);
            Check("classic campaign", new CinderSim(CampaignStages.ForIndex(0, 5, 5, 5)), false);
        }

        // =====================================================================
        // Peril trigger semantics
        // =====================================================================

        /// <summary>
        /// QA plan item T2.4 — the skip-the-phase failure. Health does not land on
        /// 35%; it steps across it. A threshold test written as an equality, or as
        /// "the previous tick was inside a band", opens zero windows on this run.
        /// </summary>
        [Test]
        public void Peril_OpensOnceWhenOneTickSkipsStraightPastTheThreshold()
        {
            var config = Dungeon(CampaignStages.AshMarch, default);
            config.Hazards = new[] { HazardConfig.Wall(0f) };
            var sim = new CinderSim(in config);
            var opens = 0;
            var fractionBefore = -1f;
            var fractionAfter = -1f;
            // Every fraction the run ever observed, so "no tick stood on the
            // line" is a measurement rather than a margin I picked by hand.
            var everObserved = new List<float>();

            for (var t = 0; t < Fps * 120 && sim.Mode != SimMode.GameOver; t++)
            {
                var healthBefore = sim.Player.Health;
                var maxBefore = MaxHealth(in config, sim);
                everObserved.Add(healthBefore / maxBefore);
                sim.Tick(Toward(sim, 280f, 604f));
                if ((sim.Events & SimEvents.PerilOpened) == 0) continue;
                opens += 1;
                if (opens > 1) continue;
                fractionBefore = healthBefore / maxBefore;
                fractionAfter = sim.Player.Health / MaxHealth(in config, sim);
            }

            Assert.That(opens, Is.EqualTo(1), "the script must open exactly one window over its life");
            Assert.That(fractionBefore, Is.GreaterThan(HackSpec.PerilHealthFraction),
                $"setup: the tick before the open must still be above {HackSpec.PerilHealthFraction} "
                + $"(measured {fractionBefore:0.####})");
            Assert.That(fractionAfter, Is.LessThan(HackSpec.PerilHealthFraction),
                $"setup: the opening tick must land below it (measured {fractionAfter:0.####})");

            // The gap IS the test: one tick carried health from above the line to
            // below it, and no tick in the whole run ever occupied the span in
            // between. A rule written as "health == 35%", or as "the previous tick
            // sat inside a band just above the line", sees nothing here and opens
            // zero windows. Only "it crossed" fires.
            var landedInTheGap = 0;
            for (var i = 0; i < everObserved.Count; i++)
                if (everObserved[i] < fractionBefore && everObserved[i] > fractionAfter) landedInTheGap += 1;
            Assert.That(landedInTheGap, Is.Zero,
                $"setup: the step from {fractionBefore:0.####} to {fractionAfter:0.####} must be a single "
                + "tick with nothing observed in between");
            Assert.That(fractionAfter, Is.LessThan(HackSpec.PerilHealthFraction).And
                .LessThan(fractionBefore - (fractionBefore - HackSpec.PerilHealthFraction)),
                $"T2.4: the threshold must be SKIPPED, not touched — health stepped "
                + $"{fractionBefore:0.####} -> {fractionAfter:0.####} straight over the line at "
                + $"{HackSpec.PerilHealthFraction}, landing further below it than it started above it, "
                + "and one window must still open");
        }

        /// <summary>
        /// Hysteresis. Health dips under 35%, recovers part-way, and dips again
        /// without ever reaching 50% — the second dip must open nothing, or a
        /// player hovering on the line farms windows.
        /// </summary>
        [Test]
        public void Peril_DoesNotChainWindowsWhileHoveringBelowTheRearmLine()
        {
            var config = Dungeon(CampaignStages.CinderSpan, default, 10, 3, 10, 5, 5, 5);
            config.Hazards = new[] { HazardConfig.Vent(560f, 480f, 0f) };
            var sim = new CinderSim(in config);
            var onVent = true;
            var opens = 0;
            var entriesBelow = 0;
            var below = false;
            var peakAfterFirstOpen = 0f;

            for (var t = 0; t < Fps * 400 && sim.Mode != SimMode.GameOver; t++)
            {
                var max = MaxHealth(in config, sim);
                var fraction = sim.Player.Health / max;
                if (onVent && fraction < 0.28f) onVent = false;
                else if (!onVent && fraction > 0.62f) onVent = true;

                SimInput input;
                if (onVent)
                {
                    input = Toward(sim, 560f, 480f);
                }
                else
                {
                    var best = float.MaxValue;
                    float bestX = 300f, bestY = 780f;
                    foreach (var pickup in sim.Pickups)
                    {
                        if (pickup.Kind != PickupKind.EmberShard) continue;
                        var dx = pickup.X - sim.Player.X;
                        var dy = (pickup.Y - sim.Player.Y) * SimConfig.IsoY;
                        var distance = MathF.Sqrt(dx * dx + dy * dy);
                        if (distance < best) { best = distance; bestX = pickup.X; bestY = pickup.Y; }
                    }
                    input = Toward(sim, bestX, bestY);
                }
                input.AttackQueued = true;
                sim.Tick(in input);

                var fractionNow = sim.Player.Health / MaxHealth(in config, sim);
                var nowBelow = fractionNow < HackSpec.PerilHealthFraction;
                if (nowBelow && !below) entriesBelow += 1;
                below = nowBelow;
                if (opens > 0 && fractionNow > peakAfterFirstOpen) peakAfterFirstOpen = fractionNow;
                if ((sim.Events & SimEvents.PerilOpened) != 0) opens += 1;
            }

            Assert.That(entriesBelow, Is.GreaterThanOrEqualTo(2),
                "baseline: the bob script must cross under the peril line more than once");
            Assert.That(peakAfterFirstOpen, Is.LessThan(HackSpec.PerilRearmFraction),
                $"baseline: this run must never recover past {HackSpec.PerilRearmFraction} — "
                + $"the hysteresis latch is only under test while it does not (peak {peakAfterFirstOpen:0.###})");
            Assert.That(opens, Is.EqualTo(1),
                $"히스테리시스: {entriesBelow} crossings under {HackSpec.PerilHealthFraction} produced "
                + $"{opens} window(s). Without a recovery past {HackSpec.PerilRearmFraction} the latch stays "
                + "spent, so a player bobbing on the line cannot farm windows");
        }

        /// <summary>The other half of hysteresis: a run that DOES recover past 50%
        /// re-arms, and only then can a second window open.</summary>
        [Test]
        public void Peril_RearmsOnlyAfterHealthRecoversPastTheRearmLine()
        {
            var config = Dungeon(CampaignStages.CinderSpan, default, 10, 4, 10, 5, 5, 5);
            var sim = new CinderSim(in config);
            var fleeing = false;
            var openTicks = new List<int>();
            var peakBetween = 0f;

            for (var t = 0; t < Fps * 300 && sim.Mode != SimMode.GameOver; t++)
            {
                var max = MaxHealth(in config, sim);
                sim.Tick(Kite(sim, t, max, 0.40f, 0.65f, ref fleeing));
                var fraction = sim.Player.Health / MaxHealth(in config, sim);
                if (openTicks.Count == 1 && fraction > peakBetween) peakBetween = fraction;
                if ((sim.Events & SimEvents.PerilOpened) != 0) openTicks.Add(t);
            }

            Assert.That(openTicks.Count, Is.EqualTo(2),
                "baseline: the kiting script must open a second window for this test to mean anything");
            Assert.That(peakBetween, Is.GreaterThanOrEqualTo(HackSpec.PerilRearmFraction),
                $"a second window may only follow a recovery past {HackSpec.PerilRearmFraction} — "
                + $"measured peak between the two windows was {peakBetween:0.####}");
            Assert.That(sim.PerilUsed, Is.EqualTo(openTicks.Count),
                "PerilUsed must count exactly the windows that opened");
        }

        /// <summary>Run cap. Nine waves at one window per wave would be ten
        /// comeback grants — negotiation entry 8 caps the RUN at two.</summary>
        [Test]
        public void Peril_NeverSpendsMoreThanTheRunCap()
        {
            var anchors = new[]
            {
                CampaignStages.CinderSpan, CampaignStages.CinderSluice,
                CampaignStages.EmberBastion, CampaignStages.AshMarch, CampaignStages.EchoThrone,
            };
            var reachedTheCap = false;
            foreach (var anchor in anchors)
            {
                foreach (var vitality in new[] { 0, 4, 10 })
                {
                    var config = Dungeon(anchor, default, 10, vitality, 10, 5, 5, 5);
                    var sim = new CinderSim(in config);
                    var fleeing = false;
                    var opens = 0;
                    for (var t = 0; t < Fps * 300 && sim.Mode != SimMode.GameOver; t++)
                    {
                        sim.Tick(Kite(sim, t, MaxHealth(in config, sim), 0.40f, 0.65f, ref fleeing));
                        if ((sim.Events & SimEvents.PerilOpened) != 0) opens += 1;
                        Assert.That(sim.PerilUsed, Is.LessThanOrEqualTo(HackSpec.PerilRunCap),
                            $"{anchor} vit{vitality} t{t}: 런당 위기 발동 총 {HackSpec.PerilRunCap}회 — "
                            + "an uncapped comeback supply is the reversal the director's arithmetic refused");
                    }
                    Assert.That(opens, Is.EqualTo(sim.PerilUsed),
                        $"{anchor} vit{vitality}: every PerilOpened event must be one spent budget slot");
                    if (sim.PerilUsed == HackSpec.PerilRunCap) reachedTheCap = true;
                }
            }
            Assert.That(reachedTheCap, Is.True,
                $"baseline: at least one scripted run must actually reach the cap of {HackSpec.PerilRunCap}, "
                + "or the ceiling above is vacuous");
        }

        /// <summary>
        /// 사망 틱에서는 발동하지 않는다 — a window opened on the tick the player
        /// dies is a comeback nobody comes back from. The guard is
        /// <c>_player.Health &gt; 0f</c>, so the checkable form is: every window
        /// that ever opens, opens on a tick where the player is still alive.
        ///
        /// Honest limit: no scripted run reaches the discriminating case, which
        /// is a death on the FIRST crossing (health above the line, then 0 on one
        /// tick, latch still armed). The largest single hit in the sim is boss
        /// contact at 36 against a 100 hp floor, and bosses arrive at wave 5 by
        /// which time the latch is long spent — a 324-config sweep found none.
        /// So this pins the invariant on every window the run DOES open rather
        /// than on a death tick it cannot manufacture.
        /// </summary>
        [Test]
        public void Peril_OnlyEverOpensWhileThePlayerIsStillAlive()
        {
            var anchors = new[]
            {
                CampaignStages.CinderSpan, CampaignStages.CinderSluice,
                CampaignStages.EmberBastion, CampaignStages.AshMarch,
            };
            var deathsObserved = 0;
            var windowsObserved = 0;
            foreach (var anchor in anchors)
            {
                var config = Dungeon(anchor, default);
                var sim = new CinderSim(in config);
                for (var t = 0; t < Fps * 200; t++)
                {
                    sim.Tick(Shuffle(t));
                    if ((sim.Events & SimEvents.PerilOpened) != 0)
                    {
                        windowsObserved += 1;
                        Assert.That(sim.Player.Health, Is.GreaterThan(0f),
                            $"{anchor} t{t}: a peril window may only open on a tick the player survived — "
                            + "a comeback granted to a corpse is not a comeback");
                        Assert.That((sim.Events & SimEvents.GameOver) != 0, Is.False,
                            $"{anchor} t{t}: and never on the death tick itself");
                    }
                    if ((sim.Events & SimEvents.GameOver) != 0) deathsObserved += 1;
                    if (sim.Mode == SimMode.GameOver) break;
                }
                Assert.That(sim.PerilUsed, Is.LessThanOrEqualTo(HackSpec.PerilRunCap),
                    $"{anchor}: the run cap holds through death as well");
            }
            Assert.That(windowsObserved, Is.GreaterThan(0),
                "baseline: the scripts must open at least one window, or the guard above is never tested");
            Assert.That(deathsObserved, Is.GreaterThan(0),
                "baseline: at least one scripted run must actually die, or the death half proves nothing");
        }

        // =====================================================================
        // Surge trigger semantics
        // =====================================================================

        /// <summary>
        /// REGRESSION. The first implementation tested `_kills % 12 == 0` and a
        /// measured run reached 14 kills while opening ZERO windows: a nova kills
        /// several enemies on one tick, so the counter steps over the boundary and
        /// an exact-multiple test never sees it.
        ///
        /// The script below is the pinned counter-example — kills step 9 -> 13 on
        /// one tick, straight over 12. It must open exactly ONE window: not zero
        /// (the modulo bug) and not a backlog of them (a while-loop would grant
        /// one per boundary passed).
        /// </summary>
        [Test]
        public void Surge_OpensExactlyOnceWhenOneTickJumpsOverAKillBoundary()
        {
            var config = Dungeon(CampaignStages.CinderSpan, default, 0, 10, 10, 0, 5, 5);
            var sim = new CinderSim(in config);
            var opens = 0;
            var killsBeforeJump = -1;
            var killsAfterJump = -1;
            var opensOnTheJumpTick = 0;
            var previousKills = 0;

            for (var t = 0; t < Fps * 200 && sim.Mode != SimMode.GameOver; t++)
            {
                var input = new SimInput
                {
                    NovaQueued = t % 25 == 0,
                    MoveX = t / 60 % 2 == 0 ? 1f : -1f,
                };
                sim.Tick(in input);
                var kills = sim.Digest.Kills;
                var opened = (sim.Events & SimEvents.SurgeOpened) != 0;
                if (opened) opens += 1;
                var boundary = previousKills - previousKills % HackSpec.SurgeKillInterval + HackSpec.SurgeKillInterval;
                if (kills > boundary && previousKills < boundary && killsBeforeJump < 0)
                {
                    killsBeforeJump = previousKills;
                    killsAfterJump = kills;
                    opensOnTheJumpTick = opened ? 1 : 0;
                }
                previousKills = kills;
            }

            Assert.That(killsBeforeJump, Is.GreaterThanOrEqualTo(0),
                "baseline: this script must produce a multi-kill tick that steps OVER a "
                + $"multiple of {HackSpec.SurgeKillInterval}");
            Assert.That(killsBeforeJump % HackSpec.SurgeKillInterval, Is.Not.Zero,
                $"setup: the tick must start off-boundary ({killsBeforeJump})");
            Assert.That(killsAfterJump % HackSpec.SurgeKillInterval, Is.Not.Zero,
                $"setup: and land off-boundary ({killsAfterJump}) — a `% {HackSpec.SurgeKillInterval} == 0` "
                + "test sees neither end and opens nothing");
            Assert.That(opensOnTheJumpTick, Is.EqualTo(1),
                $"CROSSING, not an exact multiple: kills {killsBeforeJump} -> {killsAfterJump} stepped over "
                + $"{HackSpec.SurgeKillInterval} and must open exactly one window on that tick");
            Assert.That(opens, Is.EqualTo(1),
                $"one boundary crossed is one window — never a backlog (opens={opens})");
        }

        /// <summary>The ordinary door: kills reaching the interval open a window.</summary>
        [Test]
        public void Surge_OpensOnceCumulativeKillsReachTheInterval()
        {
            var config = Dungeon(CampaignStages.CinderSpan, default, 10, 10, 10, 5, 5, 5);
            var sim = new CinderSim(in config);
            var killsAtFirstOpen = -1;
            var openTick = -1;

            for (var t = 0; t < Fps * 200 && sim.Mode != SimMode.GameOver && openTick < 0; t++)
            {
                sim.Tick(Shuffle(t));
                if ((sim.Events & SimEvents.SurgeOpened) == 0) continue;
                openTick = t;
                killsAtFirstOpen = sim.Digest.Kills;
            }

            Assert.That(openTick, Is.GreaterThan(0), "baseline: the house script must open a window");
            Assert.That(killsAtFirstOpen, Is.GreaterThanOrEqualTo(HackSpec.SurgeKillInterval),
                $"기세 트리거: a window may not open before the {HackSpec.SurgeKillInterval}th cumulative kill "
                + $"(opened at {killsAtFirstOpen})");
            Assert.That(killsAtFirstOpen, Is.LessThan(HackSpec.SurgeKillInterval * 2),
                $"nor may it wait past the next boundary (opened at {killsAtFirstOpen})");
            Assert.That(sim.SurgeRemaining, Is.EqualTo(HackSpec.SurgeSeconds),
                $"the window must publish its full {HackSpec.SurgeSeconds} s on the opening tick");
        }

        /// <summary>웨이브당 1회. Without the per-wave cap a long wave with a dense
        /// pack would keep re-opening as the count rolls past each multiple.</summary>
        [Test]
        public void Surge_OpensAtMostOncePerWave()
        {
            var config = Dungeon(CampaignStages.CinderSpan, default, 10, 10, 10, 5, 5, 5);
            var sim = new CinderSim(in config);
            var opensByWave = new Dictionary<int, int>();

            for (var t = 0; t < Fps * 300 && sim.Mode != SimMode.GameOver; t++)
            {
                sim.Tick(Shuffle(t));
                if ((sim.Events & SimEvents.SurgeOpened) == 0) continue;
                opensByWave[sim.Wave] = opensByWave.TryGetValue(sim.Wave, out var n) ? n + 1 : 1;
            }

            Assert.That(opensByWave, Is.Not.Empty, "baseline: the run must open at least one window");
            foreach (var pair in opensByWave)
                Assert.That(pair.Value, Is.LessThanOrEqualTo(HackSpec.SurgeWaveCap),
                    $"wave {pair.Key} opened {pair.Value} windows — the cap is {HackSpec.SurgeWaveCap} per wave");
        }

        // =====================================================================
        // 각인 서지 조항 — one clause per test
        // =====================================================================

        /// <summary>
        /// 역류인 위기 조항: the push on the player is pinned to 0 for the window.
        ///
        /// Isolated by SLOT ORDER. Both runs carry 역류인 A + 집행인 A, so the
        /// permanent kit (push ×0.5) is identical and they open peril on the same
        /// tick; only the lower slot's clause is live. The stage has no wall, so
        /// 집행인's clause is inert and the swap is a clean on/off for 역류인's.
        /// </summary>
        [Test]
        public void CountercurrentPerilClause_PinsThePushToZero_AndOnlyInTheLowerSlot()
        {
            float DriftThroughTheWindow(SigilLoadout loadout)
            {
                var config = Dungeon(CampaignStages.CinderSluice, loadout);
                var sim = new CinderSim(in config);
                var openTick = -1;
                var xAtOpen = 0f;
                for (var t = 0; t < Fps * 200 && sim.Mode != SimMode.GameOver; t++)
                {
                    sim.Tick(openTick < 0 ? Toward(sim, 600f, 740f) : default);
                    if (openTick < 0)
                    {
                        if ((sim.Events & SimEvents.PerilOpened) == 0) continue;
                        openTick = t;
                        xAtOpen = sim.Player.X;
                        continue;
                    }
                    if (sim.PerilRemaining > 0f) continue;
                    return sim.Player.X - xAtOpen;      // neutral input all window: this is the current
                }
                return float.NaN;
            }

            var currentOwnsPeril = DriftThroughTheWindow(
                SigilLoadout.Of(SigilKind.Countercurrent, SigilFace.A, SigilKind.Executioner, SigilFace.A));
            var executionerOwnsPeril = DriftThroughTheWindow(
                SigilLoadout.Of(SigilKind.Executioner, SigilFace.A, SigilKind.Countercurrent, SigilFace.A));

            Assert.That(float.IsNaN(executionerOwnsPeril), Is.False, "baseline: the control run must open a window");
            Assert.That(MathF.Abs(executionerOwnsPeril), Is.GreaterThan(100f),
                "baseline: the lane must actually shove a neutral player through the window "
                + $"(measured {executionerOwnsPeril:0.#})");
            Assert.That(currentOwnsPeril, Is.EqualTo(0f).Within(1e-3f),
                "역류인 위기 조항: while the window is open the current must not move the player at all "
                + $"(measured {currentOwnsPeril:0.###})");
            Assert.That(executionerOwnsPeril, Is.Not.EqualTo(currentOwnsPeril),
                "위기 조항 중첩 금지: the SAME two sigils in the opposite order must give the other clause — "
                + "if both fired, swapping the slots would change nothing");
        }

        /// <summary>
        /// 집행인 위기 조항: the wall tick on the player is HALVED, not waived.
        /// The draft waived it and the director's arithmetic killed that — 6 s of
        /// exemption avoids 100 damage, 100% of base HP, reversal grade. Halved
        /// for 3 s avoids 15 (15%), and the wall still hurts every tick.
        /// </summary>
        [Test]
        public void ExecutionerPerilClause_HalvesTheWallTick_ButNeverWaivesIt()
        {
            List<float> DropsInsideTheWindow(SigilLoadout loadout)
            {
                var config = Dungeon(CampaignStages.AshMarch, loadout);
                config.Hazards = new[] { HazardConfig.Wall(0f) };
                var sim = new CinderSim(in config);
                var drops = new List<float>();
                for (var t = 0; t < Fps * 120 && sim.Mode != SimMode.GameOver; t++)
                {
                    var healthBefore = sim.Player.Health;
                    var wasOpen = sim.PerilRemaining > 0f;
                    sim.Tick(Toward(sim, 280f, 604f));
                    if (!wasOpen && sim.PerilRemaining <= 0f) continue;
                    if (sim.Player.Health < healthBefore) drops.Add(healthBefore - sim.Player.Health);
                }
                return drops;
            }

            var halved = HackSpec.SigilWallPlayerTick * 0.5f;
            var equipped = DropsInsideTheWindow(SigilLoadout.One(SigilKind.Executioner, SigilFace.A));

            Assert.That(equipped, Is.Not.Empty, "baseline: the window must contain at least one damaging tick");
            Assert.That(Contains(equipped, halved), Is.True,
                $"집행인 위기 조항: the wall tick must read {halved} inside the window "
                + $"({HackSpec.SigilWallPlayerTick} halved). Seen: {Describe(equipped)}");
            Assert.That(Contains(equipped, HackSpec.SigilWallPlayerTick), Is.False,
                $"the full {HackSpec.SigilWallPlayerTick} must not land while the window is open");

            // The no-immunity line, twice: the tick still hurts, and the amount
            // the window saves stays inside the comeback band. The ceiling is a
            // QA/negotiation threshold rather than a sim constant, so it is named
            // here instead of imported — the sim has no field for it.
            const float ComebackBandCeiling = 0.30f;   // negotiation entry 8
            Assert.That(halved, Is.GreaterThan(0f),
                "면역 금지: a waived tick would let the player stand in the band for free");
            var ticksPerWindow = HackSpec.PerilSeconds / CampaignSpec.WallTickPeriod;
            var avoided = ticksPerWindow * halved;
            Assert.That(avoided / SimConfig.PlayerMaxHealth, Is.LessThan(ComebackBandCeiling),
                $"comeback band: {ticksPerWindow} ticks x {halved} avoided = {avoided}, which must stay under "
                + $"{ComebackBandCeiling:P0} of base HP ({SimConfig.PlayerMaxHealth}) — the 6 s waiver the "
                + "draft proposed avoided 100 damage, 100% of base HP, and was reversal grade");
        }

        /// <summary>
        /// 증언인 위기 조항: the altar channel completes instantly. Cleared by the
        /// arithmetic unchanged — an altar grants OIL, so this avoids no damage
        /// and never touches the comeback band. Isolated by the same slot swap.
        /// </summary>
        [Test]
        public void WitnessPerilClause_CompletesTheAltarChannelInstantly()
        {
            int TicksFromArrivalToBlessing(SigilLoadout loadout)
            {
                var config = Dungeon(CampaignStages.EchoThrone, loadout);
                var sim = new CinderSim(in config);
                var openTick = -1;
                var arriveTick = -1;
                for (var t = 0; t < Fps * 200 && sim.Mode != SimMode.GameOver; t++)
                {
                    // Take the damage away from the altar, then walk on once the
                    // window is open, so arrival lands inside it.
                    sim.Tick(openTick < 0 ? Toward(sim, 500f, 700f) : Toward(sim, 768f, 604f));
                    if (openTick < 0)
                    {
                        if ((sim.Events & SimEvents.PerilOpened) != 0) openTick = t;
                        continue;
                    }
                    if (arriveTick < 0 && IsoWithin(768f, 604f, sim.Player.X, sim.Player.Y, CampaignSpec.AltarRadius))
                        arriveTick = t;
                    if ((sim.Events & SimEvents.AltarBlessing) == 0) continue;
                    return arriveTick < 0 ? -1 : t - arriveTick;
                }
                return -1;
            }

            var witnessOwnsPeril = TicksFromArrivalToBlessing(
                SigilLoadout.Of(SigilKind.Witness, SigilFace.B, SigilKind.Executioner, SigilFace.A));
            var executionerOwnsPeril = TicksFromArrivalToBlessing(
                SigilLoadout.Of(SigilKind.Executioner, SigilFace.A, SigilKind.Witness, SigilFace.B));

            var normalHold = (int)MathF.Round(CampaignSpec.AltarHoldSeconds * Fps);
            Assert.That(executionerOwnsPeril, Is.EqualTo(normalHold),
                $"baseline: with 증언인's clause dormant the channel must still cost its full "
                + $"{CampaignSpec.AltarHoldSeconds} s ({normalHold} ticks), measured {executionerOwnsPeril}");
            Assert.That(witnessOwnsPeril, Is.Zero,
                "증언인 위기 조항: 제단 즉시 완료 — inside the window the blessing must land on the "
                + $"arrival tick (measured {witnessOwnsPeril} ticks)");
            Assert.That(witnessOwnsPeril, Is.LessThan(executionerOwnsPeril),
                "위기 조항 중첩 금지: only the lower slot's clause fires, so the swap must move the number");
        }

        /// <summary>
        /// 판결인 서지 조항: the pylon aura stops for the window. This IS a full
        /// lift where the peril clauses were refused one, for the reason the
        /// arithmetic settles: the aura protects ENEMIES, so lifting it costs the
        /// player no safety and cannot register on the comeback band.
        ///
        /// Measured WITHIN one run — the equipped sigil moves the trajectory, so
        /// the honest control is the same sim outside its own window.
        /// </summary>
        [Test]
        public void VerdictSurgeClause_StopsThePylonAura_ForTheWindowOnly()
        {
            const float PylonX = 400f;
            const float PylonY = 604f;

            (List<float> Inside, List<float> Outside, int Opens) Census(SigilLoadout loadout)
            {
                var config = Dungeon(CampaignStages.AshMarch, loadout, 10, 10, 10, 5, 5, 5);
                config.Hazards = new[]
                {
                    HazardConfig.Wall(0f),
                    HazardConfig.Wall(CampaignSpec.WallPeriod * 0.5f, fromRight: true),
                    HazardConfig.Pylon(PylonX, PylonY),
                };
                var sim = new CinderSim(in config);
                var previous = new Dictionary<int, float>();
                var inside = new List<float>();
                var outside = new List<float>();
                var opens = 0;
                for (var t = 0; t < Fps * 300 && sim.Mode != SimMode.GameOver; t++)
                {
                    Snapshot(sim, previous);
                    var wasOpen = sim.SurgeRemaining > 0f;
                    sim.Tick(Shuffle(t));
                    if ((sim.Events & SimEvents.SurgeOpened) != 0) opens += 1;
                    // A landed swing makes the tick's drops ambiguous.
                    if ((sim.Events & (SimEvents.PlayerStruck | SimEvents.NovaCast)) != 0) continue;
                    CollectSurvivorDrops(sim, previous, wasOpen ? inside : outside, PylonX, PylonY);
                }
                return (inside, outside, opens);
            }

            var shielded = CampaignSpec.WallTickDamage * CampaignSpec.PylonAuraDamageTakenMult;
            var thinned = CampaignSpec.WallTickDamage * HackSpec.SigilPylonAuraRelief;
            var unshielded = CampaignSpec.WallTickDamage;

            var plain = Census(default);
            Assert.That(plain.Opens, Is.GreaterThan(0), "baseline: the plain run must open a window");
            Assert.That(Contains(plain.Outside, shielded), Is.True,
                $"baseline: an in-aura enemy must take {shielded} outside the window "
                + $"({CampaignSpec.WallTickDamage} x {CampaignSpec.PylonAuraDamageTakenMult}). "
                + $"Seen: {Describe(plain.Outside)}");
            Assert.That(Contains(plain.Inside, shielded), Is.True,
                "무장착이면 서지는 상태 표시만 — without 판결인 the aura must survive its own surge window. "
                + $"Seen inside: {Describe(plain.Inside)}");
            Assert.That(Contains(plain.Inside, unshielded), Is.False,
                "a plain surge window must NOT lift the aura");

            var verdict = Census(SigilLoadout.One(SigilKind.Verdict, SigilFace.A));
            Assert.That(verdict.Opens, Is.GreaterThan(0), "baseline: the equipped run must open a window");
            Assert.That(Contains(verdict.Outside, thinned), Is.True,
                $"판결인 A's permanent face must thin the aura to {thinned} outside the window. "
                + $"Seen: {Describe(verdict.Outside)}");
            Assert.That(Contains(verdict.Inside, unshielded), Is.True,
                $"판결인 서지 조항: 방벽주 오라 정지 — inside the window an in-aura enemy must take the "
                + $"undivided {unshielded}. Seen: {Describe(verdict.Inside)}");
            Assert.That(Contains(verdict.Inside, thinned), Is.False,
                "the aura must be STOPPED for the window, not merely thinned further");
        }

        /// <summary>판결인's surge clause is face-independent: it is the sigil
        /// waking up inside a window, not the face doing more of what it does.</summary>
        [Test]
        public void VerdictSurgeClause_FiresOnEitherFace()
        {
            var loadout = SigilLoadout.One(SigilKind.Verdict, SigilFace.B);
            Assert.IsTrue(loadout.HasKind(SigilKind.Verdict),
                "HasKind is the face-independent gate the surge clause reads");
            Assert.IsFalse(loadout.Has(SigilKind.Verdict, SigilFace.A),
                "setup: face B is equipped, so the permanent A effect must be absent");

            const float PylonX = 400f;
            const float PylonY = 604f;
            var config = Dungeon(CampaignStages.AshMarch, loadout, 10, 10, 10, 5, 5, 5);
            config.Hazards = new[]
            {
                HazardConfig.Wall(0f),
                HazardConfig.Wall(CampaignSpec.WallPeriod * 0.5f, fromRight: true),
                HazardConfig.Pylon(PylonX, PylonY),
            };
            var sim = new CinderSim(in config);
            var previous = new Dictionary<int, float>();
            var inside = new List<float>();
            var outside = new List<float>();
            for (var t = 0; t < Fps * 300 && sim.Mode != SimMode.GameOver; t++)
            {
                Snapshot(sim, previous);
                var wasOpen = sim.SurgeRemaining > 0f;
                sim.Tick(Shuffle(t));
                if ((sim.Events & (SimEvents.PlayerStruck | SimEvents.NovaCast)) != 0) continue;
                CollectSurvivorDrops(sim, previous, wasOpen ? inside : outside, PylonX, PylonY);
            }

            var shielded = CampaignSpec.WallTickDamage * CampaignSpec.PylonAuraDamageTakenMult;
            Assert.That(Contains(outside, shielded), Is.True,
                $"face B leaves the aura at its default {CampaignSpec.PylonAuraDamageTakenMult} outside the "
                + $"window ({shielded} per tick). Seen: {Describe(outside)}");
            Assert.That(Contains(inside, CampaignSpec.WallTickDamage), Is.True,
                "판결인 서지 조항 is face-independent — face B must stop the aura exactly as face A does. "
                + $"Seen inside: {Describe(inside)}");
        }

        /// <summary>
        /// 점화인 서지 조항: the enemy-facing hazard multiplier becomes 3 INSTEAD
        /// of paying a second time. 기세 기본이 이미 적 피해 2배이므로 점화인은
        /// 배수를 올리는 형태로 얹는다 — a layer on the layer, not a duplicate grant.
        /// </summary>
        [Test]
        public void IgnitionSurgeClause_RaisesTheEnemyHazardMultiplierToThree()
        {
            var config = Dungeon(CampaignStages.AshMarch,
                SigilLoadout.One(SigilKind.Ignition, SigilFace.A), 10, 10, 10, 5, 5, 5);
            config.Hazards = new[]
            {
                HazardConfig.Wall(0f),
                HazardConfig.Wall(CampaignSpec.WallPeriod * 0.5f, fromRight: true),
            };
            SurgeWindowCensus(in config, out var inside, out var after, out var openTick);

            var tripled = CampaignSpec.WallTickDamage * HackSpec.SigilSurgeEnemyHazardMult;
            var doubled = CampaignSpec.WallTickDamage * HackSpec.SurgeEnemyHazardMult;

            Assert.That(openTick, Is.GreaterThan(0), "baseline: the script must open a surge window");
            Assert.That(inside, Is.Not.Empty, "baseline: the wall must tick a surviving enemy inside the window");
            Assert.That(Contains(inside, tripled), Is.True,
                $"점화인 서지 조항: the enemy-side wall tick must read {tripled} inside the window "
                + $"({CampaignSpec.WallTickDamage} x {HackSpec.SigilSurgeEnemyHazardMult}). Seen: {Describe(inside)}");
            Assert.That(Contains(inside, doubled), Is.False,
                $"점화인 REPLACES the ×{HackSpec.SurgeEnemyHazardMult} base rather than stacking onto it — "
                + $"a {doubled} here means the two multipliers are being applied as separate grants");
            Assert.That(Contains(inside, doubled * HackSpec.SigilSurgeEnemyHazardMult), Is.False,
                "and they must certainly not compound");
            Assert.That(Contains(after, CampaignSpec.WallTickDamage), Is.True,
                $"the clause is windowed: once it closes the tick must fall back to {CampaignSpec.WallTickDamage}. "
                + $"Seen after: {Describe(after)}");
        }

        /// <summary>The gate AND the magnitude both live on 점화인, so an
        /// unequipped run never multiplies anything — that is what keeps the 15
        /// golden digests byte-identical.</summary>
        [Test]
        public void SurgeMultiplierConstants_LayerRatherThanDuplicate()
        {
            Assert.That(HackSpec.SigilSurgeEnemyHazardMult, Is.GreaterThan(HackSpec.SurgeEnemyHazardMult),
                "점화인 must RAISE the surge multiplier");
            Assert.That(HackSpec.SigilSurgeEnemyHazardMult,
                Is.LessThan(HackSpec.SurgeEnemyHazardMult * HackSpec.SurgeEnemyHazardMult),
                "a layer on the layer, not a second payout: 3 rather than 2x2");
            Assert.That(HackSpec.SurgeEnemyHazardMult, Is.GreaterThan(1f),
                "the base surge multiplier must be a real multiplier where a sigil unlocks it");
        }

        // =====================================================================
        // 훈련장
        // =====================================================================

        /// <summary>A trial is a fixed 60 s window ending by the CLOCK, never by a
        /// wave count. The clear reason is its own, so the lobby can tell a
        /// finished trial from a survived dungeon.</summary>
        [Test]
        public void Trial_RunsForTrainingSeconds_AndEndsWithTheTrainingClearReason()
        {
            foreach (var trialId in TrainingTrials.Ids)
            {
                var sim = new CinderSim(Trial(trialId, 0));
                var clearedAt = -1;
                var elapsedBeforeClear = -1f;
                for (var t = 1; t <= Fps * 90; t++)
                {
                    elapsedBeforeClear = sim.TrainingElapsed;
                    sim.Tick(trialId == TrainingTrials.Wall ? WallDodge(sim) : default);
                    if (!sim.StageCleared) continue;
                    clearedAt = t;
                    break;
                }

                Assert.That(clearedAt, Is.GreaterThan(0), $"{trialId}: the trial must end by the clock");
                Assert.That(elapsedBeforeClear, Is.LessThan(HackSpec.TrainingSeconds),
                    $"{trialId}: the tick before the clear must still be short of "
                    + $"{HackSpec.TrainingSeconds} s (was {elapsedBeforeClear:0.####})");
                Assert.That(sim.TrainingElapsed, Is.GreaterThanOrEqualTo(HackSpec.TrainingSeconds),
                    $"{trialId}: the clear must land on the first tick past {HackSpec.TrainingSeconds} s");
                Assert.That(sim.TrainingElapsed,
                    Is.LessThan(HackSpec.TrainingSeconds + SimConfig.FixedStep * 2f),
                    $"{trialId}: and not one tick later than that (was {sim.TrainingElapsed:0.####})");
                Assert.That(sim.Digest.Reason, Is.EqualTo(HackSpec.TrainingClearReason),
                    $"{trialId}: a finished trial is a CLEAR, not an overrun");
                Assert.That(sim.StageCleared, Is.True, $"{trialId}: StageCleared must publish the clear");
            }
        }

        /// <summary>시련은 스폰이 없다. No spawns means no kills, and no kills means
        /// no relic can drop — the training ground cannot feed the economy
        /// (negotiation entry 7 bans repeat currency payouts).</summary>
        [Test]
        public void Trial_SpawnsNothing_SoKillsAndRelicsStayZeroAtEveryTier()
        {
            foreach (var trialId in TrainingTrials.Ids)
            {
                for (var tier = 0; tier < HackSpec.TrainingTiers; tier++)
                {
                    var sim = new CinderSim(Trial(trialId, tier));
                    var seededQueue = sim.PendingSpawns;
                    var everLiving = 0;
                    var queueMovedWhileRunning = false;
                    var ticks = 0;
                    for (var t = 0; t < Fps * 90 && sim.Mode != SimMode.GameOver; t++)
                    {
                        var queueBefore = sim.PendingSpawns;
                        var clearedBefore = sim.StageCleared;
                        sim.Tick(trialId == TrainingTrials.Wall ? WallDodge(sim) : default);
                        if (sim.LivingEnemies > everLiving) everLiving = sim.LivingEnemies;
                        if (!clearedBefore && !sim.StageCleared && sim.PendingSpawns != queueBefore)
                            queueMovedWhileRunning = true;
                        ticks += 1;
                    }

                    Assert.That(ticks, Is.GreaterThan(Fps), $"{trialId} tier {tier}: the trial must actually run");
                    Assert.That(everLiving, Is.Zero,
                        $"{trialId} tier {tier}: 시련은 스폰이 없다 — not one enemy may exist at any tick");
                    // UpdateTraining REPLACES UpdateWave, so the spawn pump never
                    // runs: the wave-1 queue seeded at construction sits frozen for
                    // the whole trial and is only zeroed by the clear. A queue that
                    // moved mid-run would mean the wave path is executing after all,
                    // which is the failure this pins — the empty arena above would
                    // still look right for one tick before the first spawn landed.
                    Assert.That(seededQueue, Is.GreaterThan(0),
                        $"{trialId} tier {tier}: setup — wave 1 does seed a queue, so 'never pumped' is "
                        + "a real claim rather than an empty one");
                    Assert.That(queueMovedWhileRunning, Is.False,
                        $"{trialId} tier {tier}: the spawn queue must never be pumped — a trial runs "
                        + $"UpdateTraining INSTEAD of the wave path, so the seeded {seededQueue} cannot "
                        + "move while the trial is running");
                    Assert.That(sim.Digest.Kills, Is.Zero, $"{trialId} tier {tier}: no spawns means kills stay 0");
                    Assert.That(sim.Digest.Relics, Is.Zero,
                        $"{trialId} tier {tier}: a trial must not pay relics — 반복 재화 지급 금지 "
                        + "(negotiation entry 7); the mastery grant is one-time and lives outside the sim");
                    Assert.That(sim.Digest.Score, Is.Zero,
                        $"{trialId} tier {tier}: nor score — there is nothing here to score against");
                    Assert.That(sim.Level, Is.EqualTo(1), $"{trialId} tier {tier}: no kills means no levels");
                }
            }
        }

        /// <summary>
        /// 등급은 기믹 위상만 조인다. Tier scales the hazard CLOCK and nothing
        /// else — the telegraph seconds never move, because no title in the survey
        /// pool uses telegraph shortening as a difficulty lever.
        /// </summary>
        [Test]
        public void TrialTier_ScalesTheHazardClockAndNothingElse()
        {
            int PulsesAcrossTheWholeTrial(int tier)
            {
                var sim = new CinderSim(Trial(TrainingTrials.Vent, tier));
                var pulses = 0;
                for (var t = 0; t < Fps * 90 && sim.Mode != SimMode.GameOver; t++)
                {
                    sim.Tick(default);
                    if ((sim.Events & SimEvents.HazardPulse) != 0) pulses += 1;
                }
                return pulses;
            }

            var baseline = PulsesAcrossTheWholeTrial(0);
            Assert.That(baseline, Is.GreaterThan(0), "baseline: the vent trial must pulse at tier 0");
            Assert.That(HackSpec.TrainingTierRate(0), Is.EqualTo(1f),
                "tier 0 (견습) is the unscaled clock");

            for (var tier = 1; tier < HackSpec.TrainingTiers; tier++)
            {
                var expected = baseline * HackSpec.TrainingTierRate(tier);
                Assert.That(PulsesAcrossTheWholeTrial(tier), Is.EqualTo(expected).Within(1f),
                    $"tier {tier} must scale the hazard clock by exactly TrainingTierRate "
                    + $"({HackSpec.TrainingTierRate(tier)}): {baseline} pulses -> {expected}");
                Assert.That(HackSpec.TrainingTierRate(tier), Is.LessThan(HackSpec.TrainingTierRate(tier - 1)),
                    "each tier must tighten the clock relative to the one below it");
                Assert.That(HackSpec.TrainingTierRate(tier), Is.GreaterThan(0f),
                    "a rate of 0 would freeze the gimmick, which is the failure the sim's "
                    + "non-training default of 1 was written to prevent");
            }

            // The telegraph half of the contract is a constant, not a measurement:
            // it is read straight from CampaignSpec and never routed through the
            // rate, so the only way to defend it is to pin that it is untouched.
            Assert.That(CampaignSpec.VentTelegraph, Is.GreaterThan(0f),
                "예고 시간은 등급으로 움직이지 않는다 — the telegraph is a CampaignSpec constant with no "
                + "tier term, so a trial at any tier warns for the same seconds");
            Assert.That(CampaignSpec.WallTelegraph, Is.GreaterThan(0f), "same for the wall telegraph");
            Assert.That(CampaignSpec.CurrentTelegraph, Is.GreaterThan(0f), "and the current telegraph");
        }

        /// <summary>TryTraining is the only door into the mode; an unknown trial or
        /// an out-of-range tier must not produce a runnable config.</summary>
        [Test]
        public void TryTraining_RejectsAnUnknownTrialAndAnOutOfRangeTier()
        {
            var meta = MetaStats.Of(2, 2, 2);
            var equip = EquipTiers.Of(2, 1, 3);

            Assert.IsFalse(HackConfig.TryTraining("trial-nope", 0, meta, equip, out var unknown),
                "an unknown trial id must be refused");
            Assert.That(unknown.Mode, Is.Not.EqualTo(GameMode.Training),
                "a refused call must not hand back a training config");

            Assert.IsFalse(HackConfig.TryTraining(null, 0, meta, equip, out _),
                "a null trial id must be refused, not resolved to the first trial");
            Assert.IsFalse(HackConfig.TryTraining(TrainingTrials.Vent, -1, meta, equip, out _),
                "tier -1 is below the range");
            Assert.IsFalse(HackConfig.TryTraining(TrainingTrials.Vent, HackSpec.TrainingTiers, meta, equip, out _),
                $"tier {HackSpec.TrainingTiers} is one past the last (tiers are 0..{HackSpec.TrainingTiers - 1})");

            for (var tier = 0; tier < HackSpec.TrainingTiers; tier++)
            {
                foreach (var trialId in TrainingTrials.Ids)
                {
                    Assert.IsTrue(HackConfig.TryTraining(trialId, tier, meta, equip, out var ok),
                        $"{trialId} tier {tier} must be accepted");
                    Assert.That(ok.Mode, Is.EqualTo(GameMode.Training), $"{trialId}: mode must be Training");
                    Assert.That(ok.StageId, Is.EqualTo(trialId), $"{trialId}: the trial id is the stage id");
                    Assert.That(ok.TrainingTier, Is.EqualTo(tier), $"{trialId}: the tier must survive the call");
                    Assert.That(ok.Hazards, Is.Not.Null.And.Not.Empty,
                        $"{trialId}: every trial must carry its one gimmick");
                    Assert.That(ok.Sigils.Slot0, Is.EqualTo(SigilKind.None),
                        $"{trialId}: 각인은 따라오지 않는다 — a trial is where the gimmick is learned unaided");
                    Assert.That(ok.Sigils.Slot1, Is.EqualTo(SigilKind.None), $"{trialId}: neither slot");
                }
            }

            var named = new[]
            {
                TrainingTrials.Vent, TrainingTrials.Current, TrainingTrials.Pylon,
                TrainingTrials.Wall, TrainingTrials.Altar,
            };
            Assert.That(TrainingTrials.Ids.Length, Is.EqualTo(named.Length),
                "one trial per named constant, one dominant gimmick each — the pillar is deliberately "
                + "absent because a static blocker cannot be practised");
            foreach (var trialId in named)
                Assert.That(TrainingTrials.IndexOf(trialId), Is.GreaterThanOrEqualTo(0),
                    $"{trialId} must be reachable through the id table the lobby reads");
        }

        // =====================================================================
        // helpers that need the sim
        // =====================================================================

        /// <summary>Kiting pilot: below <paramref name="lowFraction"/> it disengages
        /// and hoovers ember shards, above <paramref name="highFraction"/> it fights
        /// again. This is the only script here that survives long enough to spend a
        /// second peril window.</summary>
        private static SimInput Kite(CinderSim sim, int tick, float maxHealth,
                                     float lowFraction, float highFraction, ref bool fleeing)
        {
            var player = sim.Player;
            var fraction = player.Health / maxHealth;
            if (fraction < lowFraction) fleeing = true;
            else if (fraction > highFraction) fleeing = false;

            var ax = 0f;
            var ay = 0f;
            var best = float.MaxValue;
            var bestX = 0f;
            var bestY = 0f;
            foreach (var pickup in sim.Pickups)
            {
                if (pickup.Kind != PickupKind.EmberShard) continue;
                var dx = pickup.X - player.X;
                var dy = (pickup.Y - player.Y) * SimConfig.IsoY;
                var distance = MathF.Sqrt(dx * dx + dy * dy);
                if (distance >= best) continue;
                best = distance;
                bestX = pickup.X;
                bestY = pickup.Y;
            }
            if (best < float.MaxValue && best > 1f)
            {
                ax += (bestX - player.X) / best * 2f;
                ay += (bestY - player.Y) / best * 2f;
            }
            if (fleeing)
            {
                foreach (var enemy in sim.Enemies)
                {
                    if (enemy.Dead) continue;
                    var dx = player.X - enemy.X;
                    var dy = (player.Y - enemy.Y) * SimConfig.IsoY;
                    var distance = MathF.Sqrt(dx * dx + dy * dy);
                    if (distance <= 0.01f || distance >= 420f) continue;
                    var weight = (420f - distance) / 420f * 3f;
                    ax += dx / distance * weight;
                    ay += dy / distance * weight;
                }
                ax += (SimConfig.ArenaX - player.X) / 600f;
                ay += (SimConfig.ArenaY - player.Y) / 400f;
            }

            var length = MathF.Sqrt(ax * ax + ay * ay);
            var input = default(SimInput);
            if (length > 0.01f) { input.MoveX = ax / length; input.MoveY = ay / length; }
            input.AttackQueued = !fleeing;
            input.NovaQueued = fleeing && tick % 30 == 0;
            input.DashQueued = fleeing && tick % 45 == 0;
            return input;
        }
    }
}
