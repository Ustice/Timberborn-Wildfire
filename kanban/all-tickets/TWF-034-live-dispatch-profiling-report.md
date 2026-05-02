---
ticket: TWF-034
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-032
write_scope:
   - scripts/**
   - docs/TEST_PLAN.md
   - docs/HANDOFF.md
   - kanban/all-tickets/TWF-034-live-dispatch-profiling-report.md
---

# TWF-034: Record Live Dispatch Profiling

## Goal

Capture enough live profiling evidence to decide whether active-frontier optimization is justified.

## Why

`TWF-011` adds real complexity. It should stay parked until full-grid dispatch has measured cost under a meaningful live workload. After `TWF-032` proves non-zero deltas, this ticket should turn existing diagnostics into a before-optimization decision record.

## Requirements

- Use live Timberborn evidence from a loaded, unpaused save.
- Include at least one run with the `TWF-031` stimulus and non-zero deltas.
- Capture dispatch elapsed time, readback elapsed time if available, tick counts, delta counts, queued changes, and relevant consumer counters.
- Summarize whether current full-grid dispatch is acceptable for the observed map size and workload.
- Recommend one of:

   - Keep full-grid dispatch for now.
   - Add more diagnostics before deciding.
   - Promote `TWF-011` to ready with specific performance evidence.

- Document commands, artifact paths, and interpretation in `docs/TEST_PLAN.md` or `docs/HANDOFF.md`.
- Add a small helper script only if existing logs/status commands are not enough.

## Dependencies

- `TWF-032` proves a meaningful non-zero live delta workload.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run typecheck` if any TypeScript changes are made.
- Run `dotnet test` if production code changes are made.
- QA must preserve the live artifact path, copied `Player.log`, command outputs, and final lock state.
- The final ticket notes must include a clear recommendation for `TWF-011`.

## Notes

- This ticket is allowed to conclude that `TWF-011` should remain deferred.
- Do not implement active-frontier buffers in this ticket.
