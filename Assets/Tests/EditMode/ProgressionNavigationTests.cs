// AMENDMENT #8 — progression navigation (design/progression-navigation-spec.md,
// qa/test-plan.md §v1.7 T-A1..T-A8).
//
// ProgressionGuide is pure and total over CampaignData, so the interesting
// checks are EXHAUSTIVE rather than exemplary. Two sweeps carry most of this
// file:
//
//   · 1024 SAVES  = PrologueDone(2) x ClearedMask(2^9). A corrupted save can
//     present an unreachable mask (bit 5 set with bit 4 clear), so restricting
//     the sweep to reachable states would leave exactly the inputs that crash
//     a lobby untested.
//   · 1024 RECORDS = TrialTiers(5 trials x 2 bits) with every stage cleared —
//     the completionist tail where the guide either falls back to a trial or
//     legitimately runs out of things to point at.
//
// Expected values are recomputed from the CATALOG, never by re-walking
// NextTarget's own loop: the target is asserted to be the MINIMUM of the
// candidate set {i : !cleared && IsUnlocked}, which is the contract the spec
// states, not the implementation's iteration order. Copying the loop would
// produce a test that agrees with any bug the loop contains.
//
// What this file deliberately does NOT test is recorded at the bottom
// (§REJECTED) — a test that cannot fail is worse than no test, because it
// reads as coverage.
using System;
using System.Collections.Generic;
using System.Text;
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class ProgressionNavigationTests
    {
        private const string CampaignKey = "abyssal-lantern:unity:campaign";

        // Same phone viewport LobbyLayoutTests audits (mobile-layout spec):
        // 390x844 CSS, portrait match 0.35 -> ~799 u wide, 0.488 CSS px/u.
        private const float EffectiveWidth = 799f;
        private const float EffectiveHeight = 1729f;
        private const float OverlapEpsilon = 1f;

        // The charcoal plate every lobby label sits on (LobbyView.PanelColor rgb).
        private static readonly Color Charcoal = new Color(5f / 255f, 4f / 255f, 9f / 255f);
        // WCAG 2.2 AA for body text.
        private const float ContrastAA = 4.5f;
        // Alpha a locked card's CanvasGroup applies (LobbyView Refresh).
        private const float LockedAlpha = 0.45f;

        private GameObject _lobbyObject;
        private LobbyView _lobby;
        private bool _hadCampaign;
        private string _campaignPayload;

        [SetUp]
        public void SetUp()
        {
            _hadCampaign = PlayerPrefs.HasKey(CampaignKey);
            _campaignPayload = PlayerPrefs.GetString(CampaignKey);

            _lobbyObject = new GameObject("ProgressionNavigationTests");
            _lobby = _lobbyObject.AddComponent<LobbyView>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_lobbyObject);
            var eventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem != null) UnityEngine.Object.DestroyImmediate(eventSystem.gameObject);
            if (_hadCampaign) PlayerPrefs.SetString(CampaignKey, _campaignPayload);
            else PlayerPrefs.DeleteKey(CampaignKey);
            PlayerPrefs.Save();
        }

        // ================================================================ T-A1 ==

        /// <summary>
        /// Every one of the 1024 save states resolves to exactly one target, and
        /// that target is the one the spec names — not the one the loop happens
        /// to find. The candidate SET is built with StageCatalog.IsUnlocked and
        /// the expectation is its MINIMUM, so a reordered catalog, an off-by-one
        /// in the scan, or a stale unlock rule all show up here.
        ///
        /// The all-done state is a legitimate terminal, not a missing target:
        /// Kind == None is asserted to hold if and only if both candidate sets
        /// are empty. "Exactly one slot" is a claim about every OTHER state.
        /// </summary>
        [Test]
        public void NextTarget_OverEveryReachableAndCorruptSave_PointsAtOneThing()
        {
            var stageCount = StageCatalog.Entries.Count;
            var prologueTargets = 0;
            var stageTargets = 0;
            var trialTargets = 0;

            for (var prologue = 0; prologue < 2; prologue++)
            for (var mask = 0; mask < (1 << 9); mask++)
            {
                var data = Save(prologue == 1, mask);
                var target = ProgressionGuide.NextTarget(in data);
                var where = $"PrologueDone={prologue == 1}, ClearedMask=0x{mask:X3}";

                // The group is always a real accordion group — the lobby indexes
                // an array with it (LobbyView.SelectGroup), so an out-of-range
                // value is an IndexOutOfRange on the player's screen.
                var group = ProgressionGuide.GroupOfTarget(in target);
                Assert.That(group, Is.InRange(0, ProgressionGuide.GroupCount - 1),
                    $"group out of the accordion for {target.Kind}/{target.Index} ({where})");

                if (prologue == 0)
                {
                    prologueTargets++;
                    Assert.That(target.Kind, Is.EqualTo(GuideTargetKind.Prologue),
                        $"an unfinished prologue gates everything ({where})");
                    Assert.That(target.Index, Is.EqualTo(-1),
                        $"the prologue is not a catalog row ({where})");
                    continue;
                }

                // Candidate set, built from the catalog's own unlock rule.
                var candidate = -1;
                for (var i = 0; i < stageCount; i++)
                {
                    var entry = StageCatalog.Entries[i];
                    if (StageCatalog.IsCleared(in data, in entry)) continue;
                    if (!StageCatalog.IsUnlocked(in data, in entry)) continue;
                    if (candidate < 0 || i < candidate) candidate = i;
                }

                if (candidate >= 0)
                {
                    stageTargets++;
                    Assert.That(target.Kind, Is.EqualTo(GuideTargetKind.Stage),
                        $"an unlocked uncleared stage exists at {candidate} ({where})");
                    Assert.That(target.Index, Is.EqualTo(candidate),
                        $"the guide must take the LOWEST open stage ({where})");
                    Assert.That(ProgressionGuide.GroupOfTarget(in target),
                        Is.EqualTo(ProgressionGuide.ActOf(candidate)),
                        $"a stage target opens its own act ({where})");
                    continue;
                }

                // No stage left. TrialTiers is 0 across this sweep, so every
                // trial is below the top tier and the fallback must fire.
                trialTargets++;
                Assert.That(target.Kind, Is.EqualTo(GuideTargetKind.Trial),
                    $"with no stage left the guide falls back to the trials ({where})");
                Assert.That(target.Index, Is.EqualTo(0),
                    $"the lowest unmastered trial is index 0 on a blank record ({where})");
                Assert.That(ProgressionGuide.GroupOfTarget(in target),
                    Is.EqualTo(ProgressionGuide.TrainingGroup),
                    $"a trial target opens the training group ({where})");
            }

            // Guard against a sweep that silently stopped exercising a branch:
            // all three kinds must actually have been produced.
            Assert.That(prologueTargets, Is.EqualTo(512), "half the sweep is prologue-locked");
            Assert.That(stageTargets, Is.GreaterThan(0), "the stage branch must be exercised");
            Assert.That(trialTargets, Is.EqualTo(1),
                "exactly one mask (0x1FF) clears every stage");
        }

        // ================================================================ T-A2 ==

        /// <summary>
        /// LockReasonFor names a branch of StageCatalog.IsUnlocked; it must never
        /// invent a third answer or disagree about WHETHER the stage is locked.
        /// 1024 saves x 9 stages.
        /// </summary>
        [Test]
        public void LockReason_AgreesWithIsUnlocked_OnEverySaveAndStage()
        {
            var byReason = new Dictionary<LockReason, int>
            {
                { LockReason.None, 0 },
                { LockReason.PrologueIncomplete, 0 },
                { LockReason.PrerequisiteUncleared, 0 },
            };

            for (var prologue = 0; prologue < 2; prologue++)
            for (var mask = 0; mask < (1 << 9); mask++)
            {
                var data = Save(prologue == 1, mask);
                for (var i = 0; i < StageCatalog.Entries.Count; i++)
                {
                    var entry = StageCatalog.Entries[i];
                    var unlocked = StageCatalog.IsUnlocked(in data, in entry);
                    var reason = ProgressionGuide.LockReasonFor(in data, in entry);
                    var where = $"{entry.Id} @ PrologueDone={prologue == 1}, mask=0x{mask:X3}";

                    Assert.That(reason == LockReason.None, Is.EqualTo(unlocked),
                        $"lock reason and unlock rule disagree for {where}");

                    // The two causes are not interchangeable: an incomplete
                    // prologue must never be reported as a missing prerequisite,
                    // because the player would go hunting for a stage to clear.
                    if (!unlocked)
                    {
                        var expected = !data.PrologueDone
                            ? LockReason.PrologueIncomplete
                            : LockReason.PrerequisiteUncleared;
                        Assert.That(reason, Is.EqualTo(expected), $"wrong cause named for {where}");
                    }

                    byReason[reason] += 1;
                }
            }

            foreach (var pair in byReason)
                Assert.That(pair.Value, Is.GreaterThan(0),
                    $"{pair.Key} never occurred — the sweep stopped covering a branch");
        }

        /// <summary>
        /// The sub-line is the only place a locked card explains itself, and N8
        /// keeps the epithet leading so a locked card still previews its gimmick.
        /// Three things are load-bearing and all three are swept: it is never
        /// empty, it always starts with the epithet, and when the cause is a
        /// missing prerequisite it names that stage in Korean — a player cannot
        /// act on "선행 필요" without the name.
        /// </summary>
        [Test]
        public void StageSubLine_LeadsWithTheEpithet_AndNamesThePrerequisite()
        {
            const string Reward = "REWARD-PROBE";
            var namedPrerequisite = 0;
            var advertisedReward = 0;
            var clearedBare = 0;

            for (var prologue = 0; prologue < 2; prologue++)
            for (var mask = 0; mask < (1 << 9); mask++)
            {
                var data = Save(prologue == 1, mask);
                for (var i = 0; i < StageCatalog.Entries.Count; i++)
                {
                    var entry = StageCatalog.Entries[i];
                    var line = ProgressionGuide.StageSubLine(in data, in entry, Reward);
                    var where = $"{entry.Id} @ PrologueDone={prologue == 1}, mask=0x{mask:X3}";

                    Assert.That(line, Is.Not.Null.And.Not.Empty, $"empty sub-line for {where}");
                    Assert.That(line.StartsWith(entry.Epithet, StringComparison.Ordinal), Is.True,
                        $"the epithet must still lead the line (N8) for {where}: '{line}'");

                    switch (ProgressionGuide.LockReasonFor(in data, in entry))
                    {
                        case LockReason.PrerequisiteUncleared:
                        {
                            var title = ProgressionGuide.PrerequisiteTitle(in entry);
                            Assert.That(title, Is.Not.Empty,
                                $"a prerequisite-locked stage must resolve its prerequisite: {where}");
                            Assert.That(line, Does.Contain(title),
                                $"the reason must name the blocking stage for {where}: '{line}'");
                            namedPrerequisite++;
                            break;
                        }
                        case LockReason.None when !StageCatalog.IsCleared(in data, in entry):
                            Assert.That(line, Does.Contain(Reward),
                                $"an open uncleared card keeps advertising its reward: {where}");
                            advertisedReward++;
                            break;
                        case LockReason.None:
                            Assert.That(line, Is.EqualTo(entry.Epithet),
                                $"a cleared card drops the redeemed reward tail: {where}");
                            clearedBare++;
                            break;
                    }
                }
            }

            Assert.That(namedPrerequisite, Is.GreaterThan(0));
            Assert.That(advertisedReward, Is.GreaterThan(0));
            Assert.That(clearedBare, Is.GreaterThan(0));
        }

        /// <summary>
        /// Two causes must read as two causes. If both collapsed onto one string
        /// the enum would still be right and the screen would still be wrong —
        /// which is exactly the "sim is correct, view is not" failure cycle-3
        /// shipped seven times.
        /// </summary>
        [Test]
        public void StageSubLine_SaysSomethingDifferentForEachCause()
        {
            var entry = StageCatalog.Entries[1];   // has a prerequisite

            var prologueLocked = Save(false, 0);
            var prerequisiteLocked = Save(true, 0);

            Assert.That(ProgressionGuide.LockReasonFor(in prologueLocked, in entry),
                Is.EqualTo(LockReason.PrologueIncomplete));
            Assert.That(ProgressionGuide.LockReasonFor(in prerequisiteLocked, in entry),
                Is.EqualTo(LockReason.PrerequisiteUncleared));

            var a = ProgressionGuide.StageSubLine(in prologueLocked, in entry, "R");
            var b = ProgressionGuide.StageSubLine(in prerequisiteLocked, in entry, "R");
            Assert.That(a, Is.Not.EqualTo(b),
                $"both lock causes produced the same sentence: '{a}'");
        }

        // ============================================================ T-A3/A4 ==

        /// <summary>
        /// The four tabs against their purchase conditions at every boundary the
        /// plan enumerates: equipment 5 tiers x (cost-1, cost, cost+1) = 15,
        /// sigils 3, growth 3, legion 3.
        ///
        /// Each currency is isolated so one tab's rule cannot be satisfied by
        /// another's state: the equipment rows own every sigil (so the sigil tab
        /// cannot compete for relics), and the sigil rows sit at the equipment
        /// cap (so no equipment step exists to outbid them).
        /// </summary>
        [Test]
        public void Badges_MatchEachTabsPurchaseCondition_AtEveryBoundary()
        {
            var cap = ProgressionGuide.EquipCap;
            var everySigil = 0;
            foreach (var kind in ProgressionGuide.SigilOrder) everySigil |= 1 << (int)kind;

            var cases = new List<(string Name, CampaignData Data, bool Growth, bool Equip, bool Legion, bool Sigil)>();

            // --- equipment: 5 tiers x 3 boundary points, sigils all owned ------
            for (var tier = 0; tier < cap; tier++)
            {
                var cost = ProgressionGuide.EquipCosts[tier];
                foreach (var relics in new[] { cost - 1, cost, cost + 1 })
                {
                    var data = Save(true, 0);
                    data.Weapon = tier;
                    data.Lantern = cap;      // capped: contributes no step
                    data.Cloak = cap;
                    data.Relics = Math.Max(relics, 0);
                    data.SigilsOwned = everySigil;
                    cases.Add(($"equip T{tier} cost {cost} relics {data.Relics}",
                        data, false, data.Relics >= cost, false, false));
                }
            }

            // --- sigils: 3 boundary points, equipment capped -------------------
            foreach (var relics in new[] { ProgressionGuide.SigilCost - 1,
                                           ProgressionGuide.SigilCost,
                                           ProgressionGuide.SigilCost + 1 })
            {
                var data = Save(true, 0);
                data.Weapon = data.Lantern = data.Cloak = cap;
                data.Relics = relics;
                data.SigilsOwned = 0;
                cases.Add(($"sigil relics {relics}", data,
                    false, false, false, relics >= ProgressionGuide.SigilCost));
            }

            // --- growth: points boundary + the stat ceiling ---------------------
            var noPoints = Save(true, 0);
            noPoints.Weapon = noPoints.Lantern = noPoints.Cloak = cap;
            noPoints.SigilsOwned = everySigil;
            cases.Add(("growth points 0", noPoints, false, false, false, false));

            var onePoint = noPoints;
            onePoint.Points = 1;
            cases.Add(("growth points 1, stats open", onePoint, true, false, false, false));

            var capped = onePoint;
            capped.Attack = capped.Vitality = capped.Swiftness = ProgressionGuide.StatCap;
            cases.Add(("growth points 1, every stat capped", capped, false, false, false, false));

            // --- legion: roster boundary ----------------------------------------
            var emptyRoster = noPoints;
            emptyRoster.Roster = new string[0];
            emptyRoster.Active = string.Empty;
            cases.Add(("legion empty roster", emptyRoster, false, false, false, false));

            var onlyActive = noPoints;
            onlyActive.Roster = new[] { "ember-cohort" };
            onlyActive.Active = "ember-cohort";
            cases.Add(("legion active only", onlyActive, false, false, false, false));

            var benched = noPoints;
            benched.Roster = new[] { "ember-cohort", "shade-echo" };
            benched.Active = "ember-cohort";
            cases.Add(("legion one benched", benched, false, false, true, false));

            Assert.That(cases.Count, Is.EqualTo(24),
                "the plan enumerates 24 boundary cases (15 equip + 3 sigil + 3 growth + 3 legion)");

            foreach (var c in cases)
            {
                var data = c.Data;
                var badges = ProgressionGuide.Badges(in data);
                Assert.That(badges.Growth, Is.EqualTo(c.Growth), $"growth badge @ {c.Name}");
                Assert.That(badges.Equip, Is.EqualTo(c.Equip), $"equip badge @ {c.Name}");
                Assert.That(badges.Legion, Is.EqualTo(c.Legion), $"legion badge @ {c.Name}");
                Assert.That(badges.Sigil, Is.EqualTo(c.Sigil), $"sigil badge @ {c.Name}");
            }
        }

        /// <summary>
        /// The relic rule, swept over Relics 0..40 x every equipment tier triple
        /// x every sigil-ownership mask (283 392 states).
        ///
        /// Three claims, none of which mentions a price literal — they are
        /// written entirely in terms of the tables read at runtime:
        ///
        ///   1. the two relic tabs NEVER light together (negotiation entry 10);
        ///   2. a lit tab always has something buyable behind it (no false
        ///      pointer — the failure mode that makes players stop looking);
        ///   3. the lit tab is the CHEAPEST affordable relic item, so the
        ///      misdirect |pointed - cheapest| is 0 against a band of 2. The
        ///      band is evaluated only where the domain is non-empty.
        /// </summary>
        [Test]
        public void Badges_NeverLightBothRelicTabs_AndAlwaysPointAtTheCheapest()
        {
            const int MisdirectBand = 2;
            var cap = ProgressionGuide.EquipCap;
            var everyOwned = (1 << ProgressionGuide.SigilOrder.Length) - 1;
            var blank = Save(true, 0);
            var tiers = new int[3];
            var worstMisdirect = -1;
            var bothLit = 0;
            var domainStates = 0;
            var equipLit = 0;
            var sigilLit = 0;
            for (var relics = 0; relics <= 40; relics++)
            for (var w = 0; w <= cap; w++)
            for (var l = 0; l <= cap; l++)
            for (var c = 0; c <= cap; c++)
            for (var owned = 0; owned < (1 << 5); owned++)
            {
                var data = blank;
                data.Relics = relics;
                data.Weapon = w;
                data.Lantern = l;
                data.Cloak = c;
                data.SigilsOwned = SigilMask(owned);

                var badges = ProgressionGuide.Badges(in data);
                if (badges.Equip && badges.Sigil) bothLit++;

                // Independent affordability, from the price tables only.
                var cheapestEquip = -1;
                tiers[0] = w; tiers[1] = l; tiers[2] = c;
                for (var slot = 0; slot < tiers.Length; slot++)
                {
                    if (tiers[slot] >= cap) continue;
                    var cost = ProgressionGuide.EquipCosts[tiers[slot]];
                    if (relics < cost) continue;
                    if (cheapestEquip < 0 || cost < cheapestEquip) cheapestEquip = cost;
                }
                var sigilAffordable = relics >= ProgressionGuide.SigilCost && owned != everyOwned;

                // Failure strings are built ON FAILURE only: this body runs
                // 283 392 times and an eager $"..." here costs more than the
                // whole rest of the fixture.
                if (ProgressionGuide.CheapestEquipCost(in data) != cheapestEquip)
                    Assert.Fail($"cheapest equipment step disagrees @ {Where(relics, w, l, c, owned)}: "
                        + $"guide {ProgressionGuide.CheapestEquipCost(in data)}, table {cheapestEquip}");
                if (ProgressionGuide.CanBuyAnySigil(in data) != sigilAffordable)
                    Assert.Fail($"sigil affordability disagrees @ {Where(relics, w, l, c, owned)}");

                // 2. no false pointer.
                if (badges.Equip)
                {
                    equipLit++;
                    if (cheapestEquip < 0)
                        Assert.Fail($"equip badge lit with nothing buyable @ {Where(relics, w, l, c, owned)}");
                }
                if (badges.Sigil)
                {
                    sigilLit++;
                    if (!sigilAffordable)
                        Assert.Fail($"sigil badge lit with nothing buyable @ {Where(relics, w, l, c, owned)}");
                }

                if (cheapestEquip < 0 && !sigilAffordable)
                {
                    if (badges.Equip || badges.Sigil)
                        Assert.Fail($"a relic tab lit with an empty relic wallet @ {Where(relics, w, l, c, owned)}");
                    continue;   // misdirect is undefined outside its domain
                }

                domainStates++;
                if (!badges.Equip && !badges.Sigil)
                    Assert.Fail($"something is affordable but no relic tab lit @ {Where(relics, w, l, c, owned)}");

                var cheapest = cheapestEquip >= 0 && sigilAffordable
                    ? Math.Min(cheapestEquip, ProgressionGuide.SigilCost)
                    : cheapestEquip >= 0 ? cheapestEquip : ProgressionGuide.SigilCost;
                var pointed = badges.Equip ? cheapestEquip : ProgressionGuide.SigilCost;

                var misdirect = pointed - cheapest;
                if (misdirect < 0)
                    Assert.Fail($"the badge undercut the cheapest item — impossible @ "
                        + $"{Where(relics, w, l, c, owned)}: pointed {pointed}, cheapest {cheapest}");
                if (misdirect > MisdirectBand)
                    Assert.Fail($"badge_misdirect_relics band breached @ {Where(relics, w, l, c, owned)}: "
                        + $"pointed {pointed}, cheapest {cheapest}, band {MisdirectBand}");
                if (misdirect > worstMisdirect) worstMisdirect = misdirect;
            }

            TestContext.WriteLine($"[badge sweep] domain states {domainStates}, "
                + $"equip lit {equipLit}, sigil lit {sigilLit}, worst misdirect {worstMisdirect}");
            Assert.That(bothLit, Is.Zero, "the two relic tabs must never light together");
            Assert.That(equipLit, Is.GreaterThan(0), "the equip branch must be exercised");
            Assert.That(sigilLit, Is.GreaterThan(0),
                "the sigil branch must be reachable — a rule that never lights sigils "
                + "would pass every other assertion here");
        }

        /// <summary>
        /// The state the plan calls out by name: T0/T0/T0 with 12 relics. Before
        /// the rule landed this pointed at the 12-relic sigil while a 2-relic
        /// equipment step was affordable — misdirect 10 against a band of 2.
        /// Pinned as a regression because it is the single case negotiation
        /// entry 10 was written about.
        /// </summary>
        [Test]
        public void Badges_RegressionOnTheStateThatMeasuredTenBeforeTheRule()
        {
            var data = Save(true, 0);
            data.Relics = ProgressionGuide.SigilCost;   // 12
            data.Weapon = data.Lantern = data.Cloak = 0;
            data.SigilsOwned = 0;

            var badges = ProgressionGuide.Badges(in data);
            var cheapestEquip = ProgressionGuide.CheapestEquipCost(in data);

            Assert.That(ProgressionGuide.CanBuyAnySigil(in data), Is.True,
                "the sigil IS affordable here — that is what made the old rule pick it");
            Assert.That(cheapestEquip, Is.EqualTo(ProgressionGuide.EquipCosts[0]),
                "the T0 step is the cheapest thing on the counter");
            Assert.That(badges.Equip, Is.True, "the cheaper relic item takes the badge");
            Assert.That(badges.Sigil, Is.False, "the dearer relic item stands down");

            // The reverse state proves the rule is a comparison and not a
            // permanent demotion of the sigil tab.
            var maxed = data;
            maxed.Weapon = maxed.Lantern = maxed.Cloak = ProgressionGuide.EquipCap;
            var reverse = ProgressionGuide.Badges(in maxed);
            Assert.That(reverse.Sigil, Is.True,
                "with no equipment step left the sigil tab must light");
            Assert.That(reverse.Equip, Is.False);
        }

        // ================================================================ T-A5 ==

        /// <summary>
        /// The completionist tail: every stage cleared, swept over all 1024
        /// trial records. Expectations are decoded from the RAW save encoding
        /// (2 bits per trial, value = best tier + 1) rather than from
        /// CampaignStore.BestTier, so a change to either side surfaces here.
        /// </summary>
        [Test]
        public void Trials_FallbackAndMasteryCount_OverEveryTrialRecord()
        {
            var trials = TrainingTrials.Ids.Length;
            var top = HackSpec.TrainingTiers - 1;
            var allCleared = StageCatalog.Entries.Count;
            var terminalStates = 0;
            var fallbackStates = 0;

            for (var record = 0; record < (1 << (2 * 5)); record++)
            {
                var data = Save(true, (1 << allCleared) - 1);
                data.TrialTiers = record;

                // Raw decode: field == top + 1 means "cleared at the top tier".
                var mastered = 0;
                var lowestUnmastered = -1;
                for (var i = 0; i < trials; i++)
                {
                    var field = (record >> (i * 2)) & 0x3;
                    if (field - 1 >= top) mastered++;
                    else if (lowestUnmastered < 0) lowestUnmastered = i;

                    Assert.That(CampaignStore.BestTier(in data, i), Is.EqualTo(field - 1),
                        $"trial {i} decode drifted @ record 0x{record:X3}");
                }

                var where = $"record 0x{record:X3}";
                Assert.That(ProgressionGuide.MasteredTrials(in data), Is.EqualTo(mastered),
                    $"mastered count disagrees with the save encoding @ {where}");
                Assert.That(CampaignStore.MasteryComplete(in data),
                    Is.EqualTo(lowestUnmastered < 0), $"mastery completion @ {where}");

                var target = ProgressionGuide.NextTarget(in data);
                if (lowestUnmastered < 0)
                {
                    terminalStates++;
                    Assert.That(target.Kind, Is.EqualTo(GuideTargetKind.None),
                        $"nothing is left to point at @ {where}");
                    Assert.That(target.Index, Is.EqualTo(-1),
                        $"a target with no subject carries no index @ {where}");
                }
                else
                {
                    fallbackStates++;
                    // mastery_pointer_coverage: an unclaimed mastery path must be
                    // pointed at, or negotiation entry 7's clause is unreachable.
                    Assert.That(data.TrainingMasteryClaimed, Is.False);
                    Assert.That(target.Kind, Is.EqualTo(GuideTargetKind.Trial),
                        $"a completionist short of mastery must be pointed at a trial @ {where}");
                    Assert.That(target.Index, Is.EqualTo(lowestUnmastered),
                        $"the lowest unmastered trial is the one to point at @ {where}");
                    Assert.That(ProgressionGuide.GroupOfTarget(in target),
                        Is.EqualTo(ProgressionGuide.TrainingGroup));
                }
            }

            Assert.That(terminalStates, Is.EqualTo(1),
                "exactly one record masters every trial");
            Assert.That(fallbackStates, Is.EqualTo(1023));
            Assert.That(ProgressionGuide.ClearedTotal(Save(true, (1 << allCleared) - 1)),
                Is.EqualTo(allCleared));
        }

        /// <summary>Per-act counters against the mask, so the header gauges
        /// cannot drift from the bits they summarise.</summary>
        [Test]
        public void ClearedCounters_SumToTheMask_OverEverySave()
        {
            for (var mask = 0; mask < (1 << 9); mask++)
            {
                var data = Save(true, mask);
                var perAct = 0;
                for (var act = 0; act < ProgressionGuide.ActCount; act++)
                {
                    var inAct = ProgressionGuide.ClearedInAct(in data, act);
                    Assert.That(inAct, Is.InRange(0, ProgressionGuide.StagesPerAct),
                        $"act {act} counted {inAct} of {ProgressionGuide.StagesPerAct} @ 0x{mask:X3}");
                    perAct += inAct;
                }
                Assert.That(perAct, Is.EqualTo(ProgressionGuide.ClearedTotal(in data)),
                    $"the per-act gauges must partition the 정화 n/9 gauge @ 0x{mask:X3}");
                Assert.That(ProgressionGuide.ClearedTotal(in data),
                    Is.EqualTo(CountBits(mask)), $"total disagrees with the mask @ 0x{mask:X3}");
            }
        }

        [TestCase(false, TestName = "OpenMetaScreen_RoutesToSanctumEquipment")]
        [TestCase(true, TestName = "MapMaintenanceButton_RoutesToSanctumEquipment")]
        public void MaintenanceRoute_UsesTheOwnedSanctumEquipmentSurface(bool clickButton)
        {
            var canvas = BuildLobby(Save(true, 0));
            _lobby.SelectRail(LobbyView.RailMap);

            if (clickButton)
            {
                var matches = new List<Button>();
                foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                {
                    var label = button.GetComponentInChildren<Text>(true);
                    if (label != null && label.text == "정비") matches.Add(button);
                }
                Assert.That(matches, Has.Count.EqualTo(1),
                    "the map must expose exactly one maintenance action");
                Assert.That(matches[0].gameObject.activeInHierarchy, Is.True,
                    "the maintenance action must be reachable from the open Map rail");
                matches[0].onClick.Invoke();
            }
            else
            {
                _lobby.OpenMetaScreen();
            }

            Assert.That(_lobby.SelectedRailForTest, Is.EqualTo(LobbyView.RailSanctum),
                "maintenance must navigate to the Sanctum owner");

            var meta = _lobbyObject.GetComponent<MetaScreenView>();
            Assert.That(meta, Is.Not.Null, "the lobby must still own its Map/Controls overlay");
            Assert.That(meta.IsOpen, Is.False,
                "maintenance must not open the Map/Controls overlay");

            var activeEquipmentHints = 0;
            foreach (var text in canvas.GetComponentsInChildren<Text>(true))
            {
                if (text.text == "유물로 장비를 강화한다 (T0-T5)"
                    && text.gameObject.activeInHierarchy)
                    activeEquipmentHints++;
            }
            Assert.That(activeEquipmentHints, Is.EqualTo(1),
                "the Sanctum equipment tab must be the one visible purchase surface");
        }

        // ================================================================ T-A6 ==

        /// <summary>
        /// The lock reason has to be READABLE, not merely present. It sits on a
        /// card the lobby dims to alpha 0.45, and the plan's palette sweep found
        /// no colour that survives that dimming — so the label was lifted out of
        /// the attenuation with its own CanvasGroup.
        ///
        /// This test would be worthless if it only checked the composited number
        /// it expects to pass, so it proves the mechanism is load-bearing in both
        /// directions: the card really is dimmed to 0.45, and the SAME colour
        /// composited through that 0.45 falls under AA. Delete
        /// ignoreParentGroups and the arithmetic below stops being hypothetical.
        /// </summary>
        [Test]
        public void LockReasonLabel_StaysAboveWcagAa_BecauseItEscapesTheDimming()
        {
            // Prologue done, nothing cleared: stage 0 is open, stages 1..8 are
            // prerequisite-locked, and act I is the group the guide opens.
            var canvas = BuildLobby(Save(true, 0));

            var lifted = new List<Text>();
            var dimmedCards = 0;
            foreach (var text in canvas.GetComponentsInChildren<Text>(true))
            {
                var own = text.GetComponent<CanvasGroup>();
                if (own == null || !own.ignoreParentGroups) continue;

                var card = AncestorCanvasGroup(text.transform.parent);
                Assert.That(card, Is.Not.Null,
                    $"the lifted label must still live inside a card group: {Path(text.transform)}");
                if (!Mathf.Approximately(card.alpha, LockedAlpha)) continue;

                dimmedCards++;
                Assert.That(own.alpha, Is.EqualTo(1f).Within(0.0001f),
                    $"a lifted reason label must stay at full alpha: {Path(text.transform)}");
                lifted.Add(text);
            }

            Assert.That(dimmedCards, Is.GreaterThan(0),
                "no locked card was dimmed to 0.45 — this save was supposed to produce "
                + "eight of them, so the test is measuring the wrong state");

            var report = new StringBuilder();
            foreach (var text in lifted)
            {
                var direct = Contrast(text.color, Charcoal);
                var composited = Contrast(Composite(text.color, Charcoal, LockedAlpha), Charcoal);
                report.AppendLine($"  {direct,5:F2}:1 lifted / {composited,5:F2}:1 if dimmed   "
                    + $"rgb({text.color.r:F2},{text.color.g:F2},{text.color.b:F2})  '{text.text}'");

                Assert.That(direct, Is.GreaterThanOrEqualTo(ContrastAA),
                    $"lock reason text fails WCAG AA on charcoal: {direct:F2}:1 — "
                    + $"'{text.text}'");

                // The negative half: without the escape hatch this same label is
                // unreadable. If this ever stops holding, the CanvasGroup is no
                // longer buying anything and the assertion above became vacuous.
                Assert.That(composited, Is.LessThan(ContrastAA),
                    $"the 0.45 dimming no longer breaks this colour ({composited:F2}:1) — "
                    + "the ignoreParentGroups escape is no longer load-bearing, so "
                    + "this test has stopped defending anything. Re-derive it.");
            }

            TestContext.WriteLine($"[lock reason contrast on charcoal {Charcoal.r * 255f:F0},"
                + $"{Charcoal.g * 255f:F0},{Charcoal.b * 255f:F0}, AA floor {ContrastAA}]\n" + report);
        }

        // ================================================================ T-A7 ==

        /// <summary>
        /// The four fold headers are new geometry dropped into a panel that was
        /// already full. Cycle-3 shipped "label covers button" twice because the
        /// tests read values and never compared rectangles.
        ///
        /// SORTIE-state only, on purpose. The sweep compares each header against
        /// every ACTIVE button, and the rail makes exactly one panel active — so
        /// walking the other two rail states would compare headers belonging to a
        /// deactivated sortie panel against the sanctum's controls. Those two
        /// panels are pinned to the SAME coordinate (LobbyView.PinPanel), so that
        /// comparison reports a full-panel collision between two things that are
        /// never drawn together. The radio is what makes the question well-posed;
        /// asking it across states would un-pose it.
        ///
        /// Cycle-8 also removed a false-positive source rather than adding one:
        /// the sanctum tab strip used to sit directly beneath the sortie panel in
        /// the stacked column, and headers scrolled past the viewport edge
        /// "collided" with it four times per fold state until the clip below was
        /// added. Those controls are now in a different rail state entirely.
        /// </summary>
        [Test]
        public void AccordionHeaders_CoverNoExistingControl_InAnyFoldState()
        {
            var canvas = BuildLobby(Save(true, 1));
            // Premise, stated not inherited: the headers only exist on screen
            // while 출정 is the live destination.
            _lobby.SelectRail(LobbyView.RailSortie);
            Rebuild(canvas.GetComponent<RectTransform>());

            var collisions = new StringBuilder();
            var pairsChecked = 0;

            for (var open = 0; open < ProgressionGuide.GroupCount; open++)
            {
                OpenGroup(canvas, open);

                var headers = new List<RectTransform>();
                var headerObjects = new HashSet<GameObject>();
                for (var g = 0; g < ProgressionGuide.GroupCount; g++)
                {
                    var header = GroupChild(canvas, g, "Panel");
                    var body = GroupChild(canvas, g, "Body");
                    var headerRect = WorldRect(header);
                    var bodyRect = WorldRect(body);

                    Assert.That(headerRect.width > 0f && headerRect.height > 0f, Is.True,
                        $"header {g} did not resolve a rect (fold state {open})");
                    Assert.That(bodyRect.width > 0f && bodyRect.height > 0f, Is.True,
                        $"body {g} did not resolve a rect (fold state {open})");

                    headers.Add(header);
                    headerObjects.Add(header.gameObject);
                }

                // Clipped to the scroll viewport before comparing. StageScroll
                // carries a RectMask2D, so a header scrolled past the viewport
                // edge is not drawn and cannot cover anything — comparing its
                // raw world rect reports collisions with whatever the panel
                // below happens to hold. After main's stacked phone layout put
                // the Sanctum tab strip directly under the sortie panel, that
                // false positive was four hits per fold state against controls
                // the header is masked away from.
                //
                // The clip is the assertion's subject, not a tolerance: what the
                // player can see is exactly the intersection of the header and
                // the viewport.
                var viewport = WorldRect(ViewportOf(canvas));
                foreach (var header in headers)
                {
                    var headerRect = Intersect(WorldRect(header), viewport);
                    if (headerRect.width <= 0f || headerRect.height <= 0f) continue;

                    foreach (var button in canvas.GetComponentsInChildren<Button>(true))
                    {
                        if (!button.gameObject.activeInHierarchy) continue;
                        if (headerObjects.Contains(button.gameObject)) continue;

                        pairsChecked++;
                        var other = WorldRect(button.GetComponent<RectTransform>());
                        var area = OverlapArea(headerRect, other);
                        if (area <= OverlapEpsilon) continue;
                        collisions.AppendLine($"  fold {open}: {Path(header)} {Describe(headerRect)} "
                            + $"x {Path(button.transform)} {Describe(other)} = {area:F1} u2");
                    }
                }
            }

            Assert.That(pairsChecked, Is.GreaterThan(0), "the audit compared nothing");
            Assert.That(collisions.Length, Is.Zero,
                $"fold headers stack on existing controls:\n{collisions}");
        }

        /// <summary>
        /// An accordion that can show two open groups is not an accordion, and
        /// one that forgets to reflow leaves the group the player just opened
        /// underneath the one above it. Both are checked in all four fold states.
        ///
        /// "Open" is read as activeInHierarchy, which since cycle-8 also answers
        /// "is the sortie panel the live rail destination". Two different
        /// questions through one property: with the rail on 성소 or 지도 every
        /// body reports inactive and the exactly-one assertion fails with a count
        /// of 0, describing a broken accordion when the accordion is fine. The
        /// SelectRail below pins the outer state so the count means what the
        /// message says it means.
        /// </summary>
        [Test]
        public void Accordion_KeepsOneGroupOpen_AndReflowsEveryGroupBelow()
        {
            var canvas = BuildLobby(Save(true, 1));
            _lobby.SelectRail(LobbyView.RailSortie);
            Rebuild(canvas.GetComponent<RectTransform>());

            var origins = new float[ProgressionGuide.GroupCount][];

            for (var open = 0; open < ProgressionGuide.GroupCount; open++)
            {
                OpenGroup(canvas, open);

                var active = 0;
                origins[open] = new float[ProgressionGuide.GroupCount];
                for (var g = 0; g < ProgressionGuide.GroupCount; g++)
                {
                    var body = GroupChild(canvas, g, "Body");
                    if (body.gameObject.activeInHierarchy) active++;
                    origins[open][g] = GroupRect(canvas, g).anchoredPosition.y;
                }

                Assert.That(active, Is.EqualTo(1),
                    $"exactly one body may be open; fold state {open} showed {active}");
                Assert.That(GroupChild(canvas, open, "Body").gameObject.activeInHierarchy, Is.True,
                    $"the tapped group {open} must be the open one");

                // Stacking order: each group sits strictly below the previous one
                // and never overlaps it. A stale y-origin shows up here.
                for (var g = 1; g < ProgressionGuide.GroupCount; g++)
                {
                    var above = WorldRect(GroupRect(canvas, g - 1));
                    var below = WorldRect(GroupRect(canvas, g));
                    Assert.That(below.yMax, Is.LessThanOrEqualTo(above.yMin + OverlapEpsilon),
                        $"group {g} {Describe(below)} overlaps group {g - 1} {Describe(above)} "
                        + $"in fold state {open}");
                }
            }

            // Opening the training group collapses all three acts above it, so
            // every group below the first must actually have moved. If SelectGroup
            // stopped repositioning, these would be equal.
            for (var g = 1; g < ProgressionGuide.GroupCount; g++)
                Assert.That(origins[3][g], Is.Not.EqualTo(origins[0][g]).Within(0.001f),
                    $"group {g} did not move when the open group changed from 0 to 3 "
                    + $"(both {origins[0][g]:F1} u) — the fold did not reflow");
        }

        /// <summary>
        /// The accordion's content arithmetic, at the ONLY card pitch the product
        /// can now produce.
        ///
        /// READ FIRST, BEFORE THE DIFF MISLEADS YOU. This test's numbers moved
        /// 416/626 -> 542/878 in cycle-8 and the rail did not cause that. Nothing
        /// about the content height changed this cycle. What changed is that the
        /// codebase stopped CONTAINING a second answer:
        ///
        ///   · The 70 u card pitch was reachable only through the desktop arm of
        ///     a tier branch. The shipped WebGL template pins E_w at ~1176
        ///     (build-webgl/index.html:18-20), and the old SideBySideFloor was
        ///     1248. 1176 < 1248, always, at every window size, for every player.
        ///     So the stacked arm was taken 100% of the time and the stacked arm
        ///     called ApplySortieTouchLayout(true) — pitch 112.
        ///   · Cycle-8 pinned that call unconditionally (LobbyView.cs:1056) and
        ///     deleted the branch. It removed an arm nothing could select.
        ///
        /// "Cycle-8 changed the content height" is the wrong lesson and it is the
        /// one the diff suggests. The right one: 416 u was never on anyone's
        /// screen, and a test asked for it by name for two cycles.
        ///
        /// AND THE ACCORDION'S JUSTIFICATION WAS NEVER VALIDATED.
        ///
        /// Do not read this test as evidence that folding was checked out. Spec
        /// §2.2 argued for the accordion on one number: an open act is 416 u and
        /// therefore FITS THE VIEWPORT WHOLE, 100% visible, which is what made
        /// folding better than the scroll it replaced. That claim was measured at
        /// `desktop: true` — a configuration no player has ever loaded. In the
        /// build that actually shipped, an open act has always been 542 u against
        /// a 422 u viewport: 77.9%, overflowing, from the day the accordion
        /// landed. The feature may well be worth having. This test has never
        /// shown that, and the version that appeared to was measuring a frame the
        /// product does not draw.
        ///
        /// Third failure mode of this cycle, and the cleanest instance of it: a
        /// pass obtained by picking the most flattering scale available. The other
        /// two were a 16-width sweep with no y samples, and a test named
        /// "AreReachable" that measured only size (both in LobbyLayoutTests).
        ///
        /// So the old test is split in two, because one half survived and one did
        /// not:
        ///
        ///   · The ARITHMETIC still holds and is still worth pinning. It just
        ///     evaluates at pitch 112 instead of pitch 70:
        ///       act      = ContentTop 6 + (HeaderPitch 48 + 3 x CardPitch)
        ///                              + 3 x HeaderPitch 48 + 4 x GroupGap 2
        ///       training = same shape with 6 rows in the open group
        ///     At pitch 70 that is 416 / 626 — the spec §2.2 numbers, reproduced
        ///     EXACTLY. That reproduction is the evidence the formula is right and
        ///     only its input moved; without it, 542 would just be a number
        ///     someone typed to make a red test green. At pitch 112: 542 / 878.
        ///
        ///   · The PRODUCT CLAIM is inverted, not re-pointed. A green test must
        ///     not carry a false justification along with corrected numbers —
        ///     that is how 416 survived two cycles in the first place. The
        ///     assertion below states the CURRENT truth (an act overflows) and
        ///     says what to do when it flips back.
        /// </summary>
        [Test]
        public void Accordion_ContentHeightMatchesTheSpecArithmetic()
        {
            var canvas = BuildLobby(Save(true, 1));
            // Premise: every measurement below lives in the SORTIE panel, and
            // the lobby opens CLOSED (D-12), so this call is what puts it on
            // screen rather than a tidy-up of an inherited default. Without it
            // the four rects below resolve on a deactivated panel and the
            // failure reads as broken arithmetic instead of a closed lobby.
            _lobby.SelectRail(LobbyView.RailSortie);
            Rebuild(canvas.GetComponent<RectTransform>());

            var viewport = WorldRect(FindDescendant(canvas.transform, "StageScroll"));

            OpenGroup(canvas, 0);
            var act = ContentHeight(canvas);
            OpenGroup(canvas, ProgressionGuide.TrainingGroup);
            var training = ContentHeight(canvas);

            TestContext.WriteLine($"[accordion content @ shipped 112 u pitch] act group "
                + $"{act:F1} u, training group {training:F1} u, viewport {viewport.height:F1} u "
                + $"({Mathf.Min(1f, viewport.height / act) * 100f:F1}% / "
                + $"{Mathf.Min(1f, viewport.height / training) * 100f:F1}% visible). "
                + "Spec §2.2's 416/626 are the same formula at the 70 u pitch, which the "
                + "shipped template could never select — those figures were never on screen.");

            Assert.That(act, Is.EqualTo(542f).Within(0.01f),
                "an open act is ContentTop 6 + (48 + 3x112) + 3x48 + 4x2 = 542 u at the "
                + "shipped pitch. The same formula at the 70 u pitch gives spec §2.2's 416 — "
                + "which is how you know the formula is right and only its input changed, NOT "
                + "that 416 was ever drawn. If this number moves, say WHICH input moved: a "
                + "changed CardPitch is a design decision, a changed formula is a layout bug. "
                + "Do not adjust it to match a measurement without naming one.");
            Assert.That(training, Is.EqualTo(878f).Within(0.01f),
                "the open training group is 6 rows: 6 + (48 + 6x112) + 3x48 + 4x2 = 878 u. "
                + "Spec §2.2's 626 is the same shape at the never-selected 70 u pitch.");

            // INVERTED CLAIM — see the docstring. This asserts the current truth,
            // which is the opposite of what the spec argued for the feature.
            Assert.That(act, Is.GreaterThan(viewport.height),
                $"an open act ({act:F1} u) now fits inside the viewport ({viewport.height:F1} u). "
                + "That is a FIX, and it is the FIRST time the accordion's justification has "
                + "actually held: spec §2.2 argued for folding on 'an act fits whole, 100% "
                + "visible', measured at a 70 u pitch the shipped template could not select. "
                + "In the build players ran it was 542 u against 422 u — 77.9% — from the day "
                + "the accordion landed. So restore the original assertion (act <= viewport), "
                + "delete this one, and record that the claim finally became true rather than "
                + "that it stopped being false. The ScrollRect also stops being load-bearing "
                + "for acts, which the next two assertions cover.");
            Assert.That(training, Is.GreaterThan(viewport.height),
                "the training group overflows too — if it stopped, the ScrollRect is dead weight");

            // What the overflow costs, asserted rather than left as a comment: the
            // ScrollRect is now the ONLY way to reach the bottom of ANY group, not
            // just the training one. Deleting it because "the accordion made
            // scrolling unnecessary" — which is what the spec's 100% figure would
            // lead a reader to believe — strands 22% of every act.
            var scroll = FindDescendant(canvas.transform, "StageScroll").GetComponent<ScrollRect>();
            Assert.That(scroll, Is.Not.Null,
                "StageScroll lost its ScrollRect. At the shipped 112 u pitch EVERY group "
                + "overflows the viewport, so the scroll is the only route to the last rows "
                + "of every act — not just the training group's overflow any more");
            Assert.That(scroll.vertical, Is.True,
                "the one axis that overflows is the one that must scroll");
        }

        // ================================================================ T-A8 ==

        /// <summary>
        /// ActOf is integer division by StagesPerAct. That is only correct while
        /// the catalog is exactly three acts of three authored in lineage order,
        /// and nothing else in the codebase enforces it — a tenth stage would
        /// silently put itself in act 3, which is the training group.
        /// </summary>
        [Test]
        public void Catalog_IsThreeActsOfThree_InWorldviewLineageOrder()
        {
            Assert.That(StageCatalog.Entries.Count,
                Is.EqualTo(ProgressionGuide.ActCount * ProgressionGuide.StagesPerAct),
                "ActOf divides by StagesPerAct — a catalog that is not ActCount x "
                + "StagesPerAct makes every act assignment wrong without any other "
                + "test noticing");
            Assert.That(ProgressionGuide.ActTitles.Length, Is.EqualTo(ProgressionGuide.ActCount));
            Assert.That(ProgressionGuide.TrainingGroup, Is.EqualTo(ProgressionGuide.ActCount));
            Assert.That(ProgressionGuide.GroupCount, Is.EqualTo(ProgressionGuide.ActCount + 1));

            // worldview.md §공간 계보.
            var lineage = new[]
            {
                new[] { "cinder-span", "ember-gallery", "abyss-chancel" },
                new[] { "witness-well", "echo-throne", "ash-verdict" },
                new[] { "cinder-sluice", "ember-bastion", "ash-march" },
            };

            for (var act = 0; act < ProgressionGuide.ActCount; act++)
            for (var slot = 0; slot < ProgressionGuide.StagesPerAct; slot++)
            {
                var index = act * ProgressionGuide.StagesPerAct + slot;
                var entry = StageCatalog.Entries[index];
                Assert.That(entry.Id, Is.EqualTo(lineage[act][slot]),
                    $"catalog index {index} left the worldview lineage");
                Assert.That(entry.CatalogIndex, Is.EqualTo(index),
                    "CatalogIndex is the clear bit — a mismatch corrupts every save");
                Assert.That(ProgressionGuide.ActOf(index), Is.EqualTo(act),
                    $"{entry.Id} lands in the wrong act");
            }
        }

        /// <summary>
        /// Every stage must resolve a hazard table, or the sigil-relevance line
        /// silently says "not live here" for a stage whose gimmicks it simply
        /// failed to look up. A typo in SimAnchorId produces exactly that.
        /// </summary>
        [Test]
        public void EffectiveHazards_ResolveForEveryStage_OverrideOrAnchor()
        {
            for (var i = 0; i < StageCatalog.Entries.Count; i++)
            {
                var entry = StageCatalog.Entries[i];
                var hazards = ProgressionGuide.EffectiveHazards(i);

                Assert.That(hazards, Is.Not.Null, $"{entry.Id} resolved no hazard table");
                Assert.That(hazards.Length, Is.GreaterThan(0), $"{entry.Id} resolved an empty table");

                if (entry.HazardOverride != null)
                {
                    Assert.That(hazards, Is.SameAs(entry.HazardOverride),
                        $"{entry.Id} has an override and must return it verbatim");
                }
                else
                {
                    Assert.That(CampaignStages.TryGet(entry.SimAnchorId, 0, 0, 0, out var anchor), Is.True,
                        $"{entry.Id} points at unknown sim anchor '{entry.SimAnchorId}'");
                    Assert.That(hazards, Is.SameAs(anchor.Hazards),
                        $"{entry.Id} has no override and must return its frozen anchor table");
                }
            }

            Assert.That(ProgressionGuide.EffectiveHazards(-1), Is.Null);
            Assert.That(ProgressionGuide.EffectiveHazards(StageCatalog.Entries.Count), Is.Null);
        }

        /// <summary>
        /// N11's claim is an ASYMMETRY: two sigils cost the same 12 relics and
        /// one of them is live in nine stages while the other is live in one.
        /// That number is the whole novelty argument, so it is counted here from
        /// the effective hazard tables rather than quoted.
        ///
        /// The pairing itself is not restated — SigilTests already proves each
        /// clause fires on its own gimmick. What is pinned is that the pairing
        /// is INJECTIVE and that every paired gimmick actually ships, because a
        /// sigil bound to a hazard no stage uses is a 12-relic purchase that can
        /// never do anything.
        /// </summary>
        [Test]
        public void SigilReach_IsTheAsymmetryTheNoveltyScorecardClaims()
        {
            var reach = new Dictionary<SigilKind, int>();
            var bound = new Dictionary<HazardKind, SigilKind>();
            var report = new StringBuilder();
            var probe = Save(true, 0);

            foreach (var kind in ProgressionGuide.SigilOrder)
            {
                var hazard = ProgressionGuide.HazardOf(kind);
                if (bound.TryGetValue(hazard, out var claimant))
                    Assert.Fail($"{kind} and {claimant} both claim {hazard} — two sigils "
                        + "sharing a gimmick makes one of them a duplicate purchase, and "
                        + "SigilLiveInTarget can no longer tell them apart");
                bound[hazard] = kind;

                var live = 0;
                for (var i = 0; i < StageCatalog.Entries.Count; i++)
                {
                    var target = new GuideTarget(GuideTargetKind.Stage, i);
                    var isLive = ProgressionGuide.SigilLiveInTarget(in probe, kind, in target);

                    // Independent count straight off the table.
                    var present = false;
                    foreach (var hazardConfig in ProgressionGuide.EffectiveHazards(i))
                        if (hazardConfig.Kind == hazard) { present = true; break; }

                    Assert.That(isLive, Is.EqualTo(present),
                        $"{kind} relevance disagrees with {StageCatalog.Entries[i].Id}'s table");
                    if (present) live++;
                }

                reach[kind] = live;
                report.AppendLine($"  {kind,-14} -> {hazard,-14} live in {live}/{StageCatalog.Entries.Count}");
            }

            TestContext.WriteLine("[sigil reach across the nine catalog stages]\n" + report);

            Assert.That(bound.Count, Is.EqualTo(ProgressionGuide.SigilOrder.Length),
                "the pairing must be injective");
            foreach (var pair in reach)
                Assert.That(pair.Value, Is.GreaterThan(0),
                    $"{pair.Key} binds a gimmick no stage ships — it can never be worth "
                    + $"{ProgressionGuide.SigilCost} relics");

            // The measured table (2026-08-07). PM quoted 점화인 9/9 and 집행인 1/9;
            // the other three are recorded so the asymmetry has a full denominator.
            Assert.That(reach[SigilKind.Ignition], Is.EqualTo(9), "점화인 / ember-vent");
            Assert.That(reach[SigilKind.Executioner], Is.EqualTo(1), "집행인 / ash-wall");
            Assert.That(reach[SigilKind.Verdict], Is.EqualTo(3), "판결인 / ember-pylon");
            Assert.That(reach[SigilKind.Countercurrent], Is.EqualTo(2), "역류인 / tide-current");
            Assert.That(reach[SigilKind.Witness], Is.EqualTo(4), "증언인 / relic-altar");

            // A non-stage target has no hazard table, so nothing is live there.
            var blank = Save(true, 0);
            foreach (var kind in ProgressionGuide.SigilOrder)
            {
                Assert.That(ProgressionGuide.SigilLiveInTarget(in blank, kind, GuideTarget.Nothing), Is.False);
                Assert.That(ProgressionGuide.SigilLiveInTarget(in blank, kind,
                    new GuideTarget(GuideTargetKind.Trial, 0)), Is.False,
                    "a trial is not a catalog stage — indexing the catalog with a trial "
                    + "index would report a neighbouring stage's gimmicks");
            }
        }

        /// <summary>
        /// SigilKind.None is the inert zero, not a sigil, and the two surfaces
        /// that can receive it reject it DIFFERENTLY on purpose:
        ///
        ///   · HazardOf throws — it is a total function on real sigils, and a
        ///     caller asking what None binds has a bug worth surfacing loudly.
        ///   · SigilLiveInTarget returns false — it runs on the Refresh path,
        ///     where an exception is a lobby that does not draw.
        ///
        /// This was a latent defect until 2026-08-07: HazardOf routed None
        /// through `default:` into RelicAltar, so the inert zero read as live in
        /// every altar stage. It was unreachable only because every caller
        /// happens to walk SigilOrder — a guarantee held by convention. The fix
        /// moved the guarantee into the type, and this test is what stops it
        /// sliding back: restore `default: return RelicAltar` and the first
        /// assertion goes green-to-red immediately.
        /// </summary>
        [Test]
        public void InertSigil_IsRejectedByBothSurfaces_NotSilentlyBoundToTheAltar()
        {
            Assert.That(() => ProgressionGuide.HazardOf(SigilKind.None),
                Throws.TypeOf<ArgumentOutOfRangeException>(),
                "SigilKind.None binds no gimmick. A `default:` arm here maps the "
                + "inert zero onto whichever hazard happens to sit last in the "
                + "switch, and the label silently claims that gimmick is live.");

            // The Refresh-path surface must stay total: no throw, no false claim.
            var data = Save(true, 0);
            for (var i = 0; i < StageCatalog.Entries.Count; i++)
            {
                var target = new GuideTarget(GuideTargetKind.Stage, i);
                Assert.That(ProgressionGuide.SigilLiveInTarget(in data, SigilKind.None, in target),
                    Is.False,
                    $"the inert sigil must never read as live — {StageCatalog.Entries[i].Id}");
            }

            // Guard against the fix being "made total" by swallowing the throw:
            // real sigils must still resolve, and at least one altar stage must
            // exist, or the assertion above passes for the wrong reason.
            var altarStages = 0;
            for (var i = 0; i < StageCatalog.Entries.Count; i++)
                foreach (var hazard in ProgressionGuide.EffectiveHazards(i))
                    if (hazard.Kind == HazardKind.RelicAltar) { altarStages++; break; }
            Assert.That(altarStages, Is.GreaterThan(0),
                "no stage ships an altar, so mapping None onto RelicAltar would be "
                + "harmless and this test proves nothing. Re-derive it against "
                + "whatever hazard the switch now falls through to.");
            Assert.That(ProgressionGuide.HazardOf(SigilKind.Witness),
                Is.EqualTo(HazardKind.RelicAltar),
                "Witness must still resolve as an explicit case, not by fallthrough");
        }

        // ============================================================== helpers ==

        private static CampaignData Save(bool prologueDone, int clearedMask) => new CampaignData
        {
            PrologueDone = prologueDone,
            ClearedMask = clearedMask,
            Roster = new string[0],
            Active = string.Empty,
        };

        /// <summary>Ownership bitmask over SigilOrder from a 5-bit selector.</summary>
        private static int SigilMask(int selector)
        {
            var mask = 0;
            for (var i = 0; i < ProgressionGuide.SigilOrder.Length; i++)
                if ((selector & (1 << i)) != 0) mask |= 1 << (int)ProgressionGuide.SigilOrder[i];
            return mask;
        }

        /// <summary>Failure-site description for the badge sweep. Called only
        /// on a failing branch — see the note in the sweep body.</summary>
        private static string Where(int relics, int w, int l, int c, int owned)
            => $"relics {relics}, tiers {w}/{l}/{c}, "
             + $"owned 0b{Convert.ToString(owned, 2).PadLeft(ProgressionGuide.SigilOrder.Length, '0')}";

        private static int CountBits(int value)
        {
            var bits = 0;
            while (value != 0) { bits += value & 1; value >>= 1; }
            return bits;
        }

        /// <summary>
        /// Builds the lobby AND runs the layout pass.
        ///
        /// Build leaves every panel at its CONSTRUCTION coordinate; the layout
        /// pass is what pins them under the rail. Skipping it audits a screen the
        /// game never draws, and once main's map/gear buttons arrived that showed
        /// up as fold headers "covering" two controls they are nowhere near at
        /// any real layout.
        ///
        /// 390x844 always. There USED to be a `desktop: true` arm, taken by one
        /// caller to reach the 70 u card pitch that the spec's 416/626 figures
        /// were computed from. Cycle-8 deleted the tier branch and pinned
        /// ApplySortieTouchLayout(true) unconditionally (LobbyView.cs:1056), so
        /// the pitch is 112 at EVERY viewport and the parameter selected nothing
        /// but a different effective width. Keeping it would have left one caller
        /// naming a mode the product cannot enter.
        ///
        /// Worth being precise about what died: the 70 u arm was already
        /// unreachable in the shipped build BEFORE this cycle. The WebGL template
        /// pins E_w at ~1176 (build-webgl/index.html:18-20), which was below the
        /// old SideBySideFloor of 1248, so every player got the stacked arm and
        /// the stacked arm called ApplySortieTouchLayout(true). `desktop: true`
        /// described an editor-only configuration.
        /// </summary>
        private Canvas BuildLobby(CampaignData data)
        {
            _lobby.Build(data, default);
            _lobby.Refresh(data);
            _lobby.ApplyLobbyLayoutForTest(390, 844);

            var canvas = _lobbyObject.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null, "the lobby must build its canvas");
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = canvas.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(EffectiveWidth, EffectiveHeight);
            rect.localScale = Vector3.one;
            rect.position = Vector3.zero;
            Rebuild(rect);
            return canvas;
        }

        private static void Rebuild(RectTransform root)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }

        /// <summary>Taps a fold header. Found by hierarchy position, not label —
        /// the header's text order is a presentation choice and this must not
        /// break when it changes.</summary>
        private static void OpenGroup(Canvas canvas, int group)
        {
            var header = GroupChild(canvas, group, "Panel").GetComponent<Button>();
            Assert.That(header, Is.Not.Null, $"group {group}'s header must be tappable");
            header.onClick.Invoke();
            Rebuild(canvas.GetComponent<RectTransform>());
        }

        private static RectTransform GroupRect(Canvas canvas, int group)
        {
            var rect = FindDescendant(canvas.transform, "Group" + group);
            Assert.That(rect, Is.Not.Null, $"the accordion must build Group{group}");
            return rect;
        }

        private static RectTransform GroupChild(Canvas canvas, int group, string child)
        {
            var found = FindDescendant(GroupRect(canvas, group), child);
            Assert.That(found, Is.Not.Null, $"Group{group} must build its {child}");
            return found;
        }

        private static float ContentHeight(Canvas canvas)
        {
            var content = FindDescendant(canvas.transform, "StageContent");
            Assert.That(content, Is.Not.Null, "the accordion must build StageContent");
            return content.sizeDelta.y;
        }

        private static RectTransform FindDescendant(Transform root, string name)
        {
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (string.Equals(child.name, name, StringComparison.Ordinal))
                    return child as RectTransform;
                var found = FindDescendant(child, name);
                if (found != null) return found;
            }
            return null;
        }
        /// <summary>The scroll viewport — the RectMask2D that decides which
        /// rows are on screen. Named lookup rather than a stored reference so
        /// the test breaks loudly if the object is renamed, instead of quietly
        /// clipping against nothing.</summary>
        private static RectTransform ViewportOf(Canvas canvas)
        {
            var viewport = FindDescendant(canvas.transform, "StageScroll");
            Assert.That(viewport, Is.Not.Null,
                "the sortie list must have a StageScroll viewport — without it there "
                + "is no mask, and every clipped-rect assertion below is comparing "
                + "geometry the player never sees");
            Assert.That(viewport.GetComponent<RectMask2D>(), Is.Not.Null,
                "StageScroll lost its RectMask2D: rows outside the viewport are now "
                + "DRAWN over whatever is beneath the panel, so the clipping this "
                + "test does is no longer a description of the screen");
            return viewport;
        }

        private static Rect Intersect(Rect a, Rect b)
        {
            var xMin = Mathf.Max(a.xMin, b.xMin);
            var yMin = Mathf.Max(a.yMin, b.yMin);
            var xMax = Mathf.Min(a.xMax, b.xMax);
            var yMax = Mathf.Min(a.yMax, b.yMax);
            return xMax <= xMin || yMax <= yMin
                ? new Rect(0f, 0f, 0f, 0f)
                : Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static CanvasGroup AncestorCanvasGroup(Transform from)
        {
            while (from != null)
            {
                var group = from.GetComponent<CanvasGroup>();
                if (group != null && !group.ignoreParentGroups) return group;
                from = from.parent;
            }
            return null;
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var min = new Vector2(corners[0].x, corners[0].y);
            var max = new Vector2(corners[2].x, corners[2].y);
            return new Rect(min, max - min);
        }

        private static float OverlapArea(Rect a, Rect b)
        {
            var x = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            var y = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return x <= 0f || y <= 0f ? 0f : x * y;
        }

        private static string Describe(Rect rect)
            => $"[{rect.xMin:F0}..{rect.xMax:F0} x {rect.yMin:F0}..{rect.yMax:F0}]";


        private static string Path(Transform transform)
        {
            var path = transform.name;
            var parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        // --- WCAG 2.2 relative luminance / contrast, computed not quoted -------

        private static float LinearChannel(float channel)
            => channel <= 0.03928f ? channel / 12.92f : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);

        private static float Luminance(Color color)
            => 0.2126f * LinearChannel(color.r)
             + 0.7152f * LinearChannel(color.g)
             + 0.0722f * LinearChannel(color.b);

        private static float Contrast(Color foreground, Color background)
        {
            var high = Luminance(foreground);
            var low = Luminance(background);
            if (high < low) { var swap = high; high = low; low = swap; }
            return (high + 0.05f) / (low + 0.05f);
        }

        /// <summary>Source-over composite of a CanvasGroup alpha onto the plate.</summary>
        private static Color Composite(Color foreground, Color background, float alpha)
            => new Color(
                foreground.r * alpha + background.r * (1f - alpha),
                foreground.g * alpha + background.g * (1f - alpha),
                foreground.b * alpha + background.b * (1f - alpha));

        // ============================================================ REJECTED ==
        //
        // Written down because "we did not test it" and "there is nothing to
        // test" are different claims and only one of them is true here.
        //
        // 1. "Prices are read at runtime, not inlined" (plan T-A4, last bullet).
        //    SigilCost is a compile-time const, so a rule that inlined today's
        //    12 and a rule that reads SigilCost are INDISTINGUISHABLE from a
        //    test — both branch identically for every input that exists. The
        //    checkable part of the claim is the SHAPE of the rule, and that is
        //    covered above by asserting "the lit tab is the cheapest affordable
        //    relic item" with no numeric literal in the assertion: a static tab
        //    priority or a hard-coded threshold breaks it, only re-inlining the
        //    exact current value survives. A test that pretended to cover the
        //    rest would pass unconditionally.
        //
        // 2. ActTitles / ActKickers string equality. Asserting
        //    ActTitles[0] == "제1부 기록" restates the declaration one file
        //    over; no bug flips one without flipping the other. What IS load
        //    bearing is that the titles array is ActCount long and that the
        //    catalog's act partition follows the lineage — both asserted above.
        //
        // 3. StageSubLine's "선행 정화 필요" branch (ProgressionGuide.cs:209).
        //    It fires only when a stage is PrerequisiteUncleared AND its
        //    PrereqId does not resolve. PrerequisiteUncleared requires
        //    PrologueDone, and the only entry without a resolvable prerequisite
        //    is index 0, which is unconditionally unlocked once the prologue is
        //    done. The branch is unreachable through the public surface; the
        //    1024x9 sweep above confirms it never fires. Asserting on it would
        //    pin behaviour no player can observe.
        //
        // 4. Touch-floor sizing of the fold headers. Owned by
        //    LobbyLayoutTests' frozen ratchet, which walks all four fold states.
        //    Duplicating it here would give two tables to update and one of them
        //    would rot.
    }
}
