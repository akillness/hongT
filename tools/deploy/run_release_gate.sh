#!/usr/bin/env bash
# Release-gate evidence producer for tools/deploy/release_provenance.py.
#
# The provenance schema (REQUIRED_EVIDENCE_MODES) demands six frozen artifacts.
# This script runs the Source-mode and Development-mode *engine* steps in the one
# order Unity permits (it takes a project-wide lock, so nothing here may overlap)
# and leaves the two browser steps to tools/qa/run_shadow_browser_evidence.mjs.
#
# Usage: bash tools/deploy/run_release_gate.sh <stage>
#   stage 1 = import gate, shadow-focused EditMode, full EditMode, payload unit
#             tests, Development build
#   stage 2 = Release build
set -uo pipefail
cd "$(dirname "$0")/../.."

UNITY="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity}"
[ -x "$UNITY" ] || UNITY="$HOME/.unity/bin/Unity"
EVIDENCE_DIR="${PAGES_EVIDENCE_DIR:-_workspace/current/qa/stage-character-shadows}"
LOG_DIR="_workspace/current/engineering/unity-logs"
mkdir -p "$EVIDENCE_DIR" "$LOG_DIR"

# Same headless hygiene as tools/unity_batch.sh: the Unity-MCP plugin fails a
# random innocent test when it cannot authorize, so mark the run as automation.
export CI="${CI:-true}"
export UNITY_MCP_KEEP_CONNECTED=false
export UNITY_MCP_START_SERVER=false
export PYTHONDONTWRITEBYTECODE=1

step() { echo "=== STEP $* ==="; }
fail() { echo "GATE-FAIL: $*"; exit 1; }
case "${1:-}" in
1)
  step "1/5 import-only compile gate"
  bash tools/unity_batch.sh import-only || fail "import-only"

  step "2/5 full EditMode"
  # One run, not two. `-testFilter "*Shadow*"` cannot be used here: the runner
  # maps it to groupNames and FullNameFilter.Match throws an NRE walking the
  # tree, so Unity exits 3 (RunError) and writes no results file at all
  # (measured: shadow-focused.log, FilterTestTreeTask.cs:13). The shadow subset
  # is extracted from this run's XML instead.
  bash tools/unity_batch.sh tests
  echo "full EditMode EXIT=$?"
  RESULTS="$(ls -t "$LOG_DIR"/test-results-*.xml 2>/dev/null | head -1)"
  [ -n "$RESULTS" ] || fail "no EditMode results"
  echo "RESULTS=$RESULTS"

  # Prop sheet contract. Pure Python, sub-second, no Unity lock. The two
  # properties are a mean the .mat tints divide by and a contrast floor below
  # which a "textured" prop is the flat tint it replaced. Both were enforced
  # only inside the generator, so a hand-edited or re-exported sheet reached a
  # release with nothing checking it. Also pins the cross-file constant:
  # normalize_prop_sheets.TARGET must equal PropImportPipeline.SheetMeanLuminance
  # or every prop ships at the wrong tint, and until now that agreement lived in
  # two comments saying "must equal" and zero assertions.
  step "3/5 prop sheet luminance + contrast"
  python3 -B tools/qa/normalize_prop_sheets.py --check || fail "prop sheets off contract"
  CS_TARGET="$(grep -oE 'SheetMeanLuminance = [0-9.]+f' Assets/Editor/PropImportPipeline.cs \
    | grep -oE '[0-9.]+' | head -1)"
  PY_TARGET="$(grep -oE '^TARGET = [0-9.]+' tools/qa/normalize_prop_sheets.py \
    | grep -oE '[0-9.]+' | head -1)"
  [ -n "$CS_TARGET" ] && [ -n "$PY_TARGET" ] || fail "could not read the sheet-mean constant from both files"
  awk -v a="$CS_TARGET" -v b="$PY_TARGET" 'BEGIN { exit (a == b) ? 0 : 1 }' \
    || fail "sheet-mean constant drift: C# $CS_TARGET vs python $PY_TARGET"
  echo "sheet-mean constant agrees: $CS_TARGET"

  step "4/5 release payload unit tests"
  python3 -B -m unittest -v tools.tests.test_release_payload 2>&1 | tail -20
  [ "${PIPESTATUS[0]}" -eq 0 ] || fail "test_release_payload"

  step "5/5 Development WebGL build"
  rm -rf build-development
  bash tools/unity_batch.sh build-development || fail "build-development"
  [ -f build-development/index.html ] || fail "no build-development/index.html"
  ;;

2)
  step "1/2 Release WebGL build"
  # The provenance refuses to be created twice, so the release tree must be the
  # one this candidate produced — not a leftover from an earlier commit.
  rm -rf build-webgl
  bash tools/unity_batch.sh build || fail "build"
  [ -f build-webgl/index.html ] || fail "no build-webgl/index.html"

  # ToonShaderRetentionGate can FAIL the build loudly, but its ABSENCE is silent:
  # it was inert for two builds (an early return on a not-yet-populated
  # report.summary.result) and every one of them reported green. "Found nothing"
  # and "checked nothing" are indistinguishable from outside — the same shape
  # this repo already refuses for empty scans. Assert the gate actually ran.
  step "2/2 shader retention gate ran"
  BUILD_LOG="$(ls -t "$LOG_DIR"/build-*.log 2>/dev/null | grep -v development | head -1)"
  [ -n "$BUILD_LOG" ] || fail "no release build log to check the retention gate against"
  grep -qE "ToonShaderRetentionGate.*variants retained" "$BUILD_LOG" \
    || fail "stripping gate did not run — ToonLit retention is UNVERIFIED in $BUILD_LOG"
  grep -oE "ToonShaderRetentionGate\] .*variants retained.*" "$BUILD_LOG" | tail -1
  ;;
*)
  echo "usage: run_release_gate.sh 1|2"
  exit 2
  ;;
esac

echo "GATE-STAGE-${1}-DONE"
