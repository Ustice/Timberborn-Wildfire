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
- Worker update 2026-05-01:
   - Added `Wildfire.Unity` shader snapshot harness contracts for fixture loading, buffer-grid creation from CLI fixture cells, accepted snapshot JSON serialization, snapshot comparison diffs, and an explicit blocked executor.
   - Added deterministic .NET coverage for fixture loading from `TWF-000` CLI fixture JSON, stable reviewable snapshot output, final-cell/per-tick-delta/visual diff messages, and the current execution blocker.
   - Did not add C# fire-spread parity rules; the harness only defines the execution boundary and validates shape with fake/blocking executors.
   - Current blocker: the repository still has no Unity batchmode project, `UnityEngine.ComputeShader` dispatcher, or standalone compute-shader compiler/readback runner, so `FireSim.compute` cannot execute in CI yet.
   - Evidence: `git diff --check` passed; `git diff --cached --check` passed; `dotnet test --filter FullyQualifiedName~ShaderSnapshotHarnessTests` passed 4 tests; `dotnet test` passed 47 tests.
   - Integrated on `main` in commit `1844b47`.
   - Coordinator verification after integration: `git diff --check`, `dotnet test` with 47 tests, and sequential `dotnet build Wildfire.slnx` all passed.
   - Tech-lead follow-ups for real GPU execution enablement: validate executor capture metadata/cardinality before writing accepted snapshots, and add malformed fixture coverage with source/property-specific errors.
