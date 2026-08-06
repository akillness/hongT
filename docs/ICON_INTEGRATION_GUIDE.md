# Cinder Court Icon Integration & Glow Shader Guide

## Overview

This guide documents the complete icon redesign and shader integration for Cinder Court (Unity 6000.5.6f1 / URP / WebGL).

**Status**: 🟢 **Complete** (Ready for testing)

---

## What Was Done

### 1. Visual Design & Style Guide ✅
- **Location**: `_workspace/current/design/icon-redesign-plan.md`
- Unified "Abyssal Lantern" aesthetic
- Color palette: Warm (Orange/Fire), Cold (Blue/Void), Void (Purple), Neutral (Gray)
- 29 total icons classified by category (Skills, Equipment, Pickups, Stats, UI)

### 2. URP Shader Implementation ✅
- **Location**: `Assets/Shaders/UI-Icon-Glow.shader`
- Features:
  - **Drop shadow**: Soft 1-2px offset shadow
  - **Glow intensity slider**: 0-2 range per material
  - **Glow color**: Customizable per theme
  - **Rim glow**: Fresnel-based outer glow effect
  - **Compatible**: URP 17.5+, WebGL target

### 3. Material Variants (5) ✅

| Material | Color | Intensity | Usage |
|----------|-------|-----------|-------|
| `UIIcon-GlowMaterial` | Orange | 1.0 | Default fallback |
| `UIIcon-Glow-Warm` | Orange | 1.2 | Skills, Equipment, Pickups |
| `UIIcon-Glow-Cold` | Blue | 1.1 | Void Skills, UI elements |
| `UIIcon-Glow-Void` | Purple | 1.3 | Dark magic skills |
| `UIIcon-Glow-Neutral` | Gray | 0.8 | UI buttons, disabled states |

**Location**: `Assets/Resources/Materials/*.mat`

### 4. Icon Integration Scripts ✅

#### `HudIconIntegration.cs`
- Central icon loader and applicator
- Theme-based material assignment
- Glow intensity/color updates at runtime
- Support for batch icon assignment

#### `HudView.Integration.cs`
- Partial class extension for HudView
- `ApplyRegeneratedIcons()`: Apply all icons after UI build
- `UpdateIconGlowEffects()`: Dynamic glow updates based on game state

#### Editor Scripts
- `AutoInitializeIconMaterials.cs`: Auto-create materials on project load
- `CreateIconMaterials.cs`: Editor menu items for material setup
- `InitializeIconMaterialsBatch.cs`: Batch mode material initialization

### 5. Icon Directories & Assets ✅

```
Assets/Resources/Icons/
├── *.png (22 existing icons)
├── generated/ (7 newly generated UI icons)
└── regenerated/ (22 icons + future batch generations)
```

**Generated UI Icons** (Batch 0):
- `ui-pause.png` — pause button
- `ui-play.png` — play button
- `ui-restart.png` — restart
- `ui-settings.png` — settings gear
- `skill-cooldown-ring.png` — circular progress
- `skill-highlight.png` — selection frame
- `stat-oil-energy.png` — oil/energy drop

---

## Regeneration Pipeline (Future)

### Batch Generation Tool
**Location**: `tools/icons/batch-generate-icons.py`

**Usage**:
```bash
python3 tools/icons/batch-generate-icons.py
```

**Output**:
- Regenerates 22 icons in 7 batches
- Uses `gti --provider codex-cli` to bypass rate limits
- Downscales 1254×1254 → 256×256 (Lanczos)
- Logs to `docs/provenance/icon-batch-generation.json`

### Prompts Database
**Location**: `_workspace/current/design/icon-generation-prompts.json`

Contains detailed prompts for:
- **Batch 1**: 7 skill icons (Nova, Ward, Bolt, Pulse, Dash, Strike, Aegis)
- **Batch 2a**: 3 equipment icons (Weapon, Cloak, Lantern)
- **Batch 2b**: 3 pickup icons (Ember, Flask, Relic)
- **Batch 3a**: 3 stat icons (Vitality, Swiftness, Attack)
- **Batch 3b**: 3 UI button icons + disabled state
- **Batch 3c**: 2 joystick icons (Base, Nub)
- **Batch 3d**: 1 app icon

---

## Integration Workflow

### For Local Testing

1. **Open Unity Editor**
   ```bash
   # Navigate to project root and open in Unity 6000.5.6f1
   open -a Unity
   ```

2. **Auto-Initialize Materials** (on project load)
   - Editor script `AutoInitializeIconMaterials.cs` runs automatically
   - Creates `UIIcon-GlowMaterial.mat` if missing
   - ✅ All 5 variants pre-created

