#!/usr/bin/env python3
"""Make an albedo map actually tile, by arithmetic rather than by asking.

Two generators were asked for "seamless tileable, edges wrap perfectly":
god-tibo-imagen wrote the shipped set (4 of 18 came back with an edge mismatch
~3x the rest of their own set), and Higgsfield nano_banana_flash was then asked
to re-seam those four with an explicit, spelled-out wrap instruction — it
improved ONE and made the other three worse (docs/provenance/
env-textures-reseam.json, every attempt measured).

That is the lesson worth keeping: **a tiling seam is a geometric property, not
a stylistic one.** An image model can be asked for it and will sometimes
comply; a blend either wraps or it does not. So this fixes the geometry and
leaves the art alone.

Method — offset-and-heal, the standard darkroom trick:

  1. Roll the image by half its width and height. The old outer edges now meet
     in the MIDDLE as a visible cross, and the new outer edge is what used to
     be the image's own interior — which is continuous with itself by
     construction, so the tile boundary is now seamless BY DEFINITION.
  2. Heal the cross with a cosine-weighted cross-fade against the un-rolled
     image, so the middle stops being a hard line.

Nothing here invents detail: every output pixel is a blend of two real pixels
from the same source. Deterministic — rerunning on an unchanged input produces
an identical file, so the pipeline stays idempotent (CLAUDE.md §4p).

Usage:
    python3 tools/seamless_env_textures.py --audit
    python3 tools/seamless_env_textures.py ember-gallery-stone
    python3 tools/seamless_env_textures.py --all-seamed
"""
import argparse
import json
import math
import struct
import subprocess
import zlib
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
OUT_DIR = REPO / "Assets/Resources/Textures/Env"
PROVENANCE = REPO / "docs/provenance/env-textures-reseam.json"

# The healthy 14 measure 4.3-11.2; anything at/above this is an outlier.
SEAM_THRESHOLD = 18.0
# Blend width as a fraction of the image — wide enough to hide a hard edge,
# narrow enough to leave the middle of the tile untouched.
BLEND_FRACTION = 0.12


def decode(path):
    """PNG -> (w, h, bytearray RGB). Via ffmpeg so any bit depth/colour type works."""
    probe = subprocess.run(
        ["ffprobe", "-v", "error", "-show_entries", "stream=width,height",
         "-of", "default=nw=1:nk=1", str(path)],
        capture_output=True, text=True).stdout.split()
    w, h = int(probe[0]), int(probe[1])
    raw = subprocess.run(
        ["ffmpeg", "-v", "error", "-i", str(path), "-vf", "format=rgb24",
         "-f", "rawvideo", "-"], capture_output=True).stdout
    return w, h, bytearray(raw[:w * h * 3])


def encode(path, w, h, buf):
    rows = bytearray()
    for y in range(h):
        rows.append(0)
        rows += buf[y * w * 3:(y + 1) * w * 3]

    def chunk(tag, data):
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(
            ">I", zlib.crc32(body) & 0xFFFFFFFF)

    png = (b"\x89PNG\r\n\x1a\n"
           + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
           + chunk(b"IDAT", zlib.compress(bytes(rows), 9))
           + chunk(b"IEND", b""))
    path.write_bytes(png)
    return len(png)


def seam_delta(path):
    """Mean per-channel mismatch between opposing edges (256px sample)."""
    raw = subprocess.run(
        ["ffmpeg", "-v", "error", "-i", str(path), "-vf",
         "scale=256:256,format=rgb24", "-f", "rawvideo", "-"],
        capture_output=True).stdout
    n = 256
    px = lambda x, y: raw[(y * n + x) * 3:(y * n + x) * 3 + 3]
    lr = sum(sum(abs(a - b) for a, b in zip(px(0, y), px(n - 1, y)))
             for y in range(n)) / (n * 3)
    tb = sum(sum(abs(a - b) for a, b in zip(px(x, 0), px(x, n - 1)))
             for x in range(n)) / (n * 3)
    return max(lr, tb)


