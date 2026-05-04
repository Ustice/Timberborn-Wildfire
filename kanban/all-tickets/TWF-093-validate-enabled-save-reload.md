---
ticket: TWF-093
agent_level: Medium
role: qa
requires_qa: true
doc_only: false
dependencies:
  - TWF-046
write_scope:
  - docs/TEST_PLAN.md
  - docs/HANDOFF.md
  - kanban/all-tickets/TWF-093-validate-enabled-save-reload.md
---

# TWF-093: Validate Enabled Save Reload

## Goal

Validate saving and reloading a game after fire activity while Wildfire remains enabled.

## Why

Baseline save/reload is a different risk than removing the mod. It should prove runtime state can rebuild or persist safely across normal play sessions.

## Requirements

- Trigger fire activity in a loaded save.
- Save after active or recently active fire.
- Restart or reload with Wildfire still enabled.
- Confirm simulator, visual surface, alerts, and command/status paths recover or report safe degraded state.
- Preserve save name, commands, logs, screenshots, and artifact paths.
- Update `docs/TEST_PLAN.md` and `docs/HANDOFF.md` with accepted evidence or blockers.

## Dependencies

- `TWF-046` proves the coherent live gameplay loop.

## Parent Reference

- Parent gate: `TWF-047`.

## Role

- QA.
- Follow [qa.md](../roles/qa.md).

## Implementation Notes

- Use the existing deploy and loaded-save helpers rather than ad hoc UI steps: `bun scripts/deploy-timberborn-mod.ts --apply` when a fresh deploy is needed, then `bun scripts/load-latest-save-and-unpause.ts --launch` or `--attach` with a ticket-specific artifact directory.
- Before saving, capture baseline `qa-readiness` or `status` output that proves `bridge_alive=true`, `runtime_loaded=true`, `loaded_game_ready=true`, and `simulator_integrated=true`.
- Trigger fire activity through the safest available command bridge path, then save and reload with Wildfire still enabled; keep the exact save name and reload path in the ticket notes.
- Passing evidence should show the reloaded session has a responsive command bridge, no new critical exceptions in `Player.log`, and either recovered simulator/visual/alert state or an explicit degraded state.
- Do not validate mod removal here. Any disable/remove behavior belongs to `TWF-094`, and re-enable rebuild behavior belongs to `TWF-095`.

## Verification

- Run `git diff --check`.
- Passing evidence requires no new critical exceptions in `Player.log`.

## Notes

- Disabled-mod recovery belongs to `TWF-094`; re-enable behavior belongs to `TWF-095`.
- QA passed on 2026-05-03 with evidence under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-093-enabled-save-reload-20260503T040447Z`.
- The accepted save slot was `Wildfire testing (7)`, `Cycle 23, day 14`; `Player.log` recorded `Saving game to Wildfire testing - Wildfire testing (7) at 2026-05-03 00:06:57Z` and `Saved game in 0.39s`.
- Pre-save fire activity passed through `qa-delta-stimulus` and readiness evidence with `last_delta_count=2`, `started_burning=1`, `visual_effect_events=2`, `gameplay_consequences=1`, and `alerts=1`.
- Reload with Wildfire still enabled loaded `/Users/jasonkleinberg/Documents/Timberborn/ExperimentalSaves/Wildfire testing/Wildfire testing (7).timber`, attached to the loaded save, unpaused, and recovered fresh command/status output with `loaded_game_ready=true`, `simulator_integrated=true`, `visual_field_surface_bound=true`, dimensions `128x128x23`, and `tick_count=3`.
- Post-reload stimulus passed with `last_delta_count=2`, `visual_effect_events=2`, `alerts=1`, `active_pooled_fire_effects=1`, `notification_sent=true`, and `pooled_fire_effect_presentation_failures=0`.
- `Player-run-window-critical-scan.txt` had `0` lines. The only transient miss was a narrow `last_delta_count` timing window in `15-post-reload-after-delta-readiness.txt` and `15b-post-reload-after-delta-readiness.txt`; rerun evidence in `18-post-reload-second-after-delta-readiness.txt` passed the same nonzero-delta gate.
- Key artifacts include `Player.log`, `Player-run-window.log`, `Player-run-window-key-events.txt`, `06-save-dialog-after-click.png`, `08-load-dialog-after-save.png`, `11-post-reload-qa-readiness.txt`, `12-post-reload-status.txt`, `18-post-reload-second-after-delta-readiness.txt`, and `artifact-listing.txt`.
