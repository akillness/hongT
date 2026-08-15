#!/usr/bin/env bash
# The court floor plate — a FLOOR, not a picture of a room.
#
# WHAT IS BEING REPLACED AND WHY. Assets/Art/Textures/cinder-court-backdrop.png is a
# fully painted isometric SCENE: it has its own stairs, columns, braziers, an arched
# doorway, hanging lanterns and railings drawn into it, lit from a fixed direction.
# SceneBuilder stretches that single image across one UV 0-1 quad covering the entire
# 1536 x 1024 sim world, and everything else in the game stands on top of it.
#
# That was defensible when the world had no standing geometry. It is not any more, and
# it breaks the concept three ways at once (design/concept-gap-check-20260813.md):
#
#   1. STYLE. A painted, photoreal-leaning scene under cel-shaded toon geometry.
#   2. DOUBLE ARCHITECTURE. The image already contains columns, braziers and rails;
#      LobbyCourt now places real ones on top. The room is drawn twice, once flat and
#      once solid, and they do not agree.
#   3. PERSPECTIVE. The painting is locked to one isometric viewpoint. The lobby
#      camera orbits +/-6 degrees, so the painted architecture cannot move with it —
#      the ground slides under a room that stays still.
#
# The replacement is the thing the plate should always have been: a top-down, seamless,
# tileable COURT FLOOR. Architecture belongs to the kit meshes; the floor's job is to
# be a floor.
#
# WHAT THE PROMPT FORBIDS, AND WHY EACH ONE. Every negative here corresponds to one of
# the three failures above — they are not style garnish:
#
#   * no walls/columns/stairs/furniture  -> failure 2. Anything with a silhouette
#                                           drawn into the ground competes with a real
#                                           mesh standing on it.
#   * no baked shadows or highlights     -> failure 3. Baked light is a viewpoint. The
#                                           scene has a live key light; a floor that
#                                           carries its own sun disagrees with it from
#                                           every angle but one.
#   * orthographic top-down, no horizon  -> failure 3 again, stated positively.
#   * flat colour regions + ink linework -> failure 1, and the same COMMON contract the
#                                           toon stage set is generated against
#                                           (tools/gen_toon_env_retake.sh).
#
# NOTHING IS OVERWRITTEN. Output lands in a staging directory; adoption is decided by
# tools/qa/measure_toon_textures.py-style measurement and done by hand. This repo has
# destroyed committed textures once with an in-flight regeneration.
#
# Usage:
#   bash tools/gen_court_floor_plate.sh
#   FORCE=1 bash tools/gen_court_floor_plate.sh
set -uo pipefail
cd "$(dirname "$0")/.."
OUT="${PLATE_OUT:-_workspace/current/engineering/court-floor-plate}"
mkdir -p "$OUT"

COMMON="flat cel-shaded game texture, bold clean hand-drawn ink linework defining every shape, many small and medium flat colour regions packed edge to edge and separated by dark ink lines, no single flat colour region larger than one tenth of the image, hard edges, two-tone shading at most, no soft gradients, no photographic noise, NO baked shadows, NO baked highlights, NO light direction, seamless tileable square texture, edges wrap perfectly, strictly orthographic top-down view of a floor, no horizon, no perspective, no walls, no columns, no stairs, no furniture, no objects standing up, MID-TONE VALUE overall, neither bright nor washed out, keep the darkest regions genuinely dark, no text, no logo, no border, no vignette"

# id | prompt
# The identities stay inside the worldview's vocabulary: this is a court of memory,
# and its floor is a judgment floor — flagstone, inlaid oath-lines, ash in the joints.
TARGETS=(
  "court-floor|dark basalt flagstone courtroom floor, large rectangular slabs in a regular grid, thin inlaid brass oath-lines tracing a broad circle across the slabs, fine grey ash settled in the joints, faint dull-ember cracks between a few slabs"
  "court-floor-alt|worn charcoal stone judgment floor, interlocking hexagonal and rectangular flags, pale indigo runic seams inlaid along the joints, drifted ash, a scatter of hairline fractures"
)

gen() { # slug prompt
  local slug="$1" prompt="$2"
  local path="$OUT/$slug.png"
  if [ -s "$path" ] && [ "${FORCE:-0}" != "1" ]; then echo "skip  $path"; return 0; fi
  local delay=15
  for attempt in 1 2 3 4 5; do
    if gti --provider codex-cli --prompt "$prompt, $COMMON" --output "$path" >/dev/null 2>&1 \
       && [ -s "$path" ]; then
      echo "ok    $path (attempt $attempt)"
      sleep 8
      return 0
    fi
    echo "retry $path in ${delay}s (attempt $attempt failed)"
    sleep "$delay"
    delay=$((delay * 2))
  done
  echo "FAIL  $path"
  return 1
}

fail=0
for row in "${TARGETS[@]}"; do
  IFS='|' read -r slug prompt <<<"$row"
  gen "$slug" "$prompt" || fail=1
done
echo "done (fail=$fail)"
echo
echo "NOTHING has been adopted. Look at them, then measure, then copy by hand."
exit "$fail"