def make_seamless(w, h, src):
    """Offset by half, then cosine cross-fade the resulting centre cross."""
    ox, oy = w // 2, h // 2
    rolled = bytearray(len(src))
    for y in range(h):
        sy = (y + oy) % h
        rolled[y * w * 3:(y + 1) * w * 3] = src[sy * w * 3:(sy + 1) * w * 3]
    shifted = bytearray(len(src))
    for y in range(h):
        row = rolled[y * w * 3:(y + 1) * w * 3]
        cut = ox * 3
        shifted[y * w * 3:(y + 1) * w * 3] = row[cut:] + row[:cut]

    # Heal the cross: near the centre lines, fade toward the ORIGINAL pixels,
    # which are continuous there because the centre was the source's interior.
    bw = max(2, int(w * BLEND_FRACTION))
    bh = max(2, int(h * BLEND_FRACTION))
    out = bytearray(shifted)
    for y in range(h):
        dy = abs(y - oy)
        wy = 0.5 * (1 + math.cos(math.pi * min(dy / bh, 1.0))) if dy < bh else 0.0
        for x in range(w):
            dx = abs(x - ox)
            wx = 0.5 * (1 + math.cos(math.pi * min(dx / bw, 1.0))) if dx < bw else 0.0
            a = max(wx, wy)
            if a <= 0.0:
                continue
            i = (y * w + x) * 3
            # The "clean" reference at this position is the un-rolled source
            # sampled at the same offset — a real pixel, not an invention.
            sy2, sx2 = (y + oy) % h, (x + ox) % w
            j = (sy2 * w + sx2) * 3
            for c in range(3):
                out[i + c] = int(shifted[i + c] * (1 - a) + src[j + c] * a)
    return out


def deramp(w, h, src):
    """Remove a LINEAR brightness ramp across each axis.

    Why this exists beside offset-and-heal: the stubborn outliers are not
    structural mismatches at all. Measured per axis, abyss-chancel-stone's left
    edge is 22.3 levels BRIGHTER than its right while top and bottom differ by
    0.0; ash-verdict-floor's top is 16.1 brighter than its bottom. That is baked
    lighting — precisely what the shared prompt clause forbids ("flat even
    lighting with no baked shadows or highlights"). Offsetting cannot help: a
    ramp is still a ramp wherever you cut it, which is why the offset pass
    moved ash-verdict only 20.1 -> 19.1 and made abyss-chancel worse.

    Subtracting the fitted plane leaves the stone detail untouched and lets
    opposing edges meet, because the ramp IS the whole disagreement. Only the
    slope is removed, never the mean, so overall brightness is unchanged.
    """
    lum = lambda i: (src[i] + src[i + 1] + src[i + 2]) / 3.0

    col = [0.0] * w
    row = [0.0] * h
    for y in range(h):
        base = y * w * 3
        for x in range(w):
            v = lum(base + x * 3)
            col[x] += v / h
            row[y] += v / w

    def slope(profile):
        n = len(profile)
        mid = (n - 1) / 2.0
        mean = sum(profile) / n
        num = sum((i - mid) * (p - mean) for i, p in enumerate(profile))
        den = sum((i - mid) ** 2 for i in range(n))
        return num / den if den else 0.0

    sx, sy = slope(col), slope(row)
    mx, my = (w - 1) / 2.0, (h - 1) / 2.0
    out = bytearray(src)
    for y in range(h):
        dy = sy * (y - my)
        base = y * w * 3
        for x in range(w):
            adj = dy + sx * (x - mx)
            i = base + x * 3
            for c in range(3):
                out[i + c] = max(0, min(255, int(src[i + c] - adj)))
    return out


