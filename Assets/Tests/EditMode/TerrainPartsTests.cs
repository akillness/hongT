// Lane T-b contract: the abyss-chancel terrain prefab carries the
// connectivity-split candidate parts as INDIVIDUAL children (authoring-time
// separation, never runtime splitting), alongside the untouched slab/apron
// fight floor. Echo-throne stays slab-only: its richer candidates are
// billboards (2D planes) — unusable at the 55° camera (spec §S4, non-goal).
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class TerrainPartsTests
    {
        [Test]
        public void AbyssChancel_CarriesSplitPartsAndIntactFloor()
        {
            var prefab = Resources.Load<GameObject>("Terrain/terrain-abyss-chancel");
            Assert.That(prefab, Is.Not.Null);
            int parts = 0, slabs = 0, aprons = 0;
            foreach (Transform child in prefab.transform)
            {
                if (child.name.Contains("-part-")) parts++;
                else if (child.name.Contains("-slab-")) slabs++;
                else if (child.name.Contains("-apron-")) aprons++;
            }
            Assert.That(parts, Is.GreaterThanOrEqualTo(40), "split parts missing");
            Assert.That(slabs, Is.EqualTo(4), "slab floor must stay intact");
            Assert.That(aprons, Is.EqualTo(1), "apron must stay intact");
        }

        [Test]
        public void EchoThrone_StaysSlabOnly_BillboardNonGoal()
        {
            var prefab = Resources.Load<GameObject>("Terrain/terrain-echo-throne");
            Assert.That(prefab, Is.Not.Null);
            foreach (Transform child in prefab.transform)
                Assert.That(child.name, Does.Not.Contain("-part-"),
                    "echo-throne candidates are 2D billboards — split is a non-goal");
        }
    }
}
