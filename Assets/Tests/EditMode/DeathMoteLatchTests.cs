// Death-mote emission latch.
//
// SimEvents.EnemyKilled is a bare flag: no victim id, no position. The emitter
// therefore has to FIND the corpse in sim.Enemies, and the whole correctness
// question is "which corpses has it already fired for". A browser smoke run
// cannot answer it — the arena is lit by torches and lava, so a warm-pixel
// sweep over 24 frames of live combat reads 2127 +/- 21 whether or not motes
// spawned [MEASURED 2026-08-09]. The right coordinate system for this question
// is the latch itself (§4m: assert where the right and wrong implementations
// actually differ).
//
// The rejected alternative is what these tests really defend. Deciding
// freshness from EnemyState.FadeTime (a countdown from EnemyFade = 0.34 s)
// looks simpler and fails two ways: at 60 Hz a corpse can satisfy any
// value-window on two consecutive frames (double burst), and when the view
// batches several sim ticks it can satisfy it on none (silent miss). Identity
// has neither failure mode, because CinderSim.cs:266 guarantees ids are never
// reused within a run.
using System.Collections.Generic;
using CinderCourt.View;
using NUnit.Framework;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class DeathMoteLatchTests
    {
        // Same size as the shipped ring (VfxDirector.DeathLatchSlots). These
        // call the PRODUCTION helpers — VfxDirector.DeathAlreadyEmitted /
        // LatchDeath / ClearDeathLatch, reachable via InternalsVisibleTo
        // (AssemblyInfo.cs:6). An earlier draft of this fixture re-implemented
        // the ring locally and had to be thrown away: breaking the real one
        // would have left every test green, which is the §4m trap the latch
        // itself was written to avoid.
        const int Slots = 32;

        sealed class Latch
        {
            readonly int[] _ring = new int[Slots];
            int _cursor;

            public bool AlreadyEmitted(int id) =>
                VfxDirector.DeathAlreadyEmitted(_ring, id);

            public void Record(int id) =>
                VfxDirector.LatchDeath(_ring, ref _cursor, id);

            public void Clear() =>
                VfxDirector.ClearDeathLatch(_ring, ref _cursor);
        }

        /// <summary>Emit ids for one OnEvents pass over a corpse list. Mirrors
        /// the shape of the loop in VfxDirector.OnEvents: skip latched, latch,
        /// emit.</summary>
        static List<int> Pass(Latch latch, IEnumerable<int> corpseIds)
        {
            var emitted = new List<int>();
            foreach (var id in corpseIds)
            {
                if (latch.AlreadyEmitted(id)) continue;
                latch.Record(id);
                emitted.Add(id);
            }
            return emitted;
        }

        [Test]
        public void ACorpseEmitsExactlyOnce_EvenWhileItStaysInTheList()
        {
            // The failure this prevents: a corpse lingers for EnemyFade (0.34 s
            // = ~20 frames at 60 Hz) and EnemyKilled can be raised again by a
            // LATER kill while it is still published. Without the latch every
            // one of those frames re-emits for the same body.
            var latch = new Latch();
            var corpses = new[] { 7 };
            Assert.That(Pass(latch, corpses), Is.EqualTo(new[] { 7 }),
                "first sighting must emit");
            for (var frame = 0; frame < 20; frame++)
                Assert.That(Pass(latch, corpses), Is.Empty,
                    $"frame {frame}: the same corpse must never emit twice");
        }

        [Test]
        public void SeveralKillsInOneBatchEachEmit()
        {
            // A nova can kill the whole wave on one tick. Every distinct body
            // is its own reward beat and must get its own burst.
            var latch = new Latch();
            Assert.That(Pass(latch, new[] { 3, 4, 5, 6 }),
                Is.EqualTo(new[] { 3, 4, 5, 6 }));
        }

        [Test]
        public void NewKillsStillEmitWhileOldCorpsesAreStillListed()
        {
            var latch = new Latch();
            Pass(latch, new[] { 1, 2 });
            // Next batch: the old two are still fading, a third just died.
            Assert.That(Pass(latch, new[] { 1, 2, 3 }), Is.EqualTo(new[] { 3 }),
                "a fresh corpse must not be masked by the ones beside it");
        }

        [Test]
        public void ClearingTheLatchLetsTheNextRunEmitTheSameIds()
        {
            // THE mutation-sensitive one. CinderSim.Restart resets _nextEnemyId
            // to 1 (CinderSim.cs:924), so run N+1 reuses run N's ids. Drop the
            // wipe from ClearTransient and the first kills of every subsequent
            // run silently lose their motes — the player sees the effect once
            // per session and never again.
            var latch = new Latch();
            Pass(latch, new[] { 1, 2, 3 });
            latch.Clear();
            Assert.That(Pass(latch, new[] { 1, 2, 3 }), Is.EqualTo(new[] { 1, 2, 3 }),
                "after a run ends the ids restart at 1 — a stale ring would "
                + "swallow the next run's first three kills");
        }

        [Test]
        public void EvictionCannotResurrectAnIdThatIsStillOnTheField()
        {
            // The ring is finite, so an id CAN age out. That is only safe if a
            // corpse always leaves the published list well inside 32 kills.
            // EnemyFade is 0.34 s and the simultaneous-enemy cap is 20 (§2), so
            // 32 slots cover any real frame many times over. This test pins the
            // headroom: fill the ring completely and the oldest entry is the
            // only thing lost.
            var latch = new Latch();
            for (var id = 1; id <= Slots; id++) Pass(latch, new[] { id });
            Assert.That(Pass(latch, new[] { Slots }), Is.Empty,
                "the most recent id must still be latched");
            Pass(latch, new[] { Slots + 1 });      // evicts id 1
            Assert.That(Pass(latch, new[] { 1 }), Is.EqualTo(new[] { 1 }),
                "id 1 aged out after 32 later kills — by then its corpse is "
                + "long gone, so re-emitting is impossible in practice");
            Assert.That(Slots, Is.GreaterThan(20),
                "the ring must exceed the 20-enemy simultaneous cap or a single "
                + "wipe could evict a body still on the field");
        }
    }
}
