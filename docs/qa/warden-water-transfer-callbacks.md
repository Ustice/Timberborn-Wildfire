# Warden water destination changes during native transfer

2026-09-08. Test-first correction from `2b28d8a`, no engine or gameplay-policy change.

## Reproduced failure

Actual `WardenEquipment.TryReturn` debited its native inventory. The native `InventoryChanged` observer then called `destination.Disable()`. The method nevertheless gave the unit to that disabled inventory and returned true; the shared coordinator remained unpoisoned and `ThrowIfSaveUnsafe` allowed saving.

The two original runs reproduced exactly: `returned=True`, no exception, disabled destination stock1, source stock0, save permitted. Physical water was conserved in the disabled destination. The demonstrated defect was successful transfer after the destination's enabled admission changed, **not** proven water duplication or overfill.

A discriminating control already passed before the fix: filling the destination inside the same callback caused native `GiveExisting` capacity checks to throw and the existing coordinator to become indeterminate. Native `GiveExisting` checks capacity/reserved capacity but does not check Enabled.

## Correction and proof

- `TryReturn` rechecks destination Enabled and unreserved capacity after native source subtraction, before giving water. Drift throws under the existing transfer guard; no false success or refund.
- `TryFill` applies the same destination invariant after source subtraction and refuses an already disabled equipment inventory at preflight. No extra guard, resource ledger or generic transfer framework.
- Original native observer exceptions still escape unchanged through the existing guard. Tests cover exceptions from both source subtraction and destination addition, and retain their exact original cause and post-mutation quantities.

The existing district lifecycle fixture supplies real native inventory initializers, storage/capacity, component cache and native event publishers. Six added cases cover the original failure, full-destination control, ordinary return, pre-disabled refusal, and two native observer failures. No Unity truth predicate is replaced or fabricated.

**TryFill's public positive path remains unexecuted by these managed tests:** native GoodReserver.HasReservedStock checks actual BaseComponent/Unity liveness. Its equivalent native TakeExisting/GiveExisting sequence and added destination check were source-reviewed; this is not a physical station-pickup proof. No actor movement, game UI, actual owner deletion, or arbitrary callback mutation isolation is claimed. In particular, this slice does not define new post-Give policies for every possible observer-driven cargo change.

## Validation

- `31fae33`: preserved RED test checkpoint,1 failed/2 passed in two runs.
- `641e237`: correction and additional controls; focused30 passed,0 skipped.
- Full Release native suite: **1,672 passed**,0 failed,0 skipped.
- Production diff: one preflight condition and two two-line post-debit checks in WardenEquipment.
- Native IL, original/repeated RED, GREEN and full logs plus report: `/tmp/wildfire-warden-transfer-callback-review/`.

A post-debit failure is deliberately indeterminate. The fix neither rolls native changes back nor silently retries. It uses the existing save/dispatch failure boundary; normal native observer behavior and source registration policies are otherwise unchanged.
