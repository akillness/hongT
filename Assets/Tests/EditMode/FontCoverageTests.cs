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
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        public void HudKorean_ImporterPreservesShippedWebGlFontPolicy()
        {
            const string AssetPath = "Assets/Resources/Fonts/HudKorean.otf";
            var importer = AssetImporter.GetAtPath(AssetPath) as TrueTypeFontImporter;

            Assert.That(importer, Is.Not.Null, AssetPath + " must be imported as a TrueType font");
            Assert.That(importer.fontRenderingMode, Is.EqualTo(FontRenderingMode.HintedSmooth),
                "the shipped dynamic atlas must use hinted anti-aliasing");
            Assert.That(importer.includeFontData, Is.True,
                "WebGL must embed the font bytes; it has no dependable OS font");
            Assert.That(importer.characterPadding, Is.EqualTo(1),
                "the shipped atlas retains its one-pixel glyph padding");
            Assert.That(importer.fontReferences, Is.Empty,
                "the importer must not advertise an external fallback that WebGL cannot ship");
        }

        [Test]
        public void RuntimeBuiltHudAndLobbyTexts_UseSharedTypographyPolicy()
        {
            var shippedFont = Resources.Load<Font>("Fonts/HudKorean");
            Assert.That(shippedFont, Is.Not.Null, "the shipped HudKorean font must load from Resources");

            var existingEventSystem = Object.FindAnyObjectByType<EventSystem>();
            var hudObject = new GameObject("FontCoverageTests.Hud");
            var lobbyObject = new GameObject("FontCoverageTests.Lobby");
            try
            {
                var hud = hudObject.AddComponent<HudView>();
                hud.Build();

                var lobby = lobbyObject.AddComponent<LobbyView>();
                var data = new CampaignData
                {
                    PrologueDone = true,
                    ClearedMask = 1,
                    Roster = new string[0],
                    Active = string.Empty,
                    ActiveSlots = new string[0],
                };
                lobby.Build(data, default);
                lobby.Refresh(data);

                AssertSharedTypography("HUD", hudObject, shippedFont);
                AssertSharedTypography("Lobby", lobbyObject, shippedFont);
            }
            finally
            {
                Object.DestroyImmediate(hudObject);
                Object.DestroyImmediate(lobbyObject);
                if (existingEventSystem == null)
                {
                    var createdEventSystem = Object.FindAnyObjectByType<EventSystem>();
                    if (createdEventSystem != null)
                        Object.DestroyImmediate(createdEventSystem.gameObject);
                }
            }
        }

        static void AssertSharedTypography(string surface, GameObject root, Font shippedFont)
        {
            var labels = root.GetComponentsInChildren<Text>(true);
            Assert.That(labels, Is.Not.Empty, surface + " factory must build UnityEngine.UI.Text labels");

            var shippedMaterial = shippedFont.material;
            var violations = new List<string>();
            foreach (var label in labels)
            {
                var id = TextPath(label, root.transform);
                if (!object.ReferenceEquals(label.font, shippedFont))
                    violations.Add(id + " does not use the exact shipped HudKorean Font");
                if (!object.ReferenceEquals(label.material, shippedMaterial))
                    violations.Add(id + " does not use HudKorean's exact material");
                if (!Mathf.Approximately(label.lineSpacing, ViewTypography.LineSpacing))
                    violations.Add(id + $" lineSpacing={label.lineSpacing}, expected {ViewTypography.LineSpacing}");
                if (label.resizeTextForBestFit)
                    violations.Add(id + " enables resizeTextForBestFit");

                var expectedStyle = label.fontSize >= ViewTypography.HeadingMinimumSize
                    ? FontStyle.Bold
                    : FontStyle.Normal;
                if (label.fontStyle != expectedStyle)
                    violations.Add(id + $" fontStyle={label.fontStyle}, expected {expectedStyle} at size {label.fontSize}");
            }

            Assert.That(violations, Is.Empty,
                surface + " labels drifted from ViewTypography:\n" + string.Join("\n", violations));
        }

        static string TextPath(Text label, Transform root)
        {
            var parts = new List<string>();
            for (var current = label.transform; current != null; current = current.parent)
            {
                parts.Add(current.name);
                if (current == root) break;
            }
            parts.Reverse();
            return string.Join("/", parts) + $" [\"{label.text.Replace("\n", "\\n")}\"]";
        }

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
        ///
        /// Reads ShippedCmap() rather than Font.HasCharacter, and the merge
        /// that brought the two together is where that changed. This test
        /// arrived asking UnityEngine.Font, which is exactly the question main
        /// had just finished proving unreliable: the runtime can answer "yes"
        /// from an OS fallback the WebGL build does not have. Same test, same
        /// subject, a source that cannot lie about it.
        /// </summary>
        [Test]
        public void RuntimeComposedNumbers_AreCoveredByHudFont()
        {
            var cmap = ShippedCmap();

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
                if (!cmap.Contains(c) && !missing.Contains(c))
                    missing.Add(c);

            Assert.That(missing, Is.Empty,
                $"HudKorean.otf lacks {missing.Count} glyph(s) produced by ToString(): "
                + string.Join("", missing)
                + " — these never appear in source, so gen_hud_font.sh cannot harvest them.");
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
