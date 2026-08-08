using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Auto-initialize icon materials on project load.
/// This ensures materials are created before HudView tries to use them.
/// </summary>
[InitializeOnLoad]
public static class AutoInitializeIconMaterials
{
    private const string InitKey = "CinderCourt_IconMaterials_Initialized";
    private const string ShaderPath = "Assets/Shaders/UI-Icon-Glow.shader";
    private const string MaterialPath = "Assets/Resources/Materials/UIIcon-GlowMaterial.mat";

    static AutoInitializeIconMaterials()
    {
        // Only run once per session
        if (SessionState.GetBool(InitKey, false))
            return;

        SessionState.SetBool(InitKey, true);
        
        Debug.Log("[AutoInitializeIconMaterials] Checking icon material setup...");
        
        // Check if material exists
        var existingMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (existingMaterial != null)
        {
            Debug.Log("[AutoInitializeIconMaterials] ✓ Material already exists");
            return;
        }

        // Check if shader exists
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
        {
            Debug.LogWarning("[AutoInitializeIconMaterials] ✗ Shader not found: " + ShaderPath);
            return;
        }

        Debug.Log("[AutoInitializeIconMaterials] Creating material...");
        
        // Create material
        var material = new Material(shader)
        {
            name = "UIIcon-GlowMaterial"
        };

        // Set default properties
        material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
        material.SetColor("_GlowColor", new Color(1f, 0.6f, 0.2f, 1f));
        material.SetFloat("_GlowIntensity", 1f);
        material.SetVector("_ShadowOffset", new Vector4(1f, -1f, 0f, 0f));
        material.SetColor("_ShadowColor", new Color(0f, 0f, 0f, 0.3f));
        material.SetFloat("_OutlineWidth", 0.02f);

        // Create directory if needed
        System.IO.Directory.CreateDirectory("Assets/Resources/Materials");

        // Save material
        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[AutoInitializeIconMaterials] ✓ Material created at " + MaterialPath);
    }
}
