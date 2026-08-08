// Command agent, part 2/2 — the RUNNER: one step per FINISHED game event.
//
// A plan (CommandPlan) is a list of orders. This turns it into a sequence the
// player can watch resolve: every step gates on readiness, dispatches exactly
// one deterministic latch, waits for the SIMULATION to acknowledge it, lets the
// resulting event breathe, and only then advances. Nothing here polls Unity —
// the host pushes an observation each frame and applies the returned signal, so
// the whole state machine is pure C# and testable without a scene (same
// contract as CommandConsoleBuffer / CommandPlanParser).
//
// The simulation is never touched from here. A signal asks the host to set the
// SAME latch a keystroke would set; the sim's own rules still decide what that
// latch does, and a step that the sim refuses is REPORTED, never faked.
using System;
using CinderCourt.Sim;

namespace CinderCourt.View
{
    /// <summary>
    /// Everything the runner is allowed to know about the live run, pushed by
    /// the host once per frame. Primitives only — the agent must not hold a sim
    /// reference, or a stale plan could outlive the run that produced it.
    /// </summary>
    public struct CommandAgentObservation
    {
        /// <summary>False the moment the run stops (death, extraction, lobby).
        /// Aborts the whole sequence — orders for a dead run are noise.</summary>
        public bool RunLive;
        /// <summary>Lantern oil. Skills that cost more than this cannot fire.</summary>
        public float Charge;
        public float BoltCooldown;
        public float PulseCooldown;
        public float NovaCooldown;
        public float AegisCooldown;
        public float DashCooldown;
        /// <summary>0 ⇒ companion orders are impossible, not merely slow.</summary>
        public int CompanionSlots;
        /// <summary>Soonest ready slot (matches the HUD's own reduction).</summary>
        public float CompanionSkillCooldown;
        public bool CompanionCasting;
        /// <summary>CompanionBehavior.Hold — the ack for a Defend order.</summary>
        public bool CompanionHolding;
        public int LivingEnemies;
    }

    public enum CommandAgentSignalKind
    {
        None = 0,
        /// <summary>Set this intent's latch now, and say so.</summary>
        Dispatch = 1,
        /// <summary>Honest progress copy — usually a step the sim refused.</summary>
        Note = 2,
        Finished = 3,
        Aborted = 4,
    }

    /// <summary>One instruction to the host per tick. Never more: a frame that
    /// both dispatched and finished would race two toasts into one line.</summary>
    public readonly struct CommandAgentSignal
    {
        public static readonly CommandAgentSignal None = default;

        public readonly CommandAgentSignalKind Kind;
        public readonly CompanionCommandIntent Intent;
        /// <summary>Progress prefix for Dispatch ("2/4 • "), full copy otherwise.</summary>
        public readonly string Message;
        /// <summary>Optional model rationale for this step ("몰린 적 정리").</summary>
        public readonly string Detail;
        public readonly int StepIndex;    // 1-based, for display
        public readonly int StepCount;

        public CommandAgentSignal(CommandAgentSignalKind kind, CompanionCommandIntent intent,
            string message, string detail, int stepIndex, int stepCount)
        {
            Kind = kind;
            Intent = intent;
            Message = message;
            Detail = detail;
            StepIndex = stepIndex;
            StepCount = stepCount;
        }
    }

    /// <summary>
    /// Per-intent gate / acknowledgement / settle contract. Costs and cooldowns
    /// are read from HackSpec, never re-typed: CLAUDE.md §2 makes the numbers the
    /// gate, and a second copy of 45 oil here would be a second contract.
    /// </summary>
    public static class CommandAgentSpec
    {
        /// <summary>Longest a SEQUENCE step may wait for its resource. One step
        /// over the longest cooldown in the kit (void-aegis, 12 s) so a fully
        /// spent bar still resolves instead of reporting a false failure.</summary>
        public static readonly float GateTimeout = HackSpec.AegisCooldown + 2f;
        /// <summary>The latch is consumed by the next fixed step (1/60 s); 1.5 s
        /// of slack covers a hitching frame and still fails fast when the sim
        /// silently refused the order.</summary>
        public const float AckTimeout = 1.5f;
        /// <summary>Cooldown at/below this counts as ready.</summary>
        public const float ReadyEpsilon = 0.0001f;
        /// <summary>A cast pushes the cooldown to its full value, so any rise
        /// this large is the acknowledgement we are waiting for.</summary>
        public const float AckEpsilon = 0.05f;

