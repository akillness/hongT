// HudKorean.otf is a GENERATED SUBSET (tools/gen_hud_font.sh): any new
// user-visible Korean string ships missing glyphs unless the font was
// regenerated — WebGL has no OS fallback, so the character simply vanishes
// (bit us live: Lane K's "난독화" toast). This gate re-derives the required
// character set from View source the same way the generator does and asserts
// the imported font covers every one.
//
// AMENDMENT #8 widened it. The Hangul-only sweep below was NARROWER than the
// generator, which harvests every character inside a quoted literal — and the
// difference is not academic: `·` shipped in four player-visible HUD strings
// and `−` in a fifth, both absent from the font, both invisible to this test
// for the entire life of the file. A gate narrower than the thing it guards
// has a blind spot exactly the width of that difference.
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class FontCoverageTests
    {
        /// <summary>The generator's own quote rules, verbatim
        /// (tools/gen_hud_font.sh). Kept as a pair for the reason stated in the
        /// file header: the simple pattern skips any literal containing a
        /// backslash, and a run of those mis-pairs the quotes so whole strings
        /// fall into the gaps between matches.</summary>
        private static readonly string[] QuotedLiteralPatterns =
        {
            "\"([^\"\\\\]*)\"",
            "\"((?:[^\"\\\\\\n]|\\\\.)*)\"",
        };

        [Test]
        public void HudKorean_CoversEveryKoreanCharacterInViewStrings()
        {
            var font = Resources.Load<Font>("Fonts/HudKorean");
            Assert.That(font, Is.Not.Null, "Fonts/HudKorean missing");

            var viewDir = Path.Combine(Application.dataPath, "Scripts/View");
            Assert.That(Directory.Exists(viewDir), Is.True, viewDir);

            var required = new HashSet<char>();
            foreach (var path in Directory.GetFiles(viewDir, "*.cs"))
            {
                var source = File.ReadAllText(path);
                // Rule 1 — every Hangul syllable anywhere in the source
                // (comments included: a cheap superset, and the generator
                // harvests identically).
                foreach (Match match in Regex.Matches(source, "[가-힣]"))
                    required.Add(match.Value[0]);

                // Rule 2 — every non-ASCII character inside a quoted literal.
                // This is the generator's own union rule, restricted to the
                // characters that can actually be missing: ASCII is always in
                // the subset (string.punctuation + letters + digits), so only
                // the > 127 range can surprise us. Both quote patterns are used
                // because neither is a superset of the other on real C# — the
                // simple one refuses literals containing a backslash, which is
                // how a whole string once fell between two matches.
                foreach (var pattern in QuotedLiteralPatterns)
                    foreach (Match match in Regex.Matches(source, pattern))
                        foreach (var c in match.Groups[1].Value)
                            if (c > 127) required.Add(c);
            }
            Assert.That(required, Is.Not.Empty, "no Korean found — harvest rule broken?");

            var missing = new List<char>();
            foreach (var c in required)
                if (!font.HasCharacter(c))
                    missing.Add(c);

            Assert.That(missing, Is.Empty,
                $"HudKorean.otf lacks {missing.Count} glyph(s): " +
                string.Join("", missing) +
                " — run `bash tools/gen_hud_font.sh` and rebuild.");
        }

        /// <summary>
        /// AMENDMENT #9 — the hole the source sweep above cannot see.
        ///
        /// Rule 1 harvests Hangul from source and rule 2 harvests non-ASCII
        /// from quoted literals, so anything a developer TYPES is covered.
        /// Neither sees a character that only exists after `ToString()` runs.
        /// Today those are ASCII digits and separators, which are always in
        /// the subset — but "today" is the whole risk: a format string that
        /// grows a thousands separator, a percent sign from a culture that
        /// uses U+066A, or a minus sign that formats as U+2212 rather than
        /// hyphen-minus would all ship as tofu with the sweep above green.
        ///
        /// So compose the codex's strings the way the game does, and sweep
        /// the RESULT.
        /// </summary>
        [Test]
        public void RuntimeComposedNumbers_AreCoveredByHudFont()
        {
            var font = Resources.Load<Font>("Fonts/HudKorean");
            Assert.That(font, Is.Not.Null, "Fonts/HudKorean missing");

            var composed = new StringBuilder();
            // The four formats the codex uses, at values that exercise every
            // branch: a level-1 collapse, a fractional stat, a percentage and
            // a large additive term.
            float[] samples = { 0f, 0.5f, 7f, 58f, 87.3f, 100f, 154f, 218f, 235.4f, 1000f };
            foreach (var v in samples)
            {
                composed.Append(v.ToString("0.#"));
                composed.Append(v.ToString("0.0"));
                composed.Append(v.ToString("0"));
                composed.Append((v * 100f).ToString("0"));
            }
            // The seen-counter and the tier labels interpolate ints.
            for (var i = 0; i <= 23; i++) composed.Append(i.ToString());

            var missing = new List<char>();
            foreach (var c in composed.ToString())
                if (!font.HasCharacter(c) && !missing.Contains(c))
                    missing.Add(c);

            Assert.That(missing, Is.Empty,
                $"HudKorean.otf lacks {missing.Count} glyph(s) produced by ToString(): "
                + string.Join("", missing)
                + " — these never appear in source, so gen_hud_font.sh cannot harvest them.");
        }
    }
}
