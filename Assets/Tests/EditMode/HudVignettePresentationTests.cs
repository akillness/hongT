using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class HudVignettePresentationTests
    {
        [Test]
        public void DamageAndLowHealthVignettes_WarnWithoutObscuringCombat()
        {
            var highPulseTime = Mathf.PI * 0.5f
                / HudView.LowHealthVignettePulseAngularSpeed;
            var lowPulseTime = Mathf.PI * 1.5f
                / HudView.LowHealthVignettePulseAngularSpeed;

            var high = HudView.LowHealthVignetteAlpha(highPulseTime, 1f);
            var low = HudView.LowHealthVignetteAlpha(lowPulseTime, 1f);

            Assert.That(HudView.DamageVignetteHitAlpha, Is.InRange(0.30f, 0.40f));
            Assert.That(low, Is.EqualTo(0.06f).Within(1e-4f));
            Assert.That(high, Is.EqualTo(0.18f).Within(1e-4f));
            Assert.That(high, Is.LessThan(HudView.DamageVignetteHitAlpha));
            Assert.That(HudView.LowHealthVignetteAlpha(highPulseTime, 0.5f),
                Is.EqualTo(high * 0.5f).Within(1e-4f));
        }

        [Test]
        public void LowHealthVignette_NeverBecomesNegativeWhenMotionIsDisabled()
        {
            Assert.That(HudView.LowHealthVignetteAlpha(0f, -1f), Is.EqualTo(0f));
        }
    }
}
