---
ticket: TWF-003
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
   - TWF-001
write_scope:
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
---

# TWF-003: Add CPU Snapshot Suite

## Goal

Create stable deterministic snapshots for seeded CPU simulation scenarios.

## Requirements

- Add snapshot coverage for at least three starter scenarios.
- Assert same seed and initial grid produce identical snapshots.
- Record how snapshots should later be compared against GPU output.
- Update `docs/TEST_PLAN.md` only for validation procedure details.

## Dependencies

- TWF-001 should settle CPU tick semantics first.

## Role

- Worker using [../roles/worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run test`.

## Notes

- Keep snapshots compact enough to review in diffs.
- Worker update 2026-04-30: Added CPU snapshot coverage for `single-ignition-point`, `water-barrier`, and `vertical-fuel-column` starter scenarios in `tests/Wildfire.Core.Tests/CpuSnapshotTests.cs`.
- Worker evidence 2026-04-30: Each scenario runs the same seed, dimensions, tick count, and initial packed cells twice and asserts identical formatted snapshots before comparing against the checked-in baseline.
- Worker evidence 2026-04-30: Documented future GPU comparison procedure in `docs/TEST_PLAN.md` using CPU packed-cell snapshots as the oracle.
- Worker verification 2026-04-30: `git diff --check` passed.
- Worker verification 2026-04-30: `bun run test` passed with 16 tests.
- Worker blockers 2026-04-30: No blockers found; GPU output comparison remains future work because no GPU readback suite exists in this ticket scope.
