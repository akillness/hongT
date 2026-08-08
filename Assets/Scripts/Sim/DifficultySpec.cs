// AMENDMENT #11 — difficulty + cooperative enemy group AI. Numeric truth:
// docs/SIM_SPEC_HACKSLASH.md §16.
//
// NOT a frozen contract file: it is additive and every number here resolves to a
// no-op on Difficulty.Normal (value 0), which is what default(HackConfig) and every
// pre-amendment initializer produce. The golden dungeon digest therefore stays
// byte-identical on the Normal path.
//
// Pure C#. No UnityEngine references allowed in this assembly (asmdef enforces).
using System;

namespace CinderCourt.Sim
{
    /// <summary>
    /// Run difficulty (§16). Four tiers, review-driven: the axes are exactly the three
    /// a player can feel — incoming damage, enemy aggression, and how coordinated the
    /// pack is. <see cref="Normal"/> is 0 so it is the implicit default everywhere and
    /// reproduces the pre-amendment simulation exactly.
    /// </summary>
    public enum Difficulty
    {
        /// <summary>Pre-amendment baseline. Every multiplier is neutral, group AI off.</summary>
        Normal = 0,
        /// <summary>Story pace: softer hits, slower enemy cadence, group AI off.</summary>
        Story = 1,
        /// <summary>Group AI on: enemies surround, hold a ring, and take turns.</summary>
        Hard = 2,
        /// <summary>Group AI on, tighter ring, more simultaneous attackers.</summary>
        Nightmare = 3,
    }

    /// <summary>
    /// The resolved multiplier set for one <see cref="Difficulty"/>. A readonly struct so
    /// the sim can hold it by value with no per-tick allocation.
    /// </summary>
    public readonly struct DifficultyProfile
    {
        /// <summary>Multiplies damage the player receives (§16 A).</summary>
        public readonly float IncomingDamageMul;

        /// <summary>
        /// Multiplies the enemy attack cooldown (§16 B). Below 1 = more aggressive.
        /// </summary>
        public readonly float AttackCooldownMul;

        /// <summary>
        /// Max enemies allowed to be mid-attack at once (§16 C). 0 = unlimited, which is
        /// the pre-amendment behaviour: every enemy in range swings whenever its own
        /// cooldown allows.
        /// </summary>
        public readonly int AttackTokens;

        /// <summary>
        /// Group AI master switch (§16 C). When false the enemy steering is the
        /// pre-amendment straight chase plus separation, untouched.
        /// </summary>
        public readonly bool GroupAi;

        /// <summary>
        /// Holding-ring radius as a multiple of <see cref="SimConfig.EnemyAttackRange"/>
        /// (§16 D). Enemies without an attack token orbit to their slot on this ring
        /// instead of piling onto the player.
        /// </summary>
        public readonly float RingRadiusMul;

        /// <summary>
        /// Token priority bias for enemies that are NOT in front of the player (§16 E).
        /// Their distance is scaled by this before sorting, so flankers get to swing
        /// first and hits arrive from the side/back. 1 = no bias.
        /// </summary>
        public readonly float FlankBias;

        public DifficultyProfile(
            float incomingDamageMul,
            float attackCooldownMul,
            int attackTokens,
            bool groupAi,
            float ringRadiusMul,
            float flankBias)
        {
            IncomingDamageMul = incomingDamageMul;
            AttackCooldownMul = attackCooldownMul;
            AttackTokens = attackTokens;
            GroupAi = groupAi;
            RingRadiusMul = ringRadiusMul;
            FlankBias = flankBias;
        }
    }

    /// <summary>
    /// The §16 numeric table. Adjectives do not clear the gate; these numbers do.
    /// </summary>
    public static class DifficultySpec
    {
        /// <summary>Number of selectable tiers (§16). Matches <see cref="Difficulty"/>.</summary>
        public const int Count = 4;

        /// <summary>
        /// Surround slots on the holding ring (§16 D). Eight, deliberately matching the
        /// eight fixed spawn points, and a power of two so <c>id &amp; 7</c> distributes
        /// ids evenly with no modulo bias.
        /// </summary>
        public const int RingSlots = 8;

