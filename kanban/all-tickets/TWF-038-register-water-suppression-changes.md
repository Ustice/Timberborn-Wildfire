---
ticket: TWF-038
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-032
  - TWF-035
write_scope:
  - src/Wildfire.Timberborn/**
  - scripts/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-038-register-water-suppression-changes.md
---

# TWF-038: Register Water Suppression Changes

## Goal

Add a narrow Timberborn adapter path that registers water or wetness changes with the GPU simulator and proves suppression changes affect later ticks.

## Why

The design includes water as a packed field and shows water dumped onto a target cell as an external change. Fire should not only spread; Timberborn needs a safe way to influence it through queued changes that the simulator applies on the next tick.

## Requirements

- Register suppression through `IGpuFireSimulator.RegisterChange`.
- Keep the command or adapter surface narrow and allowlisted.
- Reject arbitrary destructive commands and broad coordinate mutation.
- Preserve tick-boundary behavior: changes apply on the next dispatch tick.
- Expose command/status detail for accepted target, queued changes, tick advancement, and resulting deltas.
- Add deterministic tests for command validation, change registration, and next-tick behavior where possible.
- Document prerequisites and live evidence in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-032` proves live non-zero GPU deltas.
- `TWF-035` improves water and material mapping.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run typecheck` if TypeScript scripts change.
- Run `dotnet test`.
- QA must use a loaded, unpaused save and capture command output, copied `Player.log`, and status evidence showing queued suppression work and later dispatch.

## Notes

- This ticket should prove the interaction contract, not implement broad firefighting gameplay.

## Worker Notes

- Added QA-only `qa-water-suppression-stimulus` as the narrow Timberborn adapter path for suppression registration.
- The command accepts no arguments, chooses the deterministic center cell, and queues exactly one `SetWater=3` `FireSimChange` through `IGpuFireSimulator.RegisterChange`.
- Hardened QA-only simulator change commands so argument-bearing coordinate mutation attempts are rejected before runtime state is queried.
- Exposed `WaterChangedCount` as `last_delta_consumer_water_changed` in command status/readiness tokens.
- Added persistent `last_positive_water_changed_tick` and `last_positive_water_changed_count` status fields so command-side QA proof survives later zero-delta ticks.
- Extended `scripts/invoke-timberborn-command.ts` to allow the new command.
- Added `--require-water-changed` so live QA requires the persistent water-change proof instead of accepting `last_delta_count > 0`.
- Documented live QA prerequisites and evidence in `docs/TEST_PLAN.md`.
- Did not run live Timberborn QA; this ticket still requires QA after worker/review.

## Changed Files

- `src/Wildfire.Timberborn/TimberbornFireSystem.cs`
- `src/Wildfire.Timberborn/TimberbornDeltaConsumers.cs`
- `src/Wildfire.Timberborn/TimberbornFireRuntime.cs`
- `src/Wildfire.Timberborn/TimberbornQaCommandBridge.cs`
- `src/Wildfire.Timberborn/TimberbornQaCommandFileBridge.cs`
- `scripts/invoke-timberborn-command.ts`
- `tests/Wildfire.Core.Tests/TimberbornQaCommandBridgeTests.cs`
- `docs/TEST_PLAN.md`
- `kanban/all-tickets/TWF-038-register-water-suppression-changes.md`

## Worker Evidence

- `bun run typecheck`: passed.
- `dotnet test`: passed, 101 tests.
- `dotnet build Wildfire.slnx`: passed, 0 warnings, 0 errors.
- `git diff --check`: passed.
- 2026-05-02 Tech Lead review fix rerun: `git diff --check`, `bun run typecheck`, `dotnet test`, and `dotnet build Wildfire.slnx` passed after exposing `last_delta_consumer_water_changed` and adding `--require-water-changed`.
- 2026-05-02 Worker QA evidence fix rerun: `git diff --check` passed; `bun run typecheck` passed; `dotnet test` passed, 102 tests; `dotnet build Wildfire.slnx` passed, 0 warnings, 0 errors.

## Completion Details

- Deterministic tests cover command allowlisting/help, command result target/status fields including `last_delta_consumer_water_changed`, `last_positive_water_changed_tick`, and `last_positive_water_changed_count`, argument rejection for QA-only simulator change commands, direct `SetWater` registration, next-cadence-dispatch behavior, and stable positive-water evidence after a later zero-water dispatch.
- Live QA should run the TEST_PLAN `qa-water-suppression-stimulus` sequence against a loaded, unpaused save and capture command output plus `Player.log`; the follow-up command must pass `--require-water-changed` by reporting `last_positive_water_changed_count>0` or otherwise prove `water_changed=<nonzero>`.

## QA Evidence

2026-05-02 live QA result: fail.

- Fresh deploy: `bun scripts/deploy-timberborn-mod.ts --apply --clean --skip-asset-bundle --lock-timeout=120` passed after closing Timberborn under QA control. Deploy ran `dotnet build Wildfire.slnx --configuration Debug`, rebuilt the current managed assemblies, replaced `~/Documents/Timberborn/Mods/Wildfire`, copied the existing AssetBundles, and released the shared QA lock.
- First guarded launch: `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --lock-timeout=120` passed with artifact directory `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T19-26-00-068Z`; post-status reported `tick_count=3`.
- First stimulus command: `bun scripts/invoke-timberborn-command.ts qa-water-suppression-stimulus --wait=6 --require-advanced-tick` passed. The command result reported `tick_count=25`, `queued_changes=1`, `target_x=64`, `target_y=64`, `target_z=11`, `set_water=3`, and `queued_water_changes=1`.
- First required follow-up command: `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick --require-water-changed` failed. The command result reported `tick_count=32`, `queued_changes=0`, `last_delta_count=0`, and `last_delta_consumer_water_changed=0`.
- Retry guarded launch from a fresh runtime: `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --lock-timeout=120` passed with artifact directory `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T19-28-27-165Z`; post-status reported `tick_count=3`.
- Retry stimulus command: `bun scripts/invoke-timberborn-command.ts qa-water-suppression-stimulus --wait=6 --require-advanced-tick` passed. The command result in `Player.log` reported `tick_count=10`, `queued_changes=1`, `target_x=64`, `target_y=64`, `target_z=11`, `set_water=3`, and `queued_water_changes=1`.
- Retry follow-up command after attempting to pause shortly after the stimulus: `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick --require-water-changed` failed. The command result reported `tick_count=16`, `queued_changes=0`, `last_delta_count=0`, and `last_delta_consumer_water_changed=0`.
- Copied QA artifact directory: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-038-live-20260502T192652Z`.
- Artifact files: `01-qa-water-suppression-stimulus-outbox.txt`, `02-qa-readiness-require-water-changed-outbox.txt`, `04-retry-qa-readiness-require-water-changed-outbox.txt`, `Player.log`, `relevant-player-log-tokens.txt`, and `final-lock-state.txt`.
- Required `Player.log` registration and dispatch tokens were present in the retry log: `wildfire_command_request command=qa-water-suppression-stimulus`, `wildfire_timberborn_changes_registered source=qa_water_suppression count=1 pending_changes=1`, `wildfire_timberborn_qa_water_suppression_queued cell_index=188480 x=64 y=64 z=11 set_water=3 queued_water_changes=1`, `wildfire_timberborn_dispatch_completed tick=11 delta_count=1`, and `wildfire_timberborn_delta_consumer_completed tick=11 ... water_changed=1`.
- The deterministic center cell was not already at `water=3` in the fresh retry runtime: the next dispatch after the stimulus produced `delta_count=1` and `water_changed=1`.
- Final lock state: no `build-deploy.lock` file remained under `~/Library/Application Support/Timberborn/WildfireQA/locks`.

QA failure reason: the live runtime proves the queued `SetWater=3` change registers and produces a non-zero water delta on the next dispatch, but the required follow-up `qa-readiness --require-water-changed` command does not prove `last_delta_consumer_water_changed>0`. By the time the command bridge returns readiness, later zero-delta dispatches have overwritten the last-dispatch water counter.

Worker fix: status now retains the last positive water-change tick/count until runtime reset, and `--require-water-changed` checks `last_positive_water_changed_count` instead of the volatile last-dispatch counter.

Recommended board move: after review, move TWF-038 back to live QA retry; do not move to integration until the corrected command-side proof passes.

2026-05-02 live QA retry result: pass.

- Fresh deploy: `bun scripts/deploy-timberborn-mod.ts --apply --clean --skip-asset-bundle --lock-timeout=120` passed. Deploy ran `dotnet build Wildfire.slnx --configuration Debug`, rebuilt current managed assemblies, replaced `~/Documents/Timberborn/Mods/Wildfire`, copied existing AssetBundles, and released the shared QA lock.
- Guarded latest-save launch/unpause: `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --lock-timeout=120` passed with artifact directory `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T19-39-17-740Z`; post-status reported `tick_count=3`.
- Stimulus command: `bun scripts/invoke-timberborn-command.ts qa-water-suppression-stimulus --wait=6 --require-advanced-tick` passed. The command result reported `tick_count=9`, `queued_changes=1`, `target_x=64`, `target_y=64`, `target_z=11`, `set_water=3`, `queued_water_changes=1`, `last_positive_water_changed_tick=0`, and `last_positive_water_changed_count=0`.
- Required follow-up command: `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick --require-water-changed` passed. The command result reported `tick_count=11`, `queued_changes=0`, `last_delta_count=0`, `last_delta_consumer_water_changed=0`, `last_positive_water_changed_tick=10`, and `last_positive_water_changed_count=1`.
- Copied QA artifact directory: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-038-live-retry-20260502T194053Z`.
- Artifact files: `Player.log`, `qa-water-suppression-stimulus-command-result.txt`, `qa-readiness-require-water-changed-outbox.txt`, `qa-readiness-command-result-from-log.txt`, `relevant-player-log-tokens.txt`, and `final-lock-state.txt`.
- Required `Player.log` tokens were present in the retry log: `wildfire_command_request command=qa-water-suppression-stimulus`, `wildfire_timberborn_changes_registered source=qa_water_suppression count=1 pending_changes=1`, `wildfire_timberborn_qa_water_suppression_queued cell_index=188480 x=64 y=64 z=11 set_water=3 queued_water_changes=1`, `wildfire_timberborn_dispatch_completed tick=10 delta_count=1`, and `wildfire_timberborn_delta_consumer_completed tick=10 ... water_changed=1`.
- Strongest tick proof: the stimulus command observed `tick_count=9`; the next dispatch at tick `10` produced `delta_count=1` and consumer `water_changed=1`; the follow-up readiness command persisted `last_positive_water_changed_tick=10` and `last_positive_water_changed_count=1` after the later tick `11` settled back to `last_delta_count=0` and `last_delta_consumer_water_changed=0`.
- The deterministic center cell was not already at `water=3` in the retry runtime: the next dispatch after the stimulus produced `delta_count=1` and `water_changed=1`.
- Final lock state: no `build-deploy.lock` file remained under `~/Library/Application Support/Timberborn/WildfireQA/locks`.

Recommended board move: move TWF-038 from `04-verify` to `05-integration`; QA did not move board symlinks.
