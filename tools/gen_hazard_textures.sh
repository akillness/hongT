#!/usr/bin/env bash
# Generate the 33 stage/hazard primary textures for the nine-stage campaign
# gimmick visual remaster. This uses god-tibo-imagen (gti) only.
#
# Modes:
#   --dry-run   Validate all prompt/request shapes without backend generation.
#   --smoke     Generate one asset, default cinder-span-ember-vent-underlay.
#   --generate  Generate all missing assets serially, resuming existing outputs.
#   --derive    Rebuild final 512 RGB PNGs from preserved source PNGs, no gti.
#
# Safety:
# - Outputs only to Assets/Resources/Textures/Hazards.
# - Reads Env/Fx references but never writes to Env/Fx.
# - Uses private-codex, repeatable --image references, strict serial retry, and
#   sanitized debug artifacts under _workspace/current/engineering/hazard-texture-gen.
set -uo pipefail

cd "$(dirname "$0")/.."

OUT_DIR="Assets/Resources/Textures/Hazards"
EVIDENCE_ROOT="_workspace/current/engineering/hazard-texture-gen"
DEBUG_ROOT="$EVIDENCE_ROOT/gti-debug"
DRY_RUN_ROOT="$EVIDENCE_ROOT/dry-run"
SOURCE_ROOT="$EVIDENCE_ROOT/source"
DERIVE_TMP="$EVIDENCE_ROOT/derive-tmp"
PROVIDER="${GTI_PROVIDER:-private-codex}"
MODEL="${GTI_MODEL:-gpt-5.4}"
SIZE="${GTI_SIZE:-1024x1024}"
FINAL_SIZE=512
BACKOFF=(15 30 60 120 240)
SUCCESS_GAP=8

MODE=""
FILTER_ASSET=""
FROM_ASSET=""
LIMIT=0
FORCE=0
NO_EXEC=0

usage() {
  cat <<'USAGE'
Usage: tools/gen_hazard_textures.sh --dry-run|--smoke|--generate|--derive [options]

Options:
  --asset <stage-hazard-role>  Run only one asset id.
  --from <stage-hazard-role>   Resume from the first matching asset id.
  --limit <n>                  Stop after n selected assets.
  --force                      Regenerate existing outputs.
  --no-exec                    Print assembled gti commands without running them.
  -h, --help                   Show this help.

Examples:
  tools/gen_hazard_textures.sh --dry-run --no-exec --asset cinder-span-ember-vent-underlay
  tools/gen_hazard_textures.sh --dry-run
  tools/gen_hazard_textures.sh --smoke
  tools/gen_hazard_textures.sh --generate --from echo-throne-tide-current-bed
  tools/gen_hazard_textures.sh --derive
USAGE
}

option_value() {
  local option="$1"
  local value="${2:-}"
  if [ -z "$value" ] || [[ "$value" == --* ]]; then
    echo "error: $option requires a value" >&2
    return 2
  fi
  printf '%s\n' "$value"
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --dry-run|--smoke|--generate|--derive)
      if [ -n "$MODE" ]; then
        echo "error: choose only one mode" >&2
        exit 2
      fi
      MODE="${1#--}"
      ;;
    --asset)
      FILTER_ASSET="$(option_value "$1" "${2:-}")" || exit 2
      shift
      ;;
    --from)
      FROM_ASSET="$(option_value "$1" "${2:-}")" || exit 2
      shift
      ;;
    --limit)
      LIMIT="$(option_value "$1" "${2:-}")" || exit 2
      shift
      ;;
    --force)
      FORCE=1
      ;;
    --no-exec)
      NO_EXEC=1
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "error: unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
  shift
done

if [ -z "$MODE" ]; then
  echo "error: choose --dry-run, --smoke, --generate, or --derive" >&2
  usage >&2
  exit 2
fi

