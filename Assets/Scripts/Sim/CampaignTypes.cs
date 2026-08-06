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

        /// <summary>
        /// Ash-wall timetable crush band (docs/SIM_SPEC_DUNGEONS.md §Gimmick 3 v1.1).
        /// Edge encoding rides the existing PushX field: +1 advances from the left
        /// edge (x 248) rightward, −1 advances from the right edge (x 1288) leftward.
        /// </summary>
        public static HazardConfig Wall(float phase, bool fromRight = false) => new HazardConfig
        {
            Kind = HazardKind.AshWall,
            X = fromRight ? CampaignSpec.WallEdgeRightX : CampaignSpec.WallEdgeX,
            Y = SimConfig.ArenaY,
            Phase = phase,
            PushX = fromRight ? -1f : 1f,
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

        // --- amendment #5 v1.1 (docs/SIM_SPEC_DUNGEONS.md REVISION v1.1) ----
        // Retune rationale: gimmicks must bite the combat convergence point
        // (768,604) — see design/gimmick-retune-spec.md.

        /// <summary>Tide-current push band (§Gimmick 1). Rect test, NOT iso-weighted.</summary>
        public const float CurrentHalfW = 520f;
        public const float CurrentHalfH = 110f;   // bands y 360-580/630-850: safe corridor 50px
        public const float CurrentPeriod = 6f;
        public const float CurrentTelegraph = 0.8f;
        public const float CurrentActive = 3.2f;  // threat duty 53%
        public const float CurrentPush = 200f;    // vs player 218 — upstream walking ~pinned

        /// <summary>Ember-pylon enemy shield (§Gimmick 2).</summary>
        public const float PylonBodyRadius = 30f;
        public const float PylonAuraRadius = 280f;  // covers spawn point from all 3 pylons
        public const float PylonHp = 300f;
        public const float PylonAuraDamageTakenMult = 0.40f;  // -60%: unmissable shield

        /// <summary>Ash-wall timetable (§Gimmick 3). Cycle 23.0 s, both edges cross centre.</summary>
        public const float WallEdgeX = 248f;
        public const float WallEdgeRightX = 1288f;
        public const float WallDepthMax = 560f;   // left max x808 / right max x728 — past centre 768
        public const float WallRest = 4.5f;
        public const float WallTelegraph = 1.5f;
        public const float WallAdvance = 7f;
        public const float WallHold = 3f;
        public const float WallRecede = 7f;
        public const float WallPeriod = WallRest + WallTelegraph + WallAdvance + WallHold + WallRecede;
        public const float WallSpeed = 80f;
        public const float WallTickDamage = 10f;
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
            HazardConfig.Vent(500f, 604f, 0.9f),   // v1.1: bomb the only safe corridor
            HazardConfig.Vent(1030f, 604f, 2.1f),
            HazardConfig.Pillar(768f, 604f),
        };

        private static readonly HazardConfig[] EmberBastionHazards =
        {
            HazardConfig.Pylon(560f, 500f),
            HazardConfig.Pylon(980f, 700f),
            HazardConfig.Pylon(768f, 430f),        // v1.1: third pylon — aura covers spawn
            HazardConfig.Pillar(640f, 650f),
            HazardConfig.Pillar(900f, 560f),
            HazardConfig.Vent(768f, 604f, 0.6f),
        };

        private static readonly HazardConfig[] AshMarchHazards =
        {
            HazardConfig.Wall(0f),
            HazardConfig.Wall(11.5f, fromRight: true),  // v1.1: closing jaws, half-period offset
            HazardConfig.Altar(768f, 604f),             // v1.1: corridor reward, periodically engulfed
            HazardConfig.Pylon(768f, 520f),             // v1.2 finale: bastion guards the corridor altar
            HazardConfig.Vent(560f, 760f, 0.6f),
            HazardConfig.Vent(980f, 450f, 1.8f),
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

    /// <summary>
    /// Training-ground trials (AMENDMENT #7 — design/training-and-surge-spec.md).
    /// One dominant gimmick per trial and NOTHING else: no spawns, no boss, no
    /// wave table. The survey's training cross-table is empty in exactly this
    /// column (T8 gimmick-only trial, 0/11) because an RNG title cannot make a
    /// hazard reproducible enough to practise. Ours are fixed-phase, so they are
    /// the one thing here that IS practisable.
    ///
    /// The pillar is deliberately absent: it is a static blocker, so "practising"
    /// it is not a skill (the cross-table row is empty for that reason).
    /// </summary>
    public static class TrainingTrials
    {
        public const string Vent = "trial-vent";
        public const string Current = "trial-current";
        public const string Pylon = "trial-pylon";
        public const string Wall = "trial-wall";
        public const string Altar = "trial-altar";

        /// <summary>Trial ids in lobby display order.</summary>
        public static readonly string[] Ids = { Vent, Current, Pylon, Wall, Altar };

        private static readonly HazardConfig[] VentTrial =
        {
            HazardConfig.Vent(568f, 484f, 0f),
            HazardConfig.Vent(968f, 484f, 0.6f),
            HazardConfig.Vent(568f, 724f, 1.2f),
            HazardConfig.Vent(968f, 724f, 1.8f),
        };

        private static readonly HazardConfig[] CurrentTrial =
        {
            // Band centres match the shipped cinder-sluice geometry (y 360-580 /
            // 630-850, a 50 px safe corridor). The first draft used 484/724,
            // which leaves 20 px — the browser showed the player vanishing
            // between two overlays at the spawn point.
            HazardConfig.Current(768f, 470f, CampaignSpec.CurrentPush, 0f),
            HazardConfig.Current(768f, 740f, -CampaignSpec.CurrentPush, 3f),
        };

        private static readonly HazardConfig[] PylonTrial =
        {
            HazardConfig.Pylon(568f, 604f),
            HazardConfig.Pylon(968f, 604f),
            HazardConfig.Pylon(768f, 460f),
        };

        private static readonly HazardConfig[] WallTrial =
        {
            HazardConfig.Wall(0f),
            HazardConfig.Wall(CampaignSpec.WallPeriod * 0.5f, fromRight: true),
        };

        private static readonly HazardConfig[] AltarTrial =
        {
            HazardConfig.Altar(608f, 500f),
            HazardConfig.Altar(928f, 708f),
            // The watching vent sits BETWEEN the altars, never on the spawn point
            // (768, 604) — a probe found a parked player dying to it before the
            // trial could teach anything about channelling.
            HazardConfig.Vent(768f, 460f, 0f),
        };

        /// <summary>Trial index in <see cref="Ids"/>, or -1.</summary>
        public static int IndexOf(string trialId)
        {
            for (int index = 0; index < Ids.Length; index += 1)
            {
                if (string.Equals(Ids[index], trialId, System.StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// Hazard placement for a trial. Tier does not change the layout — it
        /// scales the CLOCK in the sim (HackSpec.TrainingTierRate), never the
        /// telegraph seconds and never the roster.
        /// </summary>
        public static bool TryGet(string trialId, out HazardConfig[] hazards)
        {
            switch (IndexOf(trialId))
            {
                case 0: hazards = VentTrial; return true;
                case 1: hazards = CurrentTrial; return true;
                case 2: hazards = PylonTrial; return true;
                case 3: hazards = WallTrial; return true;
                case 4: hazards = AltarTrial; return true;
                default: hazards = System.Array.Empty<HazardConfig>(); return false;
            }
        }
    }
}
