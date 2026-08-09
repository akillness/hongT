using System;
using System.Collections.Generic;
using CinderCourt.Sim;
using UnityEngine;

namespace CinderCourt.View
{
    /// <summary>Presentation data for one logical campaign stage.</summary>
    public readonly struct BossPresentation
    {
        public readonly EnemyVisual Visual;
        public readonly string ResourceId;
        public readonly Color Tint;
        public readonly float Scale;
        public readonly string HudName;

        public BossPresentation(EnemyVisual visual, string resourceId, Color tint, float scale, string hudName)
        {
            Visual = visual;
            ResourceId = resourceId;
            Tint = tint;
            Scale = scale;
            HudName = hudName;
        }
    }

    /// <summary>Immutable catalog record for one logical campaign stage.</summary>
    public readonly struct StageEntry
    {
        public readonly int CatalogIndex;
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Kicker;
        public readonly string Title;
        public readonly string HazardIcon;
        public readonly string SimAnchorId;
        public readonly HazardConfig[] HazardOverride;
        public readonly string PrereqId;
        public readonly string TerrainId;
        public readonly Color AccentColor;
        public readonly BossPresentation Boss;
        public readonly string StoryKey;
        public readonly string CompanionReward;
        /// <summary>
        /// The room's own win condition, phrased for the player (dungeon-revival
        /// spec §"each room needs a distinct objective"). Presentation-only text:
        /// the Sim still decides clears. Must be non-empty and unique per room so
        /// a contiguous route never repeats the same instruction twice.
        /// </summary>
        public readonly string RoomObjective;

        /// <summary>
        /// One-line gimmick identity shown on the lobby card (fun-pass v1.2):
        /// the stage's dominant gimmick in the preview→mastery lineage, phrased
        /// per worldview.md '기믹 계보' (court function made physical).
        ///
        /// Not interchangeable with <see cref="RoomObjective"/>: the epithet is
        /// the card's two-word IDENTITY ("분출구 입문"), the objective is the
        /// room's INSTRUCTION ("…를 끊고 …를 처단하라"). LobbyView reads this.
        /// </summary>
        public readonly string Epithet;

        /// <summary>
        /// Campaign-map node position, normalised 0..1 over the map viewport
        /// (x right, y up). Presentation-only — nothing in the Sim or in the
        /// frozen numeric contract reads it, so moving a node re-draws the
        /// lobby minimap and changes nothing else.
        ///
        /// The constellation is hand-placed rather than derived from the prereq
        /// chain: the chain is linear (0→1→…→8) and an evenly spaced line reads
        /// as a progress bar, not a map. Placements keep ≥0.10 normalised
        /// separation on at least one axis so two nodes never collide at the
        /// smallest audited viewport (<see cref="CampaignMapLayout"/> pins this).
        /// </summary>
        public readonly float NodeX, NodeY;

        public StageEntry(
            int catalogIndex, string id, string displayName, string kicker, string title,
            string hazardIcon, string simAnchorId, HazardConfig[] hazardOverride,
            string prereqId, string terrainId, Color accentColor, BossPresentation boss,
            string storyKey, string companionReward, string roomObjective, string epithet,
            float nodeX, float nodeY)
        {
            CatalogIndex = catalogIndex;
            Id = id;
            DisplayName = displayName;
            Kicker = kicker;
            Title = title;
            HazardIcon = hazardIcon;
            SimAnchorId = simAnchorId;
            HazardOverride = hazardOverride;
            PrereqId = prereqId;
            TerrainId = terrainId;
            AccentColor = accentColor;
            Boss = boss;
            StoryKey = storyKey;
            CompanionReward = companionReward;
            RoomObjective = roomObjective;
            Epithet = epithet;
            NodeX = nodeX;
            NodeY = nodeY;
        }

    }

    /// <summary>
    /// The view-layer campaign catalog. Logical entries may share a frozen Sim anchor;
    /// only a non-null hazard override changes the anchor configuration.
    /// </summary>
    public static class StageCatalog
    {
        // ------------------------------------------------ fun-pass v1.2 tables --
        // campaign-fun-pass-spec.md: every stage = one dominant gimmick, ramped
        // preview→mastery. Placements are the spec's verbatim sim coordinates;
        // simultaneous-telegraph budget (≤3 total, ≤2 same-kind) pre-computed in
        // the spec and frozen by the TestLane LCM census.

        // Stage 1 "불씨 윤무" — vent mastery: clockwise phase ring (0/0.6/1.2/1.8 s
        // on the 2.4 s vent period) around a central pillar.
        static readonly HazardConfig[] EmberGalleryHazards =
        {
            HazardConfig.Vent(560f, 480f, 0f),
            HazardConfig.Vent(980f, 480f, 0.6f),
            HazardConfig.Vent(980f, 720f, 1.2f),
            HazardConfig.Vent(560f, 720f, 1.8f),
            HazardConfig.Pillar(768f, 604f),
        };

