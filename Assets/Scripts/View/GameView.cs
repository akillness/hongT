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
        // AMENDMENT #6 (D6.6): up to 3 simultaneous companions, slot order
        // matching HackConfig.CompanionSlots(). Unused trailing slots stay null.
        const int MaxCompanionViews = 3;
        readonly ActorView[] _companionViews = new ActorView[MaxCompanionViews];

        float _accumulator;
        /// <summary>Sim seconds advanced by THIS frame's tick batch
        /// (steps × FixedStep). ActorView's launch heuristics divide the sim
        /// step by this, never by Time.deltaTime — a frame shorter than the
        /// fixed step runs 0 or 1 ticks, so render time reports a walk as a
        /// knockback (see ActorView.SyncPlayer).</summary>
        float _simDelta;

        bool _digestWritten;
        bool _pendingBossRoar;    // §M: BossSpawned seen, boss view not yet rented
        bool _isDungeon;
        bool _isTraining;
        /// <summary>Training borrows the DUNGEON presentation whole — hazard
        /// visuals, skill row, level and combo readouts. A trial exists to
        /// practise a gimmick with your full kit, so a trial rendered on the
        /// arena HUD draws no hazards and shows no skills, which teaches
        /// nothing (caught in the browser, not by any test). Sim behaviour
        /// stays split on <see cref="_isDungeon"/>; only presentation is shared.</summary>
        bool _dungeonPresentation;
        bool _campaignUiOn;
        bool _dungeonUiOn;
        string _logicalStageId;
        // Room objective line for the live room, resolved ONCE per Begin from the
        // catalog. Cached rather than looked up per frame: the sync path runs at
        // 60 Hz and the catalog lookup is a linear id scan.
        string _roomObjective = string.Empty;


        // --- presentation state (presentation-impact-spec #1/#3/#6) ----------
        // Hit-stop / slow-mo drive Time.timeScale ONLY. Determinism-safe: the
        // fixed-step accumulator consumes scaled deltaTime exactly like a slow
        // frame — tick size and per-tick input rules never change (spec
        // §determinism). Recovery decays on unscaledDeltaTime so the pulse can
        // never wedge itself. timeScale is force-restored on EndRun, GameOver,
        // and OnDisable — every exit path.
        float _hitStopTimer;      // seconds left at HitStopScale (0.05)
        // Unscaled seconds since the last ImpactBudget Light pulse. Starts huge so the
        // very first connect of a run fires instead of being eaten by the refractory.
        float _lightImpactAge = 999f;

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

        // W12 footsteps: sim distance accumulator. 52 sim units per step at the
        // warden's 218 u/s gives ~4.2 steps/s — a jog cadence. View-only.
        const float StepStride = 52f;
        float _stepAccumulator, _lastStepX, _lastStepY;

        void SyncFootsteps()
        {
            if (Audio == null || _sim == null || _sim.Player.Health <= 0f) return;
            var dx = _sim.Player.X - _lastStepX;
            var dy = _sim.Player.Y - _lastStepY;
            _lastStepX = _sim.Player.X;
            _lastStepY = _sim.Player.Y;
            _stepAccumulator += Mathf.Abs(dx) + Mathf.Abs(dy);
            if (_stepAccumulator < StepStride) return;
            _stepAccumulator = 0f;
            Audio.PlayFootstep();
        }

        /// <summary>W14: stage id -> weapon family, deterministic and stable
        /// across sessions (plain char hash, no RNG). Empty id -> null.</summary>
        internal static string WeaponArchetypeFor(string stageId)
        {
            if (string.IsNullOrEmpty(stageId)) return null;
            var h = 0;
            for (var i = 0; i < stageId.Length; i++) h = h * 31 + stageId[i];
            switch (((h % 3) + 3) % 3)
            {
                case 0: return "dagger";
                case 1: return "bow";
                default: return "hammer";
            }
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
            _isTraining = config.Mode == GameMode.Training;
            _dungeonPresentation = _isDungeon || _isTraining;
            EndRun();
            _logicalStageId = logicalStageId ?? string.Empty;
            // Arena/prologue resolve to "" and the HUD chip stays hidden; a dungeon
            // room resolves to its own catalog objective.
            _roomObjective = _isDungeon ? StageCatalog.ObjectiveFor(_logicalStageId) : string.Empty;

            // AMENDMENT #6 (D6.6): companionId is kept for legacy single-
            // companion call sites, but the spawned roster always comes from
            // the config itself, so a config carrying 2-3 CompanionIds
            // spawns every slot even when companionId only echoes slot 0.
            var companionSlots = _isDungeon ? config.CompanionSlots() : System.Array.Empty<string>();
            var companionActive = companionSlots.Length > 0;
            // Integration 2026-08-08: arm AMENDMENT #13 (adaptive waves) and
            // #14 (graded loot) for dungeon runs only. Bounds (#15) stays dark
            // until the EnvironmentBuilder wall-ring sync (MV-2) lands —
            // enabling it alone would let the player walk through the ring.
            _sim = _isDungeon
                ? new CinderSim(in config, DungeonProgressionConfig.All)
                : new CinderSim(in config);
            _accumulator = 0f;
            _digestWritten = false;
            _lastPlayerHealth = _sim.Player.Health;
            _deathNumberPunchTimer = 0f;
            if (_damageNumbers != null) _damageNumbers.transform.localScale = Vector3.one;
            if (Hud != null) Hud.ResetRunUi();
            // AMENDMENT #11 UI: latch the run's tier for the whole run. Unconditional
            // on purpose — arena and prologue carry Difficulty.Normal, so this is what
            // clears a badge left over from a previous Nightmare descent.
            if (Hud != null) Hud.SetRunDifficulty(config.Difficulty);

            EnsureInitialized();
            _playerView.gameObject.SetActive(true);
            _playerView.ResetForPool();
            // W14: deterministic weapon silhouette per dungeon room. Arena and
            // prologue resolve to "" -> null -> the legacy equip-weapon mesh.
            _playerView.SetWeaponArchetype(WeaponArchetypeFor(_logicalStageId));
            _stepAccumulator = 0f;
            _lastStepX = _sim.Player.X;
            _lastStepY = _sim.Player.Y;

            if (_dungeonPresentation)
            {
                if (Hud != null)
                {
                    // Latched for the WHOLE run, ceremony included.
                    Hud.SetTrialMode(_isTraining);
                    // A trial has no wave table, so the wave counter reads 0 and
                    // the trial banner carries the clock instead.
                    var waves = _isDungeon ? config.ToCampaignConfig().Waves : 0;
                    var bossName = _isDungeon ? BossNameFor(_logicalStageId) : string.Empty;
                    if (!_campaignUiOn)
                    {
                        _campaignUiOn = true;
                        Hud.EnableCampaignUi(stageDisplayName, waves);
                    }
                    if (!_dungeonUiOn)
                    {
                        _dungeonUiOn = true;
                        Hud.EnableDungeonUi(bossName);
                    }
                    Hud.RefreshDungeonStage(stageDisplayName, waves, bossName, companionActive);
                    Hud.SetCampaignSurfacesVisible(true);
                }

                if (Bootstrap != null)
                {
                    for (var slot = 0; slot < companionSlots.Length && slot < MaxCompanionViews; slot++)
                    {
                        var (prefab, tint) = Bootstrap.CompanionVisual(companionSlots[slot]);
                        var view = ActorView.Create(prefab, new Color(1f, 0.86f, 0.55f), 0.92f);
                        view.name = "Companion" + slot;
                        if (tint.HasValue)
                            LobbyStaging.TintRenderers(view.gameObject, tint.Value);
                        _companionViews[slot] = view;
                    }
                }
            }
            else if (Hud != null)
            {
                Hud.SetTrialMode(false);
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
            for (var slot = 0; slot < _companionViews.Length; slot++)
            {
                var view = _companionViews[slot];
                if (view == null) continue;
                if (Application.isPlaying) Destroy(view.gameObject);
                else DestroyImmediate(view.gameObject);
                _companionViews[slot] = null;
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
            _roomObjective = string.Empty;

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
                input.CompanionSkillQueued = false;
                _accumulator -= SimConfig.FixedStep;
                steps++;
            }
            if (_accumulator >= SimConfig.FixedStep)
                _accumulator = SimConfig.FixedStep; // drop backlog beyond catch-up
            // Only consume latches when at least one tick sampled them —
            // otherwise a 144 Hz frame with no step would eat Q/E presses.
            if (steps > 0 && Input != null) Input.ClearLatches();
            _simDelta = steps * SimConfig.FixedStep;

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
            // AMENDMENT #9: a guidance card holds the run at a hard 0, and it
            // takes precedence over everything below — including the smoothing
            // ease-back, which would otherwise walk the scale up to 1 under an
            // open card. Returned early rather than folded into `target` so the
            // freeze is exact and cannot be softened by a concurrent hit-stop.
            //
            // The console proved this trap is real: it pinned timeScale at 0.2
            // when a run ended with it open, and needed a guard in
            // HudView.ResetRunUi to escape. 0.2 is merely slow; 0 is a hard
            // freeze with Time.deltaTime == 0, so even a dismiss animation would
            // not run. Two guarantees keep it escapable: ResetRunUi closes the
            // card on every run end, and the dismiss path is driven by
            // unscaled input polling, never by scaled time.
            if (Hud != null && Hud.GuidancePaused)
            {
                Time.timeScale = 0f;
                return;
            }
            // Console slow-mo first and OUTSIDE TimeEffectsAllowed: it buys
            // typing time (accessibility), not decoration — reduced-motion
            // players need it most. Determinism: timeScale only stretches
            // wall-clock per tick; tick size and input rules are unchanged
            // (presentation-impact-spec determinism note).
            var consoleOpen = Hud != null && Hud.CommandConsoleOpen;
            // The Light refractory clock runs on unscaled time and OUTSIDE the
            // reduced-motion gate, so toggling the accessibility switch mid-run can
            // never leave the clock frozen at a value that suppresses the next hit.
            if (_lightImpactAge < 999f) _lightImpactAge += Time.unscaledDeltaTime;
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
            // Hit-stop and camera punch now resolve through ImpactBudget: one tier
            // table, one merge rule, and a Light tier so an ordinary connect on a
            // surviving enemy finally has a tactile channel instead of only flash +
            // spark + SFX + number. The tiers are strictly ordered inside Resolve, so
            // a finisher that also kills still reads as a finisher.
            var impact = ImpactBudget.Resolve(
                (events & SimEvents.EnemyHit) != 0,
                (events & SimEvents.EnemyKilled) != 0,
                (events & SimEvents.ComboFinisher) != 0,
                _hitStopTimer,
                _lightImpactAge,
                ViewPrefs.TimeEffectsAllowed);
            _hitStopTimer = impact.HitStop;
            if (impact.ConsumedLight) _lightImpactAge = 0f;
            if ((events & SimEvents.ComboFinisher) != 0)
            {
                _finisherTick = true;   // gold damage numbers this batch (#6)
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
            // LAST to keep the 0.35 boss punch intact (§W). The new Light tier is
            // appended BELOW WaveStarted: it is the weakest punch in the game and
            // must never preempt a wave arrival.
            if (Rig != null)
            {
                var heavy = (events & (SimEvents.ComboFinisher | SimEvents.EnemyKilled)) != 0;
                if ((events & SimEvents.BossSpawned) != 0) Rig.Punch(0.07f, 0.35f);
                else if (heavy) Rig.Punch(impact.PunchAmplitude, impact.PunchDuration);
                else if ((events & SimEvents.WaveStarted) != 0) Rig.Punch(0.05f, 0.15f);
                else if (impact.PunchAmplitude > 0f) Rig.Punch(impact.PunchAmplitude, impact.PunchDuration);
            }

            // §W wave-arrival telegraph: warning rings at the incoming wave's
            // spawn points. Boss waves ring red/larger via the same call.
            if (Vfx != null && (events & SimEvents.WaveStarted) != 0)
                Vfx.SpawnWaveWarnings(_sim.Wave, (events & SimEvents.BossSpawned) != 0);
            // §P2: equip pickup flash — gold pulse on the player model.
            if ((events & SimEvents.EquipDropped) != 0 && _playerView != null)
                _playerView.FlashEquip();
            // Dash afterimage ghosts (vfx survey): baked-mesh trail along the
            // dash path. Reduced motion drops it entirely — it is pure flair.
            if ((events & SimEvents.DashUsed) != 0 && _playerView != null
                && !ViewPrefs.ReducedMotion)
                _playerView.TriggerAfterimages();
            // §M: boss entrance roar. The authored `show` clip (Mutant Roaring)
            // shipped dead for want of a driver. Resolved View-side on purpose:
            // a sim-side pose would be overwritten by the boss AI on the very
            // next tick, and holding the boss still sim-side would hand the
            // player free hits. Deferred one frame is impossible here — the
            // boss view is rented during the same SyncViews pass, so the roar
            // is started when the view is first seen carrying IsBoss.
            if ((events & SimEvents.BossSpawned) != 0) _pendingBossRoar = true;
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
            // §M/#9: the combo tier MUST be current before SyncPlayer resolves
            // the attack pose. ActorView only re-issues the animator value when
            // it CHANGES, so a tier that arrives after the swing starts would
            // lock the wrong variant for the entire swing, not just one frame.
            // AMENDMENT #10 widens main's dungeon gate to include a trial, so the
            // combo tier drives the pose there too. Everything else is main's.
            if (_dungeonPresentation) _playerView.SetComboTier(((IHackSnapshot)_sim).ComboIndex);
            _playerView.SyncPlayer(_sim.Player, _simDelta);
            SyncFootsteps();

            if (playerDamage > 0.01f && _damageNumbers != null)
                ShowDamageNumber(_sim.Player.X, _sim.Player.Y, playerDamage, EnemyDamageColor);

            // §K3: decay the element window once per frame, then hand the live
            // color (or default = clear) to every enemy before it syncs, so a
            // mesh struck inside the window flashes WHAT hit it.
            if (_elementTintTime > 0f) _elementTintTime -= Time.deltaTime;
            var liveTint = _elementTintTime > 0f ? _elementTint : default;
            var enemies = _sim.Enemies;
            // Retune R2 ("-60% must be VISIBLE"): pylon shield coverage is
            // judged per enemy per frame against the published hazard list —
            // cheap loop, list is already allocated by the sim's Publish.
            var hazards = _sim.Hazards;
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
                    // §M: the entrance roar starts the frame the boss's view is
                    // first rented, which is the same frame BossSpawned raised
                    // the flag. Consumed here so a second boss (monarch escort
                    // waves) cannot inherit a stale roar.
                    if (state.IsBoss && _pendingBossRoar)
                    {
                        _pendingBossRoar = false;
                        view.PlayRoar();
                    }
                }
                // SyncEnemy reports the health delta since last frame — the
                // view-side hit signal (presentation #5) that also feeds the
                // floating damage numbers (#6).
                // Retune R2: cyan shield tint while ANY live pylon covers this
                // enemy — same iso metric as the sim judge, so the visual and
                // the damage mult can never disagree. The tint drops the frame
                // after the last covering pylon dies (re-judged every frame).
                view.SetShieldTint(CoveredByLivePylon(hazards, state.X, state.Y));
                view.SetElementTint(liveTint);
                var damage = view.SyncEnemy(in state, _simDelta);

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

            if (Vfx != null) Vfx.SyncPickups(_sim.Pickups, _sim.PickupGrades);
            if (Vfx != null) Vfx.SyncWard(_sim.Player);
            // §3.6 (#9): idle threat hint — arrow appears after 0.4 s of no
            // player movement, points at the nearest living enemy.
            if (Vfx != null) Vfx.SyncThreatArrow(_sim.Player, _sim.Enemies);
            if (Hud != null) Hud.Sync(_sim);
            // AMENDMENT #10: the surge window is readable for EVERY player, sigils
            // or not — the beat is the narrative (G1), the clause is the payoff.
            //
            // A finished run publishes ZERO. UpdateSurge stops running at
            // GameOver, so the timer freezes wherever it was and the banner
            // would sit on top of the defeat panel forever — seen in the browser
            // reading "위기 0.1" behind 잿불 법정 함락.
            var surgeSim = _sim as CinderSim;
            var runLive = _sim.Mode != SimMode.GameOver;
            if (Hud != null && surgeSim != null)
                Hud.SyncSurge(
                    runLive ? surgeSim.PerilRemaining : 0f,
                    runLive ? surgeSim.SurgeRemaining : 0f,
                    surgeSim.TrainingElapsed, surgeSim.TrainingHits,
                    _isTraining && runLive);

            if (_dungeonPresentation)
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
                if (Hud != null)
                {
                    // AMENDMENT #8: reduce the per-slot cooldowns to the soonest one before
                    // handing it to the HUD — the cast order is global, so that single number
                    // is exactly what the control promises.
                    var readySlots = hack.CompanionCount;
                    var soonest = 0f;
                    var anyReady = false;
                    var anyCasting = false;
                    for (var slot = 0; slot < readySlots; slot++)
                    {
                        var cooldown = hack.CompanionSkillCooldownAt(slot);
                        if (cooldown <= 0f) anyReady = true;
                        if (slot == 0 || cooldown < soonest) soonest = cooldown;
                        if (hack.CompanionSkillCastingAt(slot)) anyCasting = true;
                    }
                    Hud.SyncCompanionSkill(readySlots, soonest, anyReady);

                    // Companion stance readout: the console/keys' FocusAttack=Follow,
                    // Defend=Hold, Recall=Follow orders drive CompanionBehavior; any slot
                    // engaged means the Follow order is actively pursuing, not just escorting.
                    var stanceEngaged = false;
                    for (var slot = 0; slot < readySlots; slot++)
                        if (hack.CompanionEngagedAt(slot)) { stanceEngaged = true; break; }
                    Hud.SyncCompanionStance(readySlots, hack.CompanionBehavior, stanceEngaged);
                    // Command agent: the same primitives, pushed once per frame
                    // so a typed sequence can gate on readiness and wait for the
                    // SIM to acknowledge each step before starting the next one
                    // (HudView.CommandAgent.cs -> CommandSequenceRunner).
                    Hud.SyncCommandAgent(runLive, _sim.Charge,
                        hack.SkillCooldowns, hack.DashCooldown,
                        readySlots, soonest, anyCasting,
                        hack.CompanionBehavior, _sim.LivingEnemies);

                    // Room objective readout: the contiguous route never returns to the
                    // lobby between rooms, so BossAlive is what re-frames the same
                    // objective as the room's final beat.
                    Hud.SyncRoomObjective(_roomObjective, _sim.BossAlive);
                }

                if (Vfx != null)
                    Vfx.SyncExtraction(hack.ExtractionProgress, hack.ExtractionTarget, _sim.Player);
                // §P2 rank glow. ComboIndex IS the current swing during Attack
                // (sim advances it at swing end) and preloads the next tier
                // between swings — both correct. SetComboTier itself is hoisted
                // above SyncPlayer (§M/#9) because it now selects the pose too.
                _playerView.SetEquipRanks(_sim.WeaponRank, _sim.LanternRank, _sim.CloakRank);
                // §Lane P: socket props follow the same live ranks (idempotent
                // per band — a mid-run rank-up swaps the prop immediately).
                _playerView.AttachEquipProps(_sim.WeaponRank, _sim.LanternRank, _sim.CloakRank);
                // AMENDMENT #6 (D6.6): one gaze/idle resolution per active
                // slot. Each companion tracks the nearest living enemy inside
                // its own attack range independently of its siblings.
                for (var slot = 0; slot < _companionViews.Length; slot++)
                {
                    var view = _companionViews[slot];
                    if (view == null) continue;
                    var companionX = hack.CompanionXAt(slot);
                    var companionY = hack.CompanionYAt(slot);
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
                        var deltaX = enemy.X - companionX;
                        var deltaY = (enemy.Y - companionY) * SimConfig.IsoY;
                        var distSq = deltaX * deltaX + deltaY * deltaY;
                        if (distSq >= bestSq) continue;
                        bestSq = distSq;
                        gazeYaw = Mathf.Round(
                            Mathf.Atan2(deltaX, -(enemy.Y - companionY))
                            * Mathf.Rad2Deg / 22.5f) * 22.5f;
                    }
                    var playerDeltaX = _sim.Player.X - companionX;
                    var playerDeltaY = _sim.Player.Y - companionY;
                    var restIdle = float.IsNaN(gazeYaw) && !hack.CompanionAttackingAt(slot)
                        && playerDeltaX * playerDeltaX + playerDeltaY * playerDeltaY
                           < HackSpec.CompanionFollowOffset * HackSpec.CompanionFollowOffset * 2.25f;
                    view.SyncCompanion(companionX, companionY, hack.CompanionFacingAt(slot),
                        hack.CompanionAttackingAt(slot), gazeYaw, restIdle);
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
            // Retune R2: the shield read outranks the catalog tint too — the
            // boss is a prime target to route INTO an aura, and a permanent
            // catalog re-tint after SyncEnemy would silently erase the cyan.
            if (view.FlashLive || view.ShieldLive) return;
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

        /// <summary>
        /// True when a living Ember Pylon's aura covers the sim position —
        /// mirrors CinderSim.EnemyDamageTakenMult (Hp &gt; 0 + iso-weighted
        /// distance &lt;= CampaignSpec.PylonAuraRadius) so the shield tint and
        /// the -60% judge stay one truth. Non-pylon kinds skip in O(1); the
        /// arena path publishes zero hazards so this is dungeon-only cost.
        /// </summary>
        static bool CoveredByLivePylon(
            IReadOnlyList<HazardState> hazards, float x, float y)
        {
            for (var i = 0; i < hazards.Count; i++)
            {
                var hazard = hazards[i];
                if (hazard.Kind != HazardKind.EmberPylon || hazard.Hp <= 0f) continue;
                var deltaX = x - hazard.X;
                var deltaY = (y - hazard.Y) * SimConfig.IsoY;
                if (deltaX * deltaX + deltaY * deltaY
                    <= CampaignSpec.PylonAuraRadius * CampaignSpec.PylonAuraRadius)
                    return true;
            }
            return false;
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
