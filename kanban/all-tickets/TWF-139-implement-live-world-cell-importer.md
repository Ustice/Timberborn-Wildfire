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
- 2026-05-04 worker pass replaced terrain-only initialization with `TimberbornWorldCellImporter`, a composable provider pipeline, terrain import through `ITerrainService`, explicit `safe_unavailable` providers for trees/crops/buildings/storage/infrastructure/water/badwater until safe live enumeration APIs are proven, companion field construction from `WildfireMaterialFieldSchema`, and import-count status tokens.
- The Timberborn compute simulator factory now accepts companion fields and uploads companion target IDs and packed companion state buffers beside packed cells. The shader still owns fire rules; Timberborn only observes world state and supplies fields.
- Deterministic coverage added fake-provider importer tests for provider composition, material counts, safe-unavailable counters, target IDs, and fail-closed unknown companion fields. Focused verification passed for importer, mapper, command status, and visual-field surface tests.
- A first live proof with terrain plus safe-unavailable providers loaded successfully and reported `world_import_total_sources=16604`, `world_import_terrain_sources=16604`, and `world_import_safe_unavailable=7` through `qa-readiness`. After that proof, the building provider was upgraded from safe-unavailable to a terrain-surface `PausableBuilding` probe so the importer can capture a real burnable object class without scanning the full grid.
- A follow-up live proof for the optimized building provider did not reach save load: the QA loader stayed on the Timberborn main menu after repeated `Continue` clicks, and the fresh `Player.log` contained no importer tokens. Treat this as a live harness/load caveat for QA follow-up rather than evidence against the importer.
- Verification after the final importer changes passed: `git diff --check`, `bun run typecheck`, `bun test`, `dotnet test Wildfire.slnx`, `dotnet build Wildfire.slnx`, and `bun run kanban:audit`. The kanban audit still reports the known sibling-worktree TWF-066 edits as its only critical findings.
