---
ticket: TWF-029
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-012
   - TWF-017
   - TWF-019
   - TWF-028
write_scope:
   - src/Wildfire.Timberborn/**
   - scripts/invoke-timberborn-command.ts
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-029-add-qa-readiness-command.md
---

# TWF-029: Add QA Readiness Command

## Goal

Add an allowlisted `qa-readiness` command to the Timberborn command bridge so QA can remotely check loaded-game readiness without navigating menus.

## Why

`TWF-028` found that generic remote Timberborn menu navigation is not practical or safe with current surfaces. The safer path is command-oriented: add one explicit remote command per QA outcome instead of trying to drive UI menus.

## Requirements

- Add `qa-readiness` to the existing command bridge allowlist.
- Keep the command read-only and non-destructive.
- Report whether the Wildfire command bridge is alive, whether the game-context runtime is loaded, whether the simulator is integrated, and the current simulator dimensions/tick fields already exposed by `status`.
- Include any additional loaded-game readiness fields that can be read safely from existing Wildfire state without probing Timberborn UI.
- Log `wildfire_command_request command=qa-readiness` and a searchable `wildfire_command_result command=qa-readiness ...` result.
- Update `scripts/invoke-timberborn-command.ts` so it can invoke `qa-readiness`.
- Do not add generic menu navigation, generic VisualElement invocation, save loading, save deletion, or UI-click commands.
- Document the command and its limitations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-012` provides the command bridge.
- `TWF-017` provides startup/log harness conventions.
- `TWF-019` binds the bridge to live Timberborn.
- `TWF-028` provides the research conclusion and safety boundary.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Run `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6` against a loaded Timberborn save when local game state is available.
- Capture `Player.log` evidence for `wildfire_command_request command=qa-readiness` and `wildfire_command_result command=qa-readiness success=true`.

## Notes

- This ticket intentionally replaces the idea of generic remote menu navigation with a narrow, safe command.
- If live Timberborn QA is not available, complete the code and unit tests, then record the missing live evidence and smallest unblock.

## Worker Notes

- 2026-05-01: Implemented `qa-readiness` as an allowlisted read-only bridge command in `TimberbornQaCommandBridge`.
- 2026-05-01: Added readiness fields to command result tokens: `bridge_alive`, `runtime_loaded`, and `loaded_game_ready`, while preserving existing status fields for `simulator_integrated`, dimensions, `tick_count`, `queued_changes`, and `last_delta_count`.
- 2026-05-01: `TimberbornFireRuntime` now reports whether the game-context runtime singleton has loaded, without touching Timberborn UI, save state, or the simulation grid.
- 2026-05-01: Updated `scripts/invoke-timberborn-command.ts` so `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6` is accepted by the local invoke allowlist.
- 2026-05-01: Updated `docs/TEST_PLAN.md` with command usage and limitations. `qa-readiness` is not a menu navigation, UI-click, save-load, save-delete, `VisualElement`, grid mutation, or destructive debug surface.

## Worker Evidence

- `git diff --check`: passed.
- `dotnet test`: passed, 71 tests.
- `dotnet build Wildfire.slnx`: passed.
- Live command attempt against the currently running Timberborn session reached the command bridge, but the deployed mod is older than this worktree: `Player.log` line 112 showed `wildfire_command_bridge_ready ... known_commands=help,status`.
- Live command attempt output: `wildfire_command_result command=qa-readiness success=false status=failure ... message=Unknown_command_'qa-readiness'.`
- `Player.log` captured `wildfire_command_request command=qa-readiness` at line 59423 and the matching unknown-command result at line 59424.

## Worker Blockers

- Live acceptance evidence for `wildfire_command_result command=qa-readiness success=true` is blocked by the currently running Timberborn session using the pre-TWF-029 deployed bridge.
- Smallest unblock: close Timberborn, deploy this worktree's mod with `bun scripts/deploy-timberborn-mod.ts --apply --clean` after required AssetBundles are present or rebuilt, restart Timberborn, load the Wildfire save, then run `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6` and capture the matching `Player.log` request/result tokens.

## Worker Revision Notes

- 2026-05-01: Fast-forwarded the ticket worktree from `f890bbc` to current `main` at `c3ad4db` before integration follow-up work.
- 2026-05-01: Fixed `help` to return the bridge's real `KnownCommands` list so `qa-readiness` is present in both the message and returned command list.
- 2026-05-01: Added focused bridge tests for `qa-readiness` success handling, request/result logging, known-command exposure, and token fields for `runtime_loaded`, `loaded_game_ready`, dimensions, `tick_count`, `queued_changes`, and `last_delta_count`.

## Worker Revision Evidence

- `git diff --check`: passed.
- `dotnet test`: passed, 73 tests.
- `dotnet build Wildfire.slnx`: passed.

## QA Evidence

- 2026-05-01: Confirmed `TWF-029` was still on current `main` with a clean working tree before QA.
- 2026-05-01: Closed Timberborn with `osascript -e 'tell application id "com.mechanistry.timberborn" to quit'`.
- 2026-05-01: Deployed the current Wildfire mod with `bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout=120`; build, AssetBundle generation, clean copy, and `deploy_complete target_dir=/Users/jasonkleinberg/Documents/Timberborn/Mods/Wildfire` all passed.
- 2026-05-01: Restarted Timberborn with `bun scripts/check-timberborn-startup.ts --launch --wait=120 --screenshot=always --lock-timeout=120`. The harness timed out because startup dialogs and save loading required manual QA actions inside the wait window, but it captured artifacts at `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/startup-harness/2026-05-01T18-18-42-045Z/`.
- 2026-05-01: Startup Mods dialog screenshot showed `Wildfire v0.1.0.0` visible and enabled at `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-029-live/current-screen.png`; clicked documented `startup_mods.ok`.
- 2026-05-01: Experimental Mode dialog was cleared with Return. This startup gate is not yet in `docs/timberborn-menu-coordinate-guide.md`.
- 2026-05-01: Loaded the Wildfire save from the documented main-menu Continue target. Loaded-save screenshot: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-029-live/after-continue.png`.
- 2026-05-01: Initial readiness probe passed while paused with `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6`, returning `tick_count=0`.
- 2026-05-01: Unpaused the loaded save with Space, waited for dispatch, then reran `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6`; result passed with `tick_count=15`.
- 2026-05-01: Added `--require-advanced-tick` to `scripts/invoke-timberborn-command.ts` so live QA can fail fast when the save is loaded but paused.
- 2026-05-01: Final QA command `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick` passed with `wildfire_command_result command=qa-readiness success=true status=success bridge_alive=true runtime_loaded=true loaded_game_ready=true simulator_integrated=true width=128 height=128 depth=23 tick_count=178 queued_changes=0 last_delta_count=0 message=loaded_game_ready`.

## QA Log Evidence

- `/Users/jasonkleinberg/Library/Logs/Mechanistry/Timberborn/Player.log:94`: `Loading saved game Wildfire testing - 2026-05-01 14h16m, Day 9-17.autosave at 2026-05-01 14:18:57Z`.
- `/Users/jasonkleinberg/Library/Logs/Mechanistry/Timberborn/Player.log:105`: `wildfire_timberborn_diagnostic_asset_loaded bundle=wildfire_diagnostic_mac asset=assets/wildfiregenerated/diagnostic.txt text_length=33`.
- `/Users/jasonkleinberg/Library/Logs/Mechanistry/Timberborn/Player.log:107`: `wildfire_timberborn_compute_asset_loaded bundle=wildfire_compute_mac asset=assets/wildfiregenerated/firesim.compute`.
- `/Users/jasonkleinberg/Library/Logs/Mechanistry/Timberborn/Player.log:113`: `wildfire_timberborn_runtime_simulator_initialized width=128 height=128 depth=23`.
- `/Users/jasonkleinberg/Library/Logs/Mechanistry/Timberborn/Player.log:115`: `wildfire_command_bridge_ready ... known_commands=help,qa-readiness,status`.
- `/Users/jasonkleinberg/Library/Logs/Mechanistry/Timberborn/Player.log:1900`: `wildfire_timberborn_gpu_dispatch_kernel_started kernel=SimulateFullGrid tick=178 groups=16x16x6 change_count=0`.
- `/Users/jasonkleinberg/Library/Logs/Mechanistry/Timberborn/Player.log:1904`: `wildfire_timberborn_gpu_readback_completed tick=178 delta_count=0`.
- `/Users/jasonkleinberg/Library/Logs/Mechanistry/Timberborn/Player.log:1905`: `wildfire_timberborn_dispatch_completed tick=178 delta_count=0 elapsed_ms=1.233`.
- `/Users/jasonkleinberg/Library/Logs/Mechanistry/Timberborn/Player.log:1908`: `wildfire_command_request command=qa-readiness`.
- `/Users/jasonkleinberg/Library/Logs/Mechanistry/Timberborn/Player.log:1909`: `wildfire_command_result command=qa-readiness success=true status=success bridge_alive=true runtime_loaded=true loaded_game_ready=true simulator_integrated=true width=128 height=128 depth=23 tick_count=178 queued_changes=0 last_delta_count=0 message=loaded_game_ready`.

## QA Acceptance

- Add `qa-readiness` to the bridge allowlist: pass. `Player.log:115` reports `known_commands=help,qa-readiness,status`.
- Keep the command read-only and non-destructive: pass. Validation used only the read-only command bridge; no grid mutation, save deletion, or generic UI command was used.
- Report bridge/runtime/simulator readiness and numeric simulator fields: pass. Final command returned `bridge_alive=true`, `runtime_loaded=true`, `loaded_game_ready=true`, `simulator_integrated=true`, `width=128`, `height=128`, `depth=23`, `tick_count=178`, `queued_changes=0`, and `last_delta_count=0`.
- Log request and result tokens: pass. `Player.log:1908` and `Player.log:1909` contain the required `wildfire_command_request` and successful `wildfire_command_result` tokens.
- Update local invocation support: pass. `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick` is accepted and passed.
- Document command limitations and live unpause expectation: pass. `docs/TEST_PLAN.md` now documents the `--require-advanced-tick` live QA guard and the need to unpause before requiring an advanced tick.

## QA Result

- Pass. Recommended board move: `04-verify` -> `05-integration`.
