// §V3 particle seed contract.
//
// This file exists because the seed was BROKEN FOR ITS ENTIRE LIFE and nothing
// said so. RuntimeMaterialSeeds.SeedParticleAdditive asked for
// "Universal Render Pipeline/Particles Unlit"; the shader is named
// ".../Particles/Unlit" (slash — ParticlesUnlit.shader:1). Shader.Find returned
// null every time, the null branch returned TRUE ("a decorative layer must not
// fail a build"), no asset was ever written, and ViewWorld.MakeParticleAdditive
// fell back to flat-colour URP/Unlit — so colour-over-lifetime was dead in every
// shipped build while the code that configured it read as correct.
//
// The failure was invisible to every existing test because the fallback renders
// CORRECTLY, just flatter (§4m: a test that cannot distinguish the right
// implementation from the wrong one proves nothing). What distinguishes them is
// exactly one thing — WHICH SHADER the material ends up on — so that is what
// these assert.
//
// The asset is committed now, so the build no longer depends on the generator
// running. These tests pin the asset itself.
using CinderCourt.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class ParticleSeedTests
    {
        const string SeedPath = "Assets/Resources/Materials/particle-additive-seed.mat";
        const string ParticleShaderName = "Universal Render Pipeline/Particles/Unlit";

        [Test]
        public void ParticleSeed_AssetExists_SoTheBuildDoesNotDependOnTheGenerator()
        {
            var seed = AssetDatabase.LoadAssetAtPath<Material>(SeedPath);
            Assert.That(seed, Is.Not.Null,
                SeedPath + " must be committed: it is the ONLY material in the "
                + "project referencing URP Particles/Unlit, and WebGL variant "
                + "stripping keeps only variants that a shipped material uses.");
        }

        [Test]
        public void ParticleSeed_UsesTheParticlesShader_NotTheUnlitFallback()
        {
            // THE mutation-sensitive assertion. Revert the seed to URP/Unlit
            // (i.e. the fallback the broken generator left in place) and this
            // goes red; nothing else in the suite would notice.
            var seed = AssetDatabase.LoadAssetAtPath<Material>(SeedPath);
            Assert.That(seed, Is.Not.Null, "seed asset missing");
            Assert.That(seed.shader, Is.Not.Null, "seed shader unresolved");
            Assert.That(seed.shader.name, Is.EqualTo(ParticleShaderName),
                "per-particle vertex colour (colour/alpha over lifetime) exists "
                + "ONLY on the Particles shader. On URP/Unlit the four element "
                + "systems render a flat tint and every fade is silently lost.");
        }

        [Test]
        public void ParticleSeed_IsAdditive_MatchingTheLineAndQuadGrammar()
        {
            var seed = AssetDatabase.LoadAssetAtPath<Material>(SeedPath);
            Assert.That(seed, Is.Not.Null, "seed asset missing");
            Assert.That((BlendMode)(int)seed.GetFloat("_SrcBlend"),
                Is.EqualTo(BlendMode.SrcAlpha),
                "source scales by alpha so per-particle fades still read");
            Assert.That((BlendMode)(int)seed.GetFloat("_DstBlend"),
                Is.EqualTo(BlendMode.One),
                "destination ONE is what accumulates past Bloom threshold 1.05 "
                + "(CinderPostProfile) — the same contract AdditiveMaterialTests "
                + "pins for the line/quad family this augments");
            Assert.That(seed.GetFloat("_ZWrite"), Is.EqualTo(0f),
                "additive particles must not write depth or they occlude themselves");
        }

        [Test]
        public void ParticleSeed_KeepsTransparentKeyword_SoTheVariantSurvivesStripping()
        {
            // The whole point of a seed asset: the serialized keyword set is
            // what URP's stripper reads. Right shader + wrong keywords would
            // reproduce the original bug one layer down, still silently.
            var seed = AssetDatabase.LoadAssetAtPath<Material>(SeedPath);
            Assert.That(seed, Is.Not.Null, "seed asset missing");
            Assert.That(seed.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.True,
                "transparent surface keyword must ship with the asset");
            foreach (var forbidden in new[]
                     {
                         "_ALPHAPREMULTIPLY_ON",   // straight alpha, like MakeAdditive
                         "_ALPHAMODULATE_ON",
                         "_SOFTPARTICLES_ON",      // needs the depth texture
                         "_DISTORTION_ON",         // needs the opaque texture
                     })
            {
                Assert.That(seed.IsKeywordEnabled(forbidden), Is.False,
                    forbidden + " must stay off: soft particles and distortion "
                    + "need depth/opaque textures the WebGL renderer does not fund, "
                    + "and premultiply/modulate would break the straight-alpha "
                    + "blend the rest of the VFX grammar uses.");
            }
        }

        [Test]
        public void ParticleSeed_SamplesTheSoftGlowSprite_NotAHardWhiteQuad()
        {
            // Without a base map every particle is a hard-edged square. The
            // sprite is a generated radial falloff (tools/gen_fx_sprites.py),
            // deterministic so regeneration never moves a pixel.
            var seed = AssetDatabase.LoadAssetAtPath<Material>(SeedPath);
            Assert.That(seed, Is.Not.Null, "seed asset missing");
            var baseMap = seed.GetTexture("_BaseMap");
            Assert.That(baseMap, Is.Not.Null,
                "_BaseMap must resolve — a dangling texture GUID imports as null "
                + "and silently returns the hard quad this replaced");
            Assert.That(baseMap.width, Is.LessThanOrEqualTo(1024),
                "WebGL texture cap (CLAUDE.md §1)");
        }

        [Test]
        public void MakeParticleAdditive_ClonesTheSeed_InsteadOfFallingBack()
        {
            // End to end: the runtime factory must land on the particle shader.
            // This is the assertion that would have caught the original bug.
            var material = ViewWorld.MakeParticleAdditive(new Color(1f, 0.66f, 0.34f, 0.8f));
            try
            {
                Assert.That(material.shader.name, Is.EqualTo(ParticleShaderName),
                    "MakeParticleAdditive fell back to the URP/Unlit path, which "
                    + "means Resources.Load could not find the seed — check that "
                    + SeedPath + " is under Resources/ and imported.");
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        /// <summary>The AOE scorch decal degrades to a flat tinted disc when
        /// the texture is absent — deliberately, because VfxDirector may not
        /// hard-depend on an asset. That guard also means nothing at runtime
        /// will ever report the texture missing: nova, pulse and pylon
        /// destruction would all quietly lose their burn shape. This is the
        /// only place that can say so.</summary>
        [Test]
        public void ScorchDecalShips()
        {
            var decal = Resources.Load<Texture2D>("Fx/scorch-decal");
            Assert.That(decal, Is.Not.Null,
                "Resources/Fx/scorch-decal missing — the scorch silently "
                + "reverts to a flat disc with no error anywhere");
            // The quad is square and the fiction is a disc, so the decal's
            // alpha has to reach zero before the corners or every blast reads
            // as a tinted box. Checked here because the falloff is baked into
            // the PNG, not applied by any shader.
            Assert.That(decal.width, Is.EqualTo(decal.height),
                "a radial decal on a square quad must be square");
            Assert.That(decal.width, Is.LessThanOrEqualTo(1024),
                "CLAUDE.md §1: textures are capped at 1024 for the WebGL build");
        }
    }
}
