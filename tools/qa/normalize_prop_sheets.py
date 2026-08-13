#!/usr/bin/env python3
"""Normalize prop material sheets to a known mean luminance.

WHY THIS EXISTS. CinderToonLit multiplies: `albedo = SAMPLE(_BaseMap) *
_BaseColor`. The tints in PropImportPipeline were chosen when albedo WAS the
tint, so binding a sheet with its own value silently multiplies the two. Measured
on the first generated set:

    prop-cloth  sheet 0.160 x tint 0.148 -> 0.024   (0.16x intended)
    prop-iron   sheet 0.173 x tint 0.409 -> 0.071   (0.17x intended)

equip-cloak-basic has emission exactly 0, so nothing adds the light back: it
renders as a pure black silhouette. That is precisely the "FBX import lands
near-black on the dark court floor" defect the prop pipeline was written to fix,
reintroduced through the texture channel.

THE CONTRACT, MADE ENFORCEABLE. gen_prop_textures.sh already asserts that the
sheet carries PATTERN and the tint is the VALUE authority. Asking the image model
for "mid-grey, pattern only" is a hope; normalizing here is the same claim as
arithmetic. After this pass every sheet has mean luminance TARGET, and the
pipeline divides its tints by exactly that constant, so the product lands back on
the intended tint by construction.

GAMMA, NOT A LINEAR SCALE. Cloth needs a large lift to reach target; multiplying
clips every mid-tone to white and the weave disappears. A gamma c**g is monotone,
never clips, and preserves the ordering that makes the pattern readable.

BUT GAMMA IS NOT FREE, AND THE TARGET IS BOUNDED FROM BOTH SIDES. Lifting a sheet
compresses the end it lifts from, and on these sheets the pattern lives in the
shadows. Measured on the first cloth sheet (source mean 0.156), surviving contrast
after the compensated tint:

    target 0.75 -> gamma 0.155 -> 5 levels of 255   (flat: the tint it replaced)
    target 0.60 -> gamma 0.275 -> 8 levels
    target 0.50 -> gamma 0.373 -> 12 levels

Lower target = milder gamma = more pattern survives. The floor is tint headroom:
the largest tint component is 0.46 (weapon body), and the pipeline divides by
TARGET, so TARGET must stay above ~0.5 or a compensated tint exceeds 1 and clips
before lighting. Hence 0.50 — the mildest gamma the multiply can afford.

The real fix is upstream: gen_prop_textures.sh now asks for mid-grey pattern
instead of "near-black plate", so the gamma has little work left to do. This pass
is the guard, not the strategy.

CONTRAST IS CHECKED, NOT ASSUMED. A mean-only check passes a solid grey square,
which is the exact failure the gamma can cause. The shipped toon environment
sheets measure p95-p05 of 0.26 to 0.58, so a normalised prop sheet must clear the
bottom of that range to count as a texture at all.

    python3 tools/qa/normalize_prop_sheets.py [--target 0.50] [--check]
"""
import argparse
import glob
import sys

import numpy as np
from PIL import Image

# Must equal PropImportPipeline.SheetMeanLuminance.
TARGET = 0.50

# Floor for p95-p05 luminance spread. Calibrated from the toon environment
# sheets already shipping (abyss-chancel 0.264-0.307, ash-march 0.255-0.366,
# ash-verdict 0.497-0.577): the bottom of the range that reads as a texture in
# this game. Below it the sheet is a flat field wearing a texture fetch.
MIN_CONTRAST = 0.26
SHEETS = "Assets/Resources/Textures/Props/prop-*.png"


def luminance_map(a):
    return 0.2126 * a[..., 0] + 0.7152 * a[..., 1] + 0.0722 * a[..., 2]


def luminance(a):
    return float(luminance_map(a).mean())


def contrast(a):
    """p95-p05 luminance spread — how much pattern actually survives.

    Mean alone cannot see the failure this file can CAUSE: a solid grey square
    hits any target exactly. Gamma-lifting a dark sheet compresses the shadow
    end, which on these sheets is where all the linework lives, so the check has
    to measure the thing the fix endangers.
    """
    lum = luminance_map(a)
    return float(np.percentile(lum, 95) - np.percentile(lum, 5))


def solve_gamma(a, target):
    lo, hi = 0.02, 20.0
    for _ in range(60):
        mid = (lo + hi) * 0.5
        if luminance(np.power(a, mid)) < target:
            hi = mid
        else:
            lo = mid
    return (lo + hi) * 0.5


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--target", type=float, default=TARGET)
    parser.add_argument("--check", action="store_true",
                        help="measure only, non-zero exit if any sheet is off "
                             "target mean or below the contrast floor")
    args = parser.parse_args()

    paths = sorted(glob.glob(SHEETS))
    if not paths:
        raise SystemExit(f"FATAL: no sheets at {SHEETS}")
    bad = []
    for path in paths:
        image = Image.open(path).convert("RGB")
        a = np.asarray(image, dtype=np.float32) / 255.0
        name = path.split("/")[-1]
        if args.check:
            lum, spread = luminance(a), contrast(a)
            faults = []
            if abs(lum - args.target) > 0.02:
                faults.append(f"mean {lum:.3f} != {args.target:.2f}")
            if spread < MIN_CONTRAST:
                faults.append(f"contrast {spread:.3f} < {MIN_CONTRAST:.2f}")
            print(f"{name:16s} lum={lum:.3f} contrast={spread:.3f} "
                  f"{'ok' if not faults else 'FAIL: ' + '; '.join(faults)}")
            if faults:
                bad.append(name)
            continue
        before, before_contrast = luminance(a), contrast(a)
        gamma = solve_gamma(a, args.target)
        out = np.power(a, gamma)
        Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8)).save(path)
        print(f"{name:16s} lum {before:.3f} -> {luminance(out):.3f} "
              f"contrast {before_contrast:.3f} -> {contrast(out):.3f} "
              f"(gamma {gamma:.3f})")
    if bad:
        print("REJECTED: " + ", ".join(bad))
        sys.exit(1)
    print("done")


if __name__ == "__main__":
    main()
