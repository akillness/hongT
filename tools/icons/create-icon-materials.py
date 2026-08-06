#!/usr/bin/env python3
"""
Direct material file creation for Cinder Court icon glow effects.
Creates serialized Unity material assets without requiring the editor.
"""

import json
import os
from pathlib import Path
from datetime import datetime

# Unity YAML material template
MATERIAL_TEMPLATE = """%%YAML 1.1
%TAG ! tag:yaml.org,2002:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: {shader_guid}, type: 3}}
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_LockedProperties: ""
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _MainTex:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats:
    - _GlowIntensity: {glow_intensity}
    - _OutlineWidth: 0.02
    - _ShadowOffset: 1
    m_Colors:
    - _Color: {{r: 1, g: 1, b: 1, a: 1}}
    - _GlowColor: {{r: {glow_r}, g: {glow_g}, b: {glow_b}, a: 1}}
    - _ShadowColor: {{r: 0, g: 0, b: 0, a: 0.3}}
  m_BuildTextureStacks: []
"""

# Material variants configuration
VARIANTS = [
    {
        "name": "UIIcon-GlowMaterial",
        "glow_color": (1.0, 0.6, 0.2),  # Orange
        "glow_intensity": 1.0,
        "description": "Default glow material"
    },
    {
        "name": "UIIcon-Glow-Warm",
        "glow_color": (1.0, 0.6, 0.2),  # Orange
        "glow_intensity": 1.2,
        "description": "Warm theme for skills, equipment, pickups"
    },
    {
        "name": "UIIcon-Glow-Cold",
        "glow_color": (0.3, 0.9, 1.0),  # Blue
        "glow_intensity": 1.1,
        "description": "Cold theme for void skills and UI"
    },
    {
        "name": "UIIcon-Glow-Void",
        "glow_color": (0.7, 0.3, 1.0),  # Purple
        "glow_intensity": 1.3,
        "description": "Void theme for dark magic skills"
    },
    {
        "name": "UIIcon-Glow-Neutral",
        "glow_color": (0.8, 0.8, 0.8),  # Gray
        "glow_intensity": 0.8,
        "description": "Neutral theme for UI buttons"
    }
]

# Shader GUID (this is a placeholder; ideally this would be read from the shader's .meta file)
SHADER_GUID = "abc123def456abc123def456abc123de"  # This will be updated if .meta is found

def find_shader_guid():
    """Find the actual GUID of the UI-Icon-Glow shader."""
    shader_meta_path = "Assets/Shaders/UI-Icon-Glow.shader.meta"
    if os.path.exists(shader_meta_path):
        try:
            with open(shader_meta_path, 'r') as f:
                content = f.read()
                # Extract GUID from meta file
                if "guid:" in content:
                    guid_line = [line for line in content.split('\n') if 'guid:' in line][0]
                    guid = guid_line.split("guid:")[1].strip()
                    return guid
        except:
            pass
    return SHADER_GUID

def create_material_file(material_config, shader_guid, output_dir):
    """Create a Unity material .yaml file."""
    name = material_config["name"]
    glow_r, glow_g, glow_b = material_config["glow_color"]
    glow_intensity = material_config["glow_intensity"]
    
    content = MATERIAL_TEMPLATE.format(
        name=name,
        shader_guid=shader_guid,
        glow_r=glow_r,
        glow_g=glow_g,
        glow_b=glow_b,
        glow_intensity=glow_intensity
    )
    
    output_path = os.path.join(output_dir, f"{name}.mat")
    with open(output_path, 'w') as f:
        f.write(content)
    
    return output_path

def create_material_meta(mat_file_path):
    """Create a .meta file for a material asset."""
    import uuid
    guid = uuid.uuid4().hex[:32]
    
    meta_content = f"""fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 2100000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    
    meta_path = mat_file_path + ".meta"
    with open(meta_path, 'w') as f:
        f.write(meta_content)
    
    return meta_path

def main():
    print("\n" + "="*70)
    print("Cinder Court Icon Glow Material Generator")
    print("="*70)
    
    # Setup directories
    material_dir = "Assets/Resources/Materials"
    Path(material_dir).mkdir(parents=True, exist_ok=True)
    
    # Find shader GUID
    shader_guid = find_shader_guid()
    print(f"\nShader GUID: {shader_guid}")
    
    # Create materials
    print(f"\nCreating materials in {material_dir}...")
    created_materials = []
    
    for variant in VARIANTS:
        try:
            mat_path = create_material_file(variant, shader_guid, material_dir)
            meta_path = create_material_meta(mat_path)
            
            created_materials.append({
                "name": variant["name"],
                "description": variant["description"],
                "glow_color": variant["glow_color"],
                "glow_intensity": variant["glow_intensity"],
                "file": mat_path,
                "meta": meta_path
            })
            
            print(f"  ✓ {variant['name']}: {variant['description']}")
        except Exception as e:
            print(f"  ✗ {variant['name']}: {e}")
    
    # Create provenance
    provenance = {
        "tool": "Direct Material Generator (Python)",
        "generated_at": datetime.now().isoformat(),
        "shader": {
            "name": "UI/Icon-Glow",
            "path": "Assets/Shaders/UI-Icon-Glow.shader",
            "guid": shader_guid
        },
        "materials_created": created_materials,
        "total": len(created_materials),
        "output_directory": material_dir
    }
    
    prov_path = "docs/provenance/icon-materials-generated.json"
    Path("docs/provenance").mkdir(parents=True, exist_ok=True)
    
    with open(prov_path, 'w') as f:
        json.dump(provenance, f, indent=2)
    
    print(f"\nProvenance saved to: {prov_path}")
    
    print(f"\n{'='*70}")
    print(f"✓ Generated {len(created_materials)} material assets")
    print(f"{'='*70}\n")
    
    return 0

if __name__ == "__main__":
    exit(main())
