using System.Collections.Generic;
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class PresentationFeedbackTests
    {
        [Test]
        public void BossIntro_UsesProvidedName_AndResetsTransientInputSafeElements()
        {
            var preferences = PreferenceSnapshot.Capture();
            var existingEventSystem = Object.FindAnyObjectByType<EventSystem>();
            GameObject hudObject = null;
            try
            {
                ViewPrefs.ReducedMotion = false;
                hudObject = new GameObject("BossIntroPresentationTest");
                var hud = hudObject.AddComponent<HudView>();
                hud.Build();

                var initiallyDisabled = new List<Image>();
                foreach (var image in hudObject.GetComponentsInChildren<Image>(true))
                    if (!image.enabled) initiallyDisabled.Add(image);

                hud.ShowBossIntro("Gate Sovereign");

                var title = FindText(hudObject, "— Gate Sovereign —");
                Assert.That(title, Is.Not.Null, "boss intro must render the supplied boss name");
                Assert.That(title.raycastTarget, Is.False,
                    "boss intro text is transient presentation and must not consume input");

                var activated = ActivatedImages(initiallyDisabled);
                Assert.That(activated.Count, Is.EqualTo(2),
                    "boss intro must enable its two letterbox presentation bars");
                foreach (var image in activated)
                    Assert.That(image.raycastTarget, Is.False,
                        "boss intro letterbox bars are presentation-only and must not consume input");

                hud.ResetRunUi();
                Assert.That(FindText(hudObject, "— Gate Sovereign —"), Is.Null,
                    "starting a new run must clear an interrupted boss intro");
                Assert.That(EnabledCount(activated), Is.Zero,
                    "starting a new run must disable interrupted boss-intro letterboxes");

                hud.ShowBossIntro("Gate Sovereign");
                hud.OnEvents(SimEvents.GameOver, new CinderSim());
                Assert.That(FindText(hudObject, "— Gate Sovereign —"), Is.Null,
                    "game over must clear an interrupted boss intro before opening its terminal panel");
                Assert.That(EnabledCount(activated), Is.Zero,
                    "game over must disable interrupted boss-intro letterboxes");
            }
            finally
            {
                if (hudObject != null) Object.DestroyImmediate(hudObject);
                DestroyCreatedEventSystem(existingEventSystem);
                preferences.Restore();
            }
        }

        [Test]
        public void CampaignStageClear_StartsCeremonyWithoutPrematureOrDuplicateTerminalPanel()
        {
            var preferences = PreferenceSnapshot.Capture();
            var existingEventSystem = Object.FindAnyObjectByType<EventSystem>();
            GameObject hudObject = null;
            try
            {
                ViewPrefs.ReducedMotion = false;
                hudObject = new GameObject("StageClearPresentationTest");
                var hud = hudObject.AddComponent<HudView>();
                hud.Build();
                hud.EnableCampaignUi("Abyss Chancel", 3);

                var terminalPanel = StageClearPanel(hudObject);
                var flash = hudObject.GetComponentInChildren<Canvas>(true)
                    .transform.Find("StageClearFlash").GetComponent<Image>();
                Assert.That(terminalPanel.activeSelf, Is.False,
                    "campaign stage-clear terminal panel must begin hidden");
                Assert.That(flash.enabled, Is.False,
                    "stage-clear ceremony flash must begin disabled");

                var digest = new CinderSim().Digest;
                hud.ShowStageClear(digest);
                hud.ShowStageClear(digest);

                var banner = StageClearBanner(hudObject);
                Assert.That(banner.text, Is.EqualTo("구역 정화"),
                    "stage clear must immediately begin its visible ceremony banner");
                Assert.That(banner.raycastTarget, Is.False,
                    "stage-clear ceremony banner is presentation-only and must not consume input");
                Assert.That(flash.enabled, Is.True,
                    "stage clear must immediately begin its transient flash ceremony");
                Assert.That(flash.raycastTarget, Is.False,
                    "stage-clear ceremony flash is presentation-only and must not consume input");
                Assert.That(terminalPanel.activeSelf, Is.False,
                    "terminal stage-clear panel must wait until the ceremony completes");
                Assert.That(hud.RetryModalVisible, Is.False,
                    "a pending ceremony must not enable terminal-panel retry input");
                Assert.That(StageClearPanelCount(hudObject), Is.EqualTo(1),
                    "duplicate stage-clear events during one ceremony must not duplicate the terminal panel");

                hud.ResetRunUi();
                Assert.That(flash.enabled, Is.False,
                    "a new run must stop a pending stage-clear flash");
                Assert.That(banner.text, Is.Empty,
                    "a new run must clear pending stage-clear presentation text");
                Assert.That(terminalPanel.activeSelf, Is.False,
                    "a new run must not inherit a terminal stage-clear panel");
            }
            finally
            {
                if (hudObject != null) Object.DestroyImmediate(hudObject);
                DestroyCreatedEventSystem(existingEventSystem);
                preferences.Restore();
            }
        }

        /// <summary>
        /// Every speaker the shipping catalog can emit must resolve to its
        /// intended palette class. Regression: the cycle-2 executor wing
        /// (SLUICE KEEPER / BASTION SENTINEL / ASH MAGISTRATE) shipped
        /// rendering in the ambient watcher tint because the bubble matched
        /// name prefixes instead of the catalog's own speaker identity —
        /// three bosses spoke as narration for a whole cycle. Walking the
        /// live StageCatalog means a new stage cannot repeat it silently.
        /// </summary>
        [Test]
        public void StorySpeakers_ResolveTheirIntendedBubblePalette()
        {
            var boss = SpeechBubbleView.SpeakerColor(StoryCatalog.CinderWarden);
            var warden = SpeechBubbleView.SpeakerColor(StoryCatalog.DuskWarden);
            var ambient = SpeechBubbleView.SpeakerColor(StoryCatalog.Watcher);
            Assert.That(boss, Is.Not.EqualTo(ambient),
                "a boss must be visually distinct from watcher narration");
            Assert.That(warden, Is.Not.EqualTo(ambient),
                "the warden must be visually distinct from watcher narration");
            Assert.That(boss, Is.Not.EqualTo(warden),
                "boss and warden voices must stay distinguishable");

            // The watcher opens every stage; every other beat is spoken by a
            // NAMED character (boss or warden — abyss-chancel deliberately lets
            // its boss keep the last word), and a named character must never
            // paint as narration. That is exactly the defect this pins.
            var beats = new[]
            {
                StoryCatalog.StageStart, StoryCatalog.BossEntry,
                StoryCatalog.BossPhase2, StoryCatalog.Completion,
            };
            var namedBeats = 0;
            for (var index = 0; index < StageCatalog.Entries.Count; index += 1)
            {
                var storyKey = StageCatalog.Entries[index].StoryKey;
                foreach (var beat in beats)
                {
                    if (!StoryCatalog.TryGet(storyKey, beat, out var speaker, out _)) continue;
                    var voice = StoryCatalog.VoiceOf(speaker);
                    var tint = SpeechBubbleView.SpeakerColor(speaker);
                    if (beat == StoryCatalog.StageStart)
                    {
                        Assert.That(voice, Is.EqualTo(SpeakerVoice.Ambient),
                            $"{storyKey}/{beat} is watcher narration");
                        Assert.That(tint, Is.EqualTo(ambient),
                            $"{storyKey}/{beat} must paint as narration");
                        continue;
                    }

                    namedBeats += 1;
                    Assert.That(voice, Is.Not.EqualTo(SpeakerVoice.Ambient),
                        $"{storyKey}/{beat} speaker '{speaker}' is a named character and "
                        + "must not fall through to ambient narration");
                    Assert.That(tint, Is.EqualTo(voice == SpeakerVoice.Boss ? boss : warden),
                        $"{storyKey}/{beat} must paint its bubble with the {voice} tint");
                }
            }
            Assert.That(namedBeats, Is.EqualTo(3 * StageCatalog.Entries.Count),
                "every stage must contribute boss-entry, phase-2 and completion beats");

            foreach (var executor in new[]
                     {
                         StoryCatalog.SluiceKeeper, StoryCatalog.BastionSentinel,
                         StoryCatalog.AshMagistrate,
                     })
                Assert.That(SpeechBubbleView.SpeakerColor(executor), Is.EqualTo(boss),
                    $"cycle-2 executor '{executor}' must speak in the boss tint, not narration");

            Assert.That(SpeechBubbleView.SpeakerColor(null), Is.EqualTo(ambient),
                "a missing speaker must fall back to ambient narration");
            Assert.That(SpeechBubbleView.SpeakerColor("CINDER IMPOSTOR"), Is.EqualTo(ambient),
                "palette must key off catalog identity, not a name prefix");
        }


        private static List<Image> ActivatedImages(List<Image> initiallyDisabled)
        {
            var activated = new List<Image>();
            foreach (var image in initiallyDisabled)
                if (image.enabled) activated.Add(image);
            return activated;
        }

        private static int EnabledCount(List<Image> images)
        {
            var count = 0;
            foreach (var image in images)
                if (image.enabled) count++;
            return count;
        }

        private static Text FindText(GameObject root, string content)
        {
            foreach (var text in root.GetComponentsInChildren<Text>(true))
                if (text.text == content) return text;
            return null;
        }

        private static Text StageClearBanner(GameObject root)
        {
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (text.rectTransform.sizeDelta == new Vector2(560f, 84f)) return text;
            }
            Assert.Fail("stage-clear ceremony banner is missing");
            return null;
        }

        private static GameObject StageClearPanel(GameObject root)
        {
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                if (IsStageClearPanel(image)) return image.gameObject;
            Assert.Fail("campaign stage-clear terminal panel is missing");
            return null;
        }

        private static int StageClearPanelCount(GameObject root)
        {
            var count = 0;
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                if (IsStageClearPanel(image)) count++;
            return count;
        }

        private static bool IsStageClearPanel(Image image)
        {
            if (image.gameObject.name != "Panel" ||
                image.rectTransform.sizeDelta != new Vector2(480f, 240f) ||
                FindText(image.gameObject, "구역 정화") == null)
                return false;

            var hasCampaignButton = false;
            var hasRetryButton = false;
            foreach (var button in image.GetComponentsInChildren<Button>(true))
            {
                var label = button.GetComponentInChildren<Text>(true);
                hasCampaignButton |= label != null && label.text == "캠페인으로";
                hasRetryButton |= label != null && label.text == "재강하 (R)";
            }
            return hasCampaignButton && hasRetryButton;
        }

        private static void DestroyCreatedEventSystem(EventSystem existingEventSystem)
        {
            if (existingEventSystem != null) return;
            var eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem != null) Object.DestroyImmediate(eventSystem.gameObject);
        }

        private readonly struct PreferenceSnapshot
        {
            private readonly bool _hadReducedMotionKey;
            private readonly int _reducedMotionValue;
            private readonly bool _reducedMotion;

            private PreferenceSnapshot(bool hadReducedMotionKey, int reducedMotionValue, bool reducedMotion)
            {
                _hadReducedMotionKey = hadReducedMotionKey;
                _reducedMotionValue = reducedMotionValue;
                _reducedMotion = reducedMotion;
            }

            public static PreferenceSnapshot Capture() => new PreferenceSnapshot(
                PlayerPrefs.HasKey("al:reduced-motion"),
                PlayerPrefs.GetInt("al:reduced-motion"),
                ViewPrefs.ReducedMotion);

            public void Restore()
            {
                ViewPrefs.ReducedMotion = _reducedMotion;
                if (_hadReducedMotionKey)
                    PlayerPrefs.SetInt("al:reduced-motion", _reducedMotionValue);
                else
                    PlayerPrefs.DeleteKey("al:reduced-motion");
                PlayerPrefs.Save();
            }
        }
    }
}
