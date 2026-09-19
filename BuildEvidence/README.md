# BuildEvidence — stabilization qualification

Captured 2026-09-18 on Unity 6000.6.0f1 (macOS, arm64).

Branch: release/stabilization-repair
Baseline tag: pre-stabilization-repair (332a2e1, pre-repair state)

## Gates

| Gate | Status | Evidence |
|---|---|---|
| Editor + test assembly compile | PASS — 0 errors, 0 warnings | compile/summary.txt, compile/editor.log |
| Standalone player compile + link | PASS — Succeeded, 8 scenes, 117,289,247 bytes, 0 errors | player-build/build-summary.txt |
| Standalone player boot smoke | PASS — boots clean, 0 exceptions | player-build/smoke-test.txt |
| Player-build CI script | PARTIAL — lockfile guard verified; batchmode wrapper unexercised | player-build/smoke-test.txt |
| EditMode tests | PASS — 61/61 | tests/summary.txt |
| PlayMode tests | PASS — 26/26 | tests/playmode-TestResults.xml |
| Deterministic generation | PASS — 26 files byte-identical over two builds | deterministic-generation/result.txt |
| Progression contract | PASS — explicit vs evidence completion, gated routing | progression/summary.txt |
| Baseline capture | DONE | baseline/ |

## baseline/

- environment.txt — Unity version, branch, HEAD, archive hash, test counts
- archive-sha256.txt — SHA-256 of a tar of all tracked + untracked source
- manifest.json, packages-lock.json — package state
- ProjectSettings/ — full project settings copy
- build-settings-EditorBuildSettings.asset — enabled scene list

## Caveats

- The Unity Test Runner can wedge after a player build ("An unexpected error
  happened while running tests"); a domain reload alone did not clear it, but
  a retry did. Verify the assembly rebuilt (see the AGENTS.md note on stale
  assemblies) before trusting any green run.
- A test run reports the count of the assembly it loaded. A stale assembly
  once reported 51/51 while a compile error sat in the tree; the numbers
  above were re-captured after confirming the assemblies rebuilt.
- The smoke test boots the player headless and checks for exceptions. It
  drives NO input, so it does not qualify the first-person controls — see
  player-build/smoke-test.txt.
- A player build rewrites ProjectSettings/ProjectSettings.asset: it
  reorders (and transiently empties) `preloadedAssets`. Revert that churn
  rather than committing it.
- BuildEvidence/ is tracked so the qualification record travels with the repo.
  Only the player binaries are ignored (`player-build/*.app` and the
  `*_BackUpThisFolder_*` sidecar) — they are 112 MB and reproducible with
  `ci/build-player.sh`.
