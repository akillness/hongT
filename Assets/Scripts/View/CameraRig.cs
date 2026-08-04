// Mode-aware camera: arena framing (v0.1 behavior preserved), lobby orbit,
// prologue top-down ortho with a 2.5D reveal, dungeon 55° perspective with
// crowd-density distance tiers (docs/SIM_SPEC_HACKSLASH.md §1, §10).
using CinderCourt.Sim;
using UnityEngine;

namespace CinderCourt.View
{
    public sealed class CameraRig : MonoBehaviour
    {
        public enum Profile { Arena, Lobby, Prologue, PrologueReveal, Dungeon }

        const float BaseFov = 32f;
        const float ReferenceAspect = 1.5f;
        static readonly Vector3 ArenaCenter = ViewWorld.ToWorld(768f, 604f);

        Camera _camera;
        Vector3 _basePosition;
        Quaternion _baseRotation;
        float _shakeTime, _shakeDuration, _shakeAmplitude;
        float _lastAspect;

        Profile _profile = Profile.Arena;
        float _profileTime;
        // Dungeon distance tiers (spec §10): calm 17, big-wave/boss 21.
        float _dungeonDistance = 17f;
        float _dungeonTargetDistance = 17f;
        // Prologue reveal interpolation state.
        float _revealT;

        public Profile Current => _profile;

        void Awake()
        {
            // SceneBuilder bakes Main Camera into the scene, so it exists at
            // Awake. GameDirector.Attach (also Awake-time) calls SetProfile
            // BEFORE Start would run — a Start-time grab would silently skip
            // the boot profile's projection setup (ortho prologue etc.).
            _camera = Camera.main;
            if (_camera == null) return;
            _basePosition = _camera.transform.position;
            _baseRotation = _camera.transform.rotation;
            ApplyAspect(true);
        }

        public void SetProfile(Profile profile)
        {
            if (_profile == profile) return;
            _profile = profile;
            _profileTime = 0f;
            _revealT = 0f;
            if (_camera == null) return;
            switch (profile)
            {
                case Profile.Arena:
                    _camera.orthographic = false;
                    _camera.fieldOfView = BaseFov;
                    _camera.transform.SetPositionAndRotation(_basePosition, _baseRotation);
                    ApplyAspect(true);
                    break;
                case Profile.Lobby:
                    _camera.orthographic = false;
                    _camera.fieldOfView = 36f;
                    break;
                case Profile.Prologue:
                    // Vertical top-down orthographic — reads as a 2D defense game.
                    _camera.orthographic = true;
                    _camera.orthographicSize = 2.7f * 1.15f;   // arena half-height * 1.15
                    _camera.transform.position = ArenaCenter + new Vector3(0f, 14f, 0f);
                    _camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case Profile.PrologueReveal:
                    _camera.orthographic = false; // perspective blend-down
                    _camera.fieldOfView = 42f;
                    break;
                case Profile.Dungeon:
                    _camera.orthographic = false;
                    _camera.fieldOfView = 42f;    // original-verified combat FOV
                    _dungeonDistance = _dungeonTargetDistance = 17f;
                    break;
            }
        }

        /// <summary>Dungeon crowd tier: big wave / boss pulls the camera out.</summary>
        public void SetDungeonCrowd(bool bigWave)
            => _dungeonTargetDistance = bigWave ? 21f : 17f;

        public void OnEvents(SimEvents events)
        {
            if ((events & SimEvents.NovaCast) != 0) Shake(0.2f, 0.06f);
            else if ((events & SimEvents.BossPhase2) != 0) Shake(0.3f, 0.09f);
            else if ((events & SimEvents.PlayerDamaged) != 0) Shake(0.12f, 0.045f);
        }

        void Shake(float duration, float amplitude)
        {
            _shakeDuration = duration;
            _shakeTime = duration;
            _shakeAmplitude = amplitude;
        }

