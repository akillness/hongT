// AMENDMENT #13 (W4 — point-budget waves + DDA) and AMENDMENT #14 (W5 — graded
// loot + bad-luck protection). Numeric truth: docs/SIM_SPEC_HACKSLASH.md §17/§18.
//
// NOT a frozen contract file. It follows the DifficultySpec.cs precedent
// (AMENDMENT #11): every number here is reachable only when the caller opts in
// through DungeonProgressionConfig, and default(DungeonProgressionConfig) leaves
// both features off. The existing CinderSim(in HackConfig) constructor forwards
// that default, so every golden digest — arena, prologue and dungeon — stays
// byte-identical without a re-bless.
//
// §13 (determinism) is NOT amended. There is no RNG here: the budget is integer
// arithmetic, the difficulty band is an integer accumulator driven by observed
// wave outcomes, and the loot roll is an integer avalanche hash of run state —
// the same precedent as EliteSpawnModulus, the id%7 equipment shard and the
// Ember Rest offer hash.
//
// Pure C#. No UnityEngine references allowed in this assembly (asmdef enforces).
using System;
using System.Collections.Generic;

namespace CinderCourt.Sim
{
    /// <summary>
    /// Opt-in switches for the two dungeon-only progression amendments. Both
    /// default to <c>false</c>, which is what every pre-amendment call site
    /// produces, so the frozen numbers are the default behaviour.
    /// </summary>
    public struct DungeonProgressionConfig
    {
        /// <summary>AMENDMENT #13: replace the fixed spawn/health curve with the
        /// point budget and let the DDA band scale it.</summary>
        public bool AdaptiveWaves;

        /// <summary>AMENDMENT #14: grade every dungeon drop and run the pity ledger.</summary>
        public bool GradedLoot;

        /// <summary>
        /// AMENDMENT #15: dungeon movement bounds. <c>default</c> (both half-axes 0)
        /// resolves to the frozen <see cref="SimConfig.ArenaHalfWidth"/> /
        /// <see cref="SimConfig.ArenaHalfHeight"/>, so it is inert unless set.
        /// </summary>
        public DungeonBounds Bounds;

        /// <summary>True when at least one amendment is live.</summary>
        public bool Any => AdaptiveWaves || GradedLoot || Bounds.Active;

        /// <summary>#13 + #14 only. Bounds stay frozen — the movement amendment has
        /// a hard View coupling (the boundary wall ring), so it is opted into
        /// separately rather than riding along.</summary>
        public static DungeonProgressionConfig All => new DungeonProgressionConfig
        {
            AdaptiveWaves = true,
            GradedLoot = true,
        };

        /// <summary>#13 + #14 + #15 at the recommended expanded bounds.</summary>
        public static DungeonProgressionConfig Everything => new DungeonProgressionConfig
        {
            AdaptiveWaves = true,
            GradedLoot = true,
            Bounds = DungeonBoundsSpec.Expanded,
        };
    }

    /// <summary>
    /// AMENDMENT #15 §19 — dungeon movement bounds. Half-axes of the clamp ellipse
    /// around the frozen arena centre (768, 604). Zero means "use the frozen
    /// constant", which is what <c>default</c> produces.
    /// </summary>
    public struct DungeonBounds
    {
        public float HalfWidth;
        public float HalfHeight;

        /// <summary>True only when BOTH axes are set. A half-set struct is treated as
        /// inert rather than silently expanding one axis.</summary>
        public bool Active => HalfWidth > 0f && HalfHeight > 0f;

        public static DungeonBounds Of(float halfWidth, float halfHeight) => new DungeonBounds
        {
            HalfWidth = halfWidth,
            HalfHeight = halfHeight,
        };
    }