if ! [[ "$LIMIT" =~ ^[0-9]+$ ]]; then
  echo "error: --limit must be a non-negative integer" >&2
  exit 2
fi

COMMON_PROMPT="1024x1024 square game texture element, top-down orthographic physical floor inlay, fully opaque surface with no transparent gaps, dark fantasy Cinder Court material, integrated into the surrounding stone floor, readable at 55 degree camera pitch, flat even albedo lighting, no baked shadows, no UI overlay look, no text, no logo, no border, no vignette, no watermark"
NEGATIVE_PROMPT="transparent holes, alpha checkerboard, floating UI ring, text, logo, glyph labels, border, vignette, perspective scene, camera blur, characters, weapons, enemies, HUD, white background, magenta debug color, baked character shadows, unrelated props"

# stage|act|concept|floorRef|stoneRef
STAGES=(
  "cinder-span|1|charcoal basalt bridge with scorched seams and hot orange ember fissures|Assets/Resources/Textures/Env/cinder-span-floor.png|Assets/Resources/Textures/Env/cinder-span-stone.png"
  "ember-gallery|1|fire-blackened gallery stone, obsidian panels, circular vent rhythm, hot ember seams|Assets/Resources/Textures/Env/ember-gallery-floor.png|Assets/Resources/Textures/Env/ember-gallery-stone.png"
  "abyss-chancel|1|violet and indigo oath cathedral stone, pale rune cuts, cold veil glow|Assets/Resources/Textures/Env/abyss-chancel-floor.png|Assets/Resources/Textures/Env/abyss-chancel-stone.png"
  "witness-well|2|wet jade well stone, mineral rings, restrained teal water staining|Assets/Resources/Textures/Env/witness-well-floor.png|Assets/Resources/Textures/Env/witness-well-stone.png"
  "echo-throne|2|dark blue granite throne floor, silver veins, concentric echo and current motifs|Assets/Resources/Textures/Env/echo-throne-floor.png|Assets/Resources/Textures/Env/echo-throne-stone.png"
  "ash-verdict|2|ash sandstone court, smoke-stained judgment stone, muted gold verdict seams|Assets/Resources/Textures/Env/ash-verdict-floor.png|Assets/Resources/Textures/Env/ash-verdict-stone.png"
  "cinder-sluice|3|wet iron sluice floor, rusted grate, blue current streaks over dark stone|Assets/Resources/Textures/Env/cinder-sluice-floor.png|Assets/Resources/Textures/Env/cinder-sluice-stone.png"
  "ember-bastion|3|iron and ember fortress plates, warm fire stone contrasted with cyan ward residue|Assets/Resources/Textures/Env/ember-bastion-floor.png|Assets/Resources/Textures/Env/ember-bastion-stone.png"
  "ash-march|3|desaturated ash execution road, trampled grey flagstone, pale final judgment gold|Assets/Resources/Textures/Env/ash-march-floor.png|Assets/Resources/Textures/Env/ash-march-stone.png"
)

