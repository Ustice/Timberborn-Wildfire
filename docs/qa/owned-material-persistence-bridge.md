# Paired native material persistence bridge

The native codec can now encode one `WF2` owned-material snapshot. Runtime activation remains deliberately blocked: the production initializer still imports provider enumeration IDs, sanitizes WF1 cells against current definitions, and restores those old buffers in place. A WF2 load fails before that initializer and preserves the exact original string through `TimberbornRuntimePersistence`. Ordinary runtime saves remain WF1 until a coherent owned session exists.

## One save association

`TimberbornOwnedMaterialSnapshot` validates and defensively copies the complete Core `FireSimSnapshot` together with `TimberbornMaterialBindingSnapshot`. Every simulator-known identity, active or archived, must have the exact retained native target/local-slot binding. Additional native slots are retained definitions only; they are not known GPU history and do not authorize fresh fuel. Native Guids, target allocation cursor, transformed local-coordinate/slot bijection, and slot cursors survive. Hidden and removed target origins remain available through `TryResolveOrigin`; sinks must still resolve and validate the current native entity.

WF2 replaces FIRE with exactly one OWNED record; ASH, BEAVER, and Guid-keyed BURN rows retain their current formats. Missing/duplicate OWNED, FIRE in WF2, OWNED in WF1, unsupported schemas, malformed identities, dangling archived bindings, and trailing binary data reject the whole envelope. WF1 remains readable without invented archives or numeric-ID-to-Guid conversion.

OWNED is base64 over explicit little-endian primitives, avoiding reflection/default construction of get-only identity structs. Native schema 1 writes Core version, dimensions/tick/seed/parameters, cells/transport/companion/target/slot arrays, material authority and archives, ordered ordinary pending commands, then native bindings. Arrays carry signed 32-bit lengths; grid arrays must equal checked grid volume before allocation, archives cannot exceed known slots, and all lengths must fit the remaining bytes. Presence bytes must be zero or one. Acknowledged ash/handoff commands cannot be serialized as pending ordinary work. Core validation checks packed fields, finite parameters and the exact active/archive authority partition.

`PrepareRestore(actualSolidTerrainCells, createNewSimulator)` stages a fresh registry and invokes a constructor-only factory with a defensive complete snapshot. It returns the unpublished pair. The factory owns cleanup if construction fails; callers own a successfully returned simulator. The terrain baseline is required, not inferred from source fuel or archives. Capture belongs on the runtime's serialized main-thread save boundary; this class supplies association validation, not a concurrent-world lock.

## Evidence

Focused managed tests cover nonzero active and archived identities, raw material history, native Guid/local-slot bindings including never-activated slots, parameters/seed/tick, every ordinary command field and null-versus-zero semantics, malformed/truncated/oversized lengths, duplicate/trailing records, cross-ledger aliases/missing bindings, defensive copies, and failed staging before publication. An installed-native fixture executes `SingletonSaver.GetSingleton` / `ObjectSaver.Set` and `ObjectLoader.Get` against actual `SerializedWorld`, preserving the single payload and decoding its nonzero archived identity. This proves managed serialization, not GPU restoration, file creation, native world settlement, or gameplay.

The separate Core/backend snapshot work owns complete GPU readback, coordinator authority reconstruction, and new backend creation. This native bridge does not duplicate that implementation.

## Exact activation work remaining

1. Replace provider enumeration IDs with the validated native registry projection; deliberately preserve/refine stockpile exclusion, empty-stockpile allowed goods, positive output goods, and same-entity storage composite semantics. Reconcile a settled projection before publishing the restored pair.
2. Route every consequence by the retained originating owner, including burn damage state and native liveness/type checks. Current-cell fallback is not valid for owned deltas.
3. Connect native initializer/session publication and main-thread save capture to the complete backend factory and the same registry. Remove the explicit WF2 gate only with this evidence.
4. Define authoritative first activation of never-GPU-active local slots. Core Fresh currently rejects any already-known target; hidden new slots cannot be revealed by inventing an archive or registering an arbitrary new slot. Changed same-owner material profiles also cannot silently Fresh/refill or Refresh a different profile.
5. Add coherent mutable terrain baseline handling, including hidden ground underneath occupied cells and unowned air/solid transitions. The prototype registry baseline is currently constructor-only.

Natural ignition and civilian injury activation remain unchanged and gated. No game, Unity, deployment, or live save action occurred for this bridge.
