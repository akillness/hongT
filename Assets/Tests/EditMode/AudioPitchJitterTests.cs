// AudioDirector voice-pool + pitch-jitter contract (improvement-brainstorm.md
// TOP 1). NextPitch is the deterministic view-only jitter that de-phases
// rapid one-shot retriggers so a combo burst does not buzz. WebGL requires
// AudioSource.pitch to stay strictly positive, so the bound assertions below
// are a hard contract, not decoration.
using CinderCourt.View;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class AudioPitchJitterTests
    {
        // Mirror of AudioDirector.PitchJitter — the public contract the WebGL
        // "pitch must be positive" limit rides on.
        const float Jitter = 0.06f;

        [Test]
        public void EveryDrawStaysInsideTheJitterBandAndAboveZero()
        {
            // WebGL rejects a non-positive pitch; the band [0.94,1.06] must hold
            // across a long run so no seed path can ever emit <= 0.
            uint state = 0x9E3779B9u;
            for (var i = 0; i < 100000; i++)
            {
                var pitch = AudioDirector.NextPitch(ref state);
                Assert.That(pitch, Is.GreaterThan(0f),
                    "WebGL AudioSource.pitch must stay strictly positive");
                Assert.That(pitch, Is.InRange(1f - Jitter, 1f + Jitter),
                    "jitter must never push the cue outside the recognizable band");
            }
        }

        [Test]
        public void ConsecutiveDrawsDiffer_SoRetriggersDePhase()
        {
            // The whole point is that two back-to-back cues do NOT share a
            // pitch — that is what breaks the in-phase overlap buzz.
            uint state = 0x9E3779B9u;
            var a = AudioDirector.NextPitch(ref state);
            var b = AudioDirector.NextPitch(ref state);
            var c = AudioDirector.NextPitch(ref state);
            Assert.That(a, Is.Not.EqualTo(b), "adjacent cues must not share a pitch");
            Assert.That(b, Is.Not.EqualTo(c), "adjacent cues must not share a pitch");
        }

        [Test]
        public void IsDeterministicForAGivenSeed_SoEditModeIsReproducible()
        {
            // View-only RNG: identical seed => identical stream. This is what
            // lets the sim stay frozen-deterministic while the jitter is still
            // testable.
            uint first = 12345u;
            uint second = 12345u;
            for (var i = 0; i < 64; i++)
                Assert.That(AudioDirector.NextPitch(ref first),
                    Is.EqualTo(AudioDirector.NextPitch(ref second)),
                    "same seed must reproduce the same jitter stream");
        }

        [Test]
        public void CoversBothSidesOfUnityPitch_NotAOneSidedBias()
        {
            // A jitter that only ever raised (or only lowered) pitch would drift
            // the cue's character. Over a run it must land on both sides of 1.
            uint state = 0x9E3779B9u;
            var sawBelow = false;
            var sawAbove = false;
            for (var i = 0; i < 10000 && !(sawBelow && sawAbove); i++)
            {
                var pitch = AudioDirector.NextPitch(ref state);
                if (pitch < 1f) sawBelow = true;
                else if (pitch > 1f) sawAbove = true;
            }
            Assert.That(sawBelow, Is.True, "jitter must reach below unity pitch");
            Assert.That(sawAbove, Is.True, "jitter must reach above unity pitch");
        }
    }
}
