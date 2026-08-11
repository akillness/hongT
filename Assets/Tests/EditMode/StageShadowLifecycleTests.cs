using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class StageShadowLifecycleTests
    {
        Light _originalSun;
        AmbientMode _originalAmbientMode;
        Color _originalAmbient;
        Color _originalFog;

        [SetUp]
        public void CaptureGlobals()
        {
            _originalSun = RenderSettings.sun;
            _originalAmbientMode = RenderSettings.ambientMode;
            _originalAmbient = RenderSettings.ambientLight;
            _originalFog = RenderSettings.fogColor;
            StageShadowPolicy.ResetSessionForTests();
        }

        [TearDown]
        public void RestoreGlobals()
        {
            StageMood.Clear();
            RenderSettings.sun = _originalSun;
            RenderSettings.ambientMode = _originalAmbientMode;
            RenderSettings.ambientLight = _originalAmbient;
            RenderSettings.fogColor = _originalFog;
            StageShadowPolicy.ResetSessionForTests();
        }

        [Test]
        public void Apply_CreatesOneHardShadowSunAndColliderFreeReceiver()
        {
            var root = StageMood.Apply("cinder-span",
                SimConfig.ArenaHalfWidth, SimConfig.ArenaHalfHeight);
            try
            {
                var policy = root.GetComponent<StageShadowPolicy>();
                Assert.That(policy, Is.Not.Null);
                Assert.That(policy.OwnsLease, Is.True);
                Assert.That(RenderSettings.sun, Is.SameAs(policy.KeyLight));
                Assert.That(policy.KeyLight.shadows, Is.EqualTo(LightShadows.Hard));
                Assert.That(policy.KeyLightingRenderingLayers,
                    Is.EqualTo(StageShadowPolicy.DefaultRenderingLayerMask));
                Assert.That(policy.KeyShadowRenderingLayers,
                    Is.EqualTo(StageShadowPolicy.CharacterShadowRenderingLayerMask));
                Assert.That(policy.KeyUsesCustomShadowLayers, Is.True);
                Assert.That(policy.KeyUsesPipelineShadowBias, Is.False,
                    "the actor-only key must not inherit Mobile_RPAsset's 1/1 bias");
                Assert.That(policy.KeyLight.shadowBias,
                    Is.EqualTo(StageShadowPolicy.CharacterShadowDepthBias).Within(0.0001f));
                Assert.That(policy.KeyLight.shadowNormalBias,
                    Is.EqualTo(StageShadowPolicy.CharacterShadowNormalBias).Within(0.0001f));
                Assert.That(policy.KeyLight.renderingLayerMask,
                    Is.EqualTo(StageShadowPolicy.CharacterShadowRenderingLayerMask));

                var receiver = root.transform.Find(StageShadowPolicy.ReceiverName);
                Assert.That(receiver, Is.Not.Null);
                Assert.That(receiver.GetComponent<MeshFilter>(), Is.Not.Null);
                Assert.That(receiver.GetComponent<MeshRenderer>(), Is.Not.Null);
                Assert.That(receiver.GetComponents<Collider>(), Is.Empty);
                Assert.That(receiver.GetComponent<MeshRenderer>().shadowCastingMode,
                    Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(receiver.GetComponent<MeshRenderer>().renderingLayerMask,
                    Is.EqualTo(StageShadowPolicy.DefaultRenderingLayerMask));
            }
            finally
            {
                StageMood.Clear();
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DevelopmentDiagnosticToggle_ChangesOnlyTheReceiverDraw()
        {
            var root = StageMood.Apply("cinder-span");
            try
            {
                var policy = root.GetComponent<StageShadowPolicy>();
                var receiver = policy.ReceiverRenderer;
                Assert.That(receiver.enabled, Is.True);
                Assert.That(
                    StageShadowPolicy.SetReceiverEnabledForDiagnostics(false), Is.True);
                Assert.That(receiver.enabled, Is.False);
                Assert.That(policy.KeyLight.enabled, Is.True,
                    "the A/B toggle must not mutate stage lighting or caster state");
                Assert.That(
                    StageShadowPolicy.SetReceiverEnabledForDiagnostics(true), Is.True);
                Assert.That(receiver.enabled, Is.True);
            }
            finally
            {
                StageMood.Clear();
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Clear_RestoresExactLeaseOnceAndBeforeTheKeyIsDestroyed()
        {
            var sentinelRoot = new GameObject("sentinel-sun");
            var sentinelSun = sentinelRoot.AddComponent<Light>();
            sentinelSun.type = LightType.Directional;
            var ambient = new Color(0.13f, 0.24f, 0.35f, 1f);
            var fog = new Color(0.41f, 0.31f, 0.21f, 1f);
            RenderSettings.sun = sentinelSun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fogColor = fog;

            var root = StageMood.Apply("echo-throne");
            var policy = root.GetComponent<StageShadowPolicy>();
            var key = RenderSettings.sun;
            try
            {
                Assert.That(key, Is.Not.SameAs(sentinelSun));
                StageMood.Clear();
                Assert.That(key, Is.Not.Null,
                    "the lease restores synchronously while the old key object still exists");
                Assert.That(RenderSettings.sun, Is.SameAs(sentinelSun));
                Assert.That(RenderSettings.ambientMode, Is.EqualTo(AmbientMode.Trilight));
                Assert.That(RenderSettings.ambientLight, Is.EqualTo(ambient));
                Assert.That(RenderSettings.fogColor, Is.EqualTo(fog));
                Assert.That(policy.CapturedPipelineAssetHasOriginalSettings, Is.True,
                    "resolution and distance must be restored on the exact captured asset");

                StageMood.Clear();
                Assert.That(RenderSettings.sun, Is.SameAs(sentinelSun),
                    "a second restore is an idempotent no-op");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sentinelRoot);
            }
        }

        [Test]
        public void SameFrameStageSwitch_LeavesOnlyTheNewStageAsSunOwner()
        {
            var before = RenderSettings.sun;
            var first = StageMood.Apply("cinder-span");
            var firstKey = RenderSettings.sun;
            StageMood.Clear();
            var second = StageMood.Apply("ash-march");
            var secondKey = RenderSettings.sun;
            try
            {
                Object.DestroyImmediate(first);
                Assert.That(secondKey, Is.Not.Null.And.Not.SameAs(firstKey));
                Assert.That(RenderSettings.sun, Is.SameAs(secondKey),
                    "late destruction of stage A must not restore over stage B");
            }
            finally
            {
                StageMood.Clear();
                Object.DestroyImmediate(second);
                Assert.That(RenderSettings.sun, Is.SameAs(before));
            }
        }
    }
}
