# Declaration-driven physical inventory capture

Source checkpoint `48c56f3`; no engine, game, deployment or stock effect activation.

This is the historical capture-only checkpoint. [Constructed inventory material admission](constructed-inventory-material.md) now admits supported constructed inventory sets after exact declaration-driven effects; the broader role and live-engine limits below remain historical evidence boundaries.

`TimberbornInventoryMaterial` and the explicit initial accounting selection now carry the existing immutable `TimberbornInventoryDeclaration`: native role plus exact Inventory.ComponentName. The redundant three-value captured-role enum is removed. Existing native-role numeric values and OWNED4 codecs are unchanged. Copied physical readings include the declaration, Enabled and all positive native Stock, including reservations and disabled positive goods. A dormant disabled empty GoodStack remains declared but has no physical part.

The native provider discovers and validates complete actual role ownership before CaptureBodyFacts reads stock. Those transient bindings supply physical capture, covering every declared role. Initial and retained final rereads rediscover and compare exact inventory references and declarations before reading stock, then compare complete copied body readings and check topology again. No native references are saved. Initial selections and declaration/material consistency now compare full role-and-name equality; selecting a same-role inventory under another name rejects.

This source does **not** admit multiple-inventory material composition or additional inventory effect routes. Both the existing multiple-role guard and explicit supported-role guards remain. Capturing a Manufactory/RecoveredGoodStack/WardenStation inventory is observation, not permission to burn it or incorporate it into first-release fuel accounting. Stored-good quantities, scalar fuel and existing body capacity policy are unchanged.

Validation on installed native macOS assemblies:

- Full native suite: **1,268 passed**, zero failed/skipped.
- Five new native managed cases execute actual Manufactory/SimpleOutput InitializeInventory, production topology validation, native Stock getters and native InventoryChanged callbacks over supplied managed inventory backing state.
- Distinct inventories retain Log3 and Log2 separately; the latter is disabled with one existing stock reservation. Capture preserves physical quantities, reservation ownership and consumption counters. Same-good roles do not become an accidental combined physical row.
- Aliased references, duplicate names and foreign claims reject even when native stock storage is deliberately unreadable. This proves production topology validation does not need stock. The **provider's ordering of that validation before reads is source-reviewed**; the test does not fabricate a live Unity entity or inject a test-only public stock-read hook.
- An actual native InventoryChanged callback changes the other inventory's name and stock. Original copied facts remain unchanged; the production exact binding comparator rejects the changed binding. This is a managed callback/reread seam proof, not full live-provider callback execution.
- Initial selection tests reject wrong names, foreign roles and duplicates; unchanged positive stock with changed declaration no longer compares equal. New-role and multiple-inventory material guards continue to reject. Existing OWNED1–4 codec, disabled stock, complete restore and inventory reservation tests remain green.

A future controller proof must exercise actual live body ownership/component lookup and full provider final revalidation with two native roles. Native storage effect integration, owner-level single burn budget, supported role activation, recovered terminal deletion and full colony support remain separate gates. No consumed receipt, refund, new inventory ledger or policy decision is introduced here.
