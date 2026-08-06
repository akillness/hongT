// Regression guard for the duplicated-Hangul bug in the companion command
// console. Root cause: TWO writers reached the same uGUI InputField — the
// field's own KeyPressed/Append path (still fed with the IME-committed
// syllable) and HudView's Keyboard.onTextInput mirror (required because
// activeInputHandler:1 kills the legacy stream uGUI would read). Every typed
// syllable therefore landed twice.
//
// The fix is structural: the field is readOnly, so only the mirror writes.
// These tests drive the field's own writer directly (InputField.ProcessEvent ->
// KeyPressed -> Append) and assert it stays inert.
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class CommandConsoleFieldTests
    {
        private GameObject _hudObject;
        private HudView _hud;

        [SetUp]
        public void SetUp()
        {
            _hudObject = new GameObject("CommandConsoleFieldTests");
            _hud = _hudObject.AddComponent<HudView>();
            _hud.Build();
            // OpenCommandConsole is dungeon-gated (orders need a guardian on
            // the field) — same order GameView uses to begin a run.
            _hud.EnableCampaignUi("차가운 회랑", 3);
            _hud.EnableDungeonUi("재의 감시자");
            _hud.ApplyLayout(1280, 720, new Rect(0, 0, 1280, 720));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_hudObject);
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null) Object.DestroyImmediate(eventSystem.gameObject);
        }

        private InputField OpenConsoleField()
        {
            _hud.ToggleCommandConsole();
            Assert.IsTrue(_hud.CommandConsoleOpen, "the dungeon HUD must accept the console");
            var field = _hudObject.GetComponentInChildren<InputField>(true);
            Assert.IsNotNull(field, "the console builds exactly one InputField");
            return field;
        }

        [Test]
        public void TheConsoleFieldNeverWritesItself()
        {
            var field = OpenConsoleField();
            Assert.IsTrue(field.readOnly,
                "readOnly is the whole fix: it makes InputField.Append/Backspace inert " +
                "so the onTextInput mirror is the single writer");

            // This is the exact path that produced the second copy.
            field.ProcessEvent(new Event { type = EventType.KeyDown, character = 'a' });
            Assert.AreEqual(string.Empty, field.text,
                "a KeyDown reaching the field must not append — that is the duplicate");
        }

        [Test]
        public void TheConsoleFieldKeepsTheSixtyCharacterCommandCap()
        {
            var field = OpenConsoleField();
            Assert.AreEqual(60, field.characterLimit,
                "the field cap and CommandConsoleBuffer's cap must stay in lockstep");
            Assert.AreEqual(InputField.LineType.SingleLine, field.lineType);
        }

        [Test]
        public void EachConsoleSessionStartsFromAnEmptyCommandLine()
        {
            var field = OpenConsoleField();
            field.text = "이전 명령";          // leftover from the previous session
            _hud.ToggleCommandConsole();
            Assert.IsFalse(_hud.CommandConsoleOpen, "toggle closes an open console");

            _hud.ToggleCommandConsole();
            Assert.IsTrue(_hud.CommandConsoleOpen, "reopening works after a close");
            Assert.AreEqual(string.Empty, field.text,
                "opening clears the field and the buffer together");
        }


    }
}
