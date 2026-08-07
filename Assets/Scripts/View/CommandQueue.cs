// Command agent, part 3/3 — the QUEUE: several orders parked at once, each
// released by a game EVENT rather than by the clock.
//
// Parts 1/2 (CommandPlan) and 2/2 (CommandSequenceRunner) already turn ONE
// sentence into an ordered sequence, but a new sentence replaces whatever was
// in flight and every step advances on cooldown/ack/settle timers. That makes
// "지금 결계, 셋 잡으면 노바" impossible to express: the second half has to be
// typed at the right moment or not at all.
//
// This part adds the missing axis. A submitted order may carry a leading
// TRIGGER phrase; if it does, the plan is parked instead of started, and the
// queue head is released the moment its trigger fires. Firing means handing the
// plan to the same runner a typed order uses, which sets the same deterministic
// InputAdapter latches — so the sim's view of the world is byte-identical to a
// player who happened to type at that instant.
//
// Pure C# (no UnityEngine), same contract as CommandConsoleBuffer: SimEvents
// masks are pushed in, decisions come out, and every rule is testable without a
// scene.
//
// HONEST LIMIT — SimEvents is a per-tick FLAG MASK, not a counter: two enemies
// dying in the same fixed step raise EnemyKilled once. A "3 처치" trigger
// therefore counts KILL TICKS, not corpses, and can need more than three deaths
// in a heavy pull. Counting corpses would mean reading sim state on a schedule
// the view does not own, which is exactly the coupling this layer refuses.
using System.Collections.Generic;
using CinderCourt.Sim;

namespace CinderCourt.View
{
    /// <summary>Which game event releases a parked plan.</summary>
    public enum CommandTriggerKind
    {
        /// <summary>No wait: run as soon as the runner is free.</summary>
        Immediate = 0,
        /// <summary>N kill ticks (see the honest limit above).</summary>
        Kills = 1,
        WaveStart = 2,
        BossSpawn = 3,
        Pickup = 4,
        PlayerDamaged = 5,
        Extraction = 6,
    }

    /// <summary>A release condition. Immutable: a trigger that could be edited
    /// while parked would let the HUD promise one thing and fire on another.</summary>
    public readonly struct CommandTrigger
    {
        /// <summary>Upper bound on a counted trigger. Ten kill ticks is already
        /// most of a wave; a typo'd "300킬" would otherwise park a plan forever.</summary>
        public const int MaxCount = 10;

        public readonly CommandTriggerKind Kind;
        /// <summary>Kills only: how many kill ticks to wait for. 1 elsewhere.</summary>
        public readonly int Count;

        CommandTrigger(CommandTriggerKind kind, int count)
        {
            Kind = kind;
            Count = count < 1 ? 1 : count > MaxCount ? MaxCount : count;
        }

        public static readonly CommandTrigger Immediate =
            new CommandTrigger(CommandTriggerKind.Immediate, 1);

        public static CommandTrigger Of(CommandTriggerKind kind, int count = 1)
            => kind == CommandTriggerKind.Immediate
                ? Immediate
                : new CommandTrigger(kind, count);

        public bool IsImmediate => Kind == CommandTriggerKind.Immediate;

        /// <summary>Condition copy for the HUD. <paramref name="progress"/> is
        /// the kill ticks already seen, so a counted trigger reads as progress
        /// ("처치 2/3") instead of as a static promise.</summary>
        public string Describe(int progress)
        {
            switch (Kind)
            {
                case CommandTriggerKind.Kills: return $"처치 {progress}/{Count}";
                case CommandTriggerKind.WaveStart: return "다음 웨이브";
                case CommandTriggerKind.BossSpawn: return "보스 등장";
                case CommandTriggerKind.Pickup: return "전리품 획득";
                case CommandTriggerKind.PlayerDamaged: return "피격 시";
                case CommandTriggerKind.Extraction: return "추출 완료";
                default: return "즉시";
            }
        }

        /// <summary>Does this tick's event mask satisfy one unit of the trigger?
        /// Counted triggers call this once per tick and accumulate.</summary>
        public bool Fires(SimEvents events)
        {
            switch (Kind)
            {
                case CommandTriggerKind.Kills: return (events & SimEvents.EnemyKilled) != 0;
                case CommandTriggerKind.WaveStart: return (events & SimEvents.WaveStarted) != 0;
                case CommandTriggerKind.BossSpawn: return (events & SimEvents.BossSpawned) != 0;
                case CommandTriggerKind.Pickup: return (events & SimEvents.PickupCollected) != 0;
                case CommandTriggerKind.PlayerDamaged: return (events & SimEvents.PlayerDamaged) != 0;
                case CommandTriggerKind.Extraction: return (events & SimEvents.ExtractionComplete) != 0;
                default: return true;
            }
        }
    }

    /// <summary>One parked order.</summary>
    public sealed class CommandQueueEntry
    {
        public CommandQueueEntry(CommandTrigger trigger, CommandPlan plan)
        {
            Trigger = trigger;
            Plan = plan;
        }

