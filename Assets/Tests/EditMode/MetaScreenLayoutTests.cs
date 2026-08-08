// Portrait layout contract for the meta screen (W7/W8 tabbed lobby overlay).
//
// HANDOFF NOTE — these tests FAIL on the code that introduced MetaScreenView
// (6379b1a). They were written by the environment lane, not the UI lane, to
// hand over two portrait defects as a reproducible artifact instead of a
// screenshot. Nothing in MetaScreenView.cs was touched.
//
// Why nothing caught this: HudLayoutTests and LobbyLayoutTests both pin their
// portrait canvas at 799 u, which is correct for THEM because HudView (L395)
// and LobbyView (L669) drop matchWidthOrHeight to 0.35 in portrait.
// MetaScreenView pins 0.5 (L127) and never syncs on orientation — its Update
// (L214) only ticks the map — so its real portrait canvas is 653 u. A test
// reusing the 799 constant would place the close button at 687 u, clear of the
// tab row, and pass on the bug. So the width here is DERIVED from the scaler
// the view actually installs.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CinderCourt.View;

namespace CinderCourt.Tests
{
    public class MetaScreenLayoutTests
    {
        // Worst measured phone viewport (mobile-layout spec).
        const int PhoneWidth = 390;
        const int PhoneHeight = 844;
        // Interactive rects may touch but not stack (<= 1 u counts as touch).
        const float OverlapEpsilon = 1f;

