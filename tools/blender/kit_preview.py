# Preview render for a dungeon-kit GLB (headless Blender).
#
# Renders the part from the SHIPPED dungeon camera angle — 55 degree pitch, the
# same one CameraRig uses — because a part judged from a three-quarter art view
# can read completely differently once it is lying on the arena floor. Silhouette
# and top-face readability are what matter here, and only this pitch shows them.
#
# Usage:
#   blender -b --factory-startup --python-exit-code 1 \
#     -P tools/blender/kit_preview.py -- --glb <in.glb> --out <out.png>
import argparse
import math
import sys

import bpy
from mathutils import Vector

PITCH_DEGREES = 55.0     # CameraRig dungeon pitch
FOV_DEGREES = 42.0       # CameraRig dungeon FOV


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--glb", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--size", type=int, default=512)
    return parser.parse_args(argv)


def main():
    args = parse_args()

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=args.glb)

    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if not meshes:
        print("no mesh in glb", file=sys.stderr)
        return 1

    # Fit the camera to the part's own bounds so parts of wildly different scale
    # all fill the frame the same way — otherwise a 0.4 m censer and a 4 m
    # buttress cannot be compared at a glance.
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for obj in meshes:
        for corner in obj.bound_box:
            world = obj.matrix_world @ Vector(corner)
            lo = Vector((min(lo[i], world[i]) for i in range(3)))
            hi = Vector((max(hi[i], world[i]) for i in range(3)))
    centre = (lo + hi) * 0.5
    radius = max((hi - lo).length * 0.5, 1e-3)

    pitch = math.radians(PITCH_DEGREES)
    distance = radius / math.tan(math.radians(FOV_DEGREES) * 0.5) * 1.35

    # VIEW THE LONG AXIS BROADSIDE. A fixed -Y camera looks straight down the
    # length of any part whose long horizontal axis happens to land on Blender's
    # Y, and an end-on wall is indistinguishable from a narrow column.
    #
    # This is not hypothetical: it happened, and the mesh was nearly discarded
    # and the whole text-to-3D approach nearly abandoned over it. wall-straight
    # measures 0.97 x 0.44 x 0.17 — a correct 2.2:1 wall panel — but glTF is
    # Y-up and the importer maps its length onto Blender -Y, so the fixed camera
    # framed the 0.17 end cap. Right and wrong looked the same from that
    # viewpoint (CLAUDE.md §4m), so the viewpoint has to be derived from the
    # part instead of assumed.
    span = hi - lo
    if span.x >= span.y:
        # Long axis on X: stand off along -Y so X runs across the frame.
        azimuth = Vector((0.0, -1.0))
    else:
        # Long axis on Y: stand off along -X instead.
        azimuth = Vector((-1.0, 0.0))

    camera_data = bpy.data.cameras.new("preview")
    camera_data.angle = math.radians(FOV_DEGREES)
    camera = bpy.data.objects.new("preview", camera_data)
    bpy.context.scene.collection.objects.link(camera)
    camera.location = centre + Vector((
        azimuth.x * distance * math.cos(pitch),
        azimuth.y * distance * math.cos(pitch),
        distance * math.sin(pitch),
    ))
    direction = (centre - camera.location).normalized()
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = camera

    # Three-point-ish rig at low intensity: the parts are judged on silhouette
    # and material value, and a single hard key blows out the ember highlights
    # that are the whole reason for the style suffix.
    for name, vector, energy in (
        ("key", Vector((1.0, -1.0, 1.4)), 4.0),
        ("fill", Vector((-1.2, -0.6, 0.5)), 1.6),
        ("rim", Vector((0.0, 1.4, 0.8)), 2.4),
    ):
        light_data = bpy.data.lights.new(name, type="AREA")
        light_data.energy = energy * max(radius * radius, 0.05) * 40.0
        light_data.size = radius * 2.0
        light = bpy.data.objects.new(name, light_data)
        bpy.context.scene.collection.objects.link(light)
        light.location = centre + vector.normalized() * distance * 0.8
        direction = (centre - light.location).normalized()
        light.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = args.size
    scene.render.resolution_y = args.size
    scene.render.film_transparent = False
    scene.render.filepath = args.out
    scene.world = bpy.data.worlds.new("preview")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (
        0.05, 0.05, 0.06, 1.0)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value = 0.6

    bpy.ops.render.render(write_still=True)
    print(f"rendered {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
