// Command agent, part 1/2 — the PLAN: what one typed order decomposes into.
//
// The console used to resolve one sentence into exactly ONE intent. A command
// agent instead turns "결계 두르고 3초 뒤에 노바" into an ORDERED sequence, and
// CommandSequenceRunner (part 2/2) runs one step per FINISHED game event.
//
// Pure C# (no UnityEngine), same contract as CommandConsoleBuffer: every rule
// here is testable without a scene. Two producers, one model:
//   ParseLocal  — offline keyword scan, POSITION-ordered (no key, no network)
//   ParseJson   — Gemini reply {"summary":…,"steps":[{"do":…,"say":…,"sec":…}]}
// Both funnel into the closed CompanionCommandIntent vocabulary, so the sim
// still only ever sees the same deterministic latches a keystroke sets. A plan
// changes WHEN a latch is set, never WHAT the simulation does with it.
using System;
using System.Collections.Generic;

namespace CinderCourt.View
{
    /// <summary>Act = one intent latch. Wait = a pure dwell between beats.</summary>
    public enum CommandStepKind { Act = 0, Wait = 1 }

    /// <summary>Where a plan came from. The HUD says it out loud because a
    /// network plan and a keyword plan fail in completely different ways.</summary>
    public enum CommandPlanSource { None = 0, Local = 1, Gemini = 2 }

    /// <summary>One beat of a sequence. Immutable value type — a plan that is
    /// mid-flight can never be edited underneath the runner.</summary>
    public readonly struct CommandStep
    {
        /// <summary>Wait bounds. 10 s is already an eternity mid-fight; a model
        /// asking for 60 s would otherwise wedge the rest of the sequence.</summary>
        public const float MinWaitSeconds = 0.1f;
        public const float MaxWaitSeconds = 10f;

        public readonly CommandStepKind Kind;
        public readonly CompanionCommandIntent Intent;
        /// <summary>Wait steps only: dwell length. Zero on Act steps — an Act's
        /// dwell is owned by CommandAgentSpec, not by the sentence.</summary>
        public readonly float Seconds;
        /// <summary>Optional one-line rationale for the HUD ("몰린 적 정리").</summary>
        public readonly string Say;

        CommandStep(CommandStepKind kind, CompanionCommandIntent intent, float seconds, string say)
        {
            Kind = kind;
            Intent = intent;
            Seconds = seconds;
            Say = say;
        }

        public static CommandStep Act(CompanionCommandIntent intent, string say = null)
            => new CommandStep(CommandStepKind.Act, intent, 0f, say);

        public static CommandStep Wait(float seconds, string say = null)
            => new CommandStep(CommandStepKind.Wait, CompanionCommandIntent.Unknown,
                ClampWait(seconds), say);

        public static float ClampWait(float seconds)
            => seconds < MinWaitSeconds ? MinWaitSeconds
                : seconds > MaxWaitSeconds ? MaxWaitSeconds : seconds;
    }

    /// <summary>An ordered, bounded action sequence.</summary>
    public sealed class CommandPlan
    {
        /// <summary>Hard cap. Longer than this stops being a command and becomes
        /// a script the player can neither follow nor cancel in time.</summary>
        public const int MaxSteps = 6;

        public static readonly CommandPlan Empty =
            new CommandPlan(null, CommandPlanSource.None, null);

        readonly CommandStep[] _steps;

        public CommandPlan(IReadOnlyList<CommandStep> steps, CommandPlanSource source, string summary)
        {
            var count = steps == null ? 0 : Math.Min(steps.Count, MaxSteps);
            _steps = count == 0 ? Array.Empty<CommandStep>() : new CommandStep[count];
            for (var i = 0; i < count; i++) _steps[i] = steps[i];
            Source = count == 0 ? CommandPlanSource.None : source;
            Summary = summary;
        }

        public int Count => _steps.Length;
        public bool IsEmpty => _steps.Length == 0;
        /// <summary>True when the plan is worth announcing as a sequence (and
        /// worth WAITING on cooldowns for — see CommandSequenceRunner).</summary>
        public bool IsSequence => _steps.Length > 1;
        public CommandPlanSource Source { get; }
        public string Summary { get; }
        public CommandStep StepAt(int index) => _steps[index];
        public IReadOnlyList<CommandStep> Steps => _steps;
    }

