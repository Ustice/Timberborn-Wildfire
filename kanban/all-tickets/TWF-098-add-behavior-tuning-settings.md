---
ticket: TWF-098
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-096
  - TWF-069
write_scope:
  - src/Wildfire.Timberborn/**
  - src/Wildfire.Unity/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-098-add-behavior-tuning-settings.md
---

# TWF-098: Add Behavior Tuning Settings

## Goal

Add conservative release settings for behavior intensity only where the tuned behavior contract supports them.

## Why

Players may want softer or stronger fire behavior, but settings must not bypass deterministic rules or destabilize the GPU path.

## Requirements

- Build on the `TWF-096` settings framework.
- Add only behavior settings that can be implemented safely after `TWF-069`.
- Prefer coarse presets over fragile raw constants unless raw values are clearly safe.
- Keep setting interpretation deterministic and bounded.
- Include consequence-related settings only after their behavior is implemented, such as burn consequence intensity, beaver danger severity, ash fertility strength, or hazardous-good behavior.
- Do not expose per-resource fuel constants to players unless the design explicitly accepts advanced tuning.
- Add deterministic tests for setting-to-runtime behavior.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-096` provides the settings framework.
- `TWF-069` accepts behavior tuning.

## Parent Reference

- Parent gate: `TWF-048`.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture status or recording evidence for at least one behavior setting.

## Notes

- Visual intensity and debug visibility belong to `TWF-099`.
- Resource fuel and flammability values come from `TWF-114`; explosives or volatile goods come from `TWF-116`.
