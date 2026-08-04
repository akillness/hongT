// Owns the CinderSim and the fixed-step accumulator (spec §World — NOT Unity
// FixedUpdate). Run lifecycle is driven by GameDirector via Begin/EndRun.
// Persistence: GameDirector/CampaignStore own the campaign key — this class
// only writes the run digest (localStorage parity with the original page).
using System.Collections.Generic;
using CinderCourt.Sim;
using UnityEngine;

namespace CinderCourt.View
{
    public sealed class GameView : MonoBehaviour
    {
        public InputAdapter Input;
        public HudView Hud;
        public AudioDirector Audio;
        public VfxDirector Vfx;
        public CameraRig Rig;
        public GameBootstrap Bootstrap;

        /// <summary>Raised after presentation dispatch each tick with events set.
        /// GameDirector persists progress and drives story from here.</summary>
        public System.Action<SimEvents, ICinderSim> OnRunEvents;

        public ICinderSim Sim => _sim;

        CinderSim _sim;
        readonly Dictionary<int, ActorView> _enemyViews = new Dictionary<int, ActorView>(SimConfig.EnemyCap * 2);
        readonly Stack<ActorView>[] _pools = new Stack<ActorView>[6];
        readonly List<int> _toRecycle = new List<int>(SimConfig.EnemyCap);

        ActorView _playerView;
        ActorView _companionView;
        float _accumulator;
        bool _digestWritten;
        bool _isDungeon;
        bool _campaignUiOn;
        bool _dungeonUiOn;

        // --- presentation state (presentation-impact-spec #1/#3/#6) ----------
        // Hit-stop / slow-mo drive Time.timeScale ONLY. Determinism-safe: the
        // fixed-step accumulator consumes scaled deltaTime exactly like a slow
        // frame — tick size and per-tick input rules never change (spec
        // §determinism). Recovery decays on unscaledDeltaTime so the pulse can
        // never wedge itself. timeScale is force-restored on EndRun, GameOver,
        // and OnDisable — every exit path.
        float _hitStopTimer;      // seconds left at HitStopScale (0.05)
        float _slowMoTimer;       // seconds left at _slowMoScale (boss beat)
        float _slowMoScale = 1f;
        DamageNumberPool _damageNumbers;
        bool _finisherTick;       // gold damage numbers on ComboFinisher ticks
        const float HitStopScale = 0.05f;

        void Start()
        {
            for (var i = 0; i < _pools.Length; i++)
                _pools[i] = new Stack<ActorView>(8);
            _playerView = ActorView.Create(
                Bootstrap != null ? Bootstrap.PlayerPrefab : null,
                new Color(0.55f, 0.75f, 1f), 1f);
            _playerView.name = "Player";
            _playerView.EnableSwingTrail();   // spec #8, player only
            _playerView.gameObject.SetActive(false);   // lobby boots first
            var poolHost = new GameObject("DamageNumbers");
            poolHost.transform.SetParent(transform, false);
            _damageNumbers = poolHost.AddComponent<DamageNumberPool>();
        }

        /// <summary>Start a run. Idempotent across restarts of the same mode.</summary>
        public void Begin(in HackConfig config, string stageDisplayName, string companionId)
        {
            EndRun();
            _sim = new CinderSim(in config);
            _isDungeon = config.Mode == GameMode.Dungeon;
            _accumulator = 0f;
            _digestWritten = false;
            if (_playerView == null) Start();
            _playerView.gameObject.SetActive(true);
            _playerView.ResetForPool();

            if (_isDungeon)
            {
                if (!_campaignUiOn && Hud != null)
                {
                    _campaignUiOn = true;
                    Hud.EnableCampaignUi(stageDisplayName, config.ToCampaignConfig().Waves);
                }
                if (!_dungeonUiOn && Hud != null)
                {
                    _dungeonUiOn = true;
                    Hud.EnableDungeonUi(BossNameFor(config.StageId));
                }
                if (Hud != null) Hud.SetCampaignSurfacesVisible(true);

                if (!string.IsNullOrEmpty(companionId) && Bootstrap != null)
                {
                    var (prefab, tint) = Bootstrap.CompanionVisual(companionId);
                    _companionView = ActorView.Create(prefab, new Color(1f, 0.86f, 0.55f), 0.92f);
                    _companionView.name = "Companion";
                    if (tint.HasValue)
                        LobbyStaging.TintRenderers(_companionView.gameObject, tint.Value);
                }
            }
            else if (Hud != null)
            {
                Hud.SetCampaignSurfacesVisible(false);
            }
        }

