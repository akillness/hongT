// Deterministic fixed-step port of the original Cinder Court run loop.
// Numeric truth: docs/SIM_SPEC.md. Contract: Assets/Scripts/Sim/SimTypes.cs (FROZEN).
// No UnityEngine, no RNG, no LINQ, no per-tick heap allocation.
using System;
using System.Collections.Generic;

namespace CinderCourt.Sim
{
    /// <summary>
    /// Pure C# simulation. One <see cref="Tick"/> advances exactly 1/60 s and runs the
    /// original fixedUpdate order: player -> enemies -> skills -> pickups -> wave.
    /// The default constructor is the frozen arena run (docs/SIM_SPEC.md); the
    /// <see cref="CampaignConfig"/> constructor layers the campaign amendment
    /// (docs/SIM_SPEC_CAMPAIGN.md) on top without touching any arena number; the
    /// <see cref="HackConfig"/> constructor adds the v0.2.0 hack &amp; slash rules
    /// (docs/SIM_SPEC_HACKSLASH.md) — prologue, combo, dash, skills, elites,
    /// companion and boss phase 2 — again without moving an arena number.
    /// </summary>
    public sealed class CinderSim : ICinderSim, ICampaignSnapshot, IHackSnapshot, IRunPreparationSnapshot
    {
        // --- spec constants that SimConfig does not expose (docs/SIM_SPEC.md) ---
        private const float EnemyHealthPerWave = 9f;        // 58 + min(92, (wave-1)*9)
        private const float EnemyHealthWaveCap = 92f;
        private const float EnemyCooldownPerWave = 0.025f;  // 1.22 + min(0.38, wave*0.025)
        private const float EnemyCooldownWaveCap = 0.38f;
        private const float EnemySpeedBase = 78f;           // min(128, 78 + wave*3.2 + (id%3)*2.5)
        private const float EnemySpeedPerWave = 3.2f;
        private const float EnemySpeedIdStep = 2.5f;
        private const float EnemySpeedCap = 128f;
        private const float ContactDamageBase = 7f;         // min(18, 7 + floor(wave*0.8))
        private const float ContactDamagePerWave = 0.8f;
        private const float ContactDamageCap = 18f;
        private const float FirstAttackDelayStep = 0.18f;   // (id%3)*0.18
        private const float EnemyChaseSlack = 5f;           // chase while distance > range-5
        private const float EnemyFacingDeadzone = 4f;       // facing flips when |dx| > 4
        private const int WaveSpawnBase = 3;                // min(20, 3 + floor(wave*1.2))
        private const float WaveSpawnPerWave = 1.2f;
        private const float SpawnIntervalBase = 0.62f;      // max(0.28, 0.62 - wave*0.018)
        private const float SpawnIntervalPerWave = 0.018f;
        private const float SpawnIntervalMin = 0.28f;
        private const int KillScorePerWave = 100;
        private const int BossKillScorePerWave = 1000;
        private const float NovaFlashDuration = 0.42f;
        private const int AttackClipFrames = 5;             // 5-frame attack clip @ 12 fps
        private const float AttackClipFps = 12f;
        private const int AttackActiveFirstFrame = 2;       // SimConfig.AttackActiveFrom
        private const int AttackActiveLastFrame = 3;        // SimConfig.AttackActiveTo (exclusive frame 4)
        private const int EnemyContactFrame = 2;            // SimConfig.EnemyContactDelay
        private const float SeparationMinDistanceSq = 0.01f;
        private const float MoveEpsilon = 0.001f;
        private const int VisualRotation = 4;               // (wave + spawnIndex) % 4
        private const int BossVisualPeriod = 10;            // wave%10==5 commander, wave%10==0 monarch
        private const string OverrunReason = "overrun";
        private const string RunningReason = "";

        /// <summary>Enemy record: the published state plus sim-only bookkeeping.</summary>
        private struct Enemy
        {
            public EnemyState State;
            public float AttackCooldown;
            public bool DidDamage;
            public int LastHitAttack;
            // --- hack & slash amendment (inert outside GameMode.Dungeon) ---
            public bool IsElite;
            public float KnockX, KnockY;   // knockback velocity in px/s
            public float KnockTime;        // seconds of knockback left
        }

        /// <summary>Mutable per-hazard bookkeeping (config stays immutable/shared).</summary>
        private struct HazardRuntime
        {
            public int Cycle;          // last completed vent cycle / current activation / wall cycle
            public float Hold;         // relic-altar dwell seconds
            public float Cooldown;     // relic-altar cooldown seconds
            // --- amendment #5 (docs/SIM_SPEC_DUNGEONS.md) ---
            public float Hp;           // ember-pylon remaining hp (0 = destroyed)
            public int Tick;           // last completed ash-wall damage tick index
            public int LastHitAttack;  // ember-pylon one-hit-per-attackId guard
        }

        private static readonly HazardConfig[] NoHazards = new HazardConfig[0];
        private static readonly HazardRuntime[] NoHazardRuntime = new HazardRuntime[0];

        private Enemy[] _enemies = new Enemy[SimConfig.EnemyCap];
        private int _enemyCount;
        private PickupState[] _pickups = new PickupState[SimConfig.EnemyCap];
        private int _pickupCount;

        private readonly List<EnemyState> _enemyView = new List<EnemyState>(SimConfig.EnemyCap);
        private readonly List<PickupState> _pickupView = new List<PickupState>(SimConfig.EnemyCap);

        private PlayerState _player;
        private SimMode _mode;
        private SimEvents _events;

        private int _wave;
        private int _waveSeed;
        private int _score;
        private int _kills;
        private int _relics;
        private float _charge;
        private float _novaCooldown;
        private float _wardCooldown;
        private float _novaFlash;
        private float _novaX;
        private float _novaY;
        private int _pendingSpawns;
        private bool _pendingBoss;
        private int _spawnIndexInWave;
        private float _spawnTimer;
        private float _intermission;
        private int _livingEnemies;
        private int _nextEnemyId;
        private int _nextPickupId;
        private string _reason;

        // --- campaign amendment state (inert on the arena path) ---
        private readonly bool _campaign;
        private readonly CampaignConfig _config;
        private readonly HazardConfig[] _hazards;
        private readonly HazardRuntime[] _hazardRuntime;
        private readonly List<HazardState> _hazardView;
        private readonly string _stageId;
        private float _stageTime;
        private bool _stageCleared;
        private int _livingBosses;
        private int _weaponRank, _lanternRank, _cloakRank;
        private float _playerDamage;
        private float _playerMaxHealth;
        private float _lanternRegen;
        private float _playerSpeed;
        private float _baseDamage;
        private float _baseMaxHealth;
        private float _baseRegen;
        private float _baseSpeed;

        /// <summary>Elite corpse marker: the extraction target of §3.</summary>
        private struct Corpse
        {
            public float X, Y;
            public float Life;
            public EnemyVisual Visual;
        }

        // --- hack &amp; slash amendment state (inert outside GameMode.Dungeon) ---
        private readonly bool _hack;
        private readonly HackConfig _hackConfig;
        private readonly GameMode _gameMode;
        private readonly bool _prologue;
        private readonly bool _dungeon;
        private readonly bool _companionActive;
        private readonly float[] _skillCooldowns = new float[HackSpec.SkillCount];
        private int _level;
        private int _xp;
        private int _comboIndex;      // hit the next Space press starts (0..2)
        private int _comboSwing;      // hit currently swinging, -1 when idle
        private float _comboLink;     // seconds left of the 0.9 s chain window
        private bool _comboLanded;    // current swing already damaged something
        private float _dashCooldown;
        private float _dashTime;
        private float _dashDirX, _dashDirY;
        private float _castInvuln;
        private float _shield;
        private float _shieldTime;
        private float _pulseTime;
        private float _pulseTick;
        private float _pulseX, _pulseY;
        private int _elitesAlive;
        private int _spawnOrdinal;
        private bool _eliteThisWave;
        private bool _extractedThisWave;
        private float _extractionProgress;
        private float _extractionTarget;
        private float _extractionBonus;
        private int _rosterMask;
        private Corpse[] _corpses = new Corpse[8];
        private int _corpseCount;
        private float _companionX, _companionY;
        private float _companionTimer;
        private float _companionShow;
        private int _companionFacing;
        private CompanionBehavior _companionBehavior;
        private readonly float _boltDamage;
        private readonly float _pulseTickDamage;
        private readonly float _ashNovaDamage;
        private readonly float _companionAttackInterval;
        private readonly float _companionAttackRange;
        private readonly float _companionDamageScale;
        private bool _emberRestOpen;
        private int _emberRestRoomIndex;
        private int _emberRestSeed;
        private PreparationOffer _emberRestOffer0, _emberRestOffer1, _emberRestOffer2;
        private PreparationOffer _selectedPreparation;
        private readonly PreparationOffer _appliedPreparationInput;
        private float _bossHp, _bossMaxHp;
        private int _bossPhase;
        private bool _bossPhase2Done;
        private bool _bossPhase3Done;

        /// <summary>Arena run — the frozen SIM_SPEC path. Behaviour must never change.</summary>
        public CinderSim()
        {
            _campaign = false;
            _config = default;
            _hack = false;
            _hackConfig = default;
            _gameMode = GameMode.Arena;
            _prologue = false;
            _dungeon = false;
            _companionActive = false;
            _boltDamage = HackSpec.BoltDamage;
            _pulseTickDamage = HackSpec.PulseTickDamage;
            _ashNovaDamage = HackSpec.AshNovaDamage;
            _companionAttackInterval = HackSpec.CompanionAttackInterval;
            _companionAttackRange = HackSpec.CompanionAttackRange;
            _companionDamageScale = HackSpec.CompanionDamageScale;
            _hazards = NoHazards;
            _hazardRuntime = NoHazardRuntime;
            _hazardView = new List<HazardState>(0);
            _stageId = string.Empty;
            Restart();
        }

        /// <summary>Campaign run — arena rules plus docs/SIM_SPEC_CAMPAIGN.md.</summary>
        public CinderSim(in CampaignConfig config)
        {
            _campaign = true;
            _config = config;
            _hack = false;
            _hackConfig = default;
            // v0.1 compatibility path: campaign rules without the v0.2 combat kit.
            _gameMode = GameMode.Arena;
            _prologue = false;
            _dungeon = false;
            _companionActive = false;
            _boltDamage = HackSpec.BoltDamage;
            _pulseTickDamage = HackSpec.PulseTickDamage;
            _ashNovaDamage = HackSpec.AshNovaDamage;
            _companionAttackInterval = HackSpec.CompanionAttackInterval;
            _companionAttackRange = HackSpec.CompanionAttackRange;
            _companionDamageScale = HackSpec.CompanionDamageScale;
            _hazards = config.Hazards ?? NoHazards;
            _hazardRuntime = _hazards.Length == 0 ? NoHazardRuntime : new HazardRuntime[_hazards.Length];
            _hazardView = new List<HazardState>(_hazards.Length);
            _stageId = config.StageId ?? string.Empty;
            for (int index = 0; index < _hazards.Length; index += 1)
            {
                _hazardView.Add(default);
            }
            Restart();
        }

