---
ticket: TWF-125
agent_level: Low
role: researcher
requires_qa: false
doc_only: true
dependencies: []
write_scope:
  - docs/DESIGN.md
  - docs/world-consequence-first-pass.md
  - kanban/all-tickets/TWF-125-design-constructible-fire-berms.md
---

# TWF-125: Design Constructible Fire Berms

## Goal

Design constructible fire berms or firebreak barriers as a tactical spread-control tool.

## Why

Berms are simpler than fans and fit the simulation as spread resistance or spread blocking. They also give players a readable way to plan controlled burns and settlement defense.

## Requirements

- Define whether berms fully block fire spread, reduce spread probability, increase heat loss, or combine those effects.
- Define construction materials and whether the berm itself is non-burnable.
- Define allowed placement rules, height behavior, and interactions with paths, buildings, and vegetation.
- Define whether berms block smoke or only fire spread.
- Define telemetry for berm cells, blocked spread attempts, and heat loss effects.
- Identify implementation tickets if the design becomes release-scope.
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
