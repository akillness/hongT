// The basic strike has to be findable without being told.
//
// Playtest feedback (2026-08-12): "it looks like I'm supposed to press something to
// attack, but I can't work out what to press", and separately "the screen is a mess
// of fighting and I don't know what I'm meant to do".
//
// Those two sentences are two different defects and this file holds one assertion
// for each, because the fix for one does not touch the other:
//
//   1. NO PERMANENT SURFACE. Every other control on the bottom stack names its key
//      on a card that never goes away — SHIFT, Q, E, R, F. The strike, the action the
//      player performs most, had none on desktop. Touch already had a labelled 110 u
//      button, which is exactly why the gap survived: the geometry work happened on
//      the touch layout, and the desktop path was never the one being measured.
//
//   2. THE TOAST QUEUE COLLAPSES. GameDirector queues several control lessons at
//      once and drains ONE PER SIM TICK onto a single shared 4.5 s surface. At 60 Hz
//      the whole backlog overwrites itself in a sixth of a second, every bit is
//      marked seen forever, and the player reads whichever card happened to be last.
//      CLAUDE.md §4j already states the rule this breaks — "안내 큐는 한 번에 한
//      장만 뽑는다" — and the pause tier honours it via GuidancePaused. The toast
//      tier had no equivalent gate.
//
// The second is the one that makes the feedback's "I don't know what I'm meant to
// do" a property of the build rather than of the player: the game DID explain, at a
// speed nobody can read.
using System.Collections.Generic;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class StrikeAffordanceTests
    {
        GameObject _hudObject;
        HudView _hud;

        [SetUp]
        public void SetUp()
        {
            _hudObject = new GameObject("StrikeAffordanceTests");
            _hud = _hudObject.AddComponent<HudView>();
            _hud.Build();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_hudObject);
            var eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem != null) Object.DestroyImmediate(eventSystem.gameObject);
        }

        void ArrangeDesktopDungeon()
        {
            _hud.EnableCampaignUi("차가운 회랑", 3);
            _hud.EnableDungeonUi("재의 감시자");
            _hud.SetCampaignSurfacesVisible(true);
            _hud.HidePrologueToast();
            _hud.ApplyLayout(1280, 720, new Rect(0, 0, 1280, 720));
            Assert.That(_hud.CurrentTier, Is.EqualTo(HudView.LayoutTier.Full),
                "1280x720 landscape must classify as Full tier — otherwise this "
                + "fixture is measuring a layout the assertion is not about");
        }

        static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        /// <summary>
        /// The count that found the defect, kept as the assertion that holds it shut.
        ///
        /// Deliberately phrased as "the strike is not the only unlabelled action"
        /// rather than "a label exists at (x, y)". A position assertion passes after
        /// the label has been moved somewhere useless; this one only passes while the
        /// strike is as discoverable as the four skills beside it.
        /// </summary>
        [Test]
        public void DesktopDungeon_StrikeNamesItsKey_LikeEveryOtherBottomStackControl()
        {
            ArrangeDesktopDungeon();

            Assert.That(_hud.StrikeKeyLabelVisibleForTest, Is.True,
                "the desktop HUD names SHIFT, Q, E, R and F on permanent cards but "
                + "left the basic strike — the action the player performs most — with "
                + "no permanent surface at all. Its only mentions were a toast that "
                + "shows once and a prologue step that is gone by wave 2.");

            var rect = _hud.StrikeKeyLabelRectForTest;
            Assert.That(rect, Is.Not.Null);
            var world = WorldRect(rect);
            Assert.That(world.width > 0f && world.height > 0f, Is.True,
                "degenerate rect — the layout did not resolve, so every assertion "
                + "below would be measuring nothing");
        }

        /// <summary>
        /// Containment, because a legend outside the canvas is the failure §4n
        /// records: the guidance tab overflowed its panel by 238 u while nine tests
        /// asked what its rows SAID and none asked where they were.
        /// </summary>
        [Test]
        public void DesktopDungeon_StrikeLegend_StaysInsideTheCanvas()
        {
            ArrangeDesktopDungeon();
            var canvas = _hudObject.GetComponentInChildren<Canvas>(true);
            canvas.renderMode = RenderMode.WorldSpace;
            Canvas.ForceUpdateCanvases();

            var canvasRect = WorldRect((RectTransform)canvas.transform);
            Assert.That(canvasRect.width > 0f, Is.True, "canvas has no size");

            var world = WorldRect(_hud.StrikeKeyLabelRectForTest);
            Assert.That(canvasRect.Contains(new Vector2(world.xMin, world.yMin))
                        && canvasRect.Contains(new Vector2(world.xMax, world.yMax)),
                Is.True,
                $"strike legend {world} escapes the canvas {canvasRect}");
        }

        /// <summary>
        /// The legend must not sit on top of what it labels. This is the single-
        /// builder rule (§4f) turned into an assertion: the legend and the pips are
        /// authored in one function and positioned in the same two branches, and this
        /// is what says so out loud.
        ///
        /// MUTATION that proves it can fail: set StrikeKeyGap to -80 and the legend
        /// slides under the first pip.
        /// </summary>
        [Test]
        public void DesktopDungeon_StrikeLegend_ClearsThePipsAndTheSkillRow()
        {
            ArrangeDesktopDungeon();
            var canvas = _hudObject.GetComponentInChildren<Canvas>(true);
            canvas.renderMode = RenderMode.WorldSpace;
            Canvas.ForceUpdateCanvases();

            var legend = WorldRect(_hud.StrikeKeyLabelRectForTest);
            // BOTH collectors. The first draft swept only the skill row and was
            // structurally incapable of failing: the cards live at y 18..94 and the
            // legend at y 102..122, so no displacement along x could ever produce an
            // overlap. A mutation that slid the legend 80 u INTO the pips passed
            // green. The pips — the thing this legend actually labels and the thing
            // it can actually collide with — are in the READOUT collector, not the
            // skill-row one. This is §4m in its plainest form: a test that measures in
            // a coordinate system where right and wrong coincide proves nothing, and
            // the only way to find out is to break the code on purpose (§4q).
            var others = new List<RectTransform>();
            _hud.CollectSkillRowRectsForTest(others);
            var skillRowCount = others.Count;
            _hud.CollectDungeonReadoutRectsForTest(others);
            Assert.That(skillRowCount, Is.GreaterThanOrEqualTo(5),
                "skill row rects missing — the overlap assertion would be vacuous, "
                + "which is the shape of failure this repo has hit four times (§4s)");
            Assert.That(others.Count - skillRowCount, Is.GreaterThanOrEqualTo(3),
                "the combo pips are missing from the sweep, and they are the rects "
                + "this legend sits next to — without them the test cannot fail");

            // PREMISE, and it is not decoration. The first draft of this test passed
            // under a mutation that slid the legend 80 u INTO the pips, which means it
            // could not fail and was worth nothing. The reason is that an overlap test
            // is only a test if the rects it compares are real and are in the same
            // coordinate system — so both facts get asserted before the comparison,
            // and the report below prints the geometry so a future failure is legible
            // rather than a bare boolean.
            Assert.That(legend.width > 0f && legend.height > 0f, Is.True,
                $"legend rect is degenerate {legend} — nothing can overlap nothing");
            var report = new System.Text.StringBuilder();
            report.AppendLine($"  legend  x[{legend.xMin:F1},{legend.xMax:F1}] "
                + $"y[{legend.yMin:F1},{legend.yMax:F1}]");
            var real = 0;
            foreach (var other in others)
            {
                var r = WorldRect(other);
                if (r.width > 0f && r.height > 0f) real += 1;
                report.AppendLine($"  {other.name,-16} x[{r.xMin:F1},{r.xMax:F1}] "
                    + $"y[{r.yMin:F1},{r.yMax:F1}]");
            }
            TestContext.WriteLine("[strike legend vs bottom stack, Full tier]\n" + report);
            Assert.That(real, Is.EqualTo(others.Count),
                "some bottom-stack rects are degenerate, so this sweep silently "
                + "excludes them:\n" + report);

            var hits = new List<string>();
            foreach (var other in others)
            {
                var b = WorldRect(other);
                if (!legend.Overlaps(b)) continue;
                var w = Mathf.Min(legend.xMax, b.xMax) - Mathf.Max(legend.xMin, b.xMin);
                var h = Mathf.Min(legend.yMax, b.yMax) - Mathf.Max(legend.yMin, b.yMin);
                hits.Add($"{other.name} by {w:F1}x{h:F1} u");
            }
            Assert.That(hits, Is.Empty,
                "the strike legend overlaps what it labels:\n  " + string.Join("\n  ", hits));
        }

        /// <summary>
        /// Touch must NOT get a second name for the strike. It already has a labelled
        /// 110 u button, and two names for one action is the over-explaining the
        /// survey found players punish (§4j). Stated as an assertion because "we
        /// decided not to" is the kind of unanimous decision that ends up undefended
        /// (§4q) — nobody argued, so nobody wrote it down.
        /// </summary>
        [Test]
        public void TouchDungeon_DoesNotRepeatTheStrikeName()
        {
            _hud.ForceTouchControlsForTest();
            _hud.EnableCampaignUi("차가운 회랑", 3);
            _hud.EnableDungeonUi("재의 감시자");
            _hud.SetCampaignSurfacesVisible(true);
            _hud.HidePrologueToast();
            _hud.ApplyLayout(390, 844, new Rect(0, 0, 390, 844));
            Assert.That(_hud.CurrentTier, Is.EqualTo(HudView.LayoutTier.Phone));

            Assert.That(_hud.StrikeKeyLabelVisibleForTest, Is.False,
                "touch already labels its strike button; a keyboard legend here would "
                + "name the same action twice and would also run left into the 260 u "
                + "joystick catch box");
        }

        /// <summary>
        /// The dwell gate the toast drainer depends on.
        ///
        /// WHAT THIS PROVES AND WHAT IT DOES NOT. It proves the predicate is real:
        /// the shared band reports itself occupied for the full dwell after a card is
        /// shown, so a drainer that asks will wait. It does NOT prove the drainer
        /// asks — GameDirector is a MonoBehaviour whose HUD reference is wired at
        /// scene build, and no test in this suite instantiates one, so there is no
        /// in-house way to drive DrainGuidanceQueue from here.
        ///
        /// That half was proven by MUTATION instead (§4q): removing the
        /// `if (_hud.GuidanceToastBusy) return;` line and running the browser smoke
        /// puts the whole control backlog on screen inside a sixth of a second again.
        /// Recorded here rather than left unsaid, because a test whose name implies
        /// more than it checks is worse than no test — this repo has hit that four
        /// times (§4m, §4n, §4s).
        /// </summary>
        [Test]
        public void GuidanceToast_HoldsTheSharedBandForItsFullDwell()
        {
            _hud.EnableCampaignUi("차가운 회랑", 3);
            _hud.EnableDungeonUi("재의 감시자");
            _hud.HidePrologueToast();

            Assert.That(_hud.GuidanceToastBusy, Is.False,
                "premise: the band must start free, or 'it is busy after' proves "
                + "nothing about the call that came between");

            _hud.ShowGuidanceToast("연격", "Space를 이어 치면 3타.");

            Assert.That(_hud.GuidanceToastBusy, Is.True,
                "a card was just shown and the band reports itself free — the drainer "
                + "would immediately overwrite it with the next queued lesson, which "
                + "is the defect this gate exists to close");
            Assert.That(HudView.GuidanceToastSeconds, Is.GreaterThan(2f),
                "the dwell is the whole mechanism: a sub-two-second window is not a "
                + "readable card, it is the same collapse with a longer stride");
        }
    }
}
