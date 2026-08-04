using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CinderCourt.Sim;

namespace CinderCourt.Tests
{
    public sealed class LanternReaverPrefabTests
    {
        [Test]
        public void LanternReaverPrefab_IsAUsableHumanoidActor()
        {
            var prefab = Resources.Load<GameObject>("Characters/lantern-reaver");
            Assert.That(prefab, Is.Not.Null, "Lantern Reaver Resources prefab is missing");
            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Characters/lantern-reaver.fbx");
            Assert.That(sourcePrefab, Is.Not.Null, "Lantern Reaver source FBX is missing");

            var instance = Object.Instantiate(prefab);
            var sourceInstance = Object.Instantiate(sourcePrefab);
            try
            {
                var animator = instance.GetComponentInChildren<Animator>(true);
                Assert.That(animator, Is.Not.Null, "Lantern Reaver needs an Animator");
                Assert.That(animator.runtimeAnimatorController, Is.Not.Null,
                    "Lantern Reaver Animator needs a controller");
                Assert.That(animator.avatar, Is.Not.Null, "Lantern Reaver Animator needs an Avatar");
                Assert.That(animator.avatar.isValid, Is.True, "Lantern Reaver Avatar must be valid");
                Assert.That(animator.avatar.isHuman, Is.True, "Lantern Reaver Avatar must be humanoid");

                var renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.That(renderers.Length, Is.GreaterThan(0),
                    "Lantern Reaver must contain a SkinnedMeshRenderer rather than a fallback capsule");

                var hasRenderableMesh = false;
                for (var index = 0; index < renderers.Length; index += 1)
                {
                    var renderer = renderers[index];
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

                    Assert.That(renderer.sharedMesh, Is.Not.Null,
                        $"SkinnedMeshRenderer '{renderer.name}' must reference a mesh");

                    var bounds = renderer.bounds;
                    Assert.That(bounds.size.x, Is.GreaterThan(0.01f),
                        $"SkinnedMeshRenderer '{renderer.name}' bounds must have width");
                    Assert.That(bounds.size.y, Is.GreaterThan(0.01f),
                        $"SkinnedMeshRenderer '{renderer.name}' bounds must have height");
                    Assert.That(bounds.size.z, Is.GreaterThan(0.01f),
                        $"SkinnedMeshRenderer '{renderer.name}' bounds must have depth");
                    hasRenderableMesh = true;

                }

                Assert.That(hasRenderableMesh, Is.True,
                    "Lantern Reaver needs an enabled SkinnedMeshRenderer");

                var actorBounds = AggregateWorldRendererBounds(instance, "Lantern Reaver Resources prefab");
                Assert.That(actorBounds.size.y, Is.GreaterThan(1f),
                    "Lantern Reaver must be human-sized vertically, not a collapsed or flat marker");

                var sourceBounds = AggregateWorldRendererBounds(sourceInstance, "Lantern Reaver source FBX");
                Assert.That(actorBounds.size.y, Is.EqualTo(sourceBounds.size.y).Within(0.01f),
                    "Lantern Reaver prefab and source FBX must have matching world height");
            }
            finally
            {
                Object.DestroyImmediate(sourceInstance);
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void LanternReaverPrefab_AttackActionMovesHumanoidRightHand()
        {
            var prefab = Resources.Load<GameObject>("Characters/lantern-reaver");
            Assert.That(prefab, Is.Not.Null, "Lantern Reaver Resources prefab is missing");

            var instance = Object.Instantiate(prefab);
            try
            {
                var animator = instance.GetComponentInChildren<Animator>(true);
                Assert.That(animator, Is.Not.Null, "Lantern Reaver needs an Animator");
                Assert.That(animator.avatar, Is.Not.Null, "Lantern Reaver Animator needs an Avatar");
                Assert.That(animator.avatar.isValid, Is.True, "Lantern Reaver Avatar must be valid");
                Assert.That(animator.avatar.isHuman, Is.True, "Lantern Reaver Avatar must be humanoid");
                Assert.That(animator.isHuman, Is.True, "Lantern Reaver Animator must drive a humanoid Avatar");

                var hasActionParameter = false;
                foreach (var parameter in animator.parameters)
                {
                    if (parameter.name != "action") continue;

                    Assert.That(parameter.type, Is.EqualTo(AnimatorControllerParameterType.Int),
                        "Lantern Reaver action parameter must be an integer");
                    hasActionParameter = true;
                    break;
                }

                Assert.That(hasActionParameter, Is.True,
                    "Lantern Reaver controller must expose the action integer parameter");

                var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                Assert.That(rightHand, Is.Not.Null,
                    "Lantern Reaver humanoid Avatar must map a right-hand transform");

                instance.SetActive(true);
                animator.gameObject.SetActive(true);
                animator.enabled = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                Assert.That(animator.isActiveAndEnabled, Is.True,
                    "Lantern Reaver Animator must be active and enabled before evaluation");

                animator.Rebind();
                animator.Update(0f);
                animator.SetInteger("action", (int)ActorAction.Idle);
                animator.Update(0.1f);
                var idleRightHandPosition = rightHand.position;

                animator.SetInteger("action", (int)ActorAction.Attack);
                animator.Update(0.01f);
                animator.Update(0.12f);

                var attackState = animator.GetCurrentAnimatorStateInfo(0);
                Assert.That(attackState.IsName("Base Layer.attack"), Is.True,
                    "Lantern Reaver action=Attack must enter the base-layer attack state");

                var attackRightHandPosition = rightHand.position;
                Assert.That(Vector3.Distance(idleRightHandPosition, attackRightHandPosition),
                    Is.GreaterThan(0.01f),
                    "Lantern Reaver attack action must drive a meaningful right-hand pose change");

                var maximumDirectAttackHandDelta = 0f;
                foreach (var normalizedTime in new[] { 0.2f, 0.5f, 0.8f })
                {
                    animator.Play("Base Layer.attack", 0, normalizedTime);
                    animator.Update(0f);
                    maximumDirectAttackHandDelta = Mathf.Max(
                        maximumDirectAttackHandDelta,
                        Vector3.Distance(idleRightHandPosition, rightHand.position));
                }

                Assert.That(maximumDirectAttackHandDelta, Is.GreaterThan(0.01f),
                    "Lantern Reaver attack clip must contain a meaningful retargeted right-hand pose");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
        private static Bounds AggregateWorldRendererBounds(GameObject root, string label)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var hasRenderableRenderer = false;
            var aggregate = default(Bounds);
            for (var index = 0; index < renderers.Length; index += 1)
            {
                var renderer = renderers[index];
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

                var bounds = renderer.bounds;
                Assert.That(bounds.size.x, Is.GreaterThan(0.01f),
                    $"{label} Renderer '{renderer.name}' bounds must have width");
                Assert.That(bounds.size.y, Is.GreaterThan(0.01f),
                    $"{label} Renderer '{renderer.name}' bounds must have height");
                Assert.That(bounds.size.z, Is.GreaterThan(0.01f),
                    $"{label} Renderer '{renderer.name}' bounds must have depth");

                if (hasRenderableRenderer) aggregate.Encapsulate(bounds);
                else aggregate = bounds;
                hasRenderableRenderer = true;
            }

            Assert.That(hasRenderableRenderer, Is.True, $"{label} needs an enabled Renderer");
            return aggregate;
        }
    }
}
