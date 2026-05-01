---
ticket: TWF-018
agent_level: High
role: worker
requires_qa: false
doc_only: false
dependencies:
   - TWF-006
write_scope:
   - src/Wildfire.Unity/**
   - tests/**
   - docs/TEST_PLAN.md
---

# TWF-018: Add Unity Compute Shader Execution Harness

## Goal

Provide a real execution path for `FireSim.compute` so shader snapshots can prove compile, dispatch, readback, and visual-field behavior instead of only testing wrapper contracts.

## Why

The current shader snapshot harness is intentionally blocked because the repo lacks a Unity batchmode project, `UnityEngine.ComputeShader` dispatcher, standalone shader compiler, or GPU readback runner. Until this exists, GPU behavior remains unproven.

## Requirements

- Choose and document the execution strategy: Unity batchmode project, standalone compatible runner, or another concrete shader execution path.
- Compile or load `FireSim.compute` through the chosen path.
- Dispatch at least one seeded fixture from `TWF-000`.
- Read back packed cells, compact deltas, and visual fields where supported by the current shader interface.
- Connect the runner to the existing shader snapshot harness instead of adding C# fire-spread parity.
- Keep the host-facing `IGpuFireSimulator` contract unchanged unless a ticket explicitly widens it.
- Make failures actionable with logs that name compile, dispatch, buffer, and readback phases.
- Document local prerequisites and CI limitations.

## Dependencies

- `TWF-006` shader snapshot harness.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run the shader execution harness against at least one fixture.
- Record whether the harness can run in CI or only local Unity-equipped environments.

## Notes

- Do not implement a CPU fire-spread fallback to make snapshots pass.
- If Unity installation or licensing blocks execution, update this ticket with the smallest concrete environment requirement.
