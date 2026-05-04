---
ticket: TWF-064
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-046
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-064-investigate-building-burnout-pause-consequence.md
---

# TWF-064: Investigate Building Burnout Pause Consequence

## Goal

Make the building-burnout QA stimulus either apply the expected pausable-building pause consequence in a loaded Timberborn save, or report a precise safe reason why no pause can be applied.

## Why

`TWF-046` proved the coherent live loop, but two `qa-building-burnout-stimulus` attempts produced burned-out alert/status evidence while `building_burnout_applied_consequences` stayed `0`. The first attempt matched one building cell, so Sprint 5 hardening should verify whether this is a target-selection issue, a pausable-building lookup mismatch, an already-paused target, or an expected limitation that needs clearer telemetry.

## Requirements

- Reproduce the `TWF-046` building-burnout stimulus behavior from a loaded save.
- Preserve Timberborn as the adapter; do not move fire rules into Timberborn code.
- Identify why matched building cells do not produce applied pause consequences.
- If the fix is narrow and safe, update the Timberborn consequence path and deterministic tests.
- If the behavior is expected for the current save or target type, improve status or QA telemetry so the reason is explicit.
- Capture live QA evidence for one applied pause consequence or one explicit safe no-op reason.
- Update `docs/TEST_PLAN.md` only with durable validation expectations or accepted evidence.

## Dependencies

- `TWF-046` provides the live-loop artifact and observed mismatch.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start from `src/Wildfire.Timberborn/TimberbornBuildingBurnoutConsequences.cs`, `src/Wildfire.Timberborn/TimberbornFireRuntimeInitializer.cs`, `src/Wildfire.Timberborn/TimberbornFireRuntime.cs`, `src/Wildfire.Timberborn/TimberbornDeltaConsumers.cs`, and `src/Wildfire.Timberborn/TimberbornQaCommandBridge.cs`.
- Re-run the live path with `qa-building-burnout-stimulus`, then compare `queued_building_burnout_stimulus`, `wildfire_timberborn_qa_building_burnout_stimulus_queued`, `wildfire_timberborn_delta_consumer_summary`, and `status` or `qa-readiness` fields for considered, matched, and applied building-burnout counters.
- Keep the fix narrow: target selection may choose only unpaused `PausableBuilding` cells, the consequence sink may report matched-but-already-paused as an explicit safe no-op, but Timberborn must not become the owner of burn rules.
- A safe blocker is acceptable only if it names the exact failed lookup or target condition, such as no unpaused pausable building at the mapped cell, missing `IBlockService` result, or a save target that is not represented by `PausableBuilding`.
- QA evidence should preserve the command outbox, copied `Player.log`, final status line, selected target coordinates, and whether `last_delta_consumer_building_burnout_applied_consequences` became non-zero or an explicit no-op reason was logged.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- QA must capture live command/status evidence, copied `Player.log`, final lock state, and any screenshot needed to interpret the selected building target.

## Notes

- `TWF-046` artifact root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-046-live-20260502T232641Z/`.
- This follow-up should not reopen `TWF-046`; the coherent-loop gate already passed through fire start/spread, alert/status communication, gameplay consequence counters, water suppression, delayed stability, and a clean strict failure scan.
- Blocked on the live launch environment as of the `TWF-050` QA retry: Timberborn could not be launched or activated through Steam, with `frontmost_bundle_id=com.valvesoftware.steam` and Steam waiting on a launch-args prompt. This ticket requires loaded-save live QA before it can reproduce or verify the building-burnout pause consequence.
- Unblock by clearing the Steam/Timberborn launch prompt and proving `bun scripts/load-latest-save-and-unpause.ts --launch` can reach a command-responsive loaded save again.
- 2026-05-03 coordinator update: the shared blocker has moved past the Steam launch-args prompt. Normal-launched Timberborn can start, but the live command bridge still fails the `TWF-050` gate by leaving `command-inbox.txt` without producing `command-outbox.txt`; this ticket remains blocked until `TWF-050` restores command-responsive loaded-save QA.
- 2026-05-03 worker result in `~/repos/wildfire-TWF-064`: preserved `TWF-046` evidence showed the first burnout stimulus did apply a pause consequence at dispatch tick `1768`, but later `qa-readiness` sampled volatile last-dispatch fields at tick `1770` after they had fallen back to `0`. Worker added durable telemetry fields `last_positive_building_burnout_applied_tick` and `last_positive_building_burnout_applied_count`, mirroring the water-change proof pattern, and updated `docs/TEST_PLAN.md` so QA uses those durable fields plus the nonzero `Player.log` consumer token. Worker checks passed: targeted fire-delta/QA-command tests with 40 tests, full `dotnet test` with 133 tests, and `git diff --check`. Read-only live `qa-readiness` timed out in `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-064-worker-20260503T145647Z`; coordinator moved this ticket to `04-verify` for review before live QA rerun.
- 2026-05-03 review passed in `~/repos/wildfire-TWF-064`. Reviewer confirmed durable telemetry is correct and bounded, updates only when `BuildingBurnoutAppliedConsequenceCount > 0`, resets on runtime initialization/reset, surfaces through adapter status, and does not move fire rules into Timberborn. Reviewer also confirmed preserved `TWF-046` evidence has `building_burnout_applied_consequences=1` at tick `1768`, followed by volatile zero samples. Reviewer reran `git diff --check`, targeted fire-delta/QA-command tests with 40 tests, and full `dotnet test` with 133 tests. Live QA still must capture `last_positive_building_burnout_applied_count > 0` plus `wildfire_timberborn_delta_consumer_completed ... building_burnout_applied_consequences=<nonzero>`.
- 2026-05-03 live QA passed under active `caffeinate -disu`. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-064-live-20260503T150534Z`. `qa-building-burnout-stimulus` ran successfully; post-burnout readiness reported `last_positive_building_burnout_applied_tick=34` and `last_positive_building_burnout_applied_count=1`; copied `Player.log` contains `building_burnout_applied_consequences=1`. Cleanup left Timberborn stopped, no QA helpers, no QA locks, and no command inbox/outbox. Coordinator moved this ticket to `05-integration`.
- 2026-05-03 integration complete in main checkout. Durable `last_positive_building_burnout_applied_tick` and `last_positive_building_burnout_applied_count` now flow from `TimberbornFireDeltaConsumer` through `TimberbornFireSystem`, `TimberbornFireRuntime.GetState()`, and QA/status result tokens while preserving newer disabled-state and burn-damage work. Integration checks passed: `git diff --check` for owned files, targeted fire-delta/QA-command tests with 44 tests, and `dotnet build Wildfire.slnx` with 0 warnings and 0 errors. Coordinator moved this ticket to `06-done`.
