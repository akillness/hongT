// v0.2 single-scene state machine (spec §0): Lobby <-> Prologue/Dungeon/Arena.
// Owns persistence (CampaignStore is the ONLY writer of the campaign key),
// mode routing, camera/input profiles, and run lifecycle.
using CinderCourt.Sim;
using UnityEngine;

namespace CinderCourt.View
{
    public sealed class GameDirector : MonoBehaviour
    {
        public enum State { Lobby, Prologue, Dungeon, Arena, Training }

        GameBootstrap _bootstrap;
        LobbyView _lobby;
        LobbyStaging _staging;
        CameraRig _rig;
        InputAdapter _input;
        HudView _hud;
        AudioDirector _audio;
        VfxDirector _vfx;
        GameView _game;
        SpeechBubbleView _speech;
        CutsceneView _cutscene;
        IntroVideoView _intro;

        /// <summary>localStorage flag: the concept reel has played once on this
        /// browser. Deliberately not part of CampaignStore — it is a boot-route
        /// preference, not run progress, and wiping a campaign should not make
        /// the premise replay.</summary>
        const string ConceptSeenKey = "abyssal-lantern:cinder-court:concept-seen";


        State _state = State.Lobby;
        CampaignData _data;
        string _selectedStage = "cinder-span";
        string _runStageId = "";
        bool _runEndPersisted;
        // v1.3 M3: this run was started under an armed verdict pact (the flag
        // is latched at StartDungeon — the lobby toggle is session-only view
        // state and may change while a run is live).
        bool _runWasPact;
        GameObject _stageTerrain;         // instantiated Resources/Terrain prefab
        string _stageTerrainId = "";
        GameObject _stageDressing;        // instantiated dressing clones (view-only)
        string _stageDressingId = "";
        GameObject _stageEnvironment;     // EnvironmentBuilder root (AMENDMENT #12)
        string _stageEnvironmentId = "";
        GameObject _stageMood;            // StageMood rig (separate light root)
        string _emberRestNextStageId = "";
        PreparationOffer _emberRestPreparation;
        bool _emberRestDecisionMade;

        public State Current => _state;

        public void Attach(
            GameBootstrap bootstrap, LobbyView lobby, LobbyStaging staging,
            CameraRig rig, InputAdapter input, HudView hud,
            AudioDirector audio, VfxDirector vfx, GameView game,
            SpeechBubbleView speech, CutsceneView cutscene,
            IntroVideoView intro = null)

        {
            _bootstrap = bootstrap;
            _lobby = lobby;
            _staging = staging;
            _rig = rig;
            _input = input;
            _hud = hud;
            _audio = audio;
            _vfx = vfx;
            _game = game;
            _speech = speech;
            _cutscene = cutscene;
            _intro = intro;


            _data = CampaignStore.Load();
            _hud.OnReturnHome = ReturnToLobby;
            _hud.OnRetryStage = RetryStage;
            _input.OnDungeonRetryShortcut = TryRetryStageShortcut;
            _game.OnRunEvents = OnRunEvents;

            _hud.OnEmberRestOfferSelected = SelectEmberRestOffer;
            _hud.OnEmberRestDeferred = DeferEmberRest;
            _hud.OnEmberRestContinue = ContinueFromEmberRest;
            _hud.OnEmberRestReturnHome = ReturnFromEmberRest;
            // Marking happens on DISMISS, never on show: a card the player never
            // read must not be consumed (a run can end with one up).
            _hud.OnGuidanceDismissed = MarkGuidanceSeen;
            _hud.OnAbandonConfirmed = AbandonRun;
            _hud.AbandonRelicsAtRisk = () =>
                _game != null && _game.Sim != null ? _game.Sim.Relics : 0;
            // A PREDICATE, not the record. The codex is a re-read surface: if
            // it could reach CampaignData it could mark a bit, and browsing an
            // entry would suppress the pause card that entry was meant to
            // deliver. Reading cannot consume.
            _hud.CodexEntrySeen = bit => GuidanceCatalog.Seen(in _data, bit);
            var callbacks = new LobbyCallbacks
            {
                OnSortie = OnSortie,
                OnAllocateStat = OnAllocateStat,
                OnBuyEquip = OnBuyEquip,
                OnSelectCompanion = OnSelectCompanion,
                OnBuySigil = OnBuySigil,
                OnEquipSigil = OnEquipSigil,
                OnStartTrial = StartTraining,
            };
            _lobby.Build(_data, callbacks);

            // Boot route (spec §0): default Lobby; QA deep links preserved.
            var mode = WebGLStorage.QueryParam("mode");
            var stage = WebGLStorage.QueryParam("stage");

            // Brand reel replaces the plain engine loading screen; on a first
            // run the concept reel follows it so the premise lands before the
            // menu does. Both are a pure overlay (sorting 520) above the route
            // booting underneath, so no state is gated on either, and a skip
            // abandons the whole sequence rather than advancing a clip.
            //
            // The story beats are FIRST RUN ONLY. This route fires on every
            // boot with an empty mode, and ?intro=off is a QA deep link no
            // player will find, so unconditional extra clips would tax every
            // return visit. The flag is written at play time, not on
            // completion: someone who skips has seen enough, and a reload
            // mid-reel must not restart the premise.
            //
            // Three beats on a first run — logo, premise, threat. Boot is the
            // only place they can go: every other candidate (stage-entry
            // cutscene, boss entrance) sits over a sim that has already begun.
            if (_intro != null && string.IsNullOrEmpty(mode)
                && WebGLStorage.QueryParam("intro") != "off")
            {
                if (WebGLStorage.GetString(ConceptSeenKey) == "1")
                {
                    _intro.Play();
                }
                else
                {
                    // Watcher voice, second person, one sentence a beat — the
                    // same grammar as every StoryCatalog.StageStart line. The
                    // logo carries no caption: there is nothing to narrate over
                    // a wordmark, and a line there would read as a subtitle for
                    // the studio name.
                    _intro.PlaySequence(
                        new IntroVideoView.Beat(IntroVideoView.ClipRelativePath),
                        new IntroVideoView.Beat(IntroVideoView.ConceptClipRelativePath,
                            "등불 하나가 잿불의 법정을 건넙니다. 사슬은 아직 무엇도 놓지 않았습니다."),
                        new IntroVideoView.Beat(IntroVideoView.ThreatClipRelativePath,
                            "판결을 내린 자가 아직 그 자리에 서 있습니다. 등불을 들고 마주하세요."));
                    WebGLStorage.SetString(ConceptSeenKey, "1");
                }
                if (_audio != null) _audio.SetBgmContext("intro");   // W12
            }

            if (mode == "arena") StartArena();
            else if (mode == "prologue") StartPrologue();
            else if (mode == "training") StartTraining(TrialFromQuery(), TierFromQuery());
            else if (mode == "campaign" && IsStageUnlocked(stage)) StartDungeon(stage);
            else EnterLobby();

        }

        // ------------------------------------------------------------- lobby --
        // Act cinematic latched by a clear, played by EnterLobby. Null when the
        // cleared stage did not end an act.
        string _pendingActReel;
        string _pendingActNarration;

