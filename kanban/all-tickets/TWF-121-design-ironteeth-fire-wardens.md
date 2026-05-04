---
ticket: TWF-121
agent_level: Medium
role: researcher
requires_qa: false
doc_only: true
dependencies: []
write_scope:
  - docs/DESIGN.md
  - docs/world-consequence-first-pass.md
  - kanban/all-tickets/TWF-121-design-ironteeth-fire-wardens.md
---

# TWF-121: Design Ironteeth Fire Wardens

## Goal

Design the Ironteeth Fire Wardens as the high-resource, low-labor fire suppression strategy.

## Why

Ironteeth should respond to fire with equipment and infrastructure: protective clothing, sprayers, and concentrated water application.

## Requirements

- Define building role, staffing, range, equipment requirements, and resource upkeep.
- Define how Wardens apply water into the simulation.
- Identify whether visible in-game water should also be added, and what Timberborn API evidence is needed.
- Define protective-clothing effects on smoke, singed, and burned exposure.
- Define telemetry for staffed wardens, water applied, fires targeted, and injuries prevented.
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