    /// <summary>
    /// AMENDMENT #15 §19 — the recommended expansion and the resolver both the sim
    /// and the view read, so the clamp and the boundary wall ring can never disagree.
    /// </summary>
    public static class DungeonBoundsSpec
    {
        /// <summary>
        /// [TARGET] Expanded half-width, 520 × 1.065. This axis is NOT limited by the
        /// painted floor — it is limited by the frozen gimmick geometry. Both the ash
        /// wall (<see cref="CampaignSpec.WallEdgeX"/> 248 →
        /// <see cref="CampaignSpec.WallEdgeRightX"/> 1288) and every tide current
        /// (x 768, <see cref="CampaignSpec.CurrentHalfW"/> 520) cover exactly
        /// x 248..1288. With <see cref="SimConfig.PlayerMarginClamp"/> = 34 a
        /// half-width of 554 puts the player's reach at exactly 520, so the playfield
        /// stays fully inside both gimmicks' spans. One pixel more and the ash wall
        /// becomes avoidable by standing past its edge — the gimmick would still fire,
        /// but it would no longer be a threat, which is a balance change this
        /// amendment deliberately does not make.
        /// </summary>
        public const float ExpandedHalfWidth = 554f;

        /// <summary>
        /// [TARGET] Expanded half-height, 270 × 1.548. No gimmick constrains this axis
        /// — currents are y-bands at fixed y, vents are points, the ash wall sweeps on
        /// x. The binding constraint is the painted backdrop plate: it spans sim
        /// y 0..1024 while the arena centre sits at y 604, so the room below the
        /// centre is only 420. 418 keeps the enemy ring at y 198..1010, inside the
        /// plate at both ends.
        /// </summary>
        public const float ExpandedHalfHeight = 418f;

        public static DungeonBounds Expanded => DungeonBounds.Of(ExpandedHalfWidth, ExpandedHalfHeight);

        public static DungeonBounds Frozen =>
            DungeonBounds.Of(SimConfig.ArenaHalfWidth, SimConfig.ArenaHalfHeight);

        /// <summary>
        /// Resolves the half-axes actually used. An inactive struct resolves to the
        /// frozen constants; an active one is clamped so it can never SHRINK the
        /// arena below the frozen geometry — a shrink would move hazards and spawn
        /// points outside the playfield, which is a different (and unrequested)
        /// change from the one this amendment makes.
        /// </summary>
        public static void Resolve(in DungeonBounds bounds, out float halfWidth, out float halfHeight)
        {
            if (!bounds.Active)
            {
                halfWidth = SimConfig.ArenaHalfWidth;
                halfHeight = SimConfig.ArenaHalfHeight;
                return;
            }
            halfWidth = bounds.HalfWidth < SimConfig.ArenaHalfWidth
                ? SimConfig.ArenaHalfWidth
                : bounds.HalfWidth;
            halfHeight = bounds.HalfHeight < SimConfig.ArenaHalfHeight
                ? SimConfig.ArenaHalfHeight
                : bounds.HalfHeight;
        }

        /// <summary>
        /// Ellipse parameter of the ENEMY stop line for a given half-width — the
        /// number <c>EnvironmentBuilder.StopE</c> derives the boundary wall ring from.
        /// Published so the view can track an expanded clamp instead of re-deriving
        /// it from the frozen constant.
        /// </summary>
        public static float EnemyStopE(float halfWidth) =>
            (halfWidth - SimConfig.EnemyMarginClamp) / halfWidth;

        /// <summary>Ellipse parameter of the PLAYER stop line.</summary>
        public static float PlayerStopE(float halfWidth) =>
            (halfWidth - SimConfig.PlayerMarginClamp) / halfWidth;
    }

    /// <summary>AMENDMENT #14 drop tier. Values are the table index, so the enum
    /// and <see cref="LootGradeSpec.GradeValueMul"/> can never drift apart.</summary>
    public enum LootGrade
    {
        Basic = 0,
        Fine = 1,
        Epic = 2,
    }

    /// <summary>
    /// AMENDMENT #13 §17 — the wave point budget and the dynamic difficulty band.
    /// Every member is a pure function of integers (plus one float divide for the
    /// health multiplier), so the whole class is reproducible from its arguments.
    /// </summary>
    public static class WaveBudgetSpec
    {
        // --- §17.1 budget curve ---
        /// <summary>Points a wave-1 dungeon wave is worth.</summary>
        public const int BudgetBase = 100;
        /// <summary>Points added per wave beyond the first.</summary>
        public const int BudgetPerWave = 26;
        /// <summary>Budget ceiling; reached at wave 21.</summary>
        public const int BudgetCap = 600;