    /// <summary>
    /// Free text → plan. The local path deliberately does NOT split on Korean
    /// connectives ("쓰고" / "그리고" / "한 뒤"): an open-ended verb list would
    /// rot. It scans the SAME keyword table CompanionCommandParser owns and
    /// emits matches in POSITION order, so "노바 쓰고 결계 쳐" is [Nova, Aegis]
    /// even though the rule table lists Aegis first.
    /// </summary>
    public static class CommandPlanParser
    {
        /// <summary>A bare "기다려" with no number.</summary>
        public const float DefaultWaitSeconds = 1f;

        // Longest-first inside each table: MatchAny takes the first hit.
        static readonly string[] SecondUnits = { "seconds", "secs", "sec", "초", "s" };
        static readonly string[] BareWaitWords =
        {
            "기다렸다가", "기다린 다음", "기다려", "기다리", "잠깐만", "잠깐", "잠시", "wait",
        };
        /// <summary>Consumed AFTER "N초" so "3초 대기" does not also fire Defend
        /// (대기 is a Defend keyword) and "3초 뒤에" leaves no orphan fragment.</summary>
        static readonly string[] AfterMarkers =
        {
            "기다렸다가", "기다린 다음", "기다려", "기다리", "지나면", "지나고", "있다가",
            "후에", "뒤에", "대기", "쉬어", "쉬고", "후", "뒤", "쉬",
        };
        static readonly string[] WaitWords = { "wait", "delay", "pause", "sleep", "대기", "기다리기", "기다림" };

        /// <summary>Offline plan. Empty ⇒ nothing in the sentence is a command,
        /// which is exactly when the caller may escalate to Gemini.</summary>
        public static CommandPlan ParseLocal(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return CommandPlan.Empty;

            var steps = new List<CommandStep>(CommandPlan.MaxSteps);
            var i = 0;
            while (i < text.Length && steps.Count < CommandPlan.MaxSteps)
            {
                if (TryReadWait(text, i, out var seconds, out var waitLength))
                {
                    AppendWait(steps, seconds);
                    i += waitLength;
                    continue;
                }
                if (CompanionCommandParser.TryMatchAt(text, i, out var intent, out var matchLength))
                {
                    AppendAct(steps, intent, null);
                    i += matchLength;
                    continue;
                }
                i++;
            }
            TrimTrailingWait(steps);
            return steps.Count == 0
                ? CommandPlan.Empty
                : new CommandPlan(steps, CommandPlanSource.Local, null);
        }

        /// <summary>
        /// Gemini reply → plan. Tolerant by design: the payload may be fenced,
        /// wrapped in prose, a bare array, or use any of the synonym keys below.
        /// Anything it cannot map is DROPPED, never guessed — a plan with one
        /// understood step is honest, a plan with an invented step is not.
        /// </summary>
        public static CommandPlan ParseJson(string payload)
        {
            var json = Unwrap(payload);
            if (json == null) return CommandPlan.Empty;

            var root = new JsonReader(json).ReadValue();
            if (root == null) return CommandPlan.Empty;

            var array = root.Kind == JsonKind.Array
                ? root
                : root.Find("steps") ?? root.Find("plan") ?? root.Find("sequence") ?? root.Find("actions");
            if (array == null || array.Kind != JsonKind.Array) return CommandPlan.Empty;

            var summary = root.Kind == JsonKind.Object ? root.FindString("summary") : null;
            var steps = new List<CommandStep>(CommandPlan.MaxSteps);
            for (var i = 0; i < array.Items.Count && steps.Count < CommandPlan.MaxSteps; i++)
            {
                var item = array.Items[i];
                string word = null;
                string say = null;
                var seconds = 0f;
                var hasSeconds = false;

                if (item.Kind == JsonKind.String)
                {
                    word = item.Text;
                }
                else if (item.Kind == JsonKind.Object)
                {
                    word = item.FindString("do") ?? item.FindString("action")
                        ?? item.FindString("intent") ?? item.FindString("step")
                        ?? item.FindString("command");
                    say = item.FindString("say") ?? item.FindString("text")
                        ?? item.FindString("note") ?? item.FindString("reason");
                    hasSeconds = item.TryFindNumber(out seconds,
                        "sec", "seconds", "wait", "delay", "duration");
                }

                if (string.IsNullOrWhiteSpace(word))
                {
                    // {"wait": 2} — a step that is nothing but a dwell.
                    if (hasSeconds) AppendWait(steps, seconds);
                    continue;
                }
                if (IsWaitWord(word))
                {
                    AppendWait(steps, hasSeconds ? seconds : DefaultWaitSeconds);
                    continue;
                }

                var intent = IntentFromToken(word);
                if (intent == CompanionCommandIntent.Unknown) continue;
                AppendAct(steps, intent, say);
                // "cast, then hold 2 s" expressed as a field on the act step.
                if (hasSeconds && seconds > 0f) AppendWait(steps, seconds);
            }
            TrimTrailingWait(steps);
            return steps.Count == 0
                ? CommandPlan.Empty
                : new CommandPlan(steps, CommandPlanSource.Gemini, summary);
        }

