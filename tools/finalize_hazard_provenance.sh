#!/usr/bin/env bash
# Validate and finalize docs/provenance/stage-hazard-textures.json after the
# god-tibo-imagen hazard texture batch completes.
#
# --check validates all 33 manifest records, generated files, objective image
# metrics, and review evidence without writing.
# --apply performs the same checks, then mechanically updates hashes, metrics,
# decisions, validation artifacts, and top-level status.
set -euo pipefail

cd "$(dirname "$0")/.."

MANIFEST="docs/provenance/stage-hazard-textures.json"
EXPECTED_COUNT=33
FINAL_DIMENSIONS="512x512"
FINAL_SIZE=512
LUMINANCE_STDDEV_MIN=5.0
REPEAT_EDGE_MAE_MAX=12000.0
REVIEW_ROOT="_workspace/current/engineering/hazard-texture-gen/review"
QA_ROOT="_workspace/current/qa"
TMP_DIR="_workspace/current/engineering/hazard-texture-gen/provenance"
SMOKE_DEBUG="_workspace/current/engineering/hazard-texture-gen/smoke/cinder-span-ember-vent-debug"

MODE=""

usage() {
  cat <<'USAGE'
Usage: tools/finalize_hazard_provenance.sh --check|--apply

Requires jq, magick, and shasum. Fails safely when any of the 33 generated PNGs
or review/QA evidence is missing.
USAGE
}

while [ "$#" -gt 0 ]; do
  case "$1" in
    --check|--apply)
      if [ -n "$MODE" ]; then
        echo "error: choose only one mode" >&2
        exit 2
      fi
      MODE="${1#--}"
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
  echo "error: choose --check or --apply" >&2
  usage >&2
  exit 2
fi

need_cmd() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "error: missing required command: $1" >&2
    exit 1
  fi
}

need_cmd jq
need_cmd magick
need_cmd shasum

if [ ! -s "$MANIFEST" ]; then
  echo "error: missing manifest: $MANIFEST" >&2
  exit 1
fi

count="$(jq '.assets | length' "$MANIFEST")"
if [ "$count" -ne "$EXPECTED_COUNT" ]; then
  echo "error: manifest asset count $count != $EXPECTED_COUNT" >&2
  exit 1
fi

required_count="$(jq '.required_asset_count' "$MANIFEST")"
if [ "$required_count" -ne "$EXPECTED_COUNT" ]; then
  echo "error: required_asset_count $required_count != $EXPECTED_COUNT" >&2
  exit 1
fi

find_evidence() {
  local stage="$1" asset_id="$2" request_json="$3" response_json="$4"
  local found=()
  local reviewed=()
  local candidate

  if [ -d "$REVIEW_ROOT" ]; then
    while IFS= read -r candidate; do
      reviewed+=("$candidate")
    done < <(find "$REVIEW_ROOT" -type f \( -name "*${stage}*" -o -name "*${asset_id}*" -o -name "*contact*" \) | sort)
  fi

  if [ -d "$QA_ROOT" ]; then
    while IFS= read -r candidate; do
      reviewed+=("$candidate")
    done < <(find "$QA_ROOT" -type f \( -name "*${stage}*" -o -name "*hazard*" -o -name "*matrix*" \) | sort)
  fi

  if [ "${#reviewed[@]}" -eq 0 ]; then
    return 1
  fi

  found+=("$request_json" "$response_json")
  found+=("${reviewed[@]}")
  printf '%s\n' "${found[@]}" | awk '!seen[$0]++'
}

attempt_count() {
  local asset_id="$1"
  local debug_dir="_workspace/current/engineering/hazard-texture-gen/gti-debug/generate/$asset_id"
  local dry_file="_workspace/current/engineering/hazard-texture-gen/dry-run/$asset_id.txt"
  local attempts=0

  if [ -d "$debug_dir" ]; then
    attempts="$(find "$debug_dir" -type f -name 'request*.json' | wc -l | tr -d ' ')"
  fi
  if [ "$attempts" -lt 1 ] && [ -s "$dry_file" ]; then
    attempts=1
  fi
  if [ "$attempts" -lt 1 ]; then
    attempts=1
  fi
  printf '%s\n' "$attempts"
}

