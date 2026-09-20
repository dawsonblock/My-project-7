# Escape the Elites — The Broadcast

An investigative stealth game. Infiltrate a private estate, assemble a case out of
the evidence you find, and broadcast it before security reaches you.

Built in Unity 6000.6.0f1 with URP. Currently **stabilized**: gameplay,
progression, persistence and the qualification pipeline are all verified by a
single bound evidence run, not by hand-written summaries.

![Unity](https://img.shields.io/badge/Unity-6000.6.0f1-black?logo=unity&logoColor=white)
![URP](https://img.shields.io/badge/URP-17.6.0-blue)
![Tests](https://img.shields.io/badge/tests-118%20passing-brightgreen)
![Platform](https://img.shields.io/badge/player-StandaloneOSX-lightgrey)
![Status](https://img.shields.io/badge/status-stabilized-blue)

---

## Contents

- [Gameplay](#gameplay)
- [Controls](#controls)
- [Architecture](#architecture)
- [Getting started](#getting-started)
- [Build, test, qualify](#build-test-qualify)
- [Repository layout](#repository-layout)
- [Content pipeline](#content-pipeline)
- [Qualification evidence](#qualification-evidence)
- [Package discipline](#package-discipline)
- [Third-party content](#third-party-content)
- [Notes for contributors and agents](#notes-for-contributors-and-agents)
- [License](#license)

---

## Gameplay

Six gameplay scenes form one continuous infiltration, plus a bootstrap scene and
a main menu.

| | |
|---|---|
| Scenes | `Dock` → `ServiceEntrance` → `MansionOffice` → `SecurityWing` → `BunkerServerRoom` → `BroadcastTower` |
| Objectives | 12 (one optional) |
| Evidence | 12 pieces, cross-referenced by 5 insights |
| Documents | 9 readable, some granting evidence |
| Terminals | 4 hackable, code-gated |
| Endings | 4 — fallback, partial, best, and one secret |

**The loop.** Explore, observe the security layout, avoid detection, collect
evidence, corroborate it, unlock routes, manipulate terminals, broadcast.

**Stealth.** Cameras sweep with range and FOV; guards have vision cones and
hearing; lighting, crouching, surface noise and the flashlight all move your
visibility. Hiding is not immunity — a guard on full alert can still find you.

**Progression.** Objectives activate when their prerequisites complete, and
complete only when their *full* requirements hold — the prerequisite objectives
**and** the evidence the objective declares. An action-driven objective (routing
the relay, transmitting) is completable only by the domain action that owns it, so
the requirement is enforced by the authority rather than by whichever terminal
button offered it.

**The broadcast.** Pull the server archive, route the relay from the office
security node, then transmit from the tower. Routing needs the broadcast key;
transmitting needs a routed relay. The chain is enforced in the domain, and
`SaveValidator` refuses a save whose broadcast flags claim a state the objectives
do not support.

## Controls

Keyboard and mouse, or gamepad. Both are live simultaneously; the game tracks
which device is actuating look and switches sensitivity accordingly.

| Action | Keyboard / Mouse | Gamepad |
|---|---|---|
| Move | `W` `A` `S` `D` | Left stick |
| Look | Mouse | Right stick |
| Sprint | `Left Shift` | Left stick click |
| Crouch | `C` | East button |
| Interact | `E` | South button |
| Lean left / right | `Q` / `R` (or mouse back/forward) | — |
| Flashlight | `F` | D-pad up |
| Evidence board | `Tab` | North button |
| Throw lure | `G` | Right trigger |
| Whistle | `T` | West button |
| Focus (aim) | Right mouse | Left trigger |
| Pause / back | `Esc` | Start / select |

Accessibility: mouse and controller sensitivity, FOV, invert Y, head bob, reduce
motion, toggle sprint/crouch/flashlight, Large UI and master volume. Settings that
nothing reads yet (subtitles, high contrast, per-bus volumes) are deliberately
**not** exposed rather than shown as placeholders.

## Architecture

The project is a small set of assemblies with one composition root, so gameplay
code never reaches for singletons.

```
GameRoot (Bootstrap scene, the only singleton)
  └─ GameServices registry ──┬─ ISceneService    scene transitions + readiness
                             ├─ IObjectiveService activation vs completion gates
                             ├─ ISaveService      v3 enveloped JSON, atomic writes
                             ├─ IWorldService     doors, cameras, broadcast, alert
                             ├─ IDetectionService aggregated 0–100 meter
                             ├─ IInputGate        Player/UI map ownership
                             └─ …
```

- **Commands mutate, events announce.** Input and UI dispatch `IGameCommand`s
  through a dispatcher; services mutate `GameState` and publish on a
  `GameEventBus`. UI never writes state directly.
- **Persistence is enveloped v3.** Writes go `tmp → read-back verify → atomic
  replace → .bak`, loads recover primary→backup, and `SaveMigrator` upgrades older
  versions. Slot ids are validated because they become file names.
- **Scenes commit on a handshake.** `SceneBootstrap` publishes `SceneReady` once a
  scene has finished placing the player and writing its arrival checkpoint;
  `SceneService` waits for it before publishing `SceneChanged`. A scene that never
  becomes ready rolls back to the last scene that did, and reports fatal if there
  is none — it never fades a half-initialized scene back in.
- **Content is data.** Objectives, evidence, terminals, documents, insights and
  endings are authored as JSON and compiled into ScriptableObjects.

| Assembly | Role |
|---|---|
| `Escape.Core` | services, commands, events, state, persistence, scene flow |
| `Escape.Data` | definition types, tuning, scene ids |
| `Escape.Gameplay` | player, interaction, doors, stealth, world objects |
| `Escape.AI` | guard brain, vision, hearing, patrol |
| `Escape.UI` | all screens, built programmatically |
| `Escape.Editor` | generation pipeline and CI entry points |
| `Escape.Tests.*` | EditMode and PlayMode suites |

## Getting started

**Requirements:** Unity **6000.6.0f1** (via Unity Hub), ~16 GB RAM, and an
activated license. macOS is the shipping target; the project also opens on Windows
and Linux.

```bash
git clone https://github.com/dawsonblock/My-project-7.git
```

Then:

1. Open the project in Unity 6000.6.0f1.
2. Run **Tools → Escape the Elites → Build All**. This regenerates layers, imports
   content, and rebuilds prefabs, scenes, materials and the volume profile. The
   repository ships the generated result, so this is only needed after editing
   content.
3. Open `Assets/_Game/Scenes/Bootstrap.unity` and press **Play**.

> **Never run Build All while the editor is in play mode.** `SceneFactory` opens
> scenes with `EditorSceneManager.NewScene`, which throws in play mode — and a run
> that gets that far has already recreated prefabs with fresh file ids, so the next
> successful run renumbers every scene that references them. `BuildAll` guards its
> entry points; if you ever see a mass file-id diff, revert the prefabs and scenes
> together and regenerate once from a clean tree.

## Build, test, qualify

All scripts are batchmode, need the editor **closed**, and are invoked through
`bash` so they work on a fresh clone regardless of file modes.

```bash
./ci/run-tests.sh all          # EditMode 84 + PlayMode 34, writes NUnit XML
./ci/verify-generation.sh      # BuildAll twice → generated files byte-identical
./ci/build-player.sh           # StandaloneOSX player
./ci/smoke-player.sh           # launches the built player, gates on SMOKE PASS
./ci/qualify.sh                # the whole thing, into one evidence directory
./ci/test-runner-guards.sh     # proves the result assertions can't false-green
```

**`ci/qualify.sh`** is the one that matters. It runs six stages — source identity,
generated-content sync, deterministic generation, both test suites, player build,
built-player smoke — and writes a single `manifest.json` binding every claim to the
exact revision that produced it. **Any failed stage means no qualified manifest.**
It refuses a dirty tree unless given `--allow-dirty`, which records
`"qualified": false` rather than quietly qualifying an uncommitted revision.

CI (`.github/workflows/unity-tests.yml`) runs the test suites, the determinism gate,
and a macOS standalone player build followed by the built-player smoke test.
It triggers on pushes to `main` and `stabilization/*`, and on pull requests to
`main`.

**The smoke test is real qualification.** `GameSmokeTest` is inert in normal builds
and activates only under `-gameSmokeTest`. It loads all six gameplay scenes through
the readiness handshake, drives the progression chain through the real domain
commands, routes and transmits the broadcast, verifies an ending, then saves,
reloads and checks the broadcast state survived — exiting non-zero on any failure.

## Repository layout

```
Assets/_Game/
  Core/         services, commands, events, state, persistence, bootstrap
  Data/         definition types, tuning, scene ids
  Gameplay/     player, interaction, doors, stealth, world objects
  AI/           guard brain, vision, hearing, patrol
  UI/           every screen, built programmatically
  Editor/       generation pipeline + CI entry points
  Tests/        EditMode and PlayMode suites
  MigrationReference/   authored content JSON + schema + behavioural reference
  Resources/Definitions/  GENERATED ScriptableObjects — do not hand-edit
ci/             test, build, smoke, qualify, guards
BuildEvidence/  qualification runs and the package audit
AGENTS.md       project notes for contributors and coding agents
```

## Content pipeline

`Assets/_Game/MigrationReference/*.json` is the **source of truth** for game
content, and `Resources/Definitions/*.asset` is generated from it by
`WebDataImporter`, which `BuildAll` runs.

> **Edit the JSON, never the `.asset`.** A hand edit to a generated asset survives
> until the next `BuildAll` and then silently reverts — and the determinism gate
> still passes, because both of its runs agree on the JSON-derived result.
> `Tools → Escape the Elites → Verify Generated Content Matches Source` fails the
> build if a tracked definition has drifted from its source, and `ci/qualify.sh`
> runs it as a stage.

Scenes are YAML and are canonicalized by `SceneYamlNormalizer` at the end of
generation, so regeneration is byte-stable.

## Qualification evidence

`BuildEvidence/` holds the record. Each `qualification-<utc>/` directory is a
complete, self-describing run:

```
manifest.json               every claim, bound to a commit and a source tree hash
source.txt / source.sha256  the exact revision that was built
TestResults-{EditMode,PlayMode}.xml   raw NUnit output
editmode.log / playmode.log           raw runner logs
player-build.log            build result, with byte count
player.sha256               the artifact this evidence describes
smoke.log                   raw log from the built player
stages.txt                  pass/fail per stage
```

Evidence is **generated, not curated** — no one edits a manifest to make it say
`qualified: true`. A player artifact hash identifies one build; Unity player builds
embed timestamps, so it is not a reproducibility claim. Scene and prefab generation
*is* byte-reproducible and is gated.

## Package discipline

11 direct dependencies, kept deliberately minimal. The template's feature packs
(`characters-animation`, `gameplay-storytelling`, `worldbuilding`) and unused
runtime packages (Behavior, Physics, Pipeline, Services, Timeline) were removed
once the gates were green, in two verified steps:

| | Before | After |
|---|---|---|
| Player size | 112M | 99M |
| Managed assemblies | 221 | 151 |
| Direct packages | 20 | 11 |

The current StandaloneOSX artifact is 103,956,107 bytes (99 MiB); sizes above are
`du -sh` figures measured on the same machine, since the pre-reduction build was not
retained as evidence.

Two editor-only packages are kept on purpose: `asset-manager-for-unity` (the
committed `uam/` tracking folder depends on it) and `ai.assistant` (it provides the
Unity MCP bridge). Neither ships in the player, so removing them would cost a
workflow for no size win. `BuildEvidence/package-audit.txt` records the reasoning;
do not add a dependency without checking it and re-running `ci/qualify.sh`.

## Third-party content

All third-party assets are CC0. Attribution is not required; provenance is recorded
in [`Assets/_Game/THIRD_PARTY_NOTICES.md`](Assets/_Game/THIRD_PARTY_NOTICES.md).

- **Textures** — ambientCG (concrete, corrugated steel, wood floor, paving stones, carpet)
- **Audio** — Kenney (impact, interface and UI sound sets)
- Procedurally synthesized audio under `Audio/Generated/` is project code, not third-party.

## Notes for contributors and agents

Read [`AGENTS.md`](AGENTS.md) first. It documents the generation pipeline's
invariants (never renumber prefabs; stripped-object file ids; never generate in play
mode), the editor and testing traps that have actually cost time on this project
(`-testPlatform` not `-testMode`; never `-quit` with `-runTests`; `Time.deltaTime`
is ~1e-4 s in headless batchmode; a test run reports the assembly it *loaded*, so a
stale build can look green), and the architecture map.

Two rules worth stating up front:

1. **Green means green.** The gate fails closed on a missing, empty, mislabelled or
   stale results file, and on recorded failures. Do not waive a red test by
   explaining it — fix it or make the environment deterministic.
2. **Evidence is generated.** If a claim matters, `ci/qualify.sh` should produce it.

## License

No license file is present in this repository, so all rights are reserved by
default and the code is not licensed for reuse or redistribution. Add a `LICENSE`
before distributing the game or accepting contributions.
