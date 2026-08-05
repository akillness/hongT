// §W wave-arrival telegraph contract. The View places warning rings using the
// sim's PUBLIC deterministic spawn mapping rather than duplicating spawn rules,
// so this locks the properties the telegraph relies on: the mapping is pure,
// in range, spreads across distinct points within one wave, and every spawn
// point sits inside the arena (a ring outside it would telegraph nothing).
using System.Collections.Generic;
using NUnit.Framework;
using CinderCourt.Sim;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class WaveTelegraphTests
    {
        const int RingsPerWave = 4;   // mirrors VfxDirector._waveWarnings.Length

        [Test]
        public void SpawnPointIndex_IsPureAndInRange()
        {
            var count = SimConfig.SpawnPoints.Length;
            for (var wave = 1; wave <= 20; wave++)
            for (var id = 0; id < 8; id++)
            {
                var first = CinderSim.SpawnPointIndexFor(wave, id);
                Assert.That(first, Is.InRange(0, count - 1),
                    $"wave {wave} id {id} maps outside the spawn table");
                Assert.That(CinderSim.SpawnPointIndexFor(wave, id), Is.EqualTo(first),
                    "mapping must be pure — the telegraph reads it every wave");
            }
        }

        [Test]
        public void TelegraphRings_LandOnDistinctPointsWithinAWave()
        {
            // (waveSeed + id*3) % 8 with stride 3 over 8 slots: the first four
            // ids must not collide, or the telegraph would stack rings and
            // under-read the arrival arc.
            for (var wave = 1; wave <= 20; wave++)
            {
                var seen = new HashSet<int>();
                for (var i = 0; i < RingsPerWave; i++)
                    Assert.That(seen.Add(CinderSim.SpawnPointIndexFor(wave, i)), Is.True,
                        $"wave {wave} telegraph reused a spawn point at ring {i}");
            }
        }

        [Test]
        public void EverySpawnPoint_SitsInsideTheArena()
        {
            // Arena: centre (768, 604), half extents 520 x 270.
            foreach (var point in SimConfig.SpawnPoints)
            {
                Assert.That(point.Length, Is.EqualTo(2));
                Assert.That(point[0], Is.InRange(248f, 1288f), "spawn X outside arena");
                Assert.That(point[1], Is.InRange(334f, 874f), "spawn Y outside arena");
            }
        }
    }
}
