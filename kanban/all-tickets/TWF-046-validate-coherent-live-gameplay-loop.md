---
ticket: TWF-046
agent_level: High
role: qa
requires_qa: true
doc_only: false
dependencies:
   - TWF-043
   - TWF-045
write_scope:
   - docs/TEST_PLAN.md
   - docs/HANDOFF.md
   - kanban/all-tickets/TWF-046-validate-coherent-live-gameplay-loop.md
---

# TWF-046: Validate Coherent Live Gameplay Loop

## Goal

Prove one complete live Timberborn gameplay loop: fire starts, spreads, communicates state, causes a consequence, can be suppressed or resolves, and leaves the game stable.

## Why

Release cannot rest on isolated command or render evidence. This ticket verifies that the player-facing loop works as a coherent slice in a loaded save.

## Requirements

- Start from a known loaded Timberborn save.
- Use the guarded startup and live stimulus paths.
- Capture evidence for ignition or stimulus, spread, visual or overlay state, consequence, alert or status surface, suppression or burnout resolution, and continued runtime stability.
- Preserve command outputs, copied `Player.log`, screenshots, artifact paths, and final QA lock state.
- Record any confusing player-facing behavior as follow-up ticket notes.
- Update `docs/TEST_PLAN.md` with the accepted live loop evidence.
- Update `docs/HANDOFF.md` with remaining blockers or the next exact action.

## Dependencies

- `TWF-043` tunes fire behavior.
- `TWF-045` protects release scenario behavior with snapshots.

## Role

- QA.
- Follow [qa.md](../roles/qa.md).

## Verification

- Run `git diff --check`.
- Run the documented live QA flow and preserve all artifacts.
- Passing evidence requires no new Unity exceptions or Wildfire failure tokens after the run baseline.

## Notes

- This is not a package-release ticket. It is the live-loop acceptance gate before hardening.

QA pass on 2026-05-02 from `main` at `4de4642e7fd84d5033cf4b0a694db5b74b03238b`.

Artifact root:

- `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-046-live-20260502T232641Z/`

Commands and actions:

- `bun scripts/load-latest-save-and-unpause.ts --attach --wait=180 --artifacts-dir "$ARTIFACT/latest-save-attach" --lock-timeout=60`
- `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=10 --require-advanced-tick`
- `bun scripts/invoke-timberborn-command.ts qa-delta-stimulus --wait=10 --require-advanced-tick`
- `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=10 --require-advanced-tick --require-nonzero-delta`
- `printf 'qa-building-burnout-stimulus\n' > "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/command-inbox.txt"` twice, followed by readiness checks.
- `bun scripts/invoke-timberborn-command.ts qa-water-suppression-stimulus --wait=10 --require-advanced-tick`
- `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=10 --require-advanced-tick --require-water-changed`
- `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=10 --require-advanced-tick`
- `screencapture -x` for fire alert, burnout alert, water-suppression, and final stability screenshots.
- Copied `~/Library/Logs/Mechanistry/Timberborn/Player.log` into the artifact root, extracted `Player-run-window.log`, `Player-run-window-wildfire-events.txt`, `Player-run-window-failures.txt`, and `twf-046-live-loop-summary.txt`.

Evidence:

