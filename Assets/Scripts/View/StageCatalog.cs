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

        public StageEntry(
            int catalogIndex, string id, string displayName, string kicker, string title,
            string hazardIcon, string simAnchorId, HazardConfig[] hazardOverride,
            string prereqId, string terrainId, Color accentColor, BossPresentation boss,
            string storyKey, string companionReward)
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
        }
    }

    /// <summary>
    /// The view-layer campaign catalog. Logical entries may share a frozen Sim anchor;
    /// only a non-null hazard override changes the anchor configuration.
    /// </summary>
    public static class StageCatalog
    {
        static readonly HazardConfig[] EmberGalleryHazards =
        {
            HazardConfig.Vent(560f, 480f, 0f),
            HazardConfig.Vent(980f, 720f, 1.2f),
            HazardConfig.Vent(1100f, 450f, 0.6f),
            HazardConfig.Pillar(768f, 604f),
        };

        static readonly HazardConfig[] WitnessWellHazards =
        {
            HazardConfig.Altar(768f, 604f),
            HazardConfig.Pillar(640f, 500f),
            HazardConfig.Pillar(900f, 700f),
            HazardConfig.Vent(1030f, 480f, 1.2f),
        };

        static readonly HazardConfig[] AshVerdictHazards =
        {
            HazardConfig.Altar(768f, 604f),
            HazardConfig.Vent(560f, 480f, 0f),
            HazardConfig.Vent(980f, 720f, 1.2f),
            HazardConfig.Vent(1030f, 480f, 0.6f),
        };

        static readonly StageEntry[] AllEntries =
        {
            new StageEntry(0, "cinder-span", "Cinder Span", "CINDER SPAN", "재의 다리",
                "skill-nova", "cinder-span", null, null, "cinder-span",
                new Color(0.95f, 0.35f, 0.17f),
                new BossPresentation(EnemyVisual.BossCommander, "shadow-commander-boss",
                    new Color(0.9f, 0.3f, 0.45f), 1f, "Cinder Warden"),
                "cinder-span", "ember-cohort"),
            new StageEntry(1, "ember-gallery", "Ember Gallery", "EMBER GALLERY", "불씨 회랑",
                "skill-nova", "cinder-span", EmberGalleryHazards, "cinder-span", "abyss-chancel",
                new Color(0.95f, 0.43f, 0.20f),
                new BossPresentation(EnemyVisual.BossCommander, "shadow-commander-boss",
                    new Color(0.95f, 0.45f, 0.16f), 1.08f, "Cinder Warden"),
                "ember-gallery", null),
            new StageEntry(2, "abyss-chancel", "Abyss Chancel", "ABYSS CHANCEL", "서약의 성당",
                "skill-aegis", "abyss-chancel", null, "ember-gallery", "abyss-chancel",
                new Color(0.56f, 0.40f, 1f),
                new BossPresentation(EnemyVisual.BossCommander, "shadow-commander-boss",
                    new Color(0.56f, 0.40f, 1f), 1.1f, "Veil Tactician"),
                "abyss-chancel", "shade-echo"),
            new StageEntry(3, "witness-well", "Witness Well", "WITNESS WELL", "증언의 우물",
                "skill-aegis", "abyss-chancel", WitnessWellHazards, "abyss-chancel", "echo-throne",
                new Color(0.45f, 0.78f, 1f),
                new BossPresentation(EnemyVisual.BossCommander, "shadow-commander-boss",
                    new Color(0.45f, 0.78f, 1f), 1.12f, "Veil Tactician"),
                "witness-well", null),
            new StageEntry(4, "echo-throne", "Echo Throne", "ECHO THRONE", "메아리 왕좌",
                "skill-pulse", "echo-throne", null, "witness-well", "echo-throne",
                new Color(0.45f, 0.78f, 1f),
                new BossPresentation(EnemyVisual.BossMonarch, "broken-court-monarch-boss",
                    new Color(0.75f, 0.3f, 0.9f), 1.15f, "Gate Sovereign"),
                "echo-throne", "possessed-echo"),
            new StageEntry(5, "ash-verdict", "Ash Verdict", "ASH VERDICT", "재의 판결",
                "skill-pulse", "echo-throne", AshVerdictHazards, "echo-throne", "echo-throne",
                new Color(0.87f, 0.78f, 0.41f),
                new BossPresentation(EnemyVisual.BossMonarch, "broken-court-monarch-boss",
                    new Color(0.87f, 0.78f, 0.41f), 1.18f, "Gate Sovereign"),
                "ash-verdict", null),
            // --- cycle-2 dungeon expansion (docs/SIM_SPEC_DUNGEONS.md) -------
            // New SIM anchors (not overrides): each id matches its frozen
            // CampaignStages anchor, so HazardOverride stays null.
            new StageEntry(6, "cinder-sluice", "Cinder Sluice", "CINDER SLUICE", "재의 수문",
                "skill-dash", "cinder-sluice", null, "ash-verdict", "abyss-chancel",
                new Color(0.247f, 0.659f, 0.784f),                       // #3FA8C8
                new BossPresentation(EnemyVisual.BossCommander, "shadow-commander-boss",
                    new Color(0.247f, 0.659f, 0.784f), 1.2f, "Sluice Keeper"),
                "cinder-sluice", null),
            new StageEntry(7, "ember-bastion", "Ember Bastion", "EMBER BASTION", "불씨 요새",
                "skill-ward", "ember-bastion", null, "cinder-sluice", "cinder-span",
                new Color(0.910f, 0.541f, 0.180f),                       // #E88A2E
                new BossPresentation(EnemyVisual.BossCommander, "shadow-commander-boss",
                    new Color(0.910f, 0.541f, 0.180f), 1.22f, "Bastion Sentinel"),
                "ember-bastion", null),
            new StageEntry(8, "ash-march", "Ash March", "ASH MARCH", "재의 행진",
                "skill-strike", "ash-march", null, "ember-bastion", "echo-throne",
                new Color(0.722f, 0.690f, 0.643f),                       // #B8B0A4
                new BossPresentation(EnemyVisual.BossMonarch, "broken-court-monarch-boss",
                    new Color(0.722f, 0.690f, 0.643f), 1.25f, "Ash Magistrate"),
                "ash-march", "scout-echo"),
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
        // (CampaignStages — the new stages carry no HazardOverride):
        //   cinder-sluice: current(768,470)/(768,740) r0 + pillar(768,604) r40
        //   ember-bastion: pylon(560,500)/(980,700) r30 + pillar(640,650)/
        //                  (900,560) r40 + vent(768,604) r90
        //   ash-march:     wall(x 248..608 band) + altar(1100,604) r70 +
        //                  vent(980,480) r90
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
            new DressingPlacement("terrain-cinder-span-feature-020",  470f, 250f,   0f, 18f),
            new DressingPlacement("terrain-cinder-span-feature-021",  770f, 215f,   0f, 20f),
            new DressingPlacement("terrain-cinder-span-feature-022", 1070f, 250f,   0f, 18f),
            new DressingPlacement("terrain-cinder-span-feature-023",  205f, 500f,  35f, 15f),
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

        /// <summary>
        /// Dressing table for a logical stage; null when the stage's own terrain
        /// prefab already carries its authored dressing (cinder-span) or none is
        /// defined yet (abyss-chancel/echo-throne await the T-b split).
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