        // Stage 3 "쌍 제단" — altar introduction with risk: diagonal altar pair,
        // each guarded by an offset-phase vent (channel while dodging the rhythm).
        static readonly HazardConfig[] WitnessWellHazards =
        {
            HazardConfig.Altar(560f, 500f),
            HazardConfig.Altar(980f, 700f),
            HazardConfig.Pillar(768f, 604f),
            HazardConfig.Vent(560f, 700f, 0.3f),
            HazardConfig.Vent(980f, 500f, 1.5f),
        };

        // Stage 4 "왕좌의 조류" — current preview: one weak band (+120 push) over
        // the central altar; the 1.2 s hold must ride the 2.8 s current rest window.
        static readonly HazardConfig[] EchoThroneHazards =
        {
            HazardConfig.Altar(768f, 604f),
            HazardConfig.Vent(500f, 700f, 0f),
            HazardConfig.Vent(1030f, 480f, 1.2f),
            HazardConfig.Current(768f, 604f, 120f, 0.3f),
        };

        // Stage 5 "판결의 방벽" — pylon preview: one pylon guarding the altar
        // approach (aura 280 covers the centre — kill it first or channel shielded
        // enemies).
        static readonly HazardConfig[] AshVerdictHazards =
        {
            HazardConfig.Altar(768f, 604f),
            HazardConfig.Pylon(960f, 540f),
            HazardConfig.Vent(560f, 480f, 0f),
            HazardConfig.Vent(980f, 720f, 1.2f),
        };

        static readonly StageEntry[] AllEntries =
        {
            new StageEntry(0, "cinder-span", "Cinder Span", "CINDER SPAN", "재의 다리",
                "skill-nova", "cinder-span", null, null, "cinder-span",
                new Color(0.95f, 0.35f, 0.17f),
                new BossPresentation(EnemyVisual.BossCommander, "shadow-commander-boss",
                    new Color(0.9f, 0.3f, 0.45f), 1f, "Cinder Warden"),
                "cinder-span", "ember-cohort",
                "다리를 건너오는 전열을 끊고 재의 워든을 처단하라", "분출구 입문",
                0.08f, 0.50f),
            new StageEntry(1, "ember-gallery", "Ember Gallery", "EMBER GALLERY", "불씨 회랑",
                "skill-nova", "cinder-span", EmberGalleryHazards, "cinder-span", "abyss-chancel",
                new Color(0.95f, 0.43f, 0.20f),
                new BossPresentation(EnemyVisual.BossCommander, "shadow-commander-boss",
                    new Color(0.95f, 0.45f, 0.16f), 1.08f, "Cinder Warden"),
                "ember-gallery", null,
                "분출하는 화구를 피해 회랑의 잔당을 소각하라", "불씨 윤무",
                0.22f, 0.74f),
            new StageEntry(2, "abyss-chancel", "Abyss Chancel", "ABYSS CHANCEL", "서약의 성당",
                "skill-aegis", "abyss-chancel", null, "ember-gallery", "abyss-chancel",
                new Color(0.56f, 0.40f, 1f),
                new BossPresentation(EnemyVisual.BossCommander, "shadow-commander-boss",
                    new Color(0.56f, 0.40f, 1f), 1.1f, "Veil Tactician"),
                "abyss-chancel", "shade-echo",
                "서약 제단을 사수하고 장막의 책략가를 끌어내라", "흑요석 미로",
                0.34f, 0.36f),
            new StageEntry(3, "witness-well", "Witness Well", "WITNESS WELL", "증언의 우물",
                "skill-aegis", "abyss-chancel", WitnessWellHazards, "abyss-chancel", "echo-throne",
                // Cycle-10: was (0.45,0.78,1) — the SAME value echo-throne
                // carries, and accent is the single input to StageMood, all
                // four env tints, both light colours and the flipbook theme.
                // Two adjacent stages sharing it collapsed their whole visual
                // identity into one look. Jade keeps the well cold (floor
                // warmth -0.1495 vs the -0.05 Ice threshold, TerrainFlipbook
                // .ThemeForFloorTint:236-239, so the sheet family is unchanged)
                // while moving 0.34 in the largest channel away from the
                // throne — water-green for 증언의 우물, not another cyan hall.
                new Color(0.22f, 0.76f, 0.66f),
                new BossPresentation(EnemyVisual.BossCommander, "shadow-commander-boss",
                    // Follows the accent. This stage was one of the 5/9 whose
                    // boss tint EQUALS its accent (the other four — cinder-span,
                    // ember-gallery, echo-throne, and the monarch — differ on
                    // purpose so the boss reads as a foreign thing entering the
                    // room). Moving the accent to jade without moving this would
                    // have quietly changed which camp the well is in and left
                    // its most prominent actor wearing echo-throne's cyan.
                    new Color(0.22f, 0.76f, 0.66f), 1.12f, "Veil Tactician"),
                "witness-well", null,
                "우물의 증언이 꺼지기 전 기둥 사이 전선을 유지하라", "쌍 제단",
                0.46f, 0.68f),
            new StageEntry(4, "echo-throne", "Echo Throne", "ECHO THRONE", "메아리 왕좌",
                "skill-pulse", "echo-throne", EchoThroneHazards, "witness-well", "echo-throne",
                new Color(0.45f, 0.78f, 1f),
                new BossPresentation(EnemyVisual.BossMonarch, "broken-court-monarch-boss",
                    new Color(0.75f, 0.3f, 0.9f), 1.15f, "Gate Sovereign"),
                "echo-throne", "possessed-echo",
                "왕좌의 메아리를 끊고 관문의 군주를 봉인하라", "왕좌의 조류",
                0.57f, 0.28f),
            new StageEntry(5, "ash-verdict", "Ash Verdict", "ASH VERDICT", "재의 판결",
                "skill-pulse", "echo-throne", AshVerdictHazards, "echo-throne", "echo-throne",
                new Color(0.87f, 0.78f, 0.41f),
                new BossPresentation(EnemyVisual.BossMonarch, "broken-court-monarch-boss",
                    new Color(0.87f, 0.78f, 0.41f), 1.18f, "Gate Sovereign"),
                "ash-verdict", null,
                "판결이 선고되기 전 재의 법정을 완전히 정화하라", "판결의 방벽",
                0.66f, 0.62f),
            // --- cycle-2 dungeon expansion (docs/SIM_SPEC_DUNGEONS.md) -------
            // New SIM anchors (not overrides): each id matches its frozen
            // CampaignStages anchor, so HazardOverride stays null.
            new StageEntry(6, "cinder-sluice", "Cinder Sluice", "CINDER SLUICE", "재의 수문",
                "skill-dash", "cinder-sluice", null, "ash-verdict", "abyss-chancel",
                new Color(0.247f, 0.659f, 0.784f),                       // #3FA8C8
                new BossPresentation(EnemyVisual.BossCommander, "shadow-commander-boss",
                    new Color(0.247f, 0.659f, 0.784f), 1.2f, "Sluice Keeper"),
                "cinder-sluice", null,
                "역류에 밀리지 않고 수문의 파수꾼을 물살 밖으로 끌어내라", "해류 숙달",
                0.77f, 0.34f),
            new StageEntry(7, "ember-bastion", "Ember Bastion", "EMBER BASTION", "불씨 요새",
                "skill-ward", "ember-bastion", null, "cinder-sluice", "cinder-span",
                new Color(0.910f, 0.541f, 0.180f),                       // #E88A2E
                new BossPresentation(EnemyVisual.BossCommander, "shadow-commander-boss",
                    new Color(0.910f, 0.541f, 0.180f), 1.22f, "Bastion Sentinel"),
                "ember-bastion", null,
                "적을 감싸는 불씨 기둥을 먼저 무너뜨리고 요새의 파수병을 베어라", "방벽 숙달",
                0.87f, 0.70f),
            new StageEntry(8, "ash-march", "Ash March", "ASH MARCH", "재의 행진",
                "skill-strike", "ash-march", null, "ember-bastion", "echo-throne",
                new Color(0.722f, 0.690f, 0.643f),                       // #B8B0A4
                new BossPresentation(EnemyVisual.BossMonarch, "broken-court-monarch-boss",
                    new Color(0.722f, 0.690f, 0.643f), 1.25f, "Ash Magistrate"),
                "ash-march", "scout-echo",
                "양쪽에서 닫혀오는 잿벽 사이에서 집행관을 판결하라", "집행 수렴",
                0.95f, 0.44f),

        };