        public const float SkillSettle = 0.45f;
        public const float DashSettle = 0.3f;
        public const float StanceSettle = 0.35f;
        public const float CompanionSkillSettle = 0.6f;

        // Gate reasons. Named because the runner branches on one of them
        // (a missing guardian can never resolve by waiting) and because the
        // console shows them verbatim — "실패" tells the player nothing.
        public const string BlockedNoCompanion = "동료 없음";
        public const string BlockedCharge = "기름 부족";
        public const string BlockedCooldown = "쿨다운";

        /// <summary>Orders that mean nothing without a guardian on the field.</summary>
        public static bool NeedsCompanion(CompanionCommandIntent intent)
            => intent == CompanionCommandIntent.FocusAttack
            || intent == CompanionCommandIntent.Defend
            || intent == CompanionCommandIntent.Recall
            || intent == CompanionCommandIntent.CompanionSkill;

        /// <summary>PickupInfo is pure feedback — there is no latch to confirm.</summary>
        public static bool RequiresAck(CompanionCommandIntent intent)
            => intent != CompanionCommandIntent.PickupInfo
            && intent != CompanionCommandIntent.Unknown;

        public static float CostOf(CompanionCommandIntent intent) => intent switch
        {
            CompanionCommandIntent.SkillBolt => HackSpec.BoltCost,
            CompanionCommandIntent.SkillPulse => HackSpec.PulseCost,
            CompanionCommandIntent.SkillNova => HackSpec.AshNovaCost,
            CompanionCommandIntent.SkillAegis => HackSpec.AegisCost,
            CompanionCommandIntent.SkillDash => HackSpec.DashCost,
            _ => 0f,
        };

        public static float CooldownOf(CompanionCommandIntent intent, in CommandAgentObservation o)
            => intent switch
            {
                CompanionCommandIntent.SkillBolt => o.BoltCooldown,
                CompanionCommandIntent.SkillPulse => o.PulseCooldown,
                CompanionCommandIntent.SkillNova => o.NovaCooldown,
                CompanionCommandIntent.SkillAegis => o.AegisCooldown,
                CompanionCommandIntent.SkillDash => o.DashCooldown,
                CompanionCommandIntent.CompanionSkill => o.CompanionSkillCooldown,
                _ => 0f,
            };

        public static float SettleOf(CompanionCommandIntent intent) => intent switch
        {
            CompanionCommandIntent.SkillDash => DashSettle,
            CompanionCommandIntent.SkillBolt => SkillSettle,
            CompanionCommandIntent.SkillPulse => SkillSettle,
            CompanionCommandIntent.SkillNova => SkillSettle,
            CompanionCommandIntent.SkillAegis => SkillSettle,
            CompanionCommandIntent.CompanionSkill => CompanionSkillSettle,
            CompanionCommandIntent.PickupInfo => 0f,
            _ => StanceSettle,
        };

