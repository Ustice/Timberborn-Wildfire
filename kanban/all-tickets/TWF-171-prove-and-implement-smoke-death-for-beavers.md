---
ticket: TWF-171
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-071
  - TWF-072
  - TWF-073
  - TWF-085
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-171-prove-and-implement-smoke-death-for-beavers.md
---

# TWF-171: Prove and Implement Smoke Death for Beavers

## Goal

Implement actual beaver death from sustained severe respiratory smoke exposure only after Timberborn exposes a safe, recoverability-understood native API path.

## Why

`TWF-085` proves the respiratory ladder through visible coughing and choking status icons, reversible work-speed debuffs, recovery, and death-candidate telemetry. It intentionally keeps death as skipped unsafe telemetry because native beaver health/death mutation is irreversible and needs separate proof before it becomes release behavior.

## Requirements

- Start from the `TWF-085` death-candidate telemetry instead of adding a separate smoke behavior system.
- Research Timberborn-native health, injury, death, incapacitation, or character removal APIs and document the safest available path.
- Prove that the selected API is deterministic enough for QA, does not corrupt save/load, and does not destabilize normal worker/building assignment.
- Require sustained severe respiratory exposure after coughing and choking before death can be applied.
- Keep death disabled with explicit skipped-unsafe telemetry if no safe native API is proven.
- Add deterministic tests for threshold gating, sustained exposure, recovery before death, skipped unsafe API behavior, and status/counter reporting.
- Expose bounded QA/status telemetry for smoke death attempts, applied deaths, skipped unsafe APIs, and any native API failure.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-071` defines the accepted beaver field contract.
- `TWF-072` provides exposure telemetry.
- `TWF-073` provides the shared behavior harness.
- `TWF-085` provides respiratory-smoke accumulation, coughing/choking status effects, and death-candidate telemetry.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Treat death as the final respiratory state after coughing and choking, not as a shortcut from one smoky tick.
- Prefer a native injury/health/death abstraction if Timberborn exposes one; avoid arbitrary entity destruction or reflection-heavy mutation unless a research spike proves it safe.
- Do not apply death from toxic smoke here unless it flows through the shared respiratory-death gate; toxic-smoke-specific effects remain owned by `TWF-086`.
- Live QA must distinguish "death candidate observed" from "native death safely applied".

## Verification

- Run `git diff --check`.
- Run focused deterministic behavior tests for the respiratory smoke death gate.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Deploy and launch Timberborn, then capture `status` or `qa-readiness` showing sustained severe smoke exposure and either applied death with no runtime errors or precise skipped-unsafe telemetry.

## Notes

- This ticket exists because `TWF-085` deliberately accepted coughing/choking and left death behind a separate safety gate.
- A passing implementation must not regress the visible coughing/choking icons or reversible slowdown behavior proven by `TWF-085`.
