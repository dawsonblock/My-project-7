#!/usr/bin/env bash
# Batchmode test runner for Escape the Elites.
# Usage: ci/run-tests.sh [editmode|playmode|all]   (default: all)
#
# Writes NUnit XML to <project>/TestResults-<mode>.xml and exits non-zero on
# failure. UNITY_PATH may be overridden; defaults to the project version's
# editor under the standard Hub install location.
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MODE="${1:-all}"
UNITY_VERSION="$(grep -oE 'm_EditorVersion: [0-9a-z.]+' "$PROJECT_DIR/ProjectSettings/ProjectVersion.txt" | awk '{print $2}')"

UNITY_PATH="${UNITY_PATH:-/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity}"
if [[ ! -x "$UNITY_PATH" ]]; then
    echo "Unity editor not found at $UNITY_PATH" >&2
    exit 2
fi

run_mode() {
    local mode="$1"
    echo "=== Running $mode tests (Unity $UNITY_VERSION) ==="
    "$UNITY_PATH" \
        -batchmode -nographics -silent-crashes \
        -projectPath "$PROJECT_DIR" \
        -runTests -testMode "$mode" \
        -testResults "$PROJECT_DIR/TestResults-$mode.xml" \
        -logFile "$PROJECT_DIR/Logs/ci-$mode.log" \
        -quit
    local code=$?
    # -runTests returns 0 on pass, 2 on test failure, 3 on run failure.
    if [[ $code -gt 3 ]]; then code=3; fi
    return $code
}

status=0
case "$MODE" in
    editmode) run_mode editMode || status=$? ;;
    playmode) run_mode playMode || status=$? ;;
    all)
        run_mode editMode || status=$?
        run_mode playMode || { rc=$?; [[ $status -eq 0 ]] && status=$rc; }
        ;;
    *) echo "unknown mode: $MODE" >&2; exit 2 ;;
esac
exit $status
