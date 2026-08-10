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
    public sealed class CinderSim : ICinderSim, ICampaignSnapshot, IHackSnapshot,
                                    IRunPreparationSnapshot, IGrowthChoiceSnapshot,
                                    IDerivedStatSnapshot, IDungeonProgressionSnapshot
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
        // AMENDMENT #14: PickupState lives in the FROZEN SimTypes.cs, so the grade
        // rides a parallel array kept in lockstep with _pickups on every mutation.
        private LootGrade[] _pickupGrades = new LootGrade[SimConfig.EnemyCap];

        private readonly List<EnemyState> _enemyView = new List<EnemyState>(SimConfig.EnemyCap);
        private readonly List<PickupState> _pickupView = new List<PickupState>(SimConfig.EnemyCap);
        private readonly List<LootGrade> _pickupGradeView = new List<LootGrade>(SimConfig.EnemyCap);

        // --- AMENDMENT #13 / #14 opt-in state (inert unless the caller opts in) ---
        private readonly DungeonProgressionConfig _progression;
        private int _ddaBand;
        private int _waveBudget;
        private int _waveEliteAllowance;
        private int _waveHitsTaken;
        private float _waveSeconds;
        private int _elitesThisWave;
        private int _finePity;
        private int _epicPity;
        private int _dropOrdinal;
        private LootGrade _lastLootGrade;
        // AMENDMENT #15: resolved ONCE in the constructor — the playfield cannot
        // change mid-run, which is what keeps a run reproducible from
        // (config, input sequence) alone. The field initializers are the frozen
        // constants, so the arena and campaign constructors need no change at all.
        private readonly float _boundsHalfWidth = SimConfig.ArenaHalfWidth;
        private readonly float _boundsHalfHeight = SimConfig.ArenaHalfHeight;

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
        /// <summary>
        /// AMENDMENT #11 §16: the resolved difficulty table for this run. Assigned in
        /// every constructor; the arena/campaign constructors leave it at
        /// <see cref="Difficulty.Normal"/>, whose profile is entirely neutral, so their
        /// behaviour is byte-identical to the pre-amendment build.
        /// </summary>
        private readonly DifficultyProfile _difficulty = DifficultySpec.For(Difficulty.Normal);
        /// <summary>
        /// §16 C scratch: per-enemy "cleared to swing this tick", rebuilt by
        /// <c>PlanEnemyGroup</c> at the top of every enemy update. Indexed by the same
        /// index as <c>_enemies</c>; only read on a token-limited tier.
        /// </summary>
        private bool[] _mayAttack = new bool[SimConfig.EnemyCap];


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
        private ComboVariant _comboVariant;  // finisher branch, latched at swing start
        // AMENDMENT #9 momentum gauge (A9). Dungeon-only; in every other mode
        // these stay 0 for the whole run, which is what keeps the frozen arena
        // and prologue digests bit-identical.
        private float _momentum;          // 0..HackSpec.MomentumMax
        private float _momentumGrace;     // seconds of decay protection left
        private int _momentumTierSeen;    // tier at the end of the previous tick

        // Input depth §3/§5.
        private float _chargeTime;            // seconds the attack key has been held
        private bool _growthOfferOpen;
        private float _growthOfferTime;
        private GrowthChoiceKind _lastGrowthChoice;
        private int _growthAttack, _growthVitality, _growthSwiftness;
        private float _dashCooldown;
        private float _dashTime;
        private float _dashDirX, _dashDirY;
        // Motion depth: player launch state. Deliberately private rather than
        // on PlayerState — the snapshot contract is frozen, and the View can
        // infer the launch from position velocity (ActorView L145-158) exactly
        // as it already does for enemies.
        private float _playerKnockX, _playerKnockY, _playerKnockTime;
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
        // AMENDMENT #6 (D6.1-D6.5): 0..3 companion slots. slot 0 reproduces the
        // frozen §4 follower exactly (fan-out 0, ember-cohort/fallback tuple), so a
        // zero/single-companion run stays digest-identical. Arrays are length 3 and
        // only the first _companionCount entries are live.
        private const int MaxCompanions = 3;
        private readonly float[] _companionX = new float[MaxCompanions];
        private readonly float[] _companionY = new float[MaxCompanions];
        private readonly float[] _companionTimer = new float[MaxCompanions];
        private readonly float[] _companionShow = new float[MaxCompanions];
        private readonly int[] _companionFacing = new int[MaxCompanions];
        private CompanionBehavior _companionBehavior;
        private readonly int _companionCount;
        private readonly float _boltDamage;
        private readonly float _pulseTickDamage;
        private readonly float _ashNovaDamage;
        // Per-slot D6.3 combat tuple. slot 0 carries the §4/ember-cohort values and any
        // GuardianResonance modifier; further slots carry their own archetype tuple.
        private readonly float[] _companionAttackInterval = new float[MaxCompanions];
        private readonly float[] _companionAttackRange = new float[MaxCompanions];
        private readonly float[] _companionDamageScale = new float[MaxCompanions];
        // NOTE — amendment numbering, settled at merge time. main owns #7
        // (companion autonomy) and #8 (signature skills); the momentum lane
        // already writes itself as A9 in HudView. The training-ground + surge
        // work below, which briefly also called itself #7, is therefore
        // **AMENDMENT #10** everywhere. Only the label moved — no field, no
        // constant and no behaviour changed with it.
        // AMENDMENT #7 (A7.1-A7.4): per-slot autonomy state. All of it is derived from
        // counters and fixed-step accumulation — no RNG, so §13 still holds. The target is
        // stored as an ENEMY ID, never an index: RemoveEnemyAt (CinderSim.cs:2245) shifts
        // the tail down, so indices are reused while ids from _nextEnemyId never are.
        private readonly int[] _companionTargetId = new int[MaxCompanions];
        private readonly float[] _companionLockTimer = new float[MaxCompanions];
        private readonly float[] _companionReturnGrace = new float[MaxCompanions];
        private readonly bool[] _companionEngaged = new bool[MaxCompanions];
        // AMENDMENT #8 (A8.2-A8.5): per-slot signature skill. The SPEC is resolved once at
        // construction because it is a constant of the archetype; only the cooldown and the
        // display flash are state. No RNG: the cooldown is fixed-step accumulation compared
        // against compile-time constants, so §13 still holds.
        private readonly CompanionSkillSpec[] _companionSkill = new CompanionSkillSpec[MaxCompanions];
        private readonly float[] _companionSkillCooldown = new float[MaxCompanions];
        private readonly float[] _companionSkillFlash = new float[MaxCompanions];
        /// <summary>Target-selection scratch for ONE cast, sized by the A8.2 hard cap so a
        /// cast never allocates. Holds enemy INDICES and is only ever live inside
        /// <see cref="CastCompanionSkill"/>, which does not compact the enemy array.</summary>
        private readonly int[] _companionSkillHits = new int[HackSpec.CompanionSkillTargetCap];

        // --- AMENDMENT #6 sigils: resolved ONCE at construction, so the per-tick
        // cost is a field read and an unequipped run keeps every original constant
        // (the 15 golden rows prove it). Initializers are the pre-amendment values,
        // which is what the arena and classic-campaign constructors keep.
        private float _sigilCurrentPlayerPushMult = 1f;
        private float _sigilCurrentEnemyPushMult = 1f;
        private float _sigilPylonAuraMult = CampaignSpec.PylonAuraDamageTakenMult;
        private float _sigilPylonStrikeMult = 1f;
        private float _sigilWallPlayerTick = CampaignSpec.WallTickDamage;
        private float _sigilWallEnemyTick = CampaignSpec.WallTickDamage;
        private float _sigilVentOilRefund;                     // 0 = no refund
        private float _sigilVentEnemyDamage;                   // 0 = vents stay player-only
        private float _sigilAltarHoldSeconds = CampaignSpec.AltarHoldSeconds;
        private float _sigilAltarOilBurst = CampaignSpec.AltarOilBurst;
        // --- AMENDMENT #10 surge: two deterministic windows, different axes.
        // Peril slows the hazard CLOCK (only ever lengthens a telegraph); surge
        // multiplies hazard damage dealt to ENEMIES (never touches timing). Both
        // are inert in a run that never trips them, so the goldens do not move.
        private readonly bool _surgeEnabled;
        private float _perilTimer;                             // >0 = peril window open
        private int _perilUsed;                                // run cap (HackSpec.PerilRunCap)
        private bool _perilArmed = true;                       // hysteresis latch
        private float _surgeTimer;                             // >0 = surge window open
        private int _surgeKillMark;                            // last kill count that opened one
        private bool _surgeUsedThisWave;
        private float _sigilSurgeEnemyHazardMult = HackSpec.SurgeEnemyHazardMult;
        private bool _sigilSurgeEnemyBoost;                    // 점화인 surge clause gate
        private bool _sigilPerilCurrentSuppress;               // 역류인 peril clause
        private bool _sigilPerilWallHalf;                      // 집행인 peril clause
        private bool _sigilPerilAltarInstant;                  // 증언인 peril clause
        private bool _sigilSurgePylonAuraStop;                 // 판결인 surge clause
        // --- AMENDMENT #10 training: a fixed-length trial with no spawn table.
        private readonly bool _training;
        // 1, NOT default(float). The arena/classic-campaign constructors never
        // touch this field, and a 0 here freezes the hazard clock for every
        // non-hack run — which is exactly what it did before the gate caught it
        // (7 hazard tests + a golden row).
        private readonly float _trainingRate = 1f;             // hazard clock scale by tier
        private float _trainingTimer;
        private int _trainingHits;                             // trial score: fewer is better
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
        // AMENDMENT #16 §20: resolved once from the run's stage id. None on every
        // pre-amendment path (arena, prologue, training, campaign, and any dungeon
        // run whose caller did not set DungeonProgressionConfig.BossVariety), and
        // None resolves to the frozen §7 vectors, so the goldens do not move.
        private readonly BossArchetype _bossArchetype;

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
            // Initialize companion slot 0 to frozen §4 spec (single-companion backward compat)
            _companionAttackInterval[0] = HackSpec.CompanionAttackInterval;
            _companionAttackRange[0] = HackSpec.CompanionAttackRange;
            _companionDamageScale[0] = HackSpec.CompanionDamageScale;
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
            // Initialize companion slot 0 to frozen §4 spec (campaign compat)
            _companionAttackInterval[0] = HackSpec.CompanionAttackInterval;
            _companionAttackRange[0] = HackSpec.CompanionAttackRange;
            _companionDamageScale[0] = HackSpec.CompanionDamageScale;
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
            : this(in config, default)
        {
        }

        /// <summary>
        /// Hack &amp; slash run with the opt-in progression amendments (#13 point-budget
        /// waves + DDA, #14 graded loot + pity). This is the ONLY way to reach either
        /// amendment; the single-argument constructor forwards
        /// <c>default(DungeonProgressionConfig)</c>, which is both switches off, so
        /// every existing caller and every golden digest keeps its frozen numbers.
        /// </summary>
        public CinderSim(in HackConfig config, in DungeonProgressionConfig progression)
        {
            _hack = true;
            // AMENDMENT #13/#14 are dungeon-only (seed decision D3): the arena and the
            // prologue must not gain a branch, and a trial has no economy to grade.
            _progression = config.Mode == GameMode.Dungeon ? progression : default;
            DungeonBoundsSpec.Resolve(
                in _progression.Bounds, out _boundsHalfWidth, out _boundsHalfHeight);
            _gameMode = config.Mode;
            _prologue = config.Mode == GameMode.Prologue;
            _dungeon = config.Mode == GameMode.Dungeon;
            _training = config.Mode == GameMode.Training;
            _campaign = _dungeon;
            _appliedPreparationInput = _dungeon ? config.PreparationOffer : default;
            // Surge is dungeon-only: a trial is where you learn the gimmick
            // unaided, and the arena/prologue goldens must not gain a new branch.
            _surgeEnabled = _dungeon;
            _trainingRate = _training ? HackSpec.TrainingTierRate(config.TrainingTier) : 1f;

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
            // AMENDMENT #11 §16: resolved once — the tier cannot change mid-run, which is
            // what keeps a run reproducible from (config, input sequence) alone.
            _difficulty = DifficultySpec.For(configured.Difficulty);

            _boltDamage = boltDamage;
            _pulseTickDamage = pulseTickDamage;
            _ashNovaDamage = ashNovaDamage;
            // AMENDMENT #6 (D6.2-D6.4): resolve 0..3 companion slots from the frozen
            // CompanionId + CompanionIds pair. Every slot resolves its own D6.3
            // per-archetype base tuple, then folds in the same GuardianResonance modifier.
            // For a legacy ember-cohort/fallback slot the archetype tuple IS the §4 base,
            // so this reproduces ApplyPreparation's resonance bit-for-bit and a
            // zero/single-companion ember-cohort run stays digest-identical.
            string[] slots = _dungeon ? configured.CompanionSlots() : System.Array.Empty<string>();
            _companionCount = slots.Length;
            for (int slot = 0; slot < _companionCount; slot += 1)
            {
                EnemyVisual archetype = HackSpec.CompanionArchetype(slots[slot]);
                HackSpec.CompanionStats(
                    archetype,
                    out float cadence,
                    out float range,
                    out float damageScale);
                // AMENDMENT #8: same archetype key as D6.3, so a slot's skill and its combat
                // tuple can never disagree about which companion this is. GuardianResonance
                // is deliberately NOT folded in — A8.6 keeps skills out of preparation scaling.
                _companionSkill[slot] = HackSpec.CompanionSkill(archetype);
                ApplyGuardianResonance(
                    in config.PreparationOffer, ref cadence, ref range, ref damageScale);
                _companionAttackInterval[slot] = cadence;
                _companionAttackRange[slot] = range;
                _companionDamageScale[slot] = damageScale;
            }
            _config = _dungeon ? configured.ToCampaignConfig() : default;
            _companionActive = _companionCount > 0;

            ResolveSigils(_dungeon ? configured.Sigils : default);
            // AMENDMENT #10: a trial carries its hazards on the config directly
            // (there is no campaign table for it), so the routing gains a third
            // arm. The dungeon and non-dungeon arms are main's, unchanged.
            _hazards = _dungeon
                ? (_config.Hazards ?? NoHazards)
                : (_training ? (configured.Hazards ?? NoHazards) : NoHazards);
            _hazardRuntime = _hazards.Length == 0 ? NoHazardRuntime : new HazardRuntime[_hazards.Length];
            _hazardView = new List<HazardState>(_hazards.Length);
            _stageId = _prologue
                ? HackSpec.PrologueStageId
                : (_dungeon ? (_config.StageId ?? string.Empty)
                    : (_training ? (configured.StageId ?? string.Empty) : string.Empty));
            // AMENDMENT #16 §20.3: the archetype is a pure function of the stage id
            // and cannot change mid-run — the same rule AMENDMENT #11 applies to the
            // difficulty tier, and what keeps a run reproducible from (config, input)
            // alone. _progression is already zeroed for non-dungeon modes above.
            _bossArchetype = _progression.BossVariety
                ? BossVarietySpec.ArchetypeFor(_stageId)
                : BossArchetype.None;
            for (int index = 0; index < _hazards.Length; index += 1)
            {
                _hazardView.Add(default);
            }
            Restart();
        }

        /// <summary>
        /// Turns the equipped loadout into the per-run gimmick constants
        /// (AMENDMENT #6 • design/sigil-spec.md). Called once from the hack
        /// constructor; an empty loadout writes nothing, so every field keeps the
        /// pre-amendment initializer and the run is byte-identical.
        ///
        /// Every branch is a constant swap — no probability, no per-tick state.
        /// That is survey rule 2 (predictability is the product identity) enforced
        /// structurally: there is nowhere for randomness to enter.
        /// </summary>
        private void ResolveSigils(in SigilLoadout loadout)
        {
            if (loadout.Has(SigilKind.Countercurrent, SigilFace.A))
                _sigilCurrentPlayerPushMult = HackSpec.SigilCurrentPlayerPushMult;
            if (loadout.Has(SigilKind.Countercurrent, SigilFace.B))
                _sigilCurrentEnemyPushMult = HackSpec.SigilCurrentEnemyPushMult;

            if (loadout.Has(SigilKind.Verdict, SigilFace.A))
                _sigilPylonAuraMult = HackSpec.SigilPylonAuraRelief;
            if (loadout.Has(SigilKind.Verdict, SigilFace.B))
                _sigilPylonStrikeMult = HackSpec.SigilPylonStrikeMult;

            if (loadout.Has(SigilKind.Executioner, SigilFace.A))
                _sigilWallPlayerTick = HackSpec.SigilWallPlayerTick;
            if (loadout.Has(SigilKind.Executioner, SigilFace.B))
                _sigilWallEnemyTick = HackSpec.SigilWallEnemyTick;

            if (loadout.Has(SigilKind.Ignition, SigilFace.A))
                _sigilVentOilRefund = HackSpec.SigilVentOilRefund;
            if (loadout.Has(SigilKind.Ignition, SigilFace.B))
                _sigilVentEnemyDamage = HackSpec.SigilVentEnemyDamage;

            if (loadout.Has(SigilKind.Witness, SigilFace.A))
                _sigilAltarHoldSeconds = HackSpec.SigilAltarHoldSeconds;
            if (loadout.Has(SigilKind.Witness, SigilFace.B))
                _sigilAltarOilBurst = HackSpec.SigilAltarOilBurst;

            // --- AMENDMENT #10 surge clauses. Face-independent: the clause is the
            // sigil waking up inside a window, not the face doing more.
            //
            // The three PERIL clauses are the ones that looked like immunity in
            // the draft. The director's arithmetic (negotiation entry 8) found
            // only one that actually was: the wall exemption avoided 100 damage
            // in 6 s, 100% of base HP. It is a HALVED TICK for 3 s here (25, 25%).
            // Countercurrent and Witness were cleared unchanged — the current
            // deals no direct damage and the altar grants oil, so neither one
            // avoids damage at all.
            //
            // No-stacking (entry 8 cap 2): only the peril clause of the sigil in
            // the LOWER slot fires, so two peril sigils cannot compound.
            SigilKind perilOwner = loadout.PerilPriority(
                SigilKind.Countercurrent, SigilKind.Executioner, SigilKind.Witness);
            _sigilPerilCurrentSuppress = perilOwner == SigilKind.Countercurrent;
            _sigilPerilWallHalf = perilOwner == SigilKind.Executioner;
            _sigilPerilAltarInstant = perilOwner == SigilKind.Witness;

            _sigilSurgePylonAuraStop = loadout.HasKind(SigilKind.Verdict);
            // 점화인 owns the enemy-damage clause. Gate AND magnitude both live
            // here, so an unequipped run never multiplies anything and the 15
            // golden digests stay byte-identical (the probe proved the cost of
            // getting this wrong: a plain run's wall tick would have doubled).
            _sigilSurgeEnemyBoost = loadout.HasKind(SigilKind.Ignition);
            if (_sigilSurgeEnemyBoost)
                _sigilSurgeEnemyHazardMult = HackSpec.SigilSurgeEnemyHazardMult;
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
                    ApplyGuardianResonance(
                        in offer,
                        ref companionAttackInterval,
                        ref companionAttackRange,
                        ref companionDamageScale);
                    break;
            }
        }

        /// <summary>
        /// AMENDMENT #6 (D6.3): the Amendment #4 GuardianResonance modifier, factored out so
        /// it can be applied to every companion slot after its per-archetype base. Preserves
        /// the frozen clamps (0.5 s cadence floor). Variants: 1 = faster cadence,
        /// 2 = longer range, 3 = higher damage scale; magnitude 1..2.
        /// </summary>
        private static void ApplyGuardianResonance(
            in PreparationOffer offer,
            ref float companionAttackInterval,
            ref float companionAttackRange,
            ref float companionDamageScale)
        {
            if (offer.Kind != PreparationOfferKind.GuardianResonance
                || offer.Variant < 1 || offer.Variant > 3
                || offer.Magnitude < 1 || offer.Magnitude > 2)
            {
                return;
            }

            switch (offer.Variant)
            {
                case 1:
                    companionAttackInterval = MathF.Max(
                        0.5f, companionAttackInterval * (1f - 0.10f * offer.Magnitude));
                    break;
                case 2:
                    companionAttackRange += 20f * offer.Magnitude;
                    break;
                case 3:
                    companionDamageScale *= 1f + 0.10f * offer.Magnitude;
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

        // --- IDungeonProgressionSnapshot (AMENDMENT #13 / #14 / #16) ----------

        public bool AdaptiveWavesActive => _progression.AdaptiveWaves;
        public bool GradedLootActive => _progression.GradedLoot;
        public int DifficultyBand => _ddaBand;
        public int WaveBudget => _waveBudget;
        public int WaveEliteAllowance => _waveEliteAllowance;
        public int WaveHitsTaken => _waveHitsTaken;
        public float WaveElapsedSeconds => _waveSeconds;
        public int FinePity => _finePity;
        public int EpicPity => _epicPity;
        public LootGrade LastLootGrade => _lastLootGrade;
        public IReadOnlyList<LootGrade> PickupGrades => _pickupGradeView;
        public float BoundsHalfWidth => _boundsHalfWidth;
        public float BoundsHalfHeight => _boundsHalfHeight;
        public bool ExpandedBoundsActive =>
            _boundsHalfWidth > SimConfig.ArenaHalfWidth || _boundsHalfHeight > SimConfig.ArenaHalfHeight;

        // AMENDMENT #16 §20.5. BossVarietyActive is derived from the RESOLVED
        // archetype, not from the config flag: a gated run on an unmapped stage
        // fights the frozen boss, and the View must be told that rather than
        // being told the gate is on and then handed frozen numbers.
        public bool BossVarietyActive => _bossArchetype != BossArchetype.None;
        public BossArchetype BossArchetype => _bossArchetype;
        public int BossPhaseCount => BossVarietySpec.For(_bossArchetype).PhaseCount;
        public float BossTelegraphSeconds =>
            BossVarietySpec.For(_bossArchetype).TelegraphSeconds(BossPhaseVectorIndex());
        public SimEvents Events => _events;
        public float NovaX => _novaX;
        public float NovaY => _novaY;

        // --- AMENDMENT #10 surge / training (view-read only) -------------------
        /// <summary>Seconds left in the peril window (0 = closed).</summary>
        public float PerilRemaining => _perilTimer;
        /// <summary>Seconds left in the surge window (0 = closed).</summary>
        public float SurgeRemaining => _surgeTimer;
        /// <summary>Peril windows spent this run (cap HackSpec.PerilRunCap).</summary>
        public int PerilUsed => _perilUsed;
        /// <summary>Seconds elapsed in the current trial.</summary>
        public float TrainingElapsed => _trainingTimer;
        /// <summary>Gimmick hits taken this trial — the score, lower is better.</summary>
        public int TrainingHits => _trainingHits;

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
        /// <summary>
        /// AMENDMENT #11 §16: the tier this run was constructed with. Read-only — the
        /// tier is fixed for the life of the sim, so a run stays reproducible from
        /// (config, input sequence) alone. Deliberately a CinderSim member and not an
        /// IHackSnapshot member: the frozen interface stays frozen.
        /// </summary>
        public Difficulty RunDifficulty => _hackConfig.Difficulty;

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
        public float CompanionX => _companionX[0];
        public float CompanionY => _companionY[0];
        public bool CompanionAttacking => _companionShow[0] > 0f;
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
        public int CompanionFacing => _companionFacing[0];
        // AMENDMENT #6 (D6.5): multi-slot snapshot surface. Scalars above alias slot 0,
        // so a zero/single-companion run reads identically to the pre-amendment contract.
        public int CompanionCount => _companionCount;
        public float CompanionXAt(int slot) => _companionX[ClampCompanionSlot(slot)];
        public float CompanionYAt(int slot) => _companionY[ClampCompanionSlot(slot)];
        public bool CompanionAttackingAt(int slot) => _companionShow[ClampCompanionSlot(slot)] > 0f;
        public CompanionBehavior CompanionBehaviorAt(int slot) => _companionBehavior;
        public int CompanionFacingAt(int slot) => _companionFacing[ClampCompanionSlot(slot)];
        // AMENDMENT #7 (A7.1/A7.2): derived autonomy state. Kept apart from the commanded
        // CompanionBehavior above so a command can never be read back as a derived state.
        public bool CompanionEngagedAt(int slot) => _companionEngaged[ClampCompanionSlot(slot)];
        public int CompanionTargetIdAt(int slot) => _companionTargetId[ClampCompanionSlot(slot)];
        // AMENDMENT #8 (A8.5). A run with no companions reports the default skill (None, 0, false).
        public CompanionSkillId CompanionSkillIdAt(int slot) =>
            _companionCount <= 0 ? CompanionSkillId.None : _companionSkill[ClampCompanionSlot(slot)].Id;
        public float CompanionSkillCooldownAt(int slot) =>
            _companionCount <= 0 ? 0f : _companionSkillCooldown[ClampCompanionSlot(slot)];
        public bool CompanionSkillCastingAt(int slot) =>
            _companionCount > 0 && _companionSkillFlash[ClampCompanionSlot(slot)] > 0f;

        private int ClampCompanionSlot(int slot)
        {
            if (_companionCount <= 0)
            {
                return 0;
            }
            if (slot < 0)
            {
                return 0;
            }
            return slot >= _companionCount ? 0 : slot;
        }


        // --- input depth §5 (IGrowthChoiceSnapshot, additive) -----------------
        public bool GrowthOfferOpen => _growthOfferOpen;
        public float GrowthOfferTime => _growthOfferTime;
        public GrowthChoiceKind LastGrowthChoice => _lastGrowthChoice;
        public int GrowthAttack => _growthAttack;
        public int GrowthVitality => _growthVitality;
        public int GrowthSwiftness => _growthSwiftness;

        // --- AMENDMENT #9 codex (IDerivedStatSnapshot, additive) --------------
        // Nine field reads, no arithmetic. ApplyLevelStats() (:2666) already
        // wrote every one of them on the live path; the golden digest cannot
        // move because nothing here participates in a float expression.
        public float PlayerDamage => _playerDamage;
        public float PlayerMaxHealth => _playerMaxHealth;
        public float PlayerSpeed => _playerSpeed;
        public float LanternRegenPerSecond => _lanternRegen;
        public float ExtractionBonus => _extractionBonus;
        public float BaseDamage => _baseDamage;
        public float BaseMaxHealth => _baseMaxHealth;
        public float BaseSpeed => _baseSpeed;
        public float BaseLanternRegen => _baseRegen;
        // The meta stats folded INTO the base. Without these the codex can
        // only say "72.8 comes from 72.8" on a fresh run — true, and useless.
        // The player spent those points; the breakdown owes them the line.
        public int MetaAttack => _hack ? _hackConfig.MetaStats.Attack : 0;
        public int MetaVitality => _hack ? _hackConfig.MetaStats.Vitality : 0;
        public int MetaSwiftness => _hack ? _hackConfig.MetaStats.Swiftness : 0;
        /// <summary>§3: 0..1 charge progress, for the HUD gauge.</summary>
        public float ChargeProgress => _chargeTime <= 0f
            ? 0f
            : MathF.Min(1f, _chargeTime / HackSpec.ChargeReadySeconds);

        // --- AMENDMENT #9 momentum (A9.5, additive) --------------------------
        /// <summary>A9.5: the gauge, 0..100. Outside a dungeon run nothing ever adds to
        /// it, so this is a constant 0 there rather than a mode-checked expression.</summary>
        public float Momentum => _momentum;
        /// <summary>A9.5: the tier the gauge currently sits in (0..3).</summary>
        public int MomentumTier => HackSpec.MomentumTierOf(_momentum);
        /// <summary>A9.5: the melee multiplier that tier grants; exactly 1 at tier 0.</summary>
        public float MomentumDamageMultiplier => HackSpec.MomentumDamageMulOf(_momentum);


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

            // A9.3: the gauge decays BEFORE this tick's swing resolves, so the
            // damage a swing deals is the tier the HUD showed when the player
            // committed to it — never a value that only existed mid-tick.
            UpdateMomentumDecay(dt);

            UpdatePlayer(dt, in input);

            if (_companionActive && _mode != SimMode.GameOver)
            {
                UpdateCompanionBehavior(in input);
                UpdateCompanion(dt, input.CompanionSkillQueued);
            }
            UpdateEnemies(dt);
            if (_dungeon && _mode != SimMode.GameOver)
            {
                UpdateBossPhase();
            }
            // AMENDMENT #10: surge windows resolve BEFORE the hazards they modify,
            // so a window that opens this tick is already in force for this tick's
            // hazard arithmetic (no one-tick lag between "you dropped below 35%"
            // and "the clock slowed").
            if (_surgeEnabled && _mode != SimMode.GameOver)
            {
                UpdateSurge(dt);
            }
            if ((_campaign || _training) && _mode != SimMode.GameOver)
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
                    // Input depth §5: runs AFTER the wave/kill path that can
                    // open an offer, so a level-up gained this tick gets its
                    // full window rather than losing one tick of it.
                    UpdateGrowthOffer(dt, in input);
                }
                if (_training)
                {
                    UpdateTraining(dt);
                }
                else
                {
                    UpdateWave(dt);
                }
            }

            // A9.5: edge-trigger the tier AFTER every gain and loss this tick, so a
            // gauge that crossed two thresholds in one tick still raises exactly one
            // cue, and a hit that knocked it back down raises none.
            PublishMomentumTier();

            Publish();
        }

        // --- AMENDMENT #9 momentum gauge (A9) --------------------------------

        /// <summary>A9.3: hold the gauge for the grace window after the last gain, then
        /// drain it at a constant rate. Dungeon-gated at the single point where the
        /// gauge can move at all, so every other mode carries a permanent 0.</summary>
        private void UpdateMomentumDecay(float deltaTime)
        {
            if (!_dungeon || _momentum <= 0f)
            {
                return;
            }
            if (_momentumGrace > 0f)
            {
                _momentumGrace = MathF.Max(0f, _momentumGrace - deltaTime);
                return;
            }
            _momentum = MathF.Max(0f, _momentum - HackSpec.MomentumDecayPerSecond * deltaTime);
        }

        /// <summary>A9.2: the ONLY way the gauge rises. Called once per enemy a player
        /// melee swing connects with; <paramref name="killed"/> adds the finish bonus.
        /// Skills and companions deliberately do not feed it (A9.6).</summary>
        private void GainMomentum(bool killed)
        {
            if (!_dungeon)
            {
                return;
            }
            float gain = HackSpec.MomentumPerHit + (killed ? HackSpec.MomentumPerKill : 0f);
            _momentum = MathF.Min(HackSpec.MomentumMax, _momentum + gain);
            _momentumGrace = HackSpec.MomentumGraceSeconds;
        }

        /// <summary>A9.3: being hit costs a flat slice AND cancels the grace, so the
        /// drain starts on the very next tick instead of after another 1.6 s.</summary>
        private void SpendMomentumOnHurt()
        {
            if (!_dungeon)
            {
                return;
            }
            _momentum = MathF.Max(0f, _momentum - HackSpec.MomentumHurtPenalty);
            _momentumGrace = 0f;
        }

        /// <summary>A9.5: raise <see cref="SimEvents.MomentumTierUp"/> only on an upward
        /// tier crossing, and remember the tier for the next tick's comparison.</summary>
        private void PublishMomentumTier()
        {
            int tier = HackSpec.MomentumTierOf(_momentum);
            if (tier > _momentumTierSeen)
            {
                _events |= SimEvents.MomentumTierUp;
            }
            _momentumTierSeen = tier;
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
            //
            // AMENDMENT #10: a TRIAL gets the same kit. The view already promised
            // it — GameDirector sets InputAdapter.Profile.Dungeon on entry with
            // the comment "full kit: you practise with your tools" — but the sim
            // fell through to the arena branch, which reads only Nova and Ward.
            // Q/E/Shift were dead keys in every trial: the input published
            // BoltQueued/PulseQueued/DashQueued and nothing consumed them.
            // Found by the skill-VFX lane (qa/skill-vfx-mode-coverage.md) as
            // "2 of 5 silhouettes never fire in training" — a VFX symptom whose
            // cause was here, in the sim.
            if (_dungeon || _training)
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
            // Input depth §5: swiftness shortens the dodge cycle as well as
            // raising speed. Speed alone made it the weak pick — a movement
            // stat that does not change how often you can escape is not really
            // a defensive choice. Clamped so stacked points cannot drive the
            // cooldown toward zero.
            _dashCooldown = HackSpec.DashCooldownSeconds * MathF.Max(
                HackSpec.GrowthSwiftnessCooldownFloor,
                1f - HackSpec.GrowthSwiftnessCooldown * _growthSwiftness);
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
            // Motion depth: the authored `defence` clip (Body Block) shipped
            // dead. The aegis cast window is literally a block — the player is
            // invulnerable for AegisCastInvuln seconds — so the pose and the
            // rule now say the same thing. Forced: the block must read
            // immediately, and it is what makes the i-frames legible.
            SetPlayerAction(ActorAction.Defence, true);
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
            // Zero exclusions runs exactly the frozen comparison sequence below.
            return NearestEnemyIndexExcluding(x, y, radius, null, 0);
        }

        /// <summary>
        /// <see cref="NearestEnemyIndex"/> with the first <paramref name="excludeCount"/>
        /// entries of <paramref name="exclude"/> (enemy INDICES) skipped — AMENDMENT #8 needs
        /// the 2nd..Nth nearest for a multi-target cast. Indices are valid only within one
        /// cast, which never compacts the enemy array.
        /// </summary>
        private int NearestEnemyIndexExcluding(
            float x, float y, float radius, int[] exclude, int excludeCount)
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
                if (IsExcluded(exclude, excludeCount, index))
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

        private static bool IsExcluded(int[] exclude, int excludeCount, int index)
        {
            for (int slot = 0; slot < excludeCount; slot += 1)
            {
                if (exclude[slot] == index)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Push an enemy straight away from the player over <paramref name="time"/>.</summary>
        private void Knockback(ref Enemy enemy, float distance, float time)
        {
            KnockbackFrom(ref enemy, _player.X, _player.Y, distance, time);
        }

        /// <summary>
        /// <see cref="Knockback"/> from an arbitrary source point — AMENDMENT #8's shockwave
        /// shoves away from the COMPANION, not the player. Passing the player's position
        /// reproduces the frozen push exactly, including the degenerate-overlap fallback.
        /// </summary>
        private void KnockbackFrom(
            ref Enemy enemy, float sourceX, float sourceY, float distance, float time)
        {
            float deltaX = enemy.State.X - sourceX;
            float deltaY = enemy.State.Y - sourceY;
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

        /// <summary>Launch the PLAYER away from a source point. Motion depth:
        /// until now only enemies could be launched, so nothing the boss did
        /// ever moved the player's body — every hit read as a number and a
        /// colour flash. State lives in private fields, NOT on PlayerState, so
        /// the frozen snapshot contract is untouched; the View infers the
        /// launch from position velocity exactly as it already does for
        /// enemies (ActorView L145-158).</summary>
        private void KnockbackPlayer(float sourceX, float sourceY, float distance, float time)
        {
            float deltaX = _player.X - sourceX;
            float deltaY = _player.Y - sourceY;
            float length = Hypot(deltaX, deltaY);
            if (length <= MoveEpsilon)
            {
                deltaX = -_player.Facing;
                deltaY = 0f;
                length = 1f;
            }
            float speed = distance / time;
            _playerKnockX = deltaX / length * speed;
            _playerKnockY = deltaY / length * speed;
            _playerKnockTime = time;
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

            // Input depth §5: open a choice instead of ending here. The stat
            // bump above still lands, so ignoring the offer is exactly the old
            // behaviour; choosing adds a point on top. A pending offer is
            // auto-confirmed rather than queued — two stacked offers would
            // leave the player unable to tell which level they are choosing.
            if (_dungeon)
            {
                if (_growthOfferOpen)
                {
                    ApplyGrowthChoice(GrowthChoiceKind.None);
                }
                _growthOfferOpen = true;
                _growthOfferTime = HackSpec.GrowthOfferSeconds;
            }
        }

        /// <summary>Input depth §5: bank one growth point and close the offer.
        /// <c>None</c> means the timer expired — it applies nothing extra, so
        /// a player who never presses 1/2/3 gets exactly the pre-amendment
        /// automatic distribution and loses nothing.</summary>
        private void ApplyGrowthChoice(GrowthChoiceKind choice)
        {
            _growthOfferOpen = false;
            _growthOfferTime = 0f;
            _lastGrowthChoice = choice;
            switch (choice)
            {
                case GrowthChoiceKind.Attack:
                    _growthAttack += 1;
                    break;
                case GrowthChoiceKind.Vitality:
                    _growthVitality += 1;
                    // Vitality heals immediately, or the choice would feel
                    // like nothing at the moment it is made.
                    _player.Health = MathF.Min(
                        _playerMaxHealth + HackSpec.GrowthVitalityHealth,
                        _player.Health + HackSpec.GrowthVitalityHealth);
                    break;
                case GrowthChoiceKind.Swiftness:
                    _growthSwiftness += 1;
                    break;
            }
            ApplyLevelStats();
        }

        /// <summary>Input depth §5: run the offer clock. The sim never pauses
        /// for it — the fight continues while the player decides.</summary>
        private void UpdateGrowthOffer(float deltaTime, in SimInput input)
        {
            if (!_growthOfferOpen)
            {
                return;
            }
            int picked = input.GrowthChoice;
            if (picked >= 1 && picked <= 3)
            {
                ApplyGrowthChoice((GrowthChoiceKind)picked);
                return;
            }
            _growthOfferTime -= deltaTime;
            if (_growthOfferTime <= 0f)
            {
                ApplyGrowthChoice(GrowthChoiceKind.None);
            }
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
        /// §4 + AMENDMENT #6 (D6.3/D6.4): each active companion slot trails the player
        /// by 80 px (plus its D6.4 lateral fan-out) and, on its own per-archetype cadence,
        /// hits the nearest enemy inside its range for a per-archetype share of the player's
        /// damage. Slot 0 uses fan-out 0 and — for a legacy ember-cohort/fallback run — the
        /// frozen §4 tuple, so a zero/single-companion run stays digest-identical. Companions
        /// cannot be targeted, so they have no health and never appear in the enemy contact loop.
        /// The shared <see cref="_companionBehavior"/> makes global hold/recall drive every slot.
        /// </summary>
        private void UpdateCompanion(float deltaTime, bool skillQueued)
        {
            for (int slot = 0; slot < _companionCount; slot += 1)
            {
                UpdateCompanionSlot(slot, deltaTime, skillQueued);
            }
        }

        private void UpdateCompanionSlot(int slot, float deltaTime, bool skillQueued)
        {
            // D6.4: lateral fan-out perpendicular to the player's facing. Slot 0 = 0 (frozen §4).
            float fanout = HackSpec.CompanionSlotFanout[slot];

            // AMENDMENT #7: every autonomy radius is measured from the slot's ANCHOR, not from
            // the slot itself, so §4/D6.3 attack geometry is untouched. A held slot is pinned,
            // so its own position IS its anchor — that keeps Amendment #3 hold behavior intact
            // (a held slot never pursues and never loses its swing).
            bool held = _companionBehavior == CompanionBehavior.Hold;
            float anchorX = held ? _companionX[slot] : _player.X - HackSpec.CompanionFollowOffset * _player.Facing;
            float anchorY = held ? _companionY[slot] : _player.Y + fanout;

            // A7.1: keep the locked target or acquire a new one from the anchor.
            int target = ResolveCompanionTarget(slot, anchorX, anchorY, deltaTime);

            bool wasEngaged = _companionEngaged[slot];
            bool engaged = false;
            if (_companionBehavior == CompanionBehavior.Follow)
            {
                float anchorDistance = IsoDistance(_companionX[slot], _companionY[slot], anchorX, anchorY);
                if (anchorDistance > HackSpec.CompanionLeashRadius)
                {
                    // A7.3: the leash is hard. Drop the lock and walk home this tick.
                    ClearCompanionTarget(slot);
                    target = -1;
                }
                else if (target >= 0
                    && _companionReturnGrace[slot] <= 0f
                    && IsoDistance(
                        _companionX[slot], _companionY[slot],
                        _enemies[target].State.X, _enemies[target].State.Y) > _companionAttackRange[slot])
                {
                    // A7.2: the locked target is outside this slot's attack range but still inside
                    // the leash, so close on it instead of trailing the anchor. The return grace
                    // above is hysteresis: a slot that just came home cannot immediately re-engage,
                    // which is what stops acquire/return oscillation at the radius edge.
                    engaged = true;
                }

                if (engaged)
                {
                    StepCompanionToward(
                        slot,
                        _enemies[target].State.X,
                        _enemies[target].State.Y,
                        _playerSpeed * HackSpec.CompanionPursuitSpeedScale,
                        deltaTime);
                }
                else
                {
                    // Frozen §4 follower step. With no target inside the acquire radius this is
                    // the pre-amendment path, arithmetic included.
                    StepCompanionToward(slot, anchorX, anchorY, _playerSpeed, deltaTime);
                }
            }

            // A7.3: an engagement that just ended opens the return grace; otherwise it decays.
            if (!engaged)
            {
                _companionReturnGrace[slot] = wasEngaged
                    ? HackSpec.CompanionReturnGraceSeconds
                    : MathF.Max(0f, _companionReturnGrace[slot] - deltaTime);
            }

            _companionEngaged[slot] = engaged;

            // AMENDMENT #8 (A8.4): the signature skill resolves AFTER this tick's movement and
            // BEFORE the §4 swing. Both orderings matter and both are gated by tests: moving
            // first means the skill fires from where the companion actually is (same geometry
            // the swing uses), and firing before the swing means the cadence timer below can
            // never swallow a cast that was legally ready this tick.
            UpdateCompanionSkill(slot, deltaTime, skillQueued);

            _companionShow[slot] = MathF.Max(0f, _companionShow[slot] - deltaTime);
            _companionTimer[slot] = MathF.Max(0f, _companionTimer[slot] - deltaTime);
            if (_companionTimer[slot] > 0f)
            {
                if (_companionShow[slot] <= 0f)
                {
                    _companionFacing[slot] = _player.Facing;
                }
                return;
            }

            // A7.4: the swing itself is unchanged §4/D6.3 geometry — per-archetype range from the
            // slot's OWN position. The locked target is preferred when it is in range; otherwise
            // the frozen nearest-in-range rule applies, so a lock can never cost the slot a swing.
            if (target >= 0 && IsoDistance(
                    _companionX[slot], _companionY[slot],
                    _enemies[target].State.X, _enemies[target].State.Y) > _companionAttackRange[slot])
            {
                target = -1;
            }
            if (target < 0)
            {
                target = NearestEnemyIndex(_companionX[slot], _companionY[slot], _companionAttackRange[slot]);
            }
            if (target < 0)
            {
                _companionFacing[slot] = _player.Facing;
                return;
            }

            float targetDeltaX = _enemies[target].State.X - _companionX[slot];
            if (MathF.Abs(targetDeltaX) > MoveEpsilon)
            {
                _companionFacing[slot] = targetDeltaX > 0f ? 1 : -1;
            }

            _companionTimer[slot] = _companionAttackInterval[slot];
            _companionShow[slot] = HackSpec.CompanionAttackDisplay;
            DamageEnemy(ref _enemies[target], _playerDamage * _companionDamageScale[slot]);
            if (_enemies[target].State.Dead && _enemies[target].State.Id == _companionTargetId[slot])
            {
                // A7.1: the slot finished its own target — release the lock in the same tick so the
                // snapshot never publishes a lock on a corpse.
                ClearCompanionTarget(slot);
            }
        }

        /// <summary>
        /// A7.1: hold the locked target while it lives, stays inside the leash and the lock has
        /// not expired; otherwise acquire the nearest living enemy inside
        /// <see cref="HackSpec.CompanionAcquireRadius"/> of the anchor. Returns the enemy INDEX
        /// for this tick (indices shift on removal, which is why the lock itself stores the id).
        /// </summary>
        private int ResolveCompanionTarget(int slot, float anchorX, float anchorY, float deltaTime)
        {
            _companionLockTimer[slot] = MathF.Max(0f, _companionLockTimer[slot] - deltaTime);

            int index = -1;
            bool released = false;
            if (_companionTargetId[slot] != 0)
            {
                index = EnemyIndexById(_companionTargetId[slot]);
                bool valid = index >= 0
                    && !_enemies[index].State.Dead
                    && _companionLockTimer[slot] > 0f
                    && IsoDistance(_enemies[index].State.X, _enemies[index].State.Y, anchorX, anchorY)
                        <= HackSpec.CompanionLeashRadius;
                if (!valid)
                {
                    ClearCompanionTarget(slot);
                    index = -1;
                    released = true;
                }
            }

            // A release costs one tick before the next acquisition. That single tick is what makes
            // every lock transition visible on the snapshot (id -> 0 -> id) instead of an invisible
            // same-tick refresh, and it lets an expired lock be re-contested fairly.
            if (index < 0 && !released)
            {
                int acquired = NearestEnemyIndex(anchorX, anchorY, HackSpec.CompanionAcquireRadius);
                if (acquired >= 0)
                {
                    _companionTargetId[slot] = _enemies[acquired].State.Id;
                    _companionLockTimer[slot] = HackSpec.CompanionTargetLockSeconds;
                    index = acquired;
                }
            }

            return index;
        }

        /// <summary>
        /// AMENDMENT #8 (A8.3): tick the cooldown, then cast when it is ready and enough
        /// enemies stand inside the skill radius. Auto-fire needs the archetype's
        /// <see cref="CompanionSkillSpec.MinAutoTargets"/>; a commanded cast needs only one,
        /// which is the entire difference between the two paths. A slot still on cooldown
        /// ignores the command — it is never buffered, matching the Amendment #3 rule that a
        /// redundant companion command is a no-op.
        /// A HELD slot may cast: Amendment #3 only suspends locomotion, never the slot's
        /// offensive behavior.
        /// </summary>
        private void UpdateCompanionSkill(int slot, float deltaTime, bool skillQueued)
        {
            _companionSkillFlash[slot] = MathF.Max(0f, _companionSkillFlash[slot] - deltaTime);
            _companionSkillCooldown[slot] = MathF.Max(0f, _companionSkillCooldown[slot] - deltaTime);
            if (_companionSkillCooldown[slot] > 0f)
            {
                return;
            }

            CompanionSkillSpec skill = _companionSkill[slot];
            if (skill.Id == CompanionSkillId.None || skill.MaxTargets <= 0)
            {
                return;
            }

            int required = skillQueued ? 1 : skill.MinAutoTargets;
            if (CountLivingEnemiesWithin(
                    _companionX[slot], _companionY[slot], skill.Radius, required) < required)
            {
                return;
            }

            CastCompanionSkill(slot, in skill);
        }

        /// <summary>
        /// A8.2: strike the up-to-MaxTargets nearest living enemies inside the radius,
        /// nearest first, measured from the COMPANION. Selection reuses the frozen
        /// <see cref="NearestEnemyIndex"/> comparison (lowest index wins a tie) with the
        /// already-struck indices excluded, so the hit set is a pure function of geometry.
        /// Damage is NEUTRAL — A8.6 keeps companion skills out of the §2.4 element cycle,
        /// exactly like the companion's ordinary swing.
        /// </summary>
        private void CastCompanionSkill(int slot, in CompanionSkillSpec skill)
        {
            _companionSkillCooldown[slot] = skill.Cooldown;
            _companionSkillFlash[slot] = HackSpec.CompanionSkillFlashSeconds;
            _events |= SimEvents.CompanionSkillCast;

            float damage = _playerDamage * skill.DamageScale;
            float originX = _companionX[slot];
            float originY = _companionY[slot];
            int cap = skill.MaxTargets < HackSpec.CompanionSkillTargetCap
                ? skill.MaxTargets
                : HackSpec.CompanionSkillTargetCap;
            int struck = 0;
            while (struck < cap)
            {
                int index = NearestEnemyIndexExcluding(
                    originX, originY, skill.Radius, _companionSkillHits, struck);
                if (index < 0)
                {
                    break;
                }

                _companionSkillHits[struck] = index;
                struck += 1;
                if (skill.Knockback > 0f)
                {
                    KnockbackFrom(
                        ref _enemies[index], originX, originY,
                        skill.Knockback, HackSpec.ComboKnockbackTime);
                }
                DamageEnemy(ref _enemies[index], damage);
            }
        }

        /// <summary>Living enemies inside the iso radius, counted no further than
        /// <paramref name="cap"/> — the callers only ever ask "are there at least N?".</summary>
        private int CountLivingEnemiesWithin(float x, float y, float radius, int cap)
        {
            int found = 0;
            for (int index = 0; index < _enemyCount && found < cap; index += 1)
            {
                ref Enemy enemy = ref _enemies[index];
                if (enemy.State.Dead)
                {
                    continue;
                }
                float deltaX = enemy.State.X - x;
                float deltaY = (enemy.State.Y - y) * SimConfig.IsoY;
                if (deltaX * deltaX + deltaY * deltaY <= radius * radius)
                {
                    found += 1;
                }
            }
            return found;
        }

        private void ClearCompanionTarget(int slot)
        {
            _companionTargetId[slot] = 0;
            _companionLockTimer[slot] = 0f;
        }

        /// <summary>
        /// The frozen §4 follower step, parameterised by speed: normalize, scale Y by
        /// <see cref="SimConfig.YMoveScale"/>, clamp the overshoot. Called with
        /// <c>_playerSpeed</c> it is bit-identical to the pre-amendment follow path.
        /// </summary>
        private void StepCompanionToward(int slot, float targetX, float targetY, float speed, float deltaTime)
        {
            float deltaX = targetX - _companionX[slot];
            float deltaY = targetY - _companionY[slot];
            float distance = Hypot(deltaX, deltaY);
            if (distance > MoveEpsilon)
            {
                float stepX = deltaX / distance * speed * deltaTime;
                float stepY = deltaY / distance * speed * SimConfig.YMoveScale * deltaTime;
                _companionX[slot] += MathF.Abs(stepX) >= MathF.Abs(deltaX) ? deltaX : stepX;
                _companionY[slot] += MathF.Abs(stepY) >= MathF.Abs(deltaY) ? deltaY : stepY;
            }
        }

        /// <summary>Iso-weighted distance — the §2.3 metric <c>hypot(dx, dy*1.42)</c>.</summary>
        private static float IsoDistance(float fromX, float fromY, float toX, float toY)
        {
            return Hypot(toX - fromX, (toY - fromY) * SimConfig.IsoY);
        }

        /// <summary>Index of the living-or-dying enemy carrying <paramref name="id"/>, or -1.</summary>
        private int EnemyIndexById(int id)
        {
            for (int index = 0; index < _enemyCount; index += 1)
            {
                if (_enemies[index].State.Id == id)
                {
                    return index;
                }
            }
            return -1;
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
        /// AMENDMENT #16 §20: the archetype profile in force, or null when this run
        /// is on the frozen §7 path. Returning null (rather than the None profile)
        /// keeps every call site's frozen branch textually intact, so a reader can
        /// still see what the pre-amendment code did.
        /// </summary>
        private BossArchetypeProfile BossProfileOrNull() =>
            _bossArchetype == BossArchetype.None ? null : BossVarietySpec.For(_bossArchetype);

        /// <summary>
        /// Attack-clip frame this enemy's contact lands on — its telegraph. Only a
        /// boss on an archetype moves off the frozen frame; every ordinary enemy,
        /// elite and ungated boss keeps <see cref="EnemyContactFrame"/>. Clamped
        /// into 1..AttackClipFrames-1 so a table edit can never produce a swing
        /// that lands on tick zero or never lands at all.
        /// </summary>
        private int ContactFrameFor(in Enemy enemy)
        {
            if (!enemy.State.IsBoss)
            {
                return EnemyContactFrame;
            }
            BossArchetypeProfile profile = BossProfileOrNull();
            if (profile == null)
            {
                return EnemyContactFrame;
            }
            int frame = profile.ContactFrame[BossPhaseVectorIndex()];
            if (frame < BossVarietySpec.MinContactFrame)
            {
                return BossVarietySpec.MinContactFrame;
            }
            return frame > AttackClipFrames - 1 ? AttackClipFrames - 1 : frame;
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
            // AMENDMENT #16 §20.4: an archetype brings its own thresholds AND its
            // own phase count, so a Warden latches once and stays in P2 for the
            // rest of the fight. None keeps the frozen 50/20 split.
            bool variety = _bossArchetype != BossArchetype.None;
            int phaseIndex = variety
                ? BossVarietySpec.PhaseIndexFor(_bossArchetype, fraction)
                : HackSpec.BossPhaseIndexFor(fraction);

            if (phaseIndex >= 1 && !_bossPhase2Done)
            {
                _bossPhase2Done = true;
                _events |= SimEvents.BossPhase2;
                // The escorts join the live spawn queue as ordinary enemies.
                // AMENDMENT #16 §20.2: with an archetype the summon is a column in
                // the table (a Tactician calls at BOTH boundaries, a Warden never
                // does), so the monarch-visual clause only governs the frozen path.
                if (variety)
                {
                    _pendingSpawns += BossVarietySpec.For(_bossArchetype).PhaseEscorts[1];
                }
                else if (_enemies[boss].State.Visual == EnemyVisual.BossMonarch)
                {
                    _pendingSpawns += HackSpec.MonarchPhase2Escorts;
                }
            }
            if (phaseIndex >= 2 && !_bossPhase3Done)
            {
                _bossPhase3Done = true;
                _events |= SimEvents.BossPhase2;
                if (variety)
                {
                    _pendingSpawns += BossVarietySpec.For(_bossArchetype).PhaseEscorts[2];
                }
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

            // Motion depth: a launch owns the step the same way a dash does.
            // Steering out of it would erase the hit; the player is airborne,
            // not merely slowed. Attacks stay locked out for the duration,
            // which is what makes a boss slam cost something. Runs BEFORE the
            // input read so a held key cannot fight the launch.
            if (_playerKnockTime > 0f)
            {
                float step = MathF.Min(deltaTime, _playerKnockTime);
                _player.X += _playerKnockX * step;
                _player.Y += _playerKnockY * SimConfig.YMoveScale * step;
                ClampToArena(ref _player.X, ref _player.Y, SimConfig.PlayerMarginClamp);
                _playerKnockTime -= deltaTime;
                _player.Moving = true;
                _player.ActionTime += deltaTime;
                if (_playerKnockTime <= 0f)
                {
                    _playerKnockTime = 0f;
                    SetPlayerAction(ActorAction.Idle, true);
                }
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
                // Input depth §3: a building charge costs mobility, so the
                // heavy is a commitment rather than free damage. Multiplies
                // with the swing penalty — the two never both apply, since a
                // live swing zeroes the charge.
                if (_chargeTime > 0f) attackScale *= HackSpec.ChargeMoveScale;
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

            ApplyCurrents(ref _player.X, ref _player.Y, SimConfig.PlayerMarginClamp, deltaTime, PlayerCurrentPushMult);
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
            ApplyCurrents(ref _player.X, ref _player.Y, SimConfig.PlayerMarginClamp, step, PlayerCurrentPushMult);
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

        /// <summary>Which finisher the third combo hit becomes. Dungeon only —
        /// the arena path always resolves Neutral, preserving its digest.</summary>
        internal enum ComboVariant
        {
            Neutral = 0,   // no direction held: the original finisher
            Launcher = 1,  // toward the facing: knockback x1.6
            Retreat = 2,   // away from the facing: knockback x0.7
            Spin = 3,      // vertical only: knockback x1.0, reach x1.35
        }

        /// <summary>Pure: the direction held at the finisher's first frame maps
        /// to a variant. Compared against facing so "forward" means forward for
        /// a left-facing player too. The deadzone keeps a drifting stick from
        /// flipping the branch.</summary>
        internal static ComboVariant ResolveFinisherVariant(float moveX, float moveY, int facing)
        {
            const float Deadzone = 0.35f;
            bool horizontal = MathF.Abs(moveX) >= Deadzone;
            bool vertical = MathF.Abs(moveY) >= Deadzone;

            if (horizontal)
            {
                // Facing is +1 right / -1 left, so a positive product means the
                // player pushed the way the character already looks.
                return moveX * facing > 0f ? ComboVariant.Launcher : ComboVariant.Retreat;
            }
            return vertical ? ComboVariant.Spin : ComboVariant.Neutral;
        }

        /// <summary>W1: the dungeon Launcher is aimed from a virtual point behind
        /// the player. Other finishers retain the player's position as their
        /// knockback origin, preserving their established behavior.</summary>
        internal static (float X, float Y) FinisherKnockbackOrigin(
            ComboVariant variant, float playerX, float playerY, int facing)
        {
            return variant == ComboVariant.Launcher
                ? (playerX - facing * HackSpec.ComboKnockbackDistance, playerY)
                : (playerX, playerY);
        }



        /// <summary>
        /// §2.1: a three-hit chain. Each hit owns a swing length and an active window;
        /// re-pressing inside the 0.9 s link window advances the chain, otherwise the
        /// next press restarts at hit 1.
        /// </summary>

        private void UpdateCombo(float deltaTime, in SimInput input)
        {
            // Input depth §3: holding the attack key AUTO-REPEATS the chain
            // (InputAdapter L60 latches on isPressed, deliberately — that is
            // how a held key walks 1->2->3). So a charge can only begin once
            // the chain has actually COMPLETED: _comboIndex wraps to 0 while
            // the link window is still open. Without this gate the sim is
            // never idle under a held key and the charge could never accrue.
            bool chainSpent = _dungeon && input.AttackHeld
                && ((_comboIndex == 0 && _comboLink > 0f)
                    // Once a charge exists, keep the gate shut so the link
                    // window expiring cannot restart the chain and eat it.
                    // Otherwise the usable release window would be only the
                    // 0.45 s between arming and the 0.9 s chain restart.
                    || _chargeTime > 0f);
            if (input.AttackQueued && _comboSwing < 0 && !chainSpent)
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
                // Input depth §2: the FINISHER branches on the direction held
                // the moment it starts. Latched here, not read per-enemy, so
                // every enemy in one swing takes the same variant even if the
                // stick moves mid-swing. Needs no SimInput field: MoveX/MoveY
                // already arrive on the same tick as AttackQueued.
                _comboVariant = _dungeon && hit == HackSpec.ComboLength - 1
                    ? ResolveFinisherVariant(input.MoveX, input.MoveY, _player.Facing)
                    : ComboVariant.Neutral;
                // Motion depth: the authored `critical` clip (Illegal Elbow
                // Punch) shipped dead. The Launcher — the committed forward
                // finisher — is the one swing that deserves its own pose, so
                // the strongest choice in the combo finally LOOKS different.
                SetPlayerAction(_comboVariant == ComboVariant.Launcher
                    ? ActorAction.Critical
                    : ActorAction.Attack, true);
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
                // Input depth §3: charge builds while the key STAYS down after
                // a swing has finished. The press itself already swung
                // instantly (above), so mashing is unchanged and holding costs
                // no latency — the two readings never compete for the same
                // press. Released early, the charge is simply discarded.
                if (_dungeon && input.AttackHeld)
                {
                    _chargeTime += deltaTime;
                }
                else
                {
                    if (_chargeTime >= HackSpec.ChargeReadySeconds)
                    {
                        ReleaseCharge();
                    }
                    _chargeTime = 0f;
                }
                SetPlayerAction(_player.Moving ? ActorAction.Move : ActorAction.Idle, false);
                _player.ActionTime += deltaTime;
                return;
            }

            // A live swing cancels any charge — the two cannot overlap.
            _chargeTime = 0f;

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

        /// <summary>Input depth §3: the charged heavy. Reuses the finisher's
        /// reach and arc so it never becomes a second, different weapon —
        /// only the numbers change. Damage x1.8, knockback x2.0.</summary>
        private void ReleaseCharge()
        {
            _player.AttackId += 1;
            _player.AttackCooldown = HackSpec.ComboSwing[HackSpec.ComboLength - 1];
            _comboSwing = -1;
            _comboIndex = 0;
            _comboLink = 0f;
            _comboVariant = ComboVariant.Neutral;
            SetPlayerAction(ActorAction.Critical, true);
            _events |= SimEvents.PlayerStruck;

            // A9.4: the multiplier is sampled ONCE, before any of this swing's hits
            // feed the gauge, so a swing can never buff its own later targets.
            float damage = _playerDamage * HackSpec.ChargeDamageMul * MomentumDamageMultiplier;

            bool landed = false;
            for (int index = 0; index < _enemyCount; index += 1)
            {
                ref Enemy enemy = ref _enemies[index];
                if (enemy.State.Dead || enemy.LastHitAttack == _player.AttackId)
                {
                    continue;
                }
                float deltaX = enemy.State.X - _player.X;
                float deltaY = (enemy.State.Y - _player.Y) * SimConfig.IsoY;
                if (deltaX * _player.Facing < SimConfig.FacingArcTolerance
                    || deltaX * deltaX + deltaY * deltaY
                       > SimConfig.PlayerAttackRange * SimConfig.PlayerAttackRange)
                {
                    continue;
                }
                enemy.LastHitAttack = _player.AttackId;
                landed = true;
                Knockback(ref enemy,
                    HackSpec.ComboKnockbackDistance * HackSpec.ChargeKnockbackMul,
                    HackSpec.ComboKnockbackTime);
                DamageEnemy(ref enemy, damage);
                GainMomentum(enemy.State.Dead);

            }
            if (landed)
            {
                _events |= SimEvents.ComboFinisher;
            }
        }

        /// <summary>
        /// One combo hit: arena range/arc/one-hit-per-attackId rules, hit damage scaled
        /// off the 58 base, and the finisher's 120 px knockback.
        /// </summary>
        private void SwingCombo(int index)
        {
            bool finisher = index == HackSpec.ComboLength - 1;
            // A9.4: sampled once per swing, before this swing's own hits feed the gauge.
            float damage = _playerDamage * HackSpec.ComboDamageScale[index] * MomentumDamageMultiplier;

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
                // Input depth §2: Spin sweeps wider instead of hitting harder,
                // so a vertical finisher answers "I am surrounded" rather than
                // "this one enemy must die".
                float reach = SimConfig.PlayerAttackRange
                    * (finisher && _comboVariant == ComboVariant.Spin ? HackSpec.SpinReachMul : 1f);
                if (!inFacingArc
                    || deltaX * deltaX + deltaY * deltaY > reach * reach)
                {
                    continue;
                }

                enemy.LastHitAttack = _player.AttackId;
                landed = true;
                if (finisher)
                {
                    var origin = FinisherKnockbackOrigin(
                        _comboVariant, _player.X, _player.Y, _player.Facing);
                    KnockbackFrom(ref enemy, origin.X, origin.Y,
                        HackSpec.ComboKnockbackDistance * HackSpec.FinisherKnockbackMul[(int)_comboVariant],
                        HackSpec.ComboKnockbackTime);
                }
                DamageEnemy(ref enemy, damage);
                GainMomentum(enemy.State.Dead);

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

            // Input depth §2: Retreat actually retreats — the player slides
            // backward as the swing lands. Without this the "escape" finisher
            // only weakens the knockback, which reads as a worse Neutral rather
            // than a different tool. Gated on `landed` so a whiffed swing is
            // not a free repositioning tool.
            if (landed && finisher && _comboVariant == ComboVariant.Retreat)
            {
                _player.X -= _player.Facing * HackSpec.RetreatStepDistance;
                ClampToArena(ref _player.X, ref _player.Y, SimConfig.PlayerMarginClamp);
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

            // AMENDMENT #11 §16 A: the difficulty tier scales incoming damage BEFORE
            // Ward and the shield, so an absorbed hit absorbs the tier-scaled number.
            // Normal resolves to 1.0, so the frozen arena/dungeon numbers are untouched.
            if (_difficulty.IncomingDamageMul != 1f)
            {
                amount *= _difficulty.IncomingDamageMul;
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

            // AMENDMENT #13 §17.4: the DDA counts hits that actually cost health —
            // the same "real hit" definition 부록 B pins for the channel reset, so a
            // fully-absorbed shield hit does not push the band down.
            _waveHitsTaken += 1;

            // A9.3: the gauge measures an unbroken offensive, so being hit costs a
            // flat slice of it and ends the grace window immediately.
            SpendMomentumOnHurt();


            // Motion depth: the authored `hit` clip (Standing React Small From
            // Left) shipped dead — damage only ever produced a colour flash.
            // A real hit that is not a kill now poses the recoil. Dungeon-gated
            // and never overrides a dash: an i-framed roll must keep reading as
            // a roll. The pose is not forced, so an in-progress swing wins —
            // trading a hit for a hit stays a deliberate choice.
            if (_dungeon && _player.Health > 0f && _dashTime <= 0f)
            {
                SetPlayerAction(ActorAction.Hit, false);
            }

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
            // AMENDMENT #11 §16 C/E: decide WHO is allowed to swing this tick before any
            // enemy moves. Doing it as a pre-pass is what makes the choice independent of
            // array order — the token goes to the best candidate, not to whichever enemy
            // happens to sit at a low index.
            PlanEnemyGroup();


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

        /// <summary>
        /// AMENDMENT #11 §16 C/E — the cooperative half of the group AI.
        /// <para>
        /// Runs once per tick, before any enemy moves, and decides which enemies are
        /// cleared to start a swing. A tier with <see cref="DifficultyProfile.AttackTokens"/>
        /// == 0 is unlimited and short-circuits, which is the pre-amendment rule: every
        /// enemy in range swings whenever its own cooldown allows.
        /// </para>
        /// <para>
        /// Determinism: the candidate scan is a fixed forward pass over the enemy array
        /// with a strict &lt; comparison and an id tie-break, and it uses no RNG. Same
        /// state in, same grants out — the reason it is a pre-pass at all is that granting
        /// inline would silently hand the token to the lowest array index.
        /// </para>
        /// </summary>
        private void PlanEnemyGroup()
        {
            if (_difficulty.AttackTokens <= 0)
            {
                return;   // unlimited — MayAttackThisTick answers true without the array
            }

            if (_mayAttack.Length < _enemies.Length)
            {
                _mayAttack = new bool[_enemies.Length];
            }

            // Pass 1: clear the plan and count the tokens already held by live swings.
            // A boss is the fight, not a member of the pack, so it is never gated and
            // never consumes a pack token.
            int free = _difficulty.AttackTokens;
            for (int index = 0; index < _enemyCount; index += 1)
            {
                ref Enemy enemy = ref _enemies[index];
                bool boss = !enemy.State.Dead && enemy.State.IsBoss;
                _mayAttack[index] = boss;
                if (!boss && !enemy.State.Dead && enemy.State.Action == ActorAction.Attack)
                {
                    free -= 1;
                }
            }

            // Pass 2: hand each remaining token to the best candidate. "Best" is the
            // nearest enemy that is off cooldown, except that an enemy which is NOT in
            // front of the player scores as if it were FlankBias times closer — that is
            // what makes the opening hit come from the side or the back instead of the
            // pack politely queueing in the player's face.
            //
            // Candidacy deliberately does NOT require being inside attack range. It used
            // to, and that deadlocked the whole tier: a token is what lets an enemy walk
            // at the player at all, so gating the token on already being in range meant
            // the pack orbited its holding ring (which sits OUTSIDE attack range) and
            // nobody ever swung. The token is permission to commit, not permission to
            // land. Cooldown still gates it, which is what produces the rotation — an
            // enemy that just swung drops its token and falls back to the ring to
            // recover while a fresh one steps in.
            for (int grant = 0; grant < free; grant += 1)
            {
                int best = -1;
                float bestScore = float.MaxValue;
                for (int index = 0; index < _enemyCount; index += 1)
                {
                    ref Enemy enemy = ref _enemies[index];
                    if (enemy.State.Dead
                        || _mayAttack[index]
                        || enemy.State.Action == ActorAction.Attack
                        || enemy.AttackCooldown > 0f)
                    {
                        continue;
                    }

                    float toPlayerX = _player.X - enemy.State.X;
                    float toPlayerY = (_player.Y - enemy.State.Y) * SimConfig.IsoY;
                    float range = Hypot(toPlayerX, toPlayerY);



                    bool inFront = (enemy.State.X - _player.X) * _player.Facing
                        >= DifficultySpec.ForwardThreshold;
                    float score = inFront ? range : range * _difficulty.FlankBias;
                    if (score < bestScore
                        || (score == bestScore && best >= 0
                            && enemy.State.Id < _enemies[best].State.Id))
                    {
                        bestScore = score;
                        best = index;
                    }
                }

                if (best < 0)
                {
                    break;   // nobody else is in range and off cooldown this tick
                }
                _mayAttack[best] = true;
            }
        }

        /// <summary>
        /// §16 C: did <paramref name="index"/> get a swing token this tick? Always true on
        /// an unlimited tier, which is how Normal keeps the frozen behaviour.
        /// </summary>
        private bool MayAttackThisTick(int index)
        {
            if (_difficulty.AttackTokens <= 0)
            {
                return true;
            }
            return index >= 0 && index < _mayAttack.Length && _mayAttack[index];
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
                // AMENDMENT #11 §16 C: on a group-AI tier the swing also needs a token
                // that PlanEnemyGroup granted this tick. On Normal/Story the plan grants
                // every enemy, so this reads exactly like the pre-amendment condition.
                if (distance <= SimConfig.EnemyAttackRange
                    && enemy.AttackCooldown <= 0f
                    && MayAttackThisTick(index))
                {
                    enemy.DidDamage = false;
                    enemy.AttackCooldown = (SimConfig.EnemyAttackCooldown
                        + MathF.Min(EnemyCooldownWaveCap, _wave * EnemyCooldownPerWave))
                        * _difficulty.AttackCooldownMul;   // §16 B, 1.0 on Normal
                    if (enemy.State.IsBoss)
                    {
                        // AMENDMENT #16 §20.2: the cadence axis. Below 1 = swings
                        // more often. The None profile is 1.00 across all phases,
                        // so an ungated boss keeps the pack cooldown exactly.
                        BossArchetypeProfile profile = BossProfileOrNull();
                        if (profile != null)
                        {
                            enemy.AttackCooldown *= profile.CadenceMul[BossPhaseVectorIndex()];
                        }
                    }
                    SetEnemyAction(ref enemy, ActorAction.Attack, true);
                }
                else
                {
                    // AMENDMENT #11 §16 D: an enemy that is NOT cleared to swing walks to
                    // its own slot on the holding ring around the player instead of
                    // shoving into the pile. That is the whole "the pack surrounds you and
                    // takes turns" read. Neutral tiers keep chasing the player directly.
                    float goalX = _player.X;
                    float goalY = _player.Y;
                    bool holding = false;
                    if (_difficulty.GroupAi && !MayAttackThisTick(index))
                    {
                        DifficultySpec.RingTarget(
                            enemy.State.Id,
                            _player.X,
                            _player.Y,
                            SimConfig.EnemyAttackRange * _difficulty.RingRadiusMul,
                            out goalX,
                            out goalY);
                        holding = true;
                    }

                    float moveX = goalX - enemy.State.X;
                    float moveY = goalY - enemy.State.Y;

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
                    // §16 D: a ring holder parks on ITS slot, so the arrival test is the
                    // distance to that slot. Everyone else keeps the frozen rule — stop
                    // just inside attack range of the player.
                    bool advance = holding
                        ? rawDistance > DifficultySpec.RingArriveTolerance
                        : distance > SimConfig.EnemyAttackRange - EnemyChaseSlack;
                    if (advance)
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

            ApplyCurrents(ref enemy.State.X, ref enemy.State.Y, SimConfig.EnemyMarginClamp, deltaTime, _sigilCurrentEnemyPushMult);
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

            // AMENDMENT #16 §20.2: the telegraph axis. An archetype boss lands its
            // contact on its OWN clip frame — a Warden waits 3 frames (0.25 s), a
            // Tactician 1 (0.083 s). Everyone else keeps the frozen frame 2.
            if (frame < ContactFrameFor(in enemy) || enemy.DidDamage)
            {
                return;
            }

            BossArchetypeProfile bossProfile = enemy.State.IsBoss ? BossProfileOrNull() : null;
            float contactX = _player.X - enemy.State.X;
            float contactY = (_player.Y - enemy.State.Y) * SimConfig.IsoY;
            float contactRange = SimConfig.EnemyAttackRange + SimConfig.EnemyContactBonus;
            if (enemy.State.IsBoss)
            {
                // S8-a: reach grows with the phase vector (1.00/1.10/1.20).
                // AMENDMENT #16 §20.2: an archetype substitutes its own vector.
                contactRange *= bossProfile != null
                    ? bossProfile.RangeMul[BossPhaseVectorIndex()]
                    : HackSpec.BossRangeMul[BossPhaseVectorIndex()];
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
                if (bossProfile != null)
                {
                    // AMENDMENT #16 §20.2: the archetype's own damage vector, and
                    // it applies from P1 — the None profile's P1 entry is 1.00,
                    // which is exactly the frozen "no multiplier before phase 2".
                    damage *= bossProfile.DamageMul[BossPhaseVectorIndex()];
                }
                else if (enemy.State.IsBoss && _bossPhase >= 2)
                {
                    // S8-a: P2 x1.25, P3 x1.45 — the amended curve, not a
                    // single phase-2 step (a P3 boss hitting at the P2 number
                    // would contradict the constants this amendment ships).
                    damage *= _bossPhase >= 3
                        ? HackSpec.BossPhase3DamageMul
                        : HackSpec.BossPhase2DamageMul;
                }
                float healthBefore = _player.Health;
                DamagePlayer(damage);
                // Motion depth: a phase-3 boss slam LAUNCHES the player. Gated
                // on health actually dropping, so a dash i-frame, a ward, or a
                // fully-absorbed shield hit does not throw the body — the
                // launch is the tell that the defence failed. Phase 3 only:
                // the final phase should feel different in the hands, not just
                // on the damage number.
                if (_dungeon && enemy.State.IsBoss && _bossPhase >= 3
                    && _player.Health < healthBefore && _player.Health > 0f)
                {
                    KnockbackPlayer(enemy.State.X, enemy.State.Y,
                        HackSpec.BossSlamKnockbackDistance, HackSpec.BossSlamKnockbackTime);
                }
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
                // AMENDMENT #16 §20.2: an archetype substitutes its own vector.
                BossArchetypeProfile profile = BossProfileOrNull();
                return speed * SimConfig.BossSpeedMul
                    * (profile != null
                        ? profile.SpeedMul[BossPhaseVectorIndex()]
                        : HackSpec.BossSpeedMul[BossPhaseVectorIndex()]);
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
                Array.Resize(ref _pickupGrades, _pickups.Length);
            }

            // AMENDMENT #14 §18: grade the drop before it is published. Bosses are
            // outside the pity ledger by contract — a guaranteed Epic must not be able
            // to satisfy or reset a counter that measures the grind.
            LootGrade grade = LootGrade.Basic;
            if (_progression.GradedLoot)
            {
                if (isBoss)
                {
                    grade = LootGradeSpec.BossGrade;
                }
                else
                {
                    _dropOrdinal += 1;
                    int roll = LootGradeSpec.Roll(enemyId, _wave, _dropOrdinal);
                    grade = LootGradeSpec.Resolve(roll, _finePity, _epicPity);
                    LootGradeSpec.Advance(grade, ref _finePity, ref _epicPity);
                }
                _lastLootGrade = grade;
            }
            _pickupGrades[_pickupCount] = grade;

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
                    CollectPickup(pickup.Kind, _pickupGrades[index]);
                    RemovePickupAt(index);
                    continue;
                }

                if (pickup.Life <= 0f)
                {
                    RemovePickupAt(index);
                }
            }
        }

        private void CollectPickup(PickupKind kind, LootGrade grade)
        {
            // AMENDMENT #14 §18.3: the grade scales the payload of the kind that
            // already dropped — it never changes WHICH kind dropped, so the frozen
            // id%3 / id%7 routing in SpawnPickup is untouched. With the amendment off
            // the multiplier is exactly 1 and the rank step exactly 1.
            float valueMul = _progression.GradedLoot ? LootGradeSpec.ValueMultiplier(grade) : 1f;
            if (kind == PickupKind.EmberShard)
            {
                _player.Health = MathF.Min(_playerMaxHealth, _player.Health + SimConfig.EmberShardHeal * valueMul);
            }
            else if (kind == PickupKind.OilFlask)
            {
                _charge = MathF.Min(SimConfig.LanternMax, _charge + SimConfig.OilFlaskCharge * valueMul);
            }
            else if (kind == PickupKind.EquipShard)
            {
                // Rank lands on the kill-count slot; it applies to the *next* run start.
                int steps = _progression.GradedLoot ? LootGradeSpec.RankSteps(grade) : 1;
                for (int step = 0; step < steps; step += 1)
                {
                    RaiseRank(_kills % CampaignSpec.EquipSlotCount);
                }
            }
            else
            {
                _relics += 1;
                // +0.5f: round-to-nearest. Bare truncation reads 250 x 2.10f as
                // 524 under Unity's float semantics (2.10f * 250 = 524.999...),
                // drifting from the spec table's 525. Deterministic — same
                // float expression every run, no platform branch.
                _score += _progression.GradedLoot
                    ? (int)(SimConfig.RelicScore * valueMul + 0.5f)
                    : SimConfig.RelicScore;
            }
            _events |= SimEvents.PickupCollected;
        }

        private void RemovePickupAt(int index)
        {
            int tail = _pickupCount - index - 1;
            if (tail > 0)
            {
                Array.Copy(_pickups, index + 1, _pickups, index, tail);
                // AMENDMENT #14: the grade array is index-aligned with _pickups, so it
                // has to survive the same swap-down or grades would drift onto the
                // wrong drop.
                Array.Copy(_pickupGrades, index + 1, _pickupGrades, index, tail);
            }
            _pickupCount -= 1;
            _pickups[_pickupCount] = default;
            _pickupGrades[_pickupCount] = LootGrade.Basic;
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

                // AMENDMENT #13 §17.2: a mob wave is bought from the point budget
                // instead of the fixed 3 + floor(wave*1.2) queue. The boss wave keeps
                // the frozen boss + escort formula — the budget never buys a boss.
                //
                // §17.4 (2026-08-10): the budget also carries the CAMPAIGN stage,
                // because player power compounds across a campaign and a
                // stage-blind budget made the last stage 0.67x the relative
                // difficulty of the first. Arena and prologue are not campaigns
                // and pass 0, which is the pre-existing shape exactly.
                var stageTerm = _campaign ? _config.StageIndex : 0;
                if (_progression.AdaptiveWaves && !_pendingBoss)
                {
                    _waveBudget = WaveBudgetSpec.EffectiveBudget(waveNumber, _ddaBand, stageTerm);
                    _waveEliteAllowance = WaveBudgetSpec.EliteAllowanceForBudget(_waveBudget);
                    _pendingSpawns = Math.Min(
                        SimConfig.EnemyCap, WaveBudgetSpec.SpawnCountForBudget(_waveBudget));
                }
                else if (_progression.AdaptiveWaves)
                {
                    // Boss wave: the budget is still published (the HUD band readout
                    // must not blank out) but it buys nothing.
                    _waveBudget = WaveBudgetSpec.EffectiveBudget(waveNumber, _ddaBand, stageTerm);
                    _waveEliteAllowance = 0;
                }
            }
            // AMENDMENT #13 §17.4: the DDA reads the wave that just ended, so both
            // accumulators are per wave and reset here.
            _waveHitsTaken = 0;
            _waveSeconds = 0f;
            _elitesThisWave = 0;
            _eliteThisWave = false;
            _extractedThisWave = false;
            _spawnIndexInWave = 0;
            _spawnTimer = SimConfig.FirstSpawnDelay;
            _intermission = 0f;
            _mode = SimMode.Running;
            // AMENDMENT #10: the surge cap is per wave, so a new wave re-arms it.
            // Peril's cap is per RUN and deliberately not touched here.
            _surgeUsedThisWave = false;

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

            // AMENDMENT #13 §17.4: wave clock, one of the three DDA signals. It only
            // runs while the wave is live, so the intermission is not charged to it.
            if (_progression.AdaptiveWaves)
            {
                _waveSeconds += deltaTime;
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
                SettleDifficultyBand();
                _intermission = SimConfig.WaveIntermission;
                _mode = SimMode.WaveClear;
            }
        }

        /// <summary>
        /// AMENDMENT #13 §17.4. Reads the wave that just ended and moves the band at
        /// most one step. Deterministic: three threshold comparisons on accumulated
        /// fixed-step state, no RNG, no history beyond the current band.
        /// </summary>
        private void SettleDifficultyBand()
        {
            if (!_progression.AdaptiveWaves)
            {
                return;
            }
            float maxHealth = _playerMaxHealth;
            float fraction = maxHealth > 0f ? _player.Health / maxHealth : 0f;
            _ddaBand = WaveBudgetSpec.NextBand(_ddaBand, fraction, _waveSeconds, _waveHitsTaken);
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
            // AMENDMENT #13 §17.2: with the budget on, the surplus left after paying
            // for bodies buys hit points, so the health term is a multiplier on the
            // same frozen base instead of the fixed per-wave ramp.
            float health = _dungeon
                ? (_progression.AdaptiveWaves
                    ? HackSpec.DungeonEnemyBaseHealth
                        * WaveBudgetSpec.HealthMultiplierForBudget(_waveBudget)
                    : HackSpec.DungeonEnemyBaseHealth
                        + MathF.Min(HackSpec.DungeonEnemyHealthCap, (_wave - 1) * HackSpec.DungeonEnemyHealthPerWave))
                : SimConfig.EnemyBaseHealth
                    + MathF.Min(EnemyHealthWaveCap, (_wave - 1) * EnemyHealthPerWave);
            // §3: every seventh dungeon spawn is an elite, at most one per wave.
            bool elite = false;
            if (_dungeon && !boss)
            {
                _spawnOrdinal += 1;
                bool onModulus = _spawnOrdinal % HackSpec.EliteSpawnModulus == 0;
                // AMENDMENT #13 §17.2: the id%7 cadence is unchanged; the budget only
                // replaces the "at most one per wave" cap with a purchased allowance.
                elite = _progression.AdaptiveWaves
                    ? onModulus && _elitesThisWave < _waveEliteAllowance
                    : onModulus && !_eliteThisWave;
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
                    // AMENDMENT #16 §20.2: the archetype's bulk. A Warden is 1.28x
                    // and a Tactician 0.78x, which is what stops "slow and heavy"
                    // and "fast and fragile" from being the same fight length.
                    BossArchetypeProfile profile = BossProfileOrNull();
                    if (profile != null)
                    {
                        health *= profile.HealthMul;
                    }
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
                _elitesThisWave += 1;
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
            // AMENDMENT #10: both windows and both caps are run-scoped.
            _perilTimer = 0f;
            _perilUsed = 0;
            _perilArmed = true;
            _surgeTimer = 0f;
            _surgeKillMark = 0;
            _surgeUsedThisWave = false;
            _trainingTimer = 0f;
            _trainingHits = 0;
            // AMENDMENT #13/#14: every counter is run-scoped. A restart re-opens at
            // band 0 with an empty pity ledger — neither is banked across runs, which
            // is what makes a run reproducible from (config, input sequence) alone.
            _ddaBand = 0;
            _waveBudget = 0;
            _waveEliteAllowance = 0;
            _waveHitsTaken = 0;
            _waveSeconds = 0f;
            _elitesThisWave = 0;
            _finePity = 0;
            _epicPity = 0;
            _dropOrdinal = 0;
            _lastLootGrade = LootGrade.Basic;

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
                //
                // AMENDMENT #10: a trial rides them too. The point of the training
                // ground is to practise the gimmick at YOUR numbers; a trial run
                // at stock stats would teach the wrong spacing.
                if (_dungeon || _training)
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
            // Input depth §5: banked growth points multiply on TOP of the
            // automatic level curve. At zero points every term is exactly 1 or
            // 0, so the arena and campaign paths keep their frozen values bit
            // for bit — a player who ignores the offer is unaffected.
            _playerDamage = _baseDamage
                * (1f + HackSpec.LevelDamageBonus * levels)
                * (1f + _extractionBonus)
                * (1f + HackSpec.GrowthAttackBonus * _growthAttack);
            _playerMaxHealth = _baseMaxHealth + HackSpec.LevelHealthBonus * levels
                + HackSpec.GrowthVitalityHealth * _growthVitality;
            _lanternRegen = _baseRegen + HackSpec.LevelRegenBonus * levels;
            _playerSpeed = _baseSpeed
                * (1f + HackSpec.GrowthSwiftnessSpeed * _growthSwiftness);
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
            _comboVariant = ComboVariant.Neutral;
            _chargeTime = 0f;
            // A9.3: a restart re-opens at an empty gauge — momentum is never banked
            // across runs (§11 persistence is untouched).
            _momentum = 0f;
            _momentumGrace = 0f;
            _momentumTierSeen = 0;

            _growthOfferOpen = false;
            _growthOfferTime = 0f;
            _lastGrowthChoice = GrowthChoiceKind.None;
            _growthAttack = 0;
            _growthVitality = 0;
            _growthSwiftness = 0;
            _dashCooldown = 0f;
            _dashTime = 0f;
            _dashDirX = 0f;
            _dashDirY = 0f;
            _playerKnockX = 0f;
            _playerKnockY = 0f;
            _playerKnockTime = 0f;
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
            // Initialize all companion slots to defaults.
            for (int i = 0; i < MaxCompanions; i++)
            {
                _companionTimer[i] = _companionAttackInterval[i];
                _companionShow[i] = 0f;
            }
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

        /// <summary>Park each active companion at its follow offset + D6.4 lateral fan-out (§4).
        /// No-op when no slots are active. Slot 0 uses fan-out 0, reproducing the frozen §4 follower.</summary>
        private void ResetCompanion()
        {
            for (int slot = 0; slot < _companionCount; slot += 1)
            {
                _companionX[slot] = _player.X - HackSpec.CompanionFollowOffset * _player.Facing;
                _companionY[slot] = _player.Y + HackSpec.CompanionSlotFanout[slot];
                _companionFacing[slot] = _player.Facing;
                // AMENDMENT #7: a restart drops every lock and every pursuit, so a fresh run
                // always starts in Follow at the anchor (test RestartResetsBehaviorAndTarget).
                _companionTargetId[slot] = 0;
                _companionLockTimer[slot] = 0f;
                _companionReturnGrace[slot] = 0f;
                _companionEngaged[slot] = false;
                // AMENDMENT #8 (A8.3): the cooldown starts FULL, so neither a fresh run nor a
                // restart can open with a free cast. The first cast is therefore at a time the
                // table alone predicts.
                _companionSkillCooldown[slot] = _companionSkill[slot].Cooldown;
                _companionSkillFlash[slot] = 0f;
            }
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
        /// Deterministic surge windows (AMENDMENT #10 • design/training-and-surge-spec.md).
        ///
        /// Two doors, both opened by state the sim already keeps, neither by a
        /// clock and neither by chance. The survey found the genre builds surges
        /// out of clocks (3/13) and out of nothing else, because an RNG run
        /// cannot reproduce a health threshold or a kill count well enough to
        /// learn. Ours reproduces both exactly.
        ///
        /// Peril: the FIRST tick health crosses below 35%. The crossing test is
        /// edge-based on the previous tick's value, so a single huge hit that
        /// skips the threshold still opens exactly one window (the Elden Ring
        /// skip-the-phase failure the QA plan calls out as T2.4). Re-arms only
        /// after health climbs back past 50% — hysteresis, so hovering at the
        /// line cannot chain windows. Capped at 2 per run.
        ///
        /// Surge: every 12th cumulative kill, at most once per wave.
        /// </summary>
        private void UpdateSurge(float deltaTime)
        {
            if (_perilTimer > 0f)
            {
                _perilTimer = MathF.Max(0f, _perilTimer - deltaTime);
            }
            if (_surgeTimer > 0f)
            {
                _surgeTimer = MathF.Max(0f, _surgeTimer - deltaTime);
            }

            float maxHealth = _playerMaxHealth <= 0f ? SimConfig.PlayerMaxHealth : _playerMaxHealth;
            float fraction = _player.Health / maxHealth;

            // Re-arm first: a heal past 50% this tick may legitimately be followed
            // by a drop below 35% on a later tick, never on this one.
            if (!_perilArmed && fraction >= HackSpec.PerilRearmFraction)
            {
                _perilArmed = true;
            }
            else if (_perilArmed
                     && fraction < HackSpec.PerilHealthFraction
                     && _player.Health > 0f
                     && _perilUsed < HackSpec.PerilRunCap)
            {
                _perilArmed = false;
                _perilUsed += 1;
                _perilTimer = HackSpec.PerilSeconds;
                _events |= SimEvents.PerilOpened;
            }
            else if (_perilArmed && fraction < HackSpec.PerilHealthFraction)
            {
                // Cap reached (or dead on this tick): consume the arm anyway so the
                // run cannot bank a window by bobbing across the line.
                _perilArmed = false;
            }

            // CROSSING, not an exact multiple. A nova can kill three enemies on
            // one tick, so the count steps 11 -> 14 and a `% 12 == 0` test never
            // sees the boundary — a measured run reached 14 kills and opened zero
            // windows. This is the same skip-the-threshold failure the QA plan
            // raised for peril (T2.4); the guard belongs on BOTH doors.
            if (!_surgeUsedThisWave && _kills >= _surgeKillMark + HackSpec.SurgeKillInterval)
            {
                // Snap to the highest boundary already passed so one huge tick
                // still opens exactly one window, never a backlog of them.
                _surgeKillMark = _kills - (_kills % HackSpec.SurgeKillInterval);
                _surgeUsedThisWave = true;
                _surgeTimer = HackSpec.SurgeSeconds;
                _events |= SimEvents.SurgeOpened;
            }
        }

        /// <summary>Hazard clock scale for this tick.
        ///
        /// Peril does NOT appear here. The first implementation slowed the clock
        /// as a base effect of the window and a probe caught what that costs: a
        /// plain unequipped ash-march run opened a peril window, the clock
        /// slowed, and all 15 golden digests would have moved. The window is
        /// therefore SIM STATE ONLY — every mechanical consequence is owned by an
        /// equipped sigil clause, which is also what §4 of the spec promised and
        /// what negotiation entry 9 signed ("돌발 자체는 상태 변화만").
        /// </summary>
        private float HazardRate => _trainingRate;

        /// <summary>Hazard damage multiplier against ENEMIES for this tick. Gated
        /// on 점화인: an unequipped run keeps every constant, so the goldens hold.</summary>
        private float SurgeEnemyMult
            => _surgeTimer > 0f && _sigilSurgeEnemyBoost ? _sigilSurgeEnemyHazardMult : 1f;

        /// <summary>
        /// A trial is a fixed 60 s window with no spawn table (AMENDMENT #10).
        /// It ends by the clock, never by a wave count, and it never spawns an
        /// enemy — so no kill can drop a relic here and the training ground
        /// cannot feed the economy (negotiation entry 7).
        /// </summary>
        private void UpdateTraining(float deltaTime)
        {
            _trainingTimer += deltaTime;
            if (_trainingTimer < HackSpec.TrainingSeconds)
            {
                return;
            }

            ClearRun(HackSpec.TrainingClearReason);
        }

        /// <summary>
        /// Current push on the PLAYER for this tick. 역류인's peril clause pins it
        /// to zero for the window.
        ///
        /// Cleared by the director's arithmetic unchanged (negotiation entry 8):
        /// the current deals no direct damage, so suppressing it avoids none and
        /// cannot register on the comeback band. It buys three seconds of
        /// footing, not three seconds of safety.
        /// </summary>
        private float PlayerCurrentPushMult
            => _perilTimer > 0f && _sigilPerilCurrentSuppress ? 0f : _sigilCurrentPlayerPushMult;

        /// <summary>
        /// Ember vents pulse on the cycle boundary and relic altars count dwell time.
        /// Amendment #5: tide-currents raise their activation cue, ash-walls raise a
        /// telegraph cue and apply band damage on the global 0.6 s tick grid. All run
        /// on stage time, so they keep ticking through the wave intermission.
        ///
        /// AMENDMENT #10: <see cref="HazardRate"/> scales how fast stage time
        /// accumulates — peril halves it, a trial tier tightens it. Everything
        /// downstream reads <see cref="_stageTime"/>, so ONE multiply moves every
        /// gimmick together and the monotonic cycle guards stay valid: time still
        /// only ever moves forward. That is the whole reason this is a rate and
        /// not the phase shift the first draft proposed — a phase shift can send
        /// a cycle counter backwards, and the guard would then skip forever.
        /// </summary>
        private void UpdateHazards(float deltaTime)
        {
            _stageTime += deltaTime * HazardRate;

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
                        // Vents are player risk by default (SIM_SPEC_CAMPAIGN).
                        // 점화인 A does NOT soften the hit — it pays oil for taking
                        // it. Buying resource off pain, never safety (AMENDMENT #6).
                        float healthBefore = _player.Health;
                        DamagePlayer(CampaignSpec.VentDamage);
                        if (_sigilVentOilRefund > 0f && _player.Health < healthBefore)
                        {
                            _charge = MathF.Min(SimConfig.LanternMax, _charge + _sigilVentOilRefund);
                        }
                        if (_training)
                        {
                            _trainingHits += 1;
                        }
                    }
                    // 점화인 B opts vents INTO the symmetric doctrine the current and
                    // the wall already follow — but only while equipped, so the
                    // default asymmetry (and every golden row) is untouched.
                    if (_sigilVentEnemyDamage > 0f)
                    {
                        for (int enemyIndex = 0; enemyIndex < _enemyCount; enemyIndex += 1)
                        {
                            ref Enemy ventEnemy = ref _enemies[enemyIndex];
                            if (ventEnemy.State.Dead
                                || !IsoWithin(hazard.X, hazard.Y, ventEnemy.State.X, ventEnemy.State.Y, hazard.Radius))
                            {
                                continue;
                            }
                            DamageEnemy(ref ventEnemy, _sigilVentEnemyDamage * SurgeEnemyMult);
                        }
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
                    // Edge encoding (spec v1.1): PushX +1 = left wall, -1 = right wall.
                    bool fromRight = hazard.PushX < 0f;
                    if (WallCovers(fromRight, depth, _player.X))
                    {
                        // 집행인 A: 10 -> 6. Still a tick you must walk out of —
                        // the wall keeps owning the space (AMENDMENT #6).
                        //
                        // AMENDMENT #10 peril clause: HALVED for the window, not
                        // waived. The draft waived it and the director's
                        // arithmetic killed that: 6 s of exemption avoids 100
                        // damage, 100% of base HP — reversal grade. Halved for
                        // 3 s avoids 25, and the wall still hurts every tick, so
                        // survey rule 1 ("does the hazard still change your
                        // behaviour") is answered yes by the number itself.
                        float playerTick = _perilTimer > 0f && _sigilPerilWallHalf
                            ? _sigilWallPlayerTick * 0.5f
                            : _sigilWallPlayerTick;
                        DamagePlayer(playerTick);
                        if (_training)
                        {
                            _trainingHits += 1;
                        }
                    }
                    for (int enemyIndex = 0; enemyIndex < _enemyCount; enemyIndex += 1)
                    {
                        ref Enemy enemy = ref _enemies[enemyIndex];
                        if (!enemy.State.Dead && WallCovers(fromRight, depth, enemy.State.X))
                        {
                            // 집행인 B: 10 -> 18 on the enemy side. Herding into the
                            // wall was always legal; this makes it a build.
                            DamageEnemy(ref enemy, _sigilWallEnemyTick * SurgeEnemyMult);
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
                // 증언인 A shortens the channel (1.2 -> 0.8): still a window the
                // gimmick rhythm has to allow, just a narrower one. AMENDMENT #6.
                //
                // AMENDMENT #10 peril clause: the channel completes instantly.
                // Cleared by the director's arithmetic unchanged — the altar
                // grants OIL, so this avoids no damage and never touches the
                // comeback band. What it buys is a resource you still have to
                // spend correctly.
                float holdNeeded = _perilTimer > 0f && _sigilPerilAltarInstant
                    ? 0f
                    : _sigilAltarHoldSeconds;
                if (runtime.Hold < holdNeeded)
                {
                    continue;
                }

                runtime.Hold = 0f;
                runtime.Cooldown = CampaignSpec.AltarCooldown;
                // 증언인 B pays more for the same window (18 -> 30).
                _charge = MathF.Min(SimConfig.LanternMax, _charge + _sigilAltarOilBurst);
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
        ///
        /// <paramref name="pushMult"/> is the 역류인 seam (AMENDMENT #6): 1 for an
        /// unequipped run, so the arithmetic is bit-identical to before the sigil.
        /// The defensive face HALVES the shove rather than cancelling it — 100 px/s
        /// against a 218 move still displaces you, which is the survey's
        /// no-immunity line.
        /// </summary>
        private void ApplyCurrents(ref float x, ref float y, float clampMargin, float deltaTime,
                                   float pushMult)
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
                x += hazard.PushX * pushMult * deltaTime;
                y += hazard.PushY * pushMult * deltaTime;
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
                // 판결인 B doubles what a swing takes off the body (AMENDMENT #6).
                runtime.Hp = MathF.Max(0f, runtime.Hp - damage * _sigilPylonStrikeMult);
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
        /// Damage-taken multiplier for an enemy inside any live pylon aura;
        /// stacking is non-cumulative (§Gimmick 2). 1 when no pylon applies.
        /// The multiplier is ×0.40 by default and ×0.70 under 판결인 A — the
        /// shield THINS, it never lifts, so the target-priority puzzle survives
        /// the upgrade (AMENDMENT #6, no-immunity rule).
        /// </summary>
        private float PylonAuraMultiplier(float enemyX, float enemyY)
        {
            // AMENDMENT #10 판결인 surge clause: inside a surge window the aura
            // stops entirely. This IS a full lift, and it is allowed where the
            // peril clauses were not, for one reason the arithmetic settles: the
            // aura protects ENEMIES, so lifting it costs the player nothing in
            // safety and cannot register on the comeback band. Survey rule 1
            // guards the player's relationship with a hazard, not the enemy's.
            if (_surgeTimer > 0f && _sigilSurgePylonAuraStop)
            {
                return 1f;
            }

            for (int index = 0; index < _hazards.Length; index += 1)
            {
                HazardConfig hazard = _hazards[index];
                if (hazard.Kind == HazardKind.EmberPylon
                    && _hazardRuntime[index].Hp > 0f
                    && IsoWithin(hazard.X, hazard.Y, enemyX, enemyY, CampaignSpec.PylonAuraRadius))
                {
                    return _sigilPylonAuraMult;
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

        /// <summary>Arena floor clamp. The frozen arena/campaign path uses an
        /// L1 (diamond) norm — the literal iso floor shape. AMENDMENT #4 gives
        /// the DUNGEON path an L2 (ellipse) norm instead: same bounding box,
        /// same drawn floor, but +57% reachable area (280,800 -> 441,080 u2).
        /// The diamond amputates the corners — at 75% toward the top edge it
        /// allows |x| &lt;= 130 of a possible 520 — which is what made the room
        /// feel narrow. The dressing plane (StageCatalog L170-172: x 248..1288,
        /// y 334..874) is EXACTLY the bounding box, so every point the ellipse
        /// newly admits already has floor drawn under it.</summary>
        private void ClampToArena(ref float x, ref float y, float margin)
        {
            // AMENDMENT #15 §19: the half-axes come from the resolved bounds instead of
            // the frozen constants. Outside a dungeon (and inside one without #15) they
            // ARE the frozen constants, so the diamond/arena path is untouched. The
            // margin arithmetic is unchanged — a wider playfield still keeps the same
            // 34 px player / 24 px enemy standoff from the boundary.
            float halfWidth = _boundsHalfWidth - margin;
            float halfHeight = _boundsHalfHeight - margin * 0.5f;
            float localX = x - SimConfig.ArenaX;
            float localY = y - SimConfig.ArenaY;

            float unitX = localX / halfWidth;
            float unitY = localY / halfHeight;
            float normalized = _dungeon
                ? MathF.Sqrt(unitX * unitX + unitY * unitY)   // ellipse
                : MathF.Abs(unitX) + MathF.Abs(unitY);        // diamond (frozen)

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
            _pickupGradeView.Clear();
            for (int index = 0; index < _pickupCount; index += 1)
            {
                _pickupView.Add(_pickups[index]);
                // AMENDMENT #14: index-aligned with _pickupView by construction, which
                // is the contract IDungeonProgressionSnapshot.PickupGrades states.
                _pickupGradeView.Add(_pickupGrades[index]);
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
