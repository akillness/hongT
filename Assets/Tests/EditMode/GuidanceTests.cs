// AMENDMENT #9 — EditMode gate for the in-game guidance system
// (design/ingame-guidance-spec.md, negotiation entries 13-16).
//
// Four things can silently break here, and each one lies to the player rather
// than crashing — which is why they need a test instead of a bug report:
//
//   1. Bit drift. GuidanceEntry.Bit is the save-file index. Reorder the
//      catalog and every existing player is told they have already seen
//      lessons they have not, forever. There is no recovery: the bit is set.
//   2. Budget drift. The pause tier is 8 cards by negotiation, deliberately
//      outside the surveyed genre band (median 0). "Eight" is only a contract
//      if something COUNTS the Pause entries; a constant that says 8 while the
//      catalog holds 11 is a comment, not a budget.
//   3. Copy drift. Every number in the copy is interpolated from a sim
//      constant so a balance change moves the lesson with it. The moment
//      someone retypes one as a literal the card starts lying, and it lies
//      quietly — the wrong number still renders.
//   4. Rect overlap. This cycle fixed four overlaps, D2 included: the death
//      panel's way out was drawn UNDERNEATH the sentence explaining the death,
//      so "I died and still cannot leave" was an accurate bug report about a
//      button that was clickable the whole time.
//
// The rect audits reuse LobbyLayoutTests' WorldRect seam: the canvas is
// switched to WorldSpace and sized explicitly, because Screen.* is degenerate
// in batchmode and world corners are then plain canvas units.
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class GuidanceTests
    {
        private const string CampaignKey = "abyssal-lantern:unity:campaign";
        // Rects may touch but not stack (<= 1 u counts as touch) — the same
        // epsilon HudLayoutTests and LobbyLayoutTests audit with.
        private const float OverlapEpsilon = 1f;
        private const float EffectiveWidth = 799f;
        private const float EffectiveHeight = 1729f;

        /// <summary>A number as it appears in rendered copy: "2.4", "90", "60".</summary>
        private static readonly Regex NumericToken = new Regex(@"[0-9]+(?:\.[0-9]+)?");

        private GameObject _hudObject;
        private HudView _hud;
        private bool _hadCampaign;
        private string _campaignPayload;

        [SetUp]
        public void SetUp()
        {
            _hadCampaign = PlayerPrefs.HasKey(CampaignKey);
            _campaignPayload = PlayerPrefs.GetString(CampaignKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hudObject != null) Object.DestroyImmediate(_hudObject);
            _hudObject = null;
            _hud = null;
            var eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem != null) Object.DestroyImmediate(eventSystem.gameObject);
            if (_hadCampaign) PlayerPrefs.SetString(CampaignKey, _campaignPayload);
            else PlayerPrefs.DeleteKey(CampaignKey);
            PlayerPrefs.Save();
        }

        // =================================================== T1 catalog =====

        /// <summary>
        /// Bit IS the index. MarkSeen/Seen shift by the bit while the catalog
        /// is walked by index, so a single mismatch marks one lesson seen and
        /// re-teaches another — permanently, in the save file.
        /// </summary>
        [Test]
        public void EveryEntry_CarriesItsOwnArrayIndexAsItsSaveBit()
        {
            var drift = new List<string>();
            for (var i = 0; i < GuidanceCatalog.Entries.Length; i++)
            {
                var entry = GuidanceCatalog.Entries[i];
                if (entry.Bit != i) drift.Add($"[{i}] {entry.Title} carries bit {entry.Bit}");
            }
            Assert.That(drift, Is.Empty,
                "GuidanceEntry.Bit must equal its array index — a mismatch marks the "
                + "wrong lesson as seen in every existing save:\n" + string.Join("\n", drift));
        }

        /// <summary>
        /// CampaignData.GuidanceSeen is one int. Entry 24 would be written and
        /// read through a shift the parser cannot round-trip (bit 31 makes the
        /// value negative and ExtractInt stops at the minus sign), so the
        /// ceiling is structural, not stylistic.
        /// </summary>
        [Test]
        public void CatalogWidth_FitsTheSingleIntSaveField()
        {
            Assert.That(GuidanceCatalog.Count, Is.EqualTo(GuidanceCatalog.Entries.Length),
                "Count must report the live array, not a cached number");
            Assert.That(GuidanceCatalog.Count, Is.GreaterThan(0), "an empty catalog teaches nothing");
            Assert.That(GuidanceCatalog.Count, Is.LessThanOrEqualTo(31),
                $"{GuidanceCatalog.Count} entries overflow the int save field — bit 31 "
                + "serialises negative and CampaignStore.ExtractInt reads it back as 0, "
                + "silently wiping every lesson the player has seen");
        }

        /// <summary>
        /// IndexOf resolves by title and BitForHazard/BitForPickup are built on
        /// it, so a duplicate title routes two different lessons to one bit —
        /// the first one wins and the second is never taught.
        /// </summary>
        [Test]
        public void EveryTitle_ResolvesToExactlyOneEntry()
        {
            var seen = new Dictionary<string, int>();
            var collisions = new List<string>();
            for (var i = 0; i < GuidanceCatalog.Entries.Length; i++)
            {
                var title = GuidanceCatalog.Entries[i].Title;
                Assert.That(string.IsNullOrWhiteSpace(title), Is.False,
                    $"entry {i} has no title — IndexOf(\"\") would resolve to it");
                if (seen.TryGetValue(title, out var first))
                    collisions.Add($"\"{title}\" at {first} and {i}");
                else
                    seen[title] = i;

                // The round trip is the actual contract: the title a caller
                // hands IndexOf must come back as THIS entry.
                Assert.That(GuidanceCatalog.IndexOf(title), Is.EqualTo(i),
                    $"IndexOf(\"{title}\") must resolve to entry {i}");
            }
            Assert.That(collisions, Is.Empty,
                "duplicate titles collapse two lessons onto one bit:\n"
                + string.Join("\n", collisions));
        }

        /// <summary>
        /// The pause budget, COUNTED. Comparing the constant to itself proves
        /// nothing; the contract from entry 13 is that the catalog actually
        /// holds eight run-freezing cards — six gimmicks and the two outcomes.
        /// The count is deliberately outside the surveyed band, which is only
        /// defensible while it stays exact.
        /// </summary>
        [Test]
        public void PauseTier_HoldsExactlyTheNegotiatedEightCards()
        {
            var pause = new List<GuidanceEntry>();
            var hazards = 0;
            var outcomes = 0;
            var offBudget = new List<string>();
            foreach (var entry in GuidanceCatalog.Entries)
            {
                if (entry.Tier != GuidanceTier.Pause) continue;
                pause.Add(entry);
                if (entry.Group == GuidanceGroup.Hazard) hazards += 1;
                else if (entry.Group == GuidanceGroup.Outcome) outcomes += 1;
                else offBudget.Add($"{entry.Title} ({entry.Group})");
            }

            var roll = new StringBuilder();
            foreach (var entry in pause) roll.AppendLine($"  {entry.Group,-8} {entry.Title}");
            TestContext.WriteLine($"[pause tier: {pause.Count} card(s), budget "
                + $"{GuidanceCatalog.PauseBudget}]\n" + roll);

            Assert.That(offBudget, Is.Empty,
                "only gimmicks and outcomes may freeze the run — a control or pickup that "
                + "stops the game is the over-explaining the survey found players punish:\n"
                + string.Join("\n", offBudget));
            Assert.That(pause.Count, Is.EqualTo(GuidanceCatalog.PauseBudget),
                $"{pause.Count} entries freeze the run but the negotiated budget is "
                + $"{GuidanceCatalog.PauseBudget} (entry 13). Adding a ninth is a "
                + "designer+pm decision, not a catalog edit.");
            Assert.That(hazards, Is.EqualTo(6), "six gimmicks, one card each (entry 13)");
            Assert.That(outcomes, Is.EqualTo(2), "win and lose, taught before they happen");
        }

        /// <summary>
        /// 33 words per card (entry 13: 8 cards at the survey's 3.3 words/s).
        /// A card that runs long is the failure mode the pause tier was sized
        /// to avoid, and it is invisible until someone reads all eight.
        /// </summary>
        [Test]
        public void EveryPauseCard_StaysUnderTheWordCap()
        {
            var over = new List<string>();
            var longest = 0;
            var longestTitle = "";
            foreach (var entry in GuidanceCatalog.Entries)
            {
                if (entry.Tier != GuidanceTier.Pause) continue;
                // Both input modes: a touch body is what a phone player reads,
                // and it is longer as often as not.
                foreach (var touch in new[] { false, true })
                {
                    var body = entry.BodyFor(touch);
                    Assert.That(string.IsNullOrWhiteSpace(body), Is.False,
                        $"{entry.Title} has no body for touch={touch}");
                    var words = body.Split((char[])null,
                        System.StringSplitOptions.RemoveEmptyEntries).Length;
                    if (words > longest) { longest = words; longestTitle = entry.Title; }
                    if (words > GuidanceCatalog.PauseWordCap)
                        over.Add($"{entry.Title} (touch={touch}): {words} words");
                }
            }
            TestContext.WriteLine($"[pause word cap {GuidanceCatalog.PauseWordCap}] "
                + $"longest card: {longestTitle} at {longest} words");

            Assert.That(longest, Is.GreaterThan(0), "no pause bodies were measured");
            Assert.That(over, Is.Empty,
                $"pause cards over the {GuidanceCatalog.PauseWordCap}-word cap:\n"
                + string.Join("\n", over));
        }

        /// <summary>
        /// BodyFor picks the input mode. Every control entry MUST carry a touch
        /// body, because its desktop copy names a key a phone has no way to
        /// press — a card that says "Space를 이어 치면" on a touchscreen teaches
        /// the player nothing they can act on.
        /// </summary>
        [Test]
        public void BodyFor_SwapsToTouchCopyOnlyWhereTouchCopyExists()
        {
            // Tokens that can only be pressed on a keyboard. Every one of these
            // appears in a desktop control body today.
            var desktopOnly = new[] { "W A S D", "방향키", "Space", "SHIFT" };
            var leaks = new List<string>();
            var controls = 0;

            foreach (var entry in GuidanceCatalog.Entries)
            {
                if (entry.TouchBody == null)
                {
                    Assert.That(entry.BodyFor(true), Is.EqualTo(entry.Body),
                        $"{entry.Title} has no touch copy and must fall back to Body");
                    Assert.That(entry.Group, Is.Not.EqualTo(GuidanceGroup.Control),
                        $"{entry.Title} is a control with no touch copy — a phone player "
                        + "is told to press a key that does not exist");
                    continue;
                }

                Assert.That(entry.BodyFor(false), Is.EqualTo(entry.Body),
                    $"{entry.Title}: BodyFor(false) must be the desktop copy");
                Assert.That(entry.BodyFor(true), Is.EqualTo(entry.TouchBody),
                    $"{entry.Title}: BodyFor(true) must be the touch copy");
                Assert.That(entry.TouchBody, Is.Not.EqualTo(entry.Body),
                    $"{entry.Title} carries an identical touch body — either the copy is "
                    + "input-agnostic (drop TouchBody) or it was never rewritten");

                if (entry.Group == GuidanceGroup.Control) controls += 1;
                foreach (var token in desktopOnly)
                    if (entry.TouchBody.Contains(token))
                        leaks.Add($"{entry.Title}: touch copy still says \"{token}\"");
            }

            Assert.That(controls, Is.GreaterThan(0), "no control entries were measured");
            Assert.That(leaks, Is.Empty,
                "touch copy naming a keyboard-only input:\n" + string.Join("\n", leaks));
        }

        // ============================================== T2 bit lifecycle =====

        /// <summary>
        /// MarkSeen returns true exactly once. GameDirector saves on true, and
        /// the queue re-checks Seen before showing — so a MarkSeen that keeps
        /// returning true writes the save every frame a lesson is re-queued,
        /// and one that never returns true shows the same card forever.
        /// </summary>
        [Test]
        public void MarkSeen_ReturnsTrueOnlyOnTheFirstSighting()
        {
            var data = default(CampaignData);
            var bit = GuidanceCatalog.VictoryBit;

            Assert.That(GuidanceCatalog.Seen(in data, bit), Is.False,
                "a fresh save has seen nothing");
            Assert.That(GuidanceCatalog.MarkSeen(ref data, bit), Is.True,
                "the first sighting must report a change so the caller saves");
            Assert.That(GuidanceCatalog.Seen(in data, bit), Is.True,
                "MarkSeen must actually record the bit");
            Assert.That(GuidanceCatalog.MarkSeen(ref data, bit), Is.False,
                "the second sighting must report no change — this is what stops the "
                + "same card opening on every hazard scan");
            Assert.That(GuidanceCatalog.SeenCount(in data), Is.EqualTo(1),
                "a repeated MarkSeen must not set a second bit");
        }

        /// <summary>
        /// Out-of-range bits. C# masks shift counts to 5 bits, so an unguarded
        /// `1 &lt;&lt; 32` is `1 &lt;&lt; 0` and `1 &lt;&lt; 999` is `1 &lt;&lt; 7`:
        /// a bad bit would not throw, it would corrupt a DIFFERENT lesson.
        /// BitForHazard returns -1 for an unmapped kind and GameDirector feeds
        /// that straight in, so -1 is a live input, not a hypothetical.
        /// </summary>
        [Test]
        public void OutOfRangeBits_AreRefusedWithoutTouchingNeighbours()
        {
            foreach (var bit in new[] { -1, -32, 32, 33, 999, int.MinValue, int.MaxValue })
            {
                var data = default(CampaignData);
                Assert.That(GuidanceCatalog.Seen(in data, bit), Is.False,
                    $"Seen({bit}) on an empty save must be false, not a wrapped shift");
                Assert.That(GuidanceCatalog.MarkSeen(ref data, bit), Is.False,
                    $"MarkSeen({bit}) must refuse an out-of-range bit");
                Assert.That(data.GuidanceSeen, Is.Zero,
                    $"MarkSeen({bit}) wrapped and set bit {bit & 31} instead — "
                    + "a refused write must leave the save untouched");
            }

            // And the guard must not have eaten the legal edges.
            //
            // The upper legal edge is 30, not 31. Bit 31 is the sign bit: the
            // field serialises through Append(int) and parses through
            // ExtractInt, which consumes digits only — a leading '-' ends its
            // loop immediately and yields 0. Setting bit 31 therefore reads the
            // WHOLE field back as zero on the next load and silently re-teaches
            // all 23 lessons. Verified by simulating the round trip:
            //   bit 30 -> "1073741824" -> 1073741824   ok
            //   bit 31 -> "-2147483648" -> 0           total loss
            // An earlier draft of this test asserted bit 31 was legal while the
            // ceiling test in the same file documented that it was not; the
            // round trip settled it.
            var edge = default(CampaignData);
            Assert.That(GuidanceCatalog.MarkSeen(ref edge, 0), Is.True, "bit 0 is legal");
            Assert.That(GuidanceCatalog.MarkSeen(ref edge, GuidanceCatalog.BitCeiling - 1), Is.True,
                "the highest bit below the ceiling is legal");
            Assert.That(GuidanceCatalog.MarkSeen(ref edge, GuidanceCatalog.BitCeiling), Is.False,
                "the sign bit is refused — it would wipe the field on the next load");
            Assert.That(GuidanceCatalog.Seen(in edge, 0), Is.True);
            Assert.That(GuidanceCatalog.Seen(in edge, GuidanceCatalog.BitCeiling - 1), Is.True);

            // The reason, asserted rather than described: a field carrying the
            // top legal bit still survives a save/load round trip.
            var roundTrip = default(CampaignData);
            roundTrip.Roster = new string[0];
            roundTrip.Active = string.Empty;
            roundTrip.GuidanceSeen = 1 << (GuidanceCatalog.BitCeiling - 1);
            CampaignStore.Save(in roundTrip);
            Assert.That(CampaignStore.Load().GuidanceSeen, Is.EqualTo(roundTrip.GuidanceSeen),
                "the top legal bit must survive serialisation — if this fails the "
                + "ceiling is still one too high");
        }

        /// <summary>
        /// SeenCount is the codex header — the number the player reads as
        /// "how much of this game have I been told about". It must track the
        /// bits actually set, walking the whole catalog.
        /// </summary>
        [Test]
        public void SeenCount_TracksEveryCatalogBitAsItIsSet()
        {
            var data = default(CampaignData);
            Assert.That(GuidanceCatalog.SeenCount(in data), Is.Zero,
                "a fresh save has met nothing");

            for (var i = 0; i < GuidanceCatalog.Count; i++)
            {
                Assert.That(GuidanceCatalog.MarkSeen(ref data, GuidanceCatalog.Entries[i].Bit),
                    Is.True, $"entry {i} must be markable exactly once");
                Assert.That(GuidanceCatalog.SeenCount(in data), Is.EqualTo(i + 1),
                    $"after marking {i + 1} entries the codex header must read {i + 1}");
            }

            Assert.That(GuidanceCatalog.SeenCount(in data), Is.EqualTo(GuidanceCatalog.Count),
                "every catalog entry must be reachable through its own bit");
            // Bits above the catalog must not inflate the header.
            data.GuidanceSeen |= 1 << 30;
            Assert.That(GuidanceCatalog.SeenCount(in data), Is.EqualTo(GuidanceCatalog.Count),
                "SeenCount walks the catalog, so a stray high bit must not count as a lesson");
        }

        // ================================================= T3 triggers =====

        /// <summary>
        /// Every gimmick resolves to its own pause card. The pause tier exists
        /// FOR the six gimmicks (the survey's G6 gap: 0 of 7 titles document
        /// hazards separately), so a kind that returns -1 is a gimmick the
        /// player is never warned about — and -1 is silently swallowed by
        /// GameDirector.QueueGuidance, which is why nothing else would notice.
        /// </summary>
        [Test]
        public void EveryHazardKind_ResolvesToItsOwnPauseCard()
        {
            var kinds = (HazardKind[])System.Enum.GetValues(typeof(HazardKind));
            var bits = new Dictionary<int, HazardKind>();
            var unmapped = new List<string>();

            foreach (var kind in kinds)
            {
                var bit = GuidanceCatalog.BitForHazard(kind);
                if (bit < 0) { unmapped.Add(kind.ToString()); continue; }
                Assert.That(bit, Is.LessThan(GuidanceCatalog.Count),
                    $"{kind} resolved to bit {bit}, past the catalog");
                var entry = GuidanceCatalog.Entries[bit];
                Assert.That(entry.Group, Is.EqualTo(GuidanceGroup.Hazard),
                    $"{kind} resolved to \"{entry.Title}\", which is not a hazard lesson");
                Assert.That(entry.Tier, Is.EqualTo(GuidanceTier.Pause),
                    $"{kind} resolved to a {entry.Tier} card — gimmicks kill players who "
                    + "have not been told, so they hold the run");
                if (bits.TryGetValue(bit, out var other))
                    Assert.Fail($"{kind} and {other} share bit {bit} (\"{entry.Title}\") — "
                        + "one of the two gimmicks is never taught");
                bits[bit] = kind;
            }

            Assert.That(unmapped, Is.Empty,
                "hazard kinds with no guidance entry — the player meets these with no "
                + "warning at all:\n" + string.Join("\n", unmapped));
            Assert.That(bits.Count, Is.EqualTo(kinds.Length),
                "every HazardKind must map to a distinct card");
            Assert.That(bits.Count, Is.EqualTo(6), "six gimmicks, matching the pause budget");
        }

        /// <summary>
        /// Every pickup resolves to its own toast. Pickups are the toast tier:
        /// confirming what the player just did, never stopping them for it.
        /// </summary>
        [Test]
        public void EveryPickupKind_ResolvesToItsOwnToast()
        {
            var kinds = (PickupKind[])System.Enum.GetValues(typeof(PickupKind));
            var bits = new Dictionary<int, PickupKind>();
            var unmapped = new List<string>();

            foreach (var kind in kinds)
            {
                var bit = GuidanceCatalog.BitForPickup(kind);
                if (bit < 0) { unmapped.Add(kind.ToString()); continue; }
                Assert.That(bit, Is.LessThan(GuidanceCatalog.Count),
                    $"{kind} resolved to bit {bit}, past the catalog");
                var entry = GuidanceCatalog.Entries[bit];
                Assert.That(entry.Group, Is.EqualTo(GuidanceGroup.Pickup),
                    $"{kind} resolved to \"{entry.Title}\", which is not a pickup lesson");
                Assert.That(entry.Tier, Is.EqualTo(GuidanceTier.Toast),
                    $"{kind} resolved to a {entry.Tier} card — freezing the run to explain "
                    + "a pickup the player already walked over is the over-explaining the "
                    + "survey found players punish");
                if (bits.TryGetValue(bit, out var other))
                    Assert.Fail($"{kind} and {other} share bit {bit} (\"{entry.Title}\")");
                bits[bit] = kind;
            }

            Assert.That(unmapped, Is.Empty,
                "pickup kinds with no guidance entry:\n" + string.Join("\n", unmapped));
            Assert.That(bits.Count, Is.EqualTo(kinds.Length),
                "every PickupKind must map to a distinct card");
        }

        /// <summary>
        /// The director triggers these five by name, not by scan. A rename in
        /// the catalog turns any of them into -1, and QueueGuidance drops -1
        /// without a word — win/lose would simply never be taught again.
        /// </summary>
        [Test]
        public void DirectorTriggeredBits_ResolveAtTheirNegotiatedTier()
        {
            var expected = new (string name, int bit, GuidanceTier tier)[]
            {
                ("VictoryBit", GuidanceCatalog.VictoryBit, GuidanceTier.Pause),
                ("DefeatBit", GuidanceCatalog.DefeatBit, GuidanceTier.Pause),
                ("PerilBit", GuidanceCatalog.PerilBit, GuidanceTier.Toast),
                ("SurgeBit", GuidanceCatalog.SurgeBit, GuidanceTier.Toast),
                ("FirstControlBit", GuidanceCatalog.FirstControlBit, GuidanceTier.Toast),
            };

            var distinct = new HashSet<int>();
            foreach (var (name, bit, tier) in expected)
            {
                Assert.That(bit, Is.InRange(0, GuidanceCatalog.Count - 1),
                    $"{name} resolved to {bit} — the title it looks up was renamed or "
                    + "removed, and GameDirector.QueueGuidance drops it silently");
                Assert.That(GuidanceCatalog.Entries[bit].Tier, Is.EqualTo(tier),
                    $"{name} (\"{GuidanceCatalog.Entries[bit].Title}\") must be a {tier} entry");
                Assert.That(distinct.Add(bit), Is.True,
                    $"{name} shares bit {bit} with another well-known trigger");
            }

            Assert.That(GuidanceCatalog.Entries[GuidanceCatalog.FirstControlBit].Group,
                Is.EqualTo(GuidanceGroup.Control),
                "FirstControlBit is what the prologue walks — it must be a control");
            Assert.That(GuidanceCatalog.IndexOf("존재하지 않는 제목"), Is.EqualTo(-1),
                "IndexOf must report -1 for an unknown title, not a wrong bit");
            Assert.That(GuidanceCatalog.IndexOf(""), Is.EqualTo(-1),
                "IndexOf(\"\") must not resolve to an entry");
        }

        // ============================================ T4 copy vs the sim =====

        /// <summary>
        /// Every number on a pause card, in order, against the constant it is
        /// interpolated from.
        ///
        /// This is the file's reason to exist. The copy is assembled from sim
        /// constants precisely so a balance change moves the lesson with it —
        /// and the moment one is retyped as a literal, the card keeps rendering
        /// and starts lying. Nothing else notices: a wrong number looks exactly
        /// like a right one.
        ///
        /// The expected sequences below are BUILT FROM THE SAME CONSTANTS, so
        /// they follow a balance change too. Retyping a literal in the copy is
        /// what breaks the pair apart — at the next balance change if not
        /// sooner, and an added unsourced number breaks it immediately.
        /// </summary>
        [Test]
        public void EveryPauseCardNumber_IsTheSimConstantAndNotALiteral()
        {
            var expected = new Dictionary<string, string[]>
            {
                ["분출구"] = new[]
                {
                    F(CampaignSpec.VentPeriod, "0.#"), F(CampaignSpec.VentTelegraph, "0.#"),
                    F(CampaignSpec.VentRadius, "F0"), F(CampaignSpec.VentDamage, "F0"),
                },
                ["흑요석 기둥"] = new[] { F(CampaignSpec.PillarRadius, "F0") },
                ["제단"] = new[]
                {
                    F(CampaignSpec.AltarHoldSeconds, "0.#"), F(CampaignSpec.AltarOilBurst, "F0"),
                    F(CampaignSpec.AltarCooldown, "F0"),
                },
                ["해류"] = new[]
                {
                    F(CampaignSpec.CurrentPush, "F0"), F(SimConfig.PlayerSpeed, "F0"),
                },
                ["방벽주"] = new[]
                {
                    F(CampaignSpec.PylonAuraRadius, "F0"),
                    F((1f - CampaignSpec.PylonAuraDamageTakenMult) * 100f, "F0"),
                    F(CampaignSpec.PylonHp, "F0"),
                },
                ["재의 장벽"] = new[]
                {
                    F(CampaignSpec.WallTickPeriod, "0.#"), F(CampaignSpec.WallTickDamage, "F0"),
                    F(CampaignSpec.WallPeriod, "F0"),
                },
                // Not tunable: "health reaches 0" is the rule, not a balance
                // value. Pinned anyway — if this card ever names a different
                // number it is describing a game that does not exist.
                ["승리 조건"] = new string[0],
                ["패배 조건"] = new[] { "0" },
                // The one pickup the assignment names. Toast tier, same rule.
                ["잿불 조각"] = new[] { F(SimConfig.EmberShardHeal, "F0") },
            };

            var report = new StringBuilder();
            foreach (var pair in expected)
            {
                var index = GuidanceCatalog.IndexOf(pair.Key);
                Assert.That(index, Is.GreaterThanOrEqualTo(0),
                    $"\"{pair.Key}\" is not in the catalog — this table is stale");
                var body = GuidanceCatalog.Entries[index].Body;
                var found = new List<string>();
                foreach (Match match in NumericToken.Matches(body)) found.Add(match.Value);

                report.AppendLine($"  {pair.Key,-10} [{string.Join(", ", found)}]");
                CollectionAssert.AreEqual(pair.Value, found,
                    $"\"{pair.Key}\" numbers drifted from the sim.\n"
                    + $"  copy:     {body}\n"
                    + $"  expected: [{string.Join(", ", pair.Value)}]\n"
                    + $"  found:    [{string.Join(", ", found)}]\n"
                    + "An EXTRA number is a literal typed into the copy — interpolate it "
                    + "from the constant. A CHANGED number means the copy was retyped and "
                    + "the balance moved out from under it.");
            }
            TestContext.WriteLine("[copy numbers vs sim constants]\n" + report);

            // Anti-vacuity: an empty regex or an empty table would pass every
            // assertion above without measuring anything.
            Assert.That(expected.Count, Is.EqualTo(9), "the table must cover 8 pause cards + 잿불 조각");
            Assert.That(NumericToken.Matches(GuidanceCatalog.Entries[
                GuidanceCatalog.IndexOf("분출구")].Body).Count, Is.EqualTo(4),
                "the numeric-token harvest itself must still find numbers");
        }

        // ============================================ T5 save round trip =====

        /// <summary>
        /// GuidanceSeen survives the store. It is the only record that a lesson
        /// was taught, so a dropped key re-teaches all 23 on the next boot.
        /// </summary>
        [Test]
        public void GuidanceSeen_RoundTripsThroughTheCampaignStore()
        {
            // Every bit the catalog can actually set, plus the edges around it.
            var everyCatalogBit = 0;
            for (var i = 0; i < GuidanceCatalog.Count; i++) everyCatalogBit |= 1 << i;

            foreach (var value in new[] { 0, 1, 0x7FFFFF, everyCatalogBit, 0x2A55AA })
            {
                var written = new CampaignData
                {
                    GuidanceSeen = value,
                    // Co-resident v5/v3 fields: a save that round-trips guidance
                    // by clobbering its neighbours is not a round trip.
                    TrialTiers = 0x155,
                    ClearedMask = 5,
                    Relics = 42,
                    Roster = new string[0],
                    Active = string.Empty,
                };
                CampaignStore.Save(in written);
                var read = CampaignStore.Load();

                Assert.That(read.GuidanceSeen, Is.EqualTo(value),
                    $"guidanceSeen {value} did not survive save/load");
                Assert.That(read.TrialTiers, Is.EqualTo(0x155), "v5 trial record was disturbed");
                Assert.That(read.ClearedMask, Is.EqualTo(5), "v3 clear mask was disturbed");
                Assert.That(read.Relics, Is.EqualTo(42), "relic balance was disturbed");
            }

            // The catalog's own worst case, stated separately: this is the
            // value a completionist's save actually holds.
            Assert.That(everyCatalogBit, Is.GreaterThan(0));
            Assert.That(GuidanceCatalog.SeenCount(new CampaignData { GuidanceSeen = everyCatalogBit }),
                Is.EqualTo(GuidanceCatalog.Count),
                "the round-tripped all-seen value must decode as a complete codex");
        }

        /// <summary>
        /// A pre-v6 blob has no guidanceSeen key at all. The additive
        /// convention v4 and v5 already hold to is that a missing key loads as
        /// 0 — "has seen nothing", which is the truth for a player who never
        /// had guidance. Break it and every existing save either fails to load
        /// or comes back with garbage in the field.
        /// </summary>
        [Test]
        public void PreV6Save_LoadsWithNoLessonsSeenAndNothingElseDisturbed()
        {
            // A real v5 blob: everything AMENDMENT #9 added, absent.
            const string preV6 =
                "{\"clearedMask\":5,\"equipment\":{\"weapon\":2,\"lantern\":1,\"cloak\":0},"
                + "\"stats\":{\"attack\":3,\"vitality\":4,\"swiftness\":1,\"points\":2},"
                + "\"relics\":42,\"roster\":[\"ember-warden\"],\"active\":\"ember-warden\","
                + "\"prologueDone\":true,\"sigilsOwned\":6,\"sigilFaces\":3,"
                + "\"sigilSlot0\":1,\"sigilSlot1\":2,\"trialTiers\":341,"
                + "\"trainingMastery\":true}";
            Assert.That(preV6.Contains("guidanceSeen"), Is.False,
                "the fixture must actually be a pre-v6 blob");

            PlayerPrefs.SetString(CampaignKey, preV6);
            PlayerPrefs.Save();
            var data = CampaignStore.Load();

            Assert.That(data.GuidanceSeen, Is.Zero,
                "a save from before AMENDMENT #9 must load as 'has seen nothing'");
            Assert.That(GuidanceCatalog.SeenCount(in data), Is.Zero,
                "and the codex must agree");
            Assert.That(GuidanceCatalog.Seen(in data, GuidanceCatalog.VictoryBit), Is.False,
                "no individual lesson may read as already taught");

            // Anti-vacuity: prove the blob PARSED. Without this the test passes
            // just as happily against a load that returned an empty struct,
            // which is the corruption it exists to rule out.
            Assert.That(data.TrialTiers, Is.EqualTo(341), "v5 trial record must still load");
            Assert.That(data.TrainingMasteryClaimed, Is.True, "v5 mastery flag must still load");
            Assert.That(data.ClearedMask, Is.EqualTo(5), "v3 clear mask must still load");
            Assert.That(data.SigilsOwned, Is.EqualTo(6), "v4 sigils must still load");
            Assert.That(data.Relics, Is.EqualTo(42), "relics must still load");
            Assert.That(data.Active, Is.EqualTo("ember-warden"), "active character must still load");
            Assert.That(data.PrologueDone, Is.True, "prologue flag must still load");
        }

        // ============================================ T6 card lifecycle =====

        /// <summary>
        /// The card holds the run and releases it exactly once. GameView pins
        /// timeScale at a hard 0 off GuidancePaused, so a card that fails to
        /// clear the flag is a soft-locked game; and OnGuidanceDismissed is
        /// what marks the bit, so a double raise double-saves and a missed one
        /// re-teaches the lesson forever.
        /// </summary>
        [Test]
        public void GuidanceCard_HoldsTheRunAndReportsItsBitExactlyOnce()
        {
            BuildHud();
            var raised = new List<int>();
            _hud.OnGuidanceDismissed = bit => raised.Add(bit);
            var bitUnderTest = GuidanceCatalog.BitForHazard(HazardKind.EmberVent);

            Assert.That(_hud.GuidancePaused, Is.False, "a fresh HUD holds nothing");

            Assert.That(_hud.ShowGuidancePause(bitUnderTest, "기믹", "분출구", "본문"), Is.True,
                "the first card must open");
            Assert.That(_hud.GuidancePaused, Is.True,
                "an open card must hold the run — GameView reads this to pin timeScale");
            Assert.That(raised, Is.Empty, "opening a card must not mark it seen");

            _hud.DismissGuidancePause();
            Assert.That(_hud.GuidancePaused, Is.False, "dismissing must release the run");
            CollectionAssert.AreEqual(new[] { bitUnderTest }, raised,
                "dismissal must report exactly the bit that was shown, exactly once");

            // A second dismiss is reachable: the poll runs every frame and the
            // player can hold a key across the release frame.
            _hud.DismissGuidancePause();
            Assert.That(raised.Count, Is.EqualTo(1),
                "a redundant dismiss must not raise again — the caller saves on every raise");
        }

        /// <summary>
        /// No stacking. Two cards on one surface share a dismiss: any key
        /// closes the card, so the second card would eat the keypress meant for
        /// the first and both would vanish on one press — the player reads one
        /// of the two lessons and the other is marked seen anyway.
        /// </summary>
        [Test]
        public void GuidanceCard_RefusesToOpenOverAnAlreadyOpenCard()
        {
            BuildHud();
            var raised = new List<int>();
            _hud.OnGuidanceDismissed = bit => raised.Add(bit);
            var first = GuidanceCatalog.BitForHazard(HazardKind.AshWall);
            var second = GuidanceCatalog.BitForHazard(HazardKind.EmberPylon);

            Assert.That(_hud.ShowGuidancePause(first, "기믹", "재의 장벽", "본문"), Is.True);
            Assert.That(_hud.ShowGuidancePause(second, "기믹", "방벽주", "본문"), Is.False,
                "a second card must be refused while one is up");
            Assert.That(_hud.GuidancePaused, Is.True, "the refusal must not release the run");

            _hud.DismissGuidancePause();
            CollectionAssert.AreEqual(new[] { first }, raised,
                "the refused card must not have replaced the pending bit — the player read "
                + "the first lesson, so the first bit is the one that was earned");

            // And the refused lesson must still be showable afterwards.
            Assert.That(_hud.ShowGuidancePause(second, "기믹", "방벽주", "본문"), Is.True,
                "a card refused for stacking must not be lost");
        }

        /// <summary>
        /// Dismissing nothing is a no-op — and "no-op" has to mean more than
        /// "did not throw". The dismiss poll runs on EVERY frame the HUD is
        /// alive, so this path is entered constantly with no card up; if it
        /// still ran the card's teardown it would be undoing another surface's
        /// work thousands of times a run.
        ///
        /// The observable half is the touch controls. A card hides them while
        /// it holds the run and restores them on dismiss, and so does the
        /// abandon modal — but only the card's dismiss is polled every frame.
        /// Without the early return, one stray poll restores the joystick and
        /// strike button UNDER an open abandon modal, and the player can drive
        /// the fight through a dialog that exists to stop it.
        /// </summary>
        [Test]
        public void DismissWithNoCard_LeavesEveryOtherSurfaceUntouched()
        {
            BuildHud();
            // Batchmode has no Touchscreen device, so the surfaces the dismiss
            // path restores have to be forced into existence to be observed.
            _hud.ForceTouchControlsForTest();
            var raised = 0;
            _hud.OnGuidanceDismissed = _ => raised += 1;
            var targets = new List<RectTransform>();
            _hud.CollectCombatTouchTargetsForTest(targets);
            Assert.That(targets, Is.Not.Empty,
                "the touch combat targets must exist for this to measure anything");

            Assert.DoesNotThrow(() => _hud.DismissGuidancePause(),
                "the every-frame poll must survive being called with no card up");
            Assert.That(raised, Is.Zero,
                "dismissing an absent card must not mark a bit — a stale pending bit "
                + "would mark a lesson the player never read");
            Assert.That(_hud.GuidancePaused, Is.False);

            // Now the case the early return actually protects: a DIFFERENT
            // surface is holding the run and has hidden the combat controls.
            _hud.SetLeftStackAvailable(true);
            FindButtonLabelled(_hudObject, "포기").onClick.Invoke();
            Assert.That(_hud.GuidancePaused, Is.True,
                "the abandon modal must be holding the run");
            foreach (var target in targets)
                Assert.That(target.gameObject.activeInHierarchy, Is.False,
                    $"precondition: the modal must have hidden {target.name}");

            _hud.DismissGuidancePause();

            Assert.That(raised, Is.Zero, "no card was up, so no bit may be marked");
            foreach (var target in targets)
                Assert.That(target.gameObject.activeInHierarchy, Is.False,
                    $"a stray dismiss restored {target.name} under an open abandon modal — "
                    + "the player can fight through the dialog that is holding the run");
            Assert.That(_hud.GuidancePaused, Is.True,
                "and it must not have released the modal's hold either");
        }

        /// <summary>
        /// The toast tier does not stop the game. Fifteen of the 23 entries are
        /// toasts; if this surface ever paused, the negotiated budget of eight
        /// would become 23 and the survey's over-explaining failure is exactly
        /// what the tier split exists to avoid.
        /// </summary>
        [Test]
        public void GuidanceToast_ShowsTheLessonWithoutHoldingTheRun()
        {
            BuildHud();
            _hud.ShowGuidanceToast("잿불 조각", "체력을 채운다.");

            Assert.That(_hud.GuidancePaused, Is.False,
                "the toast tier must never freeze the run");
            var rendered = FindTextContaining(_hudObject, "잿불 조각");
            Assert.That(rendered, Is.Not.Null, "the toast must render its title");
            Assert.That(rendered.text.Contains("체력을 채운다."), Is.True,
                $"the toast must render its body, not the prologue step it reuses: \"{rendered.text}\"");
            Assert.That(rendered.gameObject.activeInHierarchy, Is.True,
                "the toast surface must be visible");
        }

        /// <summary>
        /// The abandon button reveals a control; it does not open a modal. If
        /// merely making the way out available paused the run, every dungeon
        /// stage would start frozen.
        /// </summary>
        [Test]
        public void AbandonAvailability_RevealsTheControlWithoutHoldingTheRun()
        {
            BuildHud();
            _hud.SetLeftStackAvailable(true);

            Assert.That(_hud.GuidancePaused, Is.False,
                "offering the way out must not stop the run");
            var button = FindButtonLabelled(_hudObject, "포기");
            Assert.That(button, Is.Not.Null, "the abandon control must exist once available");
            Assert.That(button.gameObject.activeInHierarchy, Is.True,
                "the abandon control must be visible once available");

            _hud.SetLeftStackAvailable(false);
            Assert.That(button.gameObject.activeInHierarchy, Is.False,
                "the abandon control must hide again outside a dungeon run");
            Assert.That(_hud.GuidancePaused, Is.False);
        }

        /// <summary>
        /// The abandon modal holds the run, and BOTH answers release it.
        ///
        /// Measured in the browser before this landed: health kept dropping and
        /// the damage vignette kept flashing under an open modal. A
        /// confirmation dialog you can die inside punishes the caution it
        /// exists to reward — so GuidancePaused deliberately covers this modal
        /// too, even though only an explicit button closes it.
        /// </summary>
        [Test]
        public void AbandonModal_HoldsTheRunUntilEitherAnswerIsGiven()
        {
            BuildHud();
            var confirmed = 0;
            _hud.OnAbandonConfirmed = () => confirmed += 1;
            _hud.AbandonRelicsAtRisk = () => 7;
            _hud.SetLeftStackAvailable(true);

            // Cancel path.
            FindButtonLabelled(_hudObject, "포기").onClick.Invoke();
            Assert.That(_hud.GuidancePaused, Is.True,
                "an open abandon modal must hold the run — a dialog you can die inside is "
                + "worse than none");
            var body = FindTextContaining(_hudObject, "유물 7개");
            Assert.That(body, Is.Not.Null,
                "the modal must name what is at risk, not ask a neutral question");

            FindButtonLabelled(_hudObject, "계속 싸운다").onClick.Invoke();
            Assert.That(_hud.GuidancePaused, Is.False, "cancelling must release the run");
            Assert.That(confirmed, Is.Zero, "cancelling must not forfeit the run");

            // Confirm path.
            FindButtonLabelled(_hudObject, "포기").onClick.Invoke();
            Assert.That(_hud.GuidancePaused, Is.True);
            FindButtonLabelled(_hudObject, "포기하고 나간다").onClick.Invoke();
            Assert.That(confirmed, Is.EqualTo(1), "confirming must forfeit exactly once");
            Assert.That(_hud.GuidancePaused, Is.False,
                "the modal must close on confirm — the lobby must not inherit timeScale 0");
        }

        // ================================================ T7 rect audit =====

        /// <summary>
        /// D2, pinned. The death panel's way out was appended by a second
        /// function that could not see the first one's rects and landed 26 u
        /// inside the death-cause sentence: clickable the whole time, and
        /// invisible under a line of text. "I died and still cannot leave" was
        /// an accurate report.
        ///
        /// Audits every rect the panel puts on screen, not just the three named
        /// in the report — a fourth surface appended by a third function is the
        /// same defect wearing a different name.
        /// </summary>
        [Test]
        public void DeathPanel_DrawsNoSurfaceUnderAnother()
        {
            BuildHud();
            _hud.EnableCampaignUi("차가운 회랑", 3);
            _hud.OnEvents(SimEvents.GameOver, new CinderSim());
            var canvas = WorldSpaceCanvas();

            var retry = FindButtonLabelled(_hudObject, "재강하 (R)");
            var home = FindButtonLabelled(_hudObject, "캠페인으로");
            Assert.That(retry, Is.Not.Null, "the death panel must offer a retry");
            Assert.That(home, Is.Not.Null, "the death panel must offer a way back to the lobby");
            var panel = retry.transform.parent;
            Assert.That(home.transform.parent, Is.EqualTo(panel),
                "both exits must live on the death panel itself");
            Assert.That(panel.gameObject.activeInHierarchy, Is.True,
                "the death panel must be up for its geometry to mean anything");

            var rects = PanelSurfaces(panel, out var cause);
            Assert.That(cause, Is.Not.Null.And.Not.Empty,
                "the panel must state the cause of death");
            Assert.That(rects.Count, Is.GreaterThanOrEqualTo(4),
                $"expected at least title + cause + 2 buttons, measured {rects.Count}");

            AssertNoPairwiseOverlap(rects, $"death panel (cause: \"{cause}\")");
        }

        /// <summary>
        /// The pause card's four rows, measured against each other and against
        /// the card they are drawn on.
        ///
        /// Containment is not decoration: the growth plate shipped this cycle
        /// with its title 16 u OUTSIDE the panel — text floating on the combat
        /// scene with no plate behind it. The card is built in one function
        /// precisely so this is checkable, so check it.
        /// </summary>
        [Test]
        public void GuidanceCard_KeepsAllFourRowsApartAndInsideTheCard()
        {
            BuildHud();
            var opened = _hud.ShowGuidancePause(
                GuidanceCatalog.BitForHazard(HazardKind.EmberVent), "기믹",
                "분출구", GuidanceCatalog.Entries[
                    GuidanceCatalog.BitForHazard(HazardKind.EmberVent)].Body);
            Assert.That(opened, Is.True, "the card must open before it can be measured");
            var canvas = WorldSpaceCanvas();

            var card = FindActivePanelSized(canvas, 560f, 220f);
            Assert.That(card, Is.Not.Null,
                "the 560x220 guidance card body must be on screen while a card is up");

            var rows = new List<RectTransform>();
            var contents = new List<string>();
            foreach (Transform child in card)
            {
                var text = child.GetComponent<Text>();
                if (text == null || !child.gameObject.activeInHierarchy) continue;
                rows.Add((RectTransform)child);
                contents.Add(text.text);
            }

            Assert.That(rows.Count, Is.EqualTo(4),
                "the card is kicker + title + body + dismiss hint, measured "
                + $"{rows.Count}: [{string.Join(" | ", contents)}]");
            // Anti-vacuity: measuring four empty labels would prove nothing.
            Assert.That(contents.Contains("분출구"), Is.True, "the title row must carry the title");
            foreach (var content in contents)
                Assert.That(string.IsNullOrWhiteSpace(content), Is.False,
                    $"an empty row is drawn but says nothing: [{string.Join(" | ", contents)}]");

            AssertNoPairwiseOverlap(rows, "guidance card rows");
            AssertContainedBy(rows, card, "the 560x220 guidance card body");
        }

        /// <summary>
        /// The abandon modal, same audit. Its two answers are one destructive
        /// and one not, so a button drawn under the body text — or half off the
        /// modal — is a run lost to a misread tap.
        /// </summary>
        [Test]
        public void AbandonModal_KeepsEverySurfaceApartAndInsideTheModal()
        {
            BuildHud();
            _hud.AbandonRelicsAtRisk = () => 7;
            _hud.SetLeftStackAvailable(true);
            FindButtonLabelled(_hudObject, "포기").onClick.Invoke();
            var canvas = WorldSpaceCanvas();

            var modal = FindActivePanelSized(canvas, 480f, 200f);
            Assert.That(modal, Is.Not.Null,
                "the 480x200 abandon modal must be on screen once opened");

            var rects = PanelSurfaces(modal, out var body);
            Assert.That(body, Is.Not.Null.And.Not.Empty,
                "the modal must state the loss");
            Assert.That(rects.Count, Is.GreaterThanOrEqualTo(4),
                $"expected title + body + 2 answers, measured {rects.Count}");
            Assert.That(FindButtonLabelled(_hudObject, "계속 싸운다"), Is.Not.Null);
            Assert.That(FindButtonLabelled(_hudObject, "포기하고 나간다"), Is.Not.Null);

            AssertNoPairwiseOverlap(rects, "abandon modal");
            AssertContainedBy(rects, modal, "the 480x200 abandon modal");
        }

        // ============================================== T8 font coverage =====

        /// <summary>
        /// Every character the catalog can put on screen, against the subset
        /// font. WebGL has no OS fallback: a missing glyph is not a fallback
        /// shape, it is nothing — the character silently vanishes mid-sentence.
        ///
        /// FontCoverageTests already sweeps View source with the generator's
        /// own regex, and it does not cover this. The catalog's copy is
        /// ASSEMBLED AT RUNTIME from interpolated constants, so the characters
        /// a format produces — `{VentPeriod:0.#}` -> "2.4" — appear in no
        /// literal the regex can see. That difference is exactly the class of
        /// blind spot that shipped `·` and `−` unnoticed for the whole life of
        /// that file. This test measures the strings themselves instead.
        /// </summary>
        [Test]
        public void HudKorean_CoversEveryCharacterTheCatalogAssembles()
        {
            var font = Resources.Load<Font>("Fonts/HudKorean");
            Assert.That(font, Is.Not.Null, "Fonts/HudKorean missing");

            var required = new HashSet<char>();
            var measured = 0;
            foreach (var entry in GuidanceCatalog.Entries)
            {
                foreach (var text in new[] { entry.Title, entry.Body, entry.TouchBody })
                {
                    if (text == null) continue;
                    measured += 1;
                    foreach (var c in text)
                    {
                        // Whitespace carries no glyph; the generator's own
                        // coverage check skips it for the same reason.
                        if (!char.IsWhiteSpace(c)) required.Add(c);
                    }
                }
            }

            Assert.That(measured, Is.GreaterThanOrEqualTo(GuidanceCatalog.Count * 2),
                "every entry contributes at least a title and a body");
            Assert.That(required.Count, Is.GreaterThan(100),
                "the catalog's character set is implausibly small — harvest rule broken?");

            var missing = new List<char>();
            foreach (var c in required)
                if (!font.HasCharacter(c)) missing.Add(c);

            Assert.That(missing, Is.Empty,
                $"HudKorean.otf lacks {missing.Count} glyph(s) used by the guidance "
                + $"catalog: {string.Join("", missing)} — these render as NOTHING on WebGL. "
                + "Run `bash tools/gen_hud_font.sh` and rebuild. Note the generator scans "
                + "source literals, so a character produced only by an interpolated format "
                + "needs the literal added or the charset widened by hand.");
        }

        // ================================================= T8 codex =========
        //
        // AMENDMENT #9's in-run codex. HudLayoutTests already audits where the
        // panel and its button sit; everything below is about what the panel
        // SAYS, which is the half that can lie to the player without moving a
        // pixel out of place.

        /// <summary>
        /// AC-15: the codex holds the run while it is open and releases it on
        /// close, and it does that WITHOUT standing in for the guidance card's
        /// own hold.
        ///
        /// GuidancePaused is an OR of three independent sources
        /// (HudViewCodex.cs:1052) and GameView pins timeScale from it. Breaks if
        /// `|| _codexOpen` is dropped (an open codex over a live sim — the
        /// player reads their numbers while a vent kills them), if CloseCodex
        /// stops clearing _codexOpen (a permanently frozen run with no way out),
        /// or if someone "simplifies" the OR down to _codexOpen alone, which
        /// would unfreeze every guidance card in the game — hence the last two
        /// asserts, which hold with the codex shut.
        /// </summary>
        [Test]
        public void Codex_HoldsTheRunOnlyWhileItIsOpen()
        {
            BuildHud();
            Assert.That(_hud.GuidancePaused, Is.False, "a fresh HUD holds nothing");
            Assert.That(_hud.CodexOpenForTest, Is.False);

            _hud.OpenCodexForTest();
            Assert.That(_hud.CodexOpenForTest, Is.True, "the codex must report itself open");
            Assert.That(_hud.GuidancePaused, Is.True,
                "an open codex must freeze the run — its numbers describe the frame "
                + "it opened on, and a sim that keeps ticking makes them a lie the "
                + "player is reading while taking damage");

            _hud.CloseCodex();
            Assert.That(_hud.CodexOpenForTest, Is.False);
            Assert.That(_hud.GuidancePaused, Is.False, "closing must release the run");

            // The card path is a SEPARATE source of the hold. With the codex
            // shut, a card up must still report paused.
            var opened = _hud.ShowGuidancePause(
                GuidanceCatalog.VictoryBit, "GUIDANCE", "승리 조건", "보스를 쓰러뜨린다.");
            Assert.That(opened, Is.True, "the card must open on a fresh HUD");
            Assert.That(_hud.CodexOpenForTest, Is.False, "the card must not open the codex");
            Assert.That(_hud.GuidancePaused, Is.True,
                "a guidance card holds the run on its own — the codex is not its proxy");
        }

        /// <summary>
        /// AC-10: the stats tab is a FROZEN FRAME. SyncCodex latches on the
        /// first call after open and ignores every later one
        /// (HudViewCodex.cs:135).
        ///
        /// Both SyncCodex calls here are load-bearing. Without the second one
        /// this test passes against an implementation that re-reads the sim
        /// every frame — which is the exact bug it exists to exclude. And the
        /// reason a re-reading implementation would otherwise look correct is
        /// subtle: at timeScale 0 the sim cannot tick, so a per-frame read is
        /// ACCIDENTALLY right in the shipping build and only a second sim can
        /// tell the two apart.
        ///
        /// The reopen at the end is what stops this from passing on a dead
        /// view: it proves the strings CAN change, so the freeze in the middle
        /// was the latch and not a label nobody ever writes to.
        /// </summary>
        [Test]
        public void CodexStats_LatchTheOpeningFrameAndIgnoreLaterSyncs()
        {
            BuildHud();

            // Two sims whose four derived stats all differ, so any leak from
            // the second one into the panel is visible in the rendered text.
            var strong = LevelledDungeonRun();
            var weak = new CinderSim();
            AssertEveryDerivedStatDiffers(strong, weak);

            _hud.OpenCodexForTest();
            _hud.SyncCodex(strong);
            var latched = CodexRowStrings();
            foreach (var row in latched)
                Assert.That(row, Is.Not.Empty, "the first Sync after open must render");

            // Second Sync, different sim. THIS is the assertion that matters.
            _hud.SyncCodex(weak);
            Assert.That(CodexRowStrings(), Is.EqualTo(latched),
                "a second Sync rewrote the frozen frame — the stats tab must hold the "
                + "numbers from the frame it opened on, not the newest sim it is handed");

            // Advancing the ORIGINAL sim must not move the frame either: the
            // latch is a snapshot of values, not a live reference to an object.
            for (var tick = 0; tick < 120; tick++) strong.Tick(default);
            _hud.SyncCodex(strong);
            Assert.That(CodexRowStrings(), Is.EqualTo(latched),
                "the frozen frame followed the sim forward — the latch must copy values, "
                + "not hold a reference that keeps reading the live object");

            // Non-vacuity: a fresh open must re-latch, or every assert above
            // would also hold for a panel that never renders anything again.
            _hud.CloseCodex();
            _hud.OpenCodexForTest();
            _hud.SyncCodex(weak);
            Assert.That(CodexRowStrings(), Is.Not.EqualTo(latched),
                "reopening must take a NEW frame — if the strings never change again, "
                + "the freeze proved above is just a panel nobody writes to");
        }

        /// <summary>
        /// AC-9: at level 1 with no growth and no extraction every breakdown row
        /// collapses to the base term ALONE. `× 1.00` teaches nothing and costs
        /// a line of reading (HudViewCodex.cs:183).
        ///
        /// Asserted as string EQUALITY, deliberately. `Does.Not.Contain("×")`
        /// is the obvious form and it is worthless: it also passes on the empty
        /// string, so it holds against a panel that renders nothing at all.
        ///
        /// Every expectation is derived from the snapshot's own Base* value, so
        /// a balance change to SimConfig moves the expectation with it — a
        /// retyped "58" here would be a second source of truth that drifts.
        /// Breaks if the zero-amount `continue` is removed (rows grow a
        /// `× 레벨1(+0%)` tail), or if the format widens past "0.#".
        /// </summary>
        [Test]
        public void CodexStats_CollapseToTheBaseTermAloneAtLevelOne()
        {
            BuildHud();
            var sim = new CinderSim();
            var derived = (IDerivedStatSnapshot)sim;
            var growth = (IGrowthChoiceSnapshot)sim;

            // The premise of the test, asserted rather than assumed.
            Assert.That(sim.Level, Is.EqualTo(1), "a fresh arena run starts at level 1");
            Assert.That(derived.ExtractionBonus, Is.Zero, "no extraction yet");
            Assert.That(growth.GrowthAttack + growth.GrowthVitality + growth.GrowthSwiftness,
                Is.Zero, "no growth points banked yet");

            _hud.OpenCodexForTest();
            _hud.SyncCodex(sim);

            var expected = new[]
            {
                F(derived.BaseDamage, "0.#"),
                F(derived.BaseMaxHealth, "0.#"),
                F(derived.BaseSpeed, "0.#"),
                F(derived.BaseLanternRegen, "0.#"),
            };
            for (var row = 0; row < expected.Length; row++)
                Assert.That(_hud.CodexStatBreakdownForTest(row), Is.EqualTo(expected[row]),
                    $"row {row}: at level 1 with nothing banked the breakdown is the base "
                    + "term and nothing else — an identity factor is noise the player has "
                    + "to read past");
        }

        /// <summary>
        /// AC-8/AC-9 on the CAMPAIGN path, where the arena test above is
        /// vacuous.
        ///
        /// `new CinderSim()` is an arena run: no meta points, no equip ranks,
        /// so `BaseDamage == SimConfig.PlayerDamage` and every meta factor is
        /// zero. The collapse test passes whether the breakdown unfolds to the
        /// sim constant or stops at the run-start base — the two are the same
        /// number there.
        ///
        /// They are NOT the same on a dungeon run: `_baseDamage` folds meta and
        /// equipment in at CinderSim.cs:2631. A breakdown that stops at the
        /// base prints "72.8 comes from 72.8" and teaches nothing, which is
        /// exactly what shipped to the browser before this test existed.
        /// </summary>
        [Test]
        public void CodexStats_UnfoldPastTheRunStartBaseOnACampaignRun()
        {
            Assert.That(HackConfig.TryDungeon(
                    CampaignStages.CinderSpan,
                    MetaStats.Of(attack: 4, vitality: 3, swiftness: 2),
                    EquipTiers.Of(weapon: 2, lantern: 1, cloak: 1),
                    null, 0, out var config),
                Is.True, "cinder-span must resolve");

            BuildHud();
            var sim = new CinderSim(in config);
            var derived = (IDerivedStatSnapshot)sim;

            // The premise: the run-start base is NOT the sim constant here.
            Assert.That(derived.BaseDamage, Is.Not.EqualTo(SimConfig.PlayerDamage).Within(0.01f),
                "meta + weapon must have moved the base, or this test proves nothing");

            _hud.OpenCodexForTest();
            _hud.SyncCodex(sim);

            var damage = _hud.CodexStatBreakdownForTest(0);
            // Starts at the constant, not the folded base.
            Assert.That(damage, Does.StartWith(F(SimConfig.PlayerDamage, "0.#")),
                $"the damage breakdown must open on the sim constant, got '{damage}'");
            Assert.That(damage, Is.Not.EqualTo(F(derived.BaseDamage, "0.#")),
                "a breakdown equal to its own value explains nothing");
            // And it must name what the player spent.
            Assert.That(damage, Does.Contain("특성4"), "4 attack points are missing from the line");
            Assert.That(damage, Does.Contain("무기2"), "weapon rank 2 is missing from the line");

            var health = _hud.CodexStatBreakdownForTest(1);
            Assert.That(health, Does.StartWith(F(SimConfig.PlayerMaxHealth, "0.#")));
            Assert.That(health, Does.Contain("특성3").And.Contain("망토1"));
            // Health is ADDITIVE — a multiplicative render is 363x wrong here.
            Assert.That(health, Does.Contain(" + ").And.Not.Contain(" × "),
                $"max health folds by addition (CinderSim.cs:2677), got '{health}'");

            var speed = _hud.CodexStatBreakdownForTest(2);
            Assert.That(speed, Does.StartWith(F(SimConfig.PlayerSpeed, "0.#")));
            Assert.That(speed, Does.Contain("특성2"));

            // Recompose the damage row from what it PRINTED and check it lands
            // on the sim's own number. This is the invariant that fails when a
            // factor is added to ApplyLevelStats without a display row.
            var product = SimConfig.PlayerDamage
                * (1f + HackSpec.AttackPerPoint * 4)
                * (1f + CampaignSpec.WeaponDamagePerRank * 2);
            Assert.That(product, Is.EqualTo(derived.PlayerDamage).Within(0.05f),
                "the printed factor set must reproduce the sim's damage");
        }

        /// <summary>
        /// AC-11 layout: every guidance row must fit INSIDE the codex panel.
        ///
        /// The first two-column layout overflowed by 238 u and cut the last two
        /// groups off the bottom. Nine codex tests were green while it shipped,
        /// because every one of them asked what the rows SAID and none asked
        /// where they WERE. Rendering the right text outside the panel is the
        /// same defect class as the gauges that held the right fillAmount and
        /// drew a full bar.
        /// </summary>
        [Test]
        public void CodexGuidance_EveryRowFitsInsideThePanel()
        {
            BuildHud();
            _hud.CodexEntrySeen = _ => true;
            _hud.OpenCodexForTest();
            _hud.ShowCodexTabForTest(guidance: true);
            var canvas = WorldSpaceCanvas();

            var panel = _hud.CodexRectForTest;
            Assert.That(panel, Is.Not.Null, "the codex panel must exist once opened");
            var panelRect = WorldRectOf(panel);

            var bodies = _hud.CodexGuidanceBodiesForTest;
            Assert.That(bodies.Count, Is.EqualTo(GuidanceCatalog.Count),
                "every catalog entry needs a row");

            var escaped = new List<string>();
            for (var i = 0; i < bodies.Count; i++)
            {
                var row = WorldRectOf(bodies[i].rectTransform);
                // Bottom edge is the one that overflowed; check all four so a
                // future column change cannot escape sideways instead.
                if (row.yMin < panelRect.yMin - 0.5f || row.yMax > panelRect.yMax + 0.5f
                    || row.xMin < panelRect.xMin - 0.5f || row.xMax > panelRect.xMax + 0.5f)
                    escaped.Add($"bit {_hud.CodexGuidanceBitsForTest[i]} "
                        + $"({GuidanceCatalog.Entries[_hud.CodexGuidanceBitsForTest[i]].Title}) "
                        + $"at {row} outside {panelRect}");
            }
            Assert.That(escaped, Is.Empty,
                $"{escaped.Count} guidance row(s) render outside the codex panel:\n"
                + string.Join("\n", escaped));
        }

        private static Rect WorldRectOf(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        /// <summary>
        /// The left stack belongs to a LIVE run.
        ///
        /// Found in the browser, not here: the arena death panel came up with
        /// 포기 still armed, and the abandon modal opened on top of it —
        /// offering to forfeit a run that had already ended. The codex is the
        /// same shape of wrong, describing a sim that is over.
        ///
        /// Every codex test written before this one opened the panel mid-run
        /// and never asked what happens when the run stops.
        /// </summary>
        [Test]
        public void LeftStack_RetiresWhenTheRunEnds()
        {
            BuildHud();
            _hud.EnableCampaignUi("차가운 회랑", 3);
            _hud.SetLeftStackAvailable(true);
            Assert.That(_hud.CodexButtonRectForTest.gameObject.activeSelf, Is.True,
                "the stack must be up during a live run");

            // Open the codex, then end the run underneath it.
            _hud.OpenCodexForTest();
            Assert.That(_hud.CodexOpenForTest, Is.True);

            _hud.OnEvents(SimEvents.GameOver, new CinderSim());

            Assert.That(_hud.CodexButtonRectForTest.gameObject.activeSelf, Is.False,
                "포기 and the codex must retire when the death panel appears");
            Assert.That(_hud.AbandonRectForTest.gameObject.activeSelf, Is.False,
                "an abandon button over a death panel forfeits a run that already ended");
            Assert.That(_hud.CodexOpenForTest, Is.False,
                "an open codex must close with the run — its numbers are a past tense");
            Assert.That(_hud.GuidancePaused, Is.False,
                "and the freeze must lift, or the death panel is unreachable");
        }

        /// <summary>
        /// AC-8: the printed factors are COMPLETE — recomposing a row from the
        /// factors it shows reproduces the sim's own number. A factor the sim
        /// applied but the row omits is a breakdown that does not add up, and
        /// the player's arithmetic is the thing this tab exists to serve.
        ///
        /// Two of the four rows are ADDITIVE (max health `base + 6·L + 6·GV`,
        /// lantern regen `base + 0.3·L`) while damage and speed are
        /// multiplicative — CinderSim.cs:2688-2696. Applying the multiplicative
        /// form to max health is 363x wrong, so the shape is read PER ROW out of
        /// the rendered string (`×` vs `+`) instead of being assumed.
        ///
        /// The recomposition parses the RENDERED TEXT, never the constants. A
        /// version that recomputed from HackSpec would agree with the sim while
        /// the panel on screen said something else entirely — it would prove
        /// nothing about what is displayed.
        ///
        /// Breaks if a row drops a factor, prints the wrong operator, prints a
        /// coefficient the sim does not apply, or hands a row the wrong
        /// `multiplicative` flag.
        /// </summary>
        [Test]
        public void CodexStats_PrintedFactorsRecomposeToTheSimsOwnNumber()
        {
            BuildHud();
            var sim = LevelledDungeonRun();
            var derived = (IDerivedStatSnapshot)sim;

            _hud.OpenCodexForTest();
            _hud.SyncCodex(sim);

            var rows = new (string name, float value)[]
            {
                ("공격력", derived.PlayerDamage),
                ("최대 체력", derived.PlayerMaxHealth),
                ("이동", derived.PlayerSpeed),
                ("기름 재생", derived.LanternRegenPerSecond),
            };

            for (var row = 0; row < rows.Length; row++)
            {
                var printed = _hud.CodexStatBreakdownForTest(row);
                var recomposed = RecomposeBreakdown(printed, out var factorCount, out var shape);
                Assert.That(recomposed, Is.EqualTo(rows[row].value).Within(1e-3f),
                    $"row {row} ({rows[row].name}): \"{printed}\" recomposes to "
                    + $"{recomposed:F4} but the sim holds {rows[row].value:F4} — the row is "
                    + "missing a factor the sim applied, or prints one it does not");

                // A row with no factors recomposes to its base trivially and
                // proves nothing about completeness. The fixture is built so
                // every row carries at least one.
                Assert.That(factorCount, Is.GreaterThan(0),
                    $"row {row} ({rows[row].name}): the fixture must give this row a live "
                    + "factor, or the recomposition above is just base == base");

                // The shape must be the one the sim uses. Read off the rendered
                // operator, so a row printed with the wrong one fails here.
                var expectMultiplicative = row == 0 || row == 2;
                Assert.That(shape == BreakdownShape.Multiplicative, Is.EqualTo(expectMultiplicative),
                    $"row {row} ({rows[row].name}) printed the wrong operator: max health and "
                    + "lantern regen are SUMS in ApplyLevelStats, damage and speed are "
                    + "PRODUCTS. Printing `×` on an additive row tells the player to "
                    + "multiply, which is 363x wrong on max health");
            }

            // The two shapes must not be interchangeable on this fixture, or
            // the operator assertions above would be untestable decoration.
            var healthPrinted = _hud.CodexStatBreakdownForTest(1);
            Assert.That(RecomposeAsProduct(healthPrinted),
                Is.Not.EqualTo(derived.PlayerMaxHealth).Within(1e-3f),
                "the max-health fixture must have a factor big enough that the WRONG shape "
                + "gives a visibly wrong answer — otherwise the additive/multiplicative "
                + "distinction is untested here");
        }

        /// <summary>
        /// AC-11: the five groups PARTITION the catalog. Every entry appears
        /// under exactly one heading, and the panel builds a row for every one.
        ///
        /// A partition, not just a cover: ByGroup filters by equality
        /// (GuidanceCatalog.cs:224) so adding a sixth GuidanceGroup value
        /// without adding it to GroupOrder silently drops those entries from the
        /// codex — the lesson exists, is markable as seen, and is unreadable
        /// forever. Duplicating a group in GroupOrder is the mirror failure: the
        /// same entry rendered twice, its lock state maintained in two places.
        /// Breaks on either, and on a build loop that skips a group.
        /// </summary>
        [Test]
        public void CodexGuidance_GroupsPartitionTheWholeCatalog()
        {
            var union = new List<int>();
            foreach (var group in GuidanceCatalog.GroupOrder)
                foreach (var entry in GuidanceCatalog.ByGroup(group))
                    union.Add(entry.Bit);

            Assert.That(union.Count, Is.EqualTo(GuidanceCatalog.Count),
                "the groups must cover the catalog exactly once — "
                + $"{union.Count} grouped rows against {GuidanceCatalog.Count} entries means "
                + "an entry is either unreachable in the codex or rendered twice");
            Assert.That(union, Is.Unique, "an entry landed in two groups");
            foreach (var entry in GuidanceCatalog.Entries)
                Assert.That(union, Contains.Item(entry.Bit),
                    $"\"{entry.Title}\" (bit {entry.Bit}, group {entry.Group}) is in no group "
                    + "GroupOrder walks — it can be marked seen and never read");
            Assert.That(GuidanceCatalog.GroupOrder, Is.Unique,
                "a repeated group in GroupOrder renders its entries twice");

            // And the panel must actually build all of them.
            BuildHud();
            _hud.OpenCodexForTest();
            var built = _hud.CodexGuidanceBitsForTest;
            Assert.That(built.Count, Is.EqualTo(GuidanceCatalog.Count),
                $"the panel built {built.Count} rows for {GuidanceCatalog.Count} entries");
            Assert.That(built, Is.Unique, "the panel built one entry twice");
            foreach (var entry in GuidanceCatalog.Entries)
                Assert.That(built, Contains.Item(entry.Bit),
                    $"the codex built no row for \"{entry.Title}\" (bit {entry.Bit})");
            Assert.That(_hud.CodexGuidanceBodiesForTest.Count, Is.EqualTo(built.Count),
                "every built bit must own exactly one body label");
        }

        /// <summary>
        /// AC-12: an unseen entry keeps its title and LOSES ITS BODY. A hazard
        /// description for a stage the player has not reached is a spoiler; its
        /// title alone is not (HudViewCodex.cs:252).
        ///
        /// Asserted as "the real body text is absent", not "the text equals
        /// 잠김" — the second would pass while the body leaked into some other
        /// label on the row. Breaks if the seen branch is inverted, if the
        /// predicate's false answer starts rendering the body anyway, or if a
        /// null predicate is treated as "seen" (it must fail closed: a HUD whose
        /// save has not been wired yet must not dump all 23 bodies on screen).
        /// </summary>
        [Test]
        public void CodexGuidance_UnseenEntriesLeakNoBodyText()
        {
            BuildHud();
            _hud.CodexEntrySeen = _ => false;
            _hud.OpenCodexForTest();
            _hud.ShowCodexTabForTest(guidance: true);

            var bits = _hud.CodexGuidanceBitsForTest;
            var bodies = _hud.CodexGuidanceBodiesForTest;
            Assert.That(bits.Count, Is.EqualTo(GuidanceCatalog.Count), "all rows must build");

            for (var row = 0; row < bits.Count; row++)
            {
                var entry = GuidanceCatalog.Entries[bits[row]];
                var rendered = bodies[row].text;
                Assert.That(entry.Body, Is.Not.Empty,
                    $"\"{entry.Title}\" has no body, so this row cannot prove a leak");
                Assert.That(rendered, Does.Not.Contain(entry.Body),
                    $"locked \"{entry.Title}\" leaked its desktop body: \"{rendered}\"");
                if (entry.TouchBody != null)
                    Assert.That(rendered, Does.Not.Contain(entry.TouchBody),
                        $"locked \"{entry.Title}\" leaked its touch body: \"{rendered}\"");
                Assert.That(rendered, Is.Not.Empty,
                    $"locked \"{entry.Title}\" rendered nothing — the player must learn that "
                    + "something remains without learning what it is");
            }

            // Fail closed with no predicate at all: an unwired HUD locks
            // everything rather than opening everything.
            _hud.CloseCodex();
            _hud.CodexEntrySeen = null;
            _hud.OpenCodexForTest();
            for (var row = 0; row < bits.Count; row++)
            {
                var entry = GuidanceCatalog.Entries[bits[row]];
                Assert.That(bodies[row].text, Does.Not.Contain(entry.Body),
                    $"with no seen-predicate wired, \"{entry.Title}\" still printed its body — "
                    + "an unwired save must lock, not unlock");
            }
        }

        /// <summary>
        /// AC-13: a seen entry shows its full body for the CURRENT input mode.
        /// A control lesson that names WASD on a phone is worse than no lesson,
        /// so this runs in both modes.
        ///
        /// Breaks if the row renders `Body` unconditionally instead of
        /// `BodyFor(_touchActive)` — which is the natural way to write it and is
        /// invisible on a desktop test run, because the two strings are equal
        /// wherever TouchBody is null. Hence the non-vacuity assert: at least
        /// one row must actually differ between the modes, or "touch copy
        /// reached the panel" is unproven.
        /// </summary>
        [Test]
        public void CodexGuidance_SeenEntriesShowTheBodyForTheActiveInputMode()
        {
            AssertSeenBodiesMatchInputMode(touch: false, out var desktop);
            Object.DestroyImmediate(_hudObject);
            _hudObject = null;
            _hud = null;
            AssertSeenBodiesMatchInputMode(touch: true, out var touch);

            var swapped = 0;
            for (var row = 0; row < desktop.Count && row < touch.Count; row++)
                if (!string.Equals(desktop[row], touch[row], System.StringComparison.Ordinal))
                    swapped++;
            Assert.That(swapped, Is.GreaterThan(0),
                "not one row changed between desktop and touch. Either the catalog lost "
                + "every TouchBody, or the row renders Body directly and the input mode "
                + "never reaches the panel — a phone player reading \"Space를 이어 치면\" "
                + "has no Space key");
        }

        /// <summary>
        /// The negative requirement: BROWSING THE CODEX MARKS NOTHING.
        ///
        /// The seam is a `Func&lt;int,bool&gt;` predicate precisely so this is
        /// unwritable (HudViewCodex.cs:45), and this test pins that it stays
        /// unwritable. Marking on browse would silently suppress a pause card
        /// the player never actually received — and there is no recovery, the
        /// bit is set in the save file forever.
        ///
        /// The call counter is load-bearing: without it this passes against a
        /// codex that never consults the record at all, which is a different
        /// bug with the same green check. Breaks the moment the seam becomes an
        /// `Action`, a `ref CampaignData`, or anything else that can write —
        /// this test will not compile against a signature that marks, which is
        /// the point.
        /// </summary>
        [Test]
        public void CodexGuidance_BrowsingMarksNothingSeen()
        {
            BuildHud();

            var data = default(CampaignData);
            GuidanceCatalog.MarkSeen(ref data, GuidanceCatalog.VictoryBit);
            GuidanceCatalog.MarkSeen(ref data, GuidanceCatalog.FirstControlBit);
            var before = data.GuidanceSeen;
            Assert.That(before, Is.Not.Zero, "the fixture must start with something seen");

            // The real record, read through the real accessor. Anything the
            // codex could do to mark a bit would land in `data`.
            var asked = 0;
            _hud.CodexEntrySeen = bit => { asked++; return GuidanceCatalog.Seen(in data, bit); };

            _hud.OpenCodexForTest();
            foreach (var _ in GuidanceCatalog.GroupOrder)
            {
                _hud.ShowCodexTabForTest(guidance: true);
                _hud.ShowCodexTabForTest(guidance: false);
            }
            _hud.CloseCodex();
            _hud.OpenCodexForTest();
            _hud.ShowCodexTabForTest(guidance: true);

            Assert.That(asked, Is.GreaterThanOrEqualTo(GuidanceCatalog.Count),
                $"the codex consulted the record {asked} times for {GuidanceCatalog.Count} "
                + "entries — it must actually ask, or the bit-identity below is vacuous");
            Assert.That(data.GuidanceSeen, Is.EqualTo(before),
                "browsing the codex changed GuidanceSeen from "
                + $"0x{before:X} to 0x{data.GuidanceSeen:X}. Reading a lesson in the codex "
                + "must never mark it received — that suppresses the pause card the player "
                + "was owed, permanently");
        }

        /// <summary>
        /// AC-14: every stat row resolves a sprite or DISABLES its Image. Never
        /// a white quad (HudViewCodex.cs:344).
        ///
        /// An enabled Image with no sprite is Unity's default white 1x1 stretched
        /// to 44x44 — a bright block beside a number, on a panel whose whole job
        /// is legibility. Breaks if the `else image.enabled = false` fallback is
        /// dropped, and the second assert breaks if an icon is renamed or moved
        /// out of Resources/Icons, which is how the fallback would start firing
        /// in the first place.
        /// </summary>
        [Test]
        public void CodexStatIcons_ResolveASpriteOrDisableTheImage()
        {
            BuildHud();
            _hud.OpenCodexForTest();

            var panel = _hud.CodexRectForTest;
            Assert.That(panel, Is.Not.Null, "opening must build the panel");

            var icons = new List<Image>();
            foreach (var image in panel.GetComponentsInChildren<Image>(true))
                if (image.name.StartsWith("CodexIcon", System.StringComparison.Ordinal))
                    icons.Add(image);

            Assert.That(icons.Count, Is.EqualTo(4),
                $"the stats tab must build one icon per row, found {icons.Count}");
            foreach (var image in icons)
            {
                Assert.That(image.enabled, Is.EqualTo(image.sprite != null),
                    $"{image.name}: an enabled Image with no sprite renders as a white quad "
                    + "beside the number — a missing icon must disable the Image instead");
                Assert.That(image.raycastTarget, Is.False,
                    $"{image.name}: a decorative icon must not eat the tap that closes the panel");
                Assert.That(image.sprite, Is.Not.Null,
                    $"{image.name} fell back to the disabled branch — its sprite is missing "
                    + "from Resources/Icons. The fallback is correct; the missing asset is not");
            }
        }

        // ========================================== T9 codex glyph layout =====
        //
        // AMENDMENT #10. The user reported "분출구 설명과 이동 설명이 겹쳐 있다"
        // while all nine codex tests above were green — they asked what the rows
        // SAID, and the containment test at :1281 asked where their RECTS were.
        // Neither asks where the GLYPHS are, and that is where the defect lived:
        // Label() builds every label as HorizontalWrapMode.Overflow
        // (HudView.cs:2409), the codex bodies were the only multi-line labels in
        // the game that never overrode it, and text rendered 128 u past a rect
        // that stayed obediently inside the panel the whole time (§4n: 무엇이라고
        // 하는지와 어디 있는지는 다른 검사다 — and "where the glyphs are" is a
        // third question again).
        //
        // Measured, not assumed (probe run, 2-column body = 266 u):
        //   preferredWidth is wrap-INSENSITIVE — 흑요석 기둥 reads 282.0 whether
        //   the mode is Wrap or Overflow. preferredHeight is wrap-SENSITIVE —
        //   the same row reads 21.0 wrapped at 266 and 11.0 unwrapped. So
        //   preferredWidth alone can never prove containment, and the pair
        //   (mode == Wrap) + (wrapped height fits the rect) is what "the glyphs
        //   stay inside the rect" actually decomposes into.

        /// <summary>
        /// AC-16: every guidance body's GLYPHS stay inside its own rect.
        ///
        /// Two measurements, because neither is sufficient alone:
        ///   (a) horizontalOverflow == Wrap. Without it uGUI is free to paint
        ///       past the rect horizontally and the rect audit cannot see it —
        ///       the reported defect exactly.
        ///   (b) preferredHeight (the WRAPPED height, measured at the width the
        ///       row actually has) fits inside rect.height. With wrap on, the
        ///       cost of horizontal containment is vertical growth, so this is
        ///       the half that catches a row whose rect was sized for fewer
        ///       lines than its copy needs.
        ///
        /// Every group is opened in turn: preferredHeight is read through
        /// Graphic.pixelsPerUnit, so measuring a row while its page is folded
        /// away would be measuring in a different coordinate system than the one
        /// LayoutCodexGuidance used (§4m).
        ///
        /// Mutations that turn this RED:
        ///   - Delete `body.horizontalOverflow = HorizontalWrapMode.Wrap`
        ///     (HudViewCodex.cs, BuildCodexGuidanceTab) → (a) fails for all 23
        ///     rows. This is the shipped defect, restored.
        ///   - `CeilToInt` → `FloorToInt` in LayoutCodexGuidance → 흑요석 기둥
        ///     wraps to 21.0 u and is given an 11 u rect → (b) fails.
        ///   - Restore the fixed 26 u pitch / drop the measured `h` and hand the
        ///     body a constant CodexLineH rect → (b) fails on every wrapped row.
        ///   - Read preferredHeight BEFORE setting the rect width (the comment in
        ///     LayoutCodexGuidance warns about this): the 100 u build width
        ///     measures 3-4 lines, and the row is sized for a wrap it does not
        ///     have → the premise assert below still holds, (b) still holds, but
        ///     T4 goes RED on the overshoot. Recorded here because the three
        ///     tests split that mutation between them.
        /// </summary>
        [Test]
        public void CodexGuidance_EveryBodysGlyphsStayInsideItsOwnRect()
        {
            BuildHud();
            _hud.CodexEntrySeen = _ => true;   // every body at full length
            // Pin the canvas BEFORE the codex opens, because opening it is what
            // runs LayoutCodexGuidance and the layout measures glyphs against
            // whatever scale is live at that moment.
            //
            // Measured, not assumed: at the default ScreenSpaceOverlay scale in
            // batchmode, pixelsPerUnit is 0.577 and preferredHeight returns 8.66
            // for EVERY row — including a two-line body whose rect is 21 u.
            // Every string fits one line at that scale, so the metric stops
            // depending on the text at all. That is not a dead metric in the
            // `<= 0` sense the premise check below screens for; it is a live
            // metric answering a different question, and it silently reduced the
            // height assertions here to `8.66 > rectH`, which nothing can fail.
            //
            // Pinning first puts the layout and this test in one coordinate
            // system (ppu 1.0), where rect height and measured height agree
            // exactly and wrap is visible again.
            WorldSpaceCanvas();
            _hud.OpenCodexForTest();
            _hud.ShowCodexTabForTest(guidance: true);

            var bodies = _hud.CodexGuidanceBodiesForTest;
            var groups = _hud.CodexGuidanceRowGroupForTest;
            Assert.That(bodies.Count, Is.EqualTo(GuidanceCatalog.Count),
                "every catalog entry needs a row before its glyphs can be measured");

            var notWrapped = new List<string>();
            var overflowing = new List<string>();
            var oversized = new List<string>();
            var deadMetrics = new List<string>();
            var wrapIsLoadBearing = new List<string>();
            var measured = 0;

            for (var g = 0; g < GuidanceCatalog.GroupOrder.Length; g++)
            {
                _hud.ShowCodexGroupForTest(g);
                Canvas.ForceUpdateCanvases();

                for (var i = 0; i < bodies.Count; i++)
                {
                    if (groups[i] != g) continue;
                    var body = bodies[i];
                    var where = CodexRowLabel(i);

                    // The traversal has to actually be doing something: a folded
                    // row measures against a different pixelsPerUnit than the
                    // layout used, so an inactive row here means this loop is
                    // measuring in the wrong coordinate system.
                    Assert.That(body.gameObject.activeInHierarchy, Is.True,
                        $"{where}: group {g} is open but the row is folded away — "
                        + "these metrics would come from a different scale than "
                        + "LayoutCodexGuidance measured with");

                    var rectW = body.rectTransform.rect.width;
                    var rectH = body.rectTransform.rect.height;
                    var single = body.preferredWidth;    // wrap-insensitive
                    var wrapped = body.preferredHeight;  // wrap-sensitive
                    measured++;

                    // Premise (§4m): dead font metrics report 0 for everything,
                    // and every assertion below would pass on a broken layout.
                    if (single <= 0f || wrapped <= 0f)
                        deadMetrics.Add($"  {where}: preferredWidth {single:F1}, "
                            + $"preferredHeight {wrapped:F1} — glyph metrics are dead, "
                            + "so nothing below this line means anything");

                    // Premise: wrap must be load-bearing for at least one row,
                    // or (a) is asserting a mode nothing depends on.
                    if (single > rectW + OverlapEpsilon)
                        wrapIsLoadBearing.Add($"  {where}: one line is {single:F1} u "
                            + $"in a {rectW:F1} u column");

                    if (body.horizontalOverflow != HorizontalWrapMode.Wrap)
                        notWrapped.Add($"  {where}: horizontalOverflow is "
                            + $"{body.horizontalOverflow}, and one line of this copy is "
                            + $"{single:F1} u against a {rectW:F1} u column — "
                            + $"{Mathf.Max(0f, single - rectW):F1} u of text renders outside "
                            + "the rect, where no rect audit can see it");

                    if (wrapped > rectH + 0.5f)
                        overflowing.Add($"  {where}: wrapped text is {wrapped:F1} u tall "
                            + $"in a {rectH:F1} u rect ({wrapped - rectH:F1} u past the "
                            + $"bottom edge; one line is {single:F1} u in {rectW:F1} u)");

                    // And the other direction. A row TALLER than its own text is
                    // not a cosmetic slack: the only way to get one is for the
                    // height to have been measured against a rect that is not
                    // this one. Reading preferredHeight before the width is set
                    // does exactly that — the first pass measures at the
                    // builder's 100 u and applies the answer to a 266 u row, so
                    // every hazard body comes out 31 u for 11 u of text. Nothing
                    // overlaps and nothing escapes the band, so (a), the overflow
                    // check above, T2 and T4 are all satisfied by a layout whose
                    // heights describe a column that does not exist.
                    //
                    // LayoutCodexGuidance promises h = Max(CodexLineH,
                    // preferredHeight), so equality is the actual contract and
                    // `<=` was only half of it.
                    if (rectH > Mathf.Max(wrapped, HudView.CodexMinRowHeightForTest) + 0.5f)
                        oversized.Add($"  {where}: rect is {rectH:F1} u tall for {wrapped:F1} u "
                            + $"of text ({rectH - wrapped:F1} u of dead height) — this height "
                            + "was measured against some other width");
                }
            }

            Assert.That(measured, Is.EqualTo(GuidanceCatalog.Count),
                $"measured {measured} rows for {GuidanceCatalog.Count} entries — a group "
                + "index outside GroupOrder leaves rows that no group ever opens");
            Assert.That(deadMetrics, Is.Empty,
                "glyph metrics returned nothing, so this test cannot tell a wrapped row "
                + "from an overflowing one (the same shape as reading fillAmount off a "
                + "sprite-less Filled image, §4k):\n" + string.Join("\n", deadMetrics));
            Assert.That(wrapIsLoadBearing, Is.Not.Empty,
                "no guidance body is wider than its column on one line, so Wrap changes "
                + "nothing and this test proves nothing. Measured at the shipped 2-column "
                + "width (266 u): 흑요석 기둥 282.0, 승리 조건 340.0, 패배 조건 289.0. If the "
                + "copy shortened, this test needs a new subject, not a pass");
            Assert.That(notWrapped, Is.Empty,
                $"{notWrapped.Count} guidance body/bodies may render past their own rect. "
                + "This is the reported defect: Label() builds every label as Overflow "
                + "(HudView.cs:2409) and the other two multi-line labels override it "
                + "(:1090, :1455). 분출구 rendered 128 u into 이동's column and every rect "
                + "audit stayed green:\n" + string.Join("\n", notWrapped));
            Assert.That(overflowing, Is.Empty,
                $"{overflowing.Count} guidance body/bodies need more lines than their rect "
                + "holds. Wrap trades horizontal overflow for vertical, so a row sized for "
                + "fewer lines than its copy needs puts text under the NEXT row instead of "
                + "beside it:\n" + string.Join("\n", overflowing));
            Assert.That(oversized, Is.Empty,
                $"{oversized.Count} guidance body/bodies are TALLER than their own text. "
                + "The row height is supposed to BE the measurement — a taller rect means "
                + "preferredHeight was read against a width this row does not have, which "
                + "is what reading it before the width is assigned produces. Nothing "
                + "overlaps in that state, so every other check in this file stays "
                + "green:\n" + string.Join("\n", oversized));
        }

        /// <summary>
        /// AC-16 layout: inside one group, no two row surfaces stack.
        ///
        /// Run in BOTH seen states, because the two produce different geometry
        /// from the same code: locked rows are one line ("잠김", 11 u) at a
        /// uniform 31 u pitch, unlocked rows are 1-3 measured lines at a pitch
        /// that varies per row. A layout can be correct for one and wrong for the
        /// other, and the locked state is the one a new player sees first.
        ///
        /// Pairwise across the whole group rather than only within a column: the
        /// column assignment is itself computed (min-depth-first,
        /// LayoutCodexGuidance), so "same column" is a conclusion, not an input,
        /// and a column-width error would hide from a per-column check.
        ///
        /// Titles and bodies are checked together. A title sits at y and its body
        /// at y - CodexTitleH, so they share exactly one edge — touching, which
        /// the epsilon allows, and which is why an off-by-one in the pitch shows
        /// up here as a real overlap rather than as noise.
        ///
        /// Mutations that turn this RED:
        ///   - Advance y by the constant `CodexTitleH + CodexLineH + CodexRowGap`
        ///     instead of the measured `h` → a 2-line body (21 u) in a 31 u pitch
        ///     runs 10 u into the next title. This is the state the fix passed
        ///     through: wrap on, pitch still fixed.
        ///   - Restore the fixed 26 u pitch → ~9 u overlaps in every group with a
        ///     wrapped row.
        ///   - Drop the per-group `y[c] = CodexBodyTop` reset in the seen=false
        ///     pass and rows keep descending — caught by T4, not here, which is
        ///     why both exist.
        /// </summary>
        [Test]
        public void CodexGuidance_NoTwoRowsInAGroupStack()
        {
            BuildHud();
            var canvas = WorldSpaceCanvas();
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace),
                "world corners are only plain canvas units in WorldSpace");

            foreach (var seen in new[] { true, false })
            {
                _hud.CloseCodex();
                _hud.CodexEntrySeen = _ => seen;
                _hud.OpenCodexForTest();          // re-runs RefreshCodexGuidance + layout
                _hud.ShowCodexTabForTest(guidance: true);

                var titles = _hud.CodexGuidanceTitlesForTest;
                var bodies = _hud.CodexGuidanceBodiesForTest;
                var groups = _hud.CodexGuidanceRowGroupForTest;
                var state = seen ? "unlocked" : "locked";

                for (var g = 0; g < GuidanceCatalog.GroupOrder.Length; g++)
                {
                    _hud.ShowCodexGroupForTest(g);
                    Canvas.ForceUpdateCanvases();

                    var rects = new List<Rect>();
                    var names = new List<string>();
                    for (var i = 0; i < bodies.Count; i++)
                    {
                        if (groups[i] != g) continue;
                        rects.Add(WorldRectOf(titles[i].rectTransform));
                        names.Add($"{CodexRowLabel(i)} title");
                        rects.Add(WorldRectOf(bodies[i].rectTransform));
                        names.Add($"{CodexRowLabel(i)} body \"{bodies[i].text}\"");
                    }

                    Assert.That(rects.Count, Is.GreaterThan(0),
                        $"{state}: group {g} ({GuidanceCatalog.GroupTitle(GuidanceCatalog.GroupOrder[g])}) "
                        + "built no rows, so its layout is untested");

                    var stacked = new List<string>();
                    for (var a = 0; a < rects.Count; a++)
                    {
                        Assert.That(rects[a].width > 0f && rects[a].height > 0f, Is.True,
                            $"{state}: {names[a]} has a degenerate rect "
                            + $"[{rects[a].width:F1} x {rects[a].height:F1}] — the layout "
                            + "never resolved it, so overlap cannot be measured");
                        for (var b = a + 1; b < rects.Count; b++)
                        {
                            var dx = Mathf.Min(rects[a].xMax, rects[b].xMax)
                                   - Mathf.Max(rects[a].xMin, rects[b].xMin);
                            var dy = Mathf.Min(rects[a].yMax, rects[b].yMax)
                                   - Mathf.Max(rects[a].yMin, rects[b].yMin);
                            if (dx > OverlapEpsilon && dy > OverlapEpsilon)
                                stacked.Add($"  {names[a]} "
                                    + $"[{rects[a].xMin:F1}..{rects[a].xMax:F1} x "
                                    + $"{rects[a].yMin:F1}..{rects[a].yMax:F1}] stacks on "
                                    + $"{names[b]} "
                                    + $"[{rects[b].xMin:F1}..{rects[b].xMax:F1} x "
                                    + $"{rects[b].yMin:F1}..{rects[b].yMax:F1}] "
                                    + $"by {dx:F1} x {dy:F1} u = {dx * dy:F0} u²");
                        }
                    }

                    Assert.That(stacked, Is.Empty,
                        $"{state} group {g} "
                        + $"({GuidanceCatalog.GroupTitle(GuidanceCatalog.GroupOrder[g])}): "
                        + $"{stacked.Count} row surface(s) drawn on top of another. The "
                        + "reported defect measured 104 x 14 = 1,459 u² of 분출구 over 이동; "
                        + "with wrap on, the same pitch error reappears vertically:\n"
                        + string.Join("\n", stacked));
                }
            }
        }

        /// <summary>
        /// AC-16 paging: five chips, one open group, honest per-group counts.
        ///
        /// The paging is not decoration — it is the height budget. All 23 bodies
        /// at once needs 386 u in its best column count against a 201.8 u body
        /// (probe: cols=2 440, cols=3 329, cols=4 294 against 300 before the chip
        /// row), so "exactly one group is open" is the constraint that makes the
        /// tab fit at all. Two open groups is not a cosmetic bug; it is the
        /// overflow returning.
        ///
        /// The default open group is 0, and GroupOrder puts 위험 there on purpose:
        /// "Hazards first: they are the ones that kill a player who does not know
        /// them" (GuidanceCatalog.cs:232-233). A player who opens the tab and has
        /// to hunt for the thing that killed them got the ordering they were not
        /// promised.
        ///
        /// Chip counts are checked against a PARTIAL seen predicate. With every
        /// bit seen, "seen" and "total" are the same number and a chip printing
        /// its total, its group's total, or group 0's total all read correctly —
        /// the classic coordinate system where right and wrong coincide (§4m).
        ///
        /// Mutations that turn this RED:
        ///   - `ShowCodexGroup(0)` dropped from BuildCodexGuidanceTab → no group
        ///     open, first assert fails.
        ///   - `ShowCodexGroup(1)` or GroupOrder reordered so 위험 is not first →
        ///     the default-group assert fails.
        ///   - `SetActive(g == _codexOpenGroup)` → `SetActive(true)` → all 23 rows
        ///     active at once, the height budget is gone, exactly-one fails.
        ///   - `ByGroup(GuidanceCatalog.GroupOrder[g])` → `ByGroup((GuidanceGroup)g)`
        ///     in RefreshCodexChipCounts → chip 0 counts Control (3/9) while it
        ///     is labelled 위험 (2/6).
        ///   - Chip count printing `entries.Length + "/" + entries.Length` or
        ///     never incrementing `seen` → both fail on the partial predicate and
        ///     neither would fail on an all-seen one.
        ///   - Invert the tint in SetCodexChipActive → the open chip reads dimmer
        ///     than the four folded ones.
        /// </summary>
        [Test]
        public void CodexGuidance_PagesOneGroupAtATimeAndCountsEachHonestly()
        {
            BuildHud();
            _hud.CodexEntrySeen = _ => true;
            _hud.OpenCodexForTest();
            _hud.ShowCodexTabForTest(guidance: true);

            var chips = _hud.CodexGroupChipsForTest;
            var order = GuidanceCatalog.GroupOrder;
            Assert.That(chips.Count, Is.EqualTo(order.Length),
                $"the tab built {chips.Count} chips for {order.Length} groups — a group with "
                + "no chip is a group the player cannot reach, and its entries are "
                + "markable-as-seen and unreadable");

            // Chip i must be labelled for group i. The label is the only thing
            // the player can use to tell them apart.
            for (var g = 0; g < order.Length; g++)
            {
                var expected = GuidanceCatalog.GroupTitle(order[g]);
                var found = new List<string>();
                foreach (var text in chips[g].GetComponentsInChildren<Text>(true))
                    if (!string.IsNullOrEmpty(text.text)) found.Add(text.text);
                Assert.That(found, Contains.Item(expected),
                    $"chip {g} must read \"{expected}\" ({order[g]}), and carries "
                    + $"[{string.Join(", ", found)}] instead — chip order and GroupOrder are "
                    + "the same list, so a chip labelled for another group opens a page the "
                    + "player did not ask for");
            }

            // Hazards first, by the catalog's own stated reason.
            Assert.That(order[0], Is.EqualTo(GuidanceGroup.Hazard),
                "GroupOrder must open on hazards — \"they are the ones that kill a player "
                + "who does not know them\" (GuidanceCatalog.cs:232). A player who just died "
                + "to a vent opens this tab looking for the vent");
            Assert.That(_hud.CodexOpenGroupForTest, Is.Zero,
                $"the tab opened on group {_hud.CodexOpenGroupForTest} "
                + $"({GuidanceCatalog.GroupTitle(order[Mathf.Clamp(_hud.CodexOpenGroupForTest, 0, order.Length - 1)])}), "
                + "not group 0 (위험)");

            // Exactly one group's rows on screen, for every group.
            var groups = _hud.CodexGuidanceRowGroupForTest;
            var bodies = _hud.CodexGuidanceBodiesForTest;
            for (var g = 0; g < order.Length; g++)
            {
                _hud.ShowCodexGroupForTest(g);
                Assert.That(_hud.CodexOpenGroupForTest, Is.EqualTo(g),
                    $"asked for group {g}, the panel reports {_hud.CodexOpenGroupForTest}");

                var live = new List<string>();
                var folded = 0;
                for (var i = 0; i < bodies.Count; i++)
                {
                    if (bodies[i].gameObject.activeInHierarchy) live.Add(CodexRowLabel(i));
                    else folded++;
                }
                var expected = 0;
                for (var i = 0; i < groups.Count; i++) if (groups[i] == g) expected++;

                Assert.That(live.Count, Is.EqualTo(expected),
                    $"group {g} ({GuidanceCatalog.GroupTitle(order[g])}) is open, so exactly "
                    + $"{expected} rows must be on screen — found {live.Count} live and "
                    + $"{folded} folded. One group at a time IS the height budget: all 23 "
                    + "bodies need 386 u in their best column count against a 201.8 u body.\n"
                    + "  live: " + string.Join(", ", live));
                foreach (var i in LiveRowIndices(bodies))
                    Assert.That(groups[i], Is.EqualTo(g),
                        $"{CodexRowLabel(i)} belongs to group {groups[i]} "
                        + $"({GuidanceCatalog.GroupTitle(order[groups[i]])}) and is on screen "
                        + $"while group {g} ({GuidanceCatalog.GroupTitle(order[g])}) is open");

                // The open chip has to LOOK open. Compared as a signal STRING
                // (plate sprite + tint) rather than either one alone: the
                // implementation carries state in the sprite and keeps colour at
                // white, and a colour-only check failed this working panel once
                // already. Comparing the pair also refuses to retype the literal
                // sprite names out of SetCodexChipActive, which would make this a
                // copy of the implementation instead of a check on it (§4i).
                var openSignal = ChipStateSignal(chips[g]);
                for (var other = 0; other < chips.Count; other++)
                {
                    if (other == g) continue;
                    var foldedSignal = ChipStateSignal(chips[other]);
                    Assert.That(openSignal, Is.Not.EqualTo(foldedSignal),
                        $"chip {g} ({GuidanceCatalog.GroupTitle(order[g])}) is the open one "
                        + $"and chip {other} ({GuidanceCatalog.GroupTitle(order[other])}) is "
                        + $"folded, but both render as \"{openSignal}\" — the player has no "
                        + "way to tell which page they are on");
                }
            }

            // Per-group counts, against a predicate that is true for SOME bits.
            _hud.CloseCodex();
            _hud.CodexEntrySeen = bit => bit % 3 == 0;
            _hud.OpenCodexForTest();
            _hud.ShowCodexTabForTest(guidance: true);

            var partial = 0;
            var distinct = new List<string>();
            for (var g = 0; g < order.Length; g++)
            {
                var entries = GuidanceCatalog.ByGroup(order[g]);
                var seen = 0;
                foreach (var entry in entries) if (entry.Bit % 3 == 0) seen++;
                var expected = seen + "/" + entries.Length;
                distinct.Add(expected);
                if (seen > 0 && seen < entries.Length) partial++;

                Assert.That(_hud.CodexChipCountForTest(g), Is.EqualTo(expected),
                    $"chip {g} ({GuidanceCatalog.GroupTitle(order[g])}) printed "
                    + $"\"{_hud.CodexChipCountForTest(g)}\" for {entries.Length} entries of "
                    + $"which {seen} are seen. The count is the only progress the four folded "
                    + "groups still show, and a wrong one sends the player to a page that "
                    + "has nothing new on it");
            }
            Assert.That(partial, Is.GreaterThan(0),
                "no group is partially seen under this predicate, so a chip printing its "
                + $"total instead of its seen count would still pass. Counts: "
                + string.Join(" ", distinct));
            Assert.That(new HashSet<string>(distinct).Count, Is.GreaterThan(1),
                "every chip printed the same string, so a chip that always counts group 0 "
                + $"would pass: {string.Join(" ", distinct)}");
        }

        /// <summary>
        /// AC-16 containment: all 23 rows, folded groups included, live inside
        /// the body band the layout claims to use.
        ///
        /// The band is read from the seams (CodexBodyTopForTest,
        /// CodexBodyHeightForTest), never retyped. §4i: a test that hand-copies
        /// -98.2 and 201.8 stops measuring the layout the moment either constant
        /// moves, and then two sources describe one band. Retyping them is the
        /// exact shape of the drift this repo has already eaten twice.
        ///
        /// Hidden rows are measured too. Their geometry is real — every group is
        /// laid out, not just the open one — and a group that only overflows while
        /// folded would be invisible to a test that walked the open page.
        ///
        /// The chip row is checked against the same seam in the same units, which
        /// closes the band from above: the chips own [tabTop - CodexChipSide,
        /// tabTop], the rows own the CodexBodyHeightForTest beneath it, and
        /// CodexBodyTopForTest is what separates them.
        ///
        /// Mutations that turn this RED:
        ///   - Drop the per-group `y[c] = CodexBodyTop` reset → groups 2-5 march
        ///     down past the band; the last groups leave the panel entirely,
        ///     which is the 238 u overflow this cycle already shipped once.
        ///   - Fill-first-column instead of min-depth-first → 조작's 9 rows land
        ///     in one column at 279 u against 201.8 u.
        ///   - A sixth group, or copy long enough to push the worst group past
        ///     201.8 u → RED, which is the point: the budget is checked, not
        ///     asserted in a comment.
        ///
        /// Two mutations this test does NOT catch, verified by running them
        /// rather than reasoned about (both stayed green at 370/370):
        ///   - `CodexBodyTop` replaced by a literal -90f. The chip then reaches
        ///     0.2 u into the band, and OverlapEpsilon is 1 u — the same
        ///     tolerance HudLayoutTests and LobbyLayoutTests audit with. Tighten
        ///     it here alone and the three files stop agreeing about what
        ///     "touching" means, which is a worse trade than a 0.2 u miss.
        ///   - preferredHeight read before the rect width is assigned. Rows come
        ///     out 31 u for 11 u of text — too TALL, not too deep — and the worst
        ///     group still lands inside the band. T1's `oversized` check owns
        ///     that one; it is a height-contract failure, not a band failure.
        /// </summary>
        [Test]
        public void CodexGuidance_EveryRowLivesInsideTheBodyBand()
        {
            BuildHud();
            _hud.CodexEntrySeen = _ => true;   // deepest state: every body full length
            // Canvas first, then open. Opening runs the layout, and the layout
            // measures glyphs at the live scale: at the unpinned batchmode scale
            // (ppu 0.577) every body fits one line, so the band would be audited
            // against a geometry where no row is ever taller than its minimum —
            // the shallow case, and not the one that ships. Pinned first, 흑요석
            // 기둥 is two lines again and the band has something to hold.
            WorldSpaceCanvas();
            _hud.OpenCodexForTest();
            _hud.ShowCodexTabForTest(guidance: true);

            var bodies = _hud.CodexGuidanceBodiesForTest;
            var titles = _hud.CodexGuidanceTitlesForTest;
            Assert.That(bodies.Count, Is.EqualTo(GuidanceCatalog.Count),
                "every catalog entry needs a row before its position can be audited");

            // The band, from the seams the layout used.
            var bandHeight = HudView.CodexBodyHeightForTest;
            var bandOffset = HudView.CodexBodyTopForTest;
            Assert.That(bandHeight, Is.GreaterThan(0f),
                $"the body band is {bandHeight:F1} u tall — nothing can be inside it");
            Assert.That(bandOffset, Is.LessThan(0f),
                $"CodexBodyTopForTest is {bandOffset:F1}; the band starts BELOW the tab top, "
                + "so this must be negative or the band is being measured upward");

            var escaped = new List<string>();
            // Signed slack, tracked for every row: negative means inside the
            // band. Reported unconditionally so a green run still says how much
            // room the worst group has left, and the next group added to the
            // catalog is a measurement rather than a surprise.
            var deepest = float.NegativeInfinity;
            string deepestRow = null;

            for (var i = 0; i < bodies.Count; i++)
            {
                var page = bodies[i].rectTransform.parent as RectTransform;
                Assert.That(page, Is.Not.Null, $"{CodexRowLabel(i)} has no page parent");
                // The band is expressed in TAB-LOCAL units and the page is parked
                // at the tab origin with a top-left pivot, so the page's world
                // top IS the tab top. Asserted rather than assumed: if the pivot
                // moves, every number below is offset by the page height.
                Assert.That(page.pivot.y, Is.EqualTo(1f).Within(0.001f),
                    $"{CodexRowLabel(i)}: its page pivot is {page.pivot} — the band is "
                    + "measured down from the page's TOP edge, which is only the tab top "
                    + "while the pivot is top-left");
                Assert.That(page.anchoredPosition.y, Is.EqualTo(0f).Within(0.001f),
                    $"{CodexRowLabel(i)}: its page sits at y {page.anchoredPosition.y:F1} "
                    + "inside the tab, so tab-local and page-local no longer agree and the "
                    + "band constants describe a different band than the rows use");

                var tabTop = WorldRectOf(page).yMax;
                var bandTop = tabTop + bandOffset;
                var bandBottom = bandTop - bandHeight;

                foreach (var rect in new[] { titles[i].rectTransform, bodies[i].rectTransform })
                {
                    var world = WorldRectOf(rect);
                    var kind = rect == titles[i].rectTransform ? "title" : "body";
                    var above = world.yMax - bandTop;
                    var below = bandBottom - world.yMin;
                    if (below > deepest) { deepest = below; deepestRow = $"{CodexRowLabel(i)} {kind}"; }
                    if (above > OverlapEpsilon)
                        escaped.Add($"  {CodexRowLabel(i)} {kind} tops out {above:F1} u ABOVE "
                            + $"the band [{bandBottom:F1}..{bandTop:F1}] — it is under the "
                            + "chip row");
                    if (below > OverlapEpsilon)
                        escaped.Add($"  {CodexRowLabel(i)} {kind} runs {below:F1} u BELOW the "
                            + $"band [{bandBottom:F1}..{bandTop:F1}] "
                            + $"(row at {world.yMin:F1}..{world.yMax:F1})");
                }
            }

            Assert.That(escaped, Is.Empty,
                $"{escaped.Count} row surface(s) outside the {bandHeight:F1} u body band. "
                + $"Closest row to the bottom edge: {deepestRow} at {deepest:F1} u past it "
                + "(negative = still inside the band). "
                + "A row outside the band is either behind the chips or off the panel — the "
                + "238 u overflow this cycle already shipped cut the last two groups off the "
                + "bottom while nine content tests stayed green:\n" + string.Join("\n", escaped));

            // Close the band from above with the SAME seam, in the same units:
            // the chips must not reach into the rows' band.
            var chips = _hud.CodexGroupChipsForTest;
            Assert.That(chips.Count, Is.GreaterThan(0), "the tab must build its chips");
            var chipSide = HudView.CodexChipSideForTest;
            for (var g = 0; g < chips.Count; g++)
            {
                _hud.ShowCodexGroupForTest(g);
                Canvas.ForceUpdateCanvases();
                var chipRect = (RectTransform)chips[g].transform;
                Assert.That(chipRect.rect.height, Is.EqualTo(chipSide).Within(0.05f),
                    $"chip {g} is {chipRect.rect.height:F1} u tall against a declared "
                    + $"CodexChipSide of {chipSide:F1} — the band below it is derived from "
                    + "that constant, so the two must be the same number");

                var chipWorld = WorldRectOf(chipRect);
                var tabRect = chipRect.parent as RectTransform;
                Assert.That(tabRect, Is.Not.Null, $"chip {g} has no tab parent");
                var bandTop = WorldRectOf(tabRect).yMax + bandOffset;
                Assert.That(chipWorld.yMin, Is.GreaterThanOrEqualTo(bandTop - OverlapEpsilon),
                    $"chip {g} ({GuidanceCatalog.GroupTitle(GuidanceCatalog.GroupOrder[g])}) "
                    + $"reaches down to {chipWorld.yMin:F1}, which is "
                    + $"{bandTop - chipWorld.yMin:F1} u into the row band that starts at "
                    + $"{bandTop:F1}. The chip row and the rows below it are sized from the "
                    + "same constant precisely so this cannot happen");
            }
        }

        /// <summary>
        /// AC-1: the guidance tab is named 게임설명, not 기록.
        ///
        /// The rename is the smallest change in the cycle and was the only one
        /// with no test at all — a full mutation sweep put the old string back
        /// and all 370 stayed green. Small and undefended is how a string walks
        /// backwards during an unrelated edit.
        ///
        /// It is not cosmetic. 기록 names a LOG — "what I have already seen" —
        /// and the tab holds how the game is played, how a run progresses, and
        /// the win and lose conditions. A player looking for the rules does not
        /// open a log, which is the same failure the survey recorded against
        /// Gungeon's Ammonomicon (design/trend-survey/progression-navigation.md):
        /// a record of what you saw is not a guide to what you have not.
        ///
        /// The font is asserted alongside the string because WebGL has no OS
        /// fallback: 게임설명 needed 임 and 설, which were NOT in the 538-glyph
        /// subset this cycle started with. A rename that ships without the
        /// regenerated font renders two tofu boxes, and no string comparison
        /// would notice (§4b).
        /// </summary>
        [Test]
        public void Codex_GuidanceTabIsNamedForWhatItHolds()
        {
            BuildHud();
            _hud.OpenCodexForTest();

            var panel = _hud.CodexRectForTest;
            Assert.That(panel, Is.Not.Null, "the codex panel must exist once opened");

            var labels = new List<string>();
            foreach (var text in panel.GetComponentsInChildren<Text>(true))
                if (!string.IsNullOrEmpty(text.text)) labels.Add(text.text);

            Assert.That(labels, Contains.Item("게임설명"),
                "the guidance tab must be labelled 게임설명 — it holds the rules, the "
                + "progression and the win/lose conditions. Labels found: ["
                + string.Join(", ", labels) + "]");
            Assert.That(labels, Does.Not.Contain("기록"),
                "the old name 기록 is still on the panel. It describes a log of what the "
                + "player has seen, and the tab is a guide to what they have not");

            // The glyphs have to exist, or the correct string renders as boxes.
            var font = Resources.Load<Font>("Fonts/HudKorean");
            Assert.That(font, Is.Not.Null, "the HUD font must load from Resources");
            var missing = new List<char>();
            foreach (var ch in "게임설명")
                if (!font.HasCharacter(ch)) missing.Add(ch);
            Assert.That(missing, Is.Empty,
                $"the shipped font subset is missing {missing.Count} glyph(s) of the tab "
                + $"name: [{string.Join(", ", missing)}]. WebGL has no OS fallback, so each "
                + "one renders as a tofu box. Re-run tools/gen_hud_font.sh and commit "
                + "Assets/Resources/Fonts/HudKorean.otf with the string that needed it");
        }

        /// <summary>
        /// AC-13: both left-stack buttons carry a glyph AND keep their word.
        ///
        /// Icon-only was considered and refused for 포기 in particular: one
        /// mis-tap forfeits the whole run, and a door-with-an-arrow is not worth
        /// a run to a player who reads it wrong. So this asserts the pair, not
        /// the icon — dropping either half is a regression, and dropping the
        /// label is the more expensive one.
        ///
        /// The sprite is asserted non-null rather than merely present, because
        /// TextButton disables the Image when Resources.Load misses. That
        /// fallback is correct (a sprite-less Image draws a white quad, §4k) and
        /// it is also silent: delete the PNG and the button keeps working with
        /// no glyph and nothing fails.
        ///
        /// Mutations that turn this RED: drop either `iconId:` argument; delete
        /// either PNG from Resources/Icons; remove the label to go icon-only.
        /// </summary>
        [Test]
        public void LeftStack_ButtonsCarryBothAGlyphAndTheirWord()
        {
            BuildHud();
            _hud.SetLeftStackAvailable(true);

            var expected = new[]
            {
                ("정보", _hud.CodexButtonRectForTest),
                ("포기", _hud.AbandonRectForTest),
            };

            foreach (var (word, rect) in expected)
            {
                Assert.That(rect, Is.Not.Null, $"the {word} button must exist once the "
                    + "left stack is available");

                var words = new List<string>();
                foreach (var text in rect.GetComponentsInChildren<Text>(true))
                    if (!string.IsNullOrEmpty(text.text)) words.Add(text.text);
                Assert.That(words, Contains.Item(word),
                    $"the {word} button lost its label and is glyph-only. Recognition is "
                    + "what the icon buys; confirmation is what the word buys, and 포기 "
                    + $"costs a whole run when it is read wrong. Found: [{string.Join(", ", words)}]");

                var glyphs = new List<string>();
                foreach (var image in rect.GetComponentsInChildren<Image>(true))
                    if (image.sprite != null && image.sprite.name != "ui-button"
                        && image.sprite.name != "ui-button-active")
                        glyphs.Add(image.sprite.name);
                Assert.That(glyphs, Is.Not.Empty,
                    $"the {word} button carries no glyph — either the iconId argument is "
                    + "gone or its PNG is missing from Resources/Icons, and TextButton "
                    + "disables the Image rather than drawing a white quad, so the button "
                    + "keeps working and says nothing");
            }
        }

        /// <summary>
        /// AC-17: the codex and the abandon modal are MUTUALLY EXCLUSIVE, both
        /// directions.
        ///
        /// Found in the cycle-7 browser smoke, and it had been shippable since
        /// AMENDMENT #9. The modal is 480x200 and its raycast blocker stops taps
        /// that land ON it — the 정보 button is outside that rect and stayed live,
        /// so the codex opened over a live modal and one dismiss press had two
        /// owners.
        ///
        /// Why every existing automated check was blind to it (§4m, the sharpest
        /// instance in this file): `GuidancePaused` is an OR of both surfaces
        /// (HudView.cs:1053-1055). Codex only → true. Modal only → true. BOTH →
        /// true. The pause predicate cannot distinguish the correct state from
        /// the broken one, so this test is forbidden from using it and reads the
        /// two surfaces separately instead. Every check that could have caught
        /// this was asking "did it pause" and none was asking "what is on
        /// screen".
        ///
        /// The geometric premise is asserted, not assumed: if the modal ever
        /// grows to cover the 정보 button, the blocker starts doing this job and
        /// the exclusion below stops being the only thing holding the invariant.
        /// That is a change of subject, and it should fail here and be re-reasoned
        /// rather than silently reduce this test to a tautology.
        ///
        /// Mutations that turn this RED:
        ///   - Delete `CloseAbandonModal()` from OpenCodex → the codex-over-modal
        ///     direction fails. This is the shipped defect, restored.
        ///   - Delete `CloseCodex()` from OpenAbandonModal → the modal-over-codex
        ///     direction fails. This is the worse half: the codex is 620x440 and
        ///     covers the modal's two buttons outright, so the player is asked a
        ///     question whose answers are behind another panel.
        ///   - Make either close a no-op for the other's state (e.g. gate
        ///     CloseAbandonModal behind `_codexOpen`) → whichever direction was
        ///     gated fails.
        ///   - Re-express either assert through GuidancePaused → GREEN on all of
        ///     the above, which is the whole reason for this test.
        /// </summary>
        [Test]
        public void CodexAndAbandonModal_NeverHoldTheRunAtTheSameTime()
        {
            BuildHud();
            _hud.AbandonRelicsAtRisk = () => 7;
            _hud.SetLeftStackAvailable(true);

            // Premise: the 정보 button is OUTSIDE the modal's blocker, which is
            // why code has to enforce the exclusion at all.
            _hud.OpenAbandonModalForTest();
            var canvas = WorldSpaceCanvas();
            var modal = FindActivePanelSized(canvas, 480f, 200f);
            Assert.That(modal, Is.Not.Null,
                "the 480x200 abandon modal must be on screen before its blocker can be "
                + "measured");
            var modalRect = WorldRectOf(modal);
            var button = _hud.CodexButtonRectForTest;
            Assert.That(button, Is.Not.Null,
                "the 정보 button must exist — SetLeftStackAvailable(true) builds it, and "
                + "without it nothing can be pressed over the modal and this test has no "
                + "subject");
            Assert.That(button.gameObject.activeInHierarchy, Is.True,
                "the 정보 button is not on screen while the modal is up, so the tap that "
                + "caused this defect could not land and the premise below is describing a "
                + "button the player cannot reach");
            var buttonRect = WorldRectOf(button);
            var coverX = Mathf.Min(modalRect.xMax, buttonRect.xMax)
                       - Mathf.Max(modalRect.xMin, buttonRect.xMin);
            var coverY = Mathf.Min(modalRect.yMax, buttonRect.yMax)
                       - Mathf.Max(modalRect.yMin, buttonRect.yMin);
            Assert.That(coverX <= OverlapEpsilon || coverY <= OverlapEpsilon, Is.True,
                $"the 정보 button [{buttonRect.xMin:F1}..{buttonRect.xMax:F1} x "
                + $"{buttonRect.yMin:F1}..{buttonRect.yMax:F1}] is covered by the modal "
                + $"[{modalRect.xMin:F1}..{modalRect.xMax:F1} x "
                + $"{modalRect.yMin:F1}..{modalRect.yMax:F1}] by {coverX:F1} x {coverY:F1} u. "
                + "The modal's raycast blocker would then be stopping the tap on its own and "
                + "the exclusion below is no longer the only guard — re-reason this test "
                + "instead of letting it pass for a reason it does not state");

            // Direction 1: modal up, then codex. The reported defect.
            Assert.That(_hud.AbandonModalActiveForTest, Is.True,
                "the modal must be up before the codex is asked to displace it");
            Assert.That(_hud.CodexOpenForTest, Is.False,
                "the codex must be shut at the start of this direction, or the assert below "
                + "proves nothing about what opening it did");

            _hud.OpenCodexForTest();
            Assert.That(_hud.CodexOpenForTest, Is.True,
                "opening the codex over a modal must still open the codex — the fix is to "
                + "displace the modal, not to refuse the press");
            Assert.That(_hud.AbandonModalActiveForTest, Is.False,
                "the abandon modal is STILL on screen underneath the open codex. This is the "
                + "reported defect: the modal's blocker only covers its own 480x200 rect, the "
                + "정보 button sits outside it, and one dismiss press now has two owners. "
                + "GuidancePaused was true before and after, which is exactly why nothing "
                + "caught this from AMENDMENT #9 until a human looked at the screen");

            // Direction 2: codex up, then modal. The worse half — the codex is
            // 620x440 and buries the modal's two answers.
            _hud.CloseCodex();
            _hud.OpenCodexForTest();
            Assert.That(_hud.CodexOpenForTest, Is.True, "the codex must be up for direction 2");
            Assert.That(_hud.AbandonModalActiveForTest, Is.False,
                "the modal must be down at the start of direction 2");

            _hud.OpenAbandonModalForTest();
            Assert.That(_hud.AbandonModalActiveForTest, Is.True,
                "opening the abandon modal over the codex must still open the modal");
            Assert.That(_hud.CodexOpenForTest, Is.False,
                "the codex is STILL open underneath the abandon modal. The codex panel is "
                + "620x440 against the modal's 480x200, so it covers 계속 싸운다 and "
                + "포기하고 나간다 outright — the player is asked whether to forfeit the run "
                + "and both answers are behind another panel");

            // Now cycle. The two asserts above each fire once, on a HUD where
            // the surface being displaced had never been displaced before —
            // and `A && B` restated here would be a tautology, because
            // CodexOpenForTest was just asserted False one line up.
            //
            // What can still fail is the SECOND traversal. Both closers guard on
            // their own state (CloseCodex on _codexOpen, CloseAbandonModal on
            // activeSelf), and a guard that goes stale — an _codexOpen left set
            // after the panel hid, an activeSelf read before SetActive — displaces
            // nothing on the pass where the flag is already where the guard
            // expects it. That is a second-press defect, and one press cannot see
            // it.
            for (var pass = 0; pass < 3; pass++)
            {
                _hud.OpenCodexForTest();
                Assert.That(_hud.CodexOpenForTest, Is.True,
                    $"pass {pass}: the codex refused to reopen after being displaced — a "
                    + "closer left _codexOpen set while the panel went down, so the button "
                    + "is now dead for the rest of the run");
                Assert.That(_hud.AbandonModalActiveForTest, Is.False,
                    $"pass {pass}: opening the codex left the abandon modal up. The first "
                    + "traversal displaced it correctly, so this is a guard that only holds "
                    + "the first time");

                _hud.OpenAbandonModalForTest();
                Assert.That(_hud.AbandonModalActiveForTest, Is.True,
                    $"pass {pass}: the abandon modal refused to reopen after being displaced");
                Assert.That(_hud.CodexOpenForTest, Is.False,
                    $"pass {pass}: opening the abandon modal left the codex open underneath "
                    + "it, on a pass where the first traversal succeeded");
            }
        }

        /// <summary>
        /// AC-18 (§4o): while a surface holds the run, the touch combat controls
        /// are DOWN — and they come back when it lets go.
        ///
        /// Every run-holding surface calls SetTouchCombatControlsVisible(false)
        /// by hand, and SyncTouchModeSurfaces defensively re-hides for exactly
        /// two of them: the game-over and stage-clear panels (HudView.cs:1296).
        /// The codex, the abandon modal and the guidance card are not on that
        /// list, so for those three the CALL ORDER is the only defense. That is
        /// what this test measures.
        ///
        /// The displacement paths are the sharp cases and no test reached them
        /// before. Both openers close the other surface FIRST and hide the
        /// controls AFTER:
        ///     OpenCodex:         CloseAbandonModal() … SetTouchCombat(false)
        ///     OpenAbandonModal:  CloseCodex()        … SetTouchCombat(false)
        /// and each of those closers ends in SetTouchCombat(TRUE)
        /// (CloseAbandonModal:1562, CloseCodex). Swap the two statements in
        /// either opener and the run is held with the joystick live — the exact
        /// shape of "taps landing on a sim that cannot tick" that the comment in
        /// OpenCodex says it is preventing.
        ///
        /// What this test does NOT claim, and why (§4i — one band, one source):
        /// the death panel is asymmetric. GameOver hides the controls at :2865
        /// and THEN calls SetLeftStackAvailable(false) at :2871, which closes the
        /// codex and the modal, and those closers set the field back to true. So
        /// with the codex open at death the FIELD ends up true while the TARGETS
        /// are correctly down — SyncTouchModeSurfaces' terminal-modal guard
        /// catches it. Asserting field == false there would fail on correct code,
        /// so the death panel is asserted on its EFFECT only, and the asymmetry is
        /// recorded here rather than encoded as a contract that does not hold.
        ///
        /// Not a restatement of DismissWithNoCard_LeavesEveryOtherSurfaceUntouched
        /// (:751): that one proves a stray DISMISS cannot restore the controls
        /// under an open modal. This one proves the OPEN paths put them down in
        /// the first place, for four surfaces and across displacement.
        ///
        /// Mutations that turn this RED:
        ///   - Move `CloseAbandonModal()` in OpenCodex to after
        ///     `SetTouchCombatControlsVisible(false)` → the modal→codex
        ///     displacement leaves the joystick live under an open codex.
        ///   - Move `CloseCodex()` in OpenAbandonModal likewise → the mirror.
        ///   - Drop `SetTouchCombatControlsVisible(false)` from OpenCodex → the
        ///     plain codex case fails on both field and targets.
        ///   - Drop `SetTouchCombatControlsVisible(true)` from CloseCodex → the
        ///     release assert fails: the controls never come back and the run is
        ///     unplayable after one codex visit.
        ///   - Drop the `!terminalModalVisible` guard in SyncTouchModeSurfaces →
        ///     the death-panel-over-codex case fails on targets, which is the
        ///     case that guard exists for and nothing was checking.
        /// </summary>
        [Test]
        public void RunHoldingSurfaces_KeepTheTouchControlsDownWhileTheyHoldIt()
        {
            BuildHud();
            // Batchmode has no Touchscreen, so the surfaces have to be forced
            // into existence before their active state means anything (:756).
            _hud.ForceTouchControlsForTest();
            Assert.That(_hud.TouchActive, Is.True,
                "the touch surfaces must build, or this is a desktop run wearing a touch "
                + "label and no combat control exists to hide");
            _hud.EnableCampaignUi("차가운 회랑", 3);
            _hud.SetLeftStackAvailable(true);

            var targets = new List<RectTransform>();
            _hud.CollectCombatTouchTargetsForTest(targets);
            Assert.That(targets, Is.Not.Empty,
                "no touch combat targets exist, so every 'they are down' assert below would "
                + "pass over an empty list");

            // Premise: the controls are UP with nothing holding the run. Without
            // this, "down" is indistinguishable from "never built" (§4m).
            Assert.That(_hud.TouchCombatControlsVisibleForTest, Is.True,
                "a live run with no surface over it must leave the combat controls up");
            Assert.That(ActiveTargetNames(targets), Is.Not.Empty,
                "no touch combat target is on screen during a live, unheld run — so this test "
                + "cannot tell 'hidden by a surface' from 'never shown at all'. "
                + $"Targets built: {targets.Count}");

            // --- the three surfaces where call order is the only defense ------
            AssertHoldsDownTheControls(targets, "codex",
                open: () => _hud.OpenCodexForTest(),
                release: () => _hud.CloseCodex());

            AssertHoldsDownTheControls(targets, "abandon modal",
                open: () => _hud.OpenAbandonModalForTest(),
                release: () => FindButtonLabelled(_hudObject, "계속 싸운다").onClick.Invoke());

            AssertHoldsDownTheControls(targets, "guidance card",
                open: () => Assert.That(_hud.ShowGuidancePause(
                        GuidanceCatalog.VictoryBit, "안내", "승리 조건", "보스를 쓰러뜨린다."),
                    Is.True, "the guidance card must actually open"),
                release: () => _hud.DismissGuidancePause());

            // --- displacement: the case nothing reached before ----------------
            // modal up, then codex over it. OpenCodex closes the modal (which
            // sets the field TRUE) before hiding the controls, so the final value
            // is only correct because of the order.
            _hud.OpenAbandonModalForTest();
            Assert.That(_hud.AbandonModalActiveForTest, Is.True, "precondition: modal up");
            _hud.OpenCodexForTest();
            Assert.That(_hud.CodexOpenForTest, Is.True, "precondition: codex took over");
            Assert.That(_hud.TouchCombatControlsVisibleForTest, Is.False,
                "the codex displaced the abandon modal and the combat controls came back up "
                + "with it. CloseAbandonModal ends in SetTouchCombatControlsVisible(true), so "
                + "hiding them must happen AFTER that call — the run is held by the codex and "
                + "the joystick is live");
            Assert.That(ActiveTargetNames(targets), Is.Empty,
                "with the codex holding the run after displacing the modal, these targets are "
                + $"still on screen: {string.Join(", ", ActiveTargetNames(targets))}. Nothing "
                + "re-hides for the codex — SyncTouchModeSurfaces only defends the game-over "
                + "and stage-clear panels");

            // codex up, then modal over it. The mirror.
            _hud.CloseCodex();
            _hud.OpenCodexForTest();
            Assert.That(_hud.CodexOpenForTest, Is.True, "precondition: codex up");
            _hud.OpenAbandonModalForTest();
            Assert.That(_hud.AbandonModalActiveForTest, Is.True, "precondition: modal took over");
            Assert.That(_hud.TouchCombatControlsVisibleForTest, Is.False,
                "the abandon modal displaced the codex and the combat controls came back up. "
                + "CloseCodex ends in SetTouchCombatControlsVisible(true), so the modal must "
                + "hide them after that — the player can fight through the dialog asking "
                + "whether to forfeit the run");
            Assert.That(ActiveTargetNames(targets), Is.Empty,
                "with the abandon modal holding the run after displacing the codex, these "
                + $"targets are still live: {string.Join(", ", ActiveTargetNames(targets))}");
            FindButtonLabelled(_hudObject, "계속 싸운다").onClick.Invoke();

            // --- the death panel: EFFECT only, for the reason in the summary --
            _hud.OpenCodexForTest();
            Assert.That(_hud.CodexOpenForTest, Is.True, "precondition: codex up at death");
            _hud.OnEvents(SimEvents.GameOver, new CinderSim());
            Assert.That(_hud.CodexOpenForTest, Is.False,
                "the run ended, so the codex must retire with it");
            Assert.That(ActiveTargetNames(targets), Is.Empty,
                "the death panel is up and these combat targets are still on screen: "
                + $"{string.Join(", ", ActiveTargetNames(targets))}. The field alone does not "
                + "protect this path — GameOver hides the controls and THEN retires the left "
                + "stack, whose closers set the field back to true — so the "
                + "`!terminalModalVisible` guard in SyncTouchModeSurfaces is what holds it. "
                + "That guard had no test");

            // And the release, which is a real transition rather than a restated
            // state: the next wave takes the panel down and the controls return.
            _hud.OnEvents(SimEvents.WaveStarted, new CinderSim());
            Assert.That(_hud.TouchCombatControlsVisibleForTest, Is.True,
                "a restart must give the combat controls back, or the run after a death is "
                + "unplayable on a phone");
            Assert.That(ActiveTargetNames(targets), Is.Not.Empty,
                "the death panel came down and no combat target came back with it");
        }

        /// <summary>
        /// Opens one run-holding surface, asserts the combat controls go down,
        /// releases it, and asserts they come back.
        ///
        /// Used only for the three surfaces SyncTouchModeSurfaces does NOT
        /// re-hide for, so the field and the screen must agree and both are
        /// asserted here. The death panel is deliberately not driven through this
        /// helper — its field legitimately disagrees with its screen, and it is
        /// asserted on its effect alone at the call site.
        /// </summary>
        private void AssertHoldsDownTheControls(List<RectTransform> targets, string what,
                                                System.Action open, System.Action release)
        {
            Assert.That(ActiveTargetNames(targets), Is.Not.Empty,
                $"before opening the {what}, the combat controls are already down — the "
                + "previous surface did not release them and this case proves nothing");

            open();
            Assert.That(_hud.TouchCombatControlsVisibleForTest, Is.False,
                $"the {what} is holding the run with the combat controls still marked "
                + "visible. Nothing re-hides for this surface, so the field IS the screen");
            Assert.That(ActiveTargetNames(targets), Is.Empty,
                $"the {what} is holding the run and these combat targets are still on screen: "
                + $"{string.Join(", ", ActiveTargetNames(targets))} — taps landing on a sim "
                + "that cannot tick are worse than no controls at all");

            release();
            Assert.That(_hud.TouchCombatControlsVisibleForTest, Is.True,
                $"closing the {what} did not release the combat controls — one visit to it "
                + "leaves the rest of the run unplayable on a phone");
            Assert.That(ActiveTargetNames(targets), Is.Not.Empty,
                $"closing the {what} left every combat target off screen");
        }

        /// <summary>Which combat touch targets are on screen right now. Named,
        /// not counted — "2 of 3 active" does not say which thumb has nothing
        /// under it.</summary>
        private static List<string> ActiveTargetNames(List<RectTransform> targets)
        {
            var live = new List<string>();
            foreach (var target in targets)
                if (target != null && target.gameObject.activeInHierarchy) live.Add(target.name);
            return live;
        }

        /// <summary>Row identity for a failure message: title, save bit, group.
        /// A bare index says nothing about which lesson moved.</summary>
        private string CodexRowLabel(int row)
        {
            var bit = _hud.CodexGuidanceBitsForTest[row];
            var group = _hud.CodexGuidanceRowGroupForTest[row];
            return $"\"{GuidanceCatalog.Entries[bit].Title}\" (bit {bit}, "
                + $"{GuidanceCatalog.GroupTitle(GuidanceCatalog.GroupOrder[group])})";
        }

        /// <summary>Indices of the rows currently on screen.</summary>
        private static List<int> LiveRowIndices(IReadOnlyList<Text> bodies)
        {
            var live = new List<int>();
            for (var i = 0; i < bodies.Count; i++)
                if (bodies[i].gameObject.activeInHierarchy) live.Add(i);
            return live;
        }

        /// <summary>
        /// How a chip signals open-vs-folded, as a comparable value.
        ///
        /// The signal is the PLATE SPRITE, not the tint. An earlier version of
        /// this helper read `image.color` alone and failed a working panel: the
        /// implementation swaps ui-button-active for ui-button and leaves colour
        /// at white, because tinting a dark navy plate multiplies it and the two
        /// states came out indistinguishable in the browser — the same wall
        /// LobbyView hit and solved the same way (LobbyView.cs:1539-1553).
        ///
        /// So the check is on the pair the player actually sees. Sprite carries
        /// it when the art is present; the tint fallback carries it when the art
        /// is missing, and that fallback is real code that also has to work.
        /// Asserting only one of the two would pass a panel that signals nothing.
        /// </summary>
        private static string ChipStateSignal(GameObject chip)
        {
            var image = chip.GetComponent<Image>();
            Assert.That(image, Is.Not.Null, "a chip must have a plate to carry its state");
            var sprite = image.sprite == null ? "<none>" : image.sprite.name;
            return $"{sprite}@{image.color.grayscale * image.color.a:F3}";
        }

        // ===================================================== helpers =====

        /// <summary>Invariant-culture-free format, matching the interpolation
        /// in GuidanceCatalog exactly — the copy uses the default culture, so
        /// the expectation must too.</summary>
        private static string F(float value, string format) => value.ToString(format);

        private void BuildHud()
        {
            _hudObject = new GameObject("GuidanceTests");
            _hud = _hudObject.AddComponent<HudView>();
            _hud.Build();
        }

        // ------------------------------------------------ codex helpers -----

        /// <summary>Which operator a breakdown row printed between its terms.</summary>
        private enum BreakdownShape { NoFactors, Multiplicative, Additive }

        /// <summary>
        /// A number as `Breakdown` prints it, in whatever culture the run uses.
        /// The view formats with the default culture ("0.#"), so a comma-decimal
        /// machine renders "9,8" and a dot-decimal one "9.8" — accept both, then
        /// parse with the same culture that produced it.
        /// </summary>
        private static readonly Regex BreakdownNumber = new Regex(@"-?[0-9]+(?:[.,][0-9]+)?");

        private static float ParsePrinted(string token, string what)
        {
            Assert.That(
                float.TryParse(token, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.CurrentCulture, out var value),
                Is.True, $"{what}: \"{token}\" is not a number this culture can read");
            return value;
        }

        /// <summary>The four breakdown rows as rendered, for frame comparison.</summary>
        private string[] CodexRowStrings()
        {
            var rows = new string[8];
            for (var i = 0; i < 4; i++)
            {
                rows[i * 2] = _hud.CodexStatValueForTest(i);
                rows[i * 2 + 1] = _hud.CodexStatBreakdownForTest(i);
            }
            return rows;
        }

        /// <summary>
        /// Rebuilds a row's value from the factors it PRINTED, using the shape
        /// the row itself printed — `×` folds as a product of (1 + amount/100),
        /// `+` as a plain sum. Nothing here reads a sim constant: the whole
        /// point is to check what is on screen, not to recompute the formula and
        /// agree with it.
        ///
        /// Split on the SPACED separator, never the bare character. Every factor
        /// carries a second `+` inside its own amount — `레벨2(+4%)` — and
        /// splitting on `'+'` tears that apart, which is a bug this helper
        /// shipped with once already.
        /// </summary>
        private static float RecomposeBreakdown(string printed, out int factorCount,
                                                out BreakdownShape shape)
        {
            Assert.That(printed, Is.Not.Null.And.Not.Empty, "the row rendered nothing");

            var multiplicative = printed.Contains(" × ");
            var additive = printed.Contains(" + ");
            Assert.That(multiplicative && additive, Is.False,
                $"\"{printed}\" mixes × and + between its terms — a row is one shape or "
                + "the other, and a mixed row cannot be recomposed by any rule");

            var terms = SplitBreakdown(printed, multiplicative);
            Assert.That(terms[0], Does.Not.Contain("("),
                $"\"{printed}\": the base term must be a bare number, not a labelled factor");

            var value = ParsePrinted(BreakdownNumber.Match(terms[0]).Value, $"base of \"{printed}\"");
            factorCount = 0;
            for (var i = 1; i < terms.Length; i++)
            {
                var term = terms[i];
                var match = BreakdownNumber.Match(term);
                Assert.That(match.Success, Is.True,
                    $"factor \"{term}\" in \"{printed}\" carries no amount — a labelled "
                    + "factor with no number tells the player nothing");
                // The amount is the LAST number in the term: the label itself
                // carries one ("레벨2", "성장1"), and that is not the coefficient.
                var amountToken = match.Value;
                foreach (Match m in BreakdownNumber.Matches(term)) amountToken = m.Value;
                var amount = ParsePrinted(amountToken, $"factor \"{term}\"");
                var percent = term.Contains("%");
                Assert.That(percent, Is.EqualTo(multiplicative),
                    $"factor \"{term}\" in \"{printed}\": a × factor must be a percentage and "
                    + "a + factor a flat amount — mismatching the two makes the row unreadable");
                value = multiplicative ? value * (1f + amount / 100f) : value + amount;
                factorCount++;
            }

            shape = factorCount == 0
                ? BreakdownShape.NoFactors
                : multiplicative ? BreakdownShape.Multiplicative : BreakdownShape.Additive;
            return value;
        }

        /// <summary>Base term first, then one string per printed factor.</summary>
        private static string[] SplitBreakdown(string printed, bool multiplicative)
            => printed.Split(new[] { multiplicative ? " × " : " + " },
                             System.StringSplitOptions.None);

        /// <summary>
        /// The same row folded as a PRODUCT regardless of what it printed, with
        /// the amounts read as the flat numbers an additive row prints. Used only
        /// to prove the two shapes disagree on the fixture, so the per-row shape
        /// assertion is testing something.
        /// </summary>
        private static float RecomposeAsProduct(string printed)
        {
            var terms = SplitBreakdown(printed, printed.Contains(" × "));
            var value = ParsePrinted(BreakdownNumber.Match(terms[0]).Value, "base");
            for (var i = 1; i < terms.Length; i++)
            {
                var amountToken = (string)null;
                foreach (Match m in BreakdownNumber.Matches(terms[i])) amountToken = m.Value;
                if (amountToken == null) continue;
                value *= 1f + ParsePrinted(amountToken, "factor");
            }
            return value;
        }

        private static void AssertEveryDerivedStatDiffers(IDerivedStatSnapshot a,
                                                          IDerivedStatSnapshot b)
        {
            Assert.That(a.PlayerDamage, Is.Not.EqualTo(b.PlayerDamage).Within(1e-3f));
            Assert.That(a.PlayerMaxHealth, Is.Not.EqualTo(b.PlayerMaxHealth).Within(1e-3f));
            Assert.That(a.PlayerSpeed, Is.Not.EqualTo(b.PlayerSpeed).Within(1e-3f));
            Assert.That(a.LanternRegenPerSecond,
                Is.Not.EqualTo(b.LanternRegenPerSecond).Within(1e-3f),
                "the two fixtures must differ on all four rows, or a leak from the second "
                + "sim into the frozen frame would be invisible");
        }

        /// <summary>
        /// A real dungeon run driven past level 1 with a swiftness point banked,
        /// so all four codex rows carry a live factor: damage/health/regen get
        /// the level term and speed gets the growth term.
        ///
        /// The meta/equip loadout is chosen so every BASE value is exactly
        /// representable in the "0.#" the row prints — 58x1.3 = 75.4 damage,
        /// 100+80+40 = 220 health, 218 speed, 7 regen. A base that printed
        /// lossily (weapon rank 3 gives 68.44 -> "68.4") would put ~0.05 of
        /// rounding into the recomposition and make a 1e-3 comparison a
        /// statement about the format rather than about the factors.
        /// </summary>
        private static CinderSim LevelledDungeonRun()
        {
            Assert.That(HackConfig.TryDungeon(
                    CampaignStages.CinderSpan,
                    MetaStats.Of(attack: 10, vitality: 10, swiftness: 0),
                    EquipTiers.Of(weapon: 0, lantern: 0, cloak: 5),
                    null, 0, out var config),
                Is.True, "cinder-span must resolve");

            var sim = new CinderSim(in config);
            var growth = (IGrowthChoiceSnapshot)sim;

            // Walk at the nearest live enemy swinging, and spend every offer on
            // swiftness — the one axis that gives the speed row a factor.
            for (var tick = 0; tick < 60 * 180; tick++)
            {
                var input = new SimInput { AttackQueued = true };
                if (growth.GrowthOfferOpen) input.GrowthChoice = (int)GrowthChoiceKind.Swiftness;

                var best = float.MaxValue;
                float towardX = 0f, towardY = 0f;
                for (var i = 0; i < sim.Enemies.Count; i++)
                {
                    var enemy = sim.Enemies[i];
                    if (enemy.Dead) continue;
                    var dx = enemy.X - sim.Player.X;
                    var dy = enemy.Y - sim.Player.Y;
                    var squared = dx * dx + dy * dy * SimConfig.IsoY * SimConfig.IsoY;
                    if (squared >= best) continue;
                    best = squared;
                    var length = Mathf.Max(1f, Mathf.Sqrt(squared));
                    towardX = dx / length;
                    towardY = dy / length;
                }
                input.MoveX = towardX;
                input.MoveY = towardY;

                sim.Tick(in input);
                if (sim.Level > 1 && growth.GrowthSwiftness > 0) break;
                if (sim.Mode == SimMode.GameOver) break;
            }

            Assert.That(sim.Level, Is.GreaterThan(1),
                $"the fixture pilot must level at least once (reached {sim.Level}), or the "
                + "damage/health/regen rows carry no level factor");
            Assert.That(growth.GrowthSwiftness, Is.GreaterThan(0),
                "the fixture pilot must bank a swiftness point, or the speed row carries "
                + "no factor and its shape is untested");
            return sim;
        }

        /// <summary>
        /// Opens the codex in one input mode with every entry seen, asserts each
        /// row equals `BodyFor(touch)`, and reports the rendered bodies so the
        /// caller can prove the two modes actually differ somewhere.
        /// </summary>
        private void AssertSeenBodiesMatchInputMode(bool touch, out List<string> rendered)
        {
            BuildHud();
            if (touch)
            {
                _hud.ForceTouchControlsForTest();
                Assert.That(_hud.TouchActive, Is.True,
                    "the touch surfaces must build, or this run is a second desktop run "
                    + "wearing a touch label");
            }
            else
            {
                Assert.That(_hud.TouchActive, Is.False, "batchmode must start on desktop copy");
            }

            _hud.CodexEntrySeen = _ => true;
            _hud.OpenCodexForTest();
            _hud.ShowCodexTabForTest(guidance: true);

            var bits = _hud.CodexGuidanceBitsForTest;
            var bodies = _hud.CodexGuidanceBodiesForTest;
            Assert.That(bits.Count, Is.EqualTo(GuidanceCatalog.Count), "all rows must build");

            rendered = new List<string>(bits.Count);
            for (var row = 0; row < bits.Count; row++)
            {
                var entry = GuidanceCatalog.Entries[bits[row]];
                var text = bodies[row].text;
                rendered.Add(text);
                Assert.That(text, Is.EqualTo(entry.BodyFor(touch)),
                    $"seen \"{entry.Title}\" in {(touch ? "touch" : "desktop")} mode rendered "
                    + $"\"{text}\" — a seen entry shows its full body for the ACTIVE input "
                    + "mode, and naming a key the player has no way to press is worse than "
                    + "saying nothing");
            }
        }

        /// <summary>The HUD canvas in WorldSpace at a known size, so world
        /// corners read as plain canvas units (Screen.* is degenerate in
        /// batchmode — the seam HudLayoutTests and LobbyLayoutTests share).</summary>
        private Canvas WorldSpaceCanvas()
        {
            var canvas = _hudObject.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null, "the HUD must build its canvas");
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = (RectTransform)canvas.transform;
            rect.sizeDelta = new Vector2(EffectiveWidth, EffectiveHeight);
            rect.localScale = Vector3.one;
            rect.position = Vector3.zero;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            Assert.That(rect.rect.width, Is.EqualTo(EffectiveWidth).Within(0.01f),
                "canvas failed to take the effective phone size");
            return canvas;
        }

        /// <summary>
        /// Every rect a panel puts on screen: its own direct-child labels plus
        /// its buttons taken at the BUTTON rect, never the label stretched
        /// inside it. Reports the longest non-title label so callers can assert
        /// the panel actually said something.
        /// </summary>
        private static List<RectTransform> PanelSurfaces(Transform panel, out string longestLabel)
        {
            var rects = new List<RectTransform>();
            longestLabel = null;
            foreach (Transform child in panel)
            {
                if (!child.gameObject.activeInHierarchy) continue;
                var rect = child as RectTransform;
                if (rect == null) continue;
                if (child.GetComponent<Button>() != null) { rects.Add(rect); continue; }
                var text = child.GetComponent<Text>();
                if (text == null) continue;
                rects.Add(rect);
                if (longestLabel == null || text.text.Length > longestLabel.Length)
                    longestLabel = text.text;
            }
            return rects;
        }

        private static RectTransform FindActivePanelSized(Canvas canvas, float width, float height)
        {
            foreach (var image in canvas.GetComponentsInChildren<Image>(false))
            {
                var rect = image.rectTransform;
                if (Mathf.Abs(rect.rect.width - width) > 0.5f) continue;
                if (Mathf.Abs(rect.rect.height - height) > 0.5f) continue;
                return rect;
            }
            return null;
        }

        private static void AssertNoPairwiseOverlap(List<RectTransform> rects, string what)
        {
            var violations = new List<string>();
            for (var i = 0; i < rects.Count; i++)
            {
                var a = WorldRect(rects[i]);
                Assert.That(a.width > 0f && a.height > 0f, Is.True,
                    $"degenerate rect (layout did not resolve): {Path(rects[i])}");
                for (var j = i + 1; j < rects.Count; j++)
                {
                    var b = WorldRect(rects[j]);
                    var overlapX = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
                    var overlapY = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
                    if (overlapX > OverlapEpsilon && overlapY > OverlapEpsilon)
                        violations.Add($"  {Describe(rects[i], a)} stacks on "
                            + $"{Describe(rects[j], b)} by {overlapX:F1} x {overlapY:F1} u");
                }
            }
            Assert.That(violations, Is.Empty,
                $"{what}: a surface is drawn underneath another. This is the D2 class — "
                + "the death panel's way out sat 26 u inside the death-cause text, "
                + "clickable and invisible:\n" + string.Join("\n", violations));
        }

        private static void AssertContainedBy(List<RectTransform> rects, RectTransform body, string what)
        {
            var outer = WorldRect(body);
            var escaped = new List<string>();
            foreach (var rect in rects)
            {
                if (rect == body) continue;
                var world = WorldRect(rect);
                var overflow = Mathf.Max(
                    Mathf.Max(outer.xMin - world.xMin, world.xMax - outer.xMax),
                    Mathf.Max(outer.yMin - world.yMin, world.yMax - outer.yMax));
                if (overflow > OverlapEpsilon)
                    escaped.Add($"  {Describe(rect, world)} spills {overflow:F1} u past "
                        + $"[{outer.xMin:F0}..{outer.xMax:F0} x {outer.yMin:F0}..{outer.yMax:F0}]");
            }
            Assert.That(escaped, Is.Empty,
                $"surfaces drawn outside {what} — text with no plate behind it, floating "
                + "on the combat scene (the growth plate shipped exactly this, 16 u out):\n"
                + string.Join("\n", escaped));
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static string Describe(RectTransform rect, Rect world)
        {
            var text = rect.GetComponent<Text>();
            var label = text != null
                ? text.text
                : LabelOf(rect.GetComponent<Button>()) ?? rect.name;
            return $"\"{label}\" [{world.xMin:F0}..{world.xMax:F0} x "
                + $"{world.yMin:F0}..{world.yMax:F0}]";
        }

        private static string LabelOf(Button button)
        {
            if (button == null) return null;
            var text = button.GetComponentInChildren<Text>(true);
            return text == null ? null : text.text;
        }

        private static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
            return sb.ToString();
        }

        /// <summary>An ACTIVE button whose label is exactly this — exact, because
        /// "포기" is a prefix of "포기하고 나간다" and the two do opposite things.</summary>
        private static Button FindButtonLabelled(GameObject root, string label)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                if (string.Equals(LabelOf(button), label, System.StringComparison.Ordinal))
                    return button;
            return null;
        }

        private static Text FindTextContaining(GameObject root, string fragment)
        {
            foreach (var text in root.GetComponentsInChildren<Text>(true))
                if (text.text != null && text.text.Contains(fragment)) return text;
            return null;
        }
    }
}
