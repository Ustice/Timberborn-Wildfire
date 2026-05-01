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
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.

## Notes

- If GPU append-buffer readback is awkward in the current environment, isolate the readback API behind a small wrapper so later Unity validation can focus on one surface.
- `TWF-002` introduced shader-side `Deltas.Append(...)`; this ticket must ensure the real Unity binding allocates `wildfire.deltas` as an append buffer and resets its counter before each dispatch.

## Worker Notes

- Implemented a Unity-owned append-buffer abstraction for `wildfire.deltas`; `ComputeBufferGrid` now allocates deltas through `AllocateAppend`.
- `UnityComputeFireSimulator.Tick()` resets the append counter before dispatch, reads the compact delta counter and records after full-grid dispatch, returns `GpuFireStepResult.Deltas`, and notifies subscribed listeners from the readback result.
- Added `FireSimDeltaReadback` to decode the shader `CellDeltaGpu` layout into core `CellDelta` records without reading back full cell buffers or adding a C# fire-spread parity path.
- Added deterministic wrapper tests for append allocation, counter reset before dispatch, compact readback decoding, listener notification, unsubscribe behavior, and over-capacity counter rejection.
- Updated `docs/TEST_PLAN.md` with compact delta evidence expectations and the current compile/runtime limitation.
- Fixed tech-lead delta-contract rejection: `ApplyExternalChanges` now appends compact deltas from the pre-apply packed value to the post-apply packed value, so external changes remain observable even when the following full-grid simulation pass makes no further change.
- Added external-change-only tests covering returned `GpuFireStepResult.Deltas` and subscribed listener notification.

## Evidence

- `git diff --check`: passed.
- `dotnet test`: passed, 43 tests.
- `dotnet build Wildfire.slnx`: passed, 0 warnings, 0 errors.

## Blockers

- The repository still has no Unity runtime project, Unity batchmode harness, DirectX Shader Compiler path, or standalone compute-shader execution harness. This work verifies the C# wrapper/readback contract but cannot prove that `FireSim.compute` compiles or that Unity readback APIs populate the append counter and records at runtime.