        public CommandTrigger Trigger { get; }
        public CommandPlan Plan { get; }
        /// <summary>Kill ticks observed while this entry was the HEAD. Only the
        /// head accumulates — a queue is sequential, so an entry three deep must
        /// not silently bank progress toward a condition it is not yet watching.</summary>
        public int Progress { get; internal set; }

        /// <summary>"노바 → 결계" — what the parked plan will actually do, in
        /// order. Built from the same labels the dispatch toast uses.</summary>
        public string PlanLabel
        {
            get
            {
                if (!string.IsNullOrEmpty(Plan.Summary)) return Plan.Summary;
                var builder = new System.Text.StringBuilder();
                for (var i = 0; i < Plan.Count; i++)
                {
                    var step = Plan.StepAt(i);
                    if (builder.Length > 0) builder.Append(" → ");
                    builder.Append(step.Kind == CommandStepKind.Wait
                        ? $"{step.Seconds:0.#}초"
                        : CommandAgentSpec.LabelOf(step.Intent));
                }
                return builder.ToString();
            }
        }

        public string StatusLine => $"{Trigger.Describe(Progress)} • {PlanLabel}";
    }

    /// <summary>
    /// Strictly FIFO. Only the HEAD's trigger is evaluated, which is what makes
    /// the queue a work QUEUE and not a set of independent alarms: orders resolve
    /// in the order the player gave them, and the HUD only ever has to explain
    /// one pending condition.
    /// </summary>
    public sealed class CommandQueue
    {
        /// <summary>Depth cap. Four parked orders is already more than the
        /// console panel can list without covering the fight.</summary>
        public const int MaxEntries = 4;

        public const string RejectedFull = "대기열이 가득 찼다";
        public const string RejectedEmptyPlan = "실행할 명령이 없다";

        readonly List<CommandQueueEntry> _entries = new List<CommandQueueEntry>(MaxEntries);

        public int Count => _entries.Count;
        public bool IsEmpty => _entries.Count == 0;
        public CommandQueueEntry Head => _entries.Count == 0 ? null : _entries[0];
        public IReadOnlyList<CommandQueueEntry> Entries => _entries;

        public bool TryEnqueue(CommandTrigger trigger, CommandPlan plan, out string rejection)
        {
            rejection = null;
            if (plan == null || plan.IsEmpty) { rejection = RejectedEmptyPlan; return false; }
            if (_entries.Count >= MaxEntries) { rejection = RejectedFull; return false; }
            _entries.Add(new CommandQueueEntry(trigger, plan));
            return true;
        }

        public void Clear() => _entries.Clear();

        /// <summary>Drops the whole queue and reports what was lost, or null when
        /// there was nothing to drop (so the caller can say "실행 중인 시퀀스가
        /// 없습니다" instead of inventing a cancellation).</summary>
        public string CancelAll()
        {
            if (_entries.Count == 0) return null;
            var dropped = _entries.Count;
            _entries.Clear();
            return $"대기 명령 {dropped}건 취소";
        }

        /// <summary>One tick of the world. Only the head advances.</summary>
        public void ObserveEvents(SimEvents events)
        {
            var head = Head;
            if (head == null || head.Trigger.IsImmediate) return;
            if (head.Trigger.Fires(events)) head.Progress++;
        }

        /// <summary>Is the head's condition met? Immediate heads are always
        /// ready; counted heads need their full tally.</summary>
        public bool HeadReady
        {
            get
            {
                var head = Head;
                if (head == null) return false;
                return head.Trigger.IsImmediate || head.Progress >= head.Trigger.Count;
            }
        }

        /// <summary>Pops the head when its trigger has fired AND the runner is
        /// free. The runner check is the caller's, not ours: releasing into a
        /// busy runner would silently replace a sequence the player is watching.
        /// </summary>
        public bool TryRelease(bool runnerBusy, out CommandQueueEntry released)
        {
            released = null;
            if (runnerBusy || !HeadReady) return false;
            released = _entries[0];
            _entries.RemoveAt(0);
            return true;
        }
    }

    /// <summary>
    /// Splits "셋 잡으면 노바 쓰고 결계" into a trigger and the order text behind
    /// it. Keyword table only — no grammar, no model, and deliberately no
    /// overlap with the intent vocabulary: a phrase is only treated as a trigger
    /// when what FOLLOWS it still parses into a real plan, so "아이템 획득" stays
    /// the PickupInfo intent it has always been.
    /// </summary>
    public static class CommandTriggerParser
    {
        struct Rule
        {
            public CommandTriggerKind Kind;
            public string[] Phrases;
        }

