---
ticket: TWF-037
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-033
   - TWF-035
write_scope:
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-037-update-fire-overlay-from-deltas.md
---

# TWF-037: Update Fire Overlay From Deltas

## Goal

Update a Timberborn-facing overlay or debug visualization from compact changed cells so fire state can be inspected without creating one entity per cell.

## Why

The design calls out overlay updates as a primary delta use and explicitly says Timberborn should update only where needed. This ticket gives QA and future gameplay work a readable map of changed fire state before the richer GPU visual pipeline is fully player-polished.

## Requirements

- Consume compact deltas rather than scanning the whole grid each update.
- Keep overlay state derived from packed cells; do not add adapter-side fire rules.
- Use an existing Timberborn-safe visual/debug surface if one is available.
- Keep updates bounded to changed cells and expose counters for updated cells.
- Add deterministic tests for visual-state-change filtering and update routing.
- Document live evidence expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-033` proves delta consumers can produce a visible or gameplay-relevant result.
- `TWF-035` makes mapped cell values more meaningful.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live status counters, `Player.log` evidence, and screenshots if the overlay is visible.

## Notes

- This is the practical inspection layer. Rich pooled fire and smoke effects belong to `TWF-040`.
- Worker note 2026-05-02: implemented the inspection layer on the existing Timberborn-safe debug visual sink rather than creating per-cell entities or mutating terrain. `TimberbornFireDebugVisualCellState` now stores the latest packed cell value for each updated index, and inspection fields derive from `PackedCell` helpers.
- Delta routing note: `TimberbornFireDeltaConsumer` still consumes compact `CellDelta` batches only. It filters no-op visual-state changes, updates only the affected overlay entries, preserves the persistent overlay cell count, and now records `debug_visual_updated_cells` for the current dispatch separately from total `debug_visual_cells`.
- Status evidence note: `TimberbornQaCommandState` and `wildfire_command_result` now expose `last_delta_consumer_debug_visual_updated_cells` alongside the existing persistent debug visual count, so QA can prove the overlay update count moved without needing one entity per cell.
- Documentation note: `docs/TEST_PLAN.md` now documents `TWF-037` live evidence expectations. QA should capture `Player.log` `wildfire_timberborn_delta_consumer_completed ... debug_visual_updated_cells=<nonzero> ... debug_visual_cells=<count>` plus a follow-up `qa-readiness` or `status` result with both debug visual counters. Screenshots are optional until a later rendered panel consumes this state.
- Changed files:
   - `src/Wildfire.Timberborn/TimberbornDeltaConsumers.cs`
   - `src/Wildfire.Timberborn/TimberbornFireRuntime.cs`
   - `src/Wildfire.Timberborn/TimberbornQaCommandBridge.cs`
   - `tests/Wildfire.Core.Tests/TimberbornFireDeltaConsumerTests.cs`
   - `tests/Wildfire.Core.Tests/TimberbornQaCommandBridgeTests.cs`
   - `docs/TEST_PLAN.md`
   - `kanban/all-tickets/TWF-037-update-fire-overlay-from-deltas.md`
- Worker evidence 2026-05-02:
   - `git diff --check` passed.
   - `dotnet test` passed: 88 tests.
   - `dotnet build Wildfire.slnx` passed with 0 warnings and 0 errors.
- Blockers: none for worker scope. Live Timberborn QA is still required because `requires_qa: true`; I did not run live Timberborn.

## QA Evidence

- QA result 2026-05-02: PASS. Live Timberborn validation proved the debug inspection overlay is updated from compact deltas and exposes the new bounded-update counters.
- Commands run:
   - `osascript -e 'tell application id "com.mechanistry.timberborn" to quit' || true ...` to stop the already-running older deployed game before redeploy.
   - `bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout=30`
   - `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240`
   - `cliclick c:960,400` after the guarded launcher captured an unrelated macOS iPad connection alert covering Timberborn.
   - `bun scripts/load-latest-save-and-unpause.ts --attach --wait=180`
   - `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick`
   - `bun scripts/invoke-timberborn-command.ts qa-delta-stimulus --wait=6 --require-advanced-tick`
   - `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick --require-nonzero-delta`
- Build/deploy evidence:
   - Deploy command built `Wildfire.slnx` successfully with 0 warnings and 0 errors.
   - Deploy command rebuilt `wildfire_compute_mac` and `wildfire_diagnostic_mac`.
   - Deployed target: `~/Documents/Timberborn/Mods/Wildfire`.
- Startup/load evidence:
   - First launch artifact with unrelated macOS alert: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T18-19-42-463Z/00-current-screen-04.png`.
   - Successful guarded attach artifact directory: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T18-21-23-032Z`.
   - Successful startup summary: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T18-21-23-032Z/latest-save-startup-summary.txt`.
   - Loaded/unpaused proof from startup utility: `post_status_ok tick_count=3`.
