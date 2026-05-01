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
- [ ] Add fixture export for shader snapshots.

## Phase 3: Unity Compute Simulator

- [ ] Implement compute shader rule translation.
- [ ] Implement full-grid dispatch.
- [ ] Emit compact delta records.
- [ ] Generate visual texture output.
- [ ] Add shader snapshot tests.

## Phase 4: Timberborn GPU Integration

- [ ] Convert terrain/buildings/resources into packed cells.
- [ ] Upload external changes to GPU buffers.
- [ ] Dispatch the GPU simulator on a fixed cadence.
- [ ] Consume compact deltas for overlays and gameplay consequences.
- [ ] Validate first live gameplay loop.

## Phase 5: Optimization And Polish

- [ ] Add active frontier optimization if profiling justifies it.
- [ ] Tune visual output.
- [ ] Add runtime diagnostics.
