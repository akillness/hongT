// Hit-feel budget (presentation-impact-spec #1/#2, AMENDMENT #11 companion).
//
// WHY THIS EXISTS: the hit-stop and camera-punch decisions used to be an if/else-if
// chain inline in GameView.DispatchEvents, which made two things impossible to see and
// impossible to test — (a) a plain melee connect produced NO tactile channel at all
// (no hit-stop, no punch; only flash + spark + SFX + number), and (b) two chains with
// different merge rules decided the same frame's feel.
//
// This file is the single place that answers "how much does this tick hurt". It is
// deliberately free of UnityEngine types so it is exercisable in EditMode without a
// scene, a camera, or a running sim.
namespace CinderCourt.View
{
    /// <summary>One frame's resolved impact feedback. A readonly struct: no allocation
    /// on the per-tick path.</summary>
    public readonly struct ImpactPulse
    {
        /// <summary>Seconds to hold at <c>GameView.HitStopScale</c>. Already merged with
        /// whatever was still live, so the caller assigns it rather than Max-ing again.</summary>
        public readonly float HitStop;

        /// <summary>Camera punch amplitude, 0 when this tick earns no punch. The
        /// accessibility gate for camera motion stays in <c>CameraRig.Punch</c>
        /// (ViewPrefs.ReducedMotion) — it is NOT duplicated here.</summary>
        public readonly float PunchAmplitude;

        /// <summary>Camera punch duration in seconds, 0 when there is no punch.</summary>
        public readonly float PunchDuration;

        /// <summary>True when a Light pulse actually fired, i.e. the caller must restart
        /// its refractory clock. False when the Light hit was swallowed by the refractory
        /// window or outranked by a heavier tier.</summary>
        public readonly bool ConsumedLight;

        public ImpactPulse(float hitStop, float punchAmplitude, float punchDuration, bool consumedLight)
        {
            HitStop = hitStop;
            PunchAmplitude = punchAmplitude;
            PunchDuration = punchDuration;
            ConsumedLight = consumedLight;
        }
    }

    /// <summary>
    /// The impact tier table. Three tiers, strictly ordered Finisher &gt; Kill &gt; Light,
    /// so a tick that raises several events resolves to exactly one — no stacking, no
    /// order-dependent if/else-if.
    /// </summary>
    public static class ImpactBudget
    {
        // Light = a normal melee connect on an enemy that survives. 28 ms is under two
        // frames at 60 Hz: enough for the swing to "catch" on the target, short enough
        // that it never reads as a stutter. This tier is the one that did not exist
        // before — its absence is why ordinary swings felt like they passed through.
        public const float LightHitStop = 0.028f;

        // Kill was 0.04 s before this file existed; raised slightly so a kill still
        // clearly outweighs the new Light tier instead of sitting 12 ms above it.
        public const float KillHitStop = 0.045f;

        // Finisher was 0.07 s; nudged to 0.075 s to keep the same gap over Kill. The
        // presentation spec caps hit-stop at 80 ms, so this stays inside the cap.
        public const float FinisherHitStop = 0.075f;

        // A Light pulse may not re-arm for 140 ms. Without this, a 3-hit chain into a
        // crowd fires a Light stop nearly every tick and the screen congeals into
        // slow motion — the failure mode that makes "more hit-stop" a downgrade.
        public const float LightRefractory = 0.14f;

        // Punch tiers. Kill/Finisher reproduce the amplitudes GameView already used, so
        // this refactor cannot weaken an existing shake. Light is deliberately an order
        // of magnitude below Kill: it should be felt, not seen.
        public const float LightPunchAmplitude = 0.012f;
        public const float LightPunchDuration = 0.05f;
        public const float KillPunchAmplitude = 0.02f;
        public const float KillPunchDuration = 0.08f;
        public const float FinisherPunchAmplitude = 0.05f;
        public const float FinisherPunchDuration = 0.14f;

        /// <summary>
        /// Resolves one tick of impact feedback.
        /// </summary>
        /// <param name="light">A normal hit landed on a surviving enemy (SimEvents.EnemyHit).</param>
        /// <param name="kill">An enemy died this tick (SimEvents.EnemyKilled).</param>
        /// <param name="finisher">A combo finisher connected (SimEvents.ComboFinisher).</param>
        /// <param name="liveHitStop">Hit-stop seconds still running.</param>
        /// <param name="secondsSinceLastLight">Unscaled seconds since the last Light pulse.</param>
        /// <param name="timeEffectsAllowed">ViewPrefs.TimeEffectsAllowed (false under reduced motion).</param>
        /// <returns>
        /// The merged pulse. <see cref="ImpactPulse.HitStop"/> is never below
        /// <paramref name="liveHitStop"/>: a cheap tier can extend a stop but can never
        /// cut one short.
        /// </returns>
        public static ImpactPulse Resolve(
            bool light,
            bool kill,
            bool finisher,
            float liveHitStop,
            float secondsSinceLastLight,
            bool timeEffectsAllowed)
        {
            float requested;
            float punchAmplitude;
            float punchDuration;
            bool consumedLight = false;

            if (finisher)
            {
                requested = FinisherHitStop;
                punchAmplitude = FinisherPunchAmplitude;
                punchDuration = FinisherPunchDuration;
            }
            else if (kill)
            {
                requested = KillHitStop;
                punchAmplitude = KillPunchAmplitude;
                punchDuration = KillPunchDuration;
            }
            else if (light && secondsSinceLastLight >= LightRefractory)
            {
                requested = LightHitStop;
                punchAmplitude = LightPunchAmplitude;
                punchDuration = LightPunchDuration;
                consumedLight = true;
            }
            else
            {
                // Either nothing happened, or a Light hit landed inside the refractory
                // window. Both grant nothing — and a swallowed Light gets no punch
                // either, otherwise the camera buzzes through every crowd fight.
                requested = 0f;
                punchAmplitude = 0f;
                punchDuration = 0f;
            }

            // Reduced motion buys out the time channel only. Camera motion is gated once,
            // inside CameraRig, so the punch is reported unchanged and refused there.
            if (!timeEffectsAllowed)
            {
                requested = 0f;
            }

            float merged = requested > liveHitStop ? requested : liveHitStop;
            return new ImpactPulse(merged, punchAmplitude, punchDuration, consumedLight);
        }
    }
}
