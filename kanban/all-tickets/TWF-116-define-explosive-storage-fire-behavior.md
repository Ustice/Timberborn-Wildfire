---
ticket: TWF-116
agent_level: High
role: researcher
requires_qa: false
doc_only: false
dependencies:
  - TWF-114
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/DESIGN.md
  - docs/world-consequence-first-pass.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-116-define-explosive-storage-fire-behavior.md
---

# TWF-116: Define Explosive Storage Fire Behavior

## Goal

Define and, if safe, implement the first conservative explosive stored-good behavior for fire exposure.

## Why

Explosives should not burn like logs. They need special handling, but the first version should be bounded and deterministic rather than a broad physics blast or entity deletion system.

## Requirements

- Identify Timberborn resources, buildings, or modded content that should be treated as explosive or volatile.
- Define an armed or unstable threshold based on heat, flame, and exposure duration.
- Destroy the explosive stock through the stored-goods accounting path.
- Emit a bounded heat and fire pulse into nearby simulation cells when the threshold is reached.
- Apply optional nearby structure damage only through the burn damage service.
- Do not mutate terrain, delete arbitrary entities, or displace objects in the first implementation.
- Expose settings or constants that let release settings disable or tune the behavior.
- Expose bounded QA/status telemetry for explosive goods considered, armed stacks, triggered stacks, pulse cells, and skipped unsafe APIs.
- Add deterministic tests for threshold progression, pulse bounds, stock destruction, setting gates, and safe no-op behavior.
- Document the accepted behavior in `docs/DESIGN.md` or `docs/TEST_PLAN.md` if implementation confirms the contract.

## Dependencies

- `TWF-114` defines the resource hazard catalog.

## Role

- Researcher first, worker if the safe API path is clear.
- Follow [researcher.md](../roles/researcher.md) before implementing behavior.

## Verification

- Run `git diff --check`.
- Run `dotnet test` if code changes are made.
- Run `dotnet build Wildfire.slnx` if code changes are made.

## Notes

- Parent design reference: [world-consequence-first-pass.md](../../docs/world-consequence-first-pass.md), "Stored Items And Explosives".
- Keep the first behavior boring and bounded. More dramatic blast mechanics need a later design ticket.
- 2026-05-03 coordinator: moved to `02-ready` for Sprint 7 as a dependency-ready research/design lane. Do not overlap `TWF-075` implementation writes unless the assignment is explicitly narrowed to research-only or disjoint files.
- 2026-05-03 coordinator: required review is a hard gate; if review fails, return through `03-in-progress` for fixes, then back to `04-verify` for fresh review before any integration move.
- 2026-05-03 coordinator: moved to `03-in-progress` for a research-only pass. No files should be edited in the research pass while `TWF-075` owns overlapping implementation and docs write scopes.
- 2026-05-03 research result: safe first behavior is implementable as a deterministic hazardous stored-good service contract, but implementation should wait for `TWF-075` burn-damage ownership and `TWF-115` stored-goods accounting. The pulse can use `IGpuFireSimulator.RegisterChange(FireSimChange)` with bounded heat changes, but real stock destruction is blocked until a safe Timberborn storage/inventory enumeration and removal API is identified and wrapped.
- 2026-05-03 accepted research boundary: treat `Explosives` and `Fireworks` as first stored-good explosive ids, treat `Biofuel`, `Grease`, and `PineResin` as volatile candidates, defer firework variants until runtime evidence confirms stored-good ids, and keep `Dynamite`, `DoubleDynamite`, `TripleDynamite`, and `Detonator` in infrastructure/building classification for `TWF-117`.
- 2026-05-03 accepted first contract: detect hazardous stored goods only after storage ownership and item stacks are resolved, arm after sustained heat or burning adjacency for a short fixed threshold, trigger once per resolved stack or storage target, destroy or reduce stock only through safe inventory APIs, then enqueue a bounded origin-plus-6-neighbor heat pulse through simulator change registration. If inventory APIs are unavailable, do not pulse and increment skipped counters.
- 2026-05-03 required tests and counters for a later worker pass: catalog selection, threshold progression, pulse bounds, stock-destruction gate, setting gate, duplicate suppression, `hazardous_goods_considered`, `hazardous_goods_armed`, `hazardous_goods_triggered`, `hazardous_goods_destroyed_count`, `hazardous_pulse_cells`, `hazardous_pulse_heat_added`, `hazardous_goods_skipped_no_inventory_api`, `hazardous_goods_skipped_unknown_resource`, and `hazardous_goods_skipped_duplicate_target`.
- 2026-05-03 required settings for a later worker pass: enable/disable explosive stored-good behavior, armed threshold ticks, pulse radius initially fixed to `1`, pulse heat amount, and optional volatile-goods inclusion.
- 2026-05-03 coordinator: moved to `09-awaiting-review` for contract acceptance. This is not implementation complete and should not move to `06-done` until the contract is accepted and later implementation/review gates pass.
- 2026-05-03 Jason decision: accepted `Explosives` and `Fireworks` as first-pass explosive stored goods, accepted `Biofuel`, `Grease`, and `PineResin` as volatile candidates when runtime ids are confirmed, and accepted keeping `Dynamite`, `DoubleDynamite`, `TripleDynamite`, and `Detonator` out of stored-goods behavior as infrastructure/building special cases.
- 2026-05-03 Jason decision: if a safe inventory removal or reduction API cannot be found, pause the inventory-mutation portion and split a follow-up ticket rather than faking item destruction. If a safe API exists, the first implementation may destroy or reduce stock, enqueue a bounded origin-plus-6-neighbor heat/fire pulse, and apply nearby structure damage only through the burn damage service.
- 2026-05-03 Jason decision: investigate how unstable cores are handled before final implementation, use sustained heat or burning adjacency as the arming threshold, allow volatile goods to participate without terrain deformation, and keep the proposed settings and telemetry set.
- 2026-05-03 coordinator: contract accepted. The remaining implementation belongs downstream of safe stored-goods accounting work; do not mutate inventory or trigger pulses without a safe storage API.
