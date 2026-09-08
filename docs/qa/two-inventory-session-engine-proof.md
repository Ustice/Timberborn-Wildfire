# Two-inventory OWNED4 and native consumption — 2026-09-07

Actual licensed Unity 6000.3.6f1 / Apple M2 Pro Metal proof on immutable production source `dbe2b5fa9237d59664b40c5420a69509e41dee33`. Matching native/Core DLLs were rebuilt from the detached checkout with zero warnings/errors; production shader and all production assemblies remained unchanged between attempts. This is an Editor-hosted native assembly/factory proof, not whole-mod Editor compatibility or a game-world test (the installed player uses Unity 6000.5.5f1).

The first supplied fixture compiled and executed, then failed its native `InventoryStockChanged` assertion: it expected a positive consumed amount. Installed `Inventory.TakeInternal` explicitly negates the amount before emitting that event. The one-line fixture correction changed expected `+1` to `-1`; neither production code nor the positive completed-consumption receipt contract changed. First-attempt source/logs remain under `/tmp/wildfire-two-inventory-session-engine-run/`, including `ATTEMPT-1.md`. Corrected source SHA-256: `4259daacd90cb7730da04498673f5209f516892842ee54dd20da32310b3277c8`; original: `bae6859d2cca71be4a85454b7c3289d9ac366360ac61e0a34571a38c0a7b27aa`.

The corrected attempt passed with exit 0 under `/tmp/wildfire-two-inventory-session-engine-attempt2/`. Native DLL SHA-256 is `848dc078c259684b09de6767d70d518187ed12a45b549a19eaa96d0b1cca445d`, Core `89a5e851b975251d24c1c1a05ab82e58e3defd3858a3ae88da688bf0329c0e0f`, shader `97ddf54667267b7ae34d22eecea3408f6187a959de1ff7c0366b2c5f2af93bd0`.

| Actual boundary | Result |
| --- | --- |
| Native GameObject-backed provider capture | Same building, native Manufactory and SimpleOutput roles, distinct inventories named Factory.Input/Factory.Output, physical Log 3/2, input reservation 1, disabled positive input retained |
| Declaration preflight | Alias, duplicate name and foreign inventory reject before intentionally unreadable stock access |
| Initial formation → native GPU factory → OWNED4 | Same-good material composition has building plus Log, explicit fixture capacity 240, complete snapshot and declarations captured without stock changes |
| Native stock reduced to 2/1 → complete restore | Exact saved GPU state and OWNED4 text preserved, capacity remains 240; desired registry is CompleteStaged; current remaining stock does not rebudget or refill |
| Native inventory callback drift during initial/restore factory | Final provider reread rejects both staged candidates; actual allocated backends disposed; original copied capture remains 3/2 |
| Disabled/unreserved effects admission | Disabled positive input unavailable; enabling exposes exactly one unreserved input and one output |
| Real aggregate consumption using a synthetic damage-4 origin row | Returned Storage.DestroyedItems **2**, HazardousItems 0; native completed withdrawal order Factory.Output then Factory.Input; input reservation 1 survives |
| Reentrant native callbacks | Save, nested mutation, capture and recursive consumer calls reject under the same transaction |
| Next native withdrawal callback throws after stock mutation | Original exception preserved, no returned receipt or completed stock event, shared guard becomes indeterminate and rejects capture/retry; no refund/replay |

Both attempts attempted and completed deterministic cleanup: sessions 2/2, backends 4/4, GameObjects 2/2, errors 0. Corrected log records PASS, then CLEANUP_COMPLETE, then EXIT_AFTER_CLEANUP code=0. No UAV or persistent-allocation notice was found. No game process or deployment overlapped either run.

This fixture supplies ComponentCache/dependency state, finished placement at z31/32, BuildingSpec, native inventory initializer names/stock backing, and native environment arrays. It calls actual native role/inventory initialization, provider getters, TakeConsumed, GoodReserver, production formation/persistence/consumer, and the actual GPU snapshot factory. It does **not** run Inventory.Awake, Manufactory.Awake, full native decorators, recipes, hauling, Inventory.Save/Load, a real colony load, or ordinary shader simulation. The damage rows are explicitly synthetic with valid saved TargetId/SlotId; positive returned receipts, not signed events or net quantities, establish consumption. Paired save equality is asserted before these synthetic effects, with no post-injection save claim. Structure rollback remains reported unavailable. Natural/recovered goods effects, release fuel policy and production OWNED4 activation remain outside this proof.

Artifacts include `original-capture.json`, `initial-snapshot.json`, `owned-save.txt`, `reduced-restore-observation.json`, `native-receipt.json`, exact staged hashes/source, command, process log, and `unity.log`. The original rejected-multiple-inventory checkpoint and prior engine proofs were not overwritten.