# stage|hazard-token|hazard-kind|role|runtime-consumer|fxRef|rolePrompt
ASSETS=(
  "cinder-span|ember-vent|EmberVent|underlay|VfxDirector.EmberVent.Surface|Assets/Resources/Fx/scorch-decal.png|round scorched crater plate for an ember vent, blackened basalt lip, opaque hot orange fissures contained under the warning ring"
  "cinder-span|stone-wall|StoneWall|body|VfxDirector.StoneWall.Surface|Assets/Resources/Fx/crack-fan.png|repeatable broken stone wall body strip and contact footprint, charcoal basalt chunks, opaque wall-ground integration"
  "ember-gallery|ember-vent|EmberVent|underlay|VfxDirector.EmberVent.Surface|Assets/Resources/Fx/scorch-decal.png|round gallery vent inlay, obsidian blackened ring, radial ember scoring, opaque physical crater below the telegraph"
  "ember-gallery|obsidian-pillar|ObsidianPillar|body|VfxDirector.ObsidianPillar.Surface|Assets/Resources/Fx/shard-streak.png|obsidian pillar body top and contact base, fire-blackened gallery polish, opaque anchored footprint"
  "ember-gallery|stone-wall|StoneWall|body|VfxDirector.StoneWall.Surface|Assets/Resources/Fx/crack-fan.png|repeatable gallery wall body strip, obsidian brick and ember mortar, opaque stone-wall contact surface"
  "abyss-chancel|ember-vent|EmberVent|underlay|VfxDirector.EmberVent.Surface|Assets/Resources/Fx/scorch-decal.png|cold violet ember vent underlay, cathedral stone crater, pale indigo rune heat seams, opaque surface"
  "abyss-chancel|obsidian-pillar|ObsidianPillar|body|VfxDirector.ObsidianPillar.Surface|Assets/Resources/Fx/shard-streak.png|oath cathedral obsidian pillar body with broad violet facets, pale lilac rune planes at least thirty percent brighter than the dark floor, strong large-shape silhouette separation, quiet micro-detail, opaque anchored contact footprint"
  "abyss-chancel|stone-wall|StoneWall|body|VfxDirector.StoneWall.Surface|Assets/Resources/Fx/crack-fan.png|repeatable indigo cathedral wall body strip with broad block rhythm, pale lilac rune cuts and raised planes clearly brighter than the violet floor, strong gameplay-scale value separation, quiet micro-detail, opaque chancel wall-ground integration"
  "witness-well|ember-vent|EmberVent|underlay|VfxDirector.EmberVent.Surface|Assets/Resources/Fx/scorch-decal.png|wet jade vent underlay, mineral-stained stone crater, restrained teal water ring, opaque physical bed"
  "witness-well|obsidian-pillar|ObsidianPillar|body|VfxDirector.ObsidianPillar.Surface|Assets/Resources/Fx/shard-streak.png|wet mineral pillar body and contact base, jade-black stone, water-polished opaque footprint"
  "witness-well|relic-altar|RelicAltar|underlay|VfxDirector.RelicAltar.Surface|Assets/Resources/Fx/telegraph-ring-sheet.png|relic altar channel underlay, wet jade mineral sigil carved into stone, opaque plinth footprint below live channel"
  "witness-well|stone-wall|StoneWall|body|VfxDirector.StoneWall.Surface|Assets/Resources/Fx/crack-fan.png|repeatable wet well wall body strip, jade mineral rings, opaque stone contact surface"
  "echo-throne|ember-vent|EmberVent|underlay|VfxDirector.EmberVent.Surface|Assets/Resources/Fx/scorch-decal.png|dark blue granite vent underlay with a broad bright silver-cyan scorch rim and a deep navy center, unmistakable circular read at gameplay scale, outer fifteen percent is quiet uniform throne stone with no square frame and no shadow touching the image edge, opaque crater bed"
  "echo-throne|relic-altar|RelicAltar|underlay|VfxDirector.RelicAltar.Surface|Assets/Resources/Fx/telegraph-ring-sheet.png|throne altar underlay with a broad luminous silver-blue concentric echo sigil on dark navy granite, high gameplay-scale contrast, outer fifteen percent is quiet uniform throne stone with no square frame and no shadow touching the image edge, opaque granite channel plate"
  "echo-throne|tide-current|TideCurrent|bed|VfxDirector.TideCurrent.Surface|Assets/Resources/Fx/shockwave-sheet.png|rectangular tide current bed with wide luminous silver-cyan flow lanes over a distinctly darker navy granite channel, strong broad value bands visible at gameplay scale, quiet micro-detail, opaque no-gap band"
  "echo-throne|stone-wall|StoneWall|body|VfxDirector.StoneWall.Surface|Assets/Resources/Fx/crack-fan.png|repeatable throne wall body strip with broad dark granite blocks, bright silver-cyan raised edges and veins clearly separated from the floor value, quiet micro-detail, opaque contact edge"
  "ash-verdict|ember-vent|EmberVent|underlay|VfxDirector.EmberVent.Surface|Assets/Resources/Fx/scorch-decal.png|centered round ash sandstone vent underlay with smoky gold scorch seams, broad clean circular silhouette, outer eighteen percent is uniform matching ash court stone, no square border, no corner frame, no vignette, no shadow or motif touching any image edge, opaque crater plate"
  "ash-verdict|relic-altar|RelicAltar|underlay|VfxDirector.RelicAltar.Surface|Assets/Resources/Fx/telegraph-ring-sheet.png|centered round judgment altar underlay with a muted gold verdict sigil carved into ash sandstone, broad clean circular silhouette, outer eighteen percent is uniform matching ash court stone, no square border, no corner frame, no vignette, no shadow or motif touching any image edge, opaque channel bed"
  "ash-verdict|ember-pylon|EmberPylon|underlay|VfxDirector.EmberPylon.Surface|Assets/Resources/Fx/impact-sheet.png|centered round ember pylon aura underlay with restrained smoky judgment-gold heat scars over ash court stone, broad clean radial silhouette, outer eighteen percent is uniform matching ash court stone, no square border, no corner frame, no vignette, no shadow or scorch touching any image edge, opaque footprint"
  "ash-verdict|stone-wall|StoneWall|body|VfxDirector.StoneWall.Surface|Assets/Resources/Fx/crack-fan.png|repeatable ash sandstone wall body strip, soot streaks and muted gold cracks, opaque contact strip"
  "cinder-sluice|ember-vent|EmberVent|underlay|VfxDirector.EmberVent.Surface|Assets/Resources/Fx/scorch-decal.png|wet iron sluice vent underlay, rusted grate scorch, opaque dark stone crater"
  "cinder-sluice|obsidian-pillar|ObsidianPillar|body|VfxDirector.ObsidianPillar.Surface|Assets/Resources/Fx/shard-streak.png|wet iron-braced pillar body and contact base, rusted dark stone, opaque footprint"
  "cinder-sluice|tide-current|TideCurrent|bed|VfxDirector.TideCurrent.Surface|Assets/Resources/Fx/shockwave-sheet.png|sluice current bed, wet grate channel with blue flow lanes and rust, opaque rectangular band"
  "cinder-sluice|stone-wall|StoneWall|body|VfxDirector.StoneWall.Surface|Assets/Resources/Fx/crack-fan.png|repeatable wet sluice wall strip, iron brace rhythm, rusted opaque contact edge"
  "ember-bastion|ember-vent|EmberVent|underlay|VfxDirector.EmberVent.Surface|Assets/Resources/Fx/scorch-decal.png|fortress ember vent underlay, iron plate crater, warm fire glow held under warning rim, opaque"
  "ember-bastion|obsidian-pillar|ObsidianPillar|body|VfxDirector.ObsidianPillar.Surface|Assets/Resources/Fx/shard-streak.png|iron and ember fortress pillar body, warm fire cracks with faint cyan ward residue, opaque contact base"
  "ember-bastion|ember-pylon|EmberPylon|underlay|VfxDirector.EmberPylon.Surface|Assets/Resources/Fx/impact-sheet.png|ember pylon fortress aura underlay, scorched iron plate and cyan ward residue, opaque footprint"
  "ember-bastion|stone-wall|StoneWall|body|VfxDirector.StoneWall.Surface|Assets/Resources/Fx/crack-fan.png|repeatable fortress wall body strip, iron plates over ember stone, opaque contact surface"
  "ash-march|ember-vent|EmberVent|underlay|VfxDirector.EmberVent.Surface|Assets/Resources/Fx/scorch-decal.png|desaturated ash road vent underlay, pale final gold ember seams, opaque crater bed"
  "ash-march|relic-altar|RelicAltar|underlay|VfxDirector.RelicAltar.Surface|Assets/Resources/Fx/telegraph-ring-sheet.png|execution road altar underlay, pale final judgment gold sigil in grey ash stone, opaque channel bed"
  "ash-march|ember-pylon|EmberPylon|underlay|VfxDirector.EmberPylon.Surface|Assets/Resources/Fx/impact-sheet.png|ash march pylon underlay, pale gold scorch aura over trampled grey stone, opaque footprint"
  "ash-march|ash-wall|AshWall|band|VfxDirector.AshWall.Surface|Assets/Resources/Fx/telegraph-ring-sheet.png|opaque swallowed ash hazard field, matte warm-charcoal ash with directional wind smears from either home edge toward the advancing front, sparse muted judgment-gold dust and fracture flecks concentrated near both vertical edges, broad quiet ashen center, no flagstones, no paving slabs, no regular tiles, no road pattern, no alternate floor-material read, crop-safe cull-safe no-gap strip that reads as engulfing lethal ash rather than walkable floor"
  "ash-march|stone-wall|StoneWall|body|VfxDirector.StoneWall.Surface|Assets/Resources/Fx/crack-fan.png|repeatable ash execution road wall strip, broken grey flagstone and pale gold dust, opaque contact body"
)

