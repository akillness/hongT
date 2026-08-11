#!/usr/bin/env python3
"""Measure reduced-motion vent-sheet stability on an elliptical outer ring."""

import argparse
import json
import math

from PIL import Image


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("frame_a")
    parser.add_argument("frame_b")
    parser.add_argument("--center", nargs=2, type=float, required=True)
    parser.add_argument("--radii", nargs=2, type=float, required=True)
    parser.add_argument("--inner", type=float, default=0.78)
    parser.add_argument("--outer", type=float, default=1.03)
    args = parser.parse_args()

    image_a = Image.open(args.frame_a).convert("RGB")
    image_b = Image.open(args.frame_b).convert("RGB")
    if image_a.size != image_b.size:
        raise ValueError(f"frame sizes differ: {image_a.size} != {image_b.size}")

    cx, cy = args.center
    rx, ry = args.radii
    luminance_a = []
    luminance_b = []
    absolute_deltas = []
    pixels_a = image_a.load()
    pixels_b = image_b.load()

    x0 = max(0, math.floor(cx - rx * args.outer))
    x1 = min(image_a.width, math.ceil(cx + rx * args.outer + 1))
    y0 = max(0, math.floor(cy - ry * args.outer))
    y1 = min(image_a.height, math.ceil(cy + ry * args.outer + 1))
    for y in range(y0, y1):
        for x in range(x0, x1):
            radius = math.sqrt(((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2)
            if not args.inner <= radius <= args.outer:
                continue
            rgb_a = pixels_a[x, y]
            rgb_b = pixels_b[x, y]
            lum_a = 0.2126 * rgb_a[0] + 0.7152 * rgb_a[1] + 0.0722 * rgb_a[2]
            lum_b = 0.2126 * rgb_b[0] + 0.7152 * rgb_b[1] + 0.0722 * rgb_b[2]
            luminance_a.append(lum_a)
            luminance_b.append(lum_b)
            absolute_deltas.append(abs(lum_a - lum_b))

    mean_a = sum(luminance_a) / len(luminance_a)
    mean_b = sum(luminance_b) / len(luminance_b)
    result = {
        "sampledPixels": len(luminance_a),
        "frameAMeanLuminance": round(mean_a, 4),
        "frameBMeanLuminance": round(mean_b, 4),
        "meanDeltaPercent": round(abs(mean_a - mean_b) / mean_a * 100.0, 4),
        "pixelMeanAbsoluteDeltaPercentOfFullScale": round(
            sum(absolute_deltas) / len(absolute_deltas) / 255.0 * 100.0, 4),
        "thresholdPercent": 2.0,
        "pass": abs(mean_a - mean_b) / mean_a * 100.0 < 2.0,
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
