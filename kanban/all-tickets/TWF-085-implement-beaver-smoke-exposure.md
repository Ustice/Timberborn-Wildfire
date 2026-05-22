---
ticket: TWF-085
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-071
  - TWF-072
  - TWF-073
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-085-implement-beaver-smoke-exposure.md
---

# TWF-085: Implement Beaver Smoke Exposure

## Goal

Implement the respiratory smoke beaver behavior variant: visible coughing/choking status icons, reversible work-speed debuffs, and death only after sustained severe exposure is proven safe in a separate follow-up.

## Why

Respiratory smoke should mostly punish staying near a smoky field over time. Clean, contaminated, and toxic smoke all need to accumulate toward coughing/choking so live behavior matches what players see in smoky areas; the dispatcher should still keep toxic-smoke decision telemetry distinguishable from clean smoke and direct fire damage.

## Requirements

- Consume smoke exposure classifications from `TWF-072` through the `TWF-073` behavior harness.
- Implement coughing as a floating status icon plus bounded slowdown or work-inefficiency effect if Timberborn exposes safe APIs.
- Implement choking as a floating status icon plus stronger reversible slowdown; do not force incapacitation unless a separate proof validates the API and recovery path.
- Implement death only after sustained severe exposure and only if the safer states are proven in live evidence.
- Keep work cancellation/path mutation out unless Timberborn exposes a proven safe wrapper.
- Add deterministic tests for exposure accumulation, threshold transitions, recovery or decay, and safe no-op behavior.
- Expose bounded QA/status telemetry for exposed beavers, coughing, choking, deaths, skipped unsafe APIs, and recovery.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-071` defines the accepted beaver field contract.
- `TWF-072` provides exposure telemetry.
- `TWF-073` provides the shared behavior harness.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Use exposure accumulation over multiple simulation ticks; do not require every exposed beaver to be processed in one game tick if the adapter can batch safely.
- Coughing should show the smoke status icon and apply the lower reversible worker-speed debuff for clean, contaminated, or toxic respiratory smoke.
- Choking should show the priority smoke status icon and apply the stronger reversible worker-speed debuff for clean, contaminated, or toxic respiratory smoke.
- Death must stay gated behind sustained severe exposure and separate live evidence.
- Expected counters include exposed beavers, accumulated smoke exposure, coughing entered, coughing recovered, coughing slowdowns applied/recovered/skipped, choking entered, choking slowdowns applied/recovered/skipped, death candidates, batch skips, and unsafe API skips.
- Safe no-op behavior should still record telemetry and avoid killing or immobilizing beavers.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for the coughing or choking floating status icon, matching worker-speed debuff telemetry, and recovery. Death requires separate accepted evidence before it can be called implemented.

## Notes

- Toxic-smoke-specific consequences beyond the shared respiratory coughing/choking debuff belong to `TWF-086`.
- Fire and heat behavior belongs to `TWF-087`.
- Relevant design reference: `docs/DESIGN.md` section 20, "Beaver Field Effects".
- Worker implementation adds respiratory-smoke accumulation inside the existing TWF-073 dispatcher: bounded dispatch batches, cooldown skips, clean/contaminated/toxic smoke sample accumulation, reversible coughing/choking status states, worker-speed debuffs, decay/recovery after exposure clears, and candidate/skipped telemetry for death.
- Runtime uses the narrow smoke-reaction actuator for `StatusSubject` icon toggles and `Worker.WorkingSpeedMultiplier` debuffs. No native beaver health, incapacitation, pathing, arbitrary work cancellation, or death API is invoked.
- Verification added/updated deterministic behavior tests, command status token coverage, and `docs/TEST_PLAN.md` live QA expectations. Final command results are recorded in the worker handoff response.
- Reviewed implementation was previously parked on `~/repos/wildfire-TWF-085-beaver-smoke` / `codex/TWF-085-beaver-smoke` commit `4bdff943`. It adds normal-smoke accumulation, bounded batch/cooldown accounting, reversible coughing safe-no-op state, recovery decay, and skipped-unsafe candidate telemetry for choking/death.
- Coordinator review reran targeted behavior and command-token tests on 2026-05-20: `dotnet test Wildfire.slnx --no-restore --verbosity minimal --filter "FullyQualifiedName~TimberbornBeaverFieldBehaviorTests|FullyQualifiedName~TimberbornQaCommandBridgeTests"` passed `106/106`. Worker also reported full `dotnet test` `455/455`, `dotnet build`, and `git diff --check`.
- Earlier live QA was blocked when `bun scripts/invoke-timberborn-command.ts status --wait=5` timed out waiting for `command-outbox.txt` and the process list showed Steam without a loaded Timberborn process. No coughing/smoke behavior is accepted until the same live gate passes.
