namespace CinderCourt.Sim
{
    /// <summary>
    /// The hazard tables for the four stages that do NOT use their sim anchor's table.
    ///
    /// WHY THESE LIVE IN THE SIM. Nine stages are playable; <see cref="CampaignStages"/>
    /// carries six tables, and four stages (ember-gallery, witness-well, echo-throne,
    /// ash-verdict) supply their own, overriding the anchor they share. Those four lived
    /// in the View next to their presentation, and the cost of that was not stylistic:
    ///
    ///   * AMENDMENT #17 composed the interior onto the six sim anchors and reached
    ///     none of the four. Four of nine stages shipped with no interior at all while
    ///     the amendment was reported as complete, because "the stage table" named two
    ///     different things depending on which assembly you were standing in.
    ///   * The View references UnityEngine (StageEntry carries Color), so the standalone
    ///     dotnet harness that measures the sim in seconds (CLAUDE.md §4w) could not see
    ///     four of the nine layouts. Every gate — reachable area, detour ratio, stall,
    ///     determinism — was measured on 6/9 and reported as the set.
    ///
    /// Hazards are sim data: positions, radii and phases the sim integrates. Nothing
    /// here needs the engine, and putting them where the sim can be asked about them
    /// makes the coverage of a measurement equal to the coverage of the game.
    ///
    /// The View still owns which stage USES which table — that is presentation routing,
    /// and it stays in StageCatalog.
    /// </summary>
    public static class StageOverrideHazards
    {
        // Placements are campaign-fun-pass-spec.md's verbatim sim coordinates; the
        // simultaneous-telegraph budget (≤3 total, ≤2 same-kind) is pre-computed there
        // and frozen by the TestLane LCM census. Coordinates are UNCHANGED by #17 — the
        // arena centre did not move when the rim widened, so its furniture did not
        // either.
        //
        // Every table is composed through DungeonLayoutSpec, which is CALLED and never
        // reimplemented (§4e): the pinch rule, the sanctum hole, the ring standoff and
        // the gimmick clearance belong to the generator, and a second copy would drift
        // the moment either side moved.

        /// <summary>Stage 1 "불씨 윤무" — vent mastery: clockwise phase ring
        /// (0/0.6/1.2/1.8 s on the 2.4 s period) around a central pillar.</summary>
        public static readonly HazardConfig[] EmberGallery = DungeonLayoutSpec.Compose(
            new[]
            {
                HazardConfig.Vent(560f, 480f, 0f),
                HazardConfig.Vent(980f, 480f, 0.6f),
                HazardConfig.Vent(980f, 720f, 1.2f),
                HazardConfig.Vent(560f, 720f, 1.8f),
                HazardConfig.Pillar(768f, 604f),
            }, "ember-gallery");

        /// <summary>Stage 3 "쌍 제단" — altar introduction with risk: a diagonal altar
        /// pair, each guarded by an offset-phase vent (channel while dodging the
        /// rhythm).</summary>
        public static readonly HazardConfig[] WitnessWell = DungeonLayoutSpec.Compose(
            new[]
            {
                HazardConfig.Altar(560f, 500f),
                HazardConfig.Altar(980f, 700f),
                HazardConfig.Pillar(768f, 604f),
                HazardConfig.Vent(560f, 700f, 0.3f),
                HazardConfig.Vent(980f, 500f, 1.5f),
            }, "witness-well");

        /// <summary>Stage 4 "왕좌의 조류" — current preview: one weak band (+120 push)
        /// over the central altar; the 1.2 s hold must ride the 2.8 s rest
        /// window.</summary>
        public static readonly HazardConfig[] EchoThrone = DungeonLayoutSpec.Compose(
            new[]
            {
                HazardConfig.Altar(768f, 604f),
                HazardConfig.Vent(500f, 700f, 0f),
                HazardConfig.Vent(1030f, 480f, 1.2f),
                HazardConfig.Current(768f, 604f, 120f, 0.3f),
            }, "echo-throne");

        /// <summary>Stage 5 "판결의 방벽" — pylon preview: one pylon guarding the altar
        /// approach (aura 280 covers the centre — kill it first, or channel while the
        /// shielded enemies close).</summary>
        public static readonly HazardConfig[] AshVerdict = DungeonLayoutSpec.Compose(
            new[]
            {
                HazardConfig.Altar(768f, 604f),
                HazardConfig.Pylon(960f, 540f),
                HazardConfig.Vent(560f, 480f, 0f),
                HazardConfig.Vent(980f, 720f, 1.2f),
            }, "ash-verdict");
    }
}
