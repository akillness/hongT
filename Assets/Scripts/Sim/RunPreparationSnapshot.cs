namespace CinderCourt.Sim
{
    /// <summary>One temporary Ember Rest choice. It never represents permanent campaign meta.</summary>
    public enum PreparationOfferKind
    {
        None = 0,
        Stat = 1,
        SkillRune = 2,
        GuardianResonance = 3,
    }

    /// <summary>Value-only, deterministic offer published to the View.</summary>
    public struct PreparationOffer
    {
        public PreparationOfferKind Kind;
        public int Variant;
        public int Magnitude;

        public bool IsValid => Kind != PreparationOfferKind.None;
    }

    /// <summary>
    /// Additive read seam for run-scoped Ember Rest state and companion orientation.
    /// It deliberately does not amend the frozen IHackSnapshot contract.
    /// </summary>
    public interface IRunPreparationSnapshot
    {
        int EmberRestRoomIndex { get; }
        bool EmberRestOpen { get; }
        int EmberRestSeed { get; }
        PreparationOffer EmberRestOffer0 { get; }
        PreparationOffer EmberRestOffer1 { get; }
        PreparationOffer EmberRestOffer2 { get; }
        PreparationOffer SelectedPreparation { get; }
        int CompanionFacing { get; }
    }
}
