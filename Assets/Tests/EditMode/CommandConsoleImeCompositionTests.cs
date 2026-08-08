// W11 — browser IME composition contract for the command console.
// The bug class this pins: a Hangul syllable is BUILT in front of the caret
// ("ㄱ" -> "가" -> "각") and every step must REPLACE the last one. Appending
// instead reproduces the duplication the console already paid for once
// (_workspace/current/qa/command-console-hangul-duplication.md).
// Pure string logic — no scene, no browser, no WebGL build.
using CinderCourt.View;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class CommandConsoleImeCompositionTests
    {
        const int Limit = 60;

        static CommandConsoleImeComposition NewIme(int limit = Limit)
            => new CommandConsoleImeComposition(new CommandConsoleBuffer(limit), limit);

        static void Compose(CommandConsoleImeComposition ime, params string[] steps)
        {
            ime.BeginComposition();
            foreach (var step in steps) ime.UpdateComposition(step);
        }

        [Test]
        public void TheComposingSyllableIsReplacedNeverAppended()
        {
            var ime = NewIme();
            Compose(ime, "ㄱ", "가", "각");
            Assert.AreEqual("각", ime.Text, "each compositionupdate replaces the whole pre-edit");
            Assert.AreEqual(string.Empty, ime.CommittedText, "nothing is committed while composing");
            Assert.IsTrue(ime.IsComposing);
        }

        [Test]
        public void CommittingTheSyllableMovesItIntoTheCommandText()
        {
            var ime = NewIme();
            Compose(ime, "ㄱ", "가", "각");
            Assert.IsTrue(ime.EndComposition("각", 10));
            Assert.AreEqual("각", ime.CommittedText);
            Assert.AreEqual("각", ime.Text);
            Assert.AreEqual(string.Empty, ime.Composition);
            Assert.IsFalse(ime.IsComposing);
        }

        [Test]
        public void ANewCompositionAfterACommitStartsFromAnEmptyPreEdit()
        {
            var ime = NewIme();
            Compose(ime, "가");
            ime.EndComposition("가", 10);
            ime.BeginComposition();
            Assert.AreEqual(string.Empty, ime.Composition,
                "a fresh composition must not inherit the committed syllable");
            Assert.AreEqual("가", ime.Text);
            ime.UpdateComposition("ㄴ");
            ime.UpdateComposition("나");
            Assert.AreEqual("가나", ime.Text);
            ime.EndComposition("나", 11);
            Assert.AreEqual("가나", ime.CommittedText);
        }

        [Test]
        public void BackspaceWhileComposingEatsThePreEditAndNeverTheCommittedText()
        {
            var ime = NewIme();
            ime.Insert("노바", 5);
            Compose(ime, "ㄱ", "각");
            Assert.IsTrue(ime.DeleteBackward(6));
            Assert.AreEqual("노바", ime.Text, "the pre-edit is gone");
            Assert.IsFalse(ime.DeleteBackward(6), "an empty pre-edit does not chew into committed text");
            Assert.AreEqual("노바", ime.CommittedText);
        }

        [Test]
        public void AShorteningCompositionUpdateIsTheImeOwnBackspace()
        {
            // What Chrome/Safari actually send when the player deletes a jamo:
            // a compositionupdate with a shorter pre-edit, not a Backspace key.
            var ime = NewIme();
            Compose(ime, "ㄱ", "가", "각", "가");
            Assert.AreEqual("가", ime.Text);
            Assert.IsTrue(ime.IsComposing);
        }

        [Test]
        public void CancellingAKeepsTheCommittedTextAndDropsThePreEdit()
        {
            var ime = NewIme();
            ime.Insert("집중", 1);
            Compose(ime, "ㄱ", "공");
            Assert.IsTrue(ime.CancelComposition());
            Assert.AreEqual("집중", ime.Text);
            Assert.IsFalse(ime.IsComposing);
            Assert.IsFalse(ime.CancelComposition(), "a second cancel changes nothing");
        }

        [Test]
        public void EnglishTypingAndHangulCompositionShareOneCommandLine()
        {
            var ime = NewIme();
            ime.Insert("nova ", 1);
            Compose(ime, "ㄱ", "가", "각");
            ime.EndComposition("각", 2);
            ime.Insert("!", 3);
            Assert.AreEqual("nova 각!", ime.CommittedText);
            Assert.AreEqual("nova 각!", ime.Text);
        }

        [Test]
        public void APlainInsertCommitsAnyLivePreEditFirst()
        {
            var ime = NewIme();
            Compose(ime, "ㄱ", "가");
            ime.Insert("x", 4);
            Assert.AreEqual("가x", ime.CommittedText, "the syllable the player saw is kept, in order");
            Assert.IsFalse(ime.IsComposing);
        }

        [Test]
        public void ARepeatedSyllableInsideOneCommitSurvivesTheEchoGuard()
        {
            // The buffer drops a character delivered twice in ONE frame — that
            // guard is for two racing event sources, and a commit is one event.
            var ime = NewIme();
            ime.BeginComposition();
            ime.UpdateComposition("ㅋㅋ");
            Assert.IsTrue(ime.EndComposition("ㅋㅋ", 7));
            Assert.AreEqual("ㅋㅋ", ime.CommittedText);
        }

        [Test]
        public void AKeystrokeMatchingTheJustCommittedCharacterIsNotAnEcho()
        {
            var buffer = new CommandConsoleBuffer(Limit);
            var ime = new CommandConsoleImeComposition(buffer, Limit);
            ime.BeginComposition();
            ime.UpdateComposition("가");
            ime.EndComposition("가", 7);
            Assert.IsTrue(buffer.Feed('가', 7), "the commit closed the duplicate window");
            Assert.AreEqual("가가", ime.Text);
        }

        [Test]
        public void TheCharacterLimitCountsTheLiveSyllableToo()
        {
            var ime = NewIme(limit: 3);
            ime.Insert("abc", 1);
            ime.BeginComposition();
            Assert.IsFalse(ime.UpdateComposition("가"), "no room left for a pre-edit");
            Assert.AreEqual("abc", ime.Text);
            Assert.IsFalse(ime.EndComposition("가", 2), "and none for its commit");
            Assert.AreEqual("abc", ime.CommittedText);
        }

        [Test]
        public void ControlCharactersReachNeitherThePreEditNorTheCommit()
        {
            var ime = NewIme();
            ime.BeginComposition();
            Assert.IsFalse(ime.UpdateComposition("\n\t"));
            Assert.AreEqual(string.Empty, ime.Text);
            ime.EndComposition("가\n나", 3);
            Assert.AreEqual("가나", ime.CommittedText);
        }

        [Test]
        public void DeleteBackwardOutsideACompositionRemovesCommittedText()
        {
            var ime = NewIme();
            ime.Insert("결계", 1);
            Assert.IsTrue(ime.DeleteBackward(2));
            Assert.AreEqual("결", ime.Text);
            Assert.IsTrue(ime.DeleteBackward(3));
            Assert.IsFalse(ime.DeleteBackward(4), "an empty line cannot shrink");
            Assert.AreEqual(string.Empty, ime.Text);
        }

        [Test]
        public void FlushCommitsThePreEditSoAnEarlyEnterKeepsTheSyllable()
        {
            var ime = NewIme();
            ime.Insert("집중", 1);
            Compose(ime, "ㄱ", "공", "공격");
            Assert.AreEqual("집중공격", ime.Flush(9));
            Assert.IsFalse(ime.IsComposing);
            Assert.AreEqual("집중공격", ime.CommittedText);
        }

        [Test]
        public void ClearEndsAnyLiveCompositionSoTheNextSessionOpensClean()
        {
            var ime = NewIme();
            Compose(ime, "ㄱ", "가");
            ime.Clear();
            Assert.IsFalse(ime.IsComposing);
            Assert.AreEqual(string.Empty, ime.Composition);
        }

        [Test]
        public void ACompositionEndWithNoTextIsACancelNotACommit()
        {
            // Chrome reports an aborted composition as compositionend(data: "").
            var ime = NewIme();
            ime.Insert("방어", 1);
            Compose(ime, "ㄱ", "각");
            Assert.IsTrue(ime.EndComposition(string.Empty, 5));
            Assert.AreEqual("방어", ime.CommittedText);
            Assert.IsFalse(ime.IsComposing);
        }
    }
}
