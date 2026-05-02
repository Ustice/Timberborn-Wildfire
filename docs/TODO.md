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
- [ ] Consume compact deltas for overlays and gameplay consequences.
- [ ] Validate first live gameplay loop.

## Phase 5: Optimization And Polish

- [ ] Add active frontier optimization if profiling justifies it.
- [ ] Tune visual output.
- [x] Add runtime diagnostics.

## Next Three Sprint Plan

### Sprint 1: Prove The Live GPU Loop

Use the existing live-delta tickets to prove the simulator can be safely stimulated, can produce non-zero compact deltas from the real GPU path, can bind one narrow consequence, and can produce profiling evidence.

- `TWF-031`: add the guarded live stimulus.
- `TWF-032`: prove non-zero live GPU deltas.
- `TWF-033`: bind the first Timberborn-facing consequence.
- `TWF-034`: record live dispatch profiling and decide whether `TWF-011` should be promoted.

### Sprint 2: Make Fire Affect Timberborn

Once non-zero deltas are proven, expand the adapter-side gameplay surface without moving fire rules into Timberborn.

- `TWF-035`: expand Timberborn material mapping.
- `TWF-036`: apply building burnout consequences from deltas.
- `TWF-037`: update a fire overlay from changed cells.
- `TWF-038`: register water suppression changes.

### Sprint 3: Make Fire Legible To Players

After gameplay consequences exist, connect GPU visual output and player-facing feedback. Promote `TWF-011` into this sprint only if `TWF-034` shows full-grid dispatch is too expensive under a meaningful live workload.

- `TWF-039`: bind the GPU visual field to a Timberborn visual surface.
- `TWF-040`: add pooled fire, smoke, and ash effects.
- `TWF-041`: tune visual output with shader snapshots and live evidence.
- `TWF-042`: add a player-facing fire alert or status surface.

### Sprint 4: Tune Game Feel And Rules

After the first player-visible loop exists, tune the actual fire behavior and settle the design decisions that affect how the system feels in normal play.

- `TWF-043`: tune spread, burn, heat-loss, and suppression constants.
- `TWF-044`: resolve release-blocking simulation design decisions.
- `TWF-045`: add accepted shader snapshots for release scenarios.
- `TWF-046`: validate the first coherent gameplay loop.

### Sprint 5: Harden Saves Settings Compatibility

After the loop feels coherent, harden the mod against real saves, settings changes, compatibility drift, and failure modes.

- `TWF-047`: validate save, reload, disable, and re-enable behavior.
- `TWF-048`: add release settings with conservative defaults.
- `TWF-049`: add Timberborn compatibility probes and degraded-mode logs.
- `TWF-050`: harden GPU and asset failure modes.
- `TWF-051`: make the performance promotion decision for active frontier optimization.

### Sprint 6: Release Prep And Steam Workshop

After hardening, prepare the project for a public release through Steam Workshop as the official first distribution channel.

- `TWF-052`: set up baseline GitHub CI.
- `TWF-053`: add release packaging workflow.
- `TWF-054`: add tagged-release GitHub CD workflow.
- `TWF-055`: create release graphic and store metadata.
- `TWF-056`: write player-facing README and install docs.
- `TWF-057`: add changelog and versioning discipline.
- `TWF-058`: validate cross-platform asset bundles.
- `TWF-059`: run release-candidate clean install QA.
- `TWF-060`: scrub debug and developer surfaces for release.
- `TWF-061`: add license and attribution pass.
- `TWF-062`: add issue templates and support diagnostics.
- `TWF-063`: prepare Steam Workshop distribution.
