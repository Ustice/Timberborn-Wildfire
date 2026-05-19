---
ticket: TWF-086
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-071
  - TWF-072
  - TWF-073
  - TWF-079
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-086-implement-beaver-toxic-smoke-exposure.md
---

# TWF-086: Implement Beaver Toxic Smoke Exposure

## Goal

Implement the contaminated smoke beaver behavior variant, including faster respiratory progression and native contamination effects where safe.

## Why

Toxic smoke should not feel like ordinary smoke with a different color. Contaminated burn sources may use native badwater contamination graphics or treatment flows, and need their own API spike and QA evidence. Steam is clean suppression vapor and must not be included in this toxic behavior lane.

## Requirements

- Consume contaminated smoke or toxic smoke classifications from `TWF-079`.
- Use the `TWF-073` behavior harness rather than implementing a separate behavior path.
- Advance respiratory exposure faster than normal smoke where design constants allow it.
- Apply native badwater contamination effects only if live API tests prove the path safe.
- Preserve native treatment and graphics flows where they are used.
- Avoid treating fire as a decontamination mechanic.
- Add deterministic tests for toxic exposure accumulation, threshold differences from normal smoke, native-effect decision logic, and safe no-op behavior.
- Expose bounded QA/status telemetry for toxic exposure, contamination effect attempts, successes, failures, skipped unsafe APIs, choking, and deaths.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-071` defines the accepted beaver field contract.
- `TWF-072` provides exposure telemetry.
- `TWF-073` provides the shared behavior harness.
- `TWF-079` provides contamination-aware fire classifications.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Use the same processing harness as normal smoke so toxic smoke differs by classification and thresholds, not by a separate behavior system.
- Toxic smoke should progress respiratory exposure faster than normal smoke.
- Native badwater contamination effects are optional and must be proven safe before use.
- Fire must not clear contamination, even when toxic exposure is applied or recovered.
- Expected counters include toxic-exposed beavers, toxic exposure accumulated, native contamination attempts, native contamination successes, contamination skips, choking, deaths, recovery, and unsafe API skips.
- Safe no-op behavior should fall back to telemetry-only toxic exposure classification.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for toxic smoke exposure behavior, or an explicit safe unavailable state with the missing API documented.

## Notes

- Normal smoke behavior belongs to `TWF-085`.
- Fire and heat behavior belongs to `TWF-087`.
- Relevant design references: `docs/DESIGN.md` section 20, "Beaver Field Effects" and "Contamination Interaction".
