---
ticket: TWF-035
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
   - TWF-032
write_scope:
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/ARCHITECTURE.md
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-035-expand-timberborn-material-mapping.md
---

# TWF-035: Expand Timberborn Material Mapping

## Goal

Turn the current adapter-side cell mapping into a more useful fire-material model for terrain, vegetation, resources, buildings, water, and vertical occupancy.

## Why

The design says Timberborn converts map and entity state into packed cells while the GPU owns fire rules. Before adding broader gameplay consequences, the adapter needs better inputs: fuel, flammability, water, heat loss, and terrain should reflect real Timberborn material categories well enough for deltas to mean something.

## Requirements

- Keep `Wildfire.Core` host-agnostic.
- Keep Timberborn as an adapter that emits packed initial cells or `FireSimChange` records.
- Define deterministic material bands for at least terrain, vegetation/resource fuel, wood-like buildings, non-burnable buildings, and wet cells.
- Clarify how multi-cell and vertical building footprints map into packed cells.
- Preserve clamping to packed field widths.
- Add or update deterministic tests for priority, vertical mapping, water overlay behavior, and non-burnable material handling.
- Update architecture or test docs only where the contract changes.

## Dependencies

- `TWF-032` proves live non-zero deltas exist, so improved mapped inputs can be validated against real simulator behavior.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx` if adapter bindings change.

## Notes

- This ticket should not add fire-spread rules in C#.
- If a Timberborn API needed for material classification is missing, record the smallest follow-up research question instead of guessing broad reflection paths.
