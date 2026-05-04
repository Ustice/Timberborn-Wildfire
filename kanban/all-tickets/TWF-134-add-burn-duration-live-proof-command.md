---
ticket: TWF-134
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-089
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-134-add-burn-duration-live-proof-command.md
  - kanban/all-tickets/TWF-089-tune-fuel-burn-down-duration.md
---

# TWF-134: Add Burn Duration Live Proof Command

## Goal

Add a QA-only live proof surface that can verify `TWF-089` burn-down duration in Timberborn for low, medium, and high fuel targets.

## Why

`TWF-089` has deterministic shader evidence for low, medium, and high fuel burn-down duration, but live QA could only use the fixed `qa-delta-stimulus` path. That command queues one hard-coded medium-ish cell and does not expose fuel selection, target identity, burn start ticks, depletion ticks, or durable per-target telemetry. The live recording gate therefore cannot prove the behavior `TWF-089` is meant to accept.

## Requirements

- Add an allowlisted QA command or command sequence for burn-duration proof.
- Support named low, medium, and high fuel targets without arbitrary user coordinates.
- Queue simulator changes through the existing safe QA change path; do not let Timberborn mutate the fire grid directly.
- Report target cell index and coordinates, initial fuel, burning start tick, depletion tick when observed, elapsed burn ticks, and timeout/no-depletion status.
- Expose durable status/readiness fields or command output that survives later ticks well enough for QA to sample after recording.
- Keep this Timberborn adapter local; do not move host-specific command logic into `Wildfire.Core`.
- Preserve existing `status`, `qa-readiness`, `qa-delta-stimulus`, `qa-building-burnout-stimulus`, and `qa-water-suppression-stimulus` behavior.
- Document the live proof command and evidence contract in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-089` supplies the accepted deterministic burn-duration tuning and defines the live proof need.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start from the failed live QA evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-089-live-20260503T162018Z/`.
- The current fixed `qa-delta-stimulus` queued center cell `188480`, `x=64 y=64 z=11`, `set_cell=13311`, and did not produce durable fuel-depletion proof through tick `77`.
- Prefer a command name that makes the proof intent obvious, such as `qa-burn-duration-stimulus`.
- The command may run one target at a time or queue a bounded set of low/medium/high targets, as long as the output is deterministic and reviewable.
- Do not broaden the command bridge into arbitrary coordinate or arbitrary packed-cell mutation.

## Verification

- Run `git diff --check`.
- Run targeted command-bridge and burn-duration tests.
- Run `dotnet test`.
- Live QA must run the new proof command under active `caffeinate -disu`, capture command/status output, copied `Player.log`, and recording evidence sufficient for `TWF-089` to retry.

## Notes

- 2026-05-03 coordinator: created after `TWF-089` live QA proved launch/readiness/recording are healthy but the existing bridge lacks low/medium/high fuel burn-duration proof controls and durable depletion telemetry.
- 2026-05-03 worker result: added allowlisted `qa-burn-duration-stimulus <low|medium|high>`. The command rejects arbitrary coordinates and packed values, queues one deterministic center-adjacent `SetCell` through the existing simulator change path, and exposes durable `burn_duration_proof_*` fields through `status` and `qa-readiness`: target, cell index/coordinates, initial fuel, queued tick, burn start tick, depletion tick, elapsed burn ticks, timeout ticks, timed-out flag, and proof status.
- 2026-05-03 worker verification passed: `dotnet test --filter TimberbornQaCommandBridgeTests` with 42 tests, full `dotnet test` with 195 tests, and `git diff --check`.
- 2026-05-03 coordinator: moved to `04-verify` for review before using this proof command to unblock `TWF-089` live QA.
- 2026-05-03 review passed: no blocking findings. Review confirmed `qa-burn-duration-stimulus <low|medium|high>` is allowlisted, rejects arbitrary coordinates and packed values, queues exactly one bounded `SetCell` through `TimberbornFireSystem`, keeps logic Timberborn-adapter-local, and exposes durable `burn_duration_proof_*` fields through `status` and `qa-readiness`. Review verification passed: `git diff --check`, `dotnet test --filter TimberbornQaCommandBridgeTests` with 42 tests, and full `dotnet test` with 195 tests.
- 2026-05-03 coordinator: moved to `06-done` so `TWF-089` can retry live QA with low, medium, and high targets. A `no_depletion_timeout` result should remain a `TWF-089` live QA failure, not an acceptance shortcut.
- 2026-05-03 worker: added allowlisted `qa-burn-duration-stimulus <low|medium|high>` support in the Timberborn command bridge. The command rejects arbitrary coordinates and packed values, queues exactly one bounded `SetCell` through `TimberbornFireSystem`, and records durable `burn_duration_proof_*` fields for target identity, coordinates, initial fuel, queued tick, burn start tick, depletion tick, elapsed burn ticks, timeout window, timeout flag, and status.
- 2026-05-03 worker: target bands are low fuel `4`, medium fuel `9`, and high fuel `15`; target cells are deterministic center-adjacent cells selected from the live grid. Existing `status`, `qa-readiness`, `qa-delta-stimulus`, `qa-building-burnout-stimulus`, and `qa-water-suppression-stimulus` behavior is preserved.
