#!/usr/bin/env bash
# Determinism gate: regenerates all scenes/prefabs twice and fails if the
# second regeneration produces a git diff. Catches both content drift and
# serialization churn.
# Usage: ci/verify-generation.sh
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
UNITY_VERSION="$(grep -oE 'm_EditorVersion: [0-9a-z.]+' "$PROJECT_DIR/ProjectSettings/ProjectVersion.txt" | awk '{print $2}')"
UNITY_PATH="${UNITY_PATH:-/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity}"

cd "$PROJECT_DIR"

build_all() {
    "$UNITY_PATH" \
        -batchmode -nographics -silent-crashes \
        -projectPath "$PROJECT_DIR" \
        -executeMethod Escape.EditorTools.BuildAll.Run \
        -logFile "$PROJECT_DIR/Logs/ci-buildall.log" \
        -quit
}

echo "=== BuildAll pass 1 ==="
build_all
echo "=== BuildAll pass 2 (determinism check) ==="
build_all

if ! git diff --quiet -- Assets/; then
    echo "FAIL: regeneration produced a diff — generation is not deterministic:" >&2
    git diff --stat -- Assets/ >&2
    exit 1
fi
echo "OK: regenerated content is byte-identical."
