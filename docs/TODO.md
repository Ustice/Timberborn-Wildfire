# Wildfire Milestones

## Phase 1: Core Data Model

- [x] Scaffold solution and core projects.
- [x] Implement packed cell helpers.
- [x] Implement grid indexing helpers.
- [x] Define GPU simulator contracts, deltas, listeners, and change records.
- [x] Add packed-cell and scenario tests.

## Phase 2: Scenario Preview

- [x] Add named seeded scenarios.
- [x] Add seed/config options.
- [x] Add packed-cell-derived terminal preview.
- [x] Add fixture export for shader snapshots.

## Phase 3: Unity Compute Simulator

- [x] Implement compute shader rule translation.
- [x] Implement full-grid dispatch scaffold.
- [x] Emit compact delta records scaffold.
- [x] Generate visual texture output scaffold.
- [x] Add shader snapshot harness.
- [x] Run `FireSim.compute` through a real shader execution harness.

## Phase 4: Timberborn GPU Integration

- [x] Convert terrain/buildings/resources into packed cells.
- [x] Upload external changes to GPU buffers.
- [x] Add in-process QA command bridge scaffold.
- [x] Capture in-game Timberborn menu coordinates.
- [x] Add Timberborn mod build/deploy path.
- [x] Capture real Timberborn deploy and `Player.log` load proof.
- [x] Capture title-screen coordinates from a safe startup state.
- [ ] Bind QA command bridge to a running Timberborn invocation surface.
- [ ] Dispatch the GPU simulator on a fixed cadence.
- [ ] Consume compact deltas for overlays and gameplay consequences.
- [ ] Validate first live gameplay loop.

## Phase 5: Optimization And Polish

- [ ] Add active frontier optimization if profiling justifies it.
- [ ] Tune visual output.
- [ ] Add runtime diagnostics.
