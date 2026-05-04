---
ticket: TWF-120
agent_level: Medium
role: researcher
requires_qa: false
doc_only: true
dependencies:
  - TWF-121
  - TWF-122
  - TWF-123
  - TWF-124
  - TWF-125
write_scope:
  - docs/DESIGN.md
  - docs/world-consequence-first-pass.md
  - kanban/all-tickets/TWF-120-design-faction-fire-response-systems.md
---

# TWF-120: Design Faction Fire Response Systems

## Goal

Unify the future faction-specific active fire-response concepts into a durable design direction.

## Why

Ironteeth, Folktails, and Emberpelts should not all fight fire the same way. Suppression should become a faction identity layer without letting Timberborn adapters own core fire rules.

## Requirements

- Depend on the faction child tickets for the individual strategy writeups.
- Keep suppression effects expressed as simulation inputs, such as water application, spread resistance, smoke field modification, or beaver exposure risk.
- Compare resource cost, labor cost, injury risk, and response speed across factions.
- Identify shared UI, alert, range, staffing, and telemetry needs.
- Record which ideas are implementation-ready and which need prototype evidence.

## Dependencies

- `TWF-121` covers Ironteeth Fire Wardens.
- `TWF-122` covers Folktails Fire Bell bucket brigades.
- `TWF-123` covers Emberpelt tail-stamping response.
- `TWF-124` covers fans and airflow.
- `TWF-125` covers constructible fire berms.

## Role

- Researcher.
- Follow [researcher.md](../roles/researcher.md).

## Verification

- Run `git diff --check`.

## Notes

- Parent design reference: [world-consequence-first-pass.md](../../docs/world-consequence-first-pass.md), "Faction Fire Response Ideas".
- Deferred until the passive consequence and field-effect loop is stable.
