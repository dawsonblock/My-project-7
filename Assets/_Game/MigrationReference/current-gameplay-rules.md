# Escape the Elites — Behavioral Reference (frozen web build)

The browser build is the behavioral specification. The Unity port must
reproduce these rules; where the web build had bugs, the corrected rule is
documented.

## Core loop
Explore → observe security → avoid detection → collect evidence →
corroborate → unlock routes → manipulate terminals → broadcast.

## Evidence
- Evidence ids are stable string ids. Collecting unknown ids must fail
  safely (log + ignore), never corrupt state.
- Collected evidence never respawns on scene revisit or reload.

## Objectives
- Objectives activate when their required objectives are complete.
- Objectives complete when their *full* requirements hold: the required
  objectives and the required evidence the objective declares. Activation is
  deliberately weaker than completion — evidence finishes an objective, it
  does not reveal it.
- Evidence-driven objectives auto-complete when all required evidence is
  held. Action-driven objectives complete via commands only.
- An action-driven objective's declared requirements are also its action's
  authorization: the domain action that owns it asks the objective service
  whether it may complete before mutating state. A requirement that is only
  enforced by the UI surface that offers the button is not enforced.

## Doors
- Door requirements: none | evidence (keycard) | objective | insight.
- Unlocked doors stay unlocked forever (persisted in saves).

## Terminals
- `service_terminal` (SECURITY NODE S-01): no code; requires the player to
  hold `keycard_staff` for door/camera commands.
- `office_terminal` (SECURITY NODE S-04): unlock code `7391`, found on the
  maintenance sticky note (`doc_archive_code`).
- Terminal commands dispatch game commands; UI never mutates state.

## Archive bug (fixed — do not reintroduce)
The original web build granted `server_archive_001` unconditionally. The
corrected rule: ARCHIVE requires the `private_security_coverup` insight
(public statement + access log collected).

## Broadcast
- BROADCAST on the office terminal requires `broadcast_key_001` and routes
  the signal. The relay console then transmits.
- `route_broadcast` declares `broadcast_key_001` as required evidence, so the
  key is enforced by the routing domain, not only by the terminal that offers
  the button. A caller that skips the terminal still cannot route without it.
- Ending evaluation is deterministic: highest-priority ending whose
  requirements are met wins; `ending_bad` is the fallback.

## Stealth
- Cameras: range + FOV + line-of-sight (raycast). Walls block detection.
- Detection decays fast while hidden, moderate out of sight, slow in
  lockdown.
- Hiding is not immunity: guards on full alert can still find the player.
- Being caught reloads the last checkpoint (autosave).
