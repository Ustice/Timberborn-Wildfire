---
ticket: TWF-004
agent_level: High
role: researcher
requires_qa: false
doc_only: true
dependencies:
   - TWF-001
write_scope:
   - kanban/all-tickets/TWF-004-timberborn-adapter-research.md
---

# TWF-004: Research Timberborn Adapter Surface

## Goal

Identify the Timberborn APIs and data sources needed to convert terrain, buildings, resources, and water into packed fire cells without making Timberborn own simulation rules.

## Requirements

- Inspect current Timberborn modding references or installed assemblies available to the repo.
- Identify likely terrain, building, water, overlay, and effect integration points.
- Separate confirmed API evidence from inference.
- Recommend the first narrow Timberborn CPU backend ticket.

## Dependencies

- TWF-001 should define the stable core contract first.

## Role

- Researcher using [../roles/researcher.md](../roles/researcher.md).

## Verification

- Run `git diff --check` when the ticket is updated.

## Notes

- This ticket should not implement the adapter.
