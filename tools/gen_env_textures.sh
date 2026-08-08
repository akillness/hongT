#!/usr/bin/env bash
# Stage dungeon gimmick textures (CLAUDE.md §3: 컨셉/텍스쳐 = god-tibo-imagen).
# One seamless stone (walls/pillars/gates) + one floor (accent panels) per stage,
# consumed by EnvironmentBuilder.ApplyStageTextures.
#   Output: Assets/Resources/Textures/Env/<stageId>-{stone,floor}.png
#   Provenance: docs/provenance/env-stage-textures.md
#
# STRICTLY SERIAL with backoff: the image backend answers a 4-way parallel
# burst with HTTP 429 immediately (observed 2026-10), so concurrency here
# costs textures instead of saving time.
set -uo pipefail
cd "$(dirname "$0")/.."
OUT=Assets/Resources/Textures/Env
mkdir -p "$OUT"

COMMON="seamless tileable square texture, flat even lighting with no baked shadows or highlights, orthographic flat albedo map for a game engine, edges wrap perfectly, no text, no logo, no border, no vignette"

gen() { # id suffix prompt
  local path="$OUT/$1-$2.png"
  if [ -s "$path" ]; then echo "skip $path"; return 0; fi
  local delay=15
  for attempt in 1 2 3 4 5; do
    # provider codex-cli: the private-codex backend answers every call with
    # HTTP 429 for this account (observed 2026-10); codex-cli is the working
    # path. It ignores --size and returns ~1254px, which the importer downscales
    # to the 1024 WebGL texture ceiling (CLAUDE.md §1).
    if gti --provider codex-cli --prompt "$3, $COMMON" --output "$path" >/dev/null 2>&1 \
       && [ -s "$path" ]; then
      echo "ok   $path (attempt $attempt)"
      sleep 8            # courtesy gap between successful calls
      return 0
    fi
    echo "retry $path in ${delay}s (attempt $attempt failed)"
    sleep "$delay"
    delay=$((delay * 2))
  done
  echo "FAIL $path"
  return 1
}

STAGES=(
  "cinder-span|weathered charcoal basalt block masonry veined with dull orange ember cracks|scorched stone bridge decking planks with ash dust and faint ember seams"
  "ember-gallery|fire-blackened brick gallery wall with glowing molten orange fissures|cracked obsidian floor tiles glowing hot orange between the seams"
  "abyss-chancel|violet-grey cathedral stone masonry with pale indigo runic carving|polished dark chancel floor slabs with violet inlaid sigil lines"
  "witness-well|damp pale blue-grey well stone blocks with wet mineral staining|wet slick flagstone floor with shallow cyan water film and mineral rings"
  "echo-throne|regal dark blue granite throne-hall masonry with silver-blue veining|mirror-dark throne floor tiles with concentric pale blue echo rings"
  "ash-verdict|ash-caked pale gold sandstone courthouse blockwork with soot streaks|drifted grey ash over cracked pale gold judgment floor tiles"
  "cinder-sluice|soot-stained iron-braced channel stonework with rust and ember grit|grated sluice floor of wet dark stone with rusted iron channel strips"
  "ember-bastion|heavy fortress ramparts of red-hot forged stone and iron plating|scorched bastion floor of heavy iron plates over glowing ember stone"
  "ash-march|barren wind-scoured grey stone rampart caked in drifting ash|trampled ash-covered marching road of broken grey flagstones"
)

fail=0
for row in "${STAGES[@]}"; do
  IFS='|' read -r id stone floor <<<"$row"
  gen "$id" stone "$stone" || fail=1
  gen "$id" floor "$floor" || fail=1
done
echo "done (fail=$fail)"
ls -la "$OUT"
exit "$fail"
