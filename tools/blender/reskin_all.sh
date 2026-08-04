#!/usr/bin/env bash
# Re-skin all 7 roster characters from the Abyssal-Surge motion library.
# Output: Assets/Art/Characters/<id>.fbx + _workspace/current/engineering/reskin/<id>.json
#
# Mesh sources (audited 2026-08-04, _workspace/current/engineering/reskin):
# - guard, ember-cohort: motion-library model.glb embeds the real authored mesh
#   (heat solves cleanly: orphans 0 / 3).
# - scout, shade, possessed, shadow-commander-boss: model.glb embeds a 42-vert
#   blockout box -> swap in the authored Rodin mesh via --mesh-glb.
# - broken-court-monarch-boss: model.glb is a 10-region fused mesh where bone
#   heat fails on 100% of verts -> swap in the clean Rodin mesh too.
set -uo pipefail
cd "$(dirname "$0")/../.."

SRC="${ABYSSAL_SURGE_ROOT:-$(cd ../../../Abyssal-Surge && pwd)}"
BLENDER="${BLENDER_BIN:-/Applications/Blender.app/Contents/MacOS/Blender}"
OUT_DIR="Assets/Art/Characters"
REPORT_DIR="_workspace/current/engineering/reskin"
mkdir -p "$OUT_DIR" "$REPORT_DIR"

IDS=(guard ember-cohort scout shade possessed shadow-commander-boss broken-court-monarch-boss)

mesh_glb_for() {
  case "$1" in
    scout)                     echo "$SRC/assets/mesh/enemy/scout/glb/base_basic_pbr.glb" ;;
    shade)                     echo "$SRC/assets/mesh/enemy/shade/glb/base_basic_pbr.glb" ;;
    possessed)                 echo "$SRC/assets/mesh/enemy/possessed/glb/base_basic_pbr.glb" ;;
    shadow-commander-boss)     echo "$SRC/assets/mesh/enemy/shadow-commander-boss/glb/base_basic_pbr.glb" ;;
    broken-court-monarch-boss) echo "$SRC/assets/mesh/character/broken-court-monarch-boss-character/glb/base_basic_pbr.glb" ;;
    *)                         echo "" ;;
  esac
}

FAILED=0
for id in "${IDS[@]}"; do
  GLB="$SRC/assets/motion/ingame/characters/$id/model.glb"
  MESH="$(mesh_glb_for "$id")"
  EXTRA=()
  [ -n "$MESH" ] && EXTRA=(--mesh-glb "$MESH")
  # monarch: fused geometry (28% dup verts) — bone-heat only solves with the
  # envelope scale; boss reads fine 22% larger (gets 1.6x gameplay scale anyway).
  [ "$id" = "broken-court-monarch-boss" ] && EXTRA+=(--mesh-scale-mode span)
  echo "=== RESKIN $id ${MESH:+(mesh swap)} ==="
  if ! "$BLENDER" -b --factory-startup --python-exit-code 1 \
      -P tools/blender/reskin_character.py -- \
      --glb "$GLB" \
      --out "$OUT_DIR/$id.fbx" \
      --report "$REPORT_DIR/$id.json" \
      --max-tris 25000 ${EXTRA[@]+"${EXTRA[@]}"} > "$REPORT_DIR/$id.log" 2>&1; then
    echo "FAILED $id (see $REPORT_DIR/$id.log)"
    FAILED=$((FAILED+1))
  else
    tail -1 "$REPORT_DIR/$id.log"
  fi
done
echo "=== DONE: $((${#IDS[@]}-FAILED))/${#IDS[@]} succeeded ==="
exit $FAILED
