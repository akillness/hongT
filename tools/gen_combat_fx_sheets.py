#!/usr/bin/env python3
"""Combat impact flipbook sheet, deterministic assembly from a generated base.

WHY THE FRAMES ARE ASSEMBLED AND ONLY THE BASE IS GENERATED. Asking an image
model for an aligned NxN grid is unreliable here — tools/gen_terrain_fx_sheets.py
records the same finding, and the character pipeline's atlas attempt was rejected
for drawing section labels onto the sheet. One generated hero image plus
arithmetic gives a grid that is aligned by construction and reproducible byte for
byte from the same base.

GRAYSCALE, NOT COLOUR. VfxDirector's documented split is that the TEXTURE carries
shape and the PER-CALL TINT carries identity (see SpawnScorch). The hit spark has
two identities — ember for a normal hit, gold for a finisher — and a coloured
sheet would fight both. A mask multiplies cleanly into either.

NO BAKED ROTATION. Every frame is the same burst expanding; the spin is applied
per spawn in the View. Baking it would make every hit in the game play the
identical animation, which is the one thing a per-hit effect must not do.

ONE-SHOT, NOT A LOOP. Unlike the terrain flipbook (4x4 at a fixed 12 fps,
1.333 s loop), this sheet is played once across the effect's own lifetime — the
spark lives 0.18 s, so a fixed frame rate would either strobe or never finish.
The View maps remaining-life to frame index.

Usage:
    python3 tools/gen_combat_fx_sheets.py --base <base.png> --out <sheet.png>
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image

# Matches the terrain sheet contract: 4x4 cells of 256 px = a 1024 sheet, which
# is the WebGL texture ceiling this project holds to (CLAUDE.md §1).
GRID = 4
FRAME_SIZE = 256
FRAME_COUNT = GRID * GRID
SHEET_SIZE = GRID * FRAME_SIZE


def load_mask(path: Path, size: int) -> np.ndarray:
    """Generated burst -> single-channel intensity, contrast-stretched.

    The base is drawn on pure black for additive blending, so luminance already
    IS the mask; the stretch only removes the near-black floor the encoder
    leaves behind, which would otherwise show as a faint square edge once the
    quad is tinted and added.
    """
    image = Image.open(path).convert("L").resize((size, size), Image.LANCZOS)
    data = np.asarray(image, dtype=np.float32) / 255.0
    floor = np.percentile(data, 55.0)
    data = np.clip((data - floor) / max(1e-5, 1.0 - floor), 0.0, 1.0)
    return data


def radial_falloff(size: int) -> np.ndarray:
    """Circular window so the burst never shows the cell's square corners.

    Without it a scaled-up frame reaches the cell edge and the quad reads as a
    lit rectangle — the same corner problem the scorch decal solves by having
    alpha fall to zero before the quad's bounds.
    """
    axis = (np.arange(size, dtype=np.float32) + 0.5) / size * 2.0 - 1.0
    dx, dy = np.meshgrid(axis, axis)
    radius = np.sqrt(dx * dx + dy * dy)
    return np.clip(1.0 - (radius - 0.55) / 0.45, 0.0, 1.0) ** 1.5


def scaled(mask: np.ndarray, factor: float) -> np.ndarray:
    """Draw the burst at `factor` of the cell and centre it.

    factor is the VISIBLE SIZE, not a zoom: 0.3 puts a small burst in the middle
    of the cell, 1.0 fills it. The first version divided instead of multiplied,
    which inverted the whole animation — the sheet came out starting at its
    largest and shrinking, a hit playing backwards. The docstring asserted the
    direction and the arithmetic did the opposite, and only looking at the
    rendered sheet showed it.
    """
    size = mask.shape[0]
    source = Image.fromarray((mask * 255.0).astype(np.uint8))
    target = max(8, int(round(size * max(0.05, factor))))
    resized = source.resize((target, target), Image.LANCZOS)
    offset = (target - size) // 2
    if offset >= 0:
        cropped = resized.crop((offset, offset, offset + size, offset + size))
    else:
        cropped = Image.new("L", (size, size))
        cropped.paste(resized, (-offset, -offset))
    return np.asarray(cropped, dtype=np.float32) / 255.0


def build_sheet(base: np.ndarray, mode: str) -> Image.Image:
    window = radial_falloff(FRAME_SIZE)
    sheet = Image.new("L", (SHEET_SIZE, SHEET_SIZE))

    for index in range(FRAME_COUNT):
        t = index / (FRAME_COUNT - 1)          # 0 .. 1 across the effect

        if mode == "ring":
            # A shockwave starts at almost nothing and must REACH the edge —
            # it is read by where its front is, not by how big the blob got.
            # Starting at the burst's 0.30 would make the first third look like
            # a ring that was always there.
            expand = 0.10 + 0.90 * (1.0 - (1.0 - t) ** 2.2)
            # No hold. A ring is brightest at emission and thins as it spreads;
            # holding it would read as a lingering circle rather than a wave.
            fade = max(0.0, 1.0 - t) ** 1.1
        else:
            # Expansion eases OUT: an impact is fastest at the instant of contact
            # and coasts. A linear expansion reads as a growing circle rather than
            # a hit.
            expand = 0.30 + 0.70 * (1.0 - (1.0 - t) ** 3)

            # Brightness holds for the first fifth, then falls to nothing. Fading
            # from frame 0 would make the hardest part of the hit the dimmest.
            fade = 1.0 if t < 0.2 else max(0.0, 1.0 - (t - 0.2) / 0.8) ** 1.4

        frame = scaled(base, expand) * window * fade
        cell = Image.fromarray(np.clip(frame * 255.0, 0, 255).astype(np.uint8))
        sheet.paste(cell, ((index % GRID) * FRAME_SIZE, (index // GRID) * FRAME_SIZE))

    return sheet


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", required=True, type=Path)
    parser.add_argument("--out", required=True, type=Path)
    parser.add_argument("--mode", choices=("burst", "ring"), default="burst",
                        help="burst = impact spark, ring = expanding shockwave")
    args = parser.parse_args()

    if not args.base.exists():
        print(f"base not found: {args.base}")
        return 1

    base = load_mask(args.base, FRAME_SIZE)
    sheet = build_sheet(base, args.mode)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(args.out, optimize=True)
    print(f"wrote {args.out} ({SHEET_SIZE}x{SHEET_SIZE}, {FRAME_COUNT} frames, "
          f"{args.out.stat().st_size / 1024:.0f} KB)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
