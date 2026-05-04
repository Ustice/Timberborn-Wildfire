---
ticket: TWF-036
agent_level: High
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
  - kanban/all-tickets/TWF-036-apply-building-burnout-consequences.md
---

# TWF-036: Apply Building Burnout Consequences

## Goal

Bind one real building consequence to compact fire deltas, such as a reversible debug damage marker, disabled state, or narrowly scoped burnout notification.

## Why

The design lists building damage and destruction checks as primary delta consumers. `TWF-033` proves the first consequence path; this ticket makes that path useful for Timberborn buildings while preserving the rule that listeners react to deltas and enqueue follow-up changes instead of mutating simulation state directly.

## Requirements

- Consume only changed cells from compact deltas.
- Route through the existing Timberborn delta-consumer surface where possible.
- Keep fire rules and grid mutation outside the Timberborn adapter.
- Choose the smallest safe building consequence that can be proven in a live save.
- Expose status counters for considered deltas, matched building cells, and applied consequences.
- Add deterministic tests for the consequence decision and routing logic.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-033` proves a first consequence can be bound.
- `TWF-035` improves the material and building-cell mapping that this ticket consumes.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must use the guarded startup utility and live stimulus path, then capture command output, relevant `Player.log` lines, and status counters above zero.

## Notes

- Prefer a reversible or clearly bounded effect before destructive building changes.
- If no safe Timberborn-owned consequence API is available, move the ticket to blocked with the exact missing API and evidence.

## Worker Notes

- Implemented a bounded Timberborn-owned burnout consequence: compact deltas are converted to building-burnout consequence decisions, live Timberborn lookup uses `IBlockService.GetObjectsWithComponentAt<PausableBuilding>()`, and matched fuel-depleted pausable buildings receive `PausableBuilding.Pause()`.
- Kept fire rules and simulator mutation out of Timberborn: the adapter only reacts to changed-cell deltas and does not write fire-grid state.
- Exposed status/log counters for `building_burnout_considered_deltas`, `building_burnout_matched_cells`, and `building_burnout_applied_consequences`, plus matching `qa-readiness` / `status` token fields.
- Added deterministic tests for changed-cell routing, burnout decision conversion, and QA status token fields.
- Documented live QA expectations in `docs/TEST_PLAN.md`; QA still needs a save or stimulus that produces a fuel-depleted delta on a pausable building cell because the generic `qa-delta-stimulus` is not sufficient unless it targets a known building cell.

## Changed Files

- `src/Wildfire.Timberborn/Wildfire.Timberborn.csproj`
- `src/Wildfire.Timberborn/TimberbornBuildingBurnoutConsequences.cs`
- `src/Wildfire.Timberborn/TimberbornDeltaConsumers.cs`
- `src/Wildfire.Timberborn/TimberbornFireRuntime.cs`
- `src/Wildfire.Timberborn/TimberbornFireRuntimeInitializer.cs`
- `src/Wildfire.Timberborn/TimberbornQaCommandBridge.cs`
- `src/Wildfire.Timberborn/TimberbornQaCommandFileBridge.cs`
- `tests/Wildfire.Core.Tests/TimberbornFireDeltaConsumerTests.cs`
- `tests/Wildfire.Core.Tests/TimberbornQaCommandBridgeTests.cs`
- `docs/TEST_PLAN.md`
- `kanban/all-tickets/TWF-036-apply-building-burnout-consequences.md`

## Worker Verification

- `git diff --check`: passed.
- `dotnet test`: passed, 92 tests.
- `dotnet build Wildfire.slnx`: passed, 0 warnings and 0 errors.

## QA Required

- Requires live Timberborn QA after review.
- Capture `Player.log` proof of `wildfire_timberborn_delta_consequence_sink_bound lane=building_burnout_pause`.
- Capture a consumer pass with `building_burnout_considered_deltas`, `building_burnout_matched_cells`, and `building_burnout_applied_consequences` above zero.
- Capture `qa-readiness` or `status` output with the matching `last_delta_consumer_building_burnout_*` fields above zero.

## QA Attempt 2026-05-02

Result: failed live validation; recommend moving to `07-blocked`.

Artifact root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-036-live-20260502T184046Z`.

