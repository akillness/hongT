// FROZEN CONTRACT AMENDMENT #2 — numeric truth: docs/SIM_SPEC_HACKSLASH.md §12.
// Additive only: SIM_SPEC.md (arena) and SIM_SPEC_CAMPAIGN.md (campaign) paths are
// untouched — CinderSim() and CinderSim(in CampaignConfig) keep their behaviour.
// Pure C#. No UnityEngine references allowed in this assembly (asmdef enforces).
using System;
using System.Collections.Generic;

namespace CinderCourt.Sim
{
    /// <summary>
    /// Run flavour (docs/SIM_SPEC_HACKSLASH.md §0). <see cref="Arena"/> is the frozen
    /// infinite run, <see cref="Prologue"/> the 3-wave tutorial, <see cref="Dungeon"/>
    /// the campaign stage plus the hack &amp; slash combat kit.
    /// </summary>
    public enum GameMode { Arena = 0, Prologue = 1, Dungeon = 2, Training = 3 }

    /// <summary>Active dungeon companion locomotion mode. This is the COMMANDED mode and
    /// stays exactly the frozen Amendment #3 pair: the AMENDMENT #7 autonomy state is a
    /// derived per-slot flag (<c>IHackSnapshot.CompanionEngagedAt</c>), deliberately not a
    /// third member here, so a command surface can never be confused with a derived one.</summary>
    public enum CompanionBehavior { Follow = 0, Hold = 1 }

    /// <summary>AMENDMENT #8: the signature skill a companion archetype owns. Exactly one
    /// per archetype, append-only, <see cref="None"/> = the slot has no skill (never
    /// produced by <see cref="HackSpec.CompanionSkill"/>, which always resolves a skill).</summary>
    public enum CompanionSkillId { None = 0, Volley = 1, Hex = 2, Quake = 3, Flare = 4 }

    /// <summary>AMENDMENT #8 (A8.2): one archetype's signature-skill tuple. Immutable value
    /// type so the sim can copy it per slot at construction and never re-resolve it.</summary>
    public readonly struct CompanionSkillSpec
    {
        public readonly CompanionSkillId Id;
        /// <summary>Seconds between casts. Also the value the cooldown starts at, so no slot
        /// can open a run with a free cast.</summary>
        public readonly float Cooldown;
        /// <summary>Iso radius, measured from the COMPANION (not its anchor) — a skill is a
        /// swing, so it uses swing geometry.</summary>
        public readonly float Radius;
        /// <summary>Damage per hit as a multiple of the player's current damage. Neutral:
        /// A8.6 keeps companion skills out of the §2.4 element cycle.</summary>
        public readonly float DamageScale;
        /// <summary>Upper bound on enemies struck by one cast, nearest first.</summary>
        public readonly int MaxTargets;
        /// <summary>Living enemies required inside <see cref="Radius"/> before the skill
        /// AUTO-fires. A commanded cast bypasses this and needs only one.</summary>
        public readonly int MinAutoTargets;
        /// <summary>Push applied away from the companion, 0 for skills that do not shove.</summary>
        public readonly float Knockback;

        public CompanionSkillSpec(
            CompanionSkillId id,
            float cooldown,
            float radius,
            float damageScale,
            int maxTargets,
            int minAutoTargets,
            float knockback)
        {
            Id = id;
            Cooldown = cooldown;
            Radius = radius;
            DamageScale = damageScale;
            MaxTargets = maxTargets;
            MinAutoTargets = minAutoTargets;
            Knockback = knockback;
        }
    }

    /// <summary>
    /// Elemental cycle (docs/SIM_SPEC_HACKSLASH.md §2.4):
    /// <c>ember &gt; frost &gt; veil &gt; void &gt; ember</c>. Values 1..4 are the cycle
    /// order so "beats" is one modular step. Basic attacks and the combo are
    /// <see cref="None"/> (neutral) — only skills roll matchups.
    /// </summary>
    public enum Element { None = 0, Ember = 1, Frost = 2, Veil = 3, Void = 4 }

    /// <summary>Lobby stat allocation (docs/SIM_SPEC_HACKSLASH.md §5). Each caps at 10.</summary>
    public struct MetaStats
    {
        public int Attack;      // +3% damage per point
        public int Vitality;    // +8 max HP per point
        public int Swiftness;   // +2% move speed per point

        public static MetaStats Of(int attack, int vitality, int swiftness) => new MetaStats
        {
            Attack = attack,
            Vitality = vitality,
            Swiftness = swiftness,
        };
    }

    /// <summary>
    /// Equipment ranks T0-T5 (docs/SIM_SPEC_HACKSLASH.md §6). Same three slots and the
    /// same per-rank effects as the campaign amendment; only the ceiling story changed
    /// (relic purchases in the lobby), which is a view/persistence concern.
    /// </summary>
    public struct EquipTiers
    {
        public int Weapon;      // +6% damage per tier
        public int Lantern;     // +8% oil regen per tier
        public int Cloak;       // +8 max HP per tier

        public static EquipTiers Of(int weapon, int lantern, int cloak) => new EquipTiers
        {
            Weapon = weapon,
            Lantern = lantern,
            Cloak = cloak,
        };
    }

    /// <summary>
    /// Sigil identity (AMENDMENT #6, design/sigil-spec.md). Each sigil binds ONE
    /// dungeon gimmick; <see cref="None"/> is the inert default so an unequipped
    /// run is byte-identical to every run before the amendment.
    /// </summary>
    public enum SigilKind
    {
        None = 0,
        Countercurrent = 1,   // 역류인 — tide-current
        Verdict = 2,          // 판결인 — ember-pylon
        Executioner = 3,      // 집행인 — ash-wall
        Ignition = 4,         // 점화인 — ember-vent
        Witness = 5,          // 증언인 — relic-altar
    }

