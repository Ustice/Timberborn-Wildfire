# Retired material detachment: executable managed proposal

This is a **test-only** planner and orchestration prototype at `d840214`, based on `094e5d3`. It adds no runtime hooks, WorldSession changes, simulator rules, native material composition policy or production activation. Full native suite: **1006 passed**; focused prototype tests: **15 passed**, zero failures/skips. Logs: `/tmp/wildfire-retired-detachment-proof/{focused,full}.txt`.

## API and authority

`RetiredMaterialDetachmentPrototype.Plan(registry, canonicalRetentions, completeSnapshot, materialSimulator)` returns a whole `FireSimMaterialHandoffBatch`, or null when no retired native owner has an active GPU slot. The retention map is a transient projection of existing canonical history, not another owner registry. The simulator snapshot and registry must be captured coherently under the existing read exclusion guard. The fixture does that; it does not establish a new production snapshot API.

The registry is a **supplied coherent desired projection**. Initial native body/inventory composition and admission of native births remain caller-owned prerequisites. This planner does not make an arbitrary initial descriptor safe to activate. Known material bytes come only from simulator authority; initial values come only from the registry's existing `CreateFirstActivationRequest` for a never-known target/slot pair.

The algorithm:

1. Validate the complete snapshot and its binding association. Resolve every active target to canonical native retention.
2. Seed all active cells belonging to retired owners, including every local footprint slot. Archived retired slots are already inactive and need no work.
3. For each seeded cell, use `Registry.ResolveCell`: empty projection produces Remove; a retained lower owner uses its exact archive if available; an active incoming slot uses RestoreCaptured and recursively includes its source cell so that source is relinquished in the same batch; a never-known slot uses verified first activation.
4. Reject retired/unknown projected owners, a known slot lacking retained authority, duplicate incoming slots or a source that remains at its original cell. The existing batch validates capture single-use and source identity.
5. Derive the next attempt token from the captured simulator cursor. A no-admission result does not consume it; a failed upload may consume an attempt without applying, requiring a recaptured cursor before retry.

The current Remove request carries only a terrain boolean. This prototype can reveal only its existing class-0 nonterrain or class-1 terrain baseline; it cannot represent richer open-soil/water environment classes. Do not approximate those baselines with the boolean. A separate typed environment admission protocol is required before activating such registry composition. Material reveal must preserve current destination heat/water/ash/soil instead of replaying initial environment arrays.

No per-cell fuel estimates, lifecycle phase journal or archive reconstruction are introduced. Whole-footprint detachment preserves old target/slot/Guid bindings and leaves exact GPU outgoing archives in Core. Revealed known material retains its old fuel/history; it does not restart from its declaration.

## Delivery transaction

The test runner `Flush(guard, simulator, batch, consumeRaw)` holds the **existing** NativeResourceTransaction across the material step, its callback/listeners and delivery of the returned delta batch. The receipt callback only records the material receipt. It never declares native output delivery complete. `consumeRaw` must return successfully before Applied is returned and save exclusion ends.

The public `TimberbornOwnedDeltaConsumer.Consume` starts its own guard and cannot be passed directly into the already guarded prototype runner. The subsequent [owned step delivery proof](owned-step-delivery-proof.md) addresses composition by having the consumer own the entire synchronous step and native delivery through internal `ConsumeStep`. It exposes no raw inner-delivery capability. The old runner remains a test-only failure/capacity model; no runtime binding is implemented.

| Outcome | Material/queue implications | Native save and next-step gate |
| --- | --- | --- |
| NoWork | No retired slot remains active; no step dispatched. | Ordinary step may proceed if other prerequisites hold. |
| Whole material-request CapacityBlocked/null | No upload, token attempt, queued-input consumption or simulation. | Retired-active snapshot can remain an explicit cleanup-required checkpoint; ordinary step stays blocked. |
| Typed NotApplied | No external inputs applied, but attempt token may have advanced before failed upload. | Guard remains safe; replan from fresh authority, keep ordinary step blocked. |
| Accepted + raw consumer completes | Exact outgoing archives published, earlier old-owner outputs consumed, all incoming material admitted. | Recompute pending work; normal step only if none remains. |
| Rejected | Earlier ordinary inputs and simulation already ran under unchanged material authority. No raw native delivery is attempted or reported complete. | Reconciliation failure inside the existing guard poisons save and normal steps. No ordinary retry or guessed output replay. |
| Indeterminate | GPU write/readback or host commit may have partly completed. | Existing host guard poisoned; no save, replay or ordinary step. |
| Committed listener failure | Core authority committed, but no successful returned output batch reaches the runner. | Host delivery guard poisoned, even though Core itself permits later steps. No guessed delta replay. |
| Raw consumer failure | Native effects may have partly completed. | Same guard poisoned; no save or replay. |

Unexpected exceptions are conservatively fatal inside the guarded runner; only Core's explicit NotApplied outcome is handled as read-safe. This is a prototype contract, not a promise that every future host validation error needs to poison.

**Important existing protocol limit:** an admitted handoff rejected by the GPU still runs ordinary simulation with unchanged material. A caller-side ordinary-Tick gate cannot prevent that single rejected step. Valid planner preflight must eliminate routine rejections. An unexpected rejection is an invalid played-world boundary, so the prototype throws inside the existing guard and requires reload of a valid snapshot. This is distinct from provably no-step CapacityBlocked and NotApplied; it does not introduce another persistent phase ledger or change the shader protocol.

## Queue-capacity counterexample and verified correction

At `094e5d3`, FireSimStepCoordinator.TryTickWithInputCore calls `_changes.PrepareBatch(_changeCapacity, ...)`, then returns null if that ordinary batch already fills `_changeCapacity`. The handoff marker is one more ordinary 16-byte command in the same ExternalChanges buffer. Native and Unity adapters allocate command storage to the configured capacity; it is not merely an arbitrary host queue limit. Material request/receipt capacity is a separate bound.

The fixed path prepares all pending valid ordinary inputs plus the final material marker. Existing command/delta scratch storage grows before dispatch; ordinary Tick/ash admission budgets, command/receipt/delta layouts and HLSL stay unchanged. With ordinary budget1 and either one or three older commands, the updated prototype proves every older command/delta remains ordered under the original TargetId/SlotId, the marker follows them, exactly one simulation occurs, no queued input remains, and the archive captures the final pre-detach fuel. Native resource delivery stays inside the existing guard. The separate whole-material-request capacity case still proves no-step CapacityBlocked and a blocked normal Tick.

Allocation failure before dispatch remains NotApplied and preserves pending inputs; an attempted preparation consumes its attempt token, so the next snapshot supplies a new token. This fixes the queue deadlock without changing unexpected GPU rejection fail-stop policy. See [capacity implementation and actual native factory proof](material-control-capacity.md).

## What executes in the tests

Real Core coordinator queue admission, attempt tokens, receipt validation, active/archive publication and listener ordering execute against a scripted managed backend. NativeResourceTransaction is also real. The backend scripts a material receipt and an earlier SetFuel delta; it does **not** model fire evolution, native entities or GPU execution.

The 15 cases cover all retired footprint cells, exact archive bytes, fresh lower-owner initialization, active-source closure and source replacement, full/overfull ordinary queue, whole material capacity, old-origin delta delivery under save exclusion, rejected receipt semantics, upload/no-apply token retry, apply/readback/listener/native-consumer failure cutpoints, and refusing a retired desired projection. Native gameplay routing, initial composition, real GPU output parity and actual world save/restore remain separate proof gates.
