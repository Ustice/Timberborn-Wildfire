# OWNED4 static inventory declaration witness

This checkpoint extends the paired OWNED payload with the original owner's complete native inventory declarations: exact native role plus `Inventory.ComponentName`. It does not add quantity, reservation, recipe, filter, enabled-state, material-budget or construction-cost policy fields.

A witness with `InventoryDeclarations == null` has unknown legacy evidence. An explicitly supplied empty list proves that original observation found no inventories. The new witness constructor/capture overload requires a nonnull list and rejects duplicate roles or names. It copies and orders the declarations. `Matches` distinguishes role, exact ordinal name, and unknown versus empty; actual yield quantity and availability remain outside static compatibility.

`OwnedNativeDefinitionSet.WithInventoryDeclarations` explicitly marks complete inventory evidence, including a zero-owner world. It rejects any witness with missing evidence. The older set constructor rejects evidence-bearing witnesses rather than silently discarding their declarations. This explicit set capability determines OWNED4; it cannot be inferred from an empty collection.

OWNED4 requires original witnesses for every canonical owner, including retired owners. `ForOwners` preserves that original set after retirement. The existing guarded retirement operation still removes BURN/body accounting and requires exact native absence; it does not discard the static witness or replace it with current facts. Durable origin bindings and GPU histories are unchanged. Legacy OWNED3 continues its original retained-only witness layout for round-trip fidelity.

OWNED1, OWNED2 and OWNED3 remain decodable and re-encodable without upgrading their format. All three report unavailable complete native compatibility. Today's empty role list cannot prove what an old OWNED3 save originally contained. The codec does not synthesize declarations, recalculate quantities, or rebase history.

## Executed proof

24 focused codec/history cases pass, covering:

- Named and explicitly empty OWNED4 sets, plus zero-owner OWNED3/4 envelopes retaining their distinct capability on exact round trip.
- Legacy unknown versus explicit empty match rejection; illegal evidence mixing/downgrade; copied declarations and exact role/name differences.
- Actual owned-consumer guarded retirement preserving original declarations while deliberately removing BURN, including idempotent retirement and retired-witness omission rejection.
- Invalid enum, duplicate role/name, truncated evidence and version-downgrade payload rejection.
- Existing OWNED1–3 byte-preserving round trips and unchanged body accounting serialization.

Source checkpoint `403998d` full native run: 1,172 passed, one stale legacy test assertion expected `Complete` instead of the new correct `Unavailable`, zero skips. The independently owned observation/restore tranche updates that assertion while adding genuine original OWNED4 formation fixtures; this checkpoint does not upgrade the old fixture merely to make it pass. Focused follow-up logs are `/tmp/wildfire-owned4-focused.txt`; baseline full log `/tmp/wildfire-owned4-full.txt`.

The companion provider/formation/complete-restore change must supply actual captured declarations and require `HasInventoryDeclarations` before native staging. The preexisting material-only restore API remains a diagnostic boundary until that companion change is integrated; the codec capability alone is not a new runtime admission gate. No game, Unity, live inventory save/load, or production owned-world activation was executed here.

Integrated follow-up: complete native declaration observation and initial OWNED4 formation now feed `PrepareCompleteRestore`, which requires original declaration evidence before observing the native world. OWNED1–3 cannot use that complete entry. The renamed body-only `PrepareDiagnosticRestore` preserves the historical OWNED3 proof; it cannot verify OWNED4 roles from body-only observations. This is staged supported-world reconstruction, not ordinary-tick or production activation.
