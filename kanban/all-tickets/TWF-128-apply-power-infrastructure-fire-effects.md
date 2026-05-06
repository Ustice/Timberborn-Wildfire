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
- 2026-05-06 reviewer: failed review. The deterministic power sink behavior is accepted in isolation, including metal-only no-op and safe-unavailable mutation handling, but the live lane resolves targets ad hoc instead of consuming the accepted `TWF-075` registered burn-damage ownership/state surface. Return to implementation so power consequences use the shared ownership/classification boundary, then require fresh review before live QA or integration.
- 2026-05-06 worker fix: routed power infrastructure through the bound `TWF-075` ownership provider and shared live target registration. The power sink now requires shared infrastructure ownership for bound live runs and uses shared damage capacity/state before reporting damage or safe-unavailable network mutation. Added deterministic ownership mismatch and shared-state tests.
- 2026-05-06 live QA: passed the owned power-infrastructure proof on the `Smolville` save copy under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-128-129-smolville-live-20260506T135000Z/after-selector-fix/`. `power-infrastructure-probe.txt` queued `target_selector=power-infrastructure` against `burn_damage_target_key=power_infrastructure:-1135005760`, `burn_damage_spec_id=VerticalPowerShaft.IronTeeth(Clone)`, and `burn_damage_remaining_capacity=34`. `Player.log` recorded `wildfire_timberborn_burn_damage_applied tick=20 ... resolved_target_cells=1 ... damage_applied_targets=1 total_damage_applied=1` and `wildfire_timberborn_power_infrastructure_fire_applied tick=20 ... matched_target_cells=1 ... skipped_no_safe_api=1 ... total_damage_applied=1`. The final diff still needs fresh review before integration because the selector and infrastructure classifier changed to make this proof possible.
- 2026-05-06 coordinator review: fresh local review of `4323e57` found no blocking issues for this ticket. Moved to `05-integration`.
- 2026-05-06 coordinator integration: integrated on `main` in commits `4323e57` and `573db63`; moved to `06-done`.