        void ApplyAspect(bool force)
        {
            if (_camera == null || _profile != Profile.Arena) return;
            var aspect = _camera.aspect;
            if (!force && Mathf.Approximately(aspect, _lastAspect)) return;
            _lastAspect = aspect;
            var widen = Mathf.Clamp(ReferenceAspect / Mathf.Max(0.5f, aspect), 1f, 2f);
            _camera.fieldOfView = BaseFov * widen;
        }

        void LateUpdate()
        {
            if (_camera == null) return;
            _profileTime += Time.deltaTime;

            switch (_profile)
            {
                case Profile.Arena:
                    ApplyAspect(false);
                    ApplyShakeAround(_basePosition, _baseRotation);
                    break;

                case Profile.Lobby:
                {
                    // Slow orbit: yaw ±6°, 24 s lap around the arena center.
                    var yaw = Mathf.Sin(_profileTime * (2f * Mathf.PI / 24f)) * 6f;
                    var rotation = Quaternion.Euler(18f, yaw, 0f);
                    var position = ArenaCenter + rotation * new Vector3(0f, 2.6f, -9.5f);
                    _camera.transform.SetPositionAndRotation(
                        position, Quaternion.LookRotation(
                            ArenaCenter + new Vector3(0f, 1.1f, 0f) - position));
                    break;
                }

                case Profile.Prologue:
                    ApplyShakeAround(
                        ArenaCenter + new Vector3(0f, 14f, 0f),
                        Quaternion.Euler(90f, 0f, 0f));
                    break;

                case Profile.PrologueReveal:
                {
                    // 2.2 s sweep: 90° top-down -> 55° perspective (the "2.5D reveal").
                    // Start distance matches the ortho frame exactly:
                    // orthoSize / tan(FOV/2) = 3.105 / tan(21°) ≈ 8.1 — no size pop.
                    _revealT = Mathf.Clamp01(_profileTime / 2.2f);
                    var eased = 1f - Mathf.Pow(1f - _revealT, 3f);
                    var pitch = Mathf.Lerp(90f, 55f, eased);
                    var distance = Mathf.Lerp(8.1f, 17f, eased);
                    PlaceOrbit(pitch, distance, ArenaCenter);
                    break;
                }

                case Profile.Dungeon:
                {
                    _dungeonDistance = Mathf.Lerp(
                        _dungeonDistance, _dungeonTargetDistance,
                        1f - Mathf.Exp(-Time.deltaTime * 2.2f));
                    PlaceOrbit(55f, _dungeonDistance, ArenaCenter);
                    ApplyShakeOffset();
                    break;
                }
            }
        }

        void PlaceOrbit(float pitch, float distance, Vector3 focus)
        {
            var rotation = Quaternion.Euler(pitch, 0f, 0f);
            var position = focus - rotation * Vector3.forward * distance;
            _camera.transform.SetPositionAndRotation(position, rotation);
        }

        void ApplyShakeAround(Vector3 position, Quaternion rotation)
        {
            if (_shakeTime > 0f)
            {
                _shakeTime -= Time.deltaTime;
                var falloff = Mathf.Clamp01(_shakeTime / Mathf.Max(0.0001f, _shakeDuration));
                var offset = ShakeOffset(falloff);
                _camera.transform.SetPositionAndRotation(position + offset, rotation);
            }
            else if (_camera.transform.position != position)
            {
                _camera.transform.SetPositionAndRotation(position, rotation);
            }
        }

        void ApplyShakeOffset()
        {
            if (_shakeTime <= 0f) return;
            _shakeTime -= Time.deltaTime;
            var falloff = Mathf.Clamp01(_shakeTime / Mathf.Max(0.0001f, _shakeDuration));
            _camera.transform.position += ShakeOffset(falloff);
        }

        Vector3 ShakeOffset(float falloff)
            => new Vector3(
                Mathf.PerlinNoise(Time.time * 37f, 0.3f) - 0.5f,
                Mathf.PerlinNoise(0.7f, Time.time * 41f) - 0.5f,
                0f) * (2f * _shakeAmplitude * falloff);
    }
}
