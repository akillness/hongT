# Re-unwrap an ALREADY-EXPORTED prop FBX in place (headless Blender).
#
# WHY THIS EXISTS RATHER THAN A FIX IN THE GENERATOR. convert_equip_props.py
# authored equip-weapon-* / equip-lantern-* / equip-cloak-* from blade.glb and
# relic.glb — retained Abyssal-Surge sources that are NOT in this repo — so the
# obvious move (add smart_project there, re-run) is unrunnable. That reads like
# "these props cannot be fixed", and it is wrong for the same reason CLAUDE.md
# §4z records: the blocker was a property of the TOOL, not of the target. The
# UVs live in the FBX, and the FBX is right here. A re-unwrap pass needs no
# source GLB at all.
#
# WHAT IT TOUCHES. UV layer only. Geometry, transform and materials are
# imported and re-exported with convert_equip_props.py's own export flags
# (apply_scale_options=FBX_SCALE_ALL, bake_space_transform), so the socket-space
# contract in ActorView.AttachEquipProps is preserved byte-for-byte in meaning:
# grip/pivot at origin, striking end along +Y.
#
# Pair with probe_prop_uv_density.py — run the probe first to pick targets, this
# to fix them, then the probe again as the verdict.
#
#   blender -b --factory-startup --python-exit-code 1 \
#     -P tools/blender/unwrap_existing_props.py -- \
#     --fbx Assets/Art/Props/equip-lantern-basic.fbx ...
import argparse
import sys

import bpy


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--fbx", nargs="+", required=True)
    return parser.parse_args(argv)


def unwrap_uniform(obj):
    """Smart-project a uniform-texel-density layout (mirrors gen_weapon_props)."""
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")


def main():
    args = parse_args()
    for path in args.fbx:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=path)
        meshes = [o for o in bpy.data.objects if o.type == "MESH"]
        if len(meshes) != 1:
            raise SystemExit(f"FATAL: expected 1 mesh in {path}, found {len(meshes)}")
        mesh = meshes[0]
        unwrap_uniform(mesh)
        bpy.ops.object.select_all(action="DESELECT")
        mesh.select_set(True)
        bpy.context.view_layer.objects.active = mesh
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL",
            bake_space_transform=True, add_leaf_bones=False)
        print(f"UNWRAPPED {path}")
    print(f"done ({len(args.fbx)} props)")


if __name__ == "__main__":
    main()
