# Inactive fertilizer satchel recovery prototype

This source-only prototype adds three unregistered components/helpers, 342 production lines. It does not activate a template, register an executor, recruit a worker, apply fertilizer, change designations or add a scheduler. No engine, game, desktop or deployment was used for this checkpoint.

## Behavior and ownership

A nonempty, enabled native `FertilizerSatchel.Inventory` containing exactly one unreserved `FertileAsh` unit requests **existing-good deposit only**. An interrupted application target is cancelled; recovery never reconstructs it. Native Inventory persistence remains the amount authority. Recovery has no employer reference, suspended task, quantity cache or saved retry task. Its native `IExecutor` state contains only active-return status, elapsed hours and an obsoletable destination Inventory reference.

`FertilizerRecoveryOrder` verifies the reviewed `BehaviorSystem` and `BeaverBehavior` assembly hashes, private root-list shape and unique ordered adult anchors. `FertilizerRecoveryRoot.PostInitializeEntity` inserts this exact root immediately before Worker, after CriticalNeeder and Stranded, preserving every foreign entry and its relative position. Repeated installation does not mutate the list. Missing, duplicated, reordered or incompatible anchors decline explicitly; there is no fallback append. `RecoveryReady` revalidates ordering without changing it. Future fertilizer admission must require this readiness **before pickup**; this prototype does not make the existing satchel API a job-admission service.

The root is deliberately non-job. Installed `Worker.JobRunning` observes that classification without changing employment. Native `Decision.ReleaseWhenFinished` sets `ShouldReturnToBehavior=false`, so completion does not bypass urgent roots. A failed admission yields `ReleaseNow` and waits 0.1 native game hour before trying again through ordinary arbitration. The delay is transient. Recovery does not poll in Update or hold a worker indefinitely when no storage is available.

The return executor requires current assigned district, no critical need/death, empty native hands, no foreign raw reservation, a current fire observation and loaded native stock. It snapshots current public inventory candidates, prefers the native closest result, checks exact capacity/validator/blocking/access rules and uses the existing shared safe walk. After reserving one unit of capacity it validates current district ownership directly: a completely reserved destination may legitimately leave the active-capacity set. Arrival requires native stopping proximity to the planned and still-current accessible point plus current fire safety. Only then does the existing guarded satchel `TryReturn` perform `TakeExisting`/`GiveExisting`.

Each active Tick yields for urgent needs or invalid ownership/destination, and a return lasts at most one game hour. Cleanup clears only its exact capacity reservation; a foreign stock reservation is preserved. Non-death phase clearing is inside the existing resource transaction. A read-capture rejection cannot discard active return ownership. Indeterminate resources pause owned movement without replay. Mortality precedes the poison hold, never re-enables a dead mover, and invalidates a capture when cleanup cannot enter the mutation guard. Ordinary native death semantics still apply to goods.

## Executed managed evidence

Focused command:

```sh
dotnet test tests/Wildfire.Timberborn.Tests --no-restore --filter 'FullyQualifiedName~FertilizerRecovery|FullyQualifiedName~NativeFertilizer|FullyQualifiedName~TimberbornFireWalk|FullyQualifiedName~NativeWardenPublicInventory'
```

90 focused tests and the full 1,466-test native suite passed, zero failed/skipped. New recovery cases exercise:

- Actual installed `BeaverBehaviorInitializer.InitializeBehaviors(true)` with supplied native components in a native `ComponentCache`; actual `EntityComponent.PostInitialize` dispatches the recovery installation. This validates those managed stages, not full prefab instantiation.
- Actual native `ProcessBehaviors` executes decision probes at the installed positions: CriticalNeeder/Stranded win before recovery, recovery can precede work, and a decline lets work run. These probes isolate native arbitration; they do not impersonate full native need behavior execution.
- Actual `NeedManager.AnyNeedIsInCriticalState` over a supplied native critical Need drives the real recovery executor cancellation path. The existing shared walk-driver fixture records stop/release; poison records pause; mortality records stop without release.
- Native loaded Inventory stock, its dedicated initializer, actual Inventory Save/Load and actual EntitySaver/EntityLoader preserve the existing unit without creating an employer or old application target. Empty, reserved, disabled and malformed stock have distinct request results. Lower-level existing-good movement produces no Consumed or Produced count.
- Capture rejection retains active cancellation ownership; death during capture returns native failure and poisons the attempted capture. Native return-state Load performs no movement or inventory write.
- Actual native Worker classification is non-job both with supplied employment and after that employment reference is removed. Native clock-contract advancement releases the transient retry delay.

## Native destination contract follow-up

