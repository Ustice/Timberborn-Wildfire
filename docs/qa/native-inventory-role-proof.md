# Native Manufactory and recovered inventory proof

This is a test-only checkpoint against the installed macOS Timberborn assemblies. It does not activate new capture roles, consequences, budgets, persistence, recovered-pile spawning, or cargo policy.

## Native roles and consumption

| Native role | Physical stock | Required distinction |
| --- | --- | --- |
| `Manufactory.Inventory` | Dedicated named inventory initialized by `ManufactoryInventoryInitializer` from recipe goods | Inputs can be physically present and consumable through `TakeConsumed` while absent from `UnreservedTakeableStock`. Use `UnreservedStock`/`UnreservedAmountInStock` when deriving fire-consumable physical stock; preserve all reservations. |
| `RecoveredGoodStack.Inventory` | Dedicated `IGoodStackInventory`, initialized through the good-stack inventory decorator | `RecoveredGoodStack` is not `GoodStack`. Its `InventoryChanged` handler deletes the exact entity when empty. |
| Dormant `GoodStack.Inventory` | Named native role exists even when disabled and empty | Current physical-material capture deliberately omits this empty part. That omission cannot establish the complete static inventory-role witness. |

`Manufactory.InitializeInventory` and `SimpleOutputInventory.InitializeInventory` each prevent reassignment of their own property. They do not prevent both roles referencing the same `Inventory`. Formation must preflight aliases before material/accounting work, not count each role as another stock owner.

Native Manufactory fuel/ingredients already moved out of inventory are represented by its own production state. `ProcessedGoods` is not a second inventory to sum into captured physical stock. Current recipe, filters, enabled state, quantities, reservations and production progress are mutable native state, not suitable static compatibility fingerprints.

## Native cutpoints

Installed `Inventory.TakeConsumed` calls `TakeInternal`, which checks stock/reservations, subtracts the requested amount, then invokes `InventoryChanged` before `InventoryStockChanged(Consumed)`. Callbacks propagate exceptions.

`DistrictGoodsBalance` subscribes to `InventoryStockChanged`. Its consumption counter therefore is not a commit receipt: an earlier callback can throw, or unsubscribe that counter before it receives the event. A later subscriber can throw after the counter has already updated. None of these observations authorizes a guessed refund, replay, or fabricated completed removal.

Recovered final-unit depletion follows `InventoryChanged → RecoveredGoodStack.OnInventoryChanged → Delete → EntityService.Delete → EntityComponent.Delete`. The entity enters `Deleted` before its native deletion event. The service removes its registry entry only after `EntityComponent.Delete` returns, then calls Unity destruction. A failed deletion event can leave zero stock and a `Deleted` exact Guid still registered. That state does not satisfy exact-absence retirement.

The future raw adapter should re-resolve the exact Guid and named owning inventory immediately before mutation, re-read actual unreserved quantity, and return the exact requested amount only if `TakeConsumed` returns. It runs inside the existing outer resource guard; no nested guard, CPU stock ledger, counter-derived receipt or cell/replacement fallback. Stop further work when native deletion completes; actual removal/retirement and GPU detachment remain separate required lifecycle work.

## Executed proof

Eight fixtures execute actual native role initializers, inventory methods, native reservation checks, `DistrictGoodsBalance` subscription/counters and the existing mod resource guard over supplied managed state:

- Manufactory input stock 2/reserved 1: physical count 2, takeable-output enumeration 0, all-unreserved enumeration 1, completed consumption 1, counter 1, original reservation and its inventory retained. A second request consumes zero.
- Failure at `InventoryChanged` leaves quantity removed and counter zero; failure at the later `InventoryStockChanged` subscriber leaves the native counter one. Both return no receipt and block save/retry through the existing guard.
- Actual counter unsubscription during a supplied `InventoryChanged` callback permits a completed consumption receipt of one with counter zero. This is an event-order proof, not a complete recovered-pile deletion fixture.
- Two real native roles accept one aliased inventory.
- Recovered nonterminal removal preserves reserved survivor stock and reaches the native consumption counter.
- Recovered final-unit removal runs the real native deletion chain until an injected native deletion-event failure: stock zero, no receipt, counter zero, owner still registered as `Deleted`, foreign Guid still registered, save/retry blocked. It deliberately stops before registry removal and `UnityEngine.Object.Destroy`.
- Disabled empty harvest `GoodStack` retains its native named role while current physical-part capture returns null.

Validation: eight focused cases and the full native suite (1,121 passed; zero failed/skipped) on the isolated checkpoint. These are native managed fixtures, not native prefab construction, complete entity loading, a full colony, live successful recovered deletion, merging, disintegration, or GPU proof. No Unity/game process was launched.

## Proposed next contract, not implemented

Capture a complete static list of declared inventory roles and stable native `Inventory.ComponentName` values separately from positive physical material parts. Reject a missing required inventory and ambiguous names; reject reference aliases before summing any stock. An empty/dormant role remains in this witness. Do not persist mutable recipe IDs, filters, enabled flags or stock as static compatibility requirements.

All roles on one exact owner must share the eventual approved body/goods accounting authority; adding roles must not duplicate owner damage or credits. Manufactory inputs need the broader unreserved-physical consumption path. Recovered piles need an explicit standalone material/consequence family and exact Guid removal semantics, not fabricated building construction facts. These production changes depend on the pending fuel-budget design; this checkpoint does not choose it.
