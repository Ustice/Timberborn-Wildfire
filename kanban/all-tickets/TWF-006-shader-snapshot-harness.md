---
ticket: TWF-006
agent_level: High
role: worker
requires_qa: false
doc_only: false
dependencies:
   - TWF-000
   - TWF-002
   - TWF-004
write_scope:
   - tests/**
   - src/Wildfire.Unity/**
   - docs/TEST_PLAN.md
---

# TWF-006: Add Shader Snapshot Harness

## Goal

Create automated shader snapshot coverage for seeded scenarios. The harness should run fixtures through the GPU simulator and compare packed cell grids, compact delta counts, and useful visual checksums.

## Why

Once the rules live only in shaders, shader snapshots become the main behavioral regression guard. They replace the old C# snapshot path and make future rule tuning reviewable.

## Requirements

- Load fixture data exported by `TWF-000`.
- Run the GPU simulator for a requested tick count.
- Record final packed cell grid in a stable order.
- Record per-tick compact delta counts.
- Record visual field checksum or artifact when available.
- Store accepted snapshots in a reviewable format.
- Make snapshot failures show enough detail for a worker to find the differing cell or tick.
- Document how to update snapshots intentionally.

## Dependencies

- `TWF-000` fixture export.
- `TWF-002` full-grid shader baseline.
- `TWF-004` compact delta readback.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run the shader snapshot command.

## Notes

- If shader execution cannot run in CI yet, create the harness shape and mark the execution blocker clearly in the ticket notes.
