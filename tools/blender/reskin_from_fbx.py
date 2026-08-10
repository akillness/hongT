# Re-skin a NEW mesh onto an ALREADY-CANONICAL skeleton taken from a shipped FBX.
#
# Why this exists. tools/blender/reskin_character.py takes its skeleton from the
# Abyssal-Surge motion library (`assets/motion/ingame/characters/<id>/model.glb`)
# and renames def-humanoid-v1 bones to Unity-canonical names via BONE_MAP. That
# library is GONE [OBSERVED 2026-08-09]: ~/orca/Abyssal-Surge was rebuilt as a
# Unity project and no longer carries assets/motion or assets/mesh, so every one
# of the 12 ids misses — including the original 8, not just the four recovered
# later. reskin_all.sh is a reproduction RECORD, not a runnable step.
#
# But the skeleton is not actually lost. Every shipped
# Assets/Art/Characters/<id>.fbx already carries the 22 Unity-canonical humanoid
# bones (Hips…RightToes) — the BONE_MAP rename ALREADY RAN on them, and the
# reskin reports record it (`bones` field; all 12 measured, zero required bones
# missing). So a new mesh does not need the vanished GLB: it needs a skeleton
# donor, and there are twelve of those in the repo.
#
# What changes from the GLB script:
#   * skeleton comes from --skeleton-fbx (a shipped character FBX), not a GLB;
#   * BONE_MAP is SKIPPED — those bones are already canonical, so running the
#     DEF-* filter over them would delete the entire skeleton;
#   * the canonical set is validated directly instead, with the same hard gate:
#     a partial skeleton silently degrades Unity Humanoid mapping;
#   * the mesh donor may be an FBX or a GLB (Higgsfield exports glb/fbx).
#
# Everything downstream — bone-heat auto weights, orphan fill, tri decimation to
# the 25k WebGL ceiling, texture clamp, animation wipe, FBX export — is the same
# contract, because that is the part that was never broken.
#
# Usage:
#   blender -b --factory-startup --python-exit-code 1 \
#     -P tools/blender/reskin_from_fbx.py -- \
#     --skeleton-fbx Assets/Art/Characters/guard.fbx \
#     --mesh Assets/Art/Characters/scout.fbx \
#     --out Assets/Art/Characters/<new-id>.fbx \
#     --report _workspace/current/engineering/reskin/<new-id>.json \
#     [--max-tris 25000] [--mesh-scale-mode height|span]
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
import mathutils

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    return ARGS[ARGS.index(name) + 1] if name in ARGS else default


SKELETON = Path(arg("--skeleton-fbx")).resolve()
MESH_SRC = Path(arg("--mesh")).resolve()
OUT = Path(arg("--out")).resolve()
REPORT = Path(arg("--report")).resolve()
MAX_TRIS = int(arg("--max-tris", "25000"))
SCALE_MODE = arg("--mesh-scale-mode", "height")
MAX_TEX = 1024

# The 22 bones every shipped character carries. This is the SAME set BONE_MAP
# produces, stated as a destination rather than a rename — see the module
# docstring for why the rename must not run again.
CANONICAL = [
    "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head",
    "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
    "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
    "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes",
    "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes",
]
# Unity refuses to build a Humanoid avatar without these; the optional four
# (Chest/UpperChest/Neck/Toes) degrade quietly instead.
REQUIRED = [b for b in CANONICAL
            if b not in ("UpperChest", "LeftToes", "RightToes", "Neck")]

report = {
    "variant": "reskin_from_fbx",
    "skeletonSource": str(SKELETON),
    "meshSource": str(MESH_SRC),
    "output": str(OUT),
    "warnings": [],
    "materials": [],
}


def fail(message, **extra):
    report.update(extra)
    report["fatal"] = message
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(report, indent=2), encoding="utf-8")
    raise SystemExit(f"FATAL: {message}")


