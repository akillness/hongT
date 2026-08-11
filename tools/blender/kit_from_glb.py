# AMENDMENT #17 dungeon kit: Higgsfield GLB -> Unity FBX (headless Blender).
#
# Usage:
#   blender -b --factory-startup --python-exit-code 1 \
#     -P tools/blender/kit_from_glb.py -- --glb <in.glb> --out <out.fbx>
#
# NORMALISATION IS THE POINT. A generated mesh arrives at whatever scale and
# orientation the vendor felt like, and the runtime has to scale it to a
# capsule whose length it only learns from the sim. So every part leaves here
# in one known pose:
#
#   * longest horizontal axis on +X, so a wall's LENGTH is its X
#   * centred on X and Y
#   * min Z at 0, so the part sits ON the floor instead of half through it
#   * longest horizontal extent scaled to exactly 1.0
#
# With that, the View's scale factor IS the size it wants in world units, and
# nothing downstream needs to know what the generator produced. The
# preview-camera bug this kit already survived (a correct 2.2:1 wall framed
# end-on, tools/blender/kit_preview.py) is the same class of mistake one level
# up: assume an orientation and you measure the wrong axis.
import argparse
import math
import sys

import bpy
from mathutils import Matrix, Vector


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--glb", required=True)
    parser.add_argument("--out", required=True)
    return parser.parse_args(argv)


def world_bounds(objects):
    """Bounds from VERTICES, never from obj.bound_box.

    bound_box is a cached corner set that only refreshes on a depsgraph
    update, so reading it straight after a mesh edit returns the PREVIOUS
    shape. That cost two wrong diagnoses here: a 90 degree rotation looked
    like it had silently failed, and so did the operator path before it, when
    in fact both had applied and only the measurement was stale. Vertices are
    the mesh, so they cannot be behind it.
    """
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for obj in objects:
        for vertex in obj.data.vertices:
            world = obj.matrix_world @ vertex.co
            lo = Vector((min(lo[i], world[i]) for i in range(3)))
            hi = Vector((max(hi[i], world[i]) for i in range(3)))
    return lo, hi


def main():
    args = parse_args()

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=args.glb)

    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        print("no mesh in glb", file=sys.stderr)
        return 1

    # Join into one object: the kit is placed per-part, and a multi-object part
    # would need its own parenting rules in the importer for no benefit.
    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    part = bpy.context.view_layer.objects.active

    # TRANSFORM THE MESH DATA DIRECTLY, never through bpy.ops. The operator
    # path was tried first and does not work here: transform_apply returns
    # {'FINISHED'} and leaves the world bounds identical (measured — a wall
    # stayed 0.1703 x 0.9731 through a 90 degree Z rotation it reported as
    # applied). Operators depend on selection, active object and parenting in
    # ways a headless run does not reliably reproduce. data.transform() has no
    # such context: it multiplies vertices and that is all it does.
    part.data.transform(part.matrix_world)
    part.matrix_world = Matrix.Identity(4)

    lo, hi = world_bounds([part])
    span = hi - lo

    # Longest HORIZONTAL axis onto +X. Height (Z) is never a candidate: a
    # column is taller than it is long and must not be laid on its side.
    if span.y > span.x:
        part.data.transform(Matrix.Rotation(math.radians(90.0), 4, "Z"))
        lo, hi = world_bounds([part])
        span = hi - lo

    longest = max(span.x, span.y)
    if longest < 1e-6:
        print("degenerate part", file=sys.stderr)
        return 1
    part.data.transform(Matrix.Scale(1.0 / longest, 4))

    lo, hi = world_bounds([part])
    centre = (lo + hi) * 0.5
    part.data.transform(Matrix.Translation((-centre.x, -centre.y, -lo.z)))

    lo, hi = world_bounds([part])
    print(f"normalised span x={hi.x-lo.x:.4f} y={hi.y-lo.y:.4f} z={hi.z-lo.z:.4f} "
          f"floor={lo.z:.4f}")

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=args.out,
        use_selection=True,
        apply_scale_options="FBX_SCALE_ALL",
        path_mode="COPY",
        embed_textures=False,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        bake_anim=False,
    )
    print(f"wrote {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