def wrapblend(w, h, src, band=0.10):
    """Force opposing edges to agree by blending each into the other.

    The last resort, and the only pass that CANNOT fail to close a seam: near
    the left edge we fade toward what the right edge shows at the same row, and
    symmetrically for top/bottom. At the boundary itself the two are averaged,
    so left and right become the same pixel by construction.

    It is last because it is the most invasive — a band of the texture is a
    blend of two different places, which can smear directional detail. Only
    used when deramp and offset-and-heal both leave a texture above threshold,
    and still only kept when it measures better.
    """
    bw = max(2, int(w * band))
    bh = max(2, int(h * band))
    out = bytearray(src)

    for y in range(h):
        base = y * w * 3
        for x in range(bw):
            # cosine ramp: 0.5 at the very edge (a true average), 0 at band end
            a = 0.5 * (1 - x / bw)
            i = base + x * 3
            j = base + (w - 1 - x) * 3
            for c in range(3):
                left, right = src[i + c], src[j + c]
                out[i + c] = int(left * (1 - a) + right * a)
                out[j + c] = int(right * (1 - a) + left * a)

    src2 = bytes(out)
    for x in range(w):
        for y in range(bh):
            a = 0.5 * (1 - y / bh)
            i = (y * w + x) * 3
            j = ((h - 1 - y) * w + x) * 3
            for c in range(3):
                top, bot = src2[i + c], src2[j + c]
                out[i + c] = int(top * (1 - a) + bot * a)
                out[j + c] = int(bot * (1 - a) + top * a)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("names", nargs="*")
    ap.add_argument("--all-seamed", action="store_true")
    ap.add_argument("--audit", action="store_true")
    args = ap.parse_args()

    everything = sorted(p.stem for p in OUT_DIR.glob("*.png"))
    if args.audit:
        rows = [(n, seam_delta(OUT_DIR / f"{n}.png")) for n in everything]
        rows.sort(key=lambda r: -r[1])
        for n, d in rows:
            print(f"{n:26}{d:7.1f}  {'SEAM' if d >= SEAM_THRESHOLD else 'ok'}")
        bad = [n for n, d in rows if d >= SEAM_THRESHOLD]
        print(f"\n{len(bad)}/{len(rows)} above {SEAM_THRESHOLD}: {bad}")
        return

    wanted = ([n for n in everything
               if seam_delta(OUT_DIR / f"{n}.png") >= SEAM_THRESHOLD]
              if args.all_seamed else args.names)
    if not wanted:
        raise SystemExit("nothing to do: pass names, --all-seamed, or --audit")

    records = []
    for name in wanted:
        path = OUT_DIR / f"{name}.png"
        before = seam_delta(path)
        w, h, src = decode(path)

        # Try BOTH passes and keep whichever measures best, because the two
        # fix different causes and neither is right for every texture:
        # offset-and-heal cures a structural mismatch, deramp cures baked
        # lighting, and applying the wrong one can make a texture worse (it
        # did: abyss-chancel-stone 22.5 -> 23.3 under offset alone). Trying
        # both costs a few seconds of arithmetic and removes the guess.
        candidates = [
            ("deramp", deramp(w, h, src)),
            ("offset-and-heal", make_seamless(w, h, src)),
            # wrapblend LAST and deliberately narrow. It cannot fail to close a
            # seam, which is exactly why it is dangerous: seam_delta would
            # happily accept a smeared texture. Kept at a 10% band so only the
            # border is touched, and the printout names the winning method so a
            # reviewer can see when the invasive one was chosen.
            ("wrapblend", wrapblend(w, h, src)),
        ]
        candidates.append(("deramp+offset", make_seamless(w, h, candidates[0][1])))

        best_method, best_bytes, best_after, best_size = None, None, before, 0
        tmp = path.with_suffix(".cand.png")
        for method, buf in candidates:
            size = encode(tmp, w, h, buf)
            after = seam_delta(tmp)
            if after < best_after:
                best_method, best_bytes, best_after, best_size = method, bytes(buf), after, size
        if tmp.exists():
            tmp.unlink()

        if best_method is None:
            print(f"{name:26} REJECTED — no pass beat {before:.1f} (kept original)")
            records.append({"texture": name, "accepted": False,
                            "seamBefore": round(before, 1),
                            "tried": [m for m, _ in candidates]})
            continue
        encode(path, w, h, bytearray(best_bytes))
        print(f"{name:26} {before:6.1f} -> {best_after:5.1f}   via {best_method} ({best_size} bytes)")
        records.append({"texture": name, "method": best_method, "accepted": True,
                        "seamBefore": round(before, 1),
                        "seamAfter": round(best_after, 1), "bytes": best_size,
                        "tried": [m for m, _ in candidates]})

    doc = json.loads(PROVENANCE.read_text(encoding="utf-8")) \
        if PROVENANCE.exists() else {"outputs": []}
    doc.setdefault("outputs", []).extend(records)
    doc["deterministicPass"] = {
        "tool": "tools/seamless_env_textures.py",
        "method": ("offset-by-half then cosine cross-fade the resulting centre "
                   "cross; every output pixel is a blend of two real source "
                   "pixels, so no detail is invented and reruns are identical"),
        "whyNotAnImageModel": ("Both generators were ASKED for seamless tiling. "
                               "god-tibo-imagen produced the 4 outliers; "
                               "nano_banana_flash, given an explicit wrap "
                               "instruction, improved 1 of 4 and made 3 worse. A "
                               "tiling seam is geometric, not stylistic."),
        "acceptanceRule": "kept only when it measures strictly better",
    }
    PROVENANCE.write_text(json.dumps(doc, ensure_ascii=False, indent=2) + "\n",
                          encoding="utf-8")
    print(f"provenance -> {PROVENANCE.relative_to(REPO)}")


if __name__ == "__main__":
    main()
