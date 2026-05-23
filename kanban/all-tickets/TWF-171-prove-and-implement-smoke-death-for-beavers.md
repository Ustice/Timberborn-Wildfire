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

# TWF-171: Prove Choking Incapacitation and Smoke Death for Beavers

## Goal

Implement choking incapacitation for beavers under sustained severe respiratory smoke exposure, then implement actual smoke death only after Timberborn exposes a safe, recoverability-understood native API path.

## Why

`TWF-085` proves the respiratory ladder through visible coughing and choking status icons, reversible work-speed debuffs, recovery, and death-candidate telemetry. Choking should become more serious than a stronger slowdown: beavers should be incapacitated while choking if Timberborn exposes a safe reversible API. Death remains the final irreversible state and still needs separate proof before it becomes release behavior.

## Requirements

- Start from the `TWF-085` respiratory accumulation, choking status, and death-candidate telemetry instead of adding a separate smoke behavior system.
- Research Timberborn-native sleep, injury, incapacitation, health, death, or character-removal APIs and document the safest available path for each severity step.
- Prove that choking incapacitation is reversible, deterministic enough for QA, save/load safe, and does not destabilize normal worker/building assignment.
- Apply choking incapacitation only after sustained severe respiratory exposure reaches the choking threshold.
- Recover choking incapacitation when exposure clears or decays below the recovery threshold.
- Require sustained severe respiratory exposure after choking incapacitation before death can be applied.
- Keep incapacitation and death disabled with explicit skipped-unsafe telemetry if no safe native API is proven.
- Add deterministic tests for threshold gating, sustained exposure, choking incapacitation, recovery before death, skipped unsafe API behavior, and status/counter reporting.
- Expose bounded QA/status telemetry for choking incapacitation attempts, applied incapacitations, recovered incapacitations, smoke death attempts, applied deaths, skipped unsafe APIs, and any native API failure.
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

- Treat choking incapacitation as the next respiratory state after `TWF-085` choking slowdown, not as a shortcut from one smoky tick.
- Treat death as the final respiratory state after sustained choking incapacitation.
- Prefer native sleep, injury, incapacitation, health, or death abstractions if Timberborn exposes them; avoid arbitrary entity destruction or reflection-heavy mutation unless a research spike proves it safe.
- Do not apply death from toxic smoke here unless it flows through the shared respiratory-death gate; toxic-smoke-specific effects remain owned by `TWF-086`.
- Live QA must distinguish "choking candidate observed", "native incapacitation safely applied", "death candidate observed", and "native death safely applied".

## Verification

- Run `git diff --check`.
- Run focused deterministic behavior tests for the respiratory choking incapacitation and smoke death gates.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Deploy and launch Timberborn, then capture `status` or `qa-readiness` showing sustained severe smoke exposure and either applied/recovered choking incapacitation with no runtime errors or precise skipped-unsafe telemetry. If death is implemented too, capture the separate death gate proof.

## Notes

- This ticket exists because `TWF-085` deliberately accepted coughing/choking status and slowdown behavior while leaving incapacitation and death behind separate safety gates.
- A passing implementation must not regress the visible coughing/choking icons or reversible slowdown behavior proven by `TWF-085`.
