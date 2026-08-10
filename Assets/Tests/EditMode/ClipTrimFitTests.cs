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
using CinderCourt.Sim;
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
        public void DieClip_TrimFitsTheEnemyFadeWindow()
        {
            // Enemies despawn after SimConfig.EnemyFade of shrink-fade —
            // that is the ONLY window an enemy death animation ever gets.
            // Untrimmed, Shot In The Back showed 0.34 s of a 4.67 s take:
            // 7.3%, all standing preamble, no collapse (2026-08-10 review).
            // Round-trip against the sim constant so the two claims share an
            // origin (§4i) — delete the die row and this goes red.
            var seconds = TrimSeconds("die");
            Assert.That(seconds,
                Is.LessThanOrEqualTo(SimConfig.EnemyFade + 1f / SourceFps),
                $"die trim runs {seconds:0.###}s but an enemy only displays "
                + $"{SimConfig.EnemyFade}s before despawn — the tail is never seen");
            Assert.That(seconds,
                Is.GreaterThanOrEqualTo(SimConfig.EnemyFade - 2f / SourceFps),
                $"die trim runs {seconds:0.###}s — much shorter than the "
                + $"{SimConfig.EnemyFade}s fade leaves the corpse frozen mid-fall");
        }

        [Test]
        public void BigHitClip_TrimStaysInsideTheKnockbackWindow()
        {
            // BigHit is View-driven knockback only (the sim never emits it);
            // _knockbackTime's two writers are BossSlamKnockbackTime and
            // ComboKnockbackTime, so anything past the longer of the two is
            // unreachable. The row exists to pick WHICH frames display and to
            // keep a to-death take's floor contact out of a survival
            // reaction; this pins the reachable-window claim to the constants.
            var live = Mathf.Max(
                HackSpec.BossSlamKnockbackTime, HackSpec.ComboKnockbackTime);
            var seconds = TrimSeconds("bighit");
            Assert.That(seconds,
                Is.LessThanOrEqualTo(live + 1f / SourceFps),
                $"bighit trim runs {seconds:0.###}s but the state is live for "
                + $"at most {live}s — frames past that are dead weight that "
                + "reintroduces the fall of a to-death take");
        }

        [Test]
        public void WeaponFamilySwap_FileAssignmentsArePinned()
        {
            // 2026-08-10 weapon-family pass. Family is a VISUAL judgement
            // (rendered-preview triage; mesh-gen/manifest.json) that no probe
            // arithmetic re-derives, so the outcome is pinned here — revert
            // any row to its unarmed predecessor and this goes red (§4q).
            Assert.That(ClipFileFor("idle"), Is.EqualTo("Combat Idle"));
            Assert.That(ClipFileFor("bighit"), Is.EqualTo("Abdominal Hit Fall"));
            Assert.That(ClipFileFor("die"), Is.EqualTo("Shot In The Back"));
            Assert.That(ClipFileFor("show"), Is.EqualTo("Angry Ground Stomp"));
            // second round (130-178 band harvest):
            Assert.That(ClipFileFor("hit"), Is.EqualTo("Hit Reaction"));
            Assert.That(ClipFileFor("defence"), Is.EqualTo("Block Three"));
            Assert.That(ClipFileFor("cast"), Is.EqualTo("Mage Cast"));
        }

        [Test]
        public void ComboSwingWindows_AreTheMeasuredBladeSweeps()
        {
            // The hips-relative probe is BLIND on a spin: the whole body
            // rotates, so its "peak motion" landed on post-sweep footwork
            // and attack2 shipped showing legs instead of the blade
            // (world hand/foot ratio 1.8 at 30..37 vs 5.1 at 20..27 —
            // mesh-gen/swing-windows.json). These literals pin the
            // world-space measurement; move a window and re-measure there
            // first, or this stays red.
            var trims = Trims();
            var attack2 = trims.First(r => r.action == "attack2");
            Assert.That((attack2.first, attack2.last), Is.EqualTo((20, 27)),
                "attack2 must show Axe Spin's blade sweep (world-measured "
                + "20..27), not the footwork the hips-relative probe picked");
            var attack3 = trims.First(r => r.action == "attack3");
            Assert.That((attack3.first, attack3.last), Is.EqualTo((47, 57)),
                "attack3 pins the cleanest-feet swing of Weapon Combo 2 "
                + "(world-measured 47..57)");
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
        /// <summary>Every swing the player sees must play at a readable speed.
        ///
        /// ActorView.ResolvePoseSpeed squeezes each swing clip into the sim's
        /// own window (HackSpec.ComboSwing per combo index in a dungeon, the
        /// fixed 5-frame attack window in the arena) and CLAMPS at
        /// MaxPoseSpeed 4. The clamp is the trap: it is silent, and a clip that
        /// needs more than 4x does not merely play fast, it does not finish.
        ///
        /// MEASURED 2026-08-10: attack2 was 1.79 s into a 0.30 s window (4.00x,
        /// 93% shown) and attack3 3.83 s into 0.42 s (4.00x, 43% shown). Both
        /// read as twitches. The takes they displaced were in the same state,
        /// so nothing in the suite had ever asked this question.
        ///
        /// The ceiling here is 1.5x, not 4x: 4x is where the engine gives up,
        /// which is far past where a human stops reading the swing.</summary>
        [Test]
        public void SwingClipsPlayAtAReadableSpeed()
        {
            const float ReadableCeiling = 1.5f;
            // action -> the sim window that action's pose is held for.
            var windows = new (string action, float window, string where)[]
            {
                ("attack",   HackSpec.ComboSwing[0], "dungeon combo hit 1"),
                ("attack2",  HackSpec.ComboSwing[1], "dungeon combo hit 2"),
                ("attack3",  HackSpec.ComboSwing[2], "dungeon combo hit 3"),
                ("critical", HackSpec.ComboSwing[HackSpec.ComboLength - 1],
                             "dungeon finisher"),
            };

            foreach (var (action, window, where) in windows)
            {
                var file = ClipFileFor(action);
                Assert.That(file, Is.Not.Null, $"{action} is not in the clip table");
                var clip = LoadImportedClip($"{MotionDir}/{file}.fbx");
                Assert.That(clip, Is.Not.Null, $"{file}.fbx has no imported clip");

                var speed = ActorView.PoseSpeed(clip.length, window);
                Assert.That(speed, Is.LessThanOrEqualTo(ReadableCeiling),
                    $"{action} ({file}, {clip.length:F2}s) plays at {speed:F2}x in "
                    + $"the {where} window of {window:F2}s — trim it in ClipTrims "
                    + "so the swing reads");
                Assert.That(speed, Is.GreaterThanOrEqualTo(0.6f),
                    $"{action} plays at {speed:F2}x — a swing slowed this far "
                    + "drifts out of its own active frames");
            }
        }

        /// <summary>A swing must SWING: the hand has to cross an arc, not jab
        /// out and back along one ray.
        ///
        /// This exists because linear measurement could not tell the two apart.
        /// Cycle-11 replaced three weapon takes with boxing punches
        /// (Punch_Combo_1 / Right_Upper_Hook / Punch_Combo_5) and every test
        /// stayed green: a punch has real hand speed, real duration, a real
        /// motion window. The player carries a weapon bound to RightHand
        /// (ActorView.cs:390-392), so on screen the character jabbed with a
        /// sword in its fist — reported by the user, invisible to the suite.
        ///
        /// MEASURED 2026-08-10 with Assets/Editor/SwingArcProbe.cs, swept angle
        /// about the shoulder over the TRIMMED clip:
        ///   punches  136.7 / 67.6 / 106.5 deg   ->  325 / 233 / 254 deg/s
        ///   weapons  238.5 / 347.4 / 538.5 deg  ->  822 / 1198 / 1282 deg/s
        ///   the displaced Mixamo reference (Standing Melee Attack Horizontal,
        ///   trimmed 0.50 s) sweeps 212 deg = 424 deg/s
        ///
        /// The floor is 400 deg/s — just under that reference, so a take that
        /// reads as weakly as the one this project shipped for months still
        /// passes, and a punch (325 and below) does not.</summary>
        [Test]
        public void SwingClipsDescribeAnArcNotAJab()
        {
            const float MinDegreesPerSecond = 400f;
            var arcs = LoadSwingArcs();
            Assert.That(arcs, Is.Not.Empty,
                "no swing-arc measurements on disk. Run "
                + "`bash tools/unity_batch.sh method "
                + "CinderCourt.EditorTools.SwingArcProbe.Run` — without it this "
                + "test cannot tell a slash from a jab, which is exactly the "
                + "gap it was written for");

            foreach (var action in new[] { "attack", "attack2", "attack3", "critical" })
            {
                var file = ClipFileFor(action);
                Assert.That(file, Is.Not.Null, $"{action} is not in the clip table");
                Assert.That(arcs.ContainsKey(file), Is.True,
                    $"{action} -> '{file}' has no arc measurement. The take changed "
                    + "since SwingArcProbe last ran; re-run it before trusting this");

                var clip = LoadImportedClip($"{MotionDir}/{file}.fbx");
                Assert.That(clip, Is.Not.Null, $"{file}.fbx has no imported clip");
                Assert.That(clip.length, Is.GreaterThan(0f));

                var perSecond = arcs[file] / clip.length;
                Assert.That(perSecond, Is.GreaterThanOrEqualTo(MinDegreesPerSecond),
                    $"{action} ('{file}') sweeps {arcs[file]:F1} deg over "
                    + $"{clip.length:F2} s = {perSecond:F0} deg/s. A weapon swing "
                    + "has to cross an arc; this reads as a jab. Check the take "
                    + "family before the trim — the library's weapon cluster is "
                    + "ids 237-242, the punches are at 195-205");
            }
        }

        /// <summary>Swept-angle measurements from SwingArcProbe, clip -> degrees.
        /// Empty when the probe has never run.</summary>
        static System.Collections.Generic.Dictionary<string, float> LoadSwingArcs()
        {
            var path = "docs/provenance/swing-arcs.json";
            var result = new System.Collections.Generic.Dictionary<string, float>();
            if (!System.IO.File.Exists(path)) return result;
            // Small fixed shape from our own probe; a regex beats pulling in a
            // JSON dependency for two fields.
            var text = System.IO.File.ReadAllText(path);
            var matches = System.Text.RegularExpressions.Regex.Matches(text,
                "\\{\"clip\":\"(?<clip>[^\"]+)\",\"arcDegrees\":(?<deg>[-0-9.]+)");
            foreach (System.Text.RegularExpressions.Match m in matches)
                result[m.Groups["clip"].Value] = float.Parse(
                    m.Groups["deg"].Value, System.Globalization.CultureInfo.InvariantCulture);
            return result;
        }

        /// <summary>The clip Unity actually built, not the importer's intent.</summary>
        static AnimationClip LoadImportedClip(string path)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip c && !c.name.StartsWith("__preview"))
                    return c;
            return null;
        }
    }
}

