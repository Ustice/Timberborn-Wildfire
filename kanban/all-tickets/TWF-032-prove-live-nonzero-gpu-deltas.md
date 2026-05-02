---
ticket: TWF-032
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-031
write_scope:
   - src/Wildfire.Unity/**
   - src/Wildfire.Timberborn/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-032-prove-live-nonzero-gpu-deltas.md
---

# TWF-032: Prove Live Non-Zero GPU Deltas

## Goal

Use the guarded live stimulus from `TWF-031` to prove that Timberborn receives non-zero compact deltas from the real GPU simulator.

## Why

Current live evidence proves dispatch, readback, status, and delta-consumer hook wiring, but all observed live delta counts are zero. Before binding more gameplay consequences or optimizing dispatch, the sprint needs proof that a loaded Timberborn save can produce and report non-zero changed cells.

## Requirements

- Run the `TWF-031` stimulus against a loaded, unpaused Timberborn save.
- Prove `wildfire_timberborn_gpu_readback_completed` reports `delta_count > 0`.
- Prove `wildfire_timberborn_dispatch_completed` reports `delta_count > 0`.
- Prove `status` or `qa-readiness` reports `last_delta_count > 0` after the stimulus.
- Preserve compact-delta readback semantics; do not replace the GPU path with a fake C# simulator or dispatch-only shortcut.
- Add or adjust deterministic tests only where they protect production behavior touched by this ticket.
- Document the accepted live evidence in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-031` provides the safe live stimulus.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run typecheck`.
- Run `dotnet test`.
- QA must capture live command output, `Player.log` lines, artifact directory paths, and screenshot/status evidence.
- Passing live evidence requires numeric `delta_count > 0` from the real Timberborn GPU path.

## Notes

- This ticket proves non-zero simulator output, not final player-facing gameplay consequences.
- If deltas remain zero, treat that as a simulation/input/debugging result, not as a QA flake.
