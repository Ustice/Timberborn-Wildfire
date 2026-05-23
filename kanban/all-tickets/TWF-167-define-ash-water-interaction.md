---
ticket: TWF-167
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-079
  - TWF-164
write_scope:
  - src/Wildfire.Core/**
  - src/Wildfire.Unity/**
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/DESIGN.md
  - docs/ash-simulation-model.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-167-define-ash-water-interaction.md
---

# TWF-167: Define Ash Water Interaction

## Goal

Define and prove the narrow interaction between water and simulator-owned ash without reopening tainted ash decay.

## Why

Tainted ash decay works and is tracked by `TWF-164`. Water contact is a separate behavior question: water may wash ash away, move contamination, or report a safe-unavailable blocker depending on Timberborn APIs and the release design decision. Keeping this separate prevents a working decay path from being blocked by untested water behavior.

## Requirements

- Decide the first release behavior for clean water touching clean ash.
- Decide the first release behavior for clean water touching tainted ash.
- Decide the first release behavior for badwater or contaminated water touching any ash.
- If water washes ash out of a cell, route the ash removal through the simulator ash mutation path.
- If tainted ash can taint water, use a safe Timberborn API or report precise safe-unavailable telemetry.
- Clean water washing fertile ash must not create tainted water unless the release design explicitly accepts that tradeoff.
- Badwater or already contaminated water must not become clean because it touched ash.
- Preserve the no-decontamination rule: ash/water interaction must not reduce native soil, water, goods, plant, building, or beaver contamination.
- Expose status/QA counters for clean ash washed, tainted ash washed, water-taint attempts, water-taint successes, skipped unsafe water APIs, and no-op cases.
- Preserve save/reload behavior for remaining ash after washout.

## Dependencies

- `TWF-079` owns the broader contamination/no-decontamination contract.
- `TWF-164` confirms tainted ash decay is already working and should not be retested here except as a non-regression.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start from the simulator-owned ash mutation path, not adapter-local ash deletion.
- Keep the first behavior bounded and deterministic.
- If safe water-contamination APIs are not available, accept ash washout only with explicit safe-unavailable water-taint telemetry.
- Do not introduce toxic or contaminated steam; steam remains clean suppression vapor.

## Verification

- Run `git diff --check`.
- Run `dotnet test Wildfire.slnx --no-restore`.
- Run `bun run typecheck` if TypeScript tooling changes.
- Run the Unity shader harness if compute shader ash or water behavior changes.
- Live QA must capture before/after ash state, water-contact setup, status/QA counters, copied `Player.log`, and save/reload behavior if ash state changes.

## Notes

- 2026-05-20 created after Jason clarified that tainted ash decay works and the remaining work should split out the ash/water interaction.
- 2026-05-23 coordinator: moved to `02-ready` during GitHub issue migration because dependencies `TWF-079` and `TWF-164` are accepted. Migrated to GitHub as <https://github.com/Ustice/Timberborn-Wildfire/issues/36>.
