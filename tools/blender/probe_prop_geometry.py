# Rigid-geometry probe for prop FBXs (headless Blender).
#
# WHY THIS EXISTS. unwrap_existing_props.py round-trips a prop through
# import_scene.fbx -> export_scene.fbx(bake_space_transform=True). That is NOT
# idempotent on a file convert_equip_props.py already baked: Blender's importer
# parks the Y-up/Z-up conversion on the object transform, and exporting with the
# bake flag folds it into the vertices a SECOND time. The classic outcomes are a
# 90 degree X rotation and/or a 100x scale.
#
# probe_prop_uv_density.py cannot see either failure, by construction: it reports
# p95/p05 of uv_area/world_area per triangle. A rigid rotation leaves every
# world_area untouched, and a uniform scale multiplies them all by the same s^2,
# so the ratio is identical. "all twelve between 1.08 and 2.00" reads exactly the
# same on a prop lying on its side at 100x size. Hence this second probe: it
# measures the quantities the UV probe is blind to.
#
# Prints dimensions and world bbox per axis. Compare a re-exported file against
# the same path extracted from git HEAD; height and up-axis must be unchanged.
#
#   blender -b --factory-startup --python-exit-code 1 \
#     -P tools/blender/probe_prop_geometry.py -- --fbx a.fbx b.fbx
import argparse
import os
import sys

import bpy


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--fbx", nargs="+", required=True)
    return parser.parse_args(argv)


def main():
    args = parse_args()
    for path in args.fbx:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=path)
        for obj in [o for o in bpy.data.objects if o.type == "MESH"]:
            corners = [obj.matrix_world @ v.co for v in obj.data.vertices]
            lo = [min(c[i] for c in corners) for i in range(3)]
            hi = [max(c[i] for c in corners) for i in range(3)]
            dim = [hi[i] - lo[i] for i in range(3)]
            longest = "XYZ"[dim.index(max(dim))]
            print(
                f"{os.path.basename(path):32s} "
                f"dim=({dim[0]:.4f},{dim[1]:.4f},{dim[2]:.4f}) "
                f"lo=({lo[0]:.4f},{lo[1]:.4f},{lo[2]:.4f}) "
                f"hi=({hi[0]:.4f},{hi[1]:.4f},{hi[2]:.4f}) "
                f"long={longest} tris={len(obj.data.loop_triangles) or len(obj.data.polygons)}"
            )
    print("done")


if __name__ == "__main__":
    main()
