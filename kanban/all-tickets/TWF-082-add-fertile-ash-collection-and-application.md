---
ticket: TWF-082
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-157
  - TWF-158
  - TWF-160
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/DESIGN.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-082-add-fertile-ash-collection-and-application.md
---

# TWF-082: Add Fertile Ash Collection And Application

## Goal

Let players collect uncontaminated simulator ash as the `FertileAsh` good and apply that good back to crop or forestry areas, without creating any ash source of truth outside the simulator transport field.

## Why

Controlled burns should produce a useful farming resource, but the resource must be the ash already represented in the field. The current broken behavior came from treating the visible/rendered ash, Gatherer Flag UI, and adapter ash entries as separate realities. This ticket should close the loop from simulator ash to beaver collection to storage to application while preserving simulator authority.

## Requirements

- Read collectable ash from simulator-owned ash transport state, currently named `AtmosphericFields` in code and described as `TransportFields` in `docs/ash-simulation-model.md`.
- Do not introduce or keep `FertileAshDeposit` natural-resource templates, proxy entities, or any other duplicate collectable ash store.
- Treat any `TimberbornAshFieldService` entries as a derived read model only. They must not decide truth when simulator transport ash disagrees.
- Expose `FertileAsh` as a selectable Gatherer Flag priority with its own icon binding. The first icon may copy an existing ash/dirt asset, but it must be replaceable by `TWF-163`.
- Let Gatherer Flag workers find reachable cells with uncontaminated ash amount greater than zero from the simulator/readback field.
- Do not block berries, clear berries, or otherwise fake priority behavior. If no real ash target exists, native Gatherer Flag behavior should remain available.
- When a worker successfully collects `1 FertileAsh`, queue or apply exactly one bounded simulator ash removal for the harvested cell.
- Decrement ash only after the successful collection or inventory mutation. A failed target, failed path, full inventory, or unsafe API must not delete ash.
- Rendered ash must recede from the harvested location because rendering reads the same simulator ash field after the removal.
- Contaminated ash is not collectable as `FertileAsh`.
- Fertile ash application must consume `1 FertileAsh` good and queue or apply `1` uncontaminated simulator ash unit at the designated crop or forestry cell.
- Application must not consume goods on contaminated, invalid, unreachable, or unsafe target cells.
- Preserve native hauling and storage semantics. The end-to-end proof should use real storage or a precise safe-unavailable blocker, not adapter-side teleporting that bypasses Timberborn inventory behavior.
- Preserve save/reload behavior for ash amount, ash contamination, pending crop/forestry fertilize designations, and stored `FertileAsh` goods where Timberborn exposes safe APIs.
- Leave tainted-ash collection blocking and contaminated-cell proof to `TWF-166`; this ticket should close on the confirmed clean-ash collection/application route.
- Respect the settled day-scale decay rule from the ash model: uncontaminated ash loses `1` amount per in-game day, and contaminated ash loses `1` amount every two in-game days. Do not use per-tick decay for collection eligibility or QA timing.

## Dependencies

- `TWF-157` makes simulator transport ash authoritative.
- `TWF-158` provides the bounded queued ash mutation path.
- `TWF-160` synchronizes Timberborn persistence, status, harvest, and application consumers with simulator-owned ash state.
- `TWF-163` owns release-quality `FertileAsh` and fertilize-tool icon art. This ticket only needs replaceable icon bindings.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start from current `main`, not the stale `~/repos/wildfire-twf-082-fertile-ash-qa-fix` assumptions.
- The old repair branch is historical evidence only. Its `FertileAshDeposit` direction is explicitly rejected.
- Current code already has ash mutation fields on `FireSimChange` (`AddAsh`, `RemoveAsh`, `SetAsh`, and `SetAshContamination`). Prefer that path over adding another adapter-local ash authority.
- The selectable Gatherer Flag good and worker target planner can be Timberborn-adapter code, but the target source must be simulator ash readback/status and the collection effect must be a simulator ash mutation.
- The collection planner should surface telemetry for candidate cells, reachable cells, collected goods, depleted cells, tainted skips, full-inventory or unsafe-inventory skips, and no-target fallback.
- The application planner should surface telemetry for designations, consumed goods, applied simulator ash units, contaminated skips, missing-inventory skips, and unsafe-API skips.
- Keep `1 FertileAsh` equal to `1` uncontaminated ash amount. Do not revive the older `25` strength mapping.
- If the day-scale decay implementation is still outside this ticket after dependency reconciliation, stop and update the dependency chain before QA. Do not accept a live proof that only works because ash decays differently from the settled model.

