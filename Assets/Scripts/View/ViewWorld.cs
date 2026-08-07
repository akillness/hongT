// Shared coordinate mapping + material helpers for the view assembly.
// Sim world (x right, y screen-down/near) -> Unity XZ plane: (x*S, 0, -y*S).
using UnityEngine;

namespace CinderCourt.View
{
    public static class ViewWorld
    {
        // Dungeon-scale request (2026-10): the sim contract (CLAUDE.md §2) is
        // FROZEN at a 1536x1024 arena, so "make the dungeon bigger" can only be
        // a VIEW change — the sim-to-world quotient. 0.01 -> 0.0125 grows every
        // world-space distance derived from sim coordinates by 25% while actor
        // prefab sizes (authored directly in world units) stay put, so the floor
        // reads larger and the characters read smaller standing on it. Camera
        // constants tuned against the old quotient are compensated with
        // LegacyScaleRatio so only the dungeon framing actually changes.
        public const float Scale = 0.0125f;

        /// <summary>Pre-2026-10 sim-to-world quotient (framing compensation).</summary>
        public const float LegacyScale = 0.01f;

        /// <summary>Restores legacy framing for a world-unit camera constant.</summary>
        public const float LegacyScaleRatio = Scale / LegacyScale;

        public static Vector3 ToWorld(float simX, float simY, float height = 0f)
            => new Vector3(simX * Scale, height, -simY * Scale);

        public static readonly Vector3 ArenaCenter = ToWorld(768f, 604f);

        static Shader _unlit;
        public static Shader UnlitShader
        {
            get
            {
                if (_unlit == null) _unlit = Shader.Find("Universal Render Pipeline/Unlit");
                return _unlit;
            }
        }

        static Material _transparentSeed;

        public static Material MakeUnlit(Color color, bool transparent)
        {
            if (transparent)
            {
                // Clone the serialized seed so the _SURFACE_TYPE_TRANSPARENT
                // variant survives URP build-time stripping. Runtime-created
                // materials cannot summon stripped variants - without the seed
                // every transparent surface renders opaque in WebGL builds.
                if (_transparentSeed == null)
                    _transparentSeed = Resources.Load<Material>("Materials/unlit-transparent-seed");
                if (_transparentSeed != null)
                {
                    var clone = new Material(_transparentSeed) { color = color };
                    return clone;
                }
            }
            var material = new Material(UnlitShader) { color = color };
            if (transparent)
            {
                // Editor / seed-missing fallback: same setup, works in-editor
                // where no variant stripping occurs.
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            return material;
        }

        /// <summary>
        /// Additive-with-alpha unlit material (SrcAlpha/One) for glow-class
        /// VFX: overlapping quads/lines ACCUMULATE brightness, so stacked
        /// effects push past the Bloom threshold (CinderPostProfile 1.05)
        /// and visibly flare instead of muddying like straight alpha blend.
        /// Clones the same serialized transparent seed as MakeUnlit so the
        /// _SURFACE_TYPE_TRANSPARENT variant survives WebGL stripping; only
        /// the destination blend factor changes. Alpha fades keep working —
        /// source is still scaled by SrcAlpha.
        /// </summary>
        public static Material MakeAdditive(Color color)
        {
            var material = MakeUnlit(color, true);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            return material;
        }

        static Shader _lit;

        /// <summary>
        /// URP Lit shader, or null when it is not in the build. Every character
        /// prefab ships Lit materials, so the shader and its forward variants
        /// are always included — but the null guard keeps a stripped build from
        /// producing magenta environment instead of a flat-but-correct one.
        /// </summary>
        public static Shader LitShader
        {
            get
            {
                if (_lit == null) _lit = Shader.Find("Universal Render Pipeline/Lit");
                return _lit;
            }
        }

        /// <summary>
        /// Opaque LIT material with an optional albedo map. Used by the stage
        /// environment so the §E6 point lights and the stage mood rig actually
        /// shade the dungeon (unlit surfaces ignore every light). Falls back to
        /// <see cref="MakeUnlit"/> when the Lit shader is unavailable.
        /// </summary>
        public static Material MakeLit(Color color, Texture texture)
        {
            var shader = LitShader;
            if (shader == null)
            {
                var fallback = MakeUnlit(color, false);
                if (texture != null) fallback.SetTexture("_BaseMap", texture);
                return fallback;
            }
            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            // Dungeon stone/floor: rough, non-metallic. Specular highlights on
            // a tiling masonry map read as plastic under point lights.
            material.SetFloat("_Smoothness", 0.12f);
            material.SetFloat("_Metallic", 0f);
            if (texture != null)
            {
                texture.wrapMode = TextureWrapMode.Repeat;
                material.SetTexture("_BaseMap", texture);
                material.mainTexture = texture;
            }
            return material;
        }
    }
}
