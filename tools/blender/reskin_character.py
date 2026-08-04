# Re-skin a motion-library character GLB for Unity Humanoid retargeting.
#
# Root cause being fixed: the original pipeline partitioned faces into semantic
# regions and bound them rigidly to single bones, then baked custom-retargeted
# clips. Under animation the mesh shears/explodes at region seams. We discard
# ALL of that: keep the mesh + skeleton geometry, rebuild skinning with bone
# heat automatic weights, rename bones to Unity-canonical humanoid names, and
# export a clean FBX with NO animations. Unity retargets bench Mixamo clips
# through Mecanim Humanoid instead.
#
# Usage:
#   blender -b --factory-startup -P tools/blender/reskin_character.py -- \
#     --glb <in.glb> --out <out.fbx> --report <report.json> [--max-tris 25000]
import bpy
import json
import math
import sys
from pathlib import Path

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    if name in ARGS:
        return ARGS[ARGS.index(name) + 1]
    return default


GLB = Path(arg("--glb")).resolve()
OUT = Path(arg("--out")).resolve()
REPORT = Path(arg("--report")).resolve()
MAX_TRIS = int(arg("--max-tris", "25000"))
MAX_TEX = 1024

# def-humanoid-v1 -> Unity canonical humanoid names (identity HumanDescription
# mapping on the Unity side). Bones absent from this map are deleted, children
# reparented, so no vertex weight can land outside the retarget set.
BONE_MAP = {
    "DEF-spine": "Hips",
    "DEF-spine.001": "Spine",
    "DEF-spine.002": "Chest",
    "DEF-spine.003": "UpperChest",
    "DEF-spine.004": "Neck",
    "DEF-spine.005": "Head",
    "DEF-shoulder.L": "LeftShoulder",
    "DEF-upper_arm.L": "LeftUpperArm",
    "DEF-forearm.L": "LeftLowerArm",
    "DEF-hand.L": "LeftHand",
    "DEF-shoulder.R": "RightShoulder",
    "DEF-upper_arm.R": "RightUpperArm",
    "DEF-forearm.R": "RightLowerArm",
    "DEF-hand.R": "RightHand",
    "DEF-thigh.L": "LeftUpperLeg",
    "DEF-shin.L": "LeftLowerLeg",
    "DEF-foot.L": "LeftFoot",
    "DEF-toe.L": "LeftToes",
    "DEF-thigh.R": "RightUpperLeg",
    "DEF-shin.R": "RightLowerLeg",
    "DEF-foot.R": "RightFoot",
    "DEF-toe.R": "RightToes",
}

report = {"input": str(GLB), "output": str(OUT), "warnings": [], "materials": []}

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(GLB))

armatures = [o for o in bpy.data.objects if o.type == "ARMATURE"]
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
if not armatures or not meshes:
    raise SystemExit(f"FATAL: expected armature+meshes in {GLB}")
arm = armatures[0]

# --- 1. Flatten meshes: apply modifiers, drop old skinning ------------------
bpy.ops.object.select_all(action="DESELECT")
for mesh in meshes:
    bpy.context.view_layer.objects.active = mesh
    mesh.select_set(True)
    if mesh.data.shape_keys:
        bpy.ops.object.shape_key_remove(all=True)
    # Drop armature modifiers; convert applies whatever else remains.
    for mod in list(mesh.modifiers):
        if mod.type == "ARMATURE":
            mesh.modifiers.remove(mod)
    mesh.select_set(False)

bpy.ops.object.select_all(action="DESELECT")
for mesh in meshes:
    mesh.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
body = bpy.context.view_layer.objects.active
body.name = "Body"

# Clear parenting but keep world transform, then clear old vertex groups.
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
body.vertex_groups.clear()
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

report["sourceVertexCount"] = len(body.data.vertices)

# --- 2. Decimate to budget ---------------------------------------------------
def tri_count(obj):
    return sum(max(0, len(p.vertices) - 2) for p in obj.data.polygons)


tris = tri_count(body)
report["sourceTriCount"] = tris
if tris > MAX_TRIS:
    mod = body.modifiers.new("decimate", "DECIMATE")
    mod.ratio = MAX_TRIS / tris
    mod.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.modifier_apply(modifier=mod.name)
    report["decimatedTo"] = tri_count(body)

# --- 3. Delete junk objects --------------------------------------------------
for obj in list(bpy.data.objects):
    if obj not in (arm, body):
        bpy.data.objects.remove(obj, do_unlink=True)

