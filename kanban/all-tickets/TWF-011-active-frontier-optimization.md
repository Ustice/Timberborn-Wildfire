---
ticket: TWF-011
agent_level: High
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-002
  - TWF-004
  - TWF-010
write_scope:
  - src/Wildfire.Unity/**
  - docs/DESIGN.md
  - docs/TEST_PLAN.md
---

# TWF-011: Add Active Frontier Optimization

## Goal

Add active frontier buffers and generation-stamped candidate deduplication to avoid scanning the full grid when profiling shows the full-grid shader is too expensive.

## Why

Full-grid dispatch is the right first baseline, but large maps may spend work on inactive cells. Frontier optimization should be added only after diagnostics show a real need, because it adds complexity to shader scheduling and correctness.

## Requirements

- Confirm with diagnostics or profiling that full-grid dispatch needs optimization before implementing.
- Add current and next active buffers.
- Add generation-stamped candidate deduplication.
- Include changed cells, still-active cells, and neighbors as candidates.
- Ensure newly activated cells are processed on the next tick, not recursively in the current tick.
- Preserve the same host-facing `IGpuFireSimulator` contract.
- Add shader snapshot coverage for frontier behavior.
- Document performance before and after.

## Dependencies

- `TWF-002` full-grid shader baseline.
- `TWF-004` compact delta readback.
- `TWF-010` runtime diagnostics.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run shader snapshots.
- Record profiling evidence that justifies the optimization.

## Notes

- This ticket is intentionally later work. Do not implement it just because the design mentions frontier optimization.
