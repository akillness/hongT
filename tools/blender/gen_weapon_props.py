# Procedural low-poly weapon props: dagger, bow, hammer (headless Blender).
# D8 (_workspace/current/intake/deep-interview-seed-ui-vfx-flow.md §5): no
# image->3D tool exists in this pipeline, and Abyssal-Surge has no retained
# dagger/bow/hammer source mesh to reskin (only the generic "blade"/"relic"
# props convert_equip_props.py already ships). Author the 3 archetypes
# procedurally instead, same silhouette-tier convention as
# convert_equip_props.py: basic = charcoal, fine = 1.22x scale + ember accent.
#
#   blender -b --factory-startup --python-exit-code 1 \
#     -P tools/blender/gen_weapon_props.py -- --outdir Assets/Art/Props
#
# Socket-space contract (matches convert_equip_props.py, ActorView.AttachEquipProps
# RightHand slot): grip/pivot at origin, striking end along +Y, longest span
# sets the normalized height below. Triangle budget <=800/mesh (D8).
import argparse
import math
import sys

import bpy
from mathutils import Matrix, Vector

TRI_BUDGET = 800


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--outdir", required=True)
    return parser.parse_args(argv)


def apply_all(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def world_bounds(obj):
    corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    hi = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    return lo, hi


def triangle_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def decimate_to_budget(obj):
    tris = triangle_count(obj)
    if tris <= TRI_BUDGET:
        return tris
    modifier = obj.modifiers.new("decimate", "DECIMATE")
    modifier.ratio = TRI_BUDGET / tris
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    return triangle_count(obj)


def normalize_grip_up(obj, height):
    """Uniform-scale longest axis to `height`; grip (min-Z after reorient)
    at origin, striking end toward +Y on export (Blender +Z -> Unity +Y)."""
    apply_all(obj)
    lo, hi = world_bounds(obj)
    span = max(hi.x - lo.x, hi.y - lo.y, hi.z - lo.z)
    if span <= 0:
        raise SystemExit(f"FATAL: degenerate prop bounds for {obj.name}")
    scale = height / span
    obj.matrix_world = Matrix.Scale(scale, 4) @ obj.matrix_world
    apply_all(obj)
    lo, hi = world_bounds(obj)
    spans = {"X": hi.x - lo.x, "Y": hi.y - lo.y, "Z": hi.z - lo.z}
    longest = max(spans, key=spans.get)
    if longest == "X":
        obj.matrix_world = Matrix.Rotation(1.5707963, 4, "Y") @ obj.matrix_world
    elif longest == "Y":
        obj.matrix_world = Matrix.Rotation(1.5707963, 4, "X") @ obj.matrix_world
    apply_all(obj)
    lo, hi = world_bounds(obj)
    center = (lo + hi) * 0.5
    shift = Vector((-center.x, -center.y, -lo.z))   # grip-bottom anchor
    obj.matrix_world = Matrix.Translation(shift) @ obj.matrix_world
    apply_all(obj)


def solid_material(name, color, emission=None):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    bsdf = material.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.55
    if emission is not None:
        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 2.0
    return material


def retint(obj, material):
    obj.data.materials.clear()
    obj.data.materials.append(material)


def join_objects(objects, name):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    if len(objects) > 1:
        bpy.ops.object.join()
    merged = bpy.context.view_layer.objects.active
    merged.name = name
    return merged


def add_box(name, size, location):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    obj.scale = Vector(size)
    apply_all(obj)
    return obj


def add_cylinder(name, radius, depth, location, rotation=(0, 0, 0), verts=8):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=verts, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    apply_all(obj)
    return obj


def add_cone(name, radius, depth, location, rotation=(0, 0, 0), verts=6):
    bpy.ops.mesh.primitive_cone_add(
        vertices=verts, radius1=radius, radius2=0.0, depth=depth,
        location=location, rotation=rotation)
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    apply_all(obj)
    return obj


# --- archetypes --------------------------------------------------------------

def build_dagger():
    """Short blade + crossguard + grip. Blade tip at +Y (up in socket space)."""
    grip = add_cylinder("dagger_grip", radius=0.016, depth=0.16, location=(0, 0, 0.08))
    guard = add_box("dagger_guard", (0.07, 0.014, 0.016), location=(0, 0, 0.165))
    blade = add_cone("dagger_blade", radius=0.028, depth=0.26, location=(0, 0, 0.30),
                      rotation=(0, 0, 0), verts=4)
    return join_objects([grip, guard, blade], "dagger")


def build_bow():
    """Recurve arc built from stacked segments + a taut string, grip mid-limb."""
    segments = []
    grip = add_cylinder("bow_grip", radius=0.02, depth=0.14, location=(0, 0, 0), verts=8)
    segments.append(grip)
    # Symmetric limbs: each a chain of tapering cylinders curving outward.
    limb_offsets = [0.14, 0.30, 0.44, 0.55]
    limb_radii = [0.018, 0.015, 0.011, 0.007]
    bend = [0.0, 0.01, 0.035, 0.07]   # outward curve (X) per stage, tip flares back
    for sign in (1, -1):
        prev = Vector((0, 0, 0.07 * sign))
        for offset, radius, curve in zip(limb_offsets, limb_radii, bend):
            point = Vector((curve, 0, sign * (0.07 + offset)))
            mid = (prev + point) / 2
            seg_dir = point - prev
            length = seg_dir.length
            # Orient cylinder along seg_dir: approximate with rotation about X
            # for the small curvature used here (bow stays near-planar in YZ).
            angle = 0.0
            if length > 1e-6:
                angle = math.atan2(seg_dir.x, seg_dir.z)
            seg = add_cylinder(f"bow_limb_{sign}_{offset}", radius=radius, depth=length,
                                location=(mid.x, mid.y, mid.z), rotation=(angle, 0, 0), verts=6)
            segments.append(seg)
            prev = point
    string = add_cylinder("bow_string", radius=0.003, depth=1.13,
                           location=(0.075, 0, 0), rotation=(0, 1.5707963, 0), verts=4)
    segments.append(string)
    return join_objects(segments, "bow")


def build_hammer():
    """Long handle + rectangular striking head at the far end."""
    handle = add_cylinder("hammer_handle", radius=0.02, depth=0.62, location=(0, 0, 0.31), verts=8)
    head = add_box("hammer_head", (0.20, 0.11, 0.11), location=(0, 0, 0.66))
    cap_a = add_box("hammer_cap_a", (0.05, 0.115, 0.115), location=(0.11, 0, 0.66))
    cap_b = add_box("hammer_cap_b", (0.05, 0.115, 0.115), location=(-0.11, 0, 0.66))
    return join_objects([handle, head, cap_a, cap_b], "hammer")


BUILDERS = {
    "dagger": (build_dagger, 0.42),   # normalized height (m), grip-bottom anchored
    "bow": (build_bow, 1.05),
    "hammer": (build_hammer, 0.88),
}


def unwrap_uniform(obj):
    """Smart-project a uniform-texel-density UV layout.

    WHY (2026-08-13 texture pass): every part here is a Blender primitive, and a
    primitive's default UV maps that part to the FULL 0..1 square. `obj.scale =
    Vector(size)` then stretches the parts wildly non-uniformly — a hammer head
    0.20 long and a dagger guard 0.014 thin both still carry 0..1 — so one
    tiling sheet smears into bands on the blade and dissolves to noise on the
    guard. A per-material _BaseMap_ST cannot fix that: the mismatch is INSIDE
    one mesh. This is the defect EnvironmentBuilder solved with size-
    proportional tiling; a mesh gets to solve it in its own UVs instead.

    Runs AFTER decimation on purpose — decimating an unwrapped mesh shreds the
    islands it just made.
    """
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")


def export_fbx(obj, path):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_ALL",
        bake_space_transform=True, add_leaf_bones=False)


