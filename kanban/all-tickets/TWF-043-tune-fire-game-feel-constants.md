---
ticket: TWF-043
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-036
   - TWF-038
   - TWF-041
write_scope:
   - src/Wildfire.Unity/**
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/DESIGN.md
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-043-tune-fire-game-feel-constants.md
---

# TWF-043: Tune Fire Game Feel Constants

## Goal

Tune spread speed, burn duration, heat loss, material flammability, and water suppression so the first fire loop feels coherent in live Timberborn.

## Why

After Sprint 3, fire should be visible and player-facing. The next design step is making that loop feel playable rather than merely proving the pipeline. The tuning must stay in the GPU simulation and adapter-owned material inputs, not in a parallel C# fire path.

## Requirements

- Tune shader constants or material bands for spread speed, burn duration, heat loss, flammability, and suppression.
- Keep fire-spread rules in `FireSim.compute`.
- Keep Timberborn as an adapter that supplies material and water inputs.
- Preserve deterministic stochastic behavior from seed, tick, and cell index.
- Use at least two representative scenarios and one live Timberborn run as tuning evidence.
- Record accepted constants, commands, artifact paths, screenshots, and interpretation in `docs/TEST_PLAN.md`.
- Update `docs/DESIGN.md` only for durable rule-shape or tuning-contract decisions.

## Dependencies

- `TWF-036` provides building consequences.
- `TWF-038` provides water suppression input.
- `TWF-041` provides visual tuning evidence and live visual validation.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run the opt-in Unity shader harness when shader behavior changes.
- QA must capture live screenshots, command/status evidence, copied `Player.log`, and a short note explaining whether the tuned loop is playable.

## Notes

- Do not broaden this into new mechanics such as wind or diagonal spread unless `TWF-044` has already accepted that decision.