Commands run:

- `bun scripts/deploy-timberborn-mod.ts --apply --clean`
- `bun scripts/load-latest-save-and-unpause.ts --attach --wait=90 --artifacts-dir "~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-036-live-20260502T184046Z/latest-save-startup"`
- `bun scripts/invoke-timberborn-command.ts help --wait=6`
- `bun scripts/invoke-timberborn-command.ts status --wait=6`
- `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick`
- `bun scripts/invoke-timberborn-command.ts qa-delta-stimulus --wait=6 --require-advanced-tick`
- `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick --require-nonzero-delta`

Evidence gathered:

- Deploy was not safely applied. `deploy-apply-clean.txt` shows the deploy script acquired and released `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock`, detected `timberborn_running=true`, and refused to replace the deployed mod without explicit `--allow-open-game`.
- Live loaded-save attach passed. `latest-save-startup/2026-05-02T18-41-02-140Z/latest-save-startup-summary.txt` reports `wildfire_latest_save_startup_result=pass`, `observed_screens=loaded-save`, `simulator_integrated=true`, dimensions `128x128x23`, and `tick_count=1182`.
- Screenshots were captured in `latest-save-startup/2026-05-02T18-41-02-140Z/`: `00-current-screen-01.png`, `04-loaded-save-before-unpause.png`, and `05-loaded-save-after-unpause.png`.
- The safe command surface is still only `help`, `qa-readiness`, `qa-delta-stimulus`, and `status`. `help-sequential.txt` reports that only `qa-delta-stimulus` queues a fixed QA-only simulator change.
- The fixed stimulus queued cell `target_x=64`, `target_y=64`, `target_z=11`, `target_index=188480`, `set_cell=13311`; see `qa-delta-stimulus.txt`.
- The follow-up readiness probe proved a real compact delta: `qa-readiness-after-stimulus-require-nonzero.txt` reports `last_delta_count=1` and `last_delta_consumer_changed_cells=1`.
- `Player.log` was copied to `Player.log`, with extracted relevant lines in `player-log-relevant-tail.txt`. It shows `wildfire_timberborn_changes_registered source=qa_delta_stimulus count=1`, `wildfire_timberborn_qa_delta_stimulus_queued cell_index=188480 x=64 y=64 z=11 set_cell=13311`, and subsequent `wildfire_timberborn_dispatch_completed` rows with non-zero deltas.
- `player-log-building-burnout-search.txt` is empty: the copied `Player.log` contains no `wildfire_timberborn_delta_consequence_sink_bound lane=building_burnout_pause`, no `building_burnout_considered_deltas`, and no `last_delta_consumer_building_burnout_*` fields.
- Current live `status` / `qa-readiness` output does not include the new TWF-036 building-burnout counters. It reports only the older consumer fields such as `last_delta_consumer_fuel_depleted=0` and `last_delta_consumer_gameplay_consequences=0`.

Blocker:

- The current live setup cannot produce the required pass evidence safely. The game was already running, so the guarded deploy refused to update the mod. Against the currently running deployment, the TWF-036 sink/counters are absent. Even after a live loaded-save attach, the only non-read-only QA command is the fixed-center `qa-delta-stimulus`; it cannot target or identify a known pausable building cell and did not produce fuel depletion on a pausable building cell in this save.

Smallest next action:

- Close/restart Timberborn under QA control, deploy the current TWF-036 build with `bun scripts/deploy-timberborn-mod.ts --apply --clean`, then add or provide a targeted safe QA stimulus/save fixture that can produce a fuel-depleted compact delta on a known pausable building cell and report that cell in command output.