        /// <summary>Reel + watcher line for a stage that ENDS an act, null
        /// otherwise.
        ///
        /// The catalog is nine stages in three acts of three, so an act ends at
        /// CatalogIndex 2, 5 and 8. Derived from the index rather than a list
        /// of ids: a tenth stage appended to the catalog then simply extends
        /// the pattern instead of silently never firing.
        ///
        /// A first clear is not required. Replaying the last stage of an act
        /// plays its cinematic again — the alternative is a beat the player
        /// sees exactly once and cannot revisit, and the reel is skippable.</summary>
        static (string reel, string narration)? ActBeatFor(string stageId)
        {
            if (!StageCatalog.TryGet(stageId, out var entry)) return null;
            switch (entry.CatalogIndex)
            {
                case 2:
                    return (IntroVideoView.Act1ClipRelativePath,
                        "첫 세 재판이 끝났습니다. 사슬은 느슨해졌을 뿐, 끊어지지 않았습니다.");
                case 5:
                    return (IntroVideoView.Act2ClipRelativePath,
                        "판결은 당신을 향하지 않았습니다. 더 깊은 곳에서 명령이 이어집니다.");
                case 8:
                    return (IntroVideoView.Act3ClipRelativePath,
                        "행진이 멈췄습니다. 등불은 이제 당신의 손에서 다른 길을 밝힙니다.");
                default:
                    return null;
            }
        }

        void EnterLobby()
        {
            _state = State.Lobby;
            ClearEmberRestRoute();
            if (_audio != null)
            {
                // §4o: a surface that holds the run comes down when the run
                // does. EnterLobby is where death, abandon and clear all
                // converge (the bed swap below proves it), so stopping VO here
                // covers every exit without a per-path list that can rot.
                _audio.StopVoice();
                _audio.SetBgmContext("lobby");   // W12
            }
            if (_cutscene != null) _cutscene.Hide();   // no stale loading screen over the lobby
            SetStageTerrain(null);        // back to the base court plate
            ApplyStageDressing(null);
            SetStageEnvironment(null);
            _game.EndRun();
            _lobby.Refresh(_data);
            _lobby.Show();
            _staging.Attach(_bootstrap);   // idempotent (accent light reused)
            _staging.Show(_selectedStage, _data.Active);
            _rig.SetProfile(CameraRig.Profile.Lobby);
            _input.Mode = InputAdapter.Profile.Arena; // inert while lobby UI is up
            _hud.SetHudVisible(false);

            // Act cinematic, latched by the clear that ended an act. It rides
            // the same overlay as the boot reels (sorting 520) over a lobby
            // that has already settled, so a failed or missing clip costs the
            // beat and nothing else — IntroVideoView finishes immediately and
            // the player is already where they were going.
            if (_pendingActReel != null && _intro != null)
            {
                var reel = _pendingActReel;
                var line = _pendingActNarration;
                _pendingActReel = null;
                _pendingActNarration = null;
                _intro.PlaySequence(new IntroVideoView.Beat(reel, line));
            }
        }

        /// <summary>
        /// Dungeon ground swap: instantiate Resources/Terrain/terrain-<stage>
        /// at the arena center (court plate stays underneath as safety net).
        /// Pass null to return to the base court look.
        /// </summary>
        void SetStageTerrain(string stageId)
        {
            if (_stageTerrainId == (stageId ?? "")) return;
            if (_stageTerrain != null)
            {
                if (Application.isPlaying) Destroy(_stageTerrain);
                else DestroyImmediate(_stageTerrain);
                _stageTerrain = null;
            }
            _stageTerrainId = stageId ?? "";
            if (string.IsNullOrEmpty(stageId)) return;
            var prefab = Resources.Load<GameObject>("Terrain/terrain-" + stageId);
            if (prefab == null) return;   // stage without terrain keeps the court
            _stageTerrain = Instantiate(prefab);
            _stageTerrain.name = "StageTerrain";
            // Arena center in view space; terrain FBX is origin-centered, top y=0.
            _stageTerrain.transform.position = ViewWorld.ToWorld(768f, 512f, 0f);
        }

        /// <summary>
        /// Stage dressing pass (spec §Lane T-a): clone named children of the
        /// cinder-span library prefab at static sim-space placements. View-only,
        /// deterministic, runs once per stage change — never per frame. Pass
        /// null to clear (lobby/arena/prologue keep the bare court).
        /// </summary>
        void ApplyStageDressing(string stageId)
        {
            if (_stageDressingId == (stageId ?? "")) return;
            if (_stageDressing != null)
            {
                if (Application.isPlaying) Destroy(_stageDressing);
                else DestroyImmediate(_stageDressing);
                _stageDressing = null;
            }
            _stageDressingId = stageId ?? "";
            if (string.IsNullOrEmpty(stageId)) return;
            var table = StageCatalog.DressingFor(stageId);
            if (table == null || table.Length == 0) return;
            var library = Resources.Load<GameObject>(
                "Terrain/terrain-" + StageCatalog.DressingLibraryTerrainId);
            if (library == null) return;

            _stageDressing = new GameObject("StageDressing");
            for (var i = 0; i < table.Length; i++)
            {
                var placement = table[i];
                var source = library.transform.Find(placement.ObjectName);
                if (source == null) continue; // integrity test guards names

                // Terrain children keep their pivot at the prefab root; the
                // authored position lives in the BAKED mesh vertices. Anchor a
                // pivot at the target, clone under it with the authored pose,
                // then measure the LIVE renderer bounds (asset bounds are not
                // valid outside a scene) and counter the baked XZ offset so the
                // mesh center lands on the pivot and yaw/scale act about it.
                var pivot = new GameObject(placement.ObjectName + "-dressing");
                pivot.transform.SetParent(_stageDressing.transform, false);
                pivot.transform.SetPositionAndRotation(
                    ViewWorld.ToWorld(placement.SimX, placement.SimY, 0f),
                    Quaternion.Euler(0f, placement.RotationY, 0f));
                pivot.transform.localScale = Vector3.one * placement.Scale;

                var clone = Instantiate(source.gameObject, pivot.transform);
                clone.transform.localPosition = source.localPosition;
                clone.transform.localRotation = source.localRotation;
                clone.transform.localScale = source.localScale;

                var renderers = clone.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                var bounds = renderers[0].bounds;
                for (var r = 1; r < renderers.Length; r++) bounds.Encapsulate(renderers[r].bounds);
                var delta = pivot.transform.position - bounds.center;
                clone.transform.position += new Vector3(delta.x, 0f, delta.z);
            }
        }

        /// <summary>
        /// Modular tile environment (docs/SIM_SPEC_ENVIRONMENT.md AMENDMENT #12):
        /// Zone A floor accents + Zone B boundary ring/gates + Zone C outer
        /// verticality + §E6 lights, built deterministically ONCE at stage entry
        /// (same cadence as SetStageTerrain/ApplyStageDressing — never per
        /// frame). Pass null to clear (lobby/arena/prologue/training keep the
        /// bare court; the diamond-clamp modes are §E3 out of scope).
        /// </summary>
        void SetStageEnvironment(string stageId)
        {
            if (_stageEnvironmentId == (stageId ?? "")) return;
            if (_stageEnvironment != null)
            {
                if (Application.isPlaying) Destroy(_stageEnvironment);
                else DestroyImmediate(_stageEnvironment);
                _stageEnvironment = null;
            }
            if (_stageMood != null)
            {
                if (Application.isPlaying) Destroy(_stageMood);
                else DestroyImmediate(_stageMood);
                _stageMood = null;
                StageMood.Clear();      // RenderSettings is global
            }
            _stageEnvironmentId = stageId ?? "";
            if (string.IsNullOrEmpty(stageId))
            {
                // Lobby/arena/prologue/training keep the frozen diamond-clamp
                // playfield; clearing the environment restores it (AMENDMENT #15
                // is dungeon-only, exactly as the sim scopes it).
                if (_rig != null)
                    _rig.SetPlayfield(SimConfig.ArenaHalfWidth, SimConfig.ArenaHalfHeight);
                VfxDirector.SetPlayfield(SimConfig.ArenaHalfWidth, SimConfig.ArenaHalfHeight);
                PostFxGate.SetStageActive(false);   // §V4: post is dungeon-only
                return;
            }
            // AMENDMENT #15 (W-MV, MV-2): the boundary wall ring must be laid
            // out against the half-axes the SIM will clamp to, or an expanded
            // clamp puts the player outside the ring. This runs BEFORE
            // _game.Begin constructs the sim, so the value comes from the one
            // view-side source (GameView.DungeonProgression) rather than from a
            // snapshot that does not exist yet; both sides run it through
            // DungeonBoundsSpec.Resolve, so they cannot drift.
            GameView.DungeonPlayfield(out var halfWidth, out var halfHeight);
            _stageEnvironment = EnvironmentBuilder.Build(stageId, halfWidth, halfHeight);
            // MV-4/MV-6: the camera follow clamp and the ash-wall span are
            // derived from the same half-axes.
            if (_rig != null) _rig.SetPlayfield(halfWidth, halfHeight);
            VfxDirector.SetPlayfield(halfWidth, halfHeight);
            // §V4: bloom + vignette are dungeon-only and watchdogged from here on.
            PostFxGate.SetStageActive(true);
            // Atmosphere rig lives OUTSIDE the environment root: §E6 caps that
            // root at 4 realtime point lights.
            _stageMood = StageMood.Apply(stageId);
        }

