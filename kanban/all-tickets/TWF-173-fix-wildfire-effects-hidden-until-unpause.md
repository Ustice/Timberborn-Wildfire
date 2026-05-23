---
ticket: TWF-173
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-039
  - TWF-040
  - TWF-147
  - TWF-170
write_scope:
  - src/Wildfire.Timberborn/**
  - src/Wildfire.Unity/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-173-fix-wildfire-effects-hidden-until-unpause.md
---

# TWF-173: Fix Wildfire Effects Hidden Until Unpause

## Goal

Fix the bug where Wildfire visual effects do not load or become visible until after the game is unpaused.

## Why

The released game should render already-existing Wildfire state at loaded-save readiness. `TWF-170` fixed the paused-load ash presentation path, but the broader effects pipeline still appears to require an advancing unpaused tick before fire, smoke, ash, steam, heat haze, or field-renderer state becomes visible.

## Requirements

- Reproduce the current released issue from a paused loaded save or equivalent QA setup with existing Wildfire visual state.
- Determine whether the missing pre-unpause effects are caused by visual-field surface binding, pooled native effect initialization, GPU field renderer material readiness, transport/material buffer refresh, persisted state restore ordering, or Timberborn pause-state update order.
- Initialize existing Wildfire visual state at loaded-save readiness before the first unpaused simulation tick.
- Preserve simulator-owned state and avoid adding a second authoritative visual/effect store.
- Ensure the pre-unpause refresh does not duplicate pooled effects, emit duplicate alerts, collect ash, mutate simulator state, or advance fire behavior.
- Reuse the `TWF-170` paused-load ash fix where appropriate, but cover the broader Wildfire effects path rather than only ash fields.
- Expose or update status counters that prove effects are bound, material-ready, and visible while paused.
- Add deterministic coverage for paused loaded-save effect initialization without an advancing tick.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-039` binds visual fields to Timberborn.
- `TWF-040` owns pooled fire, smoke, and ash effects.
- `TWF-147` owns the GPU field renderer.
- `TWF-170` fixed the narrower paused-load ash presentation bug and should be used as prior art.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Treat this as a lifecycle/initialization bug, not a request to change fire, smoke, ash, steam, heat, or contamination semantics.
- Inspect the whole scene during visual QA, not only the expected effect cell, because pause-load refresh bugs often show as stale overlays, missing materials, duplicate effects, or wrong camera-layer visibility.
- Keep all Timberborn-specific lifecycle work in the adapter.

## Verification

- Run `git diff --check`.
- Run targeted paused-load presentation/effects tests.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Live QA must load a save with existing Wildfire visual state, remain paused, and capture screenshot/status/log evidence that effects are visible before unpause; then unpause and prove the effects remain stable without duplication.

## Notes

- Created on 2026-05-23 after Jason reported that Wildfire effects do not load until after the game is unpaused.
- 2026-05-23 worker branch `codex/twf-173-effects-paused-load` in `~/repos/wildfire-twf-173` diagnosed that `TWF-170` fixed ash through restored transport/material buffers, but non-ash effects still depended on visual/smoothed GPU buffers populated by a later dispatch. The implementation seeds restored GPU visual fields from persisted packed cells and transport fields, and seeds the indirect renderer's smoothed fire/smoke/steam buffer during renderer initialization without advancing simulation or adding a second authoritative store.
- 2026-05-23 worker checks passed: `git diff --check`, targeted renderer/surface tests `25/25`, targeted final renderer tests `12/12`, full `dotnet test` `481/481`, `dotnet build Wildfire.slnx`, and deploy from `~/repos/wildfire-twf-173`.
- 2026-05-23 deterministic review passed. Reviewer noted that live QA should rely on whole-scene screenshots and restore/seed log lines such as `wildfire_timberborn_gpu_indirect_renderer_seeded`; older `gpu_field_renderer_visible_regions=0` counters alone do not prove the indirect renderer failed.
- 2026-05-23 blocker: required live paused-load QA did not pass because Timberborn never foregrounded; screenshots reported `frontmost_bundle_id=com.openai.codex`, coordinator `qa-readiness --wait=6 --require-advanced-tick` timed out waiting for `command-outbox.txt`, and no command-responsive loaded save was available. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-173-paused-effects-20260523T080220Z/`. Smallest next action is to rerun `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --artifacts-dir <new-artifact-root>/latest-save-startup --lock-timeout=60` from `~/repos/wildfire-twf-173` when Timberborn can be foregrounded, then capture paused pre-unpause screenshot/status/log proof and post-unpause stability.
- 2026-05-23 coordinator rerun after the sleep/focus blocker cleared: deployed `~/repos/wildfire-twf-173` and live paused-load QA passed the startup/restore gate. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-173-paused-effects-rerun-20260523T130247Z/`; startup artifact: `latest-save-startup/2026-05-23T13-02-47-718Z/`. Before unpause, the loaded save was command-responsive at `tick_count=667` with `loaded_game_ready=true`, `simulator_integrated=true`, `ash_field_entries=758`, `visual_field_surface_bound=true`, `visual_field_surface_updated_tick=667`, `smoke_height_smoke_cells=45`, `gpu_field_renderer_enabled=true`, and `gpu_field_renderer_material_ready=true`. `Player.log` includes `wildfire_timberborn_gpu_simulator_state_restored tick=667 cell_count=57500 atmospheric_fields=57500`, `wildfire_timberborn_firesim_persistence_restored tick=667 cell_count=57500`, `wildfire_timberborn_persistence_runtime_state_restored ash_entries=758 ...`, and `wildfire_timberborn_gpu_indirect_renderer_seeded cell_count=57500`. After unpause, status advanced to `tick_count=670` with `ash_field_entries=758`, `visual_field_surface_bound=true`, `visual_field_surface_updated_tick=670`, `gpu_field_renderer_enabled=true`, and `gpu_field_renderer_material_ready=true`. Whole-scene screenshot inspection showed the restored ash/field state visible before unpause and stable after unpause; no active flame/steam-specific pooled-effect target was separately proven in this save, so final acceptance should preserve that nuance.
- 2026-05-23 reviewer pass after live rerun: no blocking findings. The reviewer accepted the live paused-load evidence plus deterministic coverage as sufficient for integration: restored ash/field state, restored smoke telemetry, visual surface binding, GPU material readiness, and indirect renderer seeding were present before unpause, then remained stable after unpause without duplicate alerts or ash mutations observed. The reviewer noted the missing active flame/steam-specific paused-load visual target is real but should not block this fix because deterministic tests cover those seed paths; file a separate follow-up QA ticket only if release proof needs an active flame/steam paused-load scenario. Integration wrinkle: the implementation is currently uncommitted local modifications in `~/repos/wildfire-twf-173`, so integration must pull the dirty worktree diff rather than the branch tip alone.
- 2026-05-23 integration complete in the main checkout. Integrated the dirty `~/repos/wildfire-twf-173` worktree diff into `main`, preserved the main-checkout Steam cloud-save dialog test-plan note while applying the paused-effects test-plan update, and corrected a partial apply by restoring the paused-load source/test patch in the main checkout. Current main-checkout behavior repopulates the GPU visual-field buffer during persistence restore and seeds the indirect renderer's smoothed fire/smoke/steam buffer before the first paused frame. Reran `git diff --check`, `bun run kanban:audit`, targeted `TimberbornGpuFieldRendererTests` with 12 tests, full `dotnet test` with 488 tests, and `dotnet build Wildfire.slnx` with 0 warnings and 0 errors. Coordinator moved this ticket to `06-done`.
