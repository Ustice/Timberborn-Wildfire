---
ticket: TWF-149
agent_level: High
role: qa
requires_qa: true
doc_only: false
dependencies:
  - TWF-141
  - TWF-144
  - TWF-076
  - TWF-077
  - TWF-078
  - TWF-079
  - TWF-084
  - TWF-115
write_scope:
  - docs/TEST_PLAN.md
  - docs/HANDOFF.md
  - kanban/all-tickets/TWF-149-gate-world-consequences-real-fields.md
---

# TWF-149: Gate World Consequences On Real Fields

## Goal

Prove that world consequences are driven by real imported field data, not fixed QA cells or standalone scaffolding.

## Requirements

- Use the generated QA scenario and imported companion target identities.
- Validate crop, tree, structure, stored goods, infrastructure, ash, contamination, and persistence tickets that are ready.
- Require nonzero consequence counters from real imported targets, or precise safe-unavailable telemetry.
- Confirm no consequence duplicates damage because of multi-cell or vertical occupancy.
- Confirm persistent ash and contamination-aware aftermath use aftermath fields rather than visual ash alone.
- Confirm save/reload preserves accepted durable state where implemented.
- Keep failed consequence gates blocked with exact missing API or behavior evidence.

## Dependencies

- `TWF-141` proves importer parity.
- `TWF-144` proves accepted tuning.
- `TWF-076`, `TWF-077`, `TWF-078`, `TWF-079`, `TWF-084`, and `TWF-115` provide consequence implementations.

## Role

- QA.
- Follow [../roles/qa.md](../roles/qa.md).

## Implementation Notes

- Run only consequence tickets that are already implemented and review-ready.
- For each consequence family, record the imported target identity, starting field values, fire delta evidence, consequence counters, and final world-state proof.
- If a safe Timberborn mutation API is missing, require explicit skipped-unsafe telemetry and a follow-up blocker.
- Do not combine multiple consequence failures into one vague blocker. Name the material class, target kind, API, and evidence file.
- This ticket gates groups of smaller tickets; it should not implement missing consequence logic.

## Verification

- Run `git diff --check`.
- Run `dotnet test` if production code changes are made.
- Live QA must preserve generated scenario, command transcripts, copied `Player.log`, recordings or screenshots, status output, and save/reload evidence when persistence is in scope.

## Notes

- This is a group gate for smaller consequence tickets. It should not be used to smuggle new consequence implementation into QA work.
