#!/usr/bin/env bash
# Deterministic offline slicer for the HUD chrome atlas (no network calls).
# Crops the single gti-generated 4x4 atlas into 16 named HUD sprites,
# color-keys the near-black cell background to real alpha (matching the
# existing Assets/Resources/Icons/*.png convention — verified via PIL:
# ui-button.png corners are alpha=0), and writes them straight into
# Assets/Resources/Icons/ so IconImportPipeline.cs picks them up on next
# Unity reimport.
set -euo pipefail

SRC="$1"                       # source atlas PNG
OUT_DIR="${2:-../../../../Assets/Resources/Icons}"
BG="#0a0810"                   # matches the prompt's canvas background
FUZZ="9%"

command -v magick >/dev/null || { echo "ImageMagick 'magick' not found" >&2; exit 1; }

W=$(magick identify -format "%w" "$SRC")
H=$(magick identify -format "%h" "$SRC")
COLS=4
ROWS=4
CELL_W=$(( W / COLS ))
CELL_H=$(( H / ROWS ))
# Inset trims the divider-line bleed at each cell edge before autotrim.
INSET_X=$(( CELL_W * 6 / 100 ))
INSET_Y=$(( CELL_H * 6 / 100 ))

mkdir -p "$OUT_DIR"

# name:targetWxH  (row-major, matches the prompt's 16-tile layout)
TILES=(
  "hud-hp-bar-frame:256x64"
  "hud-hp-bar-fill:256x64"
  "hud-oil-bar-frame:256x64"
  "hud-oil-bar-fill:256x64"
  "hud-xp-bar-frame:256x64"
  "hud-xp-bar-fill:256x64"
  "hud-meters-panel-bg:256x160"
  "hud-stats-panel-bg:256x160"
  "hud-skill-card-frame:160x160"
  "hud-skill-card-frame-ready:160x160"
  "hud-boss-bar-frame:256x38"
  "hud-boss-bar-fill:256x38"
  "hud-extraction-ring-frame:256x64"
  "hud-extraction-ring-fill:256x64"
  "hud-combo-pip-gem:64x64"
  "hud-shield-readout-frame:256x64"
)

i=0
for entry in "${TILES[@]}"; do
  name="${entry%%:*}"
  target="${entry##*:}"
  row=$(( i / COLS ))
  col=$(( i % COLS ))
  x=$(( col * CELL_W + INSET_X ))
  y=$(( row * CELL_H + INSET_Y ))
  cw=$(( CELL_W - 2 * INSET_X ))
  ch=$(( CELL_H - 2 * INSET_Y ))

  tmp="$(mktemp /tmp/hud-tile-XXXX.png)"
  magick "$SRC" -crop "${cw}x${ch}+${x}+${y}" +repage \
    -fuzz "$FUZZ" -trim +repage \
    -resize "${target}" \
    -background "$BG" -gravity center -extent "${target}" \
    "$tmp"

  out="$OUT_DIR/$name.png"
  W2=$(magick identify -format "%w" "$tmp")
  H2=$(magick identify -format "%h" "$tmp")
  magick "$tmp" -fuzz "$FUZZ" -fill none \
    -draw "color 0,0 floodfill" \
    -draw "color $((W2-1)),0 floodfill" \
    -draw "color 0,$((H2-1)) floodfill" \
    -draw "color $((W2-1)),$((H2-1)) floodfill" \
    "$out"
  rm -f "$tmp"
  echo "wrote $out (${target}, cell ${col},${row})"
  i=$(( i + 1 ))
done
