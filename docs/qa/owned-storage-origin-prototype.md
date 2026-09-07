# Owned storage origin and consumption prototype

This bounded route is implemented but not production-bound. Current WF1 production still uses the explicit legacy spatial adapter. Owned storage never calls that adapter, parses runtime hash IDs, scans overlaps, or retains an Inventory reference between mutations.

## Native ownership and accounting

`TimberbornOwnedStorageRegistration` requires a native Guid plus explicit Stockpile or SimpleOutput role. It derives the canonical Stockpile/Structure body damage key; conflicting families for one Guid reject. `TimberbornOwnedStorageInventoryApi` resolves EntityRegistry.GetEntity immediately before each read/removal, checks initialized/nondeleted native liveness and the exact Guid, selects only the designated component on that entity, and verifies that its Inventory belongs to the same EntityComponent. Missing roles/inactive inventory are unavailable; disappeared entities are not live; no foreign replacement is substituted.

Removal is bounded by current `UnreservedTakeableStock` and uses existing `TimberbornInventoryMutations.Consume` / native `TakeConsumed`. Reservations remain native. Each successful removal returns the actual quantity. A callback exception after stock mutation propagates without a fabricated receipt or refund.

`TimberbornOwnedStorageDeltaConsumer` shares `TimberbornOwnedBurnOrigins` with trees: whole-batch TargetId→retained Guid→canonical family/key preflight precedes effects. The exact damage reducer sums distinct burned cells and writes each owner once; the raw storage sink still uses its separate maximum-per-owner budget until its aggregation is corrected. The separate storage sink rechecks each native target and consumes each planned stack before any hazard effect. Hazard amounts come exclusively from successful actual removal receipts; the original delta cell remains the intentional blast/contamination location. Earlier callbacks can delete a later target; its replacement remains untouched.

The entire body-damage/storage/hazard operation uses `INativeResourceMutationGuard`, implemented by the existing coordinator and transaction. Runtime integration MUST supply the same `NativeResourceCoordinator` that protects world Save. No independent inventory transaction instance or fault ledger is created. Save/reentry during delivery is rejected; any inventory or hazard exception after mutation poisons the shared session and prevents replay or unsafe save. The interface only separates access to the existing guard from worker/native assembly coupling in tests.

## Evidence and limits

Four installed-native managed fixtures execute exact missing/deleted/uninitialized entity lookup and actual inventory/reserver/stock events. Native `TakeInternal` subtracts stock before invoking stock-change events; `TakeConsumed` supplies Consumed category. Tests prove only the unreserved survivor can burn, reserved stock and reserver ownership remain, and a throwing post-decrement subscriber leaves mutation observable while the shared guard rejects save/retry. Positive Unity liveness and component ownership still require controller QA; no GameObject was manufactured to claim that proof.

Fourteen managed routing scenarios cover old A/new B deltas at one cell, distinct-cell body damage, hidden A, deleted A, late unknown/unregistered rows, zero origin, between-stack deletion, quantities shrinking between plan/removal, hazard ordering/location, native/hazard callback failure, save during delivery, conflicting roles, and fractional credit limitations. Both storage routes share `TimberbornStoredGoodFuelBudget`. A confirmed preexisting double-spend was corrected: fractional hazard credit had been reserved while the same input budget remained available to ordinary goods. The selector now returns genuinely unallocated budget, so three one-band losses fund a cost-three hazard without also destroying a cost-two Log. Separate owned and legacy regressions cover this conservation boundary. Legacy adapter extraction changes organization only. Final full native validation is recorded with the source handoff.

## Activation gaps

- Per-owner/resource fractional fuel credit remains transient, preserving the existing policy. A test explicitly demonstrates that reconstruction loses pending credit. No complete saved quantity/history claim is made; add coherent credit persistence/policy before live activation. Stock definitions and GPU fuel are not a CPU remaining-fuel ledger.
- Restore the correct canonical live/tombstone consequence registrations with owned material bindings. A new wrapper with no family registration for a retained old Guid explicitly rejects that origin; it never infers a replacement from the current cell. The current native binding snapshot carries identity/local slots, not every consequence registration.
- A mixed-family coordinator must resolve once, apply shared body damage once, and dispatch resolved subsets under the shared guard. Do not chain these standalone tree/storage wrappers or separately damage a structure's same-entity inventory body.
- Complete provider/material lifecycle, native live component checks, save/reload, and exact-origin family routing require integration proof. Natural ignition/injury gates remain unchanged.

No game, Unity, deploy, or live save action was performed for this work.