        // Derived from the catalog length (9 entries -> 0x1FF) so adding a
        // stage can never silently truncate persisted clear bits again — the
        // 0x3F literal this replaces was written for the six-stage catalog.
        // MUST be declared after AllEntries: static initializers run in
        // declaration order, and this one reads AllEntries.Length.
        internal static readonly int ValidClearMask = (1 << AllEntries.Length) - 1;

        // ------------------------------------------------------------ dressing --
        // Spec: deep-interview-vfx-terrain-command-hardening §Lane T-a.
        // Combo stages reuse the cinder-span prefab's 90 feature/prop children as
        // a dressing LIBRARY: static tables (no RNG), sim-space coordinates,
        // placements strictly OUTSIDE the combat plane (x 248..1288, y 334..874)
        // and clear of every hazard (radius + 50). Slab/apron names are banned —
        // the fight floor is immutable.

        /// <summary>One deterministic view-only dressing placement (sim coords).</summary>
        public readonly struct DressingPlacement
        {
            public readonly string ObjectName;   // child of Terrain/terrain-cinder-span
            public readonly float SimX, SimY;    // sim-space ground position
            public readonly float RotationY;     // degrees, applied on top of source
            public readonly float Scale;         // uniform multiplier on source scale

            public DressingPlacement(string objectName, float simX, float simY, float rotationY, float scale)
            {
                ObjectName = objectName;
                SimX = simX;
                SimY = simY;
                RotationY = rotationY;
                Scale = scale;
            }
        }

        /// <summary>Prefab whose children form the shared dressing library.</summary>
        public const string DressingLibraryTerrainId = "cinder-span";

