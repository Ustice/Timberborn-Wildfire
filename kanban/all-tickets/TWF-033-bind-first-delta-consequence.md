---
ticket: TWF-033
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-032
write_scope:
   - src/Wildfire.Timberborn/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-033-bind-first-delta-consequence.md
---

# TWF-033: Bind First Delta Consequence

## Goal

Bind one concrete Timberborn-facing consequence to non-zero fire deltas and prove it in a loaded save.

## Why

`TWF-009` created the delta-consumer hook surfaces and telemetry, but live evidence still has zero consequence counters because no non-zero deltas were produced. Once `TWF-032` proves changed cells, this ticket should make one narrow result visible or gameplay-relevant without turning Timberborn into the simulation owner.

## Requirements

- Choose one first consequence lane: debug visual state, pooled visual effect, building/resource gameplay consequence, or user-facing alert.
- Bind through the existing Timberborn delta-consumer sink surface where possible.
- Keep fire rules and grid mutation out of the Timberborn adapter.
- Apply work only for changed cells from compact deltas.
- Expose or preserve status counters so QA can see the consequence count move above zero.
- Add deterministic tests for the consequence decision/routing logic where possible.
- Document live validation expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-032` proves live non-zero GPU deltas are available.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx` if Timberborn adapter bindings change.
- QA must use the guarded startup utility and live stimulus path, then capture status counters, screenshots when visual behavior is expected, and copied `Player.log`.
- Passing evidence requires at least one relevant `last_delta_consumer_*` counter greater than zero.

## Notes

- Prefer the smallest reversible consequence that proves the path.
- Do not broaden into full gameplay tuning, balance, or multiple effect systems in this ticket.