    /// <summary>
    /// Which face of a sigil is turned up. A = defensive (survive the gimmick),
    /// B = offensive (turn the gimmick on the enemy). Sidegrades, never a ladder:
    /// the survey's rule 3 (design/sigil-spec.md §설계 규칙).
    /// </summary>
    public enum SigilFace { A = 0, B = 1 }

    /// <summary>
    /// Two equipped sigils. Five exist, two fit — the choice is the point
    /// (sigil-spec §형태). Defaults to two empty slots, which resolves to every
    /// pre-amendment constant.
    /// </summary>
    public struct SigilLoadout
    {
        public SigilKind Slot0;
        public SigilFace Face0;
        public SigilKind Slot1;
        public SigilFace Face1;

        /// <summary>Slot count — the equip ceiling the lobby enforces.</summary>
        public const int Slots = 2;

        public static SigilLoadout Of(SigilKind slot0, SigilFace face0,
                                      SigilKind slot1, SigilFace face1) => new SigilLoadout
        {
            Slot0 = slot0,
            Face0 = face0,
            Slot1 = slot1,
            Face1 = face1,
        };

        /// <summary>One sigil on one face, in either slot.</summary>
        public static SigilLoadout One(SigilKind kind, SigilFace face)
            => Of(kind, face, SigilKind.None, SigilFace.A);

        /// <summary>True when this exact sigil/face pair is equipped.</summary>
        public bool Has(SigilKind kind, SigilFace face)
            => kind != SigilKind.None
               && ((Slot0 == kind && Face0 == face) || (Slot1 == kind && Face1 == face));

        /// <summary>True when this sigil is equipped on EITHER face. The surge
        /// clause (AMENDMENT #7) is face-independent: it is the sigil waking up,
        /// not the face doing more of what it already does.</summary>
        public bool HasKind(SigilKind kind)
            => kind != SigilKind.None && (Slot0 == kind || Slot1 == kind);

        /// <summary>Slot order for the peril no-stacking rule (slot 0 wins).</summary>
        public SigilKind PerilPriority(SigilKind a, SigilKind b, SigilKind c)
        {
            if (Slot0 == a || Slot0 == b || Slot0 == c) return Slot0;
            if (Slot1 == a || Slot1 == b || Slot1 == c) return Slot1;
            return SigilKind.None;
        }
    }

    /// <summary>
    /// One hack &amp; slash run setup (docs/SIM_SPEC_HACKSLASH.md §12).
    /// <see cref="Mode"/> selects the rule set; <see cref="StageId"/> is dungeon-only.
    /// </summary>
    public struct HackConfig
    {
        public GameMode Mode;
        public string StageId;
        public MetaStats MetaStats;
        public EquipTiers EquipTiers;
        /// <summary>Non-null enables the 1-slot companion (§4).</summary>
        public string CompanionId;
        /// <summary>
        /// AMENDMENT #6 (D6.2): 0..3 active companion ids, slot order preserved.
        /// Append-only next to the frozen <see cref="CompanionId"/>. When null/empty
        /// a non-empty <see cref="CompanionId"/> is promoted to a 1-element list; when
        /// both are set this list wins. Normalize through <see cref="CompanionSlots"/>.
        /// </summary>
        public string[] CompanionIds;
        /// <summary>Dungeon gimmick placement; defaults to the stage table.</summary>
        public HazardConfig[] Hazards;
        /// <summary>
        /// Owned <c>&lt;visual&gt;-echo</c> roster as a bitmask over <see cref="EnemyVisual"/>
        /// (bit i = visual i). §3 needs it to branch the extraction reward between
        /// "new roster entry + 8% damage" and "duplicate → +30 relics". Carried as a
        /// mask rather than a string list so the sim stays allocation free.
        /// </summary>
        public int RosterMask;
        /// <summary>
        /// Selected Ember Rest preparation for this destination room only. Its default
        /// value is <see cref="PreparationOfferKind.None"/> and is inert.
        /// </summary>
        public PreparationOffer PreparationOffer;
        /// <summary>
        /// Equipped sigils (AMENDMENT #6). Default = two empty slots, which
        /// resolves every gimmick constant to its pre-amendment value. Set by the
        /// view after <see cref="TryDungeon"/>, the same seam the verdict pact
        /// uses for <see cref="Hazards"/> — the signature stays put.
        /// </summary>
        public SigilLoadout Sigils;
        /// <summary>
        /// Trial tier for <see cref="GameMode.Training"/> (0..2). Inert elsewhere.
        /// </summary>
        public int TrainingTier;

        /// <summary>The frozen arena run expressed as a hack config (no meta/equipment).</summary>
        public static HackConfig Arena() => new HackConfig
        {
            Mode = GameMode.Arena,
            StageId = string.Empty,
        };

        /// <summary>The 3-wave tutorial (§1). Arena numbers, no kit, no gimmicks.</summary>
        public static HackConfig Prologue() => new HackConfig
        {
            Mode = GameMode.Prologue,
            StageId = HackSpec.PrologueStageId,
        };

        /// <summary>A campaign stage run with the full combat kit (§2-§7).</summary>
        public static bool TryDungeon(
            string stageId,
            MetaStats metaStats,
            EquipTiers equipTiers,
            string companionId,
            int rosterMask,
            out HackConfig config)
        {
            if (!CampaignStages.TryGet(stageId, equipTiers.Weapon, equipTiers.Lantern, equipTiers.Cloak, out var stage))
            {
                config = default;
                return false;
            }

            config = new HackConfig
            {
                Mode = GameMode.Dungeon,
                StageId = stage.StageId,
                MetaStats = metaStats,
                EquipTiers = equipTiers,
                CompanionId = companionId,
                Hazards = stage.Hazards,
                RosterMask = rosterMask,
            };
            return true;
        }

