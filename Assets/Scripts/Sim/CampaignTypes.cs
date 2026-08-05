// FROZEN CONTRACT AMENDMENT — numeric truth: docs/SIM_SPEC_CAMPAIGN.md.
// Additive only: SIM_SPEC.md arena rules are untouched (default CinderSim() path).
// Pure C#. No UnityEngine references allowed in this assembly (asmdef enforces).
using System;
using System.Collections.Generic;

namespace CinderCourt.Sim
{
    /// <summary>
    /// Dungeon gimmick archetype (docs/SIM_SPEC_CAMPAIGN.md §Dungeon gimmicks;
    /// 3..5 from docs/SIM_SPEC_DUNGEONS.md — amendment #5).
    /// </summary>
    public enum HazardKind
    {
        EmberVent = 0,
        ObsidianPillar = 1,
        RelicAltar = 2,
        TideCurrent = 3,
        EmberPylon = 4,
        AshWall = 5,
    }

    /// <summary>Equipment shard slot. Index order is the drop modulus order.</summary>
    public enum EquipSlot { Weapon = 0, Lantern = 1, Cloak = 2 }

    /// <summary>
    /// Deterministic placement record for one gimmick. Phase is vent/current/wall
    /// cycle offset. <see cref="HalfW"/>/<see cref="HalfH"/>/<see cref="PushX"/>/
    /// <see cref="PushY"/> are current-only; <see cref="Hp"/> is pylon-only
    /// (amendment #5 fields default to 0 and are inert on the original kinds).
    /// </summary>
    public struct HazardConfig
    {
        public HazardKind Kind;
        public float X, Y;
        public float Radius;
        public float Phase;
        public float HalfW, HalfH;
        public float PushX, PushY;
        public float Hp;

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

        /// <summary>Tide-current push band (docs/SIM_SPEC_DUNGEONS.md §Gimmick 1).</summary>
        public static HazardConfig Current(float x, float y, float pushX, float phase) => new HazardConfig
        {
            Kind = HazardKind.TideCurrent,
            X = x,
            Y = y,
            HalfW = CampaignSpec.CurrentHalfW,
            HalfH = CampaignSpec.CurrentHalfH,
            PushX = pushX,
            PushY = 0f,
            Phase = phase,
        };

        /// <summary>Ember-pylon enemy-shield object (docs/SIM_SPEC_DUNGEONS.md §Gimmick 2).</summary>
        public static HazardConfig Pylon(float x, float y) => new HazardConfig
        {
            Kind = HazardKind.EmberPylon,
            X = x,
            Y = y,
            Radius = CampaignSpec.PylonBodyRadius,
            Hp = CampaignSpec.PylonHp,
        };

        /// <summary>Ash-wall timetable crush band (docs/SIM_SPEC_DUNGEONS.md §Gimmick 3).</summary>
        public static HazardConfig Wall(float phase) => new HazardConfig
        {
            Kind = HazardKind.AshWall,
            X = CampaignSpec.WallEdgeX,
            Y = SimConfig.ArenaY,
            Phase = phase,
        };
    }

    /// <summary>
    /// Per-tick gimmick state published to the view. Vents fill
    /// <see cref="CycleT"/>/<see cref="Telegraphing"/>, altars fill <see cref="CooldownT"/>,
    /// pillars are static. Amendment #5: currents fill <see cref="Active"/>, walls
    /// fill <see cref="Active"/>/<see cref="FrontX"/>, pylons fill <see cref="Hp"/>
    /// (0 = destroyed).
    /// </summary>
    public struct HazardState
    {
        public HazardKind Kind;
        public float X, Y;
        public float Radius;
        public float CycleT;        // fmod(stageTime + phase, period)
        public bool Telegraphing;   // CycleT is inside the kind's warning window
        public float CooldownT;     // altar seconds left before it can bless again
        public bool Active;         // current push window / wall band live
        public float FrontX;        // wall leading edge (EdgeX when idle)
        public float Hp;            // pylon remaining hp
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

        // --- amendment #5 (docs/SIM_SPEC_DUNGEONS.md) -----------------------