        /// <summary>Stop the run and release run-scoped views. Safe when idle.</summary>
        public void EndRun()
        {
            _sim = null;
            foreach (var pair in _enemyViews)
                Return(pair.Value);
            _enemyViews.Clear();
            if (_companionView != null)
            {
                Destroy(_companionView.gameObject);
                _companionView = null;
            }
            if (_playerView != null) _playerView.gameObject.SetActive(false);
            if (Vfx != null) Vfx.ClearTransient();
            if (_damageNumbers != null) _damageNumbers.Clear();
            // Presentation timers must not leak into the lobby (spec #1).
            _hitStopTimer = 0f;
            _slowMoTimer = 0f;
            _slowMoScale = 1f;
            _finisherTick = false;
            Time.timeScale = 1f;
        }

        void OnDisable()
        {
            // Scene teardown / component disable mid-pulse: timeScale is a
            // global — ALWAYS hand it back at 1 (spec #1 hard requirement).
            Time.timeScale = 1f;
        }

        static string BossNameFor(string stageId) => stageId switch
        {
            "abyss-chancel" => "Veil Tactician",
            "echo-throne" => "Gate Sovereign",
            _ => "Cinder Warden",
        };

        void Update()
        {
            if (_sim == null) return;   // lobby / not started
            var delta = Mathf.Min(Time.deltaTime, SimConfig.MaxFrameDelta);
            _accumulator += delta;
            var steps = 0;
            var input = Input != null ? Input.Sample() : default;
            while (_accumulator >= SimConfig.FixedStep && steps < SimConfig.MaxCatchUpSteps)
            {
                _sim.Tick(in input);
                DispatchEvents();
                if (_sim == null) return;   // director ended the run mid-batch
                // One-shot flags must fire exactly once per sample batch.
                input.AttackQueued = false;
                input.NovaQueued = false;
                input.WardQueued = false;
                input.BoltQueued = false;
                input.PulseQueued = false;
                input.DashQueued = false;
                input.RestartQueued = false;
                _accumulator -= SimConfig.FixedStep;
                steps++;
            }
            if (_accumulator >= SimConfig.FixedStep)
                _accumulator = SimConfig.FixedStep; // drop backlog beyond catch-up
            // Only consume latches when at least one tick sampled them —
            // otherwise a 144 Hz frame with no step would eat Q/E presses.
            if (steps > 0 && Input != null) Input.ClearLatches();

            SyncViews();
            ApplyTimeScale();
        }

        /// <summary>
        /// Hit-stop (#1) + boss slow-mo (#3) resolution. Both timers decay on
        /// unscaled time; the strongest (lowest) scale wins while active and
        /// the scale eases back to 1 exponentially fast when both expire.
        /// </summary>
        void ApplyTimeScale()
        {
            var dt = Time.unscaledDeltaTime;
            var target = 1f;
            if (_slowMoTimer > 0f)
            {
                _slowMoTimer -= dt;
                target = _slowMoScale;
            }
            if (_hitStopTimer > 0f)
            {
                _hitStopTimer -= dt;
                target = Mathf.Min(target, HitStopScale);
            }
            Time.timeScale = target < 1f
                ? target
                : Mathf.MoveTowards(Time.timeScale, 1f, 4f * dt);
        }

        void DispatchEvents()
        {
            var events = _sim.Events;
            if (events == SimEvents.None) return;
            if (Audio != null) Audio.OnEvents(events);
            if (Vfx != null) Vfx.OnEvents(events, _sim);
            if (Rig != null) Rig.OnEvents(events);
            if (Hud != null) Hud.OnEvents(events, _sim);

            // --- presentation pulses (spec #1/#2/#3) --------------------------
            // Hit-stop: kill 40 ms, finisher 70 ms at timeScale 0.05 (spec cap
            // is 80 ms; Max() merges overlapping pulses instead of stacking).
            if ((events & SimEvents.ComboFinisher) != 0)
            {
                _hitStopTimer = Mathf.Max(_hitStopTimer, 0.07f);
                _finisherTick = true;   // gold damage numbers this batch (#6)
            }
            else if ((events & SimEvents.EnemyKilled) != 0)
            {
                _hitStopTimer = Mathf.Max(_hitStopTimer, 0.04f);
            }
            // Boss phase-2 slow-mo beat, synced with the taunt bubble (#3).
            if ((events & SimEvents.BossPhase2) != 0)
            {
                _slowMoTimer = 0.5f;
                _slowMoScale = 0.35f;
            }
            if ((events & SimEvents.GameOver) != 0)
            {
                // Never let a pulse slow the game-over panel (#1 risk note).
                _hitStopTimer = 0f;
                _slowMoTimer = 0f;
                Time.timeScale = 1f;
            }
            // Shake tiers (#2) via the append-only CameraRig.Punch API.
            // Priority mirrors the rig chain: BossSpawned > Finisher > Kill —
            // Punch itself refuses to weaken a stronger live shake.
            if (Rig != null)
            {
                if ((events & SimEvents.BossSpawned) != 0) Rig.Punch(0.07f, 0.35f);
                else if ((events & SimEvents.ComboFinisher) != 0) Rig.Punch(0.05f, 0.14f);
                else if ((events & SimEvents.EnemyKilled) != 0) Rig.Punch(0.02f, 0.08f);
            }

            if ((events & (SimEvents.GameOver | SimEvents.StageCleared)) != 0 && !_digestWritten)
            {
                _digestWritten = true;
                WebGLStorage.WriteRunDigest(_sim.Digest);
            }
            if ((events & SimEvents.StageCleared) != 0 && Hud != null)
                Hud.ShowStageClear(_sim.Digest);
            if ((events & SimEvents.WaveStarted) != 0)
                _digestWritten = false;

            OnRunEvents?.Invoke(events, _sim);
        }

