# Native ash harvest prototype

This is source/managed evidence for issue 83, not a game acceptance claim. The receipt shader contract is separately proven in [ash collection receipts](../reference/ash-collection-receipts.md). No game deployment or live save was performed for this worker rewrite.

## Implemented ownership

One adult gatherer reserves exactly one native FertileAsh output slot before approaching a clean ash field. Actual stopping proximity and the installed remaining route must pass the shared risk checks. After 0.1 game hours at the target, `ReadyToCollect` means empty hands and no admitted command. The shared resource coordinator admits one conditional request for one unit. The GPU receipt is authoritative: zero releases capacity and creates no goods; one puts one Uncountable native good in the carrier and changes the job to return.

Native reservation ownership is read from raw GoodReserver references; its HasReservedCapacity/HasReservedStock getters indicate usable enabled inventories and can be false while a live disabled inventory still holds an allocation. Cancellation releases the raw owned reservation through the native API under the existing resource guard. Ash admission commits its phase only after native reservation listeners return and the destination remains usable; callback-driven disable releases the new reservation and declines without leaving an unowned active phase. Warden reserve/release follows the same guarded rule and refuses existing raw foreign reservations, including station-restocking admission.

The simulator callback, carrier mutation, phase change, and producer rotation share the same synchronous resource/save guard. `AwaitingReceipt` is never serialized. An indeterminate step or native mutation freezes further resource dispatch and rejects saving until a real saved world is loaded. On the first owned executor tick after resource poison, both wardens and harvesters disable their late mover through RejectRoute before returning Running. Repeated poisoned ticks preserve the installed path, perform no private stop or inventory retry, and never reenable movement. Mortality still precedes the poison branch. Notification delivery runs separately. Wardens and ash alternate committed producer kinds; capacity rejection advances only the ordinary batch and spends neither resource.

Return uses the native capacity reservation as the destination authority. It rechecks current access, actual arrival, safe position, exact reservation quantity, and actual carried type/quantity before `UnreserveCapacity → GiveProduced → EmptyHands → Idle`, all under the shared guard. A removed or unusable destination triggers the original gatherer then native closest district inventory lookup, each with safe-route admission. No suitable result leaves the worker holding its native cargo, with a visible panel reason. No implicit CarryRoot handoff occurs.

The active Behavior reference lives on the beaver (`AshHarvestBehavior`), not the gatherer. `Decision.TransferNow` preserves that reference even when native WorkerRoot wraps its workplace decision. `ReleaseWhenFinished` does not return to a deleted workplace. The registered `AshHarvestExecutor` is saved/restored by BehaviorManager exactly once; it is not a second IPersistentEntity. Load only deserializes. Native carrier/reservation validation and owned movement restart wait until the first manager-owned Tick after all entities load.

Ordinary death wins before restored-cargo validation. It releases the owned reservation when resource state is safe and yields native mortality without a refund or production event. Native death/deletion may intentionally lose carried goods. It does not create an indeterminate transaction merely because a worker died. A poisoned resource state remains unsafe to save even when mortality proceeds.

## Installed native evidence

Inspected installed managed IL without creating a game entity:

- `BehaviorManager.Tick` runs the current executor until completion before root arbitration. `ProcessBehavior` assigns `decision.Behavior` when supplied. `SaveRunningExecutor` calls the registered executor's Save; `LoadRunningExecutor` requires a surviving Behavior reference and resolves an attached IExecutor by its native name. `Decision.TransferNow` retains an already assigned behavior, executor, and return flags.
- `GoodReserver.ReserveCapacity` creates a fixed-amount reservation and native Save persists it. `GoodCarrier.Save` persists the actual amount and Countable/Uncountable type.
- `GoodCarrierCapacityReserver.FindInventoryForCarriedGoods` uses Citizen's assigned district, `DistrictInventoryPicker.ClosestInventoryWithCapacity(Navigator.CurrentAccessOrPosition(), goods, out distance)`. The prototype uses this public finder shape and checks safe access before reserving. It does not assume a beaver has an Accessible component.
- `Inventory.GiveProduced` adds storage before unguarded inventory events. `GoodCarrier.EmptyHands` changes its carried-good value before its event. Exceptions cannot be treated as rollback. Native CarryRoot's own deposit would not protect this paired conversion.
- `Workplace.PostLoadEntity` fills `_workplaceBehaviors` via ValidateWorkplaceBehaviors; normal employment changes do not rebuild it. Native gathering falls back to other yields if the prioritized ash field has no entity yielder. The isolated compatibility seam checks the current first entry each frame, fingerprints WorkSystem, and only rewrites the reviewed list when needed; it handles reinitialization of the same list. No reflection search runs per worker decision.