        // --- §17.2 budget spend ---
        /// <summary>Points one ordinary spawn costs.</summary>
        public const int GruntCost = 16;
        /// <summary>Spawn-count floor, so a heavily throttled band still fields a wave.</summary>
        public const int MinSpawns = 4;
        /// <summary>Spawn-count ceiling. Below <see cref="SimConfig.EnemyCap"/> on
        /// purpose: past this the budget buys health instead of bodies, which is what
        /// keeps the pack readable on a phone-tier screen.</summary>
        public const int MaxSpawns = 14;
        /// <summary>Cap on the surplus-driven health bonus (a guard, not the usual
        /// binding constraint — the budget cap is reached first).</summary>
        public const float HealthSurplusCap = 1.7f;
        /// <summary>Points that buy one elite slot for the wave.</summary>
        public const int ElitePointCost = 150;
        /// <summary>Hard ceiling on elites per wave.</summary>
        public const int EliteAllowanceCap = 3;

        // --- §17.3 DDA band ---
        public const int BandMin = -2;
        public const int BandMax = 2;
        /// <summary>Band multiplier in permille so the budget stays integer arithmetic.
        /// Indexed by <c>band - BandMin</c>.</summary>
        public static readonly int[] BandPermille = { 780, 890, 1000, 1120, 1250 };

        /// <summary>Health fraction at wave clear that reads as "coasting".</summary>
        public const float HealthyFraction = 0.75f;
        /// <summary>Health fraction at wave clear that reads as "barely survived".</summary>
        public const float StruggleFraction = 0.35f;
        /// <summary>Wave duration at or under which the player is outpacing the spawner.</summary>
        public const float FastWaveSeconds = 18f;
        /// <summary>Wave duration at or over which the player is stalling.</summary>
        public const float SlowWaveSeconds = 42f;
        /// <summary>Hits taken in the wave that read as a clean clear.</summary>
        public const int CleanHits = 2;
        /// <summary>Hits taken in the wave that read as being overwhelmed.</summary>
        public const int BatteredHits = 9;
        /// <summary>Maximum band movement per wave. One band per wave is what keeps
        /// the ramp legible instead of oscillating.</summary>
        public const int StepCap = 1;

        /// <summary>Un-scaled point budget for a wave.</summary>
        public static int BaseBudget(int wave)
        {
            if (wave < 1)
            {
                return BudgetBase;
            }
            long raw = (long)BudgetBase + (long)(wave - 1) * BudgetPerWave;
            return raw > BudgetCap ? BudgetCap : (int)raw;
        }

        /// <summary>Clamps a band into the legal range.</summary>
        public static int ClampBand(int band)
        {
            if (band < BandMin)
            {
                return BandMin;
            }
            return band > BandMax ? BandMax : band;
        }

        /// <summary>Permille multiplier the band applies to the budget.</summary>
        public static int BandMultiplierPermille(int band) => BandPermille[ClampBand(band) - BandMin];

        /// <summary>Band-scaled point budget. Integer arithmetic end to end.</summary>
        public static int EffectiveBudget(int wave, int band)
        {
            return BaseBudget(wave) * BandMultiplierPermille(band) / 1000;
        }

        /// <summary>How many bodies a budget fields.</summary>
        public static int SpawnCountForBudget(int budget)
        {
            int count = budget / GruntCost;
            if (count < MinSpawns)
            {
                count = MinSpawns;
            }
            return count > MaxSpawns ? MaxSpawns : count;
        }

        /// <summary>Points a full-strength wave (<see cref="MaxSpawns"/> bodies) costs.
        /// Budget beyond this is what buys hit points.</summary>
        public const int FullRosterSpend = MaxSpawns * GruntCost;

        /// <summary>
        /// Health multiplier applied to <see cref="HackSpec.DungeonEnemyBaseHealth"/>.
        /// The budget buys bodies first; only what is left over once a full roster is
        /// paid for turns into hit points. Measuring the surplus against the FIXED
        /// full-roster spend rather than the actual truncated spend is deliberate — it
        /// makes the curve monotone in the budget, so a wave can never field tougher
        /// enemies than the wave after it.
        /// </summary>
        public static float HealthMultiplierForBudget(int budget)
        {
            int surplus = budget - FullRosterSpend;
            if (surplus <= 0)
            {
                return 1f;
            }
            float bonus = surplus / (float)FullRosterSpend;
            if (bonus > HealthSurplusCap)
            {
                bonus = HealthSurplusCap;
            }
            return 1f + bonus;
        }

