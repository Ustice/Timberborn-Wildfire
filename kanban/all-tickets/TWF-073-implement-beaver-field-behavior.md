---
ticket: TWF-073
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-071
  - TWF-072
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-073-implement-beaver-field-behavior.md
---

# TWF-073: Add Beaver Field Behavior Harness

## Goal

Add the shared Timberborn-side harness that applies beaver behavior decisions from exposure telemetry, without owning each smoke, toxic smoke, or fire progression in one oversized ticket.

## Why

Once beaver exposure can be detected, Wildfire needs a safe behavior application surface before individual danger progressions can be implemented. Smoke, toxic smoke, and fire have different player meanings and should be implemented as separate focused tickets using the same harness.

## Requirements

- Implement the shared beaver behavior dispatcher, status state, persistence hooks, QA counters, and test seams needed by the variant tickets.
- Use `TWF-072` exposure telemetry as the source of evidence and QA observability.
- Provide bounded decision hooks for smoke, toxic smoke, and fire/heat variants.
- Do not implement final coughing, choking, singed, burned, contamination, or death behavior here except as no-op/test decisions needed to prove the harness.
- Keep behavior reversible and safe across save/reload where possible.
- Do not move fire rules into Timberborn; Timberborn may only react to field exposure.
- Add deterministic tests for routing, throttling, persistence, and safe no-op behavior.
- Preserve logs, status counters, screenshots or recordings, command output, and final QA lock state.
- Document accepted live behavior evidence in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-071` defines the accepted beaver behavior contract.
- `TWF-072` proves beaver exposure detection and telemetry.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Build one dispatcher that variant tickets call through; do not let each variant invent its own persistence, throttling, or API probing.
- Keep variant state small and versioned so save/reload can recover without trapping beavers in a bad state.
- Prefer reversible decisions first: no-op telemetry, work interruption, path avoidance, and temporary debuffs before incapacitation or death.
- Expected counters include decisions evaluated, decisions applied by variant, decisions skipped by cooldown, persistence saves, persistence loads, unsafe API skips, and recovery actions.
- Safe no-op behavior should leave all beavers in vanilla state while proving the dispatcher can receive exposure classifications.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- QA must capture live evidence that the behavior harness can observe accepted exposure telemetry and apply or skip a bounded no-op decision without critical exceptions.

## Notes

- Smoke behavior belongs to `TWF-085`.
- Toxic smoke behavior belongs to `TWF-086`.
- Fire and heat behavior belongs to `TWF-087`.
- Relevant design reference: `docs/DESIGN.md` section 20, "Beaver Field Effects".
