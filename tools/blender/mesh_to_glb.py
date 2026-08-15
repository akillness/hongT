#!/usr/bin/env python3
"""Export a shipped character FBX as a RAW, unrigged GLB — the rigging source.

Why this exists: Higgsfield `3d_rigging` rigs whatever mesh it is given, and
the default vendor proxy body is NOT our character. Rigging on the proxy put
the knee at 35.0% of skeleton height where the shipped character has 28.9%,
and left the rest pose left/right asymmetric (feet at 4.8% / 9.6%). Humanoid
retarget cannot absorb that: the error rides every frame and the legs read as
crumpled. Re-rigging the SAME actions on this mesh brings the knee to
27.9/28.0% and restores symmetry.
[MEASURED 2026-08-10 — _workspace/current/engineering/mesh-gen/rig-diagnosis.json]

The armature and its modifiers are stripped on purpose: the service auto-rigs
a bare mesh, and leaving a skeleton in makes it rig a rigged thing.

The CLI cannot upload this — `higgsfield upload create` rejects `.glb`
("Cannot detect media type from extension"). Host the file at a URL the
service can fetch and pass that to `--model_url`; gh-pages works and the file
should be removed again once the jobs are submitted (a deploy would delete it
anyway, and the deploy script cannot stage that deletion).

Usage:
    blender -b --factory-startup --python-exit-code 1 -P mesh_to_glb.py -- \
        --fbx Assets/Art/Characters/human-command-boss.fbx \
        --out _workspace/current/engineering/mesh-gen/player-mesh.glb

Then:
    higgsfield generate create 3d_rigging \
        --model_url https://<host>/player-mesh.glb \
        --enable_animation true --animation_action_id <id> \
        --height_meters 1.76 --wait
"""
import sys
from pathlib import Path

import bpy

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name):
    if name not in ARGS:
        raise SystemExit(f"FATAL: missing {name}")
    return ARGS[ARGS.index(name) + 1]


SRC = Path(arg("--fbx")).resolve()
DST = Path(arg("--out")).resolve()

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(SRC))

for obj in list(bpy.data.objects):
    if obj.type == "ARMATURE":
        bpy.data.objects.remove(obj, do_unlink=True)

meshes = [o for o in bpy.data.objects if o.type == "MESH"]
if not meshes:
    raise SystemExit(f"FATAL: no mesh in {SRC}")
for mesh in meshes:
    for modifier in list(mesh.modifiers):
        mesh.modifiers.remove(modifier)
    mesh.parent = None

DST.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
for mesh in meshes:
    mesh.select_set(True)
bpy.ops.export_scene.gltf(
    filepath=str(DST), export_format="GLB",
    use_selection=True, export_animations=False)

print(f"[mesh_to_glb] {DST.name}: {len(meshes)} mesh(es), "
      f"{DST.stat().st_size} bytes")
