# Ash growth observation correction and inactive native rate prototype

`RestoreSnapshot`, transport synchronization, and day decay now update the ash read model without advancing native plants. They preserve fertile eligibility and the existing strength-derived multiplier, capped at 1.10. Their growth-applied/failed counters are zero because observations do not apply growth. Runtime construction no longer injects a native mutator into that service.

The replacement is an **unbound prototype**, not completed fertility gameplay. No configurator, template, worker, or runtime scheduler activates `TimberbornAshGrowthTick`. The wrong observation-driven mutation has been removed; no claim of active elapsed-time fertility replaces it yet.

## Native rate and ownership

The small native `ITickableSingleton, ILateTickable` entry samples an explicit ready observation, prepares candidate references under the existing coordinator's `CaptureAtRest`, then uses that same coordinator's `TransferInventory` for actual native writes and completion bookkeeping. A readiness provider is required; the prototype supplies no default-ready world. Observations contain a copied request list and grid. Prepared plans are internal, synchronous, single-use, and never saved.

The existing `Growable` timer supplies base growth. The extra fraction is:

`clamp(multiplier - 1, 0, .10) * elapsedDays / GrowthTimeInDays`

`elapsedDays` comes from the native clock's `FixedDeltaTimeInHours / 24`, once per native singleton tick. Finite elapsed time and finite positive native duration are required. Zero time does nothing. No clock ledger, load-gap catch-up, spec mutation, timer replacement, new scheduler, or Gatherable yield policy is added.

Actual installed singleton loading orders ordinary tickers before `ILateTickable`; the clock and native TimeTriggerService are ordinary. A native resource already grown, paused, dead, or dying receives no bonus, and the adapter does not force resume. Exact owner identity, initialization/Unity liveness, current cell membership, and unchanged duration are rechecked before application. Candidates deduplicate by exact object reference, preserving the previous first-reference request behavior; no maximum-across-footprint policy was introduced.

The rate samples eligibility at the late tick boundary. A resource resumed within that native tick may receive a tick-sized bonus. This is fixed-step sampling, not continuous-time attribution within a tick. Native explicit TickOnce or completion of an outstanding native tick can advance time; ordinary zero-time frames/read operations cannot independently grant bonus.

`LastApplication` on the unbound ticker is a separate nullable completion result: null before a completed interval, actual counts after successful application. It is not yet routed to Runtime QA, which still reports observation-only counters. Future binding must expose this result rather than interpret the old zero counters as timed-growth success. Native callback failure poisons the existing coordinator; no result or interval may be retried, and save is excluded.

## Evidence and limits

- Parent's two tests reproduced mutation on ash restore and same-tick transport observation: **2 failed** with expected 0, actual 1. The initial removal makes the identical tests pass. The final design removes the mutation dependency entirely and additionally compares real native Growable progress across native Save/Load and repeated ash Restore/Sync/decay.
- A counterfactual fixed .001-per-call fraction fails both actual native duration tests: duration 2 gives .501 instead of .55 after one elapsed day; duration 10 gives .101 instead of .11. The elapsed formula passes. Equal elapsed intervals split into ten parts agree within native float precision.
- **22 native growth cases** exercise installed TimeTrigger/Growable arithmetic, pause/resume, finite validation, native Save/Load, native Growable.Grow→HasGrown callback, native LivingNaturalResource.Die plus its actual pause handler, and an earlier death-subscriber exception. The callback failure leaves native growth complete but poisons the actual existing coordinator, rejects save/reentry, and cannot replay.
- The growth completion fixture supplies a remaining independent native reproduction blocker. It exercises native event dispatch without pretending to instantiate the complete world reproduction/model subscriber graph. Lifecycle components, timers, and native persistence are real installed managed types; backing configuration and caches are supplied explicitly.
- Actual native TickableSingletonService.Load and TickSingletons place the prototype after an actual configured DayNightCycle and TimeTriggerService, even when the repository lists the prototype first. Two native singleton ticks invoke it twice; loading does not invoke it. Parallel scheduling and paused Unity frame behavior are not claimed as engine-tested here.
- Actual native BlockService/Array3D methods distinguish supplied z=1 from z=0/z=2 and a separate upper resource at z=3, and collapse duplicate references to one candidate. Its native owner is deliberately uninitialized: plan application reports unsupported and does not bypass Unity liveness. This proves exact lookup/dedup, **not** that an accepted ash cell necessarily denotes the correct soil surface.
- Read-preparation exceptions stay save-safe. A lifecycle invalidation caught inside the read callback still rejects publication through CaptureAtRest's poison postcheck. The prepared empty plan is single-use and has explicit completion telemetry.

Final source validation: **1,539 native tests passed**, zero failures/skips. The first full run found only one reflection fixture still invoking the old two-argument ash service constructor (12 affected tests); adapting that call preserves its existing dispatch assertions. **228 Core tests passed**, zero failures/skips. No game, engine, shader, deployment, or live fertility execution occurred in this slice.

## Required activation proof

Do not bind the ticker merely because the arithmetic and exact block lookup pass. The next controller-owned CPU/native fixture must use a real planted GameObject/component graph, native BlockService registration, actual native column ceiling c, and the current-world readiness path. It must show that the simulator-accepted clean-ash cell is the actual soil-ceiling cell c, that c−1/c+1 and a stacked upper plant do not gain a soil benefit, and that a tall resource's upper occupied cells cannot qualify merely because they resolve to the same Growable. The current prepared adapter trusts its ready derived requests; the native soil/readiness producer and complete live mapping remain the binding gate.

Run the live prepared plan with actual entity liveness and native timer completion, and verify current-world readiness does not appear during restore. Include callback-driven deletion/movement between candidates, actual completion subscriber failure/save exclusion, and no repeated restore bonus. Do not widen to nearby XY, invent a z offset, or silently select a new multicell bonus policy to make this fixture pass.

Read-only installed-IL report, assembly hashes, source snapshots, and RED/GREEN logs: `/tmp/wildfire-native-ash-growth-rate/`. The native soil authority correction remains unchanged; repeat harvest yield acceleration remains separate from this plant-growth correction.
