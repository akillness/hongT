#!/usr/bin/env bash
# Toon material sheets for the equip props (weapon / lantern / cloak), via
# god-tibo-imagen (CLAUDE.md §3: concept + texture class -> gti).
#
# WHY TILEABLE MATERIAL SHEETS AND NOT PER-PROP UNIQUE ALBEDO. The prop FBXs
# carry UVs (LayerElementUV present in every equip-*.fbx) but they were authored
# by tools/blender/convert_equip_props.py for FLAT COLOUR, not for a painted
# atlas — the layout is whatever the primitive build produced, so a unique
# unwrap-dependent painting would smear. A tileable material sheet reads
# correctly under ANY layout, which is the same reason the environment kit
# textures are tileable (EnvironmentBuilder tiles at TilesPerWorldUnit).
#
# WHY FIVE SHEETS AND NOT TWELVE. The twelve props are five materials: blade
# steel, dark iron, bow wood+horn, lantern brass, cloak cloth. Band (basic/fine)
# is NOT a texture difference — it is the tint plus the emission term, which is
# what keeps the rank readout legible after the toon conversion.
#
# The COMMON block is the toon-environment contract (tools/gen_toon_env_textures.sh)
# with one substitution: these are PROPS at ~40-60 screen px, not floors, so the
# feature scale is coarser still — anything finer than a few strokes disappears.
#
# Usage:
#   bash tools/gen_prop_textures.sh            # only missing files
#   FORCE=1 bash tools/gen_prop_textures.sh    # regenerate everything
set -uo pipefail
cd "$(dirname "$0")/.."
OUT=Assets/Resources/Textures/Props
mkdir -p "$OUT"

# VALUE-NEUTRAL BY CONSTRUCTION. CinderToonLit multiplies sheet by tint
# (albedo = SAMPLE(_BaseMap) * _BaseColor), so a sheet that carries its own
# darkness multiplies against an already-dark tint: the first set asked for
# "near-black pitted plate" and "coarse charcoal weave" and measured 0.17x and
# 0.16x of the intended colour, with equip-cloak-basic (emission exactly 0)
# rendering as a pure black silhouette. Normalising afterwards fixes the mean
# but not the cost: lifting a 0.16 sheet to target needs gamma 0.15, which
# crushes the shadow end where ALL of this pattern lives, and the weave came
# back as 5 usable levels — the flat tint it was meant to replace.
# So the prompt must not name a value at all. It asks for PATTERN at mid grey;
# the tint owns the colour, and normalize_prop_sheets.py enforces the mean.
COMMON="mid grey value overall, medium neutral brightness, pattern and linework only with NO overall dark or light tone, strong local contrast between light and dark strokes, flat cel-shaded game texture, bold clean hand-drawn ink linework, a few large flat colour regions with hard edges, two-tone shading at most, no soft gradients, no photographic noise, no baked shadows or highlights, coarse feature scale readable when shrunk to sixty pixels, seamless tileable square texture, edges wrap perfectly, no text, no logo, no border, no vignette"

gen() { # slug prompt
  local path="$OUT/$1.png"
  if [ -s "$path" ] && [ "${FORCE:-0}" != "1" ]; then echo "skip $path"; return 0; fi
  local delay=15
  for attempt in 1 2 3 4 5; do
    # codex-cli is the provider that answers for this account (same note as
    # gen_toon_env_textures.sh); it ignores --size and the importer caps at 1024.
    if gti --provider codex-cli --prompt "$2, $COMMON" --output "$path" >/dev/null 2>&1 \
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

fail=0
gen prop-steel  "forged blade steel surface, hammered facets with a contrasting tempered spine streak" || fail=1
gen prop-iron   "heavy warhammer head surface, pitted plate with chipped edges and clear rivet marks" || fail=1
gen prop-wood   "bow limb of laminated wood grain bound with horn and wrapped leather cord" || fail=1
gen prop-brass  "lantern cage surface, panelled metal with distinct seams and rivets" || fail=1
gen prop-cloth  "heavy cloak cloth, coarse visible weave with frayed threads" || fail=1

# The generator CANNOT be allowed to emit an un-normalised sheet: the .mat tints
# divide by a fixed constant, so a fresh dark sheet would silently reproduce the
# near-black props with no failing check anywhere (CLAUDE.md §4b — an invariant
# living in one tool while the thing that breaks it lives in another is a blind
# spot by construction). Normalise here, then assert.
if [ "$fail" = "0" ]; then
  python3 tools/qa/normalize_prop_sheets.py || fail=1
  python3 tools/qa/normalize_prop_sheets.py --check || fail=1
fi
echo "done (fail=$fail)"
ls -la "$OUT"
exit "$fail"
</content>
