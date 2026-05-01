---
ticket: TWF-001
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies: []
write_scope:
   - src/Wildfire.Core/**
   - tests/Wildfire.Core.Tests/**
---

# TWF-001: Harden CPU Rules And Frontier

## Goal

Bring the first CPU core implementation into closer alignment with the Wildfire design spec.

## Requirements

- Verify each cell is processed at most once per tick.
- Add tests for candidate dedupe and active-frontier persistence when stochastic ignition does not change a cell.
- Add tests for listener-registered changes applying on the next tick.
- Review external-change delta behavior and document or fix duplicate-delta cases.
- Keep `Wildfire.Core` independent from CLI, Unity, and Timberborn.

## Dependencies

- Initial scaffold exists.

## Role

- Worker using [../roles/worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run test`.

## Notes

- The current handoff notes that delta consolidation can be improved when external changes and rules touch the same cell in one tick.
- Worker evidence: added CPU core tests covering shared-neighbor candidate dedupe, active-frontier carryover after a stochastic no-change ignition attempt, listener-registered changes applying on the following tick, and one net delta per cell when external changes and rules touch the same cell in one tick.
- Worker decision: fixed duplicate-delta behavior in `CpuFireSimulator` by consolidating same-tick deltas per cell to the original old cell and final new cell; net-zero same-tick changes are removed from the tick result.
