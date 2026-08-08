// §Lane V4 — the frame watchdog behind URP post processing.
//
// The spec gate for V4 is "프로파일 수치 첨부 없이 PASS 불가". A number attached to
// a report proves one machine on one day; the watchdog is what makes the claim
// hold on the device the player actually has. This suite pins the DECISION —
// where the trip point sits, that the window must be full before any verdict,
// and that the degrade is one-way — because those are the parts that can drift
// silently. It cannot and does not measure a real frame time: that is the
// orchestrator's live-build pass, and this file is not a substitute for it.
//
// The pure decision seam (WindowBreaches) exists so the arithmetic is testable
// without a camera, a URP asset or a frame loop.
using NUnit.Framework;
using CinderCourt.View;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class PostFxWatchdogTests
    {
        [TearDown]
        public void ClearVerdict() => PostFxGate.ResetWatchdogForTests();

        [Test]
        public void TheBudgetIsTheSpecsSixtyHertzFrame()
        {
            Assert.That(PostFxGate.FrameBudgetSeconds * 1000f, Is.EqualTo(16.667f).Within(0.01f),
                "the §V4 gate is 16.7 ms; a budget that drifts off 1/60 makes every "
                + "later measurement incomparable to the spec");
        }

        [Test]
        public void TheTripPointIsTheP95OfTheWindow()
        {
            // "p95 exceeds the budget" == "more than 5% of the window exceeds the
            // budget". With a 120-frame window, 6 frames IS 5% and must not trip;
            // 7 is the first count that does.
            var fivePercent = (int)(PostFxGate.WindowFrames * PostFxGate.OverBudgetFraction);
            Assert.That(PostFxGate.OverBudgetTrip, Is.EqualTo(fivePercent + 1),
                "the trip count is off the p95 definition");

            Assert.That(
                PostFxGate.WindowBreaches(fivePercent, PostFxGate.WindowFrames), Is.False,
                $"exactly {fivePercent}/{PostFxGate.WindowFrames} over budget is p95 ON "
                + "the line, not past it — tripping here degrades a build that meets the gate");
            Assert.That(
                PostFxGate.WindowBreaches(fivePercent + 1, PostFxGate.WindowFrames), Is.True,
                "one frame past p95 must trip");
        }

        [Test]
        public void APartialWindowNeverReachesAVerdict()
        {
            // Warm-up, a scene change, or the first seconds of a run produce a
            // short window. Judging it would degrade on a startup hitch.
            for (var samples = 0; samples < PostFxGate.WindowFrames; samples++)
                Assert.That(PostFxGate.WindowBreaches(samples, samples), Is.False,
                    $"a {samples}-sample window returned a verdict — every frame in it "
                    + "was over budget, but the window is not full yet");
            Assert.That(
                PostFxGate.WindowBreaches(PostFxGate.WindowFrames, PostFxGate.WindowFrames),
                Is.True, "a full, entirely over-budget window must breach");
        }

        [Test]
        public void AHealthyWindowNeverBreaches()
        {
            for (var over = 0; over < PostFxGate.OverBudgetTrip; over++)
                Assert.That(PostFxGate.WindowBreaches(over, PostFxGate.WindowFrames), Is.False,
                    $"{over}/{PostFxGate.WindowFrames} over budget breached — a build "
                    + "inside the gate would lose its post effects");
        }

        [Test]
        public void TheHoldWindowIsLongerThanAnySingleHitch()
        {
            // A GC spike or a texture upload can put a handful of frames over
            // budget. The hold is what separates "hitched" from "cannot afford
            // this effect", so it must outlast the measurement window itself.
            Assert.That(PostFxGate.HoldSeconds, Is.GreaterThan(0.5f),
                "a sub-half-second hold degrades on hitches");
            Assert.That(PostFxGate.WarmupSeconds, Is.GreaterThanOrEqualTo(1f),
                "scene build and shader warm-up land inside the first second and are "
                + "not steady-state render cost");
            Assert.That(PostFxGate.StallCeilingSeconds, Is.GreaterThan(0.1f),
                "the stall ceiling must sit well above a bad frame — it exists to drop "
                + "backgrounded-tab deltas, not slow frames");
        }

        [Test]
        public void TheStartingVerdictIsMeasuringNotHolding()
        {
            PostFxGate.ResetWatchdogForTests();
            Assert.That(PostFxGate.Current, Is.EqualTo(PostFxGate.Status.Measuring),
                "the watchdog must not claim a passing verdict before it has measured "
                + "anything — that is the failure mode the stale '10.0 ms' figure had");
            Assert.That(PostFxGate.DebugLine, Is.Not.Null.And.Not.Empty,
                "the watchdog state must always be reportable");
        }
    }
}
