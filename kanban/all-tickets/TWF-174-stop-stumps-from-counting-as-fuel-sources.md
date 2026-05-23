---
ticket: TWF-174
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies: []
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-174-stop-stumps-from-counting-as-fuel-sources.md
---

# TWF-174: Stop Stumps From Counting As Fuel Sources

## Goal

Stumps should blacken as burn aftermath, but they should not import as simulator fuel sources or sustain fire.

## Requirements

- Identify how Timberborn tree stumps are represented in the current tree/resource import path.
- Exclude stumps from burnable fuel source import while preserving visual blackening or aftermath state.
- Do not remove or weaken normal standing-tree, bush, crop, or vegetation fuel behavior.
- Keep the simulation core host-agnostic; the stump classification belongs in the Timberborn adapter/import layer.
- Add telemetry or deterministic coverage that distinguishes stump cells from burnable tree cells.
- Live QA should prove a stump does not start or sustain fire, while nearby valid fuel still behaves normally.

## Dependencies

- None.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start from the Timberborn resource/tree import path and identify the native state or model distinction between a standing fuel source and a stump.
- Keep the stump classification adapter-local; do not add stump-specific rules to `Wildfire.Core`.
- Preserve burned aftermath visuals. The target behavior is "stump can blacken", not "stump disappears" or "tree aftermath stops rendering".

## Verification

- Run `git diff --check`.
- Run the focused deterministic tests covering Timberborn tree/resource import and fire target classification.
- Run `dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj --no-restore` if the import classification touches shared mapper behavior.
- Deploy and run live Timberborn QA with at least one stump visible or intentionally created; confirm it blackens only and is not counted as a fuel source.

## Notes

- Reported live on 2026-05-23: "Stumps should not count as a fuel source. They should just blacken."
- 2026-05-23 coordinator: moved to `02-ready` during GitHub issue migration because the bug is concrete and dependency-free. Migrated to GitHub as <https://github.com/Ustice/Timberborn-Wildfire/issues/39>.
