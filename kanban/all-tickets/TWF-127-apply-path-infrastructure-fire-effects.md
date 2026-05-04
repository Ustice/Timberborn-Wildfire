---
ticket: TWF-127
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-075
   - TWF-114
   - TWF-117
write_scope:
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-127-apply-path-infrastructure-fire-effects.md
---

# TWF-127: Apply Path Infrastructure Fire Effects

## Goal

Make fire damage path-like infrastructure in a bounded way without corrupting Timberborn pathing or trapping the player in unsafe nav state.

## Why

`TWF-117` accepted zero-cost paths as non-burnable safe no-ops and burnable path-adjacent infrastructure as a separate functional-mutation lane. Path blocking and passability are risky enough to keep out of the first infrastructure classification pass.

## Requirements

- Use `TWF-117` classification and `TWF-075` burn damage ownership.
- Keep zero-cost paths non-burnable safe no-ops.
- Apply burn capacity only from burnable construction resources.
- Identify safe Timberborn APIs for blocking, degrading, disabling, or visually marking path infrastructure before mutating passability.
- Preserve recoverability: no permanent unreachable colony state without explicit repair or rebuild path.
- Add bounded telemetry for considered path targets, damaged path targets, blocked path targets, skipped no-safe-api cases, and repair eligibility.
- Add deterministic tests for zero-cost paths, wood path-adjacent infrastructure, mixed resources, duplicate suppression, and safe no-op behavior.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-075` provides burn damage state and target ownership.
- `TWF-114` provides construction-resource fuel classification.
- `TWF-117` accepts the infrastructure classification contract.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start with descriptor and capacity generation before any passability mutation.
- Treat pathing APIs as unsafe until a deterministic wrapper and live proof show recoverable behavior.
- Prefer explicit safe no-op telemetry over mutating path state when the API is unclear.
- Keep this ticket independent from power, water, tunnel, and dynamite behavior.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for one path infrastructure target being safely damaged, blocked, or explicitly skipped because no safe API exists.

## Notes

- Keep path mutation separate from power, water, tunnel, and dynamite behavior.
- Do not mutate Timberborn pathing unless the safe API and recovery path are proven.