def import_any(path):
    """Import an FBX or GLB and return the objects it added."""
    before = set(bpy.data.objects)
    suffix = path.suffix.lower()
    if suffix == ".fbx":
        bpy.ops.import_scene.fbx(filepath=str(path))
    elif suffix in (".glb", ".gltf"):
        bpy.ops.import_scene.gltf(filepath=str(path))
    else:
        fail(f"unsupported mesh/skeleton format: {path.suffix}")
    return [o for o in set(bpy.data.objects) - before]


def world_bounds(objects):
    lo = [math.inf] * 3
    hi = [-math.inf] * 3
    for obj in objects:
        if obj.type == "ARMATURE":
            # bound_box is unreliable for armatures (skips leaf bones, can be
            # asymmetric) — use bone heads/tails, same as the GLB script.
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


# --- 1. skeleton donor ---------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
skeleton_objects = import_any(SKELETON)
armatures = [o for o in skeleton_objects if o.type == "ARMATURE"]
if not armatures:
    fail(f"no armature in --skeleton-fbx {SKELETON}")
arm = armatures[0]

have = {b.name for b in arm.data.bones}
missing = [b for b in REQUIRED if b not in have]
if missing:
    # Hard gate, same reasoning as the GLB script: a partial skeleton does not
    # error in Unity, it silently produces a non-human avatar and the import
    # pipeline throws much later with a less useful message.
    fail(f"skeleton donor is not canonical — missing {missing}",
         bonesFound=sorted(have))
report["bones"] = sorted(have)
report["bonesOptionalMissing"] = [b for b in CANONICAL if b not in have]

# Drop the donor's own mesh: we keep its skeleton only.
for obj in skeleton_objects:
    if obj.type == "MESH":
        bpy.data.objects.remove(obj, do_unlink=True)

# --- 2. incoming mesh ----------------------------------------------------------
skel_lo, skel_hi = world_bounds([arm])
incoming = import_any(MESH_SRC)
meshes = [o for o in incoming if o.type == "MESH"]
if not meshes:
    fail(f"no mesh in --mesh {MESH_SRC}")

# Strip anything that rode along (the donor mesh file may carry its own rig).
for obj in incoming:
    if obj.type == "MESH":
        continue
    for child in list(obj.children):
        if child.type == "MESH":
            matrix = child.matrix_world.copy()
            child.parent = None
            child.matrix_world = matrix
    bpy.data.objects.remove(obj, do_unlink=True)
meshes = [o for o in bpy.data.objects if o.type == "MESH"]

# Clear any skinning the incoming mesh brought with it — it refers to a
# skeleton that is no longer in the scene, and heat will rebuild it anyway.
for mesh in meshes:
    for mod in list(mesh.modifiers):
        if mod.type == "ARMATURE":
            mesh.modifiers.remove(mod)
    for group in list(mesh.vertex_groups):
        mesh.vertex_groups.remove(group)

# --- 3. fit the mesh to the skeleton -------------------------------------------
mesh_lo, mesh_hi = world_bounds(meshes)
skel_h = skel_hi.z - skel_lo.z
mesh_h = mesh_hi.z - mesh_lo.z
skel_span = skel_hi.x - skel_lo.x
mesh_span = mesh_hi.x - mesh_lo.x
scale_h = skel_h / mesh_h if mesh_h > 1e-6 else 1.0
scale_span = skel_span / mesh_span if mesh_span > 1e-6 else scale_h
scale = max(scale_h, scale_span) if SCALE_MODE == "span" else scale_h
report["meshFit"] = {
    "scaleMode": SCALE_MODE, "scale": scale,
    "skeletonHeight": skel_h, "meshHeight": mesh_h,
    "skeletonSpan": skel_span, "meshSpan": mesh_span,
}

