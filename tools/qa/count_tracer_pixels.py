"""Count companion-comet pixels in one frame — by SHAPE, not just hue.

Hue cannot be the gate twice over: (1) the scout-echo companion BODY is tinted
(0.62, 0.95, 0.88), 0.12 from the comet's cyan; (2) the comet is ADDITIVE, so
over the lit tan plate it saturates to near-white (r == b) and any chroma term
rejects exactly the frames that matter. The mask below is only "additive
blowout": G and B near ceiling, R not above B.

Shape does the discrimination, in two rotation-invariant steps per connected
component (8-neighbour — an antialiased diagonal is not 4-connected):

  w_eff = area / bbox-diagonal   line: ~stroke width (4-6)   blob: ~17
  eig-ratio = lambda2/lambda1    line: ~(w/L)^2 << 1         ring: ~1.0
     (second-moment eigenvalues of the pixel coordinates; a circle's
      covariance is isotropic, so this separates the comet from hit-spark
      RINGS — thin loops pass the w_eff test at w_eff ~ 2.2w but cannot fake
      anisotropy)

Gate: diagonal >= 25 AND w_eff <= 8 AND eig-ratio <= 0.15.

Prints JSON {"count": N, "components": M} where count = comet pixels in
line-shaped components only.
"""
import json
import math
import sys
from collections import deque

import numpy as np
from PIL import Image


def main() -> int:
    img = np.asarray(Image.open(sys.argv[1]).convert("RGB"), dtype=np.int16)
    # Game viewport only, and NO UI text bands: white glyph rows (level-up
    # plate y~590+, wave banner y<210, lore/objective strips) are pure white
    # lines to this mask — an elongated text row passes both shape gates.
    # y 210..585 x 240..1200 is the combat floor between those bands.
    view = img[210:585, 240:1200]
    r, g, b = view[..., 0], view[..., 1], view[..., 2]
    # Ceiling 200, not 230 — measured on live frames (threshold curve in the
    # 2026-08-13 proof run): the comet's own additive contribution tops out at
    # ~(119,182,191), so over dark mid-ground G lands in the 180-230 band and a
    # 230 ceiling only accepts comets over the brightest floor. At 200 the
    # static cyan wall lattice still stays below the line gates (its hits only
    # appear at <=130).
    mask = (b > 200) & (g > 200) & (r <= b + 10)

    visited = np.zeros_like(mask, dtype=bool)
    height, width = mask.shape
    comet_pixels = 0
    comet_components = 0
    neighbours = ((1, 0), (-1, 0), (0, 1), (0, -1),
                  (1, 1), (1, -1), (-1, 1), (-1, -1))
    ys, xs = np.nonzero(mask)
    for seed_y, seed_x in zip(ys, xs):
        if visited[seed_y, seed_x]:
            continue
        queue = deque([(int(seed_y), int(seed_x))])
        visited[seed_y, seed_x] = True
        component_y = []
        component_x = []
        while queue:
            y, x = queue.popleft()
            component_y.append(y)
            component_x.append(x)
            for dy, dx in neighbours:
                ny, nx = y + dy, x + dx
                if 0 <= ny < height and 0 <= nx < width \
                        and mask[ny, nx] and not visited[ny, nx]:
                    visited[ny, nx] = True
                    queue.append((ny, nx))
        size = len(component_y)
        extent_y = max(component_y) - min(component_y) + 1
        extent_x = max(component_x) - min(component_x) + 1
        diagonal = math.hypot(extent_x, extent_y)
        if diagonal < 25.0 or size / diagonal > 8.0:
            continue
        # Linearity: eigenvalue ratio of the coordinate covariance. A segment
        # concentrates variance along one axis; a ring cannot.
        coords = np.stack([component_x, component_y]).astype(np.float64)
        eigenvalues = np.linalg.eigvalsh(np.cov(coords))
        if eigenvalues[-1] <= 1e-9:
            continue
        if eigenvalues[0] / eigenvalues[-1] > 0.15:
            continue
        comet_pixels += size
        comet_components += 1
    print(json.dumps({"count": comet_pixels, "components": comet_components}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
