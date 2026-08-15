#!/usr/bin/env bash
# Headless Unity runner. Usage:
#   bash tools/unity_batch.sh method CinderCourt.EditorTools.CharacterImportPipeline.ImportAll
#   bash tools/unity_batch.sh tests            # EditMode tests
#   bash tools/unity_batch.sh import-only      # plain asset import (compile gate)
#   bash tools/unity_batch.sh build-development # Development WebGL QA output
set -uo pipefail
cd "$(dirname "$0")/.."

UNITY="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity}"
[ -x "$UNITY" ] || UNITY="$HOME/.unity/bin/Unity"
PROJECT="$(pwd)"
LOG_DIR="_workspace/current/engineering/unity-logs"
mkdir -p "$LOG_DIR"
STAMP="$(date +%H%M%S)"

# --- headless hygiene -------------------------------------------------------
# The Unity-MCP editor plugin (com.ivanmurzak.unity.mcp, a DEV tool — it is not
# game code and ships in no build) auto-connects to its hub the moment the
# editor loads, batch mode included. Headless it has no credentials, so it logs
#
#   [Error] McpManagerClientHub Server forcefully disconnected this plugin.
#           Reason: Authorization failed.
#
# and the Unity test runner fails whichever test happens to be executing when
# that line lands — a different, innocent test on every run. That is a false
# red in the gate, and it hid a real regression for a whole cycle.
#
# The plugin already knows how to stay quiet: UnityConnectionConfig.SetDefault
# does KeepConnected = !IsCi(), and EnvironmentUtils reads CI / GITHUB_ACTIONS /
# TF_BUILD plus the explicit UNITY_MCP_* overrides. We set both belts: CI marks
# the run as automation, and UNITY_MCP_KEEP_CONNECTED=false overrides a config
# already written to disk by an interactive editor session (SetDefault only
# applies when there is no saved config; the env override always applies).
#
# Set UNITY_BATCH_KEEP_MCP=1 to opt out and debug the plugin itself.
if [ "${UNITY_BATCH_KEEP_MCP:-0}" != "1" ]; then
  export CI="${CI:-true}"
  export UNITY_MCP_KEEP_CONNECTED=false
  export UNITY_MCP_START_SERVER=false
fi

case "${1:-}" in
  method)
    LOG="$LOG_DIR/${2##*.}-$STAMP.log"
    "$UNITY" -batchmode -projectPath "$PROJECT" -executeMethod "$2" \
      -logFile "$LOG" -nographics
    CODE=$?
    ;;
  method-gfx)
    # Same as `method` but WITH a graphics device. Anything that renders -
    # Camera.Render into a RenderTexture, ReadPixels - produces a uniform
    # buffer under -nographics rather than failing, and a flat dark buffer is
    # indistinguishable from this project's near-black-material defect. A mode
    # that can only mislead is worse than one that refuses, so rendering probes
    # get their own entry instead of quietly sharing `method`.
    LOG="$LOG_DIR/${2##*.}-gfx-$STAMP.log"
    "$UNITY" -batchmode -projectPath "$PROJECT" -executeMethod "$2" \
      -logFile "$LOG"
    CODE=$?
    ;;
  tests)
    LOG="$LOG_DIR/tests-$STAMP.log"
    RESULTS="$LOG_DIR/test-results-$STAMP.xml"
    "$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform EditMode \
      -testResults "$RESULTS" -logFile "$LOG" -nographics
    CODE=$?
    echo "RESULTS: $RESULTS"
    ;;
  import-only)
    LOG="$LOG_DIR/import-$STAMP.log"
    "$UNITY" -batchmode -projectPath "$PROJECT" -quit -logFile "$LOG" -nographics
    CODE=$?
    ;;
  build)
    LOG="$LOG_DIR/build-$STAMP.log"
    "$UNITY" -batchmode -projectPath "$PROJECT" \
      -executeMethod CinderCourt.EditorTools.BuildScript.BuildWebGL \
      -buildTarget WebGL -logFile "$LOG" -nographics
    CODE=$?
    ;;
  build-development)
    LOG="$LOG_DIR/build-development-$STAMP.log"
    "$UNITY" -batchmode -projectPath "$PROJECT" \
      -executeMethod CinderCourt.EditorTools.BuildScript.BuildWebGLDevelopment \
      -buildTarget WebGL -logFile "$LOG" -nographics
    CODE=$?
    ;;
  *)
    echo "usage: unity_batch.sh method <FQN> | tests | import-only | build-development | build"; exit 2 ;;
esac

# Belt-and-braces: fail if any assembly failed to compile, whatever Unity's own
# exit code said. A compile error in ANY assembly - including a test assembly
# the invoked mode never touches - makes -executeMethod a no-op, and the run
# still produces a log that looks ordinary.
#
# WHAT IS ACTUALLY MEASURED (2026-08-13). Probed by appending a deliberate
# CS0246 to a test file: both `import-only` and `method` exit 1 on their own, so
# this branch does NOT fire for them and is redundant today. The real incident
# was ImportAll-160031, which carries six CS0103 lines and no [PropImportPipeline]
# output - the method did not run. It was reported as ok because the CALLER piped
# the wrapper through `tail -2`, which hid the EXIT= line and made `&&` test
# tail's status instead. So the failure was in how the wrapper was invoked, and
# printing the CS lines here is what makes that visible at the call site.
#
# Checked after the case rather than inside one branch so every mode is covered,
# `import-only` included - it is the mandated compile gate (CLAUDE.md §4), and a
# gate that can pass on the failure it exists to detect is worse than no gate.
# Every branch sets $LOG. Redundant for `tests`, which yields no XML anyway.
if [ "$CODE" = "0" ] && grep -q "error CS" "$LOG"; then
  echo "COMPILE-ERROR: assemblies failed to build - ${1:-} did not do its job"
  grep -m5 "error CS" "$LOG"
  CODE=1
fi

echo "EXIT=$CODE LOG=$LOG"
if [ $CODE -ne 0 ]; then
  echo "--- last 60 log lines ---"
  tail -60 "$LOG"
fi
exit $CODE
