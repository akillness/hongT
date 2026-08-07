// Seed materials for runtime-instantiated shaders.
//
// Every transparent material in the View layer is created AT RUNTIME via
// ViewWorld.MakeUnlit(..., transparent: true). URP's build-time variant
// stripping only keeps shader variants referenced by materials that exist in
// the build - so the _SURFACE_TYPE_TRANSPARENT variant of URP/Unlit gets
// stripped and every "transparent" runtime material renders opaque on WebGL
// (vent discs as solid ellipses, pickup icon margins as dark plates).
//
// The fix: one serialized material asset in Resources with the transparent
// keyword enabled. It ships in the build, the variant survives stripping, and
// ViewWorld.MakeUnlit clones it at runtime.
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    public static class RuntimeMaterialSeeds
    {
        const string Dir = "Assets/Resources/Materials";
        const string AssetPath = Dir + "/unlit-transparent-seed.mat";
        const string ParticleAssetPath = Dir + "/particle-additive-seed.mat";

        /// <summary>Batch entry: -executeMethod ...RuntimeMaterialSeeds.EnsureSeeds</summary>
        public static void EnsureSeeds()
        {
            var ok = Seed();
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Callable from BuildScript - no editor exit.</summary>
        internal static bool Seed()
        {
            var particlesOk = SeedParticleAdditive();
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null)
            {
                Debug.LogError("[MaterialSeeds] URP/Unlit missing");
                return false;
            }
            Directory.CreateDirectory(Dir);
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetPath);
            if (material == null)
            {
                material = new Material(unlit);
                AssetDatabase.CreateAsset(material, AssetPath);
            }
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); // straight alpha (no premultiply)
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log("[MaterialSeeds] unlit-transparent-seed ready");
            return particlesOk;
        }

        /// <summary>
        /// §V3 particle seed. The four pooled element ParticleSystems in
        /// VfxDirector want URP's Particles Unlit shader — it is the only path
        /// that honours per-particle vertex colour, i.e. colour/alpha over
        /// lifetime. That shader has ZERO material references in this project,
        /// so on WebGL its variants are stripped and a runtime
        /// <c>new Material(Shader.Find(...))</c> renders pink. This serialized
        /// asset is the reference that keeps the variant in the build;
        /// ViewWorld.MakeParticleAdditive clones it and never calls Shader.Find.
        ///
        /// Returns true when the shader is missing as well as when the seed is
        /// written: the runtime falls back to the proven URP/Unlit additive
        /// path, which loses per-particle fades but renders correctly — a
        /// decorative layer must not fail a build.
        /// </summary>
        static bool SeedParticleAdditive()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles Unlit");
            if (shader == null)
            {
                Debug.LogWarning(
                    "[MaterialSeeds] URP/Particles Unlit missing - "
                    + "element particles fall back to the URP/Unlit additive seed");
                return true;
            }
            Directory.CreateDirectory(Dir);
            var material = AssetDatabase.LoadAssetAtPath<Material>(ParticleAssetPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, ParticleAssetPath);
            }
            // Additive-with-alpha, matching ViewWorld.MakeAdditive's blend so the
            // element bursts accumulate past the Bloom threshold exactly like the
            // LineRenderer grammar they augment.
            // Property names verified against the shipped shader
            // (Shaders/Particles/ParticlesUnlit.shader): _Surface, _Blend,
            // _ColorMode, _Cull, and the four explicit blend factors the pass
            // reads as `Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]`.
            material.SetFloat("_Surface", 1f);          // transparent
            material.SetFloat("_Blend", 2f);            // URP particle blend: additive
            material.SetFloat("_ColorMode", 0f);        // multiply by vertex colour
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            // Straight alpha, exactly like ViewWorld.MakeAdditive: neither
            // premultiply nor alpha-modulate. Leaving _ColorMode at 0 (multiply)
            // also leaves the _COLOROVERLAY/_COLORCOLOR/_COLORADDSUBDIFF trio
            // off, which is what makes colour-over-lifetime a plain tint.
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            // Soft particles / distortion stay OFF: both need the depth or
            // opaque texture, which the WebGL renderer budget does not fund.
            material.DisableKeyword("_SOFTPARTICLES_ON");
            material.DisableKeyword("_DISTORTION_ON");
            material.SetFloat("_SoftParticlesEnabled", 0f);
            material.SetFloat("_DistortionEnabled", 0f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log("[MaterialSeeds] particle-additive-seed ready");
            return true;
        }
    }
}
