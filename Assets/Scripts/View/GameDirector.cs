// v0.2 single-scene state machine (spec §0): Lobby <-> Prologue/Dungeon/Arena.
// Owns persistence (CampaignStore is the ONLY writer of the campaign key),
// mode routing, camera/input profiles, and run lifecycle.
using CinderCourt.Sim;
using UnityEngine;

namespace CinderCourt.View
{
    public sealed class GameDirector : MonoBehaviour
    {
        public enum State { Lobby, Prologue, Dungeon, Arena }

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
        string _emberRestNextStageId = "";
        PreparationOffer _emberRestPreparation;
        bool _emberRestDecisionMade;

        public State Current => _state;

        public void Attach(
            GameBootstrap bootstrap, LobbyView lobby, LobbyStaging staging,
            CameraRig rig, InputAdapter input, HudView hud,
            AudioDirector audio, VfxDirector vfx, GameView game,
            SpeechBubbleView speech)
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

            _data = CampaignStore.Load();
            _hud.OnReturnHome = ReturnToLobby;
            _hud.OnRetryStage = RetryStage;
            _input.OnDungeonRetryShortcut = TryRetryStageShortcut;
            _game.OnRunEvents = OnRunEvents;

            _hud.OnEmberRestOfferSelected = SelectEmberRestOffer;
            _hud.OnEmberRestDeferred = DeferEmberRest;
            _hud.OnEmberRestContinue = ContinueFromEmberRest;
            var callbacks = new LobbyCallbacks
            {
                OnSortie = OnSortie,
                OnAllocateStat = OnAllocateStat,
                OnBuyEquip = OnBuyEquip,
                OnSelectCompanion = OnSelectCompanion,
            };
            _lobby.Build(_data, callbacks);

            // Boot route (spec §0): default Lobby; QA deep links preserved.
            var mode = WebGLStorage.QueryParam("mode");
            var stage = WebGLStorage.QueryParam("stage");
            if (mode == "arena") StartArena();
            else if (mode == "prologue") StartPrologue();
            else if (mode == "campaign" && IsStageUnlocked(stage)) StartDungeon(stage);
            else EnterLobby();
        }

