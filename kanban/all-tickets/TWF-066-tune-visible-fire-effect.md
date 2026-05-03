---
ticket: TWF-066
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-065
write_scope:
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-066-tune-visible-fire-effect.md
---

# TWF-066: Tune Visible Fire Effect

## Goal

Make the live Timberborn fire effect clearly visible in normal gameplay camera angles without relying on alert text or status counters.

## Why

`TWF-040` through `TWF-046` prove the pooled native presentation path and coherent live loop, but the current bounded stimulus is too small and transient to produce a satisfying fire-effect recording. Fire needs its own visual tuning pass before release screenshots and Steam Workshop media.

## Requirements

- Use the existing pooled native effect path; do not create one entity per simulated fire cell.
- Prefer Timberborn-native fire prefabs and conventions before custom art.
- Tune only presentation concerns such as scale, placement, lifetime, intensity thresholds, candidate selection, or pool reuse.
- Keep fire rules in the GPU simulation.
- Capture high-resolution screen recordings and screenshots where the fire effect itself is legible.
- Preserve command output, copied `Player.log`, artifact paths, and final QA lock state.
- Document accepted fire-effect evidence in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-065` provides the recording tool.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- QA must capture high-resolution recording evidence plus `qa-readiness` or `status` evidence with active pooled fire effects, native prefab resolution, visible effects enabled, and zero presentation failures.

## Notes

- This ticket owns fire effect readability only. Smoke, ash, and fire behavior tuning have separate tickets.
