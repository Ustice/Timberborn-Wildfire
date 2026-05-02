---
ticket: TWF-036
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-033
   - TWF-035
write_scope:
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-036-apply-building-burnout-consequences.md
---

# TWF-036: Apply Building Burnout Consequences

## Goal

Bind one real building consequence to compact fire deltas, such as a reversible debug damage marker, disabled state, or narrowly scoped burnout notification.

## Why

The design lists building damage and destruction checks as primary delta consumers. `TWF-033` proves the first consequence path; this ticket makes that path useful for Timberborn buildings while preserving the rule that listeners react to deltas and enqueue follow-up changes instead of mutating simulation state directly.

## Requirements

- Consume only changed cells from compact deltas.
- Route through the existing Timberborn delta-consumer surface where possible.
- Keep fire rules and grid mutation outside the Timberborn adapter.
- Choose the smallest safe building consequence that can be proven in a live save.
- Expose status counters for considered deltas, matched building cells, and applied consequences.
- Add deterministic tests for the consequence decision and routing logic.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-033` proves a first consequence can be bound.
- `TWF-035` improves the material and building-cell mapping that this ticket consumes.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must use the guarded startup utility and live stimulus path, then capture command output, relevant `Player.log` lines, and status counters above zero.

## Notes

- Prefer a reversible or clearly bounded effect before destructive building changes.
- If no safe Timberborn-owned consequence API is available, move the ticket to blocked with the exact missing API and evidence.
