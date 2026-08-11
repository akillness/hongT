// §M companion pose selection. The companion's Idle-vs-Move pose is decided by
// three pure helpers on ActorView, pinned EXHAUSTIVELY here rather than sampled
// (same idiom as PoseResolveTests, which pins ResolveActionValue).
//
// Why these functions exist — the regression they were extracted to kill:
// GameView.SyncViews used to INFER the pose from PLAYER PROXIMITY, roughly
//   restIdle = no gaze target && !attacking
//              && playerDistSq < CompanionFollowOffset^2 * 2.25
// i.e. "parked whenever within 120 px of the player" (80 px follow offset x
// sqrt(2.25) = 1.5). That held while a no-target companion inside the follow
// band was genuinely parked. AMENDMENT #18 then gave the companion a
// deterministic idle route that WALKS 24 px WanderStride legs entirely inside
// ComfortRadius (128 px) — so the whole wander band sits inside the 120 px
// "rest" inference. The stale inference posed a WALKING body as Idle: the
// companion slid across the floor with no walk cycle. The same inference also
// froze a companion hard-following a MOVING player inside the band.
//
// The fix reads actual per-step DISPLACEMENT instead of distance-to-player.
//
// What these tests defend (and what breaks without them):
//   * the full 2^3 truth table of ResolveCompanionAction — the input space is
//     eight rows, so sampling it would leave the shipped bug's exact row
//     (moving, no attack, no gaze) free to regress unnoticed;
//   * attack priority — all four attacking rows yield Attack. The strike must
//     always show; a swing hidden behind locomotion is a hit the player cannot
//     read while the sim is already resolving its damage;
//   * the headline regression row, moving && !attacking && !hasGaze => Move.
//     This is the slide bug. The old inference returned Idle for exactly this
//     state whenever the companion was near the player, which #18's idle route
//     guarantees it is;
//   * hasGaze && !moving => Move — a companion holding a target between strikes
//     reads as ready, not asleep;
//   * the ONE Idle row. Widening it re-freezes a walking body;
//   * the strobe guard. SyncViews runs per RENDER frame but the sim advances 0
//     or 1 fixed steps (GameView: _simDelta = steps * SimConfig.FixedStep), so
//     above 60 fps most frames re-read an UNCHANGED position. The hold must
//     therefore decay on SIM delta: a zero-step frame ages it by EXACTLY zero.
//     Decaying on render time would expire it during those frames and strobe
//     the pose Move/Idle at the render-vs-sim beat frequency;
//   * the hold still expires well inside AMENDMENT #18's WanderDwellSeconds, so
//     a genuine dwell between legs reads as a pause and not a permanent walk;
//   * the noise floor. CompanionMoved is a SQUARED magnitude with a strictly-
//     greater test, so exactly-epsilon is not motion, sign does not matter, and
//     two sub-epsilon axes can still sum to real motion;
//   * purity — all three run once per companion per frame, so hidden state
//     would make one companion's pose depend on which one was resolved first.
//
// §G1 companion facing/yaw — the SECOND defect at the SAME call site.
//
// SyncCompanion used `attackFacing != 0` as its stand-in for "is swinging".
// hack.CompanionFacingAt(slot) is ±1 ALWAYS and never 0 (CinderSim: every
// write is `_player.Facing` or `targetDeltaX > 0f ? 1 : -1`), so that test was
// a TAUTOLOGY — true on every frame of every companion's life. Two silent
// consequences:
//   * _gazeYaw was pinned to a hard 90°/270°, so the 16-direction gaze angle
//     GameView computes per frame was passed in and immediately discarded;
//   * Apply picks its yaw as `!IsNaN(_gazeYaw)` -> gaze, else attack, else the
//     movement delta. A companion's _gazeYaw was never NaN, so BOTH later
//     branches were unreachable and a walking companion stared sideways while
//     it slid down its travel path.
//
// ResolveCompanionSwingFacing(attacking, simFacing) does the narrowing, and
// SyncCompanion applies it internally so no caller can forget it. GameView now
// forwards CompanionFacingAt(slot) UNCONDITIONALLY — that is the correct call.
//
// What the facing tests defend (and what breaks without them):
//   * the whole (attacking x sign) space of ResolveCompanionSwingFacing, plus
//     the sim-impossible 0 — six rows, enumerated, not sampled;
//   * !attacking => 0 for BOTH signs. This is the entire fix: it is the only
//     way a non-zero sim facing can stop meaning "is swinging";
//   * attacking => the sim's facing passes through UNCHANGED. The strike is
//     the sim's to aim; a view-side re-derivation would fight the damage the
//     sim is already resolving;
//   * the same thing end to end through a REAL ActorView, because the helper
//     being correct is worthless if SyncCompanion stops calling it: a supplied
//     gaze angle must survive a non-zero attackFacing, and a NaN gaze must
//     stay NaN so Apply's movement branch is reachable at all.
//
// The pose and facing helpers are `internal static` on ActorView and Assets/
// Scripts/View/AssemblyInfo.cs declares InternalsVisibleTo("CinderCourt.Tests
// .EditMode"), which this asmdef also references — so every helper call below
// is a compile-time binding. The ONE reflective read in this file is
// ActorView's private _gazeYaw (§F): the yaw decision is consumed inside Apply
// and has no accessor, so reflection is the only way to observe it. That
// lookup is null-guarded — a renamed field must fail loudly rather than let
// the swing-gate pins pass without reading anything.
using System;
using System.Collections.Generic;
using System.Reflection;
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class CompanionPoseTests
    {
        private const float Tolerance = 1e-4f;

        // COMPILE-TIME FACT, not a runtime probe: these bindings only compile if
        // the helpers have exactly these signatures. Player position is a
        // position, and there is no positional parameter anywhere in the pose
        // decision path — proximity CANNOT influence the pose because it is not
        // even expressible at these call sites. Re-introducing the old
        // `playerDistSq` argument breaks this file at COMPILE time, before any
        // assertion runs. Exercised for real in ThePoseHelpers_TakeNoPlayerPosition.
        static readonly Func<float, float, bool> MovedSignature = ActorView.CompanionMoved;
        static readonly Func<float, bool, float, float> HoldSignature = ActorView.AdvanceCompanionMoveHold;
        static readonly Func<bool, bool, bool, ActorAction> ActionSignature = ActorView.ResolveCompanionAction;
        // Same compile-time lock for the swing gate: (bool, int) -> int. The
        // `attacking` flag is FIRST and it is a bool, so the reverted form —
        // deriving the gate from the facing int alone — cannot even be bound
        // here. Exercised in SwingFacing_IsPure_AcrossRepeatedAndInterleavedCalls.
        static readonly Func<bool, int, int> SwingFacingSignature = ActorView.ResolveCompanionSwingFacing;

        // The old inference's radius, rebuilt from the SAME symbol it used:
        // playerDistSq < CompanionFollowOffset^2 * 2.25, and sqrt(2.25) = 1.5.
        const float OldRestInferenceRadius = HackSpec.CompanionFollowOffset * 1.5f;

        // A companion mid-wander sits well inside BOTH the old 120 px "rest"
        // band and #18's 128 px ComfortRadius. That overlap is the bug.
        const float WanderingPlayerDistance = 60f;

        // One fixed step of idle-route locomotion, in world px. Derived, never
        // guessed: UpdateCompanionCohesion walks the leg by passing _playerSpeed
        // to StepCompanionToward, and _playerSpeed is seeded from
        // SimConfig.PlayerSpeed. ~3.63 px/step — matching ActorView's own
        // "~4 px/step at companion speed" note, and 363x CompanionMoveEpsilon.
        const float PerStepStride = SimConfig.PlayerSpeed * SimConfig.FixedStep;

        // Whole fixed steps needed to walk one 24 px WanderStride leg.
        static readonly int LegSteps =
            Mathf.CeilToInt(CompanionCohesionSpec.WanderStride / PerStepStride);

        // That leg sliced evenly across its steps — the per-step displacement
        // SyncCompanion actually observes while #18's idle route runs.
        static readonly float LegSlice = CompanionCohesionSpec.WanderStride / LegSteps;

        // Fixed steps in one wander dwell. Used only as a runaway guard: a hold
        // that outlives the dwell is the failure this fixture is looking for.
        static readonly int DwellSteps =
            Mathf.CeilToInt(CompanionCohesionSpec.WanderDwellSeconds / SimConfig.FixedStep);

        // The complete input space: 2^3 = 8 rows, enumerated, not sampled.
        static readonly (bool Moving, bool Attacking, bool Gaze, ActorAction Expected, string Why)[] PoseTable =
        {
            (false, false, false, ActorAction.Idle,
                "still, unarmed and untargeted is the ONLY resting state"),
            (false, false, true, ActorAction.Move,
                "a held target between strikes must read as ready, not asleep"),
            (false, true, false, ActorAction.Attack,
                "the strike must show even from a standstill"),
            (false, true, true, ActorAction.Attack,
                "a swing outranks the gaze stance it was aimed with"),
            (true, false, false, ActorAction.Move,
                "THE SHIPPED BUG: a no-target companion walking AMENDMENT #18's " +
                "idle route must animate, not slide"),
            (true, false, true, ActorAction.Move,
                "walking toward a target is still locomotion"),
            (true, true, false, ActorAction.Attack,
                "a swing outranks locomotion — the hit is the readable event"),
            (true, true, true, ActorAction.Attack,
                "a swing outranks everything, all three flags live"),
        };

        // The swing gate's complete input space: (attacking) x (sim facing).
        // The sim emits only ±1 — that IS the defect — but 0 is enumerated too
        // so the gate is pinned as a function of `attacking` alone rather than
        // of the facing's truthiness.
        static readonly (bool Attacking, int SimFacing, int Expected, string Why)[] SwingFacingTable =
        {
            (false, 1, 0,
                "THE SHIPPED BUG: a companion that is NOT swinging must gate to 0 even though " +
                "the sim hands it +1 on literally every frame"),
            (false, -1, 0,
                "the same, mirrored — a left-facing idle companion is no more 'swinging' than a " +
                "right-facing one"),
            (false, 0, 0,
                "sim-impossible, enumerated for completeness: not swinging is not swinging"),
            (true, 1, 1,
                "the swing must keep the sim's authoritative +1 — the sim is already resolving " +
                "that strike's damage in that direction"),
            (true, -1, -1,
                "the swing must keep the sim's authoritative -1, sign intact"),
            (true, 0, 0,
                "sim-impossible, enumerated for completeness: pass-through is pass-through"),
        };

        // §F scenario constants. A 45° gaze is deliberately NOT 90 or 270, so a
        // reverted gate cannot coincidentally produce the expected value.
        const float GazeAngle = 45f;
        const float SimX = 700f;
        const float SimY = 604f;

        // ---------------------------------------------------------------
        // A. ResolveCompanionAction — pinned exhaustively.
        // ---------------------------------------------------------------

        [Test]
        public void EveryInputCombination_ResolvesItsDocumentedPose()
        {
            Assert.That(PoseTable.Length, Is.EqualTo(8),
                $"the truth table holds {PoseTable.Length} rows but ResolveCompanionAction " +
                "takes three bools — 2^3 = 8. A missing row is an unpinned pose, and the " +
                "shipped slide bug lived in exactly one row");

            foreach (var (moving, attacking, gaze, expected, why) in PoseTable)
            {
                var resolved = ActorView.ResolveCompanionAction(moving, attacking, gaze);

                Assert.That(resolved, Is.EqualTo(expected),
                    $"ResolveCompanionAction(moving: {moving}, attacking: {attacking}, " +
                    $"hasGaze: {gaze}) resolved {resolved}, not {expected} — {why}");
            }
        }

        [Test]
        public void Attacking_OutranksMovementAndGaze()
        {
            // All four attacking rows, stated independently of the table so a
            // table edit cannot quietly retire the priority rule itself.
            foreach (var moving in new[] { false, true })
            foreach (var gaze in new[] { false, true })
            {
                var resolved = ActorView.ResolveCompanionAction(moving, attacking: true, hasGaze: gaze);

                Assert.That(resolved, Is.EqualTo(ActorAction.Attack),
                    $"an attacking companion (moving: {moving}, hasGaze: {gaze}) resolved " +
                    $"{resolved}, not Attack — the swing must always show. The sim is already " +
                    "resolving that strike's damage, so hiding it behind locomotion or a gaze " +
                    "stance makes a landed hit unreadable");
            }
        }

        [Test]
        public void WalkingWithNoTarget_Animates_TheAmendment18SlideRegression()
        {
            // The exact state the old proximity inference got wrong. AMENDMENT
            // #18's idle route runs ONLY when the companion has no target, and
            // it walks entirely inside the follow band — so this row is not an
            // edge case, it is the idle route's steady state.
            var resolved = ActorView.ResolveCompanionAction(
                moving: true, attacking: false, hasGaze: false);

            Assert.That(resolved, Is.EqualTo(ActorAction.Move),
                $"a walking, untargeted companion resolved {resolved}, not Move — this is the " +
                "shipped slide bug. The old inference read PLAYER PROXIMITY and returned Idle " +
                "here, so a body walking #18's 24 px legs slid across the floor with no walk " +
                "cycle");

            Assert.That(resolved, Is.Not.EqualTo(ActorAction.Idle),
                "the walking companion resolved Idle — the pose is being inferred from " +
                "something other than movement again");
        }

        [Test]
        public void GazeHeldBetweenStrikes_ReadsAsReady_NotAsleep()
        {
            var resolved = ActorView.ResolveCompanionAction(
                moving: false, attacking: false, hasGaze: true);

            Assert.That(resolved, Is.EqualTo(ActorAction.Move),
                $"a still companion holding a combat gaze resolved {resolved}, not Move — " +
                "between swings the body must read as ready. Idling on a live target makes " +
                "the companion look asleep while it is about to strike");
        }

        [Test]
        public void OnlyAStillUntargetedCompanion_Idles()
        {
            var resolved = ActorView.ResolveCompanionAction(
                moving: false, attacking: false, hasGaze: false);

            Assert.That(resolved, Is.EqualTo(ActorAction.Idle),
                $"a still, untargeted, non-attacking companion resolved {resolved}, not Idle — " +
                "this is the one resting state; without it the companion never stops walking");

            // Count the Idle rows across the whole space: widening the Idle
            // branch is precisely how the slide bug re-enters.
            var idleRows = 0;
            foreach (var (moving, attacking, gaze, _, _) in PoseTable)
                if (ActorView.ResolveCompanionAction(moving, attacking, gaze) == ActorAction.Idle)
                    idleRows++;

            Assert.That(idleRows, Is.EqualTo(1),
                $"{idleRows} of the 8 input rows resolve Idle, expected exactly 1 — a widened " +
                "Idle branch re-freezes a walking or target-holding body, which is the " +
                "regression this whole fixture exists for");
        }

        [Test]
        public void ResolveCompanionAction_IsPure_AcrossRepeatedAndInterleavedCalls()
        {
            var baseline = new ActorAction[PoseTable.Length];
            for (var i = 0; i < PoseTable.Length; i++)
                baseline[i] = Call(i);

            // Immediate repeat: catches a resolver that mutated something on
            // its first call for that input.
            for (var i = 0; i < PoseTable.Length; i++)
                AssertStablePose(i, baseline[i], Call(i), "an immediate repeat");

            // Reverse order: a plain repeat would miss a cache keyed on "the
            // last input I saw".
            for (var i = PoseTable.Length - 1; i >= 0; i--)
                AssertStablePose(i, baseline[i], Call(i), "a reverse-order pass");

            // Deterministic stride coprime with 8, so it visits every row
            // exactly once with a different row resolved immediately before it.
            // No randomness — a flaky purity test would be worse than none.
            for (var step = 0; step < PoseTable.Length; step++)
            {
                var i = step * 3 % PoseTable.Length;
                AssertStablePose(i, baseline[i], Call(i), "an interleaved pass");
            }
        }

        // ---------------------------------------------------------------
        // B. The headline regression, in AMENDMENT #18's own units.
        // ---------------------------------------------------------------

        [Test]
        public void WanderLegInsideTheOldRestBand_AnimatesOnEveryStep()
        {
            // The scenario really is inside the band the old code called "rest",
            // and inside #18's comfort band that keeps the companion wandering
            // instead of recovering. Both must hold or this proves nothing.
            Assert.That(WanderingPlayerDistance, Is.LessThan(OldRestInferenceRadius),
                $"the scenario places the companion {WanderingPlayerDistance} px from the " +
                $"player, outside the old {OldRestInferenceRadius} px rest inference — then it " +
                "does not reproduce the bug at all");
            Assert.That(WanderingPlayerDistance, Is.LessThan(CompanionCohesionSpec.ComfortRadius),
                $"{WanderingPlayerDistance} px is outside ComfortRadius " +
                $"({CompanionCohesionSpec.ComfortRadius} px), so #18 would be RECOVERING here, " +
                "not walking its idle route");
            Assert.That(CompanionCohesionSpec.ComfortRadius, Is.GreaterThan(OldRestInferenceRadius),
                $"ComfortRadius ({CompanionCohesionSpec.ComfortRadius}) no longer exceeds the " +
                $"old inference radius ({OldRestInferenceRadius}) — the overlap that CAUSED the " +
                "slide bug is the premise of this test");

            // Walk one whole WanderStride leg through the REAL helper pair, one
            // fixed step at a time, exactly as SyncCompanion does.
            var hold = 0f;
            var walked = 0f;

            for (var step = 0; step < LegSteps; step++)
            {
                var moved = ActorView.CompanionMoved(LegSlice, 0f);

                Assert.That(moved, Is.True,
                    $"step {step} of the wander leg moved {LegSlice} px on X and was not " +
                    $"counted as motion — a {CompanionCohesionSpec.WanderStride} px leg walked " +
                    $"over {LegSteps} fixed steps is real locomotion, not float noise");

                hold = ActorView.AdvanceCompanionMoveHold(hold, moved, SimConfig.FixedStep);
                walked += LegSlice;

                var pose = ActorView.ResolveCompanionAction(
                    moving: hold > 0f, attacking: false, hasGaze: false);

                // The old proximity inference returned Idle for EVERY step of
                // this leg — the companion was inside 120 px the whole time.
                // That is exactly the slide: a translating body with no walk
                // cycle. Player distance is never consulted below.
                Assert.That(pose, Is.EqualTo(ActorAction.Move),
                    $"step {step} of a {CompanionCohesionSpec.WanderStride} px wander leg, " +
                    $"{WanderingPlayerDistance} px from the player, posed {pose} instead of " +
                    "Move. The old code inferred Idle here from proximity alone and the " +
                    "companion slid across the floor");
            }

            Assert.That(walked, Is.EqualTo(CompanionCohesionSpec.WanderStride).Within(Tolerance),
                $"the simulated leg covered {walked} px, not " +
                $"{CompanionCohesionSpec.WanderStride} px — the scenario drifted off #18's " +
                "actual stride, so it no longer reproduces the shipped geometry");
        }

        [Test]
        public void ThePoseHelpers_TakeNoPlayerPosition()
        {
            // Exercises the compile-time signature locks declared at the top of
            // the fixture, so the guarantee is observable and not merely
            // asserted in a comment: the delegates accept ONLY displacement,
            // sim delta and three bools. There is nowhere to pass a player
            // position, which is why proximity can no longer reach the pose.
            Assert.That(MovedSignature(LegSlice, 0f),
                Is.EqualTo(ActorView.CompanionMoved(LegSlice, 0f)),
                "CompanionMoved through its (float, float) -> bool signature disagreed with a " +
                "direct call — the displacement-only contract has changed shape");

            Assert.That(HoldSignature(0f, true, SimConfig.FixedStep),
                Is.EqualTo(ActorView.AdvanceCompanionMoveHold(0f, true, SimConfig.FixedStep))
                    .Within(Tolerance),
                "AdvanceCompanionMoveHold through its (float, bool, float) -> float signature " +
                "disagreed with a direct call — the hold takes a SIM DELTA and nothing else");

            Assert.That(ActionSignature(true, false, false),
                Is.EqualTo(ActorView.ResolveCompanionAction(true, false, false)),
                "ResolveCompanionAction through its (bool, bool, bool) -> ActorAction signature " +
                "disagreed with a direct call — the pose decision takes three flags and no " +
                "geometry, which is the entire fix for the slide bug");
        }

        // ---------------------------------------------------------------
        // C. AdvanceCompanionMoveHold — the strobe guard.
        // ---------------------------------------------------------------

        [Test]
        public void AZeroStepFrame_AgesTheHoldByExactlyZero()
        {
            // Asserted EXACTLY, with no tolerance, on purpose: `x - 0f == x` is
            // exact in IEEE-754, and "ages by approximately zero" is precisely
            // the bug. A tolerance here would hide a small-but-nonzero decay,
            // which is what decaying on render time looks like.
            foreach (var hold in new[] { 0f, ActorView.CompanionMoveHoldSeconds * 0.5f,
                                         ActorView.CompanionMoveHoldSeconds })
            {
                var aged = ActorView.AdvanceCompanionMoveHold(hold, moved: false, simDelta: 0f);

                Assert.That(aged, Is.EqualTo(hold),
                    $"a zero-step render frame aged a hold of {hold} to {aged} — it must age by " +
                    "EXACTLY zero. SyncViews runs per render frame while the sim advances 0 or " +
                    "1 fixed steps, so any non-zero decay on a zero-step frame strobes the pose");
            }
        }

        [Test]
        public void HighRefreshDisplay_DoesNotStrobeThePose()
        {
            // A 240 Hz display against a 60 Hz sim: one moved step, then three
            // render frames that advance no sim steps at all. The pose must
            // read Move across the WHOLE sequence, not merely at the endpoint —
            // a strobe is a dropout in the middle.
            var hold = ActorView.AdvanceCompanionMoveHold(
                0f, moved: true, simDelta: SimConfig.FixedStep);

            AssertPoseIsMove(hold, frame: 0,
                "the sim step that actually moved the companion");

            for (var frame = 1; frame <= 3; frame++)
            {
                hold = ActorView.AdvanceCompanionMoveHold(hold, moved: false, simDelta: 0f);

                Assert.That(hold, Is.GreaterThan(0f),
                    $"the hold fell to {hold} on zero-step render frame {frame} of 3 — at 240 Hz " +
                    "three of every four frames advance no sim step, so a hold that decays on " +
                    "them expires between real steps");

                AssertPoseIsMove(hold, frame,
                    "a zero-step render frame between two sim steps");
            }
        }

        [Test]
        public void AMovingStep_ResetsTheHold_FromAnyPriorValue()
        {
            // Including from 0 (the first moving step after a dwell) and from
            // an over-large value, which is what a future caller bug looks like.
            foreach (var prior in new[] { 0f, ActorView.CompanionMoveHoldSeconds * 0.5f,
                                          ActorView.CompanionMoveHoldSeconds, 10f })
            {
                var reset = ActorView.AdvanceCompanionMoveHold(
                    prior, moved: true, simDelta: SimConfig.FixedStep);

                Assert.That(reset, Is.EqualTo(ActorView.CompanionMoveHoldSeconds).Within(Tolerance),
                    $"a moving step from a prior hold of {prior} produced {reset}, not " +
                    $"{ActorView.CompanionMoveHoldSeconds} — every moving step must re-arm the " +
                    "full window, or a companion that has been walking for a while starts " +
                    "flickering as its hold runs down mid-stride");
            }
        }

        [Test]
        public void TheHold_DecaysMonotonically_AndClampsAtZero()
        {
            var hold = ActorView.AdvanceCompanionMoveHold(
                0f, moved: true, simDelta: SimConfig.FixedStep);

            for (var step = 0; step < DwellSteps; step++)
            {
                var previous = hold;
                hold = ActorView.AdvanceCompanionMoveHold(
                    hold, moved: false, simDelta: SimConfig.FixedStep);

                Assert.That(hold, Is.GreaterThanOrEqualTo(0f),
                    $"the hold went negative ({hold}) after {step + 1} idle steps — a negative " +
                    "hold still fails the `hold > 0f` pose test, but it also means the clamp is " +
                    "gone and the value drifts without bound");

                if (previous > 0f)
                    Assert.That(hold, Is.LessThan(previous),
                        $"the hold did not shrink on idle step {step + 1} ({previous} -> {hold}) " +
                        "— a hold that stops decaying leaves the companion permanently walking");
                else
                    Assert.That(hold, Is.EqualTo(0f),
                        $"the hold left the zero clamp ({previous} -> {hold}) without a moving " +
                        "step");
            }

            Assert.That(hold, Is.EqualTo(0f),
                $"after a full wander dwell of idle steps the hold is still {hold} — it must " +
                "have reached the zero clamp long before this point");
        }

        [Test]
        public void TheHold_ExpiresWellInsideTheWanderDwell()
        {
            // Derived from the real constants by SIMULATION rather than by
            // dividing them: 3f * SimConfig.FixedStep and the 0.05f literal are
            // NOT bit-identical (0.050000004 vs 0.050000001), so counting steps
            // is the honest measurement and the ratio is not an identity.
            var (steps, elapsed) = DecayHoldToZero();

            Assert.That(steps, Is.GreaterThan(0),
                "the hold expired without a single idle step — a moving step must leave a " +
                "settle tail, or the pose drops to Idle the instant the companion pauses " +
                "between two steps of the same leg");

            Assert.That(elapsed, Is.LessThan(CompanionCohesionSpec.WanderDwellSeconds),
                $"the hold survived {elapsed} s ({steps} fixed steps), which is not strictly " +
                $"inside AMENDMENT #18's {CompanionCohesionSpec.WanderDwellSeconds} s wander " +
                "dwell — a hold that outlives the dwell means the companion never visibly " +
                "stops, so the pause between two idle legs reads as a permanent walk");
        }

        [Test]
        public void AdvanceCompanionMoveHold_IsPure()
        {
            var holds = new[] { 0f, ActorView.CompanionMoveHoldSeconds * 0.5f,
                                ActorView.CompanionMoveHoldSeconds, 10f };
            var deltas = new[] { 0f, SimConfig.FixedStep, CompanionCohesionSpec.WanderDwellSeconds };

            foreach (var hold in holds)
            foreach (var moved in new[] { false, true })
            foreach (var delta in deltas)
            {
                var first = ActorView.AdvanceCompanionMoveHold(hold, moved, delta);
                var second = ActorView.AdvanceCompanionMoveHold(hold, moved, delta);

                Assert.That(second, Is.EqualTo(first).Within(Tolerance),
                    $"AdvanceCompanionMoveHold({hold}, {moved}, {delta}) returned {first} then " +
                    $"{second} — it runs once per companion per frame, so hidden state makes one " +
                    "companion's pose depend on which companion advanced before it");
            }
        }

        // ---------------------------------------------------------------
        // D. CompanionMoved — the noise floor.
        // ---------------------------------------------------------------

        [Test]
        public void ZeroDelta_IsNotMotion()
        {
            Assert.That(ActorView.CompanionMoved(0f, 0f), Is.False,
                "a perfectly stationary companion was counted as moving — every dwell frame " +
                "would re-arm the hold and the companion would never idle again");
        }

        [Test]
        public void SubEpsilonJitter_IsNotMotion()
        {
            foreach (var scale in new[] { 0.01f, 0.5f, 0.99f })
            {
                var jitter = ActorView.CompanionMoveEpsilon * scale;

                Assert.That(ActorView.CompanionMoved(jitter, 0f), Is.False,
                    $"X jitter of {jitter} px (below CompanionMoveEpsilon " +
                    $"{ActorView.CompanionMoveEpsilon}) was counted as motion — float noise at " +
                    "arena scale would hold a parked companion in a walk cycle forever");
                Assert.That(ActorView.CompanionMoved(0f, jitter), Is.False,
                    $"Y jitter of {jitter} px was counted as motion");
            }
        }

        [Test]
        public void ExactlyEpsilon_IsNotMotion_ButJustAboveIt_Is()
        {
            // The test is strictly-greater, so epsilon itself sits on the
            // stationary side. Pinning BOTH sides stops a `>=` slip.
            Assert.That(ActorView.CompanionMoved(ActorView.CompanionMoveEpsilon, 0f), Is.False,
                $"a displacement of exactly CompanionMoveEpsilon " +
                $"({ActorView.CompanionMoveEpsilon}) counted as motion — the comparison is " +
                "strictly greater, so the boundary belongs to 'stationary'. A `>=` here would " +
                "let the noise floor itself drive the walk cycle");

            Assert.That(ActorView.CompanionMoved(0f, ActorView.CompanionMoveEpsilon), Is.False,
                "exactly CompanionMoveEpsilon on Y counted as motion — the boundary must be " +
                "identical on both axes, since the test is a squared magnitude");

            var justAbove = ActorView.CompanionMoveEpsilon * 1.01f;

            Assert.That(ActorView.CompanionMoved(justAbove, 0f), Is.True,
                $"a displacement of {justAbove} px, above CompanionMoveEpsilon " +
                $"({ActorView.CompanionMoveEpsilon}), was NOT counted as motion — the floor has " +
                "risen and real slow locomotion is being discarded as noise");
        }

        [Test]
        public void ARealPerStepStride_IsMotion()
        {
            // Derived from #18's stride and the fixed step, not hardcoded.
            Assert.That(ActorView.CompanionMoved(LegSlice, 0f), Is.True,
                $"one step of an idle-route leg ({LegSlice} px) was not counted as motion — a " +
                $"{CompanionCohesionSpec.WanderStride} px leg walked over {LegSteps} fixed steps " +
                "is exactly the locomotion the walk cycle exists for");

            Assert.That(ActorView.CompanionMoved(PerStepStride, 0f), Is.True,
                $"a full-speed step ({PerStepStride} px, from SimConfig.PlayerSpeed x " +
                "FixedStep) was not counted as motion");

            Assert.That(LegSlice, Is.GreaterThan(ActorView.CompanionMoveEpsilon),
                $"the per-step wander slice ({LegSlice} px) is no longer clear of the noise " +
                $"floor ({ActorView.CompanionMoveEpsilon} px) — real locomotion and float noise " +
                "are no longer separable, and no threshold can fix that");
        }

        [Test]
        public void MotionIsDetectedOnBothAxes_AndDiagonally()
        {
            // #18's legs alternate +X, +Y, -X, -Y, so a per-axis blind spot
            // would freeze the pose on half of every wander cycle.
            Assert.That(ActorView.CompanionMoved(LegSlice, 0f), Is.True,
                "a pure +X leg was not counted as motion — #18 walks one in every two legs " +
                "along X");
            Assert.That(ActorView.CompanionMoved(0f, LegSlice), Is.True,
                "a pure +Y leg was not counted as motion — #18 walks one in every two legs " +
                "along Y");
            Assert.That(ActorView.CompanionMoved(LegSlice, LegSlice), Is.True,
                "a diagonal step was not counted as motion — recovery toward a moving player " +
                "is diagonal almost always");

            // Two axes each BELOW the floor still sum past it: 0.8^2 + 0.8^2 =
            // 1.28 > 1. This is what makes it a squared magnitude rather than
            // an independent per-axis threshold.
            var perAxis = ActorView.CompanionMoveEpsilon * 0.8f;

            Assert.That(ActorView.CompanionMoved(perAxis, 0f), Is.False,
                $"{perAxis} px on one axis alone should sit below the floor");
            Assert.That(ActorView.CompanionMoved(perAxis, perAxis), Is.True,
                $"two sub-epsilon axes ({perAxis} px each) did not sum to motion — the test is " +
                "a squared MAGNITUDE, not a per-axis threshold. Treating the axes independently " +
                "discards real diagonal locomotion just under the floor on each axis");
        }

        [Test]
        public void MotionIsSignIndependent()
        {
            // dx^2 + dy^2 cannot care about direction, and #18 walks -X and -Y
            // legs on half of every wander cycle.
            foreach (var (dx, dy) in new[]
                     {
                         (LegSlice, 0f), (-LegSlice, 0f),
                         (0f, LegSlice), (0f, -LegSlice),
                         (-LegSlice, -LegSlice), (LegSlice, -LegSlice), (-LegSlice, LegSlice),
                     })
            {
                Assert.That(ActorView.CompanionMoved(dx, dy), Is.True,
                    $"displacement ({dx}, {dy}) was not counted as motion — the test is a " +
                    "squared magnitude, so it must be sign-independent. #18 walks -X and -Y " +
                    "legs on half of every wander cycle, and those would pose as Idle");
            }

            Assert.That(ActorView.CompanionMoved(-ActorView.CompanionMoveEpsilon, 0f), Is.False,
                "negative exactly-epsilon counted as motion while positive exactly-epsilon does " +
                "not — the boundary must be symmetric");
        }

        [Test]
        public void CompanionMoved_IsPure()
        {
            var deltas = new[]
            {
                (0f, 0f),
                (ActorView.CompanionMoveEpsilon, 0f),
                (-ActorView.CompanionMoveEpsilon, 0f),
                (LegSlice, 0f),
                (0f, LegSlice),
                (LegSlice, LegSlice),
                (-LegSlice, -LegSlice),
            };

            foreach (var (dx, dy) in deltas)
            {
                var first = ActorView.CompanionMoved(dx, dy);

                Assert.That(ActorView.CompanionMoved(dx, dy), Is.EqualTo(first),
                    $"CompanionMoved({dx}, {dy}) was not stable across an immediate repeat");
                // Reverse-argument probe: catches state keyed on "the last
                // delta I saw" without asserting any symmetry the code does not
                // promise — dx^2+dy^2 is symmetric, so the value must match too.
                Assert.That(ActorView.CompanionMoved(dy, dx), Is.EqualTo(ActorView.CompanionMoved(dx, dy)),
                    $"CompanionMoved({dx}, {dy}) and CompanionMoved({dy}, {dx}) disagree — the " +
                    "test is dx^2 + dy^2, which is symmetric in its arguments");
            }
        }

        // ---------------------------------------------------------------
        // E. ResolveCompanionSwingFacing — the swing gate, pinned exhaustively.
        // ---------------------------------------------------------------

        [Test]
        public void EverySwingFacingCombination_ResolvesItsDocumentedGate()
        {
            foreach (var (attacking, simFacing, expected, why) in SwingFacingTable)
            {
                var gated = ActorView.ResolveCompanionSwingFacing(attacking, simFacing);

                Assert.That(gated, Is.EqualTo(expected),
                    $"ResolveCompanionSwingFacing(attacking: {attacking}, simFacing: " +
                    $"{simFacing}) gated to {gated}, not {expected} — {why}");
            }
        }

        [Test]
        public void NotSwinging_GatesToZero_ForBothSigns_TheTautologyRegression()
        {
            // THE FIX, stated on its own so a table edit cannot retire it.
            foreach (var simFacing in new[] { 1, -1 })
            {
                var gated = ActorView.ResolveCompanionSwingFacing(attacking: false, simFacing: simFacing);

                Assert.That(gated, Is.EqualTo(0),
                    $"a companion that is NOT swinging gated a sim facing of {simFacing} to " +
                    $"{gated}, not 0. CompanionFacingAt is ±1 ALWAYS and never 0, so " +
                    "`attackFacing != 0` cannot mean 'is swinging' — it is true on every frame. " +
                    "A non-zero gate here re-pins _gazeYaw to a hard 90°/270°, discards " +
                    "GameView's 16-direction gaze angle, and makes Apply's movement-delta yaw " +
                    "unreachable: the walking companion stares sideways again");
            }

            // Count the non-zero rows across the whole space: a widened gate is
            // precisely how the tautology re-enters.
            var swingRows = 0;
            foreach (var (attacking, simFacing, _, _) in SwingFacingTable)
                if (ActorView.ResolveCompanionSwingFacing(attacking, simFacing) != 0)
                    swingRows++;

            Assert.That(swingRows, Is.EqualTo(2),
                $"{swingRows} of the {SwingFacingTable.Length} input rows gate to a non-zero " +
                "facing, expected exactly 2 — only the two ATTACKING ±1 rows may. Any other " +
                "row gating non-zero means the gate is reading the facing's truthiness again, " +
                "which is a tautology because the sim never emits 0");
        }

        [Test]
        public void Swinging_PassesTheSimFacingThroughUnchanged()
        {
            foreach (var simFacing in new[] { 1, -1 })
            {
                var gated = ActorView.ResolveCompanionSwingFacing(attacking: true, simFacing: simFacing);

                Assert.That(gated, Is.EqualTo(simFacing),
                    $"a swinging companion's sim facing of {simFacing} came back as {gated} — " +
                    "the strike must keep the sim's AUTHORITATIVE facing. The sim is already " +
                    "resolving that swing's damage in that direction, so a view-side rewrite " +
                    "shows a strike aimed away from the hit it just landed");
            }
        }

        [Test]
        public void SwingFacing_IsPure_AcrossRepeatedAndInterleavedCalls()
        {
            // Exercises the compile-time signature lock for real: the gate binds
            // as (bool, int) -> int, with the swing flag FIRST. The reverted
            // form derived the gate from the facing int alone, and that shape
            // cannot be bound to this delegate at all.
            Assert.That(SwingFacingSignature(true, 1),
                Is.EqualTo(ActorView.ResolveCompanionSwingFacing(true, 1)),
                "ResolveCompanionSwingFacing through its (bool, int) -> int signature disagreed " +
                "with a direct call — the gate takes the SWING FLAG plus the sim facing, and " +
                "the flag is the half that carries the meaning");

            var baseline = new int[SwingFacingTable.Length];
            for (var i = 0; i < SwingFacingTable.Length; i++)
                baseline[i] = CallSwingFacing(i);

            for (var i = 0; i < SwingFacingTable.Length; i++)
                AssertStableSwingFacing(i, baseline[i], CallSwingFacing(i), "an immediate repeat");

            for (var i = SwingFacingTable.Length - 1; i >= 0; i--)
                AssertStableSwingFacing(i, baseline[i], CallSwingFacing(i), "a reverse-order pass");

            // Stride 5 is coprime with 6, so every row is visited exactly once
            // with a different row resolved immediately before it. Deterministic
            // — a flaky purity test would be worse than none.
            for (var step = 0; step < SwingFacingTable.Length; step++)
            {
                var i = step * 5 % SwingFacingTable.Length;
                AssertStableSwingFacing(i, baseline[i], CallSwingFacing(i), "an interleaved pass");
            }
        }

        // ---------------------------------------------------------------
        // F. SyncCompanion end to end — the gate must stay WIRED.
        //
        // §E proves the helper is right. These prove SyncCompanion still calls
        // it, driving a REAL ActorView built through the same factory GameView
        // .Rent uses on a pool miss, and forwarding attackFacing the way
        // GameView now does: UNCONDITIONALLY non-zero.
        // ---------------------------------------------------------------

        [Test]
        public void IdleCompanion_KeepsTheSuppliedGazeAngle_DespiteANonZeroSimFacing()
        {
            // THE REGRESSION, end to end. This is the test that goes red if the
            // gate is reverted to `attackFacing != 0`.
            foreach (var simFacing in new[] { 1, -1 })
            {
                var stale = simFacing > 0 ? 90f : 270f;

                WithRentedActor(view =>
                {
                    view.SyncCompanion(SimX, SimY, simFacing, attacking: false,
                        gazeYaw: GazeAngle, simDelta: SimConfig.FixedStep);

                    var yaw = GazeYawOf(view);

                    Assert.That(yaw, Is.EqualTo(GazeAngle).Within(Tolerance),
                        $"a NON-attacking companion with a sim facing of {simFacing} and a " +
                        $"{GazeAngle}° gaze ended up at {yaw}°. GameView forwards " +
                        "CompanionFacingAt(slot) unconditionally and it is ±1 ALWAYS, so if " +
                        "SyncCompanion gates on `attackFacing != 0` instead of the swing flag " +
                        "it snaps here and GameView's 16-direction gaze angle is computed then " +
                        "thrown away");
                    Assert.That(yaw, Is.Not.EqualTo(stale).Within(Tolerance),
                        $"the yaw is the stale hard-snap value {stale}° — the swing gate is " +
                        "reading the sim facing's truthiness again, which is true on every " +
                        "frame of every companion's life");
                });
            }
        }

        [Test]
        public void IdleCompanionWithNoGaze_LeavesTheYawNaN_SoTheMovementBranchIsReachable()
        {
            // Apply picks `!IsNaN(_gazeYaw)` -> gaze, else attack, else the
            // movement delta. That NaN is the ONLY thing that lets the last
            // branch run, so it is load-bearing rather than merely absent.
            WithRentedActor(view =>
            {
                view.SyncCompanion(SimX, SimY, attackFacing: 1, attacking: false,
                    gazeYaw: float.NaN, simDelta: SimConfig.FixedStep);

                var yaw = GazeYawOf(view);

                Assert.That(float.IsNaN(yaw), Is.True,
                    $"an untargeted, non-attacking companion ended up at {yaw}° instead of NaN. " +
                    "Apply only reaches its movement-delta yaw when _gazeYaw IS NaN, so any " +
                    "number here re-freezes a walking companion into a sideways stare — it " +
                    "slides down its travel path facing somewhere else");
            });
        }

        [Test]
        public void SwingingCompanion_OverridesTheGazeAngle_WithTheSimsFacing()
        {
            // The gate must not over-correct: a real swing still owns the yaw,
            // and it owns it even when a gaze angle was supplied for that frame.
            foreach (var (simFacing, expected) in new[] { (1, 90f), (-1, 270f) })
            {
                WithRentedActor(view =>
                {
                    view.SyncCompanion(SimX, SimY, simFacing, attacking: true,
                        gazeYaw: GazeAngle, simDelta: SimConfig.FixedStep);

                    var yaw = GazeYawOf(view);

                    Assert.That(yaw, Is.EqualTo(expected).Within(Tolerance),
                        $"a SWINGING companion facing {simFacing} ended up at {yaw}°, not " +
                        $"{expected}°, with a {GazeAngle}° gaze also supplied. The strike frame " +
                        "must take the sim's authoritative facing — the sim is resolving that " +
                        "swing's damage in that direction, so letting the gaze angle win shows " +
                        "a strike aimed away from the hit it just landed");
                });
            }
        }

        // ---------------------------------------------------------------
        // Helpers.
        // ---------------------------------------------------------------

        /// <summary>Arms the hold with a moving step, then decays it on fixed
        /// sim steps until it clamps at zero. Returns the step count and the
        /// sim seconds it survived. Measured by simulation because the ratio
        /// CompanionMoveHoldSeconds / FixedStep is not an exact integer in
        /// float32.</summary>
        static (int Steps, float Elapsed) DecayHoldToZero()
        {
            var hold = ActorView.AdvanceCompanionMoveHold(
                0f, moved: true, simDelta: SimConfig.FixedStep);
            var steps = 0;
            var elapsed = 0f;

            while (hold > 0f)
            {
                Assert.That(steps, Is.LessThan(DwellSteps),
                    $"the hold had not expired after {steps} fixed steps, a full " +
                    $"{CompanionCohesionSpec.WanderDwellSeconds} s wander dwell — it is not " +
                    "decaying, so the companion walks forever");

                hold = ActorView.AdvanceCompanionMoveHold(
                    hold, moved: false, simDelta: SimConfig.FixedStep);
                elapsed += SimConfig.FixedStep;
                steps++;
            }

            return (steps, elapsed);
        }

        static void AssertPoseIsMove(float hold, int frame, string what)
        {
            var pose = ActorView.ResolveCompanionAction(
                moving: hold > 0f, attacking: false, hasGaze: false);

            Assert.That(pose, Is.EqualTo(ActorAction.Move),
                $"frame {frame} ({what}) posed {pose} instead of Move with a hold of {hold} — " +
                "the pose dropped out mid-sequence, which is the Move/Idle strobe at the " +
                "render-vs-sim beat frequency");
        }

        static ActorAction Call(int row)
        {
            var (moving, attacking, gaze, _, _) = PoseTable[row];
            return ActorView.ResolveCompanionAction(moving, attacking, gaze);
        }

        static void AssertStablePose(int row, ActorAction baseline, ActorAction actual, string pass)
        {
            var (moving, attacking, gaze, _, _) = PoseTable[row];

            Assert.That(actual, Is.EqualTo(baseline),
                $"ResolveCompanionAction(moving: {moving}, attacking: {attacking}, " +
                $"hasGaze: {gaze}) returned {baseline} first and {actual} on {pass} — the " +
                "resolver is not pure. It runs once per companion per frame, so hidden state " +
                "makes one companion's pose depend on which companion was resolved before it");
        }

        static int CallSwingFacing(int row)
        {
            var (attacking, simFacing, _, _) = SwingFacingTable[row];
            return ActorView.ResolveCompanionSwingFacing(attacking, simFacing);
        }

        static void AssertStableSwingFacing(int row, int baseline, int actual, string pass)
        {
            var (attacking, simFacing, _, _) = SwingFacingTable[row];

            Assert.That(actual, Is.EqualTo(baseline),
                $"ResolveCompanionSwingFacing(attacking: {attacking}, simFacing: {simFacing}) " +
                $"returned {baseline} first and {actual} on {pass} — the gate is not pure. It " +
                "runs once per companion per frame, so hidden state makes one companion's yaw " +
                "depend on which companion was synced before it");
        }

        /// <summary>Builds one actor through ActorView.Create — the same factory
        /// GameView.Rent uses for a pool miss — runs the body, then destroys
        /// every actor the body added. Snapshot/DestroyImmediate pattern shared
        /// with SwingPacingTests and BossFlashYieldTests, each of which keeps its
        /// own local copy: a leaked actor keeps taking LateUpdate for the rest of
        /// the run and pollutes every later test.</summary>
        static void WithRentedActor(Action<ActorView> body)
        {
            var existingActors = new HashSet<ActorView>(
                UnityEngine.Object.FindObjectsByType<ActorView>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None));
            try
            {
                body(ActorView.Create(null, Color.red, 1f));
            }
            finally
            {
                foreach (var actor in UnityEngine.Object.FindObjectsByType<ActorView>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (!existingActors.Contains(actor))
                        UnityEngine.Object.DestroyImmediate(actor.gameObject);
                }
            }
        }

        /// <summary>The ONE reflective read in this fixture. _gazeYaw is the
        /// value SyncCompanion decides and Apply consumes; there is no accessor,
        /// so reflection is the only way to observe the swing gate's effect
        /// rather than merely its return value. Null-guarded on purpose — a
        /// silently-null FieldInfo would let every §F pin "pass" while reading
        /// nothing at all.</summary>
        static float GazeYawOf(ActorView view)
        {
            var field = typeof(ActorView).GetField(
                "_gazeYaw", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null,
                "ActorView._gazeYaw was not found by reflection — the field was renamed or " +
                "removed, so every swing-gate pin below it is now blind and would report " +
                "success without reading the yaw at all. Re-point this lookup at the new name");

            return (float)field.GetValue(view);
        }
    }
}
