using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class ShadowQualityGateTests
    {
        [TearDown]
        public void ResetPostGate() => PostFxGate.ResetWatchdogForTests();

        [Test]
        public void TierTable_IsOneWayAndNeverTurnsShadowsOff()
        {
            Assert.That(StageShadowPolicy.ResolutionFor(StageShadowPolicy.Tier.High),
                Is.EqualTo(1024));
            Assert.That(StageShadowPolicy.ResolutionFor(StageShadowPolicy.Tier.Medium),
                Is.EqualTo(512));
            Assert.That(StageShadowPolicy.ResolutionFor(StageShadowPolicy.Tier.Low),
                Is.EqualTo(256));
            Assert.That(StageShadowPolicy.ResolutionFor(StageShadowPolicy.Tier.Failed),
                Is.EqualTo(256));

            Assert.That(StageShadowPolicy.NextTier(StageShadowPolicy.Tier.High),
                Is.EqualTo(StageShadowPolicy.Tier.Medium));
            Assert.That(StageShadowPolicy.NextTier(StageShadowPolicy.Tier.Medium),
                Is.EqualTo(StageShadowPolicy.Tier.Low));
            Assert.That(StageShadowPolicy.NextTier(StageShadowPolicy.Tier.Low),
                Is.EqualTo(StageShadowPolicy.Tier.Failed));
            Assert.That(StageShadowPolicy.NextTier(StageShadowPolicy.Tier.Failed),
                Is.EqualTo(StageShadowPolicy.Tier.Failed));
        }

        [Test]
        public void SixOfOneTwentyHoldsButSevenNeedsPersistentBreach()
        {
            var healthy = new ShadowQualityGate(StageShadowPolicy.Tier.High);
            healthy.BeginEpoch(1, 0f);
            for (var i = 0; i < PostFxGate.WindowFrames; i++)
                healthy.Sample(i < 6 ? 0.02f : 0.01f);
            Assert.That(healthy.Current, Is.EqualTo(ShadowQualityGate.Status.Holding));
            Assert.That(healthy.Tier, Is.EqualTo(StageShadowPolicy.Tier.High));

            var breached = new ShadowQualityGate(StageShadowPolicy.Tier.High);
            breached.BeginEpoch(2, 0f);
            for (var i = 0; i < PostFxGate.WindowFrames; i++)
                breached.Sample(i < 7 ? 0.02f : 0.01f);
            Assert.That(breached.Tier, Is.EqualTo(StageShadowPolicy.Tier.High),
                "one completed bad window has not yet held for 1.5 seconds");
        }

        [Test]
        public void PersistentBreach_DegradesThroughLowThenFails()
        {
            var gate = new ShadowQualityGate(StageShadowPolicy.Tier.High);
            AssertPersistentTransition(gate, 1, StageShadowPolicy.Tier.Medium);
            AssertPersistentTransition(gate, 2, StageShadowPolicy.Tier.Low);
            AssertPersistentTransition(gate, 3, StageShadowPolicy.Tier.Failed);

            gate.BeginEpoch(4, 0f);
            for (var i = 0; i < 300; i++) gate.Sample(0.02f);
            Assert.That(gate.Tier, Is.EqualTo(StageShadowPolicy.Tier.Failed),
                "Failed is terminal for the browser session");
        }

        [Test]
        public void InvalidOrBackgroundDeltasNeverEnterTheWindow()
        {
            var gate = new ShadowQualityGate(StageShadowPolicy.Tier.High);
            gate.BeginEpoch(8, 0f);
            gate.Sample(0f);
            gate.Sample(-0.1f);
            gate.Sample(PostFxGate.StallCeilingSeconds + 0.01f);
            Assert.That(gate.SamplesInWindow, Is.Zero);
            Assert.That(gate.OverBudgetInWindow, Is.Zero);
        }

        [Test]
        public void StageEpochCoordinator_PreservesPersistentDecisionsAndHasOneOwner()
        {
            var host = new GameObject("postfx-stage-epoch-test");
            host.AddComponent<Camera>();
            var gate = host.AddComponent<PostFxGate>();
            // Ordinary EditMode AddComponent does not invoke a non-ExecuteAlways
            // MonoBehaviour Awake. Use the same initialization body explicitly so
            // this fixture exercises the coordinator rather than a null singleton.
            gate.InitializeForTests();
            try
            {
                AssertEpoch(PostFxGate.Status.Measuring,
                    PostFxGate.Status.Measuring, PostFxGate.MeasurementOwner.PostFx);
                AssertEpoch(PostFxGate.Status.Holding,
                    PostFxGate.Status.Measuring, PostFxGate.MeasurementOwner.PostFx);
                AssertEpoch(PostFxGate.Status.Degraded,
                    PostFxGate.Status.Degraded, PostFxGate.MeasurementOwner.Shadow);
                AssertEpoch(PostFxGate.Status.OffByPlatform,
                    PostFxGate.Status.OffByPlatform, PostFxGate.MeasurementOwner.Shadow);

                PostFxGate.SetStageActive(false);
                Assert.That(PostFxGate.CurrentMeasurementOwner,
                    Is.EqualTo(PostFxGate.MeasurementOwner.None));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        static void AssertEpoch(
            PostFxGate.Status seed,
            PostFxGate.Status expectedStatus,
            PostFxGate.MeasurementOwner expectedOwner)
        {
            var before = PostFxGate.StageEpoch;
            PostFxGate.SeedPersistentStateForTests(seed);
            PostFxGate.SetStageActive(true);
            Assert.That(PostFxGate.StageEpoch, Is.EqualTo(before + 1));
            Assert.That(PostFxGate.Current, Is.EqualTo(expectedStatus));
            Assert.That(PostFxGate.CurrentMeasurementOwner, Is.EqualTo(expectedOwner));
            Assert.That(PostFxGate.SamplesInWindow, Is.Zero);
            Assert.That(PostFxGate.OverBudgetInWindow, Is.Zero);
        }

        static void AssertPersistentTransition(
            ShadowQualityGate gate, int epoch, StageShadowPolicy.Tier expected)
        {
            gate.BeginEpoch(epoch, 0f);
            var changed = false;
            for (var i = 0; i < 240 && !changed; i++) changed = gate.Sample(0.02f);
            Assert.That(changed, Is.True, $"epoch {epoch}: persistent breach did not transition");
            Assert.That(gate.Tier, Is.EqualTo(expected));
            Assert.That(gate.SamplesInWindow, Is.Zero,
                "a tier transition must open a fresh measurement window");
        }
    }
}
