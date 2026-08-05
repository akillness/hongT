// Lane P contract: six socket-prop prefabs exist with renderers, respect the
// ≤800-triangle budget each, keep the character total under the §T4 25k cap,
// and the tier→band mapping follows T0-1 none / T2-3 basic / T4-5 fine.
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class EquipPropTests
    {
        static readonly string[] Slots = { "weapon", "lantern", "cloak" };
        static readonly string[] Bands = { "basic", "fine" };
        const int TriBudgetPerProp = 800;

        static GameObject Load(string slot, string band)
            => Resources.Load<GameObject>($"Props/equip-{slot}-{band}");

        static int TriangleCount(GameObject prefab)
        {
            var total = 0;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                for (var s = 0; s < mesh.subMeshCount; s++)
                    total += (int)(mesh.GetIndexCount(s) / 3);
            }
            return total;
        }

        [Test]
        public void AllSixPropPrefabs_ExistWithRenderers()
        {
            foreach (var slot in Slots)
            foreach (var band in Bands)
            {
                var prefab = Load(slot, band);
                Assert.That(prefab, Is.Not.Null, $"Props/equip-{slot}-{band} missing");
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty,
                    $"equip-{slot}-{band} has no renderer");
            }
        }

        [Test]
        public void EveryProp_RespectsTriangleBudget()
        {
            var characterTotal = 0;
            foreach (var slot in Slots)
            foreach (var band in Bands)
            {
                var tris = TriangleCount(Load(slot, band));
                Assert.That(tris, Is.GreaterThan(0), $"equip-{slot}-{band} has no triangles");
                Assert.That(tris, Is.LessThanOrEqualTo(TriBudgetPerProp),
                    $"equip-{slot}-{band} over budget: {tris}");
                characterTotal += tris;
            }
            // Worst case a character wears one band of all three slots; even the
            // whole six-prop set must stay far under the 25k §T4 character cap.
            Assert.That(characterTotal, Is.LessThan(25000));
        }

        [Test]
        public void EveryProp_HasCharacterScaleWorldSize()
        {
            // FBX unit traps (cm vs m) make props microscopic or gigantic.
            // Character is ~1.8 wu tall: every prop's longest world span must
            // land in a wearable band.
            foreach (var slot in Slots)
            foreach (var band in Bands)
            {
                var prefab = Load(slot, band);
                var instance = Object.Instantiate(prefab);
                try
                {
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    var bounds = renderers[0].bounds;
                    for (var i = 1; i < renderers.Length; i++)
                        bounds.Encapsulate(renderers[i].bounds);
                    var span = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                    Assert.That(span, Is.InRange(0.15f, 1.6f),
                        $"equip-{slot}-{band} world span {span:F4} is not wearable");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        [Test]
        public void PropMaterials_UseUrpShaders()
        {
            foreach (var slot in Slots)
            foreach (var band in Bands)
            {
                foreach (var renderer in Load(slot, band).GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null, $"equip-{slot}-{band}: null material");
                    Assert.That(material.shader.name, Does.StartWith("Universal Render Pipeline/"),
                        $"equip-{slot}-{band}: non-URP shader {material.shader.name} strips on WebGL");
                }
            }
        }

        [Test]
        public void PlayerPrefab_IsHumanoidWithAllThreeSocketBones()
        {
            var player = Resources.Load<GameObject>("Characters/lantern-reaver");
            Assert.That(player, Is.Not.Null, "player prefab missing");
            var animator = player.GetComponentInChildren<Animator>();
            Assert.That(animator, Is.Not.Null.And.Property("isHuman").True,
                "player must be Humanoid for socket lookup");
            Assert.That(animator.GetBoneTransform(HumanBodyBones.RightHand), Is.Not.Null);
            Assert.That(animator.GetBoneTransform(HumanBodyBones.LeftHand), Is.Not.Null);
            Assert.That(animator.GetBoneTransform(HumanBodyBones.Chest), Is.Not.Null);
        }
    }
}
