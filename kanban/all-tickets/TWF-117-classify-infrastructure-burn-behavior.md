---
ticket: TWF-117
agent_level: Medium
role: researcher
requires_qa: false
doc_only: false
dependencies:
  - TWF-075
  - TWF-114
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/DESIGN.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-117-classify-infrastructure-burn-behavior.md
---

# TWF-117: Classify Infrastructure Burn Behavior

## Goal

Classify how paths, platforms, bridges, stairs, power shafts, fences, levees, and other structure-like infrastructure should lose burnable construction value.

## Why

Buildings are not the only constructed objects. Infrastructure often uses construction materials that can burn, but it may not have workers, inventories, or close/repair states like normal buildings.

## Requirements

- Survey infrastructure blueprints and construction-resource requirements.
- Classify infrastructure into burnable, partially burnable, non-burnable, and special-case groups.
- Use the resource catalog from `TWF-114`; metal should not fuel the fire.
- Decide whether each group should use building rollback, damaged-but-passable state, blocked state, or safe no-op behavior.
- Keep multi-cell and vertical footprint handling consistent with `TWF-075`.
- Add deterministic tests for classification and burn-capacity calculation when code changes are made.
- Document any infrastructure categories that require separate implementation tickets.

## Dependencies

- `TWF-075` provides burn damage state and multi-cell ownership.
- `TWF-114` provides construction-resource fuel classification.

## Role

- Researcher or worker.
- Follow [researcher.md](../roles/researcher.md) for classification before making code changes.

## Verification

- Run `git diff --check`.
- Run `dotnet test` if code changes are made.
- Run `dotnet build Wildfire.slnx` if code changes are made.

## Notes

- Parent design reference: [world-consequence-first-pass.md](../../docs/world-consequence-first-pass.md), "Stored Items And Explosives".
- Normal building rollback belongs to `TWF-077`.
- 2026-05-03 coordinator: moved to `03-in-progress` for a Sprint 7 research-only classification pass. Do not edit files during the research pass while `TWF-084` owns overlapping implementation and docs write scopes.
- 2026-05-03 coordinator: required review is a hard gate; if review fails, return through `03-in-progress` for fixes, then back to `04-verify` for fresh review before integration or acceptance.
- 2026-05-03 research result: current evidence supports classifying most infrastructure by construction-resource burn capacity, but only as a damage-accounting contract for now. `TWF-075` provides burn-damage state and `TWF-114` provides fuel classification, but implementation is blocked on real infrastructure target discovery, descriptor generation from Timberborn blueprints, safe path/power/water-obstacle APIs, and live runtime burn-damage sink wiring.
- 2026-05-03 accepted research boundary: use construction resources for capacity where blueprints have burnable goods, and never let metal, dirt, water, or other inert resources add fuel. Start with burn damage and counters only; no-op functional mutation unless a safe Timberborn API for blocking, disabling, or presentation rollback is proven.
- 2026-05-03 classification table: zero-cost paths are non-burnable safe no-op; wood platforms, stairs, suspension bridges, overhangs, power shafts, and wooden fences are burnable infrastructure from `Log`/`Plank`/`TreatedPlank`/`Gear`; metal platforms, metal fences, and impermeable floors are non-burnable; mixed gates, tubeways, ziplines, and automation-like path parts are partially burnable from burnable goods only; dams, levees, floodgates, valves, and sluices are water-obstacle special cases; dynamite, double/triple dynamite, detonator, and tunnels are explosive or trigger-like special cases deferred to `TWF-116`/`TWF-115`.
- 2026-05-03 required later tests and counters: zero-cost path capacity, wood platform/stair/bridge capacity, metal zero capacity, mixed-resource metal exclusion, multi-cell footprint duplicate suppression, unknown resource fail-closed behavior, explosive special-case classification, `infrastructure_targets_registered`, `infrastructure_burn_capacity_total`, `infrastructure_zero_capacity_targets`, `infrastructure_damage_applied`, `infrastructure_blocked_state_applied`, `infrastructure_skipped_no_safe_api`, `infrastructure_skipped_special_case`, and `infrastructure_unresolved_resource_ids`.
- 2026-05-03 coordinator: moved to `09-awaiting-review` for classification acceptance. This is research complete, not implementation complete.
- 2026-05-03 Jason decision: accepted zero-cost paths as non-burnable safe no-ops; accepted wood platforms, stairs, bridges, overhangs, power shafts, and fences as burnable infrastructure; accepted metal platforms, metal fences, and impermeable floors as non-burnable; accepted mixed-resource infrastructure as partially burnable from burnable resources only.
- 2026-05-03 Jason decision: first-pass infrastructure implementation should start with descriptors, burn-capacity calculation, duplicate suppression, counters, and safe no-op behavior. Functional mutation for pathing, power, and water systems should be separate follow-up tickets.
- 2026-05-03 Jason decision: water-passing infrastructure should be difficult to burn; tunnels should eventually cause destruction, but that belongs in a separate ticket; dynamite and detonators are explosive infrastructure cases.
- 2026-05-03 coordinator: classification accepted. Follow-up tickets were created for path, power, water, and tunnel/dynamite destruction behavior.
