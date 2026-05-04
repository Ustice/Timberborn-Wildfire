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
- Worker update 2026-05-01:
  - Added a Timberborn fixed-cadence dispatch scaffold in `TimberbornFireSystem.cs`.
  - `TimberbornFireSystem.Initialize` maps adapter observations to initial packed cells and creates an `IGpuFireSimulator` through `ITimberbornFireSimulatorFactory`; existing simulator-injection constructors still work.
  - External changes flow only through `IGpuFireSimulator.RegisterChange`; mapped cell observations emit sorted `SetCell` changes and heat/external registration stays queued for the simulator tick.
  - `TimberbornFixedCadenceFireDispatcher` centralizes cadence with `TimberbornFireCadence`, accumulates elapsed game time, dispatches at most once per processed game update, and skips duplicate update IDs without adding elapsed time twice.
  - Added lifecycle log tokens for simulator attach/init, cadence configuration, change registration, mapped-change batches, waiting, duplicate skips, dispatch start/completion, readback delta counts, and dispatch failures.
  - Did not add a C# fire-spread path; Timberborn remains an adapter around `IGpuFireSimulator`.
  - Did not run live Timberborn validation because TWF-013 QA is using the live UI path and this repo still lacks a serialized deploy/mod-loading pipeline. Smallest unblock: add a build/deploy/launch entrypoint that wires a real GPU simulator factory and captures `Player.log` plus the mod fire log without interrupting UI-coordinate QA.
  - Evidence: `git diff --check` passed; `dotnet test` passed 57 tests; `dotnet build Wildfire.slnx` passed with 0 warnings and 0 errors.
- Worker rebase/update 2026-05-01:
  - Rebased `codex/TWF-008-fixed-cadence` onto current `main` and preserved current main's TWF-019 command bridge plus the useful fixed-cadence dispatch work from `732779729afb3f77904b93c2132e12c5d2ef587c`.
  - Added `TimberbornFireRuntime` as the Timberborn game-context update singleton and command state provider. It dispatches an attached `IGpuFireSimulator` through `TimberbornFixedCadenceFireDispatcher` using `Time.deltaTime`, catches/logs dispatch failures, and exposes width/height/depth, last tick, queued changes, and last delta count to `status`.
  - Updated `TimberbornQaCommandFileBridge` to read status from `TimberbornFireRuntime` instead of the static placeholder provider, and bound the runtime in `WildfireConfigurator`.
  - Preserved the adapter boundary: Timberborn only registers mapped changes and ticks `IGpuFireSimulator`; no Timberborn-owned fire-spread path was added.
  - Evidence: `git diff --check` passed.
  - Evidence: `dotnet test` passed 71 tests.
  - Evidence: `dotnet build Wildfire.slnx` passed with 0 warnings and 0 errors.
  - Evidence: `bun scripts/deploy-timberborn-mod.ts --dry-run` passed, built the solution, and found all required deploy artifacts present. Real deploy was not run because Timberborn process `35721` was already running with a loaded save; hot-copying the DLL would not reload this code and could disturb live QA.
  - Live command evidence: `bun scripts/invoke-timberborn-command.ts status --wait=6` succeeded against the loaded game and returned `wildfire_command_result command=status success=true status=success simulator_integrated=false width=placeholder height=placeholder depth=placeholder tick_count=placeholder queued_changes=placeholder last_delta_count=placeholder message=ok`.
  - Player.log tokens observed: `wildfire_command_bridge_ready`, `wildfire_command_request command=status`, and `wildfire_command_result command=status ... simulator_integrated=false ...`.
  - Remaining blocker: the loaded game is still running the previously deployed mod and no live Timberborn-side simulator factory/attachment point has initialized `TimberbornFireRuntime`, so live `status` still reports placeholders. Smallest next action is to close/restart Timberborn after a real deploy of this commit, then add or wire the Timberborn-side factory/adapter attachment that calls `TimberbornFireRuntime.Initialize` or `AttachSimulator` with the real GPU simulator.
- Worker follow-up 2026-05-01:
  - Per reviewer guidance, rejected and removed a temporary dispatch-only simulator path. It would have flipped `simulator_integrated=true` without proving GPU dispatch/readback and is not TWF-008 completion.
  - Real deploy evidence: `bun scripts/deploy-timberborn-mod.ts --apply --clean` passed after closing Timberborn, rebuilt `Wildfire.slnx`, cleaned `~/Documents/Timberborn/Mods/Wildfire`, and copied `Wildfire.Timberborn.dll`, `Wildfire.Core.dll`, and PDBs.
  - Live restart/load evidence: Timberborn relaunched, the mod list showed `Wildfire v0.1.0.0`, and Continue loaded `Wildfire testing - 2026-05-01 08h41m, Day 1-2.autosave`.
  - Live command evidence: `bun scripts/invoke-timberborn-command.ts status --wait=6` returned `wildfire_command_result command=status success=true status=success simulator_integrated=false width=placeholder height=placeholder depth=placeholder tick_count=placeholder queued_changes=placeholder last_delta_count=placeholder message=ok`.
  - Player.log tokens observed from the safe deployed build: `Wildfire (v0.1.0.0)`, `wildfire_timberborn_runtime_ready cadence_interval_ms=1000`, `wildfire_command_bridge_ready`, `wildfire_command_request command=status`, and `wildfire_command_result command=status ... simulator_integrated=false ...`.
  - Exact blocker: no deployable compute-backed `IGpuFireSimulator` factory exists in the Timberborn mod. The only current compute-backed simulator is `Wildfire.Unity.UnityComputeFireSimulator`, but `Wildfire.Unity` targets `net10.0`, is not copied by `scripts/deploy-timberborn-mod.ts`, and depends on `ComputeBufferGrid`, `IFireSimComputeDispatcher`, GPU buffer handles, and a Unity `ComputeShader` asset for `FireSim.compute`. The current `FireSim.compute` import path exists only in the Unity batchmode harness, not in the live Timberborn mod package.
  - Smallest follow-up ticket: add a deployable Timberborn GPU backend that packages/imports `FireSim.compute` for the live mod, provides a Timberborn/Unity `ComputeShader` dispatcher and buffer allocator, exposes an `ITimberbornFireSimulatorFactory`, and initializes `TimberbornFireRuntime` from real adapter-produced `TimberbornCellSource` inputs. Keep this out of `Wildfire.Core` and keep Timberborn as the adapter.
- Coordinator reconciliation 2026-05-01:
  - Moved to done after `TWF-021` and `TWF-023` supplied and proved the live compute-backed simulator path.
  - Accepted evidence includes `wildfire_timberborn_gpu_dispatch_kernel`, `wildfire_timberborn_gpu_readback_completed`, `wildfire_timberborn_dispatch_completed`, and `wildfire_command_result ... simulator_integrated=true`.
  - `delta_count=0` only proves dispatch/readback; gameplay consumers and visible effects remain owned by `TWF-009`.