Final lock state:

- `~/Library/Application Support/Timberborn/WildfireQA/locks/` contained no lock files after the run.

## Worker QA Unblock 2026-05-02

- Added a narrow QA-only runtime command, `qa-building-burnout-stimulus`, to the command bridge allowlist when the live `TimberbornFireRuntime` is bound.
- The command has no user-supplied coordinates or arbitrary mutation path. It scans the initialized fire grid for the first cell where `IBlockService.GetObjectsWithComponentAt<PausableBuilding>()` returns a pausable building.
- After discovery, it queues exactly two ordered `SetCell` changes for that one cell through the existing `IGpuFireSimulator.RegisterChange` path: a building-like primed cell and then a fuel-zero cell. The existing apply kernel is intentionally ordered, so the next dispatch can emit a fuel-depleted compact delta for the pausable building cell.
- The command result reports `target_index`, `target_x`, `target_y`, `target_z`, `scanned_cells`, `primed_cell`, `set_cell`, and `queued_set_cell_changes=2`.
- The runtime logs `wildfire_timberborn_qa_building_burnout_stimulus_queued` plus `wildfire_timberborn_changes_registered source=qa_building_burnout_prime` and `source=qa_building_burnout_stimulus`.
- Added deterministic tests for the new command allowlist/routing and the exact two-change `SetCell` stimulus registration path.

## Worker Review Fix 2026-05-02

- Fixed the Tech Lead target-selection finding: `TimberbornPausableBuildingBurnoutConsequenceApi.FindTarget` now scans for the first unpaused pausable building, matching the `ApplyConsequence` requirement that only unpaused buildings can increment `building_burnout_applied_consequences`.
- If all pausable buildings found during the scan are already paused, the QA-only stimulus now reports no usable target instead of queueing a change that can produce `matched_cells>0` with `applied_consequences=0`.
- Added deterministic coverage for the scan helper to prove unusable earlier cells are skipped and the no-usable-target path throws the updated message.
- Updated `docs/TEST_PLAN.md` so QA expects the command to target an unpaused pausable building or report no usable target.
- Verification after the review fix: `git diff --check` passed, `dotnet test` passed with 94 tests, and `dotnet build Wildfire.slnx` passed with 0 warnings and 0 errors.

## QA Retry Expectations

- QA still owns closing/restarting Timberborn and redeploying the current build because the previous failed run proved the deployed mod was stale.
- After guarded startup and unpause, invoke the file bridge directly until `scripts/invoke-timberborn-command.ts` is approved for this new command:

```bash
printf 'qa-building-burnout-stimulus\n' > "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/command-inbox.txt"
```

- Capture the updated `command-outbox.txt` with `success=true`, the target fields listed above, and `queued_set_cell_changes=2`.
- Capture `Player.log` evidence for `wildfire_timberborn_delta_consequence_sink_bound lane=building_burnout_pause`, `wildfire_timberborn_qa_building_burnout_stimulus_queued`, both `qa_building_burnout_*` change-registration lines, and a subsequent `wildfire_timberborn_delta_consumer_completed` line with `building_burnout_considered_deltas>0`, `building_burnout_matched_cells>0`, and `building_burnout_applied_consequences>0`.
- Capture a follow-up `qa-readiness` or `status` result with `last_delta_consumer_building_burnout_considered_deltas>0`, `last_delta_consumer_building_burnout_matched_cells>0`, and `last_delta_consumer_building_burnout_applied_consequences>0`.

## QA Retry 2026-05-02

Result: passed live validation; recommend moving to `05-integration`.

