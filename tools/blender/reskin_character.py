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
import bmesh
import bpy
import json
import math
import mathutils
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
# "height" (default): preserve authored proportions, compress arm chain.
# "span": envelope scale (mesh may exceed skeleton height) — for fused meshes
# whose interior faces defeat bone-heat unless bones sit deep inside geometry.
SCALE_MODE = arg("--mesh-scale-mode", "height")
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

MESH_GLB = arg("--mesh-glb")  # optional: replace library mesh with this GLB's mesh

report = {"input": str(GLB), "output": str(OUT), "warnings": [], "materials": [],
          "meshSource": MESH_GLB or str(GLB)}

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(GLB))

armatures = [o for o in bpy.data.objects if o.type == "ARMATURE"]
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
if not armatures or not meshes:
    raise SystemExit(f"FATAL: expected armature+meshes in {GLB}")
arm = armatures[0]

if MESH_GLB:
    # The library mesh is a placeholder blockout (or fused seam soup). Swap in
    # the clean authored mesh, aligned to the skeleton's height and floor.
    def world_bounds(objects):
        lo = [math.inf] * 3
        hi = [-math.inf] * 3
        for obj in objects:
            if obj.type == "ARMATURE":
                # bound_box is unreliable for armatures (skips leaf bones,
                # can be asymmetric). Use bone heads/tails directly.
                points = []
                for bone in obj.data.bones:
                    points.append(obj.matrix_world @ bone.head_local)
                    points.append(obj.matrix_world @ bone.tail_local)
            else:
                points = [obj.matrix_world @ mathutils.Vector(c) for c in obj.bound_box]
            for world in points:
                for axis in range(3):
                    lo[axis] = min(lo[axis], world[axis])
                    hi[axis] = max(hi[axis], world[axis])
        return mathutils.Vector(lo), mathutils.Vector(hi)

    skel_lo, skel_hi = world_bounds([arm])
    for mesh in meshes:
        bpy.data.objects.remove(mesh, do_unlink=True)
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(Path(MESH_GLB).resolve()))
    incoming = [o for o in set(bpy.data.objects) - before]
    meshes = [o for o in incoming if o.type == "MESH"]
    if not meshes:
        raise SystemExit(f"FATAL: no mesh in --mesh-glb {MESH_GLB}")
    # Drop any armature/empties that came with the mesh file.
    for obj in incoming:
        if obj.type == "MESH":
            continue
        for child in list(obj.children):
            if child.type == "MESH":
                matrix = child.matrix_world.copy()
                child.parent = None
                child.matrix_world = matrix
        bpy.data.objects.remove(obj, do_unlink=True)
    mesh_lo, mesh_hi = world_bounds(meshes)
    skel_h = skel_hi.z - skel_lo.z
    mesh_h = mesh_hi.z - mesh_lo.z
    skel_span = skel_hi.x - skel_lo.x
    mesh_span = mesh_hi.x - mesh_lo.x
    # The skeleton was fitted to a placeholder blockout — the authored mesh is
    # the source of truth for proportions. Default: scale mesh by HEIGHT only
    # (legs/hips must land correctly or locomotion bends look rubbery), then
    # compress the arm bone chains so hand tips sit inside the mesh's actual
    # arm extent. "span" mode: envelope max() — proven necessary for monarch's
    # fused geometry (28% duplicate verts; heat needs bones deep inside).
    scale_h = skel_h / mesh_h if mesh_h > 1e-6 else 1.0
    scale_span = skel_span / mesh_span if mesh_span > 1e-6 else scale_h
    scale = max(scale_h, scale_span) if SCALE_MODE == "span" else scale_h
    center = (mesh_lo + mesh_hi) / 2
    skel_center = (skel_lo + skel_hi) / 2
    for mesh in meshes:
        mesh.matrix_world = (
            mathutils.Matrix.Translation(mathutils.Vector((
                skel_center.x, skel_center.y, skel_lo.z))) @
            mathutils.Matrix.Scale(scale, 4) @
            mathutils.Matrix.Translation(-mathutils.Vector((
                center.x, center.y, mesh_lo.z))) @
            mesh.matrix_world)

    # Arm-chain fit: hand tail x-extent -> 95% of scaled mesh half-span.
    scaled_half_span = (mesh_span * scale) / 2
    hand_tip_x = max(
        abs((arm.matrix_world @ arm.data.bones["DEF-hand.L"].tail_local).x),
        abs((arm.matrix_world @ arm.data.bones["DEF-hand.R"].tail_local).x))
    arm_fit = 1.0
    if hand_tip_x > 1e-6 and hand_tip_x > scaled_half_span * 0.95:
        arm_fit = (scaled_half_span * 0.95) / hand_tip_x
        bpy.ops.object.select_all(action="DESELECT")
        arm.select_set(True)
        bpy.context.view_layer.objects.active = arm
        bpy.ops.object.mode_set(mode="EDIT")
        arm_chain = ("DEF-shoulder", "DEF-upper_arm", "DEF-forearm", "DEF-hand")
        for edit_bone in arm.data.edit_bones:
            base = edit_bone.name.rsplit(".", 1)[0]
            if base in arm_chain:
                for point in (edit_bone.head, edit_bone.tail):
                    point.x *= arm_fit
        bpy.ops.object.mode_set(mode="OBJECT")

    overshoot = (mesh_h * scale) / skel_h if skel_h > 1e-6 else 1.0
    report["meshSwap"] = {
        "scale": scale, "scaleMode": SCALE_MODE, "armFit": arm_fit,
        "heightOvershoot": overshoot,
        "skeletonHeight": skel_h, "meshHeight": mesh_h,
        "skeletonSpan": skel_span, "meshSpan": mesh_span,
    }
    # armFit is the live proportion-mismatch signal (height fit is exact by
    # construction). Normal range measured 2026-08-04: 0.75-0.78.
    if arm_fit < 0.7:
        REPORT.parent.mkdir(parents=True, exist_ok=True)
        REPORT.write_text(json.dumps(report, indent=2), encoding="utf-8")
        raise SystemExit(
            f"FATAL: arm chain compressed {arm_fit:.2f} (<0.7) — rig/mesh "
            f"proportions fundamentally mismatched in {GLB.name}")

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

