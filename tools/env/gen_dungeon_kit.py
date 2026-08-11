#!/usr/bin/env python3
"""AMENDMENT #17 dungeon architecture kit — Higgsfield text-to-3D.

Design truth: _workspace/current/design/dungeon-interior-spec.md §5.

INCREMENTAL BY CONSTRUCTION (CLAUDE.md §4p). A bare invocation is REFUSED with
exit 2; you must name the parts you want, or pass --all for a deliberate full
re-derivation. cycle-7 lost `ui-button` (256x96 -> 256x106, corrupting every
9-slice border) because a matter script had no such gate while the generator it
paired with did. A generated part that already exists on disk is skipped even
when named, unless --force.

Usage:
    python3 tools/env/gen_dungeon_kit.py wall-straight wall-corner
    python3 tools/env/gen_dungeon_kit.py --class arch
    python3 tools/env/gen_dungeon_kit.py --all
    python3 tools/env/gen_dungeon_kit.py --list
    python3 tools/env/gen_dungeon_kit.py --all --dry-run

Requires `higgsfield auth login`. Raw GLBs land in RAW_DIR; the Blender pass
(tools/blender/kit_from_glb.py) turns them into the Unity FBXs.
"""
import argparse
import json
import os
import subprocess
import sys
import urllib.request
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
RAW_DIR = REPO / "_workspace/current/engineering/mesh-gen/kit"
# Overridable so several class-scoped runs can go in parallel without doing
# read-modify-write on one JSON and losing each other's entries. Generation is
# ~8-10 min per part and sequential over 28 parts is a four-hour wall.
#
# The GLBs on disk are the real artifact; a provenance shard is a record OF them,
# so shards can be concatenated afterwards and a lost one is rebuildable from the
# PARTS table plus the file that is sitting there.
PROVENANCE = Path(os.environ.get(
    "KIT_PROVENANCE", REPO / "docs/provenance/dungeon-kit.json"))

# Shared style suffix. Every part carries it verbatim so the kit reads as one
# set — the intro-reel lesson (docs/provenance/intro-video.json) was that a
# text-only style anchor drifts, but for hard-surface stone with no subject
# identity to preserve it is enough, and text-to-3D takes no reference image.
STYLE = (
    "dark gothic dungeon stonework, charred basalt and ash-grey granite, "
    "ember-scorched cracked edges, heavily weathered, game-ready low-poly asset, "
    "clean quad-friendly topology, PBR textures, neutral even lighting, "
    "isolated object on plain background, no ground plane, no character"
)

# The pixel-art rejection is on record: a pixel-art-leaning clip (seedance job
# 5f9618cb) clashed with this game's photoreal gothic tone and was cut
# (CLAUDE.md §3, 2026-08-10). Naming the clash here keeps the kit from
# rediscovering it 28 times.
NEGATIVE = (
    "pixel art, voxel, cartoon, cel shaded, bright saturated colors, "
    "clean new polished stone, sci-fi, plastic, character, person, "
    "ground plane, scene background"
)

# MODEL CHOICE. The spec named meshy_v6_text_to_3d; it is unreachable through
# this CLI. Two of its constraint expressions come back as
#   Error: Unsupported validation rule for meshy_v6_text_to_3d:
#          !params.enable_animation || params.enable_rigging
# which is the CLI failing to EVALUATE the rule, not the rule rejecting our
# params — `generate cost` fails the same way on a different rule, so no
# argument combination gets past it. tripo_3d has no constraint block, priced
# cleanly at 5 credits, and exposes face_limit, which is the one meshy parameter
# this kit actually needed (target_polycount).
MODEL = "tripo_3d"

# face_limit is the tri budget from the spec table, enforced at the generator
# rather than trimmed afterwards: a decimate pass on stone silhouettes eats the
# very edge chipping that makes them read as ruins.
ARCH_TRIS = 1500
OBSTACLE_TRIS = 1200

# The spec budgeted 400 for decor. tripo_3d refuses it:
#   Error: face_limit: Input should be greater than or equal to 1000
# All ten decor parts failed on that before the floor was found. 1000 is the
# tool's minimum, so the decor line costs 10,000 tri instead of 4,000 and the kit
# total moves 28,600 -> 34,600 — still a rounding error against the 120 MB WebGL
# budget, and cheaper than adding a Blender decimate pass whose whole job would
# be to undo geometry we paid to generate.
DECOR_TRIS = 1000