        /// <summary>Tide-current push band (§Gimmick 1). Rect test, NOT iso-weighted.</summary>
        public const float CurrentHalfW = 520f;
        public const float CurrentHalfH = 70f;
        public const float CurrentPeriod = 6f;
        public const float CurrentTelegraph = 0.8f;
        public const float CurrentActive = 2.4f;
        public const float CurrentPush = 140f;

        /// <summary>Ember-pylon enemy shield (§Gimmick 2).</summary>
        public const float PylonBodyRadius = 30f;
        public const float PylonAuraRadius = 220f;
        public const float PylonHp = 240f;
        public const float PylonAuraDamageTakenMult = 0.60f;

        /// <summary>Ash-wall timetable (§Gimmick 3). Cycle 22.5 s.</summary>
        public const float WallEdgeX = 248f;
        public const float WallDepthMax = 360f;
        public const float WallRest = 9f;
        public const float WallTelegraph = 1.5f;
        public const float WallAdvance = 4.5f;
        public const float WallHold = 3f;
        public const float WallRecede = 4.5f;
        public const float WallPeriod = WallRest + WallTelegraph + WallAdvance + WallHold + WallRecede;
        public const float WallSpeed = 80f;
        public const float WallTickDamage = 8f;
        public const float WallTickPeriod = 0.6f;

        /// <summary>Ember Rest room index upper bound (§Ember Rest 확장).</summary>
        public const int MaxEmberRestRoomIndex = 8;

        public static int ClampRank(int rank)
        {
            if (rank < 0)
            {
                return 0;
            }
            return rank > MaxEquipRank ? MaxEquipRank : rank;
        }
    }

    /// <summary>
    /// The shipped sim anchors (docs/SIM_SPEC_CAMPAIGN.md §Stages 0..2;
    /// docs/SIM_SPEC_DUNGEONS.md §Stages 3..5).
    /// </summary>
    public static class CampaignStages
    {
        public const string CinderSpan = "cinder-span";
        public const string AbyssChancel = "abyss-chancel";
        public const string EchoThrone = "echo-throne";
        public const string CinderSluice = "cinder-sluice";
        public const string EmberBastion = "ember-bastion";
        public const string AshMarch = "ash-march";

        private static readonly string[] AllIds =
        {
            CinderSpan, AbyssChancel, EchoThrone, CinderSluice, EmberBastion, AshMarch,
        };

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

        private static readonly HazardConfig[] CinderSluiceHazards =
        {
            HazardConfig.Current(768f, 470f, CampaignSpec.CurrentPush, 0f),
            HazardConfig.Current(768f, 740f, -CampaignSpec.CurrentPush, 3f),
            HazardConfig.Pillar(768f, 604f),
        };

        private static readonly HazardConfig[] EmberBastionHazards =
        {
            HazardConfig.Pylon(560f, 500f),
            HazardConfig.Pylon(980f, 700f),
            HazardConfig.Pillar(640f, 650f),
            HazardConfig.Pillar(900f, 560f),
            HazardConfig.Vent(768f, 604f, 0.6f),
        };

        private static readonly HazardConfig[] AshMarchHazards =
        {
            HazardConfig.Wall(0f),
            HazardConfig.Altar(1100f, 604f),
            HazardConfig.Vent(980f, 480f, 1.2f),
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

        /// <summary>Build a run config by stage order index (0..5).</summary>
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
            else if (stageIndex == 2)
            {
                config.Waves = 7;
                config.BossVisual = EnemyVisual.BossMonarch;
                config.Hazards = EchoThroneHazards;
            }
            else if (stageIndex == 3)
            {
                config.Waves = 8;
                config.BossVisual = EnemyVisual.BossCommander;
                config.Hazards = CinderSluiceHazards;
            }
            else if (stageIndex == 4)
            {
                config.Waves = 8;
                config.BossVisual = EnemyVisual.BossCommander;
                config.Hazards = EmberBastionHazards;
            }
            else
            {
                config.Waves = 9;
                config.BossVisual = EnemyVisual.BossMonarch;
                config.Hazards = AshMarchHazards;
            }

            return config;
        }
    }
}
