// EditMode gates for the room-objective slice of the dungeon-revival spec
// (_workspace/current/design/deep-interview-cinder-court-dungeon-revival.md):
//
//   (a) every room carries its OWN win condition — non-empty and unique, so a
//       contiguous route never issues the same instruction twice,
//   (b) StageCatalog.ObjectiveFor resolves a room id to that line and yields ""
//       for arena/prologue/unknown ids instead of a stale one,
//   (c) the HUD actually exposes it: hidden with no objective, shown as the
//       room line, re-framed while the room boss lives, re-labelled across a
//       direct room handoff, cleared by ResetRunUi,
//   (d) the chip stays non-interactive (mobile layout contract (c) — decorative
//       rects must never eat a tap).
//
// The HUD half drives the same entry order GameView uses to begin a run
// (Build -> EnableCampaignUi -> EnableDungeonUi) and reads the state back
// through HudView.RoomObjectiveReadout rather than reflection.
using System.Collections.Generic;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class RoomObjectiveTests
    {
        private GameObject _hudObject;
        private HudView _hud;
        private bool _hadRotateHintPref;

        [SetUp]
        public void SetUp()
        {
            // EnableDungeonUi -> ShowRotateHintIfPortrait writes this pref;
            // snapshot so the suite never pollutes the developer's editor.
            _hadRotateHintPref = PlayerPrefs.HasKey("al:rotate-hint");

            _hudObject = new GameObject("RoomObjectiveTests");
            _hud = _hudObject.AddComponent<HudView>();
            _hud.Build();
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
            if (!_hadRotateHintPref) PlayerPrefs.DeleteKey("al:rotate-hint");
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------- catalog (a) --

        [Test]
        public void EveryRoom_CarriesANonEmptyObjective()
        {
            var entries = StageCatalog.Entries;
            Assert.That(entries.Count, Is.GreaterThan(0), "the catalog must expose rooms");
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                Assert.That(entry.RoomObjective, Is.Not.Null.And.Not.Empty,
                    entry.Id + " must state what the room wants");
                Assert.That(entry.RoomObjective.Trim(), Is.EqualTo(entry.RoomObjective),
                    entry.Id + " objective must not carry padding whitespace");
            }
        }

        [Test]
        public void RoomObjectives_AreUniqueAcrossTheWholeRoute()
        {
            var entries = StageCatalog.Entries;
            var seen = new Dictionary<string, string>();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                Assert.That(seen.ContainsKey(entry.RoomObjective), Is.False,
                    entry.Id + " repeats the objective already used by " +
                    (seen.TryGetValue(entry.RoomObjective, out var owner) ? owner : "?"));
                seen[entry.RoomObjective] = entry.Id;
            }
            Assert.That(seen.Count, Is.EqualTo(entries.Count));
        }

        [Test]
        public void RoomObjective_IsDistinctFromTheRoomTitleAndDisplayName()
        {
            // A title is a name, not an instruction. If they ever collapse into the
            // same string the chip stops telling the player anything new.
            var entries = StageCatalog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                Assert.That(entry.RoomObjective, Is.Not.EqualTo(entry.Title), entry.Id);
                Assert.That(entry.RoomObjective, Is.Not.EqualTo(entry.DisplayName), entry.Id);
            }
        }

        // ------------------------------------------------------- lookup (b) --

        [Test]
        public void ObjectiveFor_ResolvesEveryRoomIdToItsOwnLine()
        {
            var entries = StageCatalog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                Assert.That(StageCatalog.ObjectiveFor(entry.Id), Is.EqualTo(entry.RoomObjective),
                    entry.Id + " must resolve to its own objective");
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("arena")]
        [TestCase("prologue")]
        [TestCase("Cinder-Span")]      // ids are ordinal/case-sensitive
        [TestCase("no-such-room")]
        public void ObjectiveFor_YieldsEmptyForEverythingThatIsNotARoom(string stageId)
        {
            Assert.That(StageCatalog.ObjectiveFor(stageId), Is.Empty,
                "a non-room id must not inherit a room's objective");
        }

        // ---------------------------------------------------------- hud (c) --

        [Test]
        public void Chip_StartsHiddenBeforeAnyObjectiveArrives()
        {
            Assert.That(_hud.RoomObjectiveReadout, Is.Empty,
                "EnableDungeonUi must not show an objective it has not been given");
        }

        [Test]
        public void Chip_StaysHiddenForArenaAndPrologueRuns()
        {
            // GameView passes "" for every non-dungeon mode.
            _hud.SyncRoomObjective("", false);
            Assert.That(_hud.RoomObjectiveReadout, Is.Empty);
            _hud.SyncRoomObjective(null, true);
            Assert.That(_hud.RoomObjectiveReadout, Is.Empty);
        }

        [Test]
        public void Chip_ShowsTheRoomObjectiveWhileTheRoomIsBeingCleared()
        {
            var objective = StageCatalog.ObjectiveFor("cinder-span");
            _hud.SyncRoomObjective(objective, false);

            var readout = _hud.RoomObjectiveReadout;
            Assert.That(readout, Is.Not.Empty, "the chip must be visible during the room");
            StringAssert.Contains(objective, readout);
        }

        [Test]
        public void Chip_ReframesTheSameObjectiveWhileTheRoomBossLives()
        {
            var objective = StageCatalog.ObjectiveFor("echo-throne");

            _hud.SyncRoomObjective(objective, false);
            var duringWaves = _hud.RoomObjectiveReadout;

            _hud.SyncRoomObjective(objective, true);
            var duringBoss = _hud.RoomObjectiveReadout;

            StringAssert.Contains(objective, duringBoss);
            Assert.That(duringBoss, Is.Not.EqualTo(duringWaves),
                "the boss beat must read differently from the wave beat");

            // ...and the transition is reversible (retry re-enters the wave phase).
            _hud.SyncRoomObjective(objective, false);
            Assert.That(_hud.RoomObjectiveReadout, Is.EqualTo(duringWaves));
        }

        [Test]
        public void Chip_HidesAgainWhenTheObjectiveIsWithdrawn()
        {
            _hud.SyncRoomObjective(StageCatalog.ObjectiveFor("abyss-chancel"), false);
            Assert.That(_hud.RoomObjectiveReadout, Is.Not.Empty);

            _hud.SyncRoomObjective(null, false);
            Assert.That(_hud.RoomObjectiveReadout, Is.Empty,
                "a withdrawn objective must not linger on screen");
        }

        [Test]
        public void Chip_RelabelsAcrossADirectRoomHandoff()
        {
            // The revived route hands off room->room with no lobby return, so the
            // cached relabel key must not pin the previous room's line.
            var first = StageCatalog.ObjectiveFor("cinder-span");
            var second = StageCatalog.ObjectiveFor("ember-gallery");
            Assert.That(second, Is.Not.EqualTo(first), "fixture precondition");

            _hud.SyncRoomObjective(first, false);
            Assert.That(_hud.RoomObjectiveReadout, Does.Contain(first));

            _hud.RefreshDungeonStage("불씨 회랑", 3, "Cinder Warden", true);
            _hud.SyncRoomObjective(second, false);

            var readout = _hud.RoomObjectiveReadout;
            StringAssert.Contains(second, readout);
            Assert.That(readout, Does.Not.Contain(first),
                "the previous room's objective must be gone after the handoff");
        }

        [Test]
        public void ResetRunUi_ClearsTheObjectiveChip()
        {
            _hud.SyncRoomObjective(StageCatalog.ObjectiveFor("ash-verdict"), true);
            Assert.That(_hud.RoomObjectiveReadout, Is.Not.Empty);

            _hud.ResetRunUi();   // every run entry / retry passes through here
            Assert.That(_hud.RoomObjectiveReadout, Is.Empty,
                "a new run must not open on the previous room's objective");
        }

        // ---------------------------------------------------------- hud (d) --

        [Test]
        public void Chip_NeverInterceptsPointerInput()
        {
            var objective = StageCatalog.ObjectiveFor("witness-well");
            _hud.SyncRoomObjective(objective, false);

            var label = FindObjectiveLabel(objective);
            Assert.That(label, Is.Not.Null, "the objective text must exist in the hierarchy");

            var chipRoot = label.transform.parent;
            Assert.That(chipRoot, Is.Not.Null);
            var graphics = chipRoot.GetComponentsInChildren<Graphic>(true);
            Assert.That(graphics.Length, Is.GreaterThan(0));
            foreach (var graphic in graphics)
                Assert.That(graphic.raycastTarget, Is.False,
                    graphic.name + " is decorative and must not eat a tap");
        }

        private Text FindObjectiveLabel(string objective)
        {
            var texts = _hudObject.GetComponentsInChildren<Text>(true);
            foreach (var text in texts)
                if (text.text != null && text.text.Contains(objective))
                    return text;
            return null;
        }
    }
}
