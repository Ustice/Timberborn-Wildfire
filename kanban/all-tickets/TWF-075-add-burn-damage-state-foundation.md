---
ticket: TWF-075
agent_level: High
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-035
  - TWF-046
  - TWF-114
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/ARCHITECTURE.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-075-add-burn-damage-state-foundation.md
---

# TWF-075: Add Burn Damage State Foundation

## Goal

Add the Timberborn-side foundation for persisted burn damage state on spec-backed world entities.

## Why

`docs/DESIGN.md` section 20 says burn damage is instance state, not static spec data. Buildings, harvestables, cuttables, plants, crops, trees, stockpiles, and resources need a common consequence foundation before specific plant and building outcomes can be implemented cleanly.

## Requirements

- Keep `Wildfire.Core` host-agnostic.
- Add Timberborn-side burn descriptors for static material behavior without storing damage on specs.
- Add per-instance burn damage state keyed by entity and simulation cell ownership.
- Track damage capacity from resource yield or construction-resource investment.
- Support resource-specific fuel and flammability scores for later tuning.
- Resolve changed simulation cells back to a single owning burn-damage target.
- Roll up multi-cell and vertical footprints without applying duplicate damage.
- Preserve deterministic tests for descriptor lookup, cell-to-entity mapping, duplicate suppression, and damage-capacity calculation.
- Document the durable service boundary in `docs/ARCHITECTURE.md` or `docs/TEST_PLAN.md` only where implementation changes the contract.

## Dependencies

- `TWF-035` provides material and vertical footprint mapping.
- `TWF-046` proves the coherent live loop that downstream consequences should consume.
- `TWF-114` defines the resource fuel and flammability catalog consumed by damage capacity calculations.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start from the existing Timberborn adapter mapping and consequence path, especially cell mapping, delta consumers, and runtime status surfaces.
- Treat burn damage as Timberborn-side per-instance state keyed by stable entity identity plus owned simulation cells.
- Use the `TWF-114` catalog for material fuel scoring; if the catalog is incomplete, fail closed with non-burnable or safe no-op behavior and record the missing resource name.
- Emit telemetry for considered cells, resolved targets, duplicate cells suppressed, unknown specs, missing resources, burn capacity, damage applied, and persistence writes.
- Add tests before live QA for descriptor lookup, multi-cell rollup, vertical footprint ownership, duplicate suppression, unknown resource behavior, and save/load serialization.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.

## Notes

