# Complete declared native inventory discovery

Source checkpoint `a8bd26c` adds an **unwired** native discovery helper. It does not change initial material capture, restore schema, physical role admission, quantities, consequence effects or budgets.

`TimberbornNativeInventoryRoles.Capture(EntityComponent)` returns a copied read-only list of transient `TimberbornDeclaredInventory` bindings. Each contains the immutable public `TimberbornInventoryDeclaration` (native role plus exact `Inventory.ComponentName`) and its native inventory reference. References are only for same-scope validation; future compatibility data uses declarations, never native references.

The declared roles are `Stockpile`, `SimpleOutput`, `GoodStack`, `Manufactory`, `RecoveredGoodStack` and `WardenStation`. Physical snapshots and initial selections now reuse this exact declaration enum and ComponentName; material/effect admission remains a separate explicit gate (see [physical capture proof](declaration-physical-inventory.md)). Discovery enumerates all instances of these actual native components and every native `Inventory` on the body. It rejects:

- Missing or foreign role/inventory references, including native role components whose owning entity differs.
- Duplicate role instances, repeated inventory components, or one inventory claimed by two roles.
- Blank or duplicate component names (ordinal exact comparison).
- An inventory component not claimed by a supported role.

The helper never reads stock, reservations, enabled flags, recipe state or filters. Empty and disabled inventories therefore retain their declared identity. An unknown inventory fails this explicitly complete observation; it is not silently omitted or assigned an invented role. The caller must hold the existing world observation guard, validate the exact entity's settled lifecycle, and revalidate the complete observation before publication. This helper adds no guard or persistence state of its own.

Ten focused native managed cases pass. They use actual native Inventory names and role initializer references, including two native roles aliasing one inventory and a disabled empty harvest GoodStack. A second inventory has no stock registries at all, proving topology validation does not need quantity reads. The cases cover missing/foreign/unclaimed references, duplicate names/roles/components, invalid declaration enum, distinct GoodStack versus Recovered identity, and copied read-only identities after the native name changes. Full native suite: **1,158 passed, zero failed/skipped**. Logs: `/tmp/wildfire-native-inventory-discovery-{focused,full}.txt`.

These tests execute the whole topology validator. They do not claim execution of `Capture`'s Unity liveness checks, complete native prefab component enumeration, production provider wiring, a saved static-role witness, or restored-world activation. No game or Unity process was launched. The next provider observation tranche can call this helper and retain declarations separately from physical stock parts; new-role material/effect admission remains an explicit later change.

## Existing mod inventories outside the supported declaration set

Read-only template/source audit found two dedicated mod inventories:

| Template/role | Native inventory name | Consequence for later observation wiring |
| --- | --- | --- |
| `WildfireWardenStationSpec → WardenStation → Inventory` | `Wildfire.WardenStation` (capacity 20, input-only Water) | The shipping-source station blueprint has `BuildingSpec` and `BlockObjectSpec`, and uses its custom WardenStation component. The follow-up now recognizes this exact declaration role; physical stock material/profile/accounting support is unchanged. |
| `AdultSpec → WardenEquipment → Inventory` | `Wildfire.WardenEquipment` (capacity 1, private Water) | Every adult gets the dedicated equipment inventory, including adults who are not on warden duty. Direct complete inventory discovery of that entity rejects until the equipment role is supported. Current body capture skips entities without `BlockObject`; the installed `Characters/Beaver/BeaverAdult.blueprint.json` has `AdultSpec` and no `BlockObjectSpec`, so this is not the same immediate body-observation gate as the station. |

Evidence: `WildfireConfigurator.WardenTemplateModuleProvider.Get`, `WardenStationInventoryInitializer.Initialize`, `WardenEquipmentInventoryInitializer.Initialize`, source `Data/Buildings/FireResponse/WardenStation.IronTeeth.blueprint.json`, and the installed `Blueprints.zip` adult definition. The other mod ash/borrowed-duty decorators add executors/behaviors; source search found no additional dedicated mod inventory initializer. This is source/template distribution evidence, not a live template-instantiation census.

The WardenStation follow-up appends stable enum value 5 and reads the actual `WardenStation.Inventory` property and native `ComponentName`. It retains the same alias/name/ownership checks. It does not infer SimpleOutput from `IGoodProcessor`, change material/accounting support, or add WardenEquipment to building capture. Private equipment remains explicitly unsupported if direct adult inventory discovery is later required.

## WardenStation declaration follow-up

Two added tests use the actual station component/property with supplied native Inventory. Empty/disabled station inventory keeps its exact supplied name; aliasing it through SimpleOutput rejects. Its exact name and appended role survive OWNED4 codec round trip. The codec case deliberately isolates declaration serialization using the existing synthetic body fixture; it does not claim successful station material formation. The 36 combined discovery/witness cases pass; original enum values remain unchanged. Complete template instantiation and live station capture remain unexecuted here.
