#!/usr/bin/env python3
"""Bring a retaken toon albedo back to its PBR twin's mean luminance.

WHY A TRANSFORM IS THE RIGHT FIX *HERE*, WHEN IT USUALLY IS NOT. The retake fixed
what the generator was actually asked to fix -- surface detail went from near-uniform
sheets (localStd 0.56-0.88) to real linework (5.26-10.32). What it did not fix is
brightness, which drifted because the original toon prompt never stated a value at
all. So the residual defect is a single global scalar, and the map's *content* is
already correct. Regenerating again would risk the linework that was just won in
order to change one number.

Compare this to tools/env/toonify_kit_albedo.py, whose first version destroyed 20
committed textures by posterising over a fixed 0..1 range. The difference is not that
transforms are safer now: it is that here the transform's target is measured (the PBR
twin's mean), the operation is monotone, and the acceptance test is the same script
that found the defect. A transform whose success criterion is "looks better" is the
dangerous kind.

WHY MULTIPLICATIVE AND NOT A LEVELS/GAMMA CURVE. Multiplying preserves ratios, so
every flat colour region keeps its relationship to every other one -- which is the
whole content of a cel texture. A gamma curve compresses one end and would flatten
either the darks or the lights, undoing the detail this is trying to protect. All
four targets are TOO BRIGHT, so the multiplier is below 1 and nothing can clip.

NOTHING IS OVERWRITTEN. Reads from one directory, writes to another.

Usage:
    python3 tools/env/value_match_toon.py --src <retake-dir> --out <dir> [names...]
"""
import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image

PBR = Path("Assets/Resources/Textures/Env")
LUMA = np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)


def mean_luma(rgb: np.ndarray) -> float:
    return float((rgb @ LUMA).mean())


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--src", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("names", nargs="*", help="file names; default every png in --src")
    args = parser.parse_args()

    args.out.mkdir(parents=True, exist_ok=True)
    names = args.names or sorted(p.name for p in args.src.glob("*.png"))
    if not names:
        print(f"no png under {args.src}", file=sys.stderr)
        return 1

    for name in names:
        src, ref = args.src / name, PBR / name
        if not ref.exists():
            print(f"skip {name}: no PBR twin to match against")
            continue
        rgb = np.asarray(Image.open(src).convert("RGB"), dtype=np.float32)
        target = mean_luma(np.asarray(Image.open(ref).convert("RGB"), dtype=np.float32))
        current = mean_luma(rgb)
        if current <= 1e-3:
            print(f"skip {name}: source is black")
            continue
        scale = target / current
        if scale >= 1.0:
            # Brightening would clip, and every measured defect is too BRIGHT.
            # Refuse rather than quietly do something the docstring does not claim.
            print(f"skip {name}: would brighten (x{scale:.3f}) — not what this is for")
            continue
        out = np.clip(rgb * scale, 0.0, 255.0)
        Image.fromarray(out.astype(np.uint8), mode="RGB").save(args.out / name, optimize=True)
        print(f"{name:28} mean {current:6.1f} -> {mean_luma(out):6.1f} "
              f"(target {target:6.1f}, x{scale:.3f})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
