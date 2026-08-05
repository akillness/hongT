// Particle seed material (spec §V3 WebGL shader-stripping contract).
// URP strips shader variants that no serialized material references. The
// runtime never calls new Material(Shader.Find(...)) for particles — it
// clones THIS asset, whose transparent-additive variant therefore survives
// the WebGL build. Idempotent: safe to re-run (batchmode friendly).
using UnityEditor;
using UnityEngine;

namespace CinderCourt.EditorTools
{
    public static class ParticleSeedMaterial
    {
        const string AssetPath = "Assets/Resources/Materials/particle-additive-seed.mat";

        [MenuItem("CinderCourt/Create Particle Seed Material")]
        public static void Create()
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError("[particle-seed] URP Particles/Unlit shader not found");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var existing = AssetDatabase.LoadAssetAtPath<Material>(AssetPath);
            var material = existing != null ? existing : new Material(shader);
            material.shader = shader;
            // Transparent surface, additive blend — the glow grammar for
            // element impacts. Floats + tag + keyword mirror what URP's shader
            // GUI writes, so the serialized variant matches runtime state.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);   // BlendMode.Additive (URP enum)
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetColor("_BaseColor", Color.white);

            if (existing == null) AssetDatabase.CreateAsset(material, AssetPath);
            else EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log($"[particle-seed] ready at {AssetPath}");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
