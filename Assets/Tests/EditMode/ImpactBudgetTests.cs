// ImpactBudget hit-feel contract (presentation-impact-spec #1/#2).
//
// The reason this tier table exists: a plain melee connect used to produce no
// tactile channel at all, and the two chains that decided hit-stop and camera
// punch could disagree about which event a tick was. These tests pin the parts a
// player can actually feel — that an ordinary hit registers, that it cannot
// congeal the screen, that heavier tiers win, and that a cheap tier can never cut
// a heavier stop short.
using CinderCourt.View;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class ImpactBudgetTests
    {
        const float Rested = ImpactBudget.LightRefractory + 1f;

        static ImpactPulse Light(float liveHitStop, float sinceLastLight) =>
            ImpactBudget.Resolve(true, false, false, liveHitStop, sinceLastLight, true);

        [Test]
        public void OrdinaryHitEarnsHitStopAndPunch_TheChannelThatDidNotExistBefore()
        {
            // The core regression guard for this change: a hit on a SURVIVING enemy
            // (no kill, no finisher) must produce a non-zero time and camera pulse.
            var pulse = Light(0f, Rested);
            Assert.That(pulse.HitStop, Is.EqualTo(ImpactBudget.LightHitStop).Within(1e-6f),
                "a normal connect must stop time briefly");
            Assert.That(pulse.PunchAmplitude, Is.GreaterThan(0f),
                "a normal connect must move the camera");
            Assert.That(pulse.ConsumedLight, Is.True,
                "the caller has to know to restart the refractory clock");
        }

        [Test]
        public void LightIsSwallowedInsideTheRefractoryWindow_SoCrowdsDoNotCongeal()
        {
            // Hitting three enemies in consecutive ticks must not chain three stops.
            var justFired = Light(0f, 0f);
            Assert.That(justFired.HitStop, Is.Zero,
                "a Light pulse inside the refractory grants no stop");
            Assert.That(justFired.PunchAmplitude, Is.Zero,
                "a swallowed Light must not buzz the camera either");
            Assert.That(justFired.ConsumedLight, Is.False,
                "a swallowed Light must not restart the clock");

            var stillInside = Light(0f, ImpactBudget.LightRefractory - 0.001f);
            Assert.That(stillInside.HitStop, Is.Zero,
                "the window is closed right up to its boundary");
        }

        [Test]
        public void LightReArmsExactlyAtTheRefractoryBoundary()
        {
            var atBoundary = Light(0f, ImpactBudget.LightRefractory);
            Assert.That(atBoundary.HitStop, Is.EqualTo(ImpactBudget.LightHitStop).Within(1e-6f),
                "the refractory is inclusive: at the boundary the Light fires again");
            Assert.That(atBoundary.ConsumedLight, Is.True);
        }

        [Test]
        public void TiersAreStrictlyOrdered_FinisherOverKillOverLight()
        {
            var light = Light(0f, Rested);
            var kill = ImpactBudget.Resolve(false, true, false, 0f, Rested, true);
            var finisher = ImpactBudget.Resolve(false, false, true, 0f, Rested, true);

            Assert.That(kill.HitStop, Is.GreaterThan(light.HitStop),
                "a kill must outweigh a glancing connect");
            Assert.That(finisher.HitStop, Is.GreaterThan(kill.HitStop),
                "a finisher must outweigh a kill");
            Assert.That(finisher.HitStop, Is.LessThanOrEqualTo(0.08f),
                "the presentation spec caps hit-stop at 80 ms");
        }

        [Test]
        public void OverlappingEventsResolveToTheHeaviestTier_NotToArrayOrder()
        {
            // A finisher that kills raises all three bits in the same tick. It must
            // read as a finisher, and it must NOT stack the three durations.
            var all = ImpactBudget.Resolve(true, true, true, 0f, Rested, true);
            Assert.That(all.HitStop, Is.EqualTo(ImpactBudget.FinisherHitStop).Within(1e-6f),
                "the heaviest tier wins outright");
            Assert.That(all.PunchAmplitude,
                Is.EqualTo(ImpactBudget.FinisherPunchAmplitude).Within(1e-6f),
                "time and camera channels must agree on the tier");
            Assert.That(all.ConsumedLight, Is.False,
                "an outranked Light did not fire, so the clock must not restart");

            var killAndLight = ImpactBudget.Resolve(true, true, false, 0f, Rested, true);
            Assert.That(killAndLight.HitStop, Is.EqualTo(ImpactBudget.KillHitStop).Within(1e-6f),
                "kill outranks the simultaneous Light");
        }

        [Test]
        public void ACheapTierCanExtendButNeverShortenALiveStop()
        {
            var live = ImpactBudget.FinisherHitStop;
            var duringFinisher = Light(live, Rested);
            Assert.That(duringFinisher.HitStop, Is.EqualTo(live).Within(1e-6f),
                "a 28 ms request must not cut a 75 ms stop short");

            var extended = ImpactBudget.Resolve(false, false, true, ImpactBudget.LightHitStop, Rested, true);
            Assert.That(extended.HitStop, Is.EqualTo(ImpactBudget.FinisherHitStop).Within(1e-6f),
                "a heavier request extends the live stop");
        }

        [Test]
        public void AQuietTickReturnsTheLiveStopUntouched()
        {
            // Resolve runs on ticks with no combat events too; it must be a no-op
            // there rather than clearing whatever pulse is mid-flight.
            var pulse = ImpactBudget.Resolve(false, false, false, 0.05f, Rested, true);
            Assert.That(pulse.HitStop, Is.EqualTo(0.05f).Within(1e-6f),
                "no events must not disturb a running stop");
            Assert.That(pulse.PunchAmplitude, Is.Zero, "no events, no punch");
        }

        [Test]
        public void ReducedMotionGrantsNoTimeStopForAnyTier()
        {
            // The accessibility gate buys out the time channel entirely, including
            // the heaviest tier, and it must not resurrect a cleared stop.
            foreach (var tier in new[] { 0, 1, 2 })
            {
                var pulse = ImpactBudget.Resolve(tier == 0, tier == 1, tier == 2, 0f, Rested, false);
                Assert.That(pulse.HitStop, Is.Zero,
                    "reduced motion must grant no hit-stop on tier " + tier);
            }
        }
    }
}
