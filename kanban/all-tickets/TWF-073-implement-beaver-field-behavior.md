---
ticket: TWF-073
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-071
  - TWF-072
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-073-implement-beaver-field-behavior.md
---

# TWF-073: Add Beaver Field Behavior Harness

## Goal

Add the shared Timberborn-side harness that applies beaver behavior decisions from exposure telemetry, without owning each smoke, toxic smoke, or fire progression in one oversized ticket.

## Why

Once beaver exposure can be detected, Wildfire needs a safe behavior application surface before individual danger progressions can be implemented. Smoke, toxic smoke, and fire have different player meanings and should be implemented as separate focused tickets using the same harness.

## Requirements

- Implement the shared beaver behavior dispatcher, status state, persistence hooks, QA counters, and test seams needed by the variant tickets.
- Use `TWF-072` exposure telemetry as the source of evidence and QA observability.
- Provide bounded decision hooks for smoke, toxic smoke, and fire/heat variants.
- Do not implement final coughing, choking, singed, burned, contamination, or death behavior here except as no-op/test decisions needed to prove the harness.
- Keep behavior reversible and safe across save/reload where possible.
- Do not move fire rules into Timberborn; Timberborn may only react to field exposure.
- Add deterministic tests for routing, throttling, persistence, and safe no-op behavior.
- Preserve logs, status counters, screenshots or recordings, command output, and final QA lock state.
- Document accepted live behavior evidence in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-071` defines the accepted beaver behavior contract.
- `TWF-072` proves beaver exposure detection and telemetry.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Build one dispatcher that variant tickets call through; do not let each variant invent its own persistence, throttling, or API probing.
- Keep variant state small and versioned so save/reload can recover without trapping beavers in a bad state.
- Prefer reversible decisions first: no-op telemetry, work interruption, path avoidance, and temporary debuffs before incapacitation or death.
- Expected counters include decisions evaluated, decisions applied by variant, decisions skipped by cooldown, persistence saves, persistence loads, unsafe API skips, and recovery actions.
- Safe no-op behavior should leave all beavers in vanilla state while proving the dispatcher can receive exposure classifications.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- QA must capture live evidence that the behavior harness can observe accepted exposure telemetry and apply or skip a bounded no-op decision without critical exceptions.

## Notes

- Smoke behavior belongs to `TWF-085`.
- Toxic smoke behavior belongs to `TWF-086`.
- Fire and heat behavior belongs to `TWF-087`.
- Relevant design reference: `docs/DESIGN.md` section 20, "Beaver Field Effects".
- 2026-05-19 coordinator reconciliation: moved back to `03-in-progress` because `~/repos/wildfire-twf-073-beaver-field-behavior` contains the active dispatcher implementation, persistence/status updates, TEST_PLAN changes, and removal of the prior hazard-avoidance path. Next action is worker completion report or reviewer pass against current `main`; do not promote to `04-verify` until the worker evidence and diff are reconciled.
- 2026-05-19 reviewer result: failed. The worktree is stale relative to current `main` and would regress current ash/fertile-ash simulator-owned behavior plus the clean `steam_cells` telemetry vocabulary. The dispatcher also currently runs from status/`qa-readiness` state collection rather than the gameplay update path, making status mutating for future actuators and leaving normal gameplay behavior unapplied unless polled. `git diff --check HEAD` passed in the worktree; `dotnet test` was not run in the read-only review. Old hazard-avoidance references were removed. Worker must rebase/reconcile against current `main`, preserve current ash/fertile-ash and `steam_cells` behavior, move dispatcher execution out of `GetState()`, then rerun focused tests plus `dotnet test` before fresh review and live QA.
- 2026-05-19 worker repair in `~/repos/wildfire-twf-073-beaver-field-behavior`: rebased onto current local `main` (`7f5caae`), moved beaver behavior dispatch out of `GetState()`/`qa-readiness` collection and into `UpdateSingleton()` after simulator dispatch, ash sync, and exposure sampling, preserved simulator-owned ash restore behavior, preserved clean `steam_cells` vocabulary, removed the old navmesh hazard-avoidance path, and added dispatcher persistence/behavior tests including clean steam routing. Verification passed `git diff --check`, focused behavior/QA/persistence tests (`100` passed), and `dotnet test Wildfire.slnx` (`443` passed). Next action is fresh review, then live Timberborn QA if review passes.
- 2026-05-19 reviewer result after worker repair: passed. Dispatcher execution is now in `UpdateSingleton()` after simulator dispatch, ash sync, and world ash effects; status/`qa-readiness` only expose last exposure and behavior counters. Current main code for simulator-owned ash/fertile-ash and clean `steam_cells` vocabulary is preserved. Old beaver hazard-avoidance/navmesh path is removed. Reviewer ran `git diff --check HEAD` and `dotnet test Wildfire.slnx --no-restore` (`443` passed). Live Timberborn QA is still required before integration.
- 2026-05-19 live QA result: passed. Evidence root `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-073-qa-20260519T090318Z` used a QA copy of `Fuel with beavers`, selected a sampled beaver target with `qa-delta-stimulus beaver-exposure`, and proved exposure log output at tick `985` immediately preceding behavior dispatch at tick `985`. Acceptance counters included `exposed_beavers=2`, `respiratory_cells=2`, `burn_cells=2`, `toxic_cells=2`, `decisions_evaluated=6`, `fire_heat_decisions_applied=3`, `noop_decisions_applied=3`, final `beaver_field_behavior_dispatcher_enabled=true`, final `beaver_field_behavior_decisions_evaluated=23`, `beaver_field_behavior_noop_decisions_applied=9`, `beaver_field_behavior_failed_decisions=0`, completed sustained stimulus `12/12`, and an empty strict critical Player.log scan. Minor note: QA used the documented `experimental_mode.start` click because the startup helper's Return key did not dismiss the experimental-mode modal.
- 2026-05-19 integration result: accepted diff integrated into `main`, including the beaver behavior dispatcher, update-path dispatch, status counters, persistence, tests, `docs/TEST_PLAN.md` harness text, and removal of the old hazard-avoidance path. Validation passed `git diff --check`, focused `TWF-073` tests (`100` passed), `dotnet test Wildfire.slnx --no-restore` (`443` passed), and a conflict-marker scan over touched docs/source/tests.