- Command counter evidence:
   - Baseline readiness returned `tick_count=15`, `last_delta_count=0`, `last_delta_consumer_debug_visual_updated_cells=0`, and `last_delta_consumer_debug_visual_cells=0`.
   - `qa-delta-stimulus` returned `tick_count=22`, `queued_changes=1`, `target_index=188480`, `target_x=64`, `target_y=64`, `target_z=11`, and `set_cell=13311`.
   - Follow-up readiness returned `tick_count=28`, `queued_changes=0`, `last_delta_count=1`, `last_delta_consumer_changed_cells=1`, `last_delta_consumer_debug_visual_updated_cells=1`, and `last_delta_consumer_debug_visual_cells=1`.
- `Player.log` evidence path: `~/Library/Logs/Mechanistry/Timberborn/Player.log`.
- `Player.log` tokens captured:
   - `wildfire_command_request command=qa-delta-stimulus`
   - `wildfire_timberborn_changes_registered source=qa_delta_stimulus count=1 pending_changes=1`
   - `wildfire_timberborn_qa_delta_stimulus_queued cell_index=188480 x=64 y=64 z=11 set_cell=13311`
   - `wildfire_timberborn_dispatch_completed tick=23 delta_count=2 elapsed_ms=5.575`
   - `wildfire_timberborn_delta_consumer_completed tick=23 changed_cells=2 debug_visual_updated_cells=2 debug_visual_cells=1 started_burning=1 stopped_burning=0 fuel_depleted=0 heat_changed=2 water_changed=0 visual_effect_events=2 gameplay_consequences=1 alerts=1 max_heat=15`
   - `wildfire_command_result command=qa-readiness success=true status=success bridge_alive=true runtime_loaded=true loaded_game_ready=true simulator_integrated=true width=128 height=128 depth=23 tick_count=28 queued_changes=0 last_delta_count=1 last_delta_consumer_changed_cells=1 last_delta_consumer_debug_visual_updated_cells=1 last_delta_consumer_debug_visual_cells=1 last_delta_consumer_started_burning=0 last_delta_consumer_fuel_depleted=0 last_delta_consumer_visual_effect_events=1 last_delta_consumer_gameplay_consequences=0 last_delta_consumer_alerts=0 message=loaded_game_ready`
- Acceptance criteria:
   - Loaded, unpaused Timberborn save: PASS, guarded attach loaded the latest save and reported `post_status_ok tick_count=3`.
   - Guarded `qa-delta-stimulus` produced non-zero changed cells: PASS, follow-up logs showed `delta_count=2` and `changed_cells=2` at tick 23.
   - Delta consumer updated the debug inspection overlay from changed cells: PASS, `debug_visual_updated_cells=2` and `debug_visual_cells=1` at tick 23.
   - Follow-up command exposed both TWF-037 counters: PASS, readiness reported `last_delta_consumer_debug_visual_updated_cells=1` and `last_delta_consumer_debug_visual_cells=1`.
   - Screenshots: not required for this ticket because no rendered Timberborn panel consumes the adapter-local overlay state yet; startup screenshots were captured by the guarded utility.
- Final lock state: `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock` absent after validation.
- Recommended board move: move TWF-037 from `04-verify` to `05-integration`.
