#!/usr/bin/env python3
"""Convert the kit's recovered PBR albedos into cel-shading-friendly maps.

WHY A TRANSFORM AND NOT A NEW gti GENERATION. Everything else in the toon pass was
generated from a prompt, and the stage floors were regenerated that way successfully.
The kit cannot be: each part carries its OWN unwrap and a texture baked to it
(recovered by extract_kit_textures.py). A prompt-generated tiling texture has no
relationship to that layout, so it would land as misaligned detail — a sarcophagus
lid's carving running across its side. The UV map is the constraint, and a transform
of the existing bake is the only operation that respects it.

WHAT THE TRANSFORM DOES, AND WHY EACH STEP IS THERE.

  posterise    Photographic albedo carries continuous grain. A cel shader quantises
               LIGHT into 2 bands, and continuous albedo noise underneath fights that
               — it reads as dirt on a flat surface rather than as material. Reducing
               the albedo to a few value steps makes the two quantisations agree.

  saturate     Toon art carries identity in hue, not in micro-contrast. A small chroma
               lift keeps the stone families (violet chancel, gold verdict, iron
               bastion) distinguishable once the value range has been flattened.

  edge ink     The outline pass draws SILHOUETTES only, so interior definition has to
               live in the texture. A dark line on luminance edges gives the carving
               and the masonry joints back the definition the posterise removed.

NORMAL MAPS ARE NOT TOUCHED HERE — they are unbound from the materials instead. The
toon shader declares _BumpMap and never samples it, so under this art direction the
20 normal maps were dead weight in the build. The files stay in the repo: the PBR
revert path is `extract_kit_textures.py`, and deleting them would make that path a
regeneration rather than a checkout.

Usage:
    python3 tools/env/toonify_kit_albedo.py            # writes *-albedo.png in place
    python3 tools/env/toonify_kit_albedo.py --dry-run
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

TEXTURE_DIR = Path("Assets/Art/Environment/Textures")

# The same transform serves the TERRAIN albedos, and for the same reason. Measured
# 2026-08-13: the arena's apron map (albedo-mat-cinder-span-apron.png) is a
# photographic charcoal-and-lava plate — continuous grain, baked lighting variation,
# a soft corner vignette — and the other session moved terrain onto the toon SHADER
# without touching those TEXTURES. A photo under a cel shader reads as a photo pasted
# on the ground, which is the concept break the user named as "the overlaid image on
# the stage floor". These maps are baked to the terrain's own unwrap exactly like the
# kit's, so a prompt-generated tile cannot replace them and a transform is again the
# only operation that respects the UV.
TERRAIN_GLOB = "Assets/Art/Terrain/**/albedo*.png"

# Four steps, not two. The SHADER already quantises light into two bands; if the
# albedo also collapsed to two, a whole part could land on a single flat colour and
# lose its silhouette against the floor. Four keeps material variation while removing
# the continuous grain.
# Quantise over the map's OWN range, not over 0..1. Measured: the kit albedos average
# luminance 59/255, so a fixed 0..1 posterise put almost every pixel in the bottom step
# and returned a near-black sheet. The first version of this tool did exactly that and
# had to be reverted off 20 committed textures.
LEVELS = 5
SATURATION = 1.18
# Edge ink is OFF by default. FIND_EDGES on a photographic bake fires on the grain,
# not on the carving, so it deposited speckle across the whole map instead of tracing
# masonry joints. Kept as an option rather than deleted, because the idea is right for
# a map with clean linework — it is wrong for this input.
EDGE_STRENGTH = 0.0
EDGE_THRESHOLD = 0.08


def toonify(image: Image.Image) -> Image.Image:
    rgb = np.asarray(image.convert("RGB"), dtype=np.float32) / 255.0

    # Posterise on VALUE, not per channel. Per-channel quantisation shifts hue at the
    # step boundaries and turns grey stone faintly green or magenta; scaling the
    # original colour by a quantised luminance keeps the hue exactly.
    luma = rgb @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)

    # Normalise to the map's own 2nd..98th percentile before stepping, then map back.
    # Percentiles rather than min/max so one bright speck cannot define the range.
    low, high = np.percentile(luma, 2.0), np.percentile(luma, 98.0)
    span = max(1e-3, float(high - low))
    unit = np.clip((luma - low) / span, 0.0, 1.0)
    stepped_unit = np.floor(unit * LEVELS) / max(1, LEVELS - 1)
    stepped = np.clip(low + stepped_unit * span, 0.02, 1.0)

    scale = stepped / np.maximum(luma, 1e-4)
    out = np.clip(rgb * scale[..., None], 0.0, 1.0)

    grey = out @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
    out = np.clip(grey[..., None] + (out - grey[..., None]) * SATURATION, 0.0, 1.0)

    # Ink the luminance edges of the ORIGINAL, not of the posterised result: the
    # posterise creates hard steps everywhere, so edges found afterwards would trace
    # its own banding instead of the carving that was in the bake.
    if EDGE_STRENGTH <= 0.0:
        return Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8), mode="RGB")

    edges = np.asarray(
        Image.fromarray((luma * 255).astype(np.uint8)).filter(ImageFilter.FIND_EDGES),
        dtype=np.float32,
    ) / 255.0
    ink = np.clip((edges - EDGE_THRESHOLD) / max(1e-3, 1.0 - EDGE_THRESHOLD), 0.0, 1.0)
    out *= (1.0 - ink[..., None] * EDGE_STRENGTH)

    return Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8), mode="RGB")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--terrain", action="store_true",
                        help="operate on the terrain albedos instead of the kit's")
    parser.add_argument("--out", type=Path,
                        help="write to this directory instead of in place. STRONGLY "
                             "preferred: this tool destroyed 20 committed textures "
                             "once by writing in place before anyone had looked at "
                             "the output.")
    args = parser.parse_args()

    if args.terrain:
        albedos = sorted(Path(".").glob(TERRAIN_GLOB))
        label = "terrain"
    else:
        albedos = sorted(TEXTURE_DIR.glob("kit-*-albedo.png"))
        label = "kit"
    if not albedos:
        print(f"no {label} albedo maps found")
        return 1

    if args.out:
        args.out.mkdir(parents=True, exist_ok=True)

    for path in albedos:
        with Image.open(path) as image:
            result = toonify(image)
        if args.dry_run:
            print(f"would write {path.name} ({result.size[0]}^2)")
            continue
        target = (args.out / path.name) if args.out else path
        result.save(target, optimize=True)
        print(f"{target}  {target.stat().st_size / 1024:.0f} KB")

    print(f"\n{len(albedos)} {label} albedo maps toonified")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
