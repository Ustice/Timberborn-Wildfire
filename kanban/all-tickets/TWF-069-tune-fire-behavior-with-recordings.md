---
ticket: TWF-069
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-065
   - TWF-043
   - TWF-046
write_scope:
   - src/Wildfire.Unity/**
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/DESIGN.md
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-069-tune-fire-behavior-with-recordings.md
---

# TWF-069: Tune Fire Behavior With Recordings

## Goal

Tune how fire behaves over time using low-resolution recordings and deterministic shader evidence, separate from visual-effect polish.

## Why

Visual effects answer whether fire looks legible; behavior tuning answers whether fire spreads, cools, suppresses, and burns out at a playable pace. Those loops need different evidence. Low-resolution recordings are enough for behavior review and cheaper to compare across scenarios.

## Requirements

- Use low-resolution recordings from `TWF-065` to compare behavior across representative scenarios.
- Keep fire-spread rules in `FireSim.compute`.
- Preserve the `TWF-044` release decisions unless a new design decision explicitly changes them.
- Tune behavior constants only when supported by deterministic shader snapshots and live recording evidence.
- Include at least single ignition, line of fuel, water barrier, sparse forest, and building cluster behavior review.
- Keep visual-effect scale, prefab selection, and screenshot aesthetics out of this ticket.
- Update `docs/DESIGN.md` only for durable behavior-contract changes.
- Document accepted behavior evidence, commands, recordings, and interpretation in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-065` provides the recording tool.
- `TWF-043` provides the current game-feel tuning baseline.
- `TWF-046` proves the coherent live loop.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run the opt-in Unity shader harness when shader behavior changes.
- QA must capture low-resolution behavior recordings plus command/status evidence for any accepted live tuning scenario.

## Notes

- This ticket should not change visual presentation unless a behavior bug makes the existing effects impossible to evaluate.
