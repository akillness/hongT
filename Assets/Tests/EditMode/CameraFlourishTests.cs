// W9 camera flourish (FOV punch + view roll) — the BOUNDS contract.
//
// WHY THIS FIXTURE EXISTS. The flourish re-frames the camera, and the camera
// frame is where this game draws every telegraph: hazard rings, wave warnings,
// the AOE crowns that are deliberately fitted to the sim's own damage ellipse
// (see SkillShapeVocabularyTests). A flourish that can grow without limit, or
// that survives a profile switch, or that ignores reduced motion, is therefore
// not a taste problem — it is a camera that lies about the hit box.
//
// So these tests pin exactly three things and nothing about taste:
//   1. every channel is clamped to the declared maxima, whatever is requested,
//   2. reduced motion refuses the effect outright,
//   3. the effect returns the camera to the profile's authored framing.
//
// EditMode only: construct → drive → DestroyImmediate. Time.deltaTime is not
// controllable here, so the envelope is driven by planting the private timer
// (and compensating for the one decrement ApplyFlourish performs) rather than
// by waiting — which also makes every assertion frame-rate independent.
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CinderCourt.Sim;
using CinderCourt.View;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class CameraFlourishTests
    {
        const BindingFlags InstanceFlags = BindingFlags.NonPublic | BindingFlags.Instance;
        const BindingFlags StaticFlags = BindingFlags.NonPublic | BindingFlags.Static;

        /// <summary>Dungeon profile FOV — the baseline the punch is a delta on.</summary>
        const float DungeonFov = 42f;

        bool _hadReducedMotionPref;
        int _reducedMotionPrefValue;
        float _bakedFogStart, _bakedFogEnd;

        [SetUp]
        public void SetUp()
        {
            // The Dungeon profile drives RenderSettings fog every LateUpdate and
            // RenderSettings is GLOBAL — the same hazard CameraRig.SetProfile
            // exists to undo. Driving the dungeon branch here would otherwise
            // leave a boss-wave fog band behind for whichever fixture runs next.
            _bakedFogStart = RenderSettings.fogStartDistance;
            _bakedFogEnd = RenderSettings.fogEndDistance;

            // Flourish gates on ViewPrefs.ReducedMotion. Snapshot the pref (and
            // the live value, which is cached in a static) so the suite never
            // pollutes the developer's editor — same discipline as
            // SkillShapeVocabularyTests.
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

        // ------------------------------------------------------- helpers -----

        static float Constant(string name) => (float)typeof(CameraRig)
            .GetField(name, StaticFlags).GetRawConstantValue();

        static float MaxFov => Constant("MaxFlourishFov");
        static float MaxRoll => Constant("MaxFlourishRoll");
        static float MaxDuration => Constant("MaxFlourishDuration");
        static float Attack => Constant("FlourishAttack");

        static float Field(CameraRig rig, string name)
            => (float)typeof(CameraRig).GetField(name, InstanceFlags).GetValue(rig);

        static void SetField(CameraRig rig, string name, float value)
            => typeof(CameraRig).GetField(name, InstanceFlags).SetValue(rig, value);

        static Quaternion PlacedRotation(CameraRig rig)
            => (Quaternion)typeof(CameraRig)
                .GetField("_placedRotation", InstanceFlags).GetValue(rig);

        static void Invoke(object target, string method)
            => target.GetType()
                .GetMethod(method, InstanceFlags).Invoke(target, null);

        /// <summary>
        /// A rig with its camera bound directly. Camera.main is unreliable in
        /// batchmode EditMode (it returns null and leaves the rig inert), which
        /// DungeonFramingAndMoodTests already documents — so the private field
        /// is set instead of relying on Awake.
        /// </summary>
        sealed class Fixture : System.IDisposable
        {
            public readonly CameraRig Rig;
            public readonly Camera Camera;
            readonly GameObject _root, _cameraGo;

            public Fixture(CameraRig.Profile profile)
            {
                _root = new GameObject("FlourishRig");
                _cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
                Camera = _cameraGo.AddComponent<Camera>();
                Rig = _root.AddComponent<CameraRig>();
                typeof(CameraRig).GetField("_camera", InstanceFlags).SetValue(Rig, Camera);
                Rig.SetProfile(profile);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(_cameraGo);
                Object.DestroyImmediate(_root);
            }
        }

        /// <summary>
        /// Plants the timer so that AFTER ApplyFlourish's single decrement the
        /// envelope sits at <paramref name="envelopePhase"/> of the way through
        /// the flourish. Compensating for Time.deltaTime here is what keeps the
        /// peak assertion exact on any editor frame rate.
        /// </summary>
        static void PlantPhase(CameraRig rig, float duration, float envelopePhase)
        {
            SetField(rig, "_flourishDuration", duration);
            SetField(rig, "_flourishTime", duration * (1f - envelopePhase) + Time.deltaTime);
            typeof(CameraRig).GetField("_flourishActive", InstanceFlags)
                .SetValue(rig, true);
        }

        // --------------------------------------------------------- tests -----

        [Test]
        public void Flourish_ClampsEveryChannelWhateverIsRequested()
        {
            using (var fixture = new Fixture(CameraRig.Profile.Dungeon))
            {
                // Deliberately absurd: a caller must not be able to author a
                // 90° roll by passing 90.
                fixture.Rig.Flourish(400f, -400f, 30f);
                Assert.That(Mathf.Abs(Field(fixture.Rig, "_flourishFov")),
                    Is.LessThanOrEqualTo(MaxFov + 1e-4f),
                    "FOV punch must clamp to MaxFlourishFov");
                Assert.That(Mathf.Abs(Field(fixture.Rig, "_flourishRoll")),
                    Is.LessThanOrEqualTo(MaxRoll + 1e-4f),
                    "roll must clamp to MaxFlourishRoll");
                Assert.That(Field(fixture.Rig, "_flourishDuration"),
                    Is.LessThanOrEqualTo(MaxDuration + 1e-4f),
                    "duration must clamp to MaxFlourishDuration");
                // Sign is meaning, not decoration: negative FOV closes the frame
                // in, positive opens it out. A clamp that flipped it would swap
                // "finisher" and "boss arrives".
                Assert.That(Field(fixture.Rig, "_flourishFov"), Is.GreaterThan(0f));
                Assert.That(Field(fixture.Rig, "_flourishRoll"), Is.LessThan(0f));
            }
        }

        [Test]
        public void Flourish_IsRefusedEntirelyUnderReducedMotion()
        {
            ViewPrefs.ReducedMotion = true;
            using (var fixture = new Fixture(CameraRig.Profile.Dungeon))
            {
                fixture.Rig.Flourish(-2.2f, 0.9f, 0.18f);
                Assert.That(Field(fixture.Rig, "_flourishTime"), Is.EqualTo(0f),
                    "reduced motion must arm no flourish at all");

                // And the event path, which is how it actually fires in play.
                fixture.Rig.OnEvents(SimEvents.BossPhase2);
                Assert.That(Field(fixture.Rig, "_flourishTime"), Is.EqualTo(0f),
                    "the event chain must respect the same gate as the API");

                Invoke(fixture.Rig, "LateUpdate");
                Assert.That(fixture.Camera.fieldOfView,
                    Is.EqualTo(DungeonFov).Within(1e-3f),
                    "a refused flourish must leave the profile FOV untouched");
            }
        }

        [Test]
        public void Flourish_WeakerRequestCannotCutAStrongerOneShort()
        {
            using (var fixture = new Fixture(CameraRig.Profile.Dungeon))
            {
                fixture.Rig.Flourish(3.5f, -1.4f, 0.30f);
                var strongFov = Field(fixture.Rig, "_flourishFov");
                var strongRoll = Field(fixture.Rig, "_flourishRoll");

                fixture.Rig.Flourish(0.2f, 0.1f, 0.05f);
                Assert.That(Field(fixture.Rig, "_flourishFov"),
                    Is.EqualTo(strongFov).Within(1e-4f),
                    "a weak request must not replace a live stronger flourish");
                Assert.That(Field(fixture.Rig, "_flourishRoll"),
                    Is.EqualTo(strongRoll).Within(1e-4f));
            }
        }

        [Test]
        public void Flourish_StaysInsideTheBudgetAcrossTheWholeEnvelope()
        {
            using (var fixture = new Fixture(CameraRig.Profile.Dungeon))
            {
                const float Duration = 0.30f;
                fixture.Rig.Flourish(MaxFov * 2f, MaxRoll * 2f, Duration);  // clamped to max
                for (var phase = 0f; phase <= 1.0001f; phase += 0.05f)
                {
                    PlantPhase(fixture.Rig, Duration, phase);
                    Invoke(fixture.Rig, "LateUpdate");

                    Assert.That(Mathf.Abs(fixture.Camera.fieldOfView - DungeonFov),
                        Is.LessThanOrEqualTo(MaxFov + 1e-3f),
                        $"FOV left the budget at envelope phase {phase}");
                    // The angle between the authored placement and the live
                    // rotation IS the roll magnitude — no Euler round-tripping.
                    Assert.That(
                        Quaternion.Angle(PlacedRotation(fixture.Rig),
                            fixture.Camera.transform.rotation),
                        Is.LessThanOrEqualTo(MaxRoll + 1e-3f),
                        $"roll left the budget at envelope phase {phase}");
                }
            }
        }

        [Test]
        public void Flourish_ReachesFullStrengthAtThePeakAndReturnsToZero()
        {
            using (var fixture = new Fixture(CameraRig.Profile.Dungeon))
            {
                const float Duration = 0.30f;
                fixture.Rig.Flourish(-MaxFov, MaxRoll, Duration);

                // Peak sits at the end of the attack ramp, by construction.
                PlantPhase(fixture.Rig, Duration, Attack);
                Invoke(fixture.Rig, "LateUpdate");
                Assert.That(fixture.Camera.fieldOfView,
                    Is.EqualTo(DungeonFov - MaxFov).Within(1e-2f),
                    "the envelope must actually reach full strength — a flourish "
                    + "that only ever passes the bound checks does nothing");
                Assert.That(
                    Quaternion.Angle(PlacedRotation(fixture.Rig),
                        fixture.Camera.transform.rotation),
                    Is.EqualTo(MaxRoll).Within(1e-2f));

                // …and the tail returns the frame exactly, not approximately.
                PlantPhase(fixture.Rig, Duration, 1f);
                Invoke(fixture.Rig, "LateUpdate");
                Assert.That(fixture.Camera.fieldOfView,
                    Is.EqualTo(DungeonFov).Within(1e-3f),
                    "the flourish must land back on the profile FOV");
                Assert.That(
                    Quaternion.Angle(PlacedRotation(fixture.Rig),
                        fixture.Camera.transform.rotation),
                    Is.LessThan(1e-2f),
                    "the flourish must land back on the authored rotation");
            }
        }

        [Test]
        public void Flourish_DoesNotSurviveAProfileSwitch()
        {
            using (var fixture = new Fixture(CameraRig.Profile.Dungeon))
            {
                fixture.Rig.Flourish(MaxFov, MaxRoll, MaxDuration);
                Assert.That(Field(fixture.Rig, "_flourishTime"), Is.GreaterThan(0f));

                // A live punch across a switch would keep adding its delta to
                // the NEW profile's baseline — the lobby would boot zoomed.
                fixture.Rig.SetProfile(CameraRig.Profile.Lobby);
                Assert.That(Field(fixture.Rig, "_flourishTime"), Is.EqualTo(0f),
                    "a profile switch must drop the live flourish");
                Invoke(fixture.Rig, "LateUpdate");
                Assert.That(fixture.Camera.fieldOfView, Is.EqualTo(36f).Within(1e-3f),
                    "the lobby must open at its own authored FOV");
            }
        }

        [Test]
        public void Flourish_EveryEventTheChainHandlesArmsABoundedPunch()
        {
            // The events the W9 chain claims to serve. Each must arm something
            // (a silent tier is a tier that was never wired) and nothing may
            // exceed the budget — the two failure modes worth a regression.
            var beats = new[]
            {
                SimEvents.BossPhase2, SimEvents.BossSpawned,
                SimEvents.ComboFinisher, SimEvents.NovaCast, SimEvents.LevelUp,
            };
            foreach (var beat in beats)
            {
                using (var fixture = new Fixture(CameraRig.Profile.Dungeon))
                {
                    fixture.Rig.OnEvents(beat);
                    var fov = Field(fixture.Rig, "_flourishFov");
                    var roll = Field(fixture.Rig, "_flourishRoll");
                    Assert.That(Field(fixture.Rig, "_flourishTime"), Is.GreaterThan(0f),
                        $"{beat} must arm a flourish");
                    Assert.That(Field(fixture.Rig, "_flourishDuration"),
                        Is.LessThanOrEqualTo(MaxDuration + 1e-4f), $"{beat} duration");
                    Assert.That(Mathf.Abs(fov), Is.LessThanOrEqualTo(MaxFov + 1e-4f),
                        $"{beat} FOV punch");
                    Assert.That(Mathf.Abs(roll), Is.LessThanOrEqualTo(MaxRoll + 1e-4f),
                        $"{beat} roll");
                    Assert.That(Mathf.Abs(fov) + Mathf.Abs(roll), Is.GreaterThan(0f),
                        $"{beat} armed a flourish with no channel set");
                }
            }
        }

        [Test]
        public void Flourish_ScalesDownWhileAShakeIsCarryingTheFrame()
        {
            using (var fixture = new Fixture(CameraRig.Profile.Dungeon))
            {
                const float Duration = 0.30f;
                fixture.Rig.Flourish(-MaxFov, 0f, Duration);
                PlantPhase(fixture.Rig, Duration, Attack);
                Invoke(fixture.Rig, "LateUpdate");
                var soloDelta = Mathf.Abs(fixture.Camera.fieldOfView - DungeonFov);

                // Same peak, but with a full-strength shake live. The two motion
                // channels must not simply sum: the composition clamp is the
                // only thing standing between "punchy" and "nauseating".
                SetField(fixture.Rig, "_shakeDuration", 0.3f);
                SetField(fixture.Rig, "_shakeTime", 0.3f);
                SetField(fixture.Rig, "_shakeAmplitude", Constant("ShakeLoadReference"));
                PlantPhase(fixture.Rig, Duration, Attack);
                Invoke(fixture.Rig, "LateUpdate");
                var composedDelta = Mathf.Abs(fixture.Camera.fieldOfView - DungeonFov);

                Assert.That(composedDelta, Is.LessThan(soloDelta),
                    "a live shake must scale the flourish down, not stack with it");
                Assert.That(composedDelta, Is.GreaterThan(0f),
                    "the clamp must attenuate the flourish, never cancel it");
            }
        }
    }
}
