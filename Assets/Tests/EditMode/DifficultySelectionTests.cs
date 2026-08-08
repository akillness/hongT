// AMENDMENT #11 §16 — the view half of difficulty: persistence and the lobby
// cycle button. The sim half (multipliers, tokens, surround ring) is pinned by
// DifficultyGroupAiTests; this file pins the parts a player touches — the choice
// survives a restart, a missing or corrupted key degrades to the pre-amendment
// game instead of an undefined one, and the button text can never drift away
// from the numbers the simulation actually uses.
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class DifficultySelectionTests
    {
        const string DifficultyKey = "al:difficulty";

        [SetUp]
        [TearDown]
        public void ClearKey()
        {
            PlayerPrefs.DeleteKey(DifficultyKey);
            PlayerPrefs.Save();
            ViewPrefs.ResetDifficultyCacheForTests();
        }

        [Test]
        public void MissingKeyResolvesToNormal_SoAnUntouchedInstallGetsTheFrozenGame()
        {
            Assert.That(ViewPrefs.Difficulty, Is.EqualTo(Difficulty.Normal),
                "a player who never opens the selector must get the pre-amendment sim");
        }

        [Test]
        public void EveryTierSurvivesASaveLoadRoundTrip()
        {
            foreach (var tier in new[]
                     {
                         Difficulty.Story, Difficulty.Normal,
                         Difficulty.Hard, Difficulty.Nightmare,
                     })
            {
                ViewPrefs.Difficulty = tier;
                ViewPrefs.ResetDifficultyCacheForTests();   // simulate a fresh session
                Assert.That(ViewPrefs.Difficulty, Is.EqualTo(tier),
                    "the selection must survive a restart");
            }
        }

        [Test]
        public void TheKeyStoresTheStableIdNotTheEnumInteger()
        {
            // An integer on disk would silently re-map if the tier list is ever
            // reordered. The id is the contract.
            ViewPrefs.Difficulty = Difficulty.Nightmare;
            Assert.That(PlayerPrefs.GetString(DifficultyKey, string.Empty),
                Is.EqualTo("nightmare"),
                "persistence must use DifficultySpec.IdOf");
        }

        [Test]
        public void CorruptedValuesMigrateToNormalRatherThanThrowing()
        {
            foreach (var junk in new[] { "", "   ", "HARD-ish", "3", "정상" })
            {
                PlayerPrefs.SetString(DifficultyKey, junk);
                PlayerPrefs.Save();
                ViewPrefs.ResetDifficultyCacheForTests();
                Assert.That(ViewPrefs.Difficulty, Is.EqualTo(Difficulty.Normal),
                    "corrupted key \"" + junk + "\" must degrade to Normal");
            }
        }

        [Test]
        public void StoredIdsAreCaseAndWhitespaceTolerant()
        {
            PlayerPrefs.SetString(DifficultyKey, "  Hard  ");
            PlayerPrefs.Save();
            ViewPrefs.ResetDifficultyCacheForTests();
            Assert.That(ViewPrefs.Difficulty, Is.EqualTo(Difficulty.Hard),
                "a hand-edited or shell-injected key must still parse");
        }

        [Test]
        public void TheCycleButtonWalksEveryTierInEasiestToHardestOrderAndWraps()
        {
            var current = Difficulty.Story;
            var seen = new System.Collections.Generic.List<Difficulty> { current };
            for (var step = 0; step < DifficultySpec.Count - 1; step++)
            {
                current = LobbyView.NextDifficulty(current);
                seen.Add(current);
            }

            Assert.That(seen, Is.EqualTo(new[]
            {
                Difficulty.Story, Difficulty.Normal,
                Difficulty.Hard, Difficulty.Nightmare,
            }), "the cycle must follow the display order, not the enum's raw values");

            Assert.That(LobbyView.NextDifficulty(Difficulty.Nightmare),
                Is.EqualTo(Difficulty.Story),
                "the hardest tier must wrap back to the easiest");
        }

        [Test]
        public void TheButtonTextReportsTheSimulationsRealNumbers()
        {
            // The label is generated from DifficultySpec.For, so a balance change
            // cannot leave a stale promise on screen.
            var hard = LobbyView.DifficultyLabelText(Difficulty.Hard);
            var hardProfile = DifficultySpec.For(Difficulty.Hard);
            Assert.That(hard, Does.Contain(hardProfile.IncomingDamageMul.ToString("0.00")),
                "the incoming-damage multiplier must be shown, not implied");
            Assert.That(hard, Does.Contain(hardProfile.AttackCooldownMul.ToString("0.00")),
                "the aggression multiplier must be shown");
            Assert.That(hard, Does.Contain(hardProfile.AttackTokens.ToString()),
                "the simultaneous-attacker cap must be shown");
            Assert.That(hard, Does.Contain("ON"),
                "Hard is a group-AI tier and must say so");
        }

        [Test]
        public void NonGroupAiTiersDoNotAdvertiseCoordination()
        {
            foreach (var tier in new[] { Difficulty.Story, Difficulty.Normal })
            {
                Assert.That(DifficultySpec.For(tier).GroupAi, Is.False,
                    "the design keeps coordination for Hard and above");
                Assert.That(LobbyView.DifficultyLabelText(tier), Does.Contain("OFF"),
                    "a tier without coordination must not claim it");
            }
        }

        [Test]
        public void EveryTierHasADistinctKoreanName()
        {
            var names = new System.Collections.Generic.HashSet<string>();
            for (var order = 0; order < DifficultySpec.Count; order++)
            {
                var name = LobbyView.DifficultyName(DifficultySpec.AtOrder(order));
                Assert.That(names.Add(name), Is.True,
                    "two tiers must never share a label: " + name);
            }
        }
    }
}
