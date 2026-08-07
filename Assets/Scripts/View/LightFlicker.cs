// LightFlicker — view-only mood animation for the §E6 dungeon point lights.
// Never touches sim state and never allocates per frame (docs/SIM_SPEC_ENVIRONMENT.md §E6/§E7).
using UnityEngine;

namespace CinderCourt.View
{
    /// <summary>
    /// Breathes a point light's intensity around its authored base so torch
    /// and altar pools read as living fire instead of static blobs. The phase
    /// is derived from the light role, so the four lights never pulse in
    /// lockstep. Respects <see cref="ViewPrefs.ReducedMotion"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LightFlicker : MonoBehaviour
    {
        /// <summary>Fraction of the base intensity the flicker swings by.</summary>
        public const float Depth = 0.18f;

        Light _light;
        float _baseIntensity;
        float _phase;
        float _rate;

        /// <summary>Binds the flicker to its light's authored intensity.</summary>
        public void Configure(float baseIntensity, int role)
        {
            _light = GetComponent<Light>();
            _baseIntensity = baseIntensity;
            // Distinct irrational-ish phase/rate per role: four lights, four
            // rhythms, no beat frequency that would read as a synchronized pulse.
            _phase = role * 1.618f;
            _rate = 2.3f + role * 0.37f;
        }

        /// <summary>Intensity this flicker would emit at the given time (pure).</summary>
        public static float IntensityAt(float baseIntensity, int role, float time)
        {
            var phase = role * 1.618f;
            var rate = 2.3f + role * 0.37f;
            var wave = Mathf.Sin(time * rate + phase) * 0.6f
                       + Mathf.Sin(time * rate * 2.7f + phase * 3f) * 0.4f;
            return baseIntensity * (1f + Depth * wave);
        }

        void Update()
        {
            if (_light == null) return;
            if (ViewPrefs.ReducedMotion)
            {
                _light.intensity = _baseIntensity;
                return;
            }
            var wave = Mathf.Sin(Time.time * _rate + _phase) * 0.6f
                       + Mathf.Sin(Time.time * _rate * 2.7f + _phase * 3f) * 0.4f;
            _light.intensity =
                _baseIntensity * (1f + Depth * ViewPrefs.MotionScale * wave);
        }
    }
}
