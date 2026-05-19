---
ticket: TWF-068
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-065
  - TWF-066
  - TWF-067
write_scope:
  - src/Wildfire.Timberborn/**
  - src/Wildfire.Unity/**
  - tests/Wildfire.Core.Tests/**
  - docs/DESIGN.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-068-tune-visible-ash-effect.md
---

# TWF-068: Tune Visible Ash Effect

## Goal

Make ash or residual burn aftermath visually legible from simulator-owned ash state.

## Why

Ash needs its own tuning pass because the right presentation differs from active fire and smoke. [docs/ash-simulation-model.md](../../docs/ash-simulation-model.md) now settles that renderers should present simulator ash state rather than inventing ash through projection or a separate visual authority.

## Requirements

- Keep visual ash behavior aligned with `docs/DESIGN.md` sections 17 and 20: no persistent ash storage in `PackedCell`, no renderer-owned ash creation, and no confusion with gameplay ash/fertility.
- Coordinate with `TWF-159`, which changes the ash presentation source of truth.
- Tune visual-field or presentation parameters only where needed for visible aftermath readability.
- Prefer Timberborn-native smoke/ash-like prefabs and material conventions before custom art.
- Capture high-resolution recordings and screenshots showing the accepted ash or aftermath behavior.
- Update `docs/DESIGN.md` only if the ash contract changes.
- Preserve command output, copied `Player.log`, artifact paths, and final QA lock state.
- Document accepted ash-effect evidence in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-065` provides the recording tool.
- `TWF-066` and `TWF-067` establish the active fire and smoke baselines.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run the opt-in Unity shader harness if visual-field shader behavior changes.
- QA must capture high-resolution recording evidence plus status/log proof of ash or aftermath visual behavior.

## Notes

- If simulator ash is not yet available, keep this ticket blocked or explicitly scoped to tuning existing presentation without expanding the old projection model.
- `TWF-159` owns removing renderer projection and reading simulator ash state; this ticket owns visual polish once that source is available.