PARTS = [
    # --- architecture kit (10) --------------------------------------------
    ("arch", "wall-straight", ARCH_TRIS,
     "a straight modular dungeon wall segment, flat back face, carved stone blocks"),
    ("arch", "wall-corner", ARCH_TRIS,
     "a modular dungeon wall corner piece, ninety degree turn, carved stone blocks"),
    ("arch", "wall-arch", ARCH_TRIS,
     "a gothic pointed archway doorway in a stone wall, open passage"),
    ("arch", "column-round", ARCH_TRIS,
     "a tall round gothic stone column with a carved capital and base"),
    ("arch", "column-broken", ARCH_TRIS,
     "a broken snapped stone column stump with rubble at its base"),
    ("arch", "floor-tile-plain", ARCH_TRIS,
     "a square flagstone floor tile panel, worn flat surface, subtle cracks"),
    ("arch", "floor-tile-sigil", ARCH_TRIS,
     "a square stone floor tile panel carved with a circular occult sigil"),
    ("arch", "stair-block", ARCH_TRIS,
     "a short flight of three worn stone steps, straight run"),
    ("arch", "rail-baluster", ARCH_TRIS,
     "a low stone balustrade railing section with turned balusters"),
    ("arch", "buttress", ARCH_TRIS,
     "a gothic flying buttress support pier of carved stone"),

    # --- collision obstacles (8) ------------------------------------------
    ("obstacle", "sarcophagus", OBSTACLE_TRIS,
     "a stone sarcophagus coffin with a carved lid, waist height"),
    ("obstacle", "column-fallen", OBSTACLE_TRIS,
     "a toppled stone column lying on its side, cracked into two pieces"),
    ("obstacle", "rubble-heap", OBSTACLE_TRIS,
     "a heap of broken masonry rubble and shattered stone blocks"),
    ("obstacle", "altar-plinth", OBSTACLE_TRIS,
     "a squat stone altar plinth pedestal, carved sides, flat top"),
    ("obstacle", "brazier-great", OBSTACLE_TRIS,
     "a large iron fire brazier bowl on a heavy stone pedestal, unlit"),
    ("obstacle", "arch-collapsed", OBSTACLE_TRIS,
     "a collapsed stone archway, half standing, broken keystone"),
    ("obstacle", "statue-base", OBSTACLE_TRIS,
     "a stone statue pedestal with the broken feet of a lost figure on top"),
    ("obstacle", "barricade", OBSTACLE_TRIS,
     "a makeshift barricade of stacked stone blocks and splintered timber"),

    # --- decorative props, non-colliding (10) -----------------------------
    ("decor", "candelabra", DECOR_TRIS,
     "a tall wrought iron candelabra with melted candle stubs"),
    ("decor", "chain-hanging", DECOR_TRIS,
     "a length of heavy rusted iron chain hanging in a loop"),
    ("decor", "banner-torn", DECOR_TRIS,
     "a tattered hanging cloth banner on a rod, frayed and burned edges"),
    ("decor", "bones-pile", DECOR_TRIS,
     "a small scattered pile of old bones and a cracked skull"),
    ("decor", "tile-broken", DECOR_TRIS,
     "a few broken floor tiles and loose stone shards lying flat"),
    ("decor", "censer", DECOR_TRIS,
     "a hanging brass incense censer on a short chain"),
    ("decor", "bookshelf-ruined", DECOR_TRIS,
     "a collapsed wooden bookshelf with scattered rotted books"),
    ("decor", "standard-flag", DECOR_TRIS,
     "a battle standard flag on a leaning wooden pole, weathered cloth"),
    ("decor", "debris-small", DECOR_TRIS,
     "a small scatter of stone chips, gravel and dust"),
    ("decor", "oil-slick", DECOR_TRIS,
     "a flat spilled pool of dark viscous oil on stone, thin puddle"),
]

BY_NAME = {name: (cls, name, tris, desc) for cls, name, tris, desc in PARTS}


def raw_path(name):
    return RAW_DIR / f"{name}.glb"


def build_prompt(desc):
    return f"{desc}. {STYLE}"


