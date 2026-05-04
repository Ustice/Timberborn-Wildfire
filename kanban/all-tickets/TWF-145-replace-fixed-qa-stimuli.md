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
