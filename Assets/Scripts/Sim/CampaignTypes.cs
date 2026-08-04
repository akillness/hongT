// FROZEN CONTRACT AMENDMENT — numeric truth: docs/SIM_SPEC_CAMPAIGN.md.
// Additive only: SIM_SPEC.md arena rules are untouched (default CinderSim() path).
// Pure C#. No UnityEngine references allowed in this assembly (asmdef enforces).
using System;
using System.Collections.Generic;

namespace CinderCourt.Sim
{
    /// <summary>Dungeon gimmick archetype (docs/SIM_SPEC_CAMPAIGN.md §Dungeon gimmicks).</summary>
    public enum HazardKind { EmberVent = 0, ObsidianPillar = 1, RelicAltar = 2 }

    /// <summary>Equipment shard slot. Index order is the drop modulus order.</summary>
    public enum EquipSlot { Weapon = 0, Lantern = 1, Cloak = 2 }

    /// <summary>Deterministic placement record for one gimmick. Phase is vent-only.</summary>
    public struct HazardConfig
    {
        public HazardKind Kind;
        public float X, Y;
        public float Radius;
        public float Phase;

        public static HazardConfig Vent(float x, float y, float phase) => new HazardConfig
        {
            Kind = HazardKind.EmberVent,
            X = x,
            Y = y,
            Radius = CampaignSpec.VentRadius,
            Phase = phase,
        };

        public static HazardConfig Pillar(float x, float y) => new HazardConfig
        {
            Kind = HazardKind.ObsidianPillar,
            X = x,
            Y = y,
            Radius = CampaignSpec.PillarRadius,
            Phase = 0f,
        };

        public static HazardConfig Altar(float x, float y) => new HazardConfig
        {
            Kind = HazardKind.RelicAltar,
            X = x,
            Y = y,
            Radius = CampaignSpec.AltarRadius,
            Phase = 0f,
        };
    }

    /// <summary>
    /// Per-tick gimmick state published to the view. Vents fill
    /// <see cref="CycleT"/>/<see cref="Telegraphing"/>, altars fill <see cref="CooldownT"/>,
    /// pillars are static.
    /// </summary>
    public struct HazardState
    {
        public HazardKind Kind;
        public float X, Y;
        public float Radius;
        public float CycleT;        // fmod(stageTime + phase, period)
        public bool Telegraphing;   // CycleT is inside the 0.8 s warning window
        public float CooldownT;     // altar seconds left before it can bless again
    }

    /// <summary>
    /// One campaign run setup. Waves 1..<see cref="Waves"/> follow the arena numeric
    /// contract; wave <c>Waves + 1</c> is the stage boss wave. Equipment ranks are the
    /// carried-over progression and are applied once at run start.
    /// </summary>
    public struct CampaignConfig
    {
        public string StageId;
        public int StageIndex;
        public int Waves;
        public EnemyVisual BossVisual;
        public HazardConfig[] Hazards;
        public int WeaponRank;
        public int LanternRank;
        public int CloakRank;

        /// <summary>Attack power after the weapon shard: 58 * (1 + 0.06r).</summary>
        public float PlayerDamage =>
            SimConfig.PlayerDamage * (1f + CampaignSpec.WeaponDamagePerRank * CampaignSpec.ClampRank(WeaponRank));

        /// <summary>Lantern regeneration after the lantern shard: 7 * (1 + 0.08r).</summary>
        public float LanternRegenPerSecond =>
            SimConfig.LanternRegenPerSecond * (1f + CampaignSpec.LanternRegenPerRank * CampaignSpec.ClampRank(LanternRank));

        /// <summary>Max health after the cloak shard: 100 + 8r.</summary>
        public float PlayerMaxHealth =>
            SimConfig.PlayerMaxHealth + CampaignSpec.CloakHealthPerRank * CampaignSpec.ClampRank(CloakRank);
    }

    /// <summary>
    /// Read-only campaign view. Every arena member keeps its SIM_SPEC meaning; the
    /// campaign members are inert on an arena run (empty stage id, no hazards, rank 0).
    /// </summary>
    public interface ICampaignSnapshot : ISimSnapshot
    {
        string StageId { get; }
        bool BossAlive { get; }
        bool StageCleared { get; }
        IReadOnlyList<HazardState> Hazards { get; }
        int WeaponRank { get; }
        int LanternRank { get; }
        int CloakRank { get; }
    }

    /// <summary>All frozen numeric constants from docs/SIM_SPEC_CAMPAIGN.md.</summary>
    public static class CampaignSpec
    {
        public const float VentRadius = 90f;
        public const float VentPeriod = 2.4f;
        public const float VentTelegraph = 0.8f;
        public const float VentDamage = 8f;

