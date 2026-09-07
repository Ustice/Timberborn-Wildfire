# Owned step and consequence delivery boundary

Source checkpoint: `9a8c112`, based on `0ed7cb3` plus the test-only retired-material detachment proof. No runtime activation, Core/backend/shader change or new resource state is included.

## API and ownership

Internal `TimberbornOwnedDeltaConsumer.ConsumeStep(Func<GpuFireStepResult?> step)` opens the consumer's existing shared resource guard before invoking the supplied synchronous step. It keeps the same transaction across simulator callbacks/listeners, complete returned-batch identity/family preflight, the single body reducer and all native family effects. It returns a typed pair of the step and consequence receipts only after successful native delivery; null means the step delegate returned no step and no native delivery occurred.

The consumer owns the transaction. There is no caller-created guard, guard token, raw delivery callback, second fault ledger or persisted phase. The existing `_consuming` latch prevents reentrant delivery and remains set through the entire operation. Existing save, capture, registration and retirement gates reject callbacks during that operation. An already-running external resource transaction cannot enter either ConsumeStep or public Consume.

The trusted internal caller must supply the actual synchronous simulator step and validate required protocol receipts before returning it. In particular, an unexpected GPU material rejection must throw before returning a step: the old material may already have simulated. That is a fail-stop boundary, not a valid played world or normal retry. No public arbitrary-delegate capability or runtime installer is added.

Public `Consume(tick, deltas)` retains its existing behavior: whole-batch identity and family validation precede the mutation guard. A rejected input therefore remains read-safe when no simulator step was run. Private PrepareDelivery/ApplyDelivery methods share the same validation and effect implementation between the entries. ConsumeStep performs that preflight inside the guard because simulation may already have mutated the world.

## Failure cutpoints

- Null step: no consequence effects, guard released safely; public delivery remains usable.
- Typed NotApplied from the step invocation only: no effects, guard released safely, the same exception rethrown. Caller must still replan if the native simulator advanced an attempt token.
- Indeterminate or Committed/listener failure from the step: shared guard poisoned; no native batch receipt, save or retry.
- Rejected material receipt: caller throws inside the boundary; no native delivery, shared guard poisoned.
- Unknown/stale returned identity or lost canonical body: whole batch rejected before native effects, but shared guard poisoned because the step may have run.
- Native consequence failure, including an exception incorrectly carrying a NotApplied classification: shared guard poisoned; actual prior mutation is not refunded or replayed. The narrow NotApplied catch does not enclose preflight or effects.

## Executed evidence

`OwnedStepDeliveryTests` runs ten cases alongside the existing ten aggregate cases (20 focused passed). Full native suite: **1,052 passed, zero failed/skipped**. Logs: `/tmp/wildfire-owned-step-delivery-focused.txt` and `/tmp/wildfire-owned-step-delivery-full.txt`.

The integration case executes the actual Core coordinator with the scripted material backend from the detachment proof and the production owned consumer/resource transaction. Old retired A and still-live B each emit an earlier queued delta; a whole handoff archives A and moves B into A's cell. A's exact retained origin counts NotLive, while B takes only its own loss of two once. No replacement-owner routing or duplicate body pass occurs. A real coordinator listener attempts public Consume, Register, Retire and history capture; each rejects while the outer transaction is held. GPU rejection poisons before native delivery.

Additional cases verify save exclusion from preflight/native callbacks, no escaping nested transaction, paired receipts after success, safe null/NotApplied, public preflight compatibility, and actual partial fake-native mutation followed by an exception. The latter proves host guard behavior, not fault injection into the running game.

The assembly grants test-only friend access to exercise the internal typed entry directly rather than using reflection. This exposes an existing native constructor overload during test compilation, so the tests also reference SoilContaminationSystem and MapIndexSystem without copying those assemblies.

## Remaining activation gates

No game, Unity or GPU test ran for this source change. The scripted backend does not implement fire evolution or native entity mutation. Native production routing, environment composition, full ordinary-queue admission, same-session complete capture/restore and actual live play remain separate gates. The older detachment test runner is not production wiring; its raw callback is superseded by consumer-owned ConsumeStep for future composition.