        // Combat plane (sim coords): center 768,604, half 520×270 → x 248..1288,
        // y 334..874. Dressing must stay outside this rectangle.
        public const float DressingPlaneMinX = 248f, DressingPlaneMaxX = 1288f;
        public const float DressingPlaneMinY = 334f, DressingPlaneMaxY = 874f;
        public const float DressingHazardClearance = 50f;

        // Library children are millimetric micro-decals on the authored plate
        // (renderer bounds ≈0.05–0.12 world units). Dressing scales are therefore
        // large: ×15–22 turns them into readable rocks/monuments on the 15.36-unit
        // plate without approaching actor scale.
        static readonly DressingPlacement[] EmberGalleryDressing =
        {
            // Broken colonnade ridge along the top edge; ember rock pocket low-left.
            new DressingPlacement("terrain-cinder-span-feature-001",  430f, 240f,   0f, 16f),
            new DressingPlacement("terrain-cinder-span-feature-002",  700f, 210f,  25f, 19f),
            new DressingPlacement("terrain-cinder-span-feature-003",  980f, 235f, -20f, 15f),
            new DressingPlacement("terrain-cinder-span-feature-004", 1230f, 260f,  40f, 18f),
            new DressingPlacement("terrain-cinder-span-prop-001",     180f, 640f,  10f, 12f),
            new DressingPlacement("terrain-cinder-span-prop-002",     160f, 760f, 200f, 11f),
            new DressingPlacement("terrain-cinder-span-prop-003",     620f, 950f,  90f, 13f),
            new DressingPlacement("terrain-cinder-span-prop-004",     900f, 960f, 270f, 12f),
            new DressingPlacement("terrain-cinder-span-feature-006", 1380f, 700f, 180f, 20f),
        };

        static readonly DressingPlacement[] WitnessWellDressing =
        {
            // Symmetric witness sentinels flanking the well; scattered court props.
            new DressingPlacement("terrain-cinder-span-feature-010",  170f, 450f,  90f, 18f),
            new DressingPlacement("terrain-cinder-span-feature-011",  180f, 720f,  90f, 18f),
            new DressingPlacement("terrain-cinder-span-feature-012", 1360f, 450f, -90f, 18f),
            new DressingPlacement("terrain-cinder-span-feature-013", 1350f, 730f, -90f, 18f),
            new DressingPlacement("terrain-cinder-span-prop-010",     500f, 250f,   0f, 11f),
            new DressingPlacement("terrain-cinder-span-prop-011",     768f, 220f,  45f, 12f),
            new DressingPlacement("terrain-cinder-span-prop-012",    1040f, 250f, -45f, 11f),
            new DressingPlacement("terrain-cinder-span-prop-013",     430f, 940f, 180f, 12f),
            new DressingPlacement("terrain-cinder-span-prop-014",    1100f, 950f, 180f, 12f),
            new DressingPlacement("terrain-cinder-span-feature-015",  768f, 985f, 180f, 22f),
        };

        static readonly DressingPlacement[] AshVerdictDressing =
        {
            // Tribunal mass top-center; verdict monuments crowding every corner.
            new DressingPlacement("terrain-cinder-span-feature-020",  620f, 200f,   0f, 20f),
            new DressingPlacement("terrain-cinder-span-feature-021",  920f, 200f,   0f, 20f),
            new DressingPlacement("terrain-cinder-span-feature-022",  768f, 160f, 180f, 22f),
            new DressingPlacement("terrain-cinder-span-feature-023",  200f, 380f,  35f, 16f),
            new DressingPlacement("terrain-cinder-span-feature-024", 1420f, 400f, -35f, 12f),
            new DressingPlacement("terrain-cinder-span-feature-025",  160f, 900f, 145f, 13f),
            new DressingPlacement("terrain-cinder-span-feature-026", 1420f, 880f, 215f, 12f),
            new DressingPlacement("terrain-cinder-span-prop-020",     540f, 955f,  15f, 12f),
            new DressingPlacement("terrain-cinder-span-prop-021",     990f, 940f, 335f, 12f),
            new DressingPlacement("terrain-cinder-span-prop-022",     768f, 975f, 180f, 14f),
        };

        // Cycle-2 tables verify against the frozen SIM ANCHOR hazards
        // (CampaignStages — the new stages carry no HazardOverride; v1.2 state):
        //   cinder-sluice: current(768,470)/(768,740) r0 + vent(500,604)/
        //                  (1030,604) r90 + pillar(768,604) r40
        //   ember-bastion: pylon(560,500)/(980,700)/(768,430) r30 + pillar
        //                  (640,650)/(900,560) r40 + vent(768,604) r90
        //   ash-march:     wall(both edges, r0 point test) + altar(768,604) r70
        //                  + pylon(768,520) r30 + vent(560,760)/(980,450) r90
        static readonly DressingPlacement[] CinderSluiceDressing =
        {
            // Channel walls flanking the two current lanes; gate mass up top.
            new DressingPlacement("terrain-cinder-span-feature-010",  200f, 420f,  90f, 17f),
            new DressingPlacement("terrain-cinder-span-feature-011",  190f, 700f,  90f, 17f),
            new DressingPlacement("terrain-cinder-span-feature-012", 1340f, 430f, -90f, 17f),
            new DressingPlacement("terrain-cinder-span-feature-013", 1345f, 720f, -90f, 17f),
            new DressingPlacement("terrain-cinder-span-feature-006",  768f, 190f,   0f, 20f),
            new DressingPlacement("terrain-cinder-span-prop-001",     500f, 260f,  15f, 12f),
            new DressingPlacement("terrain-cinder-span-prop-002",    1010f, 255f, -20f, 12f),
            new DressingPlacement("terrain-cinder-span-prop-003",     620f, 945f, 180f, 12f),
            new DressingPlacement("terrain-cinder-span-prop-004",     950f, 950f, 160f, 12f),
        };

