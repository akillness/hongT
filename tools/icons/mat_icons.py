# Magenta chroma-key matting for gti icon masters.
# raw/*.png (magenta bg) -> Assets/Resources/Icons/<id>.png (256x256 RGBA)
# Same keying rule as the character pipeline (docs/character-asset-pipeline.md).
import sys
from pathlib import Path

from PIL import Image

RAW = Path("_workspace/current/engineering/icons/raw")
OUT = Path("Assets/Resources/Icons")
OUT.mkdir(parents=True, exist_ok=True)

SIZE = 256
MARGIN = 0.06  # fraction of canvas left empty around the glyph


def key_magenta(img: Image.Image) -> Image.Image:
    rgba = img.convert("RGBA")
    px = rgba.load()
    w, h = rgba.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            # magenta: high R, low G, high B
            if r > 160 and b > 160 and g < 110 and abs(r - b) < 80:
                px[x, y] = (0, 0, 0, 0)
            elif r > 120 and b > 120 and g < r - 60 and g < b - 60:
                # despill halo: soften toward transparent
                px[x, y] = (r, g, b, max(0, a - 160))
    return rgba


def tight_bbox(img: Image.Image):
    alpha = img.getchannel("A")
    return alpha.getbbox()


def process(path: Path) -> str:
    img = key_magenta(Image.open(path))
    box = tight_bbox(img)
    if box is None:
        return f"EMPTY {path.name}"
    img = img.crop(box)
    # fit into SIZE x SIZE with margin, keep aspect
    inner = int(SIZE * (1 - 2 * MARGIN))
    scale = min(inner / img.width, inner / img.height)
    nw, nh = max(1, int(img.width * scale)), max(1, int(img.height * scale))
    img = img.resize((nw, nh), Image.LANCZOS)
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.paste(img, ((SIZE - nw) // 2, (SIZE - nh) // 2), img)
    out = OUT / path.name
    canvas.save(out)
    return f"OK {path.name} -> {out}"

# 9-slice plates: width-normalized, aspect preserved, NO square canvas -
# spriteBorder (30,14,30,14) in IconImportPipeline indexes real plate pixels.
PLATES = {"ui-button"}


def process_plate(path: Path) -> str:
    img = key_magenta(Image.open(path))
    box = tight_bbox(img)
    if box is None:
        return f"EMPTY {path.name}"
    img = img.crop(box)
    scale = SIZE / img.width
    img = img.resize((SIZE, max(1, round(img.height * scale))), Image.LANCZOS)
    out = OUT / path.name
    img.save(out)
    return f"OK {path.name} -> {out} ({img.width}x{img.height})"

# Multi-element masters: one raw image, N sprites split at the widest
# transparent vertical gap (left-to-right). Each part gets the square path.
SPLITS = {"ui-joystick": ("ui-joystick-base", "ui-joystick-nub")}


def process_split(path: Path, names) -> str:
    img = key_magenta(Image.open(path))
    alpha = img.load()
    w, h = img.size
    # column alpha mass; split at the emptiest column in the middle third
    def col_mass(x):
        return sum(alpha[x, y][3] for y in range(0, h, 4))
    third = w // 3
    gap_x = min(range(third, 2 * third), key=col_mass)
    parts = [img.crop((0, 0, gap_x, h)), img.crop((gap_x, 0, w, h))]
    lines = []
    for part, name in zip(parts, names):
        box = part.getchannel("A").getbbox()
        if box is None:
            return f"EMPTY {path.name}:{name}"
        part = part.crop(box)
        scale = (SIZE * 0.94) / max(part.width, part.height)
        nw, nh = max(1, int(part.width * scale)), max(1, int(part.height * scale))
        part = part.resize((nw, nh), Image.LANCZOS)
        canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
        canvas.paste(part, ((SIZE - nw) // 2, (SIZE - nh) // 2), part)
        canvas.save(OUT / f"{name}.png")
        lines.append(name)
    return f"OK {path.name} -> {', '.join(lines)}"


def main() -> int:
    # Scoped by default-refusal, NOT by "process everything found".
    #
    # gen_icons.sh is incremental (run_one prints "SKIP (exists)"), but this
    # matter used to be exhaustive: adding two icons re-wrote all 22. That
    # asymmetry produced a real regression in cycle-7 — ui-button came back
    # 256x106 instead of 256x96 from an UNCHANGED raw master, because the
    # committed plate carries a 1px transparent margin this pass does not
    # reproduce. spriteBorder (30,14,30,14) indexes real plate pixels, so a
    # 10px height shift moves the 9-slice corners off the corner art of every
    # button in the game.
    #
    # Same input, different output means this pass is not reproducible across
    # environments, so re-matting an icon nobody asked for can only lose.
    # Name the icons you mean:  python3 tools/icons/mat_icons.py ui-codex ...
    # `--all` still exists for a deliberate full re-derive.
    args = [a for a in sys.argv[1:] if a != "--all"]
    want_all = "--all" in sys.argv[1:]
    raws = sorted(RAW.glob("*.png"))
    if not raws:
        print("no raw icons found", file=sys.stderr)
        return 1
    if not want_all:
        if not args:
            print("refusing to re-matte every icon. Name the stems you mean, "
                  "or pass --all for a deliberate full re-derive.\n"
                  f"  available: {', '.join(p.stem for p in raws)}", file=sys.stderr)
            return 2
        wanted = set(args)
        raws = [p for p in raws if p.stem in wanted]
        missing = wanted - {p.stem for p in raws}
        if missing:
            print(f"no raw master for: {', '.join(sorted(missing))}", file=sys.stderr)
            return 2
    bad = 0
    for p in raws:
        if p.stem in SPLITS:
            line = process_split(p, SPLITS[p.stem])
        elif p.stem in PLATES:
            line = process_plate(p)
        else:
            line = process(p)
        print(line)
        if not line.startswith("OK"):
            bad += 1
    print(f"=== MATTED {len(raws) - bad}/{len(raws)} ===")
    return 1 if bad else 0


if __name__ == "__main__":
    raise SystemExit(main())