stage_field() {
  local stage_id="$1" field="$2" row
  for row in "${STAGES[@]}"; do
    IFS='|' read -r sid act concept floor_ref stone_ref <<<"$row"
    if [ "$sid" = "$stage_id" ]; then
      case "$field" in
        act) printf '%s\n' "$act" ;;
        concept) printf '%s\n' "$concept" ;;
        floor) printf '%s\n' "$floor_ref" ;;
        stone) printf '%s\n' "$stone_ref" ;;
        *) return 2 ;;
      esac
      return 0
    fi
  done
  return 1
}

prompt_for() {
  local stage_id="$1" role="$2" role_prompt="$3" concept tile_clause
  concept="$(stage_field "$stage_id" concept)"
  tile_clause=""
  case "$role" in
    body|bed)
      tile_clause=" Seamless tileable texture body, edges wrap perfectly left-to-right and top-to-bottom, no visible repetition seam, suitable for Unity Repeat wrap mode."
      ;;
  esac
  printf '%s. Stage tone: %s. Asset: %s.%s Negative prompt: %s.' \
    "$COMMON_PROMPT" "$concept" "$role_prompt" "$tile_clause" "$NEGATIVE_PROMPT"
}

asset_id_for() {
  printf '%s-%s-%s\n' "$1" "$2" "$3"
}

