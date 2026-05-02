---
ticket: TWF-048
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-046
write_scope:
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/reference/timberborn-ui.md
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-048-add-release-settings.md
---

# TWF-048: Add Release Settings

## Goal

Add the minimum useful release settings with stable keys, conservative defaults, and live validation.

## Why

Settings are part of the compatibility contract. Players need a safe way to disable or soften the feature, and future versions need stable keys instead of ad hoc debug toggles.

## Requirements

- Add a stable settings owner or equivalent Timberborn-native settings surface.
- Include conservative settings such as enable Wildfire, fire intensity or spread rate, visual intensity, and debug visibility only if each can be implemented safely.
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
