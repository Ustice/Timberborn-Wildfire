---
ticket: TWF-031
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-019
   - TWF-030
write_scope:
   - src/Wildfire.Timberborn/**
   - scripts/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-031-live-nonzero-delta-stimulus.md
---

# TWF-031: Add Guarded Live Delta Stimulus

## Goal

Create the smallest safe QA-only path that can produce at least one non-zero simulator delta in a loaded Timberborn save.

## Why

The live simulator, command bridge, startup automation, and delta-consumer hooks are now wired, but current live evidence still reports `last_delta_count=0`. The next sprint needs a controlled way to make the simulator do observable work before optimizing it or binding broad gameplay consequences.

## Requirements

- Add a narrow allowlisted QA command or utility path for a deterministic fire stimulus.
- Keep the simulation core host-agnostic.
- Keep Timberborn as an adapter; it may register allowed external changes but must not own fire rules or mutate the grid directly.
- Prefer an explicit, bounded target such as one known loaded-save cell or a small fixed test patch.
- Reject unknown commands, broad arbitrary coordinates, destructive actions, and generic debug execution.
- Report enough command/status detail to identify the stimulus request, accepted target, queued change count, and subsequent simulator tick.
- Preserve existing read-only command behavior.
- Document the command, safety boundary, prerequisites, and expected evidence in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-019` provides the allowlisted command bridge surface.
- `TWF-030` provides reliable startup/load/unpause automation for live QA.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run typecheck`.
- Run `dotnet test`.
- Run any relevant command-script help or dry-run checks with `bun`.
- QA must run the stimulus in live Timberborn only after the save is loaded through the guarded startup utility.
- QA evidence must include command output, copied `Player.log`, and final lock state.

## Notes

- This ticket is not a gameplay tuning ticket.
- Do not add broad UI automation or arbitrary map editing.
- If no safe deterministic target can be chosen from current runtime data, stop with a blocker note that names the exact missing API or evidence.

## Worker Notes

- 2026-05-02: Implemented `qa-delta-stimulus` as a live-runtime-only allowlisted command. The default/placeholder bridge remains read-only with only `help`, `qa-readiness`, and `status`; the file bridge exposes `qa-delta-stimulus` only when bound to `TimberbornFireRuntime`.
- 2026-05-02: The stimulus has no user-supplied coordinates or payload. `TimberbornFireSystem.QueueFixedQaDeltaStimulus()` chooses the deterministic center cell of the initialized `FireGrid`, registers one fixed hot/flammable `SetCell` through `IGpuFireSimulator.RegisterChange`, and logs `wildfire_timberborn_qa_delta_stimulus_queued` with the accepted target.
- 2026-05-02: Updated `scripts/invoke-timberborn-command.ts` to allow `qa-delta-stimulus` and added `--require-nonzero-delta` for the follow-up `status` or `qa-readiness` proof after a subsequent simulator tick.
- 2026-05-02: Updated `docs/TEST_PLAN.md` with the command safety boundary, prerequisites, expected command sequence, and required live `Player.log` tokens.
- 2026-05-02: Coordinator review follow-up: added deterministic tests for default read-only command construction, live-bound allowlist expansion, `qa-delta-stimulus` result target fields, and exact one-change center-cell `SetCell` queueing through `TimberbornFireSystem`.

## Evidence

- `git diff --check`: passed.
- `bun scripts/invoke-timberborn-command.ts --help`: passed and lists `qa-delta-stimulus` plus `--require-nonzero-delta`.
- `bun scripts/invoke-timberborn-command.ts arbitrary-debug-command --wait=0`: failed as expected before writing a command, with `Unknown command`.
- `bun install`: passed in the fresh ticket worktree to install the local TypeScript toolchain.
- `bun run typecheck`: passed after `bun install`; passed again after review-follow-up tests.
- `dotnet test`: passed initially, 78 tests; passed after review-follow-up tests, 82 tests.
- Review follow-up focused check: `dotnet test --filter FullyQualifiedName~TimberbornQaCommandBridgeTests` passed, 17 tests.

## Blockers

- 2026-05-02 QA: resolved. Live Timberborn QA ran after deploy/restart/load/unpause and captured accepted stimulus, queue, dispatch/readback, `last_delta_count > 0`, copied `Player.log`, and final lock state.

## Completion Details

- Smallest safe deterministic target found: the initialized live fire grid's center cell, computed from current runtime dimensions rather than supplied by a command argument.
- Architecture boundary preserved: `Wildfire.Core` was not changed; Timberborn remains an adapter that queues an external simulator change and does not mutate Timberborn save state, UI state, or GPU buffers directly.
- Existing read-only behavior preserved for default bridge construction and existing read-only commands.

## Tech-Lead Review

- 2026-05-02: Reviewed current diff for architecture boundaries, QA command safety, allowlisting, evidence, and integration risk.
- Finding: the new mutable `qa-delta-stimulus` bridge path has no deterministic unit coverage yet. Existing bridge tests still cover only read-only command behavior, unknown-command rejection, and status/readiness token formatting, so the new allowlist expansion, fixed-target result token, failure mode, and exact one-change queueing behavior are not pinned before live QA.
- Checks reviewed: `git diff --check`, `bun scripts/invoke-timberborn-command.ts --help`, `bun scripts/invoke-timberborn-command.ts arbitrary-debug-command --wait=0`, `bun run typecheck`, and `dotnet test`.
- Board recommendation: keep in `03-in-progress` until focused unit tests cover the new command path, then move to `04-verify` for live Timberborn QA evidence.
- 2026-05-02 follow-up: P2 resolved. Reviewed the added deterministic bridge/system coverage for read-only default construction, live-bound command allowlisting, stimulus result target fields, and exact one-change center `SetCell` registration. Checks reviewed: `git diff --check`, `dotnet test --filter FullyQualifiedName~TimberbornQaCommandBridgeTests`, `bun run typecheck`, and `dotnet test`. Board recommendation: move to `04-verify` for live Timberborn QA evidence.

## QA Evidence

- 2026-05-02 live QA result: PASS for TWF-031 acceptance.
- Worktree: `~/repos/wildfire-TWF-031`.
- Artifact directory: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-031-live-20260502T142908Z`.
- Startup artifact directory with screenshots: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T14-27-11-942Z`.
- First guarded startup attempt stopped safely on a macOS system alert, released the lock, and saved evidence at `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T14-26-02-547Z`.
- Alert cleared: unrelated macOS `Unable to Connect to "Jason's iPad Pro (2)"` timeout; no Timberborn coordinate click was sent through that overlay.
- Commands run:

   - `git diff --check`
   - `osascript -e 'tell application id "com.mechanistry.timberborn" to quit'`
   - `bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout=120`
   - `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --lock-timeout=120`
   - `bun scripts/load-latest-save-and-unpause.ts --attach --wait=240 --lock-timeout=120`
   - `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick`
   - `bun scripts/invoke-timberborn-command.ts qa-delta-stimulus --wait=6 --require-advanced-tick`
   - `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick`
   - `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick --require-nonzero-delta`
   - Second tight poll to catch the `last_delta_count` window: `bun scripts/invoke-timberborn-command.ts qa-delta-stimulus --wait=6 --require-advanced-tick`, then repeated `bun scripts/invoke-timberborn-command.ts status --wait=2 --require-advanced-tick`.

- Command output artifacts:

   - `qa-delta-stimulus.txt`: first live stimulus accepted at `tick_count=27`, `queued_changes=1`, target `target_index=188480`, `target_x=64`, `target_y=64`, `target_z=11`, `set_cell=13311`.
   - `qa-readiness-after-stimulus.txt`: later readiness showed subsequent simulator tick `tick_count=35`, drained queue `queued_changes=0`, and `last_delta_consumer_debug_visual_cells=1`.
   - `qa-readiness-require-nonzero-delta.txt`: initial late follow-up missed the narrow nonzero `last_delta_count` window and reported `last_delta_count=0`.
   - `second-tight-stimulus-status-poll.txt`: second bounded stimulus accepted at `tick_count=101`, then status poll captured `tick_count=102`, `queued_changes=0`, `last_delta_count=2`, `last_delta_consumer_changed_cells=2`, `last_delta_consumer_started_burning=1`, `last_delta_consumer_visual_effect_events=2`, `last_delta_consumer_gameplay_consequences=1`, and `last_delta_consumer_alerts=1`.

- Copied log artifacts:

   - `Player.log`
   - `Player-final.log`
   - `player-log-stimulus-key-lines.txt`
   - `player-log-stimulus-window.txt`
   - `player-log-final-key-lines.txt`
   - `player-log-twf031-tokens.txt`

- Key `Player-final.log` evidence:

   - `wildfire_command_bridge_ready ... known_commands=help,qa-delta-stimulus,qa-readiness,status`
   - `wildfire_command_request command=qa-delta-stimulus`
   - `wildfire_timberborn_changes_registered source=qa_delta_stimulus count=1 pending_changes=1`
   - `wildfire_timberborn_qa_delta_stimulus_queued cell_index=188480 x=64 y=64 z=11 set_cell=13311`
   - `wildfire_command_result command=qa-delta-stimulus ... tick_count=27 queued_changes=1 ... message=queued_fixed_center_stimulus_target_index=188480_target_x=64_target_y=64_target_z=11_set_cell=13311`
   - `wildfire_timberborn_gpu_queued_changes tick=28 queued_changes=1 upload_capacity=376832 valid_changes=1 ignored_changes=0`
   - `wildfire_timberborn_gpu_readback_completed tick=28 delta_count=2`
   - `wildfire_timberborn_dispatch_completed tick=28 delta_count=2 elapsed_ms=99.630`
   - Second tight poll: `wildfire_command_result command=status ... tick_count=102 queued_changes=0 last_delta_count=2 last_delta_consumer_changed_cells=2 ... last_delta_consumer_alerts=1 message=ok`

- Acceptance criteria:

   - Narrow allowlisted QA command exists and is exposed live: PASS.
   - Stimulus has no arbitrary coordinates or payload and reports a deterministic bounded target: PASS.
   - Timberborn adapter queues an external simulator change without mutating Timberborn terrain, saves, UI, or simulator buffers directly: PASS based on command contract plus `source=qa_delta_stimulus` registration and one queued change.
   - Existing loaded-game readiness and read-only command behavior preserved: PASS; `qa-readiness` returned `success=true`, `loaded_game_ready=true`, `simulator_integrated=true`, dimensions `128x128x23`, and advancing ticks.
   - Accepted target, queued change count, subsequent simulator tick/readback evidence visible: PASS.
   - `last_delta_count > 0` captured: PASS; command status captured `last_delta_count=2` at `tick_count=102`; `Player.log` also captured compact readback `delta_count=2`.

- Final lock state: no files present under `~/Library/Application Support/Timberborn/WildfireQA/locks`; deploy/startup locks were released after each run.
- Board recommendation: move TWF-031 to `05-integration`. Do not wait on TWF-032 for TWF-031 acceptance; TWF-032 can build on this by binding a visible gameplay consequence or longer-lived proof flow, but the safe stimulus path and nonzero readback evidence are live-proven here.

### Replacement QA Evidence 2026-05-02T14:35Z

- 2026-05-02 replacement live QA result: PASS for TWF-031 acceptance.
- Worktree: `~/repos/wildfire-TWF-031`.
- Artifact directory: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-031-live-20260502T143543Z`.
- Startup artifact directory with screenshots: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T14-34-46-986Z`.
- Startup/load/unpause result: guarded utility passed through startup Mods and Experimental Mode gates, loaded the latest save, clicked `hud.speed1`, and returned `post_status_ok tick_count=3`.
- Commands run:

   - `git diff --check`
   - `osascript -e 'tell application id "com.mechanistry.timberborn" to quit'`
   - `bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout=120`
   - `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --lock-timeout=120`
   - `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick`
   - `bun scripts/invoke-timberborn-command.ts qa-delta-stimulus --wait=6 --require-advanced-tick`
   - `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick --require-nonzero-delta`
   - `bun scripts/invoke-timberborn-command.ts status --wait=6 --require-advanced-tick`

- Command output artifacts:

   - `qa-readiness-before-stimulus.txt`: baseline readiness passed with `loaded_game_ready=true`, `simulator_integrated=true`, dimensions `128x128x23`, `tick_count=22`, `queued_changes=0`, and `last_delta_count=0`.
   - `qa-delta-stimulus.txt`: live stimulus accepted at `tick_count=29`, `queued_changes=1`, target `target_index=188480`, `target_x=64`, `target_y=64`, `target_z=11`, and `set_cell=13311`.
   - `qa-readiness-after-stimulus-require-nonzero.txt`: follow-up readiness after subsequent simulator ticks passed `--require-nonzero-delta` with `tick_count=34`, `queued_changes=0`, `last_delta_count=1`, `last_delta_consumer_changed_cells=1`, `last_delta_consumer_debug_visual_cells=1`, and `last_delta_consumer_visual_effect_events=1`.
   - `status-after-nonzero-readiness.txt`: later status stayed healthy at `tick_count=43`, `queued_changes=0`; as expected after the narrow nonzero window, `last_delta_count` had returned to `0`.

- Copied log artifacts:

   - `Player.log`
   - `player-log-twf031-tokens.txt`

- Key `Player.log` evidence:

   - `wildfire_command_bridge_ready ... known_commands=help,qa-delta-stimulus,qa-readiness,status`
   - `wildfire_command_request command=qa-delta-stimulus`
   - `wildfire_timberborn_changes_registered source=qa_delta_stimulus count=1 pending_changes=1`
   - `wildfire_timberborn_qa_delta_stimulus_queued cell_index=188480 x=64 y=64 z=11 set_cell=13311`
   - `wildfire_command_result command=qa-delta-stimulus ... tick_count=29 queued_changes=1 ... message=queued_fixed_center_stimulus_target_index=188480_target_x=64_target_y=64_target_z=11_set_cell=13311`
   - `wildfire_timberborn_gpu_queued_changes tick=30 queued_changes=1 upload_capacity=376832 valid_changes=1 ignored_changes=0`
   - `wildfire_timberborn_gpu_readback_completed tick=30 delta_count=2`
   - `wildfire_timberborn_dispatch_completed tick=30 delta_count=2 elapsed_ms=5.937`
   - `wildfire_command_result command=qa-readiness ... tick_count=34 queued_changes=0 last_delta_count=1 last_delta_consumer_changed_cells=1 last_delta_consumer_debug_visual_cells=1 last_delta_consumer_visual_effect_events=1 ... message=loaded_game_ready`

- Acceptance criteria:

   - Narrow allowlisted QA command exists and is exposed live: PASS.
   - Stimulus has no arbitrary coordinates or payload and reports a deterministic bounded target: PASS.
   - Timberborn adapter queues an external simulator change without mutating Timberborn terrain, saves, UI, or simulator buffers directly: PASS based on command contract plus `source=qa_delta_stimulus` registration and one queued change.
   - Existing loaded-game readiness and read-only command behavior preserved: PASS; `qa-readiness` returned `success=true`, `loaded_game_ready=true`, `simulator_integrated=true`, dimensions `128x128x23`, and advancing ticks before and after stimulus.
   - Accepted target, queued change count, subsequent simulator tick/readback evidence visible: PASS.
   - `last_delta_count > 0` captured: PASS; follow-up `qa-readiness` captured `last_delta_count=1` at `tick_count=34`, and `Player.log` captured compact readback `delta_count=2` at `tick=30`.

- Final lock state: no files present under `~/Library/Application Support/Timberborn/WildfireQA/locks`; deploy/startup locks were released after each run.
- Board recommendation: move TWF-031 to `05-integration`. The safe stimulus request is accepted and queues the bounded change, and nonzero live delta proof succeeded in this ticket. TWF-032 should own follow-up visible gameplay binding or longer-lived consequence proof, not the TWF-031 acceptance gate.
