---
ticket: TWF-087
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-071
  - TWF-072
  - TWF-073
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-087-implement-beaver-fire-heat-exposure.md
---

# TWF-087: Implement Beaver Fire Heat Exposure

## Goal

Implement the direct fire and heat beaver behavior variant: singed injury, burned work-preventing injury if safe, and death only after sustained severe exposure is proven safe.

## Why

Fire and heat should feel immediately dangerous in a different way from smoke. This ticket owns direct contact, extreme adjacency, path avoidance, injury progression, and death evidence for flame and heat exposure.

## Requirements

- Consume fire and heat exposure classifications from `TWF-072` through the `TWF-073` behavior harness.
- Treat active fire cells as forbidden or heavily avoided zones where Timberborn exposes a safe pathing hook.
- Interrupt unsafe work assigned to burning buildings, crops, or trees where Timberborn supports it.
- Implement singed as an injury-style debuff if Timberborn exposes a safe API.
- Implement burned as a work-preventing severe injury if Timberborn exposes a safe API.
- Implement death only after sustained direct heat or flame exposure and only after safer states are proven in live evidence.
- Add deterministic tests for direct-fire exposure, heat exposure, threshold transitions, work interruption, pathing decision output, and safe no-op behavior.
- Expose bounded QA/status telemetry for exposed beavers, avoided cells, interrupted work, singed, burned, deaths, skipped unsafe APIs, and recovery.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-071` defines the accepted beaver field contract.
- `TWF-072` provides exposure telemetry.
- `TWF-073` provides the shared behavior harness.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Use exposure accumulation over multiple simulation ticks, but treat active flame cells as the highest-priority danger classification.
- Singed should be the first live proof target because it maps to a typical injury-style debuff.
- Burned should prevent work until healed only after the API spike proves a safe work-blocking injury or contamination-like state.
- Death must stay gated behind sustained direct heat or flame and separate live evidence.
- Expected counters include heat-exposed beavers, active-flame contacts, avoided cells, interrupted jobs, singed entered, burned entered, healed or recovered, deaths, and unsafe API skips.
- Safe no-op behavior should still interrupt or avoid clearly unsafe work if that hook is proven safer than applying injury.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for singed or work interruption, or an explicit safe unavailable state; burned and death require separate accepted evidence before they can be called implemented.

## Notes

- Normal smoke behavior belongs to `TWF-085`.
- Toxic smoke behavior belongs to `TWF-086`.
- Relevant design reference: `docs/DESIGN.md` section 20, "Beaver Field Effects".
