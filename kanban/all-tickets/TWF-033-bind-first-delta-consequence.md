---
ticket: TWF-033
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-032
write_scope:
  - src/Wildfire.Timberborn/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-033-bind-first-delta-consequence.md
---

# TWF-033: Bind First Delta Consequence

## Goal

Bind one concrete Timberborn-facing consequence to non-zero fire deltas and prove it in a loaded save.

## Why

`TWF-009` created the delta-consumer hook surfaces and telemetry, but live evidence still has zero consequence counters because no non-zero deltas were produced. Once `TWF-032` proves changed cells, this ticket should make one narrow result visible or gameplay-relevant without turning Timberborn into the simulation owner.

## Requirements

- Choose one first consequence lane: debug visual state, pooled visual effect, building/resource gameplay consequence, or user-facing alert.
- Bind through the existing Timberborn delta-consumer sink surface where possible.
- Keep fire rules and grid mutation out of the Timberborn adapter.
- Apply work only for changed cells from compact deltas.
- Expose or preserve status counters so QA can see the consequence count move above zero.
- Add deterministic tests for the consequence decision/routing logic where possible.
- Document live validation expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-032` proves live non-zero GPU deltas are available.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx` if Timberborn adapter bindings change.
- QA must use the guarded startup utility and live stimulus path, then capture status counters, screenshots when visual behavior is expected, and copied `Player.log`.
- Passing evidence requires at least one relevant `last_delta_consumer_*` counter greater than zero.

## Notes

- Prefer the smallest reversible consequence that proves the path.
- Do not broaden into full gameplay tuning, balance, or multiple effect systems in this ticket.
- Worker note: selected the debug visual state lane because it is the smallest reversible consequence already represented by the delta-consumer sink surface. The runtime now binds `TimberbornFireDebugVisualStateSink` through `TimberbornFireDeltaConsumerSinks`, so only compact changed-cell deltas can update that adapter-facing state.
- Deterministic test note: existing `TimberbornFireDeltaConsumerTests.ConsumeRoutesChangedCellsToAdapterSinksAndSummarizesTelemetry` already covers changed-cell routing into the debug visual sink surface and telemetry. I did not add new tests because this worker write scope does not include `tests/**`.
- Live QA expectation: after `qa-delta-stimulus`, accept `Player.log` proof of the non-zero dispatch and delta-consumer pass plus a follow-up command result where the persistent sink-backed `last_delta_consumer_debug_visual_cells` counter is greater than `0`. The follow-up command may show `last_delta_count=0` if later simulator ticks have already settled the stimulus.
- Worker evidence 2026-05-02:
  - `git diff --check` passed.
  - `dotnet test` passed: 82 tests.
  - `dotnet build Wildfire.slnx` passed with 0 warnings and 0 errors.
  - Deployed the final adapter with `bun scripts/deploy-timberborn-mod.ts --apply --clean --skip-asset-bundle --lock-timeout=120`.
  - Loaded and unpaused the latest save with `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240`; artifact directory: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T14-49-26-796Z`.
  - `bun scripts/invoke-timberborn-command.ts qa-delta-stimulus --wait=6 --require-advanced-tick` returned `success=true`, `simulator_integrated=true`, `tick_count=49`, `queued_changes=1`, and `last_delta_consumer_debug_visual_cells=1`.
  - `Player.log` showed `wildfire_timberborn_delta_consequence_sink_bound lane=debug_visual_state`.
  - `Player.log` showed `wildfire_timberborn_dispatch_completed tick=50 delta_count=2` and `wildfire_timberborn_delta_consumer_completed tick=50 changed_cells=2 debug_visual_cells=1 started_burning=1 ... gameplay_consequences=1 alerts=1`.
  - `bun scripts/invoke-timberborn-command.ts status --wait=6 --require-advanced-tick` later returned `success=true`, `simulator_integrated=true`, `tick_count=62`, and `last_delta_consumer_debug_visual_cells=1`; `last_delta_count` had already returned to `0` by that later status sample.
- Worker follow-up 2026-05-02:
  - Revised `docs/TEST_PLAN.md` and this ticket to explicitly accept the final-code evidence shape: non-zero dispatch plus consumer `Player.log` pair, followed by persistent sink-backed command status.
  - Did not recapture a non-zero command result because the live command polling can miss the transient `last_delta_count` window while still proving the consequence sink persisted.

## Tech-Lead Review

- 2026-05-02: Implementation is narrow and adapter-bound, but verification should capture the post-stimulus non-zero `last_delta_count` and `last_delta_consumer_debug_visual_cells` command output or update the test-plan wording to rely on the dispatch/consumer log pair plus persistent sink-backed status. Current copied startup artifact log ends before the stimulus; the later proof is in the live `Player.log` and current outbox.
- 2026-05-02 follow-up: P2 resolved by revising `docs/TEST_PLAN.md` and ticket evidence to accept the actual proof shape: non-zero dispatch and consumer `Player.log` tokens plus later persistent sink-backed status.
