---
ticket: TWF-157
agent_level: High
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-137
  - TWF-138
write_scope:
  - src/Wildfire.Core/**
  - src/Wildfire.Unity/**
  - tests/Wildfire.Core.Tests/**
  - docs/ash-simulation-model.md
  - docs/ARCHITECTURE.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-157-migrate-ash-to-simulator-transport-state.md
---

# TWF-157: Migrate Ash To Simulator Transport State

## Goal

Make simulator transport state the authoritative source for ash amount and ash contamination.

## Why

`docs/ash-simulation-model.md` settles that visual ash and gameplay ash must not be separate authorities. Ash amount should live in the packed transport field, contamination should travel with that ash, and `CompanionFields.AshStrength` should stop acting as dynamic ash storage.

## Requirements

- Keep ash out of `PackedCell`.
- Keep ash in the existing packed transport buffer; do not add a dedicated `AshFields` buffer.
- Normalize simulator ash amount to the agreed `0-3` scale.
- Normalize ash contamination to the agreed `0-7` scale.
- Create ash from burn-created residue and deterministic stochastic smoke fallout.
- Carry contamination from the ash source: clean source creates uncontaminated ash, contaminated source or contaminated smoke creates contaminated ash.
- Mix contamination with maxing for the first implementation.
- Retire `CompanionFields.AshStrength` as dynamic ash state once transport ash is authoritative.
- Preserve material/source classification in companion or material fields without using it as the current ash amount.
- Add deterministic tests for burn-created ash, smoke-created ash, contaminated ash, and conservative contamination mixing.

## Dependencies

- `TWF-137` defines the shared material-field schema.
- `TWF-138` provides companion field storage and upload.

## Role

- Worker.
- Follow [../roles/worker.md](../roles/worker.md).

## Implementation Notes

- `docs/ash-simulation-model.md` is the source of truth for the model.
- `AtmosphericFields` is the current implementation name for the transport buffer; do not do a broad rename unless this ticket already has passing behavior.
- Prefer tests around `WildfireAtmosphericFieldState`, `FireSim.compute`, and the Unity shader harness before touching Timberborn adapters.
- Same-tick ordering should be external mutation, simulation update, then delta emission or readback.

## Verification

- Run `git diff --check`.
- Run `dotnet test Wildfire.slnx --no-restore`.
- Run `bun run typecheck`.
- Run the Unity shader harness if `FireSim.compute` behavior changes.

## Notes

- This ticket is the first implementation slice from [docs/ash-simulation-model.md](../../docs/ash-simulation-model.md).
