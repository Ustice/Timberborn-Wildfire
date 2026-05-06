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

- Record fire, smoke, ash, steam, visibility, and heat haze over a stable real-field scenario.
- Verify terrain alignment and that trees or terrain in front of the effect occlude correctly where the renderer supports it.
- Verify large-area rendering does not rely on tiny native prefabs or alert text.
- Capture status counters for renderer binding, visible regions, dropped regions, and failures.
- Record performance notes for normal camera and wide camera.
- File precise follow-up blockers for art tuning versus renderer correctness.

## Dependencies

- `TWF-133` can provide or validate the selected surface, but generated-scenario completeness should not block this gate if a better stable save is available.
- `TWF-147` implements the renderer.

## Role

- QA.
- Follow [../roles/qa.md](../roles/qa.md).

## Implementation Notes

- Capture normal gameplay camera first, then a wide camera angle for scale and performance notes.
- Use the same stable surface as `TWF-144` when possible. `TWF-156` owns the later Sprint 10 `256x256` proof.
- Separate renderer correctness failures from art tuning failures in the ticket notes.
- Alignment, occlusion, nonblank rendering, status counters, and absence of material failures are correctness.
- Color balance, density, blob shape, and haze strength are tuning unless they make the effect unreadable.
- Do not accept pooled-prefab-only screenshots as field-renderer evidence.

## Verification

- Run `git diff --check`.
- Live QA must preserve recordings, screenshots, command transcripts, copied `Player.log`, and final status output.

## Notes

- This gate decides whether visual tuning can proceed on the field renderer instead of the old prefab scaffold.
- 2026-05-05 coordinator correction: added missing `TWF-133` dependency. This ticket explicitly requires the generated QA scenario, and `TWF-133` is still the owner for making that generated scenario loadable and suitable for full-layout visual checks. The `TWF-147` Diorama proof accepts the renderer pipeline, but it is not a substitute for this gate's generated-scenario recording requirement.
- 2026-05-06 direction update: visuals are good enough to gate on stable real-field evidence rather than waiting on generated-scenario technicalities.
- 2026-05-06 coordinator: moved to `04-verify` for real-field visual QA. This should run on the same stable save used for `TWF-144` when possible. The separate `256x256` visual/performance proof belongs to Sprint 10 in `TWF-156`.
- 2026-05-06 QA result: failed required live QA on `Fuel` evidence root `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-144-148-live-20260506T071808Z/`. Status/log evidence showed field activity and no renderer failures, but actual renderer state reported `gpu_field_renderer_enabled=false` and `gpu_field_renderer_material_ready=false`. Recordings and extracted frames (`normal-live-frame-5s.png`, `normal-live-frame-12s.png`, `open-vegetation-frame-12s.png`) did not show visible field-rendered fire, smoke, ash, steam, or heat haze, so terrain alignment and occlusion remain unproven. Smallest unblock: make the normal gameplay field-renderer path visible or add a supported enablement/camera path, then rerun this gate on Fuel or another stable real save.
