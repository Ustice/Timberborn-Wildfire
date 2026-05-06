---
ticket: TWF-115
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-075
  - TWF-114
  - TWF-116
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-115-apply-stored-goods-burn-consequences.md
---

# TWF-115: Apply Stored Goods Burn Consequences

## Goal

Destroy burnable stored inventory when fire consumes stockpile, warehouse, or storage-tank fuel without duplicating goods or bypassing Timberborn inventory accounting.

## Why

Storage contents are not construction materials. A wooden warehouse can burn structurally while its stored logs, planks, paper, food, or explosives burn as inventory contents. Players need that loss to be real, deterministic, and legible.

## Requirements

- Consume compact fire deltas through the Timberborn consequence path.
- Resolve storage entities and exposed storage cells through the burn damage foundation.
- Use the resource fuel catalog from `TWF-114`.
- Destroy or reduce stored item counts through safe Timberborn inventory APIs only.
- Keep structural burn damage separate from inventory loss.
- Never create duplicate goods, negative inventory, or hidden replacement goods.
- Treat non-burnable goods as not contributing fuel, while still allowing the storage building to be damaged by other burnable value.
- Route hazardous goods to the behavior defined by `TWF-116`.
- Expose bounded QA/status telemetry for considered storage, burnable contents, destroyed item counts by resource, skipped APIs, and hazardous goods encountered.
- Add deterministic tests for resource selection, partial-stack burn, non-burnable goods, multi-cell duplicate suppression, and safe no-op behavior.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-075` provides burn damage state and cell-to-entity ownership.
- `TWF-114` provides resource fuel and flammability classification.
- `TWF-116` defines the hazardous-good behavior for explosives.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start by identifying or wrapping safe Timberborn storage enumeration and item removal or reduction APIs.
- If no safe inventory API exists, pause the inventory-mutation portion and split a follow-up ticket instead of faking item destruction.
- Use `TWF-116` explosive and volatile-goods decisions when routing hazardous stored goods.
- Keep structural burn damage separate from stored inventory loss.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for at least one burnable stored good being reduced or a precise safe unavailable state.

## Notes

- Parent design reference: [world-consequence-first-pass.md](../../docs/world-consequence-first-pass.md), "Stored Items And Explosives".
- Explosive pulse behavior belongs to `TWF-116`.
- Structure material rollback belongs to `TWF-077`.
- 2026-05-03 coordinator: moved to `07-blocked` during Sprint 7 closeout. This ticket was blocked on `TWF-116` contract acceptance and safe storage/inventory accounting surfaces.
- 2026-05-03 coordinator: Jason accepted the `TWF-116` hazardous stored-good contract. This ticket can now attempt safe Timberborn inventory enumeration and removal/reduction APIs, but must pause and split the inventory-mutation path if no safe API is found.
- 2026-05-03 coordinator: do not claim stored-goods destruction without a safe Timberborn accounting path. Required review remains a hard gate; if review later fails, return through `03-in-progress` for fixes, then back to `04-verify` for fresh review before integration.
- 2026-05-05 worker: implemented a deterministic stored-goods burn sink and Timberborn adapter edge around `Stockpile.Inventory`, `Inventory.Stock`, and `Inventory.Take(GoodAmount)`. The sink consumes fuel-loss compact deltas, resolves one storage target per tick, suppresses duplicate target cells, classifies stacks through `TimberbornResourceFuelCatalog`, partially destroys burnable non-hazardous goods by burn budget, counts hazardous volatile/explosive goods for `TWF-116`, and reports skipped unknown, inert, or missing-inventory cases instead of faking mutation.
- 2026-05-05 worker: added status/log telemetry fields `stored_good_burn_*` and command-result fields `last_delta_consumer_stored_good_burn_*`. Live QA is still required for real inventory reduction or precise safe-unavailable evidence once Timberborn loading is healthy again.
- 2026-05-06 reviewer: failed review. The current live wiring resolves storage targets independently instead of consuming the accepted `TWF-075` burn-damage ownership/state surface. The live inventory adapter also only resolves `Stockpile` inventory, so warehouses and storage tanks can fail to produce either matched-storage destruction evidence or precise safe-unavailable telemetry. Deterministic stockpile behavior remains useful, but this ticket must return to implementation and pass a fresh review before live QA or integration.
- 2026-05-06 worker fix: routed stored-goods burn through the bound `TWF-075` ownership provider. Owned storage cells now require shared storage ownership before mutation, while warehouse/pile/tank-style storage that is owned by the burn-damage state but cannot be resolved through live `Stockpile.Inventory` reports matched-storage safe-unavailable telemetry instead of disappearing. Added deterministic ownership mismatch and owned-storage/no-inventory tests.
- 2026-05-06 live QA: passed the owned storage proof on the `Fuel` save under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-077-115-127-128-129-live-20260506T134000Z/`. `storage-owned-burn-damage-probe.txt` queued `target_selector=storage` against `burn_damage_target_key=stockpile:-85609752`, `burn_damage_spec_id=SmallTank.Folktails(Clone)`, and `burn_damage_remaining_capacity=22`. `Player.log` recorded shared burn-damage application and `wildfire_timberborn_stored_goods_burn_applied` with matched storage cells. The final diff still needs fresh review before integration because the live target-registration classifier changed after the earlier review.
- 2026-05-06 coordinator review: fresh local review of `4323e57` found no blocking issues for this ticket. Moved to `05-integration`.
- 2026-05-06 coordinator integration: integrated on `main` in commits `4323e57` and `573db63`; moved to `06-done`.
