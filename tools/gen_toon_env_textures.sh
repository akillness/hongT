#!/usr/bin/env bash
# Toon-style stage surfaces (floor + stone), 9 stages x 2 = 18, via god-tibo-imagen.
#
# WHY A SEPARATE SCRIPT AND NOT A FLAG ON gen_env_textures.sh. The two sets are not
# two settings of one thing — they are different art directions that have to coexist
# while the toon pass is staged. Writing into the same paths would destroy the PBR set
# the shipped build still uses, and this repo has already lost 36 committed textures
# once to an in-flight regeneration. The toon set lands beside it under Textures/Toon
# and only becomes live when a material points at it.
#
# WHAT CHANGES IN THE PROMPT, AND WHY EACH PART IS THERE. The COMMON block below is
# the PBR script's contract with three inversions, because a cel shader wants the
# opposite of what a PBR albedo wants:
#
#   * flat colour regions instead of continuous grain — the shader quantises light,
#     so albedo noise fights the bands and reads as dirt rather than as material;
#   * drawn linework instead of photographic edges — the outline pass only draws
#     silhouettes, so any interior definition has to live in the texture;
#   * fewer, larger shapes — at this camera a 512 map covers a prop that is ~120 px
#     on screen, and fine detail simply disappears.
#
# The stage identity lines are copied VERBATIM from gen_env_textures.sh so the two
# sets describe the same nine places. Rewriting them here would let the toon dungeon
# drift into a different game.
#
# Usage:
#   bash tools/gen_toon_env_textures.sh            # only missing files
#   FORCE=1 bash tools/gen_toon_env_textures.sh    # regenerate everything
set -uo pipefail
cd "$(dirname "$0")/.."
OUT=Assets/Resources/Textures/Toon
mkdir -p "$OUT"

COMMON="flat cel-shaded game texture, bold clean hand-drawn ink linework defining every shape, large flat areas of solid colour with hard edges, two-tone shading at most, no soft gradients, no photographic noise, no baked shadows or highlights, seamless tileable square texture, edges wrap perfectly, orthographic top-down view, no text, no logo, no border, no vignette"

gen() { # id suffix prompt
  local path="$OUT/$1-$2.png"
  if [ -s "$path" ] && [ "${FORCE:-0}" != "1" ]; then echo "skip $path"; return 0; fi
  local delay=15
  for attempt in 1 2 3 4 5; do
    # Same provider note as the PBR script: codex-cli is the path that answers for
    # this account, and it ignores --size (the importer caps at 1024 anyway).
    if gti --provider codex-cli --prompt "$3, $COMMON" --output "$path" >/dev/null 2>&1 \
       && [ -s "$path" ]; then
      echo "ok   $path (attempt $attempt)"
      sleep 8
      return 0
    fi
    echo "retry $path in ${delay}s (attempt $attempt failed)"
    sleep "$delay"
    delay=$((delay * 2))
  done
  echo "FAIL $path"
  return 1
}

# id | stone prompt | floor prompt   — identities verbatim from gen_env_textures.sh
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
