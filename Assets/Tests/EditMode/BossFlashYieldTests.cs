// ActorView.FlashLive — the one signal GameView.ApplyBossPresentation yields to
// (`if (view.FlashLive) return;`).
//
// Why this file exists: the boss's stage-catalog tint is re-applied to its
// renderers EVERY frame, and it runs AFTER SyncEnemy. Before the §K3 fix that
// overwrite was unconditional, which made the boss the one enemy that could
// never show a hit flash — including the skill-element color, which is exactly
// the enemy where reading WHAT hit you matters most. The fix is a single early
// return keyed on FlashLive, so the fix is only ever as correct as FlashLive's
// truthfulness:
//   * down on the first sync after rent (no baseline means no hit),
//   * up while a real health drop's flash owns the MaterialPropertyBlock,
//   * down again after ResetForPool, so a recycled actor never inherits the
//     previous occupant's flash and blanks an unrelated boss's catalog tint,
//   * down on death, because Apply's death branch returns above the flash
//     decay and would otherwise latch a killing-blow flash for the whole fade.
//
// Determinism: _flashTime decays by Time.deltaTime inside ActorView.Apply.
// EditMode runs no player loop, so Time.deltaTime is whatever the editor last
// reported and cannot be advanced on demand. Pinning Time.timeScale to 0 (the
// precedent set by GameDirectorCampaignRouteTests) forces deltaTime to exactly
// 0 for the fixture, so no assertion below can be decided by editor frame
// timing. See the FLASH EXPIRY note at the foot of this file for what that
// costs and why the cost is unavoidable here.
using System.Collections.Generic;
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class BossFlashYieldTests
    {
        const float FullHealth = 100f;
        const float HitDamage = 15f;

        // A weaker enemy rented into a recycled actor: strictly below the
        // previous occupant's parting health, so a stale baseline reads the gap
        // as damage on the rent frame.
        const float WeakerOccupantHealth = 25f;

        float _timeScale;

        [SetUp]
        public void PinDeltaTime()
        {
            // Time.deltaTime is scaled by timeScale, so this makes the flash
            // decay term exactly 0 and the assertions below frame-independent.
            _timeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        [TearDown]
        public void RestoreDeltaTime() => Time.timeScale = _timeScale;

        [Test]
        public void FirstSyncAfterRent_LeavesTheFlashDown_EvenAtALowHealthFraction()
        {
            WithRentedActor(view =>
            {
                // A rented actor has no previous health to diff against. The
                // sentinel guard is the only thing standing between "rent an
                // already-wounded enemy" and a damage of float.MaxValue - 25 on
                // the very first frame.
                var damage = view.SyncEnemy(Enemy(WeakerOccupantHealth));

                Assert.That(damage, Is.EqualTo(0f),
                    "the first sync after rent has no baseline, so it cannot report damage");
                Assert.That(view.FlashLive, Is.False,
                    "a pooled actor must not flash on rent — the boss tint would yield to a hit that never happened");
            });
        }

        [Test]
        public void HealthDropOnALaterSync_RaisesTheFlashTheBossTintYieldsTo()
        {
            WithRentedActor(view =>
            {
                view.SyncEnemy(Enemy(FullHealth));   // establishes the baseline
                Assert.That(view.FlashLive, Is.False,
                    "precondition: the baseline sync must not arm a flash, or the drop below proves nothing");

                var damage = view.SyncEnemy(Enemy(FullHealth - HitDamage));

                Assert.That(damage, Is.EqualTo(HitDamage).Within(1e-3f),
                    "SyncEnemy reports the health delta GameView keys its damage number and contact spark off");
                Assert.That(view.FlashLive, Is.True,
                    "a real hit must own the MaterialPropertyBlock — this is the signal ApplyBossPresentation returns early on");
            });
        }

        // Only a strict decrease is a hit. A heal reported as damage is the
        // classic "fix the negative number with Mathf.Abs" regression, and it
        // would flash the boss — and suppress its catalog tint — on a frame
        // where nothing struck it.
        [TestCase(FullHealth, "unchanged health is not a hit")]
        [TestCase(FullHealth + 20f, "a heal is not a hit")]
        public void NonDamagingSecondSync_LeavesTheFlashDown(float nextHealth, string because)
        {
            WithRentedActor(view =>
            {
                view.SyncEnemy(Enemy(FullHealth));   // establishes the baseline

                var damage = view.SyncEnemy(Enemy(nextHealth));

                Assert.That(damage, Is.EqualTo(0f), because);
                Assert.That(view.FlashLive, Is.False, because);
            });
        }

        [Test]
        public void ResetForPool_ClearsALiveFlash()
        {
            WithRentedActor(view =>
            {
                view.SyncEnemy(Enemy(FullHealth));
                view.SyncEnemy(Enemy(FullHealth - HitDamage));
                Assert.That(view.FlashLive, Is.True,
                    "precondition: the actor must actually be flashing, or the reset below proves nothing");

                view.ResetForPool();

                Assert.That(view.FlashLive, Is.False,
                    "a recycled actor that keeps the previous occupant's flash would suppress an unrelated boss's catalog tint");
            });
        }

        [Test]
        public void ResetForPool_ReArmsTheRentGuard_SoTheRecycledActorDoesNotFlashOnItsFirstSync()
        {
            WithRentedActor(view =>
            {
                view.SyncEnemy(Enemy(FullHealth));
                view.SyncEnemy(Enemy(FullHealth - HitDamage));   // occupant leaves wounded

                view.ResetForPool();

                // Re-rented for a weaker enemy at full health, still below the
                // previous occupant's parting health. A surviving baseline reads
                // that gap as damage and flashes on the rent frame.
                var damage = view.SyncEnemy(Enemy(WeakerOccupantHealth));

                Assert.That(damage, Is.EqualTo(0f),
                    "ResetForPool must drop the health baseline, or the next occupant's rent looks like a hit");
                Assert.That(view.FlashLive, Is.False,
                    "a rent-frame flash on a recycled actor would blank the boss catalog tint for an enemy nothing struck");
            });
        }

        [Test]
        public void DeathAfterAHit_ReleasesTheFlash_SoTheTintIsNotSuppressedForTheWholeFade()
        {
            WithRentedActor(view =>
            {
                // Real sim sequence: a hit lands (flash armed, 0.13 s), and the
                // NEXT tick's damage finishes the actor while that flash is
                // still live. The killing blow itself never flashes — SyncEnemy
                // gates hit on !state.Dead — so arming the flash on the prior
                // sync is what makes this test capable of failing at all.
                view.SyncEnemy(Enemy(FullHealth));
                view.SyncEnemy(Enemy(FullHealth - HitDamage));
                Assert.That(view.FlashLive, Is.True,
                    "precondition: the actor must actually be flashing when it dies, or the death sync below proves nothing");

                view.SyncEnemy(Enemy(0f, dead: true));

                // Apply's death branch returns above the flash-decay block, so
                // nothing else can ever retire this flash: the actor is dead, it
                // will never take another hit, and only ResetForPool on recycle
                // would clear it. Left latched, FlashLive stays true for the
                // entire fade and ApplyBossPresentation yields the whole time.
                Assert.That(view.FlashLive, Is.False,
                    "death must release the flash — a latched flash suppresses the boss catalog tint for the full death fade");
            });
        }

        /// <summary>Boss-shaped enemy frame. Boss values throughout so the
        /// fixture reads as the scenario it defends; ActorView's flash path is
        /// visual-agnostic, the boss is simply where losing the flash hurts.</summary>
        static EnemyState Enemy(float health, bool dead = false) => new EnemyState
        {
            Id = 1,
            Visual = EnemyVisual.BossMonarch,
            X = 768f,
            Y = 604f,
            Facing = 1,
            Health = health,
            MaxHealth = FullHealth,
            Dead = dead,
            FadeTime = 0f,
            Action = ActorAction.Idle,
            ActionTime = 0f,
            IsBoss = true,
            Scale = 1.6f,
        };

        /// <summary>Builds one actor through ActorView.Create — the same factory
        /// GameView.Rent uses for a pool miss — runs the body, then destroys
        /// every actor the body added. Snapshot/DestroyImmediate pattern from
        /// GameDirectorCampaignRouteTests: a leaked actor keeps taking LateUpdate
        /// for the rest of the run and pollutes every later test.</summary>
        static void WithRentedActor(System.Action<ActorView> body)
        {
            var existingActors = new HashSet<ActorView>(
                Object.FindObjectsByType<ActorView>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            try
            {
                var view = ActorView.Create(null, Color.red, 1f);
                body(view);
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

        // FLASH EXPIRY — deliberately not asserted here.
        //
        // _flashTime is decremented by exactly one term, Time.deltaTime, inside
        // the private ActorView.Apply. ActorView exposes no clock seam: no delta
        // parameter, no injectable timer, no test hook (UpdateCastGlow takes a
        // delta but is private and drives the §V1 hand glow, not the flash).
        // EditMode runs no player loop, so nothing advances that clock on
        // demand — Time.deltaTime holds whatever the editor last reported.
        //
        // The only lever, Time.timeScale, scales that stale delta; it cannot
        // synthesize a known one. Cranking it up would expire the flash after
        // some unknowable number of Apply calls decided by the editor's frame
        // interval — a wall-clock race, and exactly the flake the suite must not
        // carry. So expiry is asserted nowhere above rather than faked, and
        // timeScale is pinned to 0 instead, which makes the contracts that ARE
        // decidable here decidable exactly.
        //
        // Expiry belongs in a PlayMode test, where the player loop supplies a
        // real deltaTime and Time.captureDeltaTime can pin it per frame.
    }
}