        static readonly DressingPlacement[] EmberBastionDressing =
        {
            // Rampart battlements ringing the fort on every closed edge.
            //
            // COUNTED, not eyeballed: split the six dressed tables by quadrant
            // about the arena centre (768,604) and this one read NW 2 / NE 2 /
            // SW 1 / SE 3 - the thinnest quadrant in the set, against a comment
            // that claims EVERY closed edge. feature-023 sits at (205,500) with
            // no southern partner, while cinder-sluice pairs (200,420) with
            // (190,700). feature-025 restores the pair: outside the plate
            // (x 248..1288, y 334..874), 420 px from the nearest hazard (the
            // verdict-pact pylon at 620,720) against a clearance requirement in
            // the tens, 220 px from the nearest neighbouring placement, and the
            // table lands at 9 with quadrants 2/2/2/3.
            new DressingPlacement("terrain-cinder-span-feature-020",  470f, 250f,   0f, 18f),
            new DressingPlacement("terrain-cinder-span-feature-021",  770f, 215f,   0f, 20f),
            new DressingPlacement("terrain-cinder-span-feature-022", 1070f, 250f,   0f, 18f),
            new DressingPlacement("terrain-cinder-span-feature-023",  205f, 500f,  35f, 15f),
            new DressingPlacement("terrain-cinder-span-feature-025",  200f, 720f,  35f, 15f),
            new DressingPlacement("terrain-cinder-span-feature-024", 1335f, 620f, -35f, 15f),
            new DressingPlacement("terrain-cinder-span-prop-020",     380f, 940f,  10f, 12f),
            new DressingPlacement("terrain-cinder-span-prop-021",     900f, 955f, 340f, 12f),
            new DressingPlacement("terrain-cinder-span-prop-022",    1240f, 930f, 200f, 12f),
        };

        static readonly DressingPlacement[] AshMarchDressing =
        {
            // Procession columns along top/right/bottom. Every placement keeps
            // SimX >= 658 — the ash wall sweeps the x 248..608 band (y-full),
            // so left-edge dressing would sit visually inside the crush lane.
            new DressingPlacement("terrain-cinder-span-feature-001",  700f, 230f,   0f, 17f),
            new DressingPlacement("terrain-cinder-span-feature-002",  900f, 215f,  15f, 18f),
            new DressingPlacement("terrain-cinder-span-feature-003", 1120f, 240f, -15f, 17f),
            new DressingPlacement("terrain-cinder-span-feature-004", 1350f, 400f, -40f, 16f),
            new DressingPlacement("terrain-cinder-span-feature-015", 1360f, 780f, 220f, 20f),
            new DressingPlacement("terrain-cinder-span-prop-010",     760f, 940f, 180f, 12f),
            new DressingPlacement("terrain-cinder-span-prop-011",    1000f, 955f, 170f, 12f),
            new DressingPlacement("terrain-cinder-span-prop-012",    1240f, 945f, 190f, 12f),
        };

