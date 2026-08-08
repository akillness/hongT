// 2026-10 dungeon presentation request, four separable contracts:
//   1. per-stage generated gimmick textures  → StageTextures_*
//   2. dungeon camera follows the player     → Follow_*
//   3. dungeon reads bigger                  → WorldScale_*
//   4. actors shrink to 0.8, mood lighting   → ActorScale_*, Mood_*, Flicker_*
//
// EditMode only: construct → inspect → DestroyImmediate. No play mode, no
// rendering. RenderSettings is global, so the mood fixture snapshots and
// restores it (the same hazard StageMood.Clear exists for at runtime).
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using CinderCourt.Sim;
using CinderCourt.View;

namespace CinderCourt.Tests
{
    [TestFixture]
    public sealed class DungeonFramingAndMoodTests
    {
        static readonly string[] StageIds =
        {
            "cinder-span", "ember-gallery", "abyss-chancel",
            "witness-well", "echo-throne", "ash-verdict",
            "cinder-sluice", "ember-bastion", "ash-march",
        };

        static Vector3 ArenaCenter => ViewWorld.ToWorld(SimConfig.ArenaX, SimConfig.ArenaY);

        // ------------------------------------------------- 3. world scale ----

        [Test]
        public void WorldScale_GrewByTheLegacyRatio_AndArenaFootprintGrewWithIt()
        {
            Assert.That(ViewWorld.LegacyScaleRatio,
                Is.EqualTo(ViewWorld.Scale / ViewWorld.LegacyScale).Within(1e-6f),
                "LegacyScaleRatio must stay the quotient of the two scales");
            Assert.That(ViewWorld.Scale, Is.GreaterThan(ViewWorld.LegacyScale),
                "the dungeon-enlargement request is exactly Scale > LegacyScale");

            // The sim contract is frozen (CLAUDE.md §2), so the arena's SIM
            // extent must be untouched while its WORLD extent grows.
            var widthWorld = SimConfig.ArenaHalfWidth * 2f * ViewWorld.Scale;
            var legacyWidthWorld = SimConfig.ArenaHalfWidth * 2f * ViewWorld.LegacyScale;
            Assert.That(SimConfig.ArenaHalfWidth, Is.EqualTo(520f),
                "sim arena half-width is a frozen contract number");
            Assert.That(widthWorld,
                Is.EqualTo(legacyWidthWorld * ViewWorld.LegacyScaleRatio).Within(1e-4f));
        }

        [Test]
        public void WorldScale_EnvironmentLayoutUsesTheSameQuotientAsViewWorld()
        {
            // EnvironmentLayout is a pure core that cannot reference ViewWorld,
            // so it carries a private copy. A drift between the two would put
            // the walls somewhere the actors never reach.
            var field = typeof(EnvironmentLayout)
                .GetField("SimToWorld", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, "EnvironmentLayout.SimToWorld must exist");
            Assert.That((double)field.GetRawConstantValue(),
                Is.EqualTo((double)ViewWorld.Scale).Within(1e-9),
                "EnvironmentLayout.SimToWorld must mirror ViewWorld.Scale");
        }

        // ------------------------------------------------ 2. camera follow ---

        [Test]
        public void Follow_ClampPassesThroughPointsInsideTheBand()
        {
            var inside = ArenaCenter + new Vector3(
                CameraRig.FollowClampX * 0.5f, 0f, -CameraRig.FollowClampZ * 0.5f);
            var clamped = CameraRig.ClampFollow(inside);
            Assert.That(clamped.x, Is.EqualTo(inside.x).Within(1e-5f));
            Assert.That(clamped.z, Is.EqualTo(inside.z).Within(1e-5f));
        }

