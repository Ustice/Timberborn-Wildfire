---
ticket: TWF-084
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-075
  - TWF-114
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-084-apply-tree-burn-consequences.md
---

# TWF-084: Apply Tree Burn Consequences

## Goal

Use burn damage to reduce cuttable tree yield as fuel burns, then kill and visually mark the tree when its yield is consumed.

## Why

Trees are not just tall crops. They use cuttable yield, vertical or multi-cell mapping, and charred/dead presentation paths that need their own implementation and QA evidence.

## Requirements

- Consume compact fire deltas through the Timberborn consequence path.
- Reduce cuttable tree yield according to accepted burn damage.
- Kill fully burned trees when their remaining yield is consumed.
- Switch burned trees to an accepted dead, burned, stump, or charred presentation when Timberborn exposes a safe visual path.
- Resolve multi-cell or vertical tree occupancy back to one tree target without duplicate damage.
- Preserve host boundaries; do not move fire-spread rules into Timberborn code.
- Add deterministic tests for tree yield loss, full-burn death, duplicate-cell suppression, and no-op behavior for non-burnable or unmapped targets.
- Expose bounded QA/status telemetry for considered tree targets, yield lost, killed targets, and visual-state updates.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-075` provides the burn damage state foundation.
- `TWF-114` provides resource fuel and flammability classification for cuttable yield value.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start with cuttable spec lookup, tree footprint mapping, and the burn damage target resolution from `TWF-075`.
- Damage capacity should be based on remaining cuttable yield, adjusted by the `TWF-114` resource catalog when the yielded good is known.
- Resolve every occupied or vertical cell back to one tree damage target to avoid multiplying damage by height or footprint.
- Safe no-op cases must include unmapped cells, unknown cuttable resources, non-burnable yields, and unavailable death or charred visual APIs.
- Expected counters include considered trees, burnable trees, yield lost, killed trees, footprint cells suppressed, visual updates, skipped unknown resources, and skipped unsafe APIs.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for at least one burned tree consequence, plus copied `Player.log` and status counters.

## Notes

- Crop burn consequences belong to `TWF-076`.
- Structure burn consequences belong to `TWF-077`.
- Avoid one alert per tree. Player-facing aggregation belongs to `TWF-080`.
- Relevant design reference: `docs/DESIGN.md` section 20, "Burn Damage State".
- 2026-05-03 coordinator: moved to `03-in-progress` for Sprint 7 in `~/repos/wildfire-TWF-084` on branch `codex/TWF-084-tree-burn-consequences`, based on reviewed and integrated `TWF-075` commit `00eac0e7ac12b1f7084be3e5bfaba2dbff1033e3`.
- 2026-05-03 coordinator: keep this tree lane separate from blocked `TWF-076` crop consequences; do not depend on unintegrated crop code. Required review is a hard gate; if review fails, return through `03-in-progress` for fixes, then back to `04-verify` for fresh review before integration.
- 2026-05-03 worker result: deterministic implementation committed in `~/repos/wildfire-TWF-084` at `6b800354e7775b1528ad563efb5b3b1f110cc5d0`. The worker added an adapter-local tree/cuttable consequence sink, safe-unavailable API boundary, yield-loss/death/visual consequence records, tree telemetry summary, and QA status counters without using unintegrated `TWF-076` crop code.
- 2026-05-03 worker evidence: deterministic tests cover tree yield loss, full-burn death and burned visual calls through a safe adapter API, duplicate multi-cell and vertical footprint suppression, unmapped no-op cases, unknown cuttable resources, non-burnable targets, skipped unsafe APIs, and telemetry propagation.
- 2026-05-03 worker verification: targeted `dotnet test --filter FullyQualifiedName~TimberbornTreeBurnConsequenceTests` passed with `8/8`, full `dotnet test` passed with `167/167`, `dotnet build Wildfire.slnx` passed with `0` warnings and `0` errors, and `git diff --check` passed.
- 2026-05-03 worker blocker: live Timberborn launch/load remains blocked, so no live evidence was captured or claimed. Before integration, QA still needs deployed mod evidence, loaded/unpaused save with a registered tree target, fire delta consuming tree fuel, `status`/`qa-readiness` tree counters, and copied `Player.log`.
- 2026-05-03 coordinator: moved to `04-verify` for review. Even if review passes, do not move to `05-integration` until required live QA evidence passes.
- 2026-05-03 review: failed review on commit `6b800354e7775b1528ad563efb5b3b1f110cc5d0`. Deterministic checks passed, but tree consequences were not wired into the live runtime, so `CreateDeltaConsumerSinks` would still use `NullTimberbornBurnDamageSink` and tree-burn QA/status counters would remain zero. Review also found `InitialYieldAmount` was dropped before burn-damage registration, so capacity could come from the static descriptor rather than the registered remaining cuttable yield.
- 2026-05-03 coordinator: moved back to `03-in-progress` for fixes. Required fixes: wire the tree burn sink/target registration enough that live runtime can produce nonzero tree-burn evidence after launch is unblocked, preserve registered remaining cuttable yield in burn-damage capacity semantics, and add deterministic tests for runtime binding and descriptor-versus-remaining-yield mismatch. After fixes land, move back to `04-verify` for fresh review before integration or blocked-live-gate disposition.
- 2026-05-03 worker fix: committed `5788353736103ee249c24103b195f9909fe259d1`. `TimberbornFireRuntime` now owns a `TimberbornTreeBurnRuntimeBinding`, exposes `RegisterTreeBurnTargets`, logs the `tree_burn` lane, and passes the binding's burn-damage sink into `CreateDeltaConsumerSinks`. `TimberbornTreeBurnTargetRegistration.InitialYieldAmount` now flows into burn-damage registration as a per-target resource-yield override.
- 2026-05-03 worker fix verification: `git diff --check` passed, targeted `dotnet test --filter FullyQualifiedName~TimberbornTreeBurnConsequenceTests` passed with `11/11`, targeted `dotnet test --filter FullyQualifiedName~TimberbornQaCommandBridgeTests` passed with `29/29`, full `dotnet test` passed with `170/170`, and `dotnet build Wildfire.slnx` passed with `0` warnings and `0` errors.
- 2026-05-03 coordinator: moved back to `04-verify` for fresh review after failed-review fixes. Do not move to integration unless the new review passes and required live QA evidence passes.
- 2026-05-03 fresh review: passed deterministic review on commit `5788353736103ee249c24103b195f9909fe259d1` with no blocking findings. The prior runtime-binding and remaining-yield capacity findings are fixed, `Wildfire.Core` remains untouched, the diff does not depend on unintegrated `TWF-076` crop code, and tree work remains scoped away from crop, structure, storage, explosive, ash, beaver, UI, and release-setting behavior.
- 2026-05-03 fresh review verification: `git diff --check` passed, targeted tree tests passed with `11/11`, targeted QA command bridge tests passed with `29/29`, full `dotnet test` passed with `170/170`, and `dotnet build Wildfire.slnx` passed with `0` warnings and `0` errors.
- 2026-05-03 coordinator: moved to `07-blocked`, not integration. Required live gate: deploy the mod after launch/load is unblocked, load and unpause a save, register at least one real tree or cuttable target, drive a compact fire delta that consumes tree fuel, and capture copied `Player.log` plus `status` or `qa-readiness` showing nonzero tree-burn considered/burnable telemetry and honest unsafe-API counters if tree death or visual APIs remain unavailable.