Artifact root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-036-retry-20260502T190222Z`.

Commands run:

- Closed Timberborn under QA control with `osascript -e 'tell application id "com.mechanistry.timberborn" to quit'`.
- `bun scripts/deploy-timberborn-mod.ts --apply --clean`
- `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --artifacts-dir "$ARTIFACT_ROOT/latest-save-startup"`
- `printf 'qa-building-burnout-stimulus\n' > "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/command-inbox.txt"`
- `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=10 --require-advanced-tick`
- Direct file-bridge `status` polling after a second `qa-building-burnout-stimulus` to capture the follow-up command result before later zero-delta ticks overwrote the last-consumer counters.

Evidence gathered:

- `close-timberborn.txt` shows Timberborn was running at retry start and closed cleanly before deploy.
- `deploy-apply-clean.txt` shows the deploy lock was acquired, `timberborn_running=false`, `dotnet build Wildfire.slnx --configuration Debug` passed with 0 warnings and 0 errors, the mod target was removed and rewritten, all managed assemblies and compute bundles were copied, `deploy_complete` was printed, and the deploy lock was released.
- `latest-save-startup/2026-05-02T19-02-57-143Z/latest-save-startup-summary.txt` and `load-latest-save-and-unpause.txt` show the guarded launch path reached `loaded-save`, unpaused with `post_status_ok tick_count=3`, and released the shared lock. Screenshots include `04-loaded-save-before-unpause.png` and `05-loaded-save-after-unpause.png`.
- `qa-building-burnout-stimulus-outbox.txt` shows `success=true`, `simulator_integrated=true`, `target_index=24778`, `target_x=74`, `target_y=65`, `target_z=1`, `scanned_cells=24779`, `primed_cell=37628`, `set_cell=37616`, and `queued_set_cell_changes=2`.
- `Player.log` and `player-log-twf-036-relevant-final.txt` show `wildfire_timberborn_delta_consequence_sink_bound lane=building_burnout_pause`.
- `Player.log` shows `wildfire_timberborn_changes_registered source=qa_building_burnout_prime count=1 pending_changes=1`, `wildfire_timberborn_changes_registered source=qa_building_burnout_stimulus count=1 pending_changes=2`, and `wildfire_timberborn_qa_building_burnout_stimulus_queued cell_index=24778 x=74 y=65 z=1 scanned_cells=24779 primed_cell=37628 set_cell=37616 queued_set_cell_changes=2`.
- `Player.log` shows the required consumer pass at tick 19: `wildfire_timberborn_delta_consumer_completed tick=19 changed_cells=3 ... fuel_depleted=1 ... building_burnout_considered_deltas=3 building_burnout_matched_cells=3 building_burnout_applied_consequences=1`.
- A later `qa-readiness` probe after the first stimulus landed after the runtime had settled back to zero-delta ticks, so it did not satisfy the last-consumer field requirement by itself. To capture the command-side proof, QA invoked the same safe stimulus again and polled `status` through the file bridge.
- `second-qa-building-burnout-stimulus-outbox.txt` shows `success=true`, `target_index=24779`, `target_x=75`, `target_y=65`, `target_z=1`, `scanned_cells=24780`, `primed_cell=37628`, `set_cell=37616`, and `queued_set_cell_changes=2`.
- `status-positive-building-burnout-counters.txt` and `status-poll-after-second-building-burnout-stimulus.txt` show the required follow-up command result: `command=status success=true`, `tick_count=80`, `last_delta_count=3`, `last_delta_consumer_building_burnout_considered_deltas=3`, `last_delta_consumer_building_burnout_matched_cells=3`, and `last_delta_consumer_building_burnout_applied_consequences=1`.
- `Player.log` also shows the second proof pass at tick 80 with `building_burnout_considered_deltas=3`, `building_burnout_matched_cells=3`, and `building_burnout_applied_consequences=1`, followed by the positive `status` command at tick 80.

Final lock state:

- `final-lock-state.txt` shows no files under `~/Library/Application Support/Timberborn/WildfireQA/locks` after the run.

QA notes:

- No implementation files or board symlinks were changed by QA.
- The save had unpaused pausable building targets available; no no-target blocker was encountered.
