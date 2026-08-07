// HudKorean.otf is a GENERATED SUBSET (tools/gen_hud_font.sh): any new
// user-visible Korean string ships missing glyphs unless the font was
// regenerated — WebGL has no OS fallback, so the character simply vanishes
// (bit us live: Lane K's "난독화" toast). This gate re-derives the required
// character set from View source the same way the generator does and asserts
// the imported font covers every one.
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class FontCoverageTests
    {
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
                // Same harvest rule as tools/gen_hud_font.sh: every Hangul
                // syllable anywhere in the source (comments included — cheap
                // superset, and the generator harvests identically).
                foreach (Match match in Regex.Matches(source, "[가-힣]"))
                    required.Add(match.Value[0]);
            }
            Assert.That(required, Is.Not.Empty, "no Korean found — harvest rule broken?");

            // Font.HasCharacter() is VACUOUS in the editor: it consults OS font
            // fallback, and HudKorean.otf.meta names its source family
            // (NanumBarunGothicOTF), which macOS resolves to the FULL locally
            // installed font. Proven on 2026-08-07 — this gate was green at
            // 582/582 while the shipped subset was actually missing 협·간·악·몽·움
            // and the live build rendered "난이도 · 동". WebGL has no fallback,
            // so the only truth is the cmap embedded in the shipped .otf.
            var cmap = ShippedCmap();
            var missing = new List<char>();
            foreach (var c in required)
                if (!cmap.Contains(c))
                    missing.Add(c);

            Assert.That(missing, Is.Empty,
                $"HudKorean.otf cmap lacks {missing.Count} glyph(s): " +
                string.Join("", missing) +
                " — run `bash tools/gen_hud_font.sh` and rebuild.");
        }

        /// <summary>
        /// Codepoints actually encoded in the shipped OTF, read from the file's
        /// own cmap table. Deliberately bypasses UnityEngine.Font so no OS
        /// fallback can mask a missing subset glyph. Handles format 4 (BMP,
        /// what the fontTools subsetter emits) and format 12 (full range).
        ///
        /// Known limit: idDelta/idRangeOffset are not walked, so a codepoint
        /// inside a segment that maps to glyph 0 (.notdef) counts as present.
        /// fontTools subset output does not produce that shape, and the error
        /// direction is a false POSITIVE (gate too lenient), never a false
        /// negative that would block a correct font.
        /// </summary>
        static HashSet<char> ShippedCmap()
        {
            var path = Path.Combine(Application.dataPath, "Resources/Fonts/HudKorean.otf");
            Assert.That(File.Exists(path), Is.True, path);
            var b = File.ReadAllBytes(path);
            var codepoints = new HashSet<char>();

            int U16(int o) => (b[o] << 8) | b[o + 1];
            long U32(int o) => ((long)b[o] << 24) | ((long)b[o + 1] << 16)
                             | ((long)b[o + 2] << 8) | b[o + 3];

            var numTables = U16(4);
            var cmapOffset = -1;
            for (var i = 0; i < numTables; i++)
            {
                var rec = 12 + i * 16;
                if (b[rec] == 'c' && b[rec + 1] == 'm' && b[rec + 2] == 'a' && b[rec + 3] == 'p')
                {
                    cmapOffset = (int)U32(rec + 8);
                    break;
                }
            }
            Assert.That(cmapOffset, Is.GreaterThan(0), "HudKorean.otf has no cmap table");

            // Prefer a Unicode subtable: (3,10) then (3,1) then (0,*).
            var numSub = U16(cmapOffset + 2);
            var best = -1; var bestScore = -1;
            for (var i = 0; i < numSub; i++)
            {
                var rec = cmapOffset + 4 + i * 8;
                int plat = U16(rec), enc = U16(rec + 2);
                var off = cmapOffset + (int)U32(rec + 4);
                var score = plat == 3 && enc == 10 ? 3 : plat == 3 && enc == 1 ? 2 : plat == 0 ? 1 : 0;
                if (score > bestScore) { bestScore = score; best = off; }
            }
            Assert.That(best, Is.GreaterThan(0), "no Unicode cmap subtable");

            var format = U16(best);
            if (format == 4)
            {
                var segX2 = U16(best + 6);
                var ends = best + 14;
                var starts = ends + segX2 + 2;
                for (var s = 0; s < segX2; s += 2)
                {
                    int end = U16(ends + s), start = U16(starts + s);
                    if (start == 0xFFFF) continue;
                    for (var cp = start; cp <= end && cp != 0xFFFF; cp++)
                        codepoints.Add((char)cp);
                }
            }
            else if (format == 12)
            {
                var nGroups = (int)U32(best + 12);
                for (var g = 0; g < nGroups; g++)
                {
                    var rec = best + 16 + g * 12;
                    var start = U32(rec); var end = U32(rec + 4);
                    for (var cp = start; cp <= end && cp <= 0xFFFF; cp++)
                        codepoints.Add((char)cp);
                }
            }
            else Assert.Fail($"unsupported cmap format {format}");

            // Sanity: a real subset has hundreds of glyphs. Catches a parse that
            // silently produced an empty set, which would make this gate vacuous
            // in exactly the way it is replacing.
            Assert.That(codepoints.Count, Is.GreaterThan(200),
                $"cmap parse yielded only {codepoints.Count} codepoints — parser broken");
            return codepoints;
        }
    }
}
