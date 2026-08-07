using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class GameDirectorCampaignRouteTests
    {
        private const string CampaignKey = "abyssal-lantern:unity:campaign";

        [UnityTest]
        public IEnumerator CampaignClear_PersistsUnlockAndCarriesSelectedOrDeferredRestIntoDirectSuccessor()
        {
            foreach (var defer in new[] { false, true })
            {
                var route = new CampaignRoute();
                try
                {
                    yield return null; // Let Unity invoke GameView.Start and initialize its presentation pool.
                    AssertCampaignClearRoute(route, defer);
                }
                finally
                {
                    route.Dispose();
                }
            }
        }

        /// <summary>
        /// Verdict Pact payout contract (meta-fun-pass-spec M3 · negotiation
        /// entry 5, signed designer+pm): an armed pact pays the in-run relic
        /// haul TIMES <see cref="GameDirector.PactRelicMultiplier"/> and
        /// nothing else — progression, stat points and the first-clear bonus
        /// line stay on their single-count terms. GameDirector's const is
        /// internal specifically so this test can pin it; until now nothing
        /// did, and the only evidence for the doubled payout was one browser
        /// screenshot. Drives the real lobby toggle, so it also pins that the
        /// toggle is reachable on a cleared card and latches into the sortie.
        /// </summary>
        [UnityTest]
        public IEnumerator ArmedPactSortie_DoublesOnlyTheInRunRelicPayout()
        {
            var route = new CampaignRoute();
            try
            {
                yield return null; // Let Unity invoke GameView.Start.
                AssertPactRelicEconomy(route);
            }
            finally
            {
                route.Dispose();
            }
        }

        [Test]
        public void LobbyMotionLabels_UseGlyphsPresentInShippedHudKoreanFont()
        {
            var font = Resources.Load<Font>("Fonts/HudKorean");
            Assert.That(font, Is.Not.Null,
                "LobbyView must load the shipped Resources/Fonts/HudKorean font rather than its builtin fallback");

            var route = new CampaignRoute();
            try
            {
                Button motionButton = null;
                foreach (var button in route.Lobby.GetComponentsInChildren<Button>(true))
                {
                    var label = button.GetComponentInChildren<Text>();
                    if (label != null && label.text.StartsWith("모션:"))
                    {
                        motionButton = button;
                        break;
                    }
                }

                Assert.That(motionButton, Is.Not.Null,
                    "the Lobby motion control must expose its rendered state label");
                var motionLabel = motionButton.GetComponentInChildren<Text>();
                Assert.That(motionLabel, Is.Not.Null,
                    "the Lobby motion control must render its state through a Text component");
                Assert.That(motionLabel.font, Is.SameAs(font),
                    "the rendered Lobby motion label must use the shipped HudKorean resource");

                var states = new List<string> { motionLabel.text };
                motionButton.onClick.Invoke();
                states.Add(motionLabel.text);
                Assert.That(states, Is.EquivalentTo(new[] { "모션: 보통", "모션: 약함" }),
                    "toggling the Lobby motion control must expose both user-visible state labels");

                var missingGlyphs = new List<string>();
                foreach (var state in states)
                {
                    foreach (var character in state)
                    {
                        if (!motionLabel.font.HasCharacter(character))
                            missingGlyphs.Add($"\"{state}\": '{character}' (U+{(int)character:X4})");
                    }
                }

                Assert.That(missingGlyphs, Is.Empty,
                    "HudKorean is missing glyphs required by LobbyView motion labels:\n" +
                    string.Join("\n", missingGlyphs));
            }
            finally
            {
                route.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator BeginBeforeFirstScheduledFrame_InitializesOnePlayerAndDamageHost()
        {
            var existingActors = new HashSet<ActorView>(
                Object.FindObjectsByType<ActorView>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            GameObject root = null;
            ActorView player = null;
            try
            {
                root = new GameObject("GameViewInitializationRegression");
                var game = root.AddComponent<GameView>();
                var config = HackConfig.Arena();

                game.Begin(in config, string.Empty, null);
                player = AssertGameViewInitializedOnce(game, existingActors,
                    "Begin before the first scheduled frame");

                yield return null;

                var playerAfterFrame = AssertGameViewInitializedOnce(game, existingActors,
                    "the first scheduled frame after Begin");
                Assert.That(playerAfterFrame, Is.SameAs(player),
                    "the scheduled Start callback must not duplicate the player initialized by Begin");
            }
            finally
            {
                foreach (var actor in Object.FindObjectsByType<ActorView>(FindObjectsInactive.Include,
                             FindObjectsSortMode.None))
                {
                    if (!existingActors.Contains(actor)) Object.DestroyImmediate(actor.gameObject);
                }
                if (root != null) Object.DestroyImmediate(root);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CompanionOneShot_IsConsumedAfterFirstTickOfCatchUpBatch(bool queueHold)
        {
            var existingActors = new HashSet<ActorView>(
                Object.FindObjectsByType<ActorView>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            GameObject root = null;
            var timeScale = Time.timeScale;
            try
            {
                root = new GameObject("GameViewCompanionOneShotRegression");
                var input = root.AddComponent<InputAdapter>();
                var game = root.AddComponent<GameView>();
                game.Input = input;
                var config = DungeonWithCompanion();
                game.Begin(in config, "Cinder Span", config.CompanionId);

                var firstTickBehavior = queueHold ? CompanionBehavior.Hold : CompanionBehavior.Follow;
                var behaviorBetweenTicks = queueHold ? CompanionBehavior.Follow : CompanionBehavior.Hold;
                var sample = game.Sim as IHackSnapshot;
                Assert.That(sample, Is.Not.Null, "the dungeon GameView must expose its companion snapshot");

                if (queueHold) input.QueueCompanionHold();
                else input.QueueCompanionRecall();
                input.QueueDash(); // Emits one first-tick event so the public callback can probe the batch boundary.

                var probes = 0;
                game.OnRunEvents = (_, sim) =>
                {
                    Assert.That(probes, Is.Zero, "only the first sampled tick may reach the probe");
                    var snapshot = sim as IHackSnapshot;
                    Assert.That(snapshot.CompanionBehavior, Is.EqualTo(firstTickBehavior),
                        "the sampled companion command must reach the first fixed tick");

                    // Establish the opposite state between ticks. A repeated sampled command would overwrite it.
                    ((CinderSim)sim).Tick(new SimInput
                    {
                        CompanionHoldQueued = !queueHold,
                        CompanionRecallQueued = queueHold,
                    });
                    probes++;
                };

                Time.timeScale = 0f;
                SetAccumulator(game, SimConfig.FixedStep * 2f);
                InvokeUpdate(game);

                Assert.That(probes, Is.EqualTo(1), "the catch-up batch must probe its first tick once");
                Assert.That(sample.CompanionBehavior, Is.EqualTo(behaviorBetweenTicks),
                    "subsequent fixed ticks from the sample must not repeat the companion one-shot");
            }
            finally
            {
                foreach (var actor in Object.FindObjectsByType<ActorView>(FindObjectsInactive.Include,
                             FindObjectsSortMode.None))
                {
                    if (!existingActors.Contains(actor)) Object.DestroyImmediate(actor.gameObject);
                }
                Time.timeScale = timeScale;
                if (root != null) Object.DestroyImmediate(root);
            }
        }

        private static HackConfig DungeonWithCompanion()
        {
            Assert.That(HackConfig.TryDungeon(
                CampaignStages.CinderSpan,
                MetaStats.Of(0, 0, 0),
                EquipTiers.Of(0, 0, 0),
                "lantern-wisp",
                0,
                out var config), Is.True, "the regression requires the standard companion dungeon fixture");
            return config;
        }

        private static void SetAccumulator(GameView game, float value)
        {
            var accumulator = typeof(GameView).GetField("_accumulator",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(accumulator, Is.Not.Null, "GameView must retain its fixed-step accumulator");
            accumulator.SetValue(game, value);
        }

        private static void InvokeUpdate(GameView game)
        {
            var update = typeof(GameView).GetMethod("Update",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(update, Is.Not.Null, "GameView must retain its fixed-step update entrypoint");
            update.Invoke(game, null);
        }

        private static ActorView AssertGameViewInitializedOnce(GameView game, HashSet<ActorView> existingActors,
                                                               string timing)
        {
            var newPlayerCount = 0;
            ActorView player = null;
            foreach (var actor in Object.FindObjectsByType<ActorView>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (existingActors.Contains(actor) || actor.gameObject.name != "Player") continue;
                newPlayerCount += 1;
                player = actor;
            }

            var damageHost = game.transform.Find("DamageNumbers");
            Assert.That(newPlayerCount, Is.EqualTo(1),
                timing + " must have exactly one player view");
            Assert.That(damageHost, Is.Not.Null,
                timing + " must create the GameView damage-number host");
            Assert.That(game.GetComponentsInChildren<DamageNumberPool>(true).Length, Is.EqualTo(1),
                timing + " must have exactly one damage-number pool");
            return player;
        }

        private static void AssertPactRelicEconomy(CampaignRoute route)
        {
            var cinderSpan = StageCatalog.Entries[0];

            // --- run 1: no pact. The toggle only exists on a CLEARED card, so
            // the first descent is necessarily the single-count baseline.
            route.StartCinderSpanThroughLobbyCallback();
            var plain = route.Game.Sim as CinderSim;
            Assert.That(plain, Is.Not.Null, "the sortie callback must start the cinder-span simulation");
            Assert.That(route.Lobby.IsPactArmed(cinderSpan.Id), Is.False,
                "an unvisited stage must not carry an armed pact");
            ClearCinderSpan(plain);

            var beforePlain = CampaignStore.Load();
            var plainHaul = plain.Relics;
            route.Game.OnRunEvents.Invoke(plain.Events, plain);
            var afterPlain = CampaignStore.Load();
            var plainPayout = afterPlain.Relics - beforePlain.Relics;
            Assert.That(plainPayout, Is.EqualTo(plainHaul),
                "a plain clear must bank the in-run relic haul exactly once "
                + "(cinder-span carries no first-clear bonus)");
            Assert.That(afterPlain.Points - beforePlain.Points, Is.EqualTo(3),
                "a first clear grants 2 stat points plus the first-boss point");

            // --- run 2: same stage, pact armed through the real lobby toggle.
            route.ReturnToLobbyThroughHud();
            route.ArmPactOnFirstStageCard();
            var beforePact = CampaignStore.Load();
            route.StartFirstStageThroughDescentButton();
            var pact = route.Game.Sim as CinderSim;
            Assert.That(pact, Is.Not.Null, "the armed sortie must start a fresh simulation");
            Assert.That(((ICampaignSnapshot)pact).Hazards.Count,
                Is.EqualTo(StageCatalog.PactFor(cinderSpan.Id).Length),
                "an armed pact must route the stage's pact hazard table into the run");
            ClearCinderSpan(pact);

            var pactHaul = pact.Relics;
            route.Game.OnRunEvents.Invoke(pact.Events, pact);
            var afterPact = CampaignStore.Load();
            var pactPayout = afterPact.Relics - beforePact.Relics;
            Assert.That(pactPayout, Is.EqualTo(pactHaul * GameDirector.PactRelicMultiplier),
                "an armed pact clear must bank exactly the doubled in-run haul — no bonus term");
            Assert.That(afterPact.Points - beforePact.Points, Is.EqualTo(2),
                "a repeat clear grants the 2-point clear award without the first-boss point");
            Assert.That(StageCatalog.IsCleared(in afterPact, in cinderSpan), Is.True,
                "a pact run must leave progression exactly where the plain clear left it");

            // negotiation-record entry 5 asks that a pact run pay at most 2.2x a
            // normal one. What THIS test can prove is the payout RULE: the pact
            // multiplies the same run's own haul by exactly 2 (asserted above),
            // so per-haul the ratio is 2.00x — inside the band by construction.
            //
            // What it cannot prove is the entry's other half. The two clears are
            // separate runs, so their in-run HAULS differ (relic drops key off
            // which enemy ids die), and one sample each is not an average. The
            // raw cross-run totals are therefore REPORTED, not gated: gating them
            // would fail on haul noise and say nothing about the multiplier.
            // Deciding the average needs a sampling study — logged as QA work in
            // qa/gate-measurements.md, not faked here with n=1.
            TestContext.WriteLine(
                $"[entry-5] payout rule: pact banks {pactPayout} on a haul of {pactHaul} "
                + $"= {(double)pactPayout / pactHaul:F2}x its own haul (band <= 2.2x). "
                + $"Cross-run sample (n=1, NOT a gate): normal payout {plainPayout} "
                + $"on haul {plainHaul}, pact payout {pactPayout} on haul {pactHaul}.");
            Assert.That(pactHaul, Is.GreaterThan(0),
                "the pact run must actually collect relics for the payout rule to mean anything");
            Assert.That(pactPayout * 10, Is.LessThanOrEqualTo(pactHaul * 22),
                $"entry 5 band: pact banked {pactPayout} on a haul of {pactHaul} — over 2.2x its own haul");
        }

        private static void AssertCampaignClearRoute(CampaignRoute route, bool defer)
        {
            route.StartCinderSpanThroughLobbyCallback();
            var completed = route.Game.Sim as CinderSim;
            Assert.That(completed, Is.Not.Null, "the sortie callback must start the cinder-span simulation");
            ClearCinderSpan(completed);

            var clearEvents = completed.Events;
            Assert.That((clearEvents & SimEvents.StageCleared) != 0,
                "the simulation must publish the stage-clear event that GameDirector consumes");
            route.Game.OnRunEvents.Invoke(clearEvents, completed);

            var persistedPayload = PlayerPrefs.GetString(CampaignKey);
            var persisted = CampaignStore.Load();
            var cinderSpan = StageCatalog.Entries[0];
            var emberGallery = StageCatalog.Entries[1];
            Assert.That(persistedPayload, Does.Contain("\"clearedMask\":1"),
                "GameDirector must write the cleared cinder-span bit through CampaignStore");
            Assert.That(StageCatalog.IsCleared(in persisted, in cinderSpan), Is.True,
                "the persisted campaign must retain the completed stage");
            Assert.That(StageCatalog.IsUnlocked(in persisted, in emberGallery), Is.True,
                "clearing cinder-span must unlock its direct ember-gallery successor");

            var rest = route.Game.EmberRestSnapshot;
            Assert.That(rest, Is.Not.Null, "a cleared stage with a direct successor must open Ember Rest");
            Assert.That(rest.EmberRestOpen, Is.True,
                "the GameDirector callback must open the active simulation's Ember Rest");
            var selected = rest.EmberRestOffer1;
            Assert.That(selected.IsValid, Is.True, "the selected Ember Rest slot must be a real offer");

            PreparationOffer handoff;
            if (defer)
            {
                Assert.That(route.Hud.OnEmberRestDeferred.Invoke(), Is.True,
                    "the wired defer callback must accept an open Ember Rest");
                handoff = default;
                Assert.That(route.Game.SelectedEmberRestPreparation.IsValid, Is.False,
                    "deferring must clear an earlier-or-current Ember Rest preparation before continuation");
            }
            else
            {
                Assert.That(route.Hud.OnEmberRestOfferSelected.Invoke(1), Is.True,
                    "the wired selection callback must accept the offered slot");
                handoff = selected;
                AssertSameOffer(selected, route.Game.SelectedEmberRestPreparation,
                    "the selected offer must remain available until the direct-successor handoff");
            }

            route.Hud.OnEmberRestContinue.Invoke();

            var successor = route.Game.Sim as CinderSim;
            Assert.That(successor, Is.Not.Null, "continuing Ember Rest must begin a successor simulation");
            Assert.That(successor, Is.Not.SameAs(completed),
                "continuing Ember Rest must consume the cleared run rather than reuse it");
            var successorCampaign = (ICampaignSnapshot)successor;
            Assert.That(successorCampaign.StageId, Is.EqualTo(emberGallery.SimAnchorId),
                "the direct successor must use ember-gallery's frozen Sim anchor");
            Assert.That(successorCampaign.Hazards.Count, Is.EqualTo(emberGallery.HazardOverride.Length),
                "the direct successor must receive ember-gallery's logical hazard override");
            Assert.That(route.Game.EmberRestSnapshot.EmberRestOpen, Is.False,
                "the direct successor must not inherit an open Ember Rest state");
            Assert.That(route.Game.SelectedEmberRestPreparation.IsValid, Is.False,
                "the consumed preparation must not remain queued after the successor begins");
            AssertSameOffer(handoff, successor.AppliedPreparationInput,
                "the direct successor must receive the selected-or-deferred preparation input exactly");

            var expected = ExpectedEmberGalleryRun(persisted, emberGallery, handoff);
            AssertSuccessorMatchesConfiguredPreparation(successor, expected, defer);
        }

        private static CinderSim ExpectedEmberGalleryRun(CampaignData persisted, StageEntry successor,
                                                         PreparationOffer handoff)
        {
            Assert.That(HackConfig.TryDungeon(successor.SimAnchorId,
                    MetaStats.Of(persisted.Attack, persisted.Vitality, persisted.Swiftness),
                    EquipTiers.Of(persisted.Weapon, persisted.Lantern, persisted.Cloak),
                    persisted.Active, 0, out var config), Is.True,
                "the unlocked direct successor must resolve to a runnable campaign config");
            config.Hazards = successor.HazardOverride;
            config.PreparationOffer = handoff;
            return new CinderSim(in config);
        }

        private static void AssertSuccessorMatchesConfiguredPreparation(CinderSim actual, CinderSim expected,
                                                                         bool deferred)
        {
            for (var tick = 0; tick < 900; tick += 1)
            {
                var input = CombatInput(actual);
                actual.Tick(in input);
                expected.Tick(in input);
            }

            Assert.That(SameDigest(actual.Digest, expected.Digest), Is.True,
                deferred
                    ? "a deferred Ember Rest must match the unprepared successor configuration"
                    : "the selected Ember Rest offer must match the direct successor configuration");

        }
        private static bool SameDigest(RunDigest left, RunDigest right)
            => left.Score == right.Score
                && left.Wave == right.Wave
                && left.Kills == right.Kills
                && left.Relics == right.Relics
                && left.HealthRemaining == right.HealthRemaining
                && left.Reason == right.Reason;

        private static void ClearCinderSpan(CinderSim sim)
        {
            for (var tick = 0; tick < 60 * 300; tick += 1)
            {
                var input = CombatInput(sim);
                sim.Tick(in input);
                if ((sim.Events & SimEvents.StageCleared) != 0) return;
                if (sim.Mode == SimMode.GameOver)
                    Assert.Fail("the rank-five campaign route pilot died before cinder-span cleared");
            }
            Assert.Fail("the campaign route pilot did not clear cinder-span");
        }

        /// <summary>
        /// The route pilot: kite the nearest live enemy, but detour onto a dropped
        /// pickup whenever one is on the floor.
        ///
        /// The detour is not decoration. Relic motes are only collected by walking
        /// inside <see cref="SimConfig.PickupMagnetRadius"/>, and a pilot that only
        /// ever chases enemies collects relics purely by accident — the whole run
        /// banked 0 or 1 relic depending on where bodies happened to fall. That made
        /// every haul assertion a coin flip: merging an unrelated sim amendment
        /// nudged positions and flipped a passing 1 into a failing 0. Steering at
        /// the pickup makes the haul a property of the RULES (drops happen, walking
        /// over them banks them) instead of a property of the layout.
        ///
        /// Enemy kiting still wins while no pickup exists, so clear behaviour and
        /// the tick-for-tick determinism check at the top of this file are unchanged
        /// in shape: both sims still receive one identical input stream.
        /// </summary>
        private static SimInput CombatInput(CinderSim sim)
        {
            var player = sim.Player;
            var input = new SimInput
            {
                AttackQueued = true,
                BoltQueued = true,
                PulseQueued = true,
                NovaQueued = true,
                WardQueued = true,
            };

            // A pickup on the floor outranks combat spacing: it expires on a timer,
            // so the only way to prove the payout rule is to actually go get it.
            var pickups = sim.Pickups;
            var bestPickup = -1;
            var bestPickupDistanceSquared = float.MaxValue;
            for (var index = 0; index < pickups.Count; index += 1)
            {
                var pickup = pickups[index];
                var px = pickup.X - player.X;
                var py = (pickup.Y - player.Y) * SimConfig.IsoY;
                var distanceSquared = px * px + py * py;
                if (distanceSquared >= bestPickupDistanceSquared) continue;
                bestPickupDistanceSquared = distanceSquared;
                bestPickup = index;
            }

            if (bestPickup >= 0)
            {
                var pickup = pickups[bestPickup];
                var toX = pickup.X - player.X;
                var toY = pickup.Y - player.Y;
                var length = Mathf.Max(0.001f, Mathf.Sqrt(toX * toX + toY * toY));
                input.MoveX = toX / length;
                input.MoveY = toY / length;
                return input;
            }

            var bestDistanceSquared = float.MaxValue;
            var deltaX = 0f;
            var deltaY = 0f;
            var enemies = sim.Enemies;
            for (var index = 0; index < enemies.Count; index += 1)
            {
                var enemy = enemies[index];
                if (enemy.Dead) continue;
                var x = enemy.X - player.X;
                var y = (enemy.Y - player.Y) * SimConfig.IsoY;
                var distanceSquared = x * x + y * y;
                if (distanceSquared >= bestDistanceSquared) continue;
                bestDistanceSquared = distanceSquared;
                deltaX = x;
                deltaY = enemy.Y - player.Y;
            }

            if (bestDistanceSquared == float.MaxValue) return input;

            var distance = Mathf.Sqrt(bestDistanceSquared);
            var toEnemyLength = Mathf.Max(0.001f, Mathf.Sqrt(deltaX * deltaX + deltaY * deltaY));
            if (distance < 120f)
            {
                input.MoveX = -deltaX / toEnemyLength;
                input.MoveY = -deltaY / toEnemyLength;
            }
            else if (distance > 150f)
            {
                input.MoveX = deltaX / toEnemyLength;
                input.MoveY = deltaY / toEnemyLength;
            }
            return input;
        }

        private static void AssertSameOffer(PreparationOffer expected, PreparationOffer actual, string message)
        {
            Assert.That(actual.Kind, Is.EqualTo(expected.Kind), message + " kind");
            Assert.That(actual.Variant, Is.EqualTo(expected.Variant), message + " variant");
            Assert.That(actual.Magnitude, Is.EqualTo(expected.Magnitude), message + " magnitude");
        }

        private sealed class CampaignRoute
        {
            private readonly HashSet<GameObject> _existingGameObjects = new HashSet<GameObject>();
            private readonly bool _hadCampaign;
            private readonly string _campaignPayload;
            private readonly bool _hadRotateHint;
            private readonly int _rotateHint;
            private readonly bool _hadReducedMotion;
            private readonly int _reducedMotion;
            private readonly GameObject _root;

            public readonly GameDirector Director;
            public readonly LobbyView Lobby;
            public readonly HudView Hud;
            public readonly GameView Game;

            public CampaignRoute()
            {
                foreach (var gameObject in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include,
                             FindObjectsSortMode.None))
                    _existingGameObjects.Add(gameObject);

                _hadCampaign = PlayerPrefs.HasKey(CampaignKey);
                _campaignPayload = PlayerPrefs.GetString(CampaignKey);
                _hadRotateHint = PlayerPrefs.HasKey("al:rotate-hint");
                _rotateHint = PlayerPrefs.GetInt("al:rotate-hint");
                _hadReducedMotion = PlayerPrefs.HasKey("al:reduced-motion");
                _reducedMotion = PlayerPrefs.GetInt("al:reduced-motion");

                var initial = new CampaignData
                {
                    PrologueDone = true,
                    Attack = 9,
                    Vitality = 9,
                    Swiftness = 9,
                    Weapon = 5,
                    Lantern = 5,
                    Cloak = 5,
                    Roster = new string[0],
                    Active = string.Empty,
                };
                CampaignStore.Save(in initial);

                _root = new GameObject("GameDirectorCampaignRouteTests");
                Director = _root.AddComponent<GameDirector>();
                Lobby = AddChild<LobbyView>("Lobby");
                var staging = AddChild<LobbyStaging>("LobbyStaging");
                var rig = AddChild<CameraRig>("CameraRig");
                var input = AddChild<InputAdapter>("Input");
                Hud = AddChild<HudView>("Hud");
                var audio = AddChild<AudioDirector>("Audio");
                var vfx = AddChild<VfxDirector>("Vfx");
                Game = AddChild<GameView>("Game");
                var speech = AddChild<SpeechBubbleView>("Speech");
                var cutscene = AddChild<CutsceneView>("Cutscene");
                Hud.Build();
                Game.Input = input;
                Game.Hud = Hud;
                Game.Audio = audio;
                Game.Vfx = vfx;
                Game.Rig = rig;
                Director.Attach(null, Lobby, staging, rig, input, Hud, audio, vfx, Game, speech, cutscene);
            }

            public void StartCinderSpanThroughLobbyCallback()
            {
                Button firstDescent = null;
                foreach (var button in Lobby.GetComponentsInChildren<Button>(true))
                {
                    var label = button.GetComponentInChildren<Text>();
                    if (label != null && label.text == "강하")
                    {
                        firstDescent = button;
                        break;
                    }
                }

                Assert.That(firstDescent, Is.Not.Null, "the lobby must expose the first stage's sortie action");
                Assert.That(firstDescent.interactable, Is.True,
                    "the initially available cinder-span sortie must be enterable");
                firstDescent.onClick.Invoke();
                Assert.That(Director.Current, Is.EqualTo(GameDirector.State.Dungeon),
                    "the first sortie callback must enter the dungeon route");
                Assert.That(((ICampaignSnapshot)Game.Sim).StageId, Is.EqualTo(StageCatalog.Entries[0].SimAnchorId),
                    "the first sortie callback must begin cinder-span's frozen Sim stage");
            }

            /// <summary>Exits a finished run the way the player does — the HUD's
            /// return-home action, which re-enters and refreshes the lobby.</summary>
            public void ReturnToLobbyThroughHud()
            {
                Assert.That(Hud.OnReturnHome, Is.Not.Null,
                    "GameDirector must wire the HUD return-home action");
                Hud.OnReturnHome.Invoke();
                Assert.That(Director.Current, Is.EqualTo(GameDirector.State.Lobby),
                    "returning home must re-enter the lobby route");
            }

            /// <summary>Arms the first stage card's 서약 toggle through its real
            /// button. The toggle is only built-visible on a cleared card, so a
            /// hidden button here means the reveal contract regressed.</summary>
            public void ArmPactOnFirstStageCard()
            {
                var toggle = FirstButtonLabelled("서약");
                Assert.That(toggle, Is.Not.Null, "a cleared stage card must expose its 서약 toggle");
                Assert.That(toggle.gameObject.activeInHierarchy, Is.True,
                    "the 서약 toggle must be revealed once the stage is cleared");
                toggle.onClick.Invoke();
                Assert.That(Lobby.IsPactArmed(StageCatalog.Entries[0].Id), Is.True,
                    "clicking the toggle must arm the stage's pact for the next sortie");
            }

            /// <summary>Re-enters the first stage through its 강하 button (a
            /// cleared stage stays replayable).</summary>
            public void StartFirstStageThroughDescentButton()
            {
                var descent = FirstButtonLabelled("강하");
                Assert.That(descent, Is.Not.Null, "the lobby must keep the first stage replayable");
                Assert.That(descent.interactable, Is.True, "a cleared stage must stay enterable");
                descent.onClick.Invoke();
                Assert.That(Director.Current, Is.EqualTo(GameDirector.State.Dungeon),
                    "the repeat sortie must enter the dungeon route");
            }

            private Button FirstButtonLabelled(string prefix)
            {
                foreach (var button in Lobby.GetComponentsInChildren<Button>(true))
                {
                    var label = button.GetComponentInChildren<Text>(true);
                    if (label != null && label.text.StartsWith(prefix, System.StringComparison.Ordinal))
                        return button;
                }
                return null;
            }

            public void Dispose()
            {
                if (_hadCampaign)
                    PlayerPrefs.SetString(CampaignKey, _campaignPayload);
                else
                    PlayerPrefs.DeleteKey(CampaignKey);
                if (_hadRotateHint)
                    PlayerPrefs.SetInt("al:rotate-hint", _rotateHint);
                else
                    PlayerPrefs.DeleteKey("al:rotate-hint");
                if (_hadReducedMotion)
                    PlayerPrefs.SetInt("al:reduced-motion", _reducedMotion);
                else
                    PlayerPrefs.DeleteKey("al:reduced-motion");
                PlayerPrefs.Save();

                var createdRoots = new List<GameObject>();
                foreach (var gameObject in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include,
                             FindObjectsSortMode.None))
                {
                    if (_existingGameObjects.Contains(gameObject) || gameObject.transform.parent != null)
                        continue;
                    createdRoots.Add(gameObject);
                }
                foreach (var root in createdRoots)
                    Object.DestroyImmediate(root);
            }

            private T AddChild<T>(string name) where T : Component
            {
                var child = new GameObject(name);
                child.transform.SetParent(_root.transform, false);
                return child.AddComponent<T>();
            }
        }
    }
}
