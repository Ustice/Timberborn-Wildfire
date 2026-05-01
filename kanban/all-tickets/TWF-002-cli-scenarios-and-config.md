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

## Dependencies

- TWF-001 should settle CPU tick semantics first.

## Role

- Worker using [../roles/worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run test`.
- Run at least one CLI scenario manually and record the command.

## Notes

- This ticket does not require Timberborn.
