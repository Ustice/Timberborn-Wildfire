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
