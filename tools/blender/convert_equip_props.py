# Equip prop GLBs -> hand/chest-socket FBXs for Unity (headless Blender).
# Spec §Lane P: rank-tier bone-socket props. Sources are the two RETAINED
# Abyssal-Surge prop meshes (blade .03, relic .05 — same assets the original
# runtime shipped as PROP_BLADE_MESH / PROP_RELIC_MESH) plus a cloak authored
# here procedurally (simple mantle sheet — no delete-marked sources §Non-Goals).
#
#   blender -b --factory-startup --python-exit-code 1 \
#     -P tools/blender/convert_equip_props.py -- \
#     --blade <blade.glb> --relic <relic.glb> --outdir Assets/Art/Props
#
# Normalization contract (socket space):
#  - weapon (RightHand): grip at origin, blade along +Y, LONGEST span = HEIGHT.
#  - lantern (LeftHand): handle top at origin, body hangs -Y.
#  - cloak (Chest): top edge at origin, sheet hangs -Y behind the back (+Z tilt).
# Sizes are in meters at character scale (actor ~1.8 m tall). "fine" tier =
# same mesh, 1.22x scale + ember accent material — silhouette upgrade without
# new sources. Triangle budget ≤800/prop (§T4); meshes are decimated if over.
import argparse
import sys

import bpy
from mathutils import Matrix, Vector

TRI_BUDGET = 800


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--blade", required=True)
    parser.add_argument("--relic", required=True)
    parser.add_argument("--outdir", required=True)
    return parser.parse_args(argv)


