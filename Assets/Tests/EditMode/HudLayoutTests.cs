// EditMode regression guard for the mobile layout contract
// (_workspace mobile-layout spec, 검증 계약):
//   (a) zero rect-rect overlap among INTERACTIVE surfaces of the same
//       active surface set at phone tier,
//   (b) every interactive rect >= 44 CSS px at the worst measured phone
//       scale (0.488 CSS px per canvas unit),
//   (c) every non-interactive Graphic keeps raycastTarget == false so
//       invisible rects cannot eat taps (joystick corner is dense),
//   (d) LayoutTier thresholds at effective width 700/980 + portrait force.
//
// Screen.width/height are read-only and degenerate in batchmode, so the
// tests drive HudView.ApplyLayout(width, height, safeArea) — the injected
// geometry seam — and force-build the touch surfaces Build() gates on
// hardware. The HUD canvas is switched to WorldSpace and sized to the
// effective phone canvas so world-space rects are measurable canvas units.
using System.Collections.Generic;
using System.Text;
using CinderCourt.View;
using CinderCourt.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class HudLayoutTests
    {
        // Worst measured phone viewport (mobile-layout spec): 390x844 CSS,
        // portrait match 0.35 -> ~799 u effective width, 0.488 CSS px/u.
        private const int PhoneWidth = 390;
        private const int PhoneHeight = 844;
        private const float SpecCssPerUnit = 0.488f;
        private const float MinCssPx = 44f;
        // Interactive rects may touch but not stack (<= 1 u counts as touch).
        private const float OverlapEpsilon = 1f;

        private GameObject _hudObject;
        private HudView _hud;
        private bool _hadRotateHintPref;
        private bool _hadReducedMotionPref;
        private int _reducedMotionPrefValue;

        [SetUp]
        public void SetUp()
        {
            // EnableDungeonUi -> ShowRotateHintIfPortrait writes this pref;
            // snapshot so the suite never pollutes the developer's editor.
            _hadRotateHintPref = PlayerPrefs.HasKey("al:rotate-hint");
            _hadReducedMotionPref = PlayerPrefs.HasKey("al:reduced-motion");
            _reducedMotionPrefValue = PlayerPrefs.GetInt("al:reduced-motion");

            _hudObject = new GameObject("HudLayoutTests");
            _hud = _hudObject.AddComponent<HudView>();
            _hud.Build();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_hudObject);
            var eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null) Object.DestroyImmediate(eventSystem.gameObject);
            if (!_hadRotateHintPref) PlayerPrefs.DeleteKey("al:rotate-hint");
            ViewPrefs.ReducedMotion = _reducedMotionPrefValue == 1;
            if (_hadReducedMotionPref)
                PlayerPrefs.SetInt("al:reduced-motion", _reducedMotionPrefValue);
            else
                PlayerPrefs.DeleteKey("al:reduced-motion");
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------- helpers --

        /// <summary>Phone-portrait geometry through the injected seam, touch
        /// surfaces forced (batchmode has no Touchscreen device), canvas in
        /// WorldSpace sized to the effective width so world rects are canvas
        /// units. Asserts the seam produced real geometry — a regression back
        /// to Screen.* reads (0x0 in batchmode) must fail loudly here, not
        /// pass silently on a zero-size canvas.</summary>
        private Canvas ArrangePhone(bool dungeon)
        {
            _hud.ForceTouchControlsForTest();
            if (dungeon)
            {
                // GameView.Begin order for a dungeon run (GameView.cs:82-92).
                _hud.EnableCampaignUi("차가운 회랑", 3);
                _hud.EnableDungeonUi("재의 감시자");
                _hud.SetCampaignSurfacesVisible(true);
                _hud.HidePrologueToast();   // rotate hint may fire on a portrait editor
            }

            _hud.ApplyLayout(PhoneWidth, PhoneHeight,
                new Rect(0, 0, PhoneWidth, PhoneHeight));

            Assert.That(float.IsFinite(_hud.LastEffectiveWidth) && _hud.LastEffectiveWidth > 0f,
                Is.True, $"effective width must be finite and positive, got {_hud.LastEffectiveWidth}");
            Assert.That(_hud.CurrentTier, Is.EqualTo(HudView.LayoutTier.Phone),
                "390x844 portrait must classify as Phone tier");
            // Spec cross-check: 390 CSS px over ~799 u => ~0.488 CSS px/u.
            Assert.That(PhoneWidth / _hud.LastEffectiveWidth,
                Is.EqualTo(SpecCssPerUnit).Within(0.005f),
                "phone scale factor drifted from the spec's measured 0.488 CSS px/u");

            var canvas = _hudObject.GetComponentInChildren<Canvas>(true);
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvas.transform;
            var effectiveHeight = _hud.LastEffectiveWidth * PhoneHeight / PhoneWidth;
            canvasRect.sizeDelta = new Vector2(_hud.LastEffectiveWidth, effectiveHeight);
            Canvas.ForceUpdateCanvases();

            Assert.That(canvasRect.rect.width,
                Is.EqualTo(_hud.LastEffectiveWidth).Within(0.01f),
                "canvas failed to take the effective phone size");
            return canvas;
        }

        /// <summary>All ACTIVE interactive surfaces: Buttons plus the pointer-
        /// handler touch panels (joystick catch, strike/dash TouchHold).</summary>
        private static List<RectTransform> InteractiveRects(Canvas canvas)
        {
            var rects = new List<RectTransform>();
            foreach (var handler in canvas.GetComponentsInChildren<IPointerDownHandler>(false))
            {
                var rect = ((Component)handler).transform as RectTransform;
                if (rect != null && !rects.Contains(rect)) rects.Add(rect);
            }
            return rects;
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
            return sb.ToString();
        }

        private static void AssertNoPairwiseOverlap(List<RectTransform> rects)
        {
            var violations = new List<string>();
            for (var i = 0; i < rects.Count; i++)
            {
                var a = WorldRect(rects[i]);
                Assert.That(a.width > 0f && a.height > 0f, Is.True,
                    $"degenerate rect (layout did not resolve): {Path(rects[i].transform)}");
                for (var j = i + 1; j < rects.Count; j++)
                {
                    var b = WorldRect(rects[j]);
                    var overlapX = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                    var overlapY = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                    if (overlapX > OverlapEpsilon && overlapY > OverlapEpsilon)
                        violations.Add(
                            $"{Path(rects[i].transform)} {a} overlaps " +
                            $"{Path(rects[j].transform)} {b} by {overlapX:F1}x{overlapY:F1} u");
                }
            }
            Assert.That(violations, Is.Empty,
                "interactive surfaces overlap at phone tier:\n" + string.Join("\n", violations));
        }

        private void AssertTouchFloor(List<RectTransform> rects)
        {
            // Real scale from the seam (~0.4883), not the rounded constant —
            // asserted equal to the spec value within 0.005 in ArrangePhone.
            var cssPerUnit = PhoneWidth / _hud.LastEffectiveWidth;
            var violations = new List<string>();
            foreach (var rect in rects)
            {
                var world = WorldRect(rect);
                var w = world.width * cssPerUnit;
                var h = world.height * cssPerUnit;
                if (w < MinCssPx || h < MinCssPx)
                    violations.Add(
                        $"{Path(rect.transform)}: {world.width:F0}x{world.height:F0} u " +
                        $"= {w:F1}x{h:F1} CSS px (< {MinCssPx})");
            }
            Assert.That(violations, Is.Empty,
                "touch targets under the 44 CSS px floor:\n" + string.Join("\n", violations));
        }

        private Image HealthFill()
        {
            foreach (var text in _hudObject.GetComponentsInChildren<Text>(true))
            {
                if (!text.text.StartsWith("체력 ")) continue;
                var fill = text.transform.parent.Find("Fill")?.GetComponent<Image>();
                Assert.That(fill, Is.Not.Null, "the rendered health label must retain its visible fill");
                return fill;
            }
            Assert.Fail("HUD did not render a health value");
            return null;
        }

        // --------------------------------------------------------- tests --

        [Test]
        public void PhoneDungeon_InteractiveSurfaces_DoNotOverlap()
        {
            var canvas = ArrangePhone(dungeon: true);
            var rects = InteractiveRects(canvas);
            // mute + 4 skill cards + dash card + joystick catch + strike + dash touch
            Assert.That(rects.Count, Is.GreaterThanOrEqualTo(9),
                "dungeon phone surface set lost interactive elements — test would be vacuous");
            AssertNoPairwiseOverlap(rects);
        }

        [Test]
        public void PhoneDungeon_TouchTargets_AtLeast44CssPx()
        {
            var canvas = ArrangePhone(dungeon: true);
            AssertTouchFloor(InteractiveRects(canvas));
        }

        [Test]
        public void PhoneArena_InteractiveSurfaces_NoOverlap_And44PxFloor()
        {
            // Arena set: Q/E cards live, dungeon row absent (GameView arena
            // path never calls EnableDungeonUi).
            var canvas = ArrangePhone(dungeon: false);
            var rects = InteractiveRects(canvas);
            // mute + nova + ward + joystick catch + strike (dash touch is
            // dungeon-only and stays inactive)
            Assert.That(rects.Count, Is.GreaterThanOrEqualTo(5),
                "arena phone surface set lost interactive elements — test would be vacuous");
            AssertNoPairwiseOverlap(rects);
            AssertTouchFloor(rects);
        }

        [Test]
        public void NonInteractiveGraphics_DoNotEatTaps()
        {
            var canvas = ArrangePhone(dungeon: true);
            var violations = new List<string>();
            var interactiveHits = 0;
            foreach (var graphic in canvas.GetComponentsInChildren<Graphic>(false))
            {
                var isHitSurface = graphic.GetComponent<IPointerDownHandler>() != null;
                if (isHitSurface) interactiveHits++;
                if (graphic.raycastTarget && !isHitSurface)
                    violations.Add($"{Path(graphic.transform)} ({graphic.GetType().Name})");
            }
            // Positive control: the hit surfaces themselves must stay
            // raycastable, or every button/joystick would be dead.
            Assert.That(interactiveHits, Is.GreaterThanOrEqualTo(9),
                "interactive hit surfaces lost their raycastable Graphic");
            Assert.That(violations, Is.Empty,
                "non-interactive Graphics with raycastTarget on (they eat taps):\n"
                + string.Join("\n", violations));
        }

        [Test]
        public void ReducedMotion_UpdatesPresentationPolicyAndPersists()
        {
            ViewPrefs.ReducedMotion = false;
            Assert.That(ViewPrefs.ReducedMotion, Is.False);
            Assert.That(ViewPrefs.MotionScale, Is.EqualTo(1f));
            Assert.That(ViewPrefs.TimeEffectsAllowed, Is.True);
            Assert.That(PlayerPrefs.GetInt("al:reduced-motion"), Is.EqualTo(0));

            ViewPrefs.ReducedMotion = true;
            Assert.That(ViewPrefs.ReducedMotion, Is.True);
            Assert.That(ViewPrefs.MotionScale, Is.EqualTo(0.4f));
            Assert.That(ViewPrefs.TimeEffectsAllowed, Is.False);
            Assert.That(PlayerPrefs.GetInt("al:reduced-motion"), Is.EqualTo(1));
        }

        [Test]
        public void RetryModalVisible_TracksActivePanel_NotPendingClearCeremony()
        {
            _hud.EnableCampaignUi("차가운 회랑", 3);
            Assert.That(_hud.RetryModalVisible, Is.False,
                "no retry panel is active before a terminal result");

            _hud.ShowStageClear(new CinderSim().Digest);
            Assert.That(_hud.RetryModalVisible, Is.False,
                "the stage-clear ceremony must not enable retry before its panel is visible");

            _hud.OnEvents(SimEvents.GameOver, new CinderSim());
            Assert.That(_hud.RetryModalVisible, Is.True,
                "an active game-over retry panel must enable the retry shortcut");

            _hud.ResetRunUi();
            Assert.That(_hud.RetryModalVisible, Is.False,
                "resetting the visible terminal panel must disable the retry shortcut");
        }

        [Test]
        public void ResetRunUi_ReseedsHealthBarForNewRun()
        {
            var boostedConfig = CampaignStages.ForIndex(0, weaponRank: 0, lanternRank: 0, cloakRank: 5);
            var boostedRun = new CinderSim(in boostedConfig);
            _hud.Sync(boostedRun);
            Assert.That(HealthFill().fillAmount, Is.EqualTo(1f).Within(0.001f),
                "the initial high-health run must fill its visible health bar");

            _hud.ResetRunUi();
            var newRun = new CinderSim();
            _hud.Sync(newRun);

            Assert.That(HealthFill().fillAmount, Is.EqualTo(1f).Within(0.001f),
                "a fresh 100-health run must not inherit the prior run's health denominator");
        }

        [Test]
        public void TierThresholds_ClassifyByEffectiveWidth()
        {
            void Apply(int w, int h) => _hud.ApplyLayout(w, h, new Rect(0, 0, w, h));

            // 16:9 landscape at reference resolution: scale exactly 1.
            Apply(1280, 720);
            Assert.That(_hud.CurrentTier, Is.EqualTo(HudView.LayoutTier.Full));
            Assert.That(_hud.LastEffectiveWidth, Is.EqualTo(1280f).Within(0.5f));

            // Square landscape window: log-lerp gives sqrt(1280*720) = 960 u,
            // independent of the window's absolute size -> Compact band.
            Apply(1000, 1000);
            Assert.That(_hud.CurrentTier, Is.EqualTo(HudView.LayoutTier.Compact));
            Assert.That(_hud.LastEffectiveWidth, Is.EqualTo(960f).Within(0.5f));

            // Bracket the 980 boundary: 1040x1000 -> ~979 u (Compact),
            // 1043x1000 -> ~980.4 u (Full).
            Apply(1040, 1000);
            Assert.That(_hud.LastEffectiveWidth, Is.LessThan(980f));
            Assert.That(_hud.CurrentTier, Is.EqualTo(HudView.LayoutTier.Compact));
            Apply(1043, 1000);
            Assert.That(_hud.LastEffectiveWidth, Is.GreaterThanOrEqualTo(980f));
            Assert.That(_hud.CurrentTier, Is.EqualTo(HudView.LayoutTier.Full));

            // The 700 u Phone edge is unreachable in landscape (min effective
            // width for w>=h is 960 u): Phone comes from the portrait force.
            Apply(PhoneWidth, PhoneHeight);
            Assert.That(_hud.CurrentTier, Is.EqualTo(HudView.LayoutTier.Phone));
            Assert.That(_hud.LastEffectiveWidth, Is.EqualTo(798.7f).Within(1f));

            // Same phone rotated to landscape grades by width thresholds
            // (spec: thresholds only grade landscape windows) -> Full.
            Apply(PhoneHeight, PhoneWidth);
            Assert.That(_hud.CurrentTier, Is.EqualTo(HudView.LayoutTier.Full));
            Assert.That(float.IsFinite(_hud.LastEffectiveWidth) && _hud.LastEffectiveWidth > 0f,
                Is.True, "effective width must stay finite across orientations");
        }
    }
}
