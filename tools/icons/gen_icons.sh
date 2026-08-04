#!/usr/bin/env bash
# Production icon set via god-tibo-imagen (gti).
# Raw magenta-keyed masters -> _workspace/current/engineering/icons/raw/
# Matted 256px sprites      -> Assets/Resources/Icons/   (done by mat_icons.py)
#
# User directive 2026-08-04: "icons to make production at release version target"
# Style contract: dark fantasy hack & slash, flat vector-like game icon,
# consistent line weight, magenta #FF00FF chroma background (pipeline standard,
# docs/character-asset-pipeline.md uses the same keying rule).
set -euo pipefail
cd "$(dirname "$0")/../.."

RAW=_workspace/current/engineering/icons/raw
mkdir -p "$RAW"

STYLE="flat vector game icon, dark fantasy hack-and-slash style, bold readable silhouette, thin ember-orange rim light, deep navy interior shading, consistent 3px outline, centered, fills 80% of frame. Solid pure magenta #FF00FF background on every pixel outside the icon. No text, no watermark, no border frame."

# id|prompt-core
ICONS=$(cat <<'EOF'
skill-bolt|jagged cyan lightning bolt crackling with energy
skill-pulse|three concentric cyan energy rings radiating outward from a bright core
skill-nova|fiery orange radial burst explosion with ember sparks
skill-aegis|ornate kite shield with a glowing ember core and gold trim
skill-dash|diagonal speed streak with three motion lines and a small comet head
skill-strike|curved sword slash arc, white-hot edge with ember trail
skill-ward|translucent protective dome barrier with cyan hex facets
equip-weapon|curved dusk-steel blade with ember-orange edge glow, hilt visible
equip-lantern|ornate brass lantern with bright ember flame inside glass
equip-cloak|flowing hooded cloak with silver clasp, night-blue fabric
stat-attack|two crossed swords, ember-orange blades
stat-vitality|faceted heart-shaped ruby with inner glow
stat-swiftness|winged boot with motion lines
app-lantern|abyssal lantern emblem, brass cage around a blazing ember flame, radial glow
pickup-ember|glowing ember shard crystal, ember-orange, faceted
pickup-flask|small oil flask with warm amber liquid and cork stopper
pickup-relic|pale cyan relic mote, faceted floating gem with inner glow
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

FAILED=0
PIDS=()
N=0
while IFS='|' read -r id core; do
  [ -z "$id" ] && continue
  run_one "$id" "$core" </dev/null &
  PIDS+=($!)
  N=$((N+1))
  # throttle: max 3 concurrent
  if [ $((N % 3)) -eq 0 ]; then wait; fi
done <<< "$ICONS"
wait

ls -la "$RAW" | tail -n +2
COUNT=$(ls "$RAW"/*.png 2>/dev/null | wc -l | tr -d ' ')
echo "=== ICONS RAW DONE: $COUNT/17 ==="
[ "$COUNT" -ge 17 ] || exit 1
