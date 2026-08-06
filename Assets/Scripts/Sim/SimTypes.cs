// FROZEN CONTRACT — do not edit without updating docs/SIM_SPEC.md and both lanes.
// Pure C#. No UnityEngine references allowed in this assembly (asmdef enforces).
using System;
using System.Collections.Generic;

namespace CinderCourt.Sim
{
    /// <summary>Simulation mode. Mirrors the original page states.</summary>
    public enum SimMode { Running, WaveClear, GameOver }

    /// <summary>Animation action requested by the sim. View maps to Animator states.</summary>
    public enum ActorAction { Idle, Move, Run, Hit, BigHit, Attack, Critical, Avoid, Defence, Die, Show }

    /// <summary>Visual archetype only — combat numbers are identical per SIM_SPEC.</summary>
    public enum EnemyVisual { EmberCohort = 0, Scout = 1, Shade = 2, Possessed = 3, BossCommander = 4, BossMonarch = 5 }

    /// <summary>EquipShard is the campaign amendment kind (docs/SIM_SPEC_CAMPAIGN.md).</summary>
    public enum PickupKind { EmberShard = 0, OilFlask = 1, RelicMote = 2, EquipShard = 3 }

    /// <summary>Per-tick input sample fed by the adapter. All fields are polled state.</summary>
    public struct SimInput
    {
        public float MoveX;      // -1..1 (already merged keyboard+touch)
        public float MoveY;      // -1..1 (screen-down positive, original convention)
        public bool AttackQueued;
        public bool NovaQueued;
        public bool WardQueued;
        public bool RestartQueued;
        // --- hack & slash amendment (docs/SIM_SPEC_HACKSLASH.md §12) ---
        public bool DashQueued;
        public bool BoltQueued;
        public bool PulseQueued;
        public bool CompanionHoldQueued;
        public bool CompanionRecallQueued;
        /// <summary>AMENDMENT #8 (A8.3): one-shot, GLOBAL like hold/recall — it orders every
        /// active slot whose skill is off cooldown to cast now, bypassing the archetype's
        /// auto-fire target threshold. A slot still on cooldown ignores it; the command is
        /// never buffered.</summary>
        public bool CompanionSkillQueued;
        // --- input depth amendment (_workspace/current/design/input-depth-spec.md) ---
        /// <summary>§3: sustained, not an edge. True for every tick the attack
        /// key is DOWN. Deliberately a plain held bool: classifying a tap by
        /// waiting for release would add up to 250 ms of latency to the most
        /// frequently pressed key in the game.</summary>
        public bool AttackHeld;
        /// <summary>§5: 0 = no choice this tick, 1..3 = the level-up option
        /// the player picked. Edge-consumed by the sim.</summary>
        public int GrowthChoice;
    }

    public struct PlayerState
    {
        public float X, Y;
        public int Facing;           // +1 right, -1 left
        public float Health;
        public float AttackCooldown;
        public float DamageCooldown;
        public float WardTime;
        public bool Moving;
        public ActorAction Action;
        public float ActionTime;     // seconds since action started
        public int AttackId;
    }

    public struct EnemyState
    {
        public int Id;
        public EnemyVisual Visual;
        public float X, Y;
        public int Facing;
        public float Health;
        public float MaxHealth;
        public bool Dead;
        public float FadeTime;
        public ActorAction Action;
        public float ActionTime;
        public bool IsBoss;
        public float Scale;          // 1.0 normal, 1.6 boss
    }

    public struct PickupState
    {
        public int Id;
        public PickupKind Kind;
        public float X, Y;
        public float Life;
        public float Bob;
    }

    /// <summary>One-frame event flags for presentation (audio/VFX). Cleared each tick.</summary>
    [Flags]
    public enum SimEvents
    {
        None = 0,
        PlayerStruck = 1 << 0,   // player swung (strike cue)
        EnemyHit = 1 << 1,       // any enemy damaged (hit cue)
        EnemyKilled = 1 << 2,    // kill cue
        NovaCast = 1 << 3,
        WardCast = 1 << 4,
        PickupCollected = 1 << 5,
        WaveStarted = 1 << 6,
        GameOver = 1 << 7,
        PlayerDamaged = 1 << 8,
        BossSpawned = 1 << 9,
        // --- campaign amendment (docs/SIM_SPEC_CAMPAIGN.md) ---
        StageCleared = 1 << 10,
        HazardPulse = 1 << 11,
        AltarBlessing = 1 << 12,
        EquipDropped = 1 << 13,
        // --- hack & slash amendment (docs/SIM_SPEC_HACKSLASH.md §12) ---
        DashUsed = 1 << 14,
        BoltCast = 1 << 15,
        PulseCast = 1 << 16,
        LevelUp = 1 << 17,
        EliteDown = 1 << 18,
        ExtractionComplete = 1 << 19,
        BossPhase2 = 1 << 20,
        ComboFinisher = 1 << 21,
        // --- companion signature skills (docs/SIM_SPEC_HACKSLASH.md A8) ---
        /// <summary>A8.5: at least one companion slot cast its signature skill this tick.</summary>
        CompanionSkillCast = 1 << 22,
    }