        /// <summary>Elites the wave may field.</summary>
        public static int EliteAllowanceForBudget(int budget)
        {
            int allowance = budget / ElitePointCost;
            if (allowance < 0)
            {
                allowance = 0;
            }
            return allowance > EliteAllowanceCap ? EliteAllowanceCap : allowance;
        }

        /// <summary>
        /// Raw performance reading for the wave that just ended, in -3..+3. Positive
        /// means the player is ahead of the curve.
        /// </summary>
        public static int PerformanceDelta(float healthFraction, float waveSeconds, int hitsTaken)
        {
            int delta = 0;
            if (healthFraction >= HealthyFraction)
            {
                delta += 1;
            }
            else if (healthFraction < StruggleFraction)
            {
                delta -= 1;
            }

            if (waveSeconds <= FastWaveSeconds)
            {
                delta += 1;
            }
            else if (waveSeconds >= SlowWaveSeconds)
            {
                delta -= 1;
            }

            if (hitsTaken <= CleanHits)
            {
                delta += 1;
            }
            else if (hitsTaken >= BatteredHits)
            {
                delta -= 1;
            }
            return delta;
        }

        /// <summary>Band for the next wave, given the band in force and the wave outcome.</summary>
        public static int NextBand(int band, float healthFraction, float waveSeconds, int hitsTaken)
        {
            int delta = PerformanceDelta(healthFraction, waveSeconds, hitsTaken);
            if (delta > StepCap)
            {
                delta = StepCap;
            }
            else if (delta < -StepCap)
            {
                delta = -StepCap;
            }
            return ClampBand(ClampBand(band) + delta);
        }
    }

    /// <summary>
    /// AMENDMENT #14 §18 — graded drops and bad-luck protection. No RNG: the roll is
    /// an integer avalanche hash of (enemy id, wave, drop ordinal) and the guarantees
    /// come from two monotone counters.
    /// </summary>
    public static class LootGradeSpec
    {
        public const int GradeCount = 3;

        /// <summary>Roll space. A roll is always 0..99.</summary>
        public const int RollModulus = 100;
        /// <summary>Rolls at or above this are at least Fine (22 of 100).</summary>
        public const int FineThreshold = 70;
        /// <summary>Rolls at or above this are Epic (8 of 100).</summary>
        public const int EpicThreshold = 92;

        /// <summary>Consecutive Basic drops after which the next graded drop is forced
        /// to at least Fine. A run therefore never sees 6 Basics in a row.</summary>
        public const int FinePityLimit = 5;
        /// <summary>Consecutive non-Epic drops after which the next graded drop is
        /// forced to Epic. A run therefore never sees 19 non-Epics in a row.</summary>
        public const int EpicPityLimit = 18;

        /// <summary>Payload multiplier per grade, indexed by <see cref="LootGrade"/>.</summary>
        public static readonly float[] GradeValueMul = { 1.00f, 1.45f, 2.10f };

        /// <summary>Equipment-rank steps an equip shard grants, indexed by grade.</summary>
        public static readonly int[] GradeRankSteps = { 1, 1, 2 };

        /// <summary>
        /// A boss drop is always Epic and is deliberately OUTSIDE the pity ledger —
        /// it neither advances nor resets the counters. Pity is a statement about the
        /// grind, and a guaranteed drop must not be able to satisfy or reset it.
        /// </summary>
        public const LootGrade BossGrade = LootGrade.Epic;

        /// <summary>
        /// Deterministic 0..99 roll. Integer avalanche mix — no floating point, no
        /// RNG, no static state, so it is reproducible on every runtime.
        /// </summary>
        public static int Roll(int enemyId, int wave, int dropOrdinal)
        {
            unchecked
            {
                int hash = enemyId * 73856093;
                hash ^= wave * 19349663;
                hash ^= dropOrdinal * 83492791;
                hash ^= hash >> 13;
                hash *= 1274126177;
                hash ^= hash >> 16;
                return (hash & 0x7fffffff) % RollModulus;
            }
        }

