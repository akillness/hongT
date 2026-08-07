// Command agent, runner side: one step per FINISHED game event.
// Pure state machine — the observation is injected, so every gate, ack,
// settle and failure path is deterministic without a scene or a live sim.
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class CommandSequenceRunnerTests
    {
        const float Frame = 1f / 60f;

        static CommandAgentObservation Ready(float charge = 100f, int companionSlots = 1)
            => new CommandAgentObservation
            {
                RunLive = true,
                Charge = charge,
                CompanionSlots = companionSlots,
            };

        static CommandPlan Plan(params CommandStep[] steps)
            => new CommandPlan(steps, CommandPlanSource.Local, null);

        static CommandStep Act(CompanionCommandIntent intent) => CommandStep.Act(intent);

        /// <summary>Spins the runner and asserts it stays silent — the whole
        /// contract is that step N+1 does not start early.</summary>
        static void AssertSilent(CommandSequenceRunner runner, in CommandAgentObservation observation,
            int frames, string because)
        {
            for (var i = 0; i < frames; i++)
                Assert.AreEqual(CommandAgentSignalKind.None,
                    runner.Tick(Frame, observation).Kind, because);
        }

        [Test]
        public void SecondStepWaitsForTheFirstEventToFinish()
        {
            var runner = new CommandSequenceRunner();
            runner.Begin(Plan(Act(CompanionCommandIntent.SkillNova),
                              Act(CompanionCommandIntent.SkillAegis)));
            var observation = Ready();

            var first = runner.Tick(Frame, observation);
            Assert.AreEqual(CommandAgentSignalKind.Dispatch, first.Kind);
            Assert.AreEqual(CompanionCommandIntent.SkillNova, first.Intent);
            Assert.AreEqual(1, first.StepIndex);
            Assert.AreEqual(2, first.StepCount);

            // The sim has not acknowledged the cast yet: nothing else may start.
            AssertSilent(runner, observation, 10, "step 2 started before the sim took step 1");

            // Cast confirmed — the nova cooldown is hot.
            observation.NovaCooldown = HackSpec.AshNovaCooldown;
            Assert.AreEqual(CommandAgentSignalKind.None, runner.Tick(Frame, observation).Kind);

            // ...and the event still has to breathe before the next order.
            Assert.AreEqual(CommandAgentSignalKind.None, runner.Tick(0.2f, observation).Kind,
                "the settle window was skipped");
            Assert.AreEqual(CommandAgentSignalKind.None, runner.Tick(0.4f, observation).Kind);

            var second = runner.Tick(Frame, observation);
            Assert.AreEqual(CommandAgentSignalKind.Dispatch, second.Kind);
            Assert.AreEqual(CompanionCommandIntent.SkillAegis, second.Intent);

            observation.AegisCooldown = HackSpec.AegisCooldown;
            Assert.AreEqual(CommandAgentSignalKind.None, runner.Tick(Frame, observation).Kind);
            var finished = runner.Tick(0.6f, observation);
            Assert.AreEqual(CommandAgentSignalKind.Finished, finished.Kind);
            Assert.IsFalse(runner.Active);
        }

        [Test]
        public void SequenceWaitsOutACooldownInsteadOfDroppingTheStep()
        {
            var runner = new CommandSequenceRunner();
            runner.Begin(Plan(Act(CompanionCommandIntent.SkillNova),
                              Act(CompanionCommandIntent.SkillAegis)));
            var observation = Ready();
            observation.AegisCooldown = 3f;

            Assert.AreEqual(CommandAgentSignalKind.Dispatch, runner.Tick(Frame, observation).Kind);
            observation.NovaCooldown = HackSpec.AshNovaCooldown;
            runner.Tick(Frame, observation);        // ack
            runner.Tick(0.6f, observation);         // settle -> step 2

            AssertSilent(runner, observation, 30, "a sequence must hold, not fire on cooldown");

            observation.AegisCooldown = 0f;
            var second = runner.Tick(Frame, observation);
            Assert.AreEqual(CommandAgentSignalKind.Dispatch, second.Kind);
            Assert.AreEqual(CompanionCommandIntent.SkillAegis, second.Intent);
        }

        [Test]
        public void LoneOrderOnCooldownReportsImmediatelyInsteadOfQueueing()
        {
            // Single orders keep the console's original semantics: fire now, or
            // say why not. Silently firing 8 s later would be a surprise cast.
            var runner = new CommandSequenceRunner();
            runner.Begin(Plan(Act(CompanionCommandIntent.SkillNova)));
            var observation = Ready();
            observation.NovaCooldown = 5f;

            var note = runner.Tick(Frame, observation);
            Assert.AreEqual(CommandAgentSignalKind.Note, note.Kind);
            StringAssert.Contains(CommandAgentSpec.BlockedCooldown, note.Message);
            Assert.AreEqual(CommandAgentSignalKind.Finished, runner.Tick(Frame, observation).Kind);
            Assert.IsFalse(runner.Active);
        }

        [Test]
        public void GateTimeoutSkipsWithTheRealReason()
        {
            var runner = new CommandSequenceRunner();
            runner.Begin(Plan(Act(CompanionCommandIntent.SkillNova),
                              Act(CompanionCommandIntent.SkillAegis)));
            var observation = Ready();
            observation.AegisCooldown = 99f;

            runner.Tick(Frame, observation);
            observation.NovaCooldown = HackSpec.AshNovaCooldown;
            runner.Tick(Frame, observation);
            runner.Tick(0.6f, observation);

            var note = runner.Tick(CommandAgentSpec.GateTimeout + 1f, observation);
            Assert.AreEqual(CommandAgentSignalKind.Note, note.Kind);
            StringAssert.Contains(CommandAgentSpec.BlockedCooldown, note.Message);
            StringAssert.Contains("공허 방패", note.Message);
        }

        [Test]
        public void EmptyLanternNamesTheResource()
        {
            var runner = new CommandSequenceRunner();
            runner.Begin(Plan(Act(CompanionCommandIntent.SkillNova)));
            var observation = Ready(charge: HackSpec.AshNovaCost - 1f);

            var note = runner.Tick(Frame, observation);
            Assert.AreEqual(CommandAgentSignalKind.Note, note.Kind);
            StringAssert.Contains(CommandAgentSpec.BlockedCharge, note.Message);
        }

        [Test]
        public void ASimThatIgnoresTheLatchIsReportedNotFaked()
        {
            var runner = new CommandSequenceRunner();
            runner.Begin(Plan(Act(CompanionCommandIntent.SkillNova),
                              Act(CompanionCommandIntent.Recall)));
            var observation = Ready();

            Assert.AreEqual(CommandAgentSignalKind.Dispatch, runner.Tick(Frame, observation).Kind);
            var note = runner.Tick(CommandAgentSpec.AckTimeout + 0.1f, observation);
            Assert.AreEqual(CommandAgentSignalKind.Note, note.Kind);
            StringAssert.Contains("반응 없음", note.Message);

            // ...and the sequence keeps going rather than wedging.
            var second = runner.Tick(Frame, observation);
            Assert.AreEqual(CommandAgentSignalKind.Dispatch, second.Kind);
            Assert.AreEqual(CompanionCommandIntent.Recall, second.Intent);
        }

        [Test]
        public void StanceOrdersAreAcknowledgedByTheBehaviorTheyProduce()
        {
            var runner = new CommandSequenceRunner();
            runner.Begin(Plan(Act(CompanionCommandIntent.Defend),
                              Act(CompanionCommandIntent.Recall)));
            var observation = Ready();

            Assert.AreEqual(CommandAgentSignalKind.Dispatch, runner.Tick(Frame, observation).Kind);
            AssertSilent(runner, observation, 5, "Defend acked before the companion held");

            observation.CompanionHolding = true;
            runner.Tick(Frame, observation);        // ack
            runner.Tick(0.5f, observation);         // settle -> step 2

            var second = runner.Tick(Frame, observation);
            Assert.AreEqual(CommandAgentSignalKind.Dispatch, second.Kind);
            Assert.AreEqual(CompanionCommandIntent.Recall, second.Intent);

            AssertSilent(runner, observation, 5, "Recall acked while the companion was still holding");
            observation.CompanionHolding = false;
            runner.Tick(Frame, observation);
            Assert.AreEqual(CommandAgentSignalKind.Finished, runner.Tick(0.5f, observation).Kind);
        }

        [Test]
        public void CompanionSkillIsAcknowledgedByItsCastFlash()
        {
            var runner = new CommandSequenceRunner();
            runner.Begin(Plan(Act(CompanionCommandIntent.CompanionSkill)));
            var observation = Ready();

            Assert.AreEqual(CommandAgentSignalKind.Dispatch, runner.Tick(Frame, observation).Kind);
            AssertSilent(runner, observation, 3, "acked without any cast evidence");
            observation.CompanionCasting = true;
            runner.Tick(Frame, observation);
            Assert.AreEqual(CommandAgentSignalKind.Finished, runner.Tick(0.7f, observation).Kind);
        }

        [Test]
        public void AMissingGuardianSkipsAtOnceInsteadOfStallingTheSequence()
        {
            // No amount of waiting summons a companion, so this gate must not
            // hold the rest of the plan hostage for GateTimeout seconds.
            var runner = new CommandSequenceRunner();
            runner.Begin(Plan(Act(CompanionCommandIntent.CompanionSkill),
                              Act(CompanionCommandIntent.SkillNova)));
            var observation = Ready(companionSlots: 0);

            var note = runner.Tick(Frame, observation);
            Assert.AreEqual(CommandAgentSignalKind.Note, note.Kind);
            StringAssert.Contains(CommandAgentSpec.BlockedNoCompanion, note.Message);

            var second = runner.Tick(Frame, observation);
            Assert.AreEqual(CommandAgentSignalKind.Dispatch, second.Kind);
            Assert.AreEqual(CompanionCommandIntent.SkillNova, second.Intent);
        }

        [Test]
        public void WaitStepHoldsTheSequenceForItsFullDuration()
        {
            var runner = new CommandSequenceRunner();
            runner.Begin(Plan(Act(CompanionCommandIntent.SkillNova),
                              CommandStep.Wait(1f),
                              Act(CompanionCommandIntent.Recall)));
            var observation = Ready();

            runner.Tick(Frame, observation);
            observation.NovaCooldown = HackSpec.AshNovaCooldown;
            runner.Tick(Frame, observation);
            runner.Tick(0.6f, observation);         // settle -> wait step

            Assert.AreEqual(CommandAgentSignalKind.None, runner.Tick(0.5f, observation).Kind);
            Assert.AreEqual(CommandAgentSignalKind.None, runner.Tick(0.6f, observation).Kind);
            var third = runner.Tick(Frame, observation);
            Assert.AreEqual(CommandAgentSignalKind.Dispatch, third.Kind);
            Assert.AreEqual(CompanionCommandIntent.Recall, third.Intent);
        }

        [Test]
        public void PickupInfoNeedsNoAcknowledgementBecauseItSetsNoLatch()
        {
            var runner = new CommandSequenceRunner();
            runner.Begin(Plan(Act(CompanionCommandIntent.PickupInfo)));
            var observation = Ready();

            Assert.AreEqual(CommandAgentSignalKind.Dispatch, runner.Tick(Frame, observation).Kind);
            Assert.AreEqual(CommandAgentSignalKind.Finished, runner.Tick(Frame, observation).Kind);
        }

        [Test]
        public void AFinishedRunAbortsWhateverIsInFlight()
        {
            var runner = new CommandSequenceRunner();
            runner.Begin(Plan(Act(CompanionCommandIntent.SkillNova),
                              Act(CompanionCommandIntent.Recall)));
            var observation = Ready();

            Assert.AreEqual(CommandAgentSignalKind.Dispatch, runner.Tick(Frame, observation).Kind);
            observation.RunLive = false;
            Assert.AreEqual(CommandAgentSignalKind.Aborted, runner.Tick(Frame, observation).Kind);
            Assert.IsFalse(runner.Active);
            Assert.AreEqual(CommandAgentSignalKind.None, runner.Tick(Frame, observation).Kind);
        }

        [Test]
        public void ANewOrderReplacesTheOneInFlight()
        {
            var runner = new CommandSequenceRunner();
            runner.Begin(Plan(Act(CompanionCommandIntent.SkillNova),
                              Act(CompanionCommandIntent.Recall)));
            var observation = Ready();
            Assert.AreEqual(CommandAgentSignalKind.Dispatch, runner.Tick(Frame, observation).Kind);

            runner.Begin(Plan(Act(CompanionCommandIntent.SkillAegis)));
            var replaced = runner.Tick(Frame, observation);
            Assert.AreEqual(CommandAgentSignalKind.Dispatch, replaced.Kind);
            Assert.AreEqual(CompanionCommandIntent.SkillAegis, replaced.Intent);
            Assert.AreEqual(1, replaced.StepCount);
        }

        [Test]
        public void CancelStopsEverythingAndStaysQuiet()
        {
            var runner = new CommandSequenceRunner();
            runner.Begin(Plan(Act(CompanionCommandIntent.SkillNova),
                              Act(CompanionCommandIntent.Recall)));
            var observation = Ready();
            runner.Tick(Frame, observation);

            runner.Cancel();
            Assert.IsFalse(runner.Active);
            Assert.AreEqual(CommandAgentSignalKind.None, runner.Tick(Frame, observation).Kind);
        }

        [Test]
        public void AnEmptyPlanIsNeverActive()
        {
            var runner = new CommandSequenceRunner();
            runner.Begin(CommandPlan.Empty);
            Assert.IsFalse(runner.Active);
            Assert.AreEqual(CommandAgentSignalKind.None, runner.Tick(Frame, Ready()).Kind);
        }
    }
}
