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

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- QA must capture live command/status evidence, copied `Player.log`, final lock state, and any screenshot needed to interpret the selected building target.

## Notes

- `TWF-046` artifact root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-046-live-20260502T232641Z/`.
- This follow-up should not reopen `TWF-046`; the coherent-loop gate already passed through fire start/spread, alert/status communication, gameplay consequence counters, water suppression, delayed stability, and a clean strict failure scan.
