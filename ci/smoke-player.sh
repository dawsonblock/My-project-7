#!/usr/bin/env bash
# Runs a built player's deterministic smoke test and gates on its result.
# Usage: ci/smoke-player.sh [path/to/App.app]
#
# The player is launched headless with -gameSmokeTest, which exercises scene
# transitions, the progression chain and a save round-trip, then exits with a
# status code and prints SMOKE PASS / SMOKE FAIL.
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOG="$PROJECT_DIR/Logs/ci-smoke.log"
mkdir -p "$(dirname "$LOG")"

APP="${1:-}"
if [[ -z "$APP" ]]; then
    # Search every path a builder may have written to: this project's Builds/,
    # game-ci's default build/ (singular), and the older builds/.
    APP="$(find "$PROJECT_DIR/Builds" "$PROJECT_DIR/builds" "$PROJECT_DIR/build" \
        -maxdepth 3 -name "*.app" 2>/dev/null | head -1 || true)"
fi
if [[ -z "$APP" || ! -d "$APP" ]]; then
    echo "No player found. Build one first (ci/build-player.sh) or pass a path." >&2
    exit 2
fi

# Resolve the executable from the bundle rather than assuming the product
# name — the .app's inner binary is named after PlayerSettings.productName.
PLIST="$APP/Contents/Info.plist"
EXE_NAME=""
if [[ -f "$PLIST" ]]; then
    EXE_NAME="$(/usr/libexec/PlistBuddy -c "Print :CFBundleExecutable" "$PLIST" 2>/dev/null || true)"
fi
BIN="$APP/Contents/MacOS/${EXE_NAME:-$(basename "$APP" .app)}"
if [[ ! -x "$BIN" ]]; then
    echo "Player executable not found at $BIN" >&2
    exit 2
fi

echo "=== Player smoke test ==="
echo "    app: $APP"
rm -f "$LOG"

set +e
"$BIN" -batchmode -nographics -gameSmokeTest -logFile "$LOG"
code=$?
set -e

if [[ $code -ne 0 ]]; then
    echo "FAIL: player exited $code" >&2
    grep -E "SMOKE (PASS|FAIL)|Exception|error CS" "$LOG" >&2 || true
    exit 1
fi

if ! grep -q "SMOKE PASS" "$LOG"; then
    echo "FAIL: no SMOKE PASS marker in $LOG" >&2
    grep -E "SMOKE (PASS|FAIL)|Exception" "$LOG" >&2 || true
    exit 1
fi

echo "OK: $(grep -m1 'SMOKE PASS' "$LOG")"
