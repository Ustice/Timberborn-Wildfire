---
ticket: TWF-042
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-036
   - TWF-037
write_scope:
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/reference/timberborn-ui.md
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-042-add-player-facing-fire-alert.md
---

# TWF-042: Add Player-Facing Fire Alert

## Goal

Add one clear player-facing alert or status surface that reports active fire risk or active fire consequences from simulator output.

## Why

The design includes alert updates in the Timberborn integration flow. Once deltas affect buildings and overlays, players need a Timberborn-native way to notice and inspect the condition without relying only on QA command output.

## Requirements

- Drive the alert from compact deltas, consequence counters, or derived active-fire state.
- Use Timberborn-native UI or alert patterns documented in the repo where possible.
- Keep the alert informational and bounded; do not add broad command/debug UI.
- Add deterministic tests for alert-state transitions where possible.
- Document live QA expectations, screenshots, and status evidence in `docs/TEST_PLAN.md`.
- Update `docs/reference/timberborn-ui.md` only if new UI guidance or discovered patterns should be durable.

## Dependencies

- `TWF-036` provides a real building consequence.
- `TWF-037` provides an inspection surface for changed fire state.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live screenshots, `Player.log` evidence, and command/status output proving the alert corresponds to simulator state.

## Notes

- Keep this to one alert or status surface. Full UI polish and settings can come after the first player-visible loop exists.