        /// <summary>Hack &amp; slash run — docs/SIM_SPEC_HACKSLASH.md §0-§7.</summary>
        public CinderSim(in HackConfig config)
        {
            _hack = true;
            _gameMode = config.Mode;
            _prologue = config.Mode == GameMode.Prologue;
            _dungeon = config.Mode == GameMode.Dungeon;
            _campaign = _dungeon;
            _appliedPreparationInput = _dungeon ? config.PreparationOffer : default;

            HackConfig configured = config;
            float boltDamage = HackSpec.BoltDamage;
            float pulseTickDamage = HackSpec.PulseTickDamage;
            float ashNovaDamage = HackSpec.AshNovaDamage;
            float companionAttackInterval = HackSpec.CompanionAttackInterval;
            float companionAttackRange = HackSpec.CompanionAttackRange;
            float companionDamageScale = HackSpec.CompanionDamageScale;
            if (_dungeon)
            {
                ApplyPreparation(
                    in config.PreparationOffer,
                    ref configured,
                    ref boltDamage,
                    ref pulseTickDamage,
                    ref ashNovaDamage,
                    ref companionAttackInterval,
                    ref companionAttackRange,
                    ref companionDamageScale);
            }

            _hackConfig = configured;
            _boltDamage = boltDamage;
            _pulseTickDamage = pulseTickDamage;
            _ashNovaDamage = ashNovaDamage;
            _companionAttackInterval = companionAttackInterval;
            _companionAttackRange = companionAttackRange;
            _companionDamageScale = companionDamageScale;
            _config = _dungeon ? configured.ToCampaignConfig() : default;
            _companionActive = _dungeon && !string.IsNullOrEmpty(configured.CompanionId);
            _hazards = _dungeon ? (_config.Hazards ?? NoHazards) : NoHazards;
            _hazardRuntime = _hazards.Length == 0 ? NoHazardRuntime : new HazardRuntime[_hazards.Length];
            _hazardView = new List<HazardState>(_hazards.Length);
            _stageId = _prologue
                ? HackSpec.PrologueStageId
                : (_dungeon ? (_config.StageId ?? string.Empty) : string.Empty);
            for (int index = 0; index < _hazards.Length; index += 1)
            {
                _hazardView.Add(default);
            }
            Restart();
        }

        /// <summary>
        /// Applies one validated room-local Ember Rest offer. This constructor-only
        /// normalization leaves every non-dungeon config path untouched.
        /// </summary>
        private static void ApplyPreparation(
            in PreparationOffer offer,
            ref HackConfig config,
            ref float boltDamage,
            ref float pulseTickDamage,
            ref float ashNovaDamage,
            ref float companionAttackInterval,
            ref float companionAttackRange,
            ref float companionDamageScale)
        {
            if (offer.Variant < 1 || offer.Variant > 3
                || offer.Magnitude < 1 || offer.Magnitude > 2)
            {
                return;
            }

            switch (offer.Kind)
            {
                case PreparationOfferKind.Stat:
                    switch (offer.Variant)
                    {
                        case 1:
                            config.MetaStats.Attack = HackSpec.ClampStat(config.MetaStats.Attack + offer.Magnitude);
                            break;
                        case 2:
                            config.MetaStats.Vitality = HackSpec.ClampStat(config.MetaStats.Vitality + offer.Magnitude);
                            break;
                        case 3:
                            config.MetaStats.Swiftness = HackSpec.ClampStat(config.MetaStats.Swiftness + offer.Magnitude);
                            break;
                    }
                    break;
                case PreparationOfferKind.SkillRune:
                    switch (offer.Variant)
                    {
                        case 1:
                            boltDamage = HackSpec.BoltDamage * (1f + 0.10f * offer.Magnitude);
                            break;
                        case 2:
                            pulseTickDamage = HackSpec.PulseTickDamage * (1f + 0.10f * offer.Magnitude);
                            break;
                        case 3:
                            ashNovaDamage = HackSpec.AshNovaDamage * (1f + 0.10f * offer.Magnitude);
                            break;
                    }
                    break;
                case PreparationOfferKind.GuardianResonance:
                    switch (offer.Variant)
                    {
                        case 1:
                            companionAttackInterval = MathF.Max(
                                0.5f,
                                HackSpec.CompanionAttackInterval * (1f - 0.10f * offer.Magnitude));
                            break;
                        case 2:
                            companionAttackRange = HackSpec.CompanionAttackRange + 20f * offer.Magnitude;
                            break;
                        case 3:
                            companionDamageScale = HackSpec.CompanionDamageScale
                                * (1f + 0.10f * offer.Magnitude);
                            break;
                    }
                    break;
            }
        }

        // --- ISimSnapshot ----------------------------------------------------
        public SimMode Mode => _mode;
        public int Wave => _wave;
        public int Score => _score;
        public int Kills => _kills;
        public int Relics => _relics;
        public float Charge => _charge;
        // In the dungeon R is ash-nova and F is void-aegis, so the arena HUD slots
        // report the corresponding §2.3 cooldowns (8 s / 12 s instead of 6.5 s / 9 s).
        public float NovaCooldown => _dungeon ? _skillCooldowns[HackSpec.SkillNova] : _novaCooldown;
        public float WardCooldown => _dungeon ? _skillCooldowns[HackSpec.SkillAegis] : _wardCooldown;
        public float NovaFlash => _novaFlash;
        public int PendingSpawns => _pendingSpawns;
        public int LivingEnemies => _livingEnemies;
        public PlayerState Player => _player;
        public IReadOnlyList<EnemyState> Enemies => _enemyView;
        public IReadOnlyList<PickupState> Pickups => _pickupView;
        public SimEvents Events => _events;
        public float NovaX => _novaX;
        public float NovaY => _novaY;

        public RunDigest Digest => new RunDigest
        {
            Score = _score,
            Wave = _wave,
            Kills = _kills,
            Relics = _relics,
            HealthRemaining = _player.Health,
            Reason = _reason,
        };

        // --- ICampaignSnapshot -----------------------------------------------
        public string StageId => _stageId;
        public bool BossAlive => _livingBosses > 0;
        public bool StageCleared => _stageCleared;
        public IReadOnlyList<HazardState> Hazards => _hazardView;
        public int WeaponRank => _weaponRank;
        public int LanternRank => _lanternRank;
        public int CloakRank => _cloakRank;

        // --- IHackSnapshot ---------------------------------------------------
        public GameMode HackMode => _gameMode;
        public int Level => _level;
        public int Xp => _xp;
        public int XpNext => HackSpec.XpToNextLevel(_level);
        public int ComboIndex => _comboIndex;
        public float DashCooldown => _dashCooldown;
        public IReadOnlyList<float> SkillCooldowns => _skillCooldowns;
        public float Shield => _shield;
        public int ElitesAlive => _elitesAlive;
        public float ExtractionProgress => _extractionProgress;
        public float ExtractionTarget => _extractionTarget;
        public float CompanionX => _companionX;
        public float CompanionY => _companionY;
        public bool CompanionAttacking => _companionShow > 0f;
        public CompanionBehavior CompanionBehavior => _companionBehavior;
        public float BossHp => _bossHp;
        public float BossMaxHp => _bossMaxHp;
        public int BossPhase => _bossPhase;
        public int RosterMask => _rosterMask;
        public int EmberRestRoomIndex => _emberRestRoomIndex;
        public bool EmberRestOpen => _emberRestOpen;
        public int EmberRestSeed => _emberRestSeed;
        public PreparationOffer EmberRestOffer0 => _emberRestOffer0;
        public PreparationOffer EmberRestOffer1 => _emberRestOffer1;
        public PreparationOffer EmberRestOffer2 => _emberRestOffer2;
        public PreparationOffer SelectedPreparation => _selectedPreparation;
        /// <summary>Ember Rest offer supplied to this dungeon run at construction.</summary>
        public PreparationOffer AppliedPreparationInput => _appliedPreparationInput;
        public int CompanionFacing => _companionFacing;

        // --- Pure wave arithmetic (shared by sim and tests) -------------------

        /// <summary>Spawn queue length for a wave, boss slot included, enemy cap applied.</summary>
        public static int SpawnCountForWave(int wave)
        {
            int queued = WaveSpawnBase + (int)MathF.Floor(wave * WaveSpawnPerWave);
            if (IsBossWave(wave))
            {
                queued += 1;
            }
            return Math.Min(SimConfig.EnemyCap, queued);
        }

        /// <summary>True when the wave opens with one extra boss spawn.</summary>
        public static bool IsBossWave(int wave) => wave % SimConfig.BossEveryWaves == 0;

        /// <summary>Spawn point index for an enemy id: (waveSeed + id*3) % 8.</summary>
        public static int SpawnPointIndexFor(int wave, int enemyId)
        {
            int waveSeed = (wave * 3) % SimConfig.SpawnPoints.Length;
            return (waveSeed + enemyId * 3) % SimConfig.SpawnPoints.Length;
        }

        /// <summary>Boss-wave escorts for a stage: min(8, 3 + stageIndex*2).</summary>
        public static int EscortCountForStage(int stageIndex)
        {
            return Math.Min(CampaignSpec.EscortCap, CampaignSpec.EscortBase + stageIndex * CampaignSpec.EscortPerStage);
        }

        /// <summary>
        /// Campaign spawn queue length. Waves 1..W keep the arena formula without the
        /// arena's every-fifth-wave boss slot; wave W+1 is boss + escorts.
        /// </summary>
        public static int SpawnCountForStageWave(in CampaignConfig config, int wave)
        {
            if (wave > config.Waves)
            {
                return Math.Min(SimConfig.EnemyCap, 1 + EscortCountForStage(config.StageIndex));
            }
            return Math.Min(SimConfig.EnemyCap, WaveSpawnBase + (int)MathF.Floor(wave * WaveSpawnPerWave));
        }

        // --- ICinderSim ------------------------------------------------------

        public void Restart()
        {
            ResetHackRun();
            ResetCampaignRun();

            _enemyCount = 0;
            _pickupCount = 0;
            _livingEnemies = 0;
            _score = 0;
            _kills = 0;
            _relics = 0;
            _charge = SimConfig.LanternMax;
            _novaCooldown = 0f;
            _wardCooldown = 0f;
            _novaFlash = 0f;
            _nextEnemyId = 1;
            _nextPickupId = 1;
            _reason = RunningReason;
            _events = SimEvents.None;

            _player = default;
            _player.X = SimConfig.ArenaX;
            _player.Y = SimConfig.ArenaY + SimConfig.PlayerStartYOffset;
            _player.Facing = 1;
            _player.Health = _playerMaxHealth;
            _player.Action = ActorAction.Idle;
            _player.ActionTime = 0f;
            _player.AttackId = 0;

            _novaX = _player.X;
            _novaY = _player.Y;

            ApplyPillars(ref _player.X, ref _player.Y, CampaignSpec.PlayerPushRadius);
            ResetCompanion();

            StartWave(1);
            _events = SimEvents.None;
            Publish();
        }

        /// <summary>
        /// Opens the post-room preparation state only after a cleared dungeon stage.
        /// The caller owns presentation and the next-stage handoff; the simulation
        /// owns the reproducible offers and selected temporary preparation.
        /// </summary>
        public bool BeginEmberRest(int roomIndex, int rewardSeed)
        {
            if (!_dungeon || !_stageCleared || _emberRestOpen
                || roomIndex < 1 || roomIndex > CampaignSpec.MaxEmberRestRoomIndex)
            {
                return false;
            }

            _emberRestOpen = true;
            _emberRestRoomIndex = roomIndex;
            _emberRestSeed = rewardSeed;
            _emberRestOffer0 = BuildPreparationOffer(rewardSeed, roomIndex, 0);
            _emberRestOffer1 = BuildPreparationOffer(rewardSeed, roomIndex, 1);
            _emberRestOffer2 = BuildPreparationOffer(rewardSeed, roomIndex, 2);
            _selectedPreparation = default;
            return true;
        }

        /// <summary>Selects an offered temporary preparation; false for invalid state or index.</summary>
        public bool TrySelectPreparation(int offerIndex)
        {
            if (!_emberRestOpen)
            {
                return false;
            }

            PreparationOffer offer;
            switch (offerIndex)
            {
                case 0: offer = _emberRestOffer0; break;
                case 1: offer = _emberRestOffer1; break;
                case 2: offer = _emberRestOffer2; break;
                default: return false;
            }

            if (!offer.IsValid)
            {
                return false;
            }

            _selectedPreparation = offer;
            return true;
        }

        /// <summary>Records an explicit no-choice outcome for the current Ember Rest.</summary>
        public bool DeferPreparation()
        {
            if (!_emberRestOpen)
            {
                return false;
            }

            _selectedPreparation = default;
            return true;
        }

        /// <summary>Closes Ember Rest while preserving the selected run-scoped preparation.</summary>
        public bool EndEmberRest()
        {
            if (!_emberRestOpen)
            {
                return false;
            }

            _emberRestOpen = false;
            return true;
        }

