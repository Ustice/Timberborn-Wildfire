---
ticket: TWF-006
agent_level: High
role: worker
requires_qa: false
doc_only: false
dependencies:
   - TWF-003
   - TWF-005
write_scope:
   - src/Wildfire.Unity/**
   - tests/**
---

# TWF-006: Add Full-Grid Unity Compute Snapshot Parity Harness

## Goal

Implement the first `Wildfire.Unity` compute prototype as a full-grid parity harness against the CPU snapshot suite.

## Requirements

- Add a first-pass `FireSim.compute` full-grid kernel using double-buffered `uint` cell buffers.
- Keep the CPU packed cell format in the lower 16 bits of each GPU `uint`; high bits must remain zero on readback.
- Mirror the current `PackedCell`, `FireRules.StepCell`, 6-neighbor heat/burning checks, and `FireRandom.Hash` behavior closely enough for snapshot parity.
- Compare the `single-ignition-point`, `water-barrier`, and `vertical-fuel-column` CPU snapshot scenarios exactly on final packed grid and per-tick delta counts.
- Produce mismatch output with scenario, tick, coordinates, CPU value, GPU value, and suspected rule area.
- Leave active-frontier GPU optimization, visual textures, Timberborn integration, and gameplay consumption of GPU deltas out of scope.
- Keep the CPU backend available and unchanged.

## Dependencies

- `TWF-003` CPU snapshot suite is merged.
- `TWF-005` Unity compute prototype plan is merged.

## Role

- Worker using [../roles/worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run test`.
- Run the new Unity compute parity harness or document the exact local blocker if Unity runtime execution is not available.

## Notes

- Start with full-grid dispatch before any GPU active-frontier optimization.
- CPU snapshots remain the oracle for the first compute pass.
