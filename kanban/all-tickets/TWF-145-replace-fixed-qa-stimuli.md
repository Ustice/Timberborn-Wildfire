---
ticket: TWF-145
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-141
  - TWF-143
write_scope:
  - src/Wildfire.Timberborn/**
  - scripts/**
  - tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-145-replace-fixed-qa-stimuli.md
---

# TWF-145: Replace Fixed QA Stimuli

## Goal

Replace fixed center-cell QA stimuli with commands that ignite or inspect real imported field targets.

## Requirements

- Replace fixed target selection for fire, burn duration, water suppression, and building burnout where real imported targets exist.
- Add allowlisted target selectors such as selected tree, nearest imported tree, nearest storage, nearest water cell, and generated-scenario checkpoint.
- Keep commands bounded and reject arbitrary coordinates or packed-cell values.
- Report target material class, companion target identity, packed cell, preset, queued tick, and expected proof fields.
- Remove or demote old fixed-cell paths once equivalent real-field commands pass tests.

## Dependencies

- `TWF-141` proves importer parity.
- `TWF-143` provides preset selection.

## Role

- Worker.
- Follow [../roles/worker.md](../roles/worker.md).

## Implementation Notes

- This is not a new gameplay system; it is QA control over real gameplay data.
- Keep command names explicit so old evidence cannot be mistaken for new field-based evidence.
- Do not allow arbitrary packed-cell injection.

## Verification

- Run `git diff --check`.
- Run `bun run typecheck` if scripts change.
- Run `bun test` if scripts change.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.

## Notes

- The goal is to stop proving that one synthetic cell can burn and start proving that real imported Timberborn targets burn.
- 2026-05-05: Replaced fixed center-cell delta, water, and burn-duration stimuli with imported field target selection. Delta and burn-duration now queue `SetHeat=15`; water suppression queues `SetWater=3` only on imported burnable targets below that water level. Building burnout keeps the real pausable-building target provider and now queues `SetHeat=15` plus `SetFuel=0` instead of synthetic packed cells.
- 2026-05-05: Expanded command/result telemetry to report target selector, target material, companion target id, initial packed cell, queued field changes, and selected burn-duration fuel bands. The Bun command helper now allowlists `qa-delta-stimulus [selector]`, `qa-water-suppression-stimulus [selector]`, `qa-building-burnout-stimulus`, `qa-burn-duration-stimulus <target>`, and `qa-fire-preset <preset>` directly.
- 2026-05-05 verification passed: `git diff --check`, `bun run typecheck`, `bun test`, focused `dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj --filter FullyQualifiedName~TimberbornQaCommandBridgeTests`, `dotnet test Wildfire.slnx`, `dotnet build Wildfire.slnx`, and `bun run kanban:audit`. The audit still reports the pre-existing sibling-worktree kanban edits under `wildfire-TWF-066-*`.