        private static PreparationOffer BuildPreparationOffer(int seed, int roomIndex, int slot)
        {
            uint value = PreparationHash(seed, roomIndex, slot);
            return new PreparationOffer
            {
                Kind = (PreparationOfferKind)(1 + (int)(value % 3u)),
                Variant = 1 + (int)((value >> 8) % 3u),
                Magnitude = 1 + (int)((value >> 16) % 2u),
            };
        }

        private static uint PreparationHash(int seed, int roomIndex, int slot)
        {
            unchecked
            {
                uint value = (uint)seed;
                value ^= (uint)roomIndex * 0x9E3779B9u;
                value ^= (uint)(slot + 1) * 0x85EBCA6Bu;
                value ^= value >> 16;
                value *= 0xC2B2AE35u;
                value ^= value >> 13;
                return value;
            }
        }

        public void Tick(in SimInput input)
        {
            _events = SimEvents.None;

            // The original restarts from the key handler, i.e. between frames: the
            // restarted state is what the next step sees.
            if (input.RestartQueued)
            {
                Restart();
                return;
            }

            if (_mode != SimMode.Running && _mode != SimMode.WaveClear)
            {
                return;
            }

            const float dt = SimConfig.FixedStep;

            // Skill keys land between frames in the original, so they resolve before
            // the step body: a ward cast is already up for this step's enemy contacts.
            CastSkills(in input);

            UpdatePlayer(dt, in input);
            if (_companionActive && _mode != SimMode.GameOver)
            {
                UpdateCompanionBehavior(in input);
                UpdateCompanion(dt);
            }
            UpdateEnemies(dt);
            if (_dungeon && _mode != SimMode.GameOver)
            {
                UpdateBossPhase();
            }
            if (_campaign && _mode != SimMode.GameOver)
            {
                UpdateHazards(dt);
            }
            if (_mode != SimMode.GameOver)
            {
                UpdateSkills(dt);
                UpdatePickups(dt);
                if (_dungeon)
                {
                    UpdateExtraction(dt);
                }
                UpdateWave(dt);
            }

            Publish();
        }

        // --- Skills ----------------------------------------------------------

        private void CastSkills(in SimInput input)
        {
            // §1: the prologue is movement + basic attack only — every skill key,
            // the dash included, is ignored.
            if (_prologue)
            {
                return;
            }

            // §2.2/§2.3: the dungeon replaces the arena kit with dash + four skills.
            if (_dungeon)
            {
                CastDungeonSkills(in input);
                return;
            }

            if (input.NovaQueued && _novaCooldown <= 0f && _charge >= SimConfig.NovaCost)
            {
                CastNova();
            }
            if (input.WardQueued && _wardCooldown <= 0f && _charge >= SimConfig.WardCost)
            {
                CastWard();
            }
        }

        private void CastNova()
        {
            _charge -= SimConfig.NovaCost;
            _novaCooldown = SimConfig.NovaCooldown;
            _novaFlash = NovaFlashDuration;
            _novaX = _player.X;
            _novaY = _player.Y;
            _events |= SimEvents.NovaCast;

            for (int index = 0; index < _enemyCount; index += 1)
            {
                ref Enemy enemy = ref _enemies[index];
                if (enemy.State.Dead)
                {
                    continue;
                }
                float deltaX = enemy.State.X - _player.X;
                float deltaY = (enemy.State.Y - _player.Y) * SimConfig.IsoY;
                if (deltaX * deltaX + deltaY * deltaY <= SimConfig.NovaRadius * SimConfig.NovaRadius)
                {
                    DamageEnemy(ref enemy, SimConfig.NovaDamage);
                }
            }
        }

        private void CastWard()
        {
            _charge -= SimConfig.WardCost;
            _wardCooldown = SimConfig.WardCooldown;
            _player.WardTime = SimConfig.WardDuration;
            _events |= SimEvents.WardCast;
        }

        // --- Hack & slash kit (docs/SIM_SPEC_HACKSLASH.md §2.2-§2.4) ----------

        /// <summary>
        /// Dungeon casting order: dash first (it cancels the swing), then Q/E/R/F.
        /// The sim only trusts the booleans — the key remap is the view's business.
        /// </summary>
        private void CastDungeonSkills(in SimInput input)
        {
            if (input.DashQueued && _dashCooldown <= 0f && _dashTime <= 0f && _charge >= HackSpec.DashCost)
            {
                CastDash(in input);
            }
            if (input.BoltQueued && _skillCooldowns[HackSpec.SkillBolt] <= 0f && _charge >= HackSpec.BoltCost)
            {
                CastRiftBolt();
            }
            if (input.PulseQueued && _skillCooldowns[HackSpec.SkillPulse] <= 0f && _charge >= HackSpec.PulseCost)
            {
                CastGravePulse();
            }
            if (input.NovaQueued && _skillCooldowns[HackSpec.SkillNova] <= 0f && _charge >= HackSpec.AshNovaCost)
            {
                CastAshNova();
            }
            if (input.WardQueued && _skillCooldowns[HackSpec.SkillAegis] <= 0f && _charge >= HackSpec.AegisCost)
            {
                CastVoidAegis();
            }
        }

        /// <summary>§2.2: 190 px in 0.22 s, invulnerable throughout, cancels the combo.</summary>
        private void CastDash(in SimInput input)
        {
            float directionX = input.MoveX;
            float directionY = input.MoveY;
            float length = Hypot(directionX, directionY);
            if (length > 0f)
            {
                directionX /= length;
                directionY /= length;
            }
            else
            {
                directionX = _player.Facing;
                directionY = 0f;
            }

            _charge -= HackSpec.DashCost;
            _dashCooldown = HackSpec.DashCooldownSeconds;
            _dashTime = HackSpec.DashTime;
            _dashDirX = directionX;
            _dashDirY = directionY;
            if (directionX != 0f)
            {
                _player.Facing = directionX > 0f ? 1 : -1;
            }
            _comboSwing = -1;
            _comboLink = HackSpec.ComboLinkWindow;
            SetPlayerAction(ActorAction.Avoid, true);
            _events |= SimEvents.DashUsed;
        }

        /// <summary>§2.3 Q: 145 to the nearest target inside 420 px, 60% splash at 115.</summary>
        private void CastRiftBolt()
        {
            _charge -= HackSpec.BoltCost;
            _skillCooldowns[HackSpec.SkillBolt] = HackSpec.BoltCooldown;
            _events |= SimEvents.BoltCast;

            int target = NearestEnemyIndex(_player.X, _player.Y, HackSpec.BoltRange);
            if (target < 0)
            {
                return;
            }

            float originX = _enemies[target].State.X;
            float originY = _enemies[target].State.Y;

            for (int index = 0; index < _enemyCount; index += 1)
            {
                if (index == target)
                {
                    continue;
                }
                ref Enemy splashed = ref _enemies[index];
                if (splashed.State.Dead
                    || !IsoWithin(originX, originY, splashed.State.X, splashed.State.Y, HackSpec.BoltSplashRadius))
                {
                    continue;
                }
                DamageEnemy(ref splashed, ElementalDamage(
                    _boltDamage * HackSpec.BoltSplashScale, HackSpec.BoltElement, splashed.State.Visual));
            }

            ref Enemy primary = ref _enemies[target];
            DamageEnemy(ref primary, ElementalDamage(
                _boltDamage, HackSpec.BoltElement, primary.State.Visual));
        }

        /// <summary>§2.3 E: a 190 px field at the cast point, 26 every 0.5 s for 3 s.</summary>
        private void CastGravePulse()
        {
            _charge -= HackSpec.PulseCost;
            _skillCooldowns[HackSpec.SkillPulse] = HackSpec.PulseCooldown;
            _pulseTime = HackSpec.PulseDuration;
            _pulseTick = HackSpec.PulseTickInterval;
            _pulseX = _player.X;
            _pulseY = _player.Y;
            _events |= SimEvents.PulseCast;
        }

        /// <summary>§2.3 R: 230 px burst for 110 with a 120 px knockback.</summary>
        private void CastAshNova()
        {
            _charge -= HackSpec.AshNovaCost;
            _skillCooldowns[HackSpec.SkillNova] = HackSpec.AshNovaCooldown;
            _novaFlash = NovaFlashDuration;
            _novaX = _player.X;
            _novaY = _player.Y;
            _events |= SimEvents.NovaCast;

            for (int index = 0; index < _enemyCount; index += 1)
            {
                ref Enemy enemy = ref _enemies[index];
                if (enemy.State.Dead
                    || !IsoWithin(_player.X, _player.Y, enemy.State.X, enemy.State.Y, HackSpec.AshNovaRadius))
                {
                    continue;
                }
                Knockback(ref enemy, HackSpec.AshNovaKnockback, HackSpec.ComboKnockbackTime);
                DamageEnemy(ref enemy, ElementalDamage(
                    _ashNovaDamage, HackSpec.AshNovaElement, enemy.State.Visual));
            }
        }

        /// <summary>§2.3 F: a 40 point absorb for 8 s plus a 0.2 s cast i-frame.</summary>
        private void CastVoidAegis()
        {
            _charge -= HackSpec.AegisCost;
            _skillCooldowns[HackSpec.SkillAegis] = HackSpec.AegisCooldown;
            _shield = HackSpec.AegisShield;
            _shieldTime = HackSpec.AegisDuration;
            _castInvuln = HackSpec.AegisCastInvuln;
            _events |= SimEvents.WardCast;
        }

        /// <summary>The grave-pulse field lives in the sim; the view only sees the cast.</summary>
        private void UpdatePulseField(float deltaTime)
        {
            if (_pulseTime <= 0f)
            {
                return;
            }

            _pulseTime -= deltaTime;
            _pulseTick -= deltaTime;
            if (_pulseTick <= 0f)
            {
                _pulseTick += HackSpec.PulseTickInterval;
                for (int index = 0; index < _enemyCount; index += 1)
                {
                    ref Enemy enemy = ref _enemies[index];
                    if (enemy.State.Dead
                        || !IsoWithin(_pulseX, _pulseY, enemy.State.X, enemy.State.Y, HackSpec.PulseRadius))
                    {
                        continue;
                    }
                    DamageEnemy(ref enemy, ElementalDamage(
                        _pulseTickDamage, HackSpec.PulseElement, enemy.State.Visual));
                }
            }

            if (_pulseTime <= 0f)
            {
                _pulseTime = 0f;
                _pulseTick = 0f;
            }
        }

        /// <summary>§2.4: only skills roll the element cycle; the combo stays neutral.</summary>
        private static float ElementalDamage(float amount, Element skill, EnemyVisual visual)
        {
            return amount * HackSpec.Matchup(skill, HackSpec.ElementOf(visual));
        }

        /// <summary>Lowest-index living enemy inside the iso radius, or -1.</summary>
        private int NearestEnemyIndex(float x, float y, float radius)
        {
            int best = -1;
            float bestSquared = 0f;
            for (int index = 0; index < _enemyCount; index += 1)
            {
                ref Enemy enemy = ref _enemies[index];
                if (enemy.State.Dead)
                {
                    continue;
                }
                float deltaX = enemy.State.X - x;
                float deltaY = (enemy.State.Y - y) * SimConfig.IsoY;
                float squared = deltaX * deltaX + deltaY * deltaY;
                if (squared > radius * radius)
                {
                    continue;
                }
                if (best < 0 || squared < bestSquared)
                {
                    best = index;
                    bestSquared = squared;
                }
            }
            return best;
        }

        /// <summary>Push an enemy straight away from the player over <paramref name="time"/>.</summary>
        private void Knockback(ref Enemy enemy, float distance, float time)
        {
            float deltaX = enemy.State.X - _player.X;
            float deltaY = enemy.State.Y - _player.Y;
            float length = Hypot(deltaX, deltaY);
            if (length <= MoveEpsilon)
            {
                deltaX = _player.Facing;
                deltaY = 0f;
                length = 1f;
            }
            float speed = distance / time;
            enemy.KnockX = deltaX / length * speed;
            enemy.KnockY = deltaY / length * speed;
            enemy.KnockTime = time;
        }