        [Test]
        public void Follow_ClampHoldsTheCameraInsideTheArenaOnBothAxes()
        {
            // Player stop line: the furthest a warden can physically get.
            var reachX = SimConfig.ArenaHalfWidth * EnvironmentBuilder.PlayerStopE * ViewWorld.Scale;
            var reachZ = SimConfig.ArenaHalfHeight * EnvironmentBuilder.PlayerStopE * ViewWorld.Scale;
            foreach (var sx in new[] { -1f, 1f })
            {
                foreach (var sz in new[] { -1f, 1f })
                {
                    var corner = ArenaCenter + new Vector3(reachX * sx, 0f, reachZ * sz);
                    var clamped = CameraRig.ClampFollow(corner) - ArenaCenter;
                    Assert.That(Mathf.Abs(clamped.x),
                        Is.LessThanOrEqualTo(CameraRig.FollowClampX + 1e-5f),
                        "follow must never track past the x clamp");
                    Assert.That(Mathf.Abs(clamped.z),
                        Is.LessThanOrEqualTo(CameraRig.FollowClampZ + 1e-5f),
                        "follow must never track past the z clamp");
                    Assert.That(Mathf.Abs(clamped.y), Is.LessThan(1e-5f),
                        "follow is a ground-plane pan; height stays on the orbit");
                }
            }
            Assert.That(CameraRig.FollowClampX, Is.GreaterThan(0f),
                "a zero clamp would silently restore the fixed centre camera");
            Assert.That(CameraRig.FollowClampX, Is.LessThan(reachX),
                "the clamp must bite before the player's own stop line");
        }

