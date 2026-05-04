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
