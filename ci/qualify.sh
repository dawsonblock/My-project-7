#!/usr/bin/env bash
# Bound qualification run for Escape the Elites.
#
# Usage: ci/qualify.sh [--allow-dirty]
#
# Produces ONE evidence directory under BuildEvidence/ that binds every claim
# in it to the exact source revision that produced it: source hash, the
# regenerated content check, the determinism gate, both test suites, the
# standalone player artifact hash, and the raw smoke log.
#
# The point is that no stage can report success without evidence, and no
# manifest is written unless every stage passed. A partial run leaves the
# stage list showing exactly where it stopped.
#
# Refuses to run against a dirty tree unless --allow-dirty is given; in that
# case the manifest records "qualified": false so a dirty run can never be
# mistaken for a release qualification.
set -uo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$PROJECT_DIR"

ALLOW_DIRTY=0
if [[ "${1:-}" == "--allow-dirty" ]]; then
    ALLOW_DIRTY=1
fi

UNITY_VERSION="$(grep -oE 'm_EditorVersion: [0-9a-z.]+' ProjectSettings/ProjectVersion.txt | awk '{print $2}')"
UNITY_PATH="${UNITY_PATH:-/Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity}"

RUN_ID="$(date -u +%Y-%m-%dT%H%M%SZ)"
# A dirty run is not evidence, so it must not land in the tracked evidence tree:
# `git add -A` would sweep a qualified:false manifest into a commit and the
# record would then contain a run that qualifies nothing. Dirty runs go to
# unqualified-<utc>/, which .gitignore keeps out of the repository entirely.
if [[ "$ALLOW_DIRTY" -eq 1 ]]; then
    EVIDENCE="$PROJECT_DIR/BuildEvidence/unqualified-$RUN_ID"
else
    EVIDENCE="$PROJECT_DIR/BuildEvidence/qualification-$RUN_ID"
fi
STAGES="$EVIDENCE/stages.txt"
APP="$PROJECT_DIR/Builds/StandaloneOSX/EscapeTheElites.app"

stage_failures=0

log()  { echo "$*"; }
warn() { echo "$*" >&2; }

# ---------------------------------------------------------------- preconditions

if [[ ! -x "$UNITY_PATH" ]]; then
    warn "Unity editor not found at $UNITY_PATH"
    exit 2
fi
if [[ -f "$PROJECT_DIR/Temp/UnityLockfile" ]]; then
    warn "Unity has the project open (Temp/UnityLockfile) — close the editor first."
    exit 2
fi

DIRTY=0
if [[ -n "$(git status --porcelain)" ]]; then
    DIRTY=1
    if [[ "$ALLOW_DIRTY" -eq 0 ]]; then
        warn "Working tree is dirty. Commit first, or pass --allow-dirty to record a"
        warn "non-qualifying run (the manifest will say \"qualified\": false)."
        exit 2
    fi
    warn "Working tree is dirty — this run will be recorded as NOT qualified."
fi

mkdir -p "$EVIDENCE"
: >"$STAGES"

log "=== Bound qualification run ==="
log "    run id   : $RUN_ID"
log "    evidence : $EVIDENCE"
log "    unity    : $UNITY_VERSION"

# ---------------------------------------------------------------- helpers

# A deterministic digest of a directory's contents (paths + file hashes).
hash_dir() {
    ( cd "$1" && find . -type f -print0 | sort -z | xargs -0 shasum -a 256 ) \
        | shasum -a 256 | awk '{print $1}'
}

# Digest of the tracked working tree — binds evidence to the source actually
# built, including uncommitted edits.
source_tree_hash() {
    git ls-files -z | sort -z | xargs -0 shasum -a 256 | shasum -a 256 | awk '{print $1}'
}

unity_exec() {
    local logfile="$1" method="$2"
    rm -f "$logfile"
    "$UNITY_PATH" -batchmode -nographics -silent-crashes \
        -projectPath "$PROJECT_DIR" \
        -executeMethod "$method" \
        -logFile "$logfile" -quit
    local code=$?
    if [[ $code -ne 0 ]]; then
        warn "  Unity exited $code (see $logfile)"
        return 1
    fi
    if grep -q 'error CS' "$logfile" 2>/dev/null; then
        warn "  compiler errors in $logfile"
        return 1
    fi
    return 0
}

# stage <name> <function...>
stage() {
    local name="$1"; shift
    log ""
    log "--- [$name] ---"
    if "$@"; then
        log "[$name] PASS"
        echo "$name: PASS" >>"$STAGES"
        return 0
    fi
    warn "[$name] FAIL"
    echo "$name: FAIL" >>"$STAGES"
    stage_failures=$((stage_failures + 1))
    return 1
}

# ---------------------------------------------------------------- source identity