def main():
    args = parse_args()
    bpy.ops.wm.read_factory_settings(use_empty=True)

    charcoal = solid_material("prop-charcoal", (0.13, 0.11, 0.16))
    ember = solid_material("prop-ember", (0.22, 0.12, 0.10), emission=(0.95, 0.35, 0.17))

    results = []
    for archetype, (builder, height) in BUILDERS.items():
        mesh = builder()
        normalize_grip_up(mesh, height=height)
        tris = decimate_to_budget(mesh)
        # Uniform-density UVs BEFORE the first export — the fine variant is a
        # uniform 1.22x scale below, which leaves the layout valid, so one call
        # covers both bands.
        unwrap_uniform(mesh)
        retint(mesh, charcoal)
        out = f"{args.outdir}/equip-weapon-{archetype}-basic.fbx"
        export_fbx(mesh, out)
        results.append((f"equip-weapon-{archetype}-basic", tris))

        retint(mesh, ember)
        mesh.matrix_world = Matrix.Scale(1.22, 4) @ mesh.matrix_world
        apply_all(mesh)
        tris = triangle_count(mesh)
        out = f"{args.outdir}/equip-weapon-{archetype}-fine.fbx"
        export_fbx(mesh, out)
        results.append((f"equip-weapon-{archetype}-fine", tris))

        for obj in list(bpy.data.objects):
            bpy.data.objects.remove(obj, do_unlink=True)

    total = sum(t for _, t in results)
    for name, tris in results:
        print(f"PROP {name}: {tris} tris")
    print(f"PROP TOTAL: {total} tris (budget {TRI_BUDGET}/mesh)")
    over = [name for name, tris in results if tris > TRI_BUDGET]
    if over:
        raise SystemExit(f"FATAL: over budget: {over}")


main()