validate_refs() {
  local asset_id="$1" floor_ref="$2" stone_ref="$3" fx_ref="$4"
  local missing=0 ref
  for ref in "$floor_ref" "$stone_ref" "$fx_ref"; do
    if [ ! -s "$ref" ]; then
      echo "error: $asset_id missing reference $ref" >&2
      missing=1
    fi
  done
  return "$missing"
}

validate_output() {
  local path="$1"
  if [ ! -s "$path" ]; then
    echo "error: empty output: $path" >&2
    return 1
  fi
  if command -v file >/dev/null 2>&1; then
    local file_info
    file_info="$(file "$path")"
    if ! printf '%s\n' "$file_info" | grep -q 'PNG image data'; then
      echo "error: output is not PNG: $path" >&2
      return 1
    fi
    if printf '%s\n' "$file_info" | grep -Eq 'RGBA|alpha'; then
      echo "error: final output must be RGB without alpha: $path ($file_info)" >&2
      return 1
    fi
  fi
  if command -v sips >/dev/null 2>&1; then
    local width height
    width="$(sips -g pixelWidth "$path" 2>/dev/null | awk '/pixelWidth/ {print $2}')"
    height="$(sips -g pixelHeight "$path" 2>/dev/null | awk '/pixelHeight/ {print $2}')"
    if [ -n "$width" ] && [ -n "$height" ] && { [ "$width" -gt 1024 ] || [ "$height" -gt 1024 ]; }; then
      echo "error: output exceeds 1024px: $path (${width}x${height})" >&2
      return 1
    fi
  fi
  return 0
}