        /// <summary>
        /// AMENDMENT #6 (D6.2): multi-slot dungeon overload. <paramref name="companionIds"/>
        /// carries 0..3 companions in slot order; it is stored verbatim and normalized by
        /// the sim through <see cref="CompanionSlots"/>. The single-id overload above stays
        /// the frozen path, so every existing caller is byte-identical.
        /// </summary>
        public static bool TryDungeon(
            string stageId,
            MetaStats metaStats,
            EquipTiers equipTiers,
            string[] companionIds,
            int rosterMask,
            out HackConfig config)
        {
            if (!CampaignStages.TryGet(stageId, equipTiers.Weapon, equipTiers.Lantern, equipTiers.Cloak, out var stage))
            {
                config = default;
                return false;
            }

            config = new HackConfig
            {
                Mode = GameMode.Dungeon,
                StageId = stage.StageId,
                MetaStats = metaStats,
                EquipTiers = equipTiers,
                CompanionId = companionIds != null && companionIds.Length > 0 ? companionIds[0] : null,
                CompanionIds = companionIds,
                Hazards = stage.Hazards,
                RosterMask = rosterMask,
            };
            return true;
        }

        /// <summary>
        /// A training-ground trial (AMENDMENT #7). One gimmick, one tier, 60 s,
        /// no spawns. Meta stats and equipment ride along so the numbers you
        /// practise are the numbers you fight with; sigils do NOT — the trial is
        /// where you learn the gimmick unaided, and surge never fires here.
        /// </summary>
        public static bool TryTraining(
            string trialId,
            int tier,
            MetaStats metaStats,
            EquipTiers equipTiers,
            out HackConfig config)
        {
            if (!TrainingTrials.TryGet(trialId, out var hazards) || tier < 0 || tier >= HackSpec.TrainingTiers)
            {
                config = default;
                return false;
            }

            config = new HackConfig
            {
                Mode = GameMode.Training,
                StageId = trialId,
                MetaStats = metaStats,
                EquipTiers = equipTiers,
                Hazards = hazards,
                TrainingTier = tier,
            };
            return true;
        }

        /// <summary>
        /// AMENDMENT #6 (D6.2): resolve the frozen <see cref="CompanionId"/> +
        /// <see cref="CompanionIds"/> pair into the active slot list. Rule: if
        /// <see cref="CompanionIds"/> has entries it wins (and <see cref="CompanionId"/>
        /// is ignored), otherwise a non-empty <see cref="CompanionId"/> promotes to a
        /// 1-element list. Null/empty/whitespace ids are dropped, duplicates are removed
        /// keeping first occurrence, and the result is capped at 3 in slot order.
        /// </summary>
        public string[] CompanionSlots() => NormalizeCompanionSlots(CompanionId, CompanionIds);

        internal static string[] NormalizeCompanionSlots(string companionId, string[] companionIds)
        {
            string[] source = companionIds != null && companionIds.Length > 0
                ? companionIds
                : (string.IsNullOrWhiteSpace(companionId) ? System.Array.Empty<string>() : new[] { companionId });
            if (source.Length == 0)
            {
                return System.Array.Empty<string>();
            }

            var slots = new List<string>(3);
            for (int index = 0; index < source.Length && slots.Count < 3; index += 1)
            {
                string id = source[index];
                if (string.IsNullOrWhiteSpace(id) || slots.Contains(id))
                {
                    continue;
                }
                slots.Add(id);
            }
            return slots.Count == 0 ? System.Array.Empty<string>() : slots.ToArray();
        }


        /// <summary>
        /// The campaign config this run rides on. Waves, boss visual and equipment
        /// ranks come straight from the stage table; <see cref="Hazards"/> may override
        /// the table placement. Non-dungeon modes get an inert config.
        /// </summary>
        public CampaignConfig ToCampaignConfig()
        {
            if (Mode != GameMode.Dungeon
                || !CampaignStages.TryGet(StageId, EquipTiers.Weapon, EquipTiers.Lantern, EquipTiers.Cloak, out var stage))
            {
                var inert = default(CampaignConfig);
                inert.StageId = StageId ?? string.Empty;
                inert.StageIndex = 0;
                inert.Waves = Mode == GameMode.Prologue ? HackSpec.PrologueWaves : 0;
                inert.BossVisual = EnemyVisual.BossCommander;
                inert.Hazards = null;
                return inert;
            }

            if (Hazards != null)
            {
                stage.Hazards = Hazards;
            }
            return stage;
        }

        /// <summary>Attack power at level 1: <c>58 × (1+0.03a) × (1+0.06w)</c> (§5, §6).</summary>
        public float PlayerDamage => SimConfig.PlayerDamage
            * (1f + HackSpec.AttackPerPoint * HackSpec.ClampStat(MetaStats.Attack))
            * (1f + CampaignSpec.WeaponDamagePerRank * CampaignSpec.ClampRank(EquipTiers.Weapon));

        /// <summary>Max health at level 1: <c>100 + 8·vitality + 8·cloak</c> (§5, §6).</summary>
        public float PlayerMaxHealth => SimConfig.PlayerMaxHealth
            + HackSpec.VitalityHealthPerPoint * HackSpec.ClampStat(MetaStats.Vitality)
            + CampaignSpec.CloakHealthPerRank * CampaignSpec.ClampRank(EquipTiers.Cloak);

        /// <summary>Move speed: <c>218 × (1+0.02s)</c> (§5).</summary>
        public float PlayerSpeed => SimConfig.PlayerSpeed
            * (1f + HackSpec.SwiftnessSpeedPerPoint * HackSpec.ClampStat(MetaStats.Swiftness));

        /// <summary>Oil regen: <c>7 × (1+0.08l)</c> (§6).</summary>
        public float LanternRegenPerSecond => SimConfig.LanternRegenPerSecond
            * (1f + CampaignSpec.LanternRegenPerRank * CampaignSpec.ClampRank(EquipTiers.Lantern));
    }

    /// <summary>
    /// Read-only hack &amp; slash view. Every campaign/arena member keeps its meaning;
    /// the members below are inert outside <see cref="GameMode.Dungeon"/>.
    /// </summary>
    public interface IHackSnapshot : ICampaignSnapshot
    {
        /// <summary>
        /// §12 calls this <c>Mode</c>; renamed because <see cref="ISimSnapshot.Mode"/>
        /// already owns that name with a different type (<see cref="SimMode"/>).
        /// </summary>
        GameMode HackMode { get; }

