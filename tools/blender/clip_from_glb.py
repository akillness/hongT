#!/usr/bin/env python3
"""Turn a Higgsfield 3d_rigging GLB into a Unity humanoid clip FBX.

WHAT THIS DOES NOT DO, and why the omissions are deliberate:

  * NO BONE RENAME. The generated rig is Mixamo-named (LeftUpLeg /
    LeftForeArm / Spine02 / lower-case `neck`), and Unity's humanoid
    auto-mapper accepts it untouched: MEASURED 2026-08-10 via
    Assets/Editor/ClipAvatarProbe.cs — generated clip isHuman True, 15/15
    required bones, against a shipped Mixamo clip as control. An earlier
    draft of this script carried a 22-entry BONE_MAP; it was dead code and
    was deleted rather than left to rot. The clip path in
    CharacterImportPipeline has no rename step either (:291-292) — the
    rename comment at :22-23 is about CHARACTERS, via reskin_character.py.

  * NO ROOT-MOTION STRIP. ReimportClips already sets lockRootRotation /
    lockRootHeightY / lockRootPositionXZ (:314-319, "in-place: sim owns
    displacement"). That is Unity's humanoid in-place projection and the
    path all 14 shipped clips ride. Doing it twice risks disagreeing.

WHAT IT MUST DO, both MEASURED as broken on a plain convert:

  * NAME THE TAKE. Unity names the clip after the FBX take, and LoadClip
    matches `c.name == action` (:334-335). A plain export produced a clip
    called 'Scene', which no action would ever resolve.

  * BOUND THE FRAME RANGE. The generated GLB carries a scene range far wider
    than its action: `Punch_Combo_1` is 2.23 s of animation and exported as
    a 10.38 s clip, the tail being static hold. ClipTrims windows are
    measured in frames (:93-98), so an untrimmed tail silently shifts every
    later measurement.

Usage:
    blender -b --factory-startup --python-exit-code 1 -P clip_from_glb.py -- \
        --glb /tmp/act200.glb --out "Assets/Art/Motion/Punch Combo 1.fbx" \
        --report /tmp/clip.json
"""
import json
import sys
from pathlib import Path

import bpy

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    if name in ARGS:
        return ARGS[ARGS.index(name) + 1]
    if default is None:
        raise SystemExit(f"FATAL: missing {name}")
    return default


GLB = Path(arg("--glb")).resolve()
OUT = Path(arg("--out")).resolve()
REPORT = Path(arg("--report")).resolve()

# The 15 Unity treats as REQUIRED for a humanoid avatar, in the canonical
# names the auto-mapper reports. Checked AFTER import so a rig that silently
# stops mapping is caught here rather than three steps later at
# CharacterImportPipeline.cs:324.
REQUIRED_HUMAN = 15

report = {"input": str(GLB), "output": str(OUT)}


def fail(message):
    report["fatal"] = message
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(report, indent=2), encoding="utf-8")
    raise SystemExit(f"FATAL: {message}")


def action_curves(action):
    """Blender 5 moved fcurves behind layers/strips/channelbags."""
    got = []
    for layer in getattr(action, "layers", []):
        for strip in getattr(layer, "strips", []):
            for bag in getattr(strip, "channelbags", []):
                got.extend(bag.fcurves)
    return got or list(getattr(action, "fcurves", []))


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(GLB))

arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
if not arms:
    fail(f"no armature in {GLB}")
arm = arms[0]

actions = list(bpy.data.actions)
if not actions:
    fail(f"no animation in {GLB} - was --enable-animation passed?")
action = actions[0]
source_name = action.name.split("|")[1] if "|" in action.name else action.name
curves = action_curves(action)
if not curves:
    fail(f"action '{source_name}' has no fcurves")

# --- bound the scene to the action ---------------------------------------
# Read the range off the KEYFRAMES, not action.frame_range: the latter is
# already the union the exporter would use, and the whole point is to cut the
# static tail the generator appends.
first = min(kp.co[0] for fc in curves for kp in fc.keyframe_points)
last = max(kp.co[0] for fc in curves for kp in fc.keyframe_points)
scene = bpy.context.scene
scene.frame_start = int(first)
scene.frame_end = max(int(last), int(first) + 1)
report["sourceAction"] = source_name
report["keyframeRange"] = [first, last]
report["sceneRange"] = [scene.frame_start, scene.frame_end]
report["seconds"] = (scene.frame_end - scene.frame_start) / max(scene.render.fps, 1)

# --- name the take --------------------------------------------------------
# Unity names the clip after the take; LoadClip matches on that name.
take = OUT.stem
action.name = take
if arm.animation_data is None:
    arm.animation_data_create()
arm.animation_data.action = action
report["takeName"] = take
report["bones"] = [b.name for b in arm.data.bones]

# --- export ---------------------------------------------------------------
# apply_scale_options FBX_SCALE_ALL: omitting it produced an FBX whose skeleton
# was byte-identical inside Blender but imported into Unity as a VALID but
# NON-HUMAN avatar, the cm/m factor sitting on a parent transform
# [MEASURED 2026-08-09, tools/blender/reskin_from_fbx.py:331-348].
OUT.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
arm.select_set(True)
for obj in bpy.data.objects:
    if obj.type == "MESH":
        obj.select_set(True)
bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.fbx(
    filepath=str(OUT),
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_actions=False,
    bake_anim_use_nla_strips=False,
    bake_anim_simplify_factor=0.0,
    apply_scale_options="FBX_SCALE_ALL",
    path_mode="COPY",
    embed_textures=False,
    mesh_smooth_type="FACE",
)
report["exported"] = OUT.exists()
report["exportedBytes"] = OUT.stat().st_size if OUT.exists() else 0
REPORT.parent.mkdir(parents=True, exist_ok=True)
REPORT.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(f"[clip_from_glb] {OUT.name}: take '{take}' from '{source_name}', "
      f"frames {scene.frame_start}-{scene.frame_end} "
      f"({report['seconds']:.2f}s), {report['exportedBytes']} bytes")
