# Escape the Elites — agent notes

Unity 6000.6.0f1, URP 17.6.0, Input System 1.20.0, AI Navigation 2.0.14.

## Build & verify

- Regenerate everything: `Tools → Escape the Elites → Build All`
  (layers → content import → prefabs → scenes → materials → volume profile)
- **Never generate while the editor is in play mode.** `SceneFactory` opens
  scenes with `EditorSceneManager.NewScene`, which throws in play mode — a run
  that fails there has already recreated prefabs with fresh fileIDs, so the
  next successful run renumbers every scene that references them and you get a
  huge meaningless diff. `BuildAll.RefuseIfPlaying` guards the entry points;
  if you ever see a mass fileID diff, revert the prefabs and scenes together
  and regenerate once from a clean tree.
- Tests: `./ci/run-tests.sh [editmode|playmode|all]` (batchmode, writes
  `TestResults-<mode>.xml`), or Test Runner window
- Standalone player: `./ci/build-player.sh [output]` (batchmode; refuses to run
  while the editor holds `Temp/UnityLockfile`), or the menu
  `Tools → Escape the Elites → Build Standalone Player`
- Determinism gate: `./ci/verify-generation.sh`, or menu
  `Tools → Escape the Elites → Verify Deterministic Generation`
  (BuildAll twice → all generated files must be byte-identical)
- **This repo has `core.fileMode=false`**, so git does not track the
  executable bit. A new script under `ci/` will be committed as `100644`
  unless you set it explicitly — and then `./ci/new-script.sh` dies with
  "Permission denied" on a fresh clone and in CI while working fine locally:
  `git update-index --chmod=+x ci/new-script.sh`. Callers inside other
  scripts should use `bash ci/x.sh` so they don't depend on the bit at all.
- **Bound qualification run: `ci/qualify.sh [--allow-dirty]`.** One command
  produces one evidence directory under `BuildEvidence/qualification-<utc>/`
  that binds every claim to the exact source revision: source tree hash, the
  generated-content sync check, the determinism gate, both test suites, the
  player artifact hash and the raw smoke log. Any failed stage → no qualified
  manifest, and the stage list shows where it stopped. It refuses a dirty tree
  unless `--allow-dirty`, which records `"qualified": false` rather than
  quietly qualifying an uncommitted revision.
- Generated-content drift gate:
  `Tools → Escape the Elites → Verify Generated Content Matches Source`
  (reimports the definitions and fails if any tracked `.asset` changed — i.e.
  if someone hand-edited a generated file). Also a stage of `ci/qualify.sh`.
- A player build can rewrite settings files. `ci/qualify.sh` reports this as
  `postRunTreeChanged`, so check the manifest rather than assuming — it is
  currently **false** on a normal build.
  - Historically `ProjectSettings/ProjectSettings.asset` churned
    `preloadedAssets` and `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`
    lost a shader-stripping `rid` on every build. That turned out to be the
    pending consequence of settings objects owned by packages the project no
    longer uses: once the removals in `chore/package-reduction` were kept, a
    subsequent full build reported no churn at all. If it reappears, revert the
    churn and find out which package reintroduced it, rather than reverting on
    sight every time.
  - `Assets/Settings/DefaultVolumeProfile.asset` still gains `PaniniProjection`
    and `ProbeVolumesOptions` as documents Unity does not load. They are inert
    (panini distance 0, no probe volumes) and cannot be removed or disabled
    from code, because the loaded component list does not contain them.

## Generated-content invariants — do not break

- **Definition assets under `Assets/_Game/Resources/Definitions/` are
  generated.** `WebDataImporter` (step 2 of `BuildAll`) rewrites them from the
  frozen JSON in `Assets/_Game/MigrationReference/` (`objectives.json`,
  `evidence.json`, `terminals.json`, …). Edit the JSON, never the `.asset` —
  a hand edit survives until the next `BuildAll` and then silently reverts,
  and the determinism gate still passes because both runs agree on the
  JSON-derived result. Requirements the *domain* enforces (for example the
  broadcast key on `route_broadcast`) must therefore be declared in the JSON.
- All `.unity` scenes are YAML. NavMeshData lives in standalone
  `Assets/_Game/Scenes/NavMesh/*.asset` — never call
  `NavMeshSurface.BuildNavMesh()` without extracting to an asset, or the
  scene silently serializes binary.
- `SceneYamlNormalizer` runs at the end of `SceneFactory.BuildAll` and
  canonicalizes scene YAML (rank-sorted docs, sequential fileIDs).
- **Stripped-object invariant:** a stripped doc's fileID must equal its
  PrefabInstance's fileID + k. The normalizer allocates them contiguously.
- **Never renumber `.prefab` files.** Scenes reference prefab internals via
  `{fileID, guid}` — renumbering produces "corrupted file IDs" and Missing
  Prefab instances at load.

## Architecture quick map