## Verification

- Run `git diff --check`.
- Run `dotnet test Wildfire.slnx --no-restore`.
- Run `bun run typecheck`.
- Run `dotnet build Wildfire.slnx`.
- Deterministic tests must prove collectable-cell selection from simulator ash, no-delete-on-failure, successful harvest depletion by one unit, storage/inventory accounting where safely available, crop application, forestry application, and save/reload state. Contaminated ash exclusion is split to `TWF-166`.
- If compute shader or simulator ash mutation behavior changes, run the Unity shader harness documented in `docs/TEST_PLAN.md`.
- Live QA must use the user-accepted `Fuel` route: load `Fuel with beavers` or the current `Fuel` save, let ash appear after the first unpause tick, inspect Gatherer Flags during work hours, set `FertileAsh` as the Gatherer Flag priority, and prove a worker collects ash from a real ash cell.
- Passing live QA must include copied `Player.log`, a `status` or `qa-readiness` result with nonzero simulator ash before collection, nonzero `fertile_ash_collection_candidate_cells`, nonzero collected `FertileAsh`, at least one depleted ash cell, and rendered ash reduction at the harvested location.
- Passing live QA must prove warehouse/native-hauling behavior by storing `FertileAsh` in a real storage building, or record the exact Timberborn inventory API blocker.
- Passing live QA must prove crop or forestry application consumes stored `FertileAsh` and adds one uncontaminated simulator ash unit to the target cell, or record the exact safe-unavailable blocker.
- Passing live QA for contaminated ash skipping is split to `TWF-166`.
- Passing live QA must include save/reload evidence after ash exists and after at least one collection or application mutation.

## Notes

- 2026-05-19 rewrite: this ticket is now the player-facing end-to-end feature and QA gate for fertile ash. It no longer describes `FertileAshDeposit` or adapter-owned ash entries as implementation options.
- 2026-05-19 worker implementation on `main`: changed the Timberborn ash read model to use the simulator `0-3` ash amount scale, changed `1 FertileAsh` to equal `1` uncontaminated ash unit, added Timberborn `IDayNightCycle`-based decay, and queues simulator `RemoveAsh` mutations for both successful collection and day-scale decay. Clean ash decays by `1` per in-game day; tainted ash decays by `1` every two in-game days. Gatherer Flags now get a `FertileAshField` priority template only for UI/behavior dispatch, with a workplace decorator that releases to native gathering unless `FertileAsh` is selected and a real simulator ash target exists.
- `docs/ash-simulation-model.md` is the source of truth for ash ownership: ash lives in simulator transport state, renderers read it, and Timberborn adapters request bounded mutations.
- `TWF-160` owns the plumbing that makes status, persistence, harvest, and application read/write simulator-owned ash. If `TWF-160` absorbs implementation work from this ticket, keep `TWF-082` as the visible gameplay and live QA acceptance gate.
- The smallest useful unblock is no longer "make beavers stop gathering berries." The unblock is "make Gatherer Flags find real simulator ash targets and decrement that same field when collection succeeds."
- 2026-05-20 Jason confirmation: accepted the clean fertile-ash collection/application route for `TWF-082`. Tainted ash has not been tested and is split into `TWF-166`, so this ticket is no longer blocked by contaminated-ash skip evidence.
