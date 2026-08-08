#!/usr/bin/env bash
# cycle-8 lobby rail icons (D-4/D-5): 성소 / 출정 / 지도.
# Same contract as gen_icons.sh — magenta-keyed masters into
# _workspace/current/engineering/icons/raw/, matted by mat_icons.py into
# Assets/Resources/Icons/.
#
# The three glyphs must be distinguishable at 103.3 u, which at the support
# floor (375 CSS, 0.4383 px/u) is 45 px. So each prompt names ONE dominant
# silhouette, not a scene: at 45 px a composition reads as a smudge.
set -euo pipefail
cd "$(dirname "$0")/../.."

RAW=_workspace/current/engineering/icons/raw
mkdir -p "$RAW"

STYLE="flat vector game icon, dark fantasy hack-and-slash style, bold readable silhouette, thin ember-orange rim light, deep navy interior shading, consistent 3px outline, centered, fills 80% of frame. Solid pure magenta #FF00FF background on every pixel outside the icon. No text, no watermark, no border frame."

# id|prompt-core
ICONS=$(cat <<'EOF'
ui-sanctum|a stone archway shrine with a single blazing ember brazier at its centre, one dominant arch silhouette
ui-sortie|a heraldic banner on a spear planted forward, pennant streaming to one side, one dominant flag silhouette
ui-map|an unrolled parchment map with three linked constellation nodes marked on it, one dominant rectangle silhouette
EOF
)

run_one() {
  local id="$1" core="$2"
  local out="$RAW/$id.png"
  if [ -s "$out" ]; then echo "SKIP $id (exists)"; return 0; fi
  for attempt in 1 2 3; do
    if gti --prompt "$core, $STYLE" --output "$out" >/dev/null 2>&1 && [ -s "$out" ]; then
      echo "OK   $id (attempt $attempt)"
      return 0
    fi
    sleep $((attempt * 5))
  done
  echo "FAIL $id"
  return 1
}

while IFS='|' read -r id core; do
  [ -z "$id" ] && continue
  run_one "$id" "$core" </dev/null &
done <<< "$ICONS"
wait

COUNT=$(ls "$RAW"/ui-sanctum.png "$RAW"/ui-sortie.png "$RAW"/ui-map.png 2>/dev/null | wc -l | tr -d ' ')
echo "=== RAIL ICONS RAW: $COUNT/3 ==="
[ "$COUNT" -eq 3 ] || exit 1
