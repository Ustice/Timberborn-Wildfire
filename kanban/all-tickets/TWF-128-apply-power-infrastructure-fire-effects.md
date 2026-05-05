---
ticket: TWF-128
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
   - kanban/all-tickets/TWF-128-apply-power-infrastructure-fire-effects.md
---

# TWF-128: Apply Power Infrastructure Fire Effects

## Goal

Make fire damage power infrastructure through burn damage and safe Timberborn power-network APIs.

## Why

`TWF-117` accepts wood shafts, power-adjacent infrastructure, and mixed construction resources as burnable or partially burnable, but power network mutation can have broad colony effects and should be isolated from path and water behavior.

## Requirements

- Use `TWF-117` classification and `TWF-075` burn damage ownership.
- Apply burn capacity only from burnable construction resources; metal must not fuel fire.
- Identify safe Timberborn APIs for disabling, disconnecting, degrading, or visually marking power infrastructure before mutating the network.
- Preserve recoverability through repair, reconstruction, or explicit safe no-op behavior.
- Add bounded telemetry for considered power targets, damaged power targets, disabled or disconnected targets, skipped no-safe-api cases, and repair eligibility.
- Add deterministic tests for wood shafts, mixed-resource power parts, metal-only no-op behavior, duplicate suppression, and safe unavailable APIs.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-075` provides burn damage state and target ownership.
- `TWF-114` provides construction-resource fuel classification.
- `TWF-117` accepts the infrastructure classification contract.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start with descriptor and capacity generation before any network mutation.
- Treat power-network APIs as unsafe until a deterministic wrapper and live proof show recoverable behavior.
- Prefer explicit safe no-op telemetry over mutating power state when the API is unclear.
- Keep this ticket independent from path, water, tunnel, and dynamite behavior.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for one power infrastructure target being safely damaged, disabled, disconnected, or explicitly skipped because no safe API exists.

## Notes

- Keep power mutation separate from path, water, tunnel, and dynamite behavior.
- Do not fake network disruption through status counters alone.
- 2026-05-05 worker: implemented the first power-infrastructure fire lane. Compact fire deltas now resolve power-like infrastructure targets, suppress duplicate cells by stable target id, calculate construction-resource burn capacity through the existing burn-damage capacity calculator, keep metal-only targets as safe no-ops, and expose `power_infrastructure_*` telemetry plus QA status fields. The live Timberborn adapter currently reports safe-unavailable mutation instead of faking network disconnection.
