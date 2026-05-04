---
ticket: TWF-003
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-001
  - TWF-002
write_scope:
  - src/Wildfire.Core/**
  - src/Wildfire.Unity/**
  - tests/**
---

# TWF-003: Upload External Changes

## Goal

Implement the path from `RegisterChange(FireSimChange change)` to a GPU-side change application pass. Registered changes should apply at the start of the next GPU tick.

## Why

Timberborn systems need a safe way to tell the simulator about heat, water, terrain, and fuel changes. Queuing changes until the next dispatch keeps mutation centralized and prevents recursive update chains.

## Requirements

- Store registered changes in the Unity GPU simulator wrapper until `Tick()`.
- Upload queued changes to a GPU buffer before the simulation pass.
- Add a shader pass or kernel that applies `SetCell`, additive fields, and individual setters.
- Ignore or safely report out-of-range cell indices.
- Clear the uploaded change queue after successful dispatch.
- Preserve the rule that listeners and hosts never mutate buffers directly.
- Add tests for queue behavior in the wrapper where possible.

## Dependencies

- `TWF-001` buffer/grid scaffold.
- `TWF-002` full-grid shader baseline.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.

## Notes

- Be careful with additive overflow. Match the packed-cell field limits instead of allowing wraparound.

## Worker Notes - 2026-05-01

- Implemented `RegisterChange` queueing in `UnityComputeFireSimulator`; queued changes are filtered for valid cell indices, uploaded to `wildfire.queued_changes`, applied through an `ApplyExternalChanges` dispatch, then followed by the existing `SimulateFullGrid` dispatch.
- Added `FireSimChangeUpload` encoding for `SetCell`, additive heat/fuel, and individual packed-field setters. Additive and setter byte values clamp to packed field limits instead of wrapping.
- Added `ApplyExternalChanges` to `FireSim.compute`. The pass mutates `CurrentCells` only inside the simulator dispatch path, skips out-of-range GPU-side indices defensively, and preserves host/listener non-mutation of simulation buffers.
- Added wrapper tests for deferred upload, apply-before-sim dispatch order, invalid-index filtering, upload-failure queue retention, and packed-field clamp behavior.
- Current blocker/unknown: the repository still has no Unity batchmode or shader snapshot harness, so this ticket proves the C# upload/dispatch contract with .NET tests but does not compile or execute `FireSim.compute` on a GPU. Delta readback also remains outside this ticket.

## Tech Lead Fix Notes - 2026-05-01

- Queue consumption policy: upload and apply-dispatch failure leave the queue untouched. Once `ApplyExternalChanges` returns successfully, the uploaded valid batch and invalid entries are consumed before `SimulateFullGrid`; the simulator tick is considered started at that point. If the full-grid dispatch then fails, applied changes are not replayed on the next tick.
- Oversized queue policy: each tick uploads the first `wildfire.queued_changes` capacity of valid queued changes in registration order and leaves later valid changes queued for future ticks. This keeps same-cell ordering deterministic across chunks.
- The current GPU apply kernel remains a documented single-thread baseline so repeated changes to the same cell apply in registration order. Follow-up should replace it with an ordered parallel apply pass after a shader execution harness exists.
- Added tests for capacity-sized chunking, apply-dispatch failure queue retention, and full-grid failure not replaying already-applied changes.

## Evidence - 2026-05-01

- `dotnet test` passed: 35 tests.
- `git diff --check` passed.
- `dotnet build Wildfire.slnx` passed with 0 warnings and 0 errors.

## Fix Evidence - 2026-05-01

- `dotnet test` passed: 38 tests.
- `git diff --check` passed.
- `dotnet build Wildfire.slnx` passed with 0 warnings and 0 errors.
- Integrated on `main` in commit `cfe8aec`.
- Coordinator verification after integration: `git diff --check`, `dotnet test` with 38 tests, and sequential `dotnet build Wildfire.slnx` all passed.