        public const float PillarRadius = 40f;
        public const float PlayerPushRadius = 26f;
        public const float EnemyPushRadius = 22f;

        public const float AltarRadius = 70f;
        public const float AltarHoldSeconds = 1.2f;
        public const float AltarOilBurst = 18f;
        public const float AltarCooldown = 6f;

        public const int EquipSlotCount = 3;
        public const int MaxEquipRank = 5;
        public const float WeaponDamagePerRank = 0.06f;
        public const float LanternRegenPerRank = 0.08f;
        public const float CloakHealthPerRank = 8f;

        /// <summary>Ordinary kill drops a shard when <c>enemyId % 7 == 3</c>.</summary>
        public const int ShardDropModulus = 7;
        public const int ShardDropRemainder = 3;

        /// <summary>Boss wave escorts: min(8, 3 + stageIndex*2).</summary>
        public const int EscortBase = 3;
        public const int EscortPerStage = 2;
        public const int EscortCap = 8;

        public const string StageClearReason = "stage-clear";

        public static int ClampRank(int rank)
        {
            if (rank < 0)
            {
                return 0;
            }
            return rank > MaxEquipRank ? MaxEquipRank : rank;
        }
    }

    /// <summary>The three shipped stages (docs/SIM_SPEC_CAMPAIGN.md §Stages, §배치 테이블).</summary>
    public static class CampaignStages
    {
        public const string CinderSpan = "cinder-span";
        public const string AbyssChancel = "abyss-chancel";
        public const string EchoThrone = "echo-throne";

        private static readonly string[] AllIds = { CinderSpan, AbyssChancel, EchoThrone };

        private static readonly HazardConfig[] CinderSpanHazards =
        {
            HazardConfig.Vent(560f, 480f, 0f),
            HazardConfig.Vent(980f, 720f, 1.2f),
        };

        private static readonly HazardConfig[] AbyssChancelHazards =
        {
            HazardConfig.Pillar(640f, 500f),
            HazardConfig.Pillar(900f, 700f),
            HazardConfig.Pillar(768f, 604f),
            HazardConfig.Vent(1100f, 450f, 0.6f),
        };

        private static readonly HazardConfig[] EchoThroneHazards =
        {
            HazardConfig.Altar(768f, 604f),
            HazardConfig.Vent(500f, 700f, 0f),
            HazardConfig.Vent(1030f, 480f, 1.2f),
        };

        /// <summary>Stage ids in campaign order.</summary>
        public static IReadOnlyList<string> Ids => AllIds;

        /// <summary>Stage order index, or -1 when the id is unknown.</summary>
        public static int IndexOf(string stageId)
        {
            for (int index = 0; index < AllIds.Length; index += 1)
            {
                if (string.Equals(AllIds[index], stageId, StringComparison.Ordinal))
                {
                    return index;
                }
            }
            return -1;
        }

        /// <summary>Build a run config for a stage id with the carried equipment ranks.</summary>
        public static bool TryGet(string stageId, int weaponRank, int lanternRank, int cloakRank, out CampaignConfig config)
        {
            int index = IndexOf(stageId);
            if (index < 0)
            {
                config = default;
                return false;
            }
            config = Build(index, weaponRank, lanternRank, cloakRank);
            return true;
        }

        /// <summary>Build a run config by stage order index (0..2).</summary>
        public static CampaignConfig ForIndex(int stageIndex, int weaponRank, int lanternRank, int cloakRank)
        {
            if (stageIndex < 0 || stageIndex >= AllIds.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(stageIndex));
            }
            return Build(stageIndex, weaponRank, lanternRank, cloakRank);
        }

        private static CampaignConfig Build(int stageIndex, int weaponRank, int lanternRank, int cloakRank)
        {
            var config = default(CampaignConfig);
            config.StageId = AllIds[stageIndex];
            config.StageIndex = stageIndex;
            config.WeaponRank = CampaignSpec.ClampRank(weaponRank);
            config.LanternRank = CampaignSpec.ClampRank(lanternRank);
            config.CloakRank = CampaignSpec.ClampRank(cloakRank);

            if (stageIndex == 0)
            {
                config.Waves = 5;
                config.BossVisual = EnemyVisual.BossCommander;
                config.Hazards = CinderSpanHazards;
            }
            else if (stageIndex == 1)
            {
                config.Waves = 6;
                config.BossVisual = EnemyVisual.BossCommander;
                config.Hazards = AbyssChancelHazards;
            }
            else
            {
                config.Waves = 7;
                config.BossVisual = EnemyVisual.BossMonarch;
                config.Hazards = EchoThroneHazards;
            }

            return config;
        }
    }
}