def import_glb(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    return [o for o in bpy.data.objects if o not in before and o.type == "MESH"]


def join_meshes(objects, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    if len(objects) > 1:
        bpy.ops.object.join()
    merged = bpy.context.view_layer.objects.active
    merged.name = name
    return merged


def world_bounds(obj):
    corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    hi = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    return lo, hi


def apply_all(obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


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


def normalize(obj, height, anchor):
    """Uniform-scale longest axis to `height`; move anchor point to origin.
    anchor: 'grip-bottom' (weapon), 'top' (lantern/cloak)."""
    apply_all(obj)
    lo, hi = world_bounds(obj)
    span = max(hi.x - lo.x, hi.y - lo.y, hi.z - lo.z)
    if span <= 0:
        raise SystemExit(f"FATAL: degenerate prop bounds for {obj.name}")
    scale = height / span
    obj.matrix_world = Matrix.Scale(scale, 4) @ obj.matrix_world
    apply_all(obj)
    lo, hi = world_bounds(obj)
    center = (lo + hi) * 0.5
    # Longest axis -> +Z in Blender (exports as +Y in Unity via FBX).
    spans = {"X": hi.x - lo.x, "Y": hi.y - lo.y, "Z": hi.z - lo.z}
    longest = max(spans, key=spans.get)
    if longest == "X":
        obj.matrix_world = Matrix.Rotation(1.5707963, 4, "Y") @ obj.matrix_world
    elif longest == "Y":
        obj.matrix_world = Matrix.Rotation(1.5707963, 4, "X") @ obj.matrix_world
    apply_all(obj)
    lo, hi = world_bounds(obj)
    center = (lo + hi) * 0.5
    if anchor == "grip-bottom":
        shift = Vector((-center.x, -center.y, -lo.z))
    else:  # 'top'
        shift = Vector((-center.x, -center.y, -hi.z))
    obj.matrix_world = Matrix.Translation(shift) @ obj.matrix_world
    apply_all(obj)


def solid_material(name, color, emission=None):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    bsdf = material.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.55
    if emission is not None:
        # Blender 4/5: emission lives on 'Emission Color' + 'Emission Strength'.
        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 2.0
    return material


def retint(obj, material):
    obj.data.materials.clear()
    obj.data.materials.append(material)


def build_cloak():
    """Procedural mantle: subdivided plane, gentle curve, hangs -Z from top."""
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=7, y_subdivisions=9, size=1)
    cloak = bpy.context.view_layer.objects.active
    cloak.name = "cloak"
    # Shape: 0.62 wide, 0.78 long, slight backward bow (y).
    cloak.scale = Vector((0.31, 0.39, 1.0))
    apply_all(cloak)
    mesh = cloak.data
    for vertex in mesh.vertices:
        t = (vertex.co.y + 0.39) / 0.78          # 0 bottom .. 1 top
        vertex.co.z = -0.085 * (1.0 - t) ** 1.5  # bow away from the back
        vertex.co.x *= 0.55 + 0.45 * t           # taper toward the hem
    # Rotate: plane XY -> hang along -Z from the top edge.
    cloak.matrix_world = Matrix.Rotation(-1.5707963, 4, "X") @ cloak.matrix_world
    apply_all(cloak)
    lo, hi = world_bounds(cloak)
    cloak.matrix_world = Matrix.Translation(
        Vector((-(lo.x + hi.x) * 0.5, -(lo.y + hi.y) * 0.5, -hi.z))) @ cloak.matrix_world
    apply_all(cloak)
    solidify = cloak.modifiers.new("solidify", "SOLIDIFY")
    solidify.thickness = 0.012
    bpy.context.view_layer.objects.active = cloak
    bpy.ops.object.modifier_apply(modifier=solidify.name)
    return cloak


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
    cyan = solid_material("prop-cyan", (0.10, 0.16, 0.20), emission=(0.17, 0.68, 0.84))

    results = []

    # --- weapon: retained blade mesh, grip at origin, blade up ------------
    blade = join_meshes(import_glb(args.blade), "weapon")
    normalize(blade, height=0.92, anchor="grip-bottom")
    tris = decimate_to_budget(blade)
    retint(blade, charcoal)
    export_fbx(blade, f"{args.outdir}/equip-weapon-basic.fbx")
    results.append(("equip-weapon-basic", tris))
    retint(blade, ember)
    blade.matrix_world = Matrix.Scale(1.22, 4) @ blade.matrix_world
    apply_all(blade)
    export_fbx(blade, f"{args.outdir}/equip-weapon-fine.fbx")
    results.append(("equip-weapon-fine", triangle_count(blade)))

    # --- lantern: retained relic mesh, hangs below the hand ---------------
    relic = join_meshes(import_glb(args.relic), "lantern")
    normalize(relic, height=0.34, anchor="top")
    tris = decimate_to_budget(relic)
    retint(relic, charcoal)
    export_fbx(relic, f"{args.outdir}/equip-lantern-basic.fbx")
    results.append(("equip-lantern-basic", tris))
    retint(relic, cyan)
    relic.matrix_world = Matrix.Scale(1.22, 4) @ relic.matrix_world
    apply_all(relic)
    export_fbx(relic, f"{args.outdir}/equip-lantern-fine.fbx")
    results.append(("equip-lantern-fine", triangle_count(relic)))

    # --- cloak: authored mantle sheet -------------------------------------
    cloak = build_cloak()
    tris = triangle_count(cloak)
    retint(cloak, charcoal)
    export_fbx(cloak, f"{args.outdir}/equip-cloak-basic.fbx")
    results.append(("equip-cloak-basic", tris))
    retint(cloak, ember)
    cloak.matrix_world = Matrix.Scale(1.18, 4) @ cloak.matrix_world
    apply_all(cloak)
    export_fbx(cloak, f"{args.outdir}/equip-cloak-fine.fbx")
    results.append(("equip-cloak-fine", triangle_count(cloak)))

    total = sum(t for _, t in results)
    for name, tris in results:
        print(f"PROP {name}: {tris} tris")
    print(f"PROP TOTAL: {total} tris (budget {TRI_BUDGET}/prop)")
    over = [name for name, tris in results if tris > TRI_BUDGET]
    if over:
        raise SystemExit(f"FATAL: over budget: {over}")


main()
