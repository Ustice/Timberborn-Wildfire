---
ticket: TWF-148
agent_level: High
role: qa
requires_qa: true
doc_only: false
dependencies:
  - TWF-147
write_scope:
  - docs/TEST_PLAN.md
  - docs/HANDOFF.md
  - kanban/all-tickets/TWF-148-gate-visual-field-renderer.md
---

# TWF-148: Gate Visual Field Renderer

## Goal

Accept or block the GPU field renderer from normal gameplay recordings.

## Requirements

- Record fire, smoke, ash, steam, visibility, and heat haze over the generated QA scenario.
- Verify terrain alignment and that trees or terrain in front of the effect occlude correctly where the renderer supports it.
- Verify large-area rendering does not rely on tiny native prefabs or alert text.
- Capture status counters for renderer binding, visible regions, dropped regions, and failures.
- Record performance notes for normal camera and wide camera.
- File precise follow-up blockers for art tuning versus renderer correctness.

## Dependencies

- `TWF-147` implements the renderer.

## Role

- QA.
- Follow [../roles/qa.md](../roles/qa.md).

## Implementation Notes

- Capture normal gameplay camera first, then a wide camera angle for scale and performance notes.
- Separate renderer correctness failures from art tuning failures in the ticket notes.
- Alignment, occlusion, nonblank rendering, status counters, and absence of material failures are correctness.
- Color balance, density, blob shape, and haze strength are tuning unless they make the effect unreadable.
- Do not accept pooled-prefab-only screenshots as field-renderer evidence.

## Verification

- Run `git diff --check`.
- Live QA must preserve recordings, screenshots, command transcripts, copied `Player.log`, and final status output.

## Notes

- This gate decides whether visual tuning can proceed on the field renderer instead of the old prefab scaffold.