        void ReturnToLobby() => EnterLobby();

        /// <summary>
        /// AMENDMENT #9 (negotiation entry 14) — leave a run in progress.
        ///
        /// Everything earned this run is forfeit: no relics, no clear bit, no
        /// roster merge. That is stricter than the genre (total forfeit is 2 of
        /// 16 surveyed titles; Loop Hero grades it 100/60/30%), and the reason
        /// is local rather than fashionable — DEFEAT already banks relics here
        /// (see the GameOver branch in OnRunEvents). Give abandonment the same
        /// or a graded payout and "walk in, grab the drops, walk out" becomes
        /// the safest way to farm, since it removes the death risk that is
        /// supposed to price those relics.
        ///
        /// No CampaignStore.Save call: forfeiting means the in-memory _data is
        /// already what the save on disk says. Writing would only risk
        /// persisting something else that changed mid-run.
        /// </summary>
        void AbandonRun()
        {
            // Any run state, not just Dungeon. The lobby has no abandon button,
            // so reaching here at all means a run is up; a state check would
            // only re-open the prologue trap this method exists to close.
            if (_state == State.Lobby) return;
            // Block the run-end handlers from firing on the way out — otherwise
            // teardown could still trip the GameOver branch and bank the relics
            // this method exists to forfeit.
            _runEndPersisted = true;
            EnterLobby();
        }

        void RetryStage()
        {
            if (_state == State.Dungeon && !string.IsNullOrEmpty(_runStageId))
            {
                StartDungeon(_runStageId);
                return;
            }
            _input.QueueRestart();
        }

        bool TryRetryStageShortcut()
        {
            if (_state != State.Dungeon || _game == null || _game.Sim == null ||
                _hud == null || !_hud.RetryModalVisible)
                return false;
            RetryStage();
            return true;
        }

        bool IsStageUnlocked(string stageId)
            => StageCatalog.TryGet(stageId, out var entry)
                && StageCatalog.IsUnlocked(in _data, in entry);

        // ------------------------------------------------------------ sorties --
        void OnSortie(string target)
        {
            if (target == "prologue") { StartPrologue(); return; }
            if (IsStageUnlocked(target)) StartDungeon(target);
        }

        void StartArena()
        {
            ClearEmberRestRoute();
            _state = State.Arena;
            if (_audio != null) _audio.SetBgmContext("stage");   // W12
            SetStageTerrain(null);
            ApplyStageDressing(null);
            SetStageEnvironment(null);
            PrepareRunUi();
            _input.Mode = InputAdapter.Profile.Arena;
            _rig.SetProfile(CameraRig.Profile.Arena);
            _game.Begin(HackConfig.Arena(), "", null);
        }

        int _prologueStep;
        float _prologueStepTimer;

        void StartPrologue()
        {
            ClearEmberRestRoute();
            _state = State.Prologue;
            SetStageTerrain(null);        // tutorial runs on the court plate
            ApplyStageDressing(null);
            SetStageEnvironment(null);
            PrepareRunUi();
            _input.Mode = InputAdapter.Profile.Prologue;
            _rig.SetProfile(CameraRig.Profile.Prologue);
            _game.Begin(HackConfig.Prologue(), "", null);
            _hud.SetPrologueMode(true);
            _prologueStep = 0;
            _prologueStepTimer = 0f;
            _hud.ShowPrologueToast(0);
            // Intro cutscene doubles as the prologue loading screen (spec §8/§1):
            // the pre-rendered lantern-court key art holds while the fresh sim
            // spins up underneath, then fades to reveal the tutorial court.
            _cutscene.Show("scene-intro", "PROLOGUE", "잿불의 법정",
                "등불을 들어라. 사슬이 무엇을 붙들고 있는지 확인할 시간이다.");
        }

        // --------------------------------------------------- training ground --
        int _trialIndex = -1;
        int _trialTier;

        static int TrialFromQuery()
        {
            int index = TrainingTrials.IndexOf(WebGLStorage.QueryParam("trial"));
            return index < 0 ? 0 : index;
        }

        static int TierFromQuery()
        {
            int tier = 0;
            int.TryParse(WebGLStorage.QueryParam("tier"), out tier);
            return tier < 0 ? 0 : (tier >= HackSpec.TrainingTiers ? HackSpec.TrainingTiers - 1 : tier);
        }

        /// <summary>
        /// Enter a trial (AMENDMENT #10). The first run of the game still gets the
        /// original three-wave prologue — a trial only replaces the REPEAT visit,
        /// so a new player's path and the prologue golden are both untouched.
        /// </summary>
        void StartTraining(int trialIndex, int tier)
        {
            if (!_data.PrologueDone)
            {
                StartPrologue();
                return;
            }
            if (trialIndex < 0 || trialIndex >= TrainingTrials.Ids.Length) return;

            var metaStats = MetaStats.Of(_data.Attack, _data.Vitality, _data.Swiftness);
            var equipTiers = EquipTiers.Of(_data.Weapon, _data.Lantern, _data.Cloak);
            if (!HackConfig.TryTraining(TrainingTrials.Ids[trialIndex], tier, metaStats, equipTiers,
                    out var config))
            {
                EnterLobby();
                return;
            }

            ClearEmberRestRoute();
            _state = State.Training;
            _trialIndex = trialIndex;
            _trialTier = tier;
            SetStageTerrain(null);
            ApplyStageDressing(null);
            SetStageEnvironment(null);
            PrepareRunUi();
            _input.Mode = InputAdapter.Profile.Dungeon;   // full kit: you practise with your tools
            _rig.SetProfile(CameraRig.Profile.Dungeon);
            _game.Begin(config, TrialDisplayName(trialIndex, tier), null);
        }

        static string TrialDisplayName(int trialIndex, int tier)
            => $"{LobbyView.TrialNames[trialIndex]} • {LobbyView.TierNames[tier]}";

        /// <summary>
        /// A trial survived to the clock records its tier and nothing else
        /// (AMENDMENT #10 • negotiation entry 7). The ONLY currency the training
        /// ground can ever pay is the one-time mastery grant, and only when every
        /// trial sits at the top tier — PM's band was "one-time <=2 relics,
        /// repeat payouts banned", and a trial spawns no enemies so there is no
        /// drop path either.
        /// </summary>
        void PersistTrialClear()
        {
            CampaignStore.RecordTrial(ref _data, _trialIndex, _trialTier);
            if (!_data.TrainingMasteryClaimed && CampaignStore.MasteryComplete(in _data))
            {
                _data.TrainingMasteryClaimed = true;
                _data.Relics += HackSpec.TrainingMasteryRelics;
            }
        }