        // ------------------------------------------------------------- lobby --
        void EnterLobby()
        {
            _state = State.Lobby;
            ClearEmberRestRoute();
            SetStageTerrain(null);        // back to the base court plate
            ApplyStageDressing(null);
            _game.EndRun();
            _lobby.Refresh(_data);
            _lobby.Show();
            _staging.Attach(_bootstrap);   // idempotent (accent light reused)
            _staging.Show(_selectedStage, _data.Active);
            _rig.SetProfile(CameraRig.Profile.Lobby);
            _input.Mode = InputAdapter.Profile.Arena; // inert while lobby UI is up
            _hud.SetHudVisible(false);
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

        void ReturnToLobby() => EnterLobby();

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
            SetStageTerrain(null);
            ApplyStageDressing(null);
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
            PrepareRunUi();
            _input.Mode = InputAdapter.Profile.Prologue;
            _rig.SetProfile(CameraRig.Profile.Prologue);
            _game.Begin(HackConfig.Prologue(), "", null);
            _hud.SetPrologueMode(true);
            _prologueStep = 0;
            _prologueStepTimer = 0f;
            _hud.ShowPrologueToast(0);
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
            var companion = string.IsNullOrEmpty(_data.Active) ? null : _data.Active;
            if (!HackConfig.TryDungeon(entry.SimAnchorId, metaStats, equipTiers, companion,
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
            _state = State.Dungeon;
            _runStageId = entry.Id;
            _runEndPersisted = false;
            SetStageTerrain(entry.TerrainId); // logical stage terrain can differ from its Sim anchor
            ApplyStageDressing(entry.Id);     // per-LOGICAL-stage dressing (spec §T-a)
            PrepareRunUi();
            _input.Mode = InputAdapter.Profile.Dungeon;
            _rig.SetProfile(CameraRig.Profile.Dungeon);
            // "— 서약" HUD title suffix: the cheapest always-visible in-run
            // marker (Begin's stageDisplayName flows to the campaign HUD).
            _game.Begin(config,
                _runWasPact ? entry.DisplayName + " — 서약" : entry.DisplayName,
                _data.Active, entry.Id);

            if (StoryCatalog.TryGet(entry.StoryKey, StoryCatalog.StageStart, out var speaker, out var text))
                _speech.Show(speaker, text, ViewWorld.ToWorld(768f, 500f, 1.4f));
        }

        void PrepareRunUi()
        {
            _lobby.Hide();
            _staging.Hide();
            _hud.ResetRunUi();             // clears interrupted ceremony timers on every entry/retry
            _hud.SetPrologueMode(false);   // every run resets; prologue re-enables
            _hud.SetHudVisible(true);
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

        static readonly int[] EquipCosts = { 2, 4, 7, 11, 16 };

        void OnBuyEquip(string slot)
        {
            var tier = slot switch
            {
                "weapon" => _data.Weapon,
                "lantern" => _data.Lantern,
                _ => _data.Cloak,
            };
            if (tier >= 5) return;
            var cost = EquipCosts[tier];
            if (_data.Relics < cost) return;
            _data.Relics -= cost;
            switch (slot)
            {
                case "weapon": _data.Weapon++; break;
                case "lantern": _data.Lantern++; break;
                default: _data.Cloak++; break;
            }
            CampaignStore.Save(in _data);
            _lobby.Refresh(_data);
        }

        void OnSelectCompanion(string id)
        {
            _data.Active = id ?? "";
            CampaignStore.Save(in _data);
            _lobby.Refresh(_data);
            _staging.Show(_selectedStage, _data.Active);
        }

        // ------------------------------------------------------------ run events --
        void OnRunEvents(SimEvents events, ICinderSim sim)
        {
            if (_state == State.Dungeon)
                DispatchStory(events, sim);

            if ((events & SimEvents.StageCleared) != 0 && !_runEndPersisted)
            {
                _runEndPersisted = true;
                var shouldBeginEmberRest = false;
                if (_state == State.Prologue)
                {
                    _data.PrologueDone = true;
                    // The 2D->2.5D reveal (spec §1): 2.2 s camera sweep, then lobby.
                    _hud.HidePrologueToast();
                    _rig.SetProfile(CameraRig.Profile.PrologueReveal);
                    _revealReturnTimer = 2.6f;
                }
                else if (_state == State.Dungeon)
                {
                    PersistDungeonClear(sim);
                    shouldBeginEmberRest = HasDirectEmberRestSuccessor(out _, out _);
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

        /// <summary>v1.3 M3 (negotiation-record entry 5, signed): a pact clear
        /// grants sim.Relics × this. VIEW-side payout — the sim's relic count
        /// is untouched. Internal so the EditMode economy test pins it.</summary>
        internal const int PactRelicMultiplier = 2;

        void PersistDungeonClear(ICinderSim sim)
        {
            if (!StageCatalog.TryGet(_runStageId, out var entry)) return;
            StageCatalog.MarkCleared(ref _data, in entry, out var firstClear);
            // Stat points: +2 per clear, +1 first boss kill (spec §5).
            _data.Points += firstClear ? 3 : 2;
            // v1.3 M3 (entry 5): pact clear pays sim.Relics × 2 — the ONLY
            // doubled term. First-clear bonus stays single (agreed 비중복);
            // in practice the pact toggle only exists on cleared cards, so a
            // pact run's firstClear is false unless a QA deep link races the
            // toggle — the bonus line below stays independent either way.
            _data.Relics += _runWasPact ? sim.Relics * PactRelicMultiplier : sim.Relics;
            // Cycle-2 first-clear relic bonus (negotiation-record entry 1,
            // signed designer+pm): view-side grant, sim untouched. One-time —
            // gated on firstClear like the companion reward below.
            if (firstClear) _data.Relics += FirstClearRelicBonus(entry.Id);

            // Equipment ranks earned in-run become the new baseline (§6 path a).
            var campaign = sim as ICampaignSnapshot;
            if (campaign != null)
            {
                _data.Weapon = Mathf.Max(_data.Weapon, campaign.WeaponRank);
                _data.Lantern = Mathf.Max(_data.Lantern, campaign.LanternRank);
                _data.Cloak = Mathf.Max(_data.Cloak, campaign.CloakRank);
            }

            // Only base stages retain their established companion rewards.
            if (firstClear && !string.IsNullOrEmpty(entry.CompanionReward))
            {
                AddToRoster(entry.CompanionReward);
                if (string.IsNullOrEmpty(_data.Active)) _data.Active = entry.CompanionReward;
            }

            var hack = sim as IHackSnapshot;
            if (hack != null) MergeRoster(hack.RosterMask);
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
                AddToRoster(baseId + "-echo");
            }
        }

        void AddToRoster(string id)
        {
            var roster = _data.Roster ?? new string[0];
            for (var i = 0; i < roster.Length; i++)
                if (roster[i] == id) return;
            var grown = new string[roster.Length + 1];
            for (var i = 0; i < roster.Length; i++) grown[i] = roster[i];
            grown[roster.Length] = id;
            _data.Roster = grown;
        }

        // ------------------------------------------------------------- story --
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
                    _speech.Show(entrySpeaker, entryText, bossAnchor);
                    // §캡처5: speaker-prefixed screen subtitle doubles the boss
                    // beat (bubble stays the in-world grammar).
                    _hud.ShowSpeakerLine(entrySpeaker, entryText);
                }
                _hud.ShowBossIntro(GameView.BossNameFor(_runStageId));
                _rig.FocusPulse(bossAnchor, 0.45f);
            }
            if ((events & SimEvents.BossPhase2) != 0 &&
                StoryCatalog.TryGet(storyKey, StoryCatalog.BossPhase2, out var phaseSpeaker, out var phaseText))
            {
                _speech.Show(phaseSpeaker, phaseText, BossAnchor(sim));
                _hud.ShowSpeakerLine(phaseSpeaker, phaseText);
            }
            if ((events & SimEvents.StageCleared) != 0 &&
                StoryCatalog.TryGet(storyKey, StoryCatalog.Completion, out var doneSpeaker, out var doneText))
                _speech.Show(doneSpeaker, doneText,
                    ViewWorld.ToWorld(sim.Player.X, sim.Player.Y, 1.6f));
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
