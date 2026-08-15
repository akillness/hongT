"""Outskirt band probes on the LIVE deployed frame (dungeon-hud capture).

Two instruments, because two different claims get made about the outskirts:

1. SEAM GUARD (luma ratio + absolute delta). SceneBuilder.cs:164-182 tuned
   VoidFloor against a measured seam: apron 48.34 vs void 22.28 = 2.17x read as
   a defect, corrected to ~1.6x; the recorded failure mode was a 4x step ("the
   world ends here"). A RATIO alone lies near black — 14.6 vs 3.4 luma is
   "4.3x" but both bands read as darkness on screen (the original defect was
   48 -> 22, a BRIGHT cliff). So the guard reports the absolute delta beside
   the ratio and flags only steps that are both wide (>= 4x) AND bright
   (delta >= 15 luma, roughly the original defect's 26-luma cliff halved).

2. HUE DELTA (R-B + saturation, pre vs post). The stage-hue pass
   (EnvironmentBuilder.RimColorFor / GameDirector.VoidTintFor) holds luminance
   constant BY DESIGN, so the seam guard cannot see it. Whether the hue landed
   is a channel question: on an ember stage the outskirt R-B must move toward
   positive. Run with --pre <frame> to get the labelled comparison the
   GameDirector comment promises per release.

   ATTRIBUTION LIVES IN THE BASELINE CHOICE, not in the channels. A shading
   change (bc799ee9's normals fix) moves R-B too — removing a full-strength
   blue rim raises R-B by tens of luma, dwarfing the hue pass. Measured
   2026-08-13 on the two canonical pairs:

     pre = 03-dungeon-settled-pre-34710f1.png   TOTAL delta (normals + hue):
       R-B -30..-19 -> -11..+22, luma -50..-80%  — dominated by bc799ee9.
     pre = renderer-census/dungeon-frame.png    STAGE-HUE ONLY (post-normals,
       pre-hue; Development-vs-Release caveat): R-B +0.3..+3.1, luma ~0 —
       the rim hue lands only on grazing-angle silhouette pixels, and the
       VoidFloor hue measured ~0 at the shipped orbit (CameraRig.cs's 0.25%
       claim holds there; the investigation's 4.75% magenta bake counted
       pixels the fog band owns).

Usage:
    python3 tools/qa/measure_outskirt_seam.py                       # seam guard
    python3 tools/qa/measure_outskirt_seam.py --pre <baseline.png>  # + hue delta
"""
import argparse
import numpy as np
from PIL import Image

POST = "_workspace/current/qa/dungeon-hud/03-dungeon-settled.png"
LUMA = np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)

# Game viewport (letterbox margins measured from the capture: x 78..1360, y 8..858).
# Outskirt = darkest bands hugging the viewport edge; plate = adjacent inward band.
PAIRS = {
    # name: (outer band, inner band) — 24px steps marching inward
    "left edge":   ((200, 560, 80, 104),  (200, 560, 128, 152)),
    "top-left":    ((120, 200, 90, 190),  (200, 280, 190, 290)),
    "bottom-left": ((760, 850, 90, 260),  (660, 748, 120, 290)),
    "top-right":   ((60, 130, 1240, 1350), (140, 210, 1180, 1290)),
}
BRIGHT_CLIFF_DELTA = 15.0   # luma; the original defect was a 26-luma cliff


def load(path):
    return np.asarray(Image.open(path).convert("RGB"), dtype=np.float32)


def seam_guard(img):
    luma = img @ LUMA

    def band(y0, y1, x0, x1):
        return float(luma[y0:y1, x0:x1].mean())

    print(f"{'seam':12s} {'outer':>7s} {'inner':>7s} {'step':>6s} {'delta':>7s}  verdict")
    flagged = False
    for name, (outer, inner) in PAIRS.items():
        o, i = band(*outer), band(*inner)
        step = (i / max(o, 1e-3)) if i > o else (o / max(i, 1e-3))
        delta = abs(o - i)
        cliff = step >= 4.0 and delta >= BRIGHT_CLIFF_DELTA
        flagged |= cliff
        print(f"{name:12s} {o:7.2f} {i:7.2f} {step:5.2f}x {delta:6.1f}   "
              + ("BRIGHT CLIFF" if cliff else "ok"))
    print("seam guard:", "FLAGGED — a wide AND bright step exists" if flagged
          else "clean (no step is both >=4x and >=15 luma)")
    return 2 if flagged else 0


def hue_delta(pre_img, post_img, label):
    print(f"\n-- hue delta ({label}) --")
    print(f"{'band':12s} {'R-B pre->post':>16s} {'sat pre->post':>15s} {'luma pre->post':>16s}")
    for name, (outer, _) in PAIRS.items():
        y0, y1, x0, x1 = outer
        a, b = pre_img[y0:y1, x0:x1], post_img[y0:y1, x0:x1]

        def sat(x):
            mx = x.max(axis=-1)
            mn = x.min(axis=-1)
            return float(np.where(mx > 1e-3, (mx - mn) / np.maximum(mx, 1e-3), 0).mean())

        rba = float((a[..., 0] - a[..., 2]).mean())
        rbb = float((b[..., 0] - b[..., 2]).mean())
        print(f"{name:12s} {rba:+7.1f} -> {rbb:+6.1f} {sat(a):6.3f} -> {sat(b):5.3f}"
              f" {float((a @ LUMA).mean()):7.1f} -> {float((b @ LUMA).mean()):6.1f}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--post", default=POST)
    parser.add_argument("--pre", help="baseline frame for the hue-delta report")
    args = parser.parse_args()
    post = load(args.post)
    code = seam_guard(post)
    if args.pre:
        hue_delta(load(args.pre), post, f"{args.pre} -> {args.post}")
    return code


if __name__ == "__main__":
    raise SystemExit(main())
