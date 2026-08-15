#!/usr/bin/env python3
"""Measure the toon stage albedo set against the PBR set it replaced.

WHAT IS BEING MEASURED, AND WHY THESE TWO NUMBERS.

  localStd   the median 8x8-tile luminance standard deviation. This is SURFACE
             DETAIL, and it is the right statistic here because it survives the
             thing that would confound a global one: a cel texture is *supposed*
             to have a small number of flat colour steps, so global std stays high
             (the steps are far apart) while the surface itself goes featureless.
             A tile-local statistic asks "within a small patch, is there anything
             at all", which is exactly the question.

  mean       plain mean luminance. Drift here is a defect regardless of art
             direction, because the toon prompt never mentions value and this
             project reads by VALUE contrast -- §E0.5 measures hazard telegraphs
             as a ratio against the environment, so an environment that brightens
             by half eats the margin the hazard gate depends on.

WHY A PBR BASELINE AND NOT AN ABSOLUTE THRESHOLD. There is no defensible absolute
number for "enough detail" -- it depends on how many screen pixels the map covers,
which depends on the camera. The PBR set is a real, shipped, accepted answer for the
same nine places at the same camera, so it is the one available anchor that does not
require inventing a constant (§4t).

The verdict is deliberately NOT "toon must beat PBR on detail" -- flattening is part
of the brief. It is: a map may not be a near-uniform sheet, and it may not drift in
value, because neither was asked for.

Usage:
    python3 tools/qa/measure_toon_textures.py
    python3 tools/qa/measure_toon_textures.py --staging <dir>   # judge a retake
"""
import argparse
import statistics
import sys
from pathlib import Path

import numpy as np
from PIL import Image

TOON = Path("Assets/Resources/Textures/Toon")
PBR = Path("Assets/Resources/Textures/Env")
STAGES = [
    "cinder-span", "ember-gallery", "abyss-chancel", "witness-well", "echo-throne",
    "ash-verdict", "cinder-sluice", "ember-bastion", "ash-march",
]
KINDS = ("stone", "floor")

# A map under this does not read as a surface.
#
# THIS NUMBER WAS WRONG ONCE, AND THE SCREEN IS WHAT CORRECTED IT. The first value
# was 1.0, taken from a gap in the measured distribution: six maps sat under 1.0 and
# the rest between 1.6 and 10.5, so 1.0 looked like a real boundary. It was a real
# boundary -- of the wrong question. It separated "one flat colour" from "more than
# one flat colour", which is not "does this read as stone". cinder-span/stone passed
# at 1.92 and the shipped frame still showed the arena's whole boundary ring as blank
# pale slabs, because that ring is made of exactly that map.
#
# 3.0 is where the retaken maps land at their worst (3.69) with a margin, and it puts
# every map that failed on screen below the line. A threshold that a browser frame
# has falsified once is worth more than one derived from a histogram (§4c, §4m).
FLAT_FLOOR = 3.0
# Value drift the prompt never asked for. 25% is a quarter of the range and is
# already visible as "washed out" beside an unshifted neighbour.
DRIFT_LIMIT = 25.0


def measure(path: Path):
    rgb = np.asarray(Image.open(path).convert("RGB"), dtype=np.float32)
    luma = rgb @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
    h, w = luma.shape
    th, tw = h // 8 * 8, w // 8 * 8
    tiles = (luma[:th, :tw]
             .reshape(th // 8, 8, tw // 8, 8)
             .transpose(0, 2, 1, 3)
             .reshape(-1, 64))
    return float(luma.mean()), float(np.median(tiles.std(axis=1)))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--staging", type=Path,
                        help="judge retaken maps in this directory instead of the "
                             "live set; nothing is copied either way")
    args = parser.parse_args()

    print(f"{'map':24}{'mean':>18}{'drift':>8}{'localStd':>20}   verdict")
    drifts, flats, adopt = [], [], []
    for stage in STAGES:
        for kind in KINDS:
            name = f"{stage}-{kind}.png"
            pbr_path, toon_path = PBR / name, TOON / name
            if not pbr_path.exists() or not toon_path.exists():
                continue
            pm, pl = measure(pbr_path)
            src = toon_path
            tag = ""
            if args.staging is not None:
                staged = args.staging / name
                if staged.exists():
                    src, tag = staged, "  [retake]"
                else:
                    continue          # only report what was retaken
            tm, tl = measure(src)
            drift = (tm - pm) / pm * 100.0
            drifts.append(drift)
            bad = []
            if tl < FLAT_FLOOR:
                bad.append("FLAT")
                flats.append(f"{stage}/{kind}")
            if abs(drift) > DRIFT_LIMIT:
                bad.append("DRIFT")
            verdict = "ok" if not bad else " ".join(bad)
            if args.staging is not None and not bad:
                adopt.append(name)
            print(f"{stage + '/' + kind:24}{pm:8.1f} ->{tm:6.1f}{drift:+7.0f}%"
                  f"{pl:11.2f} ->{tl:6.2f}   {verdict}{tag}")

    if not drifts:
        print("\nnothing measured")
        return 1
    print(f"\nmedian drift {statistics.median(drifts):+.0f}%  "
          f"(limit +/-{DRIFT_LIMIT:.0f}%)")
    print(f"near-uniform maps (localStd < {FLAT_FLOOR}): {len(flats)}"
          + (f" — {', '.join(flats)}" if flats else ""))

    if args.staging is not None:
        print(f"\nADOPTABLE ({len(adopt)}): {', '.join(adopt) if adopt else 'none'}")
        print("Nothing was copied. Adopt by hand, one file at a time, and re-run "
              "this without --staging to confirm the live set improved.")
    return 0 if not flats else 2


if __name__ == "__main__":
    raise SystemExit(main())
