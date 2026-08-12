"""Seam step on the LIVE deployed frame: terrain plate -> outskirt darkness.

SceneBuilder.cs:164-182 tuned VoidFloor against a measured seam: apron 48.34 vs
void 22.28 = 2.17x read as a defect, corrected to ~1.6x; the recorded failure
mode was a 4x step ("the world ends here"). Toon terrain moves apron luminance,
so re-measure the same variable on the live capture before claiming the
outskirts are closed.
"""
import numpy as np
from PIL import Image

img = np.asarray(Image.open("_workspace/current/qa/dungeon-hud/03-dungeon-settled.png").convert("RGB"), dtype=np.float32)
luma = img @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)

# Game viewport (letterbox margins measured from the capture: x 78..1360, y 8..858).
# Outskirt = darkest bands hugging the viewport edge; plate = adjacent inward band.
def band(y0, y1, x0, x1):
    return float(luma[y0:y1, x0:x1].mean())

pairs = {
    # name: (outer band, inner band) — 24px steps marching inward
    "left edge":   ((200, 560, 80, 104),  (200, 560, 128, 152)),
    "top-left":    ((120, 200, 90, 190),  (200, 280, 190, 290)),
    "bottom-left": ((760, 850, 90, 260),  (660, 748, 120, 290)),
    "top-right":   ((60, 130, 1240, 1350), (140, 210, 1180, 1290)),
}
print(f"{'seam':12s} {'outer':>7s} {'inner':>7s} {'step':>6s}")
worst = 0.0
for name, (outer, inner) in pairs.items():
    o, i = band(*outer), band(*inner)
    step = (i / max(o, 1e-3)) if i > o else (o / max(i, 1e-3))
    worst = max(worst, step)
    print(f"{name:12s} {o:7.2f} {i:7.2f} {step:5.2f}x")
print(f"worst step {worst:.2f}x  (recorded defect threshold 4x, pre-fix 2.17x, target ~1.6x)")
