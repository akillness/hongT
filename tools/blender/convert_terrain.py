# Terrain GLB -> FBX for Unity (headless Blender).
# Usage:
#   blender -b --factory-startup --python-exit-code 1 \
#     -P tools/blender/convert_terrain.py -- \
#     --floor <floor.glb> [--extra <props.glb> ...] --out <out.fbx>
#
# The floor GLB defines the registration transform: its bbox is scaled so the
# X span fits TARGET_X_SPAN meters, centered at the origin, floor top at y=0.
# Extra packs (props/features) are authored in the same renderer-world space,
# so the SAME transform keeps them registered with the floor.
import argparse
import math
import sys

import bpy
from mathutils import Matrix, Vector

TARGET_X_SPAN = 17.0   # arena 15.36m wide; slight apron bleed
TARGET_Y_COVER = 10.44  # arena 10.24m deep; floor must cover actor walk area


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--floor", required=True)
    parser.add_argument("--extra", action="append", default=[])
    # T-b (deep-interview §Lane T-b): fused single-mesh candidate GLB to split
    # into connectivity islands, registered INDEPENDENTLY (own bbox) because
    # candidate packs are authored in their own normalized space, not the
    # floor's renderer-world space.
    parser.add_argument("--parts", default=None)
    parser.add_argument("--parts-prefix", default="part")
    parser.add_argument("--parts-keep", type=int, default=48)
    parser.add_argument("--parts-min-tris", type=int, default=150)
    parser.add_argument("--out", required=True)
    return parser.parse_args(argv)