        /// <summary>§2.5: kill XP, level-ups and the stat bump they carry.</summary>
        private void GainXp(int amount)
        {
            if (_level >= HackSpec.LevelCap)
            {
                return;
            }

            _xp += amount;
            bool levelled = false;
            while (_level < HackSpec.LevelCap)
            {
                int required = HackSpec.XpToNextLevel(_level);
                if (required <= 0 || _xp < required)
                {
                    break;
                }
                _xp -= required;
                _level += 1;
                levelled = true;
            }

            if (!levelled)
            {
                return;
            }

            if (_level >= HackSpec.LevelCap)
            {
                _xp = 0;
            }

            float previousMax = _playerMaxHealth;
            ApplyLevelStats();
            _player.Health = MathF.Min(_playerMaxHealth, _player.Health + (_playerMaxHealth - previousMax));
            _events |= SimEvents.LevelUp;
        }

        /// <summary>§3: an elite leaves an extractable corpse marker for 10 s.</summary>
        private void DropCorpse(EnemyVisual visual, float x, float y)
        {
            if (_corpseCount == _corpses.Length)
            {
                Array.Resize(ref _corpses, _corpses.Length * 2);
            }

            ref Corpse corpse = ref _corpses[_corpseCount];
            corpse.X = x;
            corpse.Y = y;
            corpse.Life = HackSpec.CorpseLifetime;
            corpse.Visual = visual;
            _corpseCount += 1;
        }

        /// <summary>
        /// §3: corpse markers age out after 10 s, and a stationary player inside 90 px
        /// of one banks 2.0 s of channel. Any hit that lands resets the channel, and a
        /// wave only yields one extraction.
        /// </summary>
        private void UpdateExtraction(float deltaTime)
        {
            for (int index = _corpseCount - 1; index >= 0; index -= 1)
            {
                _corpses[index].Life -= deltaTime;
                if (_corpses[index].Life > 0f)
                {
                    continue;
                }
                RemoveCorpseAt(index);
            }

            _extractionTarget = 0f;

            if (_extractedThisWave || _corpseCount == 0)
            {
                _extractionProgress = 0f;
                return;
            }

            int target = -1;
            float bestSquared = 0f;
            for (int index = 0; index < _corpseCount; index += 1)
            {
                float deltaX = _corpses[index].X - _player.X;
                float deltaY = (_corpses[index].Y - _player.Y) * SimConfig.IsoY;
                float squared = deltaX * deltaX + deltaY * deltaY;
                if (squared > HackSpec.ExtractionRadius * HackSpec.ExtractionRadius)
                {
                    continue;
                }
                if (target < 0 || squared < bestSquared)
                {
                    target = index;
                    bestSquared = squared;
                }
            }

            if (target < 0)
            {
                _extractionProgress = 0f;
                return;
            }

            _extractionTarget = HackSpec.ExtractionSeconds;

            if (_player.Moving || (_events & SimEvents.PlayerDamaged) != 0)
            {
                _extractionProgress = 0f;
                return;
            }

            _extractionProgress += deltaTime;
            if (_extractionProgress < HackSpec.ExtractionSeconds)
            {
                return;
            }

            CompleteExtraction(_corpses[target].Visual);
            RemoveCorpseAt(target);
            _extractionProgress = 0f;
            _extractionTarget = 0f;
            _extractedThisWave = true;
        }

        /// <summary>
        /// §3 reward branch: a visual the roster has never seen joins it and buffs this
        /// run's damage by 8%; a duplicate pays 30 relics instead.
        /// </summary>
        private void CompleteExtraction(EnemyVisual visual)
        {
            int bit = 1 << (int)visual;
            if ((_rosterMask & bit) == 0)
            {
                _rosterMask |= bit;
                _extractionBonus += HackSpec.ExtractionDamageBonus;
                ApplyLevelStats();
            }
            else
            {
                _relics += HackSpec.ExtractionDuplicateRelics;
            }
            _events |= SimEvents.ExtractionComplete;
        }

        private void RemoveCorpseAt(int index)
        {
            int tail = _corpseCount - index - 1;
            if (tail > 0)
            {
                Array.Copy(_corpses, index + 1, _corpses, index, tail);
            }
            _corpseCount -= 1;
            _corpses[_corpseCount] = default;
        }

        /// <summary>Recall takes priority when both one-shot companion commands arrive together.</summary>
        private void UpdateCompanionBehavior(in SimInput input)
        {
            if (input.CompanionRecallQueued)
            {
                _companionBehavior = CompanionBehavior.Follow;
            }
            else if (input.CompanionHoldQueued)
            {
                _companionBehavior = CompanionBehavior.Hold;
            }
        }

        /// <summary>
        /// §4: the companion trails the player by 80 px and, every 1.1 s, hits the
        /// nearest enemy inside 200 px for 60% of the player's damage. It cannot be
        /// targeted, so it has no health and never appears in the enemy contact loop.
        /// </summary>
        private void UpdateCompanion(float deltaTime)
        {
            if (_companionBehavior == CompanionBehavior.Follow)
            {
                float targetX = _player.X - HackSpec.CompanionFollowOffset * _player.Facing;
                float targetY = _player.Y;
                float deltaX = targetX - _companionX;
                float deltaY = targetY - _companionY;
                float distance = Hypot(deltaX, deltaY);
                if (distance > MoveEpsilon)
                {
                    float stepX = deltaX / distance * _playerSpeed * deltaTime;
                    float stepY = deltaY / distance * _playerSpeed * SimConfig.YMoveScale * deltaTime;
                    _companionX += MathF.Abs(stepX) >= MathF.Abs(deltaX) ? deltaX : stepX;
                    _companionY += MathF.Abs(stepY) >= MathF.Abs(deltaY) ? deltaY : stepY;
                }
            }

            _companionShow = MathF.Max(0f, _companionShow - deltaTime);
            _companionTimer = MathF.Max(0f, _companionTimer - deltaTime);
            if (_companionTimer > 0f)
            {
                if (_companionShow <= 0f)
                {
                    _companionFacing = _player.Facing;
                }
                return;
            }

            int target = NearestEnemyIndex(_companionX, _companionY, _companionAttackRange);
            if (target < 0)
            {
                _companionFacing = _player.Facing;
                return;
            }

            float targetDeltaX = _enemies[target].State.X - _companionX;
            if (MathF.Abs(targetDeltaX) > MoveEpsilon)
            {
                _companionFacing = targetDeltaX > 0f ? 1 : -1;
            }

            _companionTimer = _companionAttackInterval;
            _companionShow = HackSpec.CompanionAttackDisplay;
            DamageEnemy(ref _enemies[target], _playerDamage * _companionDamageScale);
        }

        /// <summary>0-based index into the per-phase stat vectors.</summary>
        private int BossPhaseVectorIndex()
        {
            // 0-based index into the per-phase stat vectors. _bossPhase is
            // 1-based and reads 0 before the boss spawns, so both ends clamp
            // into P1..P3. Shared by speed, reach, and damage so the three can
            // never disagree about which phase the boss is in.
            int index = _bossPhase > 0 ? _bossPhase - 1 : 0;
            return index >= HackSpec.BossSpeedMul.Length
                ? HackSpec.BossSpeedMul.Length - 1
                : index;
        }

        /// <summary>
        /// §7 (AMENDMENT #4): the stage boss steps through three phases on HP
        /// thresholds (50% / 20%) — faster, longer reach, harder contact, and
        /// the monarch calls in three escorts on the way through.
        /// </summary>
        private void UpdateBossPhase()
        {
            int boss = -1;
            for (int index = 0; index < _enemyCount; index += 1)
            {
                if (_enemies[index].State.IsBoss && !_enemies[index].State.Dead)
                {
                    boss = index;
                    break;
                }
            }

            if (boss < 0)
            {
                _bossHp = 0f;
                _bossMaxHp = 0f;
                _bossPhase = 0;
                return;
            }

            _bossHp = _enemies[boss].State.Health;
            _bossMaxHp = _enemies[boss].State.MaxHealth;

            // S8-a: three phases on HP thresholds (70% / 40%). Latched per
            // boundary so a boss healed above a threshold cannot re-fire the
            // transition. BossPhase2 is still the only phase event on the
            // frozen SimEvents surface, so it fires on EVERY boundary — the
            // View reads _bossPhase for WHICH phase, the event only says
            // "a transition happened" (adding an event = snapshot contract
            // change, deliberately deferred).
            float fraction = _bossMaxHp > 0f ? _bossHp / _bossMaxHp : 1f;
            int phaseIndex = HackSpec.BossPhaseIndexFor(fraction);

            if (phaseIndex >= 1 && !_bossPhase2Done)
            {
                _bossPhase2Done = true;
                _events |= SimEvents.BossPhase2;
                if (_enemies[boss].State.Visual == EnemyVisual.BossMonarch)
                {
                    // The escorts join the live spawn queue as ordinary enemies.
                    _pendingSpawns += HackSpec.MonarchPhase2Escorts;
                }
            }
            if (phaseIndex >= 2 && !_bossPhase3Done)
            {
                _bossPhase3Done = true;
                _events |= SimEvents.BossPhase2;
            }

            // Snapshot phase stays 1-based (1/2/3) — the View's existing
            // "phase 2" checks keep meaning what they meant.
            _bossPhase = phaseIndex + 1;
        }

        private void UpdateSkills(float deltaTime)
        {
            if (_dungeon)
            {
                for (int index = 0; index < _skillCooldowns.Length; index += 1)
                {
                    _skillCooldowns[index] = MathF.Max(0f, _skillCooldowns[index] - deltaTime);
                }
                _dashCooldown = MathF.Max(0f, _dashCooldown - deltaTime);
                _castInvuln = MathF.Max(0f, _castInvuln - deltaTime);
                if (_shieldTime > 0f)
                {
                    _shieldTime = MathF.Max(0f, _shieldTime - deltaTime);
                    if (_shieldTime == 0f)
                    {
                        _shield = 0f;
                    }
                }
                UpdatePulseField(deltaTime);
            }

            _novaCooldown = MathF.Max(0f, _novaCooldown - deltaTime);
            _wardCooldown = MathF.Max(0f, _wardCooldown - deltaTime);
            _novaFlash = MathF.Max(0f, _novaFlash - deltaTime);
            _player.WardTime = MathF.Max(0f, _player.WardTime - deltaTime);
            _charge = MathF.Min(SimConfig.LanternMax, _charge + _lanternRegen * deltaTime);
        }

        // --- Player ----------------------------------------------------------

        private void UpdatePlayer(float deltaTime, in SimInput input)
        {
            _player.AttackCooldown = MathF.Max(0f, _player.AttackCooldown - deltaTime);
            _player.DamageCooldown = MathF.Max(0f, _player.DamageCooldown - deltaTime);

            // §2.2: the dash owns the whole step — no steering, no swing, no contact.
            if (_dashTime > 0f)
            {
                UpdateDash(deltaTime);
                return;
            }

            float movementX = input.MoveX;
            float movementY = input.MoveY;
            float movementLength = Hypot(movementX, movementY);

            if (movementLength > 0f)
            {
                movementX /= movementLength;
                movementY /= movementLength;
                float attackScale = _player.Action == ActorAction.Attack ? SimConfig.AttackMoveScale : 1f;
                _player.X += movementX * _playerSpeed * attackScale * deltaTime;
                _player.Y += movementY * _playerSpeed * SimConfig.YMoveScale * attackScale * deltaTime;
                _player.Moving = true;
                if (movementX != 0f)
                {
                    _player.Facing = movementX > 0f ? 1 : -1;
                }
                ClampToArena(ref _player.X, ref _player.Y, SimConfig.PlayerMarginClamp);
            }
            else
            {
                _player.Moving = false;
            }

            ApplyCurrents(ref _player.X, ref _player.Y, SimConfig.PlayerMarginClamp, deltaTime);
            ApplyPillars(ref _player.X, ref _player.Y, CampaignSpec.PlayerPushRadius);

            if (_dungeon)
            {
                UpdateCombo(deltaTime, in input);
                return;
            }

            if (input.AttackQueued && _player.AttackCooldown <= 0f && _player.Action != ActorAction.Attack)
            {
                _player.AttackId += 1;
                _player.AttackCooldown = SimConfig.PlayerAttackCooldown;
                SetPlayerAction(ActorAction.Attack, true);
                _events |= SimEvents.PlayerStruck;
            }

            if (_player.Action != ActorAction.Attack)
            {
                SetPlayerAction(_player.Moving ? ActorAction.Move : ActorAction.Idle, false);
            }

            _player.ActionTime += deltaTime;

            int frame = -1;
            if (_player.Action == ActorAction.Attack)
            {
                frame = (int)MathF.Floor(_player.ActionTime * AttackClipFps);
                if (frame >= AttackClipFrames)
                {
                    SetPlayerAction(ActorAction.Idle, true);
                    frame = -1;
                }
            }

            if (frame < AttackActiveFirstFrame || frame > AttackActiveLastFrame)
            {
                return;
            }

            for (int index = 0; index < _enemyCount; index += 1)
            {
                ref Enemy enemy = ref _enemies[index];
                if (enemy.State.Dead || enemy.LastHitAttack == _player.AttackId)
                {
                    continue;
                }
                float deltaX = enemy.State.X - _player.X;
                float deltaY = (enemy.State.Y - _player.Y) * SimConfig.IsoY;
                bool inFacingArc = deltaX * _player.Facing >= SimConfig.FacingArcTolerance;
                if (inFacingArc && deltaX * deltaX + deltaY * deltaY <= SimConfig.PlayerAttackRange * SimConfig.PlayerAttackRange)
                {
                    enemy.LastHitAttack = _player.AttackId;
                    DamageEnemy(ref enemy, _playerDamage);
                }
            }

            if (_campaign)
            {
                StrikePylons(_playerDamage); // amendment #5: same swing, same attackId guard
            }
        }

