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
        // Prologue side view: a 2.5D beat-em-up frame (26° pitch, south-facing)
        // replaces the old 90° top-down. Ortho height must cover the arena's
        // world depth (5.4 u) projected at 26° plus actor height ≈ 3.6.
        const float PrologueOrthoSize = 3.6f;
        const float ProloguePitch = 26f;
        const float PrologueDistance = 12f;
        static readonly Vector3 ArenaCenter = ViewWorld.ToWorld(768f, 604f);

        Camera _camera;
        Vector3 _basePosition;
        Quaternion _baseRotation;
        float _shakeTime, _shakeDuration, _shakeAmplitude;
        float _lastAspect;
        // Portrait/narrow compensation factor (mobile spec #9). 1 at the
        // reference 3:2 aspect; grows as the viewport narrows so Dungeon
        // orbit distance and Prologue ortho width preserve battlefield
        // coverage that the landscape-tuned constants assume.
        float _aspectWiden = 1f;

        Profile _profile = Profile.Arena;
        float _profileTime;
        // Dungeon distance tiers (spec §10): calm 17, big-wave/boss 21.
        float _dungeonDistance = 17f;
        float _dungeonTargetDistance = 17f;
        // Outskirt fog offsets from the live orbit distance. Derived from the
        // measured floor geometry at pitch 55: the far playable edge sits
        // ~1.7 u beyond the focus depth and the apron rim ~5.5 u beyond, so
        // starting the band 2 u out keeps the whole play area clear while the
        // rim lands at the far end. Baked scene values (19/22.5) remain the
        // Arena/Prologue baseline — those cameras never move.
        const float FogStartOffset = 2f;
        const float FogEndOffset = 5.5f;
        // Prologue reveal interpolation state.
        float _revealT;

        // Scene-authored fog band, captured at Awake and restored whenever a
        // non-dungeon profile takes over.
        float _bakedFogStart = 19f;
        float _bakedFogEnd = 22.5f;
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
            // Snapshot the scene's authored fog band before any dungeon run
            // overwrites it — this is what non-dungeon profiles restore to.
            _bakedFogStart = RenderSettings.fogStartDistance;
            _bakedFogEnd = RenderSettings.fogEndDistance;
            ApplyAspect(true);
        }

        public void SetProfile(Profile profile)
        {
            if (_profile == profile)
            {
                _focusTimer = 0f;
                _focusDuration = 1f;
                _focusTarget = Vector3.zero;
                return;
            }
            _profile = profile;
            _profileTime = 0f;
            _revealT = 0f;
            _focusTimer = 0f;   // stale boss focus must not survive a run exit
            _focusDuration = 1f;
            _focusTarget = Vector3.zero;
            // The dungeon branch drives RenderSettings fog every frame, and
            // RenderSettings is GLOBAL — without this, Lobby and Arena inherit
            // whatever band the last dungeon run left (up to 23/26.5 after a
            // boss wave), which un-dissolves their apron rims. Restore the
            // baked scene values on every non-dungeon profile.
            if (profile != Profile.Dungeon)
            {
                RenderSettings.fogStartDistance = _bakedFogStart;
                RenderSettings.fogEndDistance = _bakedFogEnd;
            }
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
                    // Side view (user request): reads like a 2D fighter plane
                    // while sim depth (sim Y) stays visible as slight vertical
                    // parallax — WASD/joystick keep their existing meaning.
                    _camera.orthographic = true;
                    _camera.orthographicSize = PrologueOrthoSize;
                    PlaceOrbit(ProloguePitch, PrologueDistance, ArenaCenter);
                    ApplyAspect(true);
                    break;
                case Profile.PrologueReveal:
                    _camera.orthographic = false; // perspective blend-down
                    _camera.fieldOfView = 42f;
                    break;
                case Profile.Dungeon:
                    _camera.orthographic = false;
                    _camera.fieldOfView = 42f;    // original-verified combat FOV
                    _dungeonDistance = _dungeonTargetDistance = 17f;
                    ApplyAspect(true);
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
            if (ViewPrefs.ReducedMotion) return;
            var scaledAmplitude = amplitude * ViewPrefs.MotionScale;
            if (scaledAmplitude <= 0f) return;
            _shakeDuration = duration;
            _shakeTime = duration;
            _shakeAmplitude = scaledAmplitude;
        }

        void ApplyAspect(bool force)
        {
            if (_camera == null) return;
            var aspect = _camera.aspect;
            if (!force && Mathf.Approximately(aspect, _lastAspect)) return;
            _lastAspect = aspect;
            // Mobile spec #9: portrait aspect 0.462 leaves 28% (Prologue) /
            // 59% (Dungeon FOV 42) of the arena width visible — widen by the
            // narrowness ratio, upper clamp 2.2 (was 2.0, Arena-only).
            _aspectWiden = Mathf.Clamp(ReferenceAspect / Mathf.Max(0.5f, aspect), 1f, 2.2f);
            switch (_profile)
            {
                case Profile.Arena:
                    _camera.fieldOfView = BaseFov * _aspectWiden;
                    break;
                case Profile.Prologue:
                    _camera.orthographicSize = PrologueOrthoSize * _aspectWiden;
                    break;
                // Dungeon consumes _aspectWiden as a distance multiplier at
                // the PlaceOrbit call site (FOV 42 stays — verified value).
            }
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
                    ApplyAspect(false);   // resize/rotate during play (spec #9)
                    PlaceOrbit(ProloguePitch, PrologueDistance, ArenaCenter);
                    ApplyShakeOffset();
                    break;


                case Profile.PrologueReveal:
                {
                    // 2.2 s sweep: 26° side view -> 55° dungeon perspective.
                    // Start distance matches the side-view ortho frame:
                    // orthoSize / tan(FOV/2) = 3.6 / tan(21°) ≈ 9.4 — no pop.
                    // Both endpoints scale by _aspectWiden so the sweep stays
                    // pop-free against the widened ortho frame (start) and the
                    // widened dungeon orbit (end) on narrow aspects.
                    ApplyAspect(false);
                    _revealT = Mathf.Clamp01(_profileTime / 2.2f);
                    var eased = 1f - Mathf.Pow(1f - _revealT, 3f);
                    var pitch = Mathf.Lerp(ProloguePitch, 55f, eased);
                    var distance = Mathf.Lerp(9.4f, 17f, eased) * _aspectWiden;
                    PlaceOrbit(pitch, distance, ArenaCenter);
                    break;
                }

                case Profile.Dungeon:
                {
                    ApplyAspect(false);   // portrait: distance-widen, FOV fixed
                    _dungeonDistance = Mathf.Lerp(
                        _dungeonDistance, _dungeonTargetDistance,
                        1f - Mathf.Exp(-Time.deltaTime * 2.2f));
                    // Boss-intro focus pull (cycle2 A1): blend orbit focus
                    // toward the pulse target, then back. View-only.
                    var focus = ArenaCenter;
                    if (_focusTimer > 0f)
                    {
                        _focusTimer -= Time.deltaTime;
                        var phase = 1f - Mathf.Clamp01(_focusTimer / _focusDuration);
                        // ease out to target in first half, ease back in second
                        var blend = phase < 0.5f
                            ? Mathf.SmoothStep(0f, 1f, phase * 2f)
                            : Mathf.SmoothStep(1f, 0f, (phase - 0.5f) * 2f);
                        focus = Vector3.Lerp(ArenaCenter, _focusTarget, blend * 0.55f);
                    }
                    PlaceOrbit(55f, _dungeonDistance * _aspectWiden, focus);
                    // Outskirt fog must TRACK the orbit, not sit at a baked
                    // distance. The dungeon has two tiers (calm 17, big-wave/
                    // boss 21), and a static 19/22.5 band tuned for calm fogs
                    // the arena centre 57% and the far playable edge 100% once
                    // the camera pulls back — the boss would dissolve into the
                    // background at the exact moment it spawns. Offsetting
                    // from the live distance keeps the playable area at 0% in
                    // both tiers while still dissolving the apron rim ~95%+.
                    var fogNear = _dungeonDistance * _aspectWiden;
                    RenderSettings.fogStartDistance = fogNear + FogStartOffset;
                    RenderSettings.fogEndDistance = fogNear + FogEndOffset;
                    ApplyShakeOffset();
                    break;
                }
            }
        }


        float _focusTimer, _focusDuration = 1f;
        Vector3 _focusTarget;

        /// <summary>
        /// Dungeon-only camera focus pull toward a world point (boss intro).
        /// Blends 55% of the way out and back over <paramref name="duration"/>.
        /// </summary>
        public void FocusPulse(Vector3 worldTarget, float duration)
        {
            if (ViewPrefs.ReducedMotion) return;   // A5 accessibility gate
            _focusTarget = worldTarget;
            _focusDuration = Mathf.Max(0.2f, duration);
            _focusTimer = _focusDuration;
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
        // --- append-only presentation API (spec #2, JuiceLane) ---------------
        // Extra shake tiers are requested by GameView instead of extending the
        // OnEvents chain above (MobileLane owns aspect/profile code paths).

        /// <summary>
        /// Request a shake without stomping a stronger one already playing:
        public void Punch(float amplitude, float duration)
        {
            if (ViewPrefs.ReducedMotion) return;
            var scaledAmplitude = amplitude * ViewPrefs.MotionScale;
            if (scaledAmplitude <= 0f) return;
            if (_shakeTime > 0f)
            {
                var falloff = Mathf.Clamp01(_shakeTime / Mathf.Max(0.0001f, _shakeDuration));
                if (_shakeAmplitude * falloff >= scaledAmplitude) return;
            }
            _shakeDuration = duration;
            _shakeTime = duration;
            _shakeAmplitude = scaledAmplitude;
        }
    }
}
