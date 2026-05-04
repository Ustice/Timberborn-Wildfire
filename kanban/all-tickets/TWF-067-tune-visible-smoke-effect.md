---
ticket: TWF-067
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-065
  - TWF-066
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-067-tune-visible-smoke-effect.md
---

# TWF-067: Tune Visible Smoke Effect

## Goal

Make smoke readable as a distinct visual state from active flame in live Timberborn recordings.

## Why

The visual field already derives smoke, but release tuning needs a separate pass so smoke does not become either invisible or visually confused with fire. Smoke should help players understand active or recently active burning without adding new simulation rules.

The current design requires smoke to read as a field or volume rather than a tile-by-tile effect. This ticket owns the smoke side of that presentation contract.

## Requirements

- Use the existing pooled presentation and visual-field surface.
- Follow `docs/DESIGN.md` section 17: cluster, blur, threshold, or otherwise aggregate neighboring smoke intensity into larger coherent regions.
- Use compact deltas only to wake or bound visual regions; do not map one changed cell to one visible effect.
- Prefer Timberborn-native smoke prefabs and conventions before custom art.
- Tune presentation concerns such as prefab choice, scale, placement, lifetime, intensity thresholds, and fire-versus-smoke selection.
- Keep smoke derived from simulator visual output; do not add Timberborn-owned fire rules.
- Capture high-resolution recordings and screenshots showing smoke as distinct from fire.
- Preserve command output, copied `Player.log`, artifact paths, and final QA lock state.
- Document accepted smoke-effect evidence in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-065` provides the recording tool.
- `TWF-066` establishes the fire-effect baseline that smoke must remain visually distinct from.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- QA must capture high-resolution recording evidence plus status/log proof of smoke-effect selection or the explicit reason smoke remains deferred.

## Notes

- If native smoke prefabs are too visually heavy or too subtle, record the smallest next production step instead of blending smoke into the fire pass.
- Relevant design references: `docs/DESIGN.md` section 17 and `docs/ARCHITECTURE.md` "Field Visual Presentation Service".
