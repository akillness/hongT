// §M2 — the two defects that made prologue combat read as a broken rig, both
// found by driving the running editor (Unity MCP play-mode trace, prologue,
// 2026-02-04) rather than by any test in this suite.
//
// 1. WALKING PLAYED `bighit`. ActorView infers the boss-slam launch the sim
//    never publishes from the player's VELOCITY, and it divided the sim's
//    position step by Time.deltaTime. The step is produced by whole 1/60 s
//    ticks and GameView runs 0 or 1 of them on a frame shorter than the fixed
//    step, so above 60 fps the quotient is inflated by (1/60)/deltaTime: at
//    120 fps a 218 px/s walk reports 436 px/s, lands inside the 400..1500
//    launch band, and the player plays the "I got hit" clip for as long as a
//    direction is held. Trace: sim=Move, animator param=4, for 1.9 s straight.
//    The divisor is now the sim time the batch actually advanced.
//
// 2. THE SWING WAS NEVER DRAWN. The sim holds the attack pose for a fixed
//    window (arena 5 frames @ 12 fps; dungeon HackSpec.ComboSwing) and drops
//    it the moment the window closes, while the authored mixamo swing is ~1 s
//    long. At animator speed 1 every swing was cut at normalizedTime
//    0.10-0.35 — the wind-up, never the strike. ActorView now fits the clip
//    into the window.
//
// What these tests defend:
//   * locomotion at ANY frame rate must not open the launch window, and a real
//     slam step must still open it — the assertions below feed the same sim
//     step with different sim deltas, which is exactly the axis the bug got
//     wrong (reverting to Time.deltaTime fails SlamStep..., because EditMode
//     has no player loop and Time.deltaTime is pinned to 0 here);
//   * ArenaSwingSeconds must equal the window a REAL CinderSim holds Attack
//     for. CinderSim's AttackClipFrames/AttackClipFps are private consts, so
//     the View mirrors them; MeasuredArenaAttackWindow... re-derives the
//     number from a live sim instead of trusting the mirror;
//   * the dungeon per-tier windows must be HackSpec.ComboSwing, indexed by the
//     combo tier GameView hands the view before the pose resolves;
//   * PoseSpeed must fit clip into window and stay inside sane rails;
//   * only swings are time-scaled — locomotion loops and reactions keep their
//     authored pace.
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class SwingPacingTests
    {
        const float FixedStep = 1f / 60f;

        // Measured px/s (ActorView.SyncPlayer's own table): walk 218,
        // slam 577. One 60 Hz tick of each.
        const float WalkStep = 218f * FixedStep;
        const float SlamStep = 577f * FixedStep;

        float _timeScale;

        [SetUp]
        public void PinDeltaTime()
        {
            // Same precedent as BossFlashYieldTests: timeScale 0 pins
            // Time.deltaTime to exactly 0, so no assertion here can be decided
            // by editor frame timing — and so a regression back to the
            // Time.deltaTime divisor cannot accidentally pass.
            _timeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        [TearDown]
        public void RestoreDeltaTime() => Time.timeScale = _timeScale;

        // --- 1. launch inference -------------------------------------------

        [Test]
        public void WalkStep_AtAnyFrameRate_NeverOpensTheLaunchWindow()
        {
            // The bug was frame-rate dependent, so the fixture is too: one 60 Hz
            // walk step delivered on frames of wildly different LENGTH. With the
            // sim delta as the divisor every one of them reports 218 px/s.
            WithRentedActor(view =>
            {
                view.SyncPlayer(Player(0f), FixedStep);          // seeds the baseline
                view.SyncPlayer(Player(WalkStep), FixedStep);

                Assert.That(view.KnockbackLive, Is.False,
                    "a 218 px/s walk is not a launch — this is the frame that made the player play `bighit` while simply walking");
            });
        }

        [Test]
        public void WalkStep_AcrossABatchOfTicks_NeverOpensTheLaunchWindow()
        {
            // The other half of the same axis: a slow frame runs SEVERAL ticks,
            // so the step grows in proportion to the sim delta. Speed is
            // unchanged, so the verdict must be unchanged.
            WithRentedActor(view =>
            {
                view.SyncPlayer(Player(0f), FixedStep);
                view.SyncPlayer(Player(WalkStep * 4f), FixedStep * 4f);

                Assert.That(view.KnockbackLive, Is.False,
                    "four ticks of walking is still 218 px/s; a batched frame must not read as a launch");
            });
        }

        [Test]
        public void SlamStep_OpensTheLaunchWindow()
        {
            // The capability the heuristic exists for. Without this the fix
            // could be "never infer a launch" and every other test still passes.
            WithRentedActor(view =>
            {
                view.SyncPlayer(Player(0f), FixedStep);
                view.SyncPlayer(Player(SlamStep), FixedStep);

                Assert.That(view.KnockbackLive, Is.True,
                    "577 px/s is the measured boss slam — the launch reaction must still fire");
            });
        }

        [Test]
        public void FrameThatAdvancedNoSimTime_CannotInferALaunch()
        {
            // Above 60 fps most frames run zero ticks. Nothing moved, so there
            // is nothing to infer, and dividing by zero must not be attempted.
            WithRentedActor(view =>
            {
                view.SyncPlayer(Player(0f), FixedStep);
                view.SyncPlayer(Player(SlamStep), 0f);

                Assert.That(view.KnockbackLive, Is.False,
                    "a frame that ran no tick advanced no sim time — it cannot produce a velocity");
            });
        }

        [Test]
        public void DashStep_IsExcluded_BecauseAvoidOwnsItsPose()
        {
            WithRentedActor(view =>
            {
                view.SyncPlayer(Player(0f, ActorAction.Avoid), FixedStep);
                view.SyncPlayer(Player(864f * FixedStep, ActorAction.Avoid), FixedStep);

                Assert.That(view.KnockbackLive, Is.False,
                    "the dash is 864 px/s and Avoid owns its own pose — a dash must never flash the launch reaction");
            });
        }

        [Test]
        public void EnemyChaseStep_NeverOpensTheLaunchWindow_ButAComboLaunchDoes()
        {
            // Enemies run the same inference on a lower gate (300 px/s), and a
            // hit is required. Chase tops out at 128 px/s; the combo launch is
            // 120 px over 0.18 s = 667 px/s.
            WithRentedActor(view =>
            {
                view.SyncEnemy(Enemy(0f, 100f), FixedStep);              // baseline, no hit
                view.SyncEnemy(Enemy(128f * FixedStep, 90f), FixedStep); // wounded WHILE chasing

                Assert.That(view.KnockbackLive, Is.False,
                    "a chasing enemy that takes a hit is not launched — 128 px/s is below the 300 px/s gate");
            });

            WithRentedActor(view =>
            {
                view.SyncEnemy(Enemy(0f, 100f), FixedStep);
                view.SyncEnemy(Enemy(667f * FixedStep, 90f), FixedStep);

                Assert.That(view.KnockbackLive, Is.True,
                    "667 px/s on a hit frame is the sim's combo launch — BigHit must fire");
            });
        }

        // --- 2. swing pacing -------------------------------------------------

        [Test]
        public void MeasuredArenaAttackWindow_EqualsTheMirroredConstant()
        {
            // ActorView.ArenaSwingSeconds mirrors CinderSim's PRIVATE
            // AttackClipFrames/AttackClipFps. Nothing in the compiler connects
            // the two, so this measures the real thing: how long a real sim
            // actually holds ActorAction.Attack after one queued strike.
            var config = HackConfig.Arena();
            var sim = new CinderSim(in config);

            var attack = new SimInput { AttackQueued = true, AttackHeld = false };
            sim.Tick(in attack);
            Assert.That(sim.Player.Action, Is.EqualTo(ActorAction.Attack),
                "the queued strike must pose Attack on the tick it is consumed");
            var ticksHeld = 1;   // the strike tick itself is inside the window


            var idle = default(SimInput);
            while (sim.Player.Action == ActorAction.Attack && ticksHeld < 600)
            {
                ticksHeld += 1;
                sim.Tick(in idle);
            }

            var measured = ticksHeld * FixedStep;
            Assert.That(measured, Is.EqualTo(ActorView.ArenaSwingSeconds).Within(FixedStep),
                $"the sim holds the arena attack pose for {measured:F3} s but the View paces its swing " +
                $"against {ActorView.ArenaSwingSeconds:F3} s — the mirrored constant has drifted from the sim");
        }

        [Test]
        public void SwingWindow_OutsideADungeon_IsTheArenaWindow()
        {
            // GameView only sets a combo tier in dungeon runs, so -1 IS the
            // arena/prologue signal — the mode the prologue trace was taken in.
            Assert.That(ActorView.SwingWindowSeconds(-1),
                Is.EqualTo(ActorView.ArenaSwingSeconds));
        }

        [Test]
        public void SwingWindow_InADungeon_IsTheComboSwingForThatTier()
        {
            for (var tier = 0; tier < HackSpec.ComboLength; tier += 1)
            {
                Assert.That(ActorView.SwingWindowSeconds(tier),
                    Is.EqualTo(HackSpec.ComboSwing[tier]),
                    $"combo tier {tier} must be paced against its own swing window");
            }

            Assert.That(ActorView.SwingWindowSeconds(HackSpec.ComboLength + 5),
                Is.EqualTo(HackSpec.ComboSwing[HackSpec.ComboLength - 1]),
                "a tier past the chain must clamp to the last swing, not index out of range");
        }

        [Test]
        public void ComboSwingWindowsAreNotAllTheSame()
        {
            // Guards the tier lookup itself: if every window were equal, the
            // per-tier test above would pass with the index ignored.
            Assert.That(HackSpec.ComboSwing[HackSpec.ComboLength - 1],
                Is.Not.EqualTo(HackSpec.ComboSwing[0]),
                "the finisher's window differs from the opener's — the pacing must be indexed, not constant");
        }

        [Test]
        public void PoseSpeed_FitsTheWholeClipIntoTheWindow()
        {
            // The observed failure: a 1.0 s clip in a 0.417 s window ran at
            // speed 1 and was cut at ~42%. 2.4x plays all of it.
            var speed = ActorView.PoseSpeed(1f, ActorView.ArenaSwingSeconds);

            Assert.That(speed, Is.EqualTo(2.4f).Within(1e-4f));
            Assert.That(1f / speed, Is.EqualTo(ActorView.ArenaSwingSeconds).Within(1e-4f),
                "the clip must finish exactly as the sim drops the pose");
        }

        [Test]
        public void PoseSpeed_StaysInsideItsRails()
        {
            Assert.That(ActorView.PoseSpeed(100f, 0.1f), Is.EqualTo(ActorView.MaxPoseSpeed),
                "an absurdly long clip must not strobe");
            Assert.That(ActorView.PoseSpeed(0.01f, 1f), Is.EqualTo(ActorView.MinPoseSpeed),
                "an absurdly short clip must not freeze mid-window");
        }

        [Test]
        public void PoseSpeed_FallsBackToAuthoredPace_WhenEitherTermIsUnknown()
        {
            Assert.That(ActorView.PoseSpeed(0f, ActorView.ArenaSwingSeconds), Is.EqualTo(1f),
                "an unmeasurable clip must play at its authored pace, not at 0");
            Assert.That(ActorView.PoseSpeed(1f, 0f), Is.EqualTo(1f),
                "a zero window must not divide by zero");
        }

        [Test]
        public void OnlySwingsAreTimeScaled()
        {
            Assert.That(ActorView.PoseValueForClip("attack"), Is.EqualTo((int)ActorAction.Attack));
            Assert.That(ActorView.PoseValueForClip("critical"), Is.EqualTo((int)ActorAction.Critical));
            Assert.That(ActorView.PoseValueForClip("attack2"), Is.EqualTo(11),
                "attack2 is the View-only substate ActorView drives with 11");
            Assert.That(ActorView.PoseValueForClip("attack3"), Is.EqualTo(12));

            // Locomotion and reactions must keep their authored pace: speeding
            // up a walk cycle would desync it from the sim's 218 px/s.
            foreach (var notASwing in new[] { "idle", "move", "run", "hit", "bighit", "avoid", "defence", "die", "show", "cast" })
            {
                Assert.That(ActorView.PoseValueForClip(notASwing), Is.EqualTo(-1),
                    $"'{notASwing}' is not a swing and must never be time-scaled");
            }
        }

        // --- fixtures ---------------------------------------------------------

        static PlayerState Player(float x, ActorAction action = ActorAction.Move) => new PlayerState
        {
            X = x,
            Y = 604f,
            Facing = 1,
            Health = SimConfig.PlayerMaxHealth,
            Moving = action == ActorAction.Move,
            Action = action,
            ActionTime = 0f,
        };

        static EnemyState Enemy(float x, float health) => new EnemyState
        {
            Id = 1,
            Visual = EnemyVisual.EmberCohort,
            X = x,
            Y = 604f,
            Facing = 1,
            Health = health,
            MaxHealth = 100f,
            Action = ActorAction.Move,
            Scale = 1f,
        };

        /// <summary>Create/destroy pattern from BossFlashYieldTests: a leaked
        /// actor keeps taking LateUpdate for the rest of the run.</summary>
        static void WithRentedActor(System.Action<ActorView> body)
        {
            var existingActors = new HashSet<ActorView>(
                Object.FindObjectsByType<ActorView>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            try
            {
                body(ActorView.Create(null, Color.red, 1f));
            }
            finally
            {
                foreach (var actor in Object.FindObjectsByType<ActorView>(FindObjectsInactive.Include,
                             FindObjectsSortMode.None))
                {
                    if (!existingActors.Contains(actor)) Object.DestroyImmediate(actor.gameObject);
                }
            }
        }
    }
}
