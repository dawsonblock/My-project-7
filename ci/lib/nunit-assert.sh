#!/usr/bin/env bash
# NUnit results assertions for the qualification gate.
#
# Sourced by ci/run-tests.sh and exercised by ci/test-runner-guards.sh. Kept in
# its own file so the guards can be tested directly: a gate that cannot be
# shown to fail is not a gate.
#
# Every guard here corresponds to a way this project's test gate has actually
# reported success without qualifying anything:
#   - no results file   → -quit made the editor exit before the tests started
#   - empty/garbage XML → the runner wrote nothing meaningful
#   - stale file        → an interrupted run left a previous green artifact
#   - wrong assembly    → -testMode was ignored, so EditMode ran under a
#                         PlayMode filename
#   - 0 tests           → the run executed nothing
#   - failures > 0      → Unity exited 0 while the document records red

# Reads a numeric attribute out of the NUnit <test-run> root element.
xml_attr() {
    local file="$1" attr="$2"
    grep -o "${attr}=\"[0-9]*\"" "$file" 2>/dev/null | head -1 | tr -dc '0-9' || true
}

# assert_results <mode> <platform> <results-file> <started-epoch-seconds>
# Returns 0 only for a genuine, current, all-green run of the requested
# platform. Prints the reason and returns 3 otherwise.
assert_results() {
    local mode="$1" platform="$2" results="$3" started="$4"

    if [[ ! -s "$results" ]]; then
        echo "FAIL: $mode produced no results file ($results) — the run did not execute." >&2
        return 3
    fi
    if ! grep -q '<test-run' "$results"; then
        echo "FAIL: $results is not an NUnit test-run document." >&2
        return 3
    fi

    local mtime
    mtime="$(stat -f %m "$results" 2>/dev/null || stat -c %Y "$results" 2>/dev/null || echo 0)"
    if [[ "$mtime" -lt "$started" ]]; then
        echo "FAIL: $results is older than this run (stale artifact) — refusing to trust it." >&2
        return 3
    fi

    # The labelled file must actually contain the labelled assembly.
    local assembly
    assembly="$(grep -o 'name="Escape.Tests.[A-Za-z]*\.dll"' "$results" 2>/dev/null | head -1 || true)"
    if [[ "$assembly" != *"$platform"* ]]; then
        echo "FAIL: $mode results do not come from the $platform assembly (found: ${assembly:-none})." >&2
        return 3
    fi

    local total failed
    total="$(xml_attr "$results" total)"
    failed="$(xml_attr "$results" failed)"
    if [[ -z "$total" || "$total" -eq 0 ]]; then
        echo "FAIL: $mode reported 0 tests — refusing to treat an empty run as a pass." >&2
        return 3
    fi
    if [[ -n "$failed" && "$failed" -ne 0 ]]; then
        echo "FAIL: $mode reported $failed failing test(s)." >&2
        return 3
    fi

    echo "    $mode: $total tests, 0 failed ($assembly)"
    return 0
}
