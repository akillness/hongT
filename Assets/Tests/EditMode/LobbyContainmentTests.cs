// Containment audit for the cycle-8 lobby rail.
//
// A SEPARATE file from LobbyLayoutTests on purpose (D-10). Overlap and
// containment are different invariants, and a file that holds both gives one
// red run two possible meanings — "the panels collided" and "a panel left the
// screen" are not the same defect and do not have the same fix. Containment is
// load-bearing enough to own a file: the shipped defect leaked out through
// exactly this gap and nothing in the suite was pointed at it.
//
// WHAT SHIPPED, and why the old suite was green while it shipped.
// build-webgl/index.html pins `#unity-canvas.unity-responsive` to
// `aspect-ratio: 1280 / 853` for every viewport at or above 501 CSS, and that
// aspect yields an effective width of ~1176 u at EVERY window size and DPR.
// The old ApplyLobbyTier branched at SideBySideFloor = 1248, so 1176 < 1248
// meant every player, always, got the stacked column — a 1604 u stack against
// a 783.7 u canvas, with no root ScrollRect to reach the rest:
//
//     SORTIE    top  -72   bottom  -692     620.0 u visible   100%
//     SANCTUM   top -708   bottom -1268      75.7 u visible    13.5%
//     MAP       top -1284  bottom -1604       0.0 u visible     0%
//
// And 13.5% overstates it. TabContent starts 116 u below the panel top
// (LobbyView.TabContent), past the 75.7 u that was on screen, so every buy
// button in the sanctum was at 0% while the top bar kept advertising the relic
// balance that pays for them.
//
// Three ways the old suite reported this as fine. Each one is a test that
// LIED rather than a test that was missing, and each has a guard below:
//
//   1. OVERLAP IS NOT CONTAINMENT. LobbyPanels_NeverOverlap_AtAnyEffectiveWidth
//      swept 16 cases and all 16 were WIDTH indices — zero y samples. Panels
//      that are off the bottom of the screen and not overlapping each other is
//      precisely the state that sweep calls passing. Guard: test 1 measures
//      all four canvas edges, and the two axes are defended by DIFFERENT rows
//      (see the mutation table on test 1).
//   2. THE NAME LIED. CampaignMapActions_AreReachableAndClearTheTouchFloor was
//      green at 0% map visibility, because it only read `interactable` and the
//      rect's own size. Nothing that claims "reachable" may stop at the
//      control's dimensions; it has to ask whether the control is on the
//      canvas. Guard: containment is measured against the canvas rect, not
//      against the panel's own size.
//   3. A PASS MEASURED AT THE BEST SCALE. The same 92 u button is 100.1 CSS px
//      at the 1280 deploy frame and 39.2 px at the 501 letterbox floor. Pick
//      the scale and you pick the answer. Guard: test 4 measures at the two
//      real band worsts, not at a convenient one.
//
// A FOURTH claim this file holds, which is not one of the three lies but the
// hole they leave behind: the RAIL itself must be on the canvas. Panel
// containment is measured after SelectRail opens a panel, and a test can call
// SelectRail without an icon — so an icon that fell off the bottom takes its
// panel's only route with it while panel containment stays green. Test 2.
//
// Screen.* is degenerate in batchmode, so every case switches the canvas to
// WorldSpace and sizes it to the effective canvas — the seam HudLayoutTests
// and LobbyLayoutTests already use.
//
// FindButton is deliberately NOT copied over from LobbyLayoutTests alongside
// WorldRect and LabelOf. It matches on a label PREFIX, and the rail
// now owns the words 성소 / 출정 / 지도 as button labels. A prefix lookup for
// "지도" no longer finds the map panel's action (renamed 전체 지도, D-R4) — it
// finds the RAIL ICON, first in GetComponentsInChildren order, and both clear
// 44 px so the swap is silent. Nothing here looks a control up by label.
using System.Collections.Generic;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class LobbyContainmentTests
    {
        /// <summary>SIM_SPEC_HACKSLASH §9 touch floor.</summary>
        private const float MinCssPx = 44f;
        /// <summary>
        /// A rect may touch an edge but not cross it.
        ///
        /// 1 u sits far below every real clearance in the file — the tightest
        /// is 16.0 u (RailLeft, the rail's own inset) — and far below every
        /// overrun the mutation tables expect to catch, the smallest of which
        /// is 14.7 u. So it never turns a real defect green by accident.
        ///
        /// It does swallow ONE documented case, deliberately: M-B (map pinned
        /// at the old constant 432) overruns the 375x667 row by 0.5 u, and that
        /// row reports green. The other two portrait rows catch the same
        /// mutation by 57.3 and 64.5 u, so the mutation is caught — but a
        /// reader diffing the M-B table against these asserts should know the
        /// 375 green is an epsilon decision and not an arithmetic accident.
        /// </summary>
        private const float Epsilon = 1f;
        private const string CampaignKey = "abyssal-lantern:unity:campaign";

        private GameObject _lobbyObject;
        private LobbyView _lobby;
        private bool _hadCampaign;
        private string _campaignPayload;

        /// <summary>
        /// The shipping viewport table, measured (harness D-4/D-11), not guessed.
        ///
        /// Two scale bands, because the WebGL template has two behaviours:
        ///   band A  CSS width >= 501, excluding landscape height <= 500   aspect-locked to 1280:853   worst 0.4261 px/u
        ///   band B  CSS width <= 500 or landscape height <= 500            fill                        worst 0.4383 px/u
        ///                                                    (within support)
        ///
        /// 375x667 (iPhone SE2) is the support FLOOR named by D-11. 320 CSS is
        /// explicitly out of range and is measured-but-not-asserted in test 4.
        ///
        /// Note before pruning a row for looking redundant: the portrait rows
        /// are NOT ordered by effective width. 375x667 resolves to E_w 855.5
        /// while the LARGER 390x844 resolves to 798.7, because portrait match
        /// 0.35 leans the scaler on height. The narrowest effective width in
        /// this table belongs to the widest phone (412x915 -> 791.5). Deleting
        /// "the small one" deletes the wrong row.
        /// </summary>
        private static readonly (int CssW, int CssH, float Ew, float Eh, string Why)[] Viewports =
        {
            (1280, 853, 1176.0f,  783.7f, "deploy: the only frame the template makes, at any window size"),
            ( 501, 334, 1175.8f,  783.8f, "letterbox floor: band A worst scale, 0.4261 px/u"),
            ( 375, 667,  855.5f, 1521.6f, "support floor: iPhone SE2, named by D-11"),
            ( 390, 844,  798.7f, 1728.6f, "the phone this suite already audited"),
            ( 412, 915,  791.5f, 1757.9f, "Pixel 7 — narrowest effective width in the table"),
            (1280, 720, 1280.0f,  720.0f, "editor 16:9: the frame the defect was reported from"),
        };

        [SetUp]
        public void SetUp()
        {
            _hadCampaign = PlayerPrefs.HasKey(CampaignKey);
            _campaignPayload = PlayerPrefs.GetString(CampaignKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (_lobbyObject != null) Object.DestroyImmediate(_lobbyObject);
            _lobbyObject = null;
            _lobby = null;
            var eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem != null) Object.DestroyImmediate(eventSystem.gameObject);
            if (_hadCampaign) PlayerPrefs.SetString(CampaignKey, _campaignPayload);
            else PlayerPrefs.DeleteKey(CampaignKey);
            PlayerPrefs.Save();
        }

        // =====================================================================
        // TEST 1 — the invariant this cycle exists to install: the open panel.
        // =====================================================================

        /// <summary>
        /// The selected panel is entirely inside the canvas, at every shipping
        /// viewport. Three rail selections x six viewports = 18 measurements.
        ///
        /// This is the replacement for the overlap sweep, not an addition to
        /// it: a radio that draws exactly one panel makes horizontal collision
        /// impossible to construct, so "they do not overlap" became a claim
        /// about nothing while "it is on the screen" became the claim that was
        /// never made (D-3/D-10).
        ///
        /// FOUR SEPARATE ASSERTS, ONE PER EDGE. Folding them into a single
        /// Rect.Contains would reduce every failure to "not inside", and the
        /// edge is the diagnosis: a bottom overrun is a stacked-column
        /// regression, a right overrun is a rail/panel derivation break, and a
        /// left or top overrun means an anchor was lost. The overrun magnitude
        /// is printed for the same reason — 820.3 u names the old MAP placement
        /// on sight.
        ///
        /// MUTATION TABLE. Which rows are load-bearing, and for which defect.
        /// The two axes are covered by DISJOINT halves of the table, which is
        /// the whole reason the table is six rows and not two:
        ///
        ///   M-A  restore the stacked column (SANCTUM top -708, MAP top -1284)
        ///        = the exact shipped defect.
        ///        SANCTUM bottom sits 1268 u below the canvas top -> RED wherever
        ///        E_h < 1268:  1280x853 (over by 484.3 u), 501x334 (484.2),
        ///                     1280x720 (548.0)
        ///        MAP bottom sits 1604 u down -> RED wherever E_h < 1604:
        ///                     1280x853 (820.3), 501x334 (820.2),
        ///                     1280x720 (884.0), 375x667 (82.4)
        ///        4 of 6 rows red — and GREEN at 390x844 and 412x915. Read that
        ///        twice: a containment test sampled only at the phone widths
        ///        this suite already owned would have shipped the defect a
        ///        second time. The LANDSCAPE rows are what catch it, and the
        ///        deploy frame is landscape.
        ///
        ///   M-B  break PanelLeft's derivation and re-pin the map at the old
        ///        side-by-side constant 432. Right edge = 856 u -> RED wherever
        ///        E_w < 856:  412x915 (over by 64.5 u), 390x844 (57.3)
        ///        2 of 6 rows red, and both are PORTRAIT. 375x667 misses it by
        ///        0.5 u (E_w 855.5) — inside the epsilon, green. So the row that
        ///        catches the width defect is the widest phone, and the row that
        ///        catches the height defect is the deploy desktop. Neither axis
        ///        is defended by the rows that defend the other.
        ///
        ///   M-C  delete the radio, leave all three panels active. NOT caught
        ///        here — all three pin to the same origin, so each is contained
        ///        on its own. Test 3 is the only guard. Recorded so nobody
        ///        assumes this test covers activation.
        ///
        ///   M-D  make SelectRail — the SETTER — a toggle. NOT caught here, and
        ///        it used to be: the loop below walks 0,1,2 on a lobby that now
        ///        opens CLOSED (D-12), so every step targets an entry that is
        ///        not live and a toggle behaves exactly like a set. It was
        ///        caught when the default was 출정, because SelectRail(1) then
        ///        closed the panel it was about to measure. The guard moved to
        ///        test 3's REVERSE walk, which re-enters the live entry by
        ///        construction. Recorded because the coverage moved without any
        ///        assertion here changing — a diff of this test shows nothing.
        /// </summary>
        [Test]
        public void SelectedPanel_IsFullyInsideTheCanvas_AtEveryShippingViewport()
        {
            var measured = 0;
            var clearances = new List<string>();

            foreach (var v in Viewports)
            {
                var canvas = BuildLobbyAt(v.CssW, v.CssH, v.Ew, v.Eh);
                var canvasRect = WorldRect((RectTransform)canvas.transform);

                for (var rail = 0; rail < 3; rail++)
                {
                    _lobby.SelectRail(rail);
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);

                    var (name, rect) = PanelFor(rail);
                    var at = $"{v.CssW}x{v.CssH} CSS (E {v.Ew:F1}x{v.Eh:F1} u, {v.Why})";

                    // The premise. SelectRail(rail) must have OPENED rail's
                    // panel — without this the loop below would happily measure
                    // a hidden rect and report the lobby as contained because
                    // nothing is on it. This is also what catches M-D: rail 1
                    // is the build default, so a toggle closes it here.
                    Assert.That(rect.gameObject.activeInHierarchy, Is.True,
                        $"{at}: SelectRail({rail}) did not open {name}. Containment cannot be "
                        + "measured on a panel the player cannot see, and a sweep that skipped "
                        + "it would report an empty lobby as perfectly contained");

                    var panel = WorldRect(rect);
                    Assert.That(panel.width > 0f && panel.height > 0f, Is.True,
                        $"{at}: {name} resolved to a degenerate rect "
                        + $"[{panel.width:F1} x {panel.height:F1}] — layout did not run, so "
                        + "containment below would be measured on a point");

                    // One assert per edge. Overruns are reported in u; divide by
                    // the row's px/u to get CSS px if that reads better.
                    Assert.That(panel.xMin, Is.GreaterThanOrEqualTo(canvasRect.xMin - Epsilon),
                        Breach(at, name, "LEFT", canvasRect.xMin - panel.xMin, panel, canvasRect));
                    Assert.That(panel.xMax, Is.LessThanOrEqualTo(canvasRect.xMax + Epsilon),
                        Breach(at, name, "RIGHT", panel.xMax - canvasRect.xMax, panel, canvasRect));
                    Assert.That(panel.yMax, Is.LessThanOrEqualTo(canvasRect.yMax + Epsilon),
                        Breach(at, name, "TOP", panel.yMax - canvasRect.yMax, panel, canvasRect));
                    Assert.That(panel.yMin, Is.GreaterThanOrEqualTo(canvasRect.yMin - Epsilon),
                        Breach(at, name, "BOTTOM", canvasRect.yMin - panel.yMin, panel, canvasRect));

                    // Tightest margin per case, for the record. The 1280x720
                    // editor frame leaves SORTIE 28.0 u of bottom clearance and
                    // that is the whole budget the layout has — a panel growing
                    // 29 u taller leaves the screen there first.
                    var slack = Mathf.Min(
                        Mathf.Min(panel.xMin - canvasRect.xMin, canvasRect.xMax - panel.xMax),
                        Mathf.Min(canvasRect.yMax - panel.yMax, panel.yMin - canvasRect.yMin));
                    clearances.Add($"  {v.CssW}x{v.CssH,-4} {name,-8} slack {slack,7:F1} u");
                    measured += 1;
                }
            }

            TestContext.WriteLine("[lobby containment: tightest edge clearance per case]\n"
                + string.Join("\n", clearances));

            // A skipped case is an unmeasured case. Every guard above lives
            // inside the loop, so a `continue` slipped in for any reason — an
            // inactive panel, a null rect, a viewport row commented out during
            // a debug session — silently shrinks this audit to nothing while
            // every assert still passes. 6 viewports x 3 rails, no exceptions.
            Assert.That(measured, Is.EqualTo(Viewports.Length * 3),
                $"only {measured} of {Viewports.Length * 3} panel/viewport pairs were actually "
                + "measured. A containment sweep that measures nothing passes");
        }

        // =====================================================================
        // TEST 2 — the same invariant for the navigation that reaches it.
        // =====================================================================

        /// <summary>
        /// All three rail icons are inside the canvas, at every shipping
        /// viewport. Same six rows, same four edges, different invariant.
        ///
        /// WHY THIS IS NOT A SUBSET OF TEST 1. Test 1 proves the open panel is
        /// on screen. It says nothing about whether the player can OPEN the
        /// other two, and the rail is the only route to them — the tier branch
        /// that used to draw all three at once is gone (D-3), so 성소 and 지도
        /// are reachable through their icons or not at all. A rail whose third
        /// icon falls off the bottom takes 지도 with it, and test 1 stays green
        /// the whole time: the MAP panel it can no longer reach is still
        /// perfectly contained, because SelectRail is what test 1 uses to open
        /// it and a test does not need an icon to call a method. That is the
        /// same shape as the defect this cycle repaired — a surface that is
        /// geometrically fine and practically unreachable — one layer up.
        ///
        /// Kept out of test 1 rather than folded in for the reason this file
        /// exists (D-10): "a panel left the screen" and "the navigation left
        /// the screen" have different fixes, and one red run should not be
        /// able to mean either.
        ///
        /// GEOMETRY. Three 103.3 u squares on a 115.3 u pitch from PanelTop:
        /// 16..119.3 across, -72..-405.9 down, so 333.9 u of rail. The binding
        /// margin is the LEFT inset — 16 u, the same RailLeft the panels are
        /// derived from — at every viewport, because the vertical slack is
        /// never below 314 u.
        ///
        /// MUTATIONS:
        ///   M-R1  lose the top anchor: build the rail at anchor (0,0) instead
        ///         of (0,1). TextButton passes its anchor straight through as
        ///         pivot AND both anchor corners, so -72 stops meaning "72 u
        ///         below the top" and starts meaning "72 u below the BOTTOM".
        ///         Every icon leaves the canvas floor — RED at all 6 rows, by
        ///         72.0 u on the first icon, 187.3 on the second and 302.6 on
        ///         the third. Note the rail would still be 333.9 u tall and
        ///         still perfectly self-consistent; only its relationship to
        ///         the canvas is wrong, which is precisely what an audit of the
        ///         rail's own rects (test 4) cannot see.
        ///   M-R2  grow the rail to six entries (bottom -751.8). RED at exactly
        ///         ONE row: the 1280x720 editor frame, over by 31.8 u. The
        ///         deploy frame survives it by 31.9 u and the letterbox by 32.0.
        ///         The shortest canvas in the table is the only thing defending
        ///         the rail's height budget, and it is the frame nobody ships —
        ///         which is the argument for keeping a non-shipping row in a
        ///         shipping table, not against it.
        /// </summary>
        [Test]
        public void RailIsFullyInsideTheCanvas_AtEveryShippingViewport()
        {
            var measured = 0;
            var clearances = new List<string>();

            foreach (var v in Viewports)
            {
                var canvas = BuildLobbyAt(v.CssW, v.CssH, v.Ew, v.Eh);
                var canvasRect = WorldRect((RectTransform)canvas.transform);

                for (var icon = 0; icon < 3; icon++)
                {
                    var rect = _lobby.RailRectForTest(icon);
                    Assert.That(rect, Is.Not.Null, $"rail entry {icon} must exist");

                    // The rail is never hidden — it is the frame the panels
                    // swap inside. An inactive icon is a destination the player
                    // cannot see, which containment cannot speak to.
                    Assert.That(rect.gameObject.activeInHierarchy, Is.True,
                        $"rail entry {icon} is inactive; the rail is always on, and an icon "
                        + "that is not drawn is a panel that cannot be opened");

                    var rail = WorldRect(rect);
                    var label = LabelOf(rect.GetComponent<Button>());
                    var at = $"{v.CssW}x{v.CssH} CSS (E {v.Ew:F1}x{v.Eh:F1} u, {v.Why}), "
                           + $"rail {icon} \"{label}\"";

                    Assert.That(rail.xMin, Is.GreaterThanOrEqualTo(canvasRect.xMin - Epsilon),
                        Breach(at, "rail", "LEFT", canvasRect.xMin - rail.xMin, rail, canvasRect));
                    Assert.That(rail.xMax, Is.LessThanOrEqualTo(canvasRect.xMax + Epsilon),
                        Breach(at, "rail", "RIGHT", rail.xMax - canvasRect.xMax, rail, canvasRect));
                    Assert.That(rail.yMax, Is.LessThanOrEqualTo(canvasRect.yMax + Epsilon),
                        Breach(at, "rail", "TOP", rail.yMax - canvasRect.yMax, rail, canvasRect));
                    Assert.That(rail.yMin, Is.GreaterThanOrEqualTo(canvasRect.yMin - Epsilon),
                        Breach(at, "rail", "BOTTOM", canvasRect.yMin - rail.yMin, rail, canvasRect));

                    var slack = Mathf.Min(
                        Mathf.Min(rail.xMin - canvasRect.xMin, canvasRect.xMax - rail.xMax),
                        Mathf.Min(canvasRect.yMax - rail.yMax, rail.yMin - canvasRect.yMin));
                    clearances.Add($"  {v.CssW}x{v.CssH,-4} rail {icon} {label,-4} "
                        + $"slack {slack,7:F1} u   bottom {rail.yMin - canvasRect.yMin,7:F1} u");
                    measured += 1;
                }
            }

            TestContext.WriteLine("[rail containment: tightest edge clearance, and the "
                + "vertical budget M-R2 spends]\n" + string.Join("\n", clearances));

            Assert.That(measured, Is.EqualTo(Viewports.Length * 3),
                $"only {measured} of {Viewports.Length * 3} rail/viewport pairs were actually "
                + "measured. A containment sweep that measures nothing passes");
        }

        // =====================================================================
        // TEST 3 — the assumption test 1 rests on.
        // =====================================================================

        /// <summary>
        /// AT MOST one of 성소 / 출정 / 지도 is active. Two is a failure; zero
        /// is a legal state, and reaching it is a thing the player does on
        /// purpose (D-12).
        ///
        /// D-12 RELAXED THIS FROM "EXACTLY ONE" AND THAT WAS NOT FREE. The old
        /// rule was not a preference, it was what made test 1 falsifiable: the
        /// containment sweep cannot see a panel that is not there, and an empty
        /// lobby is contained — trivially, perfectly, at every viewport. So a
        /// zero-panel state turns test 1 from an invariant into a tautology
        /// while leaving it GREEN.
        ///
        /// What replaces the count as the defence: test 1 OPENS each panel
        /// explicitly, asserts it actually opened, and asserts its own case
        /// count (18). Zero panels there fails on the open assert, not on
        /// arithmetic. That is why SelectRail had to stop being the click —
        /// the sweep needs an entry that cannot close what it just opened.
        ///
        /// THREE DOORS INTO THE ZERO-PANEL STATE, and only one of them is
        /// legal now:
        ///   · re-clicking the live icon (case 4) — LEGAL, and asserted to
        ///     work, because it is the dismiss the user asked for and nothing
        ///     else in the suite proves it exists;
        ///   · an out-of-range index that matches no panel (case 5) — still a
        ///     defect, still blocked, on BOTH entries;
        ///   · a Hide that deactivated the three panels instead of the root
        ///     (case 6) — still a defect: Show would restore a lobby whose
        ///     rail says 출정 while nothing is drawn.
        ///
        /// The distinction the whole file rests on is between a lobby that is
        /// closed and a lobby that is BLANK. Both draw zero panels. The first
        /// has _railSelected == RailClosed and three dim icons; the second has
        /// a rail claiming a destination that is not on screen. Case 5 and
        /// case 6 are the two ways to build the second one by accident, so
        /// every zero-panel assertion below also reads the rail's own index.
        ///
        /// Two is the older failure, unchanged: three panels drawn at one
        /// origin is the pre-rail stack with the y offsets removed, and it
        /// reads as a single garbled panel rather than as three.
        /// </summary>
        [Test]
        public void RailSelection_KeepsAtMostOnePanelActive()
        {
            BuildLobbyAt(1280, 853, 1176.0f, 783.7f);

            // 1. The state the player lands in. D-12 reversed AC-5's 출정
            //    default: all three panels reach 100% now, so the "only panel
            //    the build renders" argument for defaulting to one of them no
            //    longer describes anything. The lobby opens on the diorama.
            AssertNoPanelActive("build default — the lobby opens closed (D-12)");

            // 2. Forward through every entry, with the SETTER. Not the toggle:
            //    this walk is "put the rail on X and check X opened", and the
            //    toggle answers a different question on one of the three.
            for (var rail = 0; rail < 3; rail++)
            {
                _lobby.SelectRail(rail);
                AssertExactlyOneActive($"forward SelectRail({rail})", rail);
            }

            // 3. And backward. Order matters twice over. A latch that only ever
            //    opens would pass the ascending walk — each step opens the next
            //    panel and the previous one happens to be below it — and fail
            //    here. And this is the walk that re-enters the LIVE entry
            //    (rail 2 is live when the loop starts), so it is the only place
            //    left that catches the setter being turned back into a toggle.
            for (var rail = 2; rail >= 0; rail--)
            {
                _lobby.SelectRail(rail);
                AssertExactlyOneActive($"reverse SelectRail({rail})", rail);
            }

            // 4. The dismiss itself (D-12, user directive). Re-clicking the
            //    live icon CLOSES it — this is the feature, and this assertion
            //    is the only thing in the suite that proves it survives.
            //
            //    It reverses what this case asserted one revision ago, where
            //    a re-click was required to be a no-op (D-R2's radio). Left as
            //    a rewrite rather than a deletion because the pair is the
            //    interesting part: the same click is now required to do the
            //    opposite thing, and a reader diffing this file should land on
            //    the reason rather than on a case that quietly disappeared.
            _lobby.SelectRail(LobbyView.RailSortie);
            AssertExactlyOneActive("SelectRail(출정) before the re-click", LobbyView.RailSortie);
            _lobby.ToggleRail(LobbyView.RailSortie);
            AssertNoPanelActive("ToggleRail(출정) on the live entry must CLOSE it (D-12)");

            // ...and clicking a closed lobby's icon opens it. A dismiss that
            // cannot be undone by the same control is a trap, and "closed" is
            // not a state the rail should be able to get stuck in.
            _lobby.ToggleRail(LobbyView.RailSortie);
            AssertExactlyOneActive("ToggleRail(출정) on a closed lobby must open it",
                LobbyView.RailSortie);

            // Only the LIVE entry closes. Clicking a different icon while one
            // is open is a move, not a dismiss — otherwise every navigation
            // between two panels would cost two clicks.
            _lobby.ToggleRail(LobbyView.RailMap);
            AssertExactlyOneActive("ToggleRail(지도) while 출정 was live must move, not close",
                LobbyView.RailMap);

            // 5. Out of range clamps rather than blanking, on BOTH entries.
            //    A raw index write would leave _railSelected at -1 or 99, which
            //    matches no panel — the zero-panel state arriving through a bug
            //    instead of through a click, and indistinguishable on screen
            //    from the legal one asserted in case 4.
            _lobby.SelectRail(-1);
            AssertExactlyOneActive("SelectRail(-1) must clamp to the first entry",
                LobbyView.RailSanctum);
            _lobby.SelectRail(99);
            AssertExactlyOneActive("SelectRail(99) must clamp to the last entry",
                LobbyView.RailMap);

            //    The toggle clamps too, and proving it needs a live entry that
            //    is NOT the clamp target — otherwise a clamped re-click closes
            //    for the legitimate reason and the case says nothing about the
            //    clamp. 출정 is live for both probes below; the clamp lands on
            //    성소 and 지도.
            _lobby.SelectRail(LobbyView.RailSortie);
            _lobby.ToggleRail(-1);
            AssertExactlyOneActive("ToggleRail(-1) from 출정 must clamp to 성소, not blank",
                LobbyView.RailSanctum);

            _lobby.SelectRail(LobbyView.RailSortie);
            _lobby.ToggleRail(99);
            AssertExactlyOneActive("ToggleRail(99) from 출정 must clamp to 지도, not blank",
                LobbyView.RailMap);

            // 6. Leaving and re-entering the lobby. Hide resets the rail, and
            //    D-12 changed WHAT it resets to (closed) without changing
            //    whether it resets at all. A rail that remembered 성소 across a
            //    run would reopen a meta panel over the diorama on the way back
            //    from a dungeon — the selection is a decision about this visit.
            //
            //    This is also the one transition that can reach a BLANK lobby
            //    without touching either rail entry: Hide deactivates the ROOT,
            //    so if it were ever rewritten to hide the three panels
            //    individually, Show would restore a lobby with nothing on it —
            //    and, unlike case 4's close, with the rail still claiming a
            //    destination. That is why the assertion reads the index and not
            //    just the panel count.
            _lobby.SelectRail(LobbyView.RailSanctum);
            _lobby.Hide();
            Assert.That(_lobby.SelectedRailForTest, Is.EqualTo(LobbyView.RailClosed),
                "leaving the lobby must return the rail to closed (D-12): a remembered 성소 "
                + "reopens a meta panel over the diorama for every player coming back from a run");

            _lobby.Show();
            AssertNoPanelActive("after Hide() then Show()");

            // And the restored lobby is closed, not stuck: the rail still works.
            _lobby.ToggleRail(LobbyView.RailSortie);
            AssertExactlyOneActive("ToggleRail(출정) after Hide/Show", LobbyView.RailSortie);
        }

        // =====================================================================
        // TEST 4 — the rail's own touch floor, at the bands that are real.
        // =====================================================================

        /// <summary>
        /// The three rail icons clear 44 CSS px on BOTH axes at both band
        /// worsts. 103.3 u is not a round number for a reason: it is 44 px at
        /// the D-11 support floor, and the rail is the only route to two of the
        /// three panels, so an unreachable rail entry is an unreachable panel.
        ///
        /// MEASURED AT THE WORST OF EACH BAND, NOT AT A CONVENIENT SCALE — the
        /// third lie of this cycle. Pick 1280x853 (1.0884 px/u) and 103.3 u
        /// reports 112.4 px, clearing the floor by 155%; the same rail is 44.0
        /// px one band over. A single-scale rail audit is an audit of the
        /// scale, not of the rail.
        ///
        ///   375x667  band B  0.4383 px/u  ->  45.28 px   margin  1.28 px
        ///   501 CSS  band A  0.4261 px/u  ->  44.02 px   margin  0.02 px
        ///
        /// THE LETTERBOX MARGIN IS 0.02 px. Not slack — arithmetic. 103.3 u was
        /// chosen so this row lands exactly on the floor, so any shrink at all
        /// reddens this case first and reddens nothing else: a rail 1 u smaller
        /// still clears 375, still contains, still clears the panels. This row
        /// is the entire defence of the rail's size.
        ///
        /// MUTATION: RailIcon 103.3 -> 90.2 (the number the interview spec
        /// carried, derived from band B's 0.488 px/u — a band the shipped
        /// template does not produce for a supported phone). RED here at both
        /// rows: 38.44 px at the letterbox floor, 39.54 px at 375. GREEN in
        /// tests 1, 2, 3 and 5 — smaller icons pull PanelLeft LEFT, so the panels
        /// stay contained and stay clear of the rail. This test is the only one
        /// that notices.
        /// </summary>
        [Test]
        public void RailIcons_ClearTheTouchFloor_AtTheSupportFloorViewport()
        {
            var bands = new (int CssW, int CssH, float Ew, float Eh, string Why)[]
            {
                (375, 667, 855.5f, 1521.6f, "support floor (D-11): iPhone SE2, band B worst in range"),
                (501, 334, 1175.8f, 783.8f, "letterbox floor: band A worst, where 103.3 u == 44.0 px"),
            };

            var measured = 0;
            foreach (var band in bands)
            {
                BuildLobbyAt(band.CssW, band.CssH, band.Ew, band.Eh);

                // Derived from the round-tripped effective width rather than
                // pasted in, so the scale can never disagree with the canvas
                // the icons were just measured on (§4i).
                var cssPerUnit = band.CssW / _lobby.LastEffectiveWidth;

                for (var rail = 0; rail < 3; rail++)
                {
                    var rect = _lobby.RailRectForTest(rail);
                    Assert.That(rect, Is.Not.Null, $"rail entry {rail} must exist");

                    // A rail icon that lost its Button is a dead destination
                    // however large it is, and LabelOf reads the FIRST Text
                    // child — the same read every label-keyed sweep in
                    // LobbyLayoutTests performs. One Text per rail button is
                    // load-bearing there, so it is asserted here.
                    var button = rect.GetComponent<Button>();
                    Assert.That(button, Is.Not.Null,
                        $"rail entry {rail} must be clickable — it is the only route to its panel");
                    var label = LabelOf(button);
                    Assert.That(label, Is.Not.Empty,
                        $"rail entry {rail} reports no label, so it drops out of every "
                        + "label-keyed audit in LobbyLayoutTests");

                    var world = WorldRect(rect);
                    var w = world.width * cssPerUnit;
                    var h = world.height * cssPerUnit;
                    TestContext.WriteLine($"[rail floor] {band.CssW}x{band.CssH} "
                        + $"{cssPerUnit:F4} px/u  {label,-4} {w,6:F2} x {h,6:F2} CSS px");

                    Assert.That(w, Is.GreaterThanOrEqualTo(MinCssPx),
                        $"rail icon {label} is {w:F2} CSS px wide at {band.CssW}x{band.CssH} "
                        + $"({cssPerUnit:F4} px/u — {band.Why}), under the {MinCssPx} px floor");
                    Assert.That(h, Is.GreaterThanOrEqualTo(MinCssPx),
                        $"rail icon {label} is {h:F2} CSS px tall at {band.CssW}x{band.CssH} "
                        + $"({cssPerUnit:F4} px/u — {band.Why}), under the {MinCssPx} px floor");
                    measured += 1;
                }
            }

            Assert.That(measured, Is.EqualTo(bands.Length * 3),
                $"only {measured} of {bands.Length * 3} rail icons were measured");

            // 320 CSS (iPhone SE, band B, 0.3738 px/u) renders the rail at
            // 38.6 px. That is under the floor and it is NOT a defect: D-11
            // named 375x667 as the support floor and put 320 explicitly out of
            // range. Buying 320 costs 117.7 u, which makes the navigation rail
            // 28% larger than the 강하 button it navigates to and inverts the
            // hierarchy on every supported tier for a viewport where a 392 u
            // panel already eats a third of the screen.
            //
            // Measured and printed, never asserted. A number nobody claims does
            // not gate the build, but an unrecorded number is one nobody can
            // re-decide later — this is the debt, in the gate artifact, priced.
            BuildLobbyAt(320, 568, 856.1f, 1519.6f);
            var outOfRangeScale = 320f / _lobby.LastEffectiveWidth;
            for (var rail = 0; rail < 3; rail++)
            {
                var rect = _lobby.RailRectForTest(rail);
                var world = WorldRect(rect);
                TestContext.WriteLine($"[rail floor — OUT OF SUPPORT RANGE, not asserted] "
                    + $"320x568 {outOfRangeScale:F4} px/u  "
                    + $"{LabelOf(rect.GetComponent<Button>()),-4} "
                    + $"{world.width * outOfRangeScale,6:F2} x "
                    + $"{world.height * outOfRangeScale,6:F2} CSS px  (floor {MinCssPx})");
            }
        }

        // =====================================================================
        // TEST 5 — the derivation that makes the rail safe.
        // =====================================================================

        /// <summary>
        /// The rail and the open panel never overlap. Nine pairs (3 panels x 3
        /// icons) at two viewports.
        ///
        /// Structurally this cannot fail today: PanelLeft is DERIVED —
        /// RailLeft + RailIcon + RailGap = 16 + 103.3 + 12 = 131.3 — so the
        /// panels start where the rail ends by construction, and a rail resize
        /// moves them together. Catching the moment that derivation is cut is
        /// the entire job of this test. It is a cheap guard on a one-line
        /// refactor: the second someone writes `const float PanelLeft = 131.3f`
        /// the two numbers are free to drift, and the rail draws OVER the
        /// panels (BuildRail runs last in LobbyView.Build), so the drift hides
        /// the panel's own content rather than looking broken.
        ///
        /// MUTATION: decouple PanelLeft and grow RailIcon to 130 (a plausible
        /// "make the icons bigger" edit). Rail xMax becomes 146.0 against a
        /// panel starting at 131.3 — 14.7 u of overlap, RED at both viewports.
        /// Tests 1 to 4 stay GREEN: the panels never moved so they are still
        /// contained, and bigger icons clear the touch floor by more.
        ///
        /// THE Y-SHARE PREMISE IS NOT DECORATION. "These two rects do not
        /// overlap" is satisfied by a rail that drifted vertically off its own
        /// band just as well as by the 12 u gutter, and a test that accepts
        /// both proves neither. Each pair is required to share a y band first,
        /// so the only thing left that can be separating them is the x gutter
        /// this test is about. All nine pairs share 89.4-103.3 u of y at both
        /// viewports (rail entries occupy -72..-405.9 below the canvas top; the
        /// shortest panel, MAP, runs -72..-392).
        /// </summary>
        [Test]
        public void RailAndSelectedPanel_DoNotOverlap()
        {
            var cases = new (int CssW, int CssH, float Ew, float Eh, string Why)[]
            {
                (1280, 853, 1176.0f, 783.7f, "deploy frame"),
                ( 390, 844,  798.7f, 1728.6f, "phone portrait"),
            };

            var pairs = 0;
            foreach (var c in cases)
            {
                var canvas = BuildLobbyAt(c.CssW, c.CssH, c.Ew, c.Eh);

                for (var rail = 0; rail < 3; rail++)
                {
                    _lobby.SelectRail(rail);
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)canvas.transform);

                    var (name, panelRect) = PanelFor(rail);
                    Assert.That(panelRect.gameObject.activeInHierarchy, Is.True,
                        $"{c.CssW}x{c.CssH}: SelectRail({rail}) must open {name} before its "
                        + "clearance from the rail means anything");
                    var panel = WorldRect(panelRect);

                    for (var icon = 0; icon < 3; icon++)
                    {
                        var railRect = WorldRect(_lobby.RailRectForTest(icon));
                        var at = $"{c.CssW}x{c.CssH} ({c.Why}), {name} vs rail {icon}";

                        // Premise: they are in the same horizontal band, so the
                        // separation below has to be the gutter.
                        var shareY = Mathf.Min(panel.yMax, railRect.yMax)
                                   - Mathf.Max(panel.yMin, railRect.yMin);
                        Assert.That(shareY, Is.GreaterThan(Epsilon),
                            $"{at}: they share only {shareY:F1} u of y, so the horizontal "
                            + "clearance asserted next is being proved by a vertical gap "
                            + "instead — the rail or the panel left its band");

                        Assert.That(railRect.xMax, Is.LessThanOrEqualTo(panel.xMin + Epsilon),
                            $"{at}: the rail {Describe(railRect)} runs into {name} "
                            + $"{Describe(panel)} by {railRect.xMax - panel.xMin:F1} u. PanelLeft is "
                            + "derived from RailLeft + RailIcon + RailGap so that this cannot "
                            + "happen; if it did, that derivation was cut");
                        pairs += 1;
                    }
                }
            }

            Assert.That(pairs, Is.EqualTo(cases.Length * 9),
                $"only {pairs} of {cases.Length * 9} rail/panel pairs were compared");
        }

        // ------------------------------------------------------------ helpers --

        /// <summary>
        /// A lobby laid out for one viewport, on a canvas sized to the frame
        /// the product actually draws there.
        ///
        /// REBUILT PER CALL, host GameObject and all. LobbyView.Build has no
        /// re-entry guard: calling it twice on one instance leaves two "Lobby"
        /// canvases and two MetaScreenViews under the object, and
        /// GetComponentInChildren returns the FIRST canvas while
        /// SortieRectForTest points into the SECOND. The canvas that gets
        /// resized and the panels that get measured would then belong to
        /// different builds — a containment answer computed against a canvas
        /// the panels are not on. The assert below closes that door for good
        /// rather than relying on this comment.
        /// </summary>
        private Canvas BuildLobbyAt(int cssW, int cssH, float eW, float eH)
        {
            if (_lobbyObject != null) Object.DestroyImmediate(_lobbyObject);
            _lobbyObject = new GameObject("LobbyContainmentTests");
            _lobby = _lobbyObject.AddComponent<LobbyView>();

            var data = new CampaignData
            {
                PrologueDone = true,
                ClearedMask = 1,
                Roster = new string[0],
                Active = string.Empty,
            };
            _lobby.Build(data, default);
            _lobby.Refresh(data);
            _lobby.ApplyLobbyLayoutForTest(cssW, cssH);

            // Round-trip the scaler coupling instead of describing it. Every E
            // in the viewport table was derived from the 1280x720 reference; if
            // that reference moves, each case quietly starts auditing a
            // different frame than its label names and the whole table becomes
            // decorative. LobbyLayoutTests does this for the same reason, in
            // LobbyPanels_NeverOverlap_AtAnyEffectiveWidth.
            Assert.That(_lobby.LastEffectiveWidth, Is.EqualTo(eW).Within(1f),
                $"{cssW}x{cssH} resolved to an effective width of "
                + $"{_lobby.LastEffectiveWidth:F2}, not the {eW:F1} this case is built on. The "
                + "scaler's reference resolution moved, so every viewport in this file now "
                + "measures a different point than its label claims");

            // The height half of the same coupling, and the axis that matters
            // most here: the shipped defect was panels running off the BOTTOM,
            // so a mistyped E_h would hand this file a canvas taller than the
            // real one and every containment answer would be generous in
            // exactly the direction that shipped. E_h is not independent —
            // it is cssH / (cssW / E_w) — so it can be checked, not trusted.
            var derivedHeight = cssH / (cssW / _lobby.LastEffectiveWidth);
            Assert.That(derivedHeight, Is.EqualTo(eH).Within(1f),
                $"{cssW}x{cssH} at E_w {_lobby.LastEffectiveWidth:F1} implies an effective "
                + $"HEIGHT of {derivedHeight:F1} u, not the {eH:F1} this case passes to the "
                + "canvas. The case would audit containment against a canvas the product "
                + "never draws, on the very axis the shipped defect used");

            var canvas = _lobbyObject.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null, "the lobby must build its canvas");

            // The resized canvas must be the one the panels actually hang from.
            // Without this, a stale-build mix-up (see the summary above) reads
            // as a perfectly plausible containment result.
            //
            // Routed through PanelFor rather than the three properties direct,
            // because every lobby panel GameObject is literally named "Panel"
            // (LobbyView.Panel) — a message quoting rect.name could not say
            // WHICH one drifted. It also puts the rail-index -> panel mapping
            // that both containment tests trust through a check on every build
            // instead of only where it is used.
            for (var rail = 0; rail < 3; rail++)
            {
                var (name, rect) = PanelFor(rail);
                Assert.That(rect, Is.Not.Null, $"{name} must resolve a rect");
                Assert.That(rect.GetComponentInParent<Canvas>(true), Is.SameAs(canvas),
                    $"{name} hangs off a different canvas than the one being sized, so "
                    + "containment would be measured against a frame this panel is not on");
            }

            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = canvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(eW, eH);
            canvasRect.localScale = Vector3.one;
            canvasRect.position = Vector3.zero;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);

            // And the canvas must have TAKEN that size. A canvas that refused
            // the sizeDelta leaves every rect below measured against the wrong
            // frame, which is a lie in whichever direction the refusal points.
            Assert.That(canvasRect.rect.width, Is.EqualTo(eW).Within(0.01f),
                $"{cssW}x{cssH}: canvas failed to take the effective width {eW:F1}");
            Assert.That(canvasRect.rect.height, Is.EqualTo(eH).Within(0.01f),
                $"{cssW}x{cssH}: canvas failed to take the effective height {eH:F1}");
            return canvas;
        }

        /// <summary>The panel a rail index owns, with the word the failure
        /// message should use.</summary>
        private (string Name, RectTransform Rect) PanelFor(int rail)
        {
            if (rail == LobbyView.RailSanctum) return ("SANCTUM", _lobby.SanctumRectForTest);
            if (rail == LobbyView.RailSortie) return ("SORTIE", _lobby.SortieRectForTest);
            if (rail == LobbyView.RailMap) return ("MAP", _lobby.MapPanelRectForTest);
            throw new System.ArgumentOutOfRangeException(nameof(rail), rail, "rail index is 0..2");
        }

        /// <summary>
        /// Exactly one panel active, and it is the one <paramref name="rail"/>
        /// names. The count and the identity are asserted separately: a
        /// three-way swap keeps the count at one while opening the wrong door,
        /// and "one panel is open" is not the claim the rail makes.
        /// </summary>
        private void AssertExactlyOneActive(string at, int rail)
        {
            var live = new List<string>();
            if (_lobby.SanctumRectForTest.gameObject.activeInHierarchy) live.Add("SANCTUM");
            if (_lobby.SortieRectForTest.gameObject.activeInHierarchy) live.Add("SORTIE");
            if (_lobby.MapPanelRectForTest.gameObject.activeInHierarchy) live.Add("MAP");

            Assert.That(live.Count, Is.EqualTo(1),
                $"{at}: {live.Count} panels active [{string.Join(", ", live)}], expected exactly 1. "
                + "Zero here means the panel this case was about to measure is not on screen — "
                + "legal as a deliberate close (D-12), never as the result of opening one — and "
                + "two is the pre-rail stack with its y offsets removed");
            Assert.That(live[0], Is.EqualTo(PanelFor(rail).Name),
                $"{at}: rail {rail} opened {live[0]}");
            Assert.That(_lobby.SelectedRailForTest, Is.EqualTo(rail),
                $"{at}: the rail reports entry {_lobby.SelectedRailForTest} as selected while "
                + $"{live[0]} is the panel on screen");
        }

        /// <summary>
        /// No panel active AND the rail agrees. Both halves, because the two
        /// zero-panel states are indistinguishable by panel count alone: a
        /// lobby closed on purpose reports RailClosed, and a lobby blanked by
        /// a bug reports a destination it is not drawing. The second one is
        /// the failure the "exactly one" rule used to catch outright, so the
        /// index read is what carries that coverage forward under D-12.
        /// </summary>
        private void AssertNoPanelActive(string at)
        {
            var live = new List<string>();
            if (_lobby.SanctumRectForTest.gameObject.activeInHierarchy) live.Add("SANCTUM");
            if (_lobby.SortieRectForTest.gameObject.activeInHierarchy) live.Add("SORTIE");
            if (_lobby.MapPanelRectForTest.gameObject.activeInHierarchy) live.Add("MAP");

            Assert.That(live, Is.Empty,
                $"{at}: expected a closed lobby, found [{string.Join(", ", live)}] on screen");
            Assert.That(_lobby.SelectedRailForTest, Is.EqualTo(LobbyView.RailClosed),
                $"{at}: nothing is drawn but the rail reports entry "
                + $"{_lobby.SelectedRailForTest} as selected. That is a BLANK lobby, not a closed "
                + "one: the icon is lit over a panel the player cannot see");
        }

        /// <summary>
        /// The failure text for a crossed canvas edge. Subject-neutral on
        /// purpose — both containment tests call it, and a message hardcoded to
        /// "panel" would misreport a rail icon that fell off the bottom as a
        /// panel defect, sending the next reader to the wrong constant.
        /// </summary>
        private static string Breach(string at, string subject, string edge, float overrun,
                                     Rect rect, Rect canvas)
            => $"{at}: {subject} crosses the canvas {edge} edge by {overrun:F1} u. "
             + $"{subject} {Describe(rect)} vs canvas {Describe(canvas)}. "
             + "There is no root ScrollRect in the lobby, so nothing off the canvas can be "
             + "scrolled to: whatever it holds is at 0% for every player at this viewport";

        private static string Describe(Rect rect)
            => $"[{rect.xMin:F1}..{rect.xMax:F1} x {rect.yMin:F1}..{rect.yMax:F1}]";

        /// <summary>
        /// The button's first Text child — the same read every label-keyed
        /// sweep in LobbyLayoutTests performs, which is why the rail keeps one
        /// Text per button. Null-tolerant: a rail entry that lost its Button is
        /// a real defect, and it should arrive as the assertion that names it
        /// rather than as an NRE from inside a message-formatting helper.
        /// </summary>
        private static string LabelOf(Button button)
        {
            if (button == null) return "(no Button)";
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
    }
}
