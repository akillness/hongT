// AMENDMENT #11 UI — the run-difficulty readout.
//
// §16 locks the tier at run creation on purpose (a run must reproduce from
// (config, input sequence)), which means the player cannot change it mid-run and
// therefore MUST be able to see what they locked in. These tests pin the three
// places that promise carries: the in-run HUD badge, the geometry it displaces,
// and the result lines whose score is meaningless without a tier next to it.
//
// The static formatting helpers are pinned separately from the MonoBehaviour so a
// wording regression fails with a readable diff instead of a null rect.
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class DifficultyHudTests
    {
        const float BaselineStatsHeight = 108f;
        const float BadgedStatsHeight = 132f;
        const float StatsTopInset = 16f;
        const float MuteGap = 8f;

        GameObject _hudObject;
        HudView _hud;

        [SetUp]
        public void SetUp()
        {
            _hudObject = new GameObject("DifficultyHudTests");
            _hud = _hudObject.AddComponent<HudView>();
            _hud.Build();
            // Desktop geometry through the injected seam: Screen.* is degenerate in
            // batchmode, and the badge must be pinned on the tier where stats and the
            // mute button share the top-right column.
            _hud.ApplyLayout(1280, 720, new Rect(0, 0, 1280, 720));
            Assert.That(_hud.CurrentTier, Is.EqualTo(HudView.LayoutTier.Full),
                "1280x720 must classify as Full or this fixture is testing the wrong column");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_hudObject);
            var eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem != null) Object.DestroyImmediate(eventSystem.gameObject);
        }

        static Difficulty[] EveryTier()
        {
            var tiers = new Difficulty[DifficultySpec.Count];
            for (var order = 0; order < DifficultySpec.Count; order++)
                tiers[order] = DifficultySpec.AtOrder(order);
            return tiers;
        }

        // ------------------------------------------------ badge visibility --

        [Test]
        public void NormalRunShowsNoBadge_SoTheBaselineHudIsUntouched()
        {
            _hud.SetRunDifficulty(Difficulty.Normal);

            Assert.That(HudView.ShowsDifficultyBadge(Difficulty.Normal), Is.False,
                "Normal is the pre-amendment ruleset — a permanent '난이도 보통' row says nothing");
            Assert.That(_hud.DifficultyLabelForTest.gameObject.activeSelf, Is.False,
                "the badge row must be hidden on a baseline run");
            Assert.That(_hud.StatsRectForTest.sizeDelta.y,
                Is.EqualTo(BaselineStatsHeight).Within(0.01f),
                "a hidden row must not leave empty panel background behind");
            Assert.That(_hud.MuteRectForTest.anchoredPosition.y,
                Is.EqualTo(-(StatsTopInset + BaselineStatsHeight + MuteGap)).Within(0.01f),
                "the mute button keeps its pre-amendment position on a baseline run");
        }

        [Test]
        public void EveryOffBaselineTierShowsTheBadge()
        {
            foreach (var tier in EveryTier())
            {
                if (tier == Difficulty.Normal) continue;
                _hud.SetRunDifficulty(tier);
                Assert.That(HudView.ShowsDifficultyBadge(tier), Is.True, tier.ToString());
                Assert.That(_hud.DifficultyLabelForTest.gameObject.activeSelf, Is.True,
                    $"{tier} changes the rules, so the run must say so on screen");
                Assert.That(_hud.DifficultyLabelForTest.text,
                    Is.EqualTo(HudView.DifficultyBadgeText(tier)),
                    "the rendered row must be the formatter's output, not a second wording");
            }
        }

        [Test]
        public void TheBadgeGrowsThePanelAndPushesTheMuteButtonDown()
        {
            _hud.SetRunDifficulty(Difficulty.Nightmare);

            var height = _hud.StatsRectForTest.sizeDelta.y;
            Assert.That(height, Is.EqualTo(BadgedStatsHeight).Within(0.01f),
                "a fifth row needs a taller panel or it renders outside the background");

            var muteTop = -_hud.MuteRectForTest.anchoredPosition.y;
            Assert.That(muteTop, Is.EqualTo(StatsTopInset + height + MuteGap).Within(0.01f),
                "the mute button must follow the panel, not overlap it");
            Assert.That(muteTop, Is.GreaterThanOrEqualTo(StatsTopInset + height),
                "mute button overlaps the stats panel");
        }

        [Test]
        public void TheBadgeRowFitsInsideThePanelItGrew()
        {
            _hud.SetRunDifficulty(Difficulty.Hard);

            var row = _hud.DifficultyLabelForTest.rectTransform;
            // Label pivot is top-left, so the row spans |y| .. |y| + height downward
            // from the panel's top edge.
            var rowBottom = -row.anchoredPosition.y + row.sizeDelta.y;
            Assert.That(rowBottom,
                Is.LessThanOrEqualTo(_hud.StatsRectForTest.sizeDelta.y + 0.01f),
                "the badge row must not hang off the bottom of the stats panel");
        }

        [Test]
        public void ReturningToNormalRestoresTheBaselineGeometry()
        {
            _hud.SetRunDifficulty(Difficulty.Nightmare);
            Assert.That(_hud.StatsRectForTest.sizeDelta.y,
                Is.EqualTo(BadgedStatsHeight).Within(0.01f), "precondition");

            // An arena or prologue run carries Difficulty.Normal; the badge from the
            // previous descent must not survive into it.
            _hud.SetRunDifficulty(Difficulty.Normal);

            Assert.That(_hud.RunDifficulty, Is.EqualTo(Difficulty.Normal));
            Assert.That(_hud.DifficultyLabelForTest.gameObject.activeSelf, Is.False,
                "a stale Nightmare badge would mislabel an arena run");
            Assert.That(_hud.StatsRectForTest.sizeDelta.y,
                Is.EqualTo(BaselineStatsHeight).Within(0.01f));
            Assert.That(_hud.MuteRectForTest.anchoredPosition.y,
                Is.EqualTo(-(StatsTopInset + BaselineStatsHeight + MuteGap)).Within(0.01f));
        }

        [Test]
        public void ReLatchingTheSameTierIsIdempotent()
        {
            _hud.SetRunDifficulty(Difficulty.Hard);
            var height = _hud.StatsRectForTest.sizeDelta.y;
            var mute = _hud.MuteRectForTest.anchoredPosition;

            _hud.SetRunDifficulty(Difficulty.Hard);

            Assert.That(_hud.StatsRectForTest.sizeDelta.y, Is.EqualTo(height).Within(0.01f),
                "a second Begin on the same tier must not stack another row's worth of height");
            Assert.That(_hud.MuteRectForTest.anchoredPosition, Is.EqualTo(mute));
        }

        // ------------------------------------------------------- wording ----

        [Test]
        public void EveryBadgeNamesItsTierAndOnlyGroupAiTiersClaimCoordination()
        {
            foreach (var tier in EveryTier())
            {
                var badge = HudView.DifficultyBadgeText(tier);
                Assert.That(badge, Does.Contain(LobbyView.DifficultyName(tier)),
                    "the badge must name the tier, not just imply it");
                Assert.That(badge.Contains("협동"),
                    Is.EqualTo(DifficultySpec.For(tier).GroupAi),
                    $"{tier} must advertise coordination iff the sim actually coordinates");
            }
        }

        [Test]
        public void TheBadgeIsOneLine_SoItFitsTheSingleStatsRow()
        {
            foreach (var tier in EveryTier())
                Assert.That(HudView.DifficultyBadgeText(tier), Does.Not.Contain("\n"),
                    "a multi-line badge would overflow the 24 u stats row");
        }

        [Test]
        public void ResultSuffixIsEmptyOnNormalSoExistingRunSummariesAreUnchanged()
        {
            Assert.That(HudView.DifficultyResultSuffix(Difficulty.Normal),
                Is.EqualTo(string.Empty),
                "the baseline result line must stay byte-identical to the pre-amendment one");
        }

        [Test]
        public void ResultSuffixNamesTheTierSoScoresAreComparable()
        {
            foreach (var tier in EveryTier())
            {
                if (tier == Difficulty.Normal) continue;
                Assert.That(HudView.DifficultyResultSuffix(tier),
                    Does.Contain(LobbyView.DifficultyName(tier)),
                    $"a score earned on {tier} must carry the tier that produced it");
            }
        }

        // -------------------------------------------------- lobby marker ----

        [Test]
        public void TheLobbyButtonStatesItsPositionInTheTierOrder()
        {
            for (var order = 0; order < DifficultySpec.Count; order++)
            {
                var tier = DifficultySpec.AtOrder(order);
                Assert.That(LobbyView.DifficultyLabelText(tier),
                    Does.Contain($"[{order + 1}/{DifficultySpec.Count}]"),
                    "a cycle button hides the range: the step marker is how the player "
                    + "learns how many tiers exist and which way clicking moves");
            }
        }
    }
}
