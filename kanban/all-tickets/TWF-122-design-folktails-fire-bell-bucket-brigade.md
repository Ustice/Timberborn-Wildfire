---
ticket: TWF-122
agent_level: Medium
role: researcher
requires_qa: false
doc_only: true
dependencies: []
write_scope:
  - docs/DESIGN.md
  - docs/world-consequence-first-pass.md
  - kanban/all-tickets/TWF-122-design-folktails-fire-bell-bucket-brigade.md
---

# TWF-122: Design Folktails Fire Bell Bucket Brigade

## Goal

Design the Folktails Fire Bell as the low-resource, high-labor fire suppression strategy.

## Why

Folktails should fight fire as a community response: a staffed bell summons nearby beavers with buckets, then they fetch water and dump it on individual burning spots.

## Requirements

- Define Fire Bell staffing, alert range, summon range, and target selection.
- Define bucket assignment and return-to-work behavior.
- Prefer nearest natural water source, then fall back to stored water tanks when no natural source is in range.
- Define per-beaver water application to one fire cell or small target area.
- Define injury and smoke exposure interactions while beavers are bucket-brigading.
- Define telemetry for summoned beavers, bucket trips, water source choice, water applied, and abandoned trips.
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