# Tri budget is enforced AFTER skinning (§6-bis): bone-heat solves much better
# on the original manifold topology; Decimate preserves vertex groups.
def tri_count(obj):
    return sum(max(0, len(p.vertices) - 2) for p in obj.data.polygons)


report["sourceTriCount"] = tri_count(body)

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

# --- 4-bis. Pre-heat mesh hygiene ----------------------------------------------
# Bone-heat fails on duplicate verts / loose geometry / non-manifold seams.
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
bpy.context.view_layer.objects.active = body
bm = bmesh.new()
bm.from_mesh(body.data)
before_verts = len(bm.verts)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
loose = [v for v in bm.verts if not v.link_faces]
if loose:
    bmesh.ops.delete(bm, geom=loose, context="VERTS")
bm.to_mesh(body.data)
bm.free()
report["hygiene"] = {"mergedVerts": before_verts - len(body.data.vertices),
                     "looseRemoved": len(loose)}

# --- 5. Re-skin with automatic weights ----------------------------------------
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_set(type="ARMATURE_AUTO")

total_verts = len(body.data.vertices)


def unweighted_indices():
    return [v.index for v in body.data.vertices
            if sum(g.weight for g in v.groups) < 1e-6]


heat_orphans = unweighted_indices()
report["heatOrphans"] = len(heat_orphans)
heat_fail_ratio = len(heat_orphans) / max(1, total_verts)
if heat_fail_ratio > 0.5:
    # Heat produced (almost) nothing — rigid fallback would recreate the exact
    # per-region rigid skinning this pipeline exists to eliminate.
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    report["fatalHeatFailRatio"] = heat_fail_ratio
    REPORT.write_text(json.dumps(report, indent=2), encoding="utf-8")
    raise SystemExit(
        f"FATAL: bone-heat failed for {heat_fail_ratio:.0%} of vertices in {GLB.name}")

