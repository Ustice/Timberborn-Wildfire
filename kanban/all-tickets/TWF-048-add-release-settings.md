---
ticket: TWF-048
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-046
  - TWF-096
  - TWF-097
  - TWF-098
  - TWF-099
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/reference/timberborn-ui.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-048-add-release-settings.md
---

# TWF-048: Add Release Settings

## Goal

Integrate the minimum useful release settings after the settings framework, enable/disable control, behavior tuning settings, and visual/debug settings are implemented.

## Why

Settings are part of the compatibility contract. Players need a safe way to disable or soften the feature, and future versions need stable keys instead of ad hoc debug toggles.

## Requirements

- Confirm `TWF-096` adds a stable settings owner or equivalent Timberborn-native settings surface.
- Confirm `TWF-097` adds safe enable/disable behavior.
- Confirm `TWF-098` adds conservative behavior tuning settings.
- Confirm `TWF-099` adds visual/debug settings where safe.
- Keep player preferences in settings, not save data.
- Give settings stable keys and defaults.
- Ensure disabling the feature leaves the current save recoverable and stops new fire work safely.
- Add deterministic tests for settings interpretation where possible.
- Document live settings QA in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-046` proves the live loop that these settings control.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture screenshots or logs proving settings are visible or applied, and that disabling Wildfire behaves safely.

## Notes

- Do not add a broad custom configuration UI if Timberborn-native settings are enough.
- Child tickets: `TWF-096`, `TWF-097`, `TWF-098`, and `TWF-099`.
