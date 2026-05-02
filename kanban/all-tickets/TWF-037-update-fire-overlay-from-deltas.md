---
ticket: TWF-037
agent_level: Medium
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
   - kanban/all-tickets/TWF-037-update-fire-overlay-from-deltas.md
---

# TWF-037: Update Fire Overlay From Deltas

## Goal

Update a Timberborn-facing overlay or debug visualization from compact changed cells so fire state can be inspected without creating one entity per cell.

## Why

The design calls out overlay updates as a primary delta use and explicitly says Timberborn should update only where needed. This ticket gives QA and future gameplay work a readable map of changed fire state before the richer GPU visual pipeline is fully player-polished.

## Requirements

- Consume compact deltas rather than scanning the whole grid each update.
- Keep overlay state derived from packed cells; do not add adapter-side fire rules.
- Use an existing Timberborn-safe visual/debug surface if one is available.
- Keep updates bounded to changed cells and expose counters for updated cells.
- Add deterministic tests for visual-state-change filtering and update routing.
- Document live evidence expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-033` proves delta consumers can produce a visible or gameplay-relevant result.
- `TWF-035` makes mapped cell values more meaningful.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live status counters, `Player.log` evidence, and screenshots if the overlay is visible.

## Notes

- This is the practical inspection layer. Rich pooled fire and smoke effects belong to `TWF-040`.
