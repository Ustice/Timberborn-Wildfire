# Complete declared native inventory discovery

Source checkpoint `a8bd26c` adds an **unwired** native discovery helper. It does not change initial material capture, restore schema, physical role admission, quantities, consequence effects or budgets.

`TimberbornNativeInventoryRoles.Capture(EntityComponent)` returns a copied read-only list of transient `TimberbornDeclaredInventory` bindings. Each contains the immutable public `TimberbornInventoryDeclaration` (native role plus exact `Inventory.ComponentName`) and its native inventory reference. References are only for same-scope validation; future compatibility data uses declarations, never native references.

The declared roles are `Stockpile`, `SimpleOutput`, `GoodStack`, `Manufactory` and `RecoveredGoodStack`. This enum is separate from the currently admitted physical material role enum. Discovery enumerates all instances of these actual native components and every native `Inventory` on the body. It rejects:

- Missing or foreign role/inventory references, including native role components whose owning entity differs.
- Duplicate role instances, repeated inventory components, or one inventory claimed by two roles.
- Blank or duplicate component names (ordinal exact comparison).
- An inventory component not claimed by a supported role.

The helper never reads stock, reservations, enabled flags, recipe state or filters. Empty and disabled inventories therefore retain their declared identity. An unknown inventory fails this explicitly complete observation; it is not silently omitted or assigned an invented role. The caller must hold the existing world observation guard, validate the exact entity's settled lifecycle, and revalidate the complete observation before publication. This helper adds no guard or persistence state of its own.

Ten focused native managed cases pass. They use actual native Inventory names and role initializer references, including two native roles aliasing one inventory and a disabled empty harvest GoodStack. A second inventory has no stock registries at all, proving topology validation does not need quantity reads. The cases cover missing/foreign/unclaimed references, duplicate names/roles/components, invalid declaration enum, distinct GoodStack versus Recovered identity, and copied read-only identities after the native name changes. Full native suite: **1,158 passed, zero failed/skipped**. Logs: `/tmp/wildfire-native-inventory-discovery-{focused,full}.txt`.

These tests execute the whole topology validator. They do not claim execution of `Capture`'s Unity liveness checks, complete native prefab component enumeration, production provider wiring, a saved static-role witness, or restored-world activation. No game or Unity process was launched. The next provider observation tranche can call this helper and retain declarations separately from physical stock parts; new-role material/effect admission remains an explicit later change.
