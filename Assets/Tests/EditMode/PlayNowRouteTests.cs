// The one-button route into gameplay.
//
// Playtest feedback (2026-08-12): "there is no tutorial and I cannot tell what the UI
// is, so I cannot start", and "a first-timer cannot even work out what kind of game
// this is". The lobby asked for four decisions — read three unlabelled glyphs, open
// the right one, expand the right act, pick among nine cards — before the first frame
// of play, and every card was locked behind a training run nobody had mentioned.
//
// What is worth pinning here is NOT the button's pixels. It is the two properties that
// make the route work and that a later edit could quietly break:
//
//   1. the View's target literal and the director's constant stay the same string, and
//   2. the destination is never a locked or non-existent stage, at any save state.
//
// (2) matters most: a "start" button that lands on a locked card is worse than no
// button, because the player has now pressed the obvious thing and still cannot play.
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class PlayNowRouteTests
    {
        [Test]
        public void ViewAndDirector_ShareTheSameTargetLiteral()
        {
            // The View cannot reference GameDirector, so the string is duplicated by
            // necessity. Duplicated-by-necessity still means one fact with two sources
            // (CLAUDE.md §4i) — this assertion is what keeps them from drifting.
            Assert.That(LobbyView.GameDirectorPlayNowTarget,
                Is.EqualTo(GameDirector.PlayNowTarget),
                "the lobby button would silently route to nothing");
        }

        /// <summary>
        /// Mirrors GameDirector.StartPlayNow's choice, so the test tracks the RULE
        /// rather than a recorded answer. A recorded answer would also pass if the
        /// rule were replaced by a different one that happened to agree on the saves
        /// this test lists — the coordinate system where right and wrong coincide (§4m).
        /// </summary>
        static string Destination(in CampaignData data)
        {
            if (!data.PrologueDone) return "prologue";
            string fallback = null;
            foreach (var entry in StageCatalog.Entries)
            {
                if (!StageCatalog.IsUnlocked(in data, in entry)) continue;
                fallback = entry.Id;
                if (!StageCatalog.IsCleared(in data, in entry)) return entry.Id;
            }
            return fallback ?? "prologue";
        }

        [Test]
        public void FreshSave_GoesToTheTutorial()
        {
            var data = default(CampaignData);
            Assert.That(Destination(in data), Is.EqualTo("prologue"),
                "a newcomer must land in the only mode that teaches");
        }

        [Test]
        public void AfterPrologue_GoesToTheFirstUnclearedStage()
        {
            var data = default(CampaignData);
            data.PrologueDone = true;
            var destination = Destination(in data);

            Assert.That(StageCatalog.TryGet(destination, out var entry), Is.True, destination);
            Assert.That(StageCatalog.IsUnlocked(in data, in entry), Is.True,
                "the one-button route must never land on a locked card");
            Assert.That(StageCatalog.IsCleared(in data, in entry), Is.False,
                "'play' means continue, not replay something already cleared");
        }

        [Test]
        public void EveryClearedSave_StillLandsSomewherePlayable()
        {
            // The completionist case. A button that does nothing once the game is
            // finished is the run-holding-surface failure (§4o) pointing the other way:
            // a control that stays visible after it stops meaning anything.
            var data = default(CampaignData);
            data.PrologueDone = true;
            data.ClearedMask = StageCatalog.ValidClearMask;

            var destination = Destination(in data);
            Assert.That(destination, Is.Not.Null.And.Not.Empty);
            if (destination == "prologue") return;   // legal: nothing unlocked at all

            Assert.That(StageCatalog.TryGet(destination, out var entry), Is.True, destination);
            Assert.That(StageCatalog.IsUnlocked(in data, in entry), Is.True,
                "a fully cleared save must still route to an unlocked stage");
        }

        [Test]
        public void PartialProgress_SkipsClearedStagesInOrder()
        {
            var data = default(CampaignData);
            data.PrologueDone = true;

            // Clear stages one at a time; the destination must advance and must never
            // repeat a stage that has just been cleared.
            for (var step = 0; step < 3; step += 1)
            {
                var destination = Destination(in data);
                Assert.That(StageCatalog.TryGet(destination, out var entry), Is.True, destination);
                Assert.That(StageCatalog.IsCleared(in data, in entry), Is.False,
                    $"step {step}: routed to an already-cleared stage");
                data.ClearedMask |= 1 << entry.CatalogIndex;
            }
        }
    }
}
