#!/usr/bin/env python3
"""
Batch Icon Generator using god-tibo-imagen (gti)
Generates all UI, skill, equipment, pickup, and stat icons for Cinder Court.
"""

import json
import subprocess
import os
import sys
from pathlib import Path
from datetime import datetime

# Configuration
DESIGN_CONFIG_PATH = "_workspace/current/design/icon-generation-prompts.json"
OUTPUT_DIR = "Assets/Resources/Icons/regenerated"
PROVENANCE_DIR = "docs/provenance"
RESULTS_LOG = "_workspace/current/engineering/icon-generation-results.json"

def ensure_output_dirs():
    """Create output directories if they don't exist."""
    Path(OUTPUT_DIR).mkdir(parents=True, exist_ok=True)
    Path(PROVENANCE_DIR).mkdir(parents=True, exist_ok=True)
    Path("_workspace/current/engineering").mkdir(parents=True, exist_ok=True)

def load_prompts():
    """Load icon generation prompts from design config."""
    with open(DESIGN_CONFIG_PATH, 'r') as f:
        return json.load(f)

def generate_batch(batch_name, icons, config, results):
    """
    Generate a batch of icons using gti (god-tibo-imagen).
    """
    print(f"\n{'='*60}")
    print(f"Batch: {batch_name}")
    print(f"Icons: {len(icons)}")
    print(f"{'='*60}")
    
    batch_results = {
        "batch_name": batch_name,
        "generated_at": datetime.now().isoformat(),
        "icons": []
    }
    
    for icon in icons:
        filename = icon["filename"]
        prompt = icon["prompt"]
        name = icon["name"]
        output_path = os.path.join(OUTPUT_DIR, filename)
        
        print(f"\nGenerating: {name}")
        print(f"  Filename: {filename}")
        print(f"  Prompt: {prompt[:80]}...")
        
        try:
            # Call gti (god-tibo-imagen) with codex-cli provider
            cmd = [
                "gti",
                "--prompt", prompt,
                "--output", output_path,
                "--provider", config["provider"],
                "--size", config["size"],
                "--format", config["format"]
            ]
            
            print(f"  Command: {' '.join(cmd)}")
            result = subprocess.run(cmd, capture_output=True, text=True, timeout=60)
            
            if result.returncode == 0:
                # Post-process: downscale to 256x256
                downscale_icon(output_path, config["final_size"])
                
                icon_result = {
                    "filename": filename,
                    "name": name,
                    "prompt": prompt,
                    "status": "success",
                    "output_path": output_path,
                    "generated_at": datetime.now().isoformat()
                }
                print(f"  ✓ Generated successfully")
            else:
                icon_result = {
                    "filename": filename,
                    "name": name,
                    "prompt": prompt,
                    "status": "failed",
                    "error": result.stderr,
                    "generated_at": datetime.now().isoformat()
                }
                print(f"  ✗ Generation failed: {result.stderr}")
        
        except Exception as e:
            icon_result = {
                "filename": filename,
                "name": name,
                "prompt": prompt,
                "status": "error",
                "error": str(e),
                "generated_at": datetime.now().isoformat()
            }
            print(f"  ✗ Error: {e}")
        
        batch_results["icons"].append(icon_result)
    
    return batch_results

def downscale_icon(input_path, target_size):
    """
    Downscale icon from 1254x1254 to 256x256 using PIL.
    """
    try:
        from PIL import Image
        
        if not os.path.exists(input_path):
            print(f"  Warning: File not found {input_path}")
            return
        
        img = Image.open(input_path)
        size_tuple = tuple(map(int, target_size.split('x')))
        img_resized = img.resize(size_tuple, Image.Resampling.LANCZOS)
        img_resized.save(input_path, 'PNG', optimize=True)
        
        print(f"  Downscaled to {target_size}")
    except ImportError:
        print(f"  Warning: PIL not available, skipping downscale")
    except Exception as e:
        print(f"  Error downscaling: {e}")

def save_provenance(results, prompts_config):
    """
    Save generation results to provenance file.
    """
    provenance = {
        "tool": "god-tibo-imagen (gti)",
        "provider": prompts_config["generation_config"]["provider"],
        "generation_date": datetime.now().isoformat(),
        "output_directory": OUTPUT_DIR,
        "batches": results,
        "config": prompts_config["generation_config"]
    }
    
    provenance_path = os.path.join(PROVENANCE_DIR, "icon-batch-generation.json")
    with open(provenance_path, 'w') as f:
        json.dump(provenance, f, indent=2)
    
    print(f"\nProvenance saved to: {provenance_path}")
    return provenance_path

def main():
    print("\n" + "="*60)
    print("Cinder Court Icon Batch Generation Pipeline")
    print("="*60)
    
    # Ensure directories exist
    ensure_output_dirs()
    
    # Load prompts and config
    prompts = load_prompts()
    config = prompts["generation_config"]
    
    all_results = []
    
    # Batch 1: Skills (7 icons)
    if "batch_1_skills" in prompts:
        batch1 = generate_batch(
            "Batch 1: Skill Icons",
            prompts["batch_1_skills"],
            config,
            all_results
        )
        all_results.append(batch1)
    
    # Batch 2: Equipment (3 icons)
    if "batch_2_equipment" in prompts:
        batch2_equip = generate_batch(
            "Batch 2a: Equipment Icons",
            prompts["batch_2_equipment"],
            config,
            all_results
        )
        all_results.append(batch2_equip)
    
    # Batch 2: Pickups (3 icons)
    if "batch_2_pickups" in prompts:
        batch2_pickup = generate_batch(
            "Batch 2b: Pickup Icons",
            prompts["batch_2_pickups"],
            config,
            all_results
        )
        all_results.append(batch2_pickup)
    
    # Batch 3: Stats (3 icons)
    if "batch_3_stats" in prompts:
        batch3_stats = generate_batch(
            "Batch 3a: Stat Icons",
            prompts["batch_3_stats"],
            config,
            all_results
        )
        all_results.append(batch3_stats)
    
    # Batch 3: UI Buttons (3 icons)
    if "batch_3_ui_buttons" in prompts:
        batch3_ui = generate_batch(
            "Batch 3b: UI Button Icons",
            prompts["batch_3_ui_buttons"],
            config,
            all_results
        )
        all_results.append(batch3_ui)
    
    # Batch 3: Joystick (2 icons)
    if "batch_3_joystick" in prompts:
        batch3_joy = generate_batch(
            "Batch 3c: Joystick Icons",
            prompts["batch_3_joystick"],
            config,
            all_results
        )
        all_results.append(batch3_joy)
    
    # Batch 3: App Icon (1 icon)
    if "batch_3_app" in prompts:
        batch3_app = generate_batch(
            "Batch 3d: App Icon",
            prompts["batch_3_app"],
            config,
            all_results
        )
        all_results.append(batch3_app)
    
    # Save provenance
    save_provenance(all_results, prompts)
    
    # Print summary
    total_icons = sum(len(b["icons"]) for b in all_results)
    successful = sum(
        1 for b in all_results 
        for icon in b["icons"] 
        if icon["status"] == "success"
    )
    
    print(f"\n{'='*60}")
    print(f"Summary:")
    print(f"  Total batches: {len(all_results)}")
    print(f"  Total icons: {total_icons}")
    print(f"  Successful: {successful}")
    print(f"  Failed: {total_icons - successful}")
    print(f"{'='*60}\n")

if __name__ == "__main__":
    main()
