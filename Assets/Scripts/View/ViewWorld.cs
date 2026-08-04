// Shared coordinate mapping + material helpers for the view assembly.
// Sim world (x right, y screen-down/near) -> Unity XZ plane: (x*S, 0, -y*S).
using UnityEngine;

namespace CinderCourt.View
{
    public static class ViewWorld
    {
        public const float Scale = 0.01f;

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
    }
}