# Proportion gate. MEASURED 2026-08-09 by the code path below (the numbers are
# from should-fail.json, not from a scratch probe — an early draft of this
# comment quoted 0.725 from a throwaway probe that had deleted the armature
# before measuring, which changes the mesh bound_box):
#   skeleton (guard)  1.1257
#   mesh     (scout)  1.6993
#   height scale      0.6625      ratio 1.5095
# That bind imported into Unity as a VALID but NON-HUMAN avatar — 20 of 22
# bones mapped, LeftHand and RightHand refused, both REQUIRED. Nothing errored
# on the way: heat solved with zero orphans, all 22 bones bound, every vertex
# weighted. The same mesh on its OWN skeleton (ratio 1.0) mapped 22/22 and came
# back isHuman, which is what proves the pipeline and isolates the cause.
#
# Note what is NOT the cause, because it looks like it: hand bones sitting
# outside the mesh bounding box is NORMAL here — shipped guard has hand tips at
# x=1.12 against a mesh span of 0.75 and maps fine. The mismatch that matters
# is HEIGHT, because scaling the mesh shrinks it while the donor skeleton stays
# full size, and Unity's auto-mapper stops believing the arm chain.
#
# So: fail loudly here rather than produce an avatar the import pipeline
# rejects three steps later with a less useful message
# (CharacterImportPipeline.cs:163).
MAX_HEIGHT_RATIO = 1.25
height_ratio = max(scale_h, 1.0 / scale_h) if scale_h > 1e-6 else float("inf")
report["meshFit"]["heightRatio"] = height_ratio
report["meshFit"]["heightRatioCeiling"] = MAX_HEIGHT_RATIO
if height_ratio > MAX_HEIGHT_RATIO:
    fail(
        f"mesh/skeleton height ratio {height_ratio:.3f} exceeds {MAX_HEIGHT_RATIO} "
        f"(skeleton {skel_h:.3f} vs mesh {mesh_h:.3f}) — Unity's humanoid "
        "auto-mapper drops the hand bones at this disparity and the avatar "
        "imports as non-human. Pick a skeleton donor closer in height, or "
        "generate the mesh at the donor's height "
        "(meshy_v6 exposes rigging_height_meters)."
    )

center = (mesh_lo + mesh_hi) / 2
for mesh in meshes:
    mesh.scale = (scale, scale, scale)
    bpy.context.view_layer.update()
bpy.context.view_layer.update()
mesh_lo, mesh_hi = world_bounds(meshes)
center = (mesh_lo + mesh_hi) / 2
# Centre on the skeleton in XY, and sit both on the same floor in Z: a mesh
# floating above its own feet makes the leg heat solve nonsense.
skel_center = (skel_lo + skel_hi) / 2
offset = mathutils.Vector((skel_center.x - center.x,
                           skel_center.y - center.y,
                           skel_lo.z - mesh_lo.z))
for mesh in meshes:
    mesh.location = mesh.location + offset
bpy.context.view_layer.update()

# --- 4. join to a single body ---------------------------------------------------
bpy.ops.object.select_all(action="DESELECT")
for mesh in meshes:
    mesh.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
body = bpy.context.view_layer.objects.active
body.name = OUT.stem

# Bake the object transforms into the data — the step whose absence made the
# first proof run fail. MEASURED: without it Unity built a VALID but NON-HUMAN
# avatar, mapping only 20 of 22 bones. The hierarchy, bone names, world
# positions and skinning were all identical to a shipped character; the single
# visible difference was the rest pose reaching Unity through an unbaked object
# transform — shipped guard had Hips at localPos (0, -0.02, 0.61) while the
# re-export had (0, 0, 0.01), the offset sitting on the parent Armature instead
# of the bone. Unity's auto-mapper reads the rest pose, so a skeleton whose
# proportions live on a parent transform does not look human to it.
#
# reskin_character.py applies both (mesh at :213, armature at :234); this
# variant has to do the same or it produces an avatar the import pipeline
# rejects at CharacterImportPipeline.cs:163.
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
# --- 5. pre-heat hygiene --------------------------------------------------------
# Bone heat fails on duplicate verts, loose geometry and non-manifold seams.
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.mode_set(mode="EDIT")
mesh_data = bmesh.from_edit_mesh(body.data)
before_verts = len(mesh_data.verts)
bmesh.ops.remove_doubles(mesh_data, verts=mesh_data.verts, dist=1e-5)
loose = [v for v in mesh_data.verts if not v.link_faces]
bmesh.ops.delete(mesh_data, geom=loose, context="VERTS")
bmesh.update_edit_mesh(body.data)
bpy.ops.object.mode_set(mode="OBJECT")
report["hygiene"] = {
    "vertsBefore": before_verts,
    "vertsAfter": len(body.data.vertices),
    "looseRemoved": len(loose),
}

