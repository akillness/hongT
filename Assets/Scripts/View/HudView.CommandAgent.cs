// Command-console glue for the command agent (CommandPlan + CommandSequenceRunner).
//
// HudView owns the console, so it owns the sequence too — but none of the rules
// live here. This file is exactly three seams:
//   1. SyncCommandAgent — GameView pushes the primitives the runner may see.
//   2. TickCommandAgent — one signal per frame, applied to the SAME latches a
//      keystroke sets (ApplyCommandIntent), never to the simulation directly.
//   3. StartCommandPlan / cancel / remote-plan coroutine.
//
// Everything decidable is decided in the pure classes, which is why the agent is
// covered by EditMode tests without a scene.
using System.Collections.Generic;
using CinderCourt.Sim;
using UnityEngine;

namespace CinderCourt.View
{
    public partial class HudView
    {
        readonly CommandSequenceRunner _agent = new CommandSequenceRunner();
        CommandAgentObservation _agentObservation;

        /// <summary>Cancel vocabulary. Checked BEFORE the plan parse, so
        /// "공격 중단" stops the sequence instead of ordering another attack.</summary>
        static readonly string[] AgentCancelWords =
        {
            "취소", "중단", "정지", "그만", "멈춰", "멈춰라", "stop", "cancel", "abort",
        };

        /// <summary>True while a sequence is still walking its steps.</summary>
        public bool CommandSequenceActive => _agent.Active;
        /// <summary>1-based step the sequence is on (0 = idle). For tests/HUD.</summary>
        public int CommandSequenceStep => _agent.StepNumber;
        public int CommandSequenceCount => _agent.Count;

        /// <summary>
        /// Per-frame observation push (primitives only, like every other HUD
        /// sync). The runner must never hold a sim reference: a plan that
        /// outlived its run would keep reading a dead snapshot.
        /// </summary>
        public void SyncCommandAgent(bool runLive, float charge,
            IReadOnlyList<float> skillCooldowns, float dashCooldown,
            int companionSlots, float companionSkillCooldown, bool companionCasting,
            CompanionBehavior behavior, int livingEnemies)
        {
            _agentObservation.RunLive = runLive;
            _agentObservation.Charge = charge;
            if (skillCooldowns != null && skillCooldowns.Count >= HackSpec.SkillCount)
            {
                _agentObservation.BoltCooldown = skillCooldowns[HackSpec.SkillBolt];
                _agentObservation.PulseCooldown = skillCooldowns[HackSpec.SkillPulse];
                _agentObservation.NovaCooldown = skillCooldowns[HackSpec.SkillNova];
                _agentObservation.AegisCooldown = skillCooldowns[HackSpec.SkillAegis];
            }
            _agentObservation.DashCooldown = dashCooldown;
            _agentObservation.CompanionSlots = companionSlots;
            _agentObservation.CompanionSkillCooldown = companionSkillCooldown;
            _agentObservation.CompanionCasting = companionCasting;
            _agentObservation.CompanionHolding = behavior == CompanionBehavior.Hold;
            _agentObservation.LivingEnemies = livingEnemies;
        }

        /// <summary>One signal per frame. Game time (not unscaled) on purpose:
        /// the sim's cooldowns tick on the same clock, so the console's own
        /// slow-mo can never age a gate out from under a live cast.</summary>
        void TickCommandAgent()
        {
            if (!_agent.Active) return;

            // Dungeon-only surface (same gate as OpenCommandConsole). Dropping
            // back to the lobby mid-sequence must not leave latches firing into
            // whatever screen comes next.
            if (_dungeonRoot == null || !_dungeonRoot.activeSelf)
            {
                _agent.Cancel();
                _agentObservation.RunLive = false;
                return;
            }

            var signal = _agent.Tick(Time.deltaTime, _agentObservation);
            switch (signal.Kind)
            {
                case CommandAgentSignalKind.Dispatch:
                    ApplyCommandIntent(signal.Intent, signal.Message, signal.Detail);
                    break;
                case CommandAgentSignalKind.Note:
                    ShowConsoleToast(signal.Message, 2.5f);
                    break;
                case CommandAgentSignalKind.Finished:
                    ShowConsoleToast(signal.Message, 1.8f);
                    break;
                case CommandAgentSignalKind.Aborted:
                    ShowConsoleToast(signal.Message, 2.5f);
                    break;
            }
        }

        /// <summary>Hands a plan to the runner and announces it. A single-step
        /// plan says nothing here — its own dispatch toast is the announcement,
        /// so a lone "노바" reads exactly as it did before the agent existed.</summary>
        void StartCommandPlan(CommandPlan plan)
        {
            if (plan == null || plan.IsEmpty) return;
            var replaced = _agent.Active;
            _agent.Begin(plan);
            if (!plan.IsSequence && string.IsNullOrEmpty(plan.Summary)) return;

            var head = plan.IsSequence ? $"시퀀스 {plan.Count}단계" : "명령";
            var source = plan.Source == CommandPlanSource.Gemini ? " · 제미나이" : "";
            var summary = string.IsNullOrEmpty(plan.Summary) ? "" : " — " + plan.Summary;
            ShowConsoleToast((replaced ? "교체 · " : "") + head + source + summary, 2.5f);
        }

        /// <summary>"취소" / "중단" / "stop" → drop whatever is in flight.
        /// Returns true when the input was a control word and nothing else
        /// should interpret it.</summary>
        bool TryHandleAgentControl(string raw)
        {
            var normalized = raw.Trim().ToLowerInvariant();
            var isCancel = false;
            for (var i = 0; i < AgentCancelWords.Length; i++)
            {
                if (!normalized.Contains(AgentCancelWords[i])) continue;
                isCancel = true;
                break;
            }
            if (!isCancel) return false;

            if (!_agent.Active)
            {
                ShowConsoleToast("실행 중인 시퀀스가 없습니다", 1.5f);
                return true;
            }
            var step = _agent.StepNumber;
            var count = _agent.Count;
            _agent.Cancel();
            ShowConsoleToast($"시퀀스 취소 ({step}/{count}단계에서 중단)", 2f);
            return true;
        }

        /// <summary>Gemini planning path. Failure is reported with its REASON —
        /// a depleted key (429) and a garbled reply are different problems and
        /// the player can only act on one of them.</summary>
        System.Collections.IEnumerator PlanRemote(string raw)
        {
            yield return GeminiCommandClient.Plan(raw, (plan, failure) =>
            {
                _consoleBusy = false;
                if (plan == null || plan.IsEmpty)
                {
                    ShowConsoleToast(
                        $"시퀀스 해석 실패({failure}) — 키워드: 집중공격/방어/복귀/특기/노바/결계/파동/화살/질주",
                        3.5f);
                    return;
                }
                StartCommandPlan(plan);
            });
        }
    }
}
