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
            foreach (var button in canvas.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeInHierarchy) continue;
                measured += 1;
                var world = WorldRect(button.GetComponent<RectTransform>());
                var w = world.width * SpecCssPerUnit;
                var h = world.height * SpecCssPerUnit;
                Assert.That(world.width > 0f && world.height > 0f, Is.True,
                    $"degenerate rect (layout did not resolve): {Path(button.transform)}");
                if (w >= MinCssPx && h >= MinCssPx) continue;
                undersized.Add(LabelOf(button));
                report.AppendLine($"  {LabelOf(button),-6} {w,5:F1} x {h,5:F1} CSS px   {Path(button.transform)}");
            }
            TestContext.WriteLine($"[lobby touch-floor audit @390x844 portrait, "
                + $"{SpecCssPerUnit} CSS px/u, floor {MinCssPx}]\n" + report);

            // Deliberately smaller than the previous ratchet: primary sortie
            // controls are not allowed back into the debt table. The remaining
            // controls live in Sanctum's fixed-height tab/row grammar and need a
            // separate layout pass rather than a hidden hit-box workaround.
            var expected = new Dictionary<string, int>
            {
                { "성장", 1 }, { "장비", 1 }, { "군단", 1 }, { "각인", 1 }, { "+", 3 },
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
            var found = 0;
            foreach (var button in canvas.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeInHierarchy) continue;
                var label = LabelOf(button);
                var isRouteAction = false;
                for (var i = 0; i < routePrefixes.Length; i++)
                    isRouteAction |= label.StartsWith(routePrefixes[i], System.StringComparison.Ordinal);
                if (!isRouteAction) continue;

                found += 1;
                var world = WorldRect(button.GetComponent<RectTransform>());
                Assert.That(world.width * SpecCssPerUnit, Is.GreaterThanOrEqualTo(MinCssPx),
                    $"{label} is narrower than the phone touch floor");
                Assert.That(world.height * SpecCssPerUnit, Is.GreaterThanOrEqualTo(MinCssPx),
                    $"{label} is shorter than the phone touch floor");
            }

            Assert.That(found, Is.EqualTo(StageCatalog.Entries.Count + 1 + 1
                + HackSpec.TrainingTiers + TrainingTrials.Ids.Length),
                "the audit must cover prologue, every descent, revealed pact, all tier choices and trials");
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
        /// two states the card repeated for free are gone; the one it uniquely
        /// carries stays. This pins the split so a future edit cannot quietly
        /// restore the wall of nine status chips.
        /// </summary>
        [Test]
        public void SortieCards_StateOnlyWhatTheMapCannot()
        {
            BuildClearedLobby();   // cinder-span cleared, ember-gallery reachable

            Assert.That(_lobby.StageStatusReadout(0), Is.EqualTo("정화 완료"),
                "a cleared card keeps a live 강하 button for replay, so the clear "
                + "marker is the ONE state nothing else on the card shows");
            for (var i = 1; i < StageCatalog.Entries.Count; i++)
                Assert.That(_lobby.StageStatusReadout(i), Is.Empty,
                    $"card {i} must not repeat a route state the map already owns "
                    + "(reachable -> its own enabled 강하 button; locked -> the "
                    + "map's ??? node)");
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
