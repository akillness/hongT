// §M pose selection. ActorView.ResolveActionValue is the ONE decision point
// between "play the clip the sim asked for" and "play a View-owned substate".
// It is pure and its input space is small, so it is pinned EXHAUSTIVELY here
// rather than sampled.
//
// Why the function exists: ActorAction is a frozen sim type. The sim emits one
// Attack per swing and has no cast action at all, so the View continues the
// animator's integer past the enum — 11/12/13 = attack2/attack3/cast — and
// resolves those from state it already owns (combo tier, cast window).
//
// What these tests defend (and what breaks without them):
//   * identity for every sim-emitted action — the View must forward what the
//     sim asserted; any stray rewrite plays a different action's clip;
//   * the combo ladder 5/11/12 — the tier is the ONLY thing separating hit 1
//     from hit 2 from the finisher, because the sim emits one Attack for all
//     three, and tier 0 must stay a plain swing;
//   * both branch guards. A widened combo branch hands Critical the attack2
//     clip. A widened cast branch masks a reaction the SIM ASSERTED, so a
//     staggered (Hit/BigHit), dodging, blocking or DYING actor would stand
//     there casting — that guard is the entire safety argument for the feature;
//   * priority — a combo swing outranks a live cast window;
//   * an unseeded tier is not a combo. ActorView._comboTier is initialised to
//     -1 and GameView calls SetComboTier only in dungeon mode, so -1 is the
//     live tier for every enemy actor and for every swing outside the dungeon.
//     Loosening `comboTier > 0` to `!= 0` would put the whole non-dungeon game
//     on the attack3 finisher clip;
//   * distinctness — two poses sharing one animator value silently collapse
//     into a single state, and the collision is invisible until you watch a rig;
//   * purity — this runs once per actor per frame, so hidden state would make
//     an actor's pose depend on which actor was resolved before it.
//
// ActorView.ResolveActionValue is `internal static` and Assets/Scripts/View/
// AssemblyInfo.cs declares InternalsVisibleTo("CinderCourt.Tests.EditMode"),
// which this asmdef also references — so the call below is a compile-time
// binding. No reflection (unlike ClipTableTests, whose target lives in the
// predefined Editor assembly).
using System;
using System.Collections.Generic;
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class PoseResolveTests
    {
        // ActorView's substate literals are private, so they are restated here.
        // A renumber must fail in this file as well as in ClipTableTests: these
        // are the values SetInteger("action", v) actually sends.
        const int Attack2Value = 11, Attack3Value = 12, CastValue = 13;

        // The documented ladder. 5 is (int)ActorAction.Attack — a swing at tier
        // 0 is a plain attack, not a substate.
        static readonly (int Tier, int Expected, string Pose)[] ComboLadder =
        {
            (0, 5, "the plain attack clip"),
            (1, Attack2Value, "the attack2 clip (combo hit 2)"),
            (2, Attack3Value, "the attack3 clip (combo finisher)"),
        };

        // The tiers that actually select a substate.
        static readonly int[] ComboTiers = { 1, 2 };

        // -1 is the unseeded/non-dungeon tier, 0..2 is the sim's documented
        // ComboIndex range, 3 is past it.
        static readonly int[] GridTiers = { -1, 0, 1, 2, 3 };

        [Test]
        public void EveryAction_ResolvesItsOwnValue_WithNoComboAndNoCastWindow()
        {
            foreach (ActorAction action in Enum.GetValues(typeof(ActorAction)))
            {
                var resolved = ActorView.ResolveActionValue(action, comboTier: 0, castPoseLive: false);

                Assert.That(resolved, Is.EqualTo((int)action),
                    $"ActorAction.{action} resolved {resolved}, not {(int)action} — with no " +
                    "combo and no cast window the View must forward the sim's action " +
                    $"untouched. The actor now plays the clip at row {resolved} every time " +
                    $"the sim asks for {action}");
            }
        }

        [Test]
        public void Attack_ResolvesItsComboSubstate_PerTier()
        {
            foreach (var (tier, expected, pose) in ComboLadder)
            {
                var resolved = ActorView.ResolveActionValue(ActorAction.Attack, tier, castPoseLive: false);

                Assert.That(resolved, Is.EqualTo(expected),
                    $"Attack at combo tier {tier} resolved {resolved}, not {expected} — the " +
                    "sim emits one Attack for all three hits, so the tier is the ONLY thing " +
                    $"that selects {pose}. Every swing in the combo now looks the same, or " +
                    "looks like the wrong hit");
            }
        }

        [Test]
        public void ComboSubstates_NeverLeakOntoNonAttackActions()
        {
            foreach (ActorAction action in Enum.GetValues(typeof(ActorAction)))
            {
                if (action == ActorAction.Attack) continue;

                foreach (var tier in ComboTiers)
                {
                    var resolved = ActorView.ResolveActionValue(action, tier, castPoseLive: false);

                    Assert.That(resolved, Is.EqualTo((int)action),
                        $"a live combo tier ({tier}) rewrote ActorAction.{action} to " +
                        $"{resolved}, expected {(int)action} — the combo substates belong to " +
                        "Attack alone. The tier stays set between swings, so this would put " +
                        $"{action} on an attack clip: Critical would play an ordinary combo " +
                        "swing, and a staggered or dying actor would swing instead");
                }
            }
        }

        [Test]
        public void LiveCastWindow_ReplacesIdleOnly()
        {
            foreach (ActorAction action in Enum.GetValues(typeof(ActorAction)))
            {
                var resolved = ActorView.ResolveActionValue(action, comboTier: 0, castPoseLive: true);

                if (action == ActorAction.Idle)
                {
                    Assert.That(resolved, Is.EqualTo(CastValue),
                        $"an idle body with a live cast window resolved {resolved}, not " +
                        $"{CastValue} — the sim has no cast action, so this substate is the " +
                        "only way a cast is ever shown. The spell fires with no pose at all");
                    continue;
                }

                Assert.That(resolved, Is.EqualTo((int)action),
                    $"a live cast window rewrote ActorAction.{action} to {resolved}, expected " +
                    $"{(int)action} — the cast pose is View-owned decoration and may speak " +
                    $"ONLY for an idle body: {CastMaskDanger(action)}");
            }
        }

        [Test]
        public void ComboSwing_OutranksALiveCastWindow()
        {
            foreach (var tier in ComboTiers)
            {
                var expected = tier == 1 ? Attack2Value : Attack3Value;
                var resolved = ActorView.ResolveActionValue(ActorAction.Attack, tier, castPoseLive: true);

                Assert.That(resolved, Is.EqualTo(expected),
                    $"Attack at combo tier {tier} resolved {resolved} while a cast window was " +
                    $"live, expected {expected} — a combo swing outranks the cast pose. The " +
                    "windows overlap constantly (a cast leaves its pose window open while the " +
                    "player keeps swinging), so losing this makes mid-combo hits drop their " +
                    "swing animation");

                Assert.That(resolved, Is.Not.EqualTo(CastValue),
                    $"Attack at combo tier {tier} resolved the cast pose ({CastValue}) — the " +
                    "actor stands casting through a swing the sim is already resolving hits for");
            }
        }

        [Test]
        public void UnseededComboTier_IsNotACombo()
        {
            // Not hypothetical: ActorView._comboTier starts at -1 and GameView
            // calls SetComboTier only in dungeon mode, so -1 is the live tier
            // for every enemy actor and for every swing outside the dungeon.
            foreach (ActorAction action in Enum.GetValues(typeof(ActorAction)))
            {
                var resolved = ActorView.ResolveActionValue(action, -1, castPoseLive: false);

                Assert.That(resolved, Is.EqualTo((int)action),
                    $"ActorAction.{action} at the unseeded tier -1 resolved {resolved}, not " +
                    $"{(int)action} — -1 means 'no combo tracked', not 'combo'. Every enemy " +
                    "actor and every non-dungeon swing runs at this tier, so treating it as a " +
                    "combo would put the whole non-dungeon game on the wrong clip");
            }
        }

        [Test]
        public void ComboTierOutsideTheSimsRange_StaysInsideTheAnimatorsRows()
        {
            // IHackSnapshot.ComboIndex is documented 0..2, so these tiers are
            // out of contract and the code states no behaviour for them.
            // Observation today: anything > 1 falls into the attack3 finisher.
            // That is NOT asserted here — only the invariant that does hold.
            foreach (var tier in new[] { 3, 4, 99, int.MaxValue })
            {
                var resolved = ActorView.ResolveActionValue(ActorAction.Attack, tier, castPoseLive: false);

                Assert.That(resolved, Is.InRange(0, CastValue),
                    $"combo tier {tier} resolved {resolved}, outside the animator's " +
                    $"0..{CastValue} rows — whatever an out-of-range tier is taken to mean, " +
                    "SetInteger(\"action\") must send a value some transition matches, or the " +
                    "actor freezes in its previous pose for the rest of the swing");
            }
        }

        [Test]
        public void EveryResolvablePose_OccupiesItsOwnAnimatorValue()
        {
            var owner = new Dictionary<int, string>();

            foreach (ActorAction action in Enum.GetValues(typeof(ActorAction)))
                Claim(owner, ActorView.ResolveActionValue(action, comboTier: 0, castPoseLive: false),
                    $"ActorAction.{action}");

            Claim(owner, ActorView.ResolveActionValue(ActorAction.Attack, 1, castPoseLive: false),
                "combo tier 1 (attack2)");
            Claim(owner, ActorView.ResolveActionValue(ActorAction.Attack, 2, castPoseLive: false),
                "combo tier 2 (attack3)");
            Claim(owner, ActorView.ResolveActionValue(ActorAction.Idle, comboTier: 0, castPoseLive: true),
                "the cast pose");

            var simActionCount = Enum.GetValues(typeof(ActorAction)).Length;

            Assert.That(owner.Count, Is.EqualTo(simActionCount + 3),
                $"the resolver reaches {owner.Count} distinct animator values but there are " +
                $"{simActionCount} sim actions plus 3 View-only substates — a pose is " +
                "unreachable, so the clip authored for it never plays");

            // The substates sit immediately above the sim range by contract
            // (ClipTableTests pins the same adjacency on the clip table). If a
            // sim action is added, ActorView's literals, that table and this
            // fixture must move together.
            Assert.That(Attack2Value, Is.EqualTo(simActionCount),
                $"the substate block starts at {Attack2Value} but the sim range now ends at " +
                $"{simActionCount} — the two are adjacent by contract. Either a substate " +
                "overlaps a sim action, or a clip row between them belongs to nothing");
        }

        [Test]
        public void Resolve_IsPure_AcrossRepeatedAndInterleavedCalls()
        {
            var grid = BuildGrid();
            var baseline = new int[grid.Length];

            for (var i = 0; i < grid.Length; i++)
                baseline[i] = Call(grid[i]);

            // Immediate repeat: catches a resolver that mutated something on
            // its first call for that input.
            for (var i = 0; i < grid.Length; i++)
                AssertStable(grid[i], baseline[i], Call(grid[i]), "an immediate repeat");

            // Reverse order: a plain repeat would miss a cache keyed on "the
            // last input I saw".
            for (var i = grid.Length - 1; i >= 0; i--)
                AssertStable(grid[i], baseline[i], Call(grid[i]), "a reverse-order pass");

            // Deterministic stride, coprime with the grid length so it visits
            // every cell exactly once: each case is re-resolved with a
            // different case resolved immediately before it. No randomness —
            // a flaky purity test would be worse than none.
            for (var step = 0; step < grid.Length; step++)
            {
                var i = step * 7 % grid.Length;
                AssertStable(grid[i], baseline[i], Call(grid[i]), "an interleaved pass");
            }
        }

        /// <summary>Names what the cast pose would hide for a given action. The
        /// reaction states are the dangerous ones: the sim ASSERTED that the
        /// actor was struck, dodged, blocked or died, and View-owned decoration
        /// must never overrule a state the sim already committed to.</summary>
        static string CastMaskDanger(ActorAction action)
        {
            switch (action)
            {
                case ActorAction.Hit:
                case ActorAction.BigHit:
                    return "the actor would hold a cast pose while the sim has it staggered, " +
                           "hiding the hit that is actually landing";
                case ActorAction.Die:
                    return "a dying actor would cast instead of falling, so a death the sim " +
                           "already committed to is never shown";
                case ActorAction.Avoid:
                case ActorAction.Defence:
                    return "a dodge or block the sim asserted would be replaced by " +
                           "decoration, so the player cannot read whether their defence came out";
                case ActorAction.Move:
                case ActorAction.Run:
                    return "locomotion would freeze into a cast pose mid-stride";
                case ActorAction.Attack:
                case ActorAction.Critical:
                    return "a swing the sim is already resolving hits for would be replaced " +
                           "by decoration";
                default:
                    return "an action the sim asserted would be replaced by View-owned decoration";
            }
        }

        static (ActorAction Action, int Tier, bool Cast)[] BuildGrid()
        {
            var actions = (ActorAction[])Enum.GetValues(typeof(ActorAction));
            var grid = new (ActorAction, int, bool)[actions.Length * GridTiers.Length * 2];
            var n = 0;

            foreach (var action in actions)
            foreach (var tier in GridTiers)
            {
                grid[n++] = (action, tier, false);
                grid[n++] = (action, tier, true);
            }

            return grid;
        }

        static int Call((ActorAction Action, int Tier, bool Cast) c) =>
            ActorView.ResolveActionValue(c.Action, c.Tier, c.Cast);

        static void AssertStable((ActorAction Action, int Tier, bool Cast) c,
                                 int baseline, int actual, string pass)
        {
            Assert.That(actual, Is.EqualTo(baseline),
                $"ResolveActionValue({c.Action}, tier {c.Tier}, cast {c.Cast}) returned " +
                $"{baseline} first and {actual} on {pass} — the resolver is not pure. It runs " +
                "once per actor per frame, so hidden state makes one actor's pose depend on " +
                "which actor happened to be resolved before it");
        }

        static void Claim(IDictionary<int, string> owner, int value, string pose)
        {
            Assert.That(owner.ContainsKey(value), Is.False,
                $"{pose} and {(owner.TryGetValue(value, out var held) ? held : "another pose")} " +
                $"both resolve animator value {value} — the two collapse into one Animator " +
                "state, so one of them silently plays the other's clip forever");

            owner[value] = pose;
        }
    }
}
