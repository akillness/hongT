// Clip retiming: every animator state must finish exactly as its action
// window closes. CharacterImportPipeline.Clips carries a WINDOW column — the
// authoritative seconds the pose is actually held — and BuildController sets
// `state.speed = FitSpeed(clip.length, window)` so the authored motion fits
// inside it.
//
// The bench clips are Mixamo takes of 0.75-5.75 s; the frozen sim holds a
// swing for 0.30 s. At speed 1 the animator entered the state, showed the
// WIND-UP, and was yanked back to idle by the next `action` write before the
// strike frame ever rendered — measured: `attack` reached 24% of its clip,
// `attack3` 10%. There is no exception, no build error, no failing import.
// The controller is valid, the transitions fire, the parameter is written
// every frame. The only symptom is motion the player never sees: a punch with
// no contact, a dodge that is all crouch and no roll. That is what this
// fixture exists to catch, because nothing else in the project can.
//
// Trimming was NOT the answer and must not be reintroduced as one: an FBX
// curve-motion probe measured these clips at 87-94% motion, so there is no
// idle padding to cut. The sim's numbers are the gate and they are frozen
// (CLAUDE.md §2), so the VIEW retimes.
//
// What these tests defend (and what breaks without them):
//   * state coverage — every clip-table row needs a state in the Base Layer,
//     and the state count must match the table, so a row appended without a
//     RebuildController run (a STALE asset) fails here instead of leaving the
//     actor frozen when the sim requests the new action;
//   * effective duration — clip.length / state.speed must equal the row's
//     window. Asserted on the duration, not on the speed, because the
//     duration is the property that matters: the clip finishes as the window
//     closes. A speed reset to 1 makes it fail by the full clip length;
//   * authored-speed rows — idle/move/run/die must stay at exactly 1. `die`
//     especially: the player's death plays out behind the game-over panel and
//     an enemy's is carried by the 0.34 s shrink fade (SimConfig.EnemyFade),
//     so fitting it would run the 5.75 s take at ~17x — a blur, not a death;
//   * loop flags — idle/move/run loop, the rest are one-shots (CLAUDE.md §3).
//     A flipped flag changes what retiming even means: a looping one-shot
//     restarts inside its window instead of completing once;
//   * FitSpeed's degenerate inputs — a zero/negative window or a zero-length
//     clip must return 1, never Infinity or NaN. An infinite state speed does
//     not throw; it silently makes the state unplayable;
//   * window provenance — each window must equal the frozen constant it is
//     derived from. This binds the table to the constant's CURRENT value, so
//     a hardcoded literal that happened to be right when written fails the
//     moment the sim constant it duplicated moves.
//
// KNOWN MISCAST SET: six rows sit above LegibleSpeedCeiling (5x) because
// their source takes are too long to fit their window and still read as
// motion. They are pinned, not asserted under the ceiling — that assertion
// would fail today. The goal is to SHRINK this set: an art pass with shorter
// takes drops rows out of it, and each drop must be recorded here. A row that
// newly rises above the ceiling must also be recorded here, deliberately.
// `attack` at 4.17x is deliberately INSIDE the ceiling: it is the prologue's
// only combat read, so it is the one row that may not degrade to a blur.
//
// CharacterImportPipeline lives in Assets/Editor, which has NO asmdef, so it
// compiles into the predefined Assembly-CSharp-Editor. Unity forbids an
// asmdef assembly from referencing a predefined one, so this fixture cannot
// bind the type at compile time. Both assemblies load into the same Editor
// AppDomain, so the members are reachable by reflection — the convention
// ClipTableTests already uses.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class ClipWindowTests
    {
        const string PipelineTypeName = "CinderCourt.EditorTools.CharacterImportPipeline";
        const string ControllerPath = "Assets/Art/Motion/CinderActor.controller";
        const BindingFlags InternalStatic = BindingFlags.NonPublic | BindingFlags.Static;

        // Effective-duration tolerance, in seconds. Provably tight enough to
        // catch the failures that matter and loose enough to ignore float
        // round-trip noise:
        //   * a ONE-FRAME change in any source clip (1/24 s) with a stale
        //     controller shifts the effective duration by (1/24)/speed, worst
        //     case (1/24)/10.02 = 4.2 ms on `attack3` — 4x this tolerance;
        //   * a speed reset to 1 shifts it by the full clip length, 0.75 s+;
        //   * the controller serializes m_Speed to ~7 significant digits, so
        //     reading it back costs ~3e-7 s of error — 3000x below this.
        // It is also 0.55% of the tightest window (bighit, 0.18 s).
        const float DurationTolerance = 1e-3f;

        // Loop set per CLAUDE.md §3: "idle/move/run 루프, 나머지 원샷".
        // Sourced from the repo contract rather than from the clip table, so
        // a table edit that flips a flag is caught instead of ratified.
        static readonly string[] LoopRows = { "idle", "move", "run" };

        // Rows that keep authored speed. Loops, plus `die` — see the header.
        static readonly string[] AuthoredSpeedRows = { "idle", "move", "run", "die" };

        // Rows currently fitted above LegibleSpeedCeiling. Pinned, not
        // asserted away. Shrinking this set is the goal; changing it is a
        // deliberate edit, never a silent drift.
        static readonly string[] KnownMiscastRows =
        {
            "bighit", "critical", "avoid", "attack2", "attack3", "cast",
        };

        // Every window must be the frozen constant it is derived from, never a
        // literal that duplicates its value. CinderCourt.Sim and
        // CinderCourt.View are both referenced by this test assembly, so the
        // constants are read here exactly as the clip table reads them.
        static readonly (string Action, float Expected, string Source)[] WindowSources =
        {
            ("idle",     0f,                                            "authored loop — no window"),
            ("move",     0f,                                            "authored loop — no window"),
            ("run",      0f,                                            "authored loop — no window"),
            ("hit",      SimConfig.PlayerHitGrace,                      "SimConfig.PlayerHitGrace"),
            ("bighit",   HackSpec.ComboKnockbackTime,                   "HackSpec.ComboKnockbackTime"),
            ("attack",   HackSpec.ComboSwing[0],                        "HackSpec.ComboSwing[0]"),
            ("critical", HackSpec.ComboSwing[HackSpec.ComboLength - 1], "HackSpec.ComboSwing[HackSpec.ComboLength - 1]"),
            ("avoid",    HackSpec.DashTime,                             "HackSpec.DashTime"),
            ("defence",  SimConfig.WardDuration,                        "SimConfig.WardDuration"),
            ("die",      0f,                                            "authored speed under the SimConfig.EnemyFade shrink"),
            ("show",     ActorView.RoarDuration,                        "ActorView.RoarDuration"),
            ("attack2",  HackSpec.ComboSwing[1],                        "HackSpec.ComboSwing[1]"),
            ("attack3",  HackSpec.ComboSwing[2],                        "HackSpec.ComboSwing[2]"),
            ("cast",     ActorView.CastPoseDuration,                    "ActorView.CastPoseDuration"),
        };

        MethodInfo _actionNameAt;
        MethodInfo _fitSpeed;
        MethodInfo _windowAt;
        PropertyInfo _clipCount;
        FieldInfo _legibleSpeedCeiling;

        Dictionary<string, AnimatorState> _states;

        [OneTimeSetUp]
        public void ResolvePipelineMembers()
        {
            var pipeline = FindPipelineType();
            Assert.That(pipeline, Is.Not.Null,
                $"{PipelineTypeName} is not loaded in the Editor AppDomain — the clip table's " +
                "window column cannot be inspected, so the retiming contract is left unpinned " +
                "and a truncated clip would ship as valid");

            _actionNameAt = pipeline.GetMethod("ActionNameAt", InternalStatic);
            _windowAt = pipeline.GetMethod("WindowAt", InternalStatic);
            _fitSpeed = pipeline.GetMethod("FitSpeed", InternalStatic);
            _clipCount = pipeline.GetProperty("ClipCount", InternalStatic);
            _legibleSpeedCeiling = pipeline.GetField("LegibleSpeedCeiling", InternalStatic);

            Assert.That(_actionNameAt, Is.Not.Null,
                "CharacterImportPipeline.ActionNameAt(int) is gone — clip-table rows can no " +
                "longer be matched to animator states, so a row with no state would ship unseen");
            Assert.That(_windowAt, Is.Not.Null,
                "CharacterImportPipeline.WindowAt(int) is gone — the authoritative hold time per " +
                "row is no longer observable, so a state cut off before its strike frame renders " +
                "cannot be distinguished from a correctly fitted one");
            Assert.That(_fitSpeed, Is.Not.Null,
                "CharacterImportPipeline.FitSpeed(float, float) is gone — the retiming rule is no " +
                "longer observable, so its degenerate inputs (zero window, zero-length clip) are " +
                "unguarded and can yield an infinite state speed");
            Assert.That(_clipCount, Is.Not.Null,
                "CharacterImportPipeline.ClipCount is gone — the table length is no longer " +
                "observable, so a row appended without a controller rebuild would ship unseen");
            Assert.That(_legibleSpeedCeiling, Is.Not.Null,
                "CharacterImportPipeline.LegibleSpeedCeiling is gone — the marker separating a " +
                "fast-but-legible strike from a blur is no longer observable, so the known " +
                "miscast set cannot be tracked and would silently grow");
        }

        [Test]
        public void EveryClipRow_HasAStateInTheControllersBaseLayer()
        {
            var states = States();
            var rows = ClipCount();

            for (var index = 0; index < rows; index++)
            {
                var action = ActionNameAt(index);
                Assert.That(states.ContainsKey(action), Is.True,
                    $"clip-table row {index} is '{action}' but {ControllerPath} has no such " +
                    $"state — BuildController adds an any-state transition on action == {index} " +
                    $"with nothing to enter, so the sim requesting '{action}' leaves the actor " +
                    "stuck in its previous pose. Run CinderCourt/Rebuild Animator Controller");
            }

            // A row count mismatch is the signature of a STALE asset: the table
            // was edited and the controller was never regenerated.
            Assert.That(states.Count, Is.EqualTo(rows),
                $"{ControllerPath} has {states.Count} states but the clip table has {rows} rows " +
                $"— the asset was not rebuilt after the table changed, so every window fitted " +
                "in this controller reflects the OLD table. Run CinderCourt/Rebuild Animator " +
                $"Controller. States: {string.Join(", ", states.Keys.OrderBy(k => k))}");
        }

        [Test]
        public void EveryWindowedState_FinishesExactlyAsItsWindowCloses()
        {
            var states = States();
            var rows = ClipCount();
            var windowed = 0;

            for (var index = 0; index < rows; index++)
            {
                var action = ActionNameAt(index);
                var window = WindowAt(index);
                if (window <= 0f) continue;

                windowed += 1;
                var state = RequireState(states, action);
                var clip = RequireClip(state, action);

                // Guard before dividing: a zero or negative speed is its own
                // defect and would otherwise surface as Infinity/NaN.
                Assert.That(state.speed, Is.GreaterThan(0f),
                    $"state '{action}' has speed {state.speed} — a non-positive state speed " +
                    "stops or reverses the clip, so the pose never plays through its window");

                var effective = clip.length / state.speed;
                Assert.That(effective, Is.EqualTo(window).Within(DurationTolerance),
                    $"state '{action}' plays for {effective:F3} s but its window is " +
                    $"{window:F3} s (clip {clip.length:F3} s at {state.speed:F4}x). The clip " +
                    $"must finish exactly as the window closes; at this speed only " +
                    $"{Mathf.Clamp01(window / effective) * 100f:F0}% of it renders before the " +
                    $"sim rewrites `action` and yanks the actor back to idle. Expected speed " +
                    $"{FitSpeed(clip.length, window):F4}x — run CinderCourt/Rebuild Animator " +
                    "Controller, or retune the row's window");
            }

            // Positive control: if every window were zeroed, the loop above
            // would assert nothing and still pass.
            Assert.That(windowed, Is.GreaterThan(0),
                "no clip-table row has a window — every state now runs at authored speed, so " +
                "every one-shot is cut off the moment the sim rewrites `action`");
        }

        [Test]
        public void AuthoredSpeedRows_AreNeverFitted()
        {
            var states = States();
            var rows = ClipCount();
            var zeroWindow = new List<string>();

            for (var index = 0; index < rows; index++)
            {
                if (WindowAt(index) <= 0f) zeroWindow.Add(ActionNameAt(index));
            }

            CollectionAssert.AreEquivalent(AuthoredSpeedRows, zeroWindow,
                $"the zero-window rows are [{string.Join(", ", zeroWindow)}] but the contract is " +
                $"[{string.Join(", ", AuthoredSpeedRows)}]. A row that GAINED a window is now " +
                "retimed to a number nobody chose for it; a row that LOST one is cut off before " +
                "its motion renders");

            foreach (var action in AuthoredSpeedRows)
            {
                var state = RequireState(states, action);
                Assert.That(state.speed, Is.EqualTo(1f),
                    $"state '{action}' runs at {state.speed:F4}x, not authored speed. Its clip " +
                    "is meant to play as authored — fitting it compresses motion nobody asked " +
                    "to compress");
            }

            // `die` is the row most likely to be "fixed" by someone wiring it
            // to the fade it appears to belong to. Spell out the cost.
            var die = RequireState(states, "die");
            var dieClip = RequireClip(die, "die");
            Assert.That(die.speed, Is.EqualTo(1f),
                $"state 'die' runs at {die.speed:F4}x. It must stay at authored speed: the " +
                "player's death plays out behind the game-over panel, and an enemy's is carried " +
                $"by the {SimConfig.EnemyFade:F2} s shrink fade (SimConfig.EnemyFade), not by the " +
                $"clip. Fitting the {dieClip.length:F2} s take to that fade would run it at " +
                $"{dieClip.length / SimConfig.EnemyFade:F0}x — a blur, not a death");
        }

        [Test]
        public void LoopFlags_MatchTheActionLibraryContract()
        {
            var states = States();
            var rows = ClipCount();

            for (var index = 0; index < rows; index++)
            {
                var action = ActionNameAt(index);
                var state = RequireState(states, action);
                var clip = RequireClip(state, action);
                var shouldLoop = Array.IndexOf(LoopRows, action) >= 0;

                var consequence = shouldLoop
                    ? "a loop that stops holds its last frame, so the actor freezes mid-stride " +
                      "while the sim still reports it moving"
                    : $"a one-shot that loops restarts inside its {WindowAt(index):F3} s window " +
                      "instead of completing once, so the pose reads as a stutter and the " +
                      "return-to-idle exit time lands on the wrong frame";

                Assert.That(clip.isLooping, Is.EqualTo(shouldLoop),
                    $"clip '{action}' has isLooping == {clip.isLooping}, expected {shouldLoop} " +
                    $"(CLAUDE.md §3: idle/move/run loop, the rest are one-shots) — {consequence}");
            }
        }

        [Test]
        public void EveryWindow_MatchesTheFrozenConstantItIsDerivedFrom()
        {
            var rows = ClipCount();
            var expected = WindowSources.ToDictionary(w => w.Action, StringComparer.Ordinal);

            Assert.That(rows, Is.EqualTo(WindowSources.Length),
                $"the clip table has {rows} rows but this fixture pins {WindowSources.Length} " +
                "window sources — a new row must declare where its window comes from, or it can " +
                "carry a literal nobody traced back to the sim");

            for (var index = 0; index < rows; index++)
            {
                var action = ActionNameAt(index);
                Assert.That(expected.ContainsKey(action), Is.True,
                    $"clip-table row {index} is '{action}', which this fixture does not pin — " +
                    "add it to WindowSources with the constant its window is derived from");

                var (_, value, source) = expected[action];
                Assert.That(WindowAt(index), Is.EqualTo(value),
                    $"row '{action}' has window {WindowAt(index):F4} s but {source} is " +
                    $"{value:F4} s. The window must be READ from that constant, not duplicated " +
                    "as a literal: a literal that was right when written silently detaches the " +
                    "moment the constant moves, and the state is then fitted to a hold time the " +
                    "sim no longer uses");
            }
        }

        [Test]
        public void KnownMiscastRows_AreExactlyTheRowsAboveTheLegibleCeiling()
        {
            var states = States();
            var rows = ClipCount();
            var ceiling = LegibleSpeedCeiling();
            var miscast = new List<string>();

            for (var index = 0; index < rows; index++)
            {
                var action = ActionNameAt(index);
                if (WindowAt(index) <= 0f) continue;
                if (RequireState(states, action).speed > ceiling) miscast.Add(action);
            }

            CollectionAssert.AreEquivalent(KnownMiscastRows, miscast,
                $"rows above the {ceiling:F1}x legible ceiling are " +
                $"[{string.Join(", ", miscast)}] but this fixture pins " +
                $"[{string.Join(", ", KnownMiscastRows)}]. These are source clips too long to " +
                "fit their window and still read as motion; the set is pinned so the change is " +
                "deliberate. A row that DROPPED out was fixed by a shorter take — remove it here " +
                "and keep it out. A row that was ADDED newly degraded to a blur — either shorten " +
                "its source take or record the regression here on purpose");

            // The prologue's only combat read may not degrade to a blur.
            var attack = RequireState(states, "attack");
            Assert.That(attack.speed, Is.LessThanOrEqualTo(ceiling),
                $"state 'attack' runs at {attack.speed:F2}x, above the {ceiling:F1}x legible " +
                "ceiling. It is the prologue's only combat read and serves both the arena swing " +
                "and dungeon combo hits 0-1 — it is the one row that must stay legible, so its " +
                "source take has to get shorter rather than its speed higher");
        }

        [Test]
        public void FitSpeed_ReturnsAuthoredSpeedForDegenerateInputs()
        {
            // A missing or unwindowed clip must never divide by zero. An
            // infinite state speed does not throw; it silently makes the
            // state unplayable, which reads as "the animation is gone".
            Assert.That(FitSpeed(1.25f, 0f), Is.EqualTo(1f),
                "FitSpeed with a zero window must return authored speed — a 0-window row is a " +
                "loop or `die`, and dividing by it yields Infinity");
            Assert.That(FitSpeed(1.25f, -0.5f), Is.EqualTo(1f),
                "FitSpeed with a negative window must return authored speed — a negative speed " +
                "plays the clip backwards");
            Assert.That(FitSpeed(0f, 0.30f), Is.EqualTo(1f),
                "FitSpeed with a zero-length clip must return authored speed — a missing or " +
                "empty clip would otherwise fit to 0x and freeze the state on frame 0");
            Assert.That(FitSpeed(0f, 0f), Is.EqualTo(1f),
                "FitSpeed with both inputs degenerate must return authored speed, not NaN");

            // Positive control: FitSpeed returning 1 unconditionally would
            // satisfy every assertion above while disabling all retiming.
            Assert.That(FitSpeed(1.25f, 0.30f), Is.EqualTo(1.25f / 0.30f).Within(1e-5f),
                "FitSpeed must still fit a real clip to a real window — a 1.25 s take held for " +
                "0.30 s needs 4.17x, and returning 1 here restores the truncation this whole " +
                "contract exists to prevent");
        }

        Dictionary<string, AnimatorState> States()
        {
            if (_states != null) return _states;

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.That(controller, Is.Not.Null,
                $"{ControllerPath} is missing — every character prefab references this shared " +
                "controller, so no actor animates at all. Run CinderCourt/Rebuild Animator " +
                "Controller");
            Assert.That(controller.layers.Length, Is.GreaterThan(0),
                $"{ControllerPath} has no layers — BuildController writes every state into the " +
                "Base Layer state machine, so an empty controller means the build never ran");

            var machine = controller.layers[0].stateMachine;
            Assert.That(machine, Is.Not.Null,
                $"{ControllerPath} layer 0 has no state machine — there is nowhere for the " +
                "action states to live");

            var map = new Dictionary<string, AnimatorState>(StringComparer.Ordinal);
            foreach (var child in machine.states)
            {
                if (child.state != null) map[child.state.name] = child.state;
            }

            _states = map;
            return _states;
        }

        static AnimatorState RequireState(IReadOnlyDictionary<string, AnimatorState> states, string action)
        {
            Assert.That(states.ContainsKey(action), Is.True,
                $"{ControllerPath} has no '{action}' state — the sim can request that action " +
                "and no transition will match, leaving the actor stuck in its previous pose");
            return states[action];
        }

        static AnimationClip RequireClip(AnimatorState state, string action)
        {
            var clip = state.motion as AnimationClip;
            Assert.That(clip, Is.Not.Null,
                $"state '{action}' has no AnimationClip motion (motion is " +
                $"{(state.motion == null ? "null" : state.motion.GetType().Name)}) — the state " +
                "is entered and nothing plays, so the actor holds its previous pose in silence");
            return clip;
        }

        string ActionNameAt(int index) => (string)_actionNameAt.Invoke(null, new object[] { index });

        float WindowAt(int index) => (float)_windowAt.Invoke(null, new object[] { index });

        float FitSpeed(float clipLength, float window)
            => (float)_fitSpeed.Invoke(null, new object[] { clipLength, window });

        int ClipCount() => (int)_clipCount.GetValue(null);

        float LegibleSpeedCeiling() => (float)_legibleSpeedCeiling.GetRawConstantValue();

        static Type FindPipelineType()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index += 1)
            {
                var type = assemblies[index].GetType(PipelineTypeName, false);
                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
