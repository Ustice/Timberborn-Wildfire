---
ticket: TWF-129
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
   - kanban/all-tickets/TWF-129-apply-water-infrastructure-fire-effects.md
---

# TWF-129: Apply Water Infrastructure Fire Effects

## Goal

Define and apply conservative fire effects for dams, levees, floodgates, valves, sluices, and other water-passing infrastructure.

## Why

Jason accepted that water-passing infrastructure should likely be difficult to burn. These objects interact with water simulation and map safety, so they need their own conservative ticket instead of being grouped with ordinary burnable infrastructure.

## Requirements

- Use `TWF-117` classification and `TWF-075` burn damage ownership.
- Treat water-passing infrastructure as difficult to burn unless material evidence says otherwise.
- Apply burn capacity only from burnable construction resources; water, dirt, metal, and other inert resources must not fuel fire.
- Identify safe Timberborn APIs before changing water passage, gate state, blockage, or presentation.
- Prefer safe no-op plus explicit telemetry over risky water-simulation mutation.
- Add bounded telemetry for considered water infrastructure, burnable material value, damaged targets, water-state mutation attempts, skipped no-safe-api cases, and repair eligibility.
- Add deterministic tests for inert-resource no-op behavior, partially burnable mixed resources, duplicate suppression, difficult-to-burn thresholds, and safe unavailable APIs.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-075` provides burn damage state and target ownership.
- `TWF-114` provides construction-resource fuel classification.
- `TWF-117` accepts the infrastructure classification contract.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start with descriptor and capacity generation before any water-passage mutation.
- Treat water-simulation and water-control APIs as unsafe until a deterministic wrapper and live proof show recoverable behavior.
- Make water-passing infrastructure difficult to burn by default, then tune from evidence rather than intuition.
- Prefer explicit safe no-op telemetry over mutating water state when the API is unclear.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for one water infrastructure target being safely handled, damaged, or explicitly skipped because no safe API exists.

## Notes

- No terrain deformation belongs in this ticket.
- Keep water infrastructure separate from path, power, tunnel, and dynamite behavior.
- 2026-05-05 worker: implemented the first conservative water-infrastructure fire lane. Compact fire deltas now resolve water-passing infrastructure targets, suppress duplicate cells by stable target id, calculate burnable material value through the shared burn-damage capacity calculator, treat inert water/dirt/metal-only construction resources as safe no-ops, and apply a small difficult-to-burn resistance before damage. The live Timberborn adapter reports safe-unavailable water-state mutation instead of touching gates, passage, or water simulation.
- 2026-05-06 reviewer: failed review. The deterministic water-infrastructure sink behavior is accepted in isolation, including inert no-op and difficult-to-burn resistance, but the live lane resolves targets ad hoc instead of consuming the accepted `TWF-075` registered burn-damage ownership/state surface. Return to implementation so water infrastructure consequences use the shared ownership/classification boundary, then require fresh review before live QA or integration.
- 2026-05-06 worker fix: routed water infrastructure through the bound `TWF-075` ownership provider and shared live target registration. The water sink now requires shared infrastructure ownership for bound live runs and uses shared damage capacity/state before reporting damage or safe-unavailable water-state mutation. Added deterministic ownership mismatch and shared-state tests.
- 2026-05-06 live QA: passed the owned water-infrastructure proof on the `Smolville` save copy under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-128-129-smolville-live-20260506T135000Z/after-selector-fix/`. `water-infrastructure-probe.txt` queued `target_selector=water-infrastructure` against `burn_damage_target_key=water_infrastructure:-1004154696`, `burn_damage_spec_id=Levee.IronTeeth(Clone)`, and `burn_damage_remaining_capacity=0`. `Player.log` recorded `wildfire_timberborn_water_infrastructure_fire_applied tick=24 ... matched_target_cells=1 ... inert_material_noop_targets=1 ... total_damage_applied=0`, proving the conservative TWF-075-owned inert no-op path without mutating water simulation. The final diff still needs fresh review before integration because the selector and infrastructure classifier changed to make this proof possible.