        void StartDungeon(string stageId, PreparationOffer preparation = default)
        {
            if (!preparation.IsValid) ClearEmberRestRoute();
            if (!StageCatalog.TryGet(stageId, out var entry))
            {
                EnterLobby();
                return;
            }
            var metaStats = MetaStats.Of(_data.Attack, _data.Vitality, _data.Swiftness);
            var equipTiers = EquipTiers.Of(_data.Weapon, _data.Lantern, _data.Cloak);
            if (!HackConfig.TryDungeon(entry.SimAnchorId, metaStats, equipTiers, _data.ActiveSlots,
                    RosterMaskOf(_data.Roster), out var config))
            {

                EnterLobby();
                return;
            }
            // v1.3 M3: an armed verdict pact swaps in the pact table (base
            // placements + appended identity-gimmick extras) INSTEAD of the
            // override/anchor. Pact state is read from the lobby at sortie
            // time and latched for the whole run (retries included, as long
            // as the toggle stays armed). Everything downstream is the
            // ordinary fixed-table path — no RNG, no sim change.
            _runWasPact = _lobby.IsPactArmed(entry.Id);
            if (_runWasPact)
                config.Hazards = StageCatalog.PactFor(entry.Id);
            else if (entry.HazardOverride != null)
                config.Hazards = entry.HazardOverride;
            config.PreparationOffer = preparation;
            // AMENDMENT #6: the equipped loadout rides the same view-composed seam
            // as the pact table. Empty loadout = every pre-sigil constant.
            config.Sigils = SigilsOf(in _data);
            // AMENDMENT #11 §16: the selected tier rides the same view-composed seam.
            // Dungeon only on purpose — the arena run is the frozen contract and the
            // prologue is a tutorial, so both stay on Difficulty.Normal.
            config.Difficulty = ViewPrefs.Difficulty;

            _state = State.Dungeon;
            _runStageId = entry.Id;
            _runEndPersisted = false;
            SetStageTerrain(entry.TerrainId); // logical stage terrain can differ from its Sim anchor
            ApplyStageDressing(entry.Id);     // per-LOGICAL-stage dressing (spec §T-a)
            SetStageEnvironment(entry.Id);    // modular environment (AMENDMENT #12)
            PrepareRunUi();
            _input.Mode = InputAdapter.Profile.Dungeon;
            _rig.SetProfile(CameraRig.Profile.Dungeon);
            // "— 서약" HUD title suffix: the cheapest always-visible in-run
            // marker (Begin's stageDisplayName flows to the campaign HUD).
            _game.Begin(config,
                _runWasPact ? entry.DisplayName + " — 서약" : entry.DisplayName,
                _data.Active, entry.Id);

            // Stage-entry cutscene loading screen (spec §8): pick the pre-rendered
            // scene frame by context — a mid-campaign continuation from Ember Rest
            // rides the transition art, a Gate Sovereign stage opens on the boss
            // key art, everything else on the generic stage-entry frame. The
            // watcher's stageStart narration (frozen StoryCatalog) captions it.
            StoryCatalog.TryGet(entry.StoryKey, StoryCatalog.StageStart, out _, out var introNarration);
            var cutsceneSprite = preparation.IsValid
                ? "scene-transition"
                : entry.Boss.Visual == EnemyVisual.BossMonarch
                    ? "scene-boss-entry"
                    : "scene-stage-entry";
            // Per-stage frame when one has been authored, generic otherwise. All
            // nine stages used to collapse onto the three literals above, so the
            // loading screen said nothing about WHICH door you were opening —
            // the accent, the terrain and the boss archetype all stopped at the
            // catalog. CutsceneView already degrades to the dark backdrop on a
            // miss, but resolving here keeps the generic frame as the floor
            // instead of dropping to no art at all while the set fills in.
            cutsceneSprite = StageCutsceneSprite(cutsceneSprite, entry.Id);
            _cutscene.Show(cutsceneSprite, entry.Kicker, entry.Title, introNarration);
            // W12: the loading/story frame rides its own bed, then the stage
            // track takes over when the cutscene yields to live play (the
            // same-clip no-op in SetBgmContext makes the retry path safe).
            if (_audio != null) _audio.SetBgmContext("stage");

            if (StoryCatalog.TryGet(entry.StoryKey, StoryCatalog.StageStart, out var speaker, out var text))
            {
                // The watcher's opening is the one story beat that does NOT go
                // through DispatchStory — it fires here, over the cutscene, so
                // its VO has to be cued here too or the most 연출-heavy moment
                // in the run is the only silent one. VO first: its length sets
                // the bubble hold (see VoiceHold).
                var hold = VoiceHold(entry.StoryKey, StoryCatalog.StageStart);
                _speech.Show(speaker, text, ViewWorld.ToWorld(768f, 500f, 1.4f), hold);
            }
        }

        // Cached per stage id: Resources.Load on a miss is not free, and this
        // runs on every sortie including retries. Empty string = "no per-stage
        // frame authored", which is the common case until the set is complete.
        readonly System.Collections.Generic.Dictionary<string, string> _stageCutsceneCache =
            new System.Collections.Generic.Dictionary<string, string>(9);

        /// <summary>
        /// `<c>generic</c>-<c>stageId</c>` when that sprite exists, otherwise
        /// <paramref name="generic"/>. Authoring a new frame is therefore a
        /// drop-in: no code change, no catalog entry — the file appearing under
        /// Resources/Scenes is the whole opt-in.
        /// </summary>
        internal string StageCutsceneSprite(string generic, string stageId)
        {
            if (string.IsNullOrEmpty(stageId)) return generic;
            var key = generic + "-" + stageId;
            if (!_stageCutsceneCache.TryGetValue(key, out var resolved))
            {
                resolved = Resources.Load<Sprite>("Scenes/" + key) != null ? key : generic;
                _stageCutsceneCache[key] = resolved;
            }
            return resolved;
        }

        void PrepareRunUi()
        {
            _lobby.Hide();
            _staging.Hide();
            _hud.ResetRunUi();             // clears interrupted ceremony timers on every entry/retry
            _hud.SetPrologueMode(false);   // every run resets; prologue re-enables
            _hud.SetHudVisible(true);
            // AMENDMENT #9: the way out, in EVERY run mode.
            //
            // The first draft gated this to dungeons, reasoning that "the
            // prologue ends on its own in three waves and a trial is a 60 s
            // clock, so neither can trap anyone". That reasoning assumed the
            // player SURVIVES. A user opened the prologue, died, and got a
            // panel with exactly one button — 재강하 — because the campaign
            // back-link is added by EnableCampaignUi, which only runs for
            // dungeon presentation. Retry, die, retry: a closed loop.
            //
            // "It ends on its own" is a statement about the winning path only.
            // Every mode gets the exit.
            _hud.SetLeftStackAvailable(_state == State.Dungeon
                || _state == State.Prologue
                || _state == State.Training
                || _state == State.Arena);
            // A fresh run re-scans its hazard table; without this the second
            // visit to a stage would skip hazards the player has not met yet
            // (a pact table adds gimmicks the base table never had).
            _guidanceScannedStage = null;
            _guidanceQueue.Clear();
        }

        static int EmberRestSeedFor(in StageEntry entry) => entry.CatalogIndex;

        bool HasDirectEmberRestSuccessor(out StageEntry current, out StageEntry successor)
        {
            successor = default;
            if (!StageCatalog.TryGet(_runStageId, out current)) return false;
            var successorIndex = current.CatalogIndex + 1;
            if (successorIndex >= StageCatalog.Entries.Count) return false;
            successor = StageCatalog.Entries[successorIndex];
            return successor.CatalogIndex == successorIndex;
        }

