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

            var missing = new List<char>();
            foreach (var c in required)
                if (!font.HasCharacter(c))
                    missing.Add(c);

            Assert.That(missing, Is.Empty,
                $"HudKorean.otf lacks {missing.Count} glyph(s): " +
                string.Join("", missing) +
                " — run `bash tools/gen_hud_font.sh` and rebuild.");
        }
    }
}
