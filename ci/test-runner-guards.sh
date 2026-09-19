#!/usr/bin/env bash
# Self-validation for the qualification gate's result assertions.
#
# Usage: ci/test-runner-guards.sh
#
# Proves that assert_results accepts a genuine green run and rejects each way a
# run can look green without being one. Run this after touching ci/run-tests.sh
# or ci/lib/nunit-assert.sh — the whole point of this pass was to stop the gate
# from being able to lie about success, so the guards themselves are tested.
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/nunit-assert.sh
source "$HERE/lib/nunit-assert.sh"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

pass=0
fail=0

# Writes an NUnit-shaped results file.
#   make_result <path> <assembly> <total> <failed>
make_result() {
    local path="$1" assembly="$2" total="$3" failed="$4"
    cat >"$path" <<XML
<?xml version="1.0" encoding="utf-8"?>
<test-run id="2" testcasecount="$total" result="Passed" total="$total" passed="$((total - failed))" failed="$failed" inconclusive="0" skipped="0">
  <test-suite type="Assembly" id="1091" name="$assembly" />
</test-run>
XML
}

# expect_reject <label> <mode> <platform> <file> <started>
expect_reject() {
    local label="$1"; shift
    if out="$(assert_results "$@" 2>&1)"; then
        echo "  FAIL  $label — accepted, but should have been rejected"
        echo "        (output: $out)"
        fail=$((fail + 1))
    else
        echo "  ok    $label — rejected: $(echo "$out" | head -1)"
        pass=$((pass + 1))
    fi
}

# expect_accept <label> <mode> <platform> <file> <started>
expect_accept() {
    local label="$1"; shift
    if out="$(assert_results "$@" 2>&1)"; then
        echo "  ok    $label — accepted: $out"
        pass=$((pass + 1))
    else
        echo "  FAIL  $label — rejected, but should have been accepted"
        echo "        (output: $out)"
        fail=$((fail + 1))
    fi
}

echo "=== Qualification gate guard self-test ==="
NOW="$(date +%s)"
OLD=$((NOW - 3600))

GOOD="$TMP/good.xml"
make_result "$GOOD" "Escape.Tests.PlayMode.dll" 34 0

echo "-- must accept --"
expect_accept "genuine green PlayMode run" playMode PlayMode "$GOOD" "$NOW"

echo "-- must reject --"
expect_reject "missing results file" playMode PlayMode "$TMP/absent.xml" "$NOW"

: >"$TMP/empty.xml"
expect_reject "empty results file" playMode PlayMode "$TMP/empty.xml" "$NOW"

echo "not xml at all" >"$TMP/garbage.xml"
expect_reject "non-NUnit document" playMode PlayMode "$TMP/garbage.xml" "$NOW"

STALE="$TMP/stale.xml"
make_result "$STALE" "Escape.Tests.PlayMode.dll" 34 0
touch -t "$(date -r "$OLD" +%Y%m%d%H%M.%S)" "$STALE"
expect_reject "stale artifact from an earlier run" playMode PlayMode "$STALE" "$NOW"

MISLABELLED="$TMP/mislabelled.xml"
make_result "$MISLABELLED" "Escape.Tests.EditMode.dll" 84 0
expect_reject "EditMode results under a PlayMode request" playMode PlayMode "$MISLABELLED" "$NOW"

ZERO="$TMP/zero.xml"
make_result "$ZERO" "Escape.Tests.PlayMode.dll" 0 0
expect_reject "zero tests executed" playMode PlayMode "$ZERO" "$NOW"

RED="$TMP/red.xml"
make_result "$RED" "Escape.Tests.PlayMode.dll" 34 3
expect_reject "green exit but 3 recorded failures" playMode PlayMode "$RED" "$NOW"

echo
echo "=== $pass passed, $fail failed ==="
[[ "$fail" -eq 0 ]] || exit 1