        /// <summary>Model word → intent. Accepts the prompt vocabulary
        /// ("SkillNova", "focus_attack") and plain Korean ("노바") alike.</summary>
        public static CompanionCommandIntent IntentFromToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return CompanionCommandIntent.Unknown;
            var intent = CompanionCommandParser.FromIntentWord(Compact(token));
            return intent != CompanionCommandIntent.Unknown
                ? intent
                : CompanionCommandParser.Parse(token);
        }

        // ------------------------------------------------------------- steps --

        static void AppendAct(List<CommandStep> steps, CompanionCommandIntent intent, string say)
        {
            // Consecutive duplicates are a transcription artifact ("공격 공격"),
            // not a second order — the latch would be identical anyway.
            if (steps.Count > 0)
            {
                var last = steps[steps.Count - 1];
                if (last.Kind == CommandStepKind.Act && last.Intent == intent) return;
            }
            steps.Add(CommandStep.Act(intent, string.IsNullOrWhiteSpace(say) ? null : say.Trim()));
        }

        static void AppendWait(List<CommandStep> steps, float seconds)
        {
            if (steps.Count > 0 && steps[steps.Count - 1].Kind == CommandStepKind.Wait)
            {
                // Merge rather than burn a step slot on "2초 그리고 1초".
                var merged = steps[steps.Count - 1].Seconds + seconds;
                steps[steps.Count - 1] = CommandStep.Wait(merged);
                return;
            }
            steps.Add(CommandStep.Wait(seconds));
        }

        /// <summary>A sequence that ENDS on a dwell commands nothing — the
        /// player would watch a countdown to no action.</summary>
        static void TrimTrailingWait(List<CommandStep> steps)
        {
            while (steps.Count > 0 && steps[steps.Count - 1].Kind == CommandStepKind.Wait)
                steps.RemoveAt(steps.Count - 1);
        }

        // ------------------------------------------------------------- scan --

        static bool TryReadWait(string text, int start, out float seconds, out int length)
        {
            seconds = 0f;
            length = 0;
            var c = text[start];
            if (c < '0' || c > '9')
            {
                var bare = MatchAny(text, start, BareWaitWords);
                if (bare <= 0) return false;
                seconds = DefaultWaitSeconds;
                length = bare;
                return true;
            }

            var cursor = start;
            var value = 0f;
            while (cursor < text.Length && text[cursor] >= '0' && text[cursor] <= '9')
            {
                value = value * 10f + (text[cursor] - '0');
                cursor++;
            }
            if (cursor + 1 < text.Length && text[cursor] == '.' &&
                text[cursor + 1] >= '0' && text[cursor + 1] <= '9')
            {
                cursor++;
                var scale = 0.1f;
                while (cursor < text.Length && text[cursor] >= '0' && text[cursor] <= '9')
                {
                    value += (text[cursor] - '0') * scale;
                    scale *= 0.1f;
                    cursor++;
                }
            }
            cursor = SkipSpaces(text, cursor);
            var unit = MatchAny(text, cursor, SecondUnits);
            if (unit <= 0) return false;   // a bare number is not a duration
            cursor += unit;
            cursor = SkipSpaces(text, cursor);
            var marker = MatchAny(text, cursor, AfterMarkers);
            if (marker > 0) cursor += marker;

            seconds = value;
            length = cursor - start;
            return true;
        }

