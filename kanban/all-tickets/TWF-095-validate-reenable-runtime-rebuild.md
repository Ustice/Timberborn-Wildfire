---
ticket: TWF-095
agent_level: Medium
role: qa
requires_qa: true
doc_only: false
dependencies:
  - TWF-093
  - TWF-094
write_scope:
  - docs/TEST_PLAN.md
  - docs/HANDOFF.md
  - kanban/all-tickets/TWF-095-validate-reenable-runtime-rebuild.md
---

# TWF-095: Validate Reenable Runtime Rebuild

## Goal

Validate that re-enabling Wildfire rebuilds runtime state safely or reports a safe degraded state.

## Why

Re-enable behavior is not the same as ordinary reload. The adapter may need to rebuild caches, remap cells, rebind GPU resources, and avoid stale persisted assumptions.

## Requirements

- Re-enable Wildfire after the disabled-mod recovery path.
- Load the affected save.
- Confirm runtime state rebuilds, command/status paths respond, and no stale simulator state crashes the save.
- Confirm any missing state is either recomputed or explicitly reported as unavailable.
- Preserve logs, screenshots, save names, and command output.
- Update `docs/TEST_PLAN.md` and `docs/HANDOFF.md` with accepted evidence or blockers.

## Dependencies

- `TWF-093` validates ordinary enabled save/reload.
- `TWF-094` validates disabled-mod recovery.

## Parent Reference

- Parent gate: `TWF-047`.

## Role

- QA.
- Follow [qa.md](../roles/qa.md).

## Implementation Notes

- Start only after `TWF-093` and `TWF-094` have concrete evidence; this ticket should reuse their save name, artifact paths, and mod disable method rather than creating a parallel lifecycle story.
- Re-enable Wildfire through the same deployment path used earlier, then launch or attach with `scripts/load-latest-save-and-unpause.ts` and a ticket-specific artifact directory.
- Capture command bridge evidence after re-enable with `qa-readiness` or `status`, including `runtime_loaded`, `loaded_game_ready`, `simulator_integrated`, dimensions, `tick_count`, and any degraded-state message.
- Passing evidence should show no stale simulator crashes, no new critical `Player.log` exceptions, and either recomputed runtime state or an explicit unavailable state that downstream code can understand.
- If re-enable fails, block with the exact previous disabled evidence, re-enable deploy state, load result, command output, and log message instead of guessing at persistence behavior.

## Verification

- Run `git diff --check`.
- Passing evidence requires no new critical exceptions in `Player.log`.

## Notes

- Prefer explicit degraded state over pretending stale data survived.
- QA artifact pass was produced on 2026-05-03 under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-095-reenable-runtime-rebuild-20260503T050000Z`. The QA sub-agent stalled before a final prose report, so the coordinator verified the artifacts passively.
- The run started from the preserved `TWF-094` disabled/missing loaded state, exited to the main menu, re-enabled `Wildfire v0.1.0.0` through the Timberborn Mods dialog, restarted Timberborn, and loaded `Wildfire testing (7)` again.
- `Player-after-explicit-wildfire-testing-7-load.log` shows Wildfire active in the loaded mod list, compute and diagnostic asset loading, `wildfire_timberborn_gpu_visual_field_surface_bound`, `wildfire_timberborn_runtime_simulator_initialized width=128 height=128 depth=23`, and `wildfire_timberborn_runtime_initialize_completed`.
- The first two command probes (`23-qa-readiness-after-reenable-explicit-save.txt` and `24-status-after-reenable-explicit-save.txt`) hit a malformed command race and returned `Unknown_command_'qa-read...'`; recovery command `25-status-after-command-race-recovery.txt` passed with `runtime_loaded=true`, `loaded_game_ready=true`, `simulator_integrated=true`, dimensions `128x128x23`, and `visual_field_surface_bound=true`.
- After unpausing, `27-qa-readiness-after-unpause-advanced-tick.txt` passed with `tick_count=4`, `queued_changes=0`, `loaded_game_ready=true`, `simulator_integrated=true`, `visual_field_surface_bound=true`, `visual_field_surface_cells=376832`, and `message=loaded_game_ready`.
- Passive scan found only existing non-Wildfire Unity `gpath.c:115` assertions in copied logs; no Wildfire runtime crash or stale simulator failure was present in the re-enable evidence.
- Key artifacts include `06-main-menu-mods-before-reenable.png`, `07-main-menu-mods-wildfire-reenabled.png`, `16-load-game-dialog-reenabled.png`, `17-after-load-wildfire-testing-7-reenabled.png`, `21-post-load-wildfire-testing-7-reenabled.png`, `22-post-explicit-save-load-wait.png`, `25-status-after-command-race-recovery.txt`, `27-qa-readiness-after-unpause-advanced-tick.txt`, and `Player-after-explicit-wildfire-testing-7-load.log`.
