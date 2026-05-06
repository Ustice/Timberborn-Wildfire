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

# TWF-149: Charter World Consequences On Real Fields

## Goal

Turn the real-field consequence gate into the Sprint 10 visual-consequence and aftermath charter, then use that charter to prove world consequences from real imported field data rather than fixed QA cells or standalone scaffolding.

## Requirements

- Use the best stable real-field scenario and imported companion target identities for the charter bridge.
- Expand this gate into a Sprint 10 charter rather than trying to validate every consequence family in one QA ticket.
- Convert `docs/world-consequence-first-pass.md` packets into canonical ticket updates or new tickets before dispatch.
- Include `TWF-156` in Sprint 10 as the reusable `256x256` map/setup ticket, but it does not need to be the first ticket.
- Define the first Sprint 10 wave around scorch, burned textures, ash field/overlay, and tightened crop/tree/structure burned-state behavior.
- Keep later Sprint 10 waves for stored goods, contamination, persistence, beaver injury, and player feedback unless dependencies are already ready.
- For each ready consequence family, require nonzero consequence counters from real imported targets or precise safe-unavailable telemetry.
- Confirm no consequence duplicates damage because of multi-cell or vertical occupancy.
- Confirm persistent ash and contamination-aware aftermath use aftermath fields rather than visual ash alone.
- Confirm save/reload preserves accepted durable state where implemented.
- Keep failed consequence gates blocked with exact missing API or behavior evidence.

## Dependencies

- `TWF-141` proves importer parity.
- `TWF-144` proves accepted real-field slow-reactable tuning.
- `TWF-156` creates the Sprint 10 `256x256` scenario map for max-size local-fire proof.
- `TWF-076`, `TWF-077`, `TWF-078`, `TWF-079`, `TWF-084`, and `TWF-115` provide consequence implementations.

## Role

- QA.
- Follow [../roles/qa.md](../roles/qa.md).

## Implementation Notes

- Start by writing the Sprint 10 charter from `docs/world-consequence-first-pass.md`.
- Run only consequence tickets that are already implemented and review-ready.
- For each consequence family, record the imported target identity, starting field values, fire delta evidence, consequence counters, and final world-state proof.
- If a safe Timberborn mutation API is missing, require explicit skipped-unsafe telemetry and a follow-up blocker.
- Do not combine multiple consequence failures into one vague blocker. Name the material class, target kind, API, and evidence file.
- This ticket gates and charters groups of smaller tickets; it should not implement missing consequence logic.

## Verification

- Run `git diff --check`.
- Run `dotnet test` if production code changes are made.
- Live QA must preserve generated scenario, command transcripts, copied `Player.log`, recordings or screenshots, status output, and save/reload evidence when persistence is in scope.

## Notes

- This is a group gate for smaller consequence tickets. It should not be used to smuggle new consequence implementation into QA work.
- 2026-05-06 direction update: treat this as the bridge into Sprint 10. The Sprint 10 charter should expand the junior-ready visual-consequence plan into implementable tickets: scorch heat history, burned texture asset pipeline, persistent ash field and overlay, tightened crop/tree/structure burned states, fertile ash growth, contaminated ash, persistence, and later beaver/player-feedback follow-ups.