        /// <summary>
        /// §2.2: 190 px over 0.22 s. The final step is clipped to the remaining dash
        /// time so the travelled distance is exactly 190 px, not a fixed-step multiple.
        /// </summary>
        private void UpdateDash(float deltaTime)
        {
            float step = MathF.Min(deltaTime, _dashTime);
            float speed = HackSpec.DashDistance / HackSpec.DashTime;
            _player.X += _dashDirX * speed * step;
            _player.Y += _dashDirY * speed * SimConfig.YMoveScale * step;
            ClampToArena(ref _player.X, ref _player.Y, SimConfig.PlayerMarginClamp);
            ApplyCurrents(ref _player.X, ref _player.Y, SimConfig.PlayerMarginClamp, step);
            ApplyPillars(ref _player.X, ref _player.Y, CampaignSpec.PlayerPushRadius);
            _player.Moving = true;
            _player.ActionTime += deltaTime;

            _dashTime -= deltaTime;
            if (_dashTime <= 0f)
            {
                _dashTime = 0f;
                SetPlayerAction(ActorAction.Idle, true);
            }
        }

        /// <summary>
        /// §2.1: a three-hit chain. Each hit owns a swing length and an active window;
        /// re-pressing inside the 0.9 s link window advances the chain, otherwise the
        /// next press restarts at hit 1.
        /// </summary>
        private void UpdateCombo(float deltaTime, in SimInput input)
        {
            if (input.AttackQueued && _comboSwing < 0)
            {
                int hit = _comboLink > 0f ? _comboIndex : 0;
                if (hit < 0 || hit >= HackSpec.ComboLength)
                {
                    hit = 0;
                }
                _comboSwing = hit;
                _comboLanded = false;
                _comboLink = 0f;
                _player.AttackId += 1;
                _player.AttackCooldown = HackSpec.ComboSwing[hit];
                SetPlayerAction(ActorAction.Attack, true);
                _events |= SimEvents.PlayerStruck;
            }

            if (_comboSwing < 0)
            {
                if (_comboLink > 0f)
                {
                    _comboLink = MathF.Max(0f, _comboLink - deltaTime);
                    if (_comboLink == 0f)
                    {
                        _comboIndex = 0;
                    }
                }
                SetPlayerAction(_player.Moving ? ActorAction.Move : ActorAction.Idle, false);
                _player.ActionTime += deltaTime;
                return;
            }

            _player.ActionTime += deltaTime;

            int index = _comboSwing;
            float elapsed = _player.ActionTime;
            if (elapsed >= HackSpec.ComboActiveFrom[index] && elapsed < HackSpec.ComboActiveTo[index])
            {
                SwingCombo(index);
            }

            if (elapsed < HackSpec.ComboSwing[index])
            {
                return;
            }

            _comboSwing = -1;
            _comboIndex = (index + 1) % HackSpec.ComboLength;
            _comboLink = HackSpec.ComboLinkWindow;
            SetPlayerAction(ActorAction.Idle, true);
        }

        /// <summary>
        /// One combo hit: arena range/arc/one-hit-per-attackId rules, hit damage scaled
        /// off the 58 base, and the finisher's 120 px knockback.
        /// </summary>
        private void SwingCombo(int index)
        {
            bool finisher = index == HackSpec.ComboLength - 1;
            float damage = _playerDamage * HackSpec.ComboDamageScale[index];
            bool landed = false;

            for (int enemyIndex = 0; enemyIndex < _enemyCount; enemyIndex += 1)
            {
                ref Enemy enemy = ref _enemies[enemyIndex];
                if (enemy.State.Dead || enemy.LastHitAttack == _player.AttackId)
                {
                    continue;
                }
                float deltaX = enemy.State.X - _player.X;
                float deltaY = (enemy.State.Y - _player.Y) * SimConfig.IsoY;
                bool inFacingArc = deltaX * _player.Facing >= SimConfig.FacingArcTolerance;
                if (!inFacingArc
                    || deltaX * deltaX + deltaY * deltaY > SimConfig.PlayerAttackRange * SimConfig.PlayerAttackRange)
                {
                    continue;
                }

                enemy.LastHitAttack = _player.AttackId;
                landed = true;
                if (finisher)
                {
                    Knockback(ref enemy, HackSpec.ComboKnockbackDistance, HackSpec.ComboKnockbackTime);
                }
                DamageEnemy(ref enemy, damage);
            }

            if (_campaign)
            {
                landed |= StrikePylons(damage); // amendment #5: pylon-only hits still land
            }

            if (landed && finisher && !_comboLanded)
            {
                _events |= SimEvents.ComboFinisher;
            }
            if (landed)
            {
                _comboLanded = true;
            }
        }

        private void SetPlayerAction(ActorAction action, bool force)
        {
            if (!force && _player.Action == action)
            {
                return;
            }
            _player.Action = action;
            _player.ActionTime = 0f;
        }

        private void DamagePlayer(float amount) => DamagePlayer(amount, false);

        /// <summary>
        /// <paramref name="bypassWard"/> skips Ward but still obeys the 0.38 s grace window.
        /// </summary>
        private void DamagePlayer(float amount, bool bypassWard)
        {
            if (_mode == SimMode.GameOver || _player.DamageCooldown > 0f)
            {
                return;
            }

            // §2.2/§2.3: dash i-frames and the void-aegis cast window refuse everything,
            // hazards included, and do not even burn the contact grace.
            if (_dashTime > 0f || _castInvuln > 0f)
            {
                return;
            }

            // Ward refuses the damage outright but still burns the contact grace so a
            // warded player is not chain-hit by the same swing.
            if (!bypassWard && _player.WardTime > 0f)
            {
                _player.DamageCooldown = SimConfig.PlayerHitGrace;
                return;
            }

            // §2.3 F: the shield eats damage first; a fully absorbed hit is not a hit.
            if (_shield > 0f)
            {
                float absorbed = MathF.Min(_shield, amount);
                _shield -= absorbed;
                amount -= absorbed;
                if (_shield <= 0f)
                {
                    _shield = 0f;
                    _shieldTime = 0f;
                }
                if (amount <= 0f)
                {
                    _player.DamageCooldown = SimConfig.PlayerHitGrace;
                    return;
                }
            }

            _player.DamageCooldown = SimConfig.PlayerHitGrace;
            _player.Health = MathF.Max(0f, _player.Health - amount);
            _events |= SimEvents.PlayerDamaged;

            if (_player.Health == 0f)
            {
                _mode = SimMode.GameOver;
                _reason = OverrunReason;
                SetPlayerAction(ActorAction.Die, true);
                _events |= SimEvents.GameOver;
            }
        }

        // --- Enemies ---------------------------------------------------------

        private void UpdateEnemies(float deltaTime)
        {
            for (int index = 0; index < _enemyCount; index += 1)
            {
                if (_enemies[index].State.Dead)
                {
                    _enemies[index].State.FadeTime -= deltaTime;
                    continue;
                }
                UpdateEnemy(index, deltaTime);
                if (_mode == SimMode.GameOver)
                {
                    break;
                }
            }

            for (int index = _enemyCount - 1; index >= 0; index -= 1)
            {
                if (_enemies[index].State.Dead && _enemies[index].State.FadeTime <= 0f)
                {
                    RemoveEnemyAt(index);
                }
            }
        }

        private void UpdateEnemy(int index, float deltaTime)
        {
            ref Enemy enemy = ref _enemies[index];
            enemy.AttackCooldown = MathF.Max(0f, enemy.AttackCooldown - deltaTime);

            // §2.1/§2.3: knockback rides on top of the chase — the enemy still acts.
            if (enemy.KnockTime > 0f)
            {
                float step = MathF.Min(deltaTime, enemy.KnockTime);
                enemy.State.X += enemy.KnockX * step;
                enemy.State.Y += enemy.KnockY * SimConfig.YMoveScale * step;
                ClampToArena(ref enemy.State.X, ref enemy.State.Y, SimConfig.EnemyMarginClamp);
                enemy.KnockTime -= deltaTime;
                if (enemy.KnockTime <= 0f)
                {
                    enemy.KnockTime = 0f;
                    enemy.KnockX = 0f;
                    enemy.KnockY = 0f;
                }
            }

            float deltaX = _player.X - enemy.State.X;
            float deltaY = _player.Y - enemy.State.Y;
            float combatY = deltaY * SimConfig.IsoY;
            float distance = Hypot(deltaX, combatY);

            if (enemy.State.Action != ActorAction.Attack)
            {
                if (distance <= SimConfig.EnemyAttackRange && enemy.AttackCooldown <= 0f)
                {
                    enemy.DidDamage = false;
                    enemy.AttackCooldown = SimConfig.EnemyAttackCooldown
                        + MathF.Min(EnemyCooldownWaveCap, _wave * EnemyCooldownPerWave);
                    SetEnemyAction(ref enemy, ActorAction.Attack, true);
                }
                else
                {
                    float moveX = deltaX;
                    float moveY = deltaY;
                    float rawDistance = Hypot(moveX, moveY);
                    if (rawDistance > MoveEpsilon)
                    {
                        moveX /= rawDistance;
                        moveY /= rawDistance;
                    }

                    for (int otherIndex = 0; otherIndex < _enemyCount; otherIndex += 1)
                    {
                        if (otherIndex == index)
                        {
                            continue;
                        }
                        ref Enemy other = ref _enemies[otherIndex];
                        if (other.State.Dead)
                        {
                            continue;
                        }
                        float separationX = enemy.State.X - other.State.X;
                        float separationY = enemy.State.Y - other.State.Y;
                        float separationSquared = separationX * separationX + separationY * separationY;
                        if (separationSquared > SeparationMinDistanceSq
                            && separationSquared < SimConfig.SeparationRadius * SimConfig.SeparationRadius)
                        {
                            float separationDistance = MathF.Sqrt(separationSquared);
                            float separationWeight = (SimConfig.SeparationRadius - separationDistance) / SimConfig.SeparationRadius;
                            moveX += separationX / separationDistance * separationWeight * SimConfig.SeparationWeight;
                            moveY += separationY / separationDistance * separationWeight * SimConfig.SeparationWeight;
                        }
                    }

                    float adjustedLength = Hypot(moveX, moveY);
                    if (adjustedLength > MoveEpsilon)
                    {
                        moveX /= adjustedLength;
                        moveY /= adjustedLength;
                    }

                    float speed = SpeedFor(enemy.State.Id, enemy.State.IsBoss);
                    if (distance > SimConfig.EnemyAttackRange - EnemyChaseSlack)
                    {
                        enemy.State.X += moveX * speed * deltaTime;
                        enemy.State.Y += moveY * speed * SimConfig.YMoveScale * deltaTime;
                        ClampToArena(ref enemy.State.X, ref enemy.State.Y, SimConfig.EnemyMarginClamp);
                        // Run is reserved for bosses (SIM_SPEC animation action set).
                        SetEnemyAction(ref enemy, enemy.State.IsBoss ? ActorAction.Run : ActorAction.Move, false);
                    }
                    else
                    {
                        SetEnemyAction(ref enemy, ActorAction.Idle, false);
                    }
                }
            }

            ApplyCurrents(ref enemy.State.X, ref enemy.State.Y, SimConfig.EnemyMarginClamp, deltaTime);
            ApplyPillars(ref enemy.State.X, ref enemy.State.Y, CampaignSpec.EnemyPushRadius);

            if (MathF.Abs(deltaX) > EnemyFacingDeadzone)
            {
                enemy.State.Facing = deltaX > 0f ? 1 : -1;
            }

            enemy.State.ActionTime += deltaTime;

            int frame = -1;
            if (enemy.State.Action == ActorAction.Attack)
            {
                frame = (int)MathF.Floor(enemy.State.ActionTime * AttackClipFps);
                if (frame >= AttackClipFrames)
                {
                    SetEnemyAction(ref enemy, ActorAction.Idle, true);
                    frame = -1;
                }
            }

            if (frame < EnemyContactFrame || enemy.DidDamage)
            {
                return;
            }

            float contactX = _player.X - enemy.State.X;
            float contactY = (_player.Y - enemy.State.Y) * SimConfig.IsoY;
            float contactRange = SimConfig.EnemyAttackRange + SimConfig.EnemyContactBonus;
            if (enemy.State.IsBoss)
            {
                // S8-a: reach grows with the phase vector (1.00/1.10/1.20).
                contactRange *= HackSpec.BossRangeMul[BossPhaseVectorIndex()];
            }
            if (contactX * contactX + contactY * contactY <= contactRange * contactRange)
            {
                enemy.DidDamage = true;
                float damage = MathF.Min(ContactDamageCap, ContactDamageBase + MathF.Floor(_wave * ContactDamagePerWave));
                if (enemy.State.IsBoss)
                {
                    damage *= SimConfig.BossDamageMul;
                }
                if (enemy.IsElite)
                {
                    // §3: elites hit 1.5x harder than the wave baseline.
                    damage *= HackSpec.EliteDamageMul;
                }
                if (enemy.State.IsBoss && _bossPhase >= 2)
                {
                    // S8-a: P2 x1.25, P3 x1.45 — the amended curve, not a
                    // single phase-2 step (a P3 boss hitting at the P2 number
                    // would contradict the constants this amendment ships).
                    damage *= _bossPhase >= 3
                        ? HackSpec.BossPhase3DamageMul
                        : HackSpec.BossPhase2DamageMul;
                }
                DamagePlayer(damage);
            }
        }

