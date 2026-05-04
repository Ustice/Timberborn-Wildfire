---
ticket: TWF-142
agent_level: High
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-138
write_scope:
  - src/Wildfire.Core/**
  - src/Wildfire.Unity/**
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/ARCHITECTURE.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-142-add-runtime-fire-sim-parameters.md
---

# TWF-142: Add Runtime Fire Sim Parameters

## Goal

Move shader tuning constants into a typed runtime parameter object uploaded to the GPU.

## Requirements

- Add a `FireSimParameters` contract for cadence-independent shader behavior.
- Cover ignition base heat, water ignition penalty, retained heat, spread heat, burning-neighbor bonus, direct neighbor heat, water suppression, evaporation threshold, flammability burn pressure, water burn pressure penalty, burn heat, fuel burn-down numerator and denominator, random roll seed, cooling behavior, and visual field weights.
- Upload parameters to `FireSim.compute` without changing the default behavior.
- Keep deterministic shader snapshots stable for default parameters.
- Add one non-default test preset proving parameters actually affect output.
- Expose current parameter values through bounded status or test inspection.

## Dependencies

- `TWF-138` provides the broader field binding context.

## Role

- Worker.
- Follow [../roles/worker.md](../roles/worker.md).

## Implementation Notes

- Do not expose player settings in this ticket.
- Do not tune values here beyond preserving the current default and proving one override.
- Keep C# and HLSL field ordering explicitly tested to avoid silent buffer-layout bugs.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Run Unity batchmode shader snapshots for default and one override fixture.

## Notes

- This is the prerequisite for real tuning. Shader `#define` edits are too blunt once we need repeatable generated-scenario tuning.
