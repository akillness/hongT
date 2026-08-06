using CinderCourt.View;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Icon integration extension for HudView.
/// Loads regenerated icons and applies glow shader effects.
/// 
/// Call this from HudView.Build() after all UI elements are created.
/// </summary>
public static class HudIconIntegration
{
    private static Material _warmMaterial;  // Orange glow - skills, equipment, pickups
    private static Material _coldMaterial;  // Blue glow - void skills, UI
    private static Material _voidMaterial;  // Purple glow - defense skills
    private static Material _neutralMaterial; // Gray glow - UI buttons

    /// <summary>
    /// Icon category and theme mapping.
    /// Maps icon names to their glow material theme.
    /// </summary>
    private static readonly Dictionary<string, string> IconThemeMap = new()
    {
        // Skills - Warm (Orange)
        { "skill-nova", "warm" },
        { "skill-dash", "warm" },
        { "skill-strike", "warm" },
        
        // Skills - Cold (Blue)  
        { "skill-ward", "cold" },
        { "skill-pulse", "cold" },
        
        // Skills - Void (Purple)
        { "skill-bolt", "void" },
        { "skill-aegis", "void" },
        
        // Equipment - Warm
        { "equip-weapon", "warm" },
        { "equip-lantern", "warm" },
        { "equip-cloak", "neutral" },
        
        // Pickups - Warm
        { "pickup-ember", "warm" },
        { "pickup-flask", "warm" },
        { "pickup-relic", "cold" },
        
        // Stats - Mixed
        { "stat-attack", "warm" },
        { "stat-vitality", "warm" },
        { "stat-swiftness", "cold" },
        
        // UI - Neutral/Cold
        { "ui-button", "neutral" },
        { "ui-button-active", "cold" },
        { "ui-button-disabled", "neutral" },
        { "ui-pause", "neutral" },
        { "ui-play", "neutral" },
        { "ui-restart", "neutral" },
        { "ui-settings", "neutral" },
        { "ui-joystick-base", "neutral" },
        { "ui-joystick-nub", "neutral" },
        
        // Special
        { "app-lantern", "warm" },
        { "skill-cooldown-ring", "warm" },
        { "skill-highlight", "cold" },
        { "stat-oil-energy", "warm" }
    };

    /// <summary>
    /// Initialize all glow materials. Call once at startup.
    /// </summary>
    public static void InitializeMaterials()
    {
        if (_warmMaterial != null) return; // Already initialized

        _warmMaterial = Resources.Load<Material>("Materials/UIIcon-Glow-Warm");
        _coldMaterial = Resources.Load<Material>("Materials/UIIcon-Glow-Cold");
        _voidMaterial = Resources.Load<Material>("Materials/UIIcon-Glow-Void");
        _neutralMaterial = Resources.Load<Material>("Materials/UIIcon-Glow-Neutral");

        // Fallback to default glow material if variants not found
        if (_warmMaterial == null) _warmMaterial = Resources.Load<Material>("Materials/UIIcon-GlowMaterial");
        if (_coldMaterial == null) _coldMaterial = _warmMaterial;
        if (_voidMaterial == null) _voidMaterial = _warmMaterial;
        if (_neutralMaterial == null) _neutralMaterial = _warmMaterial;

        if (_warmMaterial == null)
        {
            Debug.LogWarning("[HudIconIntegration] Glow materials not found. Icons will use default material.");
        }
    }

    /// <summary>
    /// Load and apply a single icon with appropriate glow effect.
    /// </summary>
    public static Sprite LoadIcon(string iconKey, out Material materialToUse)
    {
        materialToUse = null;
        string theme = "neutral"; // Default theme

        // Determine theme
        if (IconThemeMap.TryGetValue(iconKey, out var mappedTheme))
        {
            theme = mappedTheme;
        }

        // Get material based on theme
        materialToUse = theme switch
        {
            "warm" => _warmMaterial,
            "cold" => _coldMaterial,
            "void" => _voidMaterial,
            "neutral" => _neutralMaterial,
            _ => _warmMaterial
        };

        // Try to load from regenerated directory first, then the curated
        // "generated" batch (icon redesign follow-up), then the flat fallback.
        var sprite = TryLoadSprite($"Icons/regenerated/{iconKey}");
        if (sprite == null)
        {
            sprite = TryLoadSprite($"Icons/generated/{iconKey}");
        }
        if (sprite == null)
        {
            sprite = TryLoadSprite($"Icons/{iconKey}");
        }


        if (sprite == null)
        {
            Debug.LogWarning($"[HudIconIntegration] Icon not found: {iconKey}");
        }

        return sprite;
    }

    /// <summary>
    /// Apply icon to an Image component with appropriate glow effect.
    /// </summary>
    public static void ApplyIcon(Image imageComponent, string iconKey)
    {
        if (imageComponent == null)
            return;

        var sprite = LoadIcon(iconKey, out var material);
        if (sprite != null)
        {
            imageComponent.sprite = sprite;
        }

        if (material != null)
        {
            imageComponent.material = material;
        }
    }

    /// <summary>
    /// Batch apply icons to multiple Image components.
    /// Dictionary format: { "component_name": "icon_key" }
    /// </summary>
    public static void ApplyIconBatch(Dictionary<Image, string> iconAssignments)
    {
        foreach (var kvp in iconAssignments)
        {
            ApplyIcon(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Try to load a sprite from Resources.
    /// </summary>
    private static Sprite TryLoadSprite(string resourcePath)
    {
        try
        {
            return Resources.Load<Sprite>(resourcePath);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Update glow intensity for an image component.
    /// </summary>
    public static void SetGlowIntensity(Image imageComponent, float intensity)
    {
        if (imageComponent?.materialForRendering != null)
        {
            imageComponent.materialForRendering.SetFloat("_GlowIntensity", intensity);
        }
    }

    /// <summary>
    /// Update glow color for an image component.
    /// </summary>
    public static void SetGlowColor(Image imageComponent, Color glowColor)
    {
        if (imageComponent?.materialForRendering != null)
        {
            imageComponent.materialForRendering.SetColor("_GlowColor", glowColor);
        }
    }

    /// <summary>
    /// Get the theme for a given icon key.
    /// </summary>
    public static string GetIconTheme(string iconKey)
    {
        return IconThemeMap.TryGetValue(iconKey, out var theme) ? theme : "neutral";
    }
}
