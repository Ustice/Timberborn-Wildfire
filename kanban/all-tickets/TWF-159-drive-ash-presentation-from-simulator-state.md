---
ticket: TWF-159
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-157
write_scope:
  - src/Wildfire.Unity/**
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-068-tune-visible-ash-effect.md
  - kanban/all-tickets/TWF-159-drive-ash-presentation-from-simulator-state.md
---

# TWF-159: Drive Ash Presentation From Simulator State

## Goal

Make ash rendering read simulator ash state without inventing, projecting, or authoring ash.

## Why

The ash model treats falling and settled ash as presentation states. Renderers should infer falling transitions from previous/current simulator ash inputs, while the simulator owns where ash exists.

## Requirements

- Remove projection from the ash overlay.
- Remove dynamic ash projection from `UpdateCompanionAsh` once simulator ash is authoritative.
- Render falling ash when the previous presentation input had no ash and the current simulator ash input has ash on a floor cell.
- Render settled ash when the previous and current simulator ash inputs both have ash.
- Use interpolation, lerp, and smoothstep for presentation only.
- Keep renderer failures isolated from gameplay consequences.
- Add deterministic or shader-harness coverage proving the renderer reads ash values without changing ash state.
- Capture live visual QA showing falling and settled ash on a real Timberborn surface.

## Dependencies

- `TWF-157` provides simulator-owned ash state.

## Role

- Worker.
- Follow [../roles/worker.md](../roles/worker.md).

## Implementation Notes

- Coordinate with `TWF-068`: that ticket remains the visual-tuning lane, while this ticket changes the source of truth and presentation contract.
- Avoid one entity per ash cell.
- Keep the primary proof in deterministic or shader tests before live QA.

## Verification

- Run `git diff --check`.
- Run `dotnet test Wildfire.slnx --no-restore`.
- Run `bun run typecheck`.
- Run the Unity shader harness if shader behavior changes.
- QA must capture high-resolution recording evidence plus status/log proof that simulator ash exists for the rendered region.

## Notes

- This ticket should not add new simulator ash rules; it consumes `TWF-157`.
