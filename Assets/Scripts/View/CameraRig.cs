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
        // replaces the old 90° top-down; ortho height covers the arena depth.
        // Prologue/Lobby constants below were tuned against ViewWorld.Scale
        // 0.01; they are multiplied by LegacyScaleRatio so the 0.0125 dungeon
        // enlargement does not silently re-frame those profiles.
        const float PrologueOrthoSize = 3.6f * ViewWorld.LegacyScaleRatio;
        const float ProloguePitch = 26f;
        const float PrologueDistance = 12f * ViewWorld.LegacyScaleRatio;
        static readonly Vector3 ArenaCenter = ViewWorld.ToWorld(768f, 604f);
        /// <summary>Arena centre under the pre-2026-10 quotient (scene rebasing).</summary>
        static readonly Vector3 LegacyArenaCenter =
            new Vector3(768f * ViewWorld.LegacyScale, 0f, -604f * ViewWorld.LegacyScale);

        Camera _camera;
        Vector3 _basePosition;
        Quaternion _baseRotation;
        float _shakeTime, _shakeDuration, _shakeAmplitude;
        float _lastAspect;

        // ---- W9 cinematic flourish (FOV punch + view roll) -------------------
        // Shake answers "how hard did that land". It cannot answer "this moment
        // matters" — a 2D Perlin offset has no weight channel, so a finisher and
        // a boss phase change read as the same jitter at different amplitudes.
        // A short FOV punch (dolly feel) plus a sub-2° roll adds that second
        // channel WITHOUT touching position, so it composes with shake instead
        // of fighting it.
        //
        // Everything here is hard-bounded on purpose: the telegraph rings and
        // hazard bands are the game's judgement surface, and a camera that
        // re-frames them is a camera that lies about the hit box. 4° of FOV over
        // ≤0.3 s moves the arena edge by ~7% and returns; the screen-space HUD
        // is unaffected by both channels.
        const float MaxFlourishFov = 4f;        // degrees, absolute
        const float MaxFlourishRoll = 1.5f;     // degrees, absolute
        const float MaxFlourishDuration = 0.3f; // seconds
        /// <summary>Attack fraction: the peak lands early so it sits ON the
        /// impact frame, then eases out over the remaining 75%.</summary>
        const float FlourishAttack = 0.25f;
        /// <summary>Shake amplitude that counts as "full load" for the
        /// composition clamp — the strongest shake in the game (BossPhase2).</summary>
        const float ShakeLoadReference = 0.09f;
        float _flourishTime, _flourishDuration, _flourishFov, _flourishRoll;
        bool _flourishActive;
        /// <summary>Rotation the profile placed this frame, BEFORE roll. Roll is
        /// post-multiplied onto this instead of onto the live transform, so it
        /// can never accumulate across frames.</summary>
        Quaternion _placedRotation = Quaternion.identity;
        // Portrait/narrow compensation factor (mobile spec #9). 1 at the
        // reference 3:2 aspect; grows as the viewport narrows so Dungeon
        // orbit distance and Prologue ortho width preserve battlefield
        // coverage that the landscape-tuned constants assume.
        float _aspectWiden = 1f;

        Profile _profile = Profile.Arena;
        float _profileTime;
        // Dungeon distance tiers — spec §10 baseline was calm 17 / big-wave 21;
        // character-shrink decision (2026-08, camera-distance-only) scaled both
        // tiers ~x1.17 so actors read smaller while arena coords stay untouched.
        //
        // 2026-08 pull-in: 20/24.5 -> 17.5/21.5. MEASURED reason — the frame's
        // flat mass is ground past fogEnd, which linear fog replaces wholesale
        // with one colour, so no texture or tone work can reach it (proved by
        // baking VoidFloor magenta: it renders 0.25% of the frame, and both the
        // grain and tone passes moved the dominant colour bucket 0.0 pt). The
        // only lever left is AREA: the ground footprint scales with distance
        // while the fog band is a fixed 3.5 u, so pulling in shrinks the
        // flat-fog region and enlarges the arena at the same time.
        //
        // Cost, stated plainly: less of the arena is on screen, so combat sight
        // lines shorten. The tiers keep their ratio (1.229) so the calm/crowd
        // relationship the wave pacing depends on is unchanged.
        const float DungeonCalmDistance = 17.5f;
        const float DungeonCrowdDistance = 21.5f;
        float _dungeonDistance = DungeonCalmDistance;
        float _dungeonTargetDistance = DungeonCalmDistance;

        // ---- dungeon player-follow (2026-10) -------------------------------
        // 0.935 of the arena half-extent, so following ~55% of the x reach and
        // ~75% of the (shallower) z reach keeps both boundary walls inside the
        // 42° frustum at the calm orbit distance while the camera still tracks.
        //
        // The 0.55/0.75 fractions were derived at the ORIGINAL calm distance of
        // 20 u. Frustum ground extent scales with distance, so pulling the
        // camera in without shrinking these would let the far arena edge leave
        // the frame when the player sits at a clamp extreme — and no gate would
        // catch it (the framing test only asserts clamp < reach, which stays
        // true). Scale them by the distance ratio so they track any future
        // change instead of silently going stale.
        const float ClampDerivationDistance = 20f;
        const float ClampDistanceRatio = DungeonCalmDistance / ClampDerivationDistance;
        internal const float FollowClampX =
            SimConfig.ArenaHalfWidth * ViewWorld.Scale * 0.55f * ClampDistanceRatio;
        internal const float FollowClampZ =
            SimConfig.ArenaHalfHeight * ViewWorld.Scale * 0.75f * ClampDistanceRatio;
        /// <summary>Exponential-smoothing rate for the follow focus (per second).</summary>
        internal const float FollowLambda = 4.5f;
        Vector3 _followAnchor;
        Vector3 _followFocus;
        bool _hasFollowAnchor;

        /// <summary>
        /// Dungeon-only: world-space point the camera should track (the player).
        /// Ignored by every other profile; call once per rendered frame.
        /// </summary>
        public void SetFollowAnchor(Vector3 world)
        {
            _followAnchor = world;
            if (!_hasFollowAnchor)
            {
                // First anchor of a run must not sweep in from the centre.
                _hasFollowAnchor = true;
                _followFocus = ClampFollow(world);
            }
        }

        /// <summary>Drops the follow anchor (run exit); focus returns to centre.</summary>
        public void ClearFollowAnchor()
        {
            _hasFollowAnchor = false;
            _followFocus = ArenaCenter;
        }

        internal static Vector3 ClampFollow(Vector3 world)
        {
            var offset = world - ArenaCenter;
            return ArenaCenter + new Vector3(
                Mathf.Clamp(offset.x, -FollowClampX, FollowClampX),
                0f,
                Mathf.Clamp(offset.z, -FollowClampZ, FollowClampZ));
        }

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
            // ViewWorld.Scale grew 0.01 -> 0.0125 (bigger dungeon). The Arena
            // camera position is scene-authored against the OLD quotient, so
            // rebase it around the new arena centre by the same ratio — Arena
            // framing must not change, only the dungeon's.
            var bakedLegacyOffset = _camera.transform.position - LegacyArenaCenter;
            _basePosition = ArenaCenter + bakedLegacyOffset * ViewWorld.LegacyScaleRatio;
            _baseRotation = _camera.transform.rotation;
            _placedRotation = _baseRotation;
            _camera.transform.position = _basePosition;
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
            // A stale anchor from the previous run would snap the next dungeon
            // entry to wherever the last player died.
            ClearFollowAnchor();
            _revealT = 0f;
            _focusTimer = 0f;   // stale boss focus must not survive a run exit
            _focusDuration = 1f;
            _focusTarget = Vector3.zero;
            // A live FOV punch must not survive into the next profile: the
            // profile switch below rewrites fieldOfView, and a flourish still
            // running would keep adding its delta to the NEW baseline.
            _flourishTime = 0f;
            _flourishActive = false;
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
                    _dungeonDistance = _dungeonTargetDistance = DungeonCalmDistance;

                    ApplyAspect(true);
                    break;
            }
        }

        /// <summary>Dungeon crowd tier: big wave / boss pulls the camera out.</summary>
        public void SetDungeonCrowd(bool bigWave)
            => _dungeonTargetDistance = bigWave ? DungeonCrowdDistance : DungeonCalmDistance;


        public void OnEvents(SimEvents events)
        {
            if ((events & SimEvents.NovaCast) != 0) Shake(0.2f, 0.06f);
            else if ((events & SimEvents.BossPhase2) != 0) Shake(0.3f, 0.09f);
            else if ((events & SimEvents.PlayerDamaged) != 0) Shake(0.12f, 0.045f);

            // --- W9 flourish chain ------------------------------------------
            // A SEPARATE chain from the shake above, deliberately: the two
            // channels answer different questions and a moment can earn one
            // without the other (a boss phase change is not a big hit, it is a
            // big MOMENT). Ordered strongest-first; Flourish itself refuses to
            // weaken a live one, so the ordering is belt-and-braces.
            //
            // Sign convention: NEGATIVE fov = the frame closes in (a finisher
            // pulls the player toward you); POSITIVE fov = the frame opens out
            // (a boss arriving makes the room bigger than you). The sim has no
            // Critical event — ComboFinisher IS this game's critical hit
            // (SimTypes.cs SimEvents, 1 << 21), so it carries that beat.
            if ((events & SimEvents.BossPhase2) != 0) Flourish(3.5f, -1.4f, 0.30f);
            else if ((events & SimEvents.BossSpawned) != 0) Flourish(2.6f, 0f, 0.28f);
            else if ((events & SimEvents.ComboFinisher) != 0) Flourish(-2.2f, 0.9f, 0.18f);
            else if ((events & SimEvents.NovaCast) != 0) Flourish(-1.6f, 0.6f, 0.22f);
            else if ((events & SimEvents.LevelUp) != 0) Flourish(-1.2f, 0f, 0.24f);
        }

        /// <summary>
        /// W9: a short, bounded camera flourish — FOV punch and/or view roll.
        /// Both channels are clamped hard (<see cref="MaxFlourishFov"/> /
        /// <see cref="MaxFlourishRoll"/> / <see cref="MaxFlourishDuration"/>)
        /// and disabled entirely under reduced motion, mirroring
        /// <see cref="Punch"/>'s accessibility contract. Negative
        /// <paramref name="fovPunch"/> closes the frame in, positive opens it out.
        /// </summary>
        public void Flourish(float fovPunch, float rollDegrees, float duration)
        {
            if (ViewPrefs.ReducedMotion) return;
            var scale = ViewPrefs.MotionScale;
            fovPunch = Mathf.Clamp(fovPunch * scale, -MaxFlourishFov, MaxFlourishFov);
            rollDegrees = Mathf.Clamp(rollDegrees * scale, -MaxFlourishRoll, MaxFlourishRoll);
            if (fovPunch == 0f && rollDegrees == 0f) return;
            duration = Mathf.Clamp(duration, 0.05f, MaxFlourishDuration);
            // Same non-stomp rule as Punch: a weaker request must not cut a
            // stronger flourish short mid-envelope.
            if (_flourishTime > 0f)
            {
                var falloff = Mathf.Clamp01(_flourishTime / Mathf.Max(0.0001f, _flourishDuration));
                if (Mathf.Abs(_flourishFov) * falloff >= Mathf.Abs(fovPunch)
                    && Mathf.Abs(_flourishRoll) * falloff >= Mathf.Abs(rollDegrees))
                    return;
            }
            _flourishFov = fovPunch;
            _flourishRoll = rollDegrees;
            _flourishDuration = duration;
            _flourishTime = duration;
        }

        /// <summary>
        /// The profile's UNFLOURISHED field of view. The flourish is a delta on
        /// top of this every frame rather than a mutation of fieldOfView, so it
        /// can never drift the baseline that <see cref="ApplyAspect"/> and
        /// <see cref="SetProfile"/> own.
        /// </summary>
        float BaseFovForProfile() => _profile switch
        {
            Profile.Arena => BaseFov * _aspectWiden,
            Profile.Lobby => 36f,
            Profile.PrologueReveal => 42f,
            Profile.Dungeon => 42f,          // original-verified combat FOV
            _ => _camera.fieldOfView,        // Prologue is ortho — no FOV channel
        };

        /// <summary>
        /// W9 per-frame application. Runs AFTER the profile has placed the
        /// camera (so <see cref="_placedRotation"/> is this frame's authored
        /// rotation) and after shake, whose live load scales the flourish down
        /// so the two channels together stay inside the same budget either one
        /// has alone.
        /// </summary>
        void ApplyFlourish()
        {
            if (_flourishTime <= 0f)
            {
                if (!_flourishActive) return;
                // One restore write: back to the exact authored FOV/rotation.
                _flourishActive = false;
                if (!_camera.orthographic) _camera.fieldOfView = BaseFovForProfile();
                _camera.transform.rotation = _placedRotation;
                return;
            }
            _flourishTime -= Time.deltaTime;
            _flourishActive = true;
            var progress = 1f - Mathf.Clamp01(_flourishTime / Mathf.Max(0.0001f, _flourishDuration));
            // Fast attack, slow release — both smoothstepped so neither end pops.
            var envelope = progress < FlourishAttack
                ? Mathf.SmoothStep(0f, 1f, progress / FlourishAttack)
                : Mathf.SmoothStep(1f, 0f, (progress - FlourishAttack) / (1f - FlourishAttack));
            // Composition clamp: at full shake load the flourish halves, so a
            // nova (shake 0.06 + fov punch) never stacks into motion sickness.
            var shakeLoad = _shakeTime > 0f
                ? Mathf.Clamp01(_shakeAmplitude
                    * Mathf.Clamp01(_shakeTime / Mathf.Max(0.0001f, _shakeDuration))
                    / ShakeLoadReference)
                : 0f;
            var composite = envelope * (1f - 0.5f * shakeLoad);
            var fov = Mathf.Clamp(_flourishFov * composite, -MaxFlourishFov, MaxFlourishFov);
            var roll = Mathf.Clamp(_flourishRoll * composite, -MaxFlourishRoll, MaxFlourishRoll);
            if (!_camera.orthographic)
                _camera.fieldOfView = Mathf.Clamp(BaseFovForProfile() + fov, 5f, 120f);
            // Post-multiply: z in camera-local space IS the view axis, so this
            // rolls the frame without disturbing the profile's pitch/yaw.
            _camera.transform.rotation = _placedRotation * Quaternion.Euler(0f, 0f, roll);
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
                    _placedRotation = _baseRotation;   // W9 roll base (pre-roll)
                    ApplyShakeAround(_basePosition, _baseRotation);
                    break;

                case Profile.Lobby:
                {
                    // Slow orbit: yaw ±6°, 24 s lap around the arena center.
                    var yaw = Mathf.Sin(_profileTime * (2f * Mathf.PI / 24f)) * 6f;
                    var rotation = Quaternion.Euler(18f, yaw, 0f);
                    var position = ArenaCenter + rotation * new Vector3(0f, 2.6f, -9.5f);
                    var look = Quaternion.LookRotation(
                        ArenaCenter + new Vector3(0f, 1.1f, 0f) - position);
                    _camera.transform.SetPositionAndRotation(position, look);
                    _placedRotation = look;   // W9 roll base (pre-roll)
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
                    var distance = Mathf.Lerp(
                        9.4f * ViewWorld.LegacyScaleRatio, DungeonCalmDistance, eased)
                        * _aspectWiden;

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
                    // Player-follow anchor (2026-10 request): the dungeon
                    // camera is no longer pinned to the arena centre. The
                    // anchor is clamped to FollowClampX/Z around the centre so
                    // the enlarged floor never slides out of frame, then
                    // critically damped toward so strafing does not jitter.
                    var desired = ClampFollow(
                        _hasFollowAnchor ? _followAnchor : ArenaCenter);
                    _followFocus = Vector3.Lerp(
                        _followFocus, desired,
                        1f - Mathf.Exp(-Time.deltaTime * FollowLambda));
                    var focus = _followFocus;
                    // Boss-intro focus pull (cycle2 A1): blend orbit focus
                    // toward the pulse target, then back. View-only.
                    if (_focusTimer > 0f)
                    {
                        _focusTimer -= Time.deltaTime;
                        var phase = 1f - Mathf.Clamp01(_focusTimer / _focusDuration);
                        // ease out to target in first half, ease back in second
                        var blend = phase < 0.5f
                            ? Mathf.SmoothStep(0f, 1f, phase * 2f)
                            : Mathf.SmoothStep(1f, 0f, (phase - 0.5f) * 2f);
                        focus = Vector3.Lerp(_followFocus, _focusTarget, blend * 0.55f);
                    }
                    PlaceOrbit(55f, _dungeonDistance * _aspectWiden, focus);
                    // Outskirt fog must TRACK the orbit, not sit at a baked
                    // distance. The dungeon has two tiers (calm/big-wave-boss,
                    // see DungeonCalmDistance/DungeonCrowdDistance), and a static
                    // 19/22.5 band tuned for calm fogs

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

            // W9 last: every profile has now written its authored placement, so
            // the flourish is a pure delta on top and the shake load it clamps
            // against is this frame's, not the previous one's.
            ApplyFlourish();
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
            _placedRotation = rotation;   // W9 roll base (pre-roll)
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