# --- 6. bone-heat auto weights --------------------------------------------------
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
try:
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
except RuntimeError as exc:
    fail(f"bone heat failed: {exc}")

# Orphans = vertices heat could not reach. The GLB script fills them from the
# nearest weighted neighbour; same rule here, because an unweighted vertex is
# left at the origin under animation and reads as an exploding mesh.
orphans = []
for vertex in body.data.vertices:
    if not any(g.weight > 0 for g in vertex.groups):
        orphans.append(vertex.index)
report["heatOrphans"] = len(orphans)
if orphans:
    kd = mathutils.kdtree.KDTree(len(body.data.vertices))
    for v in body.data.vertices:
        if any(g.weight > 0 for g in v.groups):
            kd.insert(v.co, v.index)
    kd.balance()
    filled = 0
    for index in orphans:
        vertex = body.data.vertices[index]
        _, near, _ = kd.find(vertex.co)
        if near is None:
            continue
        for group in body.data.vertices[near].groups:
            if group.weight > 0:
                body.vertex_groups[group.group].add([index], group.weight, "REPLACE")
                filled += 1
                break
    report["orphansTransferFilled"] = filled
    report["orphansRigidResidual"] = len(orphans) - filled

# --- 7. tri budget --------------------------------------------------------------
def tri_count(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


tris = tri_count(body)
report["sourceTriCount"] = tris
if tris > MAX_TRIS:
    ratio = MAX_TRIS / tris
    decimate = body.modifiers.new("decimate", "DECIMATE")
    decimate.ratio = ratio
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.modifier_apply(modifier=decimate.name)
    report["decimatedTo"] = MAX_TRIS
report["finalTriCount"] = tri_count(body)
report["finalVertexCount"] = len(body.data.vertices)

# --- 8. no animation ships ------------------------------------------------------
for action_block in list(bpy.data.actions):
    bpy.data.actions.remove(action_block)
if arm.animation_data:
    arm.animation_data_clear()
arm.name = "Armature"

# --- 9. export ------------------------------------------------------------------
OUT.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
body.select_set(True)
arm.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(
    filepath=str(OUT),
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    add_leaf_bones=False,
    bake_anim=False,
    # MEASURED: omitting apply_scale_options produced an FBX whose skeleton is
    # byte-identical inside Blender (same object transform, same bone
    # head_local, same root) yet imports into Unity with Hips at local
    # (0, 0, 0.01) instead of (0, -0.02, 0.61) — the 0.01 being the FBX cm/m
    # factor. Unity's auto-mapper then matched only 20 of 22 bones and built a
    # VALID but NON-HUMAN avatar, which CharacterImportPipeline.cs:163 rejects.
    # Every export parameter here is matched to reskin_character.py:428-438;
    # they are not cosmetic.
    apply_scale_options="FBX_SCALE_ALL",
    path_mode="COPY",
    embed_textures=True,
    mesh_smooth_type="FACE",
)
report["exported"] = OUT.exists()
report["exportedBytes"] = OUT.stat().st_size if OUT.exists() else 0

REPORT.parent.mkdir(parents=True, exist_ok=True)
REPORT.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(f"[reskin_from_fbx] {OUT.name}: {report['finalTriCount']} tris, "
      f"{report['heatOrphans']} heat orphans, {report['exportedBytes']} bytes")
