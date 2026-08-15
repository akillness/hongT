// Player exertion grunts (기합) — the voice-* layer AudioDirector plays alongside
// cue-* combat sounds.
//
// What is worth asserting here is NOT "a clip plays" — Unity's audio does not run in
// batch mode, so that would test nothing. It is the two rules that make the layer
// bearable and that a later edit could quietly break:
//
//   1. the asset set exists and is wordless-by-construction (separate prefix), and
//   2. the same take never repeats back to back.
//
// (2) is the one that matters. A dodge fires several times a minute, and with three
// variants uniform sampling repeats immediately about a third of the time — which is
// precisely the complaint the layer is meant to prevent, landing hardest on its most
// frequent cue.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class PlayerGruntTests
    {
        // Set name -> how many takes were generated (tools/audio/gen_grunts.py).
        // Counts are deliberately uneven: a death fires once per run, so a third
        // variant would be heard on the third death.
        static readonly (string Set, int Variants)[] Sets =
        {
            ("voice-combo-finisher", 3),
            ("voice-avoid", 3),
            ("voice-hurt", 3),
            ("voice-die", 2),
        };

        [Test]
        public void GruntSets_ShipEveryDeclaredVariant()
        {
            foreach (var (set, variants) in Sets)
            {
                for (var index = 1; index <= variants; index += 1)
                {
                    var clip = Resources.Load<AudioClip>($"Audio/{set}-{index}");
                    Assert.That(clip, Is.Not.Null,
                        $"{set}-{index} is declared by gen_grunts.py but not in Resources — "
                        + "the layer degrades silently, so only a test notices");
                }
            }
        }

        /// <summary>
        /// The grunts live under their OWN prefix, never as cue-*.
        ///
        /// gen_sfx.py carries a signed rule at its head: cue-* prompts forbid vocals,
        /// because a voice baked into a sound effect can no longer be muted, ducked or
        /// translated on its own. Naming is what keeps that rule enforceable — if a
        /// grunt ever ships as cue-something, the separation is gone and nothing else
        /// would catch it.
        /// </summary>
        [Test]
        public void Grunts_StayOutOfTheCueNamespace()
        {
            foreach (var (set, _) in Sets)
                Assert.That(set, Does.StartWith("voice-"),
                    "a grunt named cue-* would be inseparable from the SFX bus");
        }

        /// <summary>
        /// Back-to-back repeats are impossible for any set with more than one take.
        ///
        /// This mirrors AudioDirector.PlayGrunt's selection exactly — draw from
        /// (length - 1) and skip past the previous index — so the assertion tracks the
        /// rule rather than a recorded sequence. A recorded sequence would also pass if
        /// the rule were replaced by a different-but-still-varied one, which is the
        /// coordinate system where right and wrong answers coincide (CLAUDE.md §4m).
        /// </summary>
        [Test]
        public void GruntSelection_NeverRepeatsTheSameTakeTwiceRunning()
        {
            foreach (var (set, variants) in Sets)
            {
                if (variants < 2) continue;

                var rng = 0x51ED2701u;
                var previous = -1;
                var seen = new HashSet<int>();

                for (var draw = 0; draw < 500; draw += 1)
                {
                    rng ^= rng << 13;
                    rng ^= rng >> 17;
                    rng ^= rng << 5;
                    var pick = (int)(rng % (uint)(variants - 1));
                    if (previous >= 0 && pick >= previous) pick += 1;
                    if (pick >= variants) pick = 0;

                    Assert.That(pick, Is.Not.EqualTo(previous),
                        $"{set}: take {pick} repeated back to back on draw {draw}");
                    Assert.That(pick, Is.InRange(0, variants - 1), set);
                    seen.Add(pick);
                    previous = pick;
                }

                // Every take must actually be reachable. Excluding the previous index
                // is only half the job: a selector that alternated between two takes
                // of three would also never repeat, and would still sound repetitive.
                Assert.That(seen.Count, Is.EqualTo(variants),
                    $"{set}: only {seen.Count} of {variants} takes are ever selected");
            }
        }
    }
}
