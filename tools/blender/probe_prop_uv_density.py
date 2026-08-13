# Texel-density probe for the equip props (headless Blender).
#
# WHY THIS EXISTS. "Add smart_project" is exactly the kind of change that can
# land, run clean, and do nothing — the operator needs an EDIT-mode mesh with a
# selection, and a wrong context silently leaves the primitive 0..1-per-part UVs
# in place. Re-exporting six identical FBXs and finding out at visual QA is the
# expensive path, so measure the property we actually claim.
#
# WHAT UNIFORM DENSITY MEANS HERE. For each triangle, density = uv_area /
# world_area. A primitive's default unwrap maps every part to the full 0..1
# square, so a long blade and a thin guard get the SAME uv area over very
# different world areas — the ratio spread explodes. A smart projection holds
# the ratio roughly constant. The spread (p95/p05) is therefore the direct
# measurement of "will one tiling sheet smear", not a proxy for it.
#
#   blender -b --factory-startup --python-exit-code 1 \
#     -P tools/blender/probe_prop_uv_density.py -- --dir Assets/Art/Props
import argparse
import sys

import bpy


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--dir", required=True)
    # Spread above this reads as "one sheet cannot cover this mesh".
    parser.add_argument("--max-spread", type=float, default=6.0)
    return parser.parse_args(argv)


def tri_area_2d(a, b, c):
    return abs((b[0] - a[0]) * (c[1] - a[1]) - (c[0] - a[0]) * (b[1] - a[1])) * 0.5


def densities(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    if not mesh.uv_layers:
        return []
    uv = mesh.uv_layers[0].data
    out = []
    for tri in mesh.loop_triangles:
        world = tri.area
        if world <= 1e-9:
            continue
        a, b, c = (uv[i].uv for i in tri.loops)
        area = tri_area_2d(a, b, c)
        if area <= 1e-12:
            continue
        out.append(area / world)
    return sorted(out)


def main():
    args = parse_args()
    import glob
    import os

    failures = []
    for path in sorted(glob.glob(f"{args.dir}/equip-*.fbx")):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=path)
        name = os.path.basename(path)
        for obj in [o for o in bpy.data.objects if o.type == "MESH"]:
            values = densities(obj)
            if not values:
                print(f"{name:38s} NO-UV")
                failures.append(f"{name}: no UV layer")
                continue
            lo = values[max(0, int(len(values) * 0.05))]
            hi = values[min(len(values) - 1, int(len(values) * 0.95))]
            spread = hi / lo if lo > 0 else float("inf")
            verdict = "ok" if spread <= args.max_spread else "SMEAR"
            print(f"{name:38s} tris={len(values):4d} spread={spread:7.2f}x {verdict}")
            if verdict == "SMEAR":
                failures.append(f"{name}: {spread:.2f}x")
    if failures:
        print("OVER-SPREAD: " + "; ".join(failures))
    print(f"done ({len(failures)} over spread)")


if __name__ == "__main__":
    main()
