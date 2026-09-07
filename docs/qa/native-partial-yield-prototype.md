# Native positive-partial yield prototype

The compatibility helper and exact native adapter entry are implemented but **not activated**. Public tree/crop ReduceYield still returns Unavailable until durable request lifetime, yield-generation and save authority are integrated. There is no process flag, alternate quantity ledger or local epoch.

`TimberbornPartialYieldLoss` writes only the installed native `Yielder._yield` GoodAmount after checking its named component/resource, positive request, native reservation and Enabled state. It preserves `_initialYield`, native grower state, GoodStack and reservations. No YieldDecreased/Gathered/WasCut event is fabricated. The exact tree/crop internal entry resolves the current original Guid/family, selects the component by the native Cuttable/Gatherable yielder name and resource, and rejects a mismatched or ambiguous reference. Annual crops such as installed Carrot use Cuttable; repeat-yield plants can use Gatherable. The crop entry supports either exact native form. The caller must hold the existing shared outer resource guard.

Compatibility is reviewed only for the six native assembly hashes embedded in the helper: Yielding, Gathering, Cutting, Goods, ReservableSystem and BaseComponentSystem. Both on-disk fingerprint and private field type/mutability are checked before mutation. Future live activation must call Verify at capability initialization, before advertising partial-yield support; a first-fire version failure is not acceptable capability discovery.

Reserved, inactive, missing-capability and no-yield outcomes return no removed quantity. Requests that would reach or cross zero return **DepletionRequired with zero removed**. They do not silently leave one unit after a partial application. The depletion/growth transition remains the next bounded native lifecycle proof, not permanent immunity for the final unit.

Tree results now carry an actual YieldLost receipt. The sink increments its applied ledger by that amount, rejects negative/over-request/contradictory/unknown-status receipts, and never infers yield loss from terminal or visual success. Existing successful fake APIs explicitly report the loss they model.

## Evidence

Twenty new cases plus the complete native suite pass: **857 tests, zero failures/skips** on this branch.

- Actual helper5→3, zero harvest/yield-added callbacks, initial capacity5.
- Actual Yielder.Save/Load using native EntitySaver/EntityLoader/SerializedEntity and GoodAmount serializers restores3; next native ResetYield returns5. This is native in-memory serialization, not a world save file or live-game reload.
- Actual native GoodCarrier.PutGoodsInHands → Yielder.DecreaseYield → installed Gatherable harvest handler carries remaining3, leaves yield0, emits Gathered once and does not enable a leftover stack. This exercises the real native methods in their native completion order, not full YielderRemover navigation/reservation lifecycle.
- Reserved/inactive/depletion requests preserve quantity, initial capacity, Enabled and reservation; mismatched name/resource/version reject; multiple named yielders cannot redirect loss or resolve ambiguously.
- Exact adapter entry rejects absent identity and skips the missing original; positive Unity entity resolution remains game QA.
- A deliberate caller failure after the real field write poisons the same NativeResourceTransaction and blocks save/replay. The test does not claim a native quantity callback, because this helper intentionally emits none.
- Partial fake receipt requests [1,2,1] with actual [0,1,1], proving only the remaining deficit is retried. Terminal/visual results do not invent yield.

## Activation dependencies

A native owner Guid survives harvest/regrowth. Cumulative damage/applied-loss state alone cannot identify a new named yield generation or retire old unavailable requests. Native YieldAdded follows ResetYield, but earlier observers can throw after the native value changes and before a later Wildfire listener. Activation therefore depends on the shared durable lifecycle/history design; resetting a Guid ledger to zero or relying on an unverified event-order assumption is insufficient.

The actual Yielder-only saver/loader fixture does **not** prove final whole-crop quantity after every native component loads. GatherableYieldGrower saves ripe progress1; its Load → FastForwardGrowth(1) → TimeTrigger.Finish can invoke Yielder.ResetYield. Native named-yielder versus grower component load ordering must be verified before activation, and load-time YieldAdded must not be mistaken for a new gameplay generation.

Zero depletion also needs explicit native growth behavior. Changing quantity to zero alone can leave a finished crop timer stalled until reload. Raising Gathered as a substitute falsely reports harvest and runs unrelated consumers. The next audit must prove a narrow native grower restart/depletion operation, its exact reserved/disabled/dead eligibility, and its callback/save boundaries before wiring it into production.

No Core/GPU behavior, generation protocol, game, Unity or deployment changed in this slice.
