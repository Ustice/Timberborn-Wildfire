---
ticket: TWF-077
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-064
  - TWF-075
  - TWF-114
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-077-apply-structure-burn-damage-rollback.md
---

# TWF-077: Apply Structure Burn Damage Rollback

## Goal

Turn structure fire damage into construction-value loss, closure while burning, and repair-through-construction behavior after the fire is out.

## Why

`TWF-036` proved a narrow building burnout pause lane, and `TWF-064` hardens that existing consequence. The new design goes further: a damaged building should lose invested construction value instead of simply disappearing, close while burning, block repair until danger is gone, and then recover by receiving replacement materials.

## Requirements

- Consume compact fire deltas through the Timberborn consequence path.
- Close or disable buildings once they are damaged or on fire.
- Prevent repair while active fire or dangerous heat remains.
- Reduce construction-resource value according to burn damage and resource-specific fuel or flammability scores.
- Revert building presentation toward construction or incomplete visuals as damage crosses accepted thresholds.
- Allow repair through Timberborn construction-style resource delivery after fire danger ends.
- Preserve existing settings, inventory, worker assignments, and entity identity where Timberborn APIs make that safe.
- Add deterministic tests for closure, repair gating, material loss, multi-cell rollup, and threshold transitions.
- Expose bounded QA/status telemetry for considered buildings, material value lost, closed buildings, rollback stage, and repair eligibility.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-064` clarifies the existing pausable-building consequence path.
- `TWF-075` provides the burn damage state foundation.
- `TWF-114` provides construction-resource fuel and flammability classification.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start from the existing building consequence and pause/disable path hardened by `TWF-064`.
- Construction-material burn capacity should exclude non-burnable resources from fire fuel while still allowing the building to require repair after burnable supports are lost.
- Metal must not fuel fire. Logs, planks, gears, paper, and other dry construction inputs should use `TWF-114` scores.
- Keep stored inventory separate. Warehouses and piles may lose structural value here, but their contents belong to `TWF-115`.
- Safe no-op cases must include missing construction-resource data, buildings without a safe close/repair API, and visuals that cannot be safely moved to an unfinished state.
- Expected counters include considered structures, closed structures, burnable material value lost by resource, non-burnable value ignored for fuel, rollback stage, repair blocked, repair eligible, visual updates, and skipped unsafe APIs.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for one structure that closes from fire damage and either rolls back to an accepted construction visual or reports a precise safe limitation.

## Notes

- Do not directly destroy live Timberborn entities.
- Crop burn consequences belong to `TWF-076`.
- Tree burn consequences belong to `TWF-084`.
- Relevant design reference: `docs/DESIGN.md` section 20, "Burn Damage State".
- 2026-05-03 coordinator: moved to `07-blocked` during Sprint 7 closeout. This ticket remains blocked on `TWF-064` live building-burnout pause consequence investigation, which is itself blocked by the Steam/Timberborn launch/load issue.
- 2026-05-03 coordinator update: the upstream shared blocker is now command-responsive loaded-save QA, not the older Steam prompt. Keep this ticket blocked until `TWF-050` restores the command bridge and `TWF-064` resolves the building-burnout pause consequence, then reassess whether structure rollback can return to ready or verify.
- 2026-05-03 coordinator: do not start structure rollback until `TWF-064` has live evidence or the scope is explicitly narrowed. Required review remains a hard gate; if review later fails, return through `03-in-progress` for fixes, then back to `04-verify` for fresh review before integration.
- 2026-05-03 unblock: `TWF-064` is now done with live proof that the building-burnout pause consequence applied and durable status telemetry records it. Coordinator moved this ticket back to `02-ready`; it remains outside the current Sprint 8 live-QA recovery slice unless explicitly pulled forward.
- 2026-05-05 worker: implemented the first conservative structure burn-damage rollback lane. Compact fire deltas now resolve building-like targets, suppress duplicate structure cells by stable target id, calculate construction-value loss from burnable resources through the shared burn-damage capacity calculator, close pausable buildings while active danger remains, block repair until danger cools, and emit rollback-stage telemetry. The live Timberborn adapter preserves entity identity and reports safe-unavailable visual rollback instead of destroying entities or forcing unfinished-state presentation through an unproven API.
- 2026-05-06 reviewer: failed review. The current live wiring resolves structure targets directly from Timberborn delta cells instead of consuming the accepted `TWF-075` registered burn-damage ownership/state surface. This bypasses the single-owner, multi-cell/vertical rollup, persisted target state, and descriptor/catalog dependency boundary. The live adapter also does not represent repair-through-construction or precise skipped repair API telemetry when repair would otherwise be due. Deterministic sink behavior remains useful, but this ticket must return to implementation and pass a fresh review before live QA or integration.
- 2026-05-06 worker fix: wired the live runtime to register `TWF-075` burn-damage targets before delta consumption and pass the shared burn-damage service into the structure rollback lane. The structure sink now requires `TWF-075` ownership for bound live runs and uses the shared damage capacity/state before applying close/rollback telemetry. Added deterministic ownership mismatch and shared-state tests.