        GameObject _host;
        MetaScreenView _meta;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("MetaScreenLayoutTests");
            _meta = _host.AddComponent<MetaScreenView>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            var eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem != null) Object.DestroyImmediate(eventSystem.gameObject);
        }

        /// <summary>
        /// Effective canvas width for a screen size under the scaler the view
        /// installed. Derived, never a literal: CanvasScaler's log-lerp is
        /// scale = (w/refW)^(1-match) * (h/refH)^match, so the effective width
        /// is screenWidth / scale. Reading match off the live component means
        /// this tracks a future orientation fix instead of going stale.
        /// </summary>
        static float EffectiveWidth(CanvasScaler scaler, int width, int height)
        {
            var refRes = scaler.referenceResolution;
            var match = scaler.matchWidthOrHeight;
            var scale = Mathf.Pow(width / refRes.x, 1f - match)
                      * Mathf.Pow(height / refRes.y, match);
            return width / scale;
        }

        static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var min = new Vector2(corners[0].x, corners[0].y);
            var max = new Vector2(corners[2].x, corners[2].y);
            return new Rect(min, max - min);
        }

        static string Describe(Rect r)
            => $"[x {r.xMin:F0}..{r.xMax:F0}, y {r.yMin:F0}..{r.yMax:F0}]";

        /// <summary>
        /// Opens the screen and sizes its canvas to the phone-portrait
        /// effective width, so world rects read directly in canvas units.
        /// </summary>
        Canvas OpenPortrait(out float effectiveWidth)
        {
            // Build is mandatory: Show early-returns on a null root, which
            // would leave an empty canvas and a vacuously passing test. The
            // font matters too - a null Font degenerates every Text rect, i.e.
            // exactly the geometry being measured (LobbyView L215-217 grammar).
            var font = Resources.Load<Font>("Fonts/HudKorean")
                       ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var data = CampaignStore.Load();
            _meta.Build(font, in data);
            _meta.Show(in data, MetaScreenView.TabEquip);

            var canvas = _host.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null, "meta screen built no canvas");
            var scaler = canvas.GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null, "meta screen canvas has no scaler");

            effectiveWidth = EffectiveWidth(scaler, PhoneWidth, PhoneHeight);
            var effectiveHeight = effectiveWidth * PhoneHeight / PhoneWidth;
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.sizeDelta = new Vector2(effectiveWidth, effectiveHeight);
            canvasRect.localScale = Vector3.one;
            canvasRect.position = Vector3.zero;
            Canvas.ForceUpdateCanvases();
            return canvas;
        }

        static List<(string label, RectTransform rect)> InteractiveRects(Canvas canvas)
        {
            var found = new List<(string, RectTransform)>();
            foreach (var button in canvas.GetComponentsInChildren<Button>(true))
            {
                var label = button.GetComponentInChildren<Text>(true);
                found.Add((label != null ? label.text : button.name,
                    (RectTransform)button.transform));
            }
            return found;
        }

        /// <summary>
        /// The close button is a LATER sibling of the tab row on the same bar,
        /// so where they overlap the close button wins the raycast and a tap
        /// meant for the last tab dismisses the screen instead. That is input
        /// loss, not a cosmetic issue — HudLayoutTests states this exact
        /// purpose ("invisible rects cannot eat taps") for the combat HUD.
        /// </summary>
        [Test]
        [Explicit("Portrait defect in 6379b1a (MetaScreenView pins match 0.5 and "
            + "never syncs on orientation). Kept OFF the shared gate so other "
            + "lanes do not see a red they did not cause - run with "
            + "--testFilter MetaScreenLayoutTests. Remove this attribute when "
            + "the screen reflows in portrait; the assert prints the measured "
            + "653 u overlaps.")]
        public void PhonePortrait_CloseButtonDoesNotCoverTheLastTab()
        {
            var canvas = OpenPortrait(out var effectiveWidth);
            var rects = InteractiveRects(canvas);

            RectTransform close = null, lastTab = null;
            foreach (var (label, rect) in rects)
            {
                if (label == "닫기") close = rect;
                if (label == "조작") lastTab = rect;
            }
            Assert.That(close, Is.Not.Null, "close button not found");
            Assert.That(lastTab, Is.Not.Null, "last tab (조작) not found");

            var closeRect = WorldRect(close);
            var tabRect = WorldRect(lastTab);
            var overlapX = Mathf.Min(closeRect.xMax, tabRect.xMax)
                         - Mathf.Max(closeRect.xMin, tabRect.xMin);
            var overlapY = Mathf.Min(closeRect.yMax, tabRect.yMax)
                         - Mathf.Max(closeRect.yMin, tabRect.yMin);

            TestContext.WriteLine(
                $"[meta portrait @{PhoneWidth}x{PhoneHeight}, effective width "
                + $"{effectiveWidth:F0} u] 조작 {Describe(tabRect)}  "
                + $"닫기 {Describe(closeRect)}  overlap {overlapX:F0}x{overlapY:F0} u");

            Assert.That(overlapX > OverlapEpsilon && overlapY > OverlapEpsilon, Is.False,
                $"닫기 {Describe(closeRect)} covers 조작 {Describe(tabRect)} by "
                + $"{overlapX:F0} u — close is the later sibling so it takes the "
                + "raycast, and tapping the tab's right edge closes the screen");
        }

        /// <summary>
        /// The currency readouts are top-right anchored while the tabs run from
        /// the left, so the two rows only clear each other above a threshold
        /// width. This is the defect the scaler alone cannot fix: even at
        /// HudView's portrait match of 0.35 the canvas is ~799 u, still short.
        /// </summary>
        [Test]
        [Explicit("Portrait defect in 6379b1a (MetaScreenView pins match 0.5 and "
            + "never syncs on orientation). Kept OFF the shared gate so other "
            + "lanes do not see a red they did not cause - run with "
            + "--testFilter MetaScreenLayoutTests. Remove this attribute when "
            + "the screen reflows in portrait; the assert prints the measured "
            + "653 u overlaps.")]
        public void PhonePortrait_TabsDoNotCollideWithCurrencyReadouts()
        {
            var canvas = OpenPortrait(out var effectiveWidth);

            var readouts = new List<(string label, RectTransform rect)>();
            foreach (var text in canvas.GetComponentsInChildren<Text>(true))
            {
                if (text.text.StartsWith("유물") || text.text.StartsWith("포인트"))
                    readouts.Add((text.text, text.rectTransform));
            }
            Assert.That(readouts, Is.Not.Empty, "currency readouts not found");

            // Check EVERY tab, not a guessed one. The readouts are top-right
            // anchored and the tabs run from the left, so which pair collides
            // depends on the effective width - naming one tab up front sends
            // the reader to rects that never touch.
            var tabs = new List<(string label, RectTransform rect)>();
            foreach (var (label, rect) in InteractiveRects(canvas))
            {
                if (label == "장비" || label == "각인" || label == "지도" || label == "조작")
                    tabs.Add((label, rect));
            }
            Assert.That(tabs.Count, Is.EqualTo(4), "expected four tabs");

            var collisions = new List<string>();
            var report = new System.Text.StringBuilder();
            foreach (var (rLabel, rRect) in readouts)
            {
                var rr = WorldRect(rRect);
                report.Append($"  {rLabel} {Describe(rr)}\n");
                foreach (var (tLabel, tRect) in tabs)
                {
                    var tr = WorldRect(tRect);
                    var overlapX = Mathf.Min(rr.xMax, tr.xMax) - Mathf.Max(rr.xMin, tr.xMin);
                    var overlapY = Mathf.Min(rr.yMax, tr.yMax) - Mathf.Max(rr.yMin, tr.yMin);
                    if (overlapX > OverlapEpsilon && overlapY > OverlapEpsilon)
                        collisions.Add($"{rLabel} {Describe(rr)} over {tLabel} "
                            + $"{Describe(tr)} by {overlapX:F0}x{overlapY:F0} u");
                }
            }
            foreach (var (tLabel, tRect) in tabs)
                report.Append($"  {tLabel} {Describe(WorldRect(tRect))}\n");

            TestContext.WriteLine(
                $"[meta portrait @{PhoneWidth}x{PhoneHeight}, effective width "
                + $"{effectiveWidth:F0} u]\n" + report);

            Assert.That(collisions, Is.Empty,
                "currency readouts stack on the tab row:\n  "
                + string.Join("\n  ", collisions)
                + "\n  A scaler change alone cannot fix this - even at HudView's "
                + "portrait match of 0.35 the canvas is ~799 u, and the row needs "
                + "> 1042 u to clear. Portrait wants a reflow.");
        }
    }
}