validate_source() {
  local path="$1"
  if [ ! -s "$path" ]; then
    echo "error: empty source output: $path" >&2
    return 1
  fi
  if command -v file >/dev/null 2>&1 && ! file "$path" | grep -q 'PNG image data'; then
    echo "error: source output is not PNG: $path" >&2
    return 1
  fi
  return 0
}

resample_final() {
  local source="$1" output="$2"
  mkdir -p "$(dirname "$output")"
  if command -v magick >/dev/null 2>&1; then
    magick "$source" -alpha off -colorspace sRGB -resize "${FINAL_SIZE}x${FINAL_SIZE}!" "PNG24:$output"
  elif command -v sips >/dev/null 2>&1; then
    sips -s format png -z "$FINAL_SIZE" "$FINAL_SIZE" "$source" --out "$output" >/dev/null
  else
    echo "error: need magick or sips for deterministic ${FINAL_SIZE}x${FINAL_SIZE} resample" >&2
    return 1
  fi
  validate_output "$output"
}

derive_final() {
  local asset_id="$1" source="$2" output="$3" role="$4"
  if ! validate_source "$source"; then
    return 1
  fi
  mkdir -p "$(dirname "$output")" "$DERIVE_TMP"

  case "$role" in
    body|bed)
      if ! command -v magick >/dev/null 2>&1; then
        echo "error: magick is required for mirror-tile derivation of repeat role $asset_id" >&2
        return 1
      fi
      local row_tmp="$DERIVE_TMP/$asset_id-row.png"
      magick "$source" \( +clone -flop \) +append "$row_tmp" \
        && magick "$row_tmp" \( +clone -flip \) -append -alpha off -colorspace sRGB -resize "${FINAL_SIZE}x${FINAL_SIZE}!" "PNG24:$output"
      rm -f "$row_tmp"
      ;;
    *)
      resample_final "$source" "$output"
      return $?
      ;;
  esac
  validate_output "$output"
}

print_cmd() {
  printf 'gti'
  local arg
  for arg in "$@"; do
    printf ' %q' "$arg"
  done
  printf '\n'
}

run_gti() {
  local asset_id="$1" source_output="$2" final_output="$3" role="$4" prompt="$5" floor_ref="$6" stone_ref="$7" fx_ref="$8"
  local debug_dir="$DEBUG_ROOT/$MODE/$asset_id"
  local dry_artifact="$DRY_RUN_ROOT/$asset_id.txt"
  mkdir -p "$debug_dir"
  if [ "$MODE" = "dry-run" ]; then
    mkdir -p "$DRY_RUN_ROOT"
  fi

  local cmd_args=(
    --provider "$PROVIDER"
    --model "$MODEL"
    --size "$SIZE"
    --prompt "$prompt"
    --image "$floor_ref"
    --image "$stone_ref"
    --image "$fx_ref"
    --output "$source_output"
    --debug
    --debug-dir "$debug_dir"
  )
  if [ "$MODE" = "dry-run" ]; then
    cmd_args+=(--dry-run)
  fi

  if [ "$NO_EXEC" -eq 1 ]; then
    print_cmd "${cmd_args[@]}"
    return 0
  fi

  if [ "$MODE" = "dry-run" ]; then
    print_cmd "${cmd_args[@]}" >"$dry_artifact"
    if gti "${cmd_args[@]}" >>"$dry_artifact" 2>&1; then
      echo "dry-run ok $asset_id -> $dry_artifact"
      return 0
    fi
    echo "dry-run fail $asset_id (see $dry_artifact)" >&2
    return 1
  fi

  local idx delay
  for idx in "${!BACKOFF[@]}"; do
    if gti "${cmd_args[@]}" >/dev/null 2>&1 \
      && validate_source "$source_output" \
      && derive_final "$asset_id" "$source_output" "$final_output" "$role"; then
      echo "ok $asset_id -> $final_output (source $source_output, attempt $((idx + 1)))"
      sleep "$SUCCESS_GAP"
      return 0
    fi
    delay="${BACKOFF[$idx]}"
    echo "retry $asset_id in ${delay}s (attempt $((idx + 1)) failed)" >&2
    sleep "$delay"
  done
  echo "FAIL $asset_id -> $final_output" >&2
  return 1
}

