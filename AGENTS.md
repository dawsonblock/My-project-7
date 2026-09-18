# Escape the Elites — agent notes

Unity 6000.6.0f1, URP 17.6.0, Input System 1.19.0, AI Navigation 2.0.12.

## Build & verify

- Regenerate everything: `Tools → Escape the Elites → Build All`
  (layers → content import → prefabs → scenes → audio → materials)
- Tests: `ci/run-tests.sh [editmode|playmode|all]` (batchmode, writes
  `TestResults-<mode>.xml`), or Test Runner window
- Determinism gate: `ci/verify-generation.sh`, or menu
  `Tools → Escape the Elites → Verify Deterministic Generation`
  (BuildAll twice → all generated files must be byte-identical)

## Generated-content invariants — do not break

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

- `GameRoot` (Bootstrap scene) → `GameServices` registry → services
  (`ISceneService`, `ISaveService`, `ISaveCoordinator`, `IInputGate`, …)
- Commands mutate `GameState`; `GameEventBus` publishes. Doors/cameras/
  terminals persist via `GameState` lists + `IWorldObject.RestoreFromState`.
- Saves: enveloped v2 JSON, atomic `tmp → verify → replace → .bak`, recovery
  order primary→bak, `SaveMigrator` for old versions, `ISaveParticipant`
  captures volatile state (player pose incl. camera pitch, flashlight).
- Input: `PlayerInputActions` — Player/UI maps toggle with `IInputGate.UiOpen`;
  **System map (Pause/Cancel) stays enabled always**. Modal cancel routes
  through `InputGate` → `ICancelableUi`. UI components must keep their host
  active and lazy-bind `PlayerInputReader` (player may spawn later).

## Editor/testing gotchas

- `InputSystem.settings.editorInputBehaviorInPlayMode =
  PointersAndKeyboardsRespectGameViewFocus` — synthetic keyboard input is
  suppressed when the Game view is unfocused. Use `InputTestFixture`
  PlayMode tests, not `QueueStateEvent`.
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
