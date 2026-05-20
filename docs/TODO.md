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

Detailed sprint boundaries live in [../kanban/sprint-planning.md](../kanban/sprint-planning.md). Current sprint closeout and follow-on planning live under [../kanban/sprints/](../kanban/sprints/).

### Ash Simulator Model Follow-Up

The current ash authority direction lives in [ash-simulation-model.md](ash-simulation-model.md). The implementation should move as one coordinated ash-model wave, but the durable work items are split so the board can track the different proof gates:

- [x] `TWF-157`: migrate ash amount and contamination to simulator transport state.
- [x] `TWF-158`: add queued simulator ash mutations for Timberborn actions.
- [x] `TWF-159`: drive ash presentation from simulator state without renderer projection.
- [x] `TWF-160`: sync ash persistence, status, harvest, and fertile application with simulator state.
- [ ] `TWF-161`: rename ash field concepts after the behavior migration is proven.

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

- [x] `TWF-066`: tune visible fire effect. Live QA accepted the procedural Fire particle baseline on `QA Tunnels and Booms`.
- [ ] `TWF-088`: tune fire spread pace. Reviewed deterministic implementation is blocked on command-responsive loaded-save and low-resolution recording evidence.
- [ ] `TWF-089`: tune fuel burn-down duration. Reviewed deterministic implementation is blocked on `TWF-088` live acceptance and burn-duration recording evidence.
- [ ] `TWF-090`: tune water suppression behavior. Reviewed deterministic evidence is blocked on upstream live acceptance and suppression recording evidence.
- [ ] `TWF-091`: tune structure vertical fire behavior. Reviewed deterministic evidence is blocked on upstream live acceptance and structure recording evidence.
- [ ] `TWF-092`: tune burnout cooling behavior. Reviewed deterministic evidence is blocked on upstream live acceptance and burnout/cooling recording evidence.
- [x] `TWF-147`: implement GPU field renderer. Live 50x50 Diorama QA proved one visible rendered GPU field region with material-ready renderer telemetry and recording evidence.
- [x] `TWF-155`: clarify GPU renderer region telemetry. Split true dropped regions from below-threshold invisible regions before release diagnostics rely on the counter.

### Sprint 7: World Consequence Core

Goal: make fire damage the world in durable, resource-accounted ways.

First ready wave:

- [x] `TWF-075`: add burn damage state foundation.
- [x] `TWF-076`: apply crop burn consequences. Live QA accepted manual selected-Blueberry proof with precise safe-unavailable mutation telemetry.
- [x] `TWF-084`: apply tree burn consequences. Live QA accepted real tree/cuttable registration and nonzero tree-burn telemetry with precise safe-unavailable mutation telemetry.
- [x] `TWF-116`: define explosive storage fire behavior.
- [x] `TWF-117`: classify infrastructure burn behavior.

Follow-on gates:

- [x] `TWF-077`: apply structure burn damage rollback. Fixed for the `TWF-075` ownership bypass, live-proved on `Fuel`, reviewed, and integrated on `main`.
- [x] `TWF-115`: apply stored goods burn consequences. Fixed for the `TWF-075` ownership bypass, live-proved on `Fuel`, reviewed, and integrated on `main`.
- [x] `TWF-127`, `TWF-128`, and `TWF-129`: path, power, and water infrastructure effects. Fixed for the `TWF-075` ownership bypass, selector specificity, and power/water live registration; live-proved on `Fuel` plus `Smolville`, reviewed, and integrated on `main`.

### Sprint 10: Recovery And Consequence Gates

- [x] `TWF-066`: accepted procedural Fire readability baseline.
- [x] `TWF-072`: accepted beaver exposure telemetry, including beaver-proximate live proof without behavior mutation.
- [x] `TWF-076`: accepted crop/harvestable consequence telemetry from manual crop selection.
- [x] `TWF-084`: accepted tree/cuttable consequence telemetry.
- [x] `TWF-089`: accepted fuel burn-down recovery.
- [x] `TWF-152`: accepted native dynamite fire-triggering boundary.
- [x] `TWF-153`: accepted conservative detonator fire-safety behavior.
- [x] `TWF-154`: accepted tunnel fire destruction gate.
- [ ] Blocked carry-forward: `TWF-090`, `TWF-091`, `TWF-092`, `TWF-144`, `TWF-148`, `TWF-149`, and `TWF-150` remain explicit blockers.

### Later Sprints

- Sprint 8: live QA recovery and blocked evidence triage.
- Sprint 9: real field replacement and real-field proof closed as mixed done/blocked. `Fuel` is accepted as the stable `50x50` Sprint 9 surface, but `TWF-144` and `TWF-148` are blocked on making normal gameplay visuals plainly visible.
- Sprint 11: visual, beaver, and clean-ash verification is closed. `TWF-067`, `TWF-073`, `TWF-078`, and `TWF-082` are done after Jason confirmed the clean ash and fertile ash routes; the split tainted-ash live gate is now accepted in `TWF-166`. See [../kanban/sprints/sprint-11-visual-beaver-ash-verification.md](../kanban/sprints/sprint-11-visual-beaver-ash-verification.md).
- Later: visual consequences and aftermath from real fields should continue from [world-consequence-first-pass.md](world-consequence-first-pass.md) once `TWF-144`/`TWF-148` are unblocked.
- Sprint 12: release gameplay readiness. `TWF-166`, `TWF-161`, and `TWF-162` are done. `TWF-079`, `TWF-085`, `TWF-163`, `TWF-169`, and `TWF-170` have reviewed implementation work but are blocked on command-responsive live Timberborn reruns or screenshots. Remaining included tickets cover ash/water interaction, fire-heat beaver consequences, structure-on-fire alerts, paused-load ash visibility, rare ignition, aggregated feedback, release icons, persistence, and `256x256` QA; see [../kanban/sprints/sprint-12-release-gameplay-readiness.md](../kanban/sprints/sprint-12-release-gameplay-readiness.md).
- Later: release packaging and Steam Workshop after the gameplay loop, screenshots, and QA gates are stable.
