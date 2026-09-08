# Credited native water to one private bucket

`WardenEquipment.TryFillFromNaturalSource(source, validatePickup, commitLoaded)` converts one actual native Water quantum into the existing private Water inventory. This is the clean-source-only connected prototype: actual native `WaterInput.IsUnderwater` and finite `ContaminationPercentage == 0` are required. It does not decide the pending shipping policy for mixed water or enable a public natural-water responder by itself.

The operation requires the original live equipment inventory and owner, exact private Water-only capacity-one topology, no stock/capacity reservation, the source's current armed exclusive identity, the same resource coordinator and a settled owning native thread. The transient intake captures the exact native WaterInput, WaterService and ThreadSafeWaterMap; it rejects substituted services/maps and requires demand and credit to share the same native WaterChangeService. It does not accept an injected eligibility predicate. Caller validation owns the exact current executor, physical shoreline arrival, route/district and finite waiting policy.

The native `WaterGoodToWaterAmountConverter.GetWaterAmount` supplies the quantum (currently 0.2); the corresponding assembly joins the existing exact compatibility fingerprints. Inside one existing resource guard:

1. Validate current caller/native ownership. If clean credit is insufficient, queue at most one full native quantum demand, verify no buffer or cargo changed, and return `AwaitingCredit`. No tick is driven and no pending demand becomes cargo.
2. With sufficient credit, call actual `WaterInput.RemoveCleanWater(quantum)` and `Inventory.GiveProduced(Water1)`. Require the exact clean subtraction, unchanged dirty buffer and exactly one unreserved Water in the private inventory.
3. Revalidate caller ownership before `commitLoaded`. The caller may then change `AwaitingCredit` to its loaded phase. Final checks revalidate native identities, buffers and inventory; the phase-sensitive caller validator is deliberately not called after the phase change.

Native events may fail after subtraction or stock mutation and before production statistics. Such exceptions, callback drift or commit failure poison the same coordinator and preserve the actual partial state. There is no success result, guessed receipt, statistics repair, refund or retry. Dirty historical credit is retained unchanged even though the current map must be clean. Two bucket operations serialize against the one native buffer; there is no additional quantity ledger.

## Native scheduling boundary

Installed `BehaviorManager` is an entity `TickableComponent`/`ILateTickable`, not a late singleton. `TickableBucketService` first runs the singleton bucket, whose `TickableSingletonService.TickAll` finishes the previous parallel tick, runs singleton ticks, and starts the next parallel tick. Entity buckets run afterward, while the native settled flag is false. Direct actor-tick pickup would therefore keep declining the required settled gate.

The credit wrapper now invokes `resources.CollectNaturalWater()` immediately after its existing guarded `original.Tick()` returns. This is still the settled singleton phase, before the next parallel tick starts. Each collector then owns its own existing transfer guard. Original credit remains exactly once in the original metered slot; collection does not invoke the GPU, add a request queue or schedule a second native tick. Unsupported/no-source passthrough paths retain their native behavior. The executor publishes its waiting state during actor work and supplies the concrete collector separately.

Native evidence: `/tmp/wildfire-natural-water-bucket-api-review/{REPORT.md,bucket-tick.il.txt,entity-tick-types.txt,tick-types.txt}`. Those installed method reads establish ordering; managed scheduler fixtures explicitly supply their settled precondition and do not simulate parallel completion.

## Validation and limits

Source commit `131ba4c` initially passed 13 new conversion cases and 30 existing ownership cases. Follow-up tests establish the two-phase caller contract, two private buckets sharing one quantum, native Produced callback disable/reservation/owner drift, and same-coordinator/settled/thread refusal without durable taint. An actual native metered singleton fixture observes credit before collection, permits a fresh collector transfer, then runs the later singleton; it would fail on nested guarding or misplaced collection. Final full native suite: **1,742 passed, zero failed/skipped**, zero build warnings, `/tmp/wildfire-natural-fill-full.log`.

The conversion tests call the concrete internal accounting seam under the real coordinator and initialize actual native inventories/counters. Their already-credited native buffers are explicit test inputs via `WaterInput.AddWater`; they do not author a removal receipt or claim natural-fluid production. They execute real native demand queueing, subtraction, Produced events and callback failure. Public null-callback/missing-map refusal is also tested. These tests never fabricate positive Unity liveness.

Independent prior controller proofs establish actual natural removal/credit and native map publication: `/tmp/wildfire-natural-source-real-fluid-run` and `/tmp/wildfire-native-water-map-settled-run`. Their scope remains separate. This source series does **not** prove the full positive public fill wrapper, source-to-actor composition, physical travel, complete adult lifecycle or shipping grade policy. A controller-owned Unity/game check must combine the real source/map and live private equipment with the concrete arrival/phase callback. No engine, UI or deployment was performed for this implementation.
