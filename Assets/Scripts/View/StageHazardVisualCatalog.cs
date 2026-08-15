using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CinderCourt.Sim;

namespace CinderCourt.View
{
    /// <summary>Stage-specific physical surface binding for one campaign hazard kind.</summary>
    public readonly struct HazardSurfaceBinding
    {
        public readonly string StageId;
        public readonly HazardKind Kind;
        public readonly string PrimaryRole;
        public readonly string ResourcePath;

        public HazardSurfaceBinding(string stageId, HazardKind kind, string primaryRole, string resourcePath)
        {
            StageId = stageId;
            Kind = kind;
            PrimaryRole = primaryRole;
            ResourcePath = resourcePath;
        }

        public string Role => PrimaryRole;
    }

    /// <summary>Prompt-facing tone notes for one campaign stage's hazard surfaces.</summary>
    public readonly struct HazardStageToneProfile
    {
        public readonly string StageId;
        public readonly int Act;
        public readonly string Palette;
        public readonly string MaterialLanguage;

        public HazardStageToneProfile(string stageId, int act, string palette, string materialLanguage)
        {
            StageId = stageId;
            Act = act;
            Palette = palette;
            MaterialLanguage = materialLanguage;
        }
    }

    /// <summary>
    /// View-only catalog that maps campaign stage hazards to generated physical surface resources.
    /// </summary>
    public static class StageHazardVisualCatalog
    {
        public const string ResourceRoot = "Textures/Hazards/";

        static readonly HazardStageToneProfile[] ToneProfileArray =
        {
            new HazardStageToneProfile(
                "cinder-span", 1,
                "charcoal basalt, scorched seams, hot ember orange",
                "cinder bridge stone inlays with ember-burnt edges"),
            new HazardStageToneProfile(
                "ember-gallery", 1,
                "fire-blackened gallery stone, obsidian, circular vent orange",
                "formal gallery floor plates with vent rhythm scorch marks"),
            new HazardStageToneProfile(
                "abyss-chancel", 1,
                "violet indigo oath cathedral, pale runes, cold veil",
                "obsidian chapel surfaces with oath-rune mineral seams"),
            new HazardStageToneProfile(
                "witness-well", 2,
                "wet jade well stone, teal mineral rings, dark water green",
                "slick testimony-well inlays with jade sediment rings"),
            new HazardStageToneProfile(
                "echo-throne", 2,
                "dark blue granite, silver veins, cyan echo current",
                "throne-floor stonework with concentric echo-current channels"),
            new HazardStageToneProfile(
                "ash-verdict", 2,
                "ash sandstone court, smoke grey, judgment gold",
                "verdict-court slabs with muted gold ash scoring"),
            new HazardStageToneProfile(
                "cinder-sluice", 3,
                "iron sluice, wet grate, rust, blue current",
                "water-worn iron and basalt sluice surfaces"),
            new HazardStageToneProfile(
                "ember-bastion", 3,
                "iron ember fortress, warm fire, cyan ward contrast",
                "fortress floor armor plates with ember pressure burns"),
            new HazardStageToneProfile(
                "ash-march", 3,
                "desaturated ash execution road, pale judgment gold",
                "execution-road ash stone with subdued ceremonial trim"),
        };

        static readonly ReadOnlyCollection<HazardStageToneProfile> ToneProfileList =
            Array.AsReadOnly(ToneProfileArray);

        static readonly HazardSurfaceBinding[] BindingArray = BuildBindings();

        static readonly ReadOnlyCollection<HazardSurfaceBinding> BindingList =
            Array.AsReadOnly(BindingArray);

        public static IReadOnlyList<HazardStageToneProfile> ToneProfiles => ToneProfileList;

        public static IReadOnlyList<HazardSurfaceBinding> Bindings => BindingList;

        public static bool TryGetToneProfile(string stageId, out HazardStageToneProfile profile)
        {
            if (!string.IsNullOrEmpty(stageId))
            {
                for (var i = 0; i < ToneProfileArray.Length; i++)
                {
                    if (string.Equals(ToneProfileArray[i].StageId, stageId, StringComparison.Ordinal))
                    {
                        profile = ToneProfileArray[i];
                        return true;
                    }
                }
            }

            profile = default;
            return false;
        }

        public static bool TryGetBinding(string stageId, HazardKind kind, out HazardSurfaceBinding binding)
        {
            if (!string.IsNullOrEmpty(stageId))
            {
                for (var i = 0; i < BindingArray.Length; i++)
                {
                    var candidate = BindingArray[i];
                    if (candidate.Kind == kind
                        && string.Equals(candidate.StageId, stageId, StringComparison.Ordinal))
                    {
                        binding = candidate;
                        return true;
                    }
                }
            }

            binding = default;
            return false;
        }

        static HazardSurfaceBinding[] BuildBindings()
        {
            var bindings = new List<HazardSurfaceBinding>(40);
            var entries = StageCatalog.Entries;
            for (var stageIndex = 0; stageIndex < entries.Count; stageIndex++)
            {
                var stageId = entries[stageIndex].Id;
                if (!TryGetToneProfile(stageId, out _))
                    continue;

                var hazards = StageCatalog.PactFor(stageId);
                if (hazards == null)
                    continue;

                for (var hazardIndex = 0; hazardIndex < hazards.Length; hazardIndex++)
                {
                    AddBinding(bindings, stageId, hazards[hazardIndex].Kind);
                }

                AddRuntimeLayoutBindings(bindings, stageId);
            }

            return bindings.ToArray();
        }

        static void AddRuntimeLayoutBindings(List<HazardSurfaceBinding> bindings, string stageId)
        {
            // DungeonLayoutSpec.Compose supplies the physical lane blockers at runtime.
            // Some logical pacts expose only active gimmicks, but the texture manifest
            // still needs the stage-toned StoneWall body consumed by those blockers.
            AddBinding(bindings, stageId, HazardKind.StoneWall);
        }

        static void AddBinding(List<HazardSurfaceBinding> bindings, string stageId, HazardKind kind)
        {
            if (Contains(bindings, stageId, kind))
                return;

            var role = PrimaryRoleFor(kind);
            bindings.Add(new HazardSurfaceBinding(
                stageId,
                kind,
                role,
                ResourceRoot + stageId + "-" + KindTokenFor(kind) + "-" + role));
        }

        static bool Contains(List<HazardSurfaceBinding> bindings, string stageId, HazardKind kind)
        {
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding.Kind == kind && string.Equals(binding.StageId, stageId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        static string KindTokenFor(HazardKind kind)
        {
            switch (kind)
            {
                case HazardKind.EmberVent: return "ember-vent";
                case HazardKind.ObsidianPillar: return "obsidian-pillar";
                case HazardKind.RelicAltar: return "relic-altar";
                case HazardKind.TideCurrent: return "tide-current";
                case HazardKind.EmberPylon: return "ember-pylon";
                case HazardKind.AshWall: return "ash-wall";
                case HazardKind.StoneWall: return "stone-wall";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported hazard kind.");
            }
        }

        static string PrimaryRoleFor(HazardKind kind)
        {
            switch (kind)
            {
                case HazardKind.EmberVent: return "underlay";
                case HazardKind.ObsidianPillar: return "body";
                case HazardKind.RelicAltar: return "underlay";
                case HazardKind.TideCurrent: return "bed";
                case HazardKind.EmberPylon: return "underlay";
                case HazardKind.AshWall: return "band";
                case HazardKind.StoneWall: return "body";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported hazard kind.");
            }
        }
    }
}
