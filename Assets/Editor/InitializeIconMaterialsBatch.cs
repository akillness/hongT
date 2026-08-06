using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Batch mode script to initialize icon materials without manual editor interaction.
/// Usage: Unity -batchmode -projectPath . -executeMethod InitializeIconMaterialsBatch.Execute
/// </summary>
public static class InitializeIconMaterialsBatch
{
    public static void Execute()
    {
        Debug.Log("[InitializeIconMaterialsBatch] Starting...");
        
        try
        {
            // Ensure directories
            Directory.CreateDirectory("Assets/Materials");
            Directory.CreateDirectory("Assets/Resources/Materials");
            Directory.CreateDirectory("Assets/Shaders");

            // Load shader
            Shader glowShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/UI-Icon-Glow.shader");
            if (glowShader == null)
            {
                Debug.LogError("[InitializeIconMaterialsBatch] Shader not found!");
                EditorApplication.Exit(1);
                return;
            }

            // Create base material
            var baseMaterial = new Material(glowShader)
            {
                name = "UIIcon-GlowMaterial"
            };
            baseMaterial.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            baseMaterial.SetColor("_GlowColor", new Color(1f, 0.6f, 0.2f, 1f));
            baseMaterial.SetFloat("_GlowIntensity", 1f);
            baseMaterial.SetVector("_ShadowOffset", new Vector4(1f, -1f, 0f, 0f));
            baseMaterial.SetColor("_ShadowColor", new Color(0f, 0f, 0f, 0.3f));
            baseMaterial.SetFloat("_OutlineWidth", 0.02f);

            AssetDatabase.CreateAsset(baseMaterial, "Assets/Resources/Materials/UIIcon-GlowMaterial.mat");
            Debug.Log("[InitializeIconMaterialsBatch] ✓ Base material created");

            // Create variants
            CreateVariant(glowShader, "UIIcon-Glow-Warm.mat",
                new Color(1f, 0.6f, 0.2f, 1f), 1.2f);
            CreateVariant(glowShader, "UIIcon-Glow-Cold.mat",
                new Color(0.3f, 0.9f, 1f, 1f), 1.1f);
            CreateVariant(glowShader, "UIIcon-Glow-Void.mat",
                new Color(0.7f, 0.3f, 1f, 1f), 1.3f);
            CreateVariant(glowShader, "UIIcon-Glow-Neutral.mat",
                new Color(0.8f, 0.8f, 0.8f, 1f), 0.8f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[InitializeIconMaterialsBatch] ✓ All materials created successfully");
            EditorApplication.Exit(0);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[InitializeIconMaterialsBatch] Error: {ex}");
            EditorApplication.Exit(1);
        }
    }

    private static void CreateVariant(Shader shader, string name, Color glowColor, float intensity)
    {
        var mat = new Material(shader)
        {
            name = Path.GetFileNameWithoutExtension(name)
        };
        mat.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
        mat.SetColor("_GlowColor", glowColor);
        mat.SetFloat("_GlowIntensity", intensity);
        mat.SetVector("_ShadowOffset", new Vector4(1f, -1f, 0f, 0f));
        mat.SetColor("_ShadowColor", new Color(0f, 0f, 0f, 0.3f));
        mat.SetFloat("_OutlineWidth", 0.02f);

        AssetDatabase.CreateAsset(mat, $"Assets/Resources/Materials/{name}");
        Debug.Log($"[InitializeIconMaterialsBatch] ✓ Created variant: {name}");
    }
}