        private static void SetEnemyAction(ref Enemy enemy, ActorAction action, bool force)
        {
            if (!force && enemy.State.Action == action)
            {
                return;
            }
            enemy.State.Action = action;
            enemy.State.ActionTime = 0f;
        }

        private float SpeedFor(int enemyId, bool isBoss)
        {
            float speed = MathF.Min(
                EnemySpeedCap,
                EnemySpeedBase + _wave * EnemySpeedPerWave + enemyId % 3 * EnemySpeedIdStep);
            if (isBoss)
            {
                // S8-a: speed reads the per-phase vector (1.00/1.25/1.45) on
                // top of the frozen boss modifier. _bossPhase is 1-based, so
                // clamp into the 0-based vector; a boss that has not spawned
                // yet reports phase 0 and must resolve to P1.
                return speed * SimConfig.BossSpeedMul
                    * HackSpec.BossSpeedMul[BossPhaseVectorIndex()];
            }
            return speed;
        }

        private void DamageEnemy(ref Enemy enemy, float amount)
        {
            if (enemy.State.Dead)
            {
                return;
            }

            // Amendment #5: live pylon aura shields enemies inside it (×0.60, non-stacking).
            amount *= PylonAuraMultiplier(enemy.State.X, enemy.State.Y);
            enemy.State.Health = MathF.Max(0f, enemy.State.Health - amount);
            _events |= SimEvents.EnemyHit;

            if (enemy.State.Health != 0f)
            {
                return;
            }

            enemy.State.Dead = true;
            enemy.State.FadeTime = SimConfig.EnemyFade;
            SetEnemyAction(ref enemy, ActorAction.Die, true);
            _livingEnemies -= 1;
            bool boss = enemy.State.IsBoss;
            if (boss)
            {
                _livingBosses -= 1;
            }
            if (enemy.IsElite)
            {
                _elitesAlive -= 1;
                DropCorpse(enemy.State.Visual, enemy.State.X, enemy.State.Y);
                _events |= SimEvents.EliteDown;
            }
            if (_dungeon)
            {
                // §2.5: 10 per kill, 25 per elite, 150 per boss.
                GainXp(boss
                    ? HackSpec.XpPerBoss
                    : (enemy.IsElite ? HackSpec.XpPerElite : HackSpec.XpPerKill));
            }
            _score += (boss ? BossKillScorePerWave : KillScorePerWave) * _wave;
            _kills += 1;
            _charge = MathF.Min(SimConfig.LanternMax, _charge + SimConfig.LanternChargePerKill);
            SpawnPickup(enemy.State.Id, boss, enemy.State.X, enemy.State.Y);
            _events |= SimEvents.EnemyKilled;

            if (_campaign && boss)
            {
                // Guaranteed stage reward, then the stage ends: remaining mobs fade.
                RaiseRank(_config.StageIndex % CampaignSpec.EquipSlotCount);
                ClearStage();
            }
        }

        private void RemoveEnemyAt(int index)
        {
            int tail = _enemyCount - index - 1;
            if (tail > 0)
            {
                Array.Copy(_enemies, index + 1, _enemies, index, tail);
            }
            _enemyCount -= 1;
            _enemies[_enemyCount] = default;
        }

        // --- Pickups ---------------------------------------------------------

        private void SpawnPickup(int enemyId, bool isBoss, float x, float y)
        {
            if (_pickupCount == _pickups.Length)
            {
                Array.Resize(ref _pickups, _pickups.Length * 2);
            }

            ref PickupState pickup = ref _pickups[_pickupCount];
            pickup.Id = _nextPickupId;
            // Bosses always drop the relic mote; ordinary drops rotate on enemy id.
            // Campaign only: an id that hits the shard modulus drops equipment instead.
            pickup.Kind = isBoss
                ? PickupKind.RelicMote
                : (_campaign && enemyId % CampaignSpec.ShardDropModulus == CampaignSpec.ShardDropRemainder
                    ? PickupKind.EquipShard
                    : (PickupKind)(enemyId % 3));
            pickup.X = x;
            pickup.Y = y;
            pickup.Life = SimConfig.PickupLifetime;
            pickup.Bob = 0f;

            _pickupCount += 1;
            _nextPickupId += 1;
        }

        private void UpdatePickups(float deltaTime)
        {
            for (int index = _pickupCount - 1; index >= 0; index -= 1)
            {
                ref PickupState pickup = ref _pickups[index];
                pickup.Life -= deltaTime;
                pickup.Bob += deltaTime;

                float deltaX = _player.X - pickup.X;
                float deltaY = (_player.Y - pickup.Y) * SimConfig.IsoY;
                if (deltaX * deltaX + deltaY * deltaY <= SimConfig.PickupMagnetRadius * SimConfig.PickupMagnetRadius)
                {
                    CollectPickup(pickup.Kind);
                    RemovePickupAt(index);
                    continue;
                }

                if (pickup.Life <= 0f)
                {
                    RemovePickupAt(index);
                }
            }
        }

        private void CollectPickup(PickupKind kind)
        {
            if (kind == PickupKind.EmberShard)
            {
                _player.Health = MathF.Min(_playerMaxHealth, _player.Health + SimConfig.EmberShardHeal);
            }
            else if (kind == PickupKind.OilFlask)
            {
                _charge = MathF.Min(SimConfig.LanternMax, _charge + SimConfig.OilFlaskCharge);
            }
            else if (kind == PickupKind.EquipShard)
            {
                // Rank lands on the kill-count slot; it applies to the *next* run start.
                RaiseRank(_kills % CampaignSpec.EquipSlotCount);
            }
            else
            {
                _relics += 1;
                _score += SimConfig.RelicScore;
            }
            _events |= SimEvents.PickupCollected;
        }

        private void RemovePickupAt(int index)
        {
            int tail = _pickupCount - index - 1;
            if (tail > 0)
            {
                Array.Copy(_pickups, index + 1, _pickups, index, tail);
            }
            _pickupCount -= 1;
            _pickups[_pickupCount] = default;
        }

        // --- Wave ------------------------------------------------------------

        private void StartWave(int waveNumber)
        {
            _wave = waveNumber;
            _waveSeed = waveNumber * 3 % SimConfig.SpawnPoints.Length;
            if (_prologue)
            {
                _pendingSpawns = HackSpec.PrologueSpawnCount(waveNumber);
                _pendingBoss = false;
            }
            else
            {
                _pendingSpawns = _campaign
                    ? SpawnCountForStageWave(in _config, waveNumber)
                    : SpawnCountForWave(waveNumber);
                _pendingBoss = _campaign ? waveNumber > _config.Waves : IsBossWave(waveNumber);
            }
            _eliteThisWave = false;
            _extractedThisWave = false;
            _spawnIndexInWave = 0;
            _spawnTimer = SimConfig.FirstSpawnDelay;
            _intermission = 0f;
            _mode = SimMode.Running;

            // The original only plays the wave cue from wave 2 on.
            if (waveNumber > 1)
            {
                _events |= SimEvents.WaveStarted;
            }
        }

        private void UpdateWave(float deltaTime)
        {
            if (_mode == SimMode.WaveClear)
            {
                _intermission -= deltaTime;
                if (_intermission <= 0f)
                {
                    StartWave(_wave + 1);
                }
                return;
            }

            if (_pendingSpawns > 0 && _enemyCount < SimConfig.EnemyCap)
            {
                _spawnTimer -= deltaTime;
                if (_spawnTimer <= 0f)
                {
                    bool boss = _pendingBoss;
                    _pendingBoss = false;
                    SpawnEnemy(boss);
                    _pendingSpawns -= 1;
                    _spawnTimer = MathF.Max(SpawnIntervalMin, SpawnIntervalBase - _wave * SpawnIntervalPerWave);
                }
            }

            if (_pendingSpawns == 0 && _livingEnemies == 0)
            {
                // §1: the prologue is three waves long and then it is over.
                if (_prologue && _wave >= HackSpec.PrologueWaves)
                {
                    ClearRun(HackSpec.PrologueClearReason);
                    return;
                }
                _intermission = SimConfig.WaveIntermission;
                _mode = SimMode.WaveClear;
            }
        }

