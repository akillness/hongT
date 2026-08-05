// Owns the CinderSim and the fixed-step accumulator (spec §World — NOT Unity
// FixedUpdate). Run lifecycle is driven by GameDirector via Begin/EndRun.
// Persistence: GameDirector/CampaignStore own the campaign key — this class
// only writes the run digest (localStorage parity with the original page).
using System.Collections.Generic;
using System.Reflection;
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
        /// <summary>Read-only Ember Rest state for the active dungeon run.</summary>
        public IRunPreparationSnapshot EmberRestSnapshot => _sim as IRunPreparationSnapshot;

        /// <summary>Opens the active simulation's deterministic Ember Rest.</summary>
        public bool BeginEmberRest(int roomIndex, int rewardSeed)
            => _sim != null && _sim.BeginEmberRest(roomIndex, rewardSeed);

        /// <summary>Selects one offered preparation on the active Ember Rest.</summary>
        public bool TrySelectEmberRestOffer(int offerIndex)
            => _sim != null && _sim.TrySelectPreparation(offerIndex);

        /// <summary>Records an explicit no-preparation decision on the active Ember Rest.</summary>
        public bool DeferEmberRest()
            => _sim != null && _sim.DeferPreparation();

        /// <summary>Closes Ember Rest, leaving its selected value readable for handoff.</summary>
        public bool EndEmberRest()
            => _sim != null && _sim.EndEmberRest();

        /// <summary>Selected preparation after a successful rest choice; None when absent.</summary>
        public PreparationOffer SelectedEmberRestPreparation
            => _sim != null ? _sim.SelectedPreparation : default;


        CinderSim _sim;
        readonly Dictionary<int, ActorView> _enemyViews = new Dictionary<int, ActorView>(SimConfig.EnemyCap * 2);
        readonly Stack<ActorView>[] _pools = new Stack<ActorView>[6];
        readonly List<int> _toRecycle = new List<int>(SimConfig.EnemyCap);
        readonly Dictionary<ActorView, Renderer[]> _bossRenderers =
            new Dictionary<ActorView, Renderer[]>(2);
        MaterialPropertyBlock _bossPresentationBlock;
        bool _initialized;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        ActorView _playerView;
        ActorView _companionView;
        float _accumulator;
        bool _digestWritten;
        bool _isDungeon;
        bool _campaignUiOn;
        bool _dungeonUiOn;
        string _logicalStageId;

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
        TextMesh[] _damageNumberTexts;
        float[] _damageNumberLives;
        Color[] _damageNumberColors;
        float _lastPlayerHealth;
        float _deathNumberPunchTimer;
        bool _finisherTick;       // gold damage numbers on ComboFinisher ticks
        const float HitStopScale = 0.05f;
        const float DeathNumberPunchDuration = 0.24f;
        static readonly Color EnemyDamageColor = new Color(1f, 0.5f, 0.3f);
        static readonly Color FinisherDamageColor = new Color(0.87f, 0.78f, 0.41f);
        // §K3: the element color of the most recent cast, held briefly so the
        // damage it causes flashes that element on the struck mesh. 0.4 s is
        // the spec window; it outlives one frame because a cast's damage can
        // land over several ticks (pulse ticks, nova's spread resolution).
        const float ElementTintWindow = 0.4f;
        Color _elementTint;
        float _elementTintTime;

        /// <summary>§K3/V1 single source of truth for element color. Cast events
        /// are mutually exclusive per tick in practice; if two ever coincide the
        /// first in kit order wins, deterministically.</summary>
        internal static bool TryElementColor(SimEvents events, out Color color)
        {
            if ((events & SimEvents.BoltCast) != 0) { color = new Color(0.75f, 0.55f, 1f); return true; }   // void violet
            if ((events & SimEvents.PulseCast) != 0) { color = new Color(0.35f, 0.9f, 0.55f); return true; } // grave green
            if ((events & SimEvents.NovaCast) != 0) { color = new Color(0.95f, 0.35f, 0.17f); return true; } // ember
            if ((events & SimEvents.WardCast) != 0) { color = new Color(0.45f, 0.85f, 1f); return true; }    // cyan aegis/ward
            color = default;
            return false;
        }

        void Awake()
        {
            _bossPresentationBlock = new MaterialPropertyBlock();
        }

        void Start()
        {
            EnsureInitialized();
        }

        void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
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
            _damageNumbers.Initialize();
            CacheDamageNumberSlots();
        }

        /// <summary>Start a run. Idempotent across restarts of the same mode.</summary>
        public void Begin(in HackConfig config, string stageDisplayName, string companionId,
                          string logicalStageId = null)
        {
            _isDungeon = config.Mode == GameMode.Dungeon;
            EndRun();
            _logicalStageId = logicalStageId ?? string.Empty;
            var companionActive = !string.IsNullOrEmpty(companionId);
            _sim = new CinderSim(in config);
            _accumulator = 0f;
            _digestWritten = false;
            _lastPlayerHealth = _sim.Player.Health;
            _deathNumberPunchTimer = 0f;
            if (_damageNumbers != null) _damageNumbers.transform.localScale = Vector3.one;
            if (Hud != null) Hud.ResetRunUi();
            EnsureInitialized();
            _playerView.gameObject.SetActive(true);
            _playerView.ResetForPool();

            if (_isDungeon)
            {
                if (Hud != null)
                {
                    if (!_campaignUiOn)
                    {
                        _campaignUiOn = true;
                        Hud.EnableCampaignUi(stageDisplayName, config.ToCampaignConfig().Waves);
                    }
                    if (!_dungeonUiOn)
                    {
                        _dungeonUiOn = true;
                        Hud.EnableDungeonUi(BossNameFor(_logicalStageId));
                    }
                    Hud.RefreshDungeonStage(stageDisplayName, config.ToCampaignConfig().Waves,
                        BossNameFor(_logicalStageId), companionActive);
                    Hud.SetCampaignSurfacesVisible(true);
                }

                if (companionActive && Bootstrap != null)
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
                if (Application.isPlaying) Destroy(_companionView.gameObject);
                else DestroyImmediate(_companionView.gameObject);
                _companionView = null;
            }
            if (_playerView != null) _playerView.gameObject.SetActive(false);
            if (Vfx != null) Vfx.ClearTransient();
            ClearDamageNumbers();
            // Presentation timers must not leak into the lobby (spec #1).
            _hitStopTimer = 0f;
            _slowMoTimer = 0f;
            _slowMoScale = 1f;
            _finisherTick = false;
            _lastPlayerHealth = 0f;
            _deathNumberPunchTimer = 0f;
            _logicalStageId = string.Empty;
            Time.timeScale = 1f;
        }

        void OnDisable()
        {
            // Scene teardown / component disable mid-pulse: timeScale is a
            // global — ALWAYS hand it back at 1 (spec #1 hard requirement).
            _deathNumberPunchTimer = 0f;
            if (_damageNumbers != null) _damageNumbers.transform.localScale = Vector3.one;
            Time.timeScale = 1f;
        }

        internal static string BossNameFor(string stageId)
            => StageCatalog.TryGet(stageId, out var entry)
                ? entry.Boss.HudName
                : "Cinder Warden";

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
                input.CompanionHoldQueued = false;
                input.CompanionRecallQueued = false;
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
            // Console slow-mo first and OUTSIDE TimeEffectsAllowed: it buys
            // typing time (accessibility), not decoration — reduced-motion
            // players need it most. Determinism: timeScale only stretches
            // wall-clock per tick; tick size and input rules are unchanged
            // (presentation-impact-spec determinism note).
            var consoleOpen = Hud != null && Hud.CommandConsoleOpen;
            if (!ViewPrefs.TimeEffectsAllowed)
            {
                _hitStopTimer = 0f;
                _slowMoTimer = 0f;
                Time.timeScale = consoleOpen ? 0.2f : 1f;
                return;
            }
            var dt = Time.unscaledDeltaTime;
            var target = consoleOpen ? 0.2f : 1f;
            if (_slowMoTimer > 0f)
            {
                _slowMoTimer -= dt;
                target = Mathf.Min(target, _slowMoScale);
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
                if (ViewPrefs.TimeEffectsAllowed)
                    _hitStopTimer = Mathf.Max(_hitStopTimer, 0.07f);
                _finisherTick = true;   // gold damage numbers this batch (#6)
            }
            else if ((events & SimEvents.EnemyKilled) != 0 && ViewPrefs.TimeEffectsAllowed)
            {
                _hitStopTimer = Mathf.Max(_hitStopTimer, 0.04f);
            }
            // Boss phase-2 slow-mo beat, synced with the taunt bubble (#3).
            if ((events & SimEvents.BossPhase2) != 0 && ViewPrefs.TimeEffectsAllowed)
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
                _deathNumberPunchTimer = 0f;
                if (_damageNumbers != null)
                {
                    _damageNumbers.transform.localScale = Vector3.one;
                    if (ViewPrefs.TimeEffectsAllowed)
                        _deathNumberPunchTimer = DeathNumberPunchDuration;
                }
            }
            // Shake tiers (#2) via the append-only CameraRig.Punch API.
            // Priority mirrors the rig chain: BossSpawned > Finisher > Kill >
            // WaveStarted — Punch itself refuses to weaken a stronger live
            // shake, and a boss wave raises BOTH events, so the wave tier sits
            // LAST to keep the 0.35 boss punch intact (§W).
            if (Rig != null)
            {
                if ((events & SimEvents.BossSpawned) != 0) Rig.Punch(0.07f, 0.35f);
                else if ((events & SimEvents.ComboFinisher) != 0) Rig.Punch(0.05f, 0.14f);
                else if ((events & SimEvents.EnemyKilled) != 0) Rig.Punch(0.02f, 0.08f);
                else if ((events & SimEvents.WaveStarted) != 0) Rig.Punch(0.05f, 0.15f);
            }
            // §W wave-arrival telegraph: warning rings at the incoming wave's
            // spawn points. Boss waves ring red/larger via the same call.
            if (Vfx != null && (events & SimEvents.WaveStarted) != 0)
                Vfx.SpawnWaveWarnings(_sim.Wave, (events & SimEvents.BossSpawned) != 0);
            // §P2: equip pickup flash — gold pulse on the player model.
            if ((events & SimEvents.EquipDropped) != 0 && _playerView != null)
                _playerView.FlashEquip();
            // §Lane V1: cast-sync hand glow — element-matched convergence at
            // every cast event. Decoration reading sim events, never gating.
            // §K3: the SAME element color drives the struck mesh's hit flash,
            // so the hand and the victim always agree on what element landed.
            if (TryElementColor(events, out var element))
            {
                if (_playerView != null) _playerView.FlashCastGlow(element);
                _elementTint = element;
                _elementTintTime = ElementTintWindow;
            }

            if ((events & (SimEvents.GameOver | SimEvents.StageCleared)) != 0 && !_digestWritten)
            {
                _digestWritten = true;
                WebGLStorage.WriteRunDigest(_sim.Digest);
            }
            if ((events & SimEvents.StageCleared) != 0 && Hud != null &&
                (!StageCatalog.TryGet(_logicalStageId, out var clearedStage) ||
                 clearedStage.CatalogIndex == StageCatalog.Entries.Count - 1))
                Hud.ShowStageClear(_sim.Digest);
            if ((events & SimEvents.WaveStarted) != 0)
                _digestWritten = false;

            OnRunEvents?.Invoke(events, _sim);
        }

        void SyncViews()
        {
            if (_sim == null) return;
            ResetInactiveDamageNumberSlots();
            var playerHealth = _sim.Player.Health;
            var playerDamage = _lastPlayerHealth - playerHealth;
            _lastPlayerHealth = playerHealth;
            _playerView.SyncPlayer(_sim.Player);
            if (playerDamage > 0.01f && _damageNumbers != null)
                ShowDamageNumber(_sim.Player.X, _sim.Player.Y, playerDamage, EnemyDamageColor);

            // §K3: decay the element window once per frame, then hand the live
            // color (or default = clear) to every enemy before it syncs, so a
            // mesh struck inside the window flashes WHAT hit it.
            if (_elementTintTime > 0f) _elementTintTime -= Time.deltaTime;
            var liveTint = _elementTintTime > 0f ? _elementTint : default;
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
                view.SetElementTint(liveTint);
                var damage = view.SyncEnemy(in state);
                if (state.IsBoss && StageCatalog.TryGet(_logicalStageId, out var stage)
                    && state.Visual == stage.Boss.Visual)
                    ApplyBossPresentation(view, in stage);
                if (damage > 0f)
                {
                    // §C3: contact spark at the struck enemy (dedicated pool,
                    // 6/frame budget — a 20-enemy nova stays bounded).
                    if (Vfx != null) Vfx.SpawnHitSpark(state.X, state.Y, _finisherTick);
                    if (_damageNumbers != null)
                    {
                        var damageColor = EnemyDamageColor;
                        if (Bootstrap != null)
                        {
                            var visual = Bootstrap.EnemyVisualFor(state.Visual);
                            damageColor = visual.fallback;
                        }
                        ShowDamageNumber(state.X, state.Y, damage,
                            _finisherTick ? FinisherDamageColor : damageColor);
                    }
                }
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
                // §P2 rank glow + §C1 combo trail tier. ComboIndex IS the
                // current swing during Attack (sim advances it at swing end),
                // and preloads the next tier between swings — both correct.
                _playerView.SetEquipRanks(_sim.WeaponRank, _sim.LanternRank, _sim.CloakRank);
                // §Lane P: socket props follow the same live ranks (idempotent
                // per band — a mid-run rank-up swaps the prop immediately).
                _playerView.AttachEquipProps(_sim.WeaponRank, _sim.LanternRank, _sim.CloakRank);
                _playerView.SetComboTier(hack.ComboIndex);
                if (_companionView != null)
                {
                    var preparation = _sim as IRunPreparationSnapshot;
                    // G1: nearest living enemy inside the companion's attack
                    // range owns the gaze between strikes (iso-weighted metric,
                    // same as the sim's targeting). Near the player with no
                    // target -> rest Idle instead of treadmilling Move.
                    var gazeYaw = float.NaN;
                    var bestSq = HackSpec.CompanionAttackRange * HackSpec.CompanionAttackRange;
                    for (var i = 0; i < enemies.Count; i++)
                    {
                        var enemy = enemies[i];
                        if (enemy.Dead) continue;
                        var deltaX = enemy.X - hack.CompanionX;
                        var deltaY = (enemy.Y - hack.CompanionY) * SimConfig.IsoY;
                        var distSq = deltaX * deltaX + deltaY * deltaY;
                        if (distSq >= bestSq) continue;
                        bestSq = distSq;
                        gazeYaw = Mathf.Round(
                            Mathf.Atan2(deltaX, -(enemy.Y - hack.CompanionY))
                            * Mathf.Rad2Deg / 22.5f) * 22.5f;
                    }
                    var playerDeltaX = _sim.Player.X - hack.CompanionX;
                    var playerDeltaY = _sim.Player.Y - hack.CompanionY;
                    var restIdle = float.IsNaN(gazeYaw) && !hack.CompanionAttacking
                        && playerDeltaX * playerDeltaX + playerDeltaY * playerDeltaY
                           < HackSpec.CompanionFollowOffset * HackSpec.CompanionFollowOffset * 2.25f;
                    _companionView.SyncCompanion(hack.CompanionX, hack.CompanionY,
                        preparation != null ? preparation.CompanionFacing : 0,
                        hack.CompanionAttacking, gazeYaw, restIdle);
                }
            }
            SyncDeathNumberPunch();
        }

        void CacheDamageNumberSlots()
        {
            if (_damageNumbers == null) return;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(DamageNumberPool);
            _damageNumberTexts = type.GetField("_texts", flags)?.GetValue(_damageNumbers) as TextMesh[];
            _damageNumberLives = type.GetField("_lives", flags)?.GetValue(_damageNumbers) as float[];
            _damageNumberColors = type.GetField("_colors", flags)?.GetValue(_damageNumbers) as Color[];
        }

        void ShowDamageNumber(float simX, float simY, float amount, Color color)
        {
            if (_damageNumberTexts == null || _damageNumberLives == null ||
                _damageNumberColors == null)
            {
                _damageNumbers.Show(simX, simY, amount, color == FinisherDamageColor);
                return;
            }

            var slot = 0;
            var oldestLife = float.MaxValue;
            for (var i = 0; i < _damageNumberLives.Length; i++)
            {
                if (_damageNumberLives[i] <= 0f) { slot = i; break; }
                if (_damageNumberLives[i] < oldestLife)
                {
                    oldestLife = _damageNumberLives[i];
                    slot = i;
                }
            }

            ResetDamageNumberSlot(slot);
            _damageNumbers.Show(simX, simY, amount, color == FinisherDamageColor);
            _damageNumberColors[slot] = color;
            _damageNumberTexts[slot].color = color;
        }

        void ResetInactiveDamageNumberSlots()
        {
            if (_damageNumberLives == null) return;
            for (var i = 0; i < _damageNumberLives.Length; i++)
                if (_damageNumberLives[i] <= 0f)
                    ResetDamageNumberSlot(i);
        }

        void ResetDamageNumberSlot(int slot)
        {
            var text = _damageNumberTexts[slot];
            _damageNumberLives[slot] = 0f;
            _damageNumberColors[slot] = EnemyDamageColor;
            text.text = string.Empty;
            text.color = EnemyDamageColor;
            text.transform.localPosition = Vector3.zero;
            text.transform.localRotation = Quaternion.identity;
            text.transform.localScale = Vector3.one;
            if (text.gameObject.activeSelf) text.gameObject.SetActive(false);
        }

        void ClearDamageNumbers()
        {
            if (_damageNumbers == null) return;
            _damageNumbers.Clear();
            _damageNumbers.transform.localScale = Vector3.one;
            if (_damageNumberLives == null) return;
            for (var i = 0; i < _damageNumberLives.Length; i++)
                ResetDamageNumberSlot(i);
        }

        void SyncDeathNumberPunch()
        {
            if (_damageNumbers == null || _deathNumberPunchTimer <= 0f) return;
            _deathNumberPunchTimer -= Time.unscaledDeltaTime;
            var elapsed = DeathNumberPunchDuration - _deathNumberPunchTimer;
            const float riseDuration = 0.08f;
            var scale = elapsed < riseDuration
                ? Mathf.Lerp(1f, 1.18f, Mathf.SmoothStep(0f, 1f, elapsed / riseDuration))
                : Mathf.Lerp(1.18f, 1f, Mathf.SmoothStep(0f, 1f,
                    (elapsed - riseDuration) / (DeathNumberPunchDuration - riseDuration)));
            _damageNumbers.transform.localScale = Vector3.one * scale;
            if (_deathNumberPunchTimer <= 0f)
            {
                _deathNumberPunchTimer = 0f;
                _damageNumbers.transform.localScale = Vector3.one;
            }
        }

        void ApplyBossPresentation(ActorView view, in StageEntry stage)
        {
            // ActorView.ResetForPool clears the old MPB before this rented actor
            // can change logical stages. Reapply after SyncEnemy because its
            // flash path can otherwise clear the catalog tint.
            // Scale is an absolute SET in ActorView every frame, so this multiply
            // stays unconditional — skipping it would pop the boss to base size.
            view.transform.localScale *= stage.Boss.Scale;
            // §K3: the catalog tint YIELDS while a flash owns the block. Without
            // this the boss is the one enemy that can never show a hit color,
            // element or otherwise. ActorView restores its resting block on the
            // frame the flash ends and this re-tints in the same frame (it runs
            // after SyncEnemy), so the handoff costs no visible frame.
            if (view.FlashLive) return;
            if (!_bossRenderers.TryGetValue(view, out var renderers))
            {
                renderers = view.GetComponentsInChildren<Renderer>();
                _bossRenderers.Add(view, renderers);
            }
            _bossPresentationBlock.Clear();
            _bossPresentationBlock.SetColor(BaseColorId, stage.Boss.Tint);
            for (var i = 0; i < renderers.Length; i++)
                renderers[i].SetPropertyBlock(_bossPresentationBlock);
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
