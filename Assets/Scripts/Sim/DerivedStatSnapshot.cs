// AMENDMENT #9 — in-game codex (design/ingame-guidance-spec.md).
//
// Additive only. The frozen IHackSnapshot contract is not amended — this
// follows the IRunPreparationSnapshot and IGrowthChoiceSnapshot precedents,
// which exist for exactly this reason.
// Pure C#. No UnityEngine references allowed in this assembly (asmdef enforces).
namespace CinderCourt.Sim
{
    /// <summary>
    /// Additive read seam for the stats the sim recomputes DURING a run.
    ///
    /// The lobby shows run-START values: <see cref="HackConfig.PlayerDamage"/>
    /// and friends fold meta stats and equip ranks, and know nothing of the
    /// level curve, the extraction buff, or banked growth points. Once a run
    /// begins, <c>ApplyLevelStats()</c> recomputes all four on every level-up,
    /// extraction and growth choice — and until this seam existed, nothing
    /// outside the sim could read the result. A player at level 8 with two
    /// extractions had no way to learn their own attack power.
    ///
    /// Every member here is a field the sim ALREADY maintains. Reading them
    /// adds no arithmetic, so the golden digest cannot move.
    ///
    /// Deliberately absent: GrowthAttack / GrowthVitality / GrowthSwiftness.
    /// <see cref="IGrowthChoiceSnapshot"/> already declares those three, and
    /// two interfaces asserting ownership of one value is two sources that can
    /// diverge silently. A codex row that needs both casts both.
    /// </summary>
    public interface IDerivedStatSnapshot
    {
        /// <summary>Attack power right now: base x level x extraction x growth.</summary>
        float PlayerDamage { get; }

        /// <summary>Max health right now: base + level + growth.</summary>
        float PlayerMaxHealth { get; }

        /// <summary>Move speed right now: base x growth.</summary>
        float PlayerSpeed { get; }

        /// <summary>Oil regen per second right now: base + level.</summary>
        float LanternRegenPerSecond { get; }

        /// <summary>
        /// Accumulated extraction damage bonus, +<see cref="HackSpec.ExtractionDamageBonus"/>
        /// per successful channel. Nothing outside the sim can derive this —
        /// it is run-scoped history, not a function of any config.
        /// </summary>
        float ExtractionBonus { get; }

        /// <summary>
        /// Run-start damage before the level curve, the extraction buff and
        /// growth points. Exposed so a breakdown can show where the number
        /// came from without dividing the product back apart: float division
        /// against a value produced by multiplication is not guaranteed to
        /// round-trip, and a reconstructed base on screen would be a number
        /// the sim never held.
        /// </summary>
        float BaseDamage { get; }

        /// <summary>Run-start max health, before level and growth.</summary>
        float BaseMaxHealth { get; }

        /// <summary>Run-start move speed, before growth.</summary>
        float BaseSpeed { get; }

        /// <summary>Run-start oil regen, before the level curve.</summary>
        float BaseLanternRegen { get; }

        /// <summary>
        /// Meta points and equip ranks folded INTO the base values above.
        ///
        /// Without these the codex can only say "your attack is 72.8, which
        /// comes from 72.8" on a fresh run — true, and useless. These let the
        /// breakdown reach all the way back to the sim constant, so the line
        /// reads `58 × 특성4(+12%) × 무기2(+12%)` and every term names
        /// something the player actually did.
        /// </summary>
        int MetaAttack { get; }
        int MetaVitality { get; }
        int MetaSwiftness { get; }
        int WeaponRank { get; }
        int LanternRank { get; }
        int CloakRank { get; }
    }
}