- This ticket builds the state and mapping foundation. Plant death, building rollback, ash fields, and feedback should remain in their own tickets.
- Relevant design references: `docs/DESIGN.md` section 20, "Burn Damage State" and `docs/ARCHITECTURE.md` "Burn Damage Service".
- 2026-05-03 coordinator: moved to `02-ready` for Sprint 7. This is the first implementation lane for world-consequence core and should stay limited to shared identity, capacity, mapping, state, duplicate suppression, and telemetry foundations.
- 2026-05-03 coordinator: base implementation on reviewed `TWF-114` resource fuel catalog work. Required review is a hard gate; if review fails, return through `03-in-progress` for fixes, then back to `04-verify` for fresh review before any integration move.
- 2026-05-03 coordinator: moved to `03-in-progress` in `~/repos/wildfire-TWF-075` on branch `codex/TWF-075-burn-damage-foundation`, based on reviewed `TWF-114` commit `f679a6ba63650e218987884424aa5228f10e763a`.
- 2026-05-03 worker result: implementation committed in `~/repos/wildfire-TWF-075` at `6998d5a4e0d90677d31164b27c5dccda6d11726f`. The worker added a Timberborn-local burn damage foundation with static descriptors, resource-catalog-backed capacity, stable target/cell ownership, single-owner cell resolution, multi-cell/vertical duplicate suppression, bounded per-instance state, snapshot restore, unknown-resource fail-closed behavior, and burn-damage telemetry.
- 2026-05-03 worker scope note: the delta consumer now has an optional burn-damage sink that stays unbound by default. Crop death/yield mutation, tree death/yield mutation, structure rollback, stored-good destruction, explosives, ash/fertility, beaver effects, UI aggregation, and live Timberborn entity/save API guessing remain out of scope.
- 2026-05-03 worker verification: `git diff --check` passed, targeted `TimberbornBurnDamageStateTests` passed with `8` tests, full `dotnet test` passed with `157` tests, and `dotnet build Wildfire.slnx` passed with `0` warnings and `0` errors.
- 2026-05-03 coordinator: moved to `04-verify` for review. If review fails, return to `03-in-progress` for fixes, then back through `04-verify` for fresh review before integration.
- 2026-05-03 review: failed review on commit `6998d5a4e0d90677d31164b27c5dccda6d11726f`. The diff stayed in scope and checks passed, but resource-specific fuel/flammability scores were calculated and then discarded from exposed target state/snapshot, and burn-capacity telemetry was missing from registration summaries/log evidence.
- 2026-05-03 review verification: `git diff --check` passed, targeted `dotnet test --filter FullyQualifiedName~TimberbornBurnDamageStateTests` passed with `8/8`, full `dotnet test` passed with `157/157`, and `dotnet build Wildfire.slnx` passed with `0` warnings and `0` errors.
- 2026-05-03 coordinator: moved back to `03-in-progress` for fixes. Required fixes: preserve resource fuel/flammability and accounted resource ids in exposed state or snapshot for downstream tickets, add deterministic burn-capacity telemetry such as total/max/zero-capacity target counts, and cover both gaps in tests. After fixes land, move back to `04-verify` for fresh review before integration.
- 2026-05-03 worker fix: committed `894f6ae63e632c2d79a7ba5c2c699190a376906a`. `TimberbornBurnDamageTargetState` and `TimberbornBurnDamageStateSnapshot` now expose `FuelValue`, `Flammability`, and `AccountedResourceIds`, and registration summary/log telemetry now includes `burn_capacity_total`, `burn_capacity_max`, and `burn_capacity_zero_targets`.
- 2026-05-03 worker fix verification: `git diff --check` passed, targeted `dotnet test --filter FullyQualifiedName~TimberbornBurnDamageStateTests` passed with `10/10`, full `dotnet test` passed with `159/159`, and `dotnet build Wildfire.slnx` passed with `0` warnings and `0` errors.
- 2026-05-03 coordinator: moved back to `04-verify` for fresh review after failed-review fixes. Do not move to integration unless this new review passes.
- 2026-05-03 fresh review: failed review on commit `894f6ae63e632c2d79a7ba5c2c699190a376906a` for a narrow documentation blocker. The previous code/test findings were fixed, checks passed, and scope remained foundation-only, but `docs/ARCHITECTURE.md` still described snapshots without the newly exposed `FuelValue`, `Flammability`, and `AccountedResourceIds` fields.
- 2026-05-03 coordinator: moved back to `03-in-progress` for a narrow docs fix. After the architecture doc correction lands, move back through `04-verify` for another fresh review before integration.
- 2026-05-03 docs fix: committed `00eac0e7ac12b1f7084be3e5bfaba2dbff1033e3`. `docs/ARCHITECTURE.md` now explicitly says burn damage state snapshots include `FuelValue`, `Flammability`, and `AccountedResourceIds` as resource-accounting contract fields.
- 2026-05-03 docs fix verification: `git diff --check` passed. No `dotnet` tests or build were run because the fix was docs-only.
- 2026-05-03 coordinator: moved back to `04-verify` for fresh review after the docs-only failed-review fix. Do not move to integration unless this new review passes.
- 2026-05-03 fresh review: passed review on commit `00eac0e7ac12b1f7084be3e5bfaba2dbff1033e3` with no findings. The prior code/test findings and docs blocker are resolved; `Wildfire.Core` remains host-agnostic, Timberborn stays adapter-only, and the scope remains foundation-only.
- 2026-05-03 review verification: reviewer ran `git diff --check` for both the docs-fix range and the full `TWF-075` range, and both passed. `dotnet test`/build were not rerun for the final docs-only fix because prior code verification remained applicable.
- 2026-05-03 coordinator: moved to `05-integration` after fresh review passed.
- 2026-05-03 integration: integrated reviewed `TWF-075` delta through `00eac0e7ac12b1f7084be3e5bfaba2dbff1033e3` into the main checkout and announced with `say`.
- 2026-05-03 integration verification: `git diff --check` passed, targeted `dotnet test --filter FullyQualifiedName~TimberbornBurnDamageStateTests` passed with `10/10`, full `dotnet test` passed with `174/174`, and `dotnet build Wildfire.slnx` passed with `0` warnings and `0` errors.
- 2026-05-03 coordinator: moved to `06-done`. Downstream consequence tickets may consume the burn-damage foundation, but live-required tickets still need their own QA evidence.
