---
ticket: TWF-099
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-096
  - TWF-066
  - TWF-067
  - TWF-068
  - TWF-070
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/reference/timberborn-ui.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-099-add-visual-debug-settings.md
---

# TWF-099: Add Visual Debug Settings

## Goal

Add safe release settings for visual intensity and debug visibility.

## Why

Visual effects and diagnostics need player-safe controls, but debug surfaces must not become accidental release tools.

## Requirements

- Build on the `TWF-096` settings framework.
- Add visual intensity settings only where they are safe and bounded.
- Add debug visibility settings only for intentionally safe release diagnostics.
- Keep broad QA command/debug UI out of player-facing settings.
- Add deterministic tests for visual/debug setting interpretation.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-096` provides the settings framework.
- `TWF-066`, `TWF-067`, `TWF-068`, and `TWF-070` provide visual baselines.

## Parent Reference

- Parent gate: `TWF-048`.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture evidence that settings are visible or applied safely.

## Notes

- Release command/debug scrub belongs to `TWF-060`.
