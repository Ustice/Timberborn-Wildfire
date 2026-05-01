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
- Worker evidence 2026-05-01:
   - Added no-op-by-default Unity GPU diagnostic sink with `wildfire_gpu_simulator_initialized`, `wildfire_gpu_simulator_queued_changes`, `wildfire_gpu_simulator_dispatch_started`, `wildfire_gpu_simulator_dispatch_completed`, `wildfire_gpu_simulator_readback_started`, `wildfire_gpu_simulator_readback_completed`, and `wildfire_gpu_simulator_listeners_notified`.
   - Added live Timberborn GPU diagnostics for simulator initialization/disposal, queued change batches, dispatch kernel start/completion elapsed timing, readback counter/counts, and listener notification counts.
   - Added adapter startup/shutdown tokens `wildfire_timberborn_adapter_started`, `wildfire_timberborn_adapter_stopping`, and `wildfire_timberborn_adapter_stopped`.
   - Replaced mapped-cell per-change logging with one aggregate `wildfire_timberborn_changes_registered source=mapped_cell count=... pending_changes=...` line; single external/heat registrations still log concise counts without cell-index spam.
   - Updated `docs/TEST_PLAN.md` with the runtime diagnostic search tokens QA should use in `Player.log`.
   - Verification passed: `git diff --check`, `dotnet test` (`71` passed), and `dotnet build Wildfire.slnx` (`0` warnings, `0` errors).
   - Recommended board move: `04-verify`.
- Reviewer revision evidence 2026-05-01:
   - Fixed `wildfire_timberborn_gpu_readback_counter` to report the current dispatch/readback tick instead of `_tick + 1`.
   - Reduced pre-existing `wildfire_timberborn_dispatch_waiting` noise by logging once per cadence interval instead of every non-dispatch game update.
   - Verification rerun passed: `git diff --check`, `dotnet test` (`71` passed), and `dotnet build Wildfire.slnx` (`0` warnings, `0` errors).
   - Recommended board move remains `04-verify`.
