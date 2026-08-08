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
        // The one placement every lobby panel takes, at every effective width
        // (LobbyView.PinPanel). Spelled out rather than read back from the view:
        // PanelLeft is private, and a test that sourced the number from the code
        // under test would assert only that the code equals itself.
        //   PinnedLeft = RailLeft 16 + RailIcon 103.3 + RailGap 12
        //   PinnedTop  = -PanelTop
        private const float PinnedLeft = 131.3f;
        private const float PinnedTop = 72f;
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
        /// (SIM_SPEC_HACKSLASH §9: 버튼 최소 44px), swept over every state the
        /// lobby can hide a control behind: 3 rail destinations x 4 accordion
        /// folds.
        ///
        /// READ THIS BEFORE DIFFING THE TABLE ACROSS CYCLE-8. The eleven entries
        /// below did not change. What they MEAN changed completely, and a reader
        /// comparing the two revisions will see an identical table and conclude
        /// the rail did nothing here.
        ///
        /// Pre-rail, this audit filtered on activeInHierarchy — and the old
        /// layout never DEACTIVATED anything. It stacked all three panels into a
        /// single 1604 u column and parked the ones it could not fit off the
        /// bottom of a 783.7 u canvas: SANCTUM at y-708 (13.5% visible), MAP at
        /// y-1284 (0%). Active, laid out, measurable, and unreachable. So this
        /// table has spent two cycles reporting four Sanctum tabs and three 성장
        /// "+" buttons as AUDITED when no player could touch one of them — the
        /// tabs sat below the fold, and the tab content holding the "+" buttons
        /// starts 116 u further down still (LobbyView.TabContent), which is 40 u
        /// past the bottom edge of what was visible.
        ///
        /// That is failure mode #2 — the name lied — living inside the debt
        /// table, which is the worst place for it: a debt table's entire job is
        /// to be the artifact you trust about coverage. "Audited" meant "had a
        /// rect", and nothing in the assertion said otherwise.
        ///
        /// After the rail, those eleven entries mean what they always claimed.
        /// The radio deactivates two panels outright, so the sweep below has to
        /// OPEN each destination to measure it — and every control it now reports
        /// is one the player can actually reach by picking an icon. The number did
        /// not move; the truth of it did.
        ///
        /// Which is also why every row is old debt EXPOSED rather than new debt
        /// CREATED. The rail added three controls to the lobby and none of them
        /// land here (103.3 u square = 50.4 CSS px at this basis, and sized from
        /// the 0.4261 letterbox band on purpose — LobbyContainmentTests asserts
        /// that at the real bands). Nothing in this table was authored this cycle.
        ///
        /// AMENDMENT #8 — sweep every fold state, not just the default one.
        ///
        /// The accordion deactivates the folded groups' cards, and this audit
        /// only measures active buttons. Left as a single-state sweep it
        /// silently STOPPED measuring the tier row and all five 수련 buttons:
        /// 26 audited controls became 13, and a size regression inside a
        /// folded group would have passed unnoticed. Folding is a UI win and
        /// an audit hole at the same time, and the hole is closed here by
        /// opening each group in turn and taking the union.
        ///
        /// CYCLE-8 — the rail is the same shape, one level up. It is the third
        /// time this file has met "a state UI removes controls from the audit,
        /// so the audit has to walk the states" (§4d): Sanctum tabs in cycle-4,
        /// the accordion in AMENDMENT #8, the rail now. Left unswept, the radio
        /// would have cut this table from eleven entries to four — the four fold
        /// headers — and that shrinkage reads as an accessibility WIN in a diff.
        /// </summary>
        [Test]
        public void InteractiveLobbyRects_HoldTheMeasuredTouchFloorDebt()
        {
            var canvas = BuildClearedLobby();
            var canvasRect = canvas.GetComponent<RectTransform>();
            var report = new StringBuilder();
            var undersized = new List<string>();
            var measured = 0;
            var passes = 0;

            // Found once: FoldHeaders looks up inactive objects too, so the list
            // is the same in every rail state. Re-finding it per state would
            // assert the accordion exists three times and measure nothing extra.
            var headers = FoldHeaders(canvas);

            // Dedup by INSTANCE, not by path. The first draft of this sweep
            // keyed on Path(transform) and silently lost controls: every card
            // in a group is a "Panel" under a "Panel", so three 강하 buttons
            // share one path string and two of them vanished from the audit.
            // A dedup key that is not unique per object turns a widened audit
            // into a narrowed one — which is the exact failure this sweep was
            // added to fix, reintroduced one layer down. The rail states join the
            // same union: a control on screen in two states is one control.
            var seen = new HashSet<Button>();
            for (var rail = 0; rail < 3; rail++)
            {
                _lobby.SelectRail(rail);
                Assert.That(_lobby.SelectedRailForTest, Is.EqualTo(rail),
                    $"the rail refused destination {rail}, so this pass would measure "
                    + "whichever panel happened to be open instead");

                // Folds are swept inside every rail state rather than only inside
                // 출정. They only reveal controls while the sortie panel is drawn
                // today, so two thirds of this is redundant — deliberately. The
                // coupling "folds live in the sortie panel" is a fact about the
                // current layout, not a contract, and an audit that assumes it
                // goes quiet the moment a fold moves.
                foreach (var group in headers)
                {
                    group.onClick.Invoke();
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
                    passes += 1;

                    foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                    {
                        if (!button.gameObject.activeInHierarchy) continue;
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
                        report.AppendLine($"  {LabelOf(button),-18} {w,5:F1} x {h,5:F1} CSS px   "
                            + $"[rail {rail}] {path}");
                    }
                }
            }
            _lobby.SelectRail(LobbyView.RailSortie);   // back to this fixture's baseline

            TestContext.WriteLine($"[lobby touch-floor audit @390x844 portrait, "
                + $"{SpecCssPerUnit} CSS px/u, floor {MinCssPx}, union over 3 rail "
                + $"destinations x {ProgressionGuide.GroupCount} fold states, "
                + $"{measured} controls]\n" + report);

            Assert.That(passes, Is.EqualTo(3 * ProgressionGuide.GroupCount),
                $"the sweep ran {passes} state(s), not the {3 * ProgressionGuide.GroupCount} the "
                + "table's meaning depends on. A table built from fewer states than it claims is "
                + "the exact narrowing this sweep exists to prevent");

            // Deliberately smaller than the AMENDMENT #8 ratchet: main FIXED the
            // primary sortie controls (강하 · 서약 · 견습/숙련/판결 · 수련) so they
            // clear the floor, and a fixed control does not belong in a debt
            // table. Re-adding them after the merge would re-register debt that
            // no longer exists and hide the next real violation behind noise.
            //
            // Every row below is PRE-EXISTING debt, and the rail exposed it
            // rather than authoring it. Per row, with the arithmetic:
            //
            //  성장·장비·군단·각인  91 x 44 u  = 44.4 x 21.5 px  (LobbyView.cs:1772)
            //      The Sanctum tab strip. HEIGHT fails; width clears by 0.4 px.
            //      EXPOSED, and this is the pair the docstring above is about:
            //      pre-rail these were measured while sitting below the fold of
            //      a 783.7 u canvas. Same numbers, first time they describe a
            //      control a player can reach.
            //
            //  +  (x3)              52 x 44 u  = 25.4 x 21.5 px  (LobbyView.cs:1829)
            //      성장 tab stat allocators, the worst controls in the lobby on
            //      BOTH axes. EXPOSED — and worse than the tabs were, because
            //      TabContent starts 116 u below the panel top, so pre-rail these
            //      were 40 u past the visible edge at 0%.
            //
            //  제1부 기록 · 제2부 증언 · 제3부 집행 · 훈련장
            //                      368 x 44 u  = 179.6 x 21.5 px  (LobbyView.cs:1411)
            //      The four accordion fold headers. HEIGHT fails, same 21.5 class
            //      as the tab strip. PRE-EXISTING (AMENDMENT #8), and the only row
            //      whose numbers moved this cycle — width only, never the failing
            //      axis:
            //          pre-rail   panel 766.7 u -> header 742.7 u -> 362.4 px
            //          post-rail  panel   392 u -> header   368 u -> 179.6 px
            //      LobbyView.cs:1400 has claimed 179.6 since AMENDMENT #8, which
            //      was WRONG when written: it described the native 392 u panel
            //      while the tier being measured had stretched it to 766.7. The
            //      pinned layout makes that comment true for the first time. Do
            //      not "correct" it to 362.4 — checked cycle-8, both numbers are
            //      recorded here precisely so the agreement is not re-derived as
            //      suspicious.
            //
            // What is NOT here, and why each absence is a real claim:
            //  · The three rail icons. 103.3 x 103.3 u = 50.4 px, clearing the
            //    floor at this basis. One appearing below is an implementation
            //    defect, not a table update. NOTE the gap this table cannot see:
            //    RailIcon 90.2 (the interview spec's number) renders 44.02 px
            //    here and would stay green while breaking the D-11 support floor
            //    at 38.4 px. LobbyContainmentTests measures the rail at the real
            //    0.4383 and 0.4261 bands for exactly that reason.
            //  · 전체 지도 / 정비. 196 x 96 u = 95.6 x 46.8 px. Newly REACHABLE
            //    this cycle (0% visible before the rail) and they clear at this
            //    basis — but 96 u is 42.1 px at 0.4383 and 40.9 px at 0.4261, so
            //    they clear the floor this file asserts and MISS the one D-11
            //    named. Tracked on the D-11 debt ledger with a cycle-9 expiry
            //    (director, cycle-8) rather than added here, so it survives in a
            //    ledger instead of only as a comment.
            //  · Every sortie action. 92 x 92 u after ApplySortieTouchLayout,
            //    which is now unconditional (LobbyView.cs:1056). 강하 is 44.9 px
            //    here and 39.2 px at 0.4261 — already D-11 carried debt, same
            //    cycle-9 expiry.
            //
            // WHY THIS FILE IS NOT RE-BASED TO 0.4261, decided cycle-8. Exactly
            // two rows flip verdict between 0.488 and the D-11 floors: the map
            // actions and 강하, both listed above and both now on the ledger.
            // Re-basing would add them to the frozen table inside the one diff
            // where a reader needs to see what the rail did — the same trade this
            // file already refused for the Sanctum tabs (see the UNCLOSED note
            // below), so it gets the same answer. The basis moves in its own
            // cycle, with the table churn visible as the whole change.
            //
            // KNOWN, DELIBERATELY UNCLOSED: the SANCTUM tabs hide controls the
            // same way the folds and the rail do, and this sweep does not cycle
            // them. Only the selected tab's contents are measured — today 성장,
            // which is why exactly three stat "+" appear above and the
            // equip/legion/sigil rows do not. Cycling them would pull ~20
            // untouched controls into the frozen table, several undersized (the
            // sigil face pairs at 68 x 30 u), and registering that much unrelated
            // debt inside another cycle makes the next diff unreadable about which
            // change caused what. Measured and recorded, not silently ignored:
            // same designer+pm touch-floor item. Note the shape — this is now the
            // FOURTH nested state machine in one lobby (rail > tab > fold > row),
            // and the tab layer is the one still unswept.
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
                + "record it in _workspace/current/pm/negotiation-record.md. A control leaving "
                + "because a STATE stopped being swept is neither: check the rail/fold traversal "
                + "above before believing a shrinkage.\nMeasured:\n" + report);
        }

        /// <summary>
        /// The nineteen route actions, positively measured against the floor —
        /// not left to the debt table's absence check, because "not in the debt
        /// table" is also what a control that stopped being swept looks like.
        ///
        /// The count assertion at the bottom is the real defence. Every one of
        /// these lives in the SORTIE panel, and the rail draws at most one
        /// panel, so a sweep that trusted the entry state would measure only
        /// whatever happened to be open. It no longer trusts it: the loop walks
        /// all three rail destinations explicitly.
        ///
        /// THAT PRECAUTION IS NOW LOAD-BEARING RATHER THAN DEFENSIVE. It was
        /// written while 출정 was the default and the argument was that a
        /// one-line D-A5 decision could reasonably change. It did — D-12 made
        /// the default CLOSED, so there is no panel open at build time at all
        /// and a single-state sweep here would now measure zero controls and
        /// fail on the count rather than silently shrink. The loop is what
        /// keeps this audit independent of the entry state in either direction.
        /// </summary>
        [Test]
        public void PrimarySortieActions_ClearThe44CssPxTouchFloor()
        {
            var canvas = BuildClearedLobby();
            var routePrefixes = new[] { "재훈련", "강하", "서약", "견습", "숙련", "판결", "수련" };

            // None of these seven collide with the rail's 성소 / 출정 / 지도, which
            // is checked rather than assumed — the rail is built LAST
            // (LobbyView.cs:341), so a colliding prefix would match a rail icon
            // and quietly add a 20th "route action" that is not one.
            foreach (var prefix in routePrefixes)
                foreach (var railLabel in new[] { "성소", "출정", "지도" })
                    Assert.That(
                        prefix.StartsWith(railLabel, System.StringComparison.Ordinal)
                        || railLabel.StartsWith(prefix, System.StringComparison.Ordinal), Is.False,
                        $"route prefix '{prefix}' collides with rail label '{railLabel}'. Label "
                        + "lookups cannot tell them apart, so the count below would be measuring "
                        + "a navigation icon as a sortie action");

            // The accordion hides four groups out of five, so a single sweep sees
            // 5 of these 19 controls and calls the audit complete. §4d: a state UI
            // removes controls from the audit, so the audit has to walk the
            // states. Cycle-4 learned this on the Sanctum tabs; the accordion is
            // the same shape one panel over; the cycle-8 rail is the same shape
            // one level up again, and it is the reason for the outer loop.
            //
            // Deduped by INSTANCE, not by path: sibling cards share
            // `Group0/Body/Panel/Panel`, so a path-keyed set collapses nine
            // descents into one and the sweep meant to widen the audit narrows
            // it instead (also §4d, also learned the hard way).
            var audited = new HashSet<Button>();
            var headers = FoldHeaders(canvas);
            for (var rail = 0; rail < 3; rail++)
            {
                _lobby.SelectRail(rail);
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
                            $"{label} is narrower than the phone touch floor (rail {rail}, fold {open})");
                        Assert.That(world.height * SpecCssPerUnit, Is.GreaterThanOrEqualTo(MinCssPx),
                            $"{label} is shorter than the phone touch floor (rail {rail}, fold {open})");
                    }
                }
            }
            _lobby.SelectRail(LobbyView.RailSortie);   // back to this fixture's baseline

            Assert.That(audited.Count, Is.EqualTo(StageCatalog.Entries.Count + 1 + 1
                + HackSpec.TrainingTiers + TrainingTrials.Ids.Length),
                "the audit must cover prologue, every descent, revealed pact, all tier "
                + "choices and trials — across every rail destination and every fold state, "
                + "since no single one shows them all. A SHORTFALL here is the interesting "
                + "failure: it means a route action moved somewhere this sweep cannot see, "
                + "which reads as 'no violations' rather than as missing coverage");
        }

        /// <summary>
        /// D-3: the sortie actions are 92x92 u at EVERY effective width, because
        /// ApplySortieTouchLayout takes a constant `true` (LobbyView.cs:1056).
        ///
        /// WRITTEN AFTER THE FACT, and that is the point of the docstring. The
        /// suite was 805/805 green when a mutation sweep found this:
        ///
        ///   M-I:  ApplySortieTouchLayout(true)
        ///      -> ApplySortieTouchLayout(effectiveWidth &lt; 900f)
        ///
        /// 805/805 still passed. Nothing in the suite detected it. Measured:
        ///
        ///   390x844   E  798.7   &lt;900 true    92x92 u    44.9 CSS px   PASS
        ///   1280x853  E 1176.0   &lt;900 false   84x28 u    30.5 CSS px   FAIL
        ///   501x334   E 1176.0   &lt;900 false   84x28 u    11.9 CSS px   FAIL
        ///
        /// Every touch-floor test in this file builds at 390x844, the one
        /// viewport where both arms of that branch look fine. The deploy frame —
        /// what every player actually gets — renders 강하 at 30.5 px, 69% of the
        /// floor, and nothing looked there. That is the third failure mode this
        /// file's own docstrings name (a pass measured at the most flattering
        /// scale available), still live, inside the file that documents it.
        ///
        /// Worth saying plainly: D-3 was UNANIMOUS, and that is why it had no
        /// test. The debt table, which is contested and re-derived every cycle,
        /// was correct. The decision everyone agreed on was the undefended one.
        ///
        /// ASSERTED IN UNITS, NOT CSS PX, and the usual reason given for this is
        /// not quite the real one. A px assertion at the deploy frame does fail
        /// today — 28 u renders 30.5 px, under the 44 floor. The actual problem
        /// is that a px assertion only constrains the action to be at least
        /// 44 / 1.0884 = 40.4 u, so the desktop arm could come back as 84x44 —
        /// 47.9 px at this frame, floor cleared, test green — while D-3's 92x92
        /// is still gone and the letterbox band renders it at 18.7 px. The unit
        /// size IS the contract; px is downstream of it and downstream of
        /// whichever scale you happened to pick.
        ///
        /// FOUR FRAMES, chosen so no threshold can hide between them. The
        /// template pins E_w at ~1176 for every band-A viewport, so the only
        /// distinct values a width predicate can see are the two phone widths and
        /// 1176. A threshold anywhere in (855.5, 1176] splits this set and goes
        /// red; one above 1176 or below 798.7 is constant across every shipping
        /// viewport, which is not a branch, it is dead code with a comparison in
        /// it.
        ///
        /// Measures the RECTS the layout pass wrote, not what is on screen — the
        /// two rail states that hide the sortie panel do not change what
        /// ApplySortieTouchLayout assigned, and this test is about the assignment.
        /// Reachability is CampaignMapActions' and LobbyContainmentTests' subject;
        /// keeping them apart is why "active" and "drawn" stopped being confused
        /// in this file.
        /// </summary>
        [Test]
        public void SortieActions_HoldTheirTouchGeometry_AtEveryShippingFrame()
        {
            // Label prefix -> the size ApplySortieTouchLayout pins, and how many
            // controls carry it. Prologue is the odd one: 112 wide because it is
            // the only single-action card, 92 tall like everything else.
            var expected = new (string Prefix, float W, float H, int Count)[]
            {
                ("재훈련", 112f, 92f, 1),
                ("강하",    92f, 92f, StageCatalog.Entries.Count),
                ("서약",    92f, 92f, StageCatalog.Entries.Count),
                ("견습",    92f, 92f, 1),
                ("숙련",    92f, 92f, 1),
                ("판결",    92f, 92f, 1),
                ("수련",    92f, 92f, TrainingTrials.Ids.Length),
            };

            var frames = new (int W, int H, float E, string Why)[]
            {
                (1280, 853, 1176.0f, "the deploy frame — every player, every window size"),
                (501,  334, 1175.8f, "the 501 CSS letterbox floor, worst px/u in band A"),
                (375,  667, 855.5f,  "iPhone SE2, the D-11 support floor"),
                (390,  844, 798.7f,  "the phone width every other touch test builds at"),
            };

            var canvas = BuildClearedLobby();
            var report = new StringBuilder();
            var wrong = new List<string>();

            foreach (var f in frames)
            {
                Relayout(canvas, f.W, f.H, f.E, f.Why);
                var at = $"{f.W}x{f.H} (E={_lobby.LastEffectiveWidth:F1})";

                foreach (var e in expected)
                {
                    var found = 0;
                    foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                    {
                        if (!LabelOf(button).StartsWith(e.Prefix, System.StringComparison.Ordinal))
                            continue;
                        found++;
                        var size = button.GetComponent<RectTransform>().rect.size;
                        if (Mathf.Abs(size.x - e.W) > 0.5f || Mathf.Abs(size.y - e.H) > 0.5f)
                            wrong.Add($"  {at}: {e.Prefix} is {size.x:F1}x{size.y:F1} u, "
                                + $"not the pinned {e.W}x{e.H}");
                    }

                    // A prefix that stops matching would empty the sweep and leave
                    // `wrong` empty too — the same silent-narrowing shape the rail
                    // traversal exists to prevent, one test over.
                    Assert.That(found, Is.EqualTo(e.Count),
                        $"{at}: found {found} '{e.Prefix}' control(s), expected {e.Count}. "
                        + "The sweep is looking for a label that moved, so it is measuring "
                        + "nothing and would report no violations");
                }

                var descent = FindButton(canvas, "강하").GetComponent<RectTransform>().rect.size;
                report.AppendLine($"  {at,-24} 강하 {descent.x,5:F1} x {descent.y,5:F1} u");
            }

            TestContext.WriteLine("[sortie action geometry, D-3 constant across frames]\n" + report);

            Assert.That(wrong, Is.Empty,
                $"{wrong.Count} sortie action(s) are not at their pinned touch size. "
                + "ApplySortieTouchLayout takes a constant `true` (D-3) — if something "
                + "reintroduced a width predicate, the desktop arm is the one that has NEVER "
                + "been correct in a shipping configuration: 84x28 u renders 30.5 CSS px at "
                + "the deploy frame and 11.9 px at the letterbox floor, against a 44 px floor. "
                + "The branch had one usable arm and this is it.\n" + string.Join("\n", wrong));
        }

        /// <summary>
        /// D-8: the rail carries EXACTLY three destinations. Not "at least".
        ///
        /// WRITTEN AFTER A MUTATION SWEEP found this undefended: adding a fourth
        /// rail entry left the suite 806/806 green. Nothing counted them.
        ///
        /// READ THE REASON BEFORE DELETING THIS. A bare Is.EqualTo(3) looks
        /// arbitrary, and an arbitrary-looking assertion between a developer and
        /// a feature they have been asked to ship loses. The three is not a
        /// layout budget:
        ///
        /// The studio's answer to Darkest Dungeon 2's Altar of Hope — a screen
        /// added to answer "there is no progression" that became the source of
        /// "grindfest" complaints. The failure was not the screen's contents. It
        /// was that a permanent, top-level entry point turns an optional system
        /// into a chore the player feels obliged to visit every run. A rail icon
        /// is precisely that kind of entry point, which is why the rail is a
        /// closed set and not a menu.
        ///
        /// We have already done the small version of this once: v1.5 made 각인 a
        /// fourth SANCTUM tab inside the same 400 u strip, and the tabs have
        /// carried a 21.5 CSS px height debt ever since (see the frozen table
        /// above — 성장·장비·군단·각인, four entries, one per tab). That is what
        /// "just one more" costs when the container does not grow.
        ///
        /// A fourth icon arrives as a reasonable-looking PR. That is the whole
        /// threat model. So the count is asserted, and the arbitration is quoted
        /// here rather than left in a decision log nobody opens mid-review.
        ///
        /// If a fourth destination is genuinely right, this test is not the
        /// obstacle — the arbitration is. Reopen it, then change the 3.
        ///
        /// Three separate things are counted, because they can drift apart and
        /// the interesting failures are the ones where they do:
        ///   · icons built (a 4th icon nothing can select is a dead control),
        ///   · panels the rail switches between,
        ///   · the reachable range of BOTH rail entries — SelectRail and
        ///     ToggleRail each clamp to 0..2, so a 4th icon is unreachable
        ///     rather than broken, and unreachable is the quieter bug. Both,
        ///     because the buttons call ToggleRail and the sweeps call
        ///     SelectRail: a clamp holding on only one of them holds only for
        ///     the caller that is not the player.
        /// </summary>
        [Test]
        public void Rail_CarriesExactlyThreeDestinations()
        {
            const int Destinations = 3;
            var canvas = BuildClearedLobby();

            // Counted from the hierarchy, not from RailRectForTest: that accessor
            // clamps to 0..2 (LobbyView.cs:517), so asking it how many icons
            // exist can only ever answer three. A count that cannot return four
            // cannot detect a fourth.
            var icons = new List<string>();
            foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                if (button.gameObject.name.StartsWith("Rail", System.StringComparison.Ordinal))
                    icons.Add($"{button.gameObject.name} '{LabelOf(button)}'");

            TestContext.WriteLine($"[rail destinations] {string.Join(", ", icons)}");

            Assert.That(icons.Count, Is.EqualTo(Destinations),
                $"the rail has {icons.Count} icons, not {Destinations}: "
                + $"{string.Join(", ", icons)}. See this test's docstring before changing "
                + "the number — the count is an arbitration outcome (D-8, Altar of Hope), "
                + "not a layout budget. A fourth top-level entry point is how an optional "
                + "system becomes a chore, and this project already paid the small version "
                + "of that bill when 각인 became a fourth SANCTUM tab");

            // Every icon must reach a distinct panel, and there must be no panel
            // without an icon. Either direction failing is a dead end: an
            // unreachable panel, or an icon that opens nothing.
            var panels = new HashSet<RectTransform>
            {
                _lobby.SanctumRectForTest, _lobby.SortieRectForTest, _lobby.MapPanelRectForTest,
            };
            Assert.That(panels.Count, Is.EqualTo(Destinations),
                "three icons must switch between three DISTINCT panels; two icons resolving "
                + "to the same rect is a destination that does not exist");

            // The reachable range. Both entries clamp to 0..2, so a fourth icon
            // would be built, drawn, dimmed, and permanently unselectable — the
            // count above catches the icon, this catches the clamp drifting out
            // of step with it in either direction.
            for (var i = 0; i < Destinations; i++)
            {
                _lobby.SelectRail(i);
                Assert.That(_lobby.SelectedRailForTest, Is.EqualTo(i),
                    $"destination {i} is not selectable, so its icon is decoration");
            }

            // Probed on the SETTER, and the live entry is parked away from the
            // clamp target first. Through the toggle, SelectRail(3) clamps to 2
            // — which is already live after the loop — so a correct clamp would
            // CLOSE the panel (D-12) and report RailClosed, and this case would
            // read a working clamp as a broken one.
            _lobby.SelectRail(LobbyView.RailSanctum);
            _lobby.SelectRail(Destinations);
            Assert.That(_lobby.SelectedRailForTest, Is.EqualTo(Destinations - 1),
                $"SelectRail({Destinations}) resolved to {_lobby.SelectedRailForTest} rather than "
                + $"clamping to {Destinations - 1}. If the clamp grew, a fourth destination is "
                + "already reachable in code and the icon count above is the only thing still "
                + "holding D-8");

            // The CLICK path clamps too, and it is the one a fourth icon would
            // actually be wired to (BuildRail passes its loop index straight
            // into ToggleRail). Same parking trick, and 성소 is chosen because
            // it is neither the clamp target nor the live entry.
            _lobby.SelectRail(LobbyView.RailSanctum);
            _lobby.ToggleRail(Destinations);
            Assert.That(_lobby.SelectedRailForTest, Is.EqualTo(Destinations - 1),
                $"ToggleRail({Destinations}) resolved to {_lobby.SelectedRailForTest} rather than "
                + $"clamping to {Destinations - 1}. The rail's buttons call ToggleRail, so a clamp "
                + "that only holds on the setter holds nowhere the player can reach");

            _lobby.SelectRail(LobbyView.RailSortie);
        }

        /// <summary>
        /// D-7: the 성소 badge lights whenever any SANCTUM tab dot is lit, and
        /// goes dark when none is.
        ///
        /// WRITTEN AFTER A MUTATION SWEEP found this undefended: forcing the rail
        /// badge permanently off left the suite 806/806 green.
        ///
        /// This badge is not decoration and it is not a nicety — it is the ENTIRE
        /// compensation for a trade this cycle made. Before the rail, SANCTUM was
        /// permanently on screen, so "there is something you can afford in there"
        /// was answered by the panel simply being visible. The rail hid it behind
        /// a click. The arbitration that allowed that priced meta surfaces in
        /// CLICKS rather than in exposed pixels, and it accepted the extra click
        /// only because the badge keeps announcing the door is open.
        ///
        /// So a silently-dark badge is not a cosmetic regression. It is the spend
        /// UI going quiet one click away with nothing saying so — a smaller,
        /// slower version of the 0%-reachability defect this whole cycle exists
        /// to fix, and harder to notice because the panel is fine once you get
        /// there.
        ///
        /// MIRRORS, NEVER RE-DERIVES. The assertion compares the rail dot against
        /// the four tab dots as BUILT — not against ProgressionGuide.Badges. A
        /// test that recomputed the rule would agree with any bug the rule
        /// contains, and worse, would keep passing while the rail and the strip
        /// inside it disagreed. The contract is that the rail cannot claim a door
        /// the strip says is shut, or stay dark over one the strip says is open;
        /// that is a statement about two pieces of UI, so both are read.
        ///
        /// BOTH DIRECTIONS ARE REQUIRED, AND THEY ALTERNATE. A dark-only sweep
        /// passes trivially against a badge nailed shut, which is exactly the
        /// mutation. A lit-only sweep passes against one nailed open, which is
        /// worse than useless — a permanent badge is an unread badge. Each of the
        /// four spend surfaces gets its own lit state so no single one can carry
        /// the others, and every lit state is followed by a dark one.
        ///
        /// DRIVEN THROUGH Refresh ON ONE LOBBY, not by rebuilding per state. The
        /// badge line lives in Refresh (LobbyView.cs:757) and Refresh is what runs
        /// when a player SPENDS — the moment the badge has to go out. A sweep of
        /// freshly-built lobbies would only ever check the badge's initial value
        /// and would pass against a badge that lights correctly once and then
        /// never updates again. Sequencing dark -> lit -> dark through the real
        /// path tests the transition, which is the thing that breaks.
        /// </summary>
        [Test]
        public void SanctumRailBadge_MirrorsTheTabDots_InBothDirections()
        {
            // (name, data, expect lit). One state per independently-badgeable
            // surface: the four cannot be collapsed, because a rule that lit on
            // Points alone would pass a sweep that only ever varies Relics.
            // Interleaved with the empty save so every light is switched OFF
            // again before the next one is switched on.
            var dark = Spend(points: 0, relics: 0);
            var states = new (string Why, CampaignData Data, bool Lit)[]
            {
                ("nothing affordable", dark, false),
                ("성장 — an unspent point", Spend(points: 1, relics: 0), true),
                ("spent it", dark, false),
                ("장비 — 2 relics buys T0->T1", Spend(points: 0, relics: 2), true),
                ("spent it", dark, false),
                ("군단 — a benched companion", Spend(points: 0, relics: 0, roster: true), true),
                ("dismissed it", dark, false),
                ("각인 — 12 relics, equipment capped",
                    Spend(points: 0, relics: ProgressionGuide.SigilCost, equipCapped: true), true),
                ("spent it", dark, false),
            };

            var canvas = BuildClearedLobby();
            foreach (var s in states)
            {
                _lobby.Refresh(s.Data);
                var railBadge = BadgeUnder(_lobby.RailRectForTest(LobbyView.RailSanctum));
                Assert.That(railBadge, Is.Not.Null,
                    "the 성소 icon must carry a badge object (LobbyView.BuildRail) — without "
                    + "one there is nothing to light and every assertion here is vacuous");

                // The strip's own dots, read rather than recomputed.
                var tabDots = new List<string>();
                var anyTabLit = false;
                var tabNames = new[] { "성장", "장비", "군단", "각인" };
                foreach (var name in tabNames)
                {
                    var tab = FindButton(canvas, name);
                    Assert.That(tab, Is.Not.Null, $"the sanctum strip must expose {name}");
                    var dot = BadgeUnder((RectTransform)tab.transform);
                    Assert.That(dot, Is.Not.Null, $"tab {name} must carry a badge object");
                    if (dot.activeSelf) { anyTabLit = true; tabDots.Add(name); }
                }

                TestContext.WriteLine($"[rail badge] {s.Why,-38} rail "
                    + $"{(railBadge.activeSelf ? "LIT " : "dark")}  strip "
                    + $"[{(tabDots.Count == 0 ? "none" : string.Join(" ", tabDots))}]");

                Assert.That(anyTabLit, Is.EqualTo(s.Lit),
                    $"'{s.Why}' was built to make the strip {(s.Lit ? "light" : "stay dark")} and "
                    + $"it did not. The STATE is wrong, not the rail — every assertion below is "
                    + "measuring something other than what this case is named for");

                Assert.That(railBadge.activeSelf, Is.EqualTo(anyTabLit),
                    $"'{s.Why}': the 성소 badge is "
                    + $"{(railBadge.activeSelf ? "lit" : "dark")} while the strip inside shows "
                    + $"[{(tabDots.Count == 0 ? "none" : string.Join(" ", tabDots))}]. The rail "
                    + "dot is the OR of the tab dots (LobbyView.cs:757) and it is the only thing "
                    + "announcing an affordable purchase now that SANCTUM is a click away — a "
                    + "dark badge over an open door is the spend UI going silent, and a lit one "
                    + "over a shut door is a badge nobody will read twice");
            }
        }

        /// <summary>The 7 u ember dot a rail icon or sanctum tab carries. Found
        /// by name under the control, because neither has a test seam and adding
        /// one to production for this would be a wider change than the test.</summary>
        private static GameObject BadgeUnder(RectTransform control)
        {
            if (control == null) return null;
            for (var i = 0; i < control.childCount; i++)
            {
                var child = control.GetChild(i);
                if (string.Equals(child.name, "Badge", System.StringComparison.Ordinal))
                    return child.gameObject;
            }
            return null;
        }

        /// <summary>
        /// A save that makes exactly one spend surface affordable.
        /// <paramref name="equipCapped"/> maxes every equipment slot, which is
        /// what isolates 각인: equipment and sigils share the relic pool and the
        /// rule lights only the CHEAPEST of them, so 12 relics with a 2-relic
        /// equipment step available badges 장비, never 각인.
        /// </summary>
        private static CampaignData Spend(int points, int relics,
                                          bool roster = false, bool equipCapped = false)
        {
            var cap = ProgressionGuide.EquipCap;
            return new CampaignData
            {
                PrologueDone = true,
                ClearedMask = 1,
                Points = points,
                Relics = relics,
                Weapon = equipCapped ? cap : 0,
                Lantern = equipCapped ? cap : 0,
                Cloak = equipCapped ? cap : 0,
                Roster = roster ? new[] { "ember-cohort" } : new string[0],
                Active = string.Empty,
                ActiveSlots = new string[0],
            };
        }

        /// <summary>
        /// THE CONTRACT (cycle-8, D-2/D-3/D-10): panel placement does not vary
        /// with effective width. At all.
        ///
        /// Three panels, one pinned position: top-left anchored at
        /// (PanelLeft 131.3, PanelTop -72) at their native sizes — SORTIE
        /// 392x620, SANCTUM 400x560, MAP 424x320 (LobbyView.PinPanel, :1065).
        /// 131.3 is not a chosen number: it is RailLeft 16 + RailIcon 103.3 +
        /// RailGap 12, so the panels start exactly where the rail ends.
        ///
        /// This sweep is measured as INSETS FROM THE CANVAS EDGES, never as raw
        /// world x. A pinned panel and an edge-anchored one are indistinguishable
        /// at any single width; they differ only in how they MOVE. Inset from the
        /// left edge is constant for the first and varies for the second, which
        /// is the entire difference between the current layout and the one that
        /// shipped the defect.
        ///
        /// The first case's insets and sizes are captured and every later case is
        /// compared against them. That comparison — not the absolute values — is
        /// what catches a resurrected tier branch: a reintroduced threshold is
        /// perfectly legal geometry on both sides and only shows up as a
        /// DIFFERENCE across the width axis.
        ///
        /// Inactive panels are measured too, deliberately. The rail leaves
        /// exactly one active (LobbyView.SelectRail) and SetActive does not touch
        /// a RectTransform, so all three placements are live values whether or not
        /// they are drawn — and the two that are not drawn today are exactly the
        /// ones a future edit would break without noticing.
        ///
        /// NOT ASSERTED HERE, owned by LobbyContainmentTests: whether the selected
        /// panel FITS the canvas, the visible-fraction math, exactly-one-panel-
        /// active as a subject rather than a premise, and the full rail-vs-panel
        /// pair audit. This file answers "does placement move with width"; that
        /// file answers "is what is placed actually on screen". Two questions, and
        /// the shipped defect needed both to be asked.
        ///
        /// ------------------------------------------------------------------
        /// WHAT THIS TEST DEFENDED IN THE OLD LAYOUT, and why the 16 widths below
        /// are kept verbatim. All past tense: none of these mutations can be
        /// constructed any more, because the rail draws one panel and the tier
        /// branch is gone. The record stays because the width list is the
        /// expensive part and deleting the reasoning is how it gets trimmed.
        ///
        /// The three lobby panels had to not overlap at ANY effective width.
        ///
        /// Found in the browser after the origin/main merge: the campaign map was
        /// drawn over the sortie panel, burying the prologue card and the first
        /// two act rows. The arithmetic was why —
        ///
        ///   sanctum  16 .. 416              (left-anchored, 400 wide)
        ///   map      432 .. 856             (left-anchored at a CONSTANT)
        ///   sortie   (W-408) .. (W-16)      (RIGHT-anchored, 392 wide)
        ///
        /// sortie's left edge tracked the viewport and the map's did not, so they
        /// collided for every W below 1264. The stack threshold was 850, so the
        /// whole 850..1264 band shipped broken: 248 u of overlap at W=1000, and
        /// 88 u at the 1176 u a 1280 CSS px browser window actually produces
        /// (buffer 1351x900, dpr 1.25).
        ///
        /// Nothing caught it because this file audited 390x844 (stacked) and
        /// 1280x720 — the reference width, and the one non-stacked width where the
        /// constant happened to be right. Right and wrong coincided at both sampled
        /// points (§4m); the defect lived entirely between them.
        ///
        /// THREE THINGS THAT TEST HAD TO GET RIGHT, each of which an earlier draft
        /// of it got wrong. Recorded because every one of them made the test LIE
        /// rather than fail — and (1) and (2) still bind the sweep below:
        ///
        /// 1. The canvas is resized PER WIDTH, from LastEffectiveWidth. sanctum
        ///    and map were left-anchored while sortie was RIGHT-anchored, so
        ///    sortie's world x came from the canvas edge. Leaving the canvas at the
        ///    phone's 799 u while laying out for 1280 measured a frame the product
        ///    never draws — and it reported ~350 u of map-over-sortie on CORRECT
        ///    code. STILL BINDING: the insets below are differences against the
        ///    canvas edges, so a stale canvas moves every one of them.
        ///
        /// 2. The sweep drives SCREEN widths; the threshold was an EFFECTIVE width.
        ///    Different numbers: 1176x720 has an effective width of 1226.9, so a
        ///    case labelled "the browser's 1176" would not be testing 1176 at all.
        ///    Every landscape case here uses h = round(921600/w), which puts the
        ///    scaler's log-lerp at scale 1 and therefore E == w within 0.4 u. The
        ///    width list IS the effective-width list, so the conflation cannot come
        ///    back by editing. The two exceptions come from real devices and carry
        ///    their measured E instead of a pretence: the phone (798.7) and the
        ///    browser buffer 1351x900 (1176.2). STILL BINDING, and still asserted.
        ///
        /// 3. The two mutations fired in DISJOINT bands, and the second was 16 u
        ///    wide. A sweep had to land inside it on purpose:
        ///
        ///      M1 (threshold 850)  RED for E in [850, 1248)   — 398 u wide
        ///      M2 (map x = 432)    RED for E in [1248, 1264)  —  16 u wide
        ///
        ///    M2 needed side-by-side (E >= 1248) AND the constant still past the
        ///    sortie edge (856 > E - 408, i.e. E < 1264). Sixteen units out of the
        ///    entire width axis. That narrowness was the answer to "how did an audit
        ///    at 390 and 1280 miss this", and it is why the 1248..1263 rows below
        ///    are STILL load-bearing: they are the tightest sample anyone has ever
        ///    had to buy on this axis, and a resurrected threshold has no reason to
        ///    pick a wider band than the last one did.
        ///
        /// The two counters that used to prove "both arrangements were exercised",
        /// and the tightest-gutter assert that proved the sweep reached inside the
        /// 16 u window, are GONE — not relaxed. There is one arrangement now, so
        /// both were unsatisfiable, and an unsatisfiable assertion is a red test
        /// that teaches nothing. The width rows they protected are what survived.
        ///
        /// Also gone: the "deliberately NOT asserted — whether the stacked column
        /// FITS" note. That question stopped being deferred. It was the shipped
        /// defect (1604 u column against a 783.7 u canvas, MAP at 0%), and it is
        /// now LobbyContainmentTests' subject rather than a comment.
        /// </summary>
        [Test]
        public void LobbyPanels_HoldTheirPinnedGeometry_AtAnyEffectiveWidth()
        {
            // (screen w, screen h, expected effective width, why this row exists).
            // Kept verbatim from the overlap sweep. The labels describe the old
            // mutation bands because that is what bought the rows; under a pinned
            // layout every row asks the same question, and the point is that the
            // ANSWER must not vary across a set chosen to make it vary.
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
            var report = new StringBuilder();
            var drift = new List<string>();
            // Keyed by panel NAME, not by index: a swap of two panels' geometry
            // would otherwise compare each against the other's baseline and
            // report no drift at all.
            var baseline = new Dictionary<string, (float Left, float Top, float W, float H)>();
            string baselineAt = null;

            // One lobby, re-laid at each width: ApplyLobbyLayoutForTest forces the
            // pass, which is the path a real resize takes. Rebuilding per width
            // would test construction instead of reflow, and reflow is where a
            // width-dependent branch would live.
            foreach (var c in cases)
            {
                var canvasRect = Relayout(canvas, c.W, c.H, c.E, c.Why);
                var frame = WorldRect(canvasRect);
                var at = $"{c.W}x{c.H} (E={_lobby.LastEffectiveWidth:F1})";

                // The rail is the origin of PanelLeft, so it is measured, not
                // assumed. Its own right edge is the number the panels must clear.
                var railRight = float.NegativeInfinity;
                for (var r = 0; r < 3; r++)
                {
                    var railRect = _lobby.RailRectForTest(r);
                    Assert.That(railRect, Is.Not.Null, $"{at}: the rail must build icon {r}");
                    railRight = Mathf.Max(railRight, WorldRect(railRect).xMax);
                }

                var panels = new (string Name, RectTransform Rect, float W, float H)[]
                {
                    ("sortie",  _lobby.SortieRectForTest,   392f, 620f),
                    ("sanctum", _lobby.SanctumRectForTest,  400f, 560f),
                    ("map",     _lobby.MapPanelRectForTest, 424f, 320f),
                };

                foreach (var p in panels)
                {
                    Assert.That(p.Rect, Is.Not.Null, $"{at}: the lobby must build its {p.Name} panel");
                    var world = WorldRect(p.Rect);
                    Assert.That(world.width > 0f && world.height > 0f, Is.True,
                        $"{at}: {p.Name} resolved to a degenerate rect "
                        + $"[{world.width:F1} x {world.height:F1}], so placement cannot be measured");

                    // Insets, not world coordinates — see the note above. Under the
                    // old right-anchored sortie, left grew with every width.
                    var left = world.xMin - frame.xMin;
                    var top = frame.yMax - world.yMax;
                    report.AppendLine($"  {at,-22} {p.Name,-8} left {left,7:F1}  top {top,6:F1}  "
                        + $"{world.width,6:F1} x {world.height,6:F1}");

                    Assert.That(left, Is.EqualTo(PinnedLeft).Within(0.5f),
                        $"{at}: {p.Name} starts {left:F1} u from the canvas edge, not the pinned "
                        + $"{PinnedLeft} (RailLeft 16 + RailIcon 103.3 + RailGap 12). A panel that "
                        + "tracks an edge instead of the rail is the anchor grammar the cycle-8 "
                        + "defect was made of");
                    Assert.That(top, Is.EqualTo(PinnedTop).Within(0.5f),
                        $"{at}: {p.Name} starts {top:F1} u below the canvas top, not {PinnedTop}");
                    Assert.That(world.width, Is.EqualTo(p.W).Within(0.5f),
                        $"{at}: {p.Name} is {world.width:F1} u wide, not its native {p.W}. Its "
                        + "contents are authored to the native width, so a stretched panel gains "
                        + "an empty band rather than a wider layout (LobbyView.cs:1034)");
                    Assert.That(world.height, Is.EqualTo(p.H).Within(0.5f),
                        $"{at}: {p.Name} is {world.height:F1} u tall, not its native {p.H}");

                    // Premise, one number. The 3x3 rail-vs-panel pair audit is
                    // LobbyContainmentTests' — this exists so a rail collision
                    // fails HERE too, where the width sweep can name the width.
                    Assert.That(world.xMin, Is.GreaterThanOrEqualTo(railRight - OverlapEpsilon),
                        $"{at}: {p.Name} starts at {world.xMin:F1}, inside the rail column that "
                        + $"ends at {railRight:F1}. PanelLeft is derived from RailIcon precisely so "
                        + "a rail resize moves the panels with it");

                    if (baselineAt == null)
                    {
                        baseline[p.Name] = (left, top, world.width, world.height);
                        continue;
                    }

                    // The width-invariance claim itself. Absolute values above can
                    // all be individually correct while the layout still branches;
                    // only a comparison ACROSS widths can see a branch.
                    var b = baseline[p.Name];
                    if (Mathf.Abs(b.Left - left) > 0.5f || Mathf.Abs(b.Top - top) > 0.5f
                        || Mathf.Abs(b.W - world.width) > 0.5f || Mathf.Abs(b.H - world.height) > 0.5f)
                        drift.Add($"  {p.Name}: {baselineAt} gave left {b.Left:F1} top {b.Top:F1} "
                            + $"{b.W:F1}x{b.H:F1}, {at} gives left {left:F1} top {top:F1} "
                            + $"{world.width:F1}x{world.height:F1}");
                }

                if (baselineAt == null) baselineAt = at;
            }

            TestContext.WriteLine($"[lobby panel placement over {cases.Length} effective widths, "
                + $"insets from the canvas edges]\n{report}");

            Assert.That(drift, Is.Empty,
                $"{drift.Count} panel placement(s) changed with the effective width. The rail draws "
                + "exactly one panel, so no width can need a different arrangement — a difference "
                + "here means a tier branch is back. That branch is what shipped the defect: "
                + "SideBySideFloor was 1248 while build-webgl/index.html pins E_w at ~1176 for "
                + "every player at every window size, so the fallback column was not an edge case, "
                + $"it was the deployed state.\n{string.Join("\n", drift)}");
        }

        /// <summary>
        /// Re-lays the lobby for a screen size and resizes the WorldSpace canvas
        /// to the frame the product actually draws at that viewport.
        ///
        /// Both halves matter and neither is optional. The layout pass without the
        /// canvas resize measures a frame that is never drawn; the canvas resize
        /// without the round-trip assert silently audits a different width than the
        /// caller named, the moment the scaler's reference resolution moves.
        /// </summary>
        private RectTransform Relayout(Canvas canvas, int w, int h, float expectedE, string why)
        {
            _lobby.ApplyLobbyLayoutForTest(w, h);

            // Round-trip the scaler coupling rather than describing it (§4i):
            // every expected E is derived from the 1280x720 reference, so a
            // reference change must fail loudly instead of quietly re-pointing the
            // sweep at widths other than the ones it names.
            Assert.That(_lobby.LastEffectiveWidth, Is.EqualTo(expectedE).Within(1f),
                $"{w}x{h} resolved to an effective width of {_lobby.LastEffectiveWidth:F2}, not "
                + $"the {expectedE:F1} this case is built on ({why}). The scaler's reference "
                + "resolution moved, so this case now audits a different point than its label claims");

            var effective = _lobby.LastEffectiveWidth;
            var canvasRect = (RectTransform)canvas.transform;
            canvasRect.sizeDelta = new Vector2(effective, effective * h / w);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
            Assert.That(canvasRect.rect.width, Is.EqualTo(effective).Within(0.01f),
                $"{w}x{h}: canvas failed to take the effective width {effective:F2}");
            return canvasRect;
        }


        /// <summary>
        /// The map panel's two actions must be DRAWN INSIDE THE CANVAS, not
        /// merely active and correctly sized.
        ///
        /// This test was green for two cycles while the map was at 0% visible.
        /// It asserted `activeInHierarchy` plus two rect sizes and called the
        /// result "AreReachable" — and every one of those three assertions was
        /// true of a panel parked 1284 u down a 1604 u column against a 783.7 u
        /// canvas. Active is not on screen. A rect has a size wherever it is.
        /// The word "Reachable" in the old name was the only part of the test
        /// that made the claim, and no assertion under it measured that claim.
        ///
        /// A test whose NAME lies is worse than no test: it reads as coverage,
        /// so nobody writes the test that would have caught this. That is the
        /// second of the three ways this file has been wrong (the first was
        /// auditing 16 width samples with zero y samples; the third was picking
        /// the most flattering scale). Containment is now measured, so the name
        /// and the assertions say the same thing.
        ///
        /// TWO FRAMES, because one of them is the one that shipped:
        ///   · 1280x853 -> E 1176.0 x 783.7. What EVERY player gets: the WebGL
        ///     template locks the canvas to aspect 1280:853, so this frame is
        ///     produced at every window size (build-webgl/index.html:18-20).
        ///     Under the old column the map lived at -1284, i.e. 500 u below
        ///     this canvas's own floor — the deployed 0%.
        ///   · 375x667 -> the iPhone SE2 support floor named in D-11. Portrait,
        ///     so the scaler resolves a different aspect and containment has to
        ///     be re-proved rather than inherited from the landscape frame.
        ///
        /// Looked up by GameObject NAME, not by label, and that is load-bearing
        /// rather than fastidious. The rail owns the words 성소 / 출정 / 지도
        /// (LobbyView.RailLabels), and the map panel's own button was renamed
        /// 지도 -> "전체 지도" in this same cycle. FindButton(canvas, "지도") now
        /// prefix-matches the RAIL ICON and returns it first — 103.3 u square,
        /// active, clearing 44 px on both axes. Every assertion below would pass
        /// against the wrong control, on a lobby where the real map action had
        /// been deleted. Names come from LobbyView.cs:391 and :395.
        /// </summary>
        [Test]
        public void CampaignMapActions_AreDrawnInsideTheCanvas_AndClearTheTouchFloor()
        {
            var canvas = BuildClearedLobby();

            // Looked up by object NAME; the label is asserted separately below.
            var expectedLabels = new Dictionary<string, string>
            {
                { "OpenMapButton", "전체 지도" },
                { "MetaScreenButton", "정비" },
            };

            // (screen w, screen h, expected E, why).
            var frames = new (int W, int H, float E, string Why)[]
            {
                (1280, 853, 1176.0f, "the deploy frame — every player, every window size"),
                (375,  667, 855.5f,  "iPhone SE2, the D-11 support floor"),
            };

            foreach (var f in frames)
            {
                var canvasRect = Relayout(canvas, f.W, f.H, f.E, f.Why);
                // The map panel is a rail destination now, so it is inactive until
                // its icon is picked. Selecting it is part of the route being
                // asserted: "reachable" means reachable BY THE PLAYER'S ROUTE.
                _lobby.SelectRail(LobbyView.RailMap);
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);

                Assert.That(_lobby.SelectedRailForTest, Is.EqualTo(LobbyView.RailMap),
                    "the rail must actually be on 지도 — every assertion below is about "
                    + "the panel that selection opens");

                var frame = WorldRect(canvasRect);
                foreach (var name in new[] { "OpenMapButton", "MetaScreenButton" })
                {
                    var button = FindButtonByName(canvas, name);
                    Assert.That(button, Is.Not.Null,
                        $"the map panel must expose {name} (LobbyView.BuildMapPanel)");
                    var at = $"{f.W}x{f.H} [{f.Why}] {name} '{LabelOf(button)}'";

                    // The label as its OWN assertion, not as the lookup key. The
                    // lookup is by object name so a reword cannot silently
                    // re-point it at some other control; this line is what makes
                    // a reword fail loudly instead of passing unnoticed. Split
                    // deliberately: one of these is about identity and the other
                    // is about copy, and folding them together is how the old
                    // FindButton("지도") ended up able to audit the rail icon.
                    Assert.That(LabelOf(button), Is.EqualTo(expectedLabels[name]),
                        $"{at}: the map action's wording changed. If that is intended, update "
                        + "this expectation — but check every prefix lookup in this file first: "
                        + "the rail owns 성소 / 출정 / 지도 and is built LAST (LobbyView.cs:341), "
                        + "so a label that collides with one of those resolves to the rail icon");

                    Assert.That(button.gameObject.activeInHierarchy, Is.True,
                        $"{at}: picking 지도 must actually draw the panel's actions");

                    var world = WorldRect(button.GetComponent<RectTransform>());
                    var visible = Intersect(world, frame);
                    var fraction = world.width * world.height <= 0f
                        ? 0f
                        : visible.width * visible.height / (world.width * world.height);

                    TestContext.WriteLine($"[map action] {at}  rect "
                        + $"[{world.xMin:F1}..{world.xMax:F1} x {world.yMin:F1}..{world.yMax:F1}]  "
                        + $"canvas [{frame.xMin:F1}..{frame.xMax:F1} x "
                        + $"{frame.yMin:F1}..{frame.yMax:F1}]  {fraction * 100f:F1}% visible");

                    // The assertion the old name promised and never made.
                    Assert.That(fraction, Is.EqualTo(1f).Within(0.001f),
                        $"{at} is only {fraction * 100f:F1}% inside the canvas. It is the sole "
                        + "route to the meta screen, so a partly-drawn one is a partly-dead end. "
                        + "Note what this does NOT prove on its own: LobbyContainmentTests holds "
                        + "the panel-level containment, and a panel can be inside the canvas while "
                        + "its controls hang out of the panel. Both halves are needed");

                    Assert.That(world.width * SpecCssPerUnit, Is.GreaterThanOrEqualTo(MinCssPx),
                        $"{at} is narrower than the touch floor");
                    Assert.That(world.height * SpecCssPerUnit, Is.GreaterThanOrEqualTo(MinCssPx),
                        $"{at} is shorter than the touch floor");

                    // MEASURED, NOT ASSERTED — third failure mode, recorded so the
                    // next reader does not have to rediscover it. The floor above
                    // is checked at this file's frozen 0.488 px/u basis (390x844
                    // fill band). D-11 named two WORSE real bands: 0.4383 at the
                    // 375x667 support floor and 0.4261 at the 501 CSS letterbox
                    // floor. The 96 u action height renders 42.1 px and 40.9 px
                    // there — UNDER 44 on both. Not asserted here because
                    // re-basing this file's constant moves every row of the frozen
                    // debt table at once, which is a cycle-scale decision and not
                    // this test's to make. Registered, not silently dropped.
                    var atSupportFloor = world.height * 0.4383f;
                    var atLetterbox = world.height * 0.4261f;
                    if (atSupportFloor < MinCssPx || atLetterbox < MinCssPx)
                        TestContext.WriteLine($"  D-11 SHORTFALL (recorded, not asserted): "
                            + $"{world.height:F1} u renders {atSupportFloor:F1} px at the 375x667 "
                            + $"support floor and {atLetterbox:F1} px at the 501 CSS letterbox "
                            + $"floor, against a {MinCssPx} px floor. This file asserts at "
                            + $"{SpecCssPerUnit} px/u, which is no longer the worst real band");
                }
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

            // The lobby OPENS CLOSED since D-12 — no panel drawn, the diorama
            // on screen. Almost every case in this file is about a control
            // inside 출정, so the fixture opens it and says so rather than
            // letting each case discover that its button is inactive.
            //
            // SelectRail, never ToggleRail: this is "put the rail on 출정", and
            // the toggle answers a different question when 출정 is already the
            // live entry. A fixture that closed the panel it was asked to open
            // would fail every case downstream with a message about geometry.
            //
            // Cases that are ABOUT the closed default (the rail's own
            // invariants) live in LobbyContainmentTests, which builds its own
            // lobby and does not go through here.
            _lobby.SelectRail(LobbyView.RailSortie);

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

        /// <summary>
        /// Label-prefix lookup. SHARP EDGE, read before adding a caller: since
        /// cycle-8 the rail owns the literal strings 성소 / 출정 / 지도
        /// (LobbyView.RailLabels), and it is built LAST, so a prefix that
        /// collides with one of those three can still return a panel control —
        /// or the rail icon — depending on hierarchy order. Both are active and
        /// both clear the touch floor, so a collision does not fail, it silently
        /// audits the wrong control. Prefer FindButtonByName for anything the
        /// rail could shadow.
        /// </summary>
        private static Button FindButton(Canvas canvas, string labelPrefix)
        {
            foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                if (LabelOf(button).StartsWith(labelPrefix, System.StringComparison.Ordinal))
                    return button;
            return null;
        }

        /// <summary>
        /// Lookup by GameObject name. Immune to the rail's label collision above,
        /// and to a label being reworded — a rename of the OBJECT is a structural
        /// change a test should notice, while a rename of the WORD is a design
        /// decision it should not.
        /// </summary>
        private static Button FindButtonByName(Canvas canvas, string name)
        {
            foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                if (string.Equals(button.gameObject.name, name, System.StringComparison.Ordinal))
                    return button;
            return null;
        }

        /// <summary>Overlap of two world rects; zero-sized when disjoint.</summary>
        private static Rect Intersect(Rect a, Rect b)
        {
            var xMin = Mathf.Max(a.xMin, b.xMin);
            var yMin = Mathf.Max(a.yMin, b.yMin);
            var xMax = Mathf.Min(a.xMax, b.xMax);
            var yMax = Mathf.Min(a.yMax, b.yMax);
            return xMax <= xMin || yMax <= yMin
                ? new Rect(0f, 0f, 0f, 0f)
                : Rect.MinMaxRect(xMin, yMin, xMax, yMax);
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