stage_source() {
    {
        echo "commit: $(git rev-parse HEAD)"
        echo "branch: $(git rev-parse --abbrev-ref HEAD)"
        echo "dirty: $DIRTY"
        echo "unity: $UNITY_VERSION"
        echo "capturedUtc: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
    } >"$EVIDENCE/source.txt"

    source_tree_hash >"$EVIDENCE/source.sha256"
    git status --porcelain >"$EVIDENCE/tree-before.txt"
    log "  commit    : $(git rev-parse --short HEAD)"
    log "  tree sha  : $(cat "$EVIDENCE/source.sha256")"
    log "  dirty     : $DIRTY"
    return 0
}

# ---------------------------------------------------------------- generated content

stage_generated_content() {
    git status --porcelain -- Assets/_Game/Resources/Definitions >"$EVIDENCE/generated-before.txt"
    unity_exec "$EVIDENCE/generated-content.log" \
        Escape.EditorTools.CiChecks.VerifyGeneratedContentMatchesSource || return 1
    git status --porcelain -- Assets/_Game/Resources/Definitions >"$EVIDENCE/generated-after.txt"
    if ! diff -q "$EVIDENCE/generated-before.txt" "$EVIDENCE/generated-after.txt" >/dev/null; then
        warn "  reimporting changed the tracked definitions — they were out of sync with the JSON"
        diff -u "$EVIDENCE/generated-before.txt" "$EVIDENCE/generated-after.txt" >&2 || true
        return 1
    fi
    log "  definitions are in sync with MigrationReference/"
    return 0
}

# ---------------------------------------------------------------- determinism

stage_deterministic_generation() {
    git status --porcelain -- Assets/_Game/Scenes Assets/_Game/Prefabs >"$EVIDENCE/gen-before.txt"
    unity_exec "$EVIDENCE/deterministic-generation.log" \
        Escape.EditorTools.CiChecks.VerifyDeterministicGeneration || return 1
    git status --porcelain -- Assets/_Game/Scenes Assets/_Game/Prefabs >"$EVIDENCE/gen-after.txt"
    if ! diff -q "$EVIDENCE/gen-before.txt" "$EVIDENCE/gen-after.txt" >/dev/null; then
        warn "  regeneration changed tracked scenes/prefabs — do NOT commit a mass fileID diff."
        warn "  Revert prefabs and scenes together, then regenerate once from a clean tree."
        diff -u "$EVIDENCE/gen-before.txt" "$EVIDENCE/gen-after.txt" >&2 || true
        return 1
    fi
    log "  generation is deterministic and in sync with the tracked content"
    return 0
}

# ---------------------------------------------------------------- tests

stage_tests() {
    # Invoked through bash, not ./, because this repo has core.fileMode=false:
    # the executable bit is not tracked, so a fresh clone gets 644 and ./ci/…
    # dies with "Permission denied" before anything runs.
    if ! bash "$PROJECT_DIR/ci/run-tests.sh" all >"$EVIDENCE/tests-console.log" 2>&1; then
        warn "  ci/run-tests.sh failed:"
        tail -20 "$EVIDENCE/tests-console.log" >&2 || true
        return 1
    fi
    tail -4 "$EVIDENCE/tests-console.log" | sed 's/^/  /'
    cp -f TestResults-editMode.xml "$EVIDENCE/TestResults-EditMode.xml"
    cp -f TestResults-playMode.xml "$EVIDENCE/TestResults-PlayMode.xml"
    cp -f Logs/ci-editMode.log "$EVIDENCE/editmode.log"
    cp -f Logs/ci-playMode.log "$EVIDENCE/playmode.log"
    return 0
}

# ---------------------------------------------------------------- player + smoke

stage_player_build() {
    rm -rf "$APP"
    if ! bash "$PROJECT_DIR/ci/build-player.sh" "$APP" >"$EVIDENCE/player-build-console.log" 2>&1; then
        warn "  ci/build-player.sh failed:"
        tail -20 "$EVIDENCE/player-build-console.log" >&2 || true
        return 1
    fi
    [[ -d "$APP" ]] || { warn "  no player at $APP"; return 1; }
    cp -f Logs/ci-player-build.log "$EVIDENCE/player-build.log" 2>/dev/null || true
    hash_dir "$APP" >"$EVIDENCE/player.sha256"
    log "  artifact : $APP"
    log "  sha256   : $(cat "$EVIDENCE/player.sha256")"
    return 0
}

stage_smoke() {
    if ! bash "$PROJECT_DIR/ci/smoke-player.sh" "$APP" >"$EVIDENCE/smoke-console.log" 2>&1; then
        warn "  smoke failed:"
        tail -20 "$EVIDENCE/smoke-console.log" >&2 || true
        cp -f Logs/ci-smoke.log "$EVIDENCE/smoke.log" 2>/dev/null || true
        return 1
    fi
    cp -f Logs/ci-smoke.log "$EVIDENCE/smoke.log"
    grep -m1 'SMOKE PASS' "$EVIDENCE/smoke.log" | sed 's/^/  /'
    return 0
}

# ---------------------------------------------------------------- run

stage source                  stage_source                  || true
stage generated-content       stage_generated_content       || true
stage deterministic-generation stage_deterministic_generation || true
stage tests                   stage_tests                   || true
stage player-build            stage_player_build            || true
stage smoke                   stage_smoke                   || true

