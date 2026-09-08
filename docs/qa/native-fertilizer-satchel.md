# Native fertilizer satchel prerequisite

This is an inactive native inventory seam. No template/configurator binding, worker admission, scheduling, designation tool, movement, or application policy is enabled. The existing instant fertilizer designation path remains a separate release gap.

## Authority and transfer contract

`FertilizerSatchel` owns one native `Inventory`, named `Wildfire.FertilizerSatchel`, initialized with capacity one and Givable FertileAsh. Native stock and native inventory persistence are the only cargo authority. It adds no GoodCarrier cargo, quantity ledger, saved worker phase, or consumption-event imitation.

Pickup requires the worker to have proved arrival and own the exact source reservation: raw inventory reference, FertileAsh amount one, fixed amount, non-consuming, and no capacity reservation. Return requires the analogous exact fixed destination-capacity reservation and no stock reservation. Raw reservations remain checked even when native convenience getters hide disabled inventories. Foreign reservations are never released.

The same runtime `NativeResourceCoordinator.TransferInventory` encloses native unreserve, actual `TakeExisting`/`GiveExisting`, and worker phase callback. After unreserve callbacks, unexpected replacement reservations reject the operation. A false result may follow release of the owned reservation without moving stock; the future worker must reacquire/replan, not assume the reservation remains. There is no retry or compensating refund after a native write throws.

Application consumption requires the coordinator's positive application privilege, available only inside the attached-world conditional ash receipt callback for Applied/Added=1. Generic transfers, reads and rejected receipts do not grant it. Null phase callbacks and empty, foreign, overfilled or reserved cargo reject before `TakeConsumed`. Actual native consumption and the phase callback stay within that receipt's existing transaction.

## District and lifecycle boundary

The inventory registers with the native district inventory registry; a private `IGoodProcessor` registers with the native resource counter so its Givable input remains accountable. Registration, district changes and teardown use the same guard. Initialization and post-load registration do not manufacture cargo.

Death/deletion cleanup permits native teardown to continue. Already-indeterminate cleanup does not retry writes. A rejected or partially failed cleanup explicitly invalidates the existing transaction, including caught reentrant teardown during capture. Capture and transfer cannot return success after that invalidation; a returned simulator step or null admission becomes typed Indeterminate. Existing thrown exceptions remain the cause, latches/positive privilege close in finally, and an ordinary read failure alone does not poison the world. No death refund or consumption is manufactured.

## Executed managed/native proof

The fixtures load installed native assemblies and execute actual inventory initialization, stock/reservation operations, district stock events/counters, named `Inventory.Save` and `Inventory.Load` using native entity persistence and serializers. The restored inventory runs the actual dedicated initializer before Load. Both one unit and empty stock round-trip without replay or native stock callbacks.

Coverage includes normal existing-goods pickup/return, exact raw reservations, source/destination callback failures, destination disable after source removal, callback/phase failure after consumption, native Consumed event ordering, malformed cargo, positive receipt privilege, caught death during capture/application, district counter removal, and actual cached actor Awake/death subscription/Delete unsubscription.

Final source validation: 34 focused satchel/lifecycle cases and **1,348 full native tests passed**, zero skips. The fixed-capacity regression first failed against the previous predicate, then passed with the strict check. Actual native `GoodReserver.ReserveCapacity` constructs `GoodReservation(inventory, amount, true, false)`, so the correction preserves native fixed-capacity admission.

These are source/native managed proofs, not an engine worker proof. The fixture supplies native caches and drives the subscribed death handler; it does not execute real actor mortality. The public transfer wrapper's Unity liveness, full district lifecycle, rendered/equipped satchel, live path arrival, actual GPU receipt and a complete native worker save/resume journey remain unproved by these fixtures. The attached simulator is a receipt-contract test fake; shader proof belongs to the separate conditional application work.

## Independent Claude review

Actual authenticated, tools-disabled Claude reviewed immutable `bb0961d` source. The process completed with exit 0, `is_error=false`, API duration 318,742 ms and wall duration 319.707 seconds. Prompt, source manifest, raw response, terminal outcome and disposition are retained at `/tmp/wildfire-satchel-claude-review`.

Accepted: enforce fixed capacity reservations; independently verified with actual native IL and a red/green regression. Claude's speculative explanation about native variable/shared capacity reservations was not established. Other notes described the intentional released-reservation false result and partial district-registration fail-stop behavior; neither established a conservation or unsafe-save defect. This review does not clear the outstanding engine boundaries.
