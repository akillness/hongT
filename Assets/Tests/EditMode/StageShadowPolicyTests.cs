using System.Collections.Generic;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class StageShadowPolicyTests
    {
        [TearDown]
        public void ResetStatics() => StageShadowPolicy.ResetSessionForTests();

        [Test]
        public void LayerMasks_KeepLightingAndShadowMembershipSeparate()
        {
            Assert.That(StageShadowPolicy.DefaultRenderingLayerMask, Is.EqualTo(1u));
            Assert.That(StageShadowPolicy.CharacterShadowRenderingLayerMask, Is.EqualTo(2u));
            Assert.That(StageShadowPolicy.ActorRenderingLayerMask, Is.EqualTo(3u));
            Assert.That(
                StageShadowPolicy.ActorRenderingLayerMask
                & StageShadowPolicy.DefaultRenderingLayerMask,
                Is.Not.Zero,
                "actors must retain Default lighting when they join the shadow allow-list");
        }

        [Test]
        public void CasterPolicy_PromotesOnlyMeshRenderers()
        {
            var host = new GameObject("caster-policy-test");
            try
            {
                host.AddComponent<MeshFilter>();
                var mesh = host.AddComponent<MeshRenderer>();
                var trail = host.AddComponent<TrailRenderer>();

                Assert.That(StageShadowPolicy.TryConfigureCaster(mesh), Is.True);
                Assert.That(mesh.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
                Assert.That(mesh.receiveShadows, Is.False);
                Assert.That(mesh.renderingLayerMask,
                    Is.EqualTo(StageShadowPolicy.ActorRenderingLayerMask));

                Assert.That(StageShadowPolicy.TryConfigureCaster(trail), Is.False);
                Assert.That(trail.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(trail.receiveShadows, Is.False);
                Assert.That(trail.renderingLayerMask
                    & StageShadowPolicy.CharacterShadowRenderingLayerMask, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ActorView_FallbackIsDiagnosedButStillHasOneRealMeshCaster()
        {
            var view = ActorView.Create(null, Color.red, 1f);
            try
            {
                Assert.That(view.UsesFallbackForShadowDiagnostics, Is.True);
                Assert.That(view.RegisteredShadowCasterCount, Is.EqualTo(1));
                Assert.That(view.ShadowCasterSetsMatch(), Is.True);
                Assert.That(view.GetComponentsInChildren<Collider>(true), Is.Empty);

                var caster = view.RegisteredShadowCasterAt(0);
                Assert.That(caster, Is.TypeOf<MeshRenderer>());
                Assert.That(caster.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
                Assert.That(caster.receiveShadows, Is.False);

                var health = view.transform.Find("HealthBar");
                Assert.That(health, Is.Not.Null);
                foreach (var renderer in health.GetComponentsInChildren<Renderer>(true))
                {
                    Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                    Assert.That(renderer.renderingLayerMask
                        & StageShadowPolicy.CharacterShadowRenderingLayerMask, Is.Zero);
                }
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void LateEquipment_AttachSwapAndClearKeepsExactUniqueCasterSet()
        {
            var prefab = Resources.Load<GameObject>("Characters/lantern-reaver");
            Assert.That(prefab, Is.Not.Null, "shipping player prefab is the lifecycle fixture");
            var view = ActorView.Create(prefab, Color.white, 1f);
            try
            {
                var bodyCount = view.RegisteredShadowCasterCount;
                Assert.That(bodyCount, Is.GreaterThan(0));
                Assert.That(view.ShadowCasterSetsMatch(), Is.True);

                view.AttachEquipProps(2, 2, 2);
                Assert.That(view.RegisteredShadowCasterCount, Is.GreaterThan(bodyCount));
                Assert.That(view.ShadowCasterSetsMatch(), Is.True);
                AssertUniqueKeys(view);

                var basicEquipment = new HashSet<Renderer>();
                for (var i = bodyCount; i < view.RegisteredShadowCasterCount; i++)
                    basicEquipment.Add(view.RegisteredShadowCasterAt(i));
                view.AttachEquipProps(4, 4, 4);
                Assert.That(view.ShadowCasterSetsMatch(), Is.True);
                AssertUniqueKeys(view);
                for (var i = bodyCount; i < view.RegisteredShadowCasterCount; i++)
                    Assert.That(basicEquipment.Contains(view.RegisteredShadowCasterAt(i)), Is.False,
                        "a band swap must unregister old renderer objects before registering new ones");

                view.ClearEquipProps();
                Assert.That(view.RegisteredShadowCasterCount, Is.EqualTo(bodyCount));
                Assert.That(view.ShadowCasterSetsMatch(), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void LiveCensus_DetectsAnUnregisteredBodyRendererAddedAfterCreate()
        {
            var prefab = Resources.Load<GameObject>("Characters/lantern-reaver");
            var view = ActorView.Create(prefab, Color.white, 1f);
            try
            {
                Assert.That(view.ShadowCasterSetsMatch(), Is.True);
                var late = new GameObject("late-body-renderer");
                late.transform.SetParent(view.transform.GetChild(0), false);
                late.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>(
                    "Cube.fbx");
                late.AddComponent<MeshRenderer>();
                Assert.That(view.ShadowCasterSetsMatch(), Is.False,
                    "the census must enumerate the live model tree independently of registration");
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Coverage_ContainsPortraitFrustumFloorAndOffsetCasterBounds()
        {
            var view = ActorView.Create(null, Color.white, 1f);
            var caster = view.RegisteredShadowCasterAt(0);
            caster.transform.position += Vector3.right * 8f;
            var cameraHost = new GameObject("portrait-shadow-camera");
            var camera = cameraHost.AddComponent<Camera>();
            var center = ViewWorld.ArenaCenter + Vector3.up * 0.018f;
            camera.transform.position = center + new Vector3(0f, 10f, -10f);
            camera.transform.LookAt(center);
            camera.fieldOfView = 55f;
            camera.aspect = 375f / 667f;
            var root = StageMood.Apply("cinder-span", 0f, 0f);
            try
            {
                var policy = root.GetComponent<StageShadowPolicy>();
                policy.RefreshCoverageForTests(camera);
                var plane = new Plane(Vector3.up, center);
                for (var x = 0; x <= 1; x++)
                for (var y = 0; y <= 1; y++)
                {
                    var ray = camera.ViewportPointToRay(new Vector3(x, y, 0f));
                    Assert.That(plane.Raycast(ray, out var enter), Is.True);
                    var hit = ray.GetPoint(enter) - center;
                    Assert.That(Mathf.Abs(hit.x), Is.LessThan(policy.ReceiverHalfX));
                    Assert.That(Mathf.Abs(hit.z), Is.LessThan(policy.ReceiverHalfZ));
                }
                Assert.That(policy.ReceiverHalfX, Is.GreaterThan(8f),
                    "an offset weapon/body bound must expand the receiver from the actor origin");
                var floorCorner = center + new Vector3(
                    policy.ReceiverHalfX, 0f, policy.ReceiverHalfZ);
                Assert.That(policy.ShadowDistanceFloor, Is.GreaterThanOrEqualTo(
                    Vector3.Distance(camera.transform.position, floorCorner)));
            }
            finally
            {
                StageMood.Clear();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraHost);
                Object.DestroyImmediate(view.gameObject);
            }
        }

        static HashSet<string> Keys(ActorView view)
        {
            var keys = new HashSet<string>();
            for (var i = 0; i < view.RegisteredShadowCasterCount; i++)
                keys.Add(view.RegisteredShadowCasterKeyAt(i));
            return keys;
        }

        static void AssertUniqueKeys(ActorView view)
        {
            Assert.That(Keys(view).Count, Is.EqualTo(view.RegisteredShadowCasterCount),
                "diagnostic renderer identities must be one-to-one with registered casters");
        }
    }
}
