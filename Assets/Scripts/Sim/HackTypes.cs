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
    public enum GameMode { Arena = 0, Prologue = 1, Dungeon = 2 }

    /// <summary>Active dungeon companion locomotion mode.</summary>
    public enum CompanionBehavior { Follow = 0, Hold = 1 }

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

        // --- §5 meta stats ---
        public const int MaxStatPoints = 10;
        public const float AttackPerPoint = 0.03f;
        public const float VitalityHealthPerPoint = 8f;
        public const float SwiftnessSpeedPerPoint = 0.02f;

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