        [Test]
        public void Follow_DungeonCameraTracksTheAnchor_AndOnlyInDungeon()
        {
            var root = new GameObject("FollowRig");
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.AddComponent<Camera>();
            try
            {
                var rig = root.AddComponent<CameraRig>();
                // Camera.main is unreliable in batchmode EditMode (it returned
                // null here, leaving the rig inert), so bind the private field
                // directly instead of going through Awake.
                typeof(CameraRig)
                    .GetField("_camera", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(rig, cameraGo.GetComponent<Camera>());

                var anchor = ArenaCenter + new Vector3(CameraRig.FollowClampX * 0.5f, 0f, 0f);

                // Non-dungeon profile must ignore the anchor entirely.
                rig.SetProfile(CameraRig.Profile.Lobby);
                rig.SetFollowAnchor(anchor);
                Invoke(rig, "LateUpdate");
                var lobbyX = cameraGo.transform.position.x;

                rig.SetProfile(CameraRig.Profile.Dungeon);
                rig.SetFollowAnchor(anchor);
                Invoke(rig, "LateUpdate");
                var followedX = cameraGo.transform.position.x;

                Assert.That(followedX, Is.EqualTo(anchor.x).Within(1e-3f),
                    "dungeon orbit is pitch-only, so the focus x IS the camera x");
                Assert.That(followedX, Is.Not.EqualTo(lobbyX).Within(1e-3f),
                    "the lobby orbit must not have consumed the follow anchor");

                // A profile switch must drop the anchor: otherwise the next run
                // snaps to wherever the last one ended.
                rig.SetProfile(CameraRig.Profile.Lobby);
                rig.SetProfile(CameraRig.Profile.Dungeon);
                Invoke(rig, "LateUpdate");
                Assert.That(cameraGo.transform.position.x,
                    Is.EqualTo(ArenaCenter.x).Within(1e-3f),
                    "without an anchor the dungeon camera returns to the centre");
            }
            finally
            {
                Object.DestroyImmediate(cameraGo);
                Object.DestroyImmediate(root);
            }
        }

        static void Invoke(object target, string method)
            => target.GetType()
                .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(target, null);

        // -------------------------------------------------- 4. actor scale ---

        [Test]
        public void ActorScale_GlobalShrinkIsAppliedOnTopOfTheAuthoredBaseScale()
        {
            var view = ActorView.Create(null, Color.white, 1f);
            try
            {
                var field = typeof(ActorView).GetField(
                    "_baseScale", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That((float)field.GetValue(view),
                    Is.EqualTo(ActorView.GlobalScale).Within(1e-5f));

                var boss = ActorView.Create(null, Color.white, 1.6f);
                try
                {
                    Assert.That((float)field.GetValue(boss),
                        Is.EqualTo(1.6f * ActorView.GlobalScale).Within(1e-5f),
                        "the shrink must be proportional, not a replacement");
                    // Relative silhouettes are the gameplay contract: a boss must
                    // still read 1.6x its minions after the shrink.
                    Assert.That((float)field.GetValue(boss) / (float)field.GetValue(view),
                        Is.EqualTo(1.6f).Within(1e-5f));
                }
                finally { Object.DestroyImmediate(boss.gameObject); }

                Assert.That(ActorView.GlobalScale, Is.EqualTo(0.8f).Within(1e-6f),
                    "requested actor shrink is 0.8x");
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }

        // ------------------------------------------------- 4. mood lighting --

        Color _bakedAmbient;
        Color _bakedFog;
        UnityEngine.Rendering.AmbientMode _bakedMode;

        [SetUp]
        public void CaptureRenderSettings()
        {
            _bakedAmbient = RenderSettings.ambientLight;
            _bakedFog = RenderSettings.fogColor;
            _bakedMode = RenderSettings.ambientMode;
        }

        [TearDown]
        public void RestoreRenderSettings()
        {
            RenderSettings.ambientLight = _bakedAmbient;
            RenderSettings.fogColor = _bakedFog;
            RenderSettings.ambientMode = _bakedMode;
        }

        [Test]
        public void Mood_UnknownStageBuildsNothingAndLeavesRenderSettingsAlone()
        {
            var before = RenderSettings.ambientLight;
            Assert.That(StageMood.Apply("no-such-stage"), Is.Null);
            Assert.That(StageMood.Apply(null), Is.Null);
            Assert.That(StageMood.Apply(string.Empty), Is.Null);
            Assert.That(RenderSettings.ambientLight, Is.EqualTo(before));
        }

        [Test]
        public void Mood_EveryStageGetsShadowlessDirectionalKeyAndFill()
        {
            foreach (var stageId in StageIds)
            {
                GameObject root = null;
                try
                {
                    root = StageMood.Apply(stageId);
                    Assert.That(root, Is.Not.Null, $"{stageId}: mood rig missing");
                    Assert.That(root.name, Is.EqualTo(StageMood.RootName));
                    var lights = root.GetComponentsInChildren<Light>(true);
                    Assert.That(lights.Length, Is.EqualTo(2),
                        $"{stageId}: mood is key + fill only — extra realtime " +
                        "lights belong to the §E6 point budget");
                    foreach (var light in lights)
                    {
                        Assert.That(light.type, Is.EqualTo(LightType.Directional),
                            $"{stageId}: mood lights must not eat per-object " +
                            "point-light slots in WebGL forward");
                        Assert.That(light.shadows, Is.EqualTo(LightShadows.None),
                            $"{stageId}: §E6 allows zero shadow casters");
                        Assert.That(light.intensity, Is.GreaterThan(0f));
                    }
                    Assert.That(lights[0].intensity, Is.GreaterThan(lights[1].intensity),
                        $"{stageId}: the fill must stay below the key or the " +
                        "scene flattens");
                    Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty,
                        $"{stageId}: decoration never owns physics");
                }
                finally { if (root != null) Object.DestroyImmediate(root); }
            }
        }

        [Test]
        public void Mood_AmbientAndFogAreStageTintedButNeverBlackAndAreRestorable()
        {
            StageCatalog.TryGet("abyss-chancel", out var entry);
            var accent = entry.AccentColor;

            var ambient = StageMood.AmbientColor(accent);
            var fog = StageMood.FogColor(accent);
            Assert.That(ambient.r + ambient.g + ambient.b, Is.GreaterThan(0f),
                "a pure-black ambient makes the unlit-shadow side unreadable");
            Assert.That(ambient.maxColorComponent, Is.LessThan(accent.maxColorComponent),
                "ambient is a dark wash, not the accent at full strength");
            Assert.That(fog.maxColorComponent, Is.LessThan(ambient.maxColorComponent),
                "fog must sit under ambient so depth still reads as falloff");

            // Key light leans cold: closer to the cool white than to the accent.
            var key = StageMood.KeyColor(accent);
            var cold = new Color(0.78f, 0.84f, 1f);
            Assert.That(Distance(key, cold), Is.LessThan(Distance(key, accent)));

            var marker = new Color(0.12f, 0.34f, 0.56f, 1f);
            RenderSettings.ambientLight = marker;
            RenderSettings.fogColor = marker;
            var root = StageMood.Apply("abyss-chancel");
            try
            {
                Assert.That(RenderSettings.ambientLight, Is.Not.EqualTo(marker),
                    "Apply must actually drive the global ambient");
                Assert.That(RenderSettings.ambientLight.b,
                    Is.EqualTo(ambient.b).Within(1e-4f));
                Assert.That(RenderSettings.fogColor.b, Is.EqualTo(fog.b).Within(1e-4f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        static float Distance(Color a, Color b)
            => new Vector3(a.r - b.r, a.g - b.g, a.b - b.b).magnitude;

        [Test]
        public void Flicker_StaysWithinDepthOfBase_AndDesynchronizesAcrossRoles()
        {
            const float baseIntensity = 3.4f;
            var min = float.MaxValue;
            var max = float.MinValue;
            for (var i = 0; i <= 400; i++)
            {
                var t = i * 0.02f;
                var v = LightFlicker.IntensityAt(baseIntensity, 0, t);
                min = Mathf.Min(min, v);
                max = Mathf.Max(max, v);
            }
            Assert.That(min, Is.GreaterThanOrEqualTo(baseIntensity * (1f - LightFlicker.Depth) - 1e-4f),
                "flicker must never drop the pool below the authored floor");
            Assert.That(max, Is.LessThanOrEqualTo(baseIntensity * (1f + LightFlicker.Depth) + 1e-4f),
                "flicker must never blow past the authored ceiling into bloom");
            Assert.That(max - min, Is.GreaterThan(baseIntensity * 0.05f),
                "a flicker that never moves is not a flicker");

            // Four lights pulsing in lockstep read as a global brightness sweep.
            var desynced = false;
            for (var i = 0; i < 200 && !desynced; i++)
            {
                var t = i * 0.02f;
                for (var role = 1; role < 4 && !desynced; role++)
                {
                    if (Mathf.Abs(LightFlicker.IntensityAt(baseIntensity, 0, t)
                                  - LightFlicker.IntensityAt(baseIntensity, role, t))
                        > baseIntensity * 0.02f)
                        desynced = true;
                }
            }
            Assert.That(desynced, Is.True, "roles must not share a phase");
        }

        // --------------------------------------------- 1. stage textures -----

        [Test]
        public void StageTextures_ExistForEveryStage_AndTileWithoutSmearing()
        {
            foreach (var stageId in StageIds)
            {
                foreach (var suffix in new[] { "-stone", "-floor" })
                {
                    var path = EnvironmentBuilder.StageTexturePath + stageId + suffix;
                    var texture = Resources.Load<Texture2D>(path);
                    Assert.That(texture, Is.Not.Null,
                        $"{path} missing — regenerate with tools/gen_env_textures.sh");
                    Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Repeat),
                        $"{path}: Clamp smears the edge pixel across every tile");
                    Assert.That(texture.width, Is.LessThanOrEqualTo(1024),
                        $"{path}: WebGL texture ceiling is 1024 (CLAUDE.md §1)");
                    Assert.That(texture.height, Is.LessThanOrEqualTo(1024), path);
                }
            }
        }

        [Test]
        public void StageTextures_AreBoundToTheSharedEnvironmentMaterials()
        {
            GameObject a = null, b = null;
            try
            {
                a = EnvironmentBuilder.Build("cinder-span");
                var stoneA = FindMaterial(a, "env-stone");
                var texA = stoneA.GetTexture("_BaseMap");
                Assert.That(texA, Is.Not.Null,
                    "cinder-span stone material carries no generated albedo");

                b = EnvironmentBuilder.Build("abyss-chancel");
                var stoneB = FindMaterial(b, "env-stone");
                Assert.That(stoneB.GetTexture("_BaseMap"), Is.Not.SameAs(texA),
                    "each stage must bind its OWN concept texture");
                Assert.That(stoneB, Is.SameAs(stoneA),
                    "rebinding, not cloning: the §E7 material budget is 4 for env");
            }
            finally
            {
                if (a != null) Object.DestroyImmediate(a);
                if (b != null) Object.DestroyImmediate(b);
            }
        }

        static Material FindMaterial(GameObject root, string name)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material != null && material.name == name)
                        return material;
            Assert.Fail($"material {name} not found under {root.name}");
            return null;
        }
    }
}