        static int SkipSpaces(string text, int index)
        {
            while (index < text.Length && (text[index] == ' ' || text[index] == '\t')) index++;
            return index;
        }

        /// <summary>Length of the first table entry matching at
        /// <paramref name="index"/>, 0 when none. ASCII-case-insensitive; never
        /// lowercases the source, so scan indices can never drift.</summary>
        static int MatchAny(string text, int index, string[] table)
        {
            for (var t = 0; t < table.Length; t++)
            {
                var word = table[t];
                if (index + word.Length > text.Length) continue;
                var hit = true;
                for (var k = 0; k < word.Length; k++)
                {
                    if (char.ToLowerInvariant(text[index + k]) != word[k]) { hit = false; break; }
                }
                if (hit) return word.Length;
            }
            return 0;
        }

        static bool IsWaitWord(string word)
        {
            var compact = Compact(word);
            for (var i = 0; i < WaitWords.Length; i++)
                if (compact == WaitWords[i]) return true;
            return false;
        }

        /// <summary>"focus_attack" / "Skill Nova" → "focusattack" / "skillnova".</summary>
        static string Compact(string token)
        {
            var builder = new System.Text.StringBuilder(token.Length);
            for (var i = 0; i < token.Length; i++)
            {
                var c = token[i];
                if (c == '_' || c == '-' || c == ' ' || c == '.' || c == '\t') continue;
                builder.Append(char.ToLowerInvariant(c));
            }
            return builder.ToString();
        }

        /// <summary>Peels ``` fences and any prose around the JSON body.</summary>
        static string Unwrap(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return null;
            var open = payload.IndexOfAny(new[] { '{', '[' });
            if (open < 0) return null;
            var close = payload.LastIndexOfAny(new[] { '}', ']' });
            return close <= open ? null : payload.Substring(open, close - open + 1);
        }

        // -------------------------------------------------------- tiny JSON --
        // No JSON library is referenced by this assembly and JsonUtility cannot
        // express "array of heterogeneous objects with optional keys". The
        // payload is small and its shape is fixed, so a recursive-descent
        // reader is both smaller and more honest than fighting a serializer.

        enum JsonKind { Null = 0, Bool, Number, String, Array, Object }

        sealed class JsonValue
        {
            public JsonKind Kind;
            public string Text;
            public float Number;
            public bool Bool;
            public List<JsonValue> Items;          // Array
            public List<string> Keys;              // Object
            public List<JsonValue> Values;         // Object

            public JsonValue Find(string key)
            {
                if (Kind != JsonKind.Object || Keys == null) return null;
                for (var i = 0; i < Keys.Count; i++)
                    if (string.Equals(Keys[i], key, StringComparison.OrdinalIgnoreCase))
                        return Values[i];
                return null;
            }

            public string FindString(string key)
            {
                var value = Find(key);
                if (value == null) return null;
                return value.Kind == JsonKind.String ? value.Text
                    : value.Kind == JsonKind.Number ? value.Number.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)
                    : null;
            }

