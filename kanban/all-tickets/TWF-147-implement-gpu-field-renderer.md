---
ticket: TWF-147
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-138
  - TWF-142
write_scope:
  - src/Wildfire.Unity/**
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-147-implement-gpu-field-renderer.md
---

# TWF-147: Implement GPU Field Renderer

## Goal

Replace large-area dependence on pooled native prefabs with a renderer driven by GPU visual and companion fields.

## Requirements

- Render fire, smoke, ash, steam, visibility, and heat haze from GPU field data.
- Use real field intensity and aftermath values, not per-cell prefab selection as the primary renderer.
- Keep pooled native prefabs only as optional debug or special-case highlights.
- Support large map areas without one Unity object per burning cell.
- Expose renderer status counters: bound fields, visible cells or regions, dropped regions, material failures, and active preset.
- Preserve occlusion and alignment with Timberborn terrain as a live QA requirement.

## Dependencies

- `TWF-138` provides companion field state.
- `TWF-142` provides runtime visual parameters.

## Role

- Worker.
- Follow [../roles/worker.md](../roles/worker.md).

## Implementation Notes

- Start with the existing `VisualFields` buffer and add whatever companion field binding the renderer needs.
- Avoid per-cell GameObject creation.
- Do not tune final art in this ticket; make the renderer real and inspectable.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Live QA must capture screenshots or recordings proving visible field rendering over a real imported area.

## Notes

- The old pooled effect proved visibility could happen. This ticket makes visibility scale with the field.
- 2026-05-05: Started the primary GPU field renderer path. `TimberbornGpuFieldRendererSink` now samples the bound GPU visual-field surface, batches changed cells into bounded regions, derives steam and heat-haze channels from visual samples, and sends those regions to a single Unity mesh presenter. The pooled native-prefab path remains wired in parallel as an optional debug or special-case highlight.
- 2026-05-05: Added renderer telemetry to `status` and `qa-readiness`: renderer enabled, material ready, surface bound, visible regions, updated regions, last nonzero updated regions/tick, max regions, dropped regions, material failures, and last updated tick.
- 2026-05-05 deterministic verification passed: `git diff --check`, focused renderer/visual-field/pooled-effect tests, `bun run typecheck`, `bun test`, `dotnet test Wildfire.slnx`, and `dotnet build Wildfire.slnx`.
- 2026-05-05 moved to `04-verify` for the required live QA screenshot or recording gate. Do not mark done until Timberborn evidence proves visible field rendering over a real imported area with the new `gpu_field_renderer_*` status counters.
