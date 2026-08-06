// ViewWorld.MakeAdditive contract (vfx survey: glow-class VFX). Additive blend
// (SrcAlpha/One) is what makes stacked skill rings, hit sparks, and dash
// afterimages ACCUMULATE past the Bloom threshold instead of muddying like
// straight alpha. If a refactor silently drops the One destination factor the
// whole "화려함" survey regresses to flat alpha with zero test noise — hence
// these are frozen numeric contracts, not decoration.
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools.Utils;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class AdditiveMaterialTests
    {
        [Test]
        public void MakeAdditive_UsesSrcAlphaOne_SoOverlapsAccumulateIntoBloom()
        {
            var material = ViewWorld.MakeAdditive(new Color(1f, 0.5f, 0.2f, 0.6f));
            try
            {
                Assert.That((BlendMode)(int)material.GetFloat("_SrcBlend"),
                    Is.EqualTo(BlendMode.SrcAlpha),
                    "source must scale by alpha so fades still work");
                Assert.That((BlendMode)(int)material.GetFloat("_DstBlend"),
                    Is.EqualTo(BlendMode.One),
                    "destination ONE is what makes overlapping glow accumulate");
                Assert.That(material.GetFloat("_ZWrite"), Is.EqualTo(0f),
                    "additive glow must not write depth or it occludes itself");
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void MakeAdditive_DiffersFromMakeUnlit_OnlyInDestinationBlend()
        {
            // Additive is a deliberate variant of the proven transparent seed:
            // it must keep the transparent surface (so the WebGL-stripped
            // variant survives) and differ ONLY in the destination factor.
            var additive = ViewWorld.MakeAdditive(Color.white);
            var alpha = ViewWorld.MakeUnlit(Color.white, true);
            try
            {
                Assert.That((BlendMode)(int)alpha.GetFloat("_DstBlend"),
                    Is.EqualTo(BlendMode.OneMinusSrcAlpha),
                    "plain transparent stays over-blend");
                Assert.That((BlendMode)(int)additive.GetFloat("_DstBlend"),
                    Is.EqualTo(BlendMode.One),
                    "additive is the only surface that switches the destination factor");
                Assert.That(additive.GetFloat("_SrcBlend"),
                    Is.EqualTo(alpha.GetFloat("_SrcBlend")),
                    "both keep SrcAlpha so alpha fades read identically");
                Assert.That(additive.renderQueue, Is.EqualTo(alpha.renderQueue),
                    "additive glow shares the transparent render queue");
            }
            finally
            {
                Object.DestroyImmediate(additive);
                Object.DestroyImmediate(alpha);
            }
        }

        [Test]
        public void MakeAdditive_PreservesRequestedColor()
        {
            var color = new Color(0.75f, 0.55f, 1f, 0.9f);
            var material = ViewWorld.MakeAdditive(color);
            try
            {
                Assert.That(material.color,
                    Is.EqualTo(color).Using(ColorEqualityComparer.Instance),
                    "the caller's element color must survive the additive setup");
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }
    }
}