            public bool TryFindNumber(out float number, params string[] keys)
            {
                for (var i = 0; i < keys.Length; i++)
                {
                    var value = Find(keys[i]);
                    if (value == null) continue;
                    if (value.Kind == JsonKind.Number) { number = value.Number; return true; }
                    if (value.Kind == JsonKind.String &&
                        float.TryParse(value.Text, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out number))
                        return true;
                }
                number = 0f;
                return false;
            }
        }

        sealed class JsonReader
        {
            const int MaxDepth = 12;

            readonly string _s;
            int _i;
            int _depth;

            public JsonReader(string s) { _s = s; }

            public JsonValue ReadValue()
            {
                if (_depth > MaxDepth) return null;
                SkipWhitespace();
                if (_i >= _s.Length) return null;
                switch (_s[_i])
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"':
                        var text = ReadString();
                        return text == null ? null : new JsonValue { Kind = JsonKind.String, Text = text };
                    case 't': return ReadLiteral("true") ? new JsonValue { Kind = JsonKind.Bool, Bool = true } : null;
                    case 'f': return ReadLiteral("false") ? new JsonValue { Kind = JsonKind.Bool, Bool = false } : null;
                    case 'n': return ReadLiteral("null") ? new JsonValue { Kind = JsonKind.Null } : null;
                    default: return ReadNumber();
                }
            }

            JsonValue ReadObject()
            {
                _i++;   // '{'
                _depth++;
                var value = new JsonValue
                {
                    Kind = JsonKind.Object,
                    Keys = new List<string>(),
                    Values = new List<JsonValue>(),
                };
                SkipWhitespace();
                if (_i < _s.Length && _s[_i] == '}') { _i++; _depth--; return value; }
                while (_i < _s.Length)
                {
                    SkipWhitespace();
                    var key = ReadString();
                    if (key == null) { _depth--; return null; }
                    SkipWhitespace();
                    if (_i >= _s.Length || _s[_i] != ':') { _depth--; return null; }
                    _i++;
                    var child = ReadValue();
                    if (child == null) { _depth--; return null; }
                    value.Keys.Add(key);
                    value.Values.Add(child);
                    SkipWhitespace();
                    if (_i < _s.Length && _s[_i] == ',') { _i++; continue; }
                    if (_i < _s.Length && _s[_i] == '}') { _i++; _depth--; return value; }
                    _depth--;
                    return null;
                }
                _depth--;
                return null;
            }

            JsonValue ReadArray()
            {
                _i++;   // '['
                _depth++;
                var value = new JsonValue { Kind = JsonKind.Array, Items = new List<JsonValue>() };
                SkipWhitespace();
                if (_i < _s.Length && _s[_i] == ']') { _i++; _depth--; return value; }
                while (_i < _s.Length)
                {
                    var child = ReadValue();
                    if (child == null) { _depth--; return null; }
                    value.Items.Add(child);
                    SkipWhitespace();
                    if (_i < _s.Length && _s[_i] == ',') { _i++; continue; }
                    if (_i < _s.Length && _s[_i] == ']') { _i++; _depth--; return value; }
                    _depth--;
                    return null;
                }
                _depth--;
                return null;
            }

            string ReadString()
            {
                SkipWhitespace();
                if (_i >= _s.Length || _s[_i] != '"') return null;
                _i++;
                var builder = new System.Text.StringBuilder(24);
                while (_i < _s.Length)
                {
                    var c = _s[_i++];
                    if (c == '"') return builder.ToString();
                    if (c != '\\') { builder.Append(c); continue; }
                    if (_i >= _s.Length) return null;
                    var escape = _s[_i++];
                    switch (escape)
                    {
                        case 'n': builder.Append('\n'); break;
                        case 't': builder.Append('\t'); break;
                        case 'r': builder.Append('\r'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) return null;
                            var code = 0;
                            for (var k = 0; k < 4; k++)
                            {
                                var digit = HexValue(_s[_i + k]);
                                if (digit < 0) return null;
                                code = code * 16 + digit;
                            }
                            _i += 4;
                            builder.Append((char)code);
                            break;
                        default: builder.Append(escape); break;   // " \ / and anything odd
                    }
                }
                return null;
            }

            JsonValue ReadNumber()
            {
                var start = _i;
                if (_i < _s.Length && (_s[_i] == '-' || _s[_i] == '+')) _i++;
                var digits = 0;
                while (_i < _s.Length && ((_s[_i] >= '0' && _s[_i] <= '9') || _s[_i] == '.' ||
                                          _s[_i] == 'e' || _s[_i] == 'E' || _s[_i] == '-' || _s[_i] == '+'))
                {
                    if (_s[_i] >= '0' && _s[_i] <= '9') digits++;
                    _i++;
                }
                if (digits == 0) return null;
                return float.TryParse(_s.Substring(start, _i - start),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var number)
                    ? new JsonValue { Kind = JsonKind.Number, Number = number }
                    : null;
            }

            bool ReadLiteral(string literal)
            {
                if (_i + literal.Length > _s.Length) return false;
                for (var k = 0; k < literal.Length; k++)
                    if (_s[_i + k] != literal[k]) return false;
                _i += literal.Length;
                return true;
            }

            void SkipWhitespace()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            static int HexValue(char c)
                => c >= '0' && c <= '9' ? c - '0'
                    : c >= 'a' && c <= 'f' ? c - 'a' + 10
                    : c >= 'A' && c <= 'F' ? c - 'A' + 10
                    : -1;
        }
    }
}
