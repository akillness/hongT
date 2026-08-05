// Clip-table <-> ActorAction index alignment. CharacterImportPipeline.Clips is
// ordered data that BuildController turns into Animator wiring: for row i it
// adds an any-state transition whose only condition is `action Equals i`. The
// row's ARRAY INDEX is therefore the animator condition value, and ActorView
// drives that same parameter with SetInteger("action", (int)ActorAction.X) —
// so the enum's numeric value and the row's position must be the same number.
//
// Nothing enforces that at compile time. Reorder two rows, or insert one, and
// the project still builds, the importer still runs, and no exception is ever
// thrown — every animation from the edit onward simply plays the wrong clip.
// Dying plays the attack swing. That silent, total remap is the regression
// this fixture exists to catch.
//
// What these tests defend (and what breaks without them):
//   * per-index alignment — row i must name ActorAction i, driven off
//     Enum.GetValues so a future enum member is covered without touching this
//     file. Indexing by (int)action also pins the enum's numeric values: an
//     explicit renumber (Die = 99) demands a row 99 and fails here;
//   * the sim/View boundary — SimActionCount must equal the enum length, which
//     catches a new enum member with no row AND a bumped constant with no row;
//   * the View-only substates — indices 11/12/13 are attack2/attack3/cast, the
//     literals ActorView hardcodes as Attack2Value/Attack3Value/CastValue; an
//     inserted row anywhere below them shifts all three;
//   * uniqueness — BuildController keys its state dictionary by action name, so
//     a duplicate name makes the later row overwrite the earlier state: two
//     condition values then drive one clip and the shadowed row is unreachable.
//
// CharacterImportPipeline lives in Assets/Editor, which has NO asmdef, so it
// compiles into the predefined Assembly-CSharp-Editor. Unity forbids an asmdef
// assembly from referencing a predefined one (the dependency only runs the
// other way), so this fixture cannot bind the type at compile time no matter
// what is added to CinderCourt.Tests.EditMode.asmdef, and the View assembly's
// InternalsVisibleTo does not cover the Editor assembly either. Both assemblies
// load into the same Editor AppDomain, so the members are reachable by
// reflection — the convention BuildScriptWebGlPostprocessTests already uses for
// CinderCourt.EditorTools.BuildScript.
using System;
using System.Collections.Generic;
using System.Reflection;
using CinderCourt.Sim;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class ClipTableTests
    {
        const string PipelineTypeName = "CinderCourt.EditorTools.CharacterImportPipeline";
        const BindingFlags InternalStatic = BindingFlags.NonPublic | BindingFlags.Static;

        // The literals ActorView hardcodes (Attack2Value/Attack3Value/CastValue).
        // The sim never emits these; the View resolves them from state it already
        // owns, so they must sit immediately above the sim range.
        static readonly (int Index, string Action, string Purpose)[] ViewOnlySubstates =
        {
            (11, "attack2", "ActorView.Attack2Value — combo tier 1 swing"),
            (12, "attack3", "ActorView.Attack3Value — combo tier 2 finisher"),
            (13, "cast",    "ActorView.CastValue — skill cast pose"),
        };

        MethodInfo _actionNameAt;
        PropertyInfo _clipCount;
        FieldInfo _simActionCount;

        [OneTimeSetUp]
        public void ResolvePipelineMembers()
        {
            var pipeline = FindPipelineType();
            Assert.That(pipeline, Is.Not.Null,
                $"{PipelineTypeName} is not loaded in the Editor AppDomain — the clip table " +
                "cannot be inspected, so the animator index contract is left unpinned");

            _actionNameAt = pipeline.GetMethod("ActionNameAt", InternalStatic);
            _clipCount = pipeline.GetProperty("ClipCount", InternalStatic);
            _simActionCount = pipeline.GetField("SimActionCount", InternalStatic);

            Assert.That(_actionNameAt, Is.Not.Null,
                "CharacterImportPipeline.ActionNameAt(int) is gone — the clip table is no " +
                "longer observable, so a reorder that remaps every animation would ship unseen");
            Assert.That(_clipCount, Is.Not.Null,
                "CharacterImportPipeline.ClipCount is gone — the table length is no longer " +
                "observable, so an action with no animation row would ship unseen");
            Assert.That(_simActionCount, Is.Not.Null,
                "CharacterImportPipeline.SimActionCount is gone — the boundary separating " +
                "sim-emitted actions from View-only substates is no longer observable");
        }

        [Test]
        public void EveryActorAction_ResolvesTheClipRowAtItsOwnIndex()
        {
            var rows = ClipCount();

            foreach (ActorAction action in Enum.GetValues(typeof(ActorAction)))
            {
                // (int)action IS the animator condition value ActorView sends, so
                // this also pins the enum's numeric values, not just row order.
                var index = (int)action;

                Assert.That(index, Is.LessThan(rows),
                    $"ActorAction.{action} is value {index} but the clip table has only " +
                    $"{rows} rows — BuildController never creates a transition for {index}, " +
                    $"so the sim requesting {action} leaves the actor stuck in its last pose");

                var rowAction = ActionNameAt(index);
                Assert.That(rowAction, Is.EqualTo(action.ToString()).IgnoreCase,
                    $"clip table row {index} is '{rowAction}' but ActorAction.{action} is " +
                    $"value {index} — SetInteger(\"action\", {index}) now plays the " +
                    $"'{rowAction}' clip, so {action} and every action from row {index} " +
                    "onward are shifted onto the wrong animation");
            }
        }

        [Test]
        public void SimActionCount_MatchesTheActorActionEnumLength()
        {
            var enumLength = Enum.GetValues(typeof(ActorAction)).Length;
            var boundary = SimActionCount();

            Assert.That(boundary, Is.EqualTo(enumLength),
                $"SimActionCount is {boundary} but ActorAction has {enumLength} values — the " +
                "boundary between sim-emitted actions and View-only substates is wrong, so " +
                "either a new action has no clip row (the actor freezes when the sim requests " +
                "it) or a View-only substate is being treated as an action the sim can emit");
        }

        [Test]
        public void ViewOnlySubstates_KeepTheIndicesActorViewHardcodes()
        {
            var rows = ClipCount();
            var firstSubstate = SimActionCount();

            Assert.That(rows, Is.GreaterThanOrEqualTo(firstSubstate + ViewOnlySubstates.Length),
                $"the clip table has {rows} rows but the View needs {firstSubstate} sim rows " +
                $"plus {ViewOnlySubstates.Length} substates — a missing row means ActorView " +
                "sends a condition value no animator transition matches, and the combo or " +
                "cast pose silently never plays");

            // These indices mirror literals compiled into ActorView, so this table
            // and ActorView must be edited together — including when a new sim
            // action legitimately pushes the whole substate block up by one.
            Assert.That(ViewOnlySubstates[0].Index, Is.EqualTo(firstSubstate),
                $"the substate block starts at index {ViewOnlySubstates[0].Index} but the sim " +
                $"range now ends at {firstSubstate} — the two are adjacent by contract. If a " +
                "sim action was added, ActorView's Attack2Value/Attack3Value/CastValue " +
                $"literals must move to {firstSubstate}/{firstSubstate + 1}/{firstSubstate + 2} " +
                "and this table with them, or the combo and cast poses play the wrong clips");

            foreach (var (index, action, purpose) in ViewOnlySubstates)
            {
                var rowAction = ActionNameAt(index);
                Assert.That(rowAction, Is.EqualTo(action),
                    $"clip table row {index} is '{rowAction}', not '{action}' ({purpose}) — " +
                    $"ActorView hardcodes {index} for '{action}', so it now plays the " +
                    $"'{rowAction}' clip instead");
            }
        }

        [Test]
        public void ActionNames_AreUniqueAcrossTheWholeTable()
        {
            var rows = ClipCount();
            var firstRowFor = new Dictionary<string, int>(rows, StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < rows; index++)
            {
                var action = ActionNameAt(index);

                // Guard: a null name would crash the scan below instead of failing
                // it, and an empty one makes BuildController add a nameless state.
                Assert.That(action, Is.Not.Null.And.Not.Empty,
                    $"clip table row {index} has no action name — BuildController would add " +
                    "an animator state no clip can ever be matched to");

                if (firstRowFor.TryGetValue(action, out var earlier))
                {
                    // BuildController's state dictionary is case-SENSITIVE, so an exact
                    // repeat overwrites while a case variant instead yields two states
                    // that both answer to one ActorAction name. Both are defects.
                    var consequence = string.Equals(action, ActionNameAt(earlier), StringComparison.Ordinal)
                        ? $"BuildController keys its animator states by action name, so row {index} " +
                          $"overwrites row {earlier}'s state: condition values {earlier} and " +
                          $"{index} both drive one clip and row {earlier}'s animation is unreachable"
                        : $"the two differ only in case, so BuildController creates two animator " +
                          $"states that both answer to one action name — which clip row {earlier} " +
                          $"and row {index} each play becomes ambiguous";

                    Assert.Fail($"'{action}' names both row {earlier} and row {index} — {consequence}");
                }

                firstRowFor[action] = index;
            }
        }

        string ActionNameAt(int index) => (string)_actionNameAt.Invoke(null, new object[] { index });

        int ClipCount() => (int)_clipCount.GetValue(null);

        int SimActionCount() => (int)_simActionCount.GetRawConstantValue();

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