if [ "$NO_EXEC" -eq 0 ] && [ "$MODE" != "derive" ]; then
  if ! command -v gti >/dev/null 2>&1; then
    echo "error: gti command not found" >&2
    exit 1
  fi
  if [ ! -s "${CODEX_HOME:-$HOME/.codex}/auth.json" ]; then
    echo "error: Codex auth file not found at ${CODEX_HOME:-$HOME/.codex}/auth.json" >&2
    exit 1
  fi
fi

mkdir -p "$OUT_DIR" "$SOURCE_ROOT"

selected=0
seen_from=0
fail=0
for row in "${ASSETS[@]}"; do
  IFS='|' read -r stage_id hazard_token hazard_kind role consumer fx_ref role_prompt <<<"$row"
  asset_id="$(asset_id_for "$stage_id" "$hazard_token" "$role")"

  if [ -n "$FILTER_ASSET" ] && [ "$asset_id" != "$FILTER_ASSET" ]; then
    continue
  fi
  if [ -n "$FROM_ASSET" ] && [ "$seen_from" -eq 0 ]; then
    if [ "$asset_id" = "$FROM_ASSET" ]; then
      seen_from=1
    else
      continue
    fi
  fi
  if [ "$MODE" = "smoke" ] && [ -z "$FILTER_ASSET" ] && [ "$asset_id" != "cinder-span-ember-vent-underlay" ]; then
    continue
  fi

  selected=$((selected + 1))
  if [ "$LIMIT" -gt 0 ] && [ "$selected" -gt "$LIMIT" ]; then
    break
  fi

  floor_ref="$(stage_field "$stage_id" floor)"
  stone_ref="$(stage_field "$stage_id" stone)"
  source_output="$SOURCE_ROOT/$asset_id.png"
  output="$OUT_DIR/$asset_id.png"
  prompt="$(prompt_for "$stage_id" "$role" "$role_prompt")"

  if ! validate_refs "$asset_id" "$floor_ref" "$stone_ref" "$fx_ref"; then
    fail=1
    continue
  fi

  if [ "$MODE" = "generate" ] && [ "$FORCE" -eq 0 ] && [ -s "$output" ]; then
    echo "skip existing $asset_id -> $output"
    continue
  fi

  if [ "$MODE" = "derive" ]; then
    if [ "$NO_EXEC" -eq 1 ]; then
      echo "derive $role $source_output -> $output"
      continue
    fi
    if ! derive_final "$asset_id" "$source_output" "$output" "$role"; then
      fail=1
    else
      echo "derived $asset_id -> $output"
    fi
    continue
  fi

  if ! run_gti "$asset_id" "$source_output" "$output" "$role" "$prompt" "$floor_ref" "$stone_ref" "$fx_ref"; then
    fail=1
  fi
done

if [ -n "$FILTER_ASSET" ] && [ "$selected" -eq 0 ]; then
  echo "error: asset not found: $FILTER_ASSET" >&2
  exit 2
fi
if [ -n "$FROM_ASSET" ] && [ "$seen_from" -eq 0 ]; then
  echo "error: --from asset not found: $FROM_ASSET" >&2
  exit 2
fi

echo "done mode=$MODE selected=$selected fail=$fail"
exit "$fail"
