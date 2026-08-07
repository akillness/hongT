// Command agent, plan side: free text / model reply -> ordered sequence.
// Pure string logic — no scene, no devices, no network.
using CinderCourt.View;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class CommandPlanParserTests
    {
        static CompanionCommandIntent IntentAt(CommandPlan plan, int index)
            => plan.StepAt(index).Intent;

        // ------------------------------------------------------------ local --

        [Test]
        public void SingleKeywordStaysOneStep()
        {
            var plan = CommandPlanParser.ParseLocal("노바");
            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(CommandPlanSource.Local, plan.Source);
            Assert.IsFalse(plan.IsSequence, "a lone order must not announce itself as a sequence");
            Assert.AreEqual(CompanionCommandIntent.SkillNova, IntentAt(plan, 0));
        }

        [Test]
        public void TwoOrdersReadInPositionOrderNotRulePriority()
        {
            // The rule table lists Aegis ABOVE Nova, so the old first-match
            // classifier answered "결계" for this sentence and dropped the nova
            // entirely. A sequence must follow the sentence, not the table.
            var plan = CommandPlanParser.ParseLocal("노바 쓰고 결계 쳐");
            Assert.AreEqual(2, plan.Count);
            Assert.AreEqual(CompanionCommandIntent.SkillNova, IntentAt(plan, 0));
            Assert.AreEqual(CompanionCommandIntent.SkillAegis, IntentAt(plan, 1));
        }

        [Test]
        public void DelayBetweenOrdersBecomesAWaitStep()
        {
            var plan = CommandPlanParser.ParseLocal("집중공격하고 3초 뒤에 복귀");
            Assert.AreEqual(3, plan.Count);
            Assert.AreEqual(CompanionCommandIntent.FocusAttack, IntentAt(plan, 0));
            Assert.AreEqual(CommandStepKind.Wait, plan.StepAt(1).Kind);
            Assert.AreEqual(3f, plan.StepAt(1).Seconds, 0.001f);
            Assert.AreEqual(CompanionCommandIntent.Recall, IntentAt(plan, 2));
        }

        [Test]
        public void SecondsMarkerIsConsumedSoItCannotAlsoFireDefend()
        {
            // "대기" is a Defend keyword. As the tail of "3초 대기" it is a
            // duration, not an order — consuming it is what keeps those apart.
            var plan = CommandPlanParser.ParseLocal("3초 대기 후 노바");
            Assert.AreEqual(2, plan.Count);
            Assert.AreEqual(CommandStepKind.Wait, plan.StepAt(0).Kind);
            Assert.AreEqual(3f, plan.StepAt(0).Seconds, 0.001f);
            Assert.AreEqual(CompanionCommandIntent.SkillNova, IntentAt(plan, 1));
        }

        [Test]
        public void BareDefendKeywordStillClassifiesAsDefend()
        {
            var plan = CommandPlanParser.ParseLocal("대기해");
            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(CompanionCommandIntent.Defend, IntentAt(plan, 0));
        }

        [Test]
        public void CompoundKeywordIsConsumedWholeAndNotTwice()
        {
            // "방어태세" contains "방어": a naive scan would emit Defend twice.
            var plan = CommandPlanParser.ParseLocal("방어태세 갖춰");
            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(CompanionCommandIntent.Defend, IntentAt(plan, 0));
        }

        [Test]
        public void RepeatedOrderCollapses()
        {
            var plan = CommandPlanParser.ParseLocal("공격해 공격해");
            Assert.AreEqual(1, plan.Count);
        }

        [Test]
        public void AdjacentWaitsMerge()
        {
            var plan = CommandPlanParser.ParseLocal("2초 기다렸다가 1초 기다렸다가 노바");
            Assert.AreEqual(2, plan.Count);
            Assert.AreEqual(CommandStepKind.Wait, plan.StepAt(0).Kind);
            Assert.AreEqual(3f, plan.StepAt(0).Seconds, 0.001f);
            Assert.AreEqual(CompanionCommandIntent.SkillNova, IntentAt(plan, 1));
        }

        [Test]
        public void SequenceEndingOnAWaitDropsIt()
        {
            // A countdown to nothing is not a command.
            Assert.IsTrue(CommandPlanParser.ParseLocal("3초 기다려").IsEmpty);
            var plan = CommandPlanParser.ParseLocal("노바 쓰고 3초 기다려");
            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(CompanionCommandIntent.SkillNova, IntentAt(plan, 0));
        }

        [Test]
        public void UnorderableSentenceIsEmptySoTheCallerCanEscalate()
        {
            Assert.IsTrue(CommandPlanParser.ParseLocal("저기 왼쪽 구석으로 가").IsEmpty);
            Assert.IsTrue(CommandPlanParser.ParseLocal("   ").IsEmpty);
            Assert.IsTrue(CommandPlanParser.ParseLocal(null).IsEmpty);
        }

        [Test]
        public void PlanIsCappedAtMaxSteps()
        {
            var plan = CommandPlanParser.ParseLocal("노바 결계 파동 화살 질주 복귀 특기 집중공격");
            Assert.AreEqual(CommandPlan.MaxSteps, plan.Count);
        }

        // ------------------------------------------------------------- json --

        [Test]
        public void ModelObjectBecomesAnOrderedPlan()
        {
            const string json =
                "{\"summary\":\"방패 먼저\",\"steps\":[" +
                "{\"do\":\"SkillAegis\",\"say\":\"버티기\"}," +
                "{\"do\":\"Wait\",\"sec\":2}," +
                "{\"do\":\"SkillNova\"}]}";
            var plan = CommandPlanParser.ParseJson(json);

            Assert.AreEqual(3, plan.Count);
            Assert.AreEqual(CommandPlanSource.Gemini, plan.Source);
            Assert.AreEqual("방패 먼저", plan.Summary);
            Assert.AreEqual(CompanionCommandIntent.SkillAegis, IntentAt(plan, 0));
            Assert.AreEqual("버티기", plan.StepAt(0).Say);
            Assert.AreEqual(CommandStepKind.Wait, plan.StepAt(1).Kind);
            Assert.AreEqual(2f, plan.StepAt(1).Seconds, 0.001f);
            Assert.AreEqual(CompanionCommandIntent.SkillNova, IntentAt(plan, 2));
        }

        [Test]
        public void FencedOrProseWrappedReplyStillParses()
        {
            const string json =
                "여기 있습니다:\n```json\n{\"steps\":[{\"do\":\"Recall\"}]}\n```\n";
            var plan = CommandPlanParser.ParseJson(json);
            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(CompanionCommandIntent.Recall, IntentAt(plan, 0));
        }

        [Test]
        public void BareArrayAndBareStringsAreAccepted()
        {
            var plan = CommandPlanParser.ParseJson("[{\"do\":\"focus_attack\"},\"SkillDash\",\"노바\"]");
            Assert.AreEqual(3, plan.Count);
            Assert.AreEqual(CompanionCommandIntent.FocusAttack, IntentAt(plan, 0));
            Assert.AreEqual(CompanionCommandIntent.SkillDash, IntentAt(plan, 1));
            Assert.AreEqual(CompanionCommandIntent.SkillNova, IntentAt(plan, 2));
        }

        [Test]
        public void InventedVocabularyIsDroppedNotGuessed()
        {
            var plan = CommandPlanParser.ParseJson(
                "{\"steps\":[{\"do\":\"Teleport\"},{\"do\":\"SkillNova\"}]}");
            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(CompanionCommandIntent.SkillNova, IntentAt(plan, 0));
        }

        [Test]
        public void SecondsOnAnActStepBecomeATrailingDwell()
        {
            var plan = CommandPlanParser.ParseJson(
                "{\"steps\":[{\"do\":\"SkillNova\",\"sec\":2},{\"do\":\"Recall\"}]}");
            Assert.AreEqual(3, plan.Count);
            Assert.AreEqual(CompanionCommandIntent.SkillNova, IntentAt(plan, 0));
            Assert.AreEqual(CommandStepKind.Wait, plan.StepAt(1).Kind);
            Assert.AreEqual(CompanionCommandIntent.Recall, IntentAt(plan, 2));
        }

        [Test]
        public void OverlongWaitIsClamped()
        {
            var plan = CommandPlanParser.ParseJson(
                "{\"steps\":[{\"do\":\"Wait\",\"sec\":600},{\"do\":\"Recall\"}]}");
            Assert.AreEqual(CommandStep.MaxWaitSeconds, plan.StepAt(0).Seconds, 0.001f);
        }

        [Test]
        public void GarbageNeverThrowsAndNeverInvents()
        {
            Assert.IsTrue(CommandPlanParser.ParseJson(null).IsEmpty);
            Assert.IsTrue(CommandPlanParser.ParseJson("").IsEmpty);
            Assert.IsTrue(CommandPlanParser.ParseJson("not json at all").IsEmpty);
            Assert.IsTrue(CommandPlanParser.ParseJson("{\"steps\":[").IsEmpty);
            Assert.IsTrue(CommandPlanParser.ParseJson("{\"steps\":{}}").IsEmpty);
            Assert.IsTrue(CommandPlanParser.ParseJson("{\"steps\":[{\"do\":\"Wait\",\"sec\":1}]}").IsEmpty,
                "a plan that is nothing but a dwell commands nothing");
        }

        // --------------------------------------------------------- envelope --

        [Test]
        public void GeminiEnvelopeSurvivesJsonInsideJson()
        {
            // A real generateContent reply carries the plan as a JSON STRING, so
            // every quote arrives escaped. Skipping escapes (the old one-word
            // extractor did) would hand the parser a broken document.
            const string inner =
                "{\"summary\":\"방패 먼저\",\"steps\":[{\"do\":\"SkillAegis\"}," +
                "{\"do\":\"Wait\",\"sec\":2},{\"do\":\"SkillNova\"}]}";
            var envelope = "{\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[{\"text\":\"" +
                inner.Replace("\\", "\\\\").Replace("\"", "\\\"") +
                "\"}]}}]}";

            var payload = GeminiCommandClient.ExtractFirstText(envelope, 2048);
            Assert.AreEqual(inner, payload);

            var plan = CommandPlanParser.ParseJson(payload);
            Assert.AreEqual(3, plan.Count);
            Assert.AreEqual("방패 먼저", plan.Summary);
            Assert.AreEqual(CompanionCommandIntent.SkillAegis, IntentAt(plan, 0));
            Assert.AreEqual(CompanionCommandIntent.SkillNova, IntentAt(plan, 2));
        }

        [Test]
        public void ExtractorDecodesUnicodeEscapesAndRespectsItsLimit()
        {
            // \uXXXX is how the API encodes Hangul when it escapes non-ASCII.
            const string envelope =
                "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"\\ub178\\ubc14\"}]}}]}";
            Assert.AreEqual("노바", GeminiCommandClient.ExtractFirstText(envelope));

            const string longEnvelope =
                "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"abcdefghij\"}]}}]}";
            Assert.AreEqual("abcd", GeminiCommandClient.ExtractFirstText(longEnvelope, 4));
        }
    }
}
