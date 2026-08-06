using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor utility to create UI Icon Glow material and related assets.
/// Run from menu: Assets > Cinder Court > Create Icon Materials
/// </summary>
public static class CreateIconMaterials
{
    private const string ShaderPath = "Assets/Shaders/UI-Icon-Glow.shader";
    private const string MaterialPath = "Assets/Resources/Materials/UIIcon-GlowMaterial.mat";
    private const string MenuPath = "Assets/Cinder Court/";

    [MenuItem(MenuPath + "Create Icon Glow Material")]
    public static void CreateGlowMaterial()
    {
        Debug.Log("Creating UI Icon Glow Material...");

        // Ensure directories exist
        EnsureDirectories();

        // Load shader
        Shader glowShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (glowShader == null)
        {
            Debug.LogError($"Shader not found at {ShaderPath}");
            return;
        }

        // Create material
        Material glowMaterial = new Material(glowShader)
        {
            name = "UIIcon-GlowMaterial"
        };

        // Set default properties
        glowMaterial.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
        glowMaterial.SetColor("_GlowColor", new Color(1f, 0.6f, 0.2f, 1f));
        glowMaterial.SetFloat("_GlowIntensity", 1f);
        glowMaterial.SetVector("_ShadowOffset", new Vector4(1f, -1f, 0f, 0f));
        glowMaterial.SetColor("_ShadowColor", new Color(0f, 0f, 0f, 0.3f));
        glowMaterial.SetFloat("_OutlineWidth", 0.02f);

        // Save material
        AssetDatabase.CreateAsset(glowMaterial, MaterialPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"✓ Material created at {MaterialPath}");
        EditorGUIUtility.PingObject(glowMaterial);
    }

    [MenuItem(MenuPath + "Create Icon Variants")]
    public static void CreateIconVariants()
    {
        Debug.Log("Creating icon material variants...");

        EnsureDirectories();

        Shader glowShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (glowShader == null)
        {
            Debug.LogError($"Shader not found at {ShaderPath}");
            return;
        }

        // Warm theme (Skills, Equipment, Pickups)
        CreateVariantMaterial(
            glowShader,
            "UIIcon-Glow-Warm.mat",
            new Color(1f, 1f, 1f, 1f),
            new Color(1f, 0.6f, 0.2f, 1f),  // Orange glow
            1.2f
        );

        // Cold theme (Void skills, UI)
        CreateVariantMaterial(
            glowShader,
            "UIIcon-Glow-Cold.mat",
            new Color(1f, 1f, 1f, 1f),
            new Color(0.3f, 0.9f, 1f, 1f),  // Blue glow
            1.1f
        );

        // Void theme (Dark skills)
        CreateVariantMaterial(
            glowShader,
            "UIIcon-Glow-Void.mat",
            new Color(1f, 1f, 1f, 1f),
            new Color(0.7f, 0.3f, 1f, 1f),  // Purple glow
            1.3f
        );

        // Neutral UI theme
        CreateVariantMaterial(
            glowShader,
            "UIIcon-Glow-Neutral.mat",
            new Color(1f, 1f, 1f, 1f),
            new Color(0.8f, 0.8f, 0.8f, 1f),  // Gray glow
            0.8f
        );

        Debug.Log("✓ Icon material variants created");
    }

    private static void CreateVariantMaterial(
        Shader shader,
        string materialName,
        Color baseColor,
        Color glowColor,
        float glowIntensity
    )
    {
        string path = $"Assets/Resources/Materials/{materialName}";

        Material mat = new Material(shader)
        {
            name = materialName
        };

        mat.SetColor("_Color", baseColor);
        mat.SetColor("_GlowColor", glowColor);
        mat.SetFloat("_GlowIntensity", glowIntensity);
        mat.SetVector("_ShadowOffset", new Vector4(1f, -1f, 0f, 0f));
        mat.SetColor("_ShadowColor", new Color(0f, 0f, 0f, 0.3f));
        mat.SetFloat("_OutlineWidth", 0.02f);

        AssetDatabase.CreateAsset(mat, path);
        Debug.Log($"  Created variant: {materialName}");
    }

    [MenuItem(MenuPath + "Setup Icon Prefabs")]
    public static void SetupIconPrefabs()
    {
        Debug.Log("Setting up icon prefabs with glow materials...");

        EnsureDirectories();

        // Find all Image components in scenes and apply glow materials
        var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        foreach (var canvas in canvases)
        {
            var images = canvas.GetComponentsInChildren<Image>();
            foreach (var img in images)
            {
                if (img.sprite != null && img.sprite.name.Contains("icon") || img.sprite.name.Contains("skill"))
                {
                    Material glowMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                    if (glowMat != null)
                    {
                        img.material = glowMat;
                    }
                }
            }
        }

        Debug.Log("✓ Icon prefabs setup complete");
    }

    private static void EnsureDirectories()
    {
        string[] dirs = new[]
        {
            "Assets/Materials",
            "Assets/Resources/Materials",
            "Assets/Shaders"
        };

        foreach (string dir in dirs)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
        }
    }

    [MenuItem(MenuPath + "Validate Icon Setup")]
    public static void ValidateIconSetup()
    {
        Debug.Log("Validating icon setup...");

        bool isValid = true;

        // Check shader
        if (AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath) == null)
        {
            Debug.LogError($"✗ Shader missing: {ShaderPath}");
            isValid = false;
        }
        else
        {
            Debug.Log("✓ Shader found");
        }

        // Check material
        if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) == null)
        {
            Debug.LogError($"✗ Material missing: {MaterialPath}");
            isValid = false;
        }
        else
        {
            Debug.Log("✓ Material found");
        }

        // Check icon directory
        if (!Directory.Exists("Assets/Resources/Icons/regenerated"))
        {
            Debug.LogWarning("⚠ Regenerated icons directory not found: Assets/Resources/Icons/regenerated");
        }
        else
        {
            string[] iconFiles = Directory.GetFiles("Assets/Resources/Icons/regenerated", "*.png");
            Debug.Log($"✓ Found {iconFiles.Length} regenerated icons");
        }

        if (isValid)
        {
            Debug.Log("✓ Icon setup is valid");
        }
        else
        {
            Debug.LogError("✗ Icon setup has issues - run 'Create Icon Glow Material' first");
        }
    }
}
