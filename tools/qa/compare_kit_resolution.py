#!/usr/bin/env python3
"""512 vs 1024 for the recovered dungeon-kit maps, by the seed's calculation protocol.

WHY A SCRIPT AND NOT AN EYE. The question "can you tell 512 from 1024" is exactly the
kind a person answers differently depending on which frame they looked at last. The
protocol fixes every degree of freedom in advance — capture conditions, colour space,
aggregation, and the gate — so the same frames give the same verdict to anyone.

METRICS
    D_noise  same build captured twice        -> the measurement floor
    D_gain   untextured vs 1024               -> the size of the change being bought
    D_test   512 vs 1024                      -> what dropping resolution costs

    GATE-0   D_gain.mean >= 10 * D_noise.mean  AND  D_gain.mean >= D_noise.p99
    PASS     D_test.mean <= max(0.10 * D_gain.mean, D_noise.p99)
             AND D_test.p99 <= 0.25 * D_gain.p99

GATE-0 exists because a threshold on D_test is meaningless if the textures changed
nothing: 512 and 1024 would both read "no difference" and the pass would prove nothing.
It asserts the PREMISE before the conclusion.

The pass threshold is a RATIO, not an invented constant. "Mean difference below 2/255"
would be a number with no backing; "at most a tenth of the change we are buying" is a
statement about this project's own measured gain.

Usage:
    python3 tools/qa/compare_kit_resolution.py --frames <dir> [--mask <dir>]
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image

STAGES = ("abyss-chancel", "ember-bastion", "cinder-span")


def to_lab(path: Path) -> np.ndarray:
    """sRGB PNG -> CIELAB (D65). CIE76 distance is computed on this."""
    srgb = np.asarray(Image.open(path).convert("RGB"), dtype=np.float64) / 255.0
    linear = np.where(srgb <= 0.04045, srgb / 12.92, ((srgb + 0.055) / 1.055) ** 2.4)
    matrix = np.array([[0.4124, 0.3576, 0.1805],
                       [0.2126, 0.7152, 0.0722],
                       [0.0193, 0.1192, 0.9505]])
    xyz = linear @ matrix.T
    white = np.array([0.95047, 1.0, 1.08883])
    ratio = xyz / white
    f = np.where(ratio > 0.008856, np.cbrt(ratio), 7.787 * ratio + 16 / 116)
    lightness = 116 * f[..., 1] - 16
    a = 500 * (f[..., 0] - f[..., 1])
    b = 200 * (f[..., 1] - f[..., 2])
    return np.stack([lightness, a, b], axis=-1)


def delta_e(first: np.ndarray, second: np.ndarray, mask: np.ndarray | None):
    """CIE76 mean and p99 over the mask (whole frame when no mask is supplied)."""
    distance = np.sqrt(((first - second) ** 2).sum(-1))
    values = distance[mask] if mask is not None else distance.ravel()
    if values.size == 0:
        return 0.0, 0.0
    return float(values.mean()), float(np.percentile(values, 99))


def load_mask(mask_dir: Path, stage: str, shape) -> np.ndarray | None:
    """Kit-mesh pixels from the unlit-magenta mask build, eroded by one pixel.

    Erosion is not cosmetic: boundary pixels are anti-aliased against whatever is
    behind them, so they differ between frames for reasons that have nothing to do with
    texture resolution. Including them would measure the edges, not the surfaces.

    The mask comes from a SEPARATE build rather than from "pixels where untextured and
    1024 differ". That shortcut would define the mask in terms of D_gain and guarantee
    a large D_gain — the measurement would confirm itself.
    """
    path = mask_dir / f"f-mask-{stage}.png"
    reference = mask_dir / f"f-1024-{stage}.png"
    if not path.exists() or not reference.exists():
        return None

    # NOT an exact magenta match. That was the first design and it selected ZERO pixels:
    # the View re-tints every placement through a MaterialPropertyBlock, which overrides
    # the material's colour, so the mask build renders pale lavender rather than
    # (255, 0, 255). Colour-keying a value the runtime is free to overwrite cannot work.
    #
    # Instead: kit pixels are where the mask build differs ENORMOUSLY from the shipped
    # build. Magenta against stone is a CIE76 distance in the tens; the quantity being
    # measured (D_gain) has a mean near 1. Selecting at 25 therefore cannot manufacture
    # the result — the mask signal is more than an order of magnitude above it — while
    # still keying on geometry rather than on the texture change itself.
    marked = to_lab(path)
    shipped = to_lab(reference)
    exact = np.sqrt(((marked - shipped) ** 2).sum(-1)) > 25.0

    eroded = exact.copy()
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            eroded &= np.roll(np.roll(exact, dy, axis=0), dx, axis=1)
    return eroded if eroded.shape == shape[:2] else None


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--frames", required=True, type=Path)
    parser.add_argument("--mask", type=Path, default=None)
    args = parser.parse_args()

    print(f"{'stage':16} {'px':>10} {'D_noise':>16} {'D_gain':>16} {'D_test':>16} "
          f"{'GATE-0':>7} {'PASS':>6}")
    print("-" * 92)

    verdicts = []
    for stage in STAGES:
        none = to_lab(args.frames / f"f-none-{stage}.png")
        none_twin = to_lab(args.frames / f"f-noneb-{stage}.png")
        high = to_lab(args.frames / f"f-1024-{stage}.png")
        low = to_lab(args.frames / f"f-512-{stage}.png")

        mask = load_mask(args.mask, stage, none.shape) if args.mask else None
        count = int(mask.sum()) if mask is not None else none.shape[0] * none.shape[1]

        # An empty mask must be a HARD FAILURE, never a pass. The first version of this
        # script reported "GATE-0 OK / 512 everywhere" on a mask that matched zero
        # pixels — every metric was 0.000, so every threshold was satisfied by having
        # measured nothing at all. A gate that cannot fail is worse than no gate.
        if args.mask and count < 1000:
            print(f"{stage:16} mask matched {count} px — REFUSING to judge on an empty region")
            return 2

        noise = delta_e(none, none_twin, mask)
        gain = delta_e(none, high, mask)
        test = delta_e(low, high, mask)

        gate0 = gain[0] >= 10 * noise[0] and gain[0] >= noise[1]
        passes = (test[0] <= max(0.10 * gain[0], noise[1])
                  and test[1] <= 0.25 * gain[1])

        print(f"{stage:16} {count:10d} "
              f"{noise[0]:7.3f}/{noise[1]:7.2f} "
              f"{gain[0]:7.3f}/{gain[1]:7.2f} "
              f"{test[0]:7.3f}/{test[1]:7.2f} "
              f"{'OK' if gate0 else 'FAIL':>7} {'512' if passes else '1024':>6}")
        verdicts.append((stage, gate0, passes))

    print()
    if not all(g for _, g, _ in verdicts):
        print("GATE-0 FAILED — the textures did not measurably change the screen.")
        print("Do not read the 512/1024 column: both would report 'no difference'.")
        return 1

    print("512 everywhere" if all(p for _, _, p in verdicts)
          else "1024 required on: " + ", ".join(s for s, _, p in verdicts if not p))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
