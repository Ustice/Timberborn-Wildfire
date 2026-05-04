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
- [x] Bind QA command bridge to a running Timberborn invocation surface.
- [x] Dispatch the GPU simulator on a fixed cadence.
- [x] Consume compact deltas for overlays and gameplay consequences.
- [x] Validate first live gameplay loop.

## Phase 5: Optimization And Polish

- [ ] Add active frontier optimization if profiling justifies it.
- [ ] Tune visual output.
- [x] Add runtime diagnostics.

## Current Sprint Plan

Detailed sprint boundaries live in [../kanban/sprint-planning.md](../kanban/sprint-planning.md). Sprint 5 closeout lives in [../kanban/sprints/sprint-05.md](../kanban/sprints/sprint-05.md), and the active Sprint 6 charter lives in [../kanban/sprints/sprint-06.md](../kanban/sprints/sprint-06.md).

### Completed Gameplay Foundation

- [x] Sprint 1: prove the live GPU loop.
- [x] Sprint 2: make fire affect Timberborn through the first bounded consequence lanes.
- [x] Sprint 3: make fire legible to players through visual-field binding, pooled effects, tuning, and alerts.
- [x] Sprint 4: tune game feel, settle release-blocking simulation decisions, and validate the first coherent live gameplay loop.

### Sprint 5: Safety Rails And Test Surfaces

Goal: make the next gameplay work reproducible and safe before broader world-consequence implementation begins.

First ready wave:

- [x] `TWF-049`: add compatibility probes.
- [ ] `TWF-064`: investigate building burnout pause consequence. Blocked on live Timberborn launch.
- [x] `TWF-065`: add screen recording QA tooling.
- [x] `TWF-093`: validate enabled save/reload.
- [x] `TWF-094`: validate disabled-mod save recovery.
- [x] `TWF-096`: add release settings framework.
- [x] `TWF-114`: define resource fuel and flammability catalog.
- [x] `TWF-118`: add generated world-consequence scenario save tool.
- [x] `TWF-126`: add ticket implementation-notes hygiene pass.

Follow-on gates:

- [x] `TWF-095`, then `TWF-047`: re-enable lifecycle and parent save/reload validation.
- [ ] `TWF-050`: GPU asset failure hardening after compatibility probes. Blocked on live Timberborn launch.
- [ ] `TWF-097`: enable/disable safety setting after settings framework. Deterministic implementation passed review; live QA is blocked.
- [ ] `TWF-119`: generated scenario validation after scenario-save tool. Blocked on live Timberborn launch.

### Sprint 6: Fire Feel And Field Presentation

Goal: make fire, smoke, ash, and steam readable, then tune core fire behavior from recording evidence.

First ready wave:

- [ ] `TWF-066`: tune visible fire effect. Reviewed deterministic implementation is blocked on live Timberborn recording evidence.
- [ ] `TWF-088`: tune fire spread pace. Reviewed deterministic implementation is blocked on command-responsive loaded-save and low-resolution recording evidence.
- [ ] `TWF-089`: tune fuel burn-down duration. Reviewed deterministic implementation is blocked on `TWF-088` live acceptance and burn-duration recording evidence.
- [ ] `TWF-090`: tune water suppression behavior. Reviewed deterministic evidence is blocked on upstream live acceptance and suppression recording evidence.
- [ ] `TWF-091`: tune structure vertical fire behavior. Reviewed deterministic evidence is blocked on upstream live acceptance and structure recording evidence.
- [ ] `TWF-092`: tune burnout cooling behavior. Reviewed deterministic evidence is blocked on upstream live acceptance and burnout/cooling recording evidence.

### Sprint 7: World Consequence Core

Goal: make fire damage the world in durable, resource-accounted ways.

First ready wave:

- [x] `TWF-075`: add burn damage state foundation.
- [ ] `TWF-076`: apply crop burn consequences. Reviewed deterministic implementation is blocked on live crop-burn QA evidence.
- [ ] `TWF-084`: apply tree burn consequences. Reviewed deterministic implementation is blocked on live tree-burn QA evidence.
- [ ] `TWF-116`: define explosive storage fire behavior. Awaiting contract review after research-only pass.
- [ ] `TWF-117`: classify infrastructure burn behavior. Awaiting classification review after research-only pass.

Follow-on gates:

- [ ] `TWF-077`: apply structure burn damage rollback. Blocked on `TWF-064` live investigation.
- [ ] `TWF-115`: apply stored goods burn consequences. Blocked on `TWF-116` contract acceptance and safe inventory APIs.

### Later Sprints

- Sprint 8: ash, contamination, and persistence.
- Sprint 9: beavers and player feedback.
- Sprint 10: release packaging and Steam Workshop.
