// FROZEN CONTRACT: HudView.Integration.cs
// Icon integration extension for HudView
// Injected into Build() after UI elements are created

using UnityEngine;
using System.Collections.Generic;

namespace CinderCourt.View
{
    /// <summary>
    /// Partial extension for HudView to integrate regenerated icons with glow effects.
    /// This file should be included alongside HudView.cs
    /// </summary>
    public partial class HudView
    {
        /// <summary>
        /// Apply all regenerated icons to HUD elements after Build() completes.
        /// Call this from Update() or a coroutine after Build() finishes.
        /// </summary>
        public void ApplyRegeneratedIcons()
        {
            // Initialize icon system
            HudIconIntegration.InitializeMaterials();

            // Apply skill cooldown ring and highlight overlays
            if (_novaCooldownOverlay != null)
                HudIconIntegration.ApplyIcon(_novaCooldownOverlay, "skill-cooldown-ring");
            if (_wardCooldownOverlay != null)
                HudIconIntegration.ApplyIcon(_wardCooldownOverlay, "skill-cooldown-ring");

            // Apply skill overlays (bolt, pulse, nova, ward)
            if (_skillOverlays != null && _skillOverlays.Length >= 4)
            {
                HudIconIntegration.ApplyIcon(_skillOverlays[0], "skill-bolt");      // Bolt
                HudIconIntegration.ApplyIcon(_skillOverlays[1], "skill-pulse");     // Pulse
                HudIconIntegration.ApplyIcon(_skillOverlays[2], "skill-nova");      // Nova
                HudIconIntegration.ApplyIcon(_skillOverlays[3], "skill-ward");      // Ward
            }

            // Apply dash overlay
            if (_dashOverlay != null)
                HudIconIntegration.ApplyIcon(_dashOverlay, "skill-dash");

            // Apply stat icons if they exist
            if (_healthFill != null)
                HudIconIntegration.SetGlowIntensity(_healthFill, 1.2f); // Warm health
            if (_chargeFill != null)
                HudIconIntegration.SetGlowIntensity(_chargeFill, 1.2f); // Warm oil

            // Apply equipment icons from prep panel if available
            if (_equipPanel != null)
            {
                var equipImages = _equipPanel.GetComponentsInChildren<Image>();
                foreach (var img in equipImages)
                {
                    if (img != null && img.sprite != null)
                    {
                        string spriteName = img.sprite.name;
                        if (spriteName.Contains("equip") || spriteName.Contains("weapon") || spriteName.Contains("cloak"))
                        {
                            HudIconIntegration.ApplyIcon(img, spriteName);
                        }
                    }
                }
            }

            // Apply UI button icons
            var allButtons = GetComponentsInChildren<Button>();
            foreach (var btn in allButtons)
            {
                var img = btn.GetComponent<Image>();
                if (img != null && img.sprite != null)
                {
                    string spriteName = img.sprite.name;
                    if (spriteName.Contains("ui-") && spriteName.Contains("button"))
                    {
                        HudIconIntegration.ApplyIcon(img, spriteName);
                    }
                }
            }

            Debug.Log("[HudView.ApplyRegeneratedIcons] ✓ All icon integrations applied");
        }

        /// <summary>
        /// Update glow effects dynamically based on game state.
        /// Call from Update() loop.
        /// </summary>
        public void UpdateIconGlowEffects(float deltaTime)
        {
            // Increase glow intensity when Nova is active (charging)
            if (_novaCooldownOverlay != null)
            {
                // Map cooldown fill amount to glow intensity (0-2 range)
                float fillAmount = _novaCooldownOverlay.fillAmount;
                float glowIntensity = Mathf.Lerp(0.8f, 1.5f, fillAmount);
                HudIconIntegration.SetGlowIntensity(_novaCooldownOverlay, glowIntensity);
            }

            // Similar for Ward cooldown
            if (_wardCooldownOverlay != null)
            {
                float fillAmount = _wardCooldownOverlay.fillAmount;
                float glowIntensity = Mathf.Lerp(0.8f, 1.5f, fillAmount);
                HudIconIntegration.SetGlowIntensity(_wardCooldownOverlay, glowIntensity);
            }
        }
    }
}