The names of native helpers are not the admission proof. Installed IL establishes the chain:

- `DistrictInventoryRegistry.Add` IL0000–0017 registers only public input or output inventories in its public registry. `InventoryRegistry.UpdateRegistries` IL0000–002f adds a capacity candidate only when `PublicInput` and `HasUnreservedCapacity(string)` hold. The string overload first calls `Takes(good)`, which reads the native allowed input-good set, before checking capacity. Thus private satchels and output-only/other-good stores cannot become deposit candidates through this set.
- `DistrictInventoryPicker.InventoryIsTaking` IL0002/000f/001c additionally checks requested unreserved quantity, `IInventoryValidator.ValidInventory`, then `BlockableObject.IsUnblocked`. The prototype applies the same checks; it does not treat the validator as an allowed-good filter. Existing actual-native public Warden inventory fixtures prove public-input inclusion, private exclusion, full-capacity removal, emptying-validator rejection and blocking rejection.
- `DistrictInventoryAssigner.Awake` gets the owner's `DistrictBuilding`; `AddRegisteredInventories` IL0001–001b takes its exact `District` and that district's registry. This establishes the prototype's post-reservation `DistrictBuilding.District` check without requiring membership in a capacity set from which the reservation can legitimately remove the inventory.
- The picker obtains an enabled `Accessible`, and its Vector3 `FindRoadPath` and `FindRoadToTerrainPath` methods both start with `UnblockedSingleAccess`. The straight-line fallback additionally calls `IsReachableUnlimitedRange`. `ValidAccessible` alone checks enabled state and accessibility validators; it does **not** check native `IBlockedAccessible`. For buildings, `BuildingBlockedAccessible.IsBlocked` checks native navmesh connection between entrance and doorstep.

The prototype now reads public `UnblockedSingleAccess` after `ValidAccessible` for both route planning and arrival revalidation. No private blocked-member reflection, manual coordinate or selector framework was added. A native Accessible with supplied `IBlockedAccessible` contract returns validity true while blocked: the old helper exposed `(1.5,2,3.5)` and the regression failed; the new helper returns no point, returns the exact point when unblocked, and rejects it again if blocked before arrival. Disabled access also declines. Multiple points on one Accessible retain native `Single()` rejection and are explicitly unsupported in this destination path; the prototype does not choose an arbitrary one. The test supplies the obstruction contract and runs actual native Accessible getters, not a live doorstep/navmesh.

Root-order validation now uses direct loops, with no LINQ iterators or per-anchor result arrays. Unique ordered anchors, foreign-entry preservation, exact recovery identity and idempotence are unchanged; no cache or timing benchmark was introduced.

Evidence logs are in `/tmp/wildfire-fertilizer-recovery-review/`: `followup-focused2.log`, `followup-full-native.log`, `blocked-access-red2.log` and `native-destination-evidence.il.txt`; the preceding proposal's installed IL is retained there. The previous tools-disabled Claude attempt refused/returned API error and supplied no substantive review. No Claude findings are claimed for this implementation.

## Explicit remaining gates

**Managed components without Unity liveness do not prove a positive physical return.** Native `GoodReserver.UnreserveCapacity` tests the inventory's Unity truthiness and live EntityComponent before calling Inventory.UnreserveCapacity. The supplied managed fixture executes its reference-clear branch; it does not prove real capacity release. The fixture tests are named accordingly. The actual satchel's positive `Live`/TryReturn path, native reserve-release callbacks, destination navigation, physical arrival and complete template/save registration still require a controlled engine fixture.

The executor stores an obsoletable native Inventory reference using the existing serializer. Managed Save/Load covers the active state and missing-destination path; real referenced destination restoration, reservation PostLoad ordering and first owned replan require the full component-chain fixture. Load itself does not stop or launch a walker. A missing/invalid destination yields on first owned Tick; a valid restored return releases its old owned capacity and replans before walking.

Before activation, a single actor fixture must prove the exact template decorators and `BehaviorManager.RegisterExecutor`/reference registration, active and idle reload, unemployment, district change, no-storage backoff, urgent-needs interruption, safe farther storage, exact capacity conservation, native reservation callback failure and ordinary mortality. Required recovery types must remain registered for existing active saves if new admissions are later disabled. Native return operation callbacks must use the same world coordinator. No saved application intent or recurrence policy should be added to make this recovery test pass.

This prototype does not fix or adopt the current instant legacy fertilization service. A future employer-owned application trip still needs the separately reviewed sole-runtime conditional receipt/delivery integration. One-shot versus maintenance fertilization remains independent of recovery.
