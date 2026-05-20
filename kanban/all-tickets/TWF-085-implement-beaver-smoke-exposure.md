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

Implement the normal smoke beaver behavior variant: coughing slowdown, choking incapacitation if safe, and death only after sustained severe exposure is proven safe.

## Why

Normal smoke should mostly punish staying near a smoky field over time. It needs its own ticket so it can tune duration, thresholds, work interruption, and QA evidence without getting tangled with toxic smoke or direct fire damage.

## Requirements

- Consume smoke exposure classifications from `TWF-072` through the `TWF-073` behavior harness.
- Implement coughing as a bounded slowdown or work-inefficiency effect if Timberborn exposes a safe API.
- Implement choking as incapacitated or sleep-like behavior only if Timberborn exposes a safe API.
- Implement death only after sustained severe exposure and only if the safer states are proven in live evidence.
- Cancel or interrupt unsafe work where Timberborn supports it.
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
- Coughing should be the first live proof target because it is reversible and lower risk.
- Choking should use a sleep-like or incapacitated state only after the API spike proves that state can be entered and recovered safely.
- Death must stay gated behind sustained severe exposure and separate live evidence.
- Expected counters include exposed beavers, accumulated smoke exposure, coughing entered, coughing recovered, choking entered, choking recovered, deaths, batch skips, and unsafe API skips.
- Safe no-op behavior should still record telemetry and avoid killing or immobilizing beavers.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for coughing or an explicit safe unavailable state; choking and death require separate accepted evidence before they can be called implemented.

## Notes

- Toxic smoke behavior belongs to `TWF-086`.
- Fire and heat behavior belongs to `TWF-087`.
- Relevant design reference: `docs/DESIGN.md` section 20, "Beaver Field Effects".
- Reviewed implementation is parked on `~/repos/wildfire-TWF-085-beaver-smoke` / `codex/TWF-085-beaver-smoke` commit `4bdff943`. It adds normal-smoke accumulation, bounded batch/cooldown accounting, reversible coughing safe-no-op state, recovery decay, and skipped-unsafe candidate telemetry for choking/death.
- Coordinator review reran targeted behavior and command-token tests on 2026-05-20: `dotnet test Wildfire.slnx --no-restore --verbosity minimal --filter "FullyQualifiedName~TimberbornBeaverFieldBehaviorTests|FullyQualifiedName~TimberbornQaCommandBridgeTests"` passed `106/106`. Worker also reported full `dotnet test` `455/455`, `dotnet build`, and `git diff --check`.
- Live QA is blocked: `bun scripts/invoke-timberborn-command.ts status --wait=5` wrote the command inbox but timed out waiting for `command-outbox.txt`, and the process list showed Steam without a loaded Timberborn process. No coughing/smoke behavior is accepted until the same live gate passes.
