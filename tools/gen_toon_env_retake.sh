#!/usr/bin/env bash
# Retake the toon stage maps that came out off-brief, into a STAGING directory.
#
# WHY A RETAKE AND NOT A REGENERATION OF ALL 18. Measured across the whole set
# (tools/qa/measure_toon_textures.py), the toon maps lost surface detail against the
# PBR set they replaced -- median 8x8 local luminance std 9.28 -> 1.82. Some of that
# is the brief working as intended: a cel shader quantises light, so albedo grain
# fights the bands and the prompt asks for flat colour on purpose.
#
# But two things came with it that the brief never asked for:
#
#   1. BRIGHTNESS DRIFT, median +19%, with witness-well/floor at +126% and
#      ash-march/stone at +73%. Nothing in the toon prompt mentions value. A stage
#      that reads 126% brighter than its PBR twin is not "cel-shaded", it is washed
#      out -- and this project reads by VALUE contrast (CLAUDE.md §E0.5 hazard
#      readability is measured against the environment).
#
#   2. NEAR-UNIFORM SHEETS. Six of eighteen maps landed under localStd 1.0, which is
#      not "flat colour regions with hard edges", it is one colour. witness-well got
#      both maps that way AND the worst brightness drift, so that stage renders as a
#      pale void.
#
# ember-bastion is why this is a defect and not the medium: its toon maps came out at
# localStd 10.50 / 10.34, ABOVE their PBR originals, on the same generator with the
# same COMMON block. The brief is achievable. These eight maps missed it.
#
# WHAT CHANGED IN THE PROMPT, AND WHY. Two edits to COMMON, both aimed at the two
# measured defects rather than at "make it look better":
#
#   * "large flat areas of solid colour" -> many small-to-medium regions with an
#     explicit ceiling on any single region. The original wording is what a generator
#     satisfies most cheaply by returning one colour; a ceiling makes that answer
#     illegal while keeping the cel look.
#   * an explicit value target. Unstated value is what drifted, and the fix for an
#     unstated constraint is to state it, not to post-process the output (§4t: an
#     anchor that references an unmeasured quantity is not an anchor).
#
# NOTHING IS OVERWRITTEN HERE. Output lands in a staging directory and
# tools/qa/measure_toon_textures.py decides what, if anything, is worth adopting.
# This repo has destroyed committed textures once with an in-flight regeneration; the
# rule that came out of it is that a destructive transform is proven on a copy first.
#
# Usage:
#   bash tools/gen_toon_env_retake.sh                    # the eight measured outliers
#   RETAKE="witness-well/stone" bash tools/gen_toon_env_retake.sh
set -uo pipefail
cd "$(dirname "$0")/.."
OUT="${RETAKE_OUT:-_workspace/current/engineering/toon-retake}"
mkdir -p "$OUT"

COMMON="flat cel-shaded game texture, bold clean hand-drawn ink linework defining every shape, many small and medium flat colour regions packed edge to edge and separated by dark ink lines, no single flat colour region larger than one tenth of the image, hard edges, two-tone shading at most, no soft gradients, no photographic noise, no baked shadows or highlights, MID-TONE VALUE overall, neither bright nor washed out, keep the darkest regions genuinely dark, seamless tileable square texture, edges wrap perfectly, orthographic top-down view, no text, no logo, no border, no vignette"

# id/kind | prompt   — identities VERBATIM from gen_toon_env_textures.sh, which took
# them verbatim from gen_env_textures.sh. Rewriting them here would let the retaken
# maps describe a different place from the ones they sit beside.
declare -a TARGETS=(
  "cinder-span/floor|scorched stone bridge decking planks with ash dust and faint ember seams"
  "ember-gallery/floor|cracked obsidian floor tiles glowing hot orange between the seams"
  "abyss-chancel/stone|violet-grey cathedral stone masonry with pale indigo runic carving"
  "witness-well/stone|damp pale blue-grey well stone blocks with wet mineral staining"
  "witness-well/floor|wet slick flagstone floor with shallow cyan water film and mineral rings"
  "echo-throne/stone|regal dark blue granite throne-hall masonry with silver-blue veining"
  "echo-throne/floor|mirror-dark throne floor tiles with concentric pale blue echo rings"
  "ash-march/stone|barren wind-scoured grey stone rampart caked in drifting ash"
  "ash-verdict/stone|ash-caked pale gold sandstone courthouse blockwork with soot streaks"
  "ash-verdict/floor|drifted grey ash over cracked pale gold judgment floor tiles"
  # Detail 10.34, the second best in the whole toon set, but +38% value drift.
  # Retaken anyway because adoption is decided by measurement, not by this list:
  # a retake that loses that detail simply does not get adopted, so including it
  # costs one generation and risks nothing.
  "ember-bastion/floor|scorched bastion floor of heavy iron plates over glowing ember stone"
  # SECOND WAVE. These three passed the first gate (localStd 1.68-1.92, above the
  # 1.0 floor) and the SCREEN rejected them anyway: cinder-span/stone is what the
  # arena's boundary ring is made of, and in the shipped frame that ring still read
  # as blank pale slabs. The floor was calibrated to separate "one flat colour" from
  # "more than one", which is not the same question as "does this read as stone" --
  # the coordinate system where a right and a wrong answer coincide (CLAUDE.md
  # §4m). The floor moved to 3.0 and these three came with it.
  "cinder-span/stone|weathered charcoal basalt block masonry veined with dull orange ember cracks"
  "ember-gallery/stone|fire-blackened brick gallery wall with glowing molten orange fissures"
  "cinder-sluice/stone|soot-stained iron-braced channel stonework with rust and ember grit"
)

gen() { # slug prompt
  local slug="$1" prompt="$2"
  local path="$OUT/${slug/\//-}.png"
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
  if [ -n "${RETAKE:-}" ] && [ "$slug" != "$RETAKE" ]; then continue; fi
  gen "$slug" "$prompt" || fail=1
done
echo "done (fail=$fail)"
echo
echo "NOTHING has been adopted. Measure first:"
echo "  python3 tools/qa/measure_toon_textures.py --staging $OUT"
exit "$fail"
