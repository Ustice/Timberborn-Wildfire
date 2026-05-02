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

2026-05-02 worker pass on `codex/twf-034-live-dispatch-profiling`:

- Used existing live artifact directory `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-031-live-20260502T143543Z`; no duplicate live run was needed because it already contains the TWF-031 stimulus, copied `Player.log`, command outputs, and non-zero delta proof from the real Timberborn GPU path.
- Added `scripts/summarize-dispatch-profile.ts` to extract aggregate dispatch timing, kernel timing, delta/readback counts, command fields, and delta-consumer counters from a preserved `Player.log`.
- Profiling command:

   ```bash
   bun scripts/summarize-dispatch-profile.ts ~/Library/Application\ Support/Mechanistry/Timberborn/WildfireQA/twf-031-live-20260502T143543Z
   ```

- Summary output from the preserved artifact:

   ```text
   map=128x128x23
   ticks_observed=1..43
   dispatch_elapsed_ms count=43 min=0.469 median=2.737 p95=4.846 max=6.014 avg=2.84
   kernel SimulateFullGrid elapsed_ms count=43 min=0.015 median=0.019 p95=0.039 max=0.057 avg=0.021
   kernel ApplyExternalChanges elapsed_ms count=1 min=0.041 median=0.041 p95=0.041 max=0.041 avg=0.041
   readbacks count=43 nonzero=5 max_delta_count=2
   dispatches count=43 nonzero=5 max_delta_count=2
   consumers count=43 nonzero_changed_cells=5 max_changed_cells=2 max_visual_effect_events=2 max_gameplay_consequences=1 max_alerts=1
   commands_seen=status,status,qa-readiness,qa-delta-stimulus,qa-readiness
   latest_command_tick=34 queued_changes=0 last_delta_count=1 last_delta_consumer_changed_cells=1
   nonzero_dispatch_ticks=30:2@5.937ms,31:1@4.308ms,32:1@1.86ms,33:1@3.055ms,34:1@4.178ms
   ```

- Command evidence:

   - `qa-readiness-before-stimulus.txt` reported `tick_count=22`, `queued_changes=0`, and `last_delta_count=0`.
   - `qa-delta-stimulus.txt` reported `tick_count=29`, `queued_changes=1`, target `x=64 y=64 z=11`, and `set_cell=13311`.
   - `qa-readiness-after-stimulus-require-nonzero.txt` reported `tick_count=34`, `queued_changes=0`, `last_delta_count=1`, `last_delta_consumer_changed_cells=1`, and `last_delta_consumer_visual_effect_events=1`.
   - `status-after-nonzero-readiness.txt` later reported `tick_count=43`, `queued_changes=0`, and `last_delta_count=0`.

- `Player.log` evidence:

   - Tick `30` applied the stimulus with `wildfire_timberborn_gpu_queued_changes tick=30 queued_changes=1 valid_changes=1 ignored_changes=0`.
   - Tick `30` recorded `ApplyExternalChanges elapsed_ms=0.041`, `SimulateFullGrid elapsed_ms=0.016`, `wildfire_timberborn_gpu_readback_completed tick=30 delta_count=2`, and `wildfire_timberborn_dispatch_completed tick=30 delta_count=2 elapsed_ms=5.937`.
   - Tick `30` consumer telemetry recorded `changed_cells=2`, `visual_effect_events=2`, `gameplay_consequences=1`, and `alerts=1`.
   - Ticks `31..34` continued reporting non-zero deltas with wrapper elapsed times below `4.308 ms`.

- Interpretation: full-grid dispatch remains acceptable for this observed live workload. The total wrapper timing includes dispatch, readback, listener/consumer work, and logging; even there, p95 was under `5 ms` across `43` ticks and the single non-zero stimulus spike stayed near `6 ms`. The actual `SimulateFullGrid` kernel timing was far below the wrapper cost on this map.
- `TWF-011` recommendation: keep deferred for now. Do not promote active-frontier buffers until a larger map, sustained high-delta workload, or later consequence-heavy run shows full-grid dispatch or readback/consumer work as a measured bottleneck.
- Verification: `bun scripts/summarize-dispatch-profile.ts ~/Library/Application\ Support/Mechanistry/Timberborn/WildfireQA/twf-031-live-20260502T143543Z` passed; `bun run typecheck` passed; `git diff --check` passed. `dotnet test` was not run because no production code changed.
- Recommended board move: move `TWF-034` to `04-verify` for review/QA integration.

2026-05-02 tech-lead review:

- Findings: none blocking. The untracked `scripts/summarize-dispatch-profile.ts` was included in review with the tracked `docs/HANDOFF.md` and ticket diff.
- The preserved `TWF-031` artifact supports the profiling interpretation: it contains live loaded-save command outputs, copied `Player.log`, non-zero GPU readback/dispatch ticks, and consumer counters from the real Timberborn path.
- `TWF-011` should remain deferred. Current evidence shows low wrapper and kernel timings on the observed `128x128x23` workload, while `docs/HANDOFF.md` correctly calls for broader-map or sustained-load profiling before optimizing dispatch.
- Checks reviewed: `bun scripts/summarize-dispatch-profile.ts "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-031-live-20260502T143543Z"` passed; `bun run typecheck` passed; `git diff --check` passed. `dotnet test` remains unnecessary because no production code changed.
- Recommended board move: move `TWF-034` to `04-verify` or directly to `05-integration` if the coordinator treats this tech-lead pass as the verify gate. Keep `TWF-011` in `08-deferred`.