        void BeginEmberRest()
        {
            if (!HasDirectEmberRestSuccessor(out var current, out var successor) ||
                !_game.BeginEmberRest(current.CatalogIndex + 1, EmberRestSeedFor(in current)))
                return;
            _emberRestNextStageId = successor.Id;
            _emberRestPreparation = default;
            _emberRestDecisionMade = false;
            _hud.ShowEmberRest(_game.EmberRestSnapshot);
        }

        bool SelectEmberRestOffer(int offerIndex)
        {
            if (string.IsNullOrEmpty(_emberRestNextStageId) ||
                !_game.TrySelectEmberRestOffer(offerIndex))
                return false;
            _emberRestPreparation = _game.SelectedEmberRestPreparation;
            _emberRestDecisionMade = true;
            return true;
        }

        bool DeferEmberRest()
        {
            if (string.IsNullOrEmpty(_emberRestNextStageId) || !_game.DeferEmberRest())
                return false;
            _emberRestPreparation = default;
            _emberRestDecisionMade = true;
            return true;
        }

        void ContinueFromEmberRest()
        {
            if (!_emberRestDecisionMade || string.IsNullOrEmpty(_emberRestNextStageId) ||
                !_game.EndEmberRest())
                return;
            var nextStageId = _emberRestNextStageId;
            var preparation = _emberRestPreparation;
            _emberRestNextStageId = "";
            _emberRestPreparation = default;
            _emberRestDecisionMade = false;
            _hud.HideEmberRest();
            StartDungeon(nextStageId, preparation);
        }

        void ReturnFromEmberRest()
        {
            ReturnToLobby();
        }

        void ClearEmberRestRoute()
        {
            _emberRestNextStageId = "";
            _emberRestPreparation = default;
            _emberRestDecisionMade = false;
            _hud.HideEmberRest();
        }


        static int RosterMaskOf(string[] roster)
        {
            var mask = 0;
            if (roster == null) return 0;
            for (var i = 0; i < roster.Length; i++)
            {
                var id = roster[i];
                if (!id.EndsWith("-echo")) continue;
                var baseId = id.Substring(0, id.Length - "-echo".Length);
                var visual = baseId switch
                {
                    "ember-cohort" => (int)EnemyVisual.EmberCohort,
                    "scout" => (int)EnemyVisual.Scout,
                    "shade" => (int)EnemyVisual.Shade,
                    "possessed" => (int)EnemyVisual.Possessed,
                    _ => -1,
                };
                if (visual >= 0) mask |= 1 << visual;
            }
            return mask;
        }

        // -------------------------------------------------------- lobby intents --
        void OnAllocateStat(string stat)
        {
            if (_data.Points <= 0) return;
            switch (stat)
            {
                case "attack" when _data.Attack < 10: _data.Attack++; break;
                case "vitality" when _data.Vitality < 10: _data.Vitality++; break;
                case "swiftness" when _data.Swiftness < 10: _data.Swiftness++; break;
                default: return;
            }
            _data.Points--;
            CampaignStore.Save(in _data);
            _lobby.Refresh(_data);
        }

        // AMENDMENT #8 x main: the second copy of the price table is gone and the
        // purchase is a testable pure function. Both sides removed the same
        // duplication and chose different halves — main extracted the LOGIC into
        // TryBuyEquip (LobbyEconomyTests covers it seven ways), AMENDMENT #8 moved
        // the DATA to ProgressionGuide, where LobbyView's buy line already reads
        // it. Taking one and dropping the other would restore the duplicate this
        // alias exists to kill: the two tables agreed by luck, not construction.
        // internal: LobbyEconomyTests pins the purchase against this table, and
        // a price the tests cannot see is a price nothing defends.
        internal static readonly int[] EquipCosts = ProgressionGuide.EquipCosts;

        void OnBuyEquip(string slot)
        {
            if (!TryBuyEquip(ref _data, slot)) return;
            CampaignStore.Save(in _data);
            _lobby.Refresh(_data);
        }

        /// <summary>
        /// Pure purchase judgment + data mutation (audit M9 T-3 seam): tier
        /// lookup, tier-5 cap, cost ladder, balance check, relic debit, tier
        /// increment. NO side effects — persistence (CampaignStore.Save) and UI
        /// refresh stay with the caller so tests never touch PlayerPrefs.
        /// Unknown slot strings intentionally keep the historical behavior of
        /// both original switches (`_ =>` / `default:`): they buy CLOAK.
        /// </summary>
        internal static bool TryBuyEquip(ref CampaignData data, string slot)
        {
            var tier = slot switch
            {
                "weapon" => data.Weapon,
                "lantern" => data.Lantern,
                _ => data.Cloak,
            };
            // Derived, not literal: EquipCap is EquipCosts.Length, so a sixth
            // price cannot disagree with the cap that guards the index below.
            if (tier >= ProgressionGuide.EquipCap) return false;
            var cost = EquipCosts[tier];
            if (data.Relics < cost) return false;
            data.Relics -= cost;
            switch (slot)
            {
                case "weapon": data.Weapon++; break;
                case "lantern": data.Lantern++; break;
                default: data.Cloak++; break;
            }
            return true;
        }

        /// <summary>
        /// AMENDMENT #6 (D6.6): the legion tab toggles membership instead of
        /// replacing a single slot. "" (the "없음" button) clears every slot;
        /// clicking an already-active companion removes it; clicking a new
        /// one appends it while under the 3-slot cap and is a no-op at cap.
        /// </summary>
        void OnSelectCompanion(string id)
        {
            id = id ?? "";
            var slots = _data.ActiveSlots ?? System.Array.Empty<string>();
            if (string.IsNullOrEmpty(id))
            {
                slots = System.Array.Empty<string>();
            }
            else
            {
                var index = System.Array.IndexOf(slots, id);
                if (index >= 0)
                {
                    var shrunk = new string[slots.Length - 1];
                    for (int i = 0, w = 0; i < slots.Length; i++)
                        if (i != index) shrunk[w++] = slots[i];
                    slots = shrunk;
                }
                else if (slots.Length < 3)
                {
                    var grown = new string[slots.Length + 1];
                    System.Array.Copy(slots, grown, slots.Length);
                    grown[slots.Length] = id;
                    slots = grown;
                }
                // else: 3 slots already active — click is a no-op.
            }
            _data.ActiveSlots = slots;
            _data.Active = slots.Length > 0 ? slots[0] : "";
            CampaignStore.Save(in _data);
            _lobby.Refresh(_data);
            _staging.Show(_selectedStage, _data.Active);
        }

        /// <summary>
        /// Unlocks a sigil (AMENDMENT #6). One-time relic spend; the FACE stays
        /// free to flip forever, which is the survey's anti-lock-in rule.
        /// </summary>
        void OnBuySigil(int kind)
        {
            if (kind <= 0) return;
            var bit = 1 << kind;
            if ((_data.SigilsOwned & bit) != 0) return;
            if (_data.Relics < LobbyView.SigilCost) return;
            _data.Relics -= LobbyView.SigilCost;
            _data.SigilsOwned |= bit;
            CampaignStore.Save(in _data);
            _lobby.Refresh(_data);
        }

