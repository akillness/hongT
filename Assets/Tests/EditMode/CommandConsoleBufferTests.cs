// Command console text-entry contract. The bug this pins: with an IME active
// the console received every Hangul syllable twice ("한" -> "한한") because two
// writers reached the same uGUI InputField. HudView now keeps the field
// readOnly (structural fix) and routes keystrokes through CommandConsoleBuffer,
// which also drops a character delivered twice inside one frame.
// Pure string logic — no scene, no input devices.
using CinderCourt.View;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class CommandConsoleBufferTests
    {
        static CommandConsoleBuffer NewBuffer(int limit = 60) => new CommandConsoleBuffer(limit);

        static string Type(CommandConsoleBuffer buffer, string text, int startFrame = 1)
        {
            var frame = startFrame;
            foreach (var c in text) buffer.Feed(c, frame++);
            return buffer.Text;
        }

        [Test]
        public void PlainTypingAppendsEachCharacterExactlyOnce()
        {
            var buffer = NewBuffer();
            Assert.AreEqual("집중공격", Type(buffer, "집중공격"));
            Assert.AreEqual(4, buffer.Length);
        }

        [Test]
        public void TheSameCharacterDeliveredTwiceInOneFrameIsAcceptedOnlyOnce()
        {
            var buffer = NewBuffer();
            Assert.IsTrue(buffer.Feed('한', 12), "first delivery must be accepted");
            Assert.IsFalse(buffer.Feed('한', 12), "same frame echo must be dropped");
            Assert.AreEqual("한", buffer.Text);
        }

        [Test]
        public void ARealDoubleLetterOnTheNextFrameIsKept()
        {
            var buffer = NewBuffer();
            buffer.Feed('ㅋ', 12);
            Assert.IsTrue(buffer.Feed('ㅋ', 13), "a repeat on a later frame is genuine typing");
            Assert.AreEqual("ㅋㅋ", buffer.Text);
        }

        [Test]
        public void DifferentCharactersInOneFrameAreBothKept()
        {
            var buffer = NewBuffer();
            Assert.IsTrue(buffer.Feed('a', 7));
            Assert.IsTrue(buffer.Feed('b', 7), "only an identical char is an echo");
            Assert.AreEqual("ab", buffer.Text);
        }

        [Test]
        public void BackspaceRemovesTheLastCharacterAndIsANoOpWhenEmpty()
        {
            var buffer = NewBuffer();
            Type(buffer, "노바");
            Assert.IsTrue(buffer.Feed(CommandConsoleBuffer.Backspace, 30));
            Assert.AreEqual("노", buffer.Text);
            Assert.IsTrue(buffer.Feed(CommandConsoleBuffer.Delete, 31));
            Assert.AreEqual(string.Empty, buffer.Text);
            Assert.IsFalse(buffer.Feed(CommandConsoleBuffer.Backspace, 32), "empty buffer cannot shrink");
            Assert.AreEqual(string.Empty, buffer.Text);
        }

        [Test]
        public void RetypingTheSameCharacterAfterABackspaceInTheSameFrameIsKept()
        {
            // Deletion closes the duplicate window: this is a correction, not an echo.
            var buffer = NewBuffer();
            buffer.Feed('가', 40);
            buffer.Feed(CommandConsoleBuffer.Backspace, 40);
            Assert.IsTrue(buffer.Feed('가', 40));
            Assert.AreEqual("가", buffer.Text);
        }

        [TestCase('\n')]
        [TestCase('\r')]
        [TestCase('\t')]
        [TestCase((char)27)]
        [TestCase((char)1)]
        public void ControlCharactersNeverEnterTheText(char control)
        {
            var buffer = NewBuffer();
            Assert.IsFalse(buffer.Feed(control, 5));
            Assert.AreEqual(string.Empty, buffer.Text);
        }

        [Test]
        public void TheCharacterLimitIsHardAndLeavesTheTextIntact()
        {
            var buffer = NewBuffer(limit: 3);
            Type(buffer, "abc");
            Assert.IsFalse(buffer.Feed('d', 100), "past the cap the feed is rejected");
            Assert.AreEqual("abc", buffer.Text);
            Assert.IsTrue(buffer.Feed(CommandConsoleBuffer.Backspace, 101));
            Assert.IsTrue(buffer.Feed('d', 102), "room again after a delete");
            Assert.AreEqual("abd", buffer.Text);
        }

        [Test]
        public void ClearResetsTextAndTheDuplicateWindow()
        {
            var buffer = NewBuffer();
            buffer.Feed('x', 9);
            buffer.Clear();
            Assert.AreEqual(string.Empty, buffer.Text);
            Assert.AreEqual(0, buffer.Length);
            Assert.IsTrue(buffer.Feed('x', 9), "a fresh console session is not an echo of the old one");
            Assert.AreEqual("x", buffer.Text);
        }
    }
}
