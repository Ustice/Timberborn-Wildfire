---
ticket: TWF-017
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-013
   - TWF-016
write_scope:
   - scripts/**
   - docs/TEST_PLAN.md
---

# TWF-017: Add Live QA Startup Log Harness

## Goal

Create a repeatable command that launches or attaches to Timberborn QA, waits for startup evidence, and captures the logs/screenshots needed to prove Wildfire loaded.

## Why

Manual UI access is useful, but the sprint needs a durable smoke test that shows whether the deployed mod is actually running. This should make later live tickets faster and less ambiguous.

## Requirements

- Use Bun and TypeScript for any new script.
- Reuse documented coordinates from `TWF-013` when UI automation is needed.
- Serialize launch, deploy, and log capture with a local QA lock.
- Capture `Player.log` and any Wildfire-specific log paths named by the deploy or command bridge tickets.
- Wait for searchable success or failure tokens instead of relying only on elapsed time.
- Save screenshots only when they support a visible claim.
- Fail loudly when Timberborn is not running, the resolution does not match, or expected log evidence is missing.
- Document the command, prerequisites, output paths, and known limitations.

## Dependencies

- `TWF-013` supplies reliable UI coordinates.
- `TWF-016` supplies a deployed mod worth detecting.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run any script help or dry-run mode with `bun`.
- Run live Timberborn QA and capture evidence when the local game state is available.

## Notes

- This ticket should not load saves or trigger gameplay. `TWF-015` owns loading the latest save.
- Keep the first version focused on startup and mod-load evidence.

## Worker Results - 2026-05-01

- Added `scripts/check-timberborn-startup.ts`, a Bun/TypeScript startup harness with attach and launch modes.
- The harness reuses the shared deploy/QA lock at `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock`.
- The harness validates the documented `1920x1080` display resolution by default, activates `com.mechanistry.timberborn`, waits for required `Player.log` startup tokens, optionally requests read-only command-bridge `status`, and writes evidence bundles under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/startup-harness/<timestamp>/`.
- The harness does not click UI targets, load saves, unpause, or write screenshots unless a failure happens or `--screenshot=always` is passed.
- Updated `docs/TEST_PLAN.md` with command, prerequisites, output path, default tokens, and limitations.

Live evidence:

- Command: `bun scripts/check-timberborn-startup.ts --attach --require-command-status --wait=10`.
- Result: passed.
- Evidence file: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/startup-harness/2026-05-01T15-26-06-436Z/startup-evidence.txt`.
- Evidence captured `missing_tokens=none` and `missing_command_status=none`.
- Startup tokens captured included `wildfire_timberborn_diagnostic_asset_loaded`, `wildfire_timberborn_compute_asset_loaded`, `wildfire_timberborn_gpu_factory_created`, `wildfire_timberborn_runtime_ready`, `wildfire_timberborn_runtime_simulator_initialized`, and `wildfire_command_bridge_ready`.
- Command status evidence captured `wildfire_command_result command=status success=true status=success simulator_integrated=true width=128 height=128 depth=23 tick_count=13806 queued_changes=0 last_delta_count=0 message=ok`.
- Screenshot was not captured because the log and command output were sufficient.

Verification:

- `bun scripts/check-timberborn-startup.ts --help` passed.
- `bun scripts/check-timberborn-startup.ts --dry-run --skip-resolution-check --wait=1` passed.
- `bun scripts/check-timberborn-startup.ts --dry-run --wait=1` passed and validated `1920x1080`.
- `bun scripts/check-timberborn-startup.ts --attach --require-command-status --wait=10` passed.
- `git diff --check` passed.
- `dotnet test` passed: 71 tests.

Blockers:

- None for the scoped startup log harness.

## Revision Results - 2026-05-01

- Fixed stale startup proof: the harness now captures `Player.log` baseline existence, byte length, and mtime after acquiring the shared QA lock and before attach or launch work.
- Success and failure evidence now comes only from the current log window after that baseline. If the log is truncated or rotated during the run, the replacement file is treated as the current window.
- Fixed failure handling: failure tokens after the baseline now prevent success, are written to `startup-evidence.txt`, and are included in the loud failure message.
- Fixed command evidence reporting so stale command outbox text is not copied or displayed when no command was sent in the current run.

Revision verification:

- `bun scripts/check-timberborn-startup.ts --help` passed.
- `bun scripts/check-timberborn-startup.ts --dry-run --wait=1` passed and validated `1920x1080`.
- `git diff --check` passed.
- Targeted live attach check: `bun scripts/check-timberborn-startup.ts --attach --require-command-status --wait=2 --screenshot=never` failed as expected because Timberborn was already running and no fresh startup tokens were emitted after the baseline.
- Live attach evidence file: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/startup-harness/2026-05-01T15-32-07-538Z/startup-evidence.txt`.
- Live attach evidence captured `player_log_baseline_size=20085453`, all startup tokens missing in the current window, `missing_command_status=not_requested`, and no stale command outbox text.
- Synthetic current-window failure-token check passed: a temp `Player.log` containing old success tokens before baseline and current success plus `wildfire_timberborn_compute_asset_load_failed` after baseline exited with code 1 and reported `failure Player.log evidence`.

Revision blockers:

- No code blockers. A passing current-window live startup proof requires launching or restarting Timberborn so the startup tokens are emitted after the harness baseline; the already-running attach check correctly rejects stale startup evidence.

## QA Closeout - 2026-05-01

- Re-reviewed the staged worker changes before integration and kept the lock path aligned with the existing deploy script at `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock`.
- Re-ran `bun scripts/check-timberborn-startup.ts --help`, `bun scripts/check-timberborn-startup.ts --dry-run --skip-resolution-check --wait=1`, `git diff --check`, and `dotnet test`.
- Accepted the ticket because the current-window stale-log guard, dry-run behavior, docs, and automated tests pass, and the ticket already contains live evidence from a successful loaded-save attach run plus an expected failing stale-attach run.

## Fresh Startup Proof - 2026-05-01

- Jason approved restarting Timberborn because the game session was only being used for ad hoc play.
- Fresh launch command: `bun scripts/check-timberborn-startup.ts --launch --wait=300 --screenshot=failure`.
- During the run, QA clicked through the startup Mods dialog, the Experimental Mode Information screen, and the main-menu `Continue` target, then unpaused the loaded save.
- Result: passed.
- Evidence file: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/startup-harness/2026-05-01T17-53-26-758Z/startup-evidence.txt`.
- Evidence captured `wildfire_startup_harness_result=pass`, `missing_tokens=none`, and `missing_command_status=not_requested`.
- Startup tokens captured in the current log window included `wildfire_timberborn_gpu_factory_created`, `wildfire_timberborn_runtime_ready`, `wildfire_timberborn_diagnostic_asset_loaded`, `wildfire_timberborn_compute_asset_loaded`, `wildfire_timberborn_runtime_simulator_initialized width=128 height=128 depth=23`, and `wildfire_command_bridge_ready`.
- Loaded-save status check: `bun scripts/invoke-timberborn-command.ts status --wait=6` returned `wildfire_command_result command=status success=true status=success simulator_integrated=true width=128 height=128 depth=23 tick_count=65 queued_changes=0 last_delta_count=0 message=ok`.
- Live `Player.log` after unpause showed `wildfire_timberborn_dispatch_completed` and `wildfire_timberborn_gpu_readback_completed`.
- Blockers: none.