        /// <summary>
        /// Equip, flip or unequip. Pressing the face already lit removes the sigil;
        /// pressing the other face swaps to it. With both slots full the OLDEST
        /// (slot 0) is evicted — a full loadout must never swallow the tap silently.
        /// </summary>
        void OnEquipSigil(int kind, int face)
        {
            if (kind <= 0 || (_data.SigilsOwned & (1 << kind)) == 0) return;

            var faceBit = 1 << kind;
            var wantsB = face == 1;
            var showingB = (_data.SigilFaces & faceBit) != 0;
            var slotted = _data.SigilSlot0 == kind || _data.SigilSlot1 == kind;

            if (slotted && showingB == wantsB)
            {
                // Same face pressed again → take it off.
                if (_data.SigilSlot0 == kind) _data.SigilSlot0 = 0;
                if (_data.SigilSlot1 == kind) _data.SigilSlot1 = 0;
            }
            else
            {
                if (wantsB) _data.SigilFaces |= faceBit;
                else _data.SigilFaces &= ~faceBit;
                if (!slotted)
                {
                    if (_data.SigilSlot0 == 0) _data.SigilSlot0 = kind;
                    else if (_data.SigilSlot1 == 0) _data.SigilSlot1 = kind;
                    else { _data.SigilSlot0 = _data.SigilSlot1; _data.SigilSlot1 = kind; }
                }
            }

            CampaignStore.Save(in _data);
            _lobby.Refresh(_data);
        }

        /// <summary>Persisted slots/faces as the sim's loadout struct.</summary>
        SigilLoadout SigilsOf(in CampaignData data)
        {
            return SigilLoadout.Of(
                (SigilKind)data.SigilSlot0,
                (data.SigilFaces & (1 << data.SigilSlot0)) != 0 ? SigilFace.B : SigilFace.A,
                (SigilKind)data.SigilSlot1,
                (data.SigilFaces & (1 << data.SigilSlot1)) != 0 ? SigilFace.B : SigilFace.A);
        }

        // ------------------------------------------------------------ run events --
        void OnRunEvents(SimEvents events, ICinderSim sim)
        {
            if (_state == State.Dungeon)
                DispatchStory(events, sim);

            PumpGuidance(events, sim);

            if ((events & SimEvents.StageCleared) != 0 && !_runEndPersisted)
            {
                _runEndPersisted = true;
                var shouldBeginEmberRest = false;
                if (_state == State.Prologue)
                {
                    _data.PrologueDone = true;
                    // The prologue's own four-step toast already taught movement
                    // and striking, so mark those entries delivered rather than
                    // repeating them the first time the player enters a dungeon.
                    // A lesson told twice teaches the player that the guidance
                    // is not tracking what they know.
                    GuidanceCatalog.MarkSeen(ref _data, GuidanceCatalog.IndexOf("이동"));
                    GuidanceCatalog.MarkSeen(ref _data, GuidanceCatalog.IndexOf("연격"));
                    // The 2D->2.5D reveal (spec §1): 2.2 s camera sweep, then lobby.
                    _hud.HidePrologueToast();
                    _rig.SetProfile(CameraRig.Profile.PrologueReveal);
                    _revealReturnTimer = 2.6f;
                }
                else if (_state == State.Dungeon)
                {
                    PersistDungeonClear(sim);
                    shouldBeginEmberRest = HasDirectEmberRestSuccessor(out _, out _);
                    // An act ends every third stage. Latch it here — where the
                    // clear is known — and PLAY it in EnterLobby, which is
                    // after _game.EndRun() and the one moment on this route
                    // with no sim running underneath. Playing it now would put
                    // a five-second overlay over the victory card and a live
                    // scene, the same reason the boot reels are on the boot
                    // route and not on a stage entry.
                    var actBeat = ActBeatFor(_runStageId);
                    _pendingActReel = actBeat?.reel;
                    _pendingActNarration = actBeat?.narration;
                }
                else if (_state == State.Training)
                {
                    PersistTrialClear();
                }
                CampaignStore.Save(in _data);
                _lobby.Refresh(_data);
                if (shouldBeginEmberRest) BeginEmberRest();
            }
            if ((events & SimEvents.GameOver) != 0 && !_runEndPersisted && _state == State.Dungeon)
            {
                // Defeat: relics earned mid-run still bank (meta currency),
                // equipment keeps the pre-run baseline (spec §3/§6 contract).
                _runEndPersisted = true;
                var hack = sim as IHackSnapshot;
                if (hack != null)
                {
                    _data.Relics += sim.Relics;
                    MergeRoster(hack.RosterMask);
                }
                CampaignStore.Save(in _data);
            }
            if ((events & SimEvents.WaveStarted) != 0)
                _runEndPersisted = false;
        }

        // ============================================ AMENDMENT #9 guidance ==
        // Triggers. Everything here is edge-driven off sim events or a one-shot
        // stage scan — nothing polls, and nothing runs once an entry is seen.
        //
        // Order matters: the pause queue is drained one card at a time, because
        // two cards at once would stack modals and the second would be dismissed
        // by the keypress that closed the first.
        readonly System.Collections.Generic.List<int> _guidanceQueue =
            new System.Collections.Generic.List<int>(8);
        string _guidanceScannedStage = null;

        /// <summary>Queues an entry if it has never been shown. Deduplicates
        /// against the queue as well as the save, so a hazard appearing twice in
        /// one table cannot enqueue itself twice.</summary>
        void QueueGuidance(int bit)
        {
            if (bit < 0 || GuidanceCatalog.Seen(in _data, bit)) return;
            if (_guidanceQueue.Contains(bit)) return;
            _guidanceQueue.Add(bit);
        }

        /// <summary>
        /// One-shot scan when a stage starts: every hazard kind present in the
        /// table the run will actually use. Reading the table rather than
        /// waiting for a hazard to hurt the player is deliberate — the vent
        /// lesson is worthless after the vent has already landed.
        /// </summary>
        void ScanStageGuidance(string stageId, ICinderSim sim)
        {
            if (_guidanceScannedStage == stageId) return;
            // Hazards live on the campaign surface, not the base sim. An arena
            // run has none, and a cast that fails means there is nothing to
            // teach — mark the stage scanned either way so this never retries.
            _guidanceScannedStage = stageId;
            if (!(sim is ICampaignSnapshot campaign)) return;
            var hazards = campaign.Hazards;
            if (hazards == null) return;
            for (var i = 0; i < hazards.Count; i++)
                QueueGuidance(GuidanceCatalog.BitForHazard(hazards[i].Kind));
        }

        void PumpGuidance(SimEvents events, ICinderSim sim)
        {
            // Trials are the practice mode — a player entering one has chosen
            // the gimmick deliberately, and its lesson is on the lobby card that
            // sent them there. Pausing again would be the third telling.
            if (_state == State.Dungeon)
            {
                ScanStageGuidance(_runStageId, sim);

                if ((events & SimEvents.PickupCollected) != 0)
                    QueuePickupGuidance(sim);
                if ((events & SimEvents.PerilOpened) != 0)
                    QueueGuidance(GuidanceCatalog.PerilBit);
                if ((events & SimEvents.SurgeOpened) != 0)
                    QueueGuidance(GuidanceCatalog.SurgeBit);
                // Win and lose are taught BEFORE they can be experienced.
                //
                // Victory rides BossSpawned: the boss wave is the last moment
                // "clear the waves, then the boss" is still actionable advice
                // rather than a description of what already happened.
                //
                // Defeat rides the FIRST time the player is hit, not GameOver.
                // On GameOver the run is over and ResetRunUi tears the card down
                // (it has to — a card up at timeScale 0 with no run left is a
                // hard freeze), so a lesson queued there would never be read.
                // First damage is the moment the health bar starts meaning
                // something, which is exactly when the rule is worth knowing.
                if ((events & SimEvents.BossSpawned) != 0)
                    QueueGuidance(GuidanceCatalog.VictoryBit);
                if ((events & SimEvents.PlayerDamaged) != 0)
                    QueueGuidance(GuidanceCatalog.DefeatBit);
                QueueAffordableSkillGuidance(sim);
            }

            DrainGuidanceQueue();
        }

