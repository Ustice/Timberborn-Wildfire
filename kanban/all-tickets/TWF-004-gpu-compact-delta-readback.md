---
ticket: TWF-004
agent_level: High
role: worker
requires_qa: false
doc_only: false
dependencies:
   - TWF-002
   - TWF-003
write_scope:
   - src/Wildfire.Core/**
   - src/Wildfire.Unity/**
   - tests/**
   - docs/TEST_PLAN.md
---

# TWF-004: Read Back Compact Deltas

## Goal

Emit compact delta records from the GPU simulation and return them through `GpuFireStepResult`. The C# side should receive only changed cells, not the whole grid.

## Why

Timberborn gameplay consequences should be driven by a small changed-cell stream. Reading the whole grid every tick would make the GPU path less useful and would blur the boundary between simulation storage and gameplay events.

## Requirements

- Add a GPU delta record layout matching `CellDelta`.
- Append a delta when a cell's packed value changes.
- Read back delta count and delta records after dispatch.
- Return deltas from `IGpuFireSimulator.Tick()`.
- Notify subscribed listeners after readback.
- Keep full cell buffers on the GPU; read back compact gameplay deltas only.
- Add tests for listener notification and wrapper behavior where possible.
- Update `docs/TEST_PLAN.md` with delta evidence expectations.

## Dependencies

- `TWF-002` full-grid shader baseline.
- `TWF-003` external change upload.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run test`.
- Run `dotnet build Wildfire.slnx`.

## Notes

- If GPU append-buffer readback is awkward in the current environment, isolate the readback API behind a small wrapper so later Unity validation can focus on one surface.
