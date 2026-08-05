#!/usr/bin/env python3
"""Generate the Cinder Court UI button plates.

Why procedural rather than an image model: the 9-slice border must be
pixel-exact. The shipped ui-button.png was 256x106 with a 30 px border, which
makes the horizontal centre of a 52 u button NEGATIVE (-8 u) and the vertical
centre of an 84x28 button exactly ZERO -- 7 of 26 plated buttons rendered with
their borders overlapping. A 12 px border on a 256x96 plate clears every
button size in use. Reproducibility matters more than painterly detail here,
because the numbers have to be re-derived whenever a button size changes.

Palette is the repo's own (Assets/Scripts/View/LobbyView.cs L33-40), pulled
down toward the panel charcoal: the literal LobbyView tints were authored as
flat Image tints over a dark canvas, and used as a plate BODY they read as
bright plastic -- the opposite of the "dark stone, low blue-black fill" the
interview spec calls for.

Run:  python3 tools/ui/gen_ui_plates.py
"""
import os
import random

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Resources", "Icons")

EMBER = (0xF3, 0x59, 0x2C)
CYAN = (0x2C, 0xAD, 0xD6)
GOLD = (0xDD, 0xC8, 0x69)
BTNBACK = (26, 22, 38)
BTNACT = (52, 42, 26)
DISABLED = (18, 16, 24)

W, H = 256, 96
CORNER = 14


def stone_grain(size, seed, amount=10):
    """Low-frequency mottling so the plate reads as a struck surface rather
    than a filled rectangle. Blurred hard, so 9-slice stretching smears
    something already smooth and never reveals a repeating pixel pattern."""
    rng = random.Random(seed)
    small = Image.new("L", (max(1, size[0] // 8), max(1, size[1] // 8)))
    small.putdata([128 + rng.randint(-amount * 6, amount * 6)
                   for _ in range(small.size[0] * small.size[1])])
    return small.resize(size, Image.BICUBIC).filter(ImageFilter.GaussianBlur(3))


def plate(fill, edge, edge_alpha=170, inner_glow=None, seed=7):
    """A dark stone plate with a thin lit rim.

    Every piece of detail sits within the 12 px border region or is uniform
    across the centre, so 9-slice stretching cannot distort it.
    """
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))

    # Vertical gradient: lit along the top, sinking toward black at the
    # bottom. This is what makes it read as a slab under a key light rather
    # than a coloured card.
    grad = Image.new("L", (1, H))
    grad.putdata([int(255 * (0.42 + 0.58 * (1.0 - y / float(H - 1)) ** 1.7))
                  for y in range(H)])
    grad = grad.resize((W, H))
    body = Image.composite(
        Image.new("RGBA", (W, H), fill + (255,)),
        Image.new("RGBA", (W, H), tuple(int(c * 0.34) for c in fill) + (255,)),
        grad)

    # Stone grain over the gradient.
    body = Image.composite(
        body,
        Image.new("RGBA", (W, H), tuple(int(c * 0.72) for c in fill) + (255,)),
        stone_grain((W, H), seed))

    mask = Image.new("L", (W, H), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        [1, 1, W - 2, H - 2], radius=CORNER, fill=238)
    img.paste(body, (0, 0), mask)

    d = ImageDraw.Draw(img)
    # Lit rim, brighter along the top edge -- the key light comes from above
    # (SceneBuilder directional light, Euler 55/-28).
    d.rounded_rectangle([1, 1, W - 2, H - 2], radius=CORNER,
                        outline=edge + (edge_alpha,), width=2)
    d.line([CORNER, 2, W - CORNER, 2],
           fill=tuple(min(255, int(c * 1.3)) for c in edge) + (edge_alpha,), width=1)
    # Bottom edge takes a darker line so the slab reads as having thickness.
    d.line([CORNER, H - 3, W - CORNER, H - 3],
           fill=tuple(int(c * 0.3) for c in edge) + (edge_alpha,), width=1)

    if inner_glow:
        g = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        ImageDraw.Draw(g).rounded_rectangle(
            [3, 3, W - 4, H - 4], radius=CORNER - 2, outline=inner_glow + (110,), width=3)
        img = Image.alpha_composite(img, g.filter(ImageFilter.GaussianBlur(2.5)))
    return img


def main():
    os.makedirs(OUT, exist_ok=True)
    specs = [
        ("ui-button", BTNBACK, CYAN, 150, None, 7),
        ("ui-button-active", BTNACT, GOLD, 215, EMBER, 11),
        ("ui-button-disabled", DISABLED, (70, 74, 92), 100, None, 3),
    ]
    for name, fill, edge, alpha, glow, seed in specs:
        path = os.path.join(OUT, name + ".png")
        plate(fill, edge, alpha, glow, seed).save(path)
        print("%-22s %dx%d  %.1f KB" % (name + ".png", W, H, os.path.getsize(path) / 1024))

    print("\ncentre size with border (12,8,12,8) -- all must be > 0:")
    for label, w, h in [("stat +", 52, 44), ("stage drop", 84, 28),
                        ("skill card", 108, 72), ("tab", 120, 40),
                        ("text button", 144, 34)]:
        cw, ch = w - 24, h - 16
        print("  %-13s %3d x %3d  %s" % (label, cw, ch, "ok" if cw > 0 and ch > 0 else "CRUSHED"))


if __name__ == "__main__":
    main()
