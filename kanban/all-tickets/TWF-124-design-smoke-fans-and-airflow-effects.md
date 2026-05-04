---
ticket: TWF-124
agent_level: Medium
role: researcher
requires_qa: false
doc_only: true
dependencies: []
write_scope:
  - docs/DESIGN.md
  - docs/world-consequence-first-pass.md
  - kanban/all-tickets/TWF-124-design-smoke-fans-and-airflow-effects.md
---

# TWF-124: Design Smoke Fans And Airflow Effects

## Goal

Design fans as field-based smoke and airflow modifiers without letting them become a hand-authored per-tile exception system.

## Why

Fans could protect settlements from smoke, redirect danger, or change fire behavior. They are promising, but they touch the simulation fields deeply enough that they need a separate design pass.

## Requirements

- Define whether fans block, redirect, thin, or accelerate smoke fields.
- Define whether fans interact with toxic smoke differently from normal smoke.
- Define any fire-side consequences, such as higher heat or directional spread.
- Keep effects field-based and compatible with clustered fire/smoke visuals.
- Identify required UI controls, range visualization, power costs, and maintenance costs.
- Define deterministic tests or shader snapshots that could prove field effects before live validation.
- Link back to `TWF-120` in notes when the design is reconciled.

## Dependencies

- Deferred child of `TWF-120`.

## Role

- Researcher.
- Follow [researcher.md](../roles/researcher.md).

## Verification

- Run `git diff --check`.

## Notes

- Parent ticket: `TWF-120`.
- Design reference: [world-consequence-first-pass.md](../../docs/world-consequence-first-pass.md), "Faction Fire Response Ideas".
