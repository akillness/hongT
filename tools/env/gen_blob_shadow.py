#!/usr/bin/env python3
"""The blob-shadow mask: one soft radial falloff, generated not authored.

WHY PROCEDURAL AND NOT gti. Everything else in this project's texture pipeline goes
through an image model because the asset carries STYLE — stone grain, a lava seam, an
eruption crown. A contact shadow carries none: it is a radial alpha ramp, fully
specified by one falloff curve. Generating it from a prompt would make a deterministic
2 KB file depend on a network call and a random seed, and the result would be a worse
version of the same gradient.

WHY A BLOB AT ALL. The dungeon runs four realtime POINT lights with
LightShadows.None (§E6 "caster 0"), and the URP asset ships
m_AdditionalLightShadowsSupported: 0 — so there is no shadow map to turn on without
either adding a directional light (which flattens the "dark room, four warm pools"
mood the stages are built around) or paying for four cube shadow maps on WebGL. A blob
buys the one thing shadows are actually for here — telling the player where an object
MEETS THE FLOOR at a 55 degree camera — for a quad and no shadow pass.

THE CURVE. Not a linear ramp: a real contact shadow is darkest directly under the
object and falls off fast, so the alpha uses a shaped falloff with a flat-ish core and
a long tail. A linear gradient reads as a painted grey disc.

Usage:
    python3 tools/env/gen_blob_shadow.py
"""
from pathlib import Path

import numpy as np
from PIL import Image

# 256 is plenty: the quad is never more than ~120 screen px at this camera, and the
# image is a smooth gradient with no detail to lose. 1024 would be 16x the memory for
# a ramp that is already band-free at 256.
SIZE = 256
OUT = Path("Assets/Resources/Fx/blob-shadow.png")


def main() -> int:
    axis = (np.arange(SIZE, dtype=np.float32) + 0.5) / SIZE * 2.0 - 1.0
    dx, dy = np.meshgrid(axis, axis)
    radius = np.sqrt(dx * dx + dy * dy)

    # Core stays near-opaque out to 0.35, then falls to zero by 1.0. The exponent
    # shapes the tail: 1.8 keeps the edge soft enough that the quad's own boundary is
    # never visible, which is the whole failure mode of a hard-edged blob.
    alpha = np.clip(1.0 - (radius - 0.35) / 0.65, 0.0, 1.0) ** 1.8
    alpha[radius > 1.0] = 0.0

    # Black RGB with a varying alpha, not a grey RGB on white: the shadow is composited
    # over whatever floor texture is beneath it, so the colour must not carry a value
    # of its own — only coverage.
    rgba = np.zeros((SIZE, SIZE, 4), dtype=np.uint8)
    rgba[..., 3] = np.clip(alpha * 255.0, 0, 255).astype(np.uint8)

    OUT.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(rgba, mode="RGBA").save(OUT, optimize=True)
    print(f"wrote {OUT} ({SIZE}x{SIZE}, {OUT.stat().st_size / 1024:.1f} KB)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