The shared stopping helper and its supported assembly fingerprints are documented in [native owned walker stop](../reference/native-owned-walker-stop.md).

## Automated proof and limitations

Managed tests cover physical-arrival rejection, empty pending-state restore, one/zero receipt transitions, cargo/type/quantity mismatch rejection, and unknown phase rejection. A test executes the installed native Decision.TransferNow twice and confirms the beaver owner survives the enclosing workplace transfer.

An actual tools-disabled Claude Sonnet 5 review of integrated 980152b completed in 385,846 ms (one successful turn, no permission denials; CLI also reports a Haiku helper). Its reservation-listener admission concern was accepted and sharpened: declining TryLaunch does not give the registered ash executor a next Tick, so a prematurely started phase cannot self-heal. Independent native IL review also found disabled-inventory release guards skipping owned allocations; both findings are addressed by the raw ownership/guarded admission change.

Twelve focused managed cases exercise real native Inventory reservation decrement/events while disabled, native GoodReserver reservation callbacks disabling the target before the owner reference is assigned, post-mutation poison, raw foreign-reservation validation, and stale-reference cleanup without nested guards. Full live GoodReserver release and Cargo.TryReserve require Unity GameObject liveness and are not proven by these fixtures. An attempted uninitialized GameObject fixture hit the engine-only static initializer; the final tests use no fabricated live engine object and explicitly separate inspected IL from executed managed evidence.

Mixed-producer scheduling tests prove water/ash alternation, stale preferred work yielding, and no second producer attempt or resource spend on a full batch. Installed native managed deposit fixtures use one-unit storage and real Inventory/GoodCarrier methods in production order. They inject reservation-release, produced-stock, and emptied-carrier subscriber failures. The shared transaction rejects saves and replay after each partial mutation. Six additional native managed executor cases verify poison pauses only active owned movers, leaves idle/unowned movement untouched, preserves the installed destination on repeated ticks, and retains the unsafe-save guard. These fixtures do not construct Unity entities or invoke the full native executor/navigation/save pipeline.

`fertile_ash_collected_goods` now reports the actual successful receipt quantity from the last resource step. Radius-based candidate/reachability counters remain observations, not collection proof. Depleted-cell telemetry is unavailable: the receipt reports removal quantity, and a later simulation change cannot identify the cause of final depletion. No CPU ash ledger is introduced.

Open prototype/release gaps:

- Live travel, save/reload in each phase, actual capacity availability, output counting, UI rendering, and game-native event order remain unverified.
- A living critically hungry worker unable to find storage retains cargo and executor ownership. Safe loose-good recovery is still a release blocker; it must not silently discard cargo.
- The fallback selects the closest native district inventory. If it is unsafe while a farther one is safe, the prototype may hold unnecessarily.
- Risk thresholds, four-hour outbound/return timeout, 16-cell collection radius, and 0.1-hour harvest duration are provisional.
- No dedicated harvest animation is added. Shared whole-grid observation performance is unmeasured.
- Bots are not registered for this prototype. Older saves containing the removed dynamic, unserialized harvest executor need explicit compatibility handling before shipping; use copied saves created before a harvest begins for initial QA.

## Sole-controller live acceptance

Use copied saves, one controller, and the exact integrated commit. Do not overwrite the source saves.

1. For each faction, select FertileAshField at a staffed gatherer with one free native output slot. Place clean ash within the provisional range. Verify actual approach, work delay, one unit in hands only after a successful receipt, return, and exactly one produced unit in storage.
2. Fill or reserve the last output slot before admission. Verify no approach/receipt/mint without exact native capacity. Saturate the ordinary input batch and verify a pending worker retains empty hands until admitted.
3. Put two gatherers at the same one-unit ash cell, then test earlier contamination/depletion. At most one collected good is produced; zero receipts mint nothing.
4. Save/reload during approach, work, ReadyToCollect, loaded return, and HoldingForDeposit. Native carrier/reservation quantity remains exact and no pending receipt is replayed. Observe physical replan only after the restored executor resumes.
5. Pause/dismiss the gatherer and demolish it after collection. The beaver-owned behavior survives a reload and finds safe replacement capacity or visibly holds cargo.
6. Change the installed route by blocking it or heating its next segment. No stopped-walk success counts as physical arrival. Retry safe storage without releasing cargo to CarryRoot.
7. Exercise simultaneous warden and ash work; successful admissions alternate producer kinds. Verify water stock accounting still passes the warden acceptance cases.
8. Trigger native death in a disposable case. Native mortality completes without an ash refund/production event or a deletion veto. Treat carried-good loss as native death semantics.
9. Verify the ash panel appears when an already selected idle beaver starts work, displays its held-cargo reason, and disappears after deposit. Confirm generic resource recovery warning remains actionable; failure injection in managed tests is not live fault proof.
