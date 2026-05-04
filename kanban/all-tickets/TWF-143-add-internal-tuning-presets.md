---
ticket: TWF-143
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-142
write_scope:
  - src/Wildfire.Timberborn/**
  - scripts/**
  - tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-143-add-internal-tuning-presets.md
---

# TWF-143: Add Internal Tuning Presets

## Goal

Add internal QA/debug presets for tuning wildfire behavior without recompiling shaders or exposing player settings.

## Requirements

- Provide named presets: `default`, `slow-reactable`, `harsh`, and `conservative`.
- Allow QA-only command or fixture selection of a preset.
- Show active preset and key parameter values in `status` or `qa-readiness`.
- Reject arbitrary raw parameter input from the command bridge.
- Add tests that preset selection uploads expected parameters and rejects invalid names.

## Dependencies

- `TWF-142` provides runtime parameters.

## Role

- Worker.
- Follow [../roles/worker.md](../roles/worker.md).

## Implementation Notes

- Keep this internal. Do not add Timberborn player settings UI.
- The first accepted gameplay target is `slow-reactable`: fire spreads visibly but slowly enough that players can respond.
- Avoid duplicating every parameter in command output; status should be enough for QA to identify the preset and major knobs.

## Verification

- Run `git diff --check`.
- Run `bun run typecheck` if scripts change.
- Run `bun test` if scripts change.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.

## Notes

- This ticket is intentionally safer than arbitrary parameter editing. Presets make QA evidence reproducible.
