---
ticket: TWF-164
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-157
  - TWF-158
  - TWF-160
write_scope:
  - src/Wildfire.Core/**
  - src/Wildfire.Unity/**
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/DESIGN.md
  - docs/ash-simulation-model.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-164-add-tainted-ash-decay-and-water-washout.md
---

# TWF-164: Add Tainted Ash Decay

## Goal

Make tainted ash naturally fade over time without treating decay as a plant-growth boost or decontamination mechanic.

## Why

Tainted ash should be hazardous aftermath, not permanent map litter. Decay is the time-based cleanup path. Water-contact behavior is split to `TWF-167` so it can be tested independently from decay.

## Requirements

- Tainted ash decays gradually over simulator time.
- The first implementation must be deterministic and reproducible from tick, cell, and seed inputs.
- Visual presentation should show ash receding as ash levels fall.
- Status and QA counters should distinguish tainted ash decay from fertile ash decay.
- Save/reload must preserve remaining ash state and avoid resurrecting decayed ash.
- Do not treat fire, heat, or ash decay as decontamination of soil, goods, plants, buildings, or beavers.
- Do not treat tainted ash decay as a plant-growth boost. Clean/fertile growth bonus behavior belongs to clean ash; tainted ash must not grant it.
- Keep ash/water interaction, washout, and water-tainting out of scope; `TWF-167` owns that narrower mechanic.

## Dependencies

- `TWF-157`, `TWF-158`, and `TWF-160` align ash authority, queued mutations, and persistence/harvest synchronization with simulator-owned ash state.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Keep decay in simulator-owned ash transport state.
- If water behavior is needed, stop and use `TWF-167` instead of extending this ticket.

## Verification

- Run `git diff --check`.
- Run `dotnet test Wildfire.slnx --no-restore`.
- Run `bun run typecheck`.
- Run the Unity shader harness if compute shader ash behavior changes.
- Live QA must capture before/after evidence of tainted ash fading, with status tokens proving the ash level changed.

## Notes

- This ticket answers only the release design decision that tainted ash fades over time.
- 2026-05-20 Jason confirmation: tainted ash decay works. Ash/water interaction is split to `TWF-167`. Moved to `06-done`.