        int Level { get; }
        int Xp { get; }
        /// <summary>XP required to reach the next level; 0 at the level cap.</summary>
        int XpNext { get; }
        /// <summary>Index of the combo hit the next Space press starts (0..2).</summary>
        int ComboIndex { get; }
        float DashCooldown { get; }
        /// <summary>Length 4, indexed by <see cref="HackSpec.SkillBolt"/> and friends.</summary>
        IReadOnlyList<float> SkillCooldowns { get; }
        /// <summary>Remaining void-aegis absorption.</summary>
        float Shield { get; }
        int ElitesAlive { get; }
        /// <summary>Seconds of uninterrupted extraction channel banked so far.</summary>
        float ExtractionProgress { get; }
        /// <summary>Seconds required to finish the channel; 0 when nothing is extractable.</summary>
        float ExtractionTarget { get; }
        float CompanionX { get; }
        float CompanionY { get; }
        bool CompanionAttacking { get; }
        CompanionBehavior CompanionBehavior { get; }
        /// <summary>AMENDMENT #6 (D6.5): number of active companion slots, 0..3.
        /// A zero/single-companion run reports 0/1 and its scalar members above
        /// alias slot 0 exactly.</summary>
        int CompanionCount { get; }
        /// <summary>D6.5: slot i follower x. Out-of-range i returns slot 0 (or 0 when empty).</summary>
        float CompanionXAt(int slot);
        /// <summary>D6.5: slot i follower y.</summary>
        float CompanionYAt(int slot);
        /// <summary>D6.5: slot i visible-attack flag.</summary>
        bool CompanionAttackingAt(int slot);
        /// <summary>D6.5: slot i commanded locomotion behavior (global hold/recall).</summary>
        CompanionBehavior CompanionBehaviorAt(int slot);
        /// <summary>D6.5: slot i target-facing (+1/-1).</summary>
        int CompanionFacingAt(int slot);
        /// <summary>A7.2: true while slot i is closing on its locked target instead of
        /// trailing its anchor. Derived per tick — a held slot never reports it.</summary>
        bool CompanionEngagedAt(int slot);
        /// <summary>A7.1: enemy id this slot has locked, or 0 when it holds no target.
        /// Ids are unique and never reused, so this survives enemy-array compaction.</summary>
        int CompanionTargetIdAt(int slot);
        /// <summary>A8.5: slot i's signature skill. Constant for the run — it is a property
        /// of the archetype, not of the slot's state.</summary>
        CompanionSkillId CompanionSkillIdAt(int slot);
        /// <summary>A8.5: seconds until slot i can cast again; 0 = ready. Starts at the full
        /// cooldown, so a run never opens with a free cast.</summary>
        float CompanionSkillCooldownAt(int slot);
        /// <summary>A8.5: true for the brief display window after slot i casts, so the view
        /// has a cue without reading SimEvents (which is a per-tick, run-wide mask).</summary>
        bool CompanionSkillCastingAt(int slot);
        /// <summary>Living stage boss health; 0 when no boss is alive.</summary>
        float BossHp { get; }
        float BossMaxHp { get; }
        /// <summary>
        /// 0 until the stage boss appears, then 1 or 2. A cleared stage keeps the phase
        /// the boss died in so the result overlay can still read it.
        /// </summary>
        int BossPhase { get; }
        /// <summary>Roster bitmask after this run's extractions (persist in the view).</summary>
        int RosterMask { get; }
    }

    /// <summary>All frozen numeric constants from docs/SIM_SPEC_HACKSLASH.md.</summary>
    public static class HackSpec
    {
        // --- §1 prologue ---
        public const string PrologueStageId = "prologue";
        public const string PrologueClearReason = "prologue-clear";
        /// <summary>Trial end marker — the clock ran out, which is the only way
        /// a trial ends (AMENDMENT #7).</summary>
        public const string TrainingClearReason = "training-clear";
        public const int PrologueWaves = 3;
        private static readonly int[] PrologueSpawns = { 4, 6, 8 };

        /// <summary>Prologue wave sizes: 4 / 6 / 8 (§1).</summary>
        public static int PrologueSpawnCount(int wave)
        {
            if (wave < 1)
            {
                return PrologueSpawns[0];
            }
            return wave > PrologueSpawns.Length ? 0 : PrologueSpawns[wave - 1];
        }

        // --- §2.1 combo ---
        public const int ComboLength = 3;
        /// <summary>Hit damage 58 / 58 / 87, carried as a multiple of the 58 base.</summary>
        public static readonly float[] ComboDamageScale = { 1f, 1f, 87f / 58f };
        public static readonly float[] ComboSwing = { 0.30f, 0.30f, 0.42f };
        public static readonly float[] ComboActiveFrom = { 0.10f, 0.10f, 0.14f };
        public static readonly float[] ComboActiveTo = { 0.22f, 0.22f, 0.30f };
        public const float ComboLinkWindow = 0.9f;
        public const float ComboKnockbackDistance = 120f;
        public const float ComboKnockbackTime = 0.18f;
        /// <summary>Dungeon enemy health: <c>86 + min(140, (wave-1)*11)</c>.</summary>
        public const float DungeonEnemyBaseHealth = 86f;
        public const float DungeonEnemyHealthPerWave = 11f;
        public const float DungeonEnemyHealthCap = 140f;

        // --- §2.2 dash ---
        public const float DashDistance = 190f;
        public const float DashTime = 0.22f;
        public const float DashCooldownSeconds = 1.6f;
        public const float DashCost = 8f;

        // --- §2.3 skills ---
        public const int SkillCount = 4;
        public const int SkillBolt = 0;
        public const int SkillPulse = 1;
        public const int SkillNova = 2;
        public const int SkillAegis = 3;

