# Reserved Warden water return

The new `WardenEquipment.TryReturn(Inventory destination, GoodReserver reserver, Action commitReturned)` overload returns one existing native Water through a fixed capacity reservation. Existing one-argument Warden return, fill, consume and initialization policies are unchanged. The caller owns physical arrival, district/access and exact current return behavior; it revalidates that ownership in the callback before clearing its phase/binding.

The overload requires the exact dedicated private Water-only inventory (name, capacity 1, input/output role), the registration's original inventory, live initialized equipment/actor/destination identities and same-owner reserver. It accepts only an exact fixed nonconsuming Water1 capacity reservation with no stock reservation. Inside one existing resource guard it releases that reservation, checks callback state, debits with TakeExisting, revalidates native ownership before credit, gives with GiveExisting and validates stock and caller commit. Callback exceptions preserve their cause and poison the existing coordinator; no refund or retry is attempted. Native existing-good movement changes neither production nor consumption statistics.

A temporary native capacity-event observer checks the original reservation, expected reserved-capacity debit and exactly one event before GoodReserver clears its record; it always unsubscribes in `finally` through `using`. This guards an IL-supported overwrite risk: native GoodReserver calls Inventory.UnreserveCapacity (which invokes callbacks), then unconditionally clears its own record. A nested replacement could otherwise disappear from the subsequent raw-record check. **Reentrant GoodReserver callback reproduction remains unproved**, not a confirmed live bug. The positive ordinary public release now has the component proof below. The observer's managed tests execute actual Inventory events and explicitly supply the outer record clear, without fabricating Unity object truth.

Validation on the isolated `71a13cd` base: first 13 contract tests failed against the missing overload/stock seam (`/tmp/wildfire-warden-reserved-return-red.log`), then passed. Additional tests cover native topology refusal, fixed/foreign reservations, reserved/full/disabled/mixed stock, actual debit/credit event exceptions and mutation, phase failure, reservation replacement, repeated release events and observer removal. Final full native suite: **1,701 passed, 0 failed, 0 skipped**, `/tmp/wildfire-warden-reserved-return-full.log`. These managed tests exercise public malformed-reservation/null-callback paths and internal native stock/event boundaries. They do not establish positive Unity object liveness or physical arrival.

Caller phase/access drift that leaves native equipment ownership intact can be detected by the final callback after native credit. That outcome is indeterminate under the same guard, with goods left in their actual native location; it is never reported as successful recovery. No additional pre-credit caller callback or broad native transaction framework was introduced. The positive public transfer boundary is now proved below; composition with the recovery caller and physical arrival remains open.

Integrated at `cc614ab`; the root Release native suite also passes **1,701 tests, 0 failed, 0 skipped**, `/tmp/wildfire-integrated-reserved-return-native.log`.

## Actual public component proof

One CPU-only licensed Unity 6000.3.6f1 run passed on exact `793dfcba80c4b43f28aeae9f23bcfcff1e173f45`, with Null graphics. Actual GameObjects/native component truth, the private Warden initializer, `GoodReserver.ReserveCapacity` and the public three-argument `TryReturn` executed in two fresh graphs:

| Case | Return | Source Water | Destination Water | Capacity reservation | Phase commits |
|---|---|---|---|---|---|
| Positive | true | 1 → 0 | 0 → 1 | 1 → 0, record cleared | exactly 1 inside the resource guard |
| Disabled destination | false | remains 1 | remains 0 | remains 1, record retained | 0 |

Both coordinators remained healthy. The positive callback observed the completed native transfer and reservation release while saving was excluded; an empty second return refused without another phase. Post-return capture was allowed and conserved one unit. The disabled case subsequently released its retained reservation through the real native method before teardown. Cleanup completed equipment **2/2**, GameObjects **4/4**, errors **0**, before exit **0**, with no timeout or runtime exception.

The fixture explicitly supplies Entity Initialized, Character Alive, caller phase and arrival. This proves neither full adult initialization, district membership, physical travel, the borrowed recovery caller, nor full savegame persistence. Its post-return capture checks coordinator save admission; it does not execute EntitySaver. The adversarial public reentrant-capacity case also remains unproved.

Evidence: `/tmp/wildfire-warden-reserved-return-engine-run/{REPORT.md,command.json,positive-return.json,disabled-refusal.json,unity.log,outcome.json,prepared-hashes.json,final-input-verification.json,artifact-hashes.json}`. All eight prepared inputs remained byte-identical after execution. Exact hashes:

- Probe: `b4d680ecfffe5a22fa734b57ae4d91a307c39dd7a700ba9e4ceb1ba83c3fb44d`.
- Native DLL: `0bdab7dcd19e6e82772d4dd6d28e1d5d0d09dd2ae966b372bdfee1aa7964c591`.
- Matching Core DLL: `6e46a036ed056982764188cb549c9a7c96c63a484e7cfb170335993345abfc2c`.

The executed pin predates the equivalent explicit null check in `7e298e5`; it is not reported as an engine run of that later source. The original native build warning and unrelated licensing/cloud/shutdown notices remain in the logs. No game, deployment or GPU test ran.