# --- 4. Skeleton normalization ------------------------------------------------
bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.mode_set(mode="EDIT")
edit_bones = arm.data.edit_bones
removed_bones = []
for bone in list(edit_bones):
    if bone.name not in BONE_MAP:
        for child in bone.children:
            child.use_connect = False
            child.parent = bone.parent
        removed_bones.append(bone.name)
        edit_bones.remove(bone)
report["removedBones"] = removed_bones
missing = [b for b in BONE_MAP if b not in {bb.name for bb in edit_bones}]
if missing:
    # Hard gate: a partial skeleton silently degrades Unity Humanoid mapping.
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    report["fatalMissingBones"] = missing
    REPORT.write_text(json.dumps(report, indent=2), encoding="utf-8")
    raise SystemExit(f"FATAL: {GLB.name} missing expected bones: {missing}")
bpy.ops.object.mode_set(mode="OBJECT")
for bone in arm.data.bones:
    bone.use_deform = True
for old, new in BONE_MAP.items():
    if old in arm.data.bones:
        arm.data.bones[old].name = new
arm.name = "Armature"

# Wipe every imported action / NLA strip: clips come from the bench in Unity.
for action_block in list(bpy.data.actions):
    bpy.data.actions.remove(action_block)
if arm.animation_data:
    arm.animation_data_clear()

# --- 5. Re-skin with automatic weights ----------------------------------------
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_set(type="ARMATURE_AUTO")

# --- 6. Orphan rescue: nearest-bone rigid weight -------------------------------
bone_heads = []
for bone in arm.data.bones:
    head_world = arm.matrix_world @ bone.head_local
    tail_world = arm.matrix_world @ bone.tail_local
    mid = (head_world + tail_world) / 2
    bone_heads.append((bone.name, mid))

group_index = {g.index: g.name for g in body.vertex_groups}
orphans = 0
for vert in body.data.vertices:
    total = sum(g.weight for g in vert.groups)
    if total < 1e-6:
        world_co = body.matrix_world @ vert.co
        best_name, best_d = None, math.inf
        for name, mid in bone_heads:
            d = (world_co - mid).length_squared
            if d < best_d:
                best_name, best_d = name, d
        body.vertex_groups[best_name].add([vert.index], 1.0, "REPLACE")
        orphans += 1
report["orphanVerticesFixed"] = orphans

# Limit influences to 4 and normalize (contract requirement).
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.vertex_group_limit_total(limit=4)
bpy.ops.object.vertex_group_normalize_all(lock_active=False)

# --- 7. Texture budget ---------------------------------------------------------
tex_report = []
for img in bpy.data.images:
    if img.size[0] > MAX_TEX or img.size[1] > MAX_TEX:
        img.scale(min(img.size[0], MAX_TEX), min(img.size[1], MAX_TEX))
    tex_report.append({"name": img.name, "size": list(img.size)})
report["textures"] = tex_report

for mat_slot in body.material_slots:
    mat = mat_slot.material
    if not mat:
        continue
    albedo = None
    if mat.use_nodes:
        for node in mat.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image:
                albedo = node.image.name
                break
    report["materials"].append({"name": mat.name, "albedoTexture": albedo})

# --- 8. Weight sanity metrics ----------------------------------------------------
bad_weights = 0
for vert in body.data.vertices:
    total = sum(g.weight for g in vert.groups)
    if not (0.999 <= total <= 1.001) or not math.isfinite(total):
        bad_weights += 1
report["nonNormalizedVertices"] = bad_weights
report["finalTriCount"] = tri_count(body)
report["finalVertexCount"] = len(body.data.vertices)
report["bones"] = [b.name for b in arm.data.bones]

# --- 9. Export -------------------------------------------------------------------
OUT.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
body.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=str(OUT),
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    add_leaf_bones=False,
    bake_anim=False,
    apply_scale_options="FBX_SCALE_ALL",
    path_mode="COPY",
    embed_textures=True,
    mesh_smooth_type="FACE",
)
REPORT.parent.mkdir(parents=True, exist_ok=True)
REPORT.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(f"RESKIN OK {GLB.name}: tris {report['sourceTriCount']}->{report['finalTriCount']}, "
      f"orphansFixed {orphans}, badWeights {bad_weights}, removedBones {len(removed_bones)}")
