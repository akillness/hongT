// §K3/§V1 element-tint contract. GameView.TryElementColor is the SINGLE source
// of truth for "which element just landed": the player's hand glow (§V1) and the
// struck enemy's hit flash (§K3) both read it, so they can never disagree.
//
// What these tests defend (and what breaks without them):
//   * the four kit literals — a nudged channel silently reskins an element;
//   * mutual distinctness — two elements sharing a tint makes the flash
//     unreadable, which is the whole point of the feature;
//   * the closed cast mask — a widened bit test would tint on ordinary combat
//     ticks (note HazardPulse is NOT PulseCast, despite the name);
//   * deterministic kit-order precedence — the enum's bit values are NOT in kit
//     order (Nova = 1<<3, Ward = 1<<4, Bolt = 1<<15, Pulse = 1<<16), so any
//     refactor to "lowest bit wins" flips Bolt/Pulse behind Nova/Ward;
//   * purity — the hand and the victim call it on different frames and must
//     resolve the same tick identically.
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class ElementTintTests
    {
        // Order IS the documented kit precedence order (GameView §K3/V1).
        static readonly (SimEvents Cast, Color Color, string Nickname)[] Kit =
        {
            (SimEvents.BoltCast,  new Color(0.75f, 0.55f, 1f),    "void violet"),
            (SimEvents.PulseCast, new Color(0.35f, 0.9f, 0.55f),  "grave green"),
            (SimEvents.NovaCast,  new Color(0.95f, 0.35f, 0.17f), "ember"),
            (SimEvents.WardCast,  new Color(0.45f, 0.85f, 1f),    "cyan ward"),
        };

        // Every flag in SimEvents that is not a skill cast. A tint on any of
        // these would flash an element the player never cast.
        static readonly SimEvents[] NonCastEvents =
        {
            SimEvents.PlayerStruck, SimEvents.EnemyHit, SimEvents.EnemyKilled,
            SimEvents.PickupCollected, SimEvents.WaveStarted, SimEvents.GameOver,
            SimEvents.PlayerDamaged, SimEvents.BossSpawned, SimEvents.StageCleared,
            SimEvents.HazardPulse, SimEvents.AltarBlessing, SimEvents.EquipDropped,
            SimEvents.DashUsed, SimEvents.LevelUp, SimEvents.EliteDown,
            SimEvents.ExtractionComplete, SimEvents.BossPhase2, SimEvents.ComboFinisher,
        };

        // Frozen literals: tight enough that a single wrong digit fails.
        const float ChannelTolerance = 1e-6f;

        // Tightest real pair is Bolt vs Ward at 0.30 max-channel separation, so
        // 0.25 passes today yet fails the instant two elements are collapsed
        // together or drifted into near-identical tints.
        const float MinElementSeparation = 0.25f;

        [Test]
        public void EachCastEvent_MapsToItsSpecifiedElementColor()
        {
            foreach (var (cast, expected, nickname) in Kit)
            {
                Assert.That(GameView.TryElementColor(cast, out var actual), Is.True,
                    $"{cast} is a skill cast and must resolve an element color");
                AssertColorMatches(expected, actual, $"{cast} ({nickname})");
                Assert.That(actual.a, Is.EqualTo(1f).Within(ChannelTolerance),
                    $"{cast} tint must be fully opaque — a transparent tint flashes nothing");
            }
        }

        [Test]
        public void ElementColors_AreMutuallyDistinct()
        {
            for (var i = 0; i < Kit.Length; i++)
            for (var j = i + 1; j < Kit.Length; j++)
            {
                Assert.That(GameView.TryElementColor(Kit[i].Cast, out var left), Is.True);
                Assert.That(GameView.TryElementColor(Kit[j].Cast, out var right), Is.True);

                var separation = MaxChannelDelta(left, right);
                Assert.That(separation, Is.GreaterThanOrEqualTo(MinElementSeparation),
                    $"{Kit[i].Cast} ({Kit[i].Nickname}) and {Kit[j].Cast} ({Kit[j].Nickname}) " +
                    $"are only {separation:F3} apart on their widest channel — the player " +
                    "could not tell which element struck them");
            }
        }

        [Test]
        public void NoCastBit_ReturnsFalseAndLeavesColorDefault()
        {
            AssertNoElement(SimEvents.None, "an empty tick");

            // Each non-cast flag alone, then the busiest possible non-cast tick.
            var allNonCast = SimEvents.None;
            foreach (var noise in NonCastEvents)
            {
                AssertNoElement(noise, $"{noise} is not a skill cast");
                allNonCast |= noise;
            }

            AssertNoElement(allNonCast, "a tick full of non-cast events");
        }

        [Test]
        public void CoincidentCasts_ResolveInDocumentedKitOrder()
        {
            // Exhaustive over all 16 subsets of the four cast bits: the first
            // kit entry present must win, alone and buried in combat noise.
            var noise = SimEvents.None;
            foreach (var flag in NonCastEvents) noise |= flag;

            for (var mask = 0; mask < 1 << 4; mask++)
            {
                var events = SimEvents.None;
                for (var bit = 0; bit < Kit.Length; bit++)
                    if ((mask & (1 << bit)) != 0) events |= Kit[bit].Cast;

                var winner = -1;
                for (var bit = 0; bit < Kit.Length && winner < 0; bit++)
                    if ((mask & (1 << bit)) != 0) winner = bit;

                if (winner < 0)
                {
                    AssertNoElement(events, "no cast bit set");
                    AssertNoElement(events | noise, "no cast bit set amid combat noise");
                    continue;
                }

                var expected = Kit[winner];

                Assert.That(GameView.TryElementColor(events, out var actual), Is.True,
                    $"[{events}] contains a cast and must resolve an element");
                AssertColorMatches(expected.Color, actual,
                    $"[{events}] must resolve to {expected.Cast} ({expected.Nickname}) by kit order");

                Assert.That(GameView.TryElementColor(events | noise, out var withNoise), Is.True,
                    $"[{events}] must still resolve while other events fire");
                AssertColorMatches(expected.Color, withNoise,
                    $"[{events}] must resolve to {expected.Cast} regardless of surrounding events");
            }
        }

        [Test]
        public void RepeatedCalls_AreStableAcrossInterleavedInputs()
        {
            // Plain repetition would miss a memoized/static-cached tint, so the
            // probe input is revisited after other inputs have been resolved.
            var probe = SimEvents.BoltCast | SimEvents.WardCast;

            Assert.That(GameView.TryElementColor(probe, out var baseline), Is.True);

            var interleaved = new[]
            {
                SimEvents.PulseCast,
                SimEvents.NovaCast,
                SimEvents.None,
                SimEvents.EnemyKilled | SimEvents.WaveStarted,
                SimEvents.WardCast,
            };

            for (var pass = 0; pass < interleaved.Length; pass++)
            {
                GameView.TryElementColor(interleaved[pass], out _);

                Assert.That(GameView.TryElementColor(probe, out var again), Is.True,
                    $"probe stopped resolving after [{interleaved[pass]}] — hidden state");
                AssertColorMatches(baseline, again,
                    $"probe drifted after resolving [{interleaved[pass]}] — hidden state");
            }
        }

        static void AssertNoElement(SimEvents events, string because)
        {
            Assert.That(GameView.TryElementColor(events, out var color), Is.False,
                $"[{events}] carries no skill cast and must not tint — {because}");
            Assert.That(color, Is.EqualTo(default(Color)),
                $"[{events}] must leave the out color default so a caller ignoring the " +
                "bool cannot flash a stale or invented element");
        }

        static void AssertColorMatches(Color expected, Color actual, string label)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(ChannelTolerance), $"{label}: red channel");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(ChannelTolerance), $"{label}: green channel");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(ChannelTolerance), $"{label}: blue channel");
        }

        static float MaxChannelDelta(Color a, Color b) => Mathf.Max(
            Mathf.Abs(a.r - b.r),
            Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));
    }
}
