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
using CinderCourt.Sim;
using UnityEngine;

namespace CinderCourt.View
{
    public static class StageMood
    {
        /// <summary>Root name of the rig; also how tests find it.</summary>
        public const string RootName = "StageMood";

        /// <summary>Key-light pitch/yaw — a low raking angle for long shapes.
        /// These stay the DEFAULT (and the fallback for any stage without a
        /// row in <see cref="Rigs"/>); per-stage variation lives in the table
        /// below, not by editing these.</summary>
        public const float KeyPitch = 42f, KeyYaw = 28f;

        /// <summary>
        /// Lowest authored key angle. Receiver coverage uses the same value so
        /// the longest boss-stage projection remains inside the floor mesh.
        /// </summary>
        public const float MinimumCharacterShadowPitch = 24f;

        /// <summary>Default key/fill strengths — same fallback role as above.</summary>
        public const float KeyIntensity = 0.55f, FillIntensity = 0.22f;

        /// <summary>
        /// Per-stage rig. Before this, ONE geometry served all nine stages and
        /// accent colour was the only thing that changed between them — so two
        /// stages with close accents rendered as the same room. Direction and
        /// contrast are the other half of a stage's read, and they cost nothing:
        /// this is four floats, no new lights (§E6 still sees zero extra), no
        /// new materials.
        ///
        /// Invariant every row must hold: Key > Fill. The rig test asserts it
        /// (a fill that meets the key flattens the scene), so a row that
        /// inverts them fails loudly rather than looking slightly wrong.
        /// </summary>
        readonly struct Rig
        {
            public readonly float Pitch, Yaw, Key, Fill;
            public Rig(float pitch, float yaw, float key, float fill)
            {
                Pitch = pitch; Yaw = yaw; Key = key; Fill = fill;
            }
        }

        /// <summary>
        /// Rows are grouped by what the stage IS, not by index:
        ///   ember stages  — low hot key, hard contrast (a fire lights from the side)
        ///   cold/vault    — higher key, lifted fill (diffuse light down a nave)
        ///   boss plates   — lowest key angle, deepest contrast (long shadows)
        /// A stage missing from this table gets the defaults, so adding a
        /// stage never requires touching StageMood.
        /// </summary>
        static readonly System.Collections.Generic.Dictionary<string, Rig> Rigs =
            new System.Collections.Generic.Dictionary<string, Rig>
            {
                // Ember family: rake the key low so ember light throws long.
                ["cinder-span"]   = new Rig(34f, 20f, 0.58f, 0.20f),
                ["ember-gallery"] = new Rig(30f, 14f, 0.60f, 0.18f),
                ["ember-bastion"] = new Rig(28f, 40f, 0.62f, 0.17f),
                // Vault family: light falls from higher and bounces more.
                ["abyss-chancel"] = new Rig(52f, 34f, 0.52f, 0.26f),
                ["witness-well"]  = new Rig(56f, 22f, 0.50f, 0.28f),
                ["cinder-sluice"] = new Rig(48f, 44f, 0.54f, 0.24f),
                // Judgement/boss plates: the flattest key, the deepest shadow.
                ["echo-throne"]   = new Rig(24f, 30f, 0.56f, 0.14f),
                ["ash-verdict"]   = new Rig(38f, 52f, 0.57f, 0.19f),
                ["ash-march"]     = new Rig(44f, 8f,  0.53f, 0.23f),
            };

        static Rig RigFor(string stageId)
            => Rigs.TryGetValue(stageId, out var rig)
                ? rig
                : new Rig(KeyPitch, KeyYaw, KeyIntensity, FillIntensity);

        /// <summary>
        /// Builds the mood rig for a stage and returns its root, or null for an
        /// unknown stage id. Also tints global ambient + fog toward the stage
        /// accent so the dungeon's unlit/additive surfaces share the same mood
        /// as the lit ones.
        /// </summary>
        public static GameObject Apply(string stageId)
            => Apply(stageId, SimConfig.ArenaHalfWidth, SimConfig.ArenaHalfHeight);

        /// <summary>
        /// Builds the stage rig and a continuous receiver sized to the active
        /// dungeon playfield. The overload keeps existing tests/callers on the
        /// frozen arena defaults while GameDirector supplies expanded half-axes.
        /// </summary>
        public static GameObject Apply(
            string stageId, float halfWidthSim, float halfHeightSim)
        {
            if (string.IsNullOrEmpty(stageId)) return null;
            if (!StageCatalog.TryGet(stageId, out var entry)) return null;
            StageShadowPolicy.RestoreCurrent();
            var accent = entry.AccentColor;
            var rig = RigFor(stageId);

            var root = new GameObject(RootName);

            // Key: cool moonlight-through-vault direction light. Low intensity —
            // it exists to separate wall from floor, not to flatten the scene.
            var key = new GameObject("mood-key").transform;
            key.SetParent(root.transform, false);
            key.rotation = Quaternion.Euler(rig.Pitch, rig.Yaw, 0f);
            var keyLight = key.gameObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = KeyColor(accent);
            keyLight.intensity = rig.Key;
            keyLight.shadows = LightShadows.Hard;

            // Fill: opposite-side bounce in the stage accent, so the shadow side
            // reads as stage colour instead of black. Yaw stays tied to the
            // key's so the pair keeps its opposition when a row moves the key.
            var fill = new GameObject("mood-fill").transform;
            fill.SetParent(root.transform, false);
            fill.rotation = Quaternion.Euler(18f, rig.Yaw + 180f, 0f);
            var fillLight = fill.gameObject.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = accent;
            fillLight.intensity = rig.Fill;
            fillLight.shadows = LightShadows.None;

            var policy = root.AddComponent<StageShadowPolicy>();
            policy.Acquire(keyLight, accent, halfWidthSim, halfHeightSim);
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

        /// <summary>
        /// Restores the exact stage lease (sun, ambient mode/colour, fog and
        /// the captured URP asset's resolution/distance) once. GameDirector
        /// calls this before destroying the root; component teardown is the
        /// idempotent safety net for every other path.
        /// </summary>
        public static void Clear() => StageShadowPolicy.RestoreCurrent();
    }
}
