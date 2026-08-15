#!/usr/bin/env python3
"""Recover the dungeon kit's PBR maps from the source GLBs.

WHY THIS EXISTS. The kit ships UNTEXTURED — twenty parts sharing one flat
`kit-stone.mat` with zero texture maps — and that was never a decision about art.
Every source GLB already carries UVs and its own baked albedo, normal and
metallicRoughness (measured: 20/20 parts, 4096^2 JPEG, 52.5 MB embedded). Two steps
threw them away:

  * tools/blender/kit_from_glb.py exports FBX with embed_textures=False and
    path_mode="COPY", and nothing was actually copied — Assets/Art/Environment holds
    zero image files.
  * Assets/Editor/DungeonKitImportPipeline.cs then sets materialImportMode=None and
    force-assigns the flat material, so even a textured FBX would have been ignored.

So this is a RECOVERY, not an authoring pass: the maps that match each mesh's own
unwrap already exist and only need to reach the project.

WHAT IS DROPPED, AND WHY. metallicRoughness is not extracted. The kit is stone:
metallic is 0 everywhere, and roughness is well served by the material's constant.
Dropping it removes a third of the texture payload for no visible loss — and payload
is the binding constraint here, not fidelity (build data 69.1 MB against a 120 MB
ceiling, CLAUDE.md §1).

Usage:
    python3 tools/env/extract_kit_textures.py --size 1024
    python3 tools/env/extract_kit_textures.py --size 512 --only wall-straight
"""
import argparse
import io
import json
import struct
from pathlib import Path

from PIL import Image

SOURCE_DIR = Path("_workspace/current/engineering/mesh-gen/kit")
OUT_DIR = Path("Assets/Art/Environment/Textures")

# glTF material fields -> our suffix. Order matters only for reporting.
WANTED = [
    ("baseColorTexture", "albedo"),
    ("normalTexture", "normal"),
]


def read_glb(path: Path):
    """Return (json_chunk, binary_chunk). GLB is a 12-byte header then chunks."""
    with path.open("rb") as handle:
        magic, _version, _length = struct.unpack("<III", handle.read(12))
        if magic != 0x46546C67:
            raise ValueError(f"not a GLB: {path}")
        json_length, _json_type = struct.unpack("<II", handle.read(8))
        document = json.loads(handle.read(json_length))
        # The BIN chunk is optional in the spec but always present for these exports.
        header = handle.read(8)
        if len(header) < 8:
            return document, b""
        bin_length, _bin_type = struct.unpack("<II", header)
        return document, handle.read(bin_length)


def image_bytes(document, binary, image_index: int) -> bytes:
    image = document["images"][image_index]
    view = document["bufferViews"][image["bufferView"]]
    start = view.get("byteOffset", 0)
    return binary[start:start + view["byteLength"]]


def texture_image_index(document, texture_index: int) -> int:
    return document["textures"][texture_index]["source"]


def extract(path: Path, size: int) -> list[str]:
    document, binary = read_glb(path)
    materials = document.get("materials", [])
    if not materials:
        return []

    material = materials[0]
    pbr = material.get("pbrMetallicRoughness", {})
    written = []

    for field, suffix in WANTED:
        source = pbr.get(field) if field in pbr else material.get(field)
        if source is None:
            continue
        raw = image_bytes(document, binary, texture_image_index(document, source["index"]))
        image = Image.open(io.BytesIO(raw))

        # LANCZOS, not the default: these are 4096 originals going to 512/1024, a 4-8x
        # reduction where a box filter visibly aliases the carved detail that is the
        # entire point of recovering them.
        if image.size != (size, size):
            image = image.resize((size, size), Image.LANCZOS)

        # PNG, not the source JPEG. Unity re-compresses to a GPU format on import, so
        # the intermediate is a lossless carrier — keeping JPEG here would stack a
        # second generation of block artefacts under the GPU compressor.
        out = OUT_DIR / f"kit-{path.stem}-{suffix}.png"
        image.convert("RGB").save(out, optimize=True)
        written.append(f"{out.name} {out.stat().st_size / 1024:.0f}KB")

    return written


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--size", type=int, default=1024,
                        help="output square size; the WebGL ceiling is 1024 (CLAUDE.md §1)")
    parser.add_argument("--only", default=None, help="single part stem, e.g. wall-straight")
    args = parser.parse_args()

    if args.size > 1024:
        print(f"refusing {args.size}: WebGL texture ceiling is 1024")
        return 2

    sources = sorted(SOURCE_DIR.glob("*.glb"))
    if args.only:
        sources = [p for p in sources if p.stem == args.only]
    if not sources:
        print(f"no GLB found under {SOURCE_DIR}")
        return 1

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    total = 0
    for path in sources:
        written = extract(path, args.size)
        total += len(written)
        print(f"{path.stem:20} {' '.join(written) if written else '(no maps)'}")

    megabytes = sum(p.stat().st_size for p in OUT_DIR.glob("*.png")) / 1024 / 1024
    print(f"\n{total} maps at {args.size}^2 -> {OUT_DIR} ({megabytes:.1f} MB on disk)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
