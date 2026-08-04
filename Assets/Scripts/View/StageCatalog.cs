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
        const int ValidClearMask = 0x3F;

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
        };

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
