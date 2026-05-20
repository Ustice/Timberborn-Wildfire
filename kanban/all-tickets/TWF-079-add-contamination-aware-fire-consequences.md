---
ticket: TWF-079
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-072
  - TWF-078
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-079-add-contamination-aware-fire-consequences.md
---

# TWF-079: Add Contamination Aware Fire Consequences

## Goal

Make fire consequences respect contamination without ever treating fire as a decontamination mechanic.

## Why

The design decision is explicit: fire never reduces contamination. Contaminated fuel may burn, contaminated water may suppress, and contaminated aftermath may be tainted, but soil, water, goods, plants, buildings, and beavers are not cleansed by fire.

## Requirements

- Keep contamination state Timberborn-owned; do not add contamination storage to `PackedCell`.
- Detect contaminated burn sources or contaminated affected cells through safe Timberborn adapter surfaces.
- Produce `tainted` ash instead of `fertile` ash when contaminated burn sources or contaminated soil are involved.
- Treat badwater or contaminated water as suppression input without converting it to safe water.
- Classify toxic smoke exposure for beaver telemetry and behavior tickets where safe field data exists.
- Provide contamination-aware field classifications used by `TWF-086` rather than implementing the toxic smoke beaver behavior here.
- Preserve native badwater contamination, treatment, and graphics paths only if live API tests prove them safe.
- Add deterministic tests for tainted ash classification, no-decontamination behavior, badwater suppression semantics, and toxic exposure classification where possible.
- Expose bounded QA/status telemetry for contaminated burn sources, tainted ash, toxic smoke exposure, and skipped unsafe API paths.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-072` provides beaver exposure telemetry that contaminated smoke may feed.
- `TWF-078` provides ash quality storage.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start by discovering read-only Timberborn adapter surfaces for contaminated soil, contaminated water, badwater, contaminated goods, and contaminated plant/building state.
- The invariant is strict: fire and high heat must not reduce contamination values.
- Badwater and contaminated water may suppress fire as water-like input, but the result should remain contaminated or tainted in aftermath classification. They must not create toxic or contaminated steam.
- Toxic smoke should be exported to telemetry and `TWF-086`; this ticket should not own beaver behavior.
- Expected counters include contaminated burn sources, contaminated affected cells, badwater suppression inputs, tainted ash classifications, toxic smoke cells, and skipped unsafe API reads.
- Safe no-op behavior should preserve vanilla contamination unchanged and skip only the Wildfire consequence branch that cannot be proven safe.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence or explicit safe-unavailable telemetry for at least one contamination-aware fire interaction.

## Notes

- 2026-05-17 coordinator/code reconciliation: part of this ticket landed through the off-sprint ash implementation. Simulator ash contamination now flows into the Timberborn ash read model as `tainted`, `TimberbornTaintedAshSoilPoisoningService` reports tainted-soil poisoning attempts, and status/QA tokens expose tainted ash counters. Remaining scope still includes broader contamination-aware fire interactions: badwater/contaminated-water suppression semantics, toxic smoke classification for beaver telemetry, and live proof that native contamination is not reduced.
- 2026-05-19 design correction: there is no toxic or contaminated steam in Wildfire. Keep contamination work on contaminated smoke, tainted ash, badwater suppression semantics, and no-decontamination proof.
- Do not add any implicit high-heat cleanup or sterilization behavior.
- Toxic smoke beaver behavior belongs to `TWF-086`.
- Relevant design reference: `docs/DESIGN.md` section 20, "Contamination Interaction".
