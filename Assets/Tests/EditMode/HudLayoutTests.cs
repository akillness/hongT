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
            var eventSystem = Object.FindAnyObjectByType<EventSystem>();
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
        private static PreparationOffer Preparation(PreparationOfferKind kind, int variant, int magnitude)
        {
            return new PreparationOffer
            {
                Kind = kind,
                Variant = variant,
                Magnitude = magnitude
            };
        }

        private static Button VisibleButtonWithText(Canvas canvas, string text)
        {
            foreach (var button in canvas.GetComponentsInChildren<Button>(false))
            {
                var label = button.GetComponentInChildren<Text>();
                if (label != null && label.text.Contains(text)) return button;
            }
            Assert.Fail($"visible button text missing: {text}");
            return null;
        }

        private static void AssertVisibleText(Canvas canvas, string text)
        {
            foreach (var label in canvas.GetComponentsInChildren<Text>(false))
            {
                if (label.text.Contains(text)) return;
            }
            Assert.Fail($"visible text missing: {text}");
        }

        private static List<RectTransform> ButtonRects(params Button[] buttons)
        {
            var rects = new List<RectTransform>();
            foreach (var button in buttons)
            {
                var rect = button.transform as RectTransform;
                Assert.That(rect, Is.Not.Null, $"button lacks a RectTransform: {button.name}");
                rects.Add(rect);
            }
            return rects;
        }

        private static void AssertRaycastableActions(params Button[] buttons)
        {
            foreach (var button in buttons)
            {
                Assert.That(button.gameObject.activeInHierarchy, Is.True,
                    $"visible Ember Rest action is inactive: {button.name}");
                Assert.That(button.targetGraphic, Is.Not.Null,
                    $"visible Ember Rest action has no raycast Graphic: {button.name}");
                Assert.That(button.targetGraphic.raycastTarget, Is.True,
                    $"visible Ember Rest action cannot receive taps: {button.name}");
            }
        }

        private static void AssertHiddenActions(params Button[] buttons)
        {
            foreach (var button in buttons)
            {
                Assert.That(button.gameObject.activeInHierarchy, Is.False,
                    $"hidden Ember Rest action still occupies the raycast hierarchy: {button.name}");
            }
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

        [Test]
        public void PrologueToast_DescribesDesktopOrTouchControls()
        {
            var canvas = _hudObject.GetComponentInChildren<Canvas>(true);

            _hud.ShowPrologueToast(0);
            AssertVisibleText(canvas, "이동 — W A S D 또는 방향키");
            _hud.ShowPrologueToast(1);
            AssertVisibleText(canvas, "타격 — Space");

            _hud.ForceTouchControlsForTest();
            _hud.ShowPrologueToast(0);
            AssertVisibleText(canvas, "이동 — 왼쪽 조이스틱 드래그");
            _hud.ShowPrologueToast(1);
            AssertVisibleText(canvas, "타격 — 오른쪽 타격 버튼");
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

        /// <summary>§U1 blind-spot closure: InteractiveRects() only collects
        /// pointer handlers, so the skill row could bury non-interactive
        /// readouts (xp bar, level, combo pips, shield) while every existing
        /// test stayed green — the user-reported "skill overlay". Grade the
        /// card×readout pair set explicitly, at BOTH tiers.</summary>
        [Test]
        public void PhoneDungeon_SkillRow_DoesNotCoverReadouts()
        {
            ArrangePhone(dungeon: true);
            AssertSkillRowClearOfReadouts();
        }

        [Test]
        public void DesktopDungeon_SkillRow_DoesNotCoverReadouts()
        {
            // Desktop landscape reference (1280x720): the measured pre-fix
            // failure — combo pips at y=52 sat inside the dash card (18..106).
            _hud.EnableCampaignUi("차가운 회랑", 3);
            _hud.EnableDungeonUi("재의 감시자");
            _hud.SetCampaignSurfacesVisible(true);
            _hud.HidePrologueToast();
            _hud.ApplyLayout(1280, 720, new Rect(0, 0, 1280, 720));
            Assert.That(_hud.CurrentTier, Is.EqualTo(HudView.LayoutTier.Full),
                "1280x720 landscape must classify as Full tier");
            AssertSkillRowClearOfReadouts();
        }

        private void AssertSkillRowClearOfReadouts()
        {
            var cards = new List<RectTransform>();
            var readouts = new List<RectTransform>();
            _hud.CollectSkillRowRectsForTest(cards);
            _hud.CollectDungeonReadoutRectsForTest(readouts);
            Assert.That(cards.Count, Is.GreaterThanOrEqualTo(5),
                "skill row rects missing — assertion would be vacuous");
            Assert.That(readouts.Count, Is.GreaterThanOrEqualTo(5),
                "readout rects missing — assertion would be vacuous");
            Canvas.ForceUpdateCanvases();
            var violations = new List<string>();
            foreach (var card in cards)
            {
                var a = WorldRect(card);
                foreach (var readout in readouts)
                {
                    var b = WorldRect(readout);
                    var overlapX = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                    var overlapY = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                    if (overlapX > OverlapEpsilon && overlapY > OverlapEpsilon)
                        violations.Add(
                            $"{Path(card.transform)} {a} covers {Path(readout.transform)} {b}");
                }
            }
            Assert.That(violations, Is.Empty,
                "skill row buries readouts:\n" + string.Join("\n", violations));
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
        public void GameOver_HidesCombatTouchTargets_ClearsTouchInput_AndRestoresOnResume()
        {
            var input = _hudObject.AddComponent<InputAdapter>();
            _hud.Input = input;
            ArrangePhone(dungeon: true);

            var targets = new List<RectTransform>();
            _hud.CollectCombatTouchTargetsForTest(targets);
            Assert.That(targets, Has.Count.EqualTo(3),
                "the virtual joystick, strike, and dungeon dash hit targets must all be testable");
            foreach (var target in targets)
            {
                Assert.That(target.gameObject.activeInHierarchy, Is.True,
                    $"combat touch target was not active before game over: {Path(target)}");
                var hitGraphic = target.GetComponent<Graphic>();
                Assert.That(hitGraphic, Is.Not.Null,
                    $"combat touch target lacks its visual hit surface: {Path(target)}");
                Assert.That(hitGraphic.raycastTarget, Is.True,
                    $"active combat touch target cannot receive taps: {Path(target)}");
            }

            input.TouchMoveX = 0.75f;
            input.TouchMoveY = -0.5f;
            input.QueueAttack();
            input.QueueDash();
            _hud.OnEvents(SimEvents.GameOver, new CinderSim());

            foreach (var target in targets)
                Assert.That(target.gameObject.activeInHierarchy, Is.False,
                    $"game-over modal left a combat touch target tappable: {Path(target)}");
            var gameOverInput = input.Sample();
            Assert.That(gameOverInput.MoveX, Is.Zero,
                "game over must clear a held virtual-joystick horizontal move");
            Assert.That(gameOverInput.MoveY, Is.Zero,
                "game over must clear a held virtual-joystick vertical move");
            Assert.That(gameOverInput.AttackQueued, Is.False,
                "game over must discard a queued strike from a hidden touch target");
            Assert.That(gameOverInput.DashQueued, Is.False,
                "game over must discard a queued dash from a hidden touch target");

            _hud.Sync(new CinderSim());

            foreach (var target in targets)
            {
                Assert.That(target.gameObject.activeInHierarchy, Is.True,
                    $"normal gameplay did not restore combat touch target: {Path(target)}");
                Assert.That(target.GetComponent<Graphic>().raycastTarget, Is.True,
                    $"restored combat touch target cannot receive taps: {Path(target)}");
            }
        }

        [Test]
        public void PhoneEmberRest_OffersThreeEffects_WithReplacementWordingAnd44PxActions()
        {
            var canvas = ArrangePhone(dungeon: true);
            var attack = Preparation(PreparationOfferKind.Stat, 1, 2);
            var gravePulse = Preparation(PreparationOfferKind.SkillRune, 2, 1);
            var companionRange = Preparation(PreparationOfferKind.GuardianResonance, 2, 2);

            _hud.ShowEmberRestForTest(2, attack, gravePulse, companionRange);
            Canvas.ForceUpdateCanvases();

            Assert.That(_hud.EmberRestVisible, Is.True,
                "a prepared nonfinal room must expose the actionable Ember Rest panel");
            AssertVisibleText(canvas, "다음 방에 적용 (이전 준비 대체)");

            var attackButton = VisibleButtonWithText(canvas, "Attack +2");
            var gravePulseButton = VisibleButtonWithText(canvas, "Grave Pulse +10% tick damage");
            var companionRangeButton = VisibleButtonWithText(canvas, "Companion range +40 px");
            var deferButton = VisibleButtonWithText(canvas, "준비 보류");
            var continueButton = VisibleButtonWithText(canvas, "계속");
            Assert.That(attackButton, Is.Not.SameAs(gravePulseButton));
            Assert.That(gravePulseButton, Is.Not.SameAs(companionRangeButton));
            Assert.That(continueButton.interactable, Is.False,
                "continuation must require an explicit offer selection or defer");

            var actions = new[] { attackButton, gravePulseButton, companionRangeButton, deferButton, continueButton };
            AssertRaycastableActions(actions);
            AssertNoPairwiseOverlap(ButtonRects(actions));
            AssertTouchFloor(ButtonRects(actions));

            gravePulseButton.onClick.Invoke();
            Assert.That(continueButton.interactable, Is.True,
                "selecting a visible offer must unlock the explicit continuation control");
            AssertVisibleText(canvas, "선택됨: Grave Pulse +10% tick damage");
            AssertVisibleText(canvas, "다음 방에 적용 (이전 준비 대체)");

            deferButton.onClick.Invoke();
            AssertVisibleText(canvas, "준비 보류");
            AssertVisibleText(canvas, "다음 방에 적용 (이전 준비 대체)");
        }

        [Test]
        public void EmberRest_HideAndRunReset_RemovePanelRaycastsAndDecisionState()
        {
            var canvas = ArrangePhone(dungeon: true);
            var attack = Preparation(PreparationOfferKind.Stat, 1, 1);
            var gravePulse = Preparation(PreparationOfferKind.SkillRune, 2, 2);
            var companionRange = Preparation(PreparationOfferKind.GuardianResonance, 2, 1);

            _hud.ShowEmberRestForTest(2, attack, gravePulse, companionRange);
            var actions = new[]
            {
                VisibleButtonWithText(canvas, "Attack +1"),
                VisibleButtonWithText(canvas, "Grave Pulse +20% tick damage"),
                VisibleButtonWithText(canvas, "Companion range +20 px"),
                VisibleButtonWithText(canvas, "준비 보류"),
                VisibleButtonWithText(canvas, "계속")
            };
            var continueButton = actions[4];
            AssertRaycastableActions(actions);
            actions[3].onClick.Invoke();
            Assert.That(continueButton.interactable, Is.True,
                "defer is an explicit none choice that enables continuation");

            _hud.HideEmberRest();
            Assert.That(_hud.EmberRestVisible, Is.False,
                "hiding the panel must remove its modal interaction surface");
            AssertHiddenActions(actions);

            _hud.ShowEmberRestForTest(3, attack, gravePulse, companionRange);
            Assert.That(continueButton.interactable, Is.False,
                "reopening Ember Rest must not retain a prior defer or selection");
            _hud.ResetRunUi();
            Assert.That(_hud.EmberRestVisible, Is.False,
                "every run entry reset must hide an outstanding Ember Rest panel");
            AssertHiddenActions(actions);
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

        // ---------------------------------------------------------------
        // Left column below the meters — the band four functions write to.
        //
        // Written BEFORE the fix, deliberately, to settle which overlaps are
        // real. The interview reported three; one of them (phone stats x
        // shield, 24 u) came from reading the shield's BUILD coordinate
        // (HudView.cs:1902, y -98) without noticing ApplyDungeonTier:587-590
        // moves it to -252 on phone. That claim is refuted below rather than
        // quietly dropped: the shield is a child of dungeonRoot (:1898), so
        // the guard at :536 that would leave it at build y also means there
        // is no shield to collide with.
        //
        // Bands are [y - h, y]: Label/Panel measure y DOWN from the parent's
        // top-left (CLAUDE.md §4f).
        // ---------------------------------------------------------------

        private static float Overlap(RectTransform a, RectTransform b)
        {
            if (a == null || b == null) return 0f;
            float aTop = a.anchoredPosition.y, aBot = aTop - a.sizeDelta.y;
            float bTop = b.anchoredPosition.y, bBot = bTop - b.sizeDelta.y;
            return Mathf.Min(aTop, bTop) - Mathf.Max(aBot, bBot);
        }

        private RectTransform FindLeftRect(string name)
        {
            foreach (var rect in _hudObject.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name) return rect;
            return null;
        }

        [Test]
        public void LeftColumn_PhoneAbandonOverlapsStats()
        {
            ArrangePhone(dungeon: true);
            _hud.SetLeftStackAvailable(true);

            var abandon = _hud.AbandonRectForTest;
            var stats = _hud.StatsRectForTest;
            Assert.That(abandon, Is.Not.Null, "abandon button must exist once available");
            Assert.That(stats, Is.Not.Null);

            // Was 34 u before the shared stack existed (포기 pinned at a static
            // (16,-100) in every tier). Clearance is negative overlap, so the
            // assertion is <= 0 rather than == 0 — a stack placed further away
            // is more correct, not less.
            Assert.That(Overlap(abandon, stats), Is.LessThanOrEqualTo(0f),
                $"phone: 포기 {abandon.anchoredPosition.y} h{abandon.sizeDelta.y} "
                + $"must clear stats {stats.anchoredPosition.y} h{stats.sizeDelta.y}");
        }

        [Test]
        public void LeftColumn_DesktopAbandonOverlapsShield()
        {
            _hud.EnableCampaignUi("차가운 회랑", 3);
            _hud.EnableDungeonUi("재의 감시자");
            _hud.SetCampaignSurfacesVisible(true);
            _hud.ApplyLayout(1280, 720, new Rect(0, 0, 1280, 720));
            _hud.SetLeftStackAvailable(true);
            Assert.That(_hud.CurrentTier, Is.EqualTo(HudView.LayoutTier.Full));

            // No touch: shield stays at -98 (:590 else-branch). Was 22 u.
            Assert.That(Overlap(_hud.AbandonRectForTest, _hud.ShieldRectForTest),
                Is.LessThanOrEqualTo(0f),
                "desktop, no touch: 포기 must clear the shield readout");
        }

        [Test]
        public void LeftColumn_LandscapePhoneAbandonOverlapsEquip()
        {
            // 844x390: effective width ~1412 u -> Full tier WITH touch live.
            // Not in the interview's defect list; found by the planner while
            // re-deriving the arithmetic. One rotation away from the reported set.
            _hud.ForceTouchControlsForTest();
            _hud.EnableCampaignUi("차가운 회랑", 3);
            _hud.EnableDungeonUi("재의 감시자");
            _hud.SetCampaignSurfacesVisible(true);
            _hud.ApplyLayout(PhoneHeight, PhoneWidth, new Rect(0, 0, PhoneHeight, PhoneWidth));
            _hud.SetLeftStackAvailable(true);
            Assert.That(_hud.CurrentTier, Is.EqualTo(HudView.LayoutTier.Full));

            // Was 32 u. The stack pairs horizontally here, so 포기 sits beside
            // the codex on one row rather than below it.
            Assert.That(Overlap(_hud.AbandonRectForTest, _hud.EquipRectForTest),
                Is.LessThanOrEqualTo(0f),
                "landscape phone: 포기 must clear the equip strip");
        }

        [Test]
        public void LeftStack_BothButtonsClearEveryOccupant()
        {
            // AC-1. The 포기 tests above pin the three defects that shipped;
            // this pins the whole band, including the button those tests did
            // not know about. Sweeps tier x touch x mode, because the occupant
            // SET is mode-dependent: ApplyDungeonTier early-returns when
            // _dungeonRoot is null (HudView.cs:536), so the shield and equip
            // strip do not exist outside a dungeon.
            var configs = new (int w, int h, bool touch, bool dungeon, string label)[]
            {
                (1280, 720, false, true,  "desktop dungeon"),
                (1280, 720, false, false, "desktop prologue"),
                (PhoneHeight, PhoneWidth, true, true, "landscape 844x390"),
                (844, 344, true, true,  "landscape 844x344 (Safari toolbar)"),
                (PhoneWidth, PhoneHeight, true, true, "phone portrait"),
            };

            foreach (var c in configs)
            {
                Object.DestroyImmediate(_hudObject);
                _hudObject = new GameObject("LeftStackSweep");
                _hud = _hudObject.AddComponent<HudView>();
                _hud.Build();
                if (c.touch) _hud.ForceTouchControlsForTest();
                if (c.dungeon)
                {
                    _hud.EnableCampaignUi("차가운 회랑", 3);
                    _hud.EnableDungeonUi("재의 감시자");
                    _hud.SetCampaignSurfacesVisible(true);
                    _hud.HidePrologueToast();
                }
                _hud.ApplyLayout(c.w, c.h, new Rect(0, 0, c.w, c.h));
                _hud.SetLeftStackAvailable(true);

                // World rects, not anchoredPosition. The desktop stats panel
                // anchors top-RIGHT (HudView.cs:486-488) while the stack
                // anchors top-left, so their local y values are not comparable
                // — comparing them directly reports a 26 u collision between
                // two rects that are 900 u apart. Corners are the only frame
                // both live in.
                var canvas = _hudObject.GetComponentInChildren<Canvas>(true);
                canvas.renderMode = RenderMode.WorldSpace;
                var canvasRect = (RectTransform)canvas.transform;
                var effH = _hud.LastEffectiveWidth * c.h / (float)c.w;
                canvasRect.sizeDelta = new Vector2(_hud.LastEffectiveWidth, effH);
                Canvas.ForceUpdateCanvases();

                var stack = new[] { _hud.CodexButtonRectForTest, _hud.AbandonRectForTest };
                var occupants = new[]
                {
                    ("stats", _hud.StatsRectForTest),
                    ("equip", _hud.EquipRectForTest),
                    ("shield", _hud.ShieldRectForTest),
                };
                foreach (var button in stack)
                {
                    Assert.That(button, Is.Not.Null, $"{c.label}: stack button missing");
                    var b = WorldRect(button);
                    foreach (var (name, occupant) in occupants)
                    {
                        if (occupant == null || !occupant.gameObject.activeInHierarchy) continue;
                        var o = WorldRect(occupant);
                        var area = OverlapArea(b, o);
                        Assert.That(area, Is.LessThanOrEqualTo(1f),
                            $"{c.label}: {button.name} {b} overlaps {name} {o} by {area:F0} u^2");
                    }
                }
                var codexRect = WorldRect(stack[0]);
                var abandonRect = WorldRect(stack[1]);
                Assert.That(OverlapArea(codexRect, abandonRect), Is.LessThanOrEqualTo(1f),
                    $"{c.label}: codex {codexRect} and 포기 {abandonRect} overlap");
            }
        }

        /// <summary>Overlap AREA in canvas units. Zero when the rects are
        /// disjoint on either axis, which is what makes it safe for a
        /// horizontal pair: same row, different columns, no collision.</summary>
        private static float OverlapArea(Rect a, Rect b)
        {
            var w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            var h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return w <= 0f || h <= 0f ? 0f : w * h;
        }

        [Test]
        public void LeftColumn_PhoneShieldIsBelowStats_NotOverlapping()
        {
            // Refutes the interview's third reported defect (24 u). This one
            // passes TODAY and must keep passing: the shield lives at -252 on
            // phone, two bands below the stats panel. Pinned so a future
            // placement change cannot silently create the overlap that was
            // mistakenly reported here.
            ArrangePhone(dungeon: true);

            var shield = _hud.ShieldRectForTest;
            Assert.That(shield, Is.Not.Null);
            Assert.That(shield.anchoredPosition.y, Is.EqualTo(-252f).Within(0.5f),
                "ApplyDungeonTier:589 moves the shield off its build y of -98 on phone");
            Assert.That(Overlap(_hud.StatsRectForTest, shield), Is.LessThanOrEqualTo(0f),
                "phone stats x shield was reported as 24 u; it is not reachable");
        }
    }
}
