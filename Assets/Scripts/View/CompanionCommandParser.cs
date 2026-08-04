// Text command parsing for the companion order console (guardian spec §G/§S3
// seam). Pure string -> intent classification; NO gameplay here. Every intent
// funnels into the existing deterministic SimInput latches (InputAdapter) —
// free text and network latency never touch the simulation.
using System;

namespace CinderCourt.View
{
    /// <summary>Closed intent set the console can express. Keep in sync with
    /// GeminiCommandClient's prompt vocabulary and HudView's feedback copy.</summary>
    public enum CompanionCommandIntent
    {
        Unknown = 0,
        FocusAttack = 1,   // 집중공격: hold position, keep striking nearby enemies
        Defend = 2,        // 방어태세: recall to escort/guard the player
        Recall = 3,        // 복귀: same latch as Defend, distinct feedback copy
        PickupInfo = 4,    // 아이템획득: unsupported by the sim — honest feedback
        // Skill* = PLAYER casts (the sim has no companion skills — §S3 gate).
        // Feedback copy must say "노바 시전", never "소환수가 노바를…".
        SkillBolt = 5,
        SkillPulse = 6,
        SkillNova = 7,
        SkillAegis = 8,
        SkillDash = 9,
    }

    /// <summary>
    /// Ordered keyword table — first match wins, specific before generic
    /// ("결계 쳐" must hit SkillAegis before the generic 방어 rule can claim
    /// it). Korean-first per the repo UI contract; English aliases for dev use.
    /// </summary>
    public static class CompanionCommandParser
    {
        private struct Rule
        {
            public CompanionCommandIntent Intent;
            public string[] Keywords;
        }

        private static readonly Rule[] Rules =
        {
            new Rule { Intent = CompanionCommandIntent.SkillAegis,
                Keywords = new[] { "결계", "방패", "실드", "보호막", "aegis", "shield", "ward" } },
            new Rule { Intent = CompanionCommandIntent.SkillNova,
                Keywords = new[] { "노바", "폭발", "nova" } },
            new Rule { Intent = CompanionCommandIntent.SkillPulse,
                Keywords = new[] { "파동", "펄스", "pulse" } },
            new Rule { Intent = CompanionCommandIntent.SkillBolt,
                Keywords = new[] { "화살", "볼트", "bolt" } },
            new Rule { Intent = CompanionCommandIntent.SkillDash,
                Keywords = new[] { "질주", "대시", "돌진", "dash" } },
            new Rule { Intent = CompanionCommandIntent.FocusAttack,
                Keywords = new[] { "집중공격", "집중 공격", "공격해", "잡아", "쳐라", "붙어",
                                   "focus", "attack" } },
            new Rule { Intent = CompanionCommandIntent.Recall,
                Keywords = new[] { "복귀", "돌아와", "이리와", "이리 와", "recall", "come" } },
            new Rule { Intent = CompanionCommandIntent.Defend,
                Keywords = new[] { "방어태세", "방어 태세", "방어", "지켜", "수비", "호위",
                                   "defend", "guard" } },
            new Rule { Intent = CompanionCommandIntent.PickupInfo,
                Keywords = new[] { "아이템", "획득", "주워", "줍기", "pickup", "item", "loot" } },
        };

        /// <summary>Local-first classification. Trims, lowercases (ASCII only —
        /// Korean is case-free), then scans the ordered rule table.</summary>
        public static CompanionCommandIntent Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return CompanionCommandIntent.Unknown;
            var normalized = text.Trim().ToLowerInvariant();
            for (var r = 0; r < Rules.Length; r++)
            {
                var keywords = Rules[r].Keywords;
                for (var k = 0; k < keywords.Length; k++)
                {
                    if (normalized.Contains(keywords[k])) return Rules[r].Intent;
                }
            }
            return CompanionCommandIntent.Unknown;
        }

        /// <summary>Maps a Gemini plain-text reply (one intent word) back to the
        /// enum. Tolerates case/whitespace/punctuation; anything else is Unknown.</summary>
        public static CompanionCommandIntent FromIntentWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return CompanionCommandIntent.Unknown;
            var trimmed = word.Trim();
            var end = 0;
            while (end < trimmed.Length && char.IsLetter(trimmed[end])) end++;
            if (end == 0) return CompanionCommandIntent.Unknown;
            trimmed = trimmed.Substring(0, end);
            return trimmed.ToLowerInvariant() switch
            {
                "focusattack" => CompanionCommandIntent.FocusAttack,
                "defend" => CompanionCommandIntent.Defend,
                "recall" => CompanionCommandIntent.Recall,
                "pickupinfo" => CompanionCommandIntent.PickupInfo,
                "skillbolt" => CompanionCommandIntent.SkillBolt,
                "skillpulse" => CompanionCommandIntent.SkillPulse,
                "skillnova" => CompanionCommandIntent.SkillNova,
                "skillaegis" => CompanionCommandIntent.SkillAegis,
                "skilldash" => CompanionCommandIntent.SkillDash,
                _ => CompanionCommandIntent.Unknown,
            };
        }
    }
}
