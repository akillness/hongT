// Ward shell seed contract — the same protection the particle seed needed.
//
// CinderCourt/Vfx/WardFresnel is referenced by exactly ONE asset in the
// project (Resources/Materials/ward-fresnel-seed.mat). WebGL variant stripping
// keeps only what a shipped material references, so if that asset is deleted,
// renamed, or repointed, the shader is stripped and the ward renders pink —
// the identical failure mode that left the particle systems on a flat-colour
// fallback for the project's whole life, silently, because the fallback still
// rendered something plausible.
//
// These tests exist to make that failure loud. The runtime factory
// (ViewWorld.MakeWardShell) deliberately falls back to the old flat-alpha look
// when the seed is missing — correct behaviour for a build, useless as a
// signal, hence a test that asserts the fallback is NOT what shipped.
using CinderCourt.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class WardShellSeedTests
    {
        const string SeedPath = "Assets/Resources/Materials/ward-fresnel-seed.mat";
        const string ShaderName = "CinderCourt/Vfx/WardFresnel";

        static Material Seed()
        {
            var seed = AssetDatabase.LoadAssetAtPath<Material>(SeedPath);
            Assert.That(seed, Is.Not.Null, SeedPath + " missing");
            return seed;
        }

        [Test]
        public void WardSeed_AssetExists_SoTheShaderSurvivesStripping()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(SeedPath), Is.Not.Null,
                SeedPath + " must be committed: it is the only material "
                + "referencing " + ShaderName + ", and WebGL keeps only the "
                + "variants a shipped material uses.");
        }

        [Test]
        public void MakeWardShell_UsesTheFresnelShader_NotTheFlatAlphaFallback()
        {
            // THE mutation-sensitive assertion: delete or repoint the seed and
            // this goes red. Without it the ward silently reverts to the flat
            // 0.28-alpha sphere it shipped as, which still renders — the exact
            // shape of failure this suite exists to catch.
            var material = ViewWorld.MakeWardShell(new Color(0.45f, 0.85f, 1f, 1f));
            try
            {
                Assert.That(material.shader, Is.Not.Null, "ward shader unresolved");
                Assert.That(material.shader.name, Is.EqualTo(ShaderName),
                    "MakeWardShell fell back to flat alpha — check that "
                    + SeedPath + " exists under Resources/ and points at the "
                    + "fresnel shader.");
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void WardSeed_CompilesAndDrawsTransparent()
        {
            // The first draft of this test asserted
            //   shader.FindPassTagValue(0, "LightMode") == "UniversalForward"
            // and got string.Empty. A probe (Assets/Editor/WardShaderProbe.cs)
            // showed why: the shader is fine — isSupported True, passCount 1,
            // zero compile messages — that API simply does not report pass tags
            // for an HLSLPROGRAM pass here. The assertion was wrong, not the
            // shader, so it was REPLACED with questions that have observable
            // answers rather than loosened until it passed.
            var seed = Seed();
            Assert.That(seed.shader.isSupported, Is.True,
                "the ward shader does not compile on this platform — the shell "
                + "would render pink");
            Assert.That(seed.shader.passCount, Is.EqualTo(1),
                "one additive pass; a second would double-draw the shell");
            Assert.That(seed.renderQueue,
                Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent),
                "a shell in the opaque queue occludes the fight behind it");
            Assert.That(seed.GetTag("RenderType", false, "<none>"),
                Is.EqualTo("Transparent"),
                "URP sorts by this tag; an opaque shell hides the player inside it");
        }

        [Test]
        public void WardSeed_ShipsWithThePulseOff()
        {
            // The pulse means "about to expire" and is armed per frame from
            // player.WardTime (VfxDirector.SyncWard). A seed shipping with
            // amplitude > 0 would pulse from the moment a ward is cast — the
            // warning firing for the whole duration, which is no warning at all.
            Assert.That(Seed().GetFloat("_PulseAmplitude"), Is.EqualTo(0f),
                "the expiry pulse must start inert and be armed by the sim");
        }

        [Test]
        public void WardSeed_KeepsAReadableCoreSoTheVolumeIsVisibleHeadOn()
        {
            // Fresnel alone makes a shell invisible when you look straight
            // through it. The player is STANDING in this thing and has to see
            // where its edge is, so a small core floor is load-bearing rather
            // than decorative.
            var core = Seed().GetFloat("_CoreAlpha");
            Assert.That(core, Is.GreaterThan(0f),
                "a zero core makes the shell vanish head-on");
            Assert.That(core, Is.LessThan(0.28f),
                "at or above the old flat alpha the fresnel gains nothing — "
                + "the point is that the middle is CLEARER than the rim");
        }
    }
}