debug_dir_for() {
  local asset_id="$1"
  local debug_dir="_workspace/current/engineering/hazard-texture-gen/gti-debug/generate/$asset_id"

  if [ -s "$debug_dir/request.json" ] && [ -s "$debug_dir/response.json" ]; then
    printf '%s\n' "$debug_dir"
    return 0
  fi

  if [ "$asset_id" = "cinder-span-ember-vent-underlay" ] \
    && [ -s "$SMOKE_DEBUG/request.json" ] \
    && [ -s "$SMOKE_DEBUG/response.json" ]; then
    printf '%s\n' "$SMOKE_DEBUG"
    return 0
  fi

  return 1
}

request_prompt() {
  jq -er '
    .body.input[0].content
    | map(select(.type == "input_text"))[0].text
  ' "$1"
}

request_model() {
  jq -er '.body.model' "$1"
}

request_input_image_count() {
  jq -er '
    .body.input[0].content
    | map(select(.type == "input_image"))
    | length
  ' "$1"
}

response_status() {
  jq -er '.status' "$1"
}

postprocess_json() {
  local role="$1"
  if [ "$role" = "body" ] || [ "$role" = "bed" ]; then
    jq -nc --arg dims "$FINAL_DIMENSIONS" '[
      "Preserve private-codex source PNG under _workspace/current/engineering/hazard-texture-gen/source/<asset>.png",
      "Build a tile-safe 2x2 mirrored field by left-right mirror append followed by top-bottom mirror append; source/debug artifacts are preserved",
      "Deterministically resample the mirrored field to \($dims) RGB PNG for Assets/Resources/Textures/Hazards using magick PNG24"
    ]'
  elif [ "$role" = "band" ]; then
    jq -nc --arg dims "$FINAL_DIMENSIONS" '[
      "Preserve private-codex source PNG under _workspace/current/engineering/hazard-texture-gen/source/<asset>.png",
      "Directly resample the accepted crop-safe band source to \($dims) RGB PNG; runtime reveals/crops 0..1 source and does not Repeat AshWall band"
    ]'
  else
    jq -nc --arg dims "$FINAL_DIMENSIONS" '[
      "Preserve private-codex source PNG under _workspace/current/engineering/hazard-texture-gen/source/<asset>.png",
      "Directly resample accepted source to \($dims) RGB PNG for Assets/Resources/Textures/Hazards using magick PNG24 or sips fallback"
    ]'
  fi
}

image_stddev() {
  magick "$1" -colorspace Gray -format '%[fx:standard_deviation*255]' info:
}

edge_mae() {
  local path="$1"
  local side last left_img right_img top_img bottom_img lr_img tb_img lr_metric tb_metric
  side="$(magick identify -format '%w' "$path")"
  last=$((side - 1))
  left_img="$(mktemp "$TMP_DIR/left.XXXXXX.png")"
  right_img="$(mktemp "$TMP_DIR/right.XXXXXX.png")"
  top_img="$(mktemp "$TMP_DIR/top.XXXXXX.png")"
  bottom_img="$(mktemp "$TMP_DIR/bottom.XXXXXX.png")"
  lr_img="$(mktemp "$TMP_DIR/lr.XXXXXX.png")"
  tb_img="$(mktemp "$TMP_DIR/tb.XXXXXX.png")"

  magick "$path" -crop "1x${side}+0+0" +repage "$left_img"
  magick "$path" -crop "1x${side}+${last}+0" +repage "$right_img"
  magick "$path" -crop "${side}x1+0+0" +repage "$top_img"
  magick "$path" -crop "${side}x1+0+${last}" +repage "$bottom_img"
  lr_metric="$(magick compare -metric MAE "$left_img" "$right_img" "$lr_img" 2>&1 || true)"
  tb_metric="$(magick compare -metric MAE "$top_img" "$bottom_img" "$tb_img" 2>&1 || true)"
  rm -f "$left_img" "$right_img" "$top_img" "$bottom_img" "$lr_img" "$tb_img"

  awk -v lr="$lr_metric" -v tb="$tb_metric" '
    function first_number(s) {
      if (match(s, /[0-9]+(\.[0-9]+)?/)) return substr(s, RSTART, RLENGTH);
      return 0;
    }
    BEGIN {
      a = first_number(lr);
      b = first_number(tb);
      printf "%.8f", (a + b) / 2.0;
    }'
}

