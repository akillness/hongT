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
using UnityEngine.UI;

namespace CinderCourt.View
{
    public partial class HudView
    {
        readonly CommandSequenceRunner _agent = new CommandSequenceRunner();
        CommandAgentObservation _agentObservation;

        // W10 work queue: several orders parked at once, each released by a game
        // event. The runner still executes exactly one plan at a time — the
        // queue only decides WHEN the next one is handed over, so the latch
        // traffic the sim sees is unchanged.
        readonly CommandQueue _queue = new CommandQueue();
        GameObject _queuePanel;
        Text _queueHeader;
        Text[] _queueRows;

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
        /// <summary>Orders parked behind an event trigger. For tests/HUD.</summary>
        public int CommandQueueDepth => _queue.Count;
        /// <summary>The pending release condition, or "" when nothing is parked.</summary>
        public string CommandQueueHeadStatus => _queue.Head?.StatusLine ?? "";

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
            // Dungeon-only surface (same gate as OpenCommandConsole). Dropping
            // back to the lobby mid-sequence must not leave latches firing into
            // whatever screen comes next — and a queue parked for a fight that
            // ended is noise, so it dies with the run.
            if (_dungeonRoot == null || !_dungeonRoot.activeSelf)
            {
                if (_agent.Active || !_queue.IsEmpty)
                {
                    _agent.Cancel();
                    _queue.Clear();
                    _agentObservation.RunLive = false;
                    SyncCommandQueuePanel();
                }
                return;
            }

            ReleaseQueuedCommand();
            if (!_agent.Active) return;

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
            var source = plan.Source == CommandPlanSource.Gemini ? " • 제미나이" : "";
            var summary = string.IsNullOrEmpty(plan.Summary) ? "" : " — " + plan.Summary;
            ShowConsoleToast((replaced ? "교체 • " : "") + head + source + summary, 2.5f);
        }

        /// <summary>"취소" / "중단" / "stop" → drop whatever is in flight AND
        /// everything parked behind it. One cancel word clears the board: a
        /// player who stops the current order and then watches a queued one fire
        /// two seconds later has been lied to.
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

            var queuedCopy = _queue.CancelAll();
            SyncCommandQueuePanel();
            if (!_agent.Active)
            {
                ShowConsoleToast(queuedCopy ?? "실행 중인 시퀀스가 없습니다", 1.5f);
                return true;
            }
            var step = _agent.StepNumber;
            var count = _agent.Count;
            _agent.Cancel();
            var tail = queuedCopy == null ? "" : " • " + queuedCopy;
            ShowConsoleToast($"시퀀스 취소 ({step}/{count}단계에서 중단){tail}", 2f);
            return true;
        }

        // ============================================================= queue --

        /// <summary>
        /// Parks an order behind an event trigger when the sentence carries one.
        /// Returns false for a plain order so SubmitCommand's existing immediate
        /// path is untouched — a sentence with no trigger word costs one keyword
        /// scan and behaves exactly as it did before the queue existed.
        /// </summary>
        bool TryQueueCommand(string raw)
        {
            if (!CommandTriggerParser.TrySplit(raw, out var trigger, out var remainder, out var prefix))
                return false;

            // The trigger only counts if a real order follows it. Otherwise the
            // words fall through to the intent table, where "아이템 획득" has
            // always meant the PickupInfo reply and must keep meaning it.
            var plan = CommandPlanParser.ParseLocal(remainder);
            if (plan.IsEmpty) return false;

            // "노바 쓰고 셋 잡으면 결계" — the half before the trigger is an
            // order for RIGHT NOW. A qualifier ("적 셋") parses to nothing and
            // costs one empty parse; a real order would otherwise be dropped.
            var immediate = CommandPlanParser.ParseLocal(prefix);
            if (!immediate.IsEmpty) StartCommandPlan(immediate);

            if (!_queue.TryEnqueue(trigger, plan, out var rejection))
            {
                ShowConsoleToast(rejection, 2f);
                return true;    // handled: the player asked to queue and was told no
            }
            SyncCommandQueuePanel();
            var head = _queue.Entries[_queue.Count - 1];
            ShowConsoleToast($"대기 {_queue.Count}/{CommandQueue.MaxEntries} • {head.StatusLine}", 2.5f);
            return true;
        }

        /// <summary>
        /// One tick of the world for the queue. Called from HudView.OnEvents so
        /// the queue sees exactly the same per-tick SimEvents mask the audio and
        /// VFX directors see — no second source of truth about what happened.
        /// </summary>
        public void ObserveCommandQueueEvents(SimEvents events)
        {
            if (_queue.IsEmpty) return;
            _queue.ObserveEvents(events);
            SyncCommandQueuePanel();
        }

        /// <summary>Hands the head to the runner once its trigger has fired.
        /// Only when the runner is free: a released plan REPLACES whatever the
        /// runner holds, so releasing into a live sequence would silently
        /// discard the steps the player is watching.</summary>
        void ReleaseQueuedCommand()
        {
            if (_queue.IsEmpty) return;
            if (!_queue.TryRelease(_agent.Active, out var entry)) return;
            SyncCommandQueuePanel();
            StartCommandPlan(entry.Plan);
            // After StartCommandPlan on purpose: for a sequence it would
            // otherwise post its own "시퀀스 N단계" over this line, and naming
            // what actually fired is the more useful of the two.
            ShowConsoleToast($"발동 • {entry.PlanLabel}", 2f);
        }

        /// <summary>Queue readout: the pending condition first, then the parked
        /// orders in the order they will fire. Built lazily with the console
        /// (the only surface that can create a queue entry) and hidden whenever
        /// the queue is empty, so an idle fight is unchanged.</summary>
        void BuildCommandQueuePanel()
        {
            var root = (Transform)_safeRoot;
            _queuePanel = Panel(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0, 380), new Vector2(460, 26 + CommandQueue.MaxEntries * 20),
                new Color(0.03f, 0.04f, 0.09f, 0.82f));
            _queuePanel.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);

            _queueHeader = Label(_queuePanel.transform, 10, -4, 440, 20, "", 13, TextAnchor.MiddleLeft);
            _queueHeader.color = new Color(0.95f, 0.35f, 0.17f);

            _queueRows = new Text[CommandQueue.MaxEntries];
            for (var i = 0; i < _queueRows.Length; i++)
            {
                _queueRows[i] = Label(_queuePanel.transform, 10, -24 - i * 20, 440, 18,
                    "", 12, TextAnchor.MiddleLeft);
                _queueRows[i].color = new Color(0.62f, 0.95f, 0.88f, 0.9f);
            }
            _queuePanel.SetActive(false);
        }

        void SyncCommandQueuePanel()
        {
            if (_queuePanel == null) return;
            if (_queue.IsEmpty)
            {
                _queuePanel.SetActive(false);
                return;
            }
            _queuePanel.SetActive(true);
            _queueHeader.text = $"대기 명령 {_queue.Count}/{CommandQueue.MaxEntries} — 다음 {_queue.Head.Trigger.Describe(_queue.Head.Progress)}";
            for (var i = 0; i < _queueRows.Length; i++)
            {
                var live = i < _queue.Count;
                _queueRows[i].text = live ? $"{i + 1}. {_queue.Entries[i].StatusLine}" : "";
                // The head is the only entry whose condition is being watched;
                // dimming the rest says so without a second explanatory line.
                _queueRows[i].color = i == 0
                    ? new Color(0.62f, 0.95f, 0.88f, 1f)
                    : new Color(0.62f, 0.95f, 0.88f, 0.55f);
            }
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