        void SyncViews()
        {
            if (_sim == null) return;
            _playerView.SyncPlayer(_sim.Player);

            var enemies = _sim.Enemies;
            // Mark-and-sweep: sync live ids, recycle views whose id vanished.
            for (var i = 0; i < enemies.Count; i++)
            {
                var state = enemies[i];
                if (!_enemyViews.TryGetValue(state.Id, out var view))
                {
                    view = Rent(state.Visual);
                    _enemyViews[state.Id] = view;
                    // Elite marker (spec §3 + presentation #14): non-boss with
                    // the 1.35 scale-up gets the pulsing gold tint.
                    if (!state.IsBoss && state.Scale > 1.2f)
                        view.SetEliteTint(true);
                }
                // SyncEnemy reports the health delta since last frame — the
                // view-side hit signal (presentation #5) that also feeds the
                // floating damage numbers (#6).
                var damage = view.SyncEnemy(in state);
                if (damage > 0f && _damageNumbers != null)
                    _damageNumbers.Show(state.X, state.Y, damage, _finisherTick);
            }
            _finisherTick = false;   // consumed by this frame's batch
            if (_enemyViews.Count != enemies.Count)
            {
                _toRecycle.Clear();
                foreach (var pair in _enemyViews)
                {
                    var alive = false;
                    for (var i = 0; i < enemies.Count; i++)
                        if (enemies[i].Id == pair.Key) { alive = true; break; }
                    if (!alive) _toRecycle.Add(pair.Key);
                }
                for (var i = 0; i < _toRecycle.Count; i++)
                {
                    var id = _toRecycle[i];
                    Return(_enemyViews[id]);
                    _enemyViews.Remove(id);
                }
            }

            if (Vfx != null) Vfx.SyncPickups(_sim.Pickups);
            if (Vfx != null) Vfx.SyncWard(_sim.Player);
            if (Hud != null) Hud.Sync(_sim);

            if (_isDungeon)
            {
                if (Vfx != null) Vfx.SyncHazards(_sim.Hazards);
                if (Hud != null)
                    Hud.SyncCampaign(_sim.Wave, _sim.BossAlive,
                        _sim.WeaponRank, _sim.LanternRank, _sim.CloakRank);
                var hack = (IHackSnapshot)_sim;
                if (Hud != null)
                    Hud.SyncDungeon(hack.Level, hack.Xp, hack.XpNext, hack.ComboIndex,
                        hack.DashCooldown, hack.SkillCooldowns, hack.Shield,
                        hack.ExtractionProgress, hack.ExtractionTarget,
                        hack.BossHp, hack.BossMaxHp, hack.BossPhase, _sim.Charge);
                if (Vfx != null)
                    Vfx.SyncExtraction(hack.ExtractionProgress, hack.ExtractionTarget, _sim.Player);
                if (_companionView != null)
                {
                    _companionView.SyncCompanion(hack.CompanionX, hack.CompanionY,
                        hack.CompanionAttacking);
                }
            }
        }

        ActorView Rent(EnemyVisual visual)
        {
            var pool = _pools[(int)visual];
            if (pool.Count > 0)
            {
                var pooled = pool.Pop();
                pooled.gameObject.SetActive(true);
                pooled.ResetForPool();
                return pooled;
            }
            var (prefab, color, scale) = Bootstrap != null
                ? Bootstrap.EnemyVisualFor(visual)
                : (null, Color.red, 1f);
            var view = ActorView.Create(prefab, color, scale);
            view.name = visual.ToString();
            var marker = view.gameObject.AddComponent<VisualMarker>();
            marker.Visual = visual;
            return view;
        }

        void Return(ActorView view)
        {
            if (view == null) return;
            var marker = view.GetComponent<VisualMarker>();
            var visual = marker != null ? marker.Visual : EnemyVisual.EmberCohort;
            view.gameObject.SetActive(false);
            _pools[(int)visual].Push(view);
        }

        sealed class VisualMarker : MonoBehaviour
        {
            public EnemyVisual Visual;
        }
    }
}
