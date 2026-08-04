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
