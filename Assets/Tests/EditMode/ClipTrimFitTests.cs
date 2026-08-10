// Pose-window fit contract: a clip squeezed into a FIXED view window must be
// trimmed to fit it, not imported whole and cut off.
//
// This exists because the claim outlived the implementation. ActorView's
// comments asserted for months that the `show` and `cast` states were "fitted
// to this window (CharacterImportPipeline)" while no fit existed anywhere: no
// ClipTrims row, no baked state speed, no PoseValueForClip entry. Mutant
// Roaring imported whole — 5.42 s into RoarDuration's 1.1 s — so the boss
// entrance played about a fifth of a roar and cut mid-bellow, every time.
//
// Nothing caught it because nothing ASKED. The clip table tests pin row order,
// the controller tests pin wiring; the authored LENGTH of a clip against the
// window it has to live in was nobody's question (§4m — a suite can be green
// and still be unable to tell the two implementations apart).
//
// Measured, not guessed: Assets/Editor/ClipWindowProbe.cs samples the clip onto
// the humanoid rig at 1/60 s and reports the peak-motion frame. Re-run it if a
// clip is replaced — `bash tools/unity_batch.sh method
// CinderCourt.EditorTools.ClipWindowProbe.Run`.
//
// Editor-assembly reflection, same convention as ClipTableTests (see its header
// for why the type cannot be bound at compile time).
using System;
using System.Linq;
using System.Reflection;
using CinderCourt.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class ClipTrimFitTests
    {
        const string MotionDir = "Assets/Art/Motion";
        // Source frame rate of every Mixamo clip in this project.
        const float SourceFps = 24f;
        // A trimmed clip may miss its window by this much and still read as
        // "ends when the window ends" — one source frame is 41.7 ms.
        const float ToleranceSeconds = 0.05f;

        static Type Pipeline()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("CinderCourt.EditorTools.CharacterImportPipeline"))
                .FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null, "CharacterImportPipeline not in this AppDomain");
            return type;
        }

        static (string action, int first, int last)[] Trims()
        {
            var field = Pipeline().GetField("ClipTrims",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, "ClipTrims field missing");
            var rows = (Array)field.GetValue(null);
            var result = new (string, int, int)[rows.Length];
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows.GetValue(i);
                var t = row.GetType();
                result[i] = (
                    (string)t.GetField("Item1").GetValue(row),
                    (int)t.GetField("Item2").GetValue(row),
                    (int)t.GetField("Item3").GetValue(row));
            }
            return result;
        }

        static float TrimSeconds(string action)
        {
            var row = Trims().FirstOrDefault(r => r.action == action);
            Assert.That(row.action, Is.EqualTo(action),
                $"no ClipTrims row for '{action}' — the clip imports whole and "
                + "cannot fit a fixed pose window");
            return (row.last - row.first) / SourceFps;
        }

        [Test]
        public void ShowClip_IsTrimmedToTheRoarWindow()
        {
            // THE mutation-sensitive assertion. Delete the show row and this
            // goes red; before it existed, nothing in the suite noticed that
            // 5.42 s was being asked to fit in 1.1 s.
            var trimmed = TrimSeconds("show");
            Assert.That(trimmed,
                Is.EqualTo(ActorView.RoarDuration).Within(ToleranceSeconds),
                $"the roar is held for {ActorView.RoarDuration}s (ActorView.RoarDuration) "
                + $"but the trimmed clip runs {trimmed:0.###}s — a clip longer than its "
                + "window is cut off mid-motion, and one shorter leaves the boss frozen "
                + "in its last frame waiting for the window to close");
        }

        [Test]
        public void CastClip_IsTrimmedToThePoseWindow()
        {
            var trimmed = TrimSeconds("cast");
            Assert.That(trimmed,
                Is.EqualTo(ActorView.CastPoseDuration).Within(ToleranceSeconds),
                $"the cast pose is held for {ActorView.CastPoseDuration}s "
                + $"(ActorView.CastPoseDuration) but the trimmed clip runs {trimmed:0.###}s");
        }

        [Test]
        public void TrimmedClipsFitWithoutRetiming_SoTheyPlayAtSpeedOne()
        {
            // The alternative fix was a speed fit. It does not survive contact
            // with the numbers: 5.42 s into 1.1 s needs 4.9x, past ActorView's
            // MaxPoseSpeed of 4, and a 5x roar is a chirp. Asserting the ratio
            // documents WHY trimming was chosen and fails if a future trim
            // drifts far enough that only retiming could rescue it.
            foreach (var (action, window) in new[]
                     {
                         ("show", ActorView.RoarDuration),
                         ("cast", ActorView.CastPoseDuration),
                     })
            {
                var ratio = TrimSeconds(action) / window;
                Assert.That(ratio, Is.EqualTo(1f).Within(0.15f),
                    $"{action}: trimmed length is {ratio:0.###}x its window — at that "
                    + "ratio the clip needs retiming to fit, which is the mechanism "
                    + "this trim exists to avoid");
            }
        }

        /// <summary>Source take behind an action, straight out of the shipped
        /// clip table. Null when the action is not in it.</summary>
        static string ClipFileFor(string action)
        {
            var pipeline = Pipeline();
            var count = (int)pipeline
                .GetProperty("ClipCount", BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null);
            var nameAt = pipeline.GetMethod("ActionNameAt",
                BindingFlags.NonPublic | BindingFlags.Static);
            var fileAt = pipeline.GetMethod("ClipFileAt",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(fileAt, Is.Not.Null,
                "CharacterImportPipeline.ClipFileAt is how this test avoids a "
                + "second copy of the clip table");
            for (var i = 0; i < count; i++)
                if ((string)nameAt.Invoke(null, new object[] { i }) == action)
                    return (string)fileAt.Invoke(null, new object[] { i });
            return null;
        }

        [Test]
        public void TrimRowsLandInsideTheirSourceClips()
        {
            // A row that runs past the end of its clip silently imports short.
            //
            // The file behind each action is READ FROM THE PIPELINE, never
            // copied. A hardcoded list here pointed `attack` at "Standing Melee
            // Attack Horizontal" for one run after the take was replaced, so
            // the test measured a clip the game no longer loads and reported
            // the old trim as live [2026-08-10]. Same drift ClipWindowProbe
            // carried; same fix.
            var clipFiles = Trims()
                .Select(row => (row.action, file: ClipFileFor(row.action)))
                .Where(pair => pair.file != null)
                .ToArray();
            Assert.That(clipFiles.Length, Is.EqualTo(Trims().Length),
                "every trimmed action must name a take in the clip table");
            foreach (var (action, file) in clipFiles)
            {
                var row = Trims().FirstOrDefault(r => r.action == action);
                if (row.action != action) continue;   // untrimmed is legal
                var importer = (ModelImporter)AssetImporter.GetAtPath($"{MotionDir}/{file}.fbx");
                Assert.That(importer, Is.Not.Null, $"{file}.fbx not importable");
                var clips = importer.clipAnimations.Length > 0
                    ? importer.clipAnimations
                    : importer.defaultClipAnimations;
                Assert.That(clips.Length, Is.GreaterThan(0), $"{file}: no clip definitions");
                var defined = clips[0];
                Assert.That(row.first, Is.GreaterThanOrEqualTo(0), $"{action}: negative first frame");
                Assert.That(row.last, Is.GreaterThan(row.first), $"{action}: empty trim");
                Assert.That(defined.firstFrame, Is.EqualTo(row.first).Within(0.5f),
                    $"{action}: the importer did not receive the trim — run "
                    + "CharacterImportPipeline.ImportAll after changing ClipTrims");
                Assert.That(defined.lastFrame, Is.EqualTo(row.last).Within(0.5f),
                    $"{action}: importer lastFrame disagrees with the ClipTrims row");
            }
        }
    }
}
