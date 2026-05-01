---
ticket: TWF-010
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
   - TWF-004
   - TWF-008
write_scope:
   - src/Wildfire.Unity/**
   - src/Wildfire.Timberborn/**
   - docs/TEST_PLAN.md
---

# TWF-010: Add Runtime Diagnostics

## Goal

Add diagnostics for GPU simulator lifecycle, dispatch timing, queued changes, delta counts, readback, and Timberborn integration state.

## Why

GPU and live-game bugs can be hard to see from behavior alone. Diagnostics make failures actionable by showing whether data was uploaded, dispatched, read back, and consumed.

## Requirements

- Log simulator initialization dimensions and cell count.
- Log queued change counts before upload.
- Log dispatch tick number and elapsed time.
- Log compact delta counts after readback.
- Log listener notification counts.
- Log Timberborn adapter startup and shutdown.
- Keep logs concise and filterable.
- Avoid logging per-cell spam by default.
- Document where QA should look for diagnostics.

## Dependencies

- `TWF-004` compact delta readback.
- `TWF-008` Timberborn fixed-cadence dispatch.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.

## Notes

- Prefer structured log tokens that can be searched with `rg`.
