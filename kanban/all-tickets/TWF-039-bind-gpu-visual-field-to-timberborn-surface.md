---
ticket: TWF-039
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-037
write_scope:
   - src/Wildfire.Unity/**
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-039-bind-gpu-visual-field-to-timberborn-surface.md
---

# TWF-039: Bind GPU Visual Field To Timberborn Surface

## Goal

Expose the GPU visual field through a Timberborn visual surface so fire, smoke, ash, or heat intensity can be rendered from simulator output.

## Why

The design says GPU visuals may stay GPU-side while gameplay reactions go through C# deltas. The repository already has visual-field buffer and checksum proof; the next design step is to connect that output to a real Timberborn-facing surface without turning visual cells into entities.

## Requirements

- Keep gameplay consequences separate from GPU visual output.
- Use the existing visual-field channels: fire, smoke, ash, and heat or visibility.
- Avoid one Timberborn entity per simulated cell.
- Add the smallest binding needed to render or inspect visual-field data in Timberborn.
- Preserve shader snapshot and checksum evidence for visual-field writes.
- Add deterministic tests for binding configuration or data routing where possible.
- Document live visual QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-037` gives a delta-driven inspection path and confirms the changed-cell surface is useful before richer visual binding.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run the opt-in Unity shader harness if shader or visual-field behavior changes.
- QA must capture live screenshots or visual artifacts plus relevant `Player.log` evidence.

## Notes

- If Timberborn requires a different visual bridge than the current buffer abstraction supports, record the minimal adapter change instead of adding a parallel renderer.
- Worker implementation in `/Users/jasonkleinberg/repos/wildfire-TWF-039` on branch `codex/TWF-039-bind-gpu-visual-field`.
- Changed files:
   - `src/Wildfire.Timberborn/TimberbornGpuVisualFieldSurface.cs`
   - `src/Wildfire.Timberborn/TimberbornComputeFireSimulator.cs`
   - `src/Wildfire.Timberborn/TimberbornFireSystem.cs`
   - `src/Wildfire.Timberborn/TimberbornFireRuntime.cs`
   - `src/Wildfire.Timberborn/TimberbornQaCommandBridge.cs`
   - `src/Wildfire.Timberborn/WildfireConfigurator.cs`
   - `tests/Wildfire.Core.Tests/TimberbornGpuVisualFieldSurfaceTests.cs`
   - `tests/Wildfire.Core.Tests/TimberbornQaCommandBridgeTests.cs`
   - `docs/TEST_PLAN.md`
- Implementation details:
   - Added a Timberborn-side visual-field surface contract that binds one GPU buffer with dimensions, cell count, 16-byte stride, channel order `fire,smoke,ash,visibility`, a `TryGetBinding` view exposing the actual bound buffer handle, and `TryGetComputeBuffer` for typed Unity renderer/effect consumers.
   - Bound `ITimberbornGpuVisualFieldSurface` as a `WildfireConfigurator` game singleton and changed `TimberbornComputeFireSimulatorFactory` to receive that shared instance instead of privately constructing a hidden surface.
   - Added bounded data inspection through `InspectCells`, capped at 256 explicit cell indices per request, with a Unity `ComputeBuffer.GetData` reader in Timberborn runtime and a fakeable reader seam for deterministic tests.
   - Added `TimberbornGpuVisualFieldSurfaceBindingLifecycle`, the seam used by the compute simulator to bind real grid metadata, mark dispatch updates, and unbind on dispose without requiring plain `dotnet test` to construct Unity `ComputeBuffer` resources.
   - Wired `TimberbornComputeFireSimulator` to bind the existing `VisualFields` compute buffer, mark it updated after each `SimulateFullGrid` dispatch, and unbind on dispose.
   - Added `qa-readiness`/`status` telemetry fields: `visual_field_surface_bound`, `visual_field_surface_cells`, and `visual_field_surface_updated_tick`.
   - Kept gameplay consequences on the compact-delta consumer path; no `Wildfire.Core` contract or fire-rule change was made.
- Tech-lead review follow-up:
   - Addressed P1 by exposing both the bound buffer handle/metadata and bounded sample inspection instead of telemetry-only state.
   - Addressed P2 with lifecycle tests for grid metadata binding, dispatch update marking, and unbind/dispose behavior through the same lifecycle seam used by `TimberbornComputeFireSimulator`.
   - Addressed remaining P1 by making the live surface a reachable DI singleton and proving a consumer reference observes the same surface instance used by the compute factory lifecycle.
- Evidence:
   - `dotnet test` passed after remaining P1 follow-up: 114 passed, 0 failed.
   - `git diff --check` passed after tech-lead follow-up.
   - `dotnet test --filter FullyQualifiedName~ShaderSnapshotHarnessTests` passed after remaining P1 follow-up: 4 passed, 0 failed.
   - `WILDFIRE_RUN_UNITY_SHADER_HARNESS=1 WILDFIRE_UNITY_EXECUTABLE=/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity dotnet test --filter FullyQualifiedName~UnityBatchmodeExecutorCapturesSeededFixtureWhenEnabled` passed after remaining P1 follow-up: 1 passed, 0 failed.
- Live QA needs:
   - Capture `Player.log` tokens for `wildfire_timberborn_gpu_visual_field_surface_bound` and `wildfire_timberborn_gpu_visual_field_surface_updated`.
   - Capture a follow-up `qa-readiness` or `status` result with `visual_field_surface_bound=true`, a non-placeholder `visual_field_surface_cells`, and `visual_field_surface_updated_tick`.
   - Capture screenshot or visual artifact once a renderer/material/effect consumes the bound buffer; this worker binding now exposes an inspectable data surface, but it is not yet rendered-pixel proof by itself.

## QA Evidence

- QA result 2026-05-02: PASS. Fresh live Timberborn validation from corrected worktree `~/repos/wildfire-TWF-039` proved the DI singleton visual surface is bound to the live GPU visual field and receives dispatch update ticks.
- Fresh deploy command from corrected worktree:
   - `bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout=60`
   - Result: PASS; `dotnet build Wildfire.slnx --configuration Debug` passed, Unity rebuilt `wildfire_compute_mac` and `wildfire_diagnostic_mac`, and the script staged `~/Documents/Timberborn/Mods/Wildfire` from `~/repos/wildfire-TWF-039`.
- Runtime startup/load command:
   - `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --lock-timeout=60`
   - Result: PASS; loaded save `Wildfire testing - 2026-05-02 16h44m, Day 14-14.autosave`, observed startup-mods, experimental-mode, main-menu, and loaded-save screens, unpaused, and captured post-status at `tick_count=3`.
- Follow-up command:
   - `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=10 --require-advanced-tick`
   - Result: PASS; `wildfire_command_result command=qa-readiness success=true status=success bridge_alive=true runtime_loaded=true loaded_game_ready=true simulator_integrated=true width=128 height=128 depth=23 tick_count=15 queued_changes=0 last_delta_count=0 visual_field_surface_bound=true visual_field_surface_cells=376832 visual_field_surface_updated_tick=15 message=loaded_game_ready`.
- `Player.log` evidence path:
   - Live log: `~/Library/Logs/Mechanistry/Timberborn/Player.log`.
   - Frozen copy: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-039-live-final-20260502T204610Z/Player.log`.
   - Extracted token copy: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-039-live-final-20260502T204610Z/twf-039-player-log-excerpts.txt`.
- `Player.log` tokens captured from the fresh run:
   - `wildfire_timberborn_gpu_visual_field_surface_bound width=128 height=128 depth=23 cell_count=376832 stride_bytes=16 channels=fire,smoke,ash,visibility`.
   - `wildfire_timberborn_gpu_visual_field_surface_updated tick=1 cell_count=376832 channels=fire,smoke,ash,visibility`.
   - `wildfire_timberborn_gpu_visual_field_surface_updated tick=15 cell_count=376832 channels=fire,smoke,ash,visibility`.
- Startup and command artifacts:
   - Startup artifact directory: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T20-45-19-030Z`.
   - Startup summary: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T20-45-19-030Z/latest-save-startup-summary.txt`.
   - Post-unpause status: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T20-45-19-030Z/command-status-after-unpause.txt`.
   - Frozen QA readiness outbox: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-039-live-final-20260502T204610Z/qa-readiness-command-outbox.txt`.
- Screenshot/artifact note: rendered screenshot proof remains deferred until renderer, material, or effect code consumes the bound buffer. This ticket now proves the live bound buffer surface and telemetry, not rendered pixels.
- Cleanup state: deploy/startup shared lock `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock` was released after deploy and after startup. Timberborn was closed after evidence capture so no stale live process remains from this QA pass.
