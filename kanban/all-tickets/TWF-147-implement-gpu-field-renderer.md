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
