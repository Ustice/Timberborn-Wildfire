---
ticket: TWF-139
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-137
  - TWF-138
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-139-implement-live-world-cell-importer.md
---

# TWF-139: Implement Live World Cell Importer

## Goal

Replace terrain-only live initialization with a Timberborn world importer that feeds real map materials into packed cells and companion fields.

## Requirements

- Import terrain cells from `ITerrainService` as today.
- Add live source providers for trees, crops, buildings, storage, infrastructure, water, and badwater where safe Timberborn APIs exist.
- Map every imported object through the `TWF-137` schema.
- Populate companion target identity and burn capacity for objects that can receive consequences.
- Log imported source counts by material class and skipped safe-unavailable API counts.
- Preserve host boundaries: Timberborn observes world state and registers sources; fire rules stay in shader/core contracts.
- Fail closed for unresolved resources or unsafe APIs.

## Dependencies

- `TWF-137` defines classification.
- `TWF-138` defines companion field storage.

## Role

- Worker.
- Follow [../roles/worker.md](../roles/worker.md).

## Implementation Notes

- Start by renaming or replacing `TimberbornTerrainCellSourceProvider` with a provider that composes smaller source providers.
- Keep each source provider small: one material family per class.
- Do not mutate Timberborn world objects in this ticket.
- Do not use fixed QA cells to fill missing live data.
- Add tests with fake source providers before attempting live QA.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Live QA must capture `Player.log` and `status` or `qa-readiness` showing nonzero imported counts for at least terrain plus one burnable object class, or precise safe-unavailable counters.

## Notes

- This is the main replacement for the current scaffolding. Without it, fuel tuning is still tuning an artificial cell, not a Timberborn map.
