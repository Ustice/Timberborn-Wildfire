---
ticket: TWF-005
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-002
write_scope:
  - src/Wildfire.Unity/**
  - docs/DESIGN.md
  - docs/TEST_PLAN.md
---

# TWF-005: Generate GPU Visual Fields

## Goal

Generate a GPU-side visual field from packed cell values. The output should support fire, smoke, ash, and heat visualization without creating one entity per cell.

## Why

The design depends on visuals staying close to the simulation buffers. A visual field lets rendering use GPU data directly while gameplay continues to consume compact deltas through C#.

## Requirements

- Add a visual output target such as `Texture2DArray<float4>` or an equivalent Unity-compatible abstraction.
- Derive fire intensity from burning state and heat.
- Derive smoke intensity from burning state, fuel, and heat.
- Derive ash intensity from low/no fuel and heat history approximation available in current data.
- Derive heat or visibility intensity for the alpha channel.
- Keep the visual field GPU-driven.
- Do not add stored flame, smoke, or ash fields to `PackedCell`.
- Document any temporary approximations.

## Dependencies

- `TWF-002` full-grid shader baseline.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Capture a visual artifact if Unity rendering is available.

## Notes

- This ticket does not need polished art. It needs the data path and enough output to prove the renderer can consume it.
- Worker implementation started in `~/repos/wildfire-TWF-005` on branch `codex/TWF-005-visual-fields`.
- Implemented visual output as a `float4`-equivalent `wildfire.visual_fields` buffer because the current solution has no UnityEngine texture/runtime binding.
- `FireSim.compute` now writes R/G/B/A visual samples from the post-step packed cell value: fire from burning state and heat, smoke from burning state plus fuel and heat, ash from terrain low/no fuel plus residual heat, and alpha from max visibility.
- Did not add flame, smoke, or ash fields to `PackedCell`.
- Did not add C# fire-spread parity.
- Real rendered artifact is blocked by the existing repository limitation: no Unity batchmode project, `UnityEngine.ComputeShader` dispatcher, standalone shader compiler, GPU texture binding, or visual readback runner.
- Integrated on `main` in commit `c5a8254`.
- Coordinator verification after integration: `git diff --check`, `dotnet test` with 51 tests, and sequential `dotnet build Wildfire.slnx` all passed.