def submit(name, tris, desc, dry_run):
    """Create one job and block until it finishes. Returns the result URL."""
    prompt = build_prompt(desc)
    cmd = [
        "higgsfield", "generate", "create", MODEL,
        "--prompt", prompt,
        "--negative_prompt", NEGATIVE,
        "--face_limit", str(tris),
        "--geometry_quality", "detailed",
        "--texture", "true",
        "--texture_quality", "detailed",
        "--pbr", "true",
        "--wait", "--wait-timeout", "20m",
        "--json",
    ]
    if dry_run:
        print(f"  DRY-RUN {name}: {' '.join(cmd[:6])} ... [{tris} tris]")
        return None

    done = subprocess.run(cmd, capture_output=True, text=True)
    if done.returncode != 0:
        print(f"  FAIL {name}: {done.stderr.strip()[:400]}", file=sys.stderr)
        return None
    try:
        payload = json.loads(done.stdout)
    except json.JSONDecodeError:
        print(f"  FAIL {name}: unparsable response {done.stdout[:200]}", file=sys.stderr)
        return None
    return payload


def extract_url(payload):
    """Pull the first .glb result out of whatever shape the CLI returned."""
    stack = [payload]
    while stack:
        node = stack.pop()
        if isinstance(node, str) and node.startswith("http") and ".glb" in node:
            return node
        if isinstance(node, dict):
            stack.extend(node.values())
        elif isinstance(node, list):
            stack.extend(node)
    return None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("names", nargs="*", help="part names to generate")
    parser.add_argument("--all", action="store_true", help="deliberate full re-derivation")
    parser.add_argument("--class", dest="cls", choices=["arch", "obstacle", "decor"])
    parser.add_argument("--list", action="store_true")
    parser.add_argument("--force", action="store_true", help="regenerate parts already on disk")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    if args.list:
        for cls, name, tris, desc in PARTS:
            mark = "have" if raw_path(name).exists() else "  --"
            print(f"[{mark}] {cls:9} {name:20} {tris:>5} tri  {desc[:60]}")
        return 0

    if args.all:
        wanted = [p for p in PARTS]
    elif args.cls:
        wanted = [p for p in PARTS if p[0] == args.cls]
    elif args.names:
        unknown = [n for n in args.names if n not in BY_NAME]
        if unknown:
            print(f"unknown part(s): {', '.join(unknown)}", file=sys.stderr)
            print("run --list to see the kit", file=sys.stderr)
            return 2
        wanted = [BY_NAME[n] for n in args.names]
    else:
        # THE GATE. Not a usage nicety — a bare run is the exact shape of the
        # cycle-7 accident, where a whole family got rewritten because nobody
        # had to say which member they meant.
        print("refusing a bare run: name the parts, or --class, or --all", file=sys.stderr)
        print("run --list to see the kit", file=sys.stderr)
        return 2

    RAW_DIR.mkdir(parents=True, exist_ok=True)
    PROVENANCE.parent.mkdir(parents=True, exist_ok=True)

    record = {}
    if PROVENANCE.exists():
        record = json.loads(PROVENANCE.read_text())
    record.setdefault("tool", f"higgsfield {MODEL}")
    record.setdefault("negative_prompt", NEGATIVE)
    record.setdefault("style_suffix", STYLE)
    record.setdefault("spec", "_workspace/current/design/dungeon-interior-spec.md §5")
    record.setdefault("parts", {})

    made = skipped = failed = 0
    for cls, name, tris, desc in wanted:
        out = raw_path(name)
        if out.exists() and not args.force:
            print(f"  SKIP {name} (exists)")
            skipped += 1
            continue

        print(f"  GEN  {name} [{cls}, {tris} tri]")
        payload = submit(name, tris, desc, args.dry_run)
        if args.dry_run:
            continue
        if payload is None:
            failed += 1
            continue

        url = extract_url(payload)
        if not url:
            print(f"  FAIL {name}: no glb in result", file=sys.stderr)
            failed += 1
            continue

        urllib.request.urlretrieve(url, out)
        size = out.stat().st_size
        print(f"  OK   {name} -> {out.relative_to(REPO)} ({size/1024:.0f} KB)")
        record["parts"][name] = {
            "class": cls,
            "target_polycount": tris,
            "prompt": build_prompt(desc),
            "source_url": url,
            "raw": str(out.relative_to(REPO)),
            "bytes": size,
        }
        made += 1
        PROVENANCE.write_text(json.dumps(record, indent=2, ensure_ascii=False) + "\n")

    print(f"\nmade {made}, skipped {skipped}, failed {failed}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