json_string_array() {
  jq -R . | jq -s .
}

mkdir -p "$TMP_DIR"
updates_json="$TMP_DIR/updates.jsonl"
: >"$updates_json"

fail=0
for index in $(seq 0 $((EXPECTED_COUNT - 1))); do
  stage="$(jq -r ".assets[$index].stage_id" "$MANIFEST")"
  hazard="$(jq -r ".assets[$index].hazard_kind" "$MANIFEST")"
  role="$(jq -r ".assets[$index].role" "$MANIFEST")"
  output="$(jq -r ".assets[$index].output_path" "$MANIFEST")"
  source="$(jq -r ".assets[$index].source_output" "$MANIFEST")"
  provider="$(jq -r ".assets[$index].provider" "$MANIFEST")"
  asset_id="$(basename "$output" .png)"

  if [ ! -s "$output" ]; then
    echo "error: missing output for $stage/$hazard: $output" >&2
    fail=1
    continue
  fi
  if [ ! -s "$source" ]; then
    echo "error: missing source for $stage/$hazard: $source" >&2
    fail=1
    continue
  fi

  dims="$(magick identify -format '%wx%h' "$output")"
  mode="$(magick identify -format '%[channels]' "$output")"
  if [ "$dims" != "$FINAL_DIMENSIONS" ]; then
    echo "error: $asset_id dimensions $dims != $FINAL_DIMENSIONS" >&2
    fail=1
  fi
  case "$mode" in
    *a*|*A*)
      echo "error: $asset_id mode/channels '$mode' includes alpha" >&2
      fail=1
      canonical_mode="$mode"
      ;;
    srgb*|rgb*|RGB*|sRGB*) canonical_mode="RGB" ;;
    *)
      echo "error: $asset_id mode/channels '$mode' is not opaque RGB" >&2
      fail=1
      canonical_mode="$mode"
      ;;
  esac

  sha="$(shasum -a 256 "$output" | awk '{print $1}')"
  if ! debug_dir="$(debug_dir_for "$asset_id")"; then
    echo "error: missing generate debug request/response for $stage/$hazard" >&2
    fail=1
    continue
  fi
  request_json="$debug_dir/request.json"
  response_json="$debug_dir/response.json"

  actual_status="$(response_status "$response_json")"
  if [ "$actual_status" != "200" ]; then
    echo "error: $asset_id response status $actual_status != 200" >&2
    fail=1
  fi

  actual_input_count="$(request_input_image_count "$request_json")"
  if [ "$actual_input_count" -ne 3 ]; then
    echo "error: $asset_id request input_image count $actual_input_count != 3" >&2
    fail=1
  fi

  actual_prompt="$(request_prompt "$request_json")"
  actual_model="$(request_model "$request_json")"
  if [ -z "$actual_prompt" ] || [ "$actual_prompt" = "null" ]; then
    echo "error: $asset_id missing request prompt text" >&2
    fail=1
  fi
  if [ -z "$actual_model" ] || [ "$actual_model" = "null" ]; then
    echo "error: $asset_id missing request model" >&2
    fail=1
  fi

  expected_postprocess="$(postprocess_json "$role")"
  recorded_postprocess="$(jq -c ".assets[$index].postprocess" "$MANIFEST")"
  if [ "$MODE" = "check" ] && [ "$recorded_postprocess" != "$expected_postprocess" ]; then
    echo "error: $asset_id postprocess does not match final $FINAL_DIMENSIONS derivation" >&2
    fail=1
  fi

  stddev="$(image_stddev "$output")"
  if ! awk -v v="$stddev" -v min="$LUMINANCE_STDDEV_MIN" 'BEGIN { exit !(v >= min) }'; then
    echo "error: $asset_id luminance stddev $stddev below non-flat threshold $LUMINANCE_STDDEV_MIN" >&2
    fail=1
  fi

  edge_metric="null"
  if [ "$role" = "body" ] || [ "$role" = "bed" ]; then
    edge_metric="$(edge_mae "$output")"
    if ! awk -v v="$edge_metric" -v max="$REPEAT_EDGE_MAE_MAX" 'BEGIN { exit !(v <= max) }'; then
      echo "error: $asset_id opposite-edge MAE $edge_metric above Repeat threshold $REPEAT_EDGE_MAE_MAX" >&2
      fail=1
    fi
  fi

  evidence_file="$TMP_DIR/evidence-$asset_id.json"
  if ! find_evidence "$stage" "$asset_id" "$request_json" "$response_json" | json_string_array >"$evidence_file"; then
    echo "error: missing review/QA evidence for $stage/$hazard" >&2
    fail=1
    printf '[]\n' >"$evidence_file"
  fi
  evidence_count="$(jq 'length' "$evidence_file")"
  if [ "$evidence_count" -lt 1 ]; then
    echo "error: empty evidence list for $stage/$hazard" >&2
    fail=1
  fi

  attempts="$(attempt_count "$asset_id")"
  jq -nc \
    --argjson index "$index" \
	    --arg sha "$sha" \
	    --arg dims "$dims" \
	    --arg mode "$canonical_mode" \
	    --arg provider "$provider" \
	    --arg model "$actual_model" \
	    --arg prompt "$actual_prompt" \
	    --argjson attempts "$attempts" \
	    --argjson postprocess "$expected_postprocess" \
	    --argjson evidence "$(cat "$evidence_file")" \
	    --argjson stddev "$stddev" \
	    --argjson edge "$edge_metric" \
	    --argjson stddev_min "$LUMINANCE_STDDEV_MIN" \
	    --argjson edge_max "$REPEAT_EDGE_MAE_MAX" \
    '{
      index: $index,
	      sha256: $sha,
	      dimensions: $dims,
	      mode: $mode,
	      provider: $provider,
	      model: $model,
	      prompt: $prompt,
	      generation_attempts: $attempts,
	      postprocess: $postprocess,
      validation_artifacts: $evidence,
      acceptance_metrics: {
        pending: false,
        luminance_stddev: $stddev,
	        opposite_edge_mae: $edge,
	        thresholds: {
	          luminance_stddev_min: $stddev_min,
	          repeat_edge_mae_max: $edge_max
	        }
      }
    }' >>"$updates_json"