        /// <summary>
        /// Radial tolerance in world units before a ring-holding enemy is considered
        /// parked and drops to <see cref="ActorAction.Idle"/> (§16 D).
        /// </summary>
        public const float RingArriveTolerance = 16f;

        /// <summary>
        /// Forward test threshold reused from the frozen numeric contract: a target with
        /// <c>dx * facing &gt;= -18</c> counts as in front (CLAUDE.md §2).
        /// </summary>
        public const float ForwardThreshold = -18f;

        // Display order for a selection UI: easiest to hardest. Difficulty values are
        // NOT in difficulty order (Normal must be 0), so the order lives here instead of
        // being inferred from the enum.
        private static readonly Difficulty[] Ordered =
        {
            Difficulty.Story,
            Difficulty.Normal,
            Difficulty.Hard,
            Difficulty.Nightmare,
        };

        /// <summary>Easiest-to-hardest tier order for selection surfaces (§16).</summary>
        public static Difficulty AtOrder(int order) =>
            Ordered[order < 0 ? 0 : (order >= Ordered.Length ? Ordered.Length - 1 : order)];

        /// <summary>Position of <paramref name="difficulty"/> in <see cref="AtOrder"/>.</summary>
        public static int OrderOf(Difficulty difficulty)
        {
            for (int index = 0; index < Ordered.Length; index += 1)
            {
                if (Ordered[index] == difficulty)
                {
                    return index;
                }
            }
            return 1; // Normal
        }

        /// <summary>
        /// Stable id for persistence and deep links (§16). Lowercase, never localized.
        /// </summary>
        public static string IdOf(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Story: return "story";
                case Difficulty.Hard: return "hard";
                case Difficulty.Nightmare: return "nightmare";
                default: return "normal";
            }
        }

        /// <summary>
        /// Parses <see cref="IdOf"/>. Unknown / null / empty resolves to
        /// <see cref="Difficulty.Normal"/> so a missing save key migrates silently.
        /// </summary>
        public static Difficulty Parse(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return Difficulty.Normal;
            }
            switch (id.Trim().ToLowerInvariant())
            {
                case "story": return Difficulty.Story;
                case "hard": return Difficulty.Hard;
                case "nightmare": return Difficulty.Nightmare;
                default: return Difficulty.Normal;
            }
        }

        /// <summary>
        /// The §16 table. Out-of-range values resolve to <see cref="Difficulty.Normal"/>,
        /// so a corrupted save can never produce an undefined simulation.
        /// </summary>
        public static DifficultyProfile For(Difficulty difficulty)
        {
            switch (difficulty)
            {
                //                          incoming  cooldown  tokens  groupAi  ring   flank
                case Difficulty.Story:
                    return new DifficultyProfile(0.65f, 1.22f, 2, false, 1.00f, 1.00f);
                case Difficulty.Hard:
                    return new DifficultyProfile(1.35f, 0.84f, 3, true, 1.55f, 0.75f);
                case Difficulty.Nightmare:
                    return new DifficultyProfile(1.70f, 0.70f, 4, true, 1.35f, 0.75f);
                default:
                    return new DifficultyProfile(1.00f, 1.00f, 0, false, 1.00f, 1.00f);
            }
        }

        /// <summary>
        /// Ring slot for an enemy id (§16 D). Stable for the life of the enemy — it does
        /// NOT depend on how many enemies are alive this tick, so a kill never makes the
        /// survivors jitter to new angles.
        /// </summary>
        public static int RingSlotOf(int enemyId) => (enemyId & (RingSlots - 1));

        /// <summary>
        /// World-space holding position for <paramref name="enemyId"/> around the player
        /// (§16 D). The Y radius is divided by <see cref="SimConfig.IsoY"/> so the ring
        /// reads as a circle in the isometric projection, matching the frozen
        /// <c>hypot(dx, dy*1.42)</c> combat metric.
        /// </summary>
        public static void RingTarget(
            int enemyId,
            float playerX,
            float playerY,
            float radius,
            out float targetX,
            out float targetY)
        {
            int slot = RingSlotOf(enemyId);
            double angle = 2.0 * Math.PI * slot / RingSlots;
            targetX = playerX + radius * (float)Math.Cos(angle);
            targetY = playerY + radius * (float)Math.Sin(angle) / SimConfig.IsoY;
        }
    }
}
