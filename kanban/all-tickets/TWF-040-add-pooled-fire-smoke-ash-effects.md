---
ticket: TWF-040
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-039
write_scope:
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-040-add-pooled-fire-smoke-ash-effects.md
---

# TWF-040: Add Pooled Fire Smoke Ash Effects

## Goal

Add a bounded pooled-effects layer that presents fire, smoke, and ash from simulator output without creating one effect object per simulated cell.

## Why

The design explicitly avoids one entity per visual fire, smoke, or ash cell. Once the visual field reaches Timberborn, the adapter needs a pooled presentation layer that looks alive while staying bounded and easy to disable during QA.

## Requirements

- Drive effect placement or intensity from visual-field output and compact deltas where appropriate.
- Use pooling or another bounded strategy with clear maximum counts.
- Prefer Timberborn-native visual assets or effect patterns before custom approximations.
- Expose counters for active pooled effects and updated visual regions.
- Add deterministic tests for selection, pooling limits, and update routing where possible.
- Document screenshot and log evidence expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-039` provides the Timberborn visual-field binding.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live screenshots, relevant status counters, copied `Player.log`, and final lock state.

## Notes

- Keep this focused on presentation. Fire spread, material tuning, and gameplay damage belong to other tickets.
