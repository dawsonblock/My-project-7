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

# Reads an attribute value out of the NUnit <test-run> root element.
# The result assertions live in ci/lib/nunit-assert.sh so that
# ci/test-runner-guards.sh can exercise them directly.
# shellcheck source=lib/nunit-assert.sh
source "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/lib/nunit-assert.sh"

run_mode() {
    local mode="$1"       # editMode | playMode
    local platform="$2"   # EditMode | PlayMode — the value -testPlatform expects
    local results="$PROJECT_DIR/TestResults-$mode.xml"
    echo "=== Running $mode tests (Unity $UNITY_VERSION) ==="

    # Delete first: an interrupted run must not leave a previous green artifact
    # where the assertions below would find it.
    rm -f "$results"
    local started
    started="$(date +%s)"

    # -testPlatform, not -testMode. There is no -testMode flag: passing it is
    # ignored and the run silently defaults to EditMode, so a "playMode" run
    # would produce a PlayMode-labelled file full of EditMode results.
    # Deliberately no -quit either: -runTests already quits when the run
    # finishes, and adding -quit makes the editor shut down *before* the tests
    # start — exiting 0 having executed nothing.
    "$UNITY_PATH" \
        -batchmode -nographics -silent-crashes \
        -projectPath "$PROJECT_DIR" \
        -runTests -testPlatform "$platform" \
        -testResults "$results" \
        -logFile "$PROJECT_DIR/Logs/ci-$mode.log"
    local code=$?
    # -runTests returns 0 on pass, 2 on test failure, 3 on run failure.
    if [[ $code -gt 3 ]]; then code=3; fi

    # Compiler errors never surface through the runner's exit code.
    if grep -q 'error CS' "$PROJECT_DIR/Logs/ci-$mode.log" 2>/dev/null; then
        echo "FAIL: $mode log contains compiler errors — the assembly under test is stale." >&2
        return 3
    fi

    assert_results "$mode" "$platform" "$results" "$started" || return $?
    return $code
}

status=0
case "$MODE" in
    editmode) run_mode editMode EditMode || status=$? ;;
    playmode) run_mode playMode PlayMode || status=$? ;;
    all)
        run_mode editMode EditMode || status=$?
        run_mode playMode PlayMode || { rc=$?; [[ $status -eq 0 ]] && status=$rc; }
        ;;
    *) echo "unknown mode: $MODE" >&2; exit 2 ;;
esac
exit $status