        // Ordered, specific before generic — the same discipline
        // CompanionCommandParser.Rules keeps, for the same reason.
        static readonly Rule[] Rules =
        {
            new Rule { Kind = CommandTriggerKind.BossSpawn,
                Phrases = new[] { "보스 나오면", "보스나오면", "보스 등장하면", "보스 등장",
                                  "보스 뜨면", "보스뜨면", "on boss" } },
            new Rule { Kind = CommandTriggerKind.WaveStart,
                Phrases = new[] { "다음 웨이브에", "다음 웨이브", "다음웨이브", "웨이브 오면",
                                  "웨이브 시작하면", "웨이브에", "next wave", "on wave" } },
            new Rule { Kind = CommandTriggerKind.Extraction,
                Phrases = new[] { "추출하면", "탈출하면", "빠져나가면", "on extract" } },
            new Rule { Kind = CommandTriggerKind.PlayerDamaged,
                Phrases = new[] { "맞으면", "피격하면", "피격되면", "다치면", "on hit" } },
            new Rule { Kind = CommandTriggerKind.Pickup,
                Phrases = new[] { "전리품 먹으면", "주우면", "on pickup" } },
            new Rule { Kind = CommandTriggerKind.Kills,
                Phrases = new[] { "처치하면", "처치 후", "처치후", "잡으면", "죽이면", "없애면",
                                  "킬 후", "킬후", "on kill" } },
        };

        /// <summary>Korean numerals the console is likely to see spelled out.
        /// Index+1 is the value, so "셋" -> 3.</summary>
        static readonly string[] SpelledCounts = { "하나", "둘", "셋", "넷", "다섯" };

        public static bool TrySplit(string raw, out CommandTrigger trigger, out string remainder)
            => TrySplit(raw, out trigger, out remainder, out _);

        /// <summary>
        /// Finds a trigger phrase and returns the order text that follows it.
        /// Returns false (leaving <paramref name="remainder"/> = the original
        /// text) when the sentence carries no trigger, which is the common case
        /// and must cost nothing.
        /// </summary>
        /// <param name="prefix">Everything BEFORE the trigger word. Usually the
        /// trigger's own qualifier ("적 셋"), which parses to no plan at all —
        /// but in "노바 쓰고 셋 잡으면 결계" it is a real order the player wants
        /// NOW, and dropping it would silently eat half the sentence. The caller
        /// decides by trying to parse it.</param>
        public static bool TrySplit(string raw, out CommandTrigger trigger, out string remainder,
            out string prefix)
        {
            trigger = CommandTrigger.Immediate;
            remainder = raw ?? "";
            prefix = "";
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var text = raw.Trim();
            var bestIndex = -1;
            var bestEnd = -1;
            var bestKind = CommandTriggerKind.Immediate;
            for (var r = 0; r < Rules.Length; r++)
            {
                var phrases = Rules[r].Phrases;
                for (var p = 0; p < phrases.Length; p++)
                {
                    var at = IndexOfFolded(text, phrases[p]);
                    if (at < 0) continue;
                    // Earliest match wins; a later phrase cannot re-anchor the
                    // split, because everything before the trigger is its own
                    // qualifier ("적 셋 잡으면") and never an order.
                    if (bestIndex >= 0 && at >= bestIndex) continue;
                    bestIndex = at;
                    bestEnd = at + phrases[p].Length;
                    bestKind = Rules[r].Kind;
                }
            }
            if (bestIndex < 0) return false;

            var tail = text.Substring(bestEnd).Trim();
            // A trigger with nothing behind it is not a trigger — it is just a
            // sentence that happens to contain the word. Leave it alone so the
            // existing intent table gets its normal shot at it.
            if (tail.Length == 0) return false;

            prefix = text.Substring(0, bestIndex).Trim();
            var count = bestKind == CommandTriggerKind.Kills ? CountIn(prefix) : 1;
            trigger = CommandTrigger.Of(bestKind, count);
            remainder = tail;
            return true;
        }

        /// <summary>
        /// Case-folded search that folds PER CHARACTER instead of lowercasing
        /// the haystack. Deliberate: ToLowerInvariant can change a string's
        /// LENGTH for some characters, and a one-char drift would misreport the
        /// split index and slice the sentence in the wrong place — the same trap
        /// CompanionCommandParser.TryMatchAt documents. Phrases are authored
        /// lowercase, so only the haystack needs folding.
        /// </summary>
        static int IndexOfFolded(string text, string phrase)
        {
            if (phrase.Length == 0 || phrase.Length > text.Length) return -1;
            for (var start = 0; start + phrase.Length <= text.Length; start++)
            {
                var hit = true;
                for (var c = 0; c < phrase.Length; c++)
                {
                    if (char.ToLowerInvariant(text[start + c]) == phrase[c]) continue;
                    hit = false;
                    break;
                }
                if (hit) return start;
            }
            return -1;
        }

        /// <summary>Leading quantity in the trigger's qualifier: digits first,
        /// then the spelled-out numerals. Absent means one.</summary>
        internal static int CountIn(string qualifier)
        {
            if (string.IsNullOrEmpty(qualifier)) return 1;
            var value = 0;
            var seen = false;
            for (var i = 0; i < qualifier.Length; i++)
            {
                var c = qualifier[i];
                if (c >= '0' && c <= '9')
                {
                    value = value * 10 + (c - '0');
                    seen = true;
                    if (value > CommandTrigger.MaxCount) return CommandTrigger.MaxCount;
                    continue;
                }
                if (seen) break;      // digits are contiguous; stop at the first gap
            }
            if (seen && value > 0) return value;

            for (var i = 0; i < SpelledCounts.Length; i++)
                if (qualifier.Contains(SpelledCounts[i])) return i + 1;
            return 1;
        }
    }
}
