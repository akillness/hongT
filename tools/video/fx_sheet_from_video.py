#!/usr/bin/env python3
"""Turn a generated video into a TerrainFlipbook sprite sheet.

The consumer contract is fully specified and this script must not drift from
it (Assets/Scripts/View/TerrainFlipbook.cs:53-60, 442-447):

    4 x 4 grid, 16 frames, row-major, TOP-LEFT first, 12 fps => 1.333 s loop
    1024 px sheet => 256 px cells
    Resources/Terrain/terrain-fx-{lava,ice,shift}-sheet

Two properties of that contract shape everything below.

SEAM. Animate() is strictly forward and wraps 15 -> 0, so frame 16 has to lead
back into frame 1. Generated video does not loop. Ping-pong would close the
seam but it also plays the motion backwards every 0.67 s, which reads far worse
than a seam on drifting ash or flowing lava — and it halves unique content. So
instead the tail is cross-faded into the head: the last CROSSFADE frames are
blended with the first CROSSFADE frames, which is the standard way to atlas
non-looping footage. Direction is preserved and all 16 cells stay unique.

BLEND MODE. Lava and Ice go through ViewWorld.MakeAdditive (SrcAlpha/One), so
BLACK IS TRANSPARENT for them — the source has to be generated on black and
the sheet stays grayscale-luminance. Shift takes MakeUnlit straight alpha.
Both cases only ever read luminance through _BaseMap, which is why the shipped
sheets are single-channel; we keep that.

Usage:
    python3 tools/video/fx_sheet_from_video.py \
        --video /tmp/lava.mp4 --out Assets/Resources/Terrain/terrain-fx-lava-sheet.png

The .meta beside an existing sheet is NEVER touched: it carries non-default
import settings (sRGBTexture 0, clamp wrap, per-target maxTextureSize 1024)
that a regenerated .meta would silently revert.
"""
import argparse
import json
import subprocess
import sys
import tempfile
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
GRID = 4
FRAMES = GRID * GRID
SHEET = 1024
CELL = SHEET // GRID
CROSSFADE = 3          # frames blended from the tail into the head


def probe_duration(path):
    out = subprocess.run(
        ["ffprobe", "-v", "error", "-show_entries", "format=duration",
         "-of", "json", str(path)],
        capture_output=True, text=True, check=True).stdout
    return float(json.loads(out)["format"]["duration"])


def extract(video, work, count):
    """Pull `count` evenly spaced frames, each already square-cropped to CELL."""
    duration = probe_duration(video)
    # Skip the first and last 5%: generated clips often ease in and out, and a
    # ramp at either end is exactly what the crossfade cannot hide.
    start, span = duration * 0.05, duration * 0.90
    paths = []
    for i in range(count):
        t = start + span * (i / count)
        dst = work / f"f{i:02d}.png"
        subprocess.run(
            ["ffmpeg", "-y", "-v", "error", "-ss", f"{t:.4f}", "-i", str(video),
             "-frames:v", "1",
             "-vf", f"crop='min(iw,ih)':'min(iw,ih)',scale={CELL}:{CELL}:flags=lanczos,"
                    "format=gray",
             str(dst)],
            check=True)
        paths.append(dst)
    return paths


def blend(a, b, alpha, dst):
    """dst = a*(1-alpha) + b*alpha, single channel."""
    subprocess.run(
        ["ffmpeg", "-y", "-v", "error", "-i", str(a), "-i", str(b),
         "-filter_complex", f"[0:v][1:v]blend=all_expr='A*(1-{alpha})+B*{alpha}'",
         str(dst)],
        check=True)


def build(video, out):
    with tempfile.TemporaryDirectory() as tmp:
        work = Path(tmp)
        # Pull FRAMES + CROSSFADE: the extra tail frames are what gets folded
        # back into the head, so every one of the 16 published cells still
        # carries unique source content.
        raw = extract(video, work, FRAMES + CROSSFADE)
        cells = raw[:FRAMES]
        for i in range(CROSSFADE):
            # Frame 0 takes the most tail, so the wrap 15 -> 0 lands on nearly
            # continuous content; the influence decays across the next frames.
            alpha = (CROSSFADE - i) / (CROSSFADE + 1)
            dst = work / f"blend{i:02d}.png"
            blend(cells[i], raw[FRAMES + i], alpha, dst)
            cells[i] = dst

        rows = []
        for r in range(GRID):
            row = work / f"row{r}.png"
            inputs = []
            for c in range(GRID):
                inputs += ["-i", str(cells[r * GRID + c])]
            subprocess.run(
                ["ffmpeg", "-y", "-v", "error", *inputs,
                 "-filter_complex", f"hstack=inputs={GRID}", str(row)],
                check=True)
            rows.append(row)
        inputs = []
        for row in rows:
            inputs += ["-i", str(row)]
        out.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run(
            ["ffmpeg", "-y", "-v", "error", *inputs,
             # vstack top-to-bottom == row-major top-left-first, which is what
             # FrameSt assumes when it measures v down from 1.
             "-filter_complex", f"vstack=inputs={GRID}",
             "-pix_fmt", "gray", str(out)],
            check=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--video", required=True)
    ap.add_argument("--out", required=True)
    args = ap.parse_args()
    out = Path(args.out).resolve()
    meta = out.with_suffix(out.suffix + ".meta")
    had_meta = meta.exists()
    build(Path(args.video), out)
    if had_meta and not meta.exists():
        sys.exit("FATAL: the .meta beside the sheet disappeared - import "
                 "settings (sRGB 0, clamp wrap, maxTextureSize 1024) would be "
                 "silently reverted")
    size = out.stat().st_size
    try:
        shown = out.relative_to(REPO)
    except ValueError:
        shown = out
    print(f"[fx_sheet] {shown}: {SHEET}x{SHEET} gray, "
          f"{size/1024:.0f} KB, {FRAMES} cells @ {CELL}px, "
          f"crossfade {CROSSFADE}, meta preserved={had_meta}")


if __name__ == "__main__":
    main()