        /// <summary>Queues the entry for whatever was just picked up. Scans the
        /// live pickup list for kinds not yet seen rather than trusting an event
        /// payload the sim does not carry.</summary>
        void QueuePickupGuidance(ICinderSim sim)
        {
            var pickups = sim.Pickups;
            if (pickups == null) return;
            for (var i = 0; i < pickups.Count; i++)
                QueueGuidance(GuidanceCatalog.BitForPickup(pickups[i].Kind));
        }

        /// <summary>
        /// Teaches a skill the first frame the player can actually cast it.
        ///
        /// This is the survey's G7 (progressive teaching, 12/19): a skill
        /// explained before there is oil to cast it is a fact, and a skill
        /// explained the moment it lights up is an instruction. Cheap enough to
        /// run per event — five int compares that stop entirely once the bits
        /// are set, and Seen() short-circuits inside QueueGuidance.
        /// </summary>
        void QueueAffordableSkillGuidance(ICinderSim sim)
        {
            var charge = sim.Charge;
            // Combo needs no oil — it is the first thing available, so it goes
            // out the moment the player is in a dungeon at all. Movement rides
            // along for the same reason: the prologue normally teaches it and
            // marks the bit, but a save that arrives with PrologueDone already
            // true (deep link, imported save, a future skip button) never ran
            // that path, and "how do I walk" is not a lesson to leave to chance.
            QueueGuidance(GuidanceCatalog.FirstControlBit);
            QueueGuidance(GuidanceCatalog.IndexOf("연격"));
            if (charge >= HackSpec.DashCost) QueueGuidance(GuidanceCatalog.IndexOf("질주"));
            if (charge >= HackSpec.BoltCost) QueueGuidance(GuidanceCatalog.IndexOf("균열 화살"));
            if (charge >= HackSpec.PulseCost) QueueGuidance(GuidanceCatalog.IndexOf("묘지 파동"));
            if (charge >= HackSpec.AegisCost) QueueGuidance(GuidanceCatalog.IndexOf("공허 방패"));
            if (charge >= HackSpec.AshNovaCost) QueueGuidance(GuidanceCatalog.IndexOf("잿불 노바"));
            // Companion orders only exist when one was actually taken along.
            // Read from the lobby's active slot, not the sim: the sim exposes a
            // companion position and behaviour but no "is there one" predicate,
            // and CompanionX defaults to 0 rather than being absent.
            if (!string.IsNullOrEmpty(_data.Active))
            {
                QueueGuidance(GuidanceCatalog.IndexOf("동료 대기"));
                QueueGuidance(GuidanceCatalog.IndexOf("동료 호출"));
            }
        }

        /// <summary>Shows the next queued card, one at a time. Toast-tier
        /// entries never pause; they mark themselves seen immediately.</summary>
        void DrainGuidanceQueue()
        {
            if (_guidanceQueue.Count == 0 || _hud == null || _hud.GuidancePaused) return;
            var bit = _guidanceQueue[0];
            _guidanceQueue.RemoveAt(0);
            if (GuidanceCatalog.Seen(in _data, bit)) return;

            var entry = GuidanceCatalog.Entries[bit];
            var body = entry.BodyFor(_hud.TouchActive);
            if (entry.Tier == GuidanceTier.Pause)
            {
                if (_hud.ShowGuidancePause(bit, GuidanceKicker(entry.Group), entry.Title, body))
                    return;   // OnGuidanceDismissed marks and saves
                _guidanceQueue.Insert(0, bit);   // card busy — retry next event
                return;
            }
            _hud.ShowGuidanceToast(entry.Title, body);
            MarkGuidanceSeen(bit);
        }

        static string GuidanceKicker(GuidanceGroup group)
        {
            switch (group)
            {
                case GuidanceGroup.Hazard: return "HAZARD";
                case GuidanceGroup.Outcome: return "OUTCOME";
                case GuidanceGroup.Pickup: return "PICKUP";
                case GuidanceGroup.Surge: return "SURGE";
                default: return "CONTROL";
            }
        }

        /// <summary>Marks a lesson delivered and persists it. Called on dismiss
        /// for pause cards and immediately for toasts — never on queueing, so a
        /// card the player never saw is never consumed.</summary>
        void MarkGuidanceSeen(int bit)
        {
            if (!GuidanceCatalog.MarkSeen(ref _data, bit)) return;
            CampaignStore.Save(in _data);
        }

        /// <summary>v1.3 M3 (negotiation-record entry 5, signed): a pact clear
        /// grants sim.Relics × this. VIEW-side payout — the sim's relic count
        /// is untouched. Internal so the EditMode economy test pins it.</summary>
        internal const int PactRelicMultiplier = 2;

        void PersistDungeonClear(ICinderSim sim)
        {
            BankDungeonClear(ref _data, _runStageId, _runWasPact, sim);
        }

        /// <summary>Single settlement authority shared by the live callback and
        /// deterministic economy evidence. It mutates only persisted campaign
        /// data; the simulation snapshot remains read-only.</summary>
        internal static void BankDungeonClear(
            ref CampaignData data,
            string stageId,
            bool pact,
            ICinderSim sim)
        {
            if (!StageCatalog.TryGet(stageId, out var entry)) return;
            StageCatalog.MarkCleared(ref data, in entry, out var firstClear);
            // Stat points: +2 per clear, +1 first boss kill (spec §5).
            data.Points += firstClear ? 3 : 2;
            // v1.3 M3 (entry 5): pact clear pays sim.Relics × 2 — the ONLY
            // doubled term. First-clear bonus stays single (agreed 비중복);
            // in practice the pact toggle only exists on cleared cards, so a
            // pact run's firstClear is false unless a QA deep link races the
            // toggle — the bonus line below stays independent either way.
            data.Relics += pact ? sim.Relics * PactRelicMultiplier : sim.Relics;
            // Cycle-2 first-clear relic bonus (negotiation-record entry 1,
            // signed designer+pm): view-side grant, sim untouched. One-time —
            // gated on firstClear like the companion reward below.
            if (firstClear) data.Relics += FirstClearRelicBonus(entry.Id);

            // Equipment ranks earned in-run become the new baseline (§6 path a).
            var campaign = sim as ICampaignSnapshot;
            if (campaign != null)
            {
                data.Weapon = Mathf.Max(data.Weapon, campaign.WeaponRank);
                data.Lantern = Mathf.Max(data.Lantern, campaign.LanternRank);
                data.Cloak = Mathf.Max(data.Cloak, campaign.CloakRank);
            }

            // Only base stages retain their established companion rewards.
            if (firstClear && !string.IsNullOrEmpty(entry.CompanionReward))
            {
                AddToRoster(ref data, entry.CompanionReward);
                if (data.ActiveSlots == null || data.ActiveSlots.Length == 0)
                {
                    data.Active = entry.CompanionReward;
                    data.ActiveSlots = new[] { entry.CompanionReward };
                }

            }

            var hack = sim as IHackSnapshot;
            if (hack != null) MergeRoster(ref data, hack.RosterMask);
        }

        /// <summary>
        /// One-time relic grant per stage id (negotiation-record entry 1:
        /// +6/+8/+10 sluice/bastion/march, first clear only; agreed cap
        /// rationale: 합산 +24 = T4 비용 2.2배 억제). Original six stages
        /// grant 0 — their economy predates the entry and stays untouched.
        /// </summary>
        static int FirstClearRelicBonus(string stageId)
        {
            switch (stageId)
            {
                case "cinder-sluice": return 6;
                case "ember-bastion": return 8;
                case "ash-march": return 10;
                default: return 0;
            }
        }