        // T-b (RELEASE_NOTES.md:255-260, shipped 2026-08-05) split the fused
        // abyss-chancel GLB, so the two stages that "await the T-b split" are
        // no longer blocked — these are their tables.
        //
        // Clearance is measured against whatever table the stage ACTUALLY runs
        // (HazardOverride ?? frozen sim anchor — the same resolution order
        // StageDressingTests.HazardsFor uses; getting this wrong measures the
        // right numbers against the wrong stage):
        //   abyss-chancel: NO override, so the sim's own anchors, read by
        //                  compiling CinderCourt.Sim standalone (§4w) rather
        //                  than copying by hand —
        //                  pillar(640,500)/(900,700)/(768,604) r40
        //                  + vent(1100,450) r90
        //   echo-throne:   HAS an override (EchoThroneHazards, this file):
        //                  altar(768,604) r70 + vent(500,700)/(1030,480) r90
        //                  + current(768,604) — a TideCurrent carries HalfW/
        //                  HalfH, not Radius, so the radius+50 test reads 50
        //                  and it never becomes the binding constraint here.
        // Every placement below was checked arithmetically before it was
        // written (§4r — the margin is the gate, the adjective is not):
        // worst hazard margin beyond radius+50 is +75.2 (chancel
        // feature-029 ↔ vent) and +111.8 (throne feature-035 ↔ vent);
        // nearest-neighbour spacing ≥275; quadrants 2/3/2/2 on both.
        //
        // Library names are picked from the UNUSED pool (23 features /
        // 38 props were free) so no stage silhouette is a copy of another's.
        static readonly DressingPlacement[] AbyssChancelDressing =
        {
            // Ecclesiastical read: nave colonnade across the top, side-chapel
            // buttresses on both flanks, low reliquary clutter at the apse.
            // Cooler and more ordered than the ember stages — the chancel is
            // architecture that survived, not rubble.
            new DressingPlacement("terrain-cinder-span-feature-027",  430f, 235f,   0f, 17f),
            new DressingPlacement("terrain-cinder-span-feature-028",  768f, 200f,   0f, 20f),
            new DressingPlacement("terrain-cinder-span-feature-029", 1090f, 235f,   0f, 17f),
            new DressingPlacement("terrain-cinder-span-feature-030",  185f, 470f,  90f, 16f),
            new DressingPlacement("terrain-cinder-span-feature-031",  180f, 745f,  90f, 16f),
            new DressingPlacement("terrain-cinder-span-feature-032", 1360f, 460f, -90f, 16f),
            new DressingPlacement("terrain-cinder-span-prop-005",     560f, 950f, 180f, 12f),
            new DressingPlacement("terrain-cinder-span-prop-006",     980f, 955f, 180f, 12f),
            new DressingPlacement("terrain-cinder-span-prop-007",    1330f, 800f, 200f, 13f),
        };

        static readonly DressingPlacement[] EchoThroneDressing =
        {
            // Boss-tier plate: FEWER, BIGGER masses. terrain-echo-throne is an
            // apron plus five slabs (17 KB) — the emptiest ground in the game
            // and the one four stages stand on — so its dressing has to carry
            // the whole silhouette. Scales run 20-24 against 11-22 elsewhere:
            // monumental, symmetric, austere. The throne mass anchors dead
            // centre-north; paired sentinels march down both flanks.
            new DressingPlacement("terrain-cinder-span-feature-033",  768f, 175f, 180f, 24f),
            new DressingPlacement("terrain-cinder-span-feature-034",  480f, 230f,   0f, 21f),
            new DressingPlacement("terrain-cinder-span-feature-035", 1060f, 230f,   0f, 21f),
            new DressingPlacement("terrain-cinder-span-feature-036",  165f, 520f,  90f, 22f),
            new DressingPlacement("terrain-cinder-span-feature-037", 1375f, 520f, -90f, 22f),
            new DressingPlacement("terrain-cinder-span-feature-038",  170f, 800f,  90f, 20f),
            new DressingPlacement("terrain-cinder-span-feature-039", 1370f, 800f, -90f, 20f),
            new DressingPlacement("terrain-cinder-span-prop-015",     620f, 965f, 180f, 14f),
            new DressingPlacement("terrain-cinder-span-prop-016",     915f, 965f, 180f, 14f),
        };

        /// <summary>
        /// Dressing table for a logical stage; null only when the stage's own
        /// terrain prefab already carries its authored dressing (cinder-span).
        /// Every other stage now has a table — abyss-chancel and echo-throne
        /// were the last two holes and were filled once T-b shipped.
        /// </summary>
        public static DressingPlacement[] DressingFor(string stageId)
        {
            switch (stageId)
            {
                case "ember-gallery": return EmberGalleryDressing;
                case "witness-well": return WitnessWellDressing;
                case "ash-verdict": return AshVerdictDressing;
                case "cinder-sluice": return CinderSluiceDressing;
                case "ember-bastion": return EmberBastionDressing;
                case "ash-march": return AshMarchDressing;
                case "abyss-chancel": return AbyssChancelDressing;
                case "echo-throne": return EchoThroneDressing;
                default: return null;
            }
        }

        // ------------------------------------------------- v1.3 verdict pact --
        // meta-fun-pass-spec.md M3 + negotiation-record entry 5: opt-in replay
        // ladder for CLEARED stages. A pact table = the stage's effective table
        // (HazardOverride ?? frozen sim anchor), element-for-element in the same
        // order, PLUS 1-2 APPENDED placements of the stage's identity gimmick.
        // No new kinds, no RNG — a pact is just another fixed table, so every
        // existing hazard-rendering and census path applies unchanged.
        //
        // Telegraph budget (≤3 concurrent, ≤2 same-kind — qa band 5) verified
        // by phase arithmetic per table below. Windows (CampaignSpec, absolute
        // stage time t):
        //   vent    tel: t ≡ [1.6-ph, 2.4-ph) mod 2.4  (VentPeriod-VentTelegraph)
        //   current tel: t ≡ [-ph, 0.8-ph)    mod 6
        //   wall    tel: t ≡ [4.5-ph, 6.0-ph) mod 23   (WallRest..+WallTelegraph)
        //   pillar / altar / pylon never telegraph.

