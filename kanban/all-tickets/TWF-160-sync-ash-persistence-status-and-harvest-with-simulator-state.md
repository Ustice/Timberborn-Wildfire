---
ticket: TWF-160
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-157
  - TWF-158
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/HANDOFF.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-078-add-persistent-ash-field-service.md
  - kanban/all-tickets/TWF-082-add-fertile-ash-collection-and-application.md
  - kanban/all-tickets/TWF-160-sync-ash-persistence-status-and-harvest-with-simulator-state.md
---

# TWF-160: Sync Ash Persistence Status And Harvest With Simulator State

## Goal

Update Timberborn persistence, status, collection, and fertility consumers so they report and mutate simulator-owned ash instead of a separate ash field authority.

## Why

`TimberbornAshFieldService` was useful for the first gameplay proof, but the settled ash model says it must not remain a competing source of truth once simulator ash is authoritative.

## Requirements

- Persist simulator ash amount and contamination state across save/load.
- Report status and `qa-readiness` from simulator ash deltas or readback.
- Map uncontaminated harvested ash to `FertileAsh` at `1` good per `1` ash unit.
- Deplete simulator ash only after a successful inventory mutation.
- Leave contaminated ash unharvestable unless a future explicit toxic-ash good exists.
- Route fertile-ash application through queued simulator ash mutations.
- Preserve crop and forestry fertilize designations across save/load.
- Keep growth benefits bounded and based on uncontaminated simulator ash.
- Add deterministic tests for save/load, status counters, harvest depletion, failed inventory mutation, and fertile-ash application.
- Capture live QA for harvest, storage, application, contaminated blocking, and save/reload.

## Dependencies

- `TWF-157` makes simulator ash authoritative.
- `TWF-158` provides the queued mutation path needed by collection and application.

## Role

- Worker.
- Follow [../roles/worker.md](../roles/worker.md).

## Implementation Notes

- The existing `TimberbornAshFieldService` can be reduced, renamed, or split, but the final behavior must be adapter/read-model oriented rather than a second ash authority.
- Preserve the user-accepted warehouse QA route for `TWF-082`: load `Fuel (1)`, create a warehouse, finish construction instantly, set it to store `FertileAsh`, save, and then prove collection/application from that surface.
- Keep the previous overflow-safe inventory scan repair if present.

## Verification

- Run `git diff --check`.
- Run `dotnet test Wildfire.slnx --no-restore`.
- Run `bun run typecheck`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live harvest, storage, application, contaminated blocking, and save/reload evidence or the exact Timberborn API blocker.

## Notes

- This ticket supersedes the old split-authority wording in `TWF-078` and `TWF-082`, but it should not erase the evidence those tickets gathered.
- 2026-05-20 reconciliation: Jason confirmed this has been tested. Current implementation persists simulator atmospheric ash state, syncs the Timberborn ash read model from `AtmosphericFields`, reports ash and fertile collection counters through status/QA readiness, maps clean harvested ash to `FertileAsh`, and routes collection/application depletion back to simulator ash changes. Moved to `06-done`.
