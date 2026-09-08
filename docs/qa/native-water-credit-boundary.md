# Exact native water-credit ownership boundary

The later [inactive ownership component](native-water-ownership-component.md) implements the reviewed source/installer seam without shipping bindings; this page records the original test-only scheduler investigation.

This is a proposal with **test-only** native lifecycle/scheduler fixtures. No runtime scheduler installer, source entity, source marker or replacement water service is registered. No player-facing intake policy is selected.

## Recommendation

A practical source ownership boundary exists: decorate only the scheduled invocation of the original WaterInputService.Tick. Immediately before its native credit loop, inspect the registered native inputs and latch an ownership-ambiguous marker on each affected Wildfire source if its coordinate has more than one input or its own registration is not exactly once. Then invoke the original native service once. Firefighter filling must refuse a marked source even if the competing input later leaves. The marker persists on the source entity; it contains no water amount or per-worker credit.

This closes the earlier counterexample: a competing input cannot receive a duplicate credit and disappear before ordinary fill-time polling notices it, because the marker is recorded at every actual native input-credit boundary. A source born after one credit boundary is inspected before its first subsequent shared credit. Multiple firefighters continue to debit one native buffer synchronously under the existing resource guard.

The whole source buffer becomes unavailable when ownership becomes ambiguous, including any previously legitimate volume mixed into it. Do not guess which portion was duplicated, refund it to the river or clear the marker when coordinates appear unique again. This conservative loss is simpler than a second volume ledger. Native pending-withdrawal loss across a save remains unchanged.

## Why normal singleton ordering is insufficient

Native TickableSingletonService.Load sorts ITickableSingleton instances using only whether they implement ILateTickable. Stable ordering within those groups depends on repository enumeration. A normal or late observer is not guaranteed to be adjacent to WaterInputService; another singleton could create/remove/change an input between credit and observation. Bindito provisioning listeners observe construction, not every credit. WaterInputService.RegisterWaterInput has no coordinate lease/event and merely adds to a List; a registration hook would also need pipe-coordinate changes and active-state/load handling.

The proposed compatibility seam keeps the native service instance and scheduler slot. TickableSingletonService holds `_tickableSingletons`, an ImmutableArray of its private readonly MeteredSingleton structs. Each struct holds `_tickableSingleton`, `_metric` and `_metricsEnabled`. Rebuild only the exact original WaterInputService element using its existing constructor and a typed ITickableSingleton wrapper, preserving metric, metrics-enabled flag and position. Do not reorder other entries or rebind WaterInputService in dependency injection.

Native SingletonLifecycleService.LoadAll executes LoadSingletons, LoadNonSingletons, PostLoadSingletons, then PostLoadNonSingletons. WorldEntitiesLoader.LoadNonSingletons instantiates entities and calls EntitiesLoader.LoadAndInitialize; its later PostLoadNonSingletons calls EntitiesLoader.PostLoad. Thus an IPostLoadableSingleton installer runs after scheduler construction and entity load/initialization, but before entity post-load callbacks. Installation should only establish the invocation boundary; inspect source registrations at each actual credit tick after the complete load lifecycle, rather than treating the installer-time input list as final. The wrapper is not separately registered as a tickable singleton. The installer requires exactly one expected original service or the exact wrapper already installed by that same installer; missing, duplicate or foreign decorated targets are unsupported and cannot be guessed through. A new native Load rebuilds the list, after which installation occurs once again.

Before runtime implementation, pin reviewed TickSystem, SingletonSystem, WorldPersistence, WaterBuildings and WaterSystem binaries plus exact scheduler field/entry/constructor shape. Fail natural-water capability before admission if unsupported. Do not silently fall back to an ordinary ticking observer.

## Credit-loop callback proof and limits

The installed native WaterInputService.Tick loops its registered List, reads WaterInput.Coordinates, obtains WaterChangeService.GetWaterChangeUnsafe and calls WaterInput.AddWater. Fixed and pipe input coordinate getters only return their backing Vector3Int field. The receipt read is a dictionary lookup; AddWater is two float additions/assignments. There are no native events, user callbacks, coordinate updates or entity lifecycle changes inside this reviewed loop. The singleton scheduler finishes parallel work before ticking these consumers, so the worker is not changing that dictionary concurrently during the credit loop.

