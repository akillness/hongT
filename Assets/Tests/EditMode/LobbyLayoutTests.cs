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
