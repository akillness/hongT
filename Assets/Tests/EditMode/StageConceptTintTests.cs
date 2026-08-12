// EditMode gates for the stage-concept hue pass.
//
// Lineage, precisely: pale-ring-investigation.md was closed by TWO findings on
// 2026-08-13 — the renderer census named the ring's renderers (env-wall slabs,
// env-stone), and bc799ee9 found the mechanism (procedural meshes had no
// normals, so rim=pow(1,x)=1 covered whole faces). That commit fixes the PALE
// ring. What remains after it — and what THESE tests gate — are two stage-blind
// constants: ToonLit's default rim hue (0.72, 0.78, 1.0) on every silhouette
// edge, and the VoidFloor's baked purple-grey outskirt (frame share disputed:
// 0.25% CameraRig bake vs 4.75% investigation bake; per-release hue delta is
// the decider). One cold film on an ember stage and an ice stage alike is the
// "이미지가 덧씌워진 느낌" the 08-12/13 playtest named twice. The fix hues both
// per stage; these tests pin the two properties the fix must not lose:
//
//   (a) the hue actually moves with the accent (a constant is the defect), and
//   (b) the VALUE does not move (E0.5 reads by value contrast; the rim's
//       silhouette lift and SceneBuilder's measured void/apron seam tuning
//       both live in the value channel).
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class StageConceptTintTests
    {
        static float Luma(Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
        static float Saturation(Color c)
        {
            var max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            var min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            return max <= 0f ? 0f : (max - min) / max;
        }

        static readonly Color RimDefault = new Color(0.72f, 0.78f, 1.0f, 1f);

        [Test]
        public void RimColor_MovesWithTheStageAccent()
        {
            // Two real stages at opposite ends of the palette. Equal outputs
            // would mean the rim is still a constant — the defect itself.
            Assert.That(StageCatalog.TryGet("cinder-span", out var ember), Is.True);
            Assert.That(StageCatalog.TryGet("echo-throne", out var ice), Is.True);

            var emberRim = EnvironmentBuilder.RimColorFor(ember.AccentColor);
            var iceRim = EnvironmentBuilder.RimColorFor(ice.AccentColor);

            Assert.That(emberRim, Is.Not.EqualTo(iceRim),
                "one rim for two opposite accents = the stage-blind film again");
            // The ember stage's rim must actually be warmer than the ice
            // stage's — direction, not just difference.
            Assert.That(emberRim.r - emberRim.b, Is.GreaterThan(iceRim.r - iceRim.b),
                "the ember stage's rim must sit warmer than the ice stage's");
        }

        [Test]
        public void RimColor_PreservesTheDefaultRimsValue()
        {
            // The rim exists to lift a dark slab off a dark floor (E0.5 value
            // separation). Hue may move; the lift may not.
            var baseLuma = Luma(RimDefault);
            foreach (var entry in StageCatalog.Entries)
            {
                var rim = EnvironmentBuilder.RimColorFor(entry.AccentColor);
                Assert.That(Luma(rim), Is.EqualTo(baseLuma).Within(0.01f),
                    entry.Id + " rim moved the value channel, not just the hue");
            }
        }

        [Test]
        public void RimColor_NeverExceedsItsInputsChroma()
        {
            // §E0.5: scenery separates from telegraphs by VALUE and must not
            // borrow the hazard's chroma. Two bounds, because the relation is
            // not monotonic across the palette:
            //
            //   all stages   sat(rim) <= max(sat(base), sat(accent)) — a lerp
            //                cannot create chroma from nothing. SANITY ceiling
            //                only: for a hot accent this bound is ~2x slack.
            //                (And not "below the accent's": ash-march is
            //                near-grey at 0.109 while the base rim carries
            //                0.28 — there the BASE is the binding input, and
            //                grey is not a hazard hue to camouflage against.)
            //
            //   hot accents  the camouflage risk lives here, so pin the
            //   (sat > 0.5)  desaturation step's own signature: the rim must
            //                sit BELOW the no-desaturation lerp
            //                lerp(base, accent, 0.55). Worked ember example:
            //                with the step 0.128, without it 0.391 — deleting
            //                the step fails this instantly while the sanity
            //                ceiling (0.85) would have stayed green.
            var baseSat = Saturation(RimDefault);
            foreach (var entry in StageCatalog.Entries)
            {
                var rim = EnvironmentBuilder.RimColorFor(entry.AccentColor);
                var accentSat = Saturation(entry.AccentColor);
                var bound = Mathf.Max(baseSat, accentSat) + 0.001f;
                Assert.That(Saturation(rim), Is.LessThan(bound),
                    entry.Id + " rim is more chromatic than every input");
                if (accentSat > 0.5f)
                {
                    var undesaturated =
                        Color.Lerp(RimDefault, entry.AccentColor, 0.55f);
                    Assert.That(Saturation(rim), Is.LessThan(Saturation(undesaturated)),
                        entry.Id + " rim carries the raw accent lerp's chroma — "
                        + "the desaturation step is gone");
                }
            }
        }

        [Test]
        public void VoidTint_MovesHueButNotValue()
        {
            // SceneBuilder tuned the void's VALUE against the apron seam by
            // measurement (4x step = "the world ends here"). The stage hue
            // hook must be structurally unable to undo that tuning.
            var baked = new Color(0.425f, 0.372f, 0.506f, 1f);   // the scene tone's shape
            foreach (var entry in StageCatalog.Entries)
            {
                var tinted = GameDirector.VoidTintFor(baked, entry.AccentColor);
                Assert.That(Luma(tinted), Is.EqualTo(Luma(baked)).Within(0.01f),
                    entry.Id + " void tint moved the seam-tuned value");
                Assert.That(tinted.a, Is.EqualTo(1f));
            }

            // And the hue does move — warm accent pulls the cool base warm.
            Assert.That(StageCatalog.TryGet("cinder-span", out var ember), Is.True);
            var warm = GameDirector.VoidTintFor(baked, ember.AccentColor);
            Assert.That(warm.r - warm.b, Is.GreaterThan(baked.r - baked.b),
                "an ember accent must warm the void, or the hook is a no-op");
        }
    }
}