- Known loaded save: pass. Guarded attach classified `screen=loaded-save`, detected no blocking overlay, and recorded `already_unpaused tick_count_before=1707 tick_count_after=1709`.
- Baseline readiness: pass. `qa-readiness-baseline.txt` reports `loaded_game_ready=true`, `simulator_integrated=true`, dimensions `128x128x23`, `tick_count=1722`, and `queued_changes=0`.
- Ignition/stimulus: pass. `qa-delta-stimulus-output.txt` reports `target_index=188480`, `target_x=64`, `target_y=64`, `target_z=11`, `set_cell=13311`, and `queued_changes=1`; `Player-run-window.log` contains `wildfire_timberborn_qa_delta_stimulus_queued`.
- Spread and gameplay consequence: pass. At tick `1734`, `Player-run-window.log` shows `delta_count=2`, `changed_cells=2`, `started_burning=1`, `heat_changed=2`, `visual_effect_events=2`, `gameplay_consequences=1`, and `alerts=1`. At tick `1736`, it shows `stopped_burning=1` and `gameplay_consequences=1`.
- Visual or overlay state: pass. `qa-readiness-after-delta-require-nonzero.txt` reports `last_delta_count=1`, `last_delta_consumer_debug_visual_updated_cells=1`, `active_pooled_fire_effects=1`, `updated_visual_regions=1`, `pooled_fire_effects_visible_enabled=true`, and `pooled_fire_effects_native_prefab=CampfireFire`. Screenshot `fire-stimulus-visible-alert.png` captures the visible loaded save and native warning.
- Alert/status surface: pass. Tick `1734` logs `wildfire_timberborn_player_fire_alert_updated ... notification_sent=true ... message="Wildfire alert: 1 new fire. Max heat 15."`; the screenshot shows that quick warning.
- Suppression or resolution: pass. `qa-water-suppression-stimulus-output.txt` queued `SetWater=3` for the fixed center cell. `qa-readiness-after-water-require-water-changed.txt` reports `last_positive_water_changed_tick=1851`, `last_positive_water_changed_count=1`, `queued_changes=0`, and `message=loaded_game_ready`.
- Continued runtime stability: pass. `qa-readiness-delayed-stability.txt` at `tick_count=1908` reports `queued_changes=0`, `last_delta_count=0`, `visual_field_surface_bound=true`, `pooled_fire_effect_presentation_failures=0`, `player_fire_alert_presentation_failures=0`, and `message=loaded_game_ready`. `Player-run-window-failures.txt` has `0` strict failure-scan lines.
- Final lock state: pass. `final-lock-process-state.txt` shows no lock files under either WildfireQA lock directory. Timberborn was still running because QA attached to an already-running game rather than launching or owning the process lifecycle.

Artifacts:

- Command outputs: `load-latest-save-attach-output.txt`, `qa-readiness-baseline.txt`, `qa-delta-stimulus-output.txt`, `qa-readiness-after-delta-require-nonzero.txt`, `qa-building-burnout-stimulus-output.txt`, `qa-building-burnout-stimulus-second-output.txt`, `qa-readiness-after-building-burnout.txt`, `qa-readiness-after-building-burnout-second.txt`, `qa-water-suppression-stimulus-output.txt`, `qa-readiness-after-water-require-water-changed.txt`, and `qa-readiness-delayed-stability.txt`.
- Screenshots: `fire-stimulus-visible-alert.png`, `building-burnout-consequence-alert.png`, `building-burnout-consequence-alert-second.png`, `water-suppression-resolution.png`, and `final-stability-screen.png`.
- Logs: `Player.log`, `Player-run-window.log`, `Player-run-window-wildfire-events.txt`, `Player-run-window-failures.txt`, and `twf-046-live-loop-summary.txt`.
- Final state: `final-lock-process-state.txt`.

Follow-up notes:

- Two `qa-building-burnout-stimulus` attempts produced visible burned-out alert/status evidence and the first readiness check reported `last_delta_consumer_building_burnout_matched_cells=1`, but both checks reported `last_delta_consumer_building_burnout_applied_consequences=0`. This is confusing because the target provider selected unpaused pausable-building cells before queueing. It does not fail this ticket because the live loop produced compact-delta gameplay consequences from fire start/stop decisions, but Sprint 5 hardening should investigate the pausable-building pause mismatch.
- The live save already had prior alert and water counters from earlier QA (`last_player_fire_alert_tick=75`, `last_positive_water_changed_tick=75`) before this run. The accepted evidence uses later ticks from this run: alert tick `1734`, water tick `1851`, and stability tick `1908`.

Result:

- PASS for `TWF-046`.
- Recommended board move: `03-in-progress` -> `04-verify`.
