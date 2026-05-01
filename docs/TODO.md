# Wildfire Milestones

## Phase 1: CPU Core

- [x] Scaffold solution and core projects.
- [x] Implement packed cell helpers.
- [x] Implement deterministic CPU simulator.
- [x] Emit deltas and listener notifications.
- [x] Add first unit tests.
- [ ] Harden active frontier, dedupe, and listener next-tick behavior.
- [ ] Add seeded CPU snapshot scenarios.

## Phase 2: CLI Harness

- [x] Add minimal terminal renderer.
- [ ] Add scenario selection.
- [ ] Add seed/config options.
- [ ] Improve redraw and layer navigation ergonomics.

## Phase 3: Timberborn CPU Backend

- [ ] Discover current Timberborn adapter APIs.
- [ ] Convert terrain/buildings/resources into packed cells.
- [ ] Consume deltas for overlays and gameplay consequences.
- [ ] Validate first live gameplay loop.

## Phase 4: Unity Compute Prototype

- [ ] Implement compute shader rule translation.
- [ ] Compare GPU snapshots against CPU snapshots.
- [ ] Generate visual texture output.

## Phase 5: Timberborn GPU Backend

- [ ] Package/load compute shader.
- [ ] Drive GPU visual pipeline.
- [ ] Read back compact deltas for gameplay.
- [ ] Keep CPU fallback/debug mode.
