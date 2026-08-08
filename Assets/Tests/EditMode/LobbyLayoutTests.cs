// EditMode rect audit for the LOBBY canvas — the surface HudLayoutTests does
// not cover. HudLayoutTests audits the in-run HUD; the sortie cards live in
// LobbyView and, until this fixture existed, their geometry was only ever
// checked by hand-arithmetic in a lane doc (v1.3 test-lane, "a LobbyView rect
// audit would need a new fixture building the full lobby canvas — flagged as
// possible follow-up"). Hand-arithmetic is exactly what shipped the v1.3
// claim that the 28 u card buttons clear the 44 CSS px touch floor: they do
// not (28 u * 0.488 = 13.7 px). This fixture measures instead of asserting a
// wish, so the real numbers are in the gate artifact.
//
// Screen.* is degenerate in batchmode, so the canvas is switched to WorldSpace
// and sized to the effective phone canvas — the same seam HudLayoutTests uses.
using System.Collections.Generic;
using System.Text;
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class LobbyLayoutTests
    {
        // Worst measured phone viewport (mobile-layout spec): 390x844 CSS,
        // portrait match 0.35 -> ~799 u effective width, 0.488 CSS px/u.
        private const float SpecCssPerUnit = 0.488f;
        private const float MinCssPx = 44f;
        private const float EffectiveWidth = 799f;
        private const float EffectiveHeight = 1729f;   // 844 / 0.488
        // Interactive rects may touch but not stack (<= 1 u counts as touch).
        private const float OverlapEpsilon = 1f;
        private const string CampaignKey = "abyssal-lantern:unity:campaign";
        private const string ReducedMotionKey = "al:reduced-motion";

        private GameObject _lobbyObject;
        private LobbyView _lobby;
        private bool _hadCampaign;
        private string _campaignPayload;

        [SetUp]
        public void SetUp()
        {
            _hadCampaign = PlayerPrefs.HasKey(CampaignKey);
            _campaignPayload = PlayerPrefs.GetString(CampaignKey);

            _lobbyObject = new GameObject("LobbyLayoutTests");
            _lobby = _lobbyObject.AddComponent<LobbyView>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_lobbyObject);
            var eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem != null) Object.DestroyImmediate(eventSystem.gameObject);
            if (_hadCampaign) PlayerPrefs.SetString(CampaignKey, _campaignPayload);
            else PlayerPrefs.DeleteKey(CampaignKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// The v1.3 pact toggle was placed by cloning the 강하 button's row
        /// geometry and checked with pen-and-paper. Both live on a cleared
        /// card at once, which is the only state where they can collide.
        /// </summary>
        [Test]
        public void ClearedStageCard_PactToggleAndDescentDoNotOverlap()
        {
            var canvas = BuildClearedLobby();
            var pact = FindButton(canvas, "서약");
            var descent = FindButton(canvas, "강하");
            Assert.That(pact, Is.Not.Null, "a cleared card must expose its 서약 toggle");
            Assert.That(descent, Is.Not.Null, "a cleared card must stay replayable");
            Assert.That(pact.gameObject.activeInHierarchy, Is.True,
                "the 서약 toggle must be revealed on a cleared card");

            var pactRect = WorldRect(pact.GetComponent<RectTransform>());
            var descentRect = WorldRect(descent.GetComponent<RectTransform>());
            var overlapX = Mathf.Min(pactRect.xMax, descentRect.xMax)
                           - Mathf.Max(pactRect.xMin, descentRect.xMin);
            var overlapY = Mathf.Min(pactRect.yMax, descentRect.yMax)
                           - Mathf.Max(pactRect.yMin, descentRect.yMin);
            Assert.That(overlapX > OverlapEpsilon && overlapY > OverlapEpsilon, Is.False,
                $"서약 {Describe(pactRect)} must not stack on 강하 {Describe(descentRect)}");
            Assert.That(pactRect.xMax, Is.LessThanOrEqualTo(descentRect.xMin + OverlapEpsilon),
                "the 서약 toggle sits left of 강하 (v1.3 M3b placement)");
        }

        /// <summary>
        /// Every interactive lobby rect against the accessibility floor
        /// (SIM_SPEC_HACKSLASH §9: 버튼 최소 44px). The full phone-route pass
        /// removes all sortie actions from the debt: prologue, descent, pact,
        /// training tier and training entry grow with their scroll card pitch.
        /// The remaining Sanctum controls are tracked separately until their
        /// dense tab/row grammar receives the same treatment.
        /// </summary>
        [Test]
        public void InteractiveLobbyRects_HoldTheMeasuredTouchFloorDebt()
        {
            var canvas = BuildClearedLobby();
            var report = new StringBuilder();
            var undersized = new List<string>();
            var measured = 0;

            // AMENDMENT #8 — sweep every fold state, not just the default one.
            //
            // The accordion deactivates the folded groups' cards, and this audit
            // only measures active buttons. Left as a single-state sweep it
            // silently STOPPED measuring the tier row and all five 수련 buttons:
            // 26 audited controls became 13, and a size regression inside a
            // folded group would have passed unnoticed. Folding is a UI win and
            // an audit hole at the same time, and the hole is closed here by
            // opening each group in turn and taking the union.
            //
            // The union is what the frozen table below compares against, so the
            // table means "every control the lobby can put on screen", which is
            // strictly stronger than what it meant before this cycle.
            //
            // Dedup by INSTANCE, not by path. The first draft of this sweep
            // keyed on Path(transform) and silently lost controls: every card
            // in a group is a "Panel" under a "Panel", so three 강하 buttons
            // share one path string and two of them vanished from the audit.
            // A dedup key that is not unique per object turns a widened audit
            // into a narrowed one — which is the exact failure this sweep was
            // added to fix, reintroduced one layer down.
            var seen = new HashSet<Button>();
            foreach (var group in FoldHeaders(canvas))
            {
                group.onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(canvas.GetComponent<RectTransform>());

                foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                {
                    if (!button.gameObject.activeInHierarchy) continue;
                    // Union across states: a control visible in two fold states
                    // is one control, not two.
                    if (!seen.Add(button)) continue;
                    var path = Path(button.transform);
                    measured += 1;
                    var world = WorldRect(button.GetComponent<RectTransform>());
                    var w = world.width * SpecCssPerUnit;
                    var h = world.height * SpecCssPerUnit;
                    Assert.That(world.width > 0f && world.height > 0f, Is.True,
                        $"degenerate rect (layout did not resolve): {path}");
                    if (w >= MinCssPx && h >= MinCssPx) continue;
                    undersized.Add(LabelOf(button));
                    report.AppendLine($"  {LabelOf(button),-18} {w,5:F1} x {h,5:F1} CSS px   {path}");
                }
            }
            TestContext.WriteLine($"[lobby touch-floor audit @390x844 portrait, "
                + $"{SpecCssPerUnit} CSS px/u, floor {MinCssPx}, union over "
                + $"{ProgressionGuide.GroupCount} fold states]\n" + report);

            // Deliberately smaller than the AMENDMENT #8 ratchet: main FIXED the
            // primary sortie controls (강하 · 서약 · 견습/숙련/판결 · 수련) so they
            // clear the floor, and a fixed control does not belong in a debt
            // table. Re-adding them after the merge would re-register debt that
            // no longer exists and hide the next real violation behind noise.
            //
            // What the merge does add is the four accordion fold headers, which
            // main never saw. They are 179.6 x 21.5 CSS px — the width is the
            // widest control in the lobby and clears easily; the HEIGHT is the
            // same 21.5 class the Sanctum tab strip already carries, so this is
            // not a new worst on either axis. Registered as negotiation entry 12,
            // which also records why non-folding labels were rejected: they cost
            // no controls but push content 1058 -> 1258 u and drop visibility
            // 41.0% -> 34.5%.
            //
            // KNOWN, DELIBERATELY UNCLOSED: the SANCTUM tabs hide controls the
            // same way the folds do, and this sweep does not cycle them. Only
            // the selected tab's contents are measured — today 성장, which is why
            // exactly three stat "+" appear below and the equip/legion/sigil rows
            // do not. Cycling them would pull ~20 untouched controls into the
            // frozen table, several undersized (the sigil face pairs at 68 x 30 u),
            // and registering that much unrelated debt inside another cycle makes
            // the next diff unreadable about which change caused what. Measured
            // and recorded, not silently ignored: same designer+pm touch-floor item.
            var expected = new Dictionary<string, int>
            {
                { "성장", 1 }, { "장비", 1 }, { "군단", 1 }, { "각인", 1 }, { "+", 3 },
                { "제1부 기록", 1 }, { "제2부 증언", 1 }, { "제3부 집행", 1 },
                { "훈련장", 1 },
            };
            var actual = new Dictionary<string, int>();
            foreach (var label in undersized)
                actual[label] = actual.TryGetValue(label, out var count) ? count + 1 : 1;

            Assert.That(measured, Is.GreaterThan(undersized.Count - 1),
                "the audit must have measured at least the undersized set");
            CollectionAssert.AreEquivalent(expected, actual,
                "lobby touch-floor debt changed. A NEW undersized control is a defect — "
                + "give it >= 44 CSS px. A control LEAVING this set is an accessibility fix; "
                + "record it in _workspace/current/pm/negotiation-record.md. Measured:\n" + report);
        }

        [Test]
        public void PrimarySortieActions_ClearThe44CssPxTouchFloor()
        {
            var canvas = BuildClearedLobby();
            var routePrefixes = new[] { "재훈련", "강하", "서약", "견습", "숙련", "판결", "수련" };

            // The accordion hides four groups out of five, so a single sweep sees
            // 5 of these 19 controls and calls the audit complete. §4d: a state UI
            // removes controls from the audit, so the audit has to walk the
            // states. Cycle-4 learned this on the Sanctum tabs; the accordion is
            // the same shape one panel over.
            //
            // Deduped by INSTANCE, not by path: sibling cards share
            // `Group0/Body/Panel/Panel`, so a path-keyed set collapses nine
            // descents into one and the sweep meant to widen the audit narrows
            // it instead (also §4d, also learned the hard way).
            var audited = new HashSet<Button>();
            var headers = FoldHeaders(canvas);
            for (var open = 0; open < headers.Count; open++)
            {
                headers[open].onClick.Invoke();
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);

                foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                {
                    if (!button.gameObject.activeInHierarchy) continue;
                    var label = LabelOf(button);
                    var isRouteAction = false;
                    for (var i = 0; i < routePrefixes.Length; i++)
                        isRouteAction |= label.StartsWith(routePrefixes[i], System.StringComparison.Ordinal);
                    if (!isRouteAction || !audited.Add(button)) continue;

                    var world = WorldRect(button.GetComponent<RectTransform>());
                    Assert.That(world.width * SpecCssPerUnit, Is.GreaterThanOrEqualTo(MinCssPx),
                        $"{label} is narrower than the phone touch floor (fold {open})");
                    Assert.That(world.height * SpecCssPerUnit, Is.GreaterThanOrEqualTo(MinCssPx),
                        $"{label} is shorter than the phone touch floor (fold {open})");
                }
            }

            Assert.That(audited.Count, Is.EqualTo(StageCatalog.Entries.Count + 1 + 1
                + HackSpec.TrainingTiers + TrainingTrials.Ids.Length),
                "the audit must cover prologue, every descent, revealed pact, all tier "
                + "choices and trials — across every fold state, since no single one "
                + "shows them all");
        }

        /// <summary>
        /// The three lobby panels must not overlap at ANY effective width.
        ///
        /// Found in the browser after the origin/main merge: the campaign map was
        /// drawn over the sortie panel, burying the prologue card and the first
        /// two act rows. The arithmetic is why —
        ///
        ///   sanctum  16 .. 416              (left-anchored, 400 wide)
        ///   map      432 .. 856             (left-anchored at a CONSTANT)
        ///   sortie   (W-408) .. (W-16)      (RIGHT-anchored, 392 wide)
        ///
        /// sortie's left edge tracks the viewport and the map's does not, so they
        /// collide for every W below 1264. The stack threshold was 850, so the
        /// whole 850..1264 band shipped broken: 248 u of overlap at W=1000, and
        /// 88 u at the 1176 u a 1280 CSS px browser window actually produces
        /// (buffer 1351x900, dpr 1.25).
        ///
        /// Nothing caught it because this file audited 390x844 (stacked) and
        /// 1280x720 — the reference width, and the one non-stacked width where the
        /// constant happens to be right. Right and wrong coincided at both sampled
        /// points (§4m); the defect lived entirely between them.
        ///
        /// THREE THINGS THIS TEST HAS TO GET RIGHT, each of which an earlier draft
        /// of it got wrong. Recorded because every one of them makes the test LIE
        /// rather than fail:
        ///
        /// 1. The canvas is resized PER WIDTH, from LastEffectiveWidth. sanctum
        ///    and map are left-anchored while sortie is RIGHT-anchored, so sortie's
        ///    world x comes from the canvas edge. Leaving the canvas at the phone's
        ///    799 u while laying out for 1280 measures a frame the product never
        ///    draws — and it reports ~350 u of map-over-sortie on CORRECT code. The
        ///    shared-y-band premise below does not catch that: all three panels sit
        ///    at top -72 in both the real frame and the bogus one.
        ///
        /// 2. The sweep drives SCREEN widths; the threshold is an EFFECTIVE width.
        ///    Different numbers: 1176x720 has an effective width of 1226.9, so a
        ///    case labelled "the browser's 1176" would not be testing 1176 at all.
        ///    Every landscape case here uses h = round(921600/w), which puts the
        ///    scaler's log-lerp at scale 1 and therefore E == w within 0.4 u. The
        ///    width list IS the effective-width list, so the conflation cannot come
        ///    back by editing. The two exceptions are the ones that come from real
        ///    devices and they carry their measured E instead of a pretence: the
        ///    phone (798.7) and the browser buffer 1351x900 (1176.2).
        ///
        /// 3. The two mutations fire in DISJOINT bands, and the second is 16 u
        ///    wide. A sweep has to land inside it on purpose:
        ///
        ///      M1 (threshold 850)  RED for E in [850, 1248)   — 398 u wide
        ///      M2 (map x = 432)    RED for E in [1248, 1264)  —  16 u wide
        ///
        ///    M2 needs side-by-side (E >= 1248) AND the constant still past the
        ///    sortie edge (856 > E - 408, i.e. E < 1264). Sixteen units out of the
        ///    entire width axis. That narrowness is the answer to "how did an audit
        ///    at 390 and 1280 miss this", and it is why the 1248..1263 rows below
        ///    are load-bearing: delete them and M2 ships green while every other
        ///    assertion here still passes.
        ///
        /// Mutations that turn this RED. Quoted as map-over-sortie AREA, because
        /// that is what Check measures and prints — and area is the more sensitive
        /// test: the panels are 320-620 u tall, so a linear overlap of a fraction of
        /// a unit is still hundreds of u2 and does not slip under the epsilon.
        ///   - Stack threshold 1248 -> 850: RED at 7 of the 16 cases —
        ///     E = 1000 (79,429 u2), 1100 (47,398), 1176 (23,118), 1200 (15,360),
        ///     1240 (2,500), 1247 (305), and 1351x900 (22,979).
        ///   - Map x back to the constant 432: RED at 5 cases, all inside the 16 u
        ///     window — E = 1248 (4,995 u2), 1251 (4,244), 1255 (2,786),
        ///     1259 (1,597), 1263 (405). Caught by NOTHING outside that window:
        ///     not by 1264, not by 1280, not by 390.
        ///
        /// Deliberately NOT asserted: whether the stacked column FITS. A landscape
        /// window that stacks (E ~ 1000) puts a 1604 u column against a ~920 u
        /// canvas, so the map lands off the bottom. That is a containment and
        /// scrolling question, not an overlap one, and folding it in would give one
        /// test two failure meanings. Measured and reported, not silently ignored.
        /// </summary>
        [Test]
        public void LobbyPanels_NeverOverlap_AtAnyEffectiveWidth()
        {
            // (screen w, screen h, expected effective width, why this row exists).
            var cases = new (int W, int H, float E, string Why)[]
            {
                (390,  844,  798.7f,  "phone portrait — the width this file already audited"),
                (1000, 922,  1000f,   "M1 band, worst overlap of the sampled set"),
                (1100, 838,  1100f,   "M1 band"),
                (1176, 784,  1176f,   "M1 band at the browser's effective width"),
                (1200, 768,  1200f,   "M1 band"),
                (1240, 743,  1240f,   "M1 band, last width before the switch"),
                (1247, 739,  1247f,   "threshold bracket: last stacked width"),
                (1248, 738,  1248f,   "threshold bracket: first side-by-side width, M2 worst"),
                (1251, 737,  1251f,   "inside the 16 u M2 window"),
                (1255, 734,  1255f,   "inside the 16 u M2 window"),
                (1259, 732,  1259f,   "inside the 16 u M2 window"),
                (1263, 730,  1263f,   "last width inside the M2 window"),
                (1264, 729,  1264f,   "first width past the M2 window"),
                (1280, 720,  1280f,   "the reference width — where the old constant was right"),
                (1600, 576,  1600f,   "wide desktop, gutter far past the map"),
                (1351, 900,  1176.2f, "the browser buffer that produced the report (dpr 1.25)"),
            };

            var canvas = BuildClearedLobby();
            var canvasRect = (RectTransform)canvas.transform;
            var collisions = new List<string>();
            var sideBySide = 0;
            var stacked = 0;
            var tightestGutter = float.PositiveInfinity;
            string tightestAt = null;
            var sampled = new List<string>();

            // One lobby, re-laid at each width: ApplyLobbyLayoutForTest forces the
            // pass, which is the path a real resize takes. Rebuilding per width
            // would test construction instead of reflow, and reflow is where the
            // panels move.
            foreach (var c in cases)
            {
                _lobby.ApplyLobbyLayoutForTest(c.W, c.H);

                // Round-trip the scaler coupling rather than describing it (§4i):
                // every expected E here was derived from the 1280x720 reference, so
                // a reference change must fail loudly instead of quietly re-pointing
                // the whole sweep at widths other than the ones it names.
                Assert.That(_lobby.LastEffectiveWidth, Is.EqualTo(c.E).Within(1f),
                    $"{c.W}x{c.H} resolved to an effective width of "
                    + $"{_lobby.LastEffectiveWidth:F2}, not the {c.E:F1} this case is built on "
                    + $"({c.Why}). The scaler's reference resolution moved, so every width in "
                    + "this sweep now audits a different point than its label claims");

                // The frame the product actually draws at this viewport. Without
                // this, the right-anchored sortie panel is measured against the
                // wrong canvas edge and correct code reports ~350 u of overlap.
                var effective = _lobby.LastEffectiveWidth;
                canvasRect.sizeDelta = new Vector2(effective, effective * c.H / c.W);
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
                Assert.That(canvasRect.rect.width, Is.EqualTo(effective).Within(0.01f),
                    $"{c.W}x{c.H}: canvas failed to take the effective width {effective:F2}");

                var sanctum = WorldRect(_lobby.SanctumRectForTest);
                var map = WorldRect(_lobby.MapPanelRectForTest);
                var sortie = WorldRect(_lobby.SortieRectForTest);
                var at = $"{c.W}x{c.H} (E={effective:F1}, "
                    + $"{(_lobby.StackedForTest ? "stacked" : "side-by-side")})";

                foreach (var rect in new[] { sanctum, map, sortie })
                    Assert.That(rect.width > 0f && rect.height > 0f, Is.True,
                        $"{at}: a panel resolved to a degenerate rect "
                        + $"[{rect.width:F1} x {rect.height:F1}], so overlap cannot be measured");

                if (_lobby.StackedForTest)
                {
                    stacked++;
                    // Premise for the stacked half: the column separates
                    // VERTICALLY, so the panels must still share x. Without this,
                    // "no overlap" could be passing because a panel drifted
                    // sideways out of the column entirely.
                    var shareX = Mathf.Min(sanctum.xMax, sortie.xMax)
                               - Mathf.Max(sanctum.xMin, sortie.xMin);
                    Assert.That(shareX, Is.GreaterThan(1f),
                        $"{at}: sanctum and sortie share only {shareX:F1} u of x, so the "
                        + "stacked column is not a column and the vertical separation below "
                        + "is being proved by a horizontal gap instead");
                }
                else
                {
                    sideBySide++;
                    // Premise for the side-by-side half: side by side means SIDE by
                    // side. If the row stopped sharing a y band, every overlap check
                    // below would pass for the wrong reason.
                    Assert.That(sanctum.yMax, Is.EqualTo(map.yMax).Within(1f),
                        $"{at}: sanctum and map must share the row's top edge");
                    Assert.That(map.yMax, Is.EqualTo(sortie.yMax).Within(1f),
                        $"{at}: map and sortie must share the row's top edge");

                    // Left to right, in the order the layout claims to place them.
                    // Asserted positively so a panel that jumps to the wrong side of
                    // another fails even when the two happen not to touch.
                    Assert.That(sanctum.xMax, Is.LessThanOrEqualTo(map.xMin + OverlapEpsilon),
                        $"{at}: sanctum [{sanctum.xMin:F1}..{sanctum.xMax:F1}] must end "
                        + $"before the map begins [{map.xMin:F1}..{map.xMax:F1}]");
                    Assert.That(map.xMax, Is.LessThanOrEqualTo(sortie.xMin + OverlapEpsilon),
                        $"{at}: map [{map.xMin:F1}..{map.xMax:F1}] must end before sortie "
                        + $"begins [{sortie.xMin:F1}..{sortie.xMax:F1}]");

                    var gutter = sortie.xMin - map.xMax;
                    if (gutter < tightestGutter) { tightestGutter = gutter; tightestAt = at; }
                }

                sampled.Add($"{effective:F0}:{(_lobby.StackedForTest ? "stack" : "row")}");

                Check(at, "sanctum", sanctum, "map", map);
                Check(at, "map", map, "sortie", sortie);
                Check(at, "sanctum", sanctum, "sortie", sortie);
            }

            // --- the sweep must have exercised BOTH arrangements --------------
            Assert.That(sideBySide, Is.GreaterThan(0),
                "every sampled width stacked, so the side-by-side row — the only arrangement "
                + $"that can collide — was never exercised. Sampled: {string.Join(" ", sampled)}");
            Assert.That(stacked, Is.GreaterThan(0),
                "no sampled width stacked, so the threshold itself is untested and a sweep "
                + "entirely above it would pass with the old 850 threshold still in place. "
                + $"Sampled: {string.Join(" ", sampled)}");

            // --- and it must have reached INSIDE the 16 u M2 window ----------
            //
            // A map pinned at the constant 432 overlaps sortie by 16 - 2*gutter, so
            // it is only visible past the 1 u epsilon while the gutter is under
            // 7.5 u. If a later edit trims the 1248..1263 rows for looking
            // redundant, this assert is the only thing that notices the mutation
            // stopped being detectable.
            Assert.That(tightestGutter, Is.LessThan(6f),
                $"the tightest side-by-side gutter sampled was {tightestGutter:F1} u at "
                + $"{tightestAt}. Nothing in this sweep lands inside the 16 u window "
                + "(E in [1248, 1264)) where a map pinned at the constant 432 still runs into "
                + "the sortie panel, so that mutation would ship green. Restore a width just "
                + "above the stack threshold");

            Assert.That(collisions, Is.Empty,
                $"{collisions.Count} lobby panel overlap(s). The map is the only panel with no "
                + "anchor of its own, so it is the one that drifts into a neighbour when the "
                + $"gutter arithmetic is wrong:\n{string.Join("\n", collisions)}");

            void Check(string at, string an, Rect a, string bn, Rect b)
            {
                var area = OverlapArea(a, b);
                if (area <= OverlapEpsilon) return;
                var dx = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                var dy = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                collisions.Add($"  {at}: {an} [{a.xMin:F1}..{a.xMax:F1} x "
                    + $"{a.yMin:F1}..{a.yMax:F1}] over {bn} [{b.xMin:F1}..{b.xMax:F1} x "
                    + $"{b.yMin:F1}..{b.yMax:F1}] by {dx:F1} x {dy:F1} = {area:F0} u2");
            }
        }

        /// <summary>Overlapping area of two world rects, 0 when disjoint.</summary>
        private static float OverlapArea(Rect a, Rect b)
        {
            var w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            var h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return w <= 0f || h <= 0f ? 0f : w * h;
        }


        /// <summary>
        /// W8 added the campaign map panel, and it is the ONLY route to the tab
        /// meta screen on a phone — the stacked layout has no other entry point.
        /// Both of its actions are therefore measured positively here, not just
        /// left to the debt table's absence check: a control that is unreachable
        /// on the one layout where it is the sole route is a dead end.
        /// </summary>
        [Test]
        public void CampaignMapActions_AreReachableAndClearTheTouchFloor()
        {
            var canvas = BuildClearedLobby();   // 390x844 -> stacked layout
            foreach (var label in new[] { "지도", "정비" })
            {
                var button = FindButton(canvas, label);
                Assert.That(button, Is.Not.Null, $"the map panel must expose {label}");
                Assert.That(button.gameObject.activeInHierarchy, Is.True,
                    $"{label} is the phone's only route to the meta screen and must stay active");
                var world = WorldRect(button.GetComponent<RectTransform>());
                Assert.That(world.width * SpecCssPerUnit, Is.GreaterThanOrEqualTo(MinCssPx), label);
                Assert.That(world.height * SpecCssPerUnit, Is.GreaterThanOrEqualTo(MinCssPx), label);
            }
        }

        /// <summary>
        /// The lobby used to state every route's progression TWICE at once: as a
        /// word on the right edge of each sortie card ("정화 완료"/"강하 가능"/
        /// "잠김", LobbyView.Refresh) and as node opacity + hidden "???" labels +
        /// a "정화 N / 9 • 다음 X" header on the 심연 지도 panel beside it. The
        /// two states the card repeated for free are gone; the two it uniquely
        /// carries stay — the clear marker, and the N2 target disclosure, which
        /// marks ONE card rather than restating a state on all nine. This pins
        /// the split so a future edit cannot quietly restore the wall of nine
        /// status chips.
        /// </summary>
        [Test]
        public void SortieCards_StateOnlyWhatTheMapCannot()
        {
            BuildClearedLobby();   // cinder-span cleared, ember-gallery reachable

            Assert.That(_lobby.StageStatusReadout(0), Is.EqualTo("정화 완료"),
                "a cleared card keeps a live 강하 button for replay, so the clear "
                + "marker is the ONE state nothing else on the card shows");

            // Exactly one card may carry the target word, and never more.
            var targets = 0;
            for (var i = 1; i < StageCatalog.Entries.Count; i++)
            {
                var readout = _lobby.StageStatusReadout(i);
                if (readout == "다음 재판") { targets++; continue; }
                Assert.That(readout, Is.Empty,
                    $"card {i} must not repeat a route state the map already owns "
                    + "(reachable -> its own enabled 강하 button; locked -> the "
                    + "map's ??? node)");
            }
            Assert.That(targets, Is.LessThanOrEqualTo(1),
                "the target disclosure marks a single door; more than one turns "
                + "it back into a per-card state wall");
        }

        /// <summary>
        /// The other half of that trade: removing "잠김" made the map's "???"
        /// label the only place the lock is stated in words, and the map used to
        /// draw it at the NODE's 0.16 reveal alpha — Ink over Charcoal at 1.6:1,
        /// which is not text anyone reads. Labels now ride their own floor.
        /// </summary>
        [Test]
        public void CampaignMap_DrawsLockedLabelsAtAReadableOpacity()
        {
            BuildClearedLobby();
            var map = _lobby.CompactMap;
            Assert.That(map, Is.Not.Null, "the lobby must build its compact map");

            // cinder-span cleared -> ember-gallery reachable -> the rest locked.
            for (var i = 2; i < map.NodeCount; i++)
            {
                Assert.That(map.LabelAt(i), Is.EqualTo(CampaignMapLayout.HiddenLabel),
                    $"node {i} is locked and must still SAY so");
                Assert.That(map.LabelAlphaAt(i),
                    Is.GreaterThanOrEqualTo(0.6f),
                    $"node {i}'s lock label is now load-bearing and must be legible; "
                    + $"the node itself stays at the {CampaignMapLayout.LockedAlpha} reveal alpha");
                Assert.That(map.AlphaAt(i), Is.EqualTo(CampaignMapLayout.LockedAlpha).Within(0.001f),
                    "raising the LABEL floor must not flatten the node reveal ladder");
            }
        }

        /// <summary>
        /// "Where do I go next" was carried only by the frontier pulse, and
        /// CampaignMapView.Tick returns early under 모션 약함 — so the players who
        /// most need a static cue were the ones getting none. The ring is a
        /// placement, not an animation.
        /// </summary>
        [Test]
        public void CampaignMap_MarksTheFrontier_WithMotionReducedToo()
        {
            var hadPref = PlayerPrefs.HasKey(ReducedMotionKey);
            var previous = PlayerPrefs.GetInt(ReducedMotionKey, 0);
            try
            {
                ViewPrefs.ReducedMotion = true;
                BuildClearedLobby();
                var map = _lobby.CompactMap;

                Assert.That(map.FrontierMarkerVisible, Is.True,
                    "the reachable stage must be marked without relying on the pulse");
                Assert.That(map.FrontierMarkerPosition, Is.EqualTo(map.NodeCentreAt(1)),
                    "cinder-span is cleared, so ember-gallery is the frontier");
                Assert.That(map.LabelColorAt(1).a, Is.EqualTo(1f).Within(0.001f),
                    "the frontier's name is fully lit — colour is never the only cue");

                map.Tick(0.5f);
                Assert.That(map.FrontierMarkerVisible, Is.True,
                    "a tick under reduced motion must not retire the marker");
            }
            finally
            {
                ViewPrefs.ReducedMotion = previous == 1;
                if (!hadPref) PlayerPrefs.DeleteKey(ReducedMotionKey);
                ViewPrefs.ResetCacheForTests();
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// The accordion's fold headers, in group order. Found by hierarchy
        /// (StageContent/Group{n}/Panel) rather than by label: the header's own
        /// text is a design decision that may change, while its position in the
        /// tree is the structure this test is actually about.
        /// </summary>
        private static List<Button> FoldHeaders(Canvas canvas)
        {
            var headers = new List<Button>();
            for (var g = 0; g < ProgressionGuide.GroupCount; g++)
            {
                var wanted = "Group" + g;
                foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                {
                    var parent = button.transform.parent;
                    if (parent != null && parent.name == wanted) { headers.Add(button); break; }
                }
            }
            Assert.That(headers.Count, Is.EqualTo(ProgressionGuide.GroupCount),
                "every accordion group must expose exactly one fold header");
            return headers;
        }

        private Canvas BuildClearedLobby()
        {
            // cinder-span cleared: the only state that reveals a pact toggle.
            var data = new CampaignData
            {
                PrologueDone = true,
                ClearedMask = 1,
                Roster = new string[0],
                Active = string.Empty,
            };
            _lobby.Build(data, default);
            _lobby.Refresh(data);
            _lobby.ApplyLobbyLayoutForTest(390, 844);

            var canvas = _lobbyObject.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null, "the lobby must build its canvas");
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(EffectiveWidth, EffectiveHeight);
            rect.localScale = Vector3.one;
            rect.position = Vector3.zero;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            return canvas;
        }

        private static Button FindButton(Canvas canvas, string labelPrefix)
        {
            foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                if (LabelOf(button).StartsWith(labelPrefix, System.StringComparison.Ordinal))
                    return button;
            return null;
        }

        private static string LabelOf(Button button)
        {
            var text = button.GetComponentInChildren<Text>(true);
            return text == null ? "" : text.text;
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var min = new Vector2(corners[0].x, corners[0].y);
            var max = new Vector2(corners[2].x, corners[2].y);
            return new Rect(min, max - min);
        }

        private static string Describe(Rect rect)
            => $"[{rect.xMin:F0}..{rect.xMax:F0} x {rect.yMin:F0}..{rect.yMax:F0}]";

        private static string Path(Transform transform)
        {
            var path = transform.name;
            var parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
