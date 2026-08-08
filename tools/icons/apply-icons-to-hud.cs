using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Runtime icon applicator for Cinder Court HUD.
/// Loads regenerated icons from Assets/Resources/Icons/regenerated/
/// and applies glow material + shader effects.
/// 
/// Usage: Call ApplyAllIcons() from HudView.Build() after shader material is loaded.
/// </summary>
public static class IconApplicator
{
    private static Material _glowMaterial;
    
    static IconApplicator()
    {
        // Load the glow material (created in editor or at runtime)
        _glowMaterial = Resources.Load<Material>("Materials/UIIcon-GlowMaterial");
        if (_glowMaterial == null)
        {
            Debug.LogWarning("UIIcon-GlowMaterial not found in Resources. Icons will use default material.");
        }
    }
    
    /// <summary>
    /// Icon resource mapping: internal name -> regenerated filename
    /// </summary>
    private static readonly Dictionary<string, string> IconMapping = new()
    {
        // Skill icons
        { "skill-nova", "skill-nova.png" },
        { "skill-ward", "skill-ward.png" },
        { "skill-bolt", "skill-bolt.png" },
        { "skill-pulse", "skill-pulse.png" },
        { "skill-dash", "skill-dash.png" },
        { "skill-strike", "skill-strike.png" },
        { "skill-aegis", "skill-aegis.png" },
        
        // Equipment icons
        { "equip-weapon", "equip-weapon.png" },
        { "equip-cloak", "equip-cloak.png" },
        { "equip-lantern", "equip-lantern.png" },
        
        // Pickup icons
        { "pickup-ember", "pickup-ember.png" },
        { "pickup-flask", "pickup-flask.png" },
        { "pickup-relic", "pickup-relic.png" },
        
        // Stat icons
        { "stat-vitality", "stat-vitality.png" },
        { "stat-swiftness", "stat-swiftness.png" },
        { "stat-attack", "stat-attack.png" },
        
        // UI icons
        { "ui-button", "ui-button.png" },
        { "ui-button-active", "ui-button-active.png" },
        { "ui-button-disabled", "ui-button-disabled.png" },
        { "ui-joystick-base", "ui-joystick-base.png" },
        { "ui-joystick-nub", "ui-joystick-nub.png" },
        
        // App icon
        { "app-lantern", "app-lantern.png" }
    };
    
    /// <summary>
    /// Load an icon from the regenerated directory and apply glow shader.
    /// </summary>
    public static Sprite LoadAndApplyIcon(string iconKey, Image imageComponent)
    {
        if (!IconMapping.TryGetValue(iconKey, out var filename))
        {
            Debug.LogWarning($"Icon key '{iconKey}' not found in mapping.");
            return null;
        }
        
        // Try regenerated first, then fall back to original
        Sprite sprite = TryLoadSprite($"Icons/regenerated/{Path.GetFileNameWithoutExtension(filename)}");
        if (sprite == null)
        {
            sprite = TryLoadSprite($"Icons/{Path.GetFileNameWithoutExtension(filename)}");
        }
        
        if (sprite != null && imageComponent != null)
        {
            imageComponent.sprite = sprite;
            
            // Apply glow material if available
            if (_glowMaterial != null)
            {
                imageComponent.material = _glowMaterial;
            }
        }
        else
        {
            Debug.LogWarning($"Failed to load sprite for icon key: {iconKey}");
        }
        
        return sprite;
    }
    
    /// <summary>
    /// Try to load a sprite from Resources directory.
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
    /// Apply glow material to an Image component.
    /// </summary>
    public static void ApplyGlowEffect(Image imageComponent, float glowIntensity = 1f, Color? glowColor = null)
    {
        if (imageComponent == null || _glowMaterial == null)
            return;
        
        imageComponent.material = _glowMaterial;
        
        // Set glow parameters
        if (imageComponent.materialForRendering != null)
        {
            imageComponent.materialForRendering.SetFloat(\"_GlowIntensity\", glowIntensity);
            
            if (glowColor.HasValue)
            {
                imageComponent.materialForRendering.SetColor(\"_GlowColor\", glowColor.Value);
            }
        }
    }
    
    /// <summary>
    /// Batch apply glow effect to multiple Image components.
    /// </summary>
    public static void ApplyGlowEffectBatch(Image[] imageComponents, float glowIntensity = 1f)
    {
        foreach (var img in imageComponents)
        {
            ApplyGlowEffect(img, glowIntensity);
        }
    }
}