# --- 6. Orphan fill: surface-transfer weights from solved vertices --------------
if heat_orphans:
    mask = body.vertex_groups.new(name="__orphan_mask")
    mask.add(heat_orphans, 1.0, "REPLACE")

    # Donor = copy of body with orphan verts deleted (only solved weights remain).
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.duplicate()
    donor = bpy.context.view_layer.objects.active
    donor_bm = bmesh.new()
    donor_bm.from_mesh(donor.data)
    donor_bm.verts.ensure_lookup_table()
    doomed = [donor_bm.verts[i] for i in heat_orphans]
    bmesh.ops.delete(donor_bm, geom=doomed, context="VERTS")
    donor_bm.to_mesh(donor.data)
    donor_bm.free()

    if len(donor.data.vertices) > 0:
        bpy.ops.object.select_all(action="DESELECT")
        body.select_set(True)
        bpy.context.view_layer.objects.active = body
        transfer = body.modifiers.new("orphan_fill", "DATA_TRANSFER")
        transfer.object = donor
        transfer.use_vert_data = True
        transfer.data_types_verts = {"VGROUP_WEIGHTS"}
        transfer.vert_mapping = "POLYINTERP_NEAREST"
        transfer.layers_vgroup_select_src = "ALL"
        transfer.layers_vgroup_select_dst = "NAME"
        transfer.vertex_group = "__orphan_mask"
        bpy.ops.object.modifier_apply(modifier=transfer.name)
    bpy.data.objects.remove(donor, do_unlink=True)
    mask = body.vertex_groups.get("__orphan_mask")
    if mask:
        body.vertex_groups.remove(mask)

# Residual orphans (isolated debris far from any surface): nearest-bone rigid.
bone_heads = []
for bone in arm.data.bones:
    mid = (arm.matrix_world @ bone.head_local +
           arm.matrix_world @ bone.tail_local) / 2
    bone_heads.append((bone.name, mid))
residual = unweighted_indices()
for index in residual:
    world_co = body.matrix_world @ body.data.vertices[index].co
    best_name = min(bone_heads, key=lambda pair: (world_co - pair[1]).length_squared)[0]
    group = body.vertex_groups.get(best_name) or body.vertex_groups.new(name=best_name)
    group.add([index], 1.0, "REPLACE")
report["orphansTransferFilled"] = len(heat_orphans) - len(residual)
report["orphansRigidResidual"] = len(residual)
if len(residual) / max(1, total_verts) > 0.02:
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    report["fatalResidualRatio"] = len(residual) / total_verts
    REPORT.write_text(json.dumps(report, indent=2), encoding="utf-8")
    raise SystemExit(
        f"FATAL: {len(residual)}/{total_verts} verts still rigid after transfer in {GLB.name}")

# Smooth filled weights so island seams don't shear under motion.
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
bpy.context.view_layer.objects.active = body
if heat_orphans:
    # vertex_group_smooth polls for weight-paint context in headless Blender.
    bpy.ops.object.mode_set(mode="WEIGHT_PAINT")
    bpy.ops.object.vertex_group_smooth(
        group_select_mode="ALL", factor=0.5, repeat=3, expand=0.5)
    bpy.ops.object.mode_set(mode="OBJECT")

# --- 6-bis. Decimate AFTER skinning (weights preserved by collapse) ------------
if tri_count(body) > MAX_TRIS:
    mod = body.modifiers.new("decimate", "DECIMATE")
    mod.ratio = MAX_TRIS / tri_count(body)
    mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    report["decimatedTo"] = tri_count(body)

# Limit influences to 4 and normalize (contract requirement).
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
      f"heatOrphans {report['heatOrphans']}, filled {report['orphansTransferFilled']}, "
      f"rigidResidual {report['orphansRigidResidual']}, badWeights {bad_weights}, "
      f"removedBones {len(removed_bones)}")
