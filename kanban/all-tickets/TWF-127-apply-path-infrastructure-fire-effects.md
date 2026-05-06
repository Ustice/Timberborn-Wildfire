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
- 2026-05-05 worker: implemented the first path-infrastructure fire lane. Compact fire deltas now resolve path-like infrastructure targets, suppress duplicate cells by stable target id, calculate construction-resource burn capacity through the existing burn-damage capacity calculator, keep zero-cost paths as safe no-ops, and expose `path_infrastructure_*` telemetry plus QA status fields. The live Timberborn adapter currently reports safe-unavailable mutation instead of blocking passability.
- 2026-05-05 worker learning: production `Wildfire.Timberborn` still compiles as C# 10, so collection expressions cannot be used in shipped adapter code even though tests may accept newer syntax.
- 2026-05-06 reviewer: failed review. The deterministic path sink behavior is accepted in isolation, including zero-cost no-op and safe-unavailable mutation handling, but the live lane resolves targets ad hoc instead of consuming the accepted `TWF-075` registered burn-damage ownership/state surface. Return to implementation so path consequences use the shared ownership/classification boundary, then require fresh review before live QA or integration.
- 2026-05-06 worker fix: routed path infrastructure through the bound `TWF-075` ownership provider and shared live target registration. The path sink now requires shared infrastructure ownership for bound live runs and uses shared damage capacity/state before reporting damage or safe-unavailable mutation. Added deterministic ownership mismatch and shared-state tests.
- 2026-05-06 live QA: passed the owned path-infrastructure proof on the `Fuel` save under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-127-128-129-infra-live-20260506T134500Z/`. `path-infrastructure-probe.txt` queued `target_selector=path-infrastructure` against `burn_damage_target_key=path_infrastructure:-1038993492`, `burn_damage_spec_id=Path(Clone)`, and `burn_damage_remaining_capacity=0`. `Player.log` recorded `wildfire_timberborn_path_infrastructure_fire_applied` with `matched_target_cells=1` and `zero_cost_path_targets=1`, proving the conservative TWF-075-owned no-op path. The final diff still needs fresh review before integration because the live target-registration classifier changed after the earlier review.
- 2026-05-06 coordinator review: fresh local review of `4323e57` found no blocking issues for this ticket. Moved to `05-integration`.
- 2026-05-06 coordinator integration: integrated on `main` in commits `4323e57` and `573db63`; moved to `06-done`.
