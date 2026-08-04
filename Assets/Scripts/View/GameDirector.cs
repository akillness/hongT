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
        GameObject _stageTerrain;         // instantiated Resources/Terrain prefab
        string _stageTerrainId = "";

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
            _game.OnRunEvents = OnRunEvents;

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
            SetStageTerrain(null);        // back to the base court plate
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
            if (_stageTerrain != null) { Destroy(_stageTerrain); _stageTerrain = null; }
            _stageTerrainId = stageId ?? "";
            if (string.IsNullOrEmpty(stageId)) return;
            var prefab = Resources.Load<GameObject>("Terrain/terrain-" + stageId);
            if (prefab == null) return;   // stage without terrain keeps the court
            _stageTerrain = Instantiate(prefab);
            _stageTerrain.name = "StageTerrain";
            // Arena center in view space; terrain FBX is origin-centered, top y=0.
            _stageTerrain.transform.position = ViewWorld.ToWorld(768f, 512f, 0f);
        }

        void ReturnToLobby() => EnterLobby();

        bool IsStageUnlocked(string stageId)
        {
            if (!_data.PrologueDone) return false;
            return stageId switch
            {
                "cinder-span" => true,
                "abyss-chancel" => _data.CinderSpanCleared,
                "echo-throne" => _data.AbyssChancelCleared,
                _ => false,
            };
        }

        // ------------------------------------------------------------ sorties --
        void OnSortie(string target)
        {
            if (target == "prologue") { StartPrologue(); return; }
            if (IsStageUnlocked(target)) StartDungeon(target);
        }

        void StartArena()
        {
            _state = State.Arena;
            SetStageTerrain(null);
            PrepareRunUi();
            _input.Mode = InputAdapter.Profile.Arena;
            _rig.SetProfile(CameraRig.Profile.Arena);
            _game.Begin(HackConfig.Arena(), "", null);
        }

        int _prologueStep;
        float _prologueStepTimer;

        void StartPrologue()
        {
            _state = State.Prologue;
            SetStageTerrain(null);        // tutorial runs on the court plate
            PrepareRunUi();
            _input.Mode = InputAdapter.Profile.Prologue;
            _rig.SetProfile(CameraRig.Profile.Prologue);
            _game.Begin(HackConfig.Prologue(), "", null);
            _hud.SetPrologueMode(true);
            _prologueStep = 0;
            _prologueStepTimer = 0f;
            _hud.ShowPrologueToast(0);
        }

        void StartDungeon(string stageId)
        {
            var metaStats = MetaStats.Of(_data.Attack, _data.Vitality, _data.Swiftness);
            var equipTiers = EquipTiers.Of(_data.Weapon, _data.Lantern, _data.Cloak);
            var companion = string.IsNullOrEmpty(_data.Active) ? null : _data.Active;
            if (!HackConfig.TryDungeon(stageId, metaStats, equipTiers, companion,
                    RosterMaskOf(_data.Roster), out var config))
            {
                EnterLobby();
                return;
            }
            _state = State.Dungeon;
            _runStageId = stageId;
            _runEndPersisted = false;
            SetStageTerrain(stageId);     // stage-specific painted ground
            PrepareRunUi();
            _input.Mode = InputAdapter.Profile.Dungeon;
            _rig.SetProfile(CameraRig.Profile.Dungeon);
            _game.Begin(config, StageDisplayName(stageId), _data.Active);

            if (StoryCatalog.TryGet(stageId, StoryCatalog.StageStart, out var speaker, out var text))
                _speech.Show(speaker, text, ViewWorld.ToWorld(768f, 500f, 1.4f));
        }

        void PrepareRunUi()
        {
            _lobby.Hide();
            _staging.Hide();
            _hud.SetPrologueMode(false);   // every run resets; prologue re-enables
            _hud.SetHudVisible(true);
        }

        static string StageDisplayName(string stageId) => stageId switch
        {
            "abyss-chancel" => "Abyss Chancel",
            "echo-throne" => "Echo Throne",
            _ => "Cinder Span",
        };

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
                }
                CampaignStore.Save(in _data);
                _lobby.Refresh(_data);
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

        void PersistDungeonClear(ICinderSim sim)
        {
            var firstClear = false;
            switch (_runStageId)
            {
                case "cinder-span":
                    firstClear = !_data.CinderSpanCleared;
                    _data.CinderSpanCleared = true;
                    break;
                case "abyss-chancel":
                    firstClear = !_data.AbyssChancelCleared;
                    _data.AbyssChancelCleared = true;
                    break;
                case "echo-throne":
                    firstClear = !_data.EchoThroneCleared;
                    _data.EchoThroneCleared = true;
                    break;
            }
            // Stat points: +2 per clear, +1 first boss kill (spec §5).
            _data.Points += firstClear ? 3 : 2;
            _data.Relics += sim.Relics;

            // Equipment ranks earned in-run become the new baseline (§6 path a).
            var campaign = sim as ICampaignSnapshot;
            if (campaign != null)
            {
                _data.Weapon = Mathf.Max(_data.Weapon, campaign.WeaponRank);
                _data.Lantern = Mathf.Max(_data.Lantern, campaign.LanternRank);
                _data.Cloak = Mathf.Max(_data.Cloak, campaign.CloakRank);
            }

            // Boss-reward companion (spec §4).
            if (firstClear)
            {
                var reward = _runStageId switch
                {
                    "abyss-chancel" => "shade-echo",
                    "echo-throne" => "possessed-echo",
                    _ => "ember-cohort",
                };
                AddToRoster(reward);
                if (string.IsNullOrEmpty(_data.Active)) _data.Active = reward;
            }

            var hack = sim as IHackSnapshot;
            if (hack != null) MergeRoster(hack.RosterMask);
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
            if ((events & SimEvents.BossSpawned) != 0 &&
                StoryCatalog.TryGet(_runStageId, StoryCatalog.BossEntry, out var entrySpeaker, out var entryText))
                _speech.Show(entrySpeaker, entryText, BossAnchor(sim));
            if ((events & SimEvents.BossPhase2) != 0 &&
                StoryCatalog.TryGet(_runStageId, StoryCatalog.BossPhase2, out var phaseSpeaker, out var phaseText))
                _speech.Show(phaseSpeaker, phaseText, BossAnchor(sim));
            if ((events & SimEvents.StageCleared) != 0 &&
                StoryCatalog.TryGet(_runStageId, StoryCatalog.Completion, out var doneSpeaker, out var doneText))
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