        /// <summary>Base-prefix + appended-extras pact table (M3 contract:
        /// pact[0..base.Length-1] element-equal to the effective base, same
        /// order; extras strictly at the tail).</summary>
        static HazardConfig[] Pact(HazardConfig[] baseTable, params HazardConfig[] extras)
        {
            var pact = new HazardConfig[baseTable.Length + extras.Length];
            Array.Copy(baseTable, pact, baseTable.Length);
            Array.Copy(extras, 0, pact, baseTable.Length, extras.Length);
            return pact;
        }

        /// <summary>Frozen sim-anchor hazards for override-less stages. Ranks
        /// are irrelevant to hazards — zeros keep the lookup pure. Ids are the
        /// CampaignStages consts, so a miss is a programming error (surfaces
        /// as TypeInitializationException in the first catalog test).</summary>
        static HazardConfig[] AnchorHazards(string simStageId)
            => CampaignStages.TryGet(simStageId, 0, 0, 0, out var config)
                ? config.Hazards
                : null;

        // Stage 0 재의 다리 — +1 vent mid-bridge (768,604) ph 0.6. Windows:
        // base [1.6,2.4)/[0.4,1.2) disjoint; extra [1.0,1.8) meets each pair-
        // wise ([1.6,1.8) and [1.0,1.2)) but never both at once → max 2, 2v.
        static readonly HazardConfig[] CinderSpanPact = Pact(
            AnchorHazards(CampaignStages.CinderSpan),
            HazardConfig.Vent(768f, 604f, 0.6f));

        // Stage 1 불씨 윤무 — +2 mid-column pillars (768,468)/(768,740), NOT
        // the spec sketch's "+2 ring vents 0.3/1.5". Proof no vent phase fits:
        // the base ring's windows ([1.6,2.4) [1.0,1.8) [0.4,1.2) and
        // [2.2,2.4)∪[0,0.6)) tile the whole 2.4 s period with 2-vent zones of
        // width 0.2 every 0.6 s ([0.4,0.6) [1.0,1.2) [1.6,1.8) [2.2,2.4));
        // any additional 0.8 s vent window covers a full residue class mod
        // 0.6 and therefore intersects a 2-zone at EVERY possible phase →
        // 3 same-kind, budget breach. Pillars are the stage's own secondary
        // gimmick (base centre pillar), never telegraph, and narrow the 윤무
        // ring corridors into a mid-column slalom. 604±136 keeps the v1.2
        // walkability contract (pillar spacing ≥132; edge gap 56 ≥ player
        // diameter 52 — squeezable, not sealed). Census stays 2/2v.
        static readonly HazardConfig[] EmberGalleryPact = Pact(
            EmberGalleryHazards,
            HazardConfig.Pillar(768f, 468f),
            HazardConfig.Pillar(768f, 740f));

        // Stage 2 흑요석 미로 — +1 pillar (900,500) closes the maze diamond
        // around the vent lane. Single vent → max 1 telegraph.
        static readonly HazardConfig[] AbyssChancelPact = Pact(
            AnchorHazards(CampaignStages.AbyssChancel),
            HazardConfig.Pillar(900f, 500f));

        // Stage 3 쌍 제단 — +1 altar-guard vent ON the NW altar (560,500)
        // ph 0.9: channelling now rides the rhythm directly. Deliberate
        // colocation (v1.3 pact exemption to radial non-overlap): altar and
        // vent are both zone tests, neither is solid — a damage disc over a
        // channel disc is mechanically well-defined and IS the pact bite.
        // Vent windows: 0.3→[1.3,2.1), 1.5→[0.1,0.9), 0.9→[0.7,1.5).
        // Pairwise overlaps [1.3,1.5) and [0.7,0.9); the base pair is
        // disjoint → no triple. Max 2, 2v.
        static readonly HazardConfig[] WitnessWellPact = Pact(
            WitnessWellHazards,
            HazardConfig.Vent(560f, 500f, 0.9f));

        // Stage 4 왕좌의 조류 — +1 counter-current lane (768,740) push -120
        // ph 3.3. Position deliberately matches the sluice anchor current so
        // the view's CurrentPushSign build-time lookup resolves the -x flow.
        // LCM(6,2.4)=12 s: vents [1.6,2.4)/[0.4,1.2) disjoint (max 1);
        // currents [5.7,6)∪[0,0.5) vs [2.7,3.5) disjoint (max 1) → max 2.
        static readonly HazardConfig[] EchoThronePact = Pact(
            EchoThroneHazards,
            HazardConfig.Current(768f, 740f, -120f, 3.3f));

        // Stage 5 판결의 방벽 — +1 pylon (576,668): SW counterpart of the
        // base pylon; aura (280) reaches the centre altar from both diagonals
        // (dist ≈ 202). Pylons never telegraph; vents [1.6,2.4)/[0.4,1.2)
        // disjoint → max 1.
        static readonly HazardConfig[] AshVerdictPact = Pact(
            AshVerdictHazards,
            HazardConfig.Pylon(576f, 668f));

