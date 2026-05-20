---
ticket: TWF-169
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-084
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-169-fix-burned-trees-not-showing-stumps.md
---

# TWF-169: Fix Burned Trees Not Showing Stumps

## Goal

Fix the bug where trees that have burned all of their fuel do not show as stumps or the accepted burned-leftover visual state.

## Why

Fully burned trees should leave a clear aftermath state. If a tree consumes all fuel but remains visually tree-like, the player cannot tell whether the burn completed, whether the tree is harvestable, or whether the aftermath state persisted correctly.

## Requirements

- Reproduce the issue from current `main` with a tree or cuttable target that burns to zero fuel.
- Identify whether the failure is in simulator fuel-depleted deltas, tree/cuttable consequence routing, native visual mutation, burned texture application, persistence, or live presentation refresh.
- Ensure fully burned trees transition to the accepted stump or burned-leftover visual state when a safe native API exists.
- If Timberborn does not expose a safe stump mutation path, record precise safe-unavailable telemetry and ensure the player-facing status/counter does not imply a stump was applied.
- Preserve `TWF-084` safety boundaries: do not use unsafe object destruction or broad tree mutation that risks corrupting the save.
- Add deterministic coverage for the tree fully-burned consequence path.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-084` owns tree/cuttable burn consequences and the safe-unavailable boundary for native tree mutation.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start by comparing the existing tree consequence path with the crop burned-leftover path, but do not copy crop behavior blindly if native tree APIs differ.
- Prefer a narrow fix to the final visual-state transition over broad tree consequence refactors.
- Pay attention to save/reload behavior if the visual state is persisted separately from the simulator fuel state.

## Verification

- Run `git diff --check`.
- Run targeted tree/cuttable consequence tests.
- Run `dotnet test`.
- Live QA must capture a tree burning to zero fuel and then show either the stump/burned-leftover visual state or precise safe-unavailable telemetry with copied `Player.log` and status output.

## Notes

- Created on 2026-05-20 from Jason's bug report that trees that burned all their fuel are not showing as stumps.
- 2026-05-20 worker/reviewer pass: implementation in `~/repos/wildfire-TWF-169-burned-tree-stumps` refreshes Timberborn's native natural-resource model after removing remaining cuttable yield and only reports visual success when the leftover model is active. Review passed for QA; no unsafe object destruction or broad tree mutation was introduced. Live QA must still prove the rendered tree state or capture precise safe-unavailable telemetry.
- 2026-05-20 QA attempt from `~/repos/wildfire-TWF-169-burned-tree-stumps`: deploy/build passed, but live QA did not reach a loaded, command-responsive Timberborn state. `load-latest-save-and-unpause` timed out waiting for a known Timberborn screen after repeated `frontmost_bundle_id=com.openai.codex` screenshot-guard failures; `status --wait=5` timed out waiting for `command-outbox.txt`; Computer Use reported `cgWindowNotFound` for `com.mechanistry.timberborn`; copied `Player.log` only reached early Unity/Steam startup lines with no Wildfire mod load or command bridge evidence. No tree burn/stump behavior was exercised. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-169-burned-tree-stumps-qa-20260520T043017Z/`.