        public const float BoltRange = 420f;
        public const float BoltDamage = 145f;
        public const float BoltSplashRadius = 115f;
        public const float BoltSplashScale = 0.6f;
        public const float BoltCooldown = 6.5f;
        public const float BoltCost = 25f;
        public const Element BoltElement = Element.Void;

        public const float PulseRadius = 190f;
        public const float PulseDuration = 3f;
        public const float PulseTickInterval = 0.5f;
        public const float PulseTickDamage = 26f;
        public const float PulseCooldown = 4f;
        public const float PulseCost = 30f;
        public const Element PulseElement = Element.Ember;

        public const float AshNovaRadius = 230f;
        public const float AshNovaDamage = 110f;
        public const float AshNovaKnockback = 120f;
        public const float AshNovaCooldown = 8f;
        public const float AshNovaCost = 45f;
        public const Element AshNovaElement = Element.Ember;

        public const float AegisShield = 40f;
        public const float AegisDuration = 8f;
        public const float AegisCastInvuln = 0.2f;
        public const float AegisCooldown = 12f;
        public const float AegisCost = 30f;
        public const Element AegisElement = Element.Frost;

        // --- §2.4 elements ---
        public const float ElementAdvantage = 1.2f;
        public const float ElementDisadvantage = 0.85f;

        // --- §2.5 xp / levels ---
        public const int LevelCap = 12;
        public const int XpPerKill = 10;
        public const int XpPerElite = 25;
        public const int XpPerBoss = 150;
        private static readonly int[] XpCurve = { 30, 55, 85, 120, 160, 205, 255, 310 };
        public const int XpPerLevelBeyondCurve = 60;
        public const float LevelDamageBonus = 0.04f;
        public const float LevelHealthBonus = 6f;
        public const float LevelRegenBonus = 0.3f;

        // --- §3 elites and extraction ---
        /// <summary>Input depth §2: knockback multiplier per finisher variant,
        /// indexed by ComboVariant (Neutral/Launcher/Retreat/Spin). Neutral is
        /// exactly 1.0, so a player who never holds a direction sees the
        /// original finisher unchanged.</summary>
        public static readonly float[] FinisherKnockbackMul = { 1.00f, 1.60f, 0.70f, 1.00f };

        /// <summary>Input depth §2: the spin finisher trades force for reach.</summary>
        public const float SpinReachMul = 1.35f;

        /// <summary>Input depth §2: how far the retreat finisher slides the
        /// player back. Under one dash (190) so it repositions without
        /// replacing the dodge.</summary>
        public const float RetreatStepDistance = 74f;

        /// <summary>Motion depth: how far a phase-3 boss slam throws the
        /// PLAYER. Deliberately shorter than the player's own dash (190) so a
        /// launch never covers more ground than a deliberate dodge, and short
        /// enough that the recovery does not read as a stun-lock.</summary>
        public const float BossSlamKnockbackDistance = 150f;
        public const float BossSlamKnockbackTime = 0.26f;

        // --- input depth (_workspace/current/design/input-depth-spec.md) ---
        /// <summary>§3: hold this long AFTER a swing ends to arm the heavy.
        /// Under the boss telegraph (0.80 s) on purpose — a player who reads a
        /// wind-up still has time to push a charge into the opening.</summary>
        public const float ChargeReadySeconds = 0.45f;
        public const float ChargeDamageMul = 1.8f;
        public const float ChargeKnockbackMul = 2.0f;
        /// <summary>§3: charging is a commitment — movement is slowed while it
        /// builds, so the heavy costs position instead of being free damage.</summary>
        public const float ChargeMoveScale = 0.45f;

        /// <summary>§5: seconds before an unanswered level-up offer confirms
        /// itself. Short on purpose — a longer window would leave a player who
        /// ignores it without stats for a meaningful slice of a 19 s boss
        /// fight, which would make "ignoring costs nothing" false.</summary>
        public const float GrowthOfferSeconds = 5f;
        public const float GrowthAttackBonus = 0.08f;     // +8% damage per point
        public const float GrowthVitalityHealth = 6f;     // +6 max HP per point
        public const float GrowthSwiftnessSpeed = 0.04f;  // +4% move per point
        /// <summary>§5: -6% dash cooldown per swiftness point, floored at 0.55
        /// so a fully-invested build still has a real dodge cycle rather than
        /// a permanent i-frame.</summary>
        public const float GrowthSwiftnessCooldown = 0.06f;
        public const float GrowthSwiftnessCooldownFloor = 0.55f;

        /// <summary>B-1 (AMENDMENT #4): dungeon-only boss HP factor, on top of
        /// the frozen SimConfig.BossHealthMul. Shares UpdateBossPhase's
        /// _dungeon gate, so boss length and boss phases always apply to the
        /// same runs. Arena and plain-campaign bosses are unaffected.
        /// Sized by measurement, not taste: see the sweep in
        /// _workspace/current/design/boss-phase-metric-definition.md §6.</summary>
        public const float DungeonBossHealthMul = 6f;

        public const int EliteSpawnModulus = 7;
        public const float EliteHealthMul = 3f;
        public const float EliteDamageMul = 1.5f;
        public const float EliteScale = 1.35f;
        public const float CorpseLifetime = 10f;
        public const float ExtractionRadius = 90f;
        public const float ExtractionSeconds = 2f;
        public const float ExtractionDamageBonus = 0.08f;
        public const int ExtractionDuplicateRelics = 30;

        // --- §4 companion ---
        public const float CompanionFollowOffset = 80f;
        public const float CompanionAttackInterval = 1.1f;
        public const float CompanionAttackRange = 200f;
        public const float CompanionDamageScale = 0.6f;
        public const float CompanionAttackDisplay = 0.25f;

