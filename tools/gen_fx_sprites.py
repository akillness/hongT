#!/usr/bin/env python3
"""Generate the deterministic soft-glow sprite the §V3 particle seed samples.

Why a generator and not an image model: this asset is a mathematical falloff,
not art. A radial gradient written from a formula is exact, reproducible byte
for byte, perfectly centred, and free of the banding/noise/off-centre bias an
image model introduces. Regenerating it must never move a pixel, because the
particle seed material references it by GUID and the reel/QA evidence is
measured against it.

Output: Assets/Resources/Textures/Fx/soft-glow-256.png (RGBA, 256x256).

The alpha profile is a gamma-eased radial falloff:

    a(r) = (1 - clamp(r / R, 0, 1)) ** GAMMA        r = distance from centre

GAMMA > 1 keeps a bright compact core with a long thin tail, which is what
reads as "light" once the additive blend accumulates and Bloom (threshold 1.05,
Assets/Settings/CinderPostProfile.asset) clips the core. RGB stays pure white:
every call site tints via _BaseColor / per-particle vertex colour, so baking a
hue here would double-tint.

No third-party dependency (no PIL): PNG is written directly with zlib+struct so
the script runs on any python3, including a clean CI image.
"""
import struct
import zlib
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
OUT = REPO / "Assets/Resources/Textures/Fx/soft-glow-256.png"

SIZE = 256
GAMMA = 2.2      # falloff easing; >1 = compact core, long tail
CORE = 0.10      # inner fraction held at full alpha so the core is not a spike


def alpha_at(dx: float, dy: float) -> int:
    """Alpha byte for a pixel offset from the sprite centre."""
    r = (dx * dx + dy * dy) ** 0.5 / (SIZE / 2.0)
    if r >= 1.0:
        return 0
    if r <= CORE:
        return 255
    t = (r - CORE) / (1.0 - CORE)      # 0 at core edge, 1 at sprite edge
    return int(round(255.0 * (1.0 - t) ** GAMMA))


def build_png() -> bytes:
    centre = (SIZE - 1) / 2.0
    raw = bytearray()
    for y in range(SIZE):
        raw.append(0)                   # PNG filter type 0 (None) per scanline
        dy = y - centre
        for x in range(SIZE):
            a = alpha_at(x - centre, dy)
            raw += bytes((255, 255, 255, a))

    def chunk(tag: bytes, data: bytes) -> bytes:
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(
            ">I", zlib.crc32(body) & 0xFFFFFFFF)

    ihdr = struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0)  # 8-bit RGBA
    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", ihdr)
            + chunk(b"IDAT", zlib.compress(bytes(raw), 9))
            + chunk(b"IEND", b""))


def main() -> None:
    OUT.parent.mkdir(parents=True, exist_ok=True)
    png = build_png()
    if OUT.exists() and OUT.read_bytes() == png:
        print(f"SKIP {OUT.relative_to(REPO)} (byte-identical)")
        return
    OUT.write_bytes(png)
    print(f"wrote {OUT.relative_to(REPO)} ({len(png)} bytes, {SIZE}x{SIZE} RGBA)")


if __name__ == "__main__":
    main()