        void MergeRoster(int rosterMask)
        {
            MergeRoster(ref _data, rosterMask);
        }

        static void MergeRoster(ref CampaignData data, int rosterMask)
        {
            for (var visual = 0; visual < 4; visual++)
            {
                if ((rosterMask & (1 << visual)) == 0) continue;
                var baseId = visual switch
                {
                    (int)EnemyVisual.Scout => "scout",
                    (int)EnemyVisual.Shade => "shade",
                    (int)EnemyVisual.Possessed => "possessed",
                    _ => "ember-cohort",
                };
                AddToRoster(ref data, baseId + "-echo");
            }
        }

        void AddToRoster(string id)
        {
            AddToRoster(ref _data, id);
        }

        static void AddToRoster(ref CampaignData data, string id)
        {
            var roster = data.Roster ?? new string[0];
            for (var i = 0; i < roster.Length; i++)
                if (roster[i] == id) return;
            var grown = new string[roster.Length + 1];
            for (var i = 0; i < roster.Length; i++) grown[i] = roster[i];
            grown[roster.Length] = id;
            data.Roster = grown;
        }

        // ------------------------------------------------------------- story --
        // VO (2026-08-09 amendment) rides ALONGSIDE the existing text, never
        // instead of it: every PlayVoice below sits next to the _speech.Show
        // that already puts the same line on screen, so a build with no VO
        // assets is exactly the build that shipped yesterday.
        //
        // Keys are stage+beat, not beat alone. A generic "bossEntry" clip would
        // speak one stage's line over another stage's subtitle — the two
        // sources would contradict each other on screen (§4i). Composing the
        // key means an unrecorded beat resolves to a missing clip and stays
        // silent, which is the additive contract, while a recorded one always
        // matches the text beside it.
        static string VoiceKey(string storyKey, string beatKind)
            => storyKey + "-" + beatKind;

        /// <summary>
        /// Cue a beat's narration and report how long the bubble must stay up.
        /// SpeechBubbleView's own hold is paced for READING (~17 chars/s); the
        /// TTS speaks at ~7, so an unadjusted bubble vanishes up to 1.74 s
        /// before its own voice finishes [MEASURED, docs/provenance/voice.json].
        /// Returns 0 when nothing plays, which is exactly the value Show treats
        /// as "use your own formula" — so a build with no VO behaves as before.
        /// </summary>
        float VoiceHold(string storyKey, string beatKind)
            => _audio == null ? 0f : _audio.PlayVoice(VoiceKey(storyKey, beatKind));

        void DispatchStory(SimEvents events, ICinderSim sim)
        {
            var storyKey = StageCatalog.TryGet(_runStageId, out var stage)
                ? stage.StoryKey
                : _runStageId;
            if ((events & SimEvents.BossSpawned) != 0)
            {
                var bossAnchor = BossAnchor(sim);
                if (StoryCatalog.TryGet(storyKey, StoryCatalog.BossEntry,
                        out var entrySpeaker, out var entryText))
                {
                    // VO first: its length decides the bubble's hold.
                    var hold = VoiceHold(storyKey, StoryCatalog.BossEntry);
                    _speech.Show(entrySpeaker, entryText, bossAnchor, hold);
                    // §캡처5: speaker-prefixed screen subtitle doubles the boss
                    // beat (bubble stays the in-world grammar).
                    _hud.ShowSpeakerLine(entrySpeaker, entryText);
                }
                _hud.ShowBossIntro(GameView.BossNameFor(_runStageId));
                _rig.FocusPulse(bossAnchor, 0.45f);
            }
            if ((events & SimEvents.BossPhase2) != 0)
            {
                // BossPhase2 fires on EVERY phase boundary (P1->P2 and P2->P3;
                // the frozen SimEvents surface has no separate P3 bit). WHICH
                // phase is read from the snapshot, per the sim's own contract
                // (CinderSim.UpdateBossPhase): spec §8 L154 wants a DIFFERENT
                // last-warning line at 20% HP, so branch on BossPhase >= 3.
                var phaseBeat = sim is IHackSnapshot snap && snap.BossPhase >= 3
                    ? StoryCatalog.BossPhase3
                    : StoryCatalog.BossPhase2;
                if (StoryCatalog.TryGet(storyKey, phaseBeat, out var phaseSpeaker, out var phaseText))
                {
                    var hold = VoiceHold(storyKey, phaseBeat);
                    _speech.Show(phaseSpeaker, phaseText, BossAnchor(sim), hold);
                    _hud.ShowSpeakerLine(phaseSpeaker, phaseText);
                }
            }
            if ((events & SimEvents.StageCleared) != 0 &&
                StoryCatalog.TryGet(storyKey, StoryCatalog.Completion, out var doneSpeaker, out var doneText))
            {
                var hold = VoiceHold(storyKey, StoryCatalog.Completion);
                _speech.Show(doneSpeaker, doneText,
                    ViewWorld.ToWorld(sim.Player.X, sim.Player.Y, 1.6f), hold);
            }
        }

        static Vector3 BossAnchor(ICinderSim sim)
        {
            var enemies = sim.Enemies;
            for (var i = 0; i < enemies.Count; i++)
                if (enemies[i].IsBoss && !enemies[i].Dead)
                    return ViewWorld.ToWorld(enemies[i].X, enemies[i].Y, 2.4f);
            return ViewWorld.ToWorld(768f, 420f, 2.2f);
        }

        float _revealReturnTimer;

        void Update()
        {
            // Track the boss anchor while its bubble is live (spec §8).
            if (_speech != null && _speech.Active && _game != null && _game.Sim != null
                && _state == State.Dungeon)
                _speech.Track(BossAnchor(_game.Sim));

            // Dungeon crowd camera tier (spec §10).
            if (_state == State.Dungeon && _game != null && _game.Sim != null && _rig != null)
            {
                var sim = _game.Sim;
                var hack = sim as IHackSnapshot;
                var bigWave = sim.LivingEnemies >= 10 || (hack != null && hack.BossHp > 0f);
                _rig.SetDungeonCrowd(bigWave);
                // Player-follow framing (2026-10): the dungeon camera tracks
                // the warden instead of sitting on the arena centre. CameraRig
                // clamps + smooths; this only supplies the live world point.
                _rig.SetFollowAnchor(ViewWorld.ToWorld(sim.Player.X, sim.Player.Y));
            }

            // Prologue: tutorial toast progression (spec §1) + reveal return.
            if (_state == State.Prologue)
            {
                if (_revealReturnTimer > 0f)
                {
                    _revealReturnTimer -= Time.deltaTime;
                    if (_revealReturnTimer <= 0f) EnterLobby();
                }
                else if (_game != null && _game.Sim != null)
                {
                    AdvancePrologueToast(_game.Sim);
                }
            }
        }

        void AdvancePrologueToast(ICinderSim sim)
        {
            switch (_prologueStep)
            {
                case 0:   // waiting for movement
                    if (sim.Player.Moving) StepToast(1);
                    break;
                case 1:   // waiting for a strike
                    if (sim.Player.Action == ActorAction.Attack) StepToast(2);
                    break;
                case 2:   // oil gauge dwell
                    _prologueStepTimer += Time.deltaTime;
                    if (_prologueStepTimer >= 4f) StepToast(3);
                    break;
                case 3:   // hide once wave 2 arrives
                    if (sim.Wave >= 2) { _hud.HidePrologueToast(); _prologueStep = 4; }
                    break;
            }
        }

        void StepToast(int next)
        {
            _prologueStep = next;
            _prologueStepTimer = 0f;
            _hud.ShowPrologueToast(next);
        }
    }
}
