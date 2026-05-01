---
ticket: TWF-002
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
   - TWF-001
write_scope:
   - src/Wildfire.Cli/**
   - tests/**
---

# TWF-002: Add CLI Scenarios And Config

## Goal

Make the CLI harness useful for tuning without Timberborn.

## Requirements

- Add named seeded scenarios for single ignition, line of fuel, water barrier, vertical fuel column, sparse forest, building cluster, and mixed terrain.
- Add seed, dimensions, layer, and scenario options.
- Keep visuals derived from packed cells.
- Keep the CLI dependent only on `Wildfire.Core`.
- A mock is okay for now, but it should not pretend to be an implementation

## Role

- Worker using [../roles/worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run test`.
- Run at least one CLI scenario manually and record the command.

## Notes

- This ticket does not require Timberborn.

## Worker Notes

- 2026-05-01: Added a CLI-owned scenario catalog with named seeded scenarios: `single-ignition`, `line-of-fuel`, `water-barrier`, `vertical-fuel-column`, `sparse-forest`, `building-cluster`, and `mixed-terrain`.
- 2026-05-01: Added CLI options for `--scenario`, `--seed`, `--width`, `--height`, `--depth`, `--layer`, `--ticks`, `--delay-ms`, `--list-scenarios`, and `--help`. Existing positional `<width> <height> <depth> <seed>` input still works.
- 2026-05-01: Visual output still maps each displayed symbol/color from `PackedCell` fields via the renderer.
- 2026-05-01: Manual CLI evidence: `dotnet run --project src/Wildfire.Cli -- --scenario=mixed-terrain --seed=42 --width=12 --height=8 --depth=2 --layer=1 --ticks=3 --delay-ms=0` exited `0` and rendered ticks `1`, `2`, and `3` for `scenario=mixed-terrain`.
- 2026-05-01: `dotnet run --project src/Wildfire.Cli -- --list-scenarios` exited `0` and listed all seven required scenarios.
- 2026-05-01: `git diff --check` exited `0`.
- 2026-05-01: `bun run test` exited `0` with `26` passing tests.
- 2026-05-01: No blockers or unresolved unknowns from the worker pass.