def import_glb(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    return [o for o in bpy.data.objects if o not in before]


def world_bounds(objects):
    lo = Vector((math.inf, math.inf, math.inf))
    hi = Vector((-math.inf, -math.inf, -math.inf))
    for obj in objects:
        if obj.type != "MESH":
            continue
        for corner in obj.bound_box:
            w = obj.matrix_world @ Vector(corner)
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
    if not math.isfinite(lo.x):
        raise SystemExit("FATAL: no mesh bounds")
    return lo, hi


def main():
    args = parse_args()
    bpy.ops.wm.read_factory_settings(use_empty=True)

    floor_objects = import_glb(args.floor)
    lo, hi = world_bounds(floor_objects)
    span_x = hi.x - lo.x
    span_y = hi.y - lo.y
    if span_x <= 0 or span_y <= 0:
        raise SystemExit("FATAL: degenerate floor span")
    # Cover rule: X fits the frame, but never leave arena depth uncovered.
    # Wide overshoot past the camera frustum is harmless backdrop bleed.
    scale = max(TARGET_X_SPAN / span_x, TARGET_Y_COVER / span_y)
    center = (lo + hi) * 0.5
    # Registration transform: uniform scale, XY center -> origin, top -> y=0.
    # glTF import is Y-up -> Blender Z-up; the floor plane is XY in Blender.
    transform = (
        Matrix.Scale(scale, 4)
        @ Matrix.Translation(Vector((-center.x, -center.y, -hi.z)))
    )

    all_objects = list(floor_objects)
    for extra in args.extra:
        all_objects += import_glb(extra)

    # --- T-b: connectivity split of a fused candidate mesh ------------------
    # Authoring-time separation (§3 contract: never split at runtime). The
    # candidate cloud gets its OWN registration: X span fit to the arena
    # frame, XY centered, then each kept island grounded (per-island min-z
    # -> 0) so nothing floats where its authored terraces were removed.
    part_objects = []
    if args.parts:
        fused = import_glb(args.parts)
        meshes = [o for o in fused if o.type == "MESH"]
        if not meshes:
            raise SystemExit("FATAL: --parts GLB has no mesh")
        bpy.ops.object.select_all(action="DESELECT")
        for m in meshes:
            m.select_set(True)
        bpy.context.view_layer.objects.active = meshes[0]
        bpy.ops.mesh.separate(type="LOOSE")
        islands = [o for o in bpy.data.objects
                   if o.type == "MESH" and o not in all_objects]
        # Register the whole cloud once so relative layout survives.
        plo, phi = world_bounds(islands)
        pspan_x = phi.x - plo.x
        if pspan_x <= 0:
            raise SystemExit("FATAL: degenerate parts span")
        pscale = TARGET_X_SPAN / pspan_x
        pcenter = (plo + phi) * 0.5
        ptransform = (
            Matrix.Scale(pscale, 4)
            @ Matrix.Translation(Vector((-pcenter.x, -pcenter.y, 0.0)))
        )
        for island in islands:
            island.matrix_world = ptransform @ island.matrix_world
        # Deterministic keep-list: triangle count desc, then world position.
        def island_key(o):
            o.data.calc_loop_triangles()
            ilo, ihi = world_bounds([o])
            return (-len(o.data.loop_triangles), round(ilo.x, 3), round(ilo.y, 3))
        islands.sort(key=island_key)
        kept = []
        for island in islands:
            island.data.calc_loop_triangles()
            if len(kept) < args.parts_keep and \
               len(island.data.loop_triangles) >= args.parts_min_tris:
                kept.append(island)
            else:
                bpy.data.objects.remove(island, do_unlink=True)
        stage = args.out.rsplit("/", 1)[-1].rsplit(".", 1)[0]
        for index, island in enumerate(kept):
            ilo, ihi = world_bounds([island])
            # Ground each island: min-z -> floor top (0). Its authored terrace
            # was part of the removed candidate floor - floating reads broken.
            island.matrix_world = (
                Matrix.Translation(Vector((0.0, 0.0, -ilo.z))) @ island.matrix_world
            )
            island.name = f"{stage}-{args.parts_prefix}-{index + 1:03d}"
            part_objects.append(island)
        all_objects += part_objects

    roots = [o for o in floor_objects
             if o.parent is None or o.parent not in floor_objects]
    for extra_obj in all_objects:
        if extra_obj in part_objects or extra_obj in floor_objects:
            continue
        if extra_obj.parent is None or extra_obj.parent not in all_objects:
            roots.append(extra_obj)

    for root in roots:
        root.matrix_world = transform @ root.matrix_world

    # Deterministic material->albedo mapping. Blender dedups image names
    # ("texture_diffuse", "texture_diffuse.001"), and FBX extraction in Unity
    # loses which material owned which image. Rename every base-color image
    # after its (sanitized) material and write a sidecar manifest so the Unity
    # importer can assign albedos without guessing.
    def sanitize(name):
        return "".join(c if c.isalnum() else "-" for c in name).strip("-").lower()

    def pixels_of(img):
        import array
        buffer = array.array("f", [0.0]) * (img.size[0] * img.size[1] * 4)
        img.pixels.foreach_get(buffer)
        return buffer

    manifest = {}
    seen_images = {}   # original image name -> copy image name
    for obj in all_objects:
        if obj.type != "MESH":
            continue
        for slot in obj.material_slots:
            mat = slot.material
            if mat is None or not mat.use_nodes or mat.name in manifest:
                continue
            image = None
            for node in mat.node_tree.nodes:
                if node.type == "BSDF_PRINCIPLED":
                    base = node.inputs.get("Base Color")
                    if base is not None:
                        for link in base.links:
                            if link.from_node.type == "TEX_IMAGE" and link.from_node.image:
                                image = link.from_node.image
                                break
                if image is not None:
                    break
            if image is None:
                continue
            if image.name in seen_images:
                manifest[mat.name] = seen_images[image.name]
                continue
            if image.name.startswith("albedo-"):
                # Node already swapped to a converted copy by an earlier
                # material sharing the same source image.
                manifest[mat.name] = image.name
                continue
            new_name = f"albedo-{sanitize(mat.name)}"
            # GLB albedos are often WebP; Unity cannot import WebP, and
            # image.save() writes the PACKED bytes verbatim (still WebP).
            # Copy decoded pixels into a fresh image -> guaranteed PNG encode.
            width, height = image.size
            copy = bpy.data.images.new(new_name, width=width, height=height,
                                       alpha=True)
            copy.colorspace_settings.name = image.colorspace_settings.name
            copy.pixels.foreach_set(pixels_of(image))
            copy.file_format = "PNG"
            copy.pack()
            # Swap every texture node using the old image over to the copy.
            for user_mat in bpy.data.materials:
                if not user_mat.use_nodes:
                    continue
                for node in user_mat.node_tree.nodes:
                    if node.type == "TEX_IMAGE" and node.image is image:
                        node.image = copy
            seen_images[image.name] = new_name
            manifest[mat.name] = new_name

    # Unlit runtime: strip every non-albedo texture link so FBX embed/export
    # skips normal/orm/metallic maps entirely (dead weight, WebP anyway).
    for material in bpy.data.materials:
        if not material.use_nodes:
            continue
        for node in list(material.node_tree.nodes):
            if node.type != "TEX_IMAGE" or node.image is None:
                continue
            if not node.image.name.startswith("albedo-"):
                material.node_tree.nodes.remove(node)

    bpy.ops.object.select_all(action="DESELECT")
    for obj in all_objects:
        obj.select_set(True)

    bpy.ops.export_scene.fbx(
        filepath=args.out,
        use_selection=True,
        object_types={"MESH"},
        path_mode="COPY",
        embed_textures=True,
        mesh_smooth_type="FACE",
        apply_scale_options="FBX_SCALE_ALL",
    )
    import json
    manifest_path = args.out.rsplit(".", 1)[0] + ".albedo.json"
    with open(manifest_path, "w", encoding="utf-8") as handle:
        json.dump(manifest, handle, ensure_ascii=False, indent=1)
    lo2, hi2 = world_bounds(all_objects)
    print(f"TERRAIN OK {args.out}: span=({hi2.x-lo2.x:.2f},{hi2.y-lo2.y:.2f},{hi2.z-lo2.z:.2f}) "
          f"top={hi2.z:.3f} objects={len(all_objects)} materials={len(manifest)}")


main()
