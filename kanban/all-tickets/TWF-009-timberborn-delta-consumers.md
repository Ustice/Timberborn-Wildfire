---
ticket: TWF-009
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-004
   - TWF-005
   - TWF-008
write_scope:
   - src/Wildfire.Timberborn/**
   - docs/TEST_PLAN.md
---

# TWF-009: Consume Deltas In Timberborn

## Goal

Use compact deltas from the GPU simulator to update overlays, effects, damage, alerts, and gameplay consequences in Timberborn.

## Why

The simulator is useful only if the game responds to changed cells. This ticket connects the changed-cell stream to visible and gameplay-relevant Timberborn behavior while keeping visuals and gameplay on their intended data paths.

## Requirements

- Subscribe Timberborn systems to simulator deltas.
- Update overlays or debug visual state only for changed cells.
- Apply building, terrain, or resource consequences from relevant deltas.
- Trigger or update pooled visual effects from changed cells and/or GPU visual fields.
- Add alert or debug telemetry for major state changes.
- Keep gameplay reactions in C# delta consumers, not inside compute shaders.
- Add tests around pure decision logic where possible.
- Capture live validation evidence when environment access is available.

## Dependencies

- `TWF-004` compact delta readback.
- `TWF-005` GPU visual field output.
- `TWF-008` Timberborn fixed-cadence dispatch.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run test`.
- Run `dotnet build Wildfire.slnx`.
- Run live Timberborn QA with logs and screenshots when available.

## Notes

- Avoid broad gameplay tuning in this ticket. Focus on proving the delta-consumer path.