        /// <summary>
        /// Grade for a roll under the current pity state. Epic pity outranks Fine
        /// pity, and both outrank the roll.
        /// </summary>
        public static LootGrade Resolve(int roll, int finePity, int epicPity)
        {
            if (epicPity >= EpicPityLimit)
            {
                return LootGrade.Epic;
            }
            if (roll >= EpicThreshold)
            {
                return LootGrade.Epic;
            }
            if (finePity >= FinePityLimit)
            {
                return LootGrade.Fine;
            }
            return roll >= FineThreshold ? LootGrade.Fine : LootGrade.Basic;
        }

        /// <summary>Folds a granted grade back into the ledger.</summary>
        public static void Advance(LootGrade granted, ref int finePity, ref int epicPity)
        {
            if (granted == LootGrade.Epic)
            {
                finePity = 0;
                epicPity = 0;
                return;
            }
            if (granted == LootGrade.Fine)
            {
                finePity = 0;
                epicPity += 1;
                return;
            }
            finePity += 1;
            epicPity += 1;
        }

        /// <summary>Payload multiplier for a grade, clamped to the table.</summary>
        public static float ValueMultiplier(LootGrade grade)
        {
            int index = (int)grade;
            if (index < 0)
            {
                index = 0;
            }
            else if (index >= GradeCount)
            {
                index = GradeCount - 1;
            }
            return GradeValueMul[index];
        }

        /// <summary>Equipment-rank steps for a grade, clamped to the table.</summary>
        public static int RankSteps(LootGrade grade)
        {
            int index = (int)grade;
            if (index < 0)
            {
                index = 0;
            }
            else if (index >= GradeCount)
            {
                index = GradeCount - 1;
            }
            return GradeRankSteps[index];
        }
    }

    /// <summary>
    /// View-facing read model for both amendments. Follows the
    /// <see cref="IRunPreparationSnapshot"/> / <see cref="IGrowthChoiceSnapshot"/>
    /// precedent: the frozen <see cref="IHackSnapshot"/> is not amended.
    /// </summary>
    public interface IDungeonProgressionSnapshot
    {
        /// <summary>True when AMENDMENT #13 is live for this run.</summary>
        bool AdaptiveWavesActive { get; }
        /// <summary>True when AMENDMENT #14 is live for this run.</summary>
        bool GradedLootActive { get; }
        /// <summary>Current DDA band, -2..+2. Always 0 when #13 is off.</summary>
        int DifficultyBand { get; }
        /// <summary>Band-scaled point budget the current wave was built from. 0 when off.</summary>
        int WaveBudget { get; }
        /// <summary>Elites the current wave is allowed to field. 0 when off.</summary>
        int WaveEliteAllowance { get; }
        /// <summary>Hits the player has taken in the current wave.</summary>
        int WaveHitsTaken { get; }
        /// <summary>Seconds elapsed in the current wave.</summary>
        float WaveElapsedSeconds { get; }
        /// <summary>Consecutive Basic drops on the ledger.</summary>
        int FinePity { get; }
        /// <summary>Consecutive non-Epic drops on the ledger.</summary>
        int EpicPity { get; }
        /// <summary>Grade of the most recent drop this run.</summary>
        LootGrade LastLootGrade { get; }
        /// <summary>Grade of each live pickup, index-aligned with <see cref="ISimSnapshot.Pickups"/>.</summary>
        IReadOnlyList<LootGrade> PickupGrades { get; }

        /// <summary>AMENDMENT #15: half-width the movement clamp actually uses.
        /// Equals <see cref="SimConfig.ArenaHalfWidth"/> when #15 is off.</summary>
        float BoundsHalfWidth { get; }

        /// <summary>AMENDMENT #15: half-height the movement clamp actually uses.
        /// Equals <see cref="SimConfig.ArenaHalfHeight"/> when #15 is off.</summary>
        float BoundsHalfHeight { get; }

        /// <summary>True when the clamp is running on expanded bounds.</summary>
        bool ExpandedBoundsActive { get; }
    }
}
