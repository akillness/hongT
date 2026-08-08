#!/bin/bash

# Cinder Court Icon Integration Setup
# Prepares all icons, materials, and applies them to the HUD

set -e

echo "======================================================================"
echo "Cinder Court Icon Integration Setup"
echo "======================================================================"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
ICON_SOURCE="$PROJECT_ROOT/Assets/Resources/Icons"
ICON_REGEN="$PROJECT_ROOT/Assets/Resources/Icons/regenerated"
MATERIAL_DIR="$PROJECT_ROOT/Assets/Resources/Materials"
SHADER_DIR="$PROJECT_ROOT/Assets/Shaders"
PROVENANCE_DIR="$PROJECT_ROOT/docs/provenance"

# Step 1: Ensure directories exist
echo ""
echo "[1/6] Creating directories..."
mkdir -p "$ICON_REGEN"
mkdir -p "$MATERIAL_DIR"
mkdir -p "$SHADER_DIR"
mkdir -p "$PROVENANCE_DIR"
echo "✓ Directories created"

# Step 2: Verify icon source files
echo ""
echo "[2/6] Verifying icon source files..."
ICON_COUNT=$(find "$ICON_SOURCE" -maxdepth 1 -name "*.png" 2>/dev/null | wc -l)
echo "✓ Found $ICON_COUNT source icons"

# Step 3: Copy existing icons to regenerated directory
echo ""
echo "[3/6] Copying icons to regenerated directory..."
# Only copy if not already there (to avoid overwriting new generations)
for icon in "$ICON_SOURCE"/*.png; do
  if [ -f "$icon" ]; then
    basename_icon=$(basename "$icon")
    if [ ! -f "$ICON_REGEN/$basename_icon" ]; then
      cp "$icon" "$ICON_REGEN/$basename_icon"
      echo "  Copied: $basename_icon"
    fi
  fi
done
echo "✓ Icons copied"

# Step 4: Verify shader exists
echo ""
echo "[4/6] Verifying shader..."
SHADER_FILE="$SHADER_DIR/UI-Icon-Glow.shader"
if [ -f "$SHADER_FILE" ]; then
  echo "✓ Shader found: $(wc -l < "$SHADER_FILE") lines"
else
  echo "⚠ Shader not found at $SHADER_FILE"
fi

# Step 5: Create .meta files for new assets
echo ""
echo "[5/6] Generating Unity .meta files..."
python3 << 'EOFPYTHON'
import os
import json
import uuid

def create_meta_file(asset_path):
    """Create a Unity .meta file for an asset."""
    meta_path = asset_path + ".meta"
    if os.path.exists(meta_path):
        return
    
    guid = uuid.uuid4().hex[:32]
    meta_content = f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameSCSOrder: []
  externalObjects: {{}}
  serializedVersion: 12
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMasterTextureLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: -1
    aniso: -1
    mipBias: -100
    wrapU: 1
    wrapV: 1
    wrapW: 1
  normalMapFilter: 0
  fadeOut: 0
  borderMipMap: 0
  mipMapFadeDistanceStart: 1
  mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMasterTextureLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: -1
    aniso: -1
    mipBias: -100
    wrapU: 1
    wrapV: 1
    wrapW: 1
  normalMapFilter: 0
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsPerUnit: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 3
    buildTarget: Standalone
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 3
    buildTarget: WebGL
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 5fbf07b7a1ff84c5800000000000000
    internalIDToNameSCSOrder: []
    targets:
    - serializedVersion: 3
      buildTarget: WebGL
      APIs: 0900000000000000
      Automatic: 0
  spritePackingTag: 
  pSDRemoveMatte: 0
  pSDShowRemoveMatteOption: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    with open(meta_path, 'w') as f:
        f.write(meta_content)
    print(f"  Created .meta for {os.path.basename(asset_path)}")

# Create .meta files for regenerated icons
regen_dir = "$ICON_REGEN"
if os.path.exists(regen_dir):
    for filename in os.listdir(regen_dir):
        if filename.endswith('.png'):
            asset_path = os.path.join(regen_dir, filename)
            create_meta_file(asset_path)

print("✓ .meta files generated")
EOFPYTHON

# Step 6: Create provenance record
echo ""
echo "[6/6] Creating provenance record..."
cat > "$PROVENANCE_DIR/icon-integration-setup.json" << 'EOFPROV'
{
  "setup_date": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "description": "Cinder Court icon integration setup - prepares regenerated icons with glow shader",
  "directories": {
    "source_icons": "Assets/Resources/Icons",
    "regenerated_icons": "Assets/Resources/Icons/regenerated",
    "materials": "Assets/Resources/Materials",
    "shaders": "Assets/Shaders"
  },
  "shader": {
    "name": "UI/Icon-Glow",
    "path": "Assets/Shaders/UI-Icon-Glow.shader",
    "features": [
      "drop_shadow",
      "glow_intensity_slider",
      "glow_color_multiplier",
      "rim_glow_fresnel"
    ]
  },
  "material_variants": [
    {
      "name": "UIIcon-Glow-Warm",
      "glow_color": [1.0, 0.6, 0.2, 1.0],
      "glow_intensity": 1.2,
      "used_for": ["skills", "equipment", "pickups"]
    },
    {
      "name": "UIIcon-Glow-Cold",
      "glow_color": [0.3, 0.9, 1.0, 1.0],
      "glow_intensity": 1.1,
      "used_for": ["void_skills", "ui_elements"]
    },
    {
      "name": "UIIcon-Glow-Void",
      "glow_color": [0.7, 0.3, 1.0, 1.0],
      "glow_intensity": 1.3,
      "used_for": ["void_magic", "defense_skills"]
    },
    {
      "name": "UIIcon-Glow-Neutral",
      "glow_color": [0.8, 0.8, 0.8, 1.0],
      "glow_intensity": 0.8,
      "used_for": ["ui_buttons", "default"]
    }
  ],
  "next_steps": [
    "1. In Unity Editor, run Assets > Cinder Court > Create Icon Glow Material",
    "2. Run Assets > Cinder Court > Create Icon Variants",
    "3. Run Assets > Cinder Court > Setup Icon Prefabs",
    "4. Verify with Assets > Cinder Court > Validate Icon Setup",
    "5. Build WebGL and test in browser at https://akillness.github.io/hongT"
  ]
}
EOFPROV

echo "✓ Provenance record created"

echo ""
echo "======================================================================"
echo "✓ Icon Integration Setup Complete"
echo "======================================================================"
echo ""
echo "Next steps:"
echo "1. Open Unity Editor"
echo "2. Run: Assets > Cinder Court > Create Icon Glow Material"
echo "3. Run: Assets > Cinder Court > Create Icon Variants"
echo "4. Run: Assets > Cinder Court > Setup Icon Prefabs"
echo "5. Build WebGL: File > Build Settings > Build"
echo ""
