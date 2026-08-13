// EditMode gates for the shot-tracer pass (2026-08-13 사용자: "명중 이펙트만
// 있고 쏘는 무엇인가는 없는데" — hit effects with nothing visibly fired).
//
// Every ranged hit in this game is sim-side hitscan, so the tracer system is
// pure view decoration: comets that draw origin -> target while the sim's
// damage has already landed. These tests pin the four properties that made the
// defect (and its two near-miss regressions) possible:
//
//   (a) a companion swing draws EXACTLY ONE comet — the attack flag is a
//       0.25 s display window, so an unlatched implementation draws ~15,
//   (b) the latch sees the window CLOSE — an event-gated latch reads
//       still-casting forever and suppresses every cast after the first,
//   (c) run teardown retires live comets and drops the latches — enemy ids
//       restart at 1 every run, so a remembered id aimed into the next run
//       fires at an unrelated enemy,
//   (d) pool exhaustion wraps instead of growing or throwing.
//
// The sim is driven through its real public surface (fixed-step Tick with the
// CompanionSkillTests movement script); the view half is a real VfxDirector
// component. Nothing here asserts pixels — geometry existence and count only,
// which is what EditMode can see honestly (§4c: the screen itself is the
// browser gate's job).
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class ShotTracerTests
    {
        GameObject _host;
        VfxDirector _vfx;
        bool _hadReducedMotionPref;
        int _reducedMotionPrefValue;

        [SetUp]
        public void SetUp()
        {
            _hadReducedMotionPref = PlayerPrefs.HasKey("al:reduced-motion");
            _reducedMotionPrefValue = PlayerPrefs.GetInt("al:reduced-motion");
            ViewPrefs.ReducedMotion = false;
            _host = new GameObject("ShotTracerTests");
            _vfx = _host.AddComponent<VfxDirector>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            ViewPrefs.ReducedMotion = _reducedMotionPrefValue == 1;
            if (_hadReducedMotionPref)
                PlayerPrefs.SetInt("al:reduced-motion", _reducedMotionPrefValue);
            else
                PlayerPrefs.DeleteKey("al:reduced-motion");
            PlayerPrefs.Save();
        }

        static CinderSim CompanionSim()
        {
            Assert.IsTrue(
                HackConfig.TryDungeon(
                    CampaignStages.CinderSpan,
                    MetaStats.Of(0, 0, 0),
                    EquipTiers.Of(0, 0, 0),
                    "scout-echo",
                    0,
                    out var config),
                "cinder-span must resolve");
            return new CinderSim(in config);
        }

        /// <summary>The CompanionSkillTests movement script: keeps the player
        /// (and the following companion) circulating so enemies close to swing
        /// range instead of the run stalling at the spawn point.</summary>
        static SimInput Quiet(int tick)
        {
            var input = default(SimInput);
            input.MoveX = tick / 120 % 2 == 0 ? 1f : -1f;
            input.MoveY = tick / 200 % 2 == 0 ? 0.5f : -0.5f;
            return input;
        }

        /// <summary>Ticks until the slot's attack display window OPENS from
        /// closed, syncing the view each tick exactly like GameView does.
        /// Returns false when the script never produced a swing.</summary>
        bool StepUntilSwingEdge(CinderSim sim, int maxTicks)
        {
            var wasAttacking = ((IHackSnapshot)sim).CompanionAttackingAt(0);
            for (var tick = 0; tick < maxTicks; tick++)
            {
                var input = Quiet(tick);
                sim.Tick(in input);
                _vfx.SyncCompanionTracers(sim);
                var attacking = ((IHackSnapshot)sim).CompanionAttackingAt(0);
                if (attacking && !wasAttacking) return true;
                wasAttacking = attacking;
            }
            return false;
        }

        [Test]
        public void CompanionSwing_DrawsExactlyOneCometPerWindow()
        {
            var sim = CompanionSim();
            Assert.IsTrue(StepUntilSwingEdge(sim, 3600),
                "60 s of a live dungeon must produce at least one companion swing");
            var afterEdge = _vfx.ActiveTracerCountForTest;
            Assert.That(afterEdge, Is.EqualTo(1),
                "one swing = one comet; more means the 0.25 s display window is "
                + "being read as ~15 rising edges");

            // Holding INSIDE the same window must not add comets. Sync alone
            // (no sim tick that could legally re-swing) isolates the latch.
            _vfx.SyncCompanionTracers(sim);
            _vfx.SyncCompanionTracers(sim);
            Assert.That(_vfx.ActiveTracerCountForTest, Is.EqualTo(afterEdge),
                "the same open window re-fired — the latch is not latching");
        }

        [Test]
        public void CompanionSwing_SecondWindowFiresAgain()
        {
            // (b): the latch must see the window CLOSE between swings. The
            // cadence is 1.1 s and the window 0.25 s, so a second edge inside
            // 10 s proves close-then-reopen. EditMode never runs LateUpdate,
            // so the comet clock is stepped by hand between the swings — the
            // first comet must expire (0.23 s total life) and the second must
            // be the ONLY live one; without the manual step this asserts 2 and
            // measures pool accumulation, not the latch.
            var sim = CompanionSim();
            Assert.IsTrue(StepUntilSwingEdge(sim, 3600), "first swing");
            _vfx.StepTracersForTest(1f);   // > flight (0.09) + fade (0.14)
            Assert.That(_vfx.ActiveTracerCountForTest, Is.Zero,
                "a comet must not outlive flight + fade");
            Assert.IsTrue(StepUntilSwingEdge(sim, 600),
                "second swing within 10 s — if the sim swung but no edge fired, "
                + "the latch never observed the first window closing");
            Assert.That(_vfx.ActiveTracerCountForTest, Is.EqualTo(1),
                "the second swing must draw its own comet");
        }

        [Test]
        public void ClearTransient_RetiresCometsAndLatches()
        {
            var sim = CompanionSim();
            Assert.IsTrue(StepUntilSwingEdge(sim, 3600), "need one live comet");
            Assert.That(_vfx.ActiveTracerCountForTest, Is.GreaterThan(0));

            _vfx.ClearTransient();
            Assert.That(_vfx.ActiveTracerCountForTest, Is.Zero,
                "a live comet must not survive into the lobby");

            // (c): after teardown the NEXT run's first swing must fire — a
            // stale latch would swallow its rising edge.
            var next = CompanionSim();
            Assert.IsTrue(StepUntilSwingEdge(next, 3600), "next run's first swing");
            Assert.That(_vfx.ActiveTracerCountForTest, Is.EqualTo(1),
                "the next run's first swing was suppressed by a stale latch");
        }

        [Test]
        public void TracerPool_EvictionNeverThrowsAndCountIsBounded()
        {
            // Worst legal burst is 3 companions + a volley fan + the bolt = 7;
            // the pool is 8. Overfiring must wrap, not grow or throw.
            for (var i = 0; i < 40; i++)
            {
                var to = new Vector3(i + 1f, 0f, 0f);
                Assert.That(() => _vfx.FireTracerForTest(Vector3.zero, to),
                    Throws.Nothing);
            }
            Assert.That(_vfx.ActiveTracerCountForTest, Is.LessThanOrEqualTo(8),
                "the comet pool must wrap at its declared size");
        }
    }
}
