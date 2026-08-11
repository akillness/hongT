// Boot-safety gate for the branded intro reel (IntroVideoView).
//
// The invariant under test is NOT "the video plays" — EditMode batchmode has no
// decoder — it is "the intro can never brick the boot route": however the clip
// behaves (absent, undecodable, slow to prepare), driving the view forward must
// always deactivate it and report completion exactly once.
using System;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    public sealed class IntroVideoViewTests
    {
        GameObject _root;
        IntroVideoView _intro;

        // DERIVED from the view's own constants, not restated. These used to be a
        // literal 8 f with a comment naming "PrepareTimeout (4 s)"; raising that
        // timeout to fix the intermittent boot reel would have broken every test
        // here for a reason that has nothing to do with what they assert
        // (CLAUDE.md §4i). The margin covers the fade plus a few Step increments.
        static readonly float SettleSeconds =
            IntroVideoView.PrepareTimeout + IntroVideoView.FadeOutSecondsForTest + 2f;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("IntroVideoViewTests");
            _intro = _root.AddComponent<IntroVideoView>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        }

        void Drive(float seconds, float dt = 0.25f)
        {
            for (var t = 0f; t < seconds; t += dt) _intro.Step(dt);
        }

        [Test]
        public void IdleBeforePlay()
        {
            Assert.That(_intro.Active, Is.False,
                "a freshly constructed intro must not cover the screen");
        }

        [Test]
        public void StepWhileIdleNeverReportsCompletion()
        {
            var finished = 0;
            _intro.OnFinished = () => finished++;

            Drive(SettleSeconds);

            Assert.That(finished, Is.Zero,
                "an intro that was never played must not raise OnFinished");
            Assert.That(_intro.Active, Is.False);
        }

        [Test]
        public void PlayCoversTheScreen()
        {
            _intro.Play();
            Assert.That(_intro.Active, Is.True,
                "Play must immediately cover the screen so the boot frame is never raw");
        }

        [Test]
        public void AlwaysFinishesEvenWithoutAPlayableClip()
        {
            var finished = 0;
            _intro.OnFinished = () => finished++;

            _intro.Play();
            Drive(SettleSeconds);

            Assert.That(_intro.Active, Is.False,
                "a missing or undecodable clip must never leave the intro on screen");
            Assert.That(finished, Is.EqualTo(1),
                "completion must be reported exactly once per Play cycle");
        }

        [Test]
        public void SkipFinishesAfterTheFadeAndOnlyOnce()
        {
            var finished = 0;
            _intro.OnFinished = () => finished++;

            _intro.Play();
            _intro.Skip();
            Drive(SettleSeconds);

            Assert.That(_intro.Active, Is.False);
            Assert.That(finished, Is.EqualTo(1),
                "skipping must not double-report completion");
        }

        [Test]
        public void SkipWhileIdleIsANoOp()
        {
            var finished = 0;
            _intro.OnFinished = () => finished++;

            _intro.Skip();

            Assert.That(_intro.Active, Is.False);
            Assert.That(finished, Is.Zero,
                "skipping an intro that is not running must not raise OnFinished");
        }

        [Test]
        public void HideTearsDownWithoutReportingCompletion()
        {
            var finished = 0;
            _intro.OnFinished = () => finished++;

            _intro.Play();
            _intro.Hide();

            Assert.That(_intro.Active, Is.False,
                "Hide must clear the overlay instantly");
            Assert.That(finished, Is.Zero,
                "Hide is a silent teardown, not a completion");
        }

        [Test]
        public void ReplayRearmsAndReportsAgain()
        {
            var finished = 0;
            _intro.OnFinished = () => finished++;

            _intro.Play();
            Drive(SettleSeconds);
            Assert.That(finished, Is.EqualTo(1));

            _intro.Play();
            Assert.That(_intro.Active, Is.True,
                "a second Play must re-arm the overlay");
            Drive(SettleSeconds);

            Assert.That(_intro.Active, Is.False);
            Assert.That(finished, Is.EqualTo(2),
                "each completed Play cycle reports exactly once");
        }

        [Test]
        public void ClipStreamsFromStreamingAssetsRelativePath()
        {
            Assert.That(IntroVideoView.ClipRelativePath,
                Is.EqualTo("Video/cinder-court-intro.mp4"),
                "the brand reel path is the deploy contract with the build step");
            Assert.That(IntroVideoView.ClipUrl,
                Does.StartWith(Application.streamingAssetsPath),
                "WebGL VideoPlayer can only stream from StreamingAssets, not Resources");
            Assert.That(IntroVideoView.ClipUrl,
                Does.EndWith(IntroVideoView.ClipRelativePath.Replace('/', System.IO.Path.DirectorySeparatorChar)));
        }

        /// <summary>The boot route plays two clips through one surface. The
        /// caller's contract is exactly-once completion for the whole
        /// sequence, so a per-clip Finish would double-fire it and hand the
        /// route back while the second reel was still queued.</summary>
        [Test]
        public void SequenceReportsCompletionOnceForAllClips()
        {
            var finished = 0;
            _intro.OnFinished = () => finished++;

            _intro.PlaySequence(
                new IntroVideoView.Beat(IntroVideoView.ClipRelativePath),
                new IntroVideoView.Beat(IntroVideoView.ConceptClipRelativePath, "테스트 내레이션"));
            // Each clip times out after PrepareTimeout with no decoder, so the pair
            // drains in roughly twice SettleSeconds. Step in small increments and record
            // what actually happened rather than guessing a window: the thing
            // that proves the queue advanced is that the surface stayed up
            // ACROSS the first clip's completion.
            var stayedUpAfterFirstFinish = false;
            var finishedDuringDrive = 0;
            for (var t = 0f; t < SettleSeconds * 3f; t += 0.25f)
            {
                var wasActive = _intro.Active;
                _intro.Step(0.25f);
                // Still up, still nothing reported, and at least one clip's
                // worth of time gone: only possible if a second clip started.
                if (wasActive && _intro.Active && finished == 0 && t > 5f)
                    stayedUpAfterFirstFinish = true;
                finishedDuringDrive = finished;
            }

            Assert.That(stayedUpAfterFirstFinish, Is.True,
                "the surface must still be up past the first clip's timeout - "
                + "this is what proves the queue advanced rather than drained");
            Assert.That(finishedDuringDrive, Is.EqualTo(1),
                "a two-clip sequence reports completion exactly once");

            Drive(SettleSeconds);

            Assert.That(_intro.Active, Is.False,
                "the surface must come down when the sequence drains");
        }

        /// <summary>Skip means skip the intro, not advance to the next clip:
        /// a player tapping through the brand reel does not want the concept
        /// reel to start.</summary>
        [Test]
        public void SkipAbandonsTheWholeSequence()
        {
            var finished = 0;
            _intro.OnFinished = () => finished++;

            _intro.PlaySequence(
                new IntroVideoView.Beat(IntroVideoView.ClipRelativePath),
                new IntroVideoView.Beat(IntroVideoView.ConceptClipRelativePath));
            _intro.Skip();
            Drive(SettleSeconds);

            Assert.That(finished, Is.EqualTo(1));
            Assert.That(_intro.Active, Is.False,
                "skipping mid-sequence must not leave the second clip playing");
        }

        /// <summary>An empty or null sequence must still behave like the plain
        /// boot path rather than stranding the caller with no completion.</summary>
        [Test]
        public void EmptySequenceFallsBackToTheBrandReel()
        {
            var finished = 0;
            _intro.OnFinished = () => finished++;

            _intro.PlaySequence(System.Array.Empty<IntroVideoView.Beat>());
            Drive(SettleSeconds);

            Assert.That(finished, Is.EqualTo(1));
            Assert.That(_intro.Active, Is.False);
        }

        /// <summary>A beat's caption is optional and the brand logo has none.
        /// A sequence must survive both, because a null caption on the first
        /// beat is exactly what the boot route passes.</summary>
        [Test]
        public void BeatsCarryOptionalNarrationAndStillComplete()
        {
            var finished = 0;
            _intro.OnFinished = () => finished++;

            _intro.PlaySequence(
                new IntroVideoView.Beat(IntroVideoView.ClipRelativePath),
                new IntroVideoView.Beat(IntroVideoView.ConceptClipRelativePath,
                                        "등불 하나가 잿불의 법정을 건넙니다."),
                new IntroVideoView.Beat(IntroVideoView.ThreatClipRelativePath, null));
            Drive(SettleSeconds * 4f);

            Assert.That(finished, Is.EqualTo(1),
                "three beats, mixed captions, still exactly one completion");
            Assert.That(_intro.Active, Is.False);
        }

        /// <summary>Beat is a value type with the clip first: the boot route
        /// constructs them positionally and a reordered signature would compile
        /// while playing the caption as a URL.</summary>
        [Test]
        public void BeatKeepsClipFirstAndNarrationOptional()
        {
            var picture = new IntroVideoView.Beat("Video/x.mp4");
            Assert.That(picture.Clip, Is.EqualTo("Video/x.mp4"));
            Assert.That(picture.Narration, Is.Null,
                "a beat with no caption must not invent one");

            var spoken = new IntroVideoView.Beat("Video/y.mp4", "한 줄");
            Assert.That(spoken.Clip, Is.EqualTo("Video/y.mp4"));
            Assert.That(spoken.Narration, Is.EqualTo("한 줄"));
        }

        /// <summary>Act cinematics ship and are distinct. Same argument as the
        /// boot reels: a missing clip degrades to silence by design, so this is
        /// the only thing that can report it.</summary>
        [Test]
        public void ActCinematicsShipAndAreDistinct()
        {
            var acts = new[]
            {
                IntroVideoView.Act1ClipRelativePath,
                IntroVideoView.Act2ClipRelativePath,
                IntroVideoView.Act3ClipRelativePath,
            };
            Assert.That(acts, Is.Unique, "three acts, three reels");
            foreach (var reel in acts)
            {
                var path = System.IO.Path.Combine(Application.streamingAssetsPath, reel);
                Assert.That(System.IO.File.Exists(path), Is.True, $"missing act reel: {path}");
                Assert.That(new System.IO.FileInfo(path).Length, Is.GreaterThan(100_000),
                    $"{reel} is a git-lfs pointer or truncated, not a video");
            }
        }

        /// <summary>A missing clip degrades to "no intro" in complete silence
        /// by design, so nothing at runtime will ever tell us a story reel
        /// failed to ship. This is the only place that can.</summary>
        [Test]
        public void EveryBootReelShipsInStreamingAssets()
        {
            var reels = new[]
            {
                IntroVideoView.ClipRelativePath,
                IntroVideoView.ConceptClipRelativePath,
                IntroVideoView.ThreatClipRelativePath,
            };
            Assert.That(reels, Is.Unique,
                "a sequence that plays one clip three times is not a sequence");

            foreach (var reel in reels)
            {
                Assert.That(IntroVideoView.UrlFor(reel),
                    Does.StartWith(Application.streamingAssetsPath),
                    "WebGL VideoPlayer can only stream from StreamingAssets");
                var path = System.IO.Path.Combine(Application.streamingAssetsPath, reel);
                Assert.That(System.IO.File.Exists(path), Is.True, $"missing reel: {path}");
                // git-lfs pointers read as present but are ~130 bytes of text
                // (CLAUDE.md §107). A pointer satisfies File.Exists and then
                // fails to decode in the browser with no build-time signal.
                Assert.That(new System.IO.FileInfo(path).Length,
                    Is.GreaterThan(100_000),
                    $"{reel} is a git-lfs pointer or truncated, not a video");
            }
        }

        /// <summary>Regression: the project runs the Input System package, so
        /// any read of the legacy UnityEngine.Input class throws
        /// InvalidOperationException every frame — observed in play mode as a
        /// per-frame exception out of Update that froze the intro on screen and
        /// hid the game underneath it. Update must stay device-agnostic and
        /// survive a machine with no attached input devices (batchmode).</summary>
        [Test]
        public void UpdateNeverReadsLegacyInput()
        {
            var update = typeof(IntroVideoView).GetMethod(
                "Update",
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public);
            Assert.That(update, Is.Not.Null, "IntroVideoView must keep an Update loop");

            _intro.Play();   // phase leaves Idle, so Update does real work

            Assert.DoesNotThrow(() => update.Invoke(_intro, null),
                "reading input must go through the Input System package");
            Assert.That(_intro.Active, Is.True,
                "a single Update must not tear the intro down on its own");
        }

    }
}
