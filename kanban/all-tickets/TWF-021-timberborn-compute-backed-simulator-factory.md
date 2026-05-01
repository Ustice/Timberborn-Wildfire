---
ticket: TWF-021
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-006
   - TWF-008
   - TWF-018
write_scope:
   - src/Wildfire.Timberborn/**
   - src/Wildfire.Unity/**
   - Assets/**
   - scripts/deploy-timberborn-mod.ts
   - docs/TEST_PLAN.md
---

# TWF-021: Add Timberborn Compute-Backed Simulator Factory

## Goal

Provide a deployable Timberborn-side `ITimberbornFireSimulatorFactory` that creates a real compute-backed `IGpuFireSimulator`, attaches it to `TimberbornFireRuntime`, and lets live `status` report integrated simulator dimensions and ticks instead of placeholders.

## Why

`TWF-008` now has the fixed-cadence runtime and command-state surface, but live validation proved no real simulator is attached in the deployed Timberborn mod. The existing compute backend lives in `Wildfire.Unity` and the Unity batchmode harness; Timberborn needs a packaged live backend rather than a dispatch-only or C# no-op simulator.

## Requirements

- Package or import `FireSim.compute` for the live Timberborn mod.
- Provide a Timberborn/Unity compute dispatcher and buffer allocation path that can run inside the game.
- Expose an `ITimberbornFireSimulatorFactory` to `Wildfire.Timberborn`.
- Initialize `TimberbornFireRuntime` from real adapter-produced `TimberbornCellSource` inputs, not permanent empty sources.
- Keep `Wildfire.Core` host-agnostic and free of Timberborn or Unity references.
- Do not satisfy the ticket with a dispatch-only, no-op, or duplicate C# fire-spread simulator.
- Update `scripts/deploy-timberborn-mod.ts` only as needed to deploy the real backend and assets.
- Log enough lifecycle tokens to distinguish asset loading, factory creation, simulator attach, dispatch, and readback failures.

## Dependencies

- `TWF-006` shader snapshot harness proves `FireSim.compute` behavior outside Timberborn.
- `TWF-008` provides the fixed-cadence runtime and command-state surface but is blocked until this backend exists.
- `TWF-018` proves Unity batchmode can import and execute `FireSim.compute`.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Run any Unity shader execution harness command affected by backend or asset packaging changes.
- Run `bun scripts/deploy-timberborn-mod.ts --apply --clean`.
- Restart Timberborn, click through startup screens, load or continue the Wildfire save, then run `bun scripts/invoke-timberborn-command.ts status --wait=6`.
- Capture `Player.log` evidence for simulator factory creation, runtime attach, dispatch, readback, and a `wildfire_command_result` with `simulator_integrated=true`.

## Notes

- The TWF-008 rejected path was a `TimberbornDispatchOnlyGpuFireSimulator`; do not reintroduce that as completion.
- If `Wildfire.Unity` cannot be deployed because of target framework or asset-loading constraints, record the exact constraint and split the smallest packaging or asset-import ticket needed before touching gameplay consumers.
