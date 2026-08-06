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

        // Longer than PrepareTimeout (4 s) + FadeOutSeconds (0.6 s).
        const float SettleSeconds = 8f;

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
    }
}
