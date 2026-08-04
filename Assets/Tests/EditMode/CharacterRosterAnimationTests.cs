using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CinderCourt.Sim;

namespace CinderCourt.Tests
{
    public sealed class CharacterRosterAnimationTests
    {
        private const string ControllerPath = "Assets/Art/Motion/CinderActor.controller";

        [Test]
        public void CharacterRoster_ActorsRenderAndAnimateSharedAttack()
        {
            var sharedController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            Assert.That(sharedController, Is.Not.Null, "The shared character action controller is missing");

            foreach (var characterId in CharacterRoster.Ids)
            {
                var prefab = Resources.Load<GameObject>($"Characters/{characterId}");
                Assert.That(prefab, Is.Not.Null, $"{characterId} Resources prefab is missing");

                var instance = Object.Instantiate(prefab);
                try
                {
                    var animator = instance.GetComponentInChildren<Animator>(true);
                    Assert.That(animator, Is.Not.Null, $"{characterId} needs an Animator");
                    Assert.That(animator.isActiveAndEnabled, Is.True,
                        $"{characterId} Animator must be active and enabled");
                    Assert.That(animator.runtimeAnimatorController, Is.SameAs(sharedController),
                        $"{characterId} Animator must use the shared character action controller");
                    Assert.That(animator.avatar, Is.Not.Null, $"{characterId} Animator needs an Avatar");
                    Assert.That(animator.avatar.isValid, Is.True, $"{characterId} Avatar must be valid");
                    Assert.That(animator.avatar.isHuman, Is.True, $"{characterId} Avatar must be Humanoid");
                    Assert.That(animator.isHuman, Is.True, $"{characterId} Animator must drive a Humanoid Avatar");
                    Assert.That(HasActionParameter(animator), Is.True,
                        $"{characterId} controller must expose the shared integer action parameter");

                    var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    Assert.That(rightHand, Is.Not.Null,
                        $"{characterId} Humanoid Avatar must map a right-hand transform");

                    var restBoundsByRenderer = new Dictionary<SkinnedMeshRenderer, Bounds>();
                    var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    var enabledRendererCount = 0;
                    foreach (var renderer in renderers)
                    {
                        if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

                        enabledRendererCount += 1;
                        Assert.That(renderer.sharedMesh, Is.Not.Null,
                            $"{characterId} SkinnedMeshRenderer '{renderer.name}' must reference a mesh");
                        AssertMeshHasBlendedInfluences(characterId, renderer);
                    }

                    Assert.That(enabledRendererCount, Is.GreaterThan(0),
                        $"{characterId} must contain an enabled SkinnedMeshRenderer");

                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.Rebind();
                    animator.Update(0f);

                    foreach (var renderer in renderers)
                    {
                        if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

                        var restBounds = CaptureBakedMeshBounds(renderer,
                            $"{characterId} SkinnedMeshRenderer '{renderer.name}' at rest");
                        restBoundsByRenderer.Add(renderer, restBounds);
                    }
                    animator.SetInteger("action", (int)ActorAction.Attack);
                    animator.Update(0.01f);
                    animator.Update(0.12f);
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.attack"), Is.True,
                        $"{characterId} action=Attack must enter the shared Base Layer.attack state");

                    Vector3? firstAttackHandPosition = null;
                    var maximumAttackHandDelta = 0f;
                    foreach (var normalizedTime in new[] { 0.2f, 0.5f, 0.8f })
                    {
                        animator.Play("Base Layer.attack", 0, normalizedTime);
                        animator.Update(0f);

                        AssertAttackBoundsRemainPlausible(characterId, normalizedTime, restBoundsByRenderer);

                        if (firstAttackHandPosition.HasValue)
                        {
                            maximumAttackHandDelta = Mathf.Max(maximumAttackHandDelta,
                                Vector3.Distance(firstAttackHandPosition.Value, rightHand.position));
                        }
                        else
                        {
                            firstAttackHandPosition = rightHand.position;
                        }
                    }

                    Assert.That(maximumAttackHandDelta, Is.GreaterThan(0.01f),
                        $"{characterId} shared attack clip must drive meaningful Humanoid right-hand movement");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        private static bool HasActionParameter(Animator animator)
        {
            foreach (var parameter in animator.parameters)
            {
                if (parameter.name == "action" && parameter.type == AnimatorControllerParameterType.Int) return true;
            }

            return false;
        }

        private static void AssertMeshHasBlendedInfluences(string characterId, SkinnedMeshRenderer renderer)
        {
            var mesh = renderer.sharedMesh;
            using (var bonesPerVertex = mesh.GetBonesPerVertex())
            {
                var label = $"{characterId} SkinnedMeshRenderer '{renderer.name}'";
                Assert.That(bonesPerVertex.Length, Is.EqualTo(mesh.vertexCount),
                    $"{label} must provide one bone-influence count per vertex");

                var blendedVertexCount = 0;
                foreach (var influenceCount in bonesPerVertex)
                {
                    if (influenceCount > 1) blendedVertexCount += 1;
                }

                Assert.That(blendedVertexCount, Is.GreaterThan(0),
                    $"{label} must contain a vertex with multiple bone influences to prevent rigid seams");
            }
        }

        private static void AssertAttackBoundsRemainPlausible(
            string characterId,
            float normalizedTime,
            IReadOnlyDictionary<SkinnedMeshRenderer, Bounds> restBoundsByRenderer)
        {
            foreach (var pair in restBoundsByRenderer)
            {
                var renderer = pair.Key;
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

                var label = $"{characterId} SkinnedMeshRenderer '{renderer.name}' at attack normalized time {normalizedTime:0.0}";
                var attackBounds = CaptureBakedMeshBounds(renderer, label);

                var restExtentRadius = pair.Value.extents.magnitude;
                var attackExtentRadius = attackBounds.extents.magnitude;
                var extentRatio = attackExtentRadius / restExtentRadius;
                var volumeRatio = BoundsVolume(attackBounds) / BoundsVolume(pair.Value);
                Assert.That(extentRatio, Is.LessThanOrEqualTo(8f),
                    $"{label} expanded {extentRatio:0.00}x beyond its rest extent radius; " +
                    $"rest={pair.Value.size}, attack={attackBounds.size}");
                Assert.That(volumeRatio, Is.LessThanOrEqualTo(128f),
                    $"{label} expanded {volumeRatio:0.00}x beyond its rest bounds volume; " +
                    $"rest={pair.Value.size}, attack={attackBounds.size}");
            }
        }

        private static float BoundsVolume(Bounds bounds)
        {
            return bounds.size.x * bounds.size.y * bounds.size.z;
        }

        private static Bounds CaptureBakedMeshBounds(SkinnedMeshRenderer renderer, string label)
        {
            var bakedMesh = new Mesh();
            try
            {
                renderer.BakeMesh(bakedMesh);
                var vertices = bakedMesh.vertices;
                Assert.That(vertices.Length, Is.GreaterThan(0), $"{label} baked mesh must contain vertices");

                var bounds = new Bounds(vertices[0], Vector3.zero);
                foreach (var vertex in vertices)
                {
                    Assert.That(float.IsNaN(vertex.x) || float.IsInfinity(vertex.x) ||
                                float.IsNaN(vertex.y) || float.IsInfinity(vertex.y) ||
                                float.IsNaN(vertex.z) || float.IsInfinity(vertex.z), Is.False,
                        $"{label} baked vertex must be finite");
                    bounds.Encapsulate(vertex);
                }

                AssertFiniteNonDegenerateBounds(bounds, label);
                return bounds;
            }
            finally
            {
                Object.DestroyImmediate(bakedMesh);
            }
        }

        private static void AssertFiniteNonDegenerateBounds(Bounds bounds, string label)
        {
            Assert.That(float.IsNaN(bounds.min.x) || float.IsInfinity(bounds.min.x) ||
                        float.IsNaN(bounds.min.y) || float.IsInfinity(bounds.min.y) ||
                        float.IsNaN(bounds.min.z) || float.IsInfinity(bounds.min.z) ||
                        float.IsNaN(bounds.max.x) || float.IsInfinity(bounds.max.x) ||
                        float.IsNaN(bounds.max.y) || float.IsInfinity(bounds.max.y) ||
                        float.IsNaN(bounds.max.z) || float.IsInfinity(bounds.max.z), Is.False,
                $"{label} baked bounds must be finite");
            Assert.That(bounds.size.x, Is.GreaterThan(0.01f), $"{label} baked bounds must have width");
            Assert.That(bounds.size.y, Is.GreaterThan(0.01f), $"{label} baked bounds must have height");
            Assert.That(bounds.size.z, Is.GreaterThan(0.01f), $"{label} baked bounds must have depth");
        }
    }
}