        // --- §4 companion autonomy (AMENDMENT #7 — docs/SIM_SPEC_HACKSLASH.md A7) ---
        // Every quantity is a compile-time constant compared against accumulated
        // fixed-step floats, so §13 (no RNG anywhere) still holds. The relations are
        // chosen, not tasted: AcquireRadius < LeashRadius guarantees a slot can always
        // reach a target it is allowed to lock; LeashRadius = 4 x FollowOffset keeps the
        // leash in anchor units; PursuitSpeedScale > 1 is the only way to close on a foe
        // that walks at player speed, and stays low enough that a slot never leads.
        /// <summary>A7.4: target acquisition radius, measured from the follow anchor
        /// (not from the companion) so §4/D6.3 attack geometry is untouched.</summary>
        public const float CompanionAcquireRadius = 300f;
        /// <summary>A7.2: hard leash from the follow anchor. A locked target beyond it is
        /// dropped and the slot returns; a slot beyond it never pursues.</summary>
        public const float CompanionLeashRadius = 320f;
        /// <summary>A7.2: pursuit speed as a multiple of the player's current speed.</summary>
        public const float CompanionPursuitSpeedScale = 1.05f;
        /// <summary>A7.1: seconds a locked target is retained against a nearer late arrival
        /// (120 ticks at the 1/60 fixed step — integral, so the lock is frame-exact).</summary>
        public const float CompanionTargetLockSeconds = 2f;
        /// <summary>A7.3: dwell after an engagement ends before walking back to the anchor;
        /// suppresses acquire/return oscillation at the radius edge.</summary>
        public const float CompanionReturnGraceSeconds = 0.35f;

        // --- §4 companion multi-slot (AMENDMENT #6 — docs/SIM_SPEC_HACKSLASH.md D6.3/D6.4) ---
        /// <summary>D6.4: lateral fan-out per slot, perpendicular to facing.
        /// slot 0 = 0 (identical to the frozen §4 follower), slot 1 = +64, slot 2 = -64.</summary>
        public static readonly float[] CompanionSlotFanout = { 0f, 64f, -64f };

        /// <summary>D6.3 per-archetype combat tuple: cadence (s), attack range (px),
        /// player-damage scale. Keyed by the companion's underlying <see cref="EnemyVisual"/>.
        /// ember-cohort is pinned to the §4 tuple so the pre-amendment single-companion
        /// run stays digest-identical (approved AMENDMENT #6 correction).</summary>
        public static void CompanionStats(
            EnemyVisual visual,
            out float cadence,
            out float range,
            out float damageScale)
        {
            switch (visual)
            {
                case EnemyVisual.Scout:      // scout-echo (skirmisher)
                    cadence = 0.85f; range = 240f; damageScale = 0.50f; return;
                case EnemyVisual.Shade:      // shade-echo (caster)
                    cadence = 1.30f; range = 260f; damageScale = 0.65f; return;
                case EnemyVisual.Possessed:  // possessed-echo (heavy)
                    cadence = 1.45f; range = 150f; damageScale = 0.80f; return;
                case EnemyVisual.EmberCohort: // ember-cohort — pinned to §4 fallback
                default:
                    cadence = CompanionAttackInterval;
                    range = CompanionAttackRange;
                    damageScale = CompanionDamageScale;
                    return;
            }
        }

        /// <summary>AMENDMENT #6: map a companion id (its <c>&lt;visual&gt;-echo</c> or bare
        /// prefab id) to the underlying <see cref="EnemyVisual"/> archetype for D6.3 stats.
        /// Unknown ids fall back to <see cref="EnemyVisual.EmberCohort"/> (= §4 tuple).</summary>
        public static EnemyVisual CompanionArchetype(string companionId)
        {
            if (string.IsNullOrEmpty(companionId))
            {
                return EnemyVisual.EmberCohort;
            }
            string baseId = companionId.EndsWith("-echo")
                ? companionId.Substring(0, companionId.Length - "-echo".Length)
                : companionId;
            switch (baseId)
            {
                case "scout": return EnemyVisual.Scout;
                case "shade": return EnemyVisual.Shade;
                case "possessed": return EnemyVisual.Possessed;
                case "ember-cohort":
                default: return EnemyVisual.EmberCohort;
            }
        }

        // --- §4 companion signature skills (AMENDMENT #8 — docs/SIM_SPEC_HACKSLASH.md A8) ---
        /// <summary>A8.5: seconds the cast cue stays up on the snapshot.</summary>
        public const float CompanionSkillFlashSeconds = 0.35f;
        /// <summary>A8.2: hard cap over every archetype's MaxTargets — sizes the sim's
        /// selection scratch buffer, so no cast can allocate.</summary>
        public const int CompanionSkillTargetCap = 8;

        /// <summary>
        /// A8.2 per-archetype signature skill — the numeric gate. Keyed by the same
        /// <see cref="EnemyVisual"/> archetype as the D6.3 combat tuple, so a companion's
        /// skill and its stats can never disagree about what it is.
        /// Every archetype differs from every other on ALL FOUR of cooldown, radius,
        /// damage scale and target count; that pairwise distinctness is the machine-checkable
        /// form of "each companion has its own skill" and is asserted by
        /// <c>CompanionSkill_TableIsPairwiseDistinctOnEveryAxis</c>.
        /// Unlike D6.3 there is no §4 fallback tuple to preserve: skills are new surface,
        /// so ember-cohort gets a real skill of its own instead of an inert default.
        /// </summary>
        public static CompanionSkillSpec CompanionSkill(EnemyVisual visual)
        {
            switch (visual)
            {
                case EnemyVisual.Scout:      // scout-echo — fast, shallow, multi-target
                    return new CompanionSkillSpec(CompanionSkillId.Volley, 6f, 240f, 0.55f, 3, 2, 0f);
                case EnemyVisual.Shade:      // shade-echo — widest net, weakest per hit
                    return new CompanionSkillSpec(CompanionSkillId.Hex, 8f, 260f, 0.40f, 8, 2, 0f);
                case EnemyVisual.Possessed:  // possessed-echo — tight shockwave, only shove
                    return new CompanionSkillSpec(CompanionSkillId.Quake, 9f, 170f, 0.70f, 6, 2, 90f);
                case EnemyVisual.EmberCohort: // ember-cohort — single focused nuke
                default:
                    return new CompanionSkillSpec(CompanionSkillId.Flare, 7f, 200f, 1.10f, 1, 1, 0f);
            }
        }


