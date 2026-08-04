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

        /// <summary>Batch entry: -executeMethod ...RuntimeMaterialSeeds.EnsureSeeds</summary>
        public static void EnsureSeeds()
        {
            var ok = Seed();
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Callable from BuildScript - no editor exit.</summary>
        internal static bool Seed()
        {
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
            return true;
        }
    }
}
