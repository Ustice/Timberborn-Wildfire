# Warden public-input native contract

The 2026-09-07 live `b21f118` no-fire run placed and connected a station, then failed during another worker's native empty-output job. The recorded entity was BeaverAdult Aradya (`426f6d58-2ee1-4eb1-9b7a-35ffeef8dea4`), not evidence of an assigned Warden sortie. The preserved log is `/tmp/wildfire-warden-doorstep-live/Player-staffing-failure.log`.

`DistrictInventoryPicker.InventoryIsTaking` fails at native IL `000f`: it obtains `IInventoryValidator` at `000a`, then dereferences `ValidInventory`. Its next required component is `BlockableObject` (`0017` / `001c`). The installed `Timberborn.InventorySystem` module ID in the stack is `7e19aa807b8344d48ae1182daf30a50d`.

The station's dedicated inventory initializer deliberately creates a public input. It entered the native capacity index without the owner-specific `Emptiable` decorator, the installed native implementation of `IInventoryValidator`. Native public-input owners such as FireworkLauncher and GoodConsumingBuilding supply `Emptiable`, `EmptyInventoriesWorkplaceBehavior`, and `RemoveUnwantedStockWorkplaceBehavior`. The latter two are required by the hauling providers added automatically for `Emptiable`. The fix gives WardenStation these same three decorators. Existing `BuildingSpec` already supplies `BlockableObject` and `StatusSubject`; no new blueprint spec or validator override is required.

The private WardenEquipment inventory retains neither public flag. Native `DistrictInventoryRegistry.Add` still emits registration events for it, but does not add it to the public inventory registry. It therefore cannot be returned by `ActiveInventoriesWithCapacity`. No inventory is hidden, force-filled, or made immune to blocking to avoid this crash.

## Executed proof

`NativeWardenPublicInventoryTests` executes actual native template decoration, both mod inventory initializers, component-cache lookup, district registration/capacity indexing, and the exact private native picker predicate over supplied managed component state.

- Before the fix: the missing-native-decorator assertion fails; the counterexample independently reaches the capacity set then reproduces `NullReferenceException` in the picker.
- After the fix: normal station input is admitted; native marked-for-emptying and blocked states reject it; reserving all 20 units removes capacity; no stock is created.
- The complete template contains the native validator and both required workplace behaviors exactly once. Actual hauling-provider `Awake` resolves those behaviors.
- The private equipment initializer remains excluded from the public capacity set.

Two focused tests pass. Full native suite: 1,312 passed, zero failed/skipped on the final source. Logs and native IL evidence are under `/tmp/wildfire-warden-staffing-audit`.

This is native managed contract proof, not a new game run, navigation test, complete native lifecycle proof, or successful live staffing claim. The live exception contains no destination inventory identity; the station is the newly introduced matching invalid public input, and the managed counterexample proves this concrete defect. The controller must repeat connected no-fire staffing/hauling on the corrected build before claiming the live failure resolved.
