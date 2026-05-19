---
ticket: TWF-082
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-078
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/DESIGN.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-082-add-fertile-ash-collection-and-application.md
---

# TWF-082: Add Fertile Ash Collection And Application

## Goal

Allow beavers to collect fertile ash and place it in fields as a later gameplay loop.

## Why

The design treats automatic ash fertility as the first gameplay pass, but the longer-term goal is for controlled burns to create a resource beavers can collect and deliberately apply to fields. That turns aftermath into a player-managed farming strategy.

## Requirements

- Build on simulator-owned ash state once `TWF-157` and `TWF-158` land; until then, treat the current ash field service as a transitional adapter/read model.
- Define how fertile ash becomes collectable without duplicating or losing field state.
- Add a good, job, hauling, storage, building, or field-application path only through safe Timberborn APIs.
- Preserve ash contamination; contaminated ash should not be usable as fertilizer unless a later decontamination mechanic exists.
- Add deterministic tests for collection eligibility, resource accounting, application, and decay or depletion.
- Capture live QA evidence for collection and field application, or document the exact Timberborn API blocker.
- Update `docs/DESIGN.md` only if the accepted gameplay loop changes.

## Dependencies

- `TWF-078` provides persistent ash field state and fertile ash quality.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- 2026-05-17 coordinator/code reconciliation: implementation exists on `main` ahead of `origin/main`. Code surfaces include `TimberbornFertileAshCollectionService`, `TimberbornGathererPostFertileAshCollectionAdapter`, `TimberbornFertilizeDesignationService`, `TimberbornFertilizeCropsTool`, `TimberbornFertilizeTreesTool`, `TimberbornFertilizeToolButtons`, `FertileAsh` goods/collections/localization, and runtime hooks for consuming `FertileAsh` into player designations. Deterministic verification passed with `git diff --check origin/main..HEAD`, `bun run typecheck`, and `dotnet test Wildfire.slnx --no-restore` (`436` tests).
- Required live QA still needs to prove Gatherer Post collection, inventory mutation, designation application, tainted-cell blocking, save/reload of designations, and player-facing toolbar usability or capture exact API blockers.
- 2026-05-19 ash-model update: `1 FertileAsh` now maps to `1` uncontaminated simulator ash unit, not `25` service strength. `TWF-158` and `TWF-160` own routing collection/application through queued simulator ash mutations.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for collection and application or an explicit safe unavailable state.

## Notes

- Moved from `08-deferred` to `04-verify` because the collection/application loop is no longer merely future work. Required live QA still needs to prove Gatherer Post collection, inventory mutation, designation application, tainted-cell blocking, save/reload of designations, and player-facing toolbar usability or capture exact API blockers.
- Relevant design reference: `docs/DESIGN.md` section 20, "Ash And Fertility" and `docs/ash-simulation-model.md`.