# ---------------------------------------------------------------- manifest

git status --porcelain >"$EVIDENCE/tree-after.txt"

# A player build is known to rewrite ProjectSettings (preloadedAssets, URP
# global settings, the volume profile). Record it rather than let it pass
# unnoticed: the manifest's source hash describes the tree as it was when the
# run started, so churn afterwards is worth stating.
tree_changed=false
if ! diff -q "$EVIDENCE/tree-before.txt" "$EVIDENCE/tree-after.txt" >/dev/null 2>&1; then
    tree_changed=true
    warn "Tracked files changed during the run — see tree-after.txt (revert build churn)."
    diff -u "$EVIDENCE/tree-before.txt" "$EVIDENCE/tree-after.txt" >&2 || true
fi

# Counts are null when a stage failed before producing its results file —
# null says "unknown", whereas a sentinel like -1 reads like a real number.
num_or_null() {
    if [[ -n "$1" ]]; then echo "$1"; else echo "null"; fi
}

edit_total="$(grep -o 'total="[0-9]*"' "$EVIDENCE/TestResults-EditMode.xml" 2>/dev/null | head -1 | tr -dc '0-9' || true)"
edit_failed="$(grep -o 'failed="[0-9]*"' "$EVIDENCE/TestResults-EditMode.xml" 2>/dev/null | head -1 | tr -dc '0-9' || true)"
play_total="$(grep -o 'total="[0-9]*"' "$EVIDENCE/TestResults-PlayMode.xml" 2>/dev/null | head -1 | tr -dc '0-9' || true)"
play_failed="$(grep -o 'failed="[0-9]*"' "$EVIDENCE/TestResults-PlayMode.xml" 2>/dev/null | head -1 | tr -dc '0-9' || true)"
schema_version="$(grep -o '"version": *[0-9]*' Assets/_Game/MigrationReference/current-save-schema.json | head -1 | tr -dc '0-9' || true)"
[[ -n "$schema_version" ]] || schema_version="null"

all_passed=true
[[ "$stage_failures" -eq 0 ]] || all_passed=false
qualified="$all_passed"
[[ "$DIRTY" -eq 0 ]] || qualified=false

# Human-readable reason, so a non-qualifying manifest says why in one line.
if [[ "$stage_failures" -gt 0 ]]; then
    reason="$stage_failures stage(s) failed — see stages.txt"
elif [[ "$DIRTY" -eq 1 ]]; then
    reason="all stages passed, but the tree had uncommitted changes"
else
    reason="all stages passed on a clean tree"
fi

# "stage: PASS; stage: PASS" — one separator, no doubled delimiters.
stages_summary="$(awk '{printf "%s%s", sep, $0; sep="; "}' "$STAGES")"

cat >"$EVIDENCE/manifest.json" <<JSON
{
  "runId": "$RUN_ID",
  "qualified": $qualified,
  "qualifiedReason": "$reason",
  "sourceCommit": "$(git rev-parse HEAD)",
  "sourceBranch": "$(git rev-parse --abbrev-ref HEAD)",
  "sourceTreeClean": $([[ "$DIRTY" -eq 0 ]] && echo true || echo false),
  "sourceTreeSha256": "$(cat "$EVIDENCE/source.sha256")",
  "unityVersion": "$UNITY_VERSION",
  "saveSchemaVersion": $schema_version,
  "postRunTreeChanged": $tree_changed,
  "generatedContent": { "inSyncWithSource": $(grep -q '^generated-content: PASS' "$STAGES" && echo true || echo false) },
  "deterministicGeneration": { "passed": $(grep -q '^deterministic-generation: PASS' "$STAGES" && echo true || echo false) },
  "tests": {
    "editMode": { "total": $(num_or_null "$edit_total"), "failed": $(num_or_null "$edit_failed") },
    "playMode": { "total": $(num_or_null "$play_total"), "failed": $(num_or_null "$play_failed") }
  },
  "player": {
    "platform": "StandaloneOSX",
    "path": "Builds/StandaloneOSX/EscapeTheElites.app",
    "sha256": "$(cat "$EVIDENCE/player.sha256" 2>/dev/null || echo "")"
  },
  "smoke": {
    "passed": $(grep -q '^smoke: PASS' "$STAGES" && echo true || echo false),
    "rawLog": "smoke.log"
  },
  "stages": "$stages_summary"
}
JSON

log ""
log "=== stages ==="
cat "$STAGES"
log ""
log "=== manifest ==="
cat "$EVIDENCE/manifest.json"
log ""

if [[ "$all_passed" != true ]]; then
    warn "QUALIFICATION FAILED — no qualified manifest. See $STAGES"
    exit 1
fi
if [[ "$DIRTY" -eq 1 ]]; then
    warn "All stages passed, but the tree was dirty, so this run is NOT qualified."
    warn "Commit the revision and re-run ci/qualify.sh for a qualifying manifest."
    exit 1
fi

log "QUALIFIED — evidence bound to $(git rev-parse --short HEAD) in $EVIDENCE"
