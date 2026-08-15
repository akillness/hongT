// AMENDMENT #18 — opt-in no-target companion cohesion.
//
// The frozen follower steps directly toward every newly-computed anchor. These tests
// exercise only the additive dungeon gate and public snapshots: inside comfort, a
// changed anchor must not drag an idle companion; beyond recovery, it must walk back
// smoothly without becoming a target-driven companion.
using System;
using CinderCourt.Sim;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class CompanionCohesionTests
    {
        private const float Tolerance = 1e-4f;
        private const int MaxSetupTicks = 30;
        private const int MaxRecoveryTicks = 240;

        private static HackConfig DungeonWithCompanion()
        {
            Assert.IsTrue(
                HackConfig.TryDungeon(
                    CampaignStages.CinderSpan,
                    MetaStats.Of(0, 0, 0),
                    EquipTiers.Of(0, 0, 0),
                    "scout-echo",
                    0,
                    out var config),
                "cinder-span must resolve");
            return config;
        }

        private static float Iso(float fromX, float fromY, float toX, float toY)
        {
            float deltaX = toX - fromX;
            float deltaY = (toY - fromY) * SimConfig.IsoY;
            return MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        private static float CompanionDistance(CinderSim sim)
        {
            return Iso(sim.CompanionXAt(0), sim.CompanionYAt(0), sim.Player.X, sim.Player.Y);
        }

        private static void AssertNoTarget(CinderSim sim, string label)
        {
            Assert.That(sim.CompanionTargetIdAt(0), Is.EqualTo(0), $"{label}: no-target cohesion must not publish a lock");
            Assert.That(sim.CompanionEngagedAt(0), Is.False, $"{label}: no-target cohesion must not publish engagement");
        }

        [Test]
        public void CompanionCohesion_DefaultIsInertAndEverythingEnablesIt()
        {
            Assert.That(default(DungeonProgressionConfig).CompanionCohesion, Is.False,
                "the single-argument constructor must retain the frozen follower");
            Assert.That(DungeonProgressionConfig.Everything.CompanionCohesion, Is.True,
                "Everything must explicitly carry the companion cohesion amendment");
        }

        [Test]
        public void CompanionCohesion_InsideComfort_DoesNotFollowANewlyFlippedAnchor()
        {
            HackConfig config = DungeonWithCompanion();
            var sim = new CinderSim(in config, DungeonProgressionConfig.Everything);
            Assert.That(sim.CompanionCount, Is.EqualTo(1), "scenario requires one active companion");

            // Hold leaves the companion at the old left-facing anchor. Recalling while
            // turning left flips the live follow anchor to the player's right on this tick.
            var hold = new SimInput { CompanionHoldQueued = true };
            sim.Tick(in hold);
            float beforeX = sim.CompanionXAt(0);
            float beforeY = sim.CompanionYAt(0);

            var flipAndRecall = new SimInput { MoveX = -1f, CompanionRecallQueued = true };
            sim.Tick(in flipAndRecall);

            float anchorX = sim.Player.X - HackSpec.CompanionFollowOffset * sim.Player.Facing;
            float anchorY = sim.Player.Y + HackSpec.CompanionSlotFanout[0];
            float legacyStep = sim.PlayerSpeed * SimConfig.FixedStep;
            Assert.That(sim.Player.Facing, Is.EqualTo(-1), "the setup must flip the follow anchor");
            Assert.That(CompanionDistance(sim), Is.LessThanOrEqualTo(CompanionCohesionSpec.ComfortRadius),
                "the companion must still be inside its comfort radius after the flip");
            Assert.That(Iso(beforeX, beforeY, anchorX, anchorY), Is.GreaterThan(legacyStep),
                "the flipped anchor must visibly pull the legacy hard-anchor follower");
            AssertNoTarget(sim, "flipped comfort anchor");
            Assert.That(sim.CompanionXAt(0), Is.EqualTo(beforeX).Within(Tolerance),
                "an inside-comfort companion must not move toward the newly flipped anchor");
            Assert.That(sim.CompanionYAt(0), Is.EqualTo(beforeY).Within(Tolerance),
                "an inside-comfort companion must not move toward the newly flipped anchor");
        }

        [Test]
        public void CompanionCohesion_BeyondRecovery_ReturnsSmoothlyWithoutTargeting()
        {
            HackConfig config = DungeonWithCompanion();
            var sim = new CinderSim(in config, DungeonProgressionConfig.Everything);
            Assert.That(sim.CompanionCount, Is.EqualTo(1), "scenario requires one active companion");

            // A held diagonal dash puts the player beyond recovery while leaving the
            // companion at its initial anchor. The north-east route stays clear of the
            // deterministic opening spawn points, making this a pure no-target trace.
            var launch = new SimInput
            {
                MoveX = 1f,
                MoveY = -1f,
                DashQueued = true,
                CompanionHoldQueued = true,
            };
            sim.Tick(in launch);
            AssertNoTarget(sim, "launch tick");

            int dashDrainTicks = (int)Math.Ceiling(HackSpec.DashTime / SimConfig.FixedStep);
            for (int setupTick = 0; setupTick < MaxSetupTicks; setupTick += 1)
            {
                var hold = new SimInput { CompanionHoldQueued = true };
                sim.Tick(in hold);
                AssertNoTarget(sim, $"held separation tick {setupTick}");
                if (setupTick >= dashDrainTicks
                    && CompanionDistance(sim) > CompanionCohesionSpec.RecoveryRadius)
                {
                    break;
                }
            }

            float separation = CompanionDistance(sim);
            Assert.That(separation, Is.GreaterThan(CompanionCohesionSpec.RecoveryRadius),
                "the setup must cross recovery before recall");

            float beforeRecoveryX = sim.CompanionXAt(0);
            float beforeRecoveryY = sim.CompanionYAt(0);
            var recall = new SimInput { CompanionRecallQueued = true };
            sim.Tick(in recall);

            float maximumStep = sim.PlayerSpeed * CompanionCohesionSpec.RecoverySpeedScale
                * SimConfig.FixedStep + Tolerance;
            float distance = CompanionDistance(sim);
            float firstStep = Iso(beforeRecoveryX, beforeRecoveryY, sim.CompanionXAt(0), sim.CompanionYAt(0));
            Assert.That(sim.CompanionBehaviorAt(0), Is.EqualTo(CompanionBehavior.Follow),
                "recall must resume follow before recovery begins");
            AssertNoTarget(sim, "first recovery tick");
            Assert.That(firstStep, Is.GreaterThan(0f), "a separated companion must begin recovery");
            Assert.That(firstStep, Is.LessThanOrEqualTo(maximumStep), "recovery must use a bounded step");
            Assert.That(distance, Is.LessThan(separation), "the first recovery tick must reduce iso separation");
            Assert.That(distance, Is.GreaterThan(CompanionCohesionSpec.ComfortRadius),
                "recovery must be smooth rather than teleporting directly into comfort");

            bool sawLaterProgress = false;
            for (int tick = 0; tick < MaxRecoveryTicks && distance > CompanionCohesionSpec.ComfortRadius; tick += 1)
            {
                float previousDistance = distance;
                float previousX = sim.CompanionXAt(0);
                float previousY = sim.CompanionYAt(0);
                var idle = default(SimInput);
                sim.Tick(in idle);

                distance = CompanionDistance(sim);
                float step = Iso(previousX, previousY, sim.CompanionXAt(0), sim.CompanionYAt(0));
                AssertNoTarget(sim, $"recovery tick {tick}");
                Assert.That(step, Is.LessThanOrEqualTo(maximumStep), $"recovery tick {tick} must not teleport");
                Assert.That(distance, Is.LessThanOrEqualTo(previousDistance + Tolerance),
                    $"recovery tick {tick} must not increase iso separation");
                if (distance < previousDistance - Tolerance)
                {
                    sawLaterProgress = true;
                }
            }

            Assert.That(sawLaterProgress, Is.True, "recovery needs later deterministic progress, not one snap");
            Assert.That(distance, Is.LessThanOrEqualTo(CompanionCohesionSpec.ComfortRadius),
                "the companion must settle inside comfort after recovery");
            AssertNoTarget(sim, "settled companion");
        }
        [Test]
        public void CompanionCohesion_InsideComfort_BeginsItsBoundedIdleRouteAfterDwell()
        {
            HackConfig config = DungeonWithCompanion();
            var sim = new CinderSim(in config, DungeonProgressionConfig.Everything);
            float startX = sim.CompanionXAt(0);
            float startY = sim.CompanionYAt(0);
            Assert.That(CompanionDistance(sim), Is.LessThanOrEqualTo(CompanionCohesionSpec.ComfortRadius),
                "the fresh companion must begin in its comfort regime");

            int firstEligibleTick = Math.Max(1,
                (int)Math.Ceiling(CompanionCohesionSpec.WanderDwellSeconds / SimConfig.FixedStep));
            int lastIdleTick = firstEligibleTick + 3;

            bool moved = false;
            for (int tick = 1; tick <= lastIdleTick; tick += 1)
            {
                var idle = default(SimInput);
                sim.Tick(in idle);

                AssertNoTarget(sim, $"idle tick {tick}");
                Assert.That(CompanionDistance(sim), Is.LessThanOrEqualTo(CompanionCohesionSpec.ComfortRadius),
                    $"idle tick {tick} must remain inside comfort");

                float deltaX = sim.CompanionXAt(0) - startX;
                float deltaY = sim.CompanionYAt(0) - startY;
                float displacement = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
                float elapsed = tick * SimConfig.FixedStep;
                if (elapsed < CompanionCohesionSpec.WanderDwellSeconds - Tolerance)
                {
                    Assert.That(displacement, Is.LessThanOrEqualTo(Tolerance),
                        $"idle tick {tick} must not route before its dwell expires");
                    continue;
                }

                if (displacement > Tolerance)
                {
                    Assert.That(displacement, Is.LessThanOrEqualTo(CompanionCohesionSpec.WanderStride + Tolerance),
                        "the first idle-route move must be a configured stride, not hard-following motion");
                    moved = true;
                    break;
                }
            }

            Assert.That(moved, Is.True,
                "an inside-comfort no-target companion must begin its deterministic idle route after dwell");
        }
    }
}
