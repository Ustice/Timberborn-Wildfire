---
ticket: TWF-001
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
   - TWF-000
write_scope:
   - src/Wildfire.Unity/**
   - tests/**
   - docs/ARCHITECTURE.md
---

# TWF-001: Scaffold Unity Compute Buffers

## Goal

Create the Unity-side buffer/grid scaffold needed to hold packed fire cells, queued changes, delta output, and visual fields. This ticket should make the GPU simulator implementation possible without yet implementing fire rules.

## Why

The GPU simulator needs stable buffer ownership before rule code can be useful. Separating buffer setup from rule implementation lets a worker focus on dimensions, allocation, upload, disposal, and data layout without also debugging shader behavior.

## Requirements

- Add a `ComputeBufferGrid` or equivalent type in `Wildfire.Unity`.
- Store width, height, depth, and cell count.
- Allocate current and next cell buffers using `uint` values with packed cells in the lower 16 bits.
- Add placeholders or stubs for queued changes, delta buffer, generation buffer, and visual field ownership.
- Provide explicit disposal for GPU resources.
- Add argument validation for dimensions and input cell counts.
- Add tests where possible for non-Unity logic, such as dimension math and validation.
- Do not implement fire-spread rules in C#.

## Dependencies

- `TWF-000` fixture export should exist so buffers can be initialized from fixture-style data.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run test`.
- Run `dotnet build Wildfire.slnx`.

## Notes

- Keep Unity-specific APIs isolated inside `Wildfire.Unity`.
- If Unity compute-buffer APIs are unavailable in the plain .NET build, create adapter interfaces and keep testable validation outside the Unity-only layer.
