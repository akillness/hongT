#!/usr/bin/env bash
# Re-skin every shipped character from the Abyssal-Surge motion library.
# Output: Assets/Art/Characters/<id>.fbx + _workspace/current/engineering/reskin/<id>.json
#
# !! THIS SCRIPT CANNOT RUN TODAY — and not because of its id list. [OBSERVED
# !! 2026-08-09] The source it reads, ~/orca/Abyssal-Surge, is no longer the
# !! Three.js original: it has been rebuilt as a Unity project (Assets/,
# !! *.csproj, HEAD 96dde3ba "fix(webgl): bake Korean glyphs..."), and the
# !! lowercase asset library this driver walks — assets/motion/ingame/... and
# !! assets/mesh/... — does not exist in it. Every one of the 12 ids misses,
# !! including the original 8 that predate today's edit (verified: guard's
# !! model.glb and scout's base_basic_pbr.glb both absent).
# !!
# !! So the ids and paths below are RECOVERED PROVENANCE, not a working build
# !! step: they record exactly what produced each shipped FBX, and they will
# !! work again the moment the motion library is restored (set
# !! ABYSSAL_SURGE_ROOT to wherever it lands). The shipped FBX files in
# !! Assets/Art/Characters are the artifacts of record until then.
# !!
# !! FOR NEW MESHES, USE tools/blender/reskin_from_fbx.py INSTEAD. It takes its
# !! skeleton from a shipped Assets/Art/Characters/<id>.fbx, which already
# !! carries the 22 canonical humanoid bones (the BONE_MAP rename ran on them
# !! long ago), so it does not need the vanished library at all. PROVEN
# !! 2026-08-09 end to end: scout mesh onto scout's skeleton -> Unity avatar
# !! isValid && isHuman, 22/22 bones mapped, 0 heat orphans. It carries a
# !! height-ratio gate because a 1.51 mismatch silently produces a non-human
# !! avatar; see that script's header for the measurement.
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

# 2026-08-09: the list was 8 while Assets/Art/Characters held 12. The four
# newer ids (s1/s2/s3 stage bosses + the player mesh) were reskinned ad hoc and
# never came back into the driver, so CharacterImportPipeline's own failure
# hint — "run tools/blender/reskin_all.sh" — was a lie for a third of the cast.
# Recovered from the reports those ad-hoc runs left behind
# (_workspace/current/engineering/reskin/<id>.json: `input`, `meshSource`).
IDS=(guard lantern-reaver ember-cohort scout shade possessed
     shadow-commander-boss broken-court-monarch-boss
     s1-cinder-warden s2-veil-tactician s3-gate-sovereign human-command-boss)

# SKELETON source. Defaults to the id's own motion-library model, but the three
# stage bosses have no motion-library entry of their own — they were rigged on
# the shadow-commander skeleton and given a different mesh, which is exactly
# what their reports record as `input`. Without this the driver would look for
# characters/s1-cinder-warden/model.glb and fail on a path that never existed.
skeleton_glb_for() {
  case "$1" in
    s1-cinder-warden|s2-veil-tactician|s3-gate-sovereign)
      echo "$SRC/assets/motion/ingame/characters/shadow-commander-boss/model.glb" ;;
    *)
      echo "$SRC/assets/motion/ingame/characters/$1/model.glb" ;;
  esac
}

mesh_glb_for() {
  case "$1" in
    lantern-reaver)             echo "$SRC/assets/mesh/character/lantern-reaver-character/glb/base_basic_pbr.glb" ;;
    scout)                     echo "$SRC/assets/mesh/enemy/scout/glb/base_basic_pbr.glb" ;;
    shade)                     echo "$SRC/assets/mesh/enemy/shade/glb/base_basic_pbr.glb" ;;
    possessed)                 echo "$SRC/assets/mesh/enemy/possessed/glb/base_basic_pbr.glb" ;;
    shadow-commander-boss)     echo "$SRC/assets/mesh/enemy/shadow-commander-boss/glb/base_basic_pbr.glb" ;;
    broken-court-monarch-boss) echo "$SRC/assets/mesh/character/broken-court-monarch-boss-character/glb/base_basic_pbr.glb" ;;
    s1-cinder-warden)          echo "$SRC/assets/mesh/boss/s1-cinder-warden/glb/base_basic_pbr.glb" ;;
    s2-veil-tactician)         echo "$SRC/assets/mesh/boss/s2-veil-tactician/glb/base_basic_pbr.glb" ;;
    s3-gate-sovereign)         echo "$SRC/assets/mesh/boss/s3-gate-sovereign/glb/base_basic_pbr.glb" ;;
    # human-command-boss embeds its own authored mesh — no swap (report:
    # meshSwap null, 7711 tris straight out of the motion-library model).
    *)                         echo "" ;;
  esac
}

FAILED=0
for id in "${IDS[@]}"; do
  GLB="$(skeleton_glb_for "$id")"
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
