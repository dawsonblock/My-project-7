#!/usr/bin/env bash
# Standalone player build gate.
# Usage: ci/build-player.sh [output-path]
#
# Compiles the StandaloneOSX player and fails if the build does not succeed.
# Requires the editor to be closed — Unity holds Temp/UnityLockfile while it
# is open, and a batchmode build against a locked project hangs or fails.
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_VERSION="$(grep -oE 'm_EditorVersion: [0-9a-z.]+' "$PROJECT_DIR/ProjectSettings/ProjectVersion.txt" | awk '{print $2}')"
UNITY_PATH="${UNITY_PATH:-/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity}"

if [[ ! -x "$UNITY_PATH" ]]; then
    echo "Unity editor not found at $UNITY_PATH" >&2
    exit 2
fi

if [[ -f "$PROJECT_DIR/Temp/UnityLockfile" ]]; then
    echo "Unity has the project open (Temp/UnityLockfile) — close the editor first." >&2
    exit 2
fi

OUTPUT="${1:-$PROJECT_DIR/Builds/StandaloneOSX/EscapeTheElites.app}"
mkdir -p "$(dirname "$OUTPUT")"
LOG="$PROJECT_DIR/Logs/ci-player-build.log"
mkdir -p "$(dirname "$LOG")"

echo "=== Building StandaloneOSX player (Unity $UNITY_VERSION) ==="
echo "    output: $OUTPUT"

set +e
ETE_PLAYER_OUTPUT="$OUTPUT" "$UNITY_PATH" \
    -batchmode -nographics -silent-crashes \
    -projectPath "$PROJECT_DIR" \
    -executeMethod Escape.EditorTools.PlayerBuild.Build \
    -logFile "$LOG" \
    -quit
code=$?
set -e

# Unity exits 0 even for some failures — trust the artifact and the summary.
if [[ $code -ne 0 ]]; then
    echo "FAIL: build exited $code. See $LOG" >&2
    exit 1
fi

if [[ ! -d "$OUTPUT" ]]; then
    echo "FAIL: no player produced at $OUTPUT. See $LOG" >&2
    exit 1
fi

if ! grep -q "\[PlayerBuild\] Succeeded" "$LOG"; then
    echo "FAIL: build did not report success. See $LOG" >&2
    grep -E "\[PlayerBuild\]|error CS" "$LOG" >&2 || true
    exit 1
fi

echo "OK: player built at $OUTPUT"
