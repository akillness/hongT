// Additive only: input-depth §5. The frozen IHackSnapshot contract is not
// amended — this follows the IRunPreparationSnapshot precedent in
// RunPreparationSnapshot.cs, which exists for exactly this reason.
// Pure C#. No UnityEngine references allowed in this assembly (asmdef enforces).
namespace CinderCourt.Sim
{
    /// <summary>Which axis a level-up choice raises. Values are the 1..3 the
    /// player presses, so the key and the enum never drift apart.</summary>
    public enum GrowthChoiceKind
    {
        None = 0,
        Attack = 1,
        Vitality = 2,
        Swiftness = 3,
    }

    /// <summary>
    /// Additive read seam for the level-up offer (input depth §5).
    ///
    /// Levelling used to apply its stats silently — there was no player choice
    /// anywhere in the growth path. An offer now opens on level-up and the sim
    /// KEEPS RUNNING while it is open; ignoring it costs nothing because it
    /// auto-confirms to the old automatic distribution.
    /// </summary>
    public interface IGrowthChoiceSnapshot
    {
        /// <summary>True while an offer is waiting for 1/2/3.</summary>
        bool GrowthOfferOpen { get; }

        /// <summary>Seconds left before the offer auto-confirms. Drives the
        /// countdown the HUD draws; 0 when no offer is open.</summary>
        float GrowthOfferTime { get; }

        /// <summary>The last choice actually applied, so the HUD can show what
        /// the player got — including on an auto-confirm they never saw.</summary>
        GrowthChoiceKind LastGrowthChoice { get; }

        /// <summary>Points banked into each axis this run. Index 0/1/2 =
        /// attack/vitality/swiftness.</summary>
        int GrowthAttack { get; }
        int GrowthVitality { get; }
        int GrowthSwiftness { get; }
    }
}
