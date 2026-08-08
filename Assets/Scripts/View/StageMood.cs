// StageMood - per-stage atmosphere rig (2026-10 request: add light
// sources so the dungeon reads as a mood-lit space).
//
// View-only, built ONCE at stage entry next to EnvironmentBuilder.Build and
// destroyed with it. Deliberately a SEPARATE root from "StageEnvironment":
// docs/SIM_SPEC_ENVIRONMENT.md §E6 caps that root at four realtime POINT
// lights (EnvironmentBuilderTests.Budget_VerticesMaterialsAndLights), and the
// mood rig adds directional/ambient light, which costs one forward pass for
// the whole scene rather than a per-object light slot.
//
// Nothing here reads or writes sim state.
using UnityEngine;

namespace CinderCourt.View
{
    public static class StageMood
    {
        /// <summary>Root name of the rig; also how tests find it.</summary>
        public const string RootName = "StageMood";

        /// <summary>Key-light pitch/yaw — a low raking angle for long shapes.</summary>
        public const float KeyPitch = 42f, KeyYaw = 28f;

        /// <summary>
        /// Builds the mood rig for a stage and returns its root, or null for an
        /// unknown stage id. Also tints global ambient + fog toward the stage
        /// accent so the dungeon's unlit/additive surfaces share the same mood
        /// as the lit ones.
        /// </summary>
        public static GameObject Apply(string stageId)
        {
            if (string.IsNullOrEmpty(stageId)) return null;
            if (!StageCatalog.TryGet(stageId, out var entry)) return null;
            var accent = entry.AccentColor;

            var root = new GameObject(RootName);

            // Key: cool moonlight-through-vault direction light. Low intensity —
            // it exists to separate wall from floor, not to flatten the scene.
            var key = new GameObject("mood-key").transform;
            key.SetParent(root.transform, false);
            key.rotation = Quaternion.Euler(KeyPitch, KeyYaw, 0f);
            var keyLight = key.gameObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = KeyColor(accent);
            keyLight.intensity = 0.55f;
            keyLight.shadows = LightShadows.None;   // §E6: zero shadow casters

            // Fill: opposite-side bounce in the stage accent, so the shadow side
            // reads as stage colour instead of black.
            var fill = new GameObject("mood-fill").transform;
            fill.SetParent(root.transform, false);
            fill.rotation = Quaternion.Euler(18f, KeyYaw + 180f, 0f);
            var fillLight = fill.gameObject.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = accent;
            fillLight.intensity = 0.22f;
            fillLight.shadows = LightShadows.None;

            ApplyAmbient(accent);
            return root;
        }

        /// <summary>Key-light hue: accent pulled most of the way to cold white.</summary>
        public static Color KeyColor(Color accent)
            => Color.Lerp(accent, new Color(0.78f, 0.84f, 1f), 0.72f);

        /// <summary>Ambient floor: a dark accent wash, never pure black.</summary>
        public static Color AmbientColor(Color accent)
            => new Color(
                accent.r * 0.20f + 0.045f,
                accent.g * 0.20f + 0.050f,
                accent.b * 0.24f + 0.070f,
                1f);

        /// <summary>Fog hue: darker than ambient so depth still reads as falloff.</summary>
        public static Color FogColor(Color accent)
            => new Color(
                accent.r * 0.12f + 0.020f,
                accent.g * 0.12f + 0.024f,
                accent.b * 0.16f + 0.036f,
                1f);

        static Color _bakedAmbient;
        static Color _bakedFog;
        static bool _baked;

        static void ApplyAmbient(Color accent)
        {
            if (!_baked)
            {
                _bakedAmbient = RenderSettings.ambientLight;
                _bakedFog = RenderSettings.fogColor;
                _baked = true;
            }
            RenderSettings.ambientMode =
                UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = AmbientColor(accent);
            RenderSettings.fogColor = FogColor(accent);
        }

        /// <summary>
        /// Restores the scene-authored ambient/fog colour. RenderSettings is
        /// GLOBAL, so leaving a dungeon's wash behind would tint the lobby —
        /// the same failure CameraRig guards against for the fog band.
        /// </summary>
        public static void Clear()
        {
            if (!_baked) return;
            RenderSettings.ambientLight = _bakedAmbient;
            RenderSettings.fogColor = _bakedFog;
        }
    }
}
