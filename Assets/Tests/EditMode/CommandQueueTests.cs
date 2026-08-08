// Command agent, queue side (W10): several orders parked at once, each
// released by a game EVENT rather than by the clock.
//
// Pure state — SimEvents masks go in, release decisions come out — so every
// trigger, cap and ordering rule is deterministic without a scene or a live sim.
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class CommandQueueTests
    {
        static CommandPlan Plan(params CompanionCommandIntent[] intents)
        {
            var steps = new CommandStep[intents.Length];
            for (var i = 0; i < intents.Length; i++) steps[i] = CommandStep.Act(intents[i]);
            return new CommandPlan(steps, CommandPlanSource.Local, null);
        }

        // ------------------------------------------------------------ parsing --

        [Test]
        public void TrySplit_PlainOrder_IsNotATrigger()
        {
            Assert.That(CommandTriggerParser.TrySplit("노바 쓰고 결계 쳐",
                out var trigger, out var remainder), Is.False);
            Assert.That(trigger.IsImmediate, Is.True);
            // The caller must be able to pass the untouched text straight on.
            Assert.That(remainder, Is.EqualTo("노바 쓰고 결계 쳐"));
        }

        [Test]
        public void TrySplit_KillTrigger_ReadsCountAndTail()
        {
            Assert.That(CommandTriggerParser.TrySplit("적 셋 잡으면 노바",
                out var trigger, out var remainder, out var prefix), Is.True);
            Assert.That(trigger.Kind, Is.EqualTo(CommandTriggerKind.Kills));
            Assert.That(trigger.Count, Is.EqualTo(3), "spelled-out 셋 must read as 3");
            Assert.That(remainder, Is.EqualTo("노바"));
            // "적 셋" is a qualifier, not an order: it must parse to nothing so
            // the caller does not fire a phantom immediate plan.
            Assert.That(prefix, Is.EqualTo("적 셋"));
            Assert.That(CommandPlanParser.ParseLocal(prefix).IsEmpty, Is.True);
        }

        [Test]
        public void TrySplit_OrderBeforeTrigger_SurvivesAsPrefix()
        {
            // Half the sentence is for right now, half is for later. Dropping
            // the first half is the failure this out-param exists to prevent.
            Assert.That(CommandTriggerParser.TrySplit("노바 쓰고 셋 잡으면 결계",
                out var trigger, out var remainder, out var prefix), Is.True);
            Assert.That(trigger.Count, Is.EqualTo(3));
            Assert.That(CommandPlanParser.ParseLocal(prefix).StepAt(0).Intent,
                Is.EqualTo(CompanionCommandIntent.SkillNova));
            Assert.That(CommandPlanParser.ParseLocal(remainder).StepAt(0).Intent,
                Is.EqualTo(CompanionCommandIntent.SkillAegis));
        }

        [Test]
        public void TrySplit_TriggerWithNothingBehindIt_IsNotATrigger()
        {
            // "보스 등장" alone is a sentence, not an order-with-a-condition.
            // Leaving it alone is what keeps the existing intent table's shot
            // at ambiguous phrasings intact.
            Assert.That(CommandTriggerParser.TrySplit("보스 등장",
                out _, out var remainder), Is.False);
            Assert.That(remainder, Is.EqualTo("보스 등장"));
        }

        [Test]
        public void TrySplit_DigitCount_ClampsToMax()
        {
            Assert.That(CommandTriggerParser.TrySplit("300 잡으면 노바",
                out var trigger, out _), Is.True);
            Assert.That(trigger.Count, Is.EqualTo(CommandTrigger.MaxCount),
                "a typo'd count must not park a plan forever");
        }

        [Test]
        public void TrySplit_WaveAndBoss_MapToTheirEvents()
        {
            Assert.That(CommandTriggerParser.TrySplit("다음 웨이브에 결계",
                out var wave, out _), Is.True);
            Assert.That(wave.Kind, Is.EqualTo(CommandTriggerKind.WaveStart));
            Assert.That(wave.Fires(SimEvents.WaveStarted), Is.True);
            Assert.That(wave.Fires(SimEvents.EnemyKilled), Is.False);

            Assert.That(CommandTriggerParser.TrySplit("보스 나오면 노바",
                out var boss, out _), Is.True);
            Assert.That(boss.Kind, Is.EqualTo(CommandTriggerKind.BossSpawn));
            Assert.That(boss.Fires(SimEvents.BossSpawned), Is.True);
        }

        // -------------------------------------------------------------- queue --

        [Test]
        public void Head_HoldsUntilItsTriggerFires()
        {
            var queue = new CommandQueue();
            Assert.That(queue.TryEnqueue(CommandTrigger.Of(CommandTriggerKind.WaveStart),
                Plan(CompanionCommandIntent.SkillNova), out _), Is.True);

            queue.ObserveEvents(SimEvents.EnemyKilled | SimEvents.EnemyHit);
            Assert.That(queue.HeadReady, Is.False, "an unrelated event must not release");
            Assert.That(queue.TryRelease(runnerBusy: false, out _), Is.False);

            queue.ObserveEvents(SimEvents.WaveStarted);
            Assert.That(queue.HeadReady, Is.True);
            Assert.That(queue.TryRelease(runnerBusy: false, out var released), Is.True);
            Assert.That(released.Plan.StepAt(0).Intent, Is.EqualTo(CompanionCommandIntent.SkillNova));
            Assert.That(queue.IsEmpty, Is.True);
        }

        [Test]
        public void CountedTrigger_NeedsEveryKillTick()
        {
            var queue = new CommandQueue();
            queue.TryEnqueue(CommandTrigger.Of(CommandTriggerKind.Kills, 3),
                Plan(CompanionCommandIntent.SkillNova), out _);

            for (var tick = 0; tick < 2; tick++)
            {
                queue.ObserveEvents(SimEvents.EnemyKilled);
                Assert.That(queue.HeadReady, Is.False, $"released after only {tick + 1} kill tick(s)");
            }
            // Progress is shown, not merely counted — the HUD promise and the
            // release condition are the same number.
            Assert.That(queue.Head.Trigger.Describe(queue.Head.Progress), Is.EqualTo("처치 2/3"));

            queue.ObserveEvents(SimEvents.EnemyKilled);
            Assert.That(queue.HeadReady, Is.True);
        }

        [Test]
        public void OnlyTheHeadAccumulatesProgress()
        {
            var queue = new CommandQueue();
            queue.TryEnqueue(CommandTrigger.Of(CommandTriggerKind.WaveStart),
                Plan(CompanionCommandIntent.SkillAegis), out _);
            queue.TryEnqueue(CommandTrigger.Of(CommandTriggerKind.Kills, 2),
                Plan(CompanionCommandIntent.SkillNova), out _);

            // Two kill ticks pass while the WAVE entry is still the head.
            queue.ObserveEvents(SimEvents.EnemyKilled);
            queue.ObserveEvents(SimEvents.EnemyKilled);
            Assert.That(queue.Entries[1].Progress, Is.Zero,
                "an entry that is not yet being watched must not bank progress");

            queue.ObserveEvents(SimEvents.WaveStarted);
            Assert.That(queue.TryRelease(runnerBusy: false, out _), Is.True);
            // Now the kill entry is head, and it starts from zero.
            Assert.That(queue.HeadReady, Is.False);
            queue.ObserveEvents(SimEvents.EnemyKilled);
            queue.ObserveEvents(SimEvents.EnemyKilled);
            Assert.That(queue.HeadReady, Is.True);
        }

        [Test]
        public void Release_IsBlockedWhileTheRunnerIsBusy()
        {
            var queue = new CommandQueue();
            queue.TryEnqueue(CommandTrigger.Immediate, Plan(CompanionCommandIntent.SkillNova), out _);
            Assert.That(queue.HeadReady, Is.True);
            // Releasing into a live runner would REPLACE the sequence the player
            // is watching (CommandSequenceRunner.Begin), silently discarding it.
            Assert.That(queue.TryRelease(runnerBusy: true, out _), Is.False);
            Assert.That(queue.Count, Is.EqualTo(1));
            Assert.That(queue.TryRelease(runnerBusy: false, out _), Is.True);
        }

        [Test]
        public void Enqueue_StopsAtTheDepthCap()
        {
            var queue = new CommandQueue();
            for (var i = 0; i < CommandQueue.MaxEntries; i++)
            {
                Assert.That(queue.TryEnqueue(CommandTrigger.Of(CommandTriggerKind.WaveStart),
                    Plan(CompanionCommandIntent.Defend), out _), Is.True);
            }
            Assert.That(queue.TryEnqueue(CommandTrigger.Immediate,
                Plan(CompanionCommandIntent.SkillNova), out var rejection), Is.False);
            Assert.That(rejection, Is.EqualTo(CommandQueue.RejectedFull));
            Assert.That(queue.Count, Is.EqualTo(CommandQueue.MaxEntries));
        }

        [Test]
        public void Enqueue_RefusesAnEmptyPlan()
        {
            var queue = new CommandQueue();
            Assert.That(queue.TryEnqueue(CommandTrigger.Immediate, CommandPlan.Empty,
                out var rejection), Is.False);
            Assert.That(rejection, Is.EqualTo(CommandQueue.RejectedEmptyPlan));
            Assert.That(queue.IsEmpty, Is.True);
        }

        [Test]
        public void CancelAll_ReportsWhatItDropped_AndNullsWhenEmpty()
        {
            var queue = new CommandQueue();
            Assert.That(queue.CancelAll(), Is.Null,
                "an empty cancel must not invent a cancellation to report");

            queue.TryEnqueue(CommandTrigger.Immediate, Plan(CompanionCommandIntent.Defend), out _);
            queue.TryEnqueue(CommandTrigger.Immediate, Plan(CompanionCommandIntent.Recall), out _);
            Assert.That(queue.CancelAll(), Does.Contain("2"));
            Assert.That(queue.IsEmpty, Is.True);
        }

        [Test]
        public void StatusLine_NamesTheConditionAndTheOrder()
        {
            var queue = new CommandQueue();
            queue.TryEnqueue(CommandTrigger.Of(CommandTriggerKind.Kills, 3),
                Plan(CompanionCommandIntent.SkillNova, CompanionCommandIntent.SkillAegis), out _);
            var line = queue.Head.StatusLine;
            Assert.That(line, Does.StartWith("처치 0/3"));
            Assert.That(line, Does.Contain(CommandAgentSpec.LabelOf(CompanionCommandIntent.SkillNova)));
            Assert.That(line, Does.Contain(CommandAgentSpec.LabelOf(CompanionCommandIntent.SkillAegis)));
        }
    }
}
