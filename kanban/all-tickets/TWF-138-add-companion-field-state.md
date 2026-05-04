---
ticket: TWF-138
agent_level: High
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-137
write_scope:
  - src/Wildfire.Core/**
  - src/Wildfire.Unity/**
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/ARCHITECTURE.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-138-add-companion-field-state.md
---

# TWF-138: Add Companion Field State

## Goal

Add companion field storage beside `PackedCell` for data that does not belong in the hot 16-bit simulation cell.

## Requirements

- Keep `PackedCell` unchanged.
- Add companion fields for material class, target identity, remaining burn capacity, burn history, ash strength, ash quality, and contamination or taint classification.
- Upload companion fields to GPU-owned buffers when the simulator initializes.
- Keep gameplay deltas based on compact cell changes, but expose bounded inspection for companion field values in tests and QA status.
- Add clear defaults for empty cells and unknown materials.
- Avoid broad readback of companion fields during every tick.

## Dependencies

- `TWF-137` defines the schema and value ranges.

## Role

- Worker.
- Follow [../roles/worker.md](../roles/worker.md).

## Implementation Notes

- Treat companion fields as authoritative metadata for import, visuals, and consequences, not as a second C# simulation path.
- Use fixed-size numeric records suitable for GPU buffers.
- Start with initialization/upload and inspection. Only mutate fields that are required for burn history or aftermath.
- Do not implement active-frontier optimization in this ticket.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- If shader bindings change, run the Unity batchmode shader harness documented in `docs/TEST_PLAN.md`.

## Notes

- This ticket is the reason we do not expand `PackedCell`: packed cells stay fast, while real-world identity and aftermath state stay explicit.
- 2026-05-04 worker result: added `WildfireCompanionFieldState` and `WildfireCompanionField` in `Wildfire.Core`, plus `wildfire.companion_target_ids` and `wildfire.companion_fields` allocation/upload in `ComputeBufferGrid`.
- 2026-05-04 worker scope note: this slice initializes and uploads companion fields for GPU-owned buffers but does not yet mutate burn history or wire shader/renderer consumption. Those remain for downstream tickets.
- 2026-05-04 worker docs: updated `docs/ARCHITECTURE.md` and `docs/TEST_PLAN.md` with the companion field buffer contract.
- 2026-05-04 worker verification: `git diff --check`, `bun run typecheck`, `bun test`, `dotnet test Wildfire.slnx`, and `dotnet build Wildfire.slnx` passed. A filtered concurrent .NET test attempt was discarded after a compile fix; final .NET verification was rerun sequentially.