        private void SpawnEnemy(bool boss)
        {
            if (_enemyCount == _enemies.Length)
            {
                Array.Resize(ref _enemies, _enemies.Length * 2);
            }

            int id = _nextEnemyId;
            float[] spawnPoint = SimConfig.SpawnPoints[SpawnPointIndexFor(_wave, id)];
            // §2.1: dungeon mobs carry the combo-DPS health curve; arena/prologue keep
            // the frozen SIM_SPEC curve.
            float health = _dungeon
                ? HackSpec.DungeonEnemyBaseHealth
                    + MathF.Min(HackSpec.DungeonEnemyHealthCap, (_wave - 1) * HackSpec.DungeonEnemyHealthPerWave)
                : SimConfig.EnemyBaseHealth
                    + MathF.Min(EnemyHealthWaveCap, (_wave - 1) * EnemyHealthPerWave);
            // §3: every seventh dungeon spawn is an elite, at most one per wave.
            bool elite = false;
            if (_dungeon && !boss)
            {
                _spawnOrdinal += 1;
                elite = !_eliteThisWave && _spawnOrdinal % HackSpec.EliteSpawnModulus == 0;
            }

            if (boss)
            {
                health *= SimConfig.BossHealthMul;
                if (_dungeon)
                {
                    // B-1 (AMENDMENT #4): the dungeon boss died in 1.3-2.8 s,
                    // far too short for three phases to read. Gated on
                    // _dungeon — the SAME gate as UpdateBossPhase (L653) — so
                    // HP and phases can never apply to different runs. Arena
                    // and plain-campaign bosses are untouched: neither has
                    // phases, so neither needs the extra length.
                    health *= HackSpec.DungeonBossHealthMul;
                }
            }
            else if (elite)
            {
                health *= HackSpec.EliteHealthMul;
            }

            ref Enemy enemy = ref _enemies[_enemyCount];
            enemy.State.Id = id;
            enemy.State.Visual = boss
                ? (_campaign
                    ? _config.BossVisual
                    : (_wave % BossVisualPeriod == 0 ? EnemyVisual.BossMonarch : EnemyVisual.BossCommander))
                : (EnemyVisual)((_wave + _spawnIndexInWave) % VisualRotation);
            enemy.State.X = spawnPoint[0];
            enemy.State.Y = spawnPoint[1];
            enemy.State.Facing = spawnPoint[0] < SimConfig.ArenaX ? 1 : -1;
            enemy.State.Health = health;
            enemy.State.MaxHealth = health;
            enemy.State.Dead = false;
            enemy.State.FadeTime = 0f;
            enemy.State.Action = ActorAction.Idle;
            enemy.State.ActionTime = 0f;
            enemy.State.IsBoss = boss;
            enemy.State.Scale = boss ? SimConfig.BossScale : (elite ? HackSpec.EliteScale : 1f);
            enemy.AttackCooldown = id % 3 * FirstAttackDelayStep;
            enemy.DidDamage = false;
            enemy.LastHitAttack = -1;
            enemy.IsElite = elite;
            enemy.KnockX = 0f;
            enemy.KnockY = 0f;
            enemy.KnockTime = 0f;

            _enemyCount += 1;
            _nextEnemyId += 1;
            _spawnIndexInWave += 1;
            _livingEnemies += 1;

            if (boss)
            {
                _livingBosses += 1;
                _events |= SimEvents.BossSpawned;
            }
            else if (elite)
            {
                _elitesAlive += 1;
                _eliteThisWave = true;
            }
        }

        // --- Campaign amendment (docs/SIM_SPEC_CAMPAIGN.md) -------------------

        /// <summary>
        /// Run-start reset of every campaign-only field. On the arena path this only
        /// restates the frozen SIM_SPEC constants, so behaviour is unchanged.
        /// </summary>
        private void ResetCampaignRun()
        {
            _stageTime = 0f;
            _stageCleared = false;
            _livingBosses = 0;

            for (int index = 0; index < _hazardRuntime.Length; index += 1)
            {
                _hazardRuntime[index] = default;
                // Amendment #5: pylons start alive; one-hit guard starts unclaimed.
                if (_hazards[index].Kind == HazardKind.EmberPylon)
                {
                    _hazardRuntime[index].Hp = _hazards[index].Hp;
                }
                _hazardRuntime[index].LastHitAttack = -1;
                _hazardRuntime[index].Tick = -1;
            }

            if (_hack)
            {
                // §5/§6: meta stats and equipment tiers apply to dungeon runs only —
                // the prologue and the arena keep the frozen SIM_SPEC numbers.
                if (_dungeon)
                {
                    _weaponRank = CampaignSpec.ClampRank(_hackConfig.EquipTiers.Weapon);
                    _lanternRank = CampaignSpec.ClampRank(_hackConfig.EquipTiers.Lantern);
                    _cloakRank = CampaignSpec.ClampRank(_hackConfig.EquipTiers.Cloak);
                    _baseDamage = _hackConfig.PlayerDamage;
                    _baseMaxHealth = _hackConfig.PlayerMaxHealth;
                    _baseRegen = _hackConfig.LanternRegenPerSecond;
                    _baseSpeed = _hackConfig.PlayerSpeed;
                }
                else
                {
                    _weaponRank = 0;
                    _lanternRank = 0;
                    _cloakRank = 0;
                    _baseDamage = SimConfig.PlayerDamage;
                    _baseMaxHealth = SimConfig.PlayerMaxHealth;
                    _baseRegen = SimConfig.LanternRegenPerSecond;
                    _baseSpeed = SimConfig.PlayerSpeed;
                }
                ApplyLevelStats();
                return;
            }

            if (!_campaign)
            {
                _weaponRank = 0;
                _lanternRank = 0;
                _cloakRank = 0;
                _baseDamage = SimConfig.PlayerDamage;
                _baseMaxHealth = SimConfig.PlayerMaxHealth;
                _baseRegen = SimConfig.LanternRegenPerSecond;
                _baseSpeed = SimConfig.PlayerSpeed;
                ApplyLevelStats();
                return;
            }

            // Equipment is applied once, at run start, from the carried config ranks.
            // Ranks earned during the run land in the snapshot for persistence and
            // take effect on the next run.
            _weaponRank = CampaignSpec.ClampRank(_config.WeaponRank);
            _lanternRank = CampaignSpec.ClampRank(_config.LanternRank);
            _cloakRank = CampaignSpec.ClampRank(_config.CloakRank);
            _baseDamage = _config.PlayerDamage;
            _baseMaxHealth = _config.PlayerMaxHealth;
            _baseRegen = _config.LanternRegenPerSecond;
            _baseSpeed = SimConfig.PlayerSpeed;
            ApplyLevelStats();
        }

        /// <summary>
        /// Fold the in-run level (§2.5) and the extraction buff (§3) into the effective
        /// stats. At level 1 with no buff every multiplier is exactly 1, so the arena
        /// and campaign paths keep their frozen values bit for bit.
        /// </summary>
        private void ApplyLevelStats()
        {
            int levels = _level - 1;
            _playerDamage = _baseDamage
                * (1f + HackSpec.LevelDamageBonus * levels)
                * (1f + _extractionBonus);
            _playerMaxHealth = _baseMaxHealth + HackSpec.LevelHealthBonus * levels;
            _lanternRegen = _baseRegen + HackSpec.LevelRegenBonus * levels;
            _playerSpeed = _baseSpeed;
        }

        /// <summary>Run-start reset of every hack &amp; slash field (§2-§7).</summary>
        private void ResetHackRun()
        {
            _level = 1;
            _xp = 0;
            _comboIndex = 0;
            _comboSwing = -1;
            _comboLink = 0f;
            _comboLanded = false;
            _dashCooldown = 0f;
            _dashTime = 0f;
            _dashDirX = 0f;
            _dashDirY = 0f;
            _castInvuln = 0f;
            _shield = 0f;
            _shieldTime = 0f;
            _pulseTime = 0f;
            _pulseTick = 0f;
            _pulseX = 0f;
            _pulseY = 0f;
            _elitesAlive = 0;
            _spawnOrdinal = 0;
            _eliteThisWave = false;
            _extractedThisWave = false;
            _extractionProgress = 0f;
            _extractionTarget = 0f;
            _extractionBonus = 0f;
            _rosterMask = _hack ? _hackConfig.RosterMask : 0;
            _corpseCount = 0;
            _companionTimer = _companionAttackInterval;
            _companionShow = 0f;
            _companionBehavior = CompanionBehavior.Follow;
            _emberRestOpen = false;
            _emberRestRoomIndex = 0;
            _emberRestSeed = 0;
            _emberRestOffer0 = default;
            _emberRestOffer1 = default;
            _emberRestOffer2 = default;
            _selectedPreparation = default;
            _bossHp = 0f;
            _bossMaxHp = 0f;
            _bossPhase = 0;
            _bossPhase2Done = false;
            _bossPhase3Done = false;
            for (int index = 0; index < _skillCooldowns.Length; index += 1)
            {
                _skillCooldowns[index] = 0f;
            }
        }

        /// <summary>Park the companion at its follow offset (§4). No-op when disabled.</summary>
        private void ResetCompanion()
        {
            _companionX = _player.X - HackSpec.CompanionFollowOffset * _player.Facing;
            _companionY = _player.Y;
            _companionFacing = _player.Facing;
        }

        private void RaiseRank(int slot)
        {
            if (slot == (int)EquipSlot.Weapon)
            {
                _weaponRank = Math.Min(CampaignSpec.MaxEquipRank, _weaponRank + 1);
            }
            else if (slot == (int)EquipSlot.Lantern)
            {
                _lanternRank = Math.Min(CampaignSpec.MaxEquipRank, _lanternRank + 1);
            }
            else
            {
                _cloakRank = Math.Min(CampaignSpec.MaxEquipRank, _cloakRank + 1);
            }
            // One flag for "an equipment rank was granted this tick" (boss drop or shard).
            _events |= SimEvents.EquipDropped;
        }

        /// <summary>Stage boss down: the run ends as a clear, not as an overrun.</summary>
        private void ClearStage() => ClearRun(CampaignSpec.StageClearReason);

        /// <summary>The objective is met: the run ends as a clear, not as an overrun.</summary>
        private void ClearRun(string reason)
        {
            _stageCleared = true;
            _reason = reason;
            _mode = SimMode.GameOver;
            _events |= SimEvents.StageCleared;
            _pendingSpawns = 0;
            _pendingBoss = false;
            FadeRemainingEnemies();
        }

        /// <summary>Combat is over: survivors fade without scoring or dropping.</summary>
        private void FadeRemainingEnemies()
        {
            for (int index = 0; index < _enemyCount; index += 1)
            {
                ref Enemy enemy = ref _enemies[index];
                if (enemy.State.Dead)
                {
                    continue;
                }
                enemy.State.Dead = true;
                enemy.State.Health = 0f;
                enemy.State.FadeTime = SimConfig.EnemyFade;
                SetEnemyAction(ref enemy, ActorAction.Die, true);
                _livingEnemies -= 1;
                if (enemy.State.IsBoss)
                {
                    _livingBosses -= 1;
                }
            }
        }

