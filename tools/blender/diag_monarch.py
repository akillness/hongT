# Diagnostic: why does bone-heat fail for broken-court-monarch-boss?
# Dumps skeleton vs mesh bounds and per-bone rest geometry.
import bpy
import json
import sys
import mathutils

ARGS = sys.argv[sys.argv.index("--") + 1:]
GLB, MESH_GLB = ARGS[0], ARGS[1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")

def obj_bounds(obj):
    pts = [obj.matrix_world @ mathutils.Vector(c) for c in obj.bound_box]
    lo = [min(p[i] for p in pts) for i in range(3)]
    hi = [max(p[i] for p in pts) for i in range(3)]
    return lo, hi

print("ARM matrix_world:", [list(r) for r in arm.matrix_world])
lo, hi = obj_bounds(arm)
print("ARM bounds lo/hi:", [round(v,3) for v in lo], [round(v,3) for v in hi])
for b in arm.data.bones:
    hw = arm.matrix_world @ b.head_local
    tw = arm.matrix_world @ b.tail_local
    print(f"BONE {b.name:14s} head {[round(v,3) for v in hw]} tail {[round(v,3) for v in tw]} len {round((tw-hw).length,4)}")

before = set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=MESH_GLB)
new_meshes = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]
for m in new_meshes:
    lo, hi = obj_bounds(m)
    print("MESH", m.name, "bounds lo/hi:", [round(v,3) for v in lo], [round(v,3) for v in hi],
          "verts", len(m.data.vertices))
