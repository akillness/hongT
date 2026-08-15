#!/usr/bin/env python3
"""Locate the gold-bordered start button in a browser screenshot.

WHY THIS IS NOT DONE IN THE PAGE. The obvious implementation reads the Unity canvas
with drawImage + getImageData from inside the page. It returns black: Unity's WebGL
context is created without preserveDrawingBuffer, so the backbuffer is not readable
between frames even though Chrome can still composite it for a screenshot. An
in-page probe therefore reports "no gold pixels" for a frame that visibly has them —
a measurement that cannot distinguish a missing button from an unreadable buffer,
which is the coordinate system where right and wrong coincide (CLAUDE.md §4m).

So the frame is captured by the browser and measured here instead.

WHAT IT LOOKS FOR. The start button's visual signature is its BORDER, not its fill:
its plate is a dark 9-slice sprite and a sprite tint can only darken, so there is no
gold fill to find. It is the only gold-bordered rectangle in a lobby whose every
other panel border is cyan. Clustering the gold pixels and keeping the largest
cluster that is button-shaped rejects the gold title text and the gold currency
readouts without this script having to know where any of them sit.

Usage:  python3 tools/qa/find_gold_button.py <frame.png> <css_w> <css_h>
Prints one JSON object.
"""
import json
import sys
from collections import deque

import numpy as np
from PIL import Image

DOWNSAMPLE = 4          # the border is ~1 device px; 4x bridges antialiased gaps
MIN_CSS = 40.0          # below the 44 floor, so the caller can REPORT a violation
MAX_CSS = 400.0         # a banner or the title bar is not a button


def main() -> int:
    path, css_w, css_h = sys.argv[1], float(sys.argv[2]), float(sys.argv[3])
    rgb = np.asarray(Image.open(path).convert("RGB"), dtype=np.int16)
    h, w, _ = rgb.shape
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]

    # Gold is 0xDD,0xC8,0x69. Wide bands because the frame is composited and
    # gamma-mapped; what does not move is r >= g > b with a large g-b spread.
    gold = (r > 150) & (g > 120) & (r >= g) & ((g - b) > 40) & ((r - b) > 60)

    gh, gw = -(-h // DOWNSAMPLE), -(-w // DOWNSAMPLE)
    pad = np.zeros((gh * DOWNSAMPLE, gw * DOWNSAMPLE), dtype=bool)
    pad[:h, :w] = gold
    # ANY pixel in a cell makes the cell gold — that is what bridges the gaps.
    mask = pad.reshape(gh, DOWNSAMPLE, gw, DOWNSAMPLE).any(axis=(1, 3))

    px, py = w / css_w, h / css_h
    seen = np.zeros_like(mask)
    best = None
    for sy in range(gh):
        for sx in range(gw):
            if not mask[sy, sx] or seen[sy, sx]:
                continue
            q = deque([(sy, sx)])
            seen[sy, sx] = True
            n, y0, y1, x0, x1 = 0, sy, sy, sx, sx
            while q:
                cy, cx = q.popleft()
                n += 1
                y0, y1 = min(y0, cy), max(y1, cy)
                x0, x1 = min(x0, cx), max(x1, cx)
                for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    ny, nx = cy + dy, cx + dx
                    if 0 <= ny < gh and 0 <= nx < gw and mask[ny, nx] and not seen[ny, nx]:
                        seen[ny, nx] = True
                        q.append((ny, nx))
            w_css = (x1 - x0 + 1) * DOWNSAMPLE / px
            h_css = (y1 - y0 + 1) * DOWNSAMPLE / py
            if not (MIN_CSS <= w_css <= MAX_CSS and MIN_CSS <= h_css <= MAX_CSS):
                continue
            if best is None or n > best["cells"]:
                best = {
                    "cells": int(n),
                    "cx": round((x0 + x1 + 1) / 2 * DOWNSAMPLE / px, 1),
                    "cy": round((y0 + y1 + 1) / 2 * DOWNSAMPLE / py, 1),
                    "wCss": round(w_css, 1),
                    "hCss": round(h_css, 1),
                }

    print(json.dumps({"found": best is not None, "goldPixels": int(gold.sum()),
                      **(best or {})}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
