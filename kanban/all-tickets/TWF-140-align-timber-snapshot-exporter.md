---
ticket: TWF-140
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-137
write_scope:
  - scripts/export-timberborn-map-fixture.ts
  - scripts/convert-wildfire-snapshot-binary.ts
  - tests/export-timberborn-map-fixture.test.ts
  - tests/convert-wildfire-snapshot-binary.test.ts
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-140-align-timber-snapshot-exporter.md
---

# TWF-140: Align Timber Snapshot Exporter

## Goal

Replace the heuristic `.timber` exporter with snapshot export that follows the shared material schema.

## Requirements

- Use the `TWF-137` schema for vegetation, crops, trees, buildings, storage, infrastructure, water, badwater, empty, and unknown entities.
- Export packed cell values plus companion field values.
- Preserve the existing binary conversion utility for packed cells, and extend metadata when companion field files are emitted.
- Report unresolved templates, unresolved resources, blocked placements, and skipped fields in the export summary.
- Keep TypeScript tests deterministic and runnable with `bun test`.

## Dependencies

- `TWF-137` defines the shared classification contract.

## Role

- Worker.
- Follow [../roles/worker.md](../roles/worker.md).

## Implementation Notes

- Do not parse live Timberborn APIs here. This ticket reads `.timber` archives only.
- Keep the old `packedCellValues.values` output compatible unless a test proves a format version bump is needed.
- Prefer small parser helpers over long inline branches so junior follow-up work can add templates safely.

## Verification

- Run `git diff --check`.
- Run `bun run typecheck`.
- Run `bun test`.

## Notes

- This ticket makes offline tests meaningful. Snapshot fixtures should stop being a different fake world from live Timberborn.
