// Command console classification contract: Korean-first keywords, ordered
// rules (skill words beat the generic 방어), and the Gemini reply word mapping.
// Pure string logic — no scene, no network.
using NUnit.Framework;
using CinderCourt.View;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class CompanionCommandParserTests
    {
        [TestCase("집중공격", CompanionCommandIntent.FocusAttack)]
        [TestCase("집중 공격해", CompanionCommandIntent.FocusAttack)]
        [TestCase("저놈 잡아", CompanionCommandIntent.FocusAttack)]
        [TestCase("방어태세", CompanionCommandIntent.Defend)]
        [TestCase("방어 태세 갖춰", CompanionCommandIntent.Defend)]
        [TestCase("나를 지켜", CompanionCommandIntent.Defend)]
        [TestCase("복귀", CompanionCommandIntent.Recall)]
        [TestCase("이리와", CompanionCommandIntent.Recall)]
        [TestCase("돌아와", CompanionCommandIntent.Recall)]
        [TestCase("아이템 획득", CompanionCommandIntent.PickupInfo)]
        [TestCase("저거 주워", CompanionCommandIntent.PickupInfo)]
        [TestCase("노바", CompanionCommandIntent.SkillNova)]
        [TestCase("잿불 노바 써", CompanionCommandIntent.SkillNova)]
        [TestCase("결계", CompanionCommandIntent.SkillAegis)]
        [TestCase("방패 올려", CompanionCommandIntent.SkillAegis)]
        [TestCase("파동", CompanionCommandIntent.SkillPulse)]
        [TestCase("화살 쏴", CompanionCommandIntent.SkillBolt)]
        [TestCase("질주", CompanionCommandIntent.SkillDash)]
        [TestCase("FOCUS the enemy", CompanionCommandIntent.FocusAttack)]
        [TestCase("defend me", CompanionCommandIntent.Defend)]
        public void Parse_ClassifiesKnownCommands(string text, CompanionCommandIntent expected)
        {
            Assert.That(CompanionCommandParser.Parse(text), Is.EqualTo(expected));
        }

        [Test]
        public void Parse_SkillWordsBeatGenericDefense()
        {
            // "결계" must not fall through to 방어 even in a defensive sentence.
            Assert.That(CompanionCommandParser.Parse("방어하게 결계 쳐줘"),
                Is.EqualTo(CompanionCommandIntent.SkillAegis));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("오늘 날씨 어때")]
        public void Parse_UnknownAndEmptyFallThrough(string text)
        {
            Assert.That(CompanionCommandParser.Parse(text),
                Is.EqualTo(CompanionCommandIntent.Unknown));
        }

        [TestCase("FocusAttack", CompanionCommandIntent.FocusAttack)]
        [TestCase("focusattack", CompanionCommandIntent.FocusAttack)]
        [TestCase("  Defend  ", CompanionCommandIntent.Defend)]
        [TestCase("SkillNova.", CompanionCommandIntent.SkillNova)]
        [TestCase("Recall\n", CompanionCommandIntent.Recall)]
        [TestCase("PickupInfo", CompanionCommandIntent.PickupInfo)]
        [TestCase("banana", CompanionCommandIntent.Unknown)]
        [TestCase("", CompanionCommandIntent.Unknown)]
        [TestCase(null, CompanionCommandIntent.Unknown)]
        public void FromIntentWord_MapsGeminiReplies(string word, CompanionCommandIntent expected)
        {
            Assert.That(CompanionCommandParser.FromIntentWord(word), Is.EqualTo(expected));
        }

        [Test]
        public void ExtractFirstText_PullsIntentWordFromGeminiJson()
        {
            const string reply = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"FocusAttack\"}],"
                + "\"role\":\"model\"},\"finishReason\":\"STOP\"}]}";
            Assert.That(GeminiCommandClient.ExtractFirstText(reply), Is.EqualTo("FocusAttack"));
            Assert.That(GeminiCommandClient.ExtractFirstText(null), Is.Null);
            Assert.That(GeminiCommandClient.ExtractFirstText("{}"), Is.Null);
        }
    }
}