        // Stage 6 해류 숙달 — +1 mid-lane vent (768,604) ph 1.7: the safe
        // corridor's centre now rides the vent rhythm. Deliberate colocation
        // with the base pillar (v1.3 pact exemption to radial non-overlap):
        // the pillar core (r40) is solid, so the vent bite is the standable
        // annulus 40..90 around it — cover behind the pillar stops being
        // unconditionally safe, which IS the pact bite. No telegraph
        // interaction (pillars are silent).
        // LCM(6,2.4)=12 s. Vents: 0.9→[0.7,1.5), 2.1→[1.9,2.4)∪[0,0.3),
        // 1.7→[2.3,2.4)∪[0,0.7); only pair zone (2.1∧1.7) = [2.3,2.4)∪[0,0.3)
        // and 0.9 is disjoint from 1.7 → no vent triple (max 2v). Currents
        // [0,0.8)/[3,3.8) mod 6 disjoint (max 1). Upper bound 2v+1c = 3;
        // attained at t∈[0,0.3) and [9.5,9.8) in the LCM. ≤3 OK ≤2 same OK.
        static readonly HazardConfig[] CinderSluicePact = Pact(
            AnchorHazards(CampaignStages.CinderSluice),
            HazardConfig.Vent(768f, 604f, 1.7f));

        // Stage 7 방벽 숙달 — +1 pylon (620,720) plugs the SW gap of the
        // phalanx. Pylons/pillars never telegraph; single vent → max 1.
        static readonly HazardConfig[] EmberBastionPact = Pact(
            AnchorHazards(CampaignStages.EmberBastion),
            HazardConfig.Pylon(620f, 720f));

        // Stage 8 집행 수렴 — +1 vent (768,796) ph 1.2: south-band denial
        // under the corridor altar (r90 bites y 706..886 — the walkable
        // strip between altar and south edge). y796 keeps the audited v1.2
        // dressing clearance (prop-010 at 760,940: d≈144 ≥ r90+50) and
        // radial non-overlap with the altar (d=192 ≥ 70+90).
        // Vents: 0.6→[1.0,1.8), 1.8→[2.2,2.4)∪[0,0.6), 1.2→[0.4,1.2); pair
        // zones [1.0,1.2)/[0.4,0.6), and 0.6∧1.8 disjoint → no vent triple
        // (max 2v). Walls [4.5,6.0)+23k / [16,17.5)+23k disjoint (max 1).
        // Upper bound 2v+1w = 3 over the 276 s LCM. ≤3 OK ≤2 same-kind OK.
        static readonly HazardConfig[] AshMarchPact = Pact(
            AnchorHazards(CampaignStages.AshMarch),
            HazardConfig.Vent(768f, 796f, 1.2f));

        /// <summary>Verdict-pact hazard table for a logical stage — non-null
        /// for every catalog id (M3 contract: effective-base prefix + appended
        /// identity-gimmick extras). Null only for unknown ids.</summary>
        public static HazardConfig[] PactFor(string stageId)
        {
            switch (stageId)
            {
                case "cinder-span": return CinderSpanPact;
                case "ember-gallery": return EmberGalleryPact;
                case "abyss-chancel": return AbyssChancelPact;
                case "witness-well": return WitnessWellPact;
                case "echo-throne": return EchoThronePact;
                case "ash-verdict": return AshVerdictPact;
                case "cinder-sluice": return CinderSluicePact;
                case "ember-bastion": return EmberBastionPact;
                case "ash-march": return AshMarchPact;
                default: return null;
            }
        }
        public static IReadOnlyList<StageEntry> Entries => AllEntries;

        public static bool TryGet(string id, out StageEntry entry)
        {
            for (var i = 0; i < AllEntries.Length; i++)
            {
                if (string.Equals(AllEntries[i].Id, id, StringComparison.Ordinal))
                {
                    entry = AllEntries[i];
                    return true;
                }
            }
            entry = default;
            return false;
        }

        /// <summary>
        /// The room objective line for a logical stage id. Returns "" for arena /
        /// prologue / unknown ids so the HUD chip simply stays hidden instead of
        /// showing a stale instruction from the previous room.
        /// </summary>
        public static string ObjectiveFor(string stageId)
            => TryGet(stageId, out var entry) ? entry.RoomObjective : "";


        public static bool IsCleared(in CampaignData data, in StageEntry entry)
            => (data.ClearedMask & (1 << entry.CatalogIndex)) != 0;

        public static bool MarkCleared(ref CampaignData data, in StageEntry entry, out bool firstClear)
        {
            var bit = 1 << entry.CatalogIndex;
            firstClear = (data.ClearedMask & bit) == 0;
            data.ClearedMask = (data.ClearedMask | bit) & ValidClearMask;
            return firstClear;
        }

        public static bool IsUnlocked(in CampaignData data, in StageEntry entry)
        {
            if (!data.PrologueDone) return false;
            if (IsCleared(in data, in entry)) return true;
            if (string.IsNullOrEmpty(entry.PrereqId)) return true;
            return TryGet(entry.PrereqId, out var prerequisite)
                && IsCleared(in data, in prerequisite);
        }
    }
}
