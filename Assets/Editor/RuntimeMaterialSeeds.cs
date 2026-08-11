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
        const string StageShadowReceiverAssetPath = Dir + "/StageShadowReceiver.mat";
        const string StageShadowReceiverShaderPath = "Assets/Shaders/StageShadowReceiver.shader";
        const string StageShadowReceiverShaderName = "CinderCourt/StageShadowReceiver";
        const int StageShadowReceiverQueue = 2499;
        const float StageShadowReceiverMaxStrength = 0.65f;

        /// <summary>Batch entry: -executeMethod ...RuntimeMaterialSeeds.EnsureSeeds</summary>
        public static void EnsureSeeds()
        {
            var ok = Seed();
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Callable from BuildScript - no editor exit.</summary>
        internal static bool Seed()
        {
            var receiverOk = SeedStageShadowReceiver();
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
            return particlesOk && receiverOk;
        }

        /// <summary>
        /// Retains and validates the opaque-phase stage shadow receiver.
        ///
        /// The committed Resources material is the build-retention reference;
        /// this method is only its fail-closed repair path. Shader.Find is not
        /// used because package/editor registration order must not decide
        /// whether the WebGL player keeps the custom shadow variants.
        /// </summary>
        static bool SeedStageShadowReceiver()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(StageShadowReceiverShaderPath);
            if (shader == null || shader.name != StageShadowReceiverShaderName)
            {
                Debug.LogError("[MaterialSeeds] stage receiver shader missing or misnamed at "
                               + StageShadowReceiverShaderPath);
                return false;
            }

            Directory.CreateDirectory(Dir);
            var material = AssetDatabase.LoadAssetAtPath<Material>(StageShadowReceiverAssetPath);
            var changed = false;
            if (material == null)
            {
                material = new Material(shader) { name = "StageShadowReceiver" };
                material.renderQueue = StageShadowReceiverQueue;
                material.SetOverrideTag("RenderType", "Opaque");
                AssetDatabase.CreateAsset(material, StageShadowReceiverAssetPath);
                changed = true;
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                changed = true;
            }

            if (material.renderQueue != StageShadowReceiverQueue)
            {
                material.renderQueue = StageShadowReceiverQueue;
                changed = true;
            }
            if (material.GetTag("RenderType", false, string.Empty) != "Opaque")
            {
                material.SetOverrideTag("RenderType", "Opaque");
                changed = true;
            }
            if (material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))
            {
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                changed = true;
            }
            if (!material.HasProperty("_ShadowStrength"))
            {
                Debug.LogError("[MaterialSeeds] stage receiver has no _ShadowStrength property");
                return false;
            }

            var strength = material.GetFloat("_ShadowStrength");
            var clampedStrength = Mathf.Clamp(strength, 0f, StageShadowReceiverMaxStrength);
            if (!Mathf.Approximately(strength, clampedStrength))
            {
                material.SetFloat("_ShadowStrength", clampedStrength);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
            }
            Debug.Log("[MaterialSeeds] StageShadowReceiver ready");
            return true;
        }

        /// <summary>
        /// §V3 particle seed. The four pooled element ParticleSystems in
        /// VfxDirector want URP's Particles/Unlit shader — it is the only path
        /// that honours per-particle vertex colour, i.e. colour/alpha over
        /// lifetime. This serialized asset is the reference that keeps the
        /// variant in the build; ViewWorld.MakeParticleAdditive clones it and
        /// never calls Shader.Find.
        ///
        /// The asset is now COMMITTED (Assets/Resources/Materials/
        /// particle-additive-seed.mat, shader referenced by GUID), so the build
        /// no longer depends on this generator running at all. What follows is
        /// a validate/repair path, not the only creation path.
        ///
        /// Two bugs made the old generator a silent no-op for its whole life:
        ///   1. It asked for "Universal Render Pipeline/Particles Unlit".
        ///      The shader is named ".../Particles/Unlit" (slash, verified in
        ///      Shaders/Particles/ParticlesUnlit.shader:1). No shader has ever
        ///      had the old name, so Find always returned null.
        ///   2. Even spelled right, Shader.Find only resolves shaders already
        ///      registered in the editor's shader list, which package shaders
        ///      often are not in batchmode.
        /// Both were invisible because the null branch returns true ("a
        /// decorative layer must not fail a build") — so every WebGL build
        /// logged the warning, no asset was ever written, and all four element
        /// systems ran the flat-colour fallback with colour-over-lifetime dead.
        /// Lesson (§4z): a fallback that reports success hides the thing it is
        /// falling back from.
        ///
        /// Returns true when the shader cannot be resolved as well as when the
        /// seed is written: the runtime falls back to the proven URP/Unlit
        /// additive path, which loses per-particle fades but renders correctly.
        /// </summary>
        static bool SeedParticleAdditive()
        {
            // Virtual Packages/ path first: it resolves regardless of the
            // PackageCache hash directory and does not need the shader to be in
            // the editor's registered list. Shader.Find is the fallback.
            const string ShaderAssetPath =
                "Packages/com.unity.render-pipelines.universal/Shaders/Particles/ParticlesUnlit.shader";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderAssetPath)
                         ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogWarning(
                    "[MaterialSeeds] URP Particles/Unlit unresolvable at "
                    + ShaderAssetPath + " and via Shader.Find - element particles "
                    + "fall back to the URP/Unlit additive seed");
                return true;
            }
            Directory.CreateDirectory(Dir);
            var material = AssetDatabase.LoadAssetAtPath<Material>(ParticleAssetPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, ParticleAssetPath);
            }
            else if (material.shader != shader)
            {
                // Repair: a committed seed pointing at the wrong shader would
                // reintroduce the stripped-variant bug with no warning.
                material.shader = shader;
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
