// Focused numeric regression coverage for CameraRig's bounded presentation channels.
// EditMode only: pure numeric seams where available; reflection only for live state
// that has no observable clock-independent API. Every fixture restores global prefs,
// fog distances, and created GameObjects.
using System;
using System.Reflection;
using CinderCourt.Sim;
using CinderCourt.View;
using NUnit.Framework;
using UnityEngine;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class CameraRigPresentationTests
    {
        const BindingFlags InstanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags StaticFlags = BindingFlags.NonPublic | BindingFlags.Static;
        const float Epsilon = 1e-4f;

        bool _hadReducedMotionPref;
        int _reducedMotionPrefValue;
        float _bakedFogStart;
        float _bakedFogEnd;

        [SetUp]
        public void SetUp()
        {
            _bakedFogStart = RenderSettings.fogStartDistance;
            _bakedFogEnd = RenderSettings.fogEndDistance;
            _hadReducedMotionPref = PlayerPrefs.HasKey("al:reduced-motion");
            _reducedMotionPrefValue = PlayerPrefs.GetInt("al:reduced-motion");
            ViewPrefs.ReducedMotion = false;
        }

        [TearDown]
        public void TearDown()
        {
            RenderSettings.fogStartDistance = _bakedFogStart;
            RenderSettings.fogEndDistance = _bakedFogEnd;
            ViewPrefs.ReducedMotion = _reducedMotionPrefValue == 1;
            if (_hadReducedMotionPref)
                PlayerPrefs.SetInt("al:reduced-motion", _reducedMotionPrefValue);
            else
                PlayerPrefs.DeleteKey("al:reduced-motion");
            PlayerPrefs.Save();
        }

        [Test]
        public void FollowResponse_ReachesHalfDisplacementWithinOneHundredMilliseconds_WithoutOvershoot()
        {
            const float stepSeconds = 0.01f;
            const float target = 10f;
            var focus = 0f;

            for (var step = 0; step < 10; step += 1)
            {
                var previous = focus;
                var alpha = CameraRig.DampAlpha(stepSeconds, CameraRig.FollowLambda);
                Assert.That(alpha, Is.InRange(0f, 1f),
                    "the damping fraction itself must not permit overshoot");
                focus = Mathf.LerpUnclamped(focus, target, alpha);

                Assert.That(focus, Is.GreaterThanOrEqualTo(previous - Epsilon),
                    $"follow reversed direction at {step * 10 + 10} ms");
                Assert.That(focus, Is.LessThanOrEqualTo(target + Epsilon),
                    $"follow overshot its target at {step * 10 + 10} ms");
            }

            Assert.That(focus, Is.GreaterThanOrEqualTo(target * 0.5f),
                "the camera must cover at least half a new displacement within 100 ms");
        }

        [Test]
        public void VelocityLookAhead_IsRadiallyBounded_AndCannotEscapeEitherFollowClamp()
        {
            var arenaCenter = ViewWorld.ToWorld(SimConfig.ArenaX, SimConfig.ArenaY);
            var diagonalLead = CameraRig.FollowTarget(
                arenaCenter, new Vector3(100f, 80f, -100f));
            var leadOffset = diagonalLead - arenaCenter;

            Assert.That(leadOffset.magnitude,
                Is.EqualTo(CameraRig.FollowLookAheadMax).Within(Epsilon),
                "diagonal velocity must use a radial cap, not one cap per axis");
            Assert.That(diagonalLead.y, Is.EqualTo(0f).Within(Epsilon),
                "velocity look-ahead is a ground-plane pan");

            foreach (var sign in new[] { -1f, 1f })
            {
                var beyondCorner = arenaCenter + new Vector3(
                    CameraRig.FollowClampX * 10f * sign, 40f,
                    CameraRig.FollowClampZ * 10f * sign);
                var clamped = CameraRig.FollowTarget(
                    beyondCorner, new Vector3(100f * sign, 100f, 100f * sign))
                    - arenaCenter;

                Assert.That(clamped.x,
                    Is.EqualTo(CameraRig.FollowClampX * sign).Within(Epsilon),
                    $"{sign:+0;-0} x corner must saturate at its signed follow clamp");
                Assert.That(clamped.z,
                    Is.EqualTo(CameraRig.FollowClampZ * sign).Within(Epsilon),
                    $"{sign:+0;-0} z corner must saturate at its signed follow clamp");
                Assert.That(clamped.y, Is.EqualTo(0f).Within(Epsilon));
            }
        }

        [Test]
        public void SmoothedVelocityAndLookAhead_AreEquivalentAtThirtySixtyAndOneHundredTwentyHertz()
        {
            const float elapsedSeconds = 0.5f;
            var arenaCenter = ViewWorld.ToWorld(SimConfig.ArenaX, SimConfig.ArenaY);
            var worldVelocity = new Vector3(2f, 0f, -3f);
            var expectedVelocity = worldVelocity
                * (1f - Mathf.Exp(-CameraRig.FollowVelocityLambda * elapsedSeconds));
            var expectedTarget = CameraRig.FollowTarget(arenaCenter, expectedVelocity);

            foreach (var hertz in new[] { 30, 60, 120 })
            {
                var sampleSeconds = 1f / hertz;
                var sampleCount = (int)(hertz * elapsedSeconds);
                var smoothedVelocity = Vector3.zero;
                for (var sample = 0; sample < sampleCount; sample += 1)
                {
                    smoothedVelocity = CameraRig.SmoothFollowVelocity(
                        smoothedVelocity, worldVelocity * sampleSeconds, sampleSeconds);
                }

                Assert.That(Vector3.Distance(smoothedVelocity, expectedVelocity),
                    Is.LessThan(Epsilon),
                    $"{hertz} Hz smoothing must match the equal-time exponential response");

                var target = CameraRig.FollowTarget(arenaCenter, smoothedVelocity);
                Assert.That(Vector3.Distance(target, expectedTarget), Is.LessThan(Epsilon),
                    $"{hertz} Hz must produce the same seconds-ahead target");
            }

            // Runtime schedule: sim positions arrive at 60 Hz while a 120 Hz
            // renderer repeats each position once. Duplicate samples inside the
            // hold window must not pulse the lead toward zero.
            using (var fixture = new Fixture(CameraRig.Profile.Dungeon))
            {
                var position = arenaCenter;
                var expectedSmoothedVelocity = Vector3.zero;
                fixture.Rig.SetFollowAnchor(position);

                for (var simTick = 1; simTick <= 6; simTick += 1)
                {
                    var nextPosition = arenaCenter
                        + worldVelocity * (SimConfig.FixedStep * simTick);
                    SetField(fixture.Rig, "_followSampleAge", SimConfig.FixedStep);
                    fixture.Rig.SetFollowAnchor(nextPosition);
                    expectedSmoothedVelocity = CameraRig.SmoothFollowVelocity(
                        expectedSmoothedVelocity, nextPosition - position,
                        SimConfig.FixedStep);

                    var sampledVelocity = Field<Vector3>(fixture.Rig, "_followVelocity");
                    Assert.That(Vector3.Distance(
                            sampledVelocity, expectedSmoothedVelocity),
                        Is.LessThan(Epsilon),
                        $"60 Hz sim tick {simTick} must update the smoothed velocity once");
                    var sampledTarget = CameraRig.FollowTarget(
                        nextPosition, sampledVelocity);

                    // The intervening 120 Hz render sample repeats the same sim
                    // position and remains well inside the 50 ms hold.
                    SetField(fixture.Rig, "_followSampleAge", 1f / 120f);
                    SetField(fixture.Rig, "_followIdleAge", 1f / 120f);
                    fixture.Rig.SetFollowAnchor(nextPosition);
                    var duplicateVelocity = Field<Vector3>(
                        fixture.Rig, "_followVelocity");
                    var duplicateTarget = CameraRig.FollowTarget(
                        nextPosition, duplicateVelocity);

                    Assert.That(Vector3.Distance(
                            duplicateVelocity, sampledVelocity),
                        Is.LessThan(Epsilon),
                        $"120 Hz duplicate after sim tick {simTick} must hold velocity");
                    Assert.That(Vector3.Distance(duplicateTarget, sampledTarget),
                        Is.LessThan(Epsilon),
                        $"120 Hz duplicate after sim tick {simTick} must not pulse look-ahead");
                    Assert.That(Vector3.Distance(duplicateTarget, nextPosition),
                        Is.LessThanOrEqualTo(CameraRig.FollowLookAheadMax + Epsilon),
                        "held look-ahead must remain inside the radial budget");
                    position = nextPosition;
                }

                var heldVelocity = Field<Vector3>(fixture.Rig, "_followVelocity");
                var heldTarget = CameraRig.FollowTarget(position, heldVelocity);
                var duplicateFramesInsideHold = Mathf.FloorToInt(
                    CameraRig.FollowVelocityHoldSeconds * 120f);
                for (var duplicate = 1;
                    duplicate <= duplicateFramesInsideHold;
                    duplicate += 1)
                {
                    SetField(fixture.Rig, "_followSampleAge", duplicate / 120f);
                    SetField(fixture.Rig, "_followIdleAge", duplicate / 120f);
                    fixture.Rig.SetFollowAnchor(position);
                    Assert.That(Vector3.Distance(
                            Field<Vector3>(fixture.Rig, "_followVelocity"),
                            heldVelocity),
                        Is.LessThan(Epsilon),
                        $"duplicate frame {duplicate} inside the hold window changed velocity");
                    Assert.That(Vector3.Distance(
                            CameraRig.FollowTarget(
                                position, Field<Vector3>(fixture.Rig, "_followVelocity")),
                            heldTarget),
                        Is.LessThan(Epsilon),
                        $"duplicate frame {duplicate} inside the hold window pulsed look-ahead");
                }
            }
        }

        [Test]
        public void FollowAnchor_RejectsNonFinitePositionsWithoutCorruptingTheAuthoredFrame()
        {
            using (var fixture = new Fixture(CameraRig.Profile.Dungeon))
            {
                Invoke(fixture.Rig, "LateUpdate");
                var authoredPosition = fixture.Camera.transform.position;

                fixture.Rig.SetFollowAnchor(new Vector3(float.NaN, 0f, 0f));
                Invoke(fixture.Rig, "LateUpdate");
                Assert.That(Vector3.Distance(fixture.Camera.transform.position, authoredPosition),
                    Is.LessThan(Epsilon), "NaN anchor must be ignored");

                fixture.Rig.SetFollowAnchor(new Vector3(0f, 0f, float.PositiveInfinity));
                Invoke(fixture.Rig, "LateUpdate");
                Assert.That(Vector3.Distance(fixture.Camera.transform.position, authoredPosition),
                    Is.LessThan(Epsilon), "infinite anchor must be ignored");
            }
        }

        [Test]
        public void PublicPresentationRequests_RejectNonFiniteValuesWithoutArmingEffects()
        {
            using (var fixture = new Fixture(CameraRig.Profile.Dungeon))
            {
                fixture.Rig.Punch(float.NaN, 0.2f);
                fixture.Rig.Punch(0.05f, float.PositiveInfinity);
                Assert.That(Field<float>(fixture.Rig, "_shakeTime"), Is.EqualTo(0f),
                    "non-finite Punch requests must not arm shake");

                fixture.Rig.FocusPulse(
                    new Vector3(float.NegativeInfinity, 0f, 0f), 0.3f);
                fixture.Rig.FocusPulse(Vector3.one, float.NaN);
                Assert.That(Field<float>(fixture.Rig, "_focusTimer"), Is.EqualTo(0f),
                    "non-finite FocusPulse requests must not arm focus");

                fixture.Rig.Flourish(float.NaN, 0.5f, 0.2f);
                fixture.Rig.Flourish(1f, float.PositiveInfinity, 0.2f);
                fixture.Rig.Flourish(1f, 0.5f, float.NaN);
                Assert.That(Field<float>(fixture.Rig, "_flourishTime"), Is.EqualTo(0f),
                    "non-finite Flourish requests must not arm a flourish");
            }
        }

        [Test]
        public void ThreatFocus_IsGroundPlanar_AndNeverExceedsItsRadialBudget()
        {
            var player = ViewWorld.ToWorld(SimConfig.ArenaX, SimConfig.ArenaY);
            var threat = player + new Vector3(100f, 75f, -100f);
            var focused = CameraRig.ThreatFocus(player, threat);
            var offset = focused - player;

            Assert.That(offset.magnitude,
                Is.EqualTo(CameraRig.MaxThreatFocusOffset).Within(Epsilon),
                "a distant diagonal threat must saturate at the radial focus budget");
            Assert.That(focused.y, Is.EqualTo(player.y).Within(Epsilon),
                "an elevated threat must not pull the camera focus off the ground plane");
        }

        [Test]
        public void FocusPulse_ClampsAnAbsurdDurationToTheDeclaredMaximum()
        {
            using (var fixture = new Fixture(CameraRig.Profile.Dungeon))
            {
                fixture.Rig.FocusPulse(Vector3.one * 100f, 1000f);

                Assert.That(Field<float>(fixture.Rig, "_focusDuration"),
                    Is.EqualTo(CameraRig.MaxFocusDuration).Within(Epsilon));
                Assert.That(Field<float>(fixture.Rig, "_focusTimer"),
                    Is.EqualTo(CameraRig.MaxFocusDuration).Within(Epsilon));
            }
        }

        [Test]
        public void Punch_ClampsAbsurdRequests_AndAWeakerRequestCannotStompTheLiveShake()
        {
            using (var fixture = new Fixture(CameraRig.Profile.Dungeon))
            {
                fixture.Rig.Punch(1000f, 1000f);

                Assert.That(Field<float>(fixture.Rig, "_shakeAmplitude"),
                    Is.EqualTo(CameraRig.MaxShakeAmplitude).Within(Epsilon),
                    "shake amplitude must saturate at the authored maximum");
                Assert.That(Field<float>(fixture.Rig, "_shakeDuration"),
                    Is.EqualTo(CameraRig.MaxShakeDuration).Within(Epsilon),
                    "shake duration must saturate at the authored maximum");

                var strongAmplitude = Field<float>(fixture.Rig, "_shakeAmplitude");
                var strongDuration = Field<float>(fixture.Rig, "_shakeDuration");
                var strongTime = Field<float>(fixture.Rig, "_shakeTime");
                fixture.Rig.Punch(strongAmplitude * 0.1f, strongDuration * 0.1f);

                Assert.That(Field<float>(fixture.Rig, "_shakeAmplitude"),
                    Is.EqualTo(strongAmplitude).Within(Epsilon),
                    "a weaker hit must not replace the amplitude of a stronger live hit");
                Assert.That(Field<float>(fixture.Rig, "_shakeDuration"),
                    Is.EqualTo(strongDuration).Within(Epsilon),
                    "a weaker hit must not shorten a stronger live hit");
                Assert.That(Field<float>(fixture.Rig, "_shakeTime"),
                    Is.EqualTo(strongTime).Within(Epsilon),
                    "a refused weaker hit must not restart or truncate the live envelope");
            }
        }

        [Test]
        public void Punch_ShorterThanAFrameStillAppliesOneBoundedSampleBeforeClearing()
        {
            using (var fixture = new Fixture(CameraRig.Profile.Dungeon))
            {
                Invoke(fixture.Rig, "LateUpdate");
                var authoredPosition = fixture.Camera.transform.position;
                var subFrameDuration = Mathf.Max(1e-6f, Time.deltaTime * 0.01f);

                fixture.Rig.Punch(CameraRig.MaxShakeAmplitude, subFrameDuration);
                Invoke(fixture.Rig, "LateUpdate");

                var observedOffset = Vector3.Distance(
                    fixture.Camera.transform.position, authoredPosition);
                Assert.That(observedOffset, Is.GreaterThan(1e-6f),
                    "a sub-frame impact must still earn one visible shake sample");
                Assert.That(observedOffset,
                    Is.LessThanOrEqualTo(CameraRig.MaxShakeAmplitude + Epsilon),
                    "the one impact sample must remain inside the radial shake budget");
                Assert.That(Field<float>(fixture.Rig, "_shakeTime"), Is.EqualTo(0f),
                    "the sub-frame request must clear after its one sample");

                Invoke(fixture.Rig, "LateUpdate");
                Assert.That(Vector3.Distance(fixture.Camera.transform.position, authoredPosition),
                    Is.LessThan(Epsilon),
                    "the frame after the one-shot sample must restore authored placement");
            }
        }

        [Test]
        public void ShakeEnvelope_IsBounded_AndRetainsMoreStrengthThanLinearDecay()
        {
            var samples = new[] { -2f, 0f, 0.1f, 0.25f, 0.5f, 0.75f, 1f, 2f };
            foreach (var remaining in samples)
            {
                var envelope = CameraRig.ShakeEnvelope(remaining);
                Assert.That(envelope, Is.InRange(0f, 1f),
                    $"envelope left [0,1] for remaining={remaining}");
                if (remaining > 0f && remaining < 1f)
                {
                    Assert.That(envelope, Is.GreaterThan(remaining),
                        $"the authored shake tail must retain more energy than linear decay at {remaining}");
                }
            }

            Assert.That(CameraRig.ShakeEnvelope(-2f), Is.EqualTo(0f).Within(Epsilon));
            Assert.That(CameraRig.ShakeEnvelope(2f), Is.EqualTo(1f).Within(Epsilon));
        }

        [Test]
        public void ProfileSwitch_ClearsEveryLiveShakeChannel()
        {
            using (var fixture = new Fixture(CameraRig.Profile.Dungeon))
            {
                fixture.Rig.Punch(CameraRig.MaxShakeAmplitude, CameraRig.MaxShakeDuration);
                Assert.That(Field<float>(fixture.Rig, "_shakeTime"), Is.GreaterThan(0f));

                fixture.Rig.SetProfile(CameraRig.Profile.Lobby);

                Assert.That(Field<float>(fixture.Rig, "_shakeTime"), Is.EqualTo(0f));
                Assert.That(Field<float>(fixture.Rig, "_shakeDuration"), Is.EqualTo(0f));
                Assert.That(Field<float>(fixture.Rig, "_shakeAmplitude"), Is.EqualTo(0f));
            }
        }

        [Test]
        public void ReducedMotionToggle_CancelsLivePresentation_AndRestoresAuthoredFramingOnLateUpdate()
        {
            using (var fixture = new Fixture(CameraRig.Profile.Dungeon))
            {
                Invoke(fixture.Rig, "LateUpdate");
                var authoredPosition = fixture.Camera.transform.position;
                var authoredRotation = fixture.Camera.transform.rotation;
                var authoredFov = fixture.Camera.fieldOfView;
                var arenaCenter = ViewWorld.ToWorld(SimConfig.ArenaX, SimConfig.ArenaY);

                fixture.Rig.Punch(CameraRig.MaxShakeAmplitude, CameraRig.MaxShakeDuration);
                fixture.Rig.Flourish(400f, 400f, 30f);
                fixture.Rig.FocusPulse(arenaCenter + Vector3.right * 100f, 30f);
                PlantFlourishPeak(fixture.Rig);
                PlantFocusPeak(fixture.Rig);

                Assert.That(Field<float>(fixture.Rig, "_shakeTime"), Is.GreaterThan(0f));
                Assert.That(Field<float>(fixture.Rig, "_flourishTime"), Is.GreaterThan(0f));
                Assert.That(Field<float>(fixture.Rig, "_focusTimer"), Is.GreaterThan(0f));

                Invoke(fixture.Rig, "LateUpdate");
                Assert.That(Mathf.Abs(fixture.Camera.fieldOfView - authoredFov),
                    Is.GreaterThan(0.5f), "the planted flourish must be visibly live before cancellation");
                Assert.That(Mathf.Abs(fixture.Camera.transform.position.x - authoredPosition.x),
                    Is.GreaterThan(0.5f), "the planted focus pull must be visibly live before cancellation");

                ViewPrefs.ReducedMotion = true;
                Invoke(fixture.Rig, "LateUpdate");

                Assert.That(Field<float>(fixture.Rig, "_shakeTime"), Is.EqualTo(0f));
                Assert.That(Field<float>(fixture.Rig, "_shakeDuration"), Is.EqualTo(0f));
                Assert.That(Field<float>(fixture.Rig, "_shakeAmplitude"), Is.EqualTo(0f));
                Assert.That(Field<float>(fixture.Rig, "_flourishTime"), Is.EqualTo(0f));
                Assert.That(Field<float>(fixture.Rig, "_focusTimer"), Is.EqualTo(0f));
                Assert.That(Vector3.Distance(fixture.Camera.transform.position, authoredPosition),
                    Is.LessThan(1e-3f), "LateUpdate must restore the profile-authored position");
                Assert.That(Quaternion.Angle(fixture.Camera.transform.rotation, authoredRotation),
                    Is.LessThan(0.01f), "LateUpdate must restore the profile-authored rotation");
                Assert.That(fixture.Camera.fieldOfView,
                    Is.EqualTo(authoredFov).Within(1e-3f),
                    "LateUpdate must restore the profile-authored field of view");
            }
        }

        static T Field<T>(CameraRig rig, string name)
            => (T)typeof(CameraRig).GetField(name, InstanceFlags).GetValue(rig);

        static void SetField<T>(CameraRig rig, string name, T value)
            => typeof(CameraRig).GetField(name, InstanceFlags).SetValue(rig, value);

        static float Constant(string name)
            => (float)typeof(CameraRig).GetField(name, StaticFlags).GetRawConstantValue();

        static void Invoke(object target, string method)
            => target.GetType().GetMethod(method, InstanceFlags).Invoke(target, null);

        static void PlantFlourishPeak(CameraRig rig)
        {
            var duration = Field<float>(rig, "_flourishDuration");
            SetField(rig, "_flourishTime",
                duration * (1f - Constant("FlourishAttack")) + Time.deltaTime);
        }

        static void PlantFocusPeak(CameraRig rig)
        {
            var duration = Field<float>(rig, "_focusDuration");
            SetField(rig, "_focusTimer", duration * 0.5f + Time.deltaTime);
        }

        sealed class Fixture : IDisposable
        {
            public readonly CameraRig Rig;
            public readonly Camera Camera;
            readonly GameObject _root;
            readonly GameObject _cameraGo;

            public Fixture(CameraRig.Profile profile)
            {
                _root = new GameObject("CameraRigPresentationTestRoot");
                _cameraGo = new GameObject("CameraRigPresentationTestCamera") { tag = "MainCamera" };
                Camera = _cameraGo.AddComponent<Camera>();
                Rig = _root.AddComponent<CameraRig>();
                typeof(CameraRig).GetField("_camera", InstanceFlags).SetValue(Rig, Camera);
                Rig.SetProfile(profile);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_cameraGo);
                UnityEngine.Object.DestroyImmediate(_root);
            }
        }
    }
}
