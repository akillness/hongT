// AMENDMENT #13 (W4 — point-budget waves + DDA), AMENDMENT #14 (W5 — graded
// loot + bad-luck protection) and AMENDMENT #16 (W6 — boss archetype variety).
// Numeric truth: docs/SIM_SPEC_HACKSLASH.md §17/§18/§20.
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
    /// Opt-in switches for dungeon-only progression amendments. Every switch
    /// defaults to <c>false</c>, which is what every pre-amendment call site
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

        /// <summary>
        /// AMENDMENT #16 §20: give the stage boss an archetype instead of letting
        /// every boss share one phase table. Off means <see cref="BossArchetype.None"/>
        /// everywhere, which resolves to the frozen §7 vectors exactly.
        /// </summary>
        public bool BossVariety;

        /// <summary>
        /// AMENDMENT #18: let a no-target companion preserve its local positioning
        /// and deterministic idle route, recovering only after it leaves the player's
        /// cohesion radius. Off retains the frozen hard-anchor follower exactly.
        /// </summary>
        public bool CompanionCohesion;

        /// <summary>True when at least one amendment is live.</summary>
        public bool Any => AdaptiveWaves || GradedLoot || Bounds.Active || BossVariety || CompanionCohesion;

        /// <summary>#13 + #14 only. Bounds stay frozen — the movement amendment has
        /// a hard View coupling (the boundary wall ring), so it is opted into
        /// separately rather than riding along. #16 is excluded for the same reason:
        /// its telegraph rhythm is only readable once the View differentiates the
        /// telegraph, so turning it on blind would change the fight without changing
        /// what the player sees. <c>All</c> is deliberately NOT amended by #16.</summary>
        public static DungeonProgressionConfig All => new DungeonProgressionConfig
        {
            AdaptiveWaves = true,
            GradedLoot = true,
        };

        /// <summary>#13 + #14 + #15 + #16 + #18 at their production settings.</summary>
        public static DungeonProgressionConfig Everything => new DungeonProgressionConfig
        {
            AdaptiveWaves = true,
            GradedLoot = true,
            Bounds = DungeonBoundsSpec.Expanded,
            BossVariety = true,
            CompanionCohesion = true,
        };
    }

    /// <summary>
    /// AMENDMENT #18 — deterministic no-target companion autonomy. These values
    /// are opt-in through <see cref="DungeonProgressionConfig.CompanionCohesion"/>;
    /// default progression still takes the untouched hard-anchor follower path.
    /// </summary>
    public static class CompanionCohesionSpec
    {
        /// <summary>
        /// Player-relative iso distance at or below which a recovery latch releases.
        /// It covers the 80 px follow offset plus every D6.4 fan-out.
        public const float ComfortRadius = 128f;

        /// <summary>
        /// Player-relative iso distance that interrupts idle wandering and starts
        /// recovery. Separation from <see cref="ComfortRadius"/> prevents edge churn.
        /// </summary>
        public const float RecoveryRadius = 200f;

        /// <summary>
        /// Recovery outruns a walking player without the snap of a teleport.
        /// </summary>
        public const float RecoverySpeedScale = 1.25f;

        /// <summary>One deterministic idle leg in world pixels.</summary>
        public const float WanderStride = 24f;

        /// <summary>Still time before starting the next idle leg.</summary>
        public const float WanderDwellSeconds = 0.35f;
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
        /// [TARGET] Expanded half-width, 520 × 1.4135.
        ///
        /// AMENDMENT #17 (_workspace/current/design/dungeon-interior-spec.md) RAISED this
        /// from 554. #15's ceiling was 554 because the gimmick geometry was frozen: the
        /// ash wall and every tide current covered exactly x 248..1288, so a wider
        /// playfield would have let the player stand past the ash wall's edge and the
        /// gimmick would still fire without being a threat. **#17 moves the gimmicks with
        /// the bounds** (<see cref="CampaignSpec.WallEdgeX"/> 33 /
        /// <see cref="CampaignSpec.WallEdgeRightX"/> 1503 /
        /// <see cref="CampaignSpec.CurrentHalfW"/> 735), so that ceiling is gone and the
        /// binding constraints are now the two that cannot be moved by editing constants:
        ///
        ///   1. Painted plate. It reaches sim x ±850 around the arena centre
        ///      (EnvironmentBuilder.cs:1066). The boundary wall ring stands at e 1.02,
        ///      i.e. x 18..1518 — inside the plate with 100 px to spare.
        ///   2. Camera frame. The frustum half-width at the focus plane is
        ///      D·tan21°·1.5 and this playfield half-width is 735 · ViewWorld.Scale,
        ///      so the frame lands at e = D·tan21°·1.5 / (735·Scale). This ratio is
        ///      INVARIANT under the 2026-08 movement-area enlargement because Scale
        ///      (0.0125 → 0.0150) and the calm distance D (17.5 → 21.0) both grew by
        ///      ×1.2 and cancel: 17.5·tan21°·1.5 / (735·0.0125) = 21.0·tan21°·1.5 /
        ///      (735·0.0150) = e 1.097, so the wall ring at e 1.02 keeps its 7.5%
        ///      margin. **This is why 735 and not more** — a larger bound raises e
        ///      toward 1.011 and clips the wall regardless of the Scale/distance pair,
        ///      because the wall ring's e is fixed by geometry, not by the quotient.

        ///
        /// [OBSERVED] <c>SimConfig.WorldWidth</c>/<c>WorldHeight</c> do NOT constrain this.
        /// They are referenced nowhere outside their own definition (repo-wide grep over
        /// Assets/Scripts and Assets/Tests) — nominal constants, not a clamp.
        /// </summary>
        public const float ExpandedHalfWidth = 735f;

        /// <summary>
        /// [TARGET] Expanded half-height, 270 × 1.4444. Still bounded by the painted
        /// backdrop plate (sim y 0..1024, arena centre y 604 → only 420 below centre),
        /// which is why this axis grows less than x even though no gimmick constrains it.
        ///
        /// LOWERED from #15's 418 on purpose. 390 is not a tighter reading of the plate —
        /// it is the value that makes the AREA ratio land on the contracted 2×:
        /// π·735·390 / π·520·270 = 2.0417. 418 would give 2.188× with no extra design
        /// value, while spending the plate's entire bottom margin (y 1022 against the
        /// plate edge 1024) on an axis the camera reads foreshortened at 55° pitch.
        /// At 390 the enemy ring (margin 24) sits at y 238..970, 54 px clear of the plate.
        /// </summary>
        public const float ExpandedHalfHeight = 390f;

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

        // --- §17.4 campaign progression term (2026-08-10 balance pass) ---
        /// <summary>Permille added to the budget per campaign stage index.
        ///
        /// MEASURED, and it corrects a sag the DDA band cannot reach. Player
        /// power compounds across a campaign (meta stats to 10, equipment to
        /// rank 5) while the wave budget is stage-blind:
        ///     fully built player   damage 1.69x, health 2.20x -> 3.72x
        ///     enemy load s0 -> s8                              -> 2.49x
        /// so the last stage runs at 0.67x the relative difficulty of the
        /// first. The campaign gets EASIER as it goes.
        ///
        /// The DDA band is not the fix: it multiplies every stage by the same
        /// 1.25x at its ceiling, which shifts the curve without changing its
        /// slope (0.67 -> 0.80, still sagging). A per-stage term is the missing
        /// input.
        ///
        /// 90 permille per anchor index. The sim knows the SIM ANCHOR index
        /// (0..5), not the catalog index (0..8) — catalog pairs share an
        /// anchor — so the ramp is 1.00 / 1.09 / 1.18 / 1.27 / 1.36 / 1.45 and
        /// the paired stage inherits its partner's step. That leaves stage 8 at
        /// 2.49 * 1.45 = 3.61x against a 3.72x player: 0.97 relative, flat
        /// within the resolution the anchor mapping allows.
        ///
        /// Dungeon-only by construction: this whole spec is gated behind
        /// DungeonProgressionConfig, which zeroes for arena, prologue and the
        /// classic campaign anchors, so no golden digest can see it.</summary>
        public const int StageProgressionPermille = 90;

        /// <summary>Budget scaled by the DDA band and the campaign stage.</summary>
        public static int EffectiveBudget(int wave, int band, int stageIndex)
        {
            int stage = stageIndex < 0 ? 0 : stageIndex;
            int stagePermille = 1000 + stage * StageProgressionPermille;
            long scaled = (long)BaseBudget(wave) * BandMultiplierPermille(band) / 1000;
            return (int)(scaled * stagePermille / 1000);
        }

        /// <summary>Band-scaled budget with no campaign term — the arena and
        /// prologue shape, kept so existing callers and tests read unchanged.</summary>
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
    /// AMENDMENT #16 §20 — which boss this is. <see cref="None"/> is the frozen
    /// behaviour (one shared phase table) and is what an ungated run, a
    /// non-dungeon run, or an unmapped stage id resolves to. The three stage
    /// archetypes and the one final-boss archetype are named after the boss
    /// display names already in <c>StageCatalog.BossPresentation</c> — "Cinder
    /// Warden", "Veil Tactician", "Gate Sovereign" — so the sim archetype, the
    /// HUD name and the asset lane's GLB (s1-cinder-warden / s2-veil-tactician /
    /// s3-gate-sovereign) can never disagree about which boss is on screen.
    /// </summary>
    public enum BossArchetype
    {
        /// <summary>Frozen §7 behaviour. Numerically identical to a pre-#16 run.</summary>
        None = 0,
        /// <summary>Heavy two-phase bruiser. Slow, long telegraph, huge reach.</summary>
        Warden = 1,
        /// <summary>Fast three-phase summoner. Short telegraph, short reach, escorts.</summary>
        Tactician = 2,
        /// <summary>Three-phase pattern-shifter. Every axis moves at every boundary.</summary>
        Sovereign = 3,
        /// <summary>Final boss. The frozen three-phase curve, uniformly reinforced.</summary>
        Monarch = 4,
    }

    /// <summary>
    /// AMENDMENT #16 §20 — one boss's per-phase numbers. Every vector is indexed
    /// 0/1/2 for P1/P2/P3 and is exactly <see cref="BossVarietySpec.MaxPhases"/>
    /// long, so a caller can index it with the same clamped phase index the
    /// frozen <c>HackSpec</c> vectors use. A two-phase archetype repeats its P2
    /// values in the P3 slot: the slot is unreachable (<see cref="PhaseCount"/>
    /// clamps the index), and repeating rather than zeroing means a future
    /// off-by-one reads a plausible number instead of a boss that stops moving.
    /// </summary>
    public sealed class BossArchetypeProfile
    {
        public readonly BossArchetype Archetype;

        /// <summary>Live phases, 2 or 3. The phase index never exceeds this minus one.</summary>
        public readonly int PhaseCount;

        /// <summary>Health fraction at or below which the boss enters P2.</summary>
        public readonly float Phase2Fraction;

        /// <summary>Health fraction at or below which the boss enters P3.
        /// Ignored when <see cref="PhaseCount"/> is 2.</summary>
        public readonly float Phase3Fraction;

        /// <summary>Multiplier on the swing cooldown. Below 1 = faster cadence.</summary>
        public readonly float[] CadenceMul;

        /// <summary>Multiplier on move speed, replacing <c>HackSpec.BossSpeedMul</c>.</summary>
        public readonly float[] SpeedMul;

        /// <summary>Multiplier on contact reach, replacing <c>HackSpec.BossRangeMul</c>.</summary>
        public readonly float[] RangeMul;

        /// <summary>Multiplier on contact damage. P1 is 1.00 on the frozen profile,
        /// which is what makes <see cref="BossArchetype.None"/> reproduce the
        /// "no multiplier before phase 2" frozen clause exactly.</summary>
        public readonly float[] DamageMul;

        /// <summary>Attack-clip frame the contact lands on — the telegraph. Larger =
        /// longer windup. Frozen is 2 of a 5-frame clip at 12 fps.</summary>
        public readonly int[] ContactFrame;

        /// <summary>Escorts summoned when the boss ENTERS that phase. Index 0 is
        /// unused (a boss does not summon on spawn) and is always 0.</summary>
        public readonly int[] PhaseEscorts;

        /// <summary>Multiplier on the boss's spawn health, on top of the frozen
        /// <c>SimConfig.BossHealthMul</c> × <c>HackSpec.DungeonBossHealthMul</c>.</summary>
        public readonly float HealthMul;

        public BossArchetypeProfile(
            BossArchetype archetype,
            int phaseCount,
            float phase2Fraction,
            float phase3Fraction,
            float[] cadenceMul,
            float[] speedMul,
            float[] rangeMul,
            float[] damageMul,
            int[] contactFrame,
            int[] phaseEscorts,
            float healthMul)
        {
            Archetype = archetype;
            PhaseCount = phaseCount;
            Phase2Fraction = phase2Fraction;
            Phase3Fraction = phase3Fraction;
            CadenceMul = cadenceMul;
            SpeedMul = speedMul;
            RangeMul = rangeMul;
            DamageMul = damageMul;
            ContactFrame = contactFrame;
            PhaseEscorts = phaseEscorts;
            HealthMul = healthMul;
        }

        /// <summary>Telegraph duration in seconds for a 0-based phase index.</summary>
        public float TelegraphSeconds(int phaseIndex) =>
            ContactFrame[BossVarietySpec.ClampPhaseIndex(this, phaseIndex)] / BossVarietySpec.AttackClipFps;
    }

    /// <summary>
    /// AMENDMENT #16 §20 — the archetype table and the stage→archetype mapping.
    ///
    /// §13 (determinism) is NOT amended. There is no RNG and no hash here: the
    /// mapping is a static string table keyed on the stage id the run was
    /// constructed with, and every per-phase number is a table lookup on an
    /// integer phase index. The same (config, input sequence) therefore produces
    /// the same run, which is the same guarantee #13/#14/#15 gave.
    /// </summary>
    public static class BossVarietySpec
    {
        /// <summary>Vector length of every per-phase array.</summary>
        public const int MaxPhases = 3;

        // These three mirror the attack-clip constants in CinderSim.cs:46-50
        // (AttackClipFrames 5, AttackClipFps 12, EnemyContactFrame 2). They are
        // restated here because the telegraph axis is a CONTRACT number this
        // amendment owns, and the profile table has to be validatable without
        // reaching into the sim's privates.
        public const float AttackClipFps = 12f;
        /// <summary>Earliest legal contact frame. Frame 0 would land damage on the
        /// same tick the swing starts — no telegraph at all.</summary>
        public const int MinContactFrame = 1;
        /// <summary>Latest legal contact frame. The clip is 5 frames, so frame 5
        /// ends it before contact and the boss would never damage anything.</summary>
        public const int MaxContactFrame = 4;

        // --- §20.2 the archetype table (the numeric gate) --------------------
        //
        // Read the columns as "what this boss makes the player do":
        //   Warden    — stand off, wait out a 0.25 s windup, punish the recovery.
        //               Two phases only: the fight is short and heavy, not a
        //               three-act structure. Reach 1.34/1.48 is the widest in the
        //               table, so sidestepping is not enough — you have to leave.
        //   Tactician — never stops swinging (cadence 0.72 → 0.54) but each swing
        //               is cheap (damage 0.84 → 1.06) and the windup is one frame.
        //               Threat comes from the escorts it calls at both boundaries
        //               and from a 1.74x closing speed, not from the hits.
        //   Sovereign — every one of the five axes moves at every boundary; the
        //               telegraph in particular walks 3 → 2 → 1 frames, so the
        //               read the player learned in P1 is wrong twice.
        //   Monarch   — the frozen curve, reinforced. Cadence 1.00/0.85/0.72
        //               tracks the ratio of the frozen HackSpec.BossAttackInterval
        //               vector (1.37/1.16/0.99 → 1.000/0.847/0.723), which is the
        //               first time those declared-but-unconsumed constants shape
        //               anything. Escorts stay 3-at-P2, matching
        //               HackSpec.MonarchPhase2Escorts.
        //
        // Within every archetype all five axes are monotone in the direction of
        // "harder" across its live phases: cadence and contact frame never rise,
        // speed, reach and damage never fall. A boss can therefore never get
        // easier by losing health, which is the invariant the frozen §7 table had
        // and the one BossVarietyProfileTests pins.

        private static readonly BossArchetypeProfile FrozenProfile = new BossArchetypeProfile(
            BossArchetype.None,
            phaseCount: 3,
            phase2Fraction: 0.50f,   // HackSpec.BossPhase2HealthFraction
            phase3Fraction: 0.20f,   // HackSpec.BossPhase3HealthFraction
            cadenceMul: new[] { 1.00f, 1.00f, 1.00f },
            speedMul: new[] { 1.00f, 1.25f, 1.45f },    // HackSpec.BossSpeedMul
            rangeMul: new[] { 1.00f, 1.10f, 1.20f },    // HackSpec.BossRangeMul
            damageMul: new[] { 1.00f, 1.25f, 1.45f },   // 1 / BossPhase2DamageMul / BossPhase3DamageMul
            contactFrame: new[] { 2, 2, 2 },            // CinderSim.EnemyContactFrame
            phaseEscorts: new[] { 0, 0, 0 },            // the monarch-visual clause handles the frozen path
            healthMul: 1.00f);

        private static readonly BossArchetypeProfile WardenProfile = new BossArchetypeProfile(
            BossArchetype.Warden,
            phaseCount: 2,
            phase2Fraction: 0.55f,
            phase3Fraction: 0.55f,   // unreachable; equals P2 so an off-by-one is inert
            cadenceMul: new[] { 1.55f, 1.34f, 1.34f },
            speedMul: new[] { 0.82f, 0.96f, 0.96f },
            rangeMul: new[] { 1.34f, 1.48f, 1.48f },
            damageMul: new[] { 1.34f, 1.72f, 1.72f },
            contactFrame: new[] { 3, 3, 3 },
            phaseEscorts: new[] { 0, 0, 0 },
            healthMul: 1.28f);

        private static readonly BossArchetypeProfile TacticianProfile = new BossArchetypeProfile(
            BossArchetype.Tactician,
            phaseCount: 3,
            phase2Fraction: 0.72f,
            phase3Fraction: 0.38f,
            cadenceMul: new[] { 0.72f, 0.62f, 0.54f },
            speedMul: new[] { 1.30f, 1.52f, 1.74f },
            rangeMul: new[] { 0.90f, 0.95f, 1.00f },
            damageMul: new[] { 0.84f, 0.94f, 1.06f },
            contactFrame: new[] { 1, 1, 1 },
            phaseEscorts: new[] { 0, 3, 2 },
            healthMul: 0.78f);

        private static readonly BossArchetypeProfile SovereignProfile = new BossArchetypeProfile(
            BossArchetype.Sovereign,
            phaseCount: 3,
            phase2Fraction: 0.66f,
            phase3Fraction: 0.33f,
            cadenceMul: new[] { 1.12f, 0.90f, 0.68f },
            speedMul: new[] { 1.00f, 1.28f, 1.60f },
            rangeMul: new[] { 1.06f, 1.16f, 1.26f },
            damageMul: new[] { 1.00f, 1.22f, 1.48f },
            contactFrame: new[] { 3, 2, 1 },
            phaseEscorts: new[] { 0, 1, 2 },
            healthMul: 1.00f);

        private static readonly BossArchetypeProfile MonarchProfile = new BossArchetypeProfile(
            BossArchetype.Monarch,
            phaseCount: 3,
            phase2Fraction: 0.50f,
            phase3Fraction: 0.20f,
            cadenceMul: new[] { 1.00f, 0.85f, 0.72f },
            speedMul: new[] { 1.05f, 1.32f, 1.55f },
            rangeMul: new[] { 1.00f, 1.10f, 1.22f },
            damageMul: new[] { 1.05f, 1.32f, 1.58f },
            contactFrame: new[] { 2, 2, 1 },
            phaseEscorts: new[] { 0, 3, 0 },   // HackSpec.MonarchPhase2Escorts
            healthMul: 1.15f);

        /// <summary>Indexed by <c>(int)BossArchetype</c>, so the enum and the table
        /// can never drift apart.</summary>
        private static readonly BossArchetypeProfile[] Table =
        {
            FrozenProfile, WardenProfile, TacticianProfile, SovereignProfile, MonarchProfile,
        };

        /// <summary>Every archetype in table order, <see cref="BossArchetype.None"/> first.</summary>
        public static IReadOnlyList<BossArchetypeProfile> Profiles => Table;

        /// <summary>Profile for an archetype. An out-of-range value resolves to the
        /// frozen profile rather than throwing — a bad enum must degrade to the
        /// pre-amendment fight, not kill the run.</summary>
        public static BossArchetypeProfile For(BossArchetype archetype)
        {
            int index = (int)archetype;
            return index < 0 || index >= Table.Length ? FrozenProfile : Table[index];
        }

        // --- §20.3 stage → archetype mapping ---------------------------------
        //
        // Keyed on the stage id the run was constructed with. The sim is handed
        // StageCatalog's SimAnchorId (one of the six CampaignStages ids), so the
        // six anchor rows are the ones that actually fire. The three
        // logical-only ids are listed too, each carrying its anchor's archetype,
        // so a call site that ever passes a LOGICAL id lands on the same boss
        // instead of falling off the table into None:
        //
        //   ember-gallery -> cinder-span    (Warden)
        //   witness-well  -> abyss-chancel  (Tactician)
        //   ash-verdict   -> echo-throne    (Sovereign)
        //
        // Assignment follows the boss display names already in the catalog:
        // "Cinder Warden" (cinder-span, ember-gallery), "Veil Tactician"
        // (abyss-chancel, witness-well), "Gate Sovereign" (echo-throne,
        // ash-verdict). The two cycle-2 anchors take the archetype their own
        // name and identity gimmick imply — "Sluice Keeper" fights in a current
        // on the dash stage (Tactician), "Bastion Sentinel" holds a wall on the
        // ward stage (Warden). ash-march is the last stage in campaign order, so
        // it takes the one final-boss archetype.
        private readonly struct StageArchetype
        {
            public readonly string StageId;
            public readonly BossArchetype Archetype;

            public StageArchetype(string stageId, BossArchetype archetype)
            {
                StageId = stageId;
                Archetype = archetype;
            }
        }

        private static readonly StageArchetype[] StageTable =
        {
            // --- sim anchors (CampaignStages.Ids order) ---
            new StageArchetype("cinder-span", BossArchetype.Warden),
            new StageArchetype("abyss-chancel", BossArchetype.Tactician),
            new StageArchetype("echo-throne", BossArchetype.Sovereign),
            new StageArchetype("cinder-sluice", BossArchetype.Tactician),
            new StageArchetype("ember-bastion", BossArchetype.Warden),
            new StageArchetype("ash-march", BossArchetype.Monarch),
            // --- logical-only StageCatalog ids, aliased to their anchor ---
            new StageArchetype("ember-gallery", BossArchetype.Warden),
            new StageArchetype("witness-well", BossArchetype.Tactician),
            new StageArchetype("ash-verdict", BossArchetype.Sovereign),
        };

        /// <summary>Number of mapped stage ids.</summary>
        public static int MappedStageCount => StageTable.Length;

        /// <summary>Mapped stage id at a table position, for validation.</summary>
        public static string MappedStageIdAt(int index) => StageTable[index].StageId;

        /// <summary>
        /// Archetype for a stage id. An unknown or empty id resolves to
        /// <see cref="BossArchetype.None"/> — the frozen fight — because an
        /// unmapped stage is exactly the case where guessing would be a silent
        /// balance change.
        /// </summary>
        public static BossArchetype ArchetypeFor(string stageId)
        {
            if (string.IsNullOrEmpty(stageId))
            {
                return BossArchetype.None;
            }
            for (int index = 0; index < StageTable.Length; index += 1)
            {
                if (string.Equals(StageTable[index].StageId, stageId, StringComparison.Ordinal))
                {
                    return StageTable[index].Archetype;
                }
            }
            return BossArchetype.None;
        }

        // --- §20.4 phase resolution ------------------------------------------

        /// <summary>Clamps a 0-based phase index into a profile's live range.</summary>
        public static int ClampPhaseIndex(BossArchetypeProfile profile, int phaseIndex)
        {
            if (phaseIndex < 0)
            {
                return 0;
            }
            int last = profile.PhaseCount - 1;
            return phaseIndex > last ? last : phaseIndex;
        }

        /// <summary>
        /// 0-based phase index for a health fraction. Same shape as
        /// <c>HackSpec.BossPhaseIndexFor</c> — thresholds are inclusive and the
        /// result is clamped to the archetype's live phase count, which is what
        /// makes a two-phase boss a two-phase boss.
        /// </summary>
        public static int PhaseIndexFor(BossArchetype archetype, float healthFraction)
        {
            BossArchetypeProfile profile = For(archetype);
            int index = 0;
            if (profile.PhaseCount >= 3 && healthFraction <= profile.Phase3Fraction)
            {
                index = 2;
            }
            else if (healthFraction <= profile.Phase2Fraction)
            {
                index = 1;
            }
            return ClampPhaseIndex(profile, index);
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

        // --- AMENDMENT #16 §20.5 ---------------------------------------------

        /// <summary>True when this run's boss is running on an archetype profile.
        /// False means the frozen §7 fight, which is what an ungated run gives.</summary>
        bool BossVarietyActive { get; }

        /// <summary>Archetype resolved from the run's stage id.
        /// <see cref="BossArchetype.None"/> when #16 is off or the stage is unmapped.</summary>
        BossArchetype BossArchetype { get; }

        /// <summary>Live phase count for this run's boss — 2 for a Warden, 3 for
        /// everyone else. The View sizes its phase pips from this instead of
        /// assuming three.</summary>
        int BossPhaseCount { get; }

        /// <summary>Windup of the boss's CURRENT phase, in seconds. This is the
        /// telegraph the View has to draw for: a Warden's 0.25 s and a Tactician's
        /// 0.083 s cannot share one ring animation length.</summary>
        float BossTelegraphSeconds { get; }
    }
}
