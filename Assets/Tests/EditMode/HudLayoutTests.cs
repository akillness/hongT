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
//
// Also the uGUI FILL-RENDER contract for the 체력/기름 meters (and every other
// generated Filled surface). uGUI short-circuits Image.OnPopulateMesh to the
// plain Graphic full-rect quad when activeSprite is null
// (Library/PackageCache/com.unity.ugui@67707a67a4ab/Runtime/UGUI/UI/Core/
// Image.cs:883-889), so the Type.Filled branch — and fillAmount with it — is
// never reached. The meters were built sprite-less and sat visually FULL for
// the whole life of the bug while fillAmount was written correctly every
// frame. ResetRunUi_ReseedsHealthBarForNewRun asserted fillAmount == 1f and
// stayed green throughout: an assertion on fillAmount alone only re-states the
// field the code already set, it never touches the geometry a player sees.
// The guards below therefore assert (e) every Filled Image under the HUD owns
// a sprite, and (f) the MESH those meters emit — read back out of
// Graphic.OnPopulateMesh(VertexHelper) by reflection — narrows in proportion
// to a drain driven through the real sim, not through a poked field.
using System.Collections.Generic;
using System.Reflection;
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
        // Mesh x-extent vs fillAmount: the quad is generated from the same
        // float, so anything past this is a different code path, not drift.
        private const float MeshFillTolerance = 0.005f;

        private GameObject _hudObject;
        private HudView _hud;
        private bool _hadRotateHintPref;
        private bool _hadReducedMotionPref;
        private int _reducedMotionPrefValue;
        private bool _hadOsHintPref;
        private string _osHintPrefValue;

        [SetUp]
        public void SetUp()
        {
            // EnableDungeonUi -> ShowRotateHintIfPortrait writes this pref;
            // snapshot so the suite never pollutes the developer's editor.
            _hadRotateHintPref = PlayerPrefs.HasKey("al:rotate-hint");
            _hadReducedMotionPref = PlayerPrefs.HasKey("al:reduced-motion");
            _reducedMotionPrefValue = PlayerPrefs.GetInt("al:reduced-motion");
            // Editor fallback store of the WebGL OS hint (ViewPrefs seeding
            // reads it through WebGLStorage) — snapshot for the same reason.
            _hadOsHintPref = PlayerPrefs.HasKey("al:os-reduced-motion");
            _osHintPrefValue = PlayerPrefs.GetString("al:os-reduced-motion");

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
            if (_hadOsHintPref)
                PlayerPrefs.SetString("al:os-reduced-motion", _osHintPrefValue);
            else
                PlayerPrefs.DeleteKey("al:os-reduced-motion");
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

        /// <summary>The 기름 meter fill. Unlike 체력 the oil prefix is ambiguous
        /// — the prologue toast line "기름 게이지를 보라..." also starts with it —
        /// so this takes the first candidate that actually owns a Fill child
        /// and only fails once every candidate is exhausted.</summary>
        private Image ChargeFill()
        {
            var candidates = 0;
            foreach (var text in _hudObject.GetComponentsInChildren<Text>(true))
            {
                if (!text.text.StartsWith("기름 ")) continue;
                candidates++;
                var fill = text.transform.parent.Find("Fill")?.GetComponent<Image>();
                if (fill != null) return fill;
            }
            Assert.Fail(candidates == 0
                ? "HUD did not render an oil value"
                : $"{candidates} oil label(s) rendered but none kept a visible Fill child");
            return null;
        }

        /// <summary>Runs the graphic's real mesh generation and returns the
        /// emitted x-extent as a fraction of its own rect width. Reflection is
        /// the only seam: Graphic.OnPopulateMesh(VertexHelper) is protected and
        /// CanvasRenderer never hands the built mesh back. The call is virtual,
        /// so this dispatches to Image.OnPopulateMesh — the exact method whose
        /// null-activeSprite early-out caused the meters to render full.</summary>
        private static float MeshWidthFraction(Graphic graphic)
        {
            var populate = typeof(Graphic).GetMethod(
                "OnPopulateMesh",
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new[] { typeof(VertexHelper) },
                null);
            Assert.That(populate, Is.Not.Null,
                "Graphic.OnPopulateMesh(VertexHelper) is gone — this uGUI version "
                + "needs a new mesh seam before the fill geometry can be graded");

            var width = graphic.rectTransform.rect.width;
            Assert.That(width, Is.GreaterThan(0f),
                $"degenerate fill rect (layout did not resolve): {Path(graphic.transform)}");

            using (var vertices = new VertexHelper())
            {
                populate.Invoke(graphic, new object[] { vertices });
                Assert.That(vertices.currentVertCount, Is.GreaterThan(0),
                    "the graphic emitted no geometry at all — the bar is invisible, "
                    + $"not partial: {Path(graphic.transform)}");

                var min = float.MaxValue;
                var max = float.MinValue;
                var vertex = new UIVertex();
                for (var i = 0; i < vertices.currentVertCount; i++)
                {
                    vertices.PopulateUIVertex(ref vertex, i);
                    min = Mathf.Min(min, vertex.position.x);
                    max = Mathf.Max(max, vertex.position.x);
                }
                return (max - min) / width;
            }
        }

        /// <summary>The assertion the shipped bug would have failed: the mesh
        /// the meter actually emits must be as narrow as its fillAmount claims.
        /// A sprite-less Filled Image keeps returning the full-rect quad
        /// (fraction 1.0) no matter what fillAmount holds. Returns the measured
        /// fraction so the caller can also grade it against the full bar.</summary>
        private static float AssertFillReachesMesh(Image fill, string meter)
        {
            // overrideSprite is the public getter for the private activeSprite
            // that Image.OnPopulateMesh gates on (Image.cs:885, :408). Compared
            // through Unity's == so a destroyed sprite reads as null too.
            Assert.That(fill.overrideSprite != null, Is.True,
                $"{meter} fill has no sprite, so uGUI never reaches the Type.Filled "
                + $"branch: {Path(fill.transform)}");
            Assert.That(fill.type, Is.EqualTo(Image.Type.Filled),
                $"{meter} fill stopped being a Filled Image: {Path(fill.transform)}");
            Assert.That(fill.fillAmount, Is.LessThan(1f - MeshFillTolerance),
                $"{meter} fill is still at {fill.fillAmount:F3} — a full bar cannot "
                + "distinguish a real fill from the full-rect fallback quad");

            var measured = MeshWidthFraction(fill);
            Assert.That(measured, Is.EqualTo(fill.fillAmount).Within(MeshFillTolerance),
                $"{meter} bar rendered {measured:P1} of its {fill.rectTransform.rect.width:F0} u "
                + $"width but fillAmount says {fill.fillAmount:P1} "
                + $"({Path(fill.transform)}) — fillAmount never reached the mesh");
            return measured;
        }

        /// <summary>Dungeon run held on the attack key long enough for
        /// CinderSim.ChargeProgress to go positive, which is the only thing
        /// that lazily builds the charge-gauge Filled Image (HudView
        /// SyncChargeGauge). Asserted, so a sim change that kills the charge
        /// path fails here instead of quietly shrinking the surface census.</summary>
        private static CinderSim ChargingDungeonRun()
        {
            Assert.That(HackConfig.TryDungeon(
                    CampaignStages.CinderSpan,
                    MetaStats.Of(0, 0, 0),
                    EquipTiers.Of(0, 0, 0), (string)null,
                    0,
                    out var config),
                Is.True, $"unknown stage {CampaignStages.CinderSpan}");
            var sim = new CinderSim(in config);
            var hold = new SimInput { AttackHeld = true };
            for (var tick = 0; tick < 30 && sim.ChargeProgress <= 0f; tick++)
            {
                sim.Tick(in hold);
            }
            Assert.That(sim.ChargeProgress, Is.GreaterThan(0f),
                "a held attack must accrue charge — without it the charge gauge "
                + "is never built and the Filled surface census is short one");
            return sim;
        }

        /// <summary>
        /// The acquisition toast column is parked on the left edge at vertical
        /// centre because that is the one band nothing else claims. That is a
        /// geometric claim about a HUD whose surfaces move per tier, so it is
        /// measured here rather than trusted: a full column at phone tier must
        /// clear every interactive surface (skill row, dash card, companion
        /// orders, mute, joystick catch box, strike/dash touch pads) and the two
        /// non-interactive combat readouts a toast could bury — the boss bar and
        /// the room objective chip.
        /// </summary>
        [Test]
        public void LootToastColumn_CoversNoCombatSurfaceAtPhoneTier()
        {
            var canvas = ArrangePhone(dungeon: true);
            // Four DISTINCT rows: identical pickups stack onto one row by design,
            // so pushing the same kind four times would measure a third of the
            // column and pass vacuously.
            _hud.PushLootToast(LootToastKind.Shard, LootGrade.Basic);
            _hud.PushLootToast(LootToastKind.Flask, LootGrade.Fine);
            _hud.PushLootToast(LootToastKind.Relic, LootGrade.Epic);
            _hud.PushLootToast(LootToastKind.Equip, LootGrade.Fine);
            // Both readouts have to be ON SCREEN for the audit to mean anything.
            _hud.SyncRoomObjective("증언의 우물을 사수하라", true);
            Canvas.ForceUpdateCanvases();

            var toasts = new List<RectTransform>();
            _hud.CollectActiveLootToastRects(toasts);
            Assert.That(toasts.Count, Is.EqualTo(LootToastQueue.Capacity),
                "a full column is the worst case and the only one worth measuring");

            var others = InteractiveRects(canvas);
            var objective = _hud.RoomObjectiveRect;
            Assert.That(objective, Is.Not.Null);
            Assert.That(objective.gameObject.activeInHierarchy, Is.True,
                "the objective chip must be up, or this audit measures nothing");
            others.Add(objective);
            var bossBar = _hud.BossBarRectForTest;
            if (bossBar != null && bossBar.gameObject.activeInHierarchy) others.Add(bossBar);

            var violations = new List<string>();
            for (var i = 0; i < toasts.Count; i++)
            {
                var a = WorldRect(toasts[i]);
                Assert.That(a.width > 0f && a.height > 0f, Is.True,
                    $"degenerate toast rect: {Path(toasts[i].transform)}");
                for (var j = 0; j < others.Count; j++)
                {
                    var b = WorldRect(others[j]);
                    var overlapX = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                    var overlapY = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                    if (overlapX > OverlapEpsilon && overlapY > OverlapEpsilon)
                        violations.Add($"toast row {i} {a} covers "
                            + $"{Path(others[j].transform)} {b} by {overlapX:F1}x{overlapY:F1} u");
                }
            }
            Assert.That(violations, Is.Empty,
                "the loot toast column buries a combat surface at phone tier:\n"
                + string.Join("\n", violations));
        }

        /// <summary>
        /// The toast has to answer WHAT and HOW GOOD, and a stack has to say how
        /// many — that is the whole feature. Read back off the rendered Text, not
        /// off the model, so a row wired to the wrong widget fails here.
        /// </summary>
        [Test]
        public void LootToastRows_NameTheItemItsGradeAndTheStackCount()
        {
            ArrangePhone(dungeon: true);

            _hud.PushLootToast(LootToastKind.Relic, LootGrade.Epic);
            Assert.That(_hud.LootToastReadout(0), Is.EqualTo("전설의 유물"),
                "an Epic relic must be named as one");

            _hud.PushLootToast(LootToastKind.Shard, LootGrade.Basic);
            _hud.PushLootToast(LootToastKind.Shard, LootGrade.Basic);
            Assert.That(_hud.LootToastCount, Is.EqualTo(2), "two shards are one row");
            Assert.That(_hud.LootToastReadout(0), Is.EqualTo("잿불 파편 x2"),
                "a stacked row states its count");
            Assert.That(_hud.LootToastReadout(1), Is.EqualTo("전설의 유물"),
                "the older row keeps its own text while the newer one stacks");
        }

        /// <summary>A retry must not open on the previous run's last pickup.</summary>
        [Test]
        public void ResetRunUi_ClearsTheLootToastColumn()
        {
            ArrangePhone(dungeon: true);
            _hud.PushLootToast(LootToastKind.Flask, LootGrade.Fine);
            Assert.That(_hud.LootToastCount, Is.EqualTo(1));

            _hud.ResetRunUi();

            Assert.That(_hud.LootToastCount, Is.EqualTo(0));
            var rows = new List<RectTransform>();
            _hud.CollectActiveLootToastRects(rows);
            Assert.That(rows, Is.Empty, "cleared rows must also be deactivated");
        }

        /// <summary>uGUI precondition, whole hierarchy. Image.OnPopulateMesh
        /// returns the plain Graphic full-rect quad when activeSprite is null
        /// (Image.cs:883-889), so a Filled Image with no sprite renders
        /// permanently full. Walks every Image so a Filled surface added later
        /// is graded automatically instead of shipping the same bug again.</summary>
        [Test]
        public void EveryFilledHudImage_OwnsASprite_SoFillAmountCanReachTheMesh()
        {
            ArrangePhone(dungeon: true);
            // Lazily-built Filled surfaces: the charge gauge only exists once a
            // live dungeon sim reports charge progress.
            _hud.Sync(ChargingDungeonRun());

            var filled = 0;
            var violations = new List<string>();
            foreach (var image in _hudObject.GetComponentsInChildren<Image>(true))
            {
                if (image.type != Image.Type.Filled) continue;
                filled++;
                if (image.overrideSprite == null)
                    violations.Add(
                        $"{Path(image.transform)} (fillMethod {image.fillMethod}, "
                        + $"fillAmount {image.fillAmount:F3}) renders a full-rect quad "
                        + "regardless of fillAmount");
            }

            // Census: StageClearFlash + 체력 + 기름 + nova/ward cooldowns (Build)
            // + xp + boss + extract + dash + 4 skill cooldowns (EnableDungeonUi)
            // + charge gauge = 14.
            Assert.That(filled, Is.GreaterThanOrEqualTo(14),
                $"only {filled} Filled surfaces found — the dungeon HUD lost fills "
                + "and the sweep would be vacuous");
            Assert.That(violations, Is.Empty,
                "Filled Images with no sprite (uGUI never reaches Type.Filled):\n"
                + string.Join("\n", violations));
        }

        /// <summary>체력 geometry, driven through the real sim. Ticks a live
        /// arena run until enemy contact lands the first damage, then asserts
        /// the MESH the bar emits narrowed. fillAmount alone was already
        /// correct throughout the bug, so only the mesh grades the fix.</summary>
        [Test]
        public void HealthMeter_MeshNarrows_WhenTheSimDrainsHealth()
        {
            var sim = new CinderSim();
            var idle = default(SimInput);
            _hud.Sync(sim);

            var fill = HealthFill();
            Assert.That(fill.fillAmount, Is.EqualTo(1f).Within(0.001f),
                "a fresh run must start visually full");
            var fullFraction = MeshWidthFraction(fill);
            Assert.That(fullFraction, Is.EqualTo(1f).Within(MeshFillTolerance),
                "control: a full bar must emit a full-width quad, or the mesh "
                + "readback itself is measuring the wrong thing");

            // Enemies spawn, walk in, and land contact damage on their own —
            // no field is poked, the drain path runs end to end.
            var damagedAt = -1;
            for (var tick = 1; tick <= 1200 && damagedAt < 0; tick++)
            {
                sim.Tick(in idle);
                if ((sim.Events & SimEvents.PlayerDamaged) != SimEvents.None) damagedAt = tick;
            }
            Assert.That(damagedAt, Is.GreaterThan(0),
                "no enemy ever damaged the idle player — the drain was never exercised");
            Assert.That(sim.Player.Health, Is.LessThan(SimConfig.PlayerMaxHealth),
                "the sim reported damage without taking health");

            _hud.Sync(sim);
            Assert.That(fill.fillAmount,
                Is.EqualTo(sim.Player.Health / SimConfig.PlayerMaxHealth).Within(0.001f),
                "the HUD did not take the sim's drained health");
            var drainedFraction = AssertFillReachesMesh(fill, "체력");
            Assert.That(drainedFraction, Is.LessThan(fullFraction - MeshFillTolerance),
                $"체력 mesh did not narrow: full bar spanned {fullFraction:P1} of the "
                + $"rect, the drained bar still spans {drainedFraction:P1}");
        }

        /// <summary>기름 geometry. The user reported BOTH meters, and the oil
        /// bar is a second Bar() instance of the same Filled construction — it
        /// gets the same mesh-level grade, spent through a real ward cast.</summary>
        [Test]
        public void OilMeter_MeshNarrows_WhenTheSimSpendsCharge()
        {
            var sim = new CinderSim();
            _hud.Sync(sim);

            var fill = ChargeFill();
            Assert.That(fill.fillAmount, Is.EqualTo(1f).Within(0.001f),
                "a fresh run starts at LanternMax, so the oil bar starts full");
            var fullFraction = MeshWidthFraction(fill);
            Assert.That(fullFraction, Is.EqualTo(1f).Within(MeshFillTolerance),
                "control: a full oil bar must emit a full-width quad");

            // Ward costs 30 oil against a LanternMax of 100 and only regains
            // 7/s, so the cast is a visible bite out of the bar.
            var ward = new SimInput { WardQueued = true };
            var spentAt = -1;
            for (var tick = 1; tick <= 600 && spentAt < 0; tick++)
            {
                sim.Tick(in ward);
                if (sim.Charge < SimConfig.LanternMax - SimConfig.WardCost * 0.5f) spentAt = tick;
            }
            Assert.That(spentAt, Is.GreaterThan(0),
                "the ward cast never took its oil — the spend was never exercised");

            _hud.Sync(sim);
            Assert.That(fill.fillAmount,
                Is.EqualTo(sim.Charge / SimConfig.LanternMax).Within(0.001f),
                "the HUD did not take the sim's spent charge");
            var spentFraction = AssertFillReachesMesh(fill, "기름");
            Assert.That(spentFraction, Is.LessThan(fullFraction - MeshFillTolerance),
                $"기름 mesh did not narrow: full bar spanned {fullFraction:P1} of the "
                + $"rect, the spent bar still spans {spentFraction:P1}");
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

        /// <summary>Spec §2.4 auto-detection: with NO explicit lobby choice
        /// (no "al:reduced-motion" key), the OS hint mirrored by the WebGL
        /// shell ("al:os-reduced-motion", read via WebGLStorage — PlayerPrefs
        /// string in the editor) decides the default.</summary>
        [Test]
        public void ReducedMotion_NoExplicitChoice_SeedsFromOsHint()
        {
            PlayerPrefs.DeleteKey("al:reduced-motion");
            PlayerPrefs.SetString("al:os-reduced-motion", "1");
            ViewPrefs.ResetCacheForTests();

            Assert.That(ViewPrefs.ReducedMotion, Is.True,
                "an OS prefers-reduced-motion user must get the reduced default without touching the lobby");
            Assert.That(ViewPrefs.MotionScale, Is.EqualTo(0.4f));
            Assert.That(ViewPrefs.TimeEffectsAllowed, Is.False);
            Assert.That(PlayerPrefs.HasKey("al:reduced-motion"), Is.False,
                "the OS hint is a default, not a choice — it must not be persisted as one");

            PlayerPrefs.SetString("al:os-reduced-motion", "0");
            ViewPrefs.ResetCacheForTests();
            Assert.That(ViewPrefs.ReducedMotion, Is.False,
                "clearing the OS setting must clear the seeded default on the next boot");
        }

        /// <summary>Spec §2.4 guard: an explicit lobby choice — including
        /// explicit OFF — always beats the OS hint. HasKey is the
        /// discriminator; GetInt(key, 0) could not tell OFF from no-choice,
        /// and would force-enable reduced motion for explicit-OFF users on
        /// every boot.</summary>
        [Test]
        public void ReducedMotion_ExplicitChoice_AlwaysBeatsOsHint()
        {
            ViewPrefs.ReducedMotion = false;                       // explicit OFF
            PlayerPrefs.SetString("al:os-reduced-motion", "1");    // OS says reduce
            ViewPrefs.ResetCacheForTests();
            Assert.That(ViewPrefs.ReducedMotion, Is.False,
                "an explicit OFF must survive an OS reduced-motion hint across boots");

            ViewPrefs.ReducedMotion = true;                        // explicit ON
            PlayerPrefs.SetString("al:os-reduced-motion", "0");    // OS says no-preference
            ViewPrefs.ResetCacheForTests();
            Assert.That(ViewPrefs.ReducedMotion, Is.True,
                "an explicit ON must survive an OS no-preference hint across boots");
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

        /// <summary>
        /// W15: the painted Ember Rest plate is produced on the asset lane and
        /// may not exist yet, so BOTH states are real shipping states. This
        /// asserts whichever one is live rather than assuming the art is there —
        /// and in both cases the panel must stay fully actionable, because a
        /// decorative layer that swallowed the offer taps is a softlock.
        /// </summary>
        [Test]
        public void EmberRestBackdrop_IsOptional_AndNeverBlocksTheOffers()
        {
            var canvas = ArrangePhone(dungeon: true);
            var attack = Preparation(PreparationOfferKind.Stat, 1, 1);
            var gravePulse = Preparation(PreparationOfferKind.SkillRune, 2, 2);
            var companionRange = Preparation(PreparationOfferKind.GuardianResonance, 2, 1);

            _hud.ShowEmberRestForTest(2, attack, gravePulse, companionRange);
            Canvas.ForceUpdateCanvases();

            var panel = FindDescendant(canvas.transform, "EmberRestPanel");
            Assert.That(panel, Is.Not.Null, "the Ember Rest panel must exist");
            var backdrop = FindDescendant(panel, "EmberRestBackdrop");
            var scrim = FindDescendant(panel, "EmberRestScrim");

            if (_hud.EmberRestBackdropPresent)
            {
                Assert.That(backdrop, Is.Not.Null);
                Assert.That(scrim, Is.Not.Null, "art without a scrim is unreadable copy");
                Assert.That(_hud.EmberRestScrimOpacity,
                    Is.EqualTo(HudView.EmberRestScrimAlpha).Within(0.001f));
                // uGUI draws in sibling order, so readability is an ORDERING
                // property, stated exactly: art at 0, scrim at 1, and every
                // readable element after them.
                Assert.That(backdrop.GetSiblingIndex(), Is.Zero,
                    "the art must be the panel's first child");
                Assert.That(scrim.GetSiblingIndex(), Is.EqualTo(1),
                    "the scrim must be drawn over the art and under everything else");
                Assert.That(panel.childCount, Is.GreaterThan(2),
                    "the panel must carry readable content above the scrim");
                foreach (var layer in new[] { backdrop, scrim })
                    Assert.That(layer.GetComponent<Image>().raycastTarget, Is.False,
                        $"{layer.name} must never intercept a tap meant for an offer");
            }
            else
            {
                Assert.That(backdrop, Is.Null, "no art means no backdrop layer at all");
                Assert.That(scrim, Is.Null, "no art means nothing to darken");
                Assert.That(_hud.EmberRestScrimOpacity, Is.Zero);
            }

            // The contract that matters is identical either way.
            var actions = new[]
            {
                VisibleButtonWithText(canvas, "Attack +1"),
                VisibleButtonWithText(canvas, "준비 보류"),
                VisibleButtonWithText(canvas, "계속"),
            };
            AssertRaycastableActions(actions);
            actions[0].onClick.Invoke();
            Assert.That(actions[2].interactable, Is.True,
                "an offer must remain selectable whether or not the plate loaded");
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == name && child != root)
                    return child;
            return null;
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
    }
}