    public struct RunDigest
    {
        public int Score, Wave, Kills, Relics;
        public float HealthRemaining;
        public string Reason;
    }

    /// <summary>
    /// Read-only view of the sim after a tick. View layer may keep the reference;
    /// lists are reused (do not cache elements across ticks).
    /// </summary>
    public interface ISimSnapshot
    {
        SimMode Mode { get; }
        int Wave { get; }
        int Score { get; }
        int Kills { get; }
        int Relics { get; }
        float Charge { get; }            // lantern oil 0..100
        float NovaCooldown { get; }
        float WardCooldown { get; }
        float NovaFlash { get; }         // seconds remaining of nova flash
        int PendingSpawns { get; }
        int LivingEnemies { get; }
        PlayerState Player { get; }
        IReadOnlyList<EnemyState> Enemies { get; }
        IReadOnlyList<PickupState> Pickups { get; }
        SimEvents Events { get; }
        /// <summary>Last nova origin (valid when NovaCast event set).</summary>
        float NovaX { get; }
        float NovaY { get; }
        RunDigest Digest { get; }
    }

    /// <summary>Deterministic fixed-step simulation. Implemented in CinderSim.cs.</summary>
    public interface ICinderSim : ISimSnapshot
    {
        /// <summary>Advance exactly one fixed step (1/60 s) with the given input.</summary>
        void Tick(in SimInput input);
        void Restart();
    }

    /// <summary>All frozen numeric constants from docs/SIM_SPEC.md.</summary>
    public static class SimConfig
    {
        public const float FixedStep = 1f / 60f;
        public const float MaxFrameDelta = 0.25f;
        public const int MaxCatchUpSteps = 5;

        public const float WorldWidth = 1536f, WorldHeight = 1024f;
        public const float ArenaX = 768f, ArenaY = 604f;
        public const float ArenaHalfWidth = 520f, ArenaHalfHeight = 270f;

        public const float PlayerMaxHealth = 100f;
        public const float PlayerSpeed = 218f;
        public const float PlayerDamage = 58f;
        public const float PlayerAttackRange = 160f;
        public const float PlayerAttackCooldown = 0.48f;
        public const float PlayerHitGrace = 0.38f;
        public const float PlayerMarginClamp = 34f;
        public const float PlayerStartYOffset = 42f;
        public const float AttackMoveScale = 0.42f;
        public const float YMoveScale = 0.68f;
        public const float AttackActiveFrom = 2f / 12f;   // clip frame 2 @12fps
        public const float AttackActiveTo = 4f / 12f;     // through frame 3

        public const float EnemyBaseHealth = 58f;
        public const float EnemyAttackRange = 76f;
        public const float EnemyAttackCooldown = 1.22f;
        public const int EnemyCap = 20;
        public const float EnemyMarginClamp = 24f;
        public const float EnemyContactBonus = 14f;
        public const float EnemyContactDelay = 2f / 12f;  // clip frame 2
        public const float EnemyFade = 0.34f;
        public const float SeparationRadius = 70f;
        public const float SeparationWeight = 0.76f;

        public const float LanternMax = 100f;
        public const float LanternRegenPerSecond = 7f;
        public const float LanternChargePerKill = 6f;
        public const float NovaCost = 45f, NovaCooldown = 6.5f, NovaRadius = 250f, NovaDamage = 96f;
        public const float WardCost = 30f, WardCooldown = 9f, WardDuration = 3f;

        public const float PickupLifetime = 12f;
        public const float PickupMagnetRadius = 78f;
        public const int RelicScore = 250;
        public const float EmberShardHeal = 18f;
        public const float OilFlaskCharge = 35f;

        public const float IsoY = 1.42f;
        public const float FacingArcTolerance = -18f;

        public const float WaveIntermission = 2.15f;
        public const float FirstSpawnDelay = 0.18f;

        public const int BossEveryWaves = 5;
        public const float BossHealthMul = 6f, BossDamageMul = 2f, BossSpeedMul = 0.7f, BossScale = 1.6f;

        public static readonly float[][] SpawnPoints =
        {
            new[] { 284f, 577f }, new[] { 421f, 405f }, new[] { 694f, 350f },
            new[] { 1027f, 389f }, new[] { 1239f, 570f }, new[] { 1138f, 743f },
            new[] { 848f, 840f }, new[] { 536f, 798f },
        };
    }
}