done

if [ "$fail" -ne 0 ]; then
  echo "provenance $MODE failed; manifest not modified" >&2
  exit 1
fi

if [ "$MODE" = "check" ]; then
  echo "provenance check ok: $EXPECTED_COUNT generated assets, RGB $FINAL_DIMENSIONS, metrics and evidence present"
  exit 0
fi

tmp_manifest="$(mktemp "$TMP_DIR/manifest.XXXXXX")"
jq --slurpfile updates "$updates_json" \
  --arg final_dimensions "$FINAL_DIMENSIONS" \
  --argjson final_size "$FINAL_SIZE" '
  reduce $updates[] as $u (.;
	    .assets[$u.index].sha256 = $u.sha256
	    | .assets[$u.index].dimensions = $u.dimensions
	    | .assets[$u.index].mode = $u.mode
	    | .assets[$u.index].provider = $u.provider
	    | .assets[$u.index].model = $u.model
	    | .assets[$u.index].prompt = $u.prompt
	    | .assets[$u.index].generation_attempts = $u.generation_attempts
	    | .assets[$u.index].postprocess = $u.postprocess
	    | .assets[$u.index].import_settings.max_size = $final_size
    | .assets[$u.index].acceptance_metrics = $u.acceptance_metrics
    | .assets[$u.index].validation_artifacts = $u.validation_artifacts
    | .assets[$u.index].decision = "accepted"
	  )
	  | .status = "accepted"
	  | .finalized = "2026-08-11"
	  | .smoke = "_workspace/current/engineering/hazard-texture-gen/smoke/cinder-span-ember-vent-debug/"
	' "$MANIFEST" >"$tmp_manifest"
mv "$tmp_manifest" "$MANIFEST"

echo "provenance apply ok: updated $MANIFEST"
