---
ticket: TWF-158
agent_level: High
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-157
write_scope:
  - src/Wildfire.Core/**
  - src/Wildfire.Unity/**
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/ARCHITECTURE.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-158-add-queued-simulator-ash-mutations.md
---

# TWF-158: Add Queued Simulator Ash Mutations

## Goal

Add a bounded queued ash-change path so Timberborn adapters can request ash additions and removals without directly mutating simulator buffers or maintaining competing ash state.

## Why

The ash model requires Timberborn actions to queue simulator changes for the next cycle. Collection, fertile-ash application, water washing, and future cleanup actions need one controlled mutation path that preserves simulator authority.

## Requirements

- Add a queued ash mutation contract parallel to existing queued fire-cell changes.
- Support bounded ash add, remove, and set-style requests needed by collection and application.
- Apply queued ash changes before the simulation update in each cycle.
- Clamp ash amount to `0-3` and contamination to `0-7`.
- Treat uncontaminated application as contamination `0`.
- Prevent contaminated-cell fertilizing from consuming `FertileAsh` unless a future decontamination mechanic is explicitly designed.
- Expose counters for queued, applied, clamped, rejected, and failed ash mutations.
- Add deterministic tests for queued addition, queued removal, same-tick ordering, clamping, and contaminated-cell rejection.

## Dependencies

- `TWF-157` makes simulator transport ash authoritative.

## Role

- Worker.
- Follow [../roles/worker.md](../roles/worker.md).

## Implementation Notes

- Keep Timberborn services as adapters: they may request changes and read status, but they should not own ash truth.
- Preserve the existing `FertileAsh` good id and toolbar loop where possible, but route application through the queued simulator mutation path.
- Use `map`, `reduce`, `flatMap`, and similar TypeScript collection helpers for any touched TypeScript tooling.

## Verification

- Run `git diff --check`.
- Run `dotnet test Wildfire.slnx --no-restore`.
- Run `bun run typecheck`.

## Notes

- This ticket is the bridge between simulator ash authority and Timberborn gameplay actions.
- 2026-05-20 reconciliation: Jason confirmed this has been tested. Current implementation exposes bounded `AddAsh`, `RemoveAsh`, `SetAsh`, and `SetAshContamination` changes through `FireSimChange`, uploads them through the Unity/Timberborn compute paths, clamps ash amount and contamination, and routes fertile ash application and collection removals through queued simulator changes. Moved to `06-done`.
