---
ticket: TWF-000
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies: []
write_scope:
   - src/Wildfire.Cli/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
---

# TWF-000: Add CLI Fixture Export

## Goal

Add a CLI mode that exports seeded scenario grids as deterministic shader fixtures. The output should be easy for a future shader test harness to load without running Timberborn.

## Why

The project has one authoritative GPU simulation path, but shader work still needs repeatable input data. Fixture export gives developers stable starting grids, seeds, dimensions, and expected metadata without recreating scenario setup by hand.

## Requirements

- Add a CLI option such as `--export-fixture=<path>` that writes the selected scenario to disk.
- Include scenario name, seed, width, height, depth, selected layer metadata, and packed cell values.
- Use a structured format that is easy to parse from C# or TypeScript, such as JSON.
- Keep export deterministic: the same scenario, seed, and dimensions must produce identical output.
- Do not add a C# fire-spread simulation path.
- Add tests for export shape and deterministic output.
- Update `docs/TEST_PLAN.md` with the fixture format and intended shader-test use.

## Dependencies

- Current scenario preview CLI.
- Current packed cell and grid helpers.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run the new export command twice with the same arguments and confirm identical output.

## Notes

- Prefer adding a small exporter class instead of putting all serialization directly in `Program.cs`.
- Keep the output small and explicit; this is a test fixture, not a save format.
- Worker implementation in `~/repos/wildfire-TWF-000` on branch `codex/TWF-000-cli-fixture-export`.
- Added `--export-fixture=<path>` to write deterministic JSON from the selected scenario without running any C# fire-spread simulation path.
- Fixture JSON includes `formatVersion`, scenario, seed, grid dimensions, selected layer index/offset/cell count, `uint16` packed value metadata, and full-grid packed values in `x + y * width + z * width * height` order.
- Evidence: `dotnet test` passed with 19 tests.
- Evidence: two identical CLI exports for `sparse-forest`, seed `42`, dimensions `9x7x2`, layer `1` compared equal with `cmp -s`.
- Evidence: `git diff --check` passed.
- Blockers: none found.
- Integrated on `main` in commit `1dadc50`.
- Coordinator verification after integration: `git diff --check`, `dotnet test`, `dotnet build Wildfire.slnx`, and repeated CLI export comparison all passed.