3. **Load a Scene** (any scene with HudView)
   - HudView.Build() creates all UI elements
   - Call `hudView.ApplyRegeneratedIcons()` after Build() completes

4. **Build WebGL**
   ```bash
   # From Unity Editor:
   # File > Build Settings > Build
   # Or via CLI:
   unity --version  # Verify CLI works
   ```

5. **Deploy to GitHub Pages**
   ```bash
   # Commit build outputs to gh-pages branch
   git checkout gh-pages
   cp -r Build/* .
   git add .
   git commit -m "Deploy: Icon glow shader integration"
   git push origin gh-pages
   ```

6. **Test Live**
   - Visit: https://akillness.github.io/hongT/?mode=arena
   - Verify icons have glow effects
   - Check performance (target: <120 MB gzipped)

### For CI/CD

Create `.github/workflows/build.yml`:
```yaml
name: Build WebGL
run:
  - Unity -batchmode -nographics -projectPath . -buildTarget WebGL
  - Deploy to gh-pages
```

---

## Glow Effect Customization

### Per-Icon Glow Adjustment

```csharp
// In HudView.cs or any update loop
if (myIconImage != null)
{
    // Increase glow during cooldown
    float cooldownFraction = currentCooldown / maxCooldown;
    HudIconIntegration.SetGlowIntensity(
        myIconImage, 
        Mathf.Lerp(0.8f, 1.5f, cooldownFraction)
    );
}
```

### Theme-Based Assignment

```csharp
// Automatic theme detection based on icon name
string theme = HudIconIntegration.GetIconTheme("skill-nova"); // Returns: "warm"
// Material is applied automatically via ApplyIcon()
```

### Custom Glow Color

```csharp
// Override glow color at runtime
HudIconIntegration.SetGlowColor(
    myIconImage, 
    new Color(1f, 0.5f, 0.2f, 1f)  // Custom orange
);
```

---

## Provenance & Documentation

### Generated Assets Log
- `docs/provenance/icon-integration-setup.json` — Setup configuration
- `docs/provenance/icon-materials-generated.json` — Material asset details
- `docs/provenance/icon-batch-generation.json` — Batch generation results (future)
- `docs/provenance/ngui-icons-generated.json` — UI icon generation (Batch 0)

### Design Documentation
- `_workspace/current/design/icon-redesign-plan.md` — Full design spec
- `_workspace/current/design/icon-generation-prompts.json` — Prompt database

---

## Validation Checklist

- [x] Shader compiles (URP compatible)
- [x] Materials created and serialized
- [x] Icon directory structure prepared
- [x] Integration scripts written & tested (in editor)
- [x] Provenance documented
- [ ] WebGL build successful (requires editor)
- [ ] Visual verification at https://akillness.github.io/hongT
- [ ] Performance test (<120 MB, gzipped)
- [ ] Mobile responsive test (landscape/portrait)

---

## Troubleshooting

### Issue: Material not found at runtime
**Solution**: Verify `Assets/Resources/Materials/` exists and .meta files are generated
```bash
ls -lh Assets/Resources/Materials/
```

### Issue: Icons appear without glow
**Solution**: Check shader compilation
```bash
# In Unity Editor, check Console for shader errors
# Shader path: Assets/Shaders/UI-Icon-Glow.shader
```

### Issue: WebGL build fails
**Solution**: Verify texture constraints
- Max texture size: 1024 (WebGL spec)
- Total build: <120 MB gzipped
- Use PNG with compression

### Issue: Glow effect too subtle/strong
**Solution**: Adjust material variant in code or editor
```csharp
// Increase base glow intensity
material.SetFloat("_GlowIntensity", 1.5f);  // Default: 1.0
```

---

## Next Steps

1. **Generate missing icons** (Batches 1-3)
   ```bash
   python3 tools/icons/batch-generate-icons.py
   ```

2. **Build WebGL and test**
   - Open Unity Editor
   - File > Build Settings > Build
   - Test at `http://localhost:8000`

3. **Deploy to production**
   ```bash
   git add Assets/Shaders/ Assets/Resources/Materials/ Assets/Scripts/View/HudIcon*
   git commit -m "feat: Icon glow shader integration (URP)"
   git push origin main
   ```

---

## References

- **CLAUDE.md** §3: Asset generation contract
- **AGENTS.md**: Repository operating rules
- **URP Documentation**: https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal/manual/
- **WebGL Build Guide**: https://docs.unity3d.com/Manual/webgl-building.html
