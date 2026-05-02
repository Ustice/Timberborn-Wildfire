---
ticket: TWF-045
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
   - TWF-043
   - TWF-044
write_scope:
   - src/Wildfire.Cli/**
   - src/Wildfire.Unity/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-045-add-release-scenario-shader-snapshots.md
---

# TWF-045: Add Release Scenario Shader Snapshots

## Goal

Add accepted shader snapshot coverage for the scenarios that should protect release behavior.

## Why

The test plan names release-relevant scenarios, but the release gate needs accepted evidence after game-feel tuning settles. Snapshot coverage should make shader behavior reviewable without launching Timberborn every time.

## Requirements

- Add or refresh accepted snapshots for single ignition, line of fuel, water barrier, vertical fuel column, sparse forest, building cluster, and mixed terrain/fuel/water unless a scenario is explicitly deferred.
- Record seed, dimensions, tick count, final packed cells, per-tick delta counts, and useful visual checksums or artifacts.
- Keep snapshot changes intentional and reviewable scenario by scenario.
- Ensure append-buffer counter reset behavior remains covered.
- Update `docs/TEST_PLAN.md` with commands and accepted artifact locations.

## Dependencies

- `TWF-043` settles game-feel constants.
- `TWF-044` settles or defers release-blocking simulation decisions.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run the opt-in Unity shader harness for any snapshots that require real shader execution.

## Notes

- Do not bless broad snapshot churn without explaining the behavior change that caused it.