Consequently an immediately preceding observer sees the same native list/coordinates used to credit inputs, before subsequent singleton/entity production or lifecycle callbacks can hide a collision. The fixture also attaches a throwing native pipe CoordinatesChanged delegate: reading coordinates during actual credit does not invoke it.

This guarantee is for reviewed native coordinate-provider/service implementations and the scheduled call path. An external mod replacing those implementations, directly calling WaterInputService.Tick or mutating scheduler targets requires explicit compatibility handling. An unexpected observer failure must prevent native credit and source admission; it cannot be caught and treated as successful observation. Production source tracking should use a typed ambiguity result, not a broad exception catch. If a failure occurs while persisting/marking ownership, use the existing shared unsafe-save boundary rather than letting a partial marker update become trusted.

Source lookup must identify exact native source/Guid and reference. A newly introduced or restored source whose prior credit ownership cannot be proven must not adopt an existing positive buffer as trusted. The runtime marker still needs its native entity component registration and strict saved-state contract; the fixture tests the native bool serialization primitive, not an implemented source component or world-save integration.

## Managed evidence

Eight new tests use the real native SingletonLifecycleService.LoadAll, TickableSingletonService.Load/TickSingletons, MeteredSingleton.Tick, WaterInputService and native buffers:

- PostLoad installs after native scheduler construction; observer runs before credit and a later singleton callback.
- Original metric, flag, scheduler position and service reference are preserved. Exact repeated installation is idempotent; native reload reconstructs an unwrapped service and installs once.
- Missing/foreign/duplicate water targets reject without changing the scheduler list.
- Source collision is marked before actual credit; competitor removal cannot clear it. Native EntitySaver/EntityLoader bool round-trip preserves the marker, and the proposed admission check rejects its bucket.
- Observer failure stops credit and later consumers. A later consumer exception cannot erase an already recorded marker.
- A source introduced after one credit boundary is marked before its next shared credit.
- Native pipe coordinate reads remain callback-free during credit.

The wrapper, metric endpoints and lifecycle repository are managed test proxies; native scheduler/lifecycle/resource methods are real. The fixture supplies a completed water receipt, does not run fluid work, and does not launch Unity/game or execute production source/player flows. Exact managed/source proof is not live game integration proof.

Validation at commit `3e75e2e`: focused `NativeWaterCreditSchedulerTests` **8 passed**, full native suite **926 passed**, zero failures or skips. Logs: `/tmp/wildfire-shoreline-proof/scheduler-focused.txt` and `/tmp/wildfire-shoreline-proof/scheduler-full.txt`.

## Installed evidence

Local files under /tmp/wildfire-shoreline-proof: singleton-lifecycle.il.txt, singleton-tick.il.txt, metered-singleton.il.txt, tick-order.il.txt, world-entities-loader.il.txt, fixed-coordinates.il.txt and pipe-coordinate-getter.il.txt. Related resource/save limits are in [native-shoreline-water-proof.md](native-shoreline-water-proof.md).

Reviewed hashes for a future runtime compatibility gate:

- Timberborn.TickSystem.dll: `5df66413408a54fc616343668600f114fd33056fafe0bf4b2465be084c55cb9d`
- Timberborn.SingletonSystem.dll: `231e0464941fc95f44809302c4f4b2e87b61904e1bb16ff937684011582b8aab`
- Timberborn.WorldPersistence.dll: `831546b1f90af169d6d85076ca26bd885574daf56e0ad27f3b90bc5b1fdb8bb0`
- Timberborn.WaterBuildings.dll: `f006ae2661e3c3682cb55cba4ee75c58f1ebff7c9028edc775c823490c9106f3`
- Timberborn.WaterSystem.dll: `0680fd336e1d33ac7107b8a4f7d5e47f0535cd0d998c6654086ade44d8bebe20`