        // --- §5 meta stats ---
        public const int MaxStatPoints = 10;
        public const float AttackPerPoint = 0.03f;
        public const float VitalityHealthPerPoint = 8f;
        public const float SwiftnessSpeedPerPoint = 0.02f;

        // --- §13 sigils (AMENDMENT #6 — design/sigil-spec.md) -----------------
        // Meta upgrades that touch the GIMMICKS instead of the stat block.
        // Sourced from .survey/meta-upgrade-gimmick-interaction/. Three rules the
        // numbers below obey, each one a survey finding:
        //
        //  1. NO IMMUNITY, resistance only. The line the survey draws is "does the
        //     hazard still change your behaviour" — so the current still shoves you
        //     (100 px/s against 218 move), the wall still hurts (6 a tick), and the
        //     vent's defensive face does not reduce its damage AT ALL, it converts
        //     the hit into oil. A sigil never makes standing in a hazard correct.
        //  2. NO RANDOM PROCS. Every value here is a constant multiplier or
        //     replacement resolved once at construction. Predictability IS the
        //     product identity (the D4-affliction backlash vs HoT-wall praise).
        //  3. SIDEGRADES. A/B are different axes (survive vs kill), not a ladder,
        //     so neither face is the "correct" pick independent of the stage.
        //
        // Unequipped resolves to the pre-amendment constants exactly — proven by
        // the 15 golden rows staying byte-identical.
        public const int SigilSlots = SigilLoadout.Slots;

        /// <summary>역류인 A — current push ON THE PLAYER scales by this.</summary>
        public const float SigilCurrentPlayerPushMult = 0.5f;   // 200 -> 100 vs move 218
        /// <summary>역류인 B — current push ON ENEMIES scales by this.</summary>
        public const float SigilCurrentEnemyPushMult = 1.5f;

        /// <summary>판결인 A — pylon aura damage-taken multiplier is RAISED to this
        /// (0.40 -> 0.70: the shield thins, it does not vanish).</summary>
        public const float SigilPylonAuraRelief = 0.70f;
        /// <summary>판결인 B — damage dealt TO a pylon body scales by this.</summary>
        public const float SigilPylonStrikeMult = 2f;

        /// <summary>집행인 A — wall tick on the player, replacing 10.</summary>
        public const float SigilWallPlayerTick = 6f;
        /// <summary>집행인 B — wall tick on enemies, replacing 10.</summary>
        public const float SigilWallEnemyTick = 18f;

        /// <summary>점화인 A — oil granted when a vent hits the player. The DAMAGE
        /// IS UNCHANGED: this face buys resource off pain, never safety.</summary>
        public const float SigilVentOilRefund = 12f;
        /// <summary>점화인 B — vent damage dealt to enemies, replacing 8. Vents are
        /// player-only by default (SIM_SPEC_CAMPAIGN); this face opts INTO the
        /// symmetric doctrine for vents, and only while equipped.</summary>
        public const float SigilVentEnemyDamage = 14f;

        /// <summary>증언인 A — altar channel seconds, replacing 1.2.</summary>
        public const float SigilAltarHoldSeconds = 0.8f;
        /// <summary>증언인 B — altar oil burst, replacing 18.</summary>
        public const float SigilAltarOilBurst = 30f;

        // --- §14 surge (AMENDMENT #7 — design/training-and-surge-spec.md) -----
        // Deterministic surge windows. Sourced from
        // .survey/roguelike-training-and-surge/: the genre builds surges out of a
        // CLOCK (3/13) and never out of a health threshold (0/13) or a kill count
        // (0~1/13), because an RNG run cannot reproduce either one. Ours can.
        //
        // A window is SIM STATE ONLY. It opens, it publishes, it closes — and by
        // itself it changes no number. Every mechanical consequence is owned by
        // an equipped sigil clause (§13 + CinderSim.ResolveSigils).
        //
        // That rule is the second correction this feature took, and a probe
        // forced it: the draft slowed the hazard clock as a base effect of peril,
        // which fired on plain unequipped runs and would have moved all 15 golden
        // digests. Making the window inert on its own also fills the exact gap
        // the survey found — "upgrade layer x surge form" was 0/15 filled — so
        // the correction is the better design, not just the safer one.

        /// <summary>Peril arms when health first drops below this fraction.</summary>
        public const float PerilHealthFraction = 0.35f;
        /// <summary>Peril re-arms only after health recovers past this fraction
        /// (hysteresis — stops a re-trigger chain around the threshold).</summary>
        public const float PerilRearmFraction = 0.50f;
        /// <summary>Peril windows allowed per run (negotiation entry 8 cap).</summary>
        public const int PerilRunCap = 2;
        /// <summary>Peril window seconds. 3, not 6: the director's arithmetic put a
        /// 6 s wall exemption at 100% of base HP avoided — reversal grade.</summary>
        public const float PerilSeconds = 3f;

        /// <summary>Every Nth cumulative kill opens a surge window.</summary>
        public const int SurgeKillInterval = 12;
        /// <summary>Surge windows allowed per wave.</summary>
        public const int SurgeWaveCap = 1;
        /// <summary>Surge window seconds.</summary>
        public const float SurgeSeconds = 6f;
        /// <summary>Hazard damage dealt to ENEMIES scales by this inside a surge.
        /// The cross-table cells this comes from are "sweep enemies with the
        /// current" and "herd enemies into the wall" — both empty in the genre.</summary>
        public const float SurgeEnemyHazardMult = 2f;
        /// <summary>점화인 raises the surge multiplier to this instead of adding a
        /// second payout (a layer on the layer, not a duplicate grant).</summary>
        public const float SigilSurgeEnemyHazardMult = 3f;

