---
ticket: TWF-072
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-071
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-072-add-beaver-field-exposure-telemetry.md
---

# TWF-072: Add Beaver Field Exposure Telemetry

## Goal

Detect and report when beavers are in or near accepted wildfire fields without changing beaver behavior yet.

## Why

Before changing pathing, work, injury, or panic behavior, Wildfire needs a safe way to prove it can identify beaver exposure to fire, smoke, steam, ash, heat, and suppression fields. Telemetry gives QA a low-risk bridge between field simulation and beaver behavior.

## Requirements

- Implement the narrowest safe Timberborn adapter surface that can identify beaver positions or beaver-adjacent cells.
- Sample accepted wildfire fields from the existing visual-field or packed-cell surfaces without mutating the simulation grid.
- Classify exposure separately for respiratory danger, burn danger, contaminated smoke, toxic steam, and tainted aftermath where field data can support it.
- Report bounded telemetry for exposed beavers or candidate cells through status, `qa-readiness`, logs, or a dedicated QA command.
- Avoid per-beaver spam; aggregate counts and only include bounded sample details.
- Add deterministic tests for exposure classification where possible.
- Document QA commands and expected tokens in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-071` defines which field exposures matter.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start with the safest read-only beaver position surface available in the Timberborn adapter.
- Exposure can be sampled over multiple game ticks; it does not need to process every beaver in the same tick if batching avoids frame spikes.
- Keep detail samples bounded, such as a few beaver IDs, cells, and classifications, with aggregate counts for the rest.
- Expected counters include sampled beavers, exposed beavers, respiratory exposure cells, heat exposure cells, toxic exposure cells, tainted aftermath cells, batching skips, and unavailable API skips.
- Safe no-op behavior should report that beaver position sampling is unavailable without changing simulation or beaver state.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- QA must capture live loaded-save evidence showing exposure telemetry either detects a controlled beaver/field case or reports an explicit safe unavailable state.

## Notes

- This ticket should not alter beaver behavior. It is the instrumentation layer for later behavior changes.
- Relevant design reference: `docs/DESIGN.md` section 20, "Beaver Field Effects" and "Contamination Interaction".
