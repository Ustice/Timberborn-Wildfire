# Explicit native owner retirement prototype

This is a callable owned-consumer boundary, not production lifecycle wiring. No event hook, GPU detachment scheduler, intake policy or initial-world activation is added.

## Native presence and deliberate retirement

`ITimberbornOwnedBodyLiveness.ObservePresence(Guid)` distinguishes `Absent`, `Live`, `Uninitialized`, `Deleted` and `InvalidNativeReference`. Only a missing exact EntityRegistry entry is `Absent`; identity mismatch throws before state classification. Ordinary `IsLive` remains true only for a fully initialized, nondeleted, live native object. Dead standing plants, leftovers and disabled buildings are not absent merely because their yield, life or operating state changed.

`TimberbornOwnedDeltaConsumer.RetireNativeOwner(Guid)` requires an already registered canonical owner, consistent body accounting and exact native absence. Preflight uses the existing read exclusion guard; ordinary invalid admission does not poison the session. The mutation uses the same resource guard as consequences/save, removes that owner's desired material projection and BURN registration, and explicitly transitions its existing canonical origin binding to `RetiredNativeOwner`. Repeating the operation on a consistently retired, still-absent owner is a no-op.

The binding's existing persisted retention enum is now explicit authority. Capture checks retention/BURN agreement; losing a BURN entry accidentally cannot manufacture retirement. Strict restore uses `RegisterRetired` for saved tombstones. A retired Guid cannot be registered or receive live body consequences after a direct body upsert. No separate owner registry or new persistence schema is introduced.

Native Guid/TargetId/local-slot bindings, natural actual-yield receipts and terminal/presentation intent, and fractional storage credit remain intact. Delayed old-owner deltas still resolve to that owner and report NotLive. The operation never touches simulator material, creates goods, restores yield or copies fuel from a body definition. Existing GPU archives are outside this method and remain simulator-owned.

A registration observer exception after publication, or same-Guid native reappearance during that callback, poisons the existing shared guard. There is no speculative rollback, refund or retry. Ordinary completed native deletion itself is not a resource failure. Retirement is explicitly rejected inside an active consequence/capture transaction; a future event adapter must defer its hint until the outer operation finishes rather than nesting another guard.

## Exact native deletion evidence

Installed EntityComponent.InternalDelete sets `Deleted` before IDeletableEntity callbacks, unregisters components, then posts EntityDeletedEvent. EntityService.Delete removes the Guid from EntityRegistry only after those calls return, then invokes Unity Destroy. Native callbacks/events can throw before registry removal. SerializedWorldFactory.SaveEntities does not skip Deleted entries, so `!IsLive` cannot certify that native persistence omits an owner.

Three new managed fixtures execute actual EntityComponent.Delete, component-cache lookup, EventBus dispatch, EntityRegistry.RemoveEntity and the new native presence probe. They show:

- Component and deletion-event callbacks both see Deleted while the same registry entry remains.
- Exact absence appears only after actual registry removal.
- Failure in either callback stage propagates the original cause and leaves the entry classified Deleted.

The IDeletableEntity callback is a managed proxy. Successful completion invokes native component deletion and registry removal separately; Unity Destroy and a full GameObject lifecycle are **not** executed. Positive initialized Unity liveness, actual crop harvesting, standing-tree/leftover rendering and stockpile deconstruction remain controller-owned game validation.

## Regression proof

The owned tests cover retained native yield receipts/storage credits, exact old-origin lookup, deliberate two-cell projection removal, idempotence, unknown owner, every non-absent status including an invalid enum, missing BURN rejection, same-Guid resurrection rejection after direct upsert, nested-guard exclusion, and failure/reappearance after body publication without replay.

At `05eb1fb` (with baseline native proxy-context fix `8ec1f14`): focused **25 passed**; full native suite **981 passed**, zero failures/skips. Logs are `/tmp/wildfire-native-origin-retirement/retirement-focused.txt` and `retirement-full.txt`. The companion witnessed-capture/restore work requires exact Absent for retired owners and Live for retained owners; preserve those stricter checks when integrating the one-line RegisterRetired restore change.

## Required activation work remains

- Hook actual completed native removal into this explicit operation outside read-only capture. EntityDeletedEvent is an in-progress hint, not completion acknowledgment; failed or skipped notifications need reconciliation against exact registry presence. Paused-save ordering needs a deliberate lifecycle boundary.
- Detach every still-active GPU slot of retired owners using acknowledged whole-batch material handoff **before ordinary simulation**. Retired owner plus still-active material can be a derived cleanup-required checkpoint; it is not permission to run normal gameplay against ghost material. This prototype installs no such dispatch gate.
- Persist/capture only after native receipt delivery and all relevant consequences finish. Material receipt publication precedes downstream listeners, so a later failure cannot be treated as full native delivery success.
- Preserve tombstones/archives; do not silently reuse tokens, restore old fuel into replacement Guid, auto-retire during Capture or garbage-collect provenance without a proven delivery horizon.
- Keep same-Guid resurrection unsupported. Installed Game uses DummyUndoRegistry, but map-editor/native UndoableEntity can restore a saved Guid. That context needs a separate explicit history policy.

Read-only IL and design investigation are retained under `/tmp/wildfire-native-origin-retirement/`, including `REPORT.md`, `EntityComponent.il.txt`, `EntityService.il.txt`, `EventBus.il.txt`, `SerializedWorldFactory.il.txt`, `Cuttable.il.txt`, and `GameSceneConfigurator.il.txt`.