        // --- §15 training ground (AMENDMENT #7) -------------------------------
        /// <summary>A trial is a fixed 60 s window — no wave table, no spawns.</summary>
        public const float TrainingSeconds = 60f;
        /// <summary>Trial tiers. Tier k scales hazard PERIODS by Tier{k}Rate; the
        /// telegraph seconds never move, because no title in the survey pool uses
        /// telegraph shortening as a difficulty lever.</summary>
        public const int TrainingTiers = 3;
        /// <summary>Period multipliers per tier (견습 / 숙련 / 판결).</summary>
        public static float TrainingTierRate(int tier) => tier <= 0 ? 1f : tier == 1 ? 0.85f : 0.7f;
        /// <summary>One-time relic grant for clearing every trial at 판결 tier
        /// (negotiation entry 7 — repeat currency payouts are banned).</summary>
        public const int TrainingMasteryRelics = 2;

        // --- §7 boss phases (AMENDMENT #4 — boss-phase-metric-definition) -----
        // Three phases on HP thresholds. The stat vector is stored as TIME
        // values in seconds, so "the numeric sum gets smaller" IS the
        // strengthening. Summing the three stored arrays below:
        //   P1 0.80+1.37+5.00 = 7.17
        //   P2 0.80+1.16+4.00 = 5.96
        //   P3 0.80+0.99+3.25 = 5.04
        // Monotonically decreasing. (The research's fuller Phase Time Budget
        // adds a fourth closing-distance term T_reach = max(0, D_ref-range)/
        // speed, giving 11.26 -> 9.10 -> 7.61; that term is derived from live
        // positions, not stored here, so these constants carry the three
        // schedulable values only.)
        //
        // Thresholds are 50/20, NOT an even split: 50% already carries a
        // speech-bubble contract (SIM_SPEC_HACKSLASH §7), and pulling the
        // second boundary in to 20% lengthens the hardest phase instead of
        // compressing it.
        //
        // Telegraph is HELD at 0.80 s across all phases. Shrinking it to 0.60
        // buys 3.2% of the total difficulty delta while cutting the reaction
        // margin from 1.36x to 1.15x over human visual reaction (~250 ms) plus
        // input latency — the worst trade in the table.
        //
        // Boss base speed is x0.7 (SIM_SPEC §Bosses), so even P3's x1.45 lands
        // at 0.7*1.45 = 1.015 — merely ordinary-enemy pace, not a chase the
        // player cannot escape.
        public const float BossPhase2HealthFraction = 0.50f;   // P1 -> P2
        public const float BossPhase3HealthFraction = 0.20f;   // P2 -> P3

        /// <summary>Per-phase time budget, indexed 0/1/2 for P1/P2/P3.
        /// Smaller = stronger; the sum decreases monotonically.</summary>
        public static readonly float[] BossAttackInterval = { 1.37f, 1.16f, 0.99f };
        public static readonly float[] BossTelegraph = { 0.80f, 0.80f, 0.80f };
        public static readonly float[] BossSkillCooldown = { 5.00f, 4.00f, 3.25f };

        /// <summary>Multiplier vectors, indexed 0/1/2. Deliberately NOT all
        /// raised per phase — simultaneous growth multiplies difficulty and
        /// reads as "suddenly unwinnable".</summary>
        public static readonly float[] BossSpeedMul = { 1.00f, 1.25f, 1.45f };
        public static readonly float[] BossRangeMul = { 1.00f, 1.10f, 1.20f };

        /// <summary>Contact damage keeps the amendment-2 curve.</summary>
        public const float BossPhase2DamageMul = 1.25f;
        public const float BossPhase3DamageMul = 1.45f;
        public const int MonarchPhase2Escorts = 3;

        /// <summary>Phase index (0/1/2) for a health fraction. Pure so the
        /// boundary can be pinned without stepping a sim.</summary>
        public static int BossPhaseIndexFor(float healthFraction)
        {
            if (healthFraction <= BossPhase3HealthFraction) return 2;
            if (healthFraction <= BossPhase2HealthFraction) return 1;
            return 0;
        }

        public static int ClampStat(int points)
        {
            if (points < 0)
            {
                return 0;
            }
            return points > MaxStatPoints ? MaxStatPoints : points;
        }

        /// <summary>XP needed to go from <paramref name="level"/> to the next; 0 at cap.</summary>
        public static int XpToNextLevel(int level)
        {
            if (level >= LevelCap)
            {
                return 0;
            }
            int index = level < 1 ? 0 : level - 1;
            if (index < XpCurve.Length)
            {
                return XpCurve[index];
            }
            return XpCurve[XpCurve.Length - 1] + XpPerLevelBeyondCurve * (index - (XpCurve.Length - 1));
        }

        /// <summary>Enemy element by visual archetype (§2.4).</summary>
        public static Element ElementOf(EnemyVisual visual)
        {
            switch (visual)
            {
                case EnemyVisual.EmberCohort: return Element.Ember;
                case EnemyVisual.Scout: return Element.Frost;
                case EnemyVisual.Shade: return Element.Veil;
                case EnemyVisual.Possessed: return Element.Void;
                case EnemyVisual.BossCommander: return Element.Veil;
                case EnemyVisual.BossMonarch: return Element.Void;
                default: return Element.None;
            }
        }

        /// <summary>True when <paramref name="attacker"/> is one cycle step ahead.</summary>
        public static bool Beats(Element attacker, Element defender)
        {
            if (attacker == Element.None || defender == Element.None)
            {
                return false;
            }
            return (int)attacker % 4 + 1 == (int)defender;
        }

        /// <summary>Skill damage multiplier: +20% favourable, −15% unfavourable (§2.4).</summary>
        public static float Matchup(Element attacker, Element defender)
        {
            if (Beats(attacker, defender))
            {
                return ElementAdvantage;
            }
            return Beats(defender, attacker) ? ElementDisadvantage : 1f;
        }
    }
}