- **Packages are deliberately minimal — 11 direct dependencies.** The template's
  feature packs (`characters-animation`, `gameplay-storytelling`,
  `worldbuilding`) and the unused runtime packages (Behavior, Physics, Pipeline,
  Services, Timeline) were removed on `chore/package-reduction`, which took the
  player from 112M / 221 managed assemblies to 99M / 151. Two editor-only
  packages are kept on purpose: `asset-manager-for-unity` (the committed `uam/`
  tracking folder depends on it) and `ai.assistant` (it provides
  `Unity.AI.MCP.Editor`, the unity-mcp bridge). Do not re-add a package without
  checking `BuildEvidence/package-audit.txt` and re-running `ci/qualify.sh`.

- `GameRoot` (Bootstrap scene) → `GameServices` registry → services
  (`ISceneService`, `ISaveService`, `ISaveCoordinator`, `IInputGate`, …)
- Commands mutate `GameState`; `GameEventBus` publishes. Doors/cameras/
  terminals persist via `GameState` lists + `IWorldObject.RestoreFromState`.
- Saves: enveloped v3 JSON, atomic `tmp → verify → replace → .bak`, recovery
  order primary→bak, `SaveMigrator` for old versions, `ISaveParticipant`
  captures volatile state (player pose incl. camera pitch, flashlight).
  Slot ids are validated (`[A-Za-z0-9_-]+`) because they become file names.
- Objective completion has two gates, deliberately separate:
  `ActivationRequirementsMet` (prerequisite objectives) and
  `CompletionRequirementsMet` (prerequisites *and* the objective's declared
  evidence). Domain actions authorize themselves with `CanComplete`; a bare
  `CompleteObjectiveCommand` never finishes an Explicit objective and never
  finishes an Evidence objective whose evidence is missing.
- Input: `PlayerInputActions` — Player/UI maps toggle with `IInputGate.UiOpen`;
  **System map (Pause/Cancel) stays enabled always**. Modal cancel routes
  through `InputGate` → `ICancelableUi`. UI components must keep their host
  active and lazy-bind `PlayerInputReader` (player may spawn later).

## Editor/testing gotchas

- `InputSystem.settings.editorInputBehaviorInPlayMode =
  PointersAndKeyboardsRespectGameViewFocus` means *device* input is ignored
  while the Game view is unfocused, so don't drive tests with real device
  events (`QueueStateEvent`). `InputTestFixture`'s `Press`/`Set` write control
  values directly and do work in batchmode — measured, see the
  `Time.deltaTime` note below before blaming input for a failing PlayMode test.
- TestRunnerApi runs can wedge after an aborted run — force a domain
  reload (`RequestScriptCompilation`) before rerunning.
- `SaveModifiedSceneTask` fails PlayMode runs if the open scene is dirty
  or untitled — open a saved scene (e.g. Bootstrap) first.
- **A test run reports the count of the assembly it loaded, not the files
  on disk.** Writing a script and refreshing is not enough: if compilation
  is still pending (or failed), the runner silently executes the previous
  `Library/ScriptAssemblies/Escape.Tests.*.dll` and a stale pass looks
  green. After editing test or runtime code, confirm the assembly rebuilt
  (`stat -f %Sm Library/ScriptAssemblies/<name>.dll` vs the source mtime,
  or grep the dll for the new type) and grep the log for `error CS` before
  trusting a result. Compile errors do not surface through the runner.
- **`ci/run-tests.sh` invocation facts.** The platform flag is `-testPlatform`
  (`EditMode`/`PlayMode`); there is no `-testMode` flag, and passing one is
  silently ignored so the run defaults to EditMode and a "playMode" result
  file ends up full of EditMode tests. Never pass `-quit` alongside
  `-runTests`: the editor shuts down before the tests start and exits 0
  having run nothing. The assertions that catch all of this live in
  `ci/lib/nunit-assert.sh`; `ci/test-runner-guards.sh` proves they reject each
  false-green mode (run it after touching either). Keep those checks.
- **`Time.deltaTime` is ~1e-4s in headless batchmode** (the loop runs
  thousands of frames per second), so anything that integrates over frames —
  acceleration, a per-second detection ramp — moves ~nothing. Pin
  `Time.captureDeltaTime` (and reset it in `TearDown`) in any PlayMode test
  whose assertion depends on elapsed time. Note this is a *timing* trap, not
  an input one: measured directly, synthetic keyboard input works fine under
  `-nographics` (`PlayerInputReader.Move` reads `(0,1)` after `Press(wKey)`).
  Getting this backwards cost a diagnosis once.
- Movement rules should be tested through `IPlayerMoveInput`
  (`PlayerMovement.SetInputSource`), not only through key presses: the seam
  makes crouch/sprint/exhaustion exactly drivable without the Input System's
  device pump. Keep at least one real keyboard-driven test as well, so the
  end-to-end input chain stays covered.