        /// <summary>
        /// Ember vents pulse on the cycle boundary and relic altars count dwell time.
        /// Amendment #5: tide-currents raise their activation cue, ash-walls raise a
        /// telegraph cue and apply band damage on the global 0.6 s tick grid. All run
        /// on stage time, so they keep ticking through the wave intermission.
        /// </summary>
        private void UpdateHazards(float deltaTime)
        {
            _stageTime += deltaTime;

            for (int index = 0; index < _hazards.Length; index += 1)
            {
                HazardConfig hazard = _hazards[index];
                ref HazardRuntime runtime = ref _hazardRuntime[index];

                if (hazard.Kind == HazardKind.EmberVent)
                {
                    int cycle = (int)MathF.Floor((_stageTime + hazard.Phase) / CampaignSpec.VentPeriod);
                    if (cycle <= runtime.Cycle)
                    {
                        continue;
                    }
                    runtime.Cycle = cycle;
                    _events |= SimEvents.HazardPulse;
                    if (IsoWithin(hazard.X, hazard.Y, _player.X, _player.Y, hazard.Radius))
                    {
                        // Gimmicks are player risk only.
                        DamagePlayer(CampaignSpec.VentDamage);
                    }
                    continue;
                }

                if (hazard.Kind == HazardKind.TideCurrent)
                {
                    // One HazardPulse per activation boundary (t crosses telegraph end).
                    float shifted = _stageTime + hazard.Phase - CampaignSpec.CurrentTelegraph;
                    int activation = shifted < 0f
                        ? 0
                        : 1 + (int)MathF.Floor(shifted / CampaignSpec.CurrentPeriod);
                    if (activation > runtime.Cycle)
                    {
                        runtime.Cycle = activation;
                        _events |= SimEvents.HazardPulse;
                    }
                    continue;
                }

                if (hazard.Kind == HazardKind.AshWall)
                {
                    // One HazardPulse per telegraph entry (t crosses WallRest).
                    float shifted = _stageTime + hazard.Phase - CampaignSpec.WallRest;
                    int cycle = shifted < 0f
                        ? 0
                        : 1 + (int)MathF.Floor(shifted / CampaignSpec.WallPeriod);
                    if (cycle > runtime.Cycle)
                    {
                        runtime.Cycle = cycle;
                        _events |= SimEvents.HazardPulse;
                    }

                    // Damage rides the global 0.6 s tick grid; the band is symmetric.
                    int tick = (int)MathF.Floor((_stageTime + hazard.Phase) / CampaignSpec.WallTickPeriod);
                    if (tick <= runtime.Tick)
                    {
                        continue;
                    }
                    runtime.Tick = tick;
                    float depth = WallDepthAt(hazard.Phase, _stageTime);
                    if (depth <= 0f)
                    {
                        continue;
                    }
                    // Edge encoding (spec v1.1): PushX +1 = left wall, −1 = right wall.
                    bool fromRight = hazard.PushX < 0f;
                    if (WallCovers(fromRight, depth, _player.X))
                    {
                        DamagePlayer(CampaignSpec.WallTickDamage);
                    }
                    for (int enemyIndex = 0; enemyIndex < _enemyCount; enemyIndex += 1)
                    {
                        ref Enemy enemy = ref _enemies[enemyIndex];
                        if (!enemy.State.Dead && WallCovers(fromRight, depth, enemy.State.X))
                        {
                            DamageEnemy(ref enemy, CampaignSpec.WallTickDamage);
                        }
                    }
                    continue;
                }

                if (hazard.Kind == HazardKind.EmberPylon)
                {
                    continue; // passive: struck via StrikePylons, aura via PylonAuraMultiplier
                }

                if (hazard.Kind != HazardKind.RelicAltar)
                {
                    continue;
                }

                if (runtime.Cooldown > 0f)
                {
                    runtime.Cooldown = MathF.Max(0f, runtime.Cooldown - deltaTime);
                    runtime.Hold = 0f;
                    continue;
                }

                if (!IsoWithin(hazard.X, hazard.Y, _player.X, _player.Y, hazard.Radius))
                {
                    runtime.Hold = 0f;
                    continue;
                }

                runtime.Hold += deltaTime;
                if (runtime.Hold < CampaignSpec.AltarHoldSeconds)
                {
                    continue;
                }

                runtime.Hold = 0f;
                runtime.Cooldown = CampaignSpec.AltarCooldown;
                _charge = MathF.Min(SimConfig.LanternMax, _charge + CampaignSpec.AltarOilBurst);
                _events |= SimEvents.AltarBlessing;
            }
        }

        /// <summary>
        /// Obsidian pillars are hard blockers: an actor that ends its move inside
        /// <c>pillarRadius + actorRadius</c> is pushed back out along the iso normal.
        /// No-op on the arena path (no hazards).
        /// </summary>
        private void ApplyPillars(ref float x, ref float y, float actorRadius)
        {
            for (int index = 0; index < _hazards.Length; index += 1)
            {
                HazardConfig hazard = _hazards[index];
                if (hazard.Kind != HazardKind.ObsidianPillar)
                {
                    continue;
                }

                float target = hazard.Radius + actorRadius;
                float deltaX = x - hazard.X;
                float deltaY = (y - hazard.Y) * SimConfig.IsoY;
                float distance = Hypot(deltaX, deltaY);
                if (distance >= target)
                {
                    continue;
                }

                if (distance <= MoveEpsilon)
                {
                    // Dead centre has no normal: eject along +x deterministically.
                    x = hazard.X + target;
                    y = hazard.Y;
                    continue;
                }

                x = hazard.X + deltaX / distance * target;
                y = hazard.Y + deltaY / distance * target / SimConfig.IsoY;
            }
        }

        // --- Amendment #5 helpers (docs/SIM_SPEC_DUNGEONS.md) ------------------

        /// <summary>Ash-wall encroachment depth at <paramref name="stageTime"/> (0 = idle).</summary>
        private static float WallDepthAt(float phase, float stageTime)
        {
            float t = (stageTime + phase) % CampaignSpec.WallPeriod;
            float advanceStart = CampaignSpec.WallRest + CampaignSpec.WallTelegraph;
            float holdStart = advanceStart + CampaignSpec.WallAdvance;
            float recedeStart = holdStart + CampaignSpec.WallHold;
            if (t < advanceStart)
            {
                return 0f;
            }
            if (t < holdStart)
            {
                return (t - advanceStart) * CampaignSpec.WallSpeed;
            }
            if (t < recedeStart)
            {
                return CampaignSpec.WallDepthMax;
            }
            return CampaignSpec.WallDepthMax - (t - recedeStart) * CampaignSpec.WallSpeed;
        }

        /// <summary>Band test for a wall's edge orientation (spec v1.1).</summary>
        private static bool WallCovers(bool fromRight, float depth, float x)
            => fromRight
                ? x > CampaignSpec.WallEdgeRightX - depth
                : x < CampaignSpec.WallEdgeX + depth;

        /// <summary>Leading edge published to the view (edge X when idle).</summary>
        private static float WallFrontAt(bool fromRight, float depth)
            => fromRight
                ? CampaignSpec.WallEdgeRightX - depth
                : CampaignSpec.WallEdgeX + depth;

        /// <summary>Tide-current push window test at <paramref name="stageTime"/>.</summary>
        private static bool CurrentActiveAt(float phase, float stageTime)
        {
            float t = (stageTime + phase) % CampaignSpec.CurrentPeriod;
            return t >= CampaignSpec.CurrentTelegraph
                && t < CampaignSpec.CurrentTelegraph + CampaignSpec.CurrentActive;
        }

        /// <summary>
        /// Active tide-currents push any actor inside their rect band (symmetric —
        /// player and enemies alike). Runs after the actor's own move + clamp and
        /// before pillar push-out; re-clamps when it moved the actor. Reads the
        /// previous tick's <see cref="_stageTime"/> by contract (1-tick latency).
        /// </summary>
        private void ApplyCurrents(ref float x, ref float y, float clampMargin, float deltaTime)
        {
            bool pushed = false;
            for (int index = 0; index < _hazards.Length; index += 1)
            {
                HazardConfig hazard = _hazards[index];
                if (hazard.Kind != HazardKind.TideCurrent
                    || !CurrentActiveAt(hazard.Phase, _stageTime))
                {
                    continue;
                }
                if (MathF.Abs(x - hazard.X) > hazard.HalfW || MathF.Abs(y - hazard.Y) > hazard.HalfH)
                {
                    continue;
                }
                x += hazard.PushX * deltaTime;
                y += hazard.PushY * deltaTime;
                pushed = true;
            }
            if (pushed)
            {
                ClampToArena(ref x, ref y, clampMargin);
            }
        }

        /// <summary>
        /// One basic-attack/combo swing against every live pylon in range. Same
        /// range/arc/one-hit-per-attackId rules as enemies, body radius widens the
        /// reach. Skills never strike pylons (§Gimmick 2). Returns true if any hit.
        /// </summary>
        private bool StrikePylons(float damage)
        {
            bool landed = false;
            for (int index = 0; index < _hazards.Length; index += 1)
            {
                HazardConfig hazard = _hazards[index];
                if (hazard.Kind != HazardKind.EmberPylon)
                {
                    continue;
                }
                ref HazardRuntime runtime = ref _hazardRuntime[index];
                if (runtime.Hp <= 0f || runtime.LastHitAttack == _player.AttackId)
                {
                    continue;
                }
                float deltaX = hazard.X - _player.X;
                float deltaY = (hazard.Y - _player.Y) * SimConfig.IsoY;
                float reach = SimConfig.PlayerAttackRange + hazard.Radius;
                if (deltaX * _player.Facing < SimConfig.FacingArcTolerance
                    || deltaX * deltaX + deltaY * deltaY > reach * reach)
                {
                    continue;
                }
                runtime.LastHitAttack = _player.AttackId;
                runtime.Hp = MathF.Max(0f, runtime.Hp - damage);
                landed = true;
                _events |= SimEvents.EnemyHit;
                if (runtime.Hp <= 0f)
                {
                    _events |= SimEvents.PylonDown;
                }
            }
            return landed;
        }

        /// <summary>
        /// ×0.60 on damage TAKEN by an enemy inside any live pylon aura; stacking
        /// is non-cumulative (§Gimmick 2). 1 when no pylon applies.
        /// </summary>
        private float PylonAuraMultiplier(float enemyX, float enemyY)
        {
            for (int index = 0; index < _hazards.Length; index += 1)
            {
                HazardConfig hazard = _hazards[index];
                if (hazard.Kind == HazardKind.EmberPylon
                    && _hazardRuntime[index].Hp > 0f
                    && IsoWithin(hazard.X, hazard.Y, enemyX, enemyY, CampaignSpec.PylonAuraRadius))
                {
                    return CampaignSpec.PylonAuraDamageTakenMult;
                }
            }
            return 1f;
        }

        /// <summary>Iso-weighted containment test (docs/SIM_SPEC.md distance rule).</summary>
        private static bool IsoWithin(float centerX, float centerY, float x, float y, float radius)
        {
            float deltaX = x - centerX;
            float deltaY = (y - centerY) * SimConfig.IsoY;
            return deltaX * deltaX + deltaY * deltaY <= radius * radius;
        }

        // --- Shared math -----------------------------------------------------

        private static void ClampToArena(ref float x, ref float y, float margin)
        {
            float halfWidth = SimConfig.ArenaHalfWidth - margin;
            float halfHeight = SimConfig.ArenaHalfHeight - margin * 0.5f;
            float localX = x - SimConfig.ArenaX;
            float localY = y - SimConfig.ArenaY;
            float normalized = MathF.Abs(localX) / halfWidth + MathF.Abs(localY) / halfHeight;

            if (normalized > 1f)
            {
                localX /= normalized;
                localY /= normalized;
                x = SimConfig.ArenaX + localX;
                y = SimConfig.ArenaY + localY;
            }
        }

        private static float Hypot(float x, float y) => MathF.Sqrt(x * x + y * y);

        private void Publish()
        {
            _enemyView.Clear();
            for (int index = 0; index < _enemyCount; index += 1)
            {
                _enemyView.Add(_enemies[index].State);
            }

            _pickupView.Clear();
            for (int index = 0; index < _pickupCount; index += 1)
            {
                _pickupView.Add(_pickups[index]);
            }

            for (int index = 0; index < _hazards.Length; index += 1)
            {
                HazardConfig hazard = _hazards[index];
                var state = default(HazardState);
                state.Kind = hazard.Kind;
                state.X = hazard.X;
                state.Y = hazard.Y;
                state.Radius = hazard.Radius;
                if (hazard.Kind == HazardKind.EmberVent)
                {
                    float cycleT = (_stageTime + hazard.Phase) % CampaignSpec.VentPeriod;
                    state.CycleT = cycleT;
                    state.Telegraphing = cycleT >= CampaignSpec.VentPeriod - CampaignSpec.VentTelegraph;
                }
                else if (hazard.Kind == HazardKind.RelicAltar)
                {
                    state.CooldownT = _hazardRuntime[index].Cooldown;
                }
                else if (hazard.Kind == HazardKind.TideCurrent)
                {
                    float cycleT = (_stageTime + hazard.Phase) % CampaignSpec.CurrentPeriod;
                    state.CycleT = cycleT;
                    state.Telegraphing = cycleT < CampaignSpec.CurrentTelegraph;
                    state.Active = CurrentActiveAt(hazard.Phase, _stageTime);
                }
                else if (hazard.Kind == HazardKind.AshWall)
                {
                    float cycleT = (_stageTime + hazard.Phase) % CampaignSpec.WallPeriod;
                    state.CycleT = cycleT;
                    state.Telegraphing = cycleT >= CampaignSpec.WallRest
                        && cycleT < CampaignSpec.WallRest + CampaignSpec.WallTelegraph;
                    float depth = WallDepthAt(hazard.Phase, _stageTime);
                    state.FrontX = WallFrontAt(hazard.PushX < 0f, depth);
                    state.Active = depth > 0f;
                }
                else if (hazard.Kind == HazardKind.EmberPylon)
                {
                    state.Hp = _hazardRuntime[index].Hp;
                }
                _hazardView[index] = state;
            }
        }
    }
}
