# Native gatherable yield load correction

The installed native template factory puts the dedicated named Yielder before GatherableYieldGrower. GatherableSpec itself implements IYielderDecorable; the earlier inference that the yielder decorates Gatherable after its grower was incorrect. Actual native Gathering and Yielding template modules reproduce the order.

Yielder.Load restores its named saved GoodAmount without events. A ripe GatherableYieldGrower then loads progress1, calls TimeTrigger.Finish and Yielder.ResetYield, and replaces that restored quantity with the original full yield. A native managed reproduction saves3 of initial5 and observes5 after the grower loads. This affects repeat-yield plants; annual Cuttable crops have a different lifecycle.

## Correction and native boundary

This correction is production-registered for **all Gatherable entities in the Game context**, independently of wildfire settings or partial-yield activation. It is not a gated prototype action. Native Bindito automatic configurator discovery scans loaded assembly types for nonabstract IConfigurator implementations and matches Context("Game"); internal visibility does not exclude this configurator. MapEditor is not registered.

TimberbornGatherableYieldLoadCorrection decorates Gatherable. Its Load retains only the original native IEntityLoader. PreInitializeEntity clears that reference, validates the exact Gatherable.Yielder/spec/name, and calls that yielder's native Load again. There is no saved quantity, fire-loss replay, alternate loader or lifecycle counter. New entities do nothing; absent named saved yield leaves native initialization intact. Failure clears the loader and propagates, so it cannot become a later implicit retry.

Native EntitiesLoader completes all entity loads and batch loads before PreInitialize, Initialize and PostInitialize. The installed NaturalResourceModel subscribes to YieldAdded only in PostInitialize; its model selection then sees the corrected quantity. YielderRemover reconciles loaded reservations against yield in PostInitialize as well. The correction preserves actual native reservation and Enabled state and leaves the ripe grower's finished timer unchanged.

The native reset still happens during Load. This correction does **not** suppress its event. The installed native method scan found NaturalResourceModel as the sole YieldAdded subscriber; its later subscription makes the correction precede known native observers. An external mod or future Wildfire generation listener that subscribes earlier must not treat loading as gameplay or mutate goods from that event. Unexpected native reset callback exceptions propagate and abort loading; the correction does not catch them or invent a successful load. This is not a claim of compatibility with arbitrary third-party early subscribers.

The template module verifies the reviewed Yielding, Gathering, WorldPersistence, NaturalResourcesModelSystem, TimeSystem, TemplateInstantiation, EntitySystem and BaseComponentSystem assembly hashes before affected entities are created. This covers native decorator ordering, cached component enumeration and entity stage dispatch as well as resource methods. It refuses an unreviewed native build. There is no Harmony dependency or private quantity write in this correction.

## Verification

Eight focused actual-native managed tests pass:

- Native template modules produce Yielder-before-grower ordering and include the correction decorator.
- Actual native Bindito discovery predicate admits the correction configurator for Game, not MapEditor; whole AppDomain discovery is installed-IL evidence because test-host assembly contexts deliberately lack globally resolved game references.
- Native EntitySaver/EntityLoader and GoodAmount serializers, followed by native grower Load, reproduce3→5; correction restores3, preserves initial5/ripe progress1 and emits no second event. A later call does not reapply old saved data.
- New entity and missing native saved-yielder paths retain initialization.
- Native reservation and disabled state remain intact.
- Mismatched named reference and native saved-data read failure propagate and release the loader.
- A throwing reset observer leaves native reset quantity/timer mutated and the original exception propagating; no success is fabricated.

The fixture invokes the relevant real native methods and serializers without Unity objects. Native EntitiesLoader stage order, model subscription timing and YielderRemover reconciliation timing are installed-IL evidence, not execution of the entire scene initialization, renderer or worker reconciliation. The fixture cannot establish whole-world save/reload or renderer behavior; copied-save game QA remains required.

## Remaining partial/depletion dependencies

Public tree/crop partial-yield actions remain gated on durable request-generation authority and final-unit transitions. This native load correction neither enables them nor infers a generation from load-time YieldAdded.

An isolated actual TimeTrigger/GatherableYieldGrower experiment confirms setting ripe yield to0 alone leaves the finished timer unable to resume. Calling native private RestartGrowth after0 resets and resumes an empty repeat-yield cycle; halfway through it remains0 and completion restores the original5. That is a promising repeat-yield depletion seam, not production activation or proof for annual Cuttable crops and wood. Annual removal and tree terminal/leftover semantics still require separate exact-owner stock/reservation checks.
