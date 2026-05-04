---
ticket: TWF-123
agent_level: Medium
role: researcher
requires_qa: false
doc_only: true
dependencies: []
write_scope:
  - docs/DESIGN.md
  - docs/world-consequence-first-pass.md
  - kanban/all-tickets/TWF-123-design-emberpelt-tail-stamping-fire-response.md
---

# TWF-123: Design Emberpelt Tail Stamping Fire Response

## Goal

Design the Emberpelt fire response around direct tail-stamping suppression with higher injury risk.

## Why

This faction should feel distinct: effective and brave, but more dangerous for individual beavers than Ironteeth equipment or Folktail bucket brigades.

## Requirements

- Define how tail stamping applies suppression to one or more simulation cells.
- Define when a beaver can safely approach a fire edge and when the action is blocked.
- Define elevated singed and burned risk while stamping.
- Define whether smoke exposure still applies while stamping.
- Define telemetry for stamping attempts, successful suppression, injuries, skipped unsafe targets, and deaths if death is later enabled.
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
