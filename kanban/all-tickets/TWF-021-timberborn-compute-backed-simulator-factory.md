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
- Worker update 2026-05-01:
   - Inspected `Wildfire.Unity` compute simulator, `FireSim.compute`, Unity batchmode harness, `TimberbornFireRuntime`, deploy script, installed Timberborn `MapSize`/`ITerrainService` APIs, and the TWF-008 rejection notes.
   - Viable path found: keep `Wildfire.Unity` as the net10 harness/project, package the shared `FireSim.compute` into a Unity-built macOS AssetBundle, and implement a Timberborn-side Unity `ComputeShader`/`ComputeBuffer` adapter around the same shader contract.
   - Added a real `TimberbornComputeFireSimulatorFactory` path in progress. It loads `ComputeShaders/wildfire_compute_mac`, creates a real compute-backed `IGpuFireSimulator`, allocates Unity buffers, dispatches `ApplyExternalChanges` plus `SimulateFullGrid`, reads compact deltas, and logs asset/factory/dispatch/readback lifecycle tokens.
   - Added a runtime initializer in progress that builds the live `FireGrid` from Timberborn `MapSize.TerrainSize` and adapter-produced terrain sources from `ITerrainService.GetAllHeightsInCell(...)`, rather than a permanent empty source set.
   - Deploy packaging dry-run evidence before the private-path fix: `bun scripts/deploy-timberborn-mod.ts --dry-run` built `Wildfire.slnx`, ran Unity batchmode `FireSimAssetBundleBuilder`, produced `src/Wildfire.Unity/UnityBatchmodeProject/Build/AssetBundles/StandaloneOSX/wildfire_compute_mac`, and printed planned staging into `~/Documents/Timberborn/Mods/Wildfire/AssetBundles/wildfire_compute_mac`.
   - Live validation crash evidence: Timberborn's built-in `ModAssetBundleLoader` auto-loaded `~/Documents/Timberborn/Mods/Wildfire/AssetBundles/wildfire_compute_mac` before Wildfire code ran and rejected it as incompatible with the Unity runtime, even though Player.log showed both Timberborn and the builder on Unity `6000.3.6f1`.
   - Private-path fix: keep the Unity builder output reproducible, but stage the compute shader bundle under the private deployed folder `~/Documents/Timberborn/Mods/Wildfire/ComputeShaders/wildfire_compute_mac` and have `TimberbornComputeFireSimulatorFactory` load that exact private path manually. The loader also handles Unity returning a relative assembly path and falls back to `~/Documents/Timberborn/Mods/Wildfire`.
   - Verification evidence after the private-path fix: `git diff --check` passed; `dotnet test` passed 71 tests; `dotnet build Wildfire.slnx` passed; `WILDFIRE_RUN_UNITY_SHADER_HARNESS=1 WILDFIRE_UNITY_EXECUTABLE=/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity dotnet test --filter FullyQualifiedName~UnityBatchmodeExecutorCapturesSeededFixtureWhenEnabled` passed; `bun scripts/deploy-timberborn-mod.ts --apply --clean` rebuilt the bundle and deployed only `ComputeShaders/wildfire_compute_mac` plus managed assemblies.
   - Live validation after the private-path fix: Timberborn restarted and continued `Wildfire testing`; `bun scripts/invoke-timberborn-command.ts status --wait=6` returned `wildfire_command_result command=status success=true status=success simulator_integrated=false width=placeholder height=placeholder depth=placeholder tick_count=placeholder queued_changes=placeholder last_delta_count=placeholder message=ok`.
   - Current blocker: Player.log proves the auto-loader crash is gone and Wildfire now reaches manual load at `wildfire_timberborn_compute_asset_load_started bundle=wildfire_compute_mac path="/Users/jasonkleinberg/Documents/Timberborn/Mods/Wildfire/ComputeShaders/wildfire_compute_mac"`, but Unity still rejects the bundle with `The AssetBundle 'wildfire_compute_mac' could not be loaded because it is not compatible with this newer version of the Unity runtime. Rebuild the AssetBundle to fix this error.` Because Player.log and the builder both report Unity `6000.3.6f1`, the next fix should inspect AssetBundle build target/options or replace the packaging path rather than guessing an editor version.