        /// <summary>Ready to dispatch? <paramref name="blocked"/> names the
        /// reason when not, so the console can be specific instead of "실패".</summary>
        public static bool TryGate(CompanionCommandIntent intent,
            in CommandAgentObservation observation, out string blocked)
        {
            blocked = null;
            if (NeedsCompanion(intent) && observation.CompanionSlots <= 0)
            {
                blocked = BlockedNoCompanion;
                return false;
            }
            var cost = CostOf(intent);
            if (cost > 0f && observation.Charge + 0.001f < cost)
            {
                blocked = BlockedCharge;
                return false;
            }
            if (CooldownOf(intent, observation) > ReadyEpsilon)
            {
                blocked = BlockedCooldown;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Did the SIMULATION take the order? Cooldown-backed intents answer by
        /// rising above the value observed at dispatch (the gate guarantees that
        /// value was ~0, so any full-cooldown reset clears this). Stance orders
        /// answer with the behavior they were supposed to produce.
        /// </summary>
        public static bool Acknowledged(CompanionCommandIntent intent,
            in CommandAgentObservation observation, float dispatchCooldown)
        {
            switch (intent)
            {
                case CompanionCommandIntent.Defend:
                    return observation.CompanionHolding;
                case CompanionCommandIntent.FocusAttack:
                case CompanionCommandIntent.Recall:
                    return !observation.CompanionHolding;
                case CompanionCommandIntent.CompanionSkill:
                    return observation.CompanionCasting
                        || observation.CompanionSkillCooldown > dispatchCooldown + AckEpsilon;
                case CompanionCommandIntent.PickupInfo:
                case CompanionCommandIntent.Unknown:
                    return true;
                default:
                    return CooldownOf(intent, observation) > dispatchCooldown + AckEpsilon;
            }
        }

        /// <summary>HUD copy. Mirrors HudView.ApplyCommandIntent's wording so a
        /// skipped step and a fired step name the same thing.</summary>
        public static string LabelOf(CompanionCommandIntent intent) => intent switch
        {
            CompanionCommandIntent.FocusAttack => "집중공격",
            CompanionCommandIntent.Defend => "방어 태세",
            CompanionCommandIntent.Recall => "복귀",
            CompanionCommandIntent.PickupInfo => "아이템",
            CompanionCommandIntent.SkillBolt => "균열 화살",
            CompanionCommandIntent.SkillPulse => "묘지 파동",
            CompanionCommandIntent.SkillNova => "잿불 노바",
            CompanionCommandIntent.SkillAegis => "공허 방패",
            CompanionCommandIntent.SkillDash => "질주",
            CompanionCommandIntent.CompanionSkill => "동료 특기",
            _ => "대기",
        };
    }

    /// <summary>
    /// Runs one plan at a time. Begin replaces whatever was in flight — a player
    /// who types a new order means it, and two live sequences would fight over
    /// the same latches.
    /// </summary>
    public sealed class CommandSequenceRunner
    {
        public enum StepPhase
        {
            /// <summary>Waiting for cost + cooldown (and a companion, if needed).</summary>
            Gate = 0,
            /// <summary>Latch set; waiting for the sim to show it took.</summary>
            Ack = 1,
            /// <summary>Event window — the beat the player actually sees.</summary>
            Settle = 2,
        }

        CommandPlan _plan = CommandPlan.Empty;
        int _index;
        StepPhase _phase;
        float _phaseTimer;
        float _dispatchCooldown;
        float _gateTimeout;
        bool _hasPending;
        CommandAgentSignal _pending;

        /// <summary>True while a plan (or its final signal) is still in flight.</summary>
        public bool Active => _hasPending || _index < _plan.Count;
        public int Count => _plan.Count;
        public int StepNumber => _plan.Count == 0 ? 0 : Math.Min(_index + 1, _plan.Count);
        public StepPhase Phase => _phase;
        public CommandPlan Plan => _plan;

        public void Begin(CommandPlan plan)
        {
            _plan = plan ?? CommandPlan.Empty;
            _index = 0;
            _phase = StepPhase.Gate;
            _phaseTimer = 0f;
            _dispatchCooldown = 0f;
            _hasPending = false;
            _pending = CommandAgentSignal.None;
            // A LONE order keeps the console's original semantics: it fires now
            // or it reports why it cannot. Only a real sequence may wait out a
            // cooldown — that waiting is the entire point of ordering the steps,
            // but on a single "노바" it would fire minutes after the player asked.
            _gateTimeout = _plan.IsSequence ? CommandAgentSpec.GateTimeout : 0f;
        }

        public void Cancel()
        {
            _plan = CommandPlan.Empty;
            _index = 0;
            _phase = StepPhase.Gate;
            _phaseTimer = 0f;
            _dispatchCooldown = 0f;
            _hasPending = false;
            _pending = CommandAgentSignal.None;
        }

        /// <summary>Advances the sequence by one frame and returns at most one
        /// instruction. <paramref name="deltaSeconds"/> is game time on purpose:
        /// the console's own slow-mo must not trip the ack/gate timeouts.</summary>
        public CommandAgentSignal Tick(float deltaSeconds, in CommandAgentObservation observation)
        {
            if (_hasPending)
            {
                _hasPending = false;
                var queued = _pending;
                _pending = CommandAgentSignal.None;
                return queued;
            }
            if (_index >= _plan.Count) return CommandAgentSignal.None;

            if (!observation.RunLive)
            {
                var aborted = Signal(CommandAgentSignalKind.Aborted,
                    CompanionCommandIntent.Unknown, "전투 종료 — 시퀀스 중단", null);
                Cancel();
                return aborted;
            }

            if (deltaSeconds > 0f) _phaseTimer += deltaSeconds;
            var step = _plan.StepAt(_index);

            if (step.Kind == CommandStepKind.Wait)
                return _phaseTimer >= step.Seconds ? Advance() : CommandAgentSignal.None;

            switch (_phase)
            {
                case StepPhase.Gate:
                    return TickGate(step, observation);
                case StepPhase.Ack:
                    return TickAck(step, observation);
                default:
                    return _phaseTimer >= CommandAgentSpec.SettleOf(step.Intent)
                        ? Advance()
                        : CommandAgentSignal.None;
            }
        }

        CommandAgentSignal TickGate(in CommandStep step, in CommandAgentObservation observation)
        {
            if (!CommandAgentSpec.TryGate(step.Intent, observation, out var blocked))
            {
                // "동료 없음" can never resolve by waiting — skip it immediately
                // rather than stalling the rest of the sequence behind it.
                var hopeless = blocked == CommandAgentSpec.BlockedNoCompanion;
                if (!hopeless && _phaseTimer < _gateTimeout) return CommandAgentSignal.None;
                return Skip(step.Intent, blocked + " — 건너뜀");
            }

            _dispatchCooldown = CommandAgentSpec.CooldownOf(step.Intent, observation);
            _phase = CommandAgentSpec.RequiresAck(step.Intent) ? StepPhase.Ack : StepPhase.Settle;
            _phaseTimer = 0f;
            return Signal(CommandAgentSignalKind.Dispatch, step.Intent, Progress(), step.Say);
        }

        CommandAgentSignal TickAck(in CommandStep step, in CommandAgentObservation observation)
        {
            if (CommandAgentSpec.Acknowledged(step.Intent, observation, _dispatchCooldown))
            {
                _phase = StepPhase.Settle;
                _phaseTimer = 0f;
                return CommandAgentSignal.None;
            }
            if (_phaseTimer < CommandAgentSpec.AckTimeout) return CommandAgentSignal.None;
            // The latch was set and the sim did nothing with it. Say so — a
            // silent skip here is exactly the lie this project forbids.
            return Skip(step.Intent, "반응 없음 — 건너뜀");
        }

        /// <summary>Reports a refused step and moves on. A queued Finished (this
        /// was the last step) rides out on the NEXT tick, one signal per frame.</summary>
        CommandAgentSignal Skip(CompanionCommandIntent intent, string reason)
        {
            var note = Signal(CommandAgentSignalKind.Note, intent,
                Progress() + CommandAgentSpec.LabelOf(intent) + " " + reason, null);
            var advanced = Advance();
            if (advanced.Kind != CommandAgentSignalKind.None)
            {
                _pending = advanced;
                _hasPending = true;
            }
            return note;
        }

        CommandAgentSignal Advance()
        {
            _index++;
            _phase = StepPhase.Gate;
            _phaseTimer = 0f;
            _dispatchCooldown = 0f;
            if (_index < _plan.Count) return CommandAgentSignal.None;

            var finished = Signal(CommandAgentSignalKind.Finished, CompanionCommandIntent.Unknown,
                _plan.Count > 1 ? $"시퀀스 완료 • {_plan.Count}단계" : "명령 완료", null);
            _plan = CommandPlan.Empty;
            _index = 0;
            return finished;
        }

        string Progress() => _plan.Count > 1 ? $"{_index + 1}/{_plan.Count} • " : "";

        CommandAgentSignal Signal(CommandAgentSignalKind kind, CompanionCommandIntent intent,
            string message, string detail)
            => new CommandAgentSignal(kind, intent, message, detail,
                _plan.Count == 0 ? 0 : Math.Min(_index + 1, _plan.Count), _plan.Count);
    }
}
