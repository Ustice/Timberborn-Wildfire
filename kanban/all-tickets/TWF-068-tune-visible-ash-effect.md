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

Make ash or residual burn aftermath visually legible without pretending Wildfire stores persistent ash history.

## Why

`TWF-041` intentionally kept ash as a derived heat/fuel visual approximation. Ash needs its own tuning pass because the right presentation may differ from active fire and smoke, and because release copy and screenshots must be honest about whether aftermath is temporary or persistent.

## Requirements

- Keep visual ash behavior aligned with `docs/DESIGN.md` sections 17 and 20: derived visual output only, no persistent ash storage in `PackedCell`, and no confusion with gameplay ash/fertility.
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

- If the derived ash approximation is not good enough for release, this ticket should make that explicit and create or update the design follow-up rather than silently adding packed-cell storage.
- Persistent gameplay ash belongs to the ash field service ticket, not this visual tuning ticket.
