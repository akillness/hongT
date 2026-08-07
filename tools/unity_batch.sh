#!/usr/bin/env bash
# Headless Unity runner. Usage:
#   bash tools/unity_batch.sh method CinderCourt.EditorTools.CharacterImportPipeline.ImportAll
#   bash tools/unity_batch.sh tests            # EditMode tests
#   bash tools/unity_batch.sh import-only      # plain asset import (compile gate)
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
  *)
    echo "usage: unity_batch.sh method <FQN> | tests | import-only | build"; exit 2 ;;
esac

echo "EXIT=$CODE LOG=$LOG"
if [ $CODE -ne 0 ]; then
  echo "--- last 60 log lines ---"
  tail -60 "$LOG"
fi
exit $CODE
