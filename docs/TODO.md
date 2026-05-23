# Wildfire Milestones

GitHub Issues own active backlog work. This file tracks milestone-level status only; do not use it as a ticket board.

Historical sprint boundaries, closeout charters, final file-board statuses, and evidence manifests live on branch `archive/file-kanban-2026-05-23`.

## Foundation

- [x] Scaffold solution and core projects.
- [x] Implement packed cell helpers and grid indexing helpers.
- [x] Define GPU simulator contracts, deltas, listeners, and change records.
- [x] Add packed-cell, scenario, and shader-facing tests.
- [x] Add named seeded scenarios and packed-cell-derived CLI preview.

## GPU Simulation

- [x] Implement the authoritative Unity compute path.
- [x] Add compute-buffer allocation, shader dispatch, change upload, compact delta readback, visual fields, and snapshot harnesses.
- [x] Remove the alternate C# fire-spread execution path.
- [x] Run `FireSim.compute` through a real shader execution harness.
- [ ] Add active-frontier optimization only if post-release profiling proves full-grid dispatch is a measured blocker.

## Timberborn Integration

- [x] Convert terrain, buildings, resources, water, and badwater into packed cells and material fields.
- [x] Add the Timberborn build/deploy path and live `Player.log` load proof.
- [x] Bind the QA command bridge to a running Timberborn invocation surface.
- [x] Dispatch the GPU simulator on a fixed cadence in Timberborn.
- [x] Consume compact deltas for overlays, visual presentation, alerts, and first gameplay consequences.
- [x] Validate the first coherent live Timberborn gameplay loop.

## Release Gameplay

- [x] Add runtime diagnostics and compatibility probes.
- [x] Add release settings and save/reload safety validation.
- [x] Establish fire, smoke, ash, and steam presentation baselines.
- [x] Add burn-damage foundation and first crop, tree, structure, storage, infrastructure, explosive, tunnel, ash, and beaver-exposure consequence lanes.
- [x] Move ash amount and contamination authority into simulator transport state.
- [x] Add clean and tainted ash proof gates.
- [ ] Continue remaining release gameplay work from GitHub Issues, especially current `status:ready` and `status:blocked` issues.

## Live QA And Evidence

- [x] Add guarded Timberborn startup/load/unpause automation.
- [x] Add screen recording and coordinate-reference tooling.
- [x] Capture Timberborn menu, bottom-menu, and debug-panel references.
- [x] Preserve durable QA commands, manifests, copied logs, screenshots, and recordings for accepted gates.
- [ ] Keep command-responsive loaded-save validation as the gate for live Timberborn claims.

## Release Packaging

- [x] Add baseline GitHub CI for hosted-safe validation.
- [ ] Finish release packaging and Steam Workshop work after gameplay, screenshots, and QA gates are stable.
- [ ] Treat Workshop/media readiness as GitHub issue work, not local sprint-board work.
