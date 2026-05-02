---
ticket: TWF-038
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-032
   - TWF-035
write_scope:
   - src/Wildfire.Timberborn/**
   - scripts/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-038-register-water-suppression-changes.md
---

# TWF-038: Register Water Suppression Changes

## Goal

Add a narrow Timberborn adapter path that registers water or wetness changes with the GPU simulator and proves suppression changes affect later ticks.

## Why

The design includes water as a packed field and shows water dumped onto a target cell as an external change. Fire should not only spread; Timberborn needs a safe way to influence it through queued changes that the simulator applies on the next tick.

## Requirements

- Register suppression through `IGpuFireSimulator.RegisterChange`.
- Keep the command or adapter surface narrow and allowlisted.
- Reject arbitrary destructive commands and broad coordinate mutation.
- Preserve tick-boundary behavior: changes apply on the next dispatch tick.
- Expose command/status detail for accepted target, queued changes, tick advancement, and resulting deltas.
- Add deterministic tests for command validation, change registration, and next-tick behavior where possible.
- Document prerequisites and live evidence in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-032` proves live non-zero GPU deltas.
- `TWF-035` improves water and material mapping.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run typecheck` if TypeScript scripts change.
- Run `dotnet test`.
- QA must use a loaded, unpaused save and capture command output, copied `Player.log`, and status evidence showing queued suppression work and later dispatch.

## Notes

- This ticket should prove the interaction contract, not implement broad firefighting gameplay.
