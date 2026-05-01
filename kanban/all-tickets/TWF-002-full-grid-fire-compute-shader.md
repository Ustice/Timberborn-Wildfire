---
ticket: TWF-002
agent_level: High
role: worker
requires_qa: false
doc_only: false
dependencies:
   - TWF-001
write_scope:
   - src/Wildfire.Unity/**
   - docs/DESIGN.md
   - docs/TEST_PLAN.md
---

# TWF-002: Implement Full-Grid Fire Shader

## Goal

Implement the first `FireSim.compute` shader using the packed cell layout and full-grid dispatch. It should advance fire state on the GPU without any alternate C# simulation path.

## Why

Full-grid dispatch is the simplest useful GPU baseline. It proves the rule translation, packed-cell decoding, deterministic hash, neighbor reads, and buffer swapping before optimization adds frontier complexity.

## Requirements

- Add `FireSim.compute` with packed-cell helper functions for fuel, heat, flammability, water, terrain, and heat loss.
- Implement 6-neighbor reads for left, right, north, south, below, and above.
- Implement deterministic hash-based stochastic ignition in shader code.
- Implement the simple rule outline from `docs/DESIGN.md`.
- Dispatch over every cell in the grid.
- Write next cells into a separate output buffer.
- Swap buffers after dispatch from the Unity simulator wrapper.
- Do not add C# rule execution for parity.
- Document any shader-language differences from the design pseudocode.

## Dependencies

- `TWF-001` buffer/grid scaffold.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Run any Unity shader compile check available in the repo.

## Notes

- If shader execution cannot be tested in the current environment, record that limitation in the ticket notes and keep the implementation easy to verify later.

## Worker Notes - 2026-05-01

- Added `src/Wildfire.Unity/FireSim.compute` with a `SimulateFullGrid` kernel, packed-cell helpers, six in-bounds neighbor reads, deterministic hash ignition, full-grid bounds guards, separate `NextCells` writes, and padded compact delta appends.
- Added a Unity-side dispatch baseline that sends current/next/delta buffers, dimensions, tick, seed, and `8 x 8 x 4` full-grid thread group counts through `IFireSimComputeDispatcher`, then swaps current and next cell buffers after dispatch.
- Kept fire-spread rules out of C#; .NET tests assert dispatch shape and buffer swapping only.
- Documented shader pseudocode differences in `docs/DESIGN.md` and the current shader validation gap in `docs/TEST_PLAN.md`.
- Verification passed: `git diff --check`; `dotnet test` passed 31 tests; `dotnet build Wildfire.slnx` succeeded with 0 warnings and 0 errors.
- Shader compile/execution was not verified here. `dxc` and `glslangValidator` are not installed. Unity 6000.3.6f1 is installed, but this repository has no Unity `Assets`, `Packages`, or `ProjectSettings` project, and `UnityShaderCompiler` exposes only the internal compiler-service invocation rather than a standalone file compile command.
- Remaining blockers for live GPU proof: add a Unity project or CI shader compile harness, implement real UnityEngine compute-buffer binding/counter reset, add compact delta readback, and add external change upload in the follow-up ticket.
