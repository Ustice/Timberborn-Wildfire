---
ticket: TWF-008
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-004
   - TWF-007
write_scope:
   - src/Wildfire.Timberborn/**
   - docs/TEST_PLAN.md
---

# TWF-008: Dispatch GPU Sim From Timberborn

## Goal

Wire Timberborn to dispatch the GPU simulator on a fixed cadence. The game should register changes, tick the simulator, and receive compact deltas without directly mutating simulation state.

## Why

This is the first real live-game loop. It proves that Timberborn can act as a host adapter while the GPU simulator remains the authority for fire spread.

## Requirements

- Add a fixed-cadence update system or equivalent Timberborn integration point.
- Initialize the GPU simulator with packed cells from the adapters.
- Upload registered external changes before dispatch.
- Call `IGpuFireSimulator.Tick()` at the configured cadence.
- Keep the cadence configurable or centralized.
- Prevent multiple dispatches for the same intended game update.
- Log enough lifecycle events to diagnose startup, dispatch, and readback.
- Do not add a second C# fire-spread path.

## Dependencies

- `TWF-004` compact delta readback.
- `TWF-007` Timberborn cell adapters.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Run live Timberborn validation and capture logs when available.

## Notes

- Keep this ticket focused on dispatch and lifecycle. Gameplay consequences belong in `TWF-009`.
- Parked during coordination on 2026-05-01 after a worker produced useful scaffolding on branch `codex/TWF-008-fixed-cadence` at commit `732779729afb3f77904b93c2132e12c5d2ef587c`.
- Reason for parking: live validation is not meaningful until `TWF-012` provides an in-game command/test harness and `TWF-016` provides a repeatable Timberborn mod deploy path.
- Smallest unblock: finish `TWF-012` enough to query placeholder runtime status from inside the game, then finish `TWF-016` enough to load the adapter in Timberborn.
