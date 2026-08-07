#!/usr/bin/env python3
"""W16: terrain flipbook sprite sheets (lava/ice/shift), deterministic assembly.

Rationale (docs/provenance/scene-synopsis-art.json already found perfectpixel/ppgen
not installed here; this session confirmed the same via `which ppgen`). Asking a
single image-gen call to draw a perfectly-aligned NxN grid is unreliable (the
character-asset-pipeline's own atlas attempt was rejected for drawing generated
section labels onto the sheet, docs/character-asset-pipeline.md line 99) — so this
script follows the same pattern the repo already uses elsewhere (bake-character-
albedo.py, gen_weapon_props.py): generate ONE seamless "hero" tile via
god-tibo-imagen, then assemble the animated grid deterministically in code. Every
frame is derived from the same tileable hero image, so seams stay consistent and
the loop (frame 0 <-> frame N-1) is guaranteed continuous by construction (each
per-theme transform is parameterized by a phase in [0, 2*pi) that wraps exactly).

vfx-lane consumer contract (confirmed 2026-08-07, orchestrator relay of vfx-lane
report §3.5.2): output is GRAYSCALE PATTERN ONLY, no color — the runtime applies
per-stage tint on top (lava=emissive pattern, ice=crystal-drift pattern,
shift=drifting-ash pattern). The color hero texture is still generated via gti
(it reads better as a "seamless tileable X texture" prompt than an abstract
grayscale-pattern prompt would), then reduced to a pattern mask via per-pixel
max(R,G,B) — this isolates the glowing/bright elements (ember cracks, frost
glints, current lines) against their dark base independent of hue, which plain
luminance (0.299R+0.587G+0.114B) would under-weight for the blue/cyan themes.
Contrast is stretched afterward so the mask reads cleanly as a multiply/screen
tint target.

Usage:
  python3 tools/gen_terrain_fx_sheets.py --hero <hero.png> --theme lava --out Assets/Resources/Terrain/terrain-fx-lava-sheet.png
"""
import argparse
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

GRID = 4                 # 4x4 = 16 frames
FRAME_SIZE = 256         # px per frame
SHEET_SIZE = GRID * FRAME_SIZE  # 1024, within the WebGL 1024 texture ceiling (CLAUDE.md §1)


def load_pattern_base(path, size):
    """Hero color texture -> single-channel pattern mask (glow/crystal/current
    isolated from a dark base, hue-independent, contrast-stretched)."""
    img = Image.open(path).convert("RGB")
    w, h = img.size
    side = min(w, h)
    left = (w - side) // 2
    top = (h - side) // 2
    img = img.crop((left, top, left + side, top + side)).resize((size, size), Image.LANCZOS)
    arr = np.asarray(img, dtype=np.float32)
    pattern = arr.max(axis=2)  # per-pixel max(R,G,B): bright glow reads regardless of hue
    lo, hi = pattern.min(), pattern.max()
    if hi > lo:
        pattern = (pattern - lo) / (hi - lo) * 255.0
    return Image.fromarray(pattern.astype(np.uint8), mode="L")


def frame_lava(base, t, n):
    """Pulsing glow brightness + slow vertical roll (bubbling flow), grayscale."""
    phase = 2 * math.pi * t / n
    pulse = 0.7 + 0.5 * (0.5 + 0.5 * math.sin(phase))   # loops cleanly at t=0/t=n
    roll_px = int((t / n) * base.size[1] * 0.25)         # partial roll, wraps at n
    arr = np.asarray(base, dtype=np.float32)
    arr = np.roll(arr, roll_px, axis=0)
    # Boost only the already-bright (crack/glow) texels so the dark base stays put.
    hot = arr > 90
    arr[hot] = np.clip(arr[hot] * pulse, 0, 255)
    return Image.fromarray(arr.astype(np.uint8), mode="L")


def frame_ice(base, t, n):
    """Diagonal crystal-drift glint sweep, wraps seamlessly, grayscale."""
    phase = t / n
    w, h = base.size
    glint = Image.new("L", (w, h), 0)
    draw = ImageDraw.Draw(glint)
    band_w = w * 0.22
    pos = phase * (w + h)
    for dx in (-(w + h), 0, (w + h)):   # wrap the sweep across the loop boundary
        draw.line([(pos - h + dx, h), (pos + dx, 0)], fill=255, width=int(band_w))
    glint = glint.filter(ImageFilter.GaussianBlur(radius=18))
    base_arr = np.asarray(base, dtype=np.float32)
    glint_arr = np.asarray(glint, dtype=np.float32)
    out = np.clip(base_arr + glint_arr * 0.6, 0, 255)   # additive: brightens the drift band
    return Image.fromarray(out.astype(np.uint8), mode="L")


def frame_shift(base, t, n):
    """Horizontal drifting-ash current band, rolls across and wraps, grayscale."""
    phase = t / n
    w, h = base.size
    shift_px = int(phase * w)
    band = Image.new("L", (w, h), 0)
    draw = ImageDraw.Draw(band)
    band_w = int(w * 0.16)
    cx = shift_px % w
    for dx in (-w, 0, w):   # wrap the band across the tile boundary
        draw.rectangle([cx + dx - band_w // 2, 0, cx + dx + band_w // 2, h], fill=255)
    band = band.filter(ImageFilter.GaussianBlur(radius=14))
    base_arr = np.asarray(base, dtype=np.float32)
    band_arr = np.asarray(band, dtype=np.float32)
    out = np.clip(base_arr + band_arr * 0.55, 0, 255)
    return Image.fromarray(out.astype(np.uint8), mode="L")


THEMES = {"lava": frame_lava, "ice": frame_ice, "shift": frame_shift}


def build_sheet(hero_path, theme, out_path):
    base = load_pattern_base(hero_path, FRAME_SIZE)
    fn = THEMES[theme]
    sheet = Image.new("L", (SHEET_SIZE, SHEET_SIZE))
    for t in range(GRID * GRID):
        frame = fn(base, t, GRID * GRID)
        col = t % GRID
        row = t // GRID
        sheet.paste(frame, (col * FRAME_SIZE, row * FRAME_SIZE))
    out_path = Path(out_path)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out_path)
    return {
        "sheet": str(out_path), "mode": "L (grayscale, no color)",
        "sheetSize": [SHEET_SIZE, SHEET_SIZE],
        "grid": [GRID, GRID], "frameSize": [FRAME_SIZE, FRAME_SIZE],
        "frameCount": GRID * GRID, "frameOrder": "row-major, frame 0 = top-left, "
        "index = row*GRID + col; loops seamlessly frame[N-1] -> frame[0]",
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--hero", required=True)
    parser.add_argument("--theme", required=True, choices=list(THEMES))
    parser.add_argument("--out", required=True)
    args = parser.parse_args()
    report = build_sheet(args.hero, args.theme, args.out)
    print(report)


if __name__ == "__main__":
    main()
